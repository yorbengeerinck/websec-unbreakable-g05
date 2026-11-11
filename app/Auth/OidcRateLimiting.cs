using System.Threading.RateLimiting;

namespace Unbreakable.Auth;

public static class OidcRateLimiting
{
    public const string PostPolicy = "post-messages";

    public static IServiceCollection AddOidcRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            // Algemeen: per gebruiker (of per IP als er nog geen gebruiker is).
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                RateLimitPartition.GetFixedWindowLimiter(Key(context), _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 120,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0
                }));

            // Strenger voor het versturen van berichten.
            options.AddPolicy(PostPolicy, context =>
                RateLimitPartition.GetFixedWindowLimiter(Key(context), _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 20,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0
                }));
        });

        return services;
    }

    private static string Key(HttpContext context) =>
        context.User.FindFirst("sub")?.Value
        ?? context.Connection.RemoteIpAddress?.ToString()
        ?? "onbekend";
}
