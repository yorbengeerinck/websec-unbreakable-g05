using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Unbreakable.Auth;
using Unbreakable.Data;
using Xunit;

namespace Unbreakable.Tests;

// De tests draaien enkel als er een Postgres is: zet TEST_CONNECTIONSTRING
// (bv. Host=localhost;Port=5432;Database=unbreakable_test;Username=postgres;Password=postgres).
public sealed class PgFactAttribute : FactAttribute
{
    public PgFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TEST_CONNECTIONSTRING")))
        {
            Skip = "TEST_CONNECTIONSTRING is niet gezet (Postgres nodig).";
        }
    }
}

// Stand-in voor OpenFGA met dezelfde semantiek als het model in plan §4.4:
// can_read = recipient, can_delete = sender. Echte OpenFGA-tests staan apart (plan Y7).
public sealed class FakeAuthorizer : IMessageAuthorizer
{
    private readonly object _lock = new();
    private readonly List<(string User, string Relation, int Id)> _tuples = new();

    public bool Down { get; set; }

    public IReadOnlyList<(string User, string Relation, int Id)> Tuples
    {
        get { lock (_lock) { return _tuples.ToList(); } }
    }

    public void Clear()
    {
        lock (_lock) { _tuples.Clear(); }
        Down = false;
    }

    private void ThrowIfDown()
    {
        if (Down)
        {
            throw new PdpUnavailableException("fake PDP is down");
        }
    }

    public Task<bool> CanReadAsync(string sub, int messageId, CancellationToken cancellationToken)
    {
        ThrowIfDown();
        lock (_lock) { return Task.FromResult(_tuples.Contains((sub, "recipient", messageId))); }
    }

    public Task<bool> CanDeleteAsync(string sub, int messageId, CancellationToken cancellationToken)
    {
        ThrowIfDown();
        lock (_lock) { return Task.FromResult(_tuples.Contains((sub, "sender", messageId))); }
    }

    public Task<IReadOnlyCollection<int>> ReadableMessageIdsAsync(string sub, CancellationToken cancellationToken)
    {
        ThrowIfDown();
        lock (_lock)
        {
            IReadOnlyCollection<int> ids = _tuples.Where(t => t.User == sub && t.Relation == "recipient").Select(t => t.Id).ToList();
            return Task.FromResult(ids);
        }
    }

    public Task WriteMessageTuplesAsync(int messageId, string senderSub, string recipientSub, CancellationToken cancellationToken)
    {
        ThrowIfDown();
        lock (_lock)
        {
            _tuples.Add((senderSub, "sender", messageId));
            _tuples.Add((recipientSub, "recipient", messageId));
        }

        return Task.CompletedTask;
    }

    public Task DeleteMessageTuplesAsync(int messageId, string senderSub, string recipientSub, CancellationToken cancellationToken)
    {
        ThrowIfDown();
        lock (_lock) { _tuples.RemoveAll(t => t.Id == messageId); }
        return Task.CompletedTask;
    }
}

// Maakt JWT's met een eigen testsleutel; de echte realm is niet nodig.
public static class Tokens
{
    public const string Issuer = "https://test.example/auth/realms/unbreakable";
    public const string Audience = "unbreakable-api";
    public const string Azp = "unbreakable-spa";

    public static readonly RSA SigningKey = RSA.Create(2048);
    private static readonly RSA OtherKey = RSA.Create(2048);

    public static string Create(
        string sub,
        string username,
        string issuer = Issuer,
        string audience = Audience,
        string azp = Azp,
        string typ = "Bearer",
        int expiresInMinutes = 5,
        bool wrongKey = false,
        bool omitSub = false)
    {
        var key = new RsaSecurityKey(wrongKey ? OtherKey : SigningKey) { KeyId = "test" };
        var claims = new List<Claim> { new("preferred_username", username), new("azp", azp), new("typ", typ) };
        if (!omitSub)
        {
            claims.Add(new Claim("sub", sub));
        }

        var now = DateTime.UtcNow;
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = audience,
            Subject = new ClaimsIdentity(claims),
            NotBefore = now.AddMinutes(-10),
            IssuedAt = now.AddMinutes(-10),
            Expires = now.AddMinutes(expiresInMinutes),
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.RsaSha256)
        };
        return new JsonWebTokenHandler().CreateToken(descriptor);
    }

    // Een token met alg=none en zonder handtekening (T16).
    public static string AlgNone(string sub, string username)
    {
        static string B64(string s) => Convert.ToBase64String(Encoding.UTF8.GetBytes(s)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var exp = DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeSeconds();
        var header = B64("{\"alg\":\"none\",\"typ\":\"JWT\"}");
        var payload = B64($"{{\"iss\":\"{Issuer}\",\"aud\":\"{Audience}\",\"sub\":\"{sub}\",\"preferred_username\":\"{username}\",\"azp\":\"{Azp}\",\"typ\":\"Bearer\",\"exp\":{exp}}}");
        return $"{header}.{payload}.";
    }
}

public sealed class ApiFactory : WebApplicationFactory<Program>
{
    public FakeAuthorizer Pdp { get; } = new();

    static ApiFactory()
    {
        // Omgevingsvariabelen zijn de betrouwbaarste manier om Program-instellingen te zetten
        // (Program leest de configuratie meteen bij het opbouwen).
        Environment.SetEnvironmentVariable("ConnectionStrings__Unbreakable", Environment.GetEnvironmentVariable("TEST_CONNECTIONSTRING"));
        Environment.SetEnvironmentVariable("Auth__Mode", "oidc");
        Environment.SetEnvironmentVariable("Oidc__MetadataAddress", "http://127.0.0.1:1/niet-gebruikt");
        Environment.SetEnvironmentVariable("Oidc__Issuer", Tokens.Issuer);
        Environment.SetEnvironmentVariable("Oidc__Audience", Tokens.Audience);
        Environment.SetEnvironmentVariable("OpenFga__ApiUrl", "http://127.0.0.1:1");
        Environment.SetEnvironmentVariable("OpenFga__StoreId", "test");
        Environment.SetEnvironmentVariable("OpenFga__ModelId", "test");
        Environment.SetEnvironmentVariable("OpenFga__ApiToken", "test");
    }

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IMessageAuthorizer>();
            services.AddSingleton<IMessageAuthorizer>(Pdp);

            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                var configuration = new OpenIdConnectConfiguration { Issuer = Tokens.Issuer };
                configuration.SigningKeys.Add(new RsaSecurityKey(Tokens.SigningKey) { KeyId = "test" });
                options.Configuration = configuration;
                options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(configuration);
            });
        });
    }

    // Lege tabellen en lege tuple-store voor elke test.
    public async Task ResetAsync()
    {
        Pdp.Clear();
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<UnbreakableContext>();
        scope.ServiceProvider.GetRequiredService<UserDirectory>().Clear();
        await db.Database.ExecuteSqlRawAsync("TRUNCATE TABLE \"Messages\", \"Users\" RESTART IDENTITY");
    }
}
