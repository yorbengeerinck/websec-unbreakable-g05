using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Unbreakable.Data;
using Unbreakable.Models;

namespace Unbreakable.Api;

// TIJDELIJK (overgangsschakelaar, plan §8.4): het oude gedrag, ongewijzigd. Wie je bent, staat in de
// header X-User. Dit bestand wordt in de opruim-PR verwijderd, samen met Auth/AuthSettings.cs.
public static class LegacyApi
{
    public static void MapLegacyApi(this WebApplication app)
    {
        var api = app.MapGroup("/api");

        api.MapGet("/users", async (UnbreakableContext db) =>
        {
            var senders = await db.Messages.Select(m => m.Sender).Distinct().ToListAsync();
            var recipients = await db.Messages.Select(m => m.Recipient).Distinct().ToListAsync();
            return Results.Ok(senders.Union(recipients).OrderBy(name => name).ToList());
        });

        api.MapGet("/messages", async (HttpRequest request, UnbreakableContext db) =>
        {
            var user = CurrentUser(request);
            if (string.IsNullOrEmpty(user))
            {
                return Results.Unauthorized();
            }

            var inbox = db.Messages.Where(m => m.Recipient == user);

            var from = request.Query["from"].ToString();
            if (!string.IsNullOrEmpty(from))
            {
                inbox = inbox.Where(m => m.Sender == from);
            }

            return Results.Ok(await inbox.OrderByDescending(m => m.SentAt).ToListAsync());
        });

        api.MapGet("/messages/search", async (HttpRequest request, UnbreakableContext db) =>
        {
            var user = CurrentUser(request);
            if (string.IsNullOrEmpty(user))
            {
                return Results.Unauthorized();
            }

            var q = request.Query["q"].ToString().Trim();
            if (q.Length > 100)
            {
                return Results.BadRequest("Zoekterm te lang (max 100 tekens).");
            }

            var inbox = db.Messages.Where(m => m.Recipient == user);

            if (q.Length > 0)
            {
                // Geparameteriseerd: EF Core stuurt de zoekterm als parameter, nooit als SQL-tekst.
                // % en _ in de invoer worden ontsnapt, zodat ze letterlijk gezocht worden.
                var pattern = "%" + ApiHelpers.EscapeLike(q) + "%";
                inbox = inbox.Where(m =>
                    EF.Functions.ILike(m.Subject, pattern, "\\") ||
                    EF.Functions.ILike(m.Body, pattern, "\\"));
            }

            var messages = await inbox.OrderByDescending(m => m.SentAt).ToListAsync();
            return Results.Ok(messages);
        });

        api.MapGet("/messages/export", async (HttpRequest request, UnbreakableContext db) =>
        {
            var user = CurrentUser(request);
            if (string.IsNullOrEmpty(user))
            {
                return Results.Unauthorized();
            }

            var messages = await db.Messages
                .Where(m => m.Recipient == user)
                .OrderBy(m => m.SentAt)
                .ToListAsync();

            var json = JsonSerializer.Serialize(messages, new JsonSerializerOptions { WriteIndented = true });
            return Results.Text(json, "application/json");
        });

        api.MapGet("/messages/{id:int}", async (int id, UnbreakableContext db) =>
        {
            var message = await db.Messages.FindAsync(id);
            return message == null ? Results.NotFound() : Results.Ok(message);
        });

        api.MapPost("/messages", async (Message message, HttpRequest request, UnbreakableContext db) =>
        {
            // Opdracht 3: de API aanvaardt enkel versleutelde berichten.
            if (message.KeyLine is null or < 1 or > 50 ||
                string.IsNullOrEmpty(message.Body) || !Regex.IsMatch(message.Body, "^[A-Z0-9_]{40}$", RegexOptions.None, TimeSpan.FromMilliseconds(100)) ||
                string.IsNullOrEmpty(message.Mac) || !Regex.IsMatch(message.Mac, "^[0-9a-f]{64}$", RegexOptions.None, TimeSpan.FromMilliseconds(100)))
            {
                return Results.BadRequest("Enkel versleutelde berichten: body = 40 tekens A-Z 0-9 _, keyLine 1-50, mac = 64 hex-tekens.");
            }

            // Een lijn van het codeboek mag maar één keer gebruikt worden (anders is het geen one-time pad meer).
            if (await db.Messages.AnyAsync(m => m.KeyLine == message.KeyLine))
            {
                return Results.Conflict($"Lijn {message.KeyLine} werd al gebruikt. Kies een nieuwe lijn uit je reeks.");
            }

            // Het onderwerp is niet versleuteld, dus de server zet een vaste tekst (geen plaintext in de databank).
            message.Subject = "Versleuteld bericht";

            // Geen afzender meegegeven? Dan nemen we wie je bent.
            if (string.IsNullOrEmpty(message.Sender))
            {
                message.Sender = CurrentUser(request);
            }

            message.Id = 0;
            message.SentAt = DateTime.UtcNow;

            db.Messages.Add(message);
            await db.SaveChangesAsync();

            return Results.Created($"/api/messages/{message.Id}", message);
        });

        api.MapDelete("/messages/{id:int}", async (int id, UnbreakableContext db) =>
        {
            var message = await db.Messages.FindAsync(id);
            if (message == null)
            {
                return Results.NotFound();
            }

            db.Messages.Remove(message);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });
    }

    private static string CurrentUser(HttpRequest request)
    {
        return request.Headers["X-User"].ToString();
    }
}
