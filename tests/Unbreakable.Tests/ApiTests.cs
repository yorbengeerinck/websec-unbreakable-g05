using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Unbreakable.Tests;

// Integratietests op API-niveau, zonder realm en zonder OpenFGA: tokens met een eigen testsleutel en een
// stand-in voor OpenFGA (zelfde semantiek als §4.4). Dekt T1, T4 tot T20 en T13 tot T16 uit het plan.
// Alles zit in één klasse, zodat de tests niet parallel op dezelfde databank draaien.
public sealed class ApiTests : IClassFixture<ApiFactory>
{
    private const string Yorben = "sub-yorben";
    private const string Jalil = "sub-jalil";
    private const string Pros = "sub-pros";

    private readonly ApiFactory _factory;
    private readonly HttpClient _client;
    private int _line;

    public ApiTests(ApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private static string TokenFor(string sub) => Tokens.Create(sub, sub["sub-".Length..].ToUpperInvariant()[0] + sub["sub-".Length..][1..]);

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, string token, object body = null, string xUser = null)
    {
        var request = new HttpRequestMessage(method, url);
        if (token != null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        if (xUser != null)
        {
            request.Headers.Add("X-User", xUser);
        }

        if (body != null)
        {
            request.Content = JsonContent.Create(body);
        }

        return await _client.SendAsync(request);
    }

    // Elke gebruiker moet minstens één keer ingelogd zijn om als ontvanger gevonden te worden.
    private async Task LoginAllAsync()
    {
        foreach (var sub in new[] { Yorben, Jalil, Pros })
        {
            Assert.Equal(HttpStatusCode.OK, (await SendAsync(HttpMethod.Get, "/api/me", TokenFor(sub))).StatusCode);
        }
    }

    private object NewMessage(string recipient) => new
    {
        recipient,
        keyLine = ++_line,
        body = new string('A', 40),
        mac = new string('a', 64)
    };

    private async Task<int> SendMessageAsync(string fromSub, string toUsername)
    {
        var response = await SendAsync(HttpMethod.Post, "/api/messages", TokenFor(fromSub), NewMessage(toUsername));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("id").GetInt32();
    }

    private async Task<List<int>> InboxIdsAsync(string sub)
    {
        var response = await SendAsync(HttpMethod.Get, "/api/messages", TokenFor(sub));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.EnumerateArray().Select(e => e.GetProperty("id").GetInt32()).ToList();
    }

    [PgFact]
    public async Task T01_NoToken_Is401()
    {
        await _factory.ResetAsync();
        Assert.Equal(HttpStatusCode.Unauthorized, (await SendAsync(HttpMethod.Get, "/api/messages", null)).StatusCode);
    }

    [PgFact]
    public async Task T13_XUserHeaderWithoutToken_Is401()
    {
        await _factory.ResetAsync();
        Assert.Equal(HttpStatusCode.Unauthorized, (await SendAsync(HttpMethod.Get, "/api/messages", null, xUser: "Yorben")).StatusCode);
    }

    [PgFact]
    public async Task XUserHeaderWithToken_HasNoEffect()
    {
        await _factory.ResetAsync();
        await LoginAllAsync();
        var id = await SendMessageAsync(Yorben, "Jalil");

        // Pros stuurt X-User: Jalil mee, maar blijft Pros: geen toegang tot het bericht.
        var response = await SendAsync(HttpMethod.Get, $"/api/messages/{id}", TokenFor(Pros), xUser: "Jalil");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [PgFact]
    public async Task T14_T15_T16_InvalidTokens_Are401()
    {
        await _factory.ResetAsync();
        var bad = new Dictionary<string, string>
        {
            ["verkeerde audience"] = Tokens.Create(Yorben, "Yorben", audience: "andere-api"),
            ["verkeerde issuer"] = Tokens.Create(Yorben, "Yorben", issuer: "https://evil.example/realms/x"),
            ["verkeerde azp"] = Tokens.Create(Yorben, "Yorben", azp: "andere-client"),
            ["verkeerde typ"] = Tokens.Create(Yorben, "Yorben", typ: "ID"),
            ["verlopen"] = Tokens.Create(Yorben, "Yorben", expiresInMinutes: -5),
            ["verkeerde sleutel"] = Tokens.Create(Yorben, "Yorben", wrongKey: true),
            ["zonder sub"] = Tokens.Create(Yorben, "Yorben", omitSub: true),
            ["alg=none"] = Tokens.AlgNone(Yorben, "Yorben")
        };

        foreach (var (name, token) in bad)
        {
            var response = await SendAsync(HttpMethod.Get, "/api/messages", token);
            Assert.True(response.StatusCode == HttpStatusCode.Unauthorized, $"{name}: verwacht 401, kreeg {(int)response.StatusCode}");
        }
    }

    [PgFact]
    public async Task T04_T06_T07_T08_OnlyRecipientReads()
    {
        await _factory.ResetAsync();
        await LoginAllAsync();
        var id = await SendMessageAsync(Yorben, "Jalil");

        Assert.Equal(HttpStatusCode.OK, (await SendAsync(HttpMethod.Get, $"/api/messages/{id}", TokenFor(Jalil))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(HttpMethod.Get, $"/api/messages/{id}", TokenFor(Yorben))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(HttpMethod.Get, $"/api/messages/{id}", TokenFor(Pros))).StatusCode);
    }

    [PgFact]
    public async Task T05_T10_T11_T12_OnlySenderDeletes_AndTuplesFollow()
    {
        await _factory.ResetAsync();
        await LoginAllAsync();
        var id = await SendMessageAsync(Yorben, "Jalil");
        Assert.Equal(2, _factory.Pdp.Tuples.Count(t => t.Id == id)); // T5: sender- en recipient-tuple

        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(HttpMethod.Delete, $"/api/messages/{id}", TokenFor(Jalil))).StatusCode); // T9
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(HttpMethod.Delete, $"/api/messages/{id}", TokenFor(Pros))).StatusCode);  // T12
        Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(HttpMethod.Delete, $"/api/messages/{id}", TokenFor(Yorben))).StatusCode); // T10
        Assert.Empty(_factory.Pdp.Tuples.Where(t => t.Id == id));
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(HttpMethod.Get, $"/api/messages/{id}", TokenFor(Jalil))).StatusCode); // T11
    }

    [PgFact]
    public async Task T17_T19_T20_InboxAndExportOnlyContainReceivedMessages()
    {
        await _factory.ResetAsync();
        await LoginAllAsync();
        var toJalil = await SendMessageAsync(Yorben, "Jalil");
        var toYorben = await SendMessageAsync(Pros, "Yorben");

        Assert.Equal(new[] { toJalil }, await InboxIdsAsync(Jalil));
        Assert.Equal(new[] { toYorben }, await InboxIdsAsync(Yorben)); // niet het bericht dat hij zelf verzond (T20)
        Assert.Empty(await InboxIdsAsync(Pros));

        var export = await SendAsync(HttpMethod.Get, "/api/messages/export", TokenFor(Jalil));
        using var json = JsonDocument.Parse(await export.Content.ReadAsStringAsync());
        Assert.Equal(new[] { toJalil }, json.RootElement.EnumerateArray().Select(e => e.GetProperty("id").GetInt32()).ToArray()); // T19
    }

    [PgFact]
    public async Task T18_SearchOnlyReturnsVisibleMessages()
    {
        await _factory.ResetAsync();
        await LoginAllAsync();
        var visible = await SendMessageAsync(Yorben, "Jalil");
        await SendMessageAsync(Yorben, "Pros");

        var response = await SendAsync(HttpMethod.Get, "/api/messages/search?q=Yorben", TokenFor(Jalil));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(new[] { visible }, json.RootElement.EnumerateArray().Select(e => e.GetProperty("id").GetInt32()).ToArray());
    }

    [PgFact]
    public async Task MassAssignment_IsIgnored()
    {
        await _factory.ResetAsync();
        await LoginAllAsync();
        var response = await SendAsync(HttpMethod.Post, "/api/messages", TokenFor(Yorben), new
        {
            recipient = "Jalil",
            keyLine = ++_line,
            body = new string('A', 40),
            mac = new string('a', 64),
            sender = "Pros",
            senderSub = Pros,
            id = 999,
            subject = "gehackt",
            sentAt = "2001-01-01T00:00:00Z"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Yorben", json.RootElement.GetProperty("sender").GetString());
        Assert.NotEqual(999, json.RootElement.GetProperty("id").GetInt32());
        Assert.Equal("Versleuteld bericht", json.RootElement.GetProperty("subject").GetString());
        Assert.False(json.RootElement.TryGetProperty("senderSub", out _));
    }

    [PgFact]
    public async Task Post_Validation()
    {
        await _factory.ResetAsync();
        await LoginAllAsync();

        Assert.Equal(HttpStatusCode.BadRequest, (await SendAsync(HttpMethod.Post, "/api/messages", TokenFor(Yorben),
            new { recipient = "Jalil", keyLine = 1, body = "kort", mac = new string('a', 64) })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await SendAsync(HttpMethod.Post, "/api/messages", TokenFor(Yorben),
            new { recipient = "Bestaatniet", keyLine = 2, body = new string('A', 40), mac = new string('a', 64) })).StatusCode);

        var first = await SendAsync(HttpMethod.Post, "/api/messages", TokenFor(Yorben),
            new { recipient = "Jalil", keyLine = 3, body = new string('A', 40), mac = new string('a', 64) });
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var reuse = await SendAsync(HttpMethod.Post, "/api/messages", TokenFor(Yorben),
            new { recipient = "Jalil", keyLine = 3, body = new string('B', 40), mac = new string('b', 64) });
        Assert.Equal(HttpStatusCode.Conflict, reuse.StatusCode);
    }

    [PgFact]
    public async Task T21_PdpDown_Gives503_AndNoData()
    {
        await _factory.ResetAsync();
        await LoginAllAsync();
        var id = await SendMessageAsync(Yorben, "Jalil");

        _factory.Pdp.Down = true;
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await SendAsync(HttpMethod.Get, "/api/messages", TokenFor(Jalil))).StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await SendAsync(HttpMethod.Get, $"/api/messages/{id}", TokenFor(Jalil))).StatusCode);

        // POST met een falende PDP: 503 en de rij is teruggedraaid (compensatie).
        var response = await SendAsync(HttpMethod.Post, "/api/messages", TokenFor(Yorben), NewMessage("Jalil"));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);

        _factory.Pdp.Down = false;
        Assert.Equal(new[] { id }, await InboxIdsAsync(Jalil));
    }

    [PgFact]
    public async Task Config_IsPublic_AndReportsOidc()
    {
        var response = await SendAsync(HttpMethod.Get, "/api/config", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("oidc", json.RootElement.GetProperty("authMode").GetString());
    }

    // T36: geen autorisatievergelijkingen in de API-code.
    [Fact]
    public void T36_NoAuthorizationComparisonsInApi()
    {
        var dir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../app/Api"));
        var oidc = File.ReadAllText(Path.Combine(dir, "OidcApi.cs"));
        Assert.DoesNotContain("RecipientSub ==", oidc);
        Assert.DoesNotContain("SenderSub ==", oidc);
        Assert.DoesNotContain("== m.RecipientSub", oidc);
        Assert.DoesNotContain("== m.SenderSub", oidc);
    }
}
