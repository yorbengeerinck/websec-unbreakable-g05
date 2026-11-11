namespace Unbreakable.Auth;

// TIJDELIJK (overgangsschakelaar, plan §8.4): "legacy" = huidig gedrag (X-User), "oidc" = JWT + OpenFGA.
// Deze klasse, Api/LegacyApi.cs en GET /api/config verdwijnen in de opruim-PR.
public sealed class AuthSettings
{
    public const string Legacy = "legacy";
    public const string Oidc = "oidc";

    public string Mode { get; private init; } = Legacy;

    public bool IsOidc => Mode == Oidc;

    public static AuthSettings FromConfiguration(IConfiguration configuration)
    {
        var mode = (configuration["Auth:Mode"] ?? Legacy).Trim().ToLowerInvariant();
        if (mode != Legacy && mode != Oidc)
        {
            // Onbekende waarde: nooit stilzwijgend terugvallen op een andere modus.
            throw new InvalidOperationException($"Auth__Mode '{mode}' is ongeldig. Gebruik 'legacy' of 'oidc'.");
        }

        return new AuthSettings { Mode = mode };
    }
}
