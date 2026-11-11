using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Unbreakable.Api;
using Unbreakable.Auth;
using Unbreakable.Data;

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

// TIJDELIJK (overgangsschakelaar, plan §8.4): Auth__Mode = legacy (standaard) of oidc.
var authSettings = AuthSettings.FromConfiguration(builder.Configuration);
builder.Services.AddSingleton(authSettings);

builder.Services.AddDbContext<UnbreakableContext>(options => options.UseNpgsql(connectionString));

if (authSettings.IsOidc)
{
    // Y5: JWT-validatie. Y7: PEP + OpenFGA. Ontbrekende configuratie = de app start niet.
    builder.Services.AddOidcApiAuthentication(builder.Configuration);
    builder.Services.AddOpenFgaAuthorizer(builder.Configuration);
    builder.Services.AddSingleton<UserDirectory>();
    builder.Services.AddOidcRateLimiting();
}

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

if (authSettings.IsOidc)
{
    // Fail closed: een onbereikbare of falende OpenFGA geeft 503 en nooit data.
    app.Use(async (context, next) =>
    {
        try
        {
            await next();
        }
        catch (PdpUnavailableException ex)
        {
            app.Logger.LogError(ex, "Autorisatieservice (OpenFGA) niet bereikbaar of foutief; verzoek geweigerd.");
            if (!context.Response.HasStarted)
            {
                context.Response.Clear();
                context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                await context.Response.WriteAsync("Autorisatieservice niet beschikbaar.");
            }
        }
    });

    app.UseAuthentication();
    app.UseAuthorization();
    app.UseRateLimiter();
}

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
            // EnsureCreated voegt niets toe aan een bestaande databank, dus doen we de uitbreidingen zelf (idempotent).
            SchemaUpgrade.Apply(context);
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

// TIJDELIJK (plan §8.4): vertelt de frontend welke modus actief is. Verdwijnt in de opruim-PR.
app.MapGet("/api/config", (AuthSettings settings) => Results.Ok(new { authMode = settings.Mode }));

if (authSettings.IsOidc)
{
    app.MapOidcApi();
}
else
{
    app.MapLegacyApi();
}

app.Run();

// Zichtbaar voor de integratietests (WebApplicationFactory<Program>).
public partial class Program { }
