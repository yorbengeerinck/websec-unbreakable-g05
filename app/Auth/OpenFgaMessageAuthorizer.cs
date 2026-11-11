using OpenFga.Sdk.Client;
using OpenFga.Sdk.Client.Model;
using OpenFga.Sdk.Configuration;
using OpenFga.Sdk.Model;

namespace Unbreakable.Auth;

// De enige plek met OpenFGA-aanroepen. Elke fout, timeout of onbereikbaarheid wordt een
// PdpUnavailableException (503): fail closed.
public sealed class OpenFgaMessageAuthorizer : IMessageAuthorizer
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(3);

    private readonly OpenFgaClient _fga;

    public OpenFgaMessageAuthorizer(OpenFgaClient fga)
    {
        _fga = fga;
    }

    public Task<bool> CanReadAsync(string sub, int messageId, CancellationToken cancellationToken) =>
        CheckAsync(sub, "can_read", messageId, cancellationToken);

    public Task<bool> CanDeleteAsync(string sub, int messageId, CancellationToken cancellationToken) =>
        CheckAsync(sub, "can_delete", messageId, cancellationToken);

    public async Task<IReadOnlyCollection<int>> ReadableMessageIdsAsync(string sub, CancellationToken cancellationToken)
    {
        return await RunAsync(async ct =>
        {
            var response = await _fga.ListObjects(
                new ClientListObjectsRequest { User = $"user:{sub}", Relation = "can_read", Type = "message" },
                new ClientListObjectsOptions { Consistency = ConsistencyPreference.HIGHERCONSISTENCY },
                ct);

            var ids = new HashSet<int>();
            foreach (var obj in response.Objects ?? new List<string>())
            {
                // Vorm: "message:<id>"
                var separator = obj.IndexOf(':');
                if (separator >= 0 && int.TryParse(obj[(separator + 1)..], out var id))
                {
                    ids.Add(id);
                }
            }

            return (IReadOnlyCollection<int>)ids;
        }, cancellationToken);
    }

    public Task WriteMessageTuplesAsync(int messageId, string senderSub, string recipientSub, CancellationToken cancellationToken) =>
        RunAsync<object>(async ct =>
        {
            await _fga.Write(new ClientWriteRequest
            {
                Writes = new List<ClientTupleKey>
                {
                    new() { User = $"user:{senderSub}", Relation = "sender", Object = $"message:{messageId}" },
                    new() { User = $"user:{recipientSub}", Relation = "recipient", Object = $"message:{messageId}" }
                }
            }, null, ct);
            return null;
        }, cancellationToken);

    public Task DeleteMessageTuplesAsync(int messageId, string senderSub, string recipientSub, CancellationToken cancellationToken) =>
        RunAsync<object>(async ct =>
        {
            await _fga.Write(new ClientWriteRequest
            {
                Deletes = new List<ClientTupleKeyWithoutCondition>
                {
                    new() { User = $"user:{senderSub}", Relation = "sender", Object = $"message:{messageId}" },
                    new() { User = $"user:{recipientSub}", Relation = "recipient", Object = $"message:{messageId}" }
                }
            }, new ClientWriteOptions
            {
                // Idempotent: een tuple dat al weg is, is geen fout.
                Conflict = new ConflictOptions { OnMissingDeletes = OnMissingDeletes.Ignore }
            }, ct);
            return null;
        }, cancellationToken);

    private Task<bool> CheckAsync(string sub, string relation, int messageId, CancellationToken cancellationToken) =>
        RunAsync(async ct =>
        {
            var response = await _fga.Check(
                new ClientCheckRequest { User = $"user:{sub}", Relation = relation, Object = $"message:{messageId}" },
                new ClientCheckOptions { Consistency = ConsistencyPreference.HIGHERCONSISTENCY },
                ct);
            return response.Allowed == true;
        }, cancellationToken);

    private static async Task<T> RunAsync<T>(Func<CancellationToken, Task<T>> call, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(Timeout);
        try
        {
            return await call(timeout.Token);
        }
        catch (Exception ex)
        {
            // Bewust breed: welke fout dan ook betekent "geen beslissing" en dus "geen toegang".
            throw new PdpUnavailableException("OpenFGA gaf geen bruikbaar antwoord.", ex);
        }
    }
}

public static class OpenFgaSetup
{
    public static IServiceCollection AddOpenFgaAuthorizer(this IServiceCollection services, IConfiguration configuration)
    {
        var apiUrl = OidcSetup.Required(configuration, "OpenFga:ApiUrl");
        var storeId = OidcSetup.Required(configuration, "OpenFga:StoreId");
        var modelId = OidcSetup.Required(configuration, "OpenFga:ModelId");
        var apiToken = OidcSetup.Required(configuration, "OpenFga:ApiToken");

        // Eén client voor de hele app (singleton), met een vastgepind model-ID.
        services.AddSingleton(_ => new OpenFgaClient(new ClientConfiguration
        {
            ApiUrl = apiUrl,
            StoreId = storeId,
            AuthorizationModelId = modelId,
            Credentials = new Credentials
            {
                Method = CredentialsMethod.ApiToken,
                Config = new CredentialsConfig { ApiToken = apiToken }
            }
        }));
        services.AddSingleton<IMessageAuthorizer, OpenFgaMessageAuthorizer>();
        return services;
    }
}
