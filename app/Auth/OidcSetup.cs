using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace Unbreakable.Auth;

public static class OidcSetup
{
    public static IServiceCollection AddOidcApiAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var metadataAddress = Required(configuration, "Oidc:MetadataAddress");
        var issuer = Required(configuration, "Oidc:Issuer");
        var audience = Required(configuration, "Oidc:Audience");
        var authorizedParty = configuration["Oidc:AuthorizedParty"];
        if (string.IsNullOrWhiteSpace(authorizedParty))
        {
            authorizedParty = "unbreakable-spa";
        }

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                // Metadata en JWKS gaan over het interne, geïsoleerde Docker-netwerk (http). Alle
                // clientverkeer loopt over HTTPS via BunkerWeb. Daarom is dit enkel hier uitgeschakeld.
                options.MetadataAddress = metadataAddress;
                options.RequireHttpsMetadata = false;
                options.MapInboundClaims = false;

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = issuer,
                    ValidateAudience = true,
                    ValidAudience = audience,
                    ValidateLifetime = true,
                    RequireExpirationTime = true,
                    RequireSignedTokens = true,
                    ValidateIssuerSigningKey = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    // Enkel RS256: sluit alg=none en HS256-verwarring uit.
                    ValidAlgorithms = new[] { SecurityAlgorithms.RsaSha256 },
                    NameClaimType = "preferred_username"
                };

                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = context =>
                    {
                        var principal = context.Principal;
                        var sub = principal?.FindFirst("sub")?.Value;
                        if (string.IsNullOrWhiteSpace(sub))
                        {
                            context.Fail("sub ontbreekt.");
                        }
                        else if (principal.FindFirst("azp")?.Value != authorizedParty)
                        {
                            // Het token moet door onze SPA zijn verkregen.
                            context.Fail("azp is niet de verwachte client.");
                        }
                        else if (principal.FindFirst("typ")?.Value != "Bearer")
                        {
                            context.Fail("typ is niet Bearer.");
                        }

                        return Task.CompletedTask;
                    }
                };
            });

        services.AddAuthorization();
        return services;
    }

    internal static string Required(IConfiguration configuration, string key)
    {
        var value = configuration[key];
        if (string.IsNullOrWhiteSpace(value))
        {
            // Fail closed: zonder volledige configuratie start de app niet in oidc-modus.
            throw new InvalidOperationException(
                $"{key.Replace(':', '_')} ontbreekt (Auth__Mode=oidc vereist dit). Zet het in /etc/unbreakable/.env.");
        }

        return value;
    }
}
