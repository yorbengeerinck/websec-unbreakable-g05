using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Unbreakable.Auth;
using Unbreakable.Data;
using Unbreakable.Models;

namespace Unbreakable.Api;

// Wat een client mag meesturen. Alles anders (afzender, id, onderwerp, sub's, tijd) bepaalt de server,
// dus mass assignment is onmogelijk.
public sealed record CreateMessageRequest(string Recipient, int? KeyLine, string Body, string Mac);

// PEP: alle autorisatiebeslissingen lopen via IMessageAuthorizer (OpenFGA). Hier staat bewust geen
// enkele vergelijking van een sub met SenderSub/RecipientSub (zie test T36).
public static class OidcApi
{
    private const string Subject = "Versleuteld bericht";

    public static void MapOidcApi(this WebApplication app)
    {
        var api = app.MapGroup("/api").RequireAuthorization();

        // Elke ingelogde gebruiker staat in de tabel Users (nodig om een ontvanger op naam te vinden).
        api.AddEndpointFilter(async (context, next) =>
        {
            var http = context.HttpContext;
            var sub = Sub(http);
            var username = Username(http);
            if (string.IsNullOrWhiteSpace(sub) || string.IsNullOrWhiteSpace(username) || username.Length > 40)
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var directory = http.RequestServices.GetRequiredService<UserDirectory>();
            var db = http.RequestServices.GetRequiredService<UnbreakableContext>();
            await directory.EnsureAsync(db, sub, username, http.RequestAborted);
            return await next(context);
        });

        api.MapGet("/me", (HttpContext http) =>
        {
            long? exp = long.TryParse(http.User.FindFirst("exp")?.Value, out var value) ? value : null;
            return Results.Ok(new { sub = Sub(http), preferred_username = Username(http), exp });
        });

        // Enkel gebruikersnamen (geen sub, geen e-mail): directorydata voor de keuzelijst.
        api.MapGet("/users", async (UnbreakableContext db, CancellationToken ct) =>
            Results.Ok(await db.Users.OrderBy(u => u.Username).Select(u => u.Username).ToListAsync(ct)));

        api.MapGet("/messages", async (HttpContext http, UnbreakableContext db, IMessageAuthorizer pdp,
            ILogger<Program> log, CancellationToken ct) =>
        {
            var ids = await pdp.ReadableMessageIdsAsync(Sub(http), ct);
            Audit(log, "list", Sub(http), null, "allow", ids.Count);

            var inbox = Readable(db, ids);
            var from = http.Request.Query["from"].ToString();
            if (!string.IsNullOrEmpty(from))
            {
                inbox = inbox.Where(m => m.Sender == from);
            }

            return Results.Ok(await inbox.OrderByDescending(m => m.SentAt).ToListAsync(ct));
        });

        // De body is ciphertext, dus de server zoekt enkel op metadata (afzender, tijd), binnen de set
        // die de PDP teruggeeft. De zichtbaarheid komt altijd van OpenFGA.
        api.MapGet("/messages/search", async (HttpContext http, UnbreakableContext db, IMessageAuthorizer pdp,
            ILogger<Program> log, CancellationToken ct) =>
        {
            var q = http.Request.Query["q"].ToString().Trim();
            if (q.Length > 100)
            {
                return Results.BadRequest("Zoekterm te lang (max 100 tekens).");
            }

            var ids = await pdp.ReadableMessageIdsAsync(Sub(http), ct);
            Audit(log, "search", Sub(http), null, "allow", ids.Count);

            var inbox = Readable(db, ids);
            if (q.Length > 0)
            {
                var pattern = "%" + ApiHelpers.EscapeLike(q) + "%";
                inbox = inbox.Where(m => EF.Functions.ILike(m.Sender, pattern, "\\"));
            }

            var from = http.Request.Query["from"].ToString();
            if (!string.IsNullOrEmpty(from))
            {
                inbox = inbox.Where(m => m.Sender == from);
            }

            if (DateTime.TryParse(http.Request.Query["after"].ToString(), null, System.Globalization.DateTimeStyles.RoundtripKind, out var after))
            {
                var afterUtc = after.ToUniversalTime();
                inbox = inbox.Where(m => m.SentAt >= afterUtc);
            }

            if (DateTime.TryParse(http.Request.Query["before"].ToString(), null, System.Globalization.DateTimeStyles.RoundtripKind, out var before))
            {
                var beforeUtc = before.ToUniversalTime();
                inbox = inbox.Where(m => m.SentAt <= beforeUtc);
            }

            return Results.Ok(await inbox.OrderByDescending(m => m.SentAt).ToListAsync(ct));
        });

        // Exact dezelfde set als de inbox (één regel, geen tweede codepad).
        api.MapGet("/messages/export", async (HttpContext http, UnbreakableContext db, IMessageAuthorizer pdp,
            ILogger<Program> log, CancellationToken ct) =>
        {
            var ids = await pdp.ReadableMessageIdsAsync(Sub(http), ct);
            Audit(log, "export", Sub(http), null, "allow", ids.Count);

            var messages = await Readable(db, ids).OrderBy(m => m.SentAt).ToListAsync(ct);
            var json = JsonSerializer.Serialize(messages, new JsonSerializerOptions { WriteIndented = true });
            return Results.Text(json, "application/json");
        });

        api.MapGet("/messages/{id:int}", async (int id, HttpContext http, UnbreakableContext db,
            IMessageAuthorizer pdp, ILogger<Program> log, CancellationToken ct) =>
        {
            var allowed = await pdp.CanReadAsync(Sub(http), id, ct);
            Audit(log, "read", Sub(http), id, allowed ? "allow" : "deny");
            if (!allowed)
            {
                // Geen onderscheid tussen "bestaat niet" en "mag niet".
                return Results.NotFound();
            }

            var message = await db.Messages.FindAsync(new object[] { id }, ct);
            return message == null ? Results.NotFound() : Results.Ok(message);
        });

        api.MapPost("/messages", async (CreateMessageRequest request, HttpContext http, UnbreakableContext db,
            IMessageAuthorizer pdp, ILogger<Program> log, CancellationToken ct) =>
        {
            // Opdracht 3: de API aanvaardt enkel versleutelde berichten.
            if (request.KeyLine is null or < 1 or > 50 ||
                string.IsNullOrEmpty(request.Body) || !Regex.IsMatch(request.Body, "^[A-Z0-9_]{40}$", RegexOptions.None, TimeSpan.FromMilliseconds(100)) ||
                string.IsNullOrEmpty(request.Mac) || !Regex.IsMatch(request.Mac, "^[0-9a-f]{64}$", RegexOptions.None, TimeSpan.FromMilliseconds(100)))
            {
                return Results.BadRequest("Enkel versleutelde berichten: body = 40 tekens A-Z 0-9 _, keyLine 1-50, mac = 64 hex-tekens.");
            }

            if (string.IsNullOrWhiteSpace(request.Recipient) || request.Recipient.Length > 40)
            {
                return Results.BadRequest("Ontvanger ontbreekt of is te lang.");
            }

            var recipient = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Username == request.Recipient, ct);
            if (recipient == null)
            {
                return Results.BadRequest("Onbekende ontvanger. De ontvanger moet minstens één keer ingelogd zijn.");
            }

            // Een lijn van het codeboek mag maar één keer gebruikt worden (anders is het geen one-time pad meer).
            if (await db.Messages.AnyAsync(m => m.KeyLine == request.KeyLine, ct))
            {
                return Results.Conflict($"Lijn {request.KeyLine} werd al gebruikt. Kies een nieuwe lijn uit je reeks.");
            }

            var senderSub = Sub(http);
            var message = new Message
            {
                Sender = Username(http),
                SenderSub = senderSub,
                Recipient = recipient.Username,
                RecipientSub = recipient.Sub,
                Subject = Subject,
                Body = request.Body,
                KeyLine = request.KeyLine,
                Mac = request.Mac,
                SentAt = DateTime.UtcNow
            };

            // Volgorde (plan §4.5): eerst de rij committen, dan pas de tuples schrijven. Een rij zonder
            // tuples is onzichtbaar (fail closed); een tuple zonder rij is onschadelijk.
            db.Messages.Add(message);
            await db.SaveChangesAsync(ct);

            try
            {
                await pdp.WriteMessageTuplesAsync(message.Id, senderSub, recipient.Sub, ct);
            }
            catch (PdpUnavailableException)
            {
                // Compensatie: de rij weer weghalen. Lukt dat niet, dan blijft een onzichtbare weesrij over.
                try
                {
                    db.Messages.Remove(message);
                    await db.SaveChangesAsync(CancellationToken.None);
                }
                catch (Exception ex)
                {
                    log.LogError(ex, "Weesrij: bericht {MessageId} kon niet worden teruggedraaid na een tuple-fout.", message.Id);
                }

                Audit(log, "create", senderSub, message.Id, "error");
                throw;
            }

            Audit(log, "create", senderSub, message.Id, "allow");
            return Results.Created($"/api/messages/{message.Id}", message);
        }).RequireRateLimiting(OidcRateLimiting.PostPolicy);

        api.MapDelete("/messages/{id:int}", async (int id, HttpContext http, UnbreakableContext db,
            IMessageAuthorizer pdp, ILogger<Program> log, CancellationToken ct) =>
        {
            var allowed = await pdp.CanDeleteAsync(Sub(http), id, ct);
            Audit(log, "delete", Sub(http), id, allowed ? "allow" : "deny");
            if (!allowed)
            {
                return Results.NotFound();
            }

            var message = await db.Messages.FindAsync(new object[] { id }, ct);
            if (message == null)
            {
                return Results.NotFound();
            }

            var senderSub = message.SenderSub;
            var recipientSub = message.RecipientSub;

            db.Messages.Remove(message);
            await db.SaveChangesAsync(ct);

            try
            {
                await pdp.DeleteMessageTuplesAsync(id, senderSub, recipientSub, ct);
            }
            catch (PdpUnavailableException ex)
            {
                // Wees-tuples zijn onschadelijk: de rij bestaat niet meer en id's worden niet hergebruikt.
                log.LogWarning(ex, "Wees-tuples voor bericht {MessageId}; opruimen via reconciliatie.", id);
            }

            return Results.NoContent();
        });
    }

    // De set berichten die de PDP toestaat; de database filtert enkel nog op die id's.
    private static IQueryable<Message> Readable(UnbreakableContext db, IReadOnlyCollection<int> ids)
    {
        var idArray = ids.ToArray();
        return db.Messages.Where(m => idArray.Contains(m.Id));
    }

    private static string Sub(HttpContext http) => http.User.FindFirst("sub")?.Value ?? string.Empty;

    private static string Username(HttpContext http) => http.User.FindFirst("preferred_username")?.Value ?? string.Empty;

    // Audit: tijd (door de logger), sub, actie, object, beslissing. Nooit body, ciphertext of token.
    private static void Audit(ILogger log, string action, string sub, int? messageId, string decision, int? count = null)
    {
        log.LogInformation("authz action={Action} sub={Sub} message={MessageId} decision={Decision} count={Count}",
            action, sub, messageId, decision, count);
    }
}
