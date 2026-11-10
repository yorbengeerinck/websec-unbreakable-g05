using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Unbreakable.Data;
using Unbreakable.Models;

var builder = WebApplication.CreateBuilder(args);

// Opdracht 4: geen geheimen in de repo. De connectionstring komt uit de omgeving
// (ConnectionStrings__Unbreakable). Ontbreekt hij, dan start de app niet (fail closed).
var connectionString = builder.Configuration.GetConnectionString("Unbreakable");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "ConnectionStrings__Unbreakable ontbreekt. Zet de connectionstring in de omgeving " +
        "(/etc/unbreakable/.env), nooit in appsettings.json.");
}

builder.Services.AddDbContext<UnbreakableContext>(options => options.UseNpgsql(connectionString));

// Opdracht 4: geen CORS meer. De SPA wordt door dezelfde app geserveerd en is dus
// same-origin; een cross-origin-beleid zou alleen maar extra aanvalsvlak toevoegen.

// De app staat achter BunkerWeb. Alleen BunkerWeb mag X-Forwarded-* zetten, dus we
// vertrouwen enkel het interne Docker-netwerk waarop BunkerWeb met de app praat.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownProxies.Clear();
    options.KnownIPNetworks.Clear();
    options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse("fd05:5:0:1::/64"));
    options.ForwardLimit = 1;
});

var app = builder.Build();

// Moet als eerste middleware: daarna ziet de app het echte client-IP en schema.
app.UseForwardedHeaders();

// Security headers. CSP staat op Report-Only tot de SPA volledig CSP-clean is;
// de rest wordt meteen afgedwongen.
app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    headers["X-Content-Type-Options"] = "nosniff";
    headers["Referrer-Policy"] = "no-referrer";
    headers["X-Frame-Options"] = "DENY";
    headers["Content-Security-Policy-Report-Only"] =
        "default-src 'self'; " +
        "script-src 'self'; " +
        "style-src 'self'; " +
        "img-src 'self' data:; " +
        "connect-src 'self'; " +
        "object-src 'none'; " +
        "base-uri 'none'; " +
        "frame-ancestors 'none'; " +
        "form-action 'self'";

    if (context.Request.Path.StartsWithSegments("/api"))
    {
        headers["Cache-Control"] = "no-store";
    }

    await next();
});

// Databank aanmaken (opdracht 3: geen onversleutelde voorbeeldberichten meer).
// De databank kan iets later opstarten dan de app, dus we proberen een paar keer.
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<UnbreakableContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    for (var attempt = 1; attempt <= 30; attempt++)
    {
        try
        {
            context.Database.EnsureCreated();
            // EnsureCreated voegt geen kolommen toe aan een bestaande tabel, dus doen we dat zelf (idempotent).
            context.Database.ExecuteSqlRaw(
                "ALTER TABLE \"Messages\" ADD COLUMN IF NOT EXISTS \"KeyLine\" integer NULL; " +
                "ALTER TABLE \"Messages\" ADD COLUMN IF NOT EXISTS \"Mac\" character varying(64) NULL;");
            break;
        }
        catch (Exception ex) when (attempt < 30)
        {
            logger.LogWarning("Databank nog niet bereikbaar ({Error}), poging {Attempt}", ex.Message, attempt);
            Thread.Sleep(2000);
        }
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

// De SPA: index.html, JavaScript en CSS uit wwwroot.
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/health", () => "ok");

// De API. Wie je bent, staat voorlopig in de header X-User (opdracht 4 vervangt dit door JWT).
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
        var pattern = "%" + EscapeLike(q) + "%";
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

app.Run();

static string CurrentUser(HttpRequest request)
{
    return request.Headers["X-User"].ToString();
}

// Ontsnapt de LIKE-jokertekens zodat een zoekterm letterlijk gezocht wordt.
static string EscapeLike(string value)
{
    return value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
}
