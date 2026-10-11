# AI Security Code Review — Unbreakable (websec-unbreakable-g05)

## Metadata

| Veld | Waarde |
|------|--------|
| **OWASP-play** | `code-review-security` (secure-agent-playbook/plugins/code-security-skills/plays/code-review-security.md) |
| **Skill** | `secure-agent-playbook/plugins/code-security-skills/skills/code-review-security/SKILL.md` |
| **AI-agent** | Antigravity (Google DeepMind) — Claude Sonnet 4.6 (Thinking) |
| **Reviewdatum** | 2026-10-10 |
| **Reviewtype** | Full codebase review |

---

## Scope

### Bestanden onderzocht

| Bestand | Omschrijving |
|---------|-------------|
| `app/Program.cs` | ASP.NET Core Minimal API — alle endpoints en authenticatielogica |
| `app/appsettings.json` | Applicatieconfiguratie en verbindingsstrings |
| `app/Models/Message.cs` | Datamodel voor berichten |
| `app/Data/UnbreakableContext.cs` | Entity Framework DbContext |
| `app/Data/SeedData.cs` | Initialisatiedata voor de databank |
| `app/Dockerfile` | Container build-configuratie |
| `docker-compose.yml` | Containerorkestratie en omgevingsvariabelen |
| `app/wwwroot/index.html` | Frontend HTML shell |
| `app/wwwroot/js/app.js` | Frontend SPA-logica (routing, rendering, DOM-manipulatie) |
| `app/wwwroot/js/api.js` | Frontend API-client en gebruikerssessiebeheer |
| `docs/threat-model.md` | Bestaand dreigingsmodel |
| `docs/accepted-findings.md` | Eerder geaccepteerde bevindingen |

### Language/Framework

- **Backend**: C# / ASP.NET Core 10 (Minimal API), Entity Framework Core, PostgreSQL
- **Frontend**: Vanilla JavaScript (ES Modules), jQuery, Bootstrap
- **Infrastructuur**: Docker Compose, PostgreSQL 17

### Trust boundary

Internet-facing applicatie (poort 8080, HTTP). De SPA draait client-side in de browser; alle beveiligingscontroles moeten server-side plaatsvinden.

### Data sensitivity

Privéberichten van gebruikers, inclusief gevoelige inhoud (wachtwoorden in berichttekst in seed data). De app beweert berichten te sturen die "niemand anders kan lezen".

---

## Bevindingen

Gesorteerd op ernst (Critical → Low). Elke bevinding vermeldt de **vertrouwensgraad** per de OWASP-play: HIGH = bevestigde kwetsbaarheid, MEDIUM = contextafhankelijk risico.

---

### Bevinding 1 — SQL Injection via `FromSqlRaw` in zoekendpoint

| Veld | Waarde |
|------|--------|
| **Ernst** | CRITICAL |
| **OWASP** | A03:2021 – Injection (CWE-89) |
| **OpenCRE** | https://www.opencre.org/cre/161-451 |
| **Vertrouwensgraad** | HIGH — directe bevestiging van de kwetsbare code |
| **Bestand** | `app/Program.cs` |
| **Regels** | 85–89 |

#### Beschrijving

De zoekfunctie bouwt een SQL-query op via stringaaneenschakeling. De waarde van de queryparameter `q` wordt rechtstreeks ingevoegd in de SQL-string zonder parameterisatie of escaping.

#### Kwetsbare code

```csharp
// Program.cs, regels 85–89
var q = request.Query["q"].ToString();
var sql = "SELECT * FROM \"Messages\" WHERE \"Recipient\" = '" + user + "' " +
          "AND (\"Subject\" ILIKE '%" + q + "%' OR \"Body\" ILIKE '%" + q + "%')";

var messages = await db.Messages.FromSqlRaw(sql).OrderByDescending(m => m.SentAt).ToListAsync();
```

#### Waarom het probleem bestaat

De variabele `q` wordt ongevalideerd en zonder SQL-parameterisatie in de query ingevoegd. `FromSqlRaw()` in Entity Framework voert de string letterlijk uit. Een aanvaller kan de `q`-parameter manipuleren om de SQL-structuur te doorbreken.

#### Aanvalsscenario

Een aanvaller stuurt:
```
GET /api/messages/search?q=%' OR '1'='1
```

Dit resulteert in de query:
```sql
SELECT * FROM "Messages" WHERE "Recipient" = 'Anna'
AND ("Subject" ILIKE '%%' OR '1'='1%' OR "Body" ILIKE '%%' OR '1'='1%')
```

De conditie `'1'='1'` is altijd waar, waardoor alle berichten van alle gebruikers worden teruggegeven. Verdergaande aanvallen (UNION-based, time-based blind) zijn eveneens mogelijk.

#### Impact

Volledige blootstelling van alle berichten in de database, ongeacht de ontvanger. Aanvaller kan alle privéberichten lezen, het databaseschema uitlezen, en bij voldoende rechten willekeurige commando's uitvoeren (via PostgreSQL-extensies zoals `COPY TO/FROM PROGRAM`).

#### Aanbevolen oplossing

Vervang de `FromSqlRaw` met een geparameteriseerde EF Core LINQ-query:

```csharp
// Veilige variant — geen raw SQL nodig
var messages = await db.Messages
    .Where(m => m.Recipient == user &&
                (m.Subject.Contains(q) || m.Body.Contains(q)))
    .OrderByDescending(m => m.SentAt)
    .ToListAsync();
```

Als raw SQL toch gewenst is, gebruik `FromSqlInterpolated`:

```csharp
var likeQ = "%" + q + "%";
var messages = await db.Messages
    .FromSqlInterpolated(
        $"SELECT * FROM \"Messages\" WHERE \"Recipient\" = {user} AND (\"Subject\" ILIKE {likeQ} OR \"Body\" ILIKE {likeQ})")
    .OrderByDescending(m => m.SentAt)
    .ToListAsync();
```

---

### Bevinding 2 — Ontbrekende authenticatie: identiteit via niet-vertrouwde HTTP-header

| Veld | Waarde |
|------|--------|
| **Ernst** | HIGH |
| **OWASP** | A07:2021 – Identification and Authentication Failures (CWE-287) |
| **OpenCRE** | https://www.opencre.org/cre/633-428 |
| **Vertrouwensgraad** | HIGH — directe bevestiging |
| **Bestand** | `app/Program.cs` |
| **Regels** | 147–150 |

#### Beschrijving

De identiteit van de gebruiker wordt uitsluitend bepaald door de waarde van de HTTP-header `X-User`, die de client volledig vrij kan instellen. Er is geen enkel mechanisme (wachtwoord, token, sessie, cookie) dat de geclaimde identiteit verifieert.

#### Kwetsbare code

```csharp
// Program.cs, regels 147–150
static string CurrentUser(HttpRequest request)
{
    return request.Headers["X-User"].ToString();
}
```

```javascript
// api.js, regels 13–14
async function call(method, path, body) {
    const headers = { 'X-User': currentUser() || '' };
```

```javascript
// api.js, regels 5–7 — naam opgeslagen in localStorage zonder verificatie
export function currentUser() {
    return localStorage.getItem(USER_KEY);
}
```

#### Waarom het probleem bestaat

De `X-User`-header is een eenvoudig tekstveld dat door elke HTTP-client vrijelijk kan worden ingesteld. Er is geen sessietoken, geen JWT, geen cookie met HMAC-handtekening. De naam wordt opgeslagen in `localStorage` en bij elke API-aanroep meegestuurd, zonder enige servervalidatie.

#### Aanvalsscenario

Een aanvaller stuurt:
```
GET /api/messages HTTP/1.1
X-User: Anna
```

De API retourneert alle berichten in de inbox van Anna, zonder enige verificatie dat de aanvrager werkelijk Anna is.

#### Impact

Volledige impersonatie van elke gebruiker. Een aanvaller kan alle berichten lezen, berichten versturen namens andere gebruikers, en berichten verwijderen voor alle gebruikers in het systeem.

#### Aanbevolen oplossing

Implementeer een echte authenticatiemechanisme:

- **Minimaal**: JWT-tokens uitgedeeld na wachtwoordverificatie, gevalideerd server-side via `app.UseAuthentication()` en `[Authorize]`
- **Aanbevolen**: OpenID Connect via een externe identity provider (Azure AD, Keycloak, Auth0)
- Verwijder de `X-User`-header-logica en vervang door `HttpContext.User.Identity.Name`

---

### Bevinding 3 — Ontbrekende autorisatie op `/api/messages/{id}` en DELETE (IDOR)

| Veld | Waarde |
|------|--------|
| **Ernst** | HIGH |
| **OWASP** | A01:2021 – Broken Access Control / IDOR (CWE-862) |
| **OpenCRE** | https://www.opencre.org/cre/724-770 |
| **Vertrouwensgraad** | HIGH — directe bevestiging |
| **Bestand** | `app/Program.cs` |
| **Regels** | 109–113 en 132–143 |

#### Beschrijving

Het endpoint `GET /api/messages/{id}` retourneert elk bericht op basis van zijn numeriek ID zonder te controleren of de aanvrager de geadresseerde of afzender is. Evenzo verwijdert `DELETE /api/messages/{id}` elk bericht zonder eigendomscontrole.

#### Kwetsbare code

```csharp
// Program.cs, regels 109–113 — geen user-check, geen HttpRequest parameter
api.MapGet("/messages/{id:int}", async (int id, UnbreakableContext db) =>
{
    var message = await db.Messages.FindAsync(id);
    return message == null ? Results.NotFound() : Results.Ok(message);
});

// Program.cs, regels 132–143 — geen user-check, geen HttpRequest parameter
api.MapDelete("/messages/{id:int}", async (int id, UnbreakableContext db) =>
{
    var message = await db.Messages.FindAsync(id);
    if (message == null) return Results.NotFound();
    db.Messages.Remove(message);
    await db.SaveChangesAsync();
    return Results.NoContent();
});
```

Merk op: de `HttpRequest`-parameter ontbreekt volledig in beide handlers, waardoor `CurrentUser()` niet eens opgeroepen kan worden.

#### Aanvalsscenario (IDOR — Insecure Direct Object Reference)

Een aanvaller is ingelogd als "Chris" en stuurt:
```
GET /api/messages/1
```
Hij ontvangt het privébericht van Anna, ook al is hij niet de bedoelde ontvanger. Door sequentieel IDs te enumereren (`/api/messages/1`, `/api/messages/2`, ...) leest hij alle berichten in de database uit.

#### Impact

Horizontale privilege-escalatie: elke (ogenschijnlijk) ingelogde gebruiker kan alle berichten van alle andere gebruikers lezen en verwijderen. Volledige schending van vertrouwelijkheid en integriteit van alle berichten.

#### Aanbevolen oplossing

Voeg een eigendomscontrole toe in beide handlers:

```csharp
api.MapGet("/messages/{id:int}", async (int id, HttpRequest request, UnbreakableContext db) =>
{
    var user = CurrentUser(request);
    if (string.IsNullOrEmpty(user)) return Results.Unauthorized();

    var message = await db.Messages.FindAsync(id);
    if (message == null) return Results.NotFound();
    if (message.Recipient != user && message.Sender != user) return Results.Forbid();

    return Results.Ok(message);
});
```

---

### Bevinding 4 — Geheimen in broncodebestanden (hardcoded credentials)

| Veld | Waarde |
|------|--------|
| **Ernst** | MEDIUM |
| **OWASP** | A02:2021 / A05:2021 – Security Misconfiguration (CWE-798) |
| **OpenCRE** | https://www.opencre.org/cre/126-668 |
| **Vertrouwensgraad** | HIGH — geheimen zijn letterlijk aanwezig in de bestanden |
| **Bestanden** | `app/appsettings.json` regels 11, 15 en `docker-compose.yml` regels 7, 17 |

#### Beschrijving

Het databasewachtwoord en een API-sleutel staan hardcoded in configuratiebestanden die in de Git-repository zijn opgeslagen.

#### Kwetsbare code

```json
// app/appsettings.json, regels 10–16
"ConnectionStrings": {
  "Unbreakable": "Host=db;Port=5432;Database=unbreakable;Username=unbreakable;Password=Unbr3akable!2026"
},
"Notifications": {
  "ApiKey": "ubk_live_7Hq2Lx9Vt4Rm8Pz3Wc6Ny1Bf5Kd0Js"
}
```

```yaml
# docker-compose.yml, regels 7, 17
ConnectionStrings__Unbreakable: "Host=db;Port=5432;...;Password=Unbr3akable!2026"
POSTGRES_PASSWORD: Unbr3akable!2026
```

#### Waarom het probleem bestaat

De geheimen worden rechtstreeks in de configuratiebestanden ingeschreven en staan in de Git-repository. Iedereen met leestoegang tot de repository heeft daarmee toegang tot de database en externe API.

> Opmerking: Deze bevindingen zijn reeds gedocumenteerd in `docs/accepted-findings.md` en tijdelijk geaccepteerd voor de labomgeving. Ze zijn toch opgenomen om een volledig beeld te geven.

#### Impact

Bij leestoegang tot de repository: directe toegang tot de PostgreSQL-database met alle privéberichten. Misbruik van de externe notificatie-API. Bij hergebruik van credentials op andere systemen wordt het aanvalsoppervlak vergroot.

#### Aanbevolen oplossing

- Gebruik omgevingsvariabelen of een secrets manager (HashiCorp Vault, Azure Key Vault, Docker Secrets)
- Voeg `appsettings.json` toe aan `.gitignore` en gebruik een `appsettings.Development.json` (lokaal, niet in Git)
- Roteer alle credentials die ooit in Git hebben gestaan
- Activeer de reeds aanwezige Gitleaks-configuratie (`.gitleaks.toml`) als pre-commit hook

---

### Bevinding 5 — `UseDeveloperExceptionPage()` actief in alle omgevingen

| Veld | Waarde |
|------|--------|
| **Ernst** | LOW |
| **OWASP** | A05:2021 – Security Misconfiguration (CWE-209) |
| **OpenCRE** | https://www.opencre.org/cre/233-748 |
| **Vertrouwensgraad** | HIGH — directe bevestiging |
| **Bestand** | `app/Program.cs` |
| **Regel** | 39 |

#### Beschrijving

`app.UseDeveloperExceptionPage()` wordt onvoorwaardelijk aangeroepen voor alle omgevingen. Dit toont gedetailleerde stack traces, interne uitzonderingen en soms configuratiewaarden bij een serverfout.

#### Kwetsbare code

```csharp
// Program.cs, regel 39 — geen omgevingsconditie
app.UseDeveloperExceptionPage();
```

#### Aanvalsscenario

Een aanvaller triggert een serverfout via malformed input. De response bevat volledige stack traces met bestandspaden, regelnummers, interne uitzonderingsberichten en namespaces die de architectuur onthullen.

#### Impact

Informatielekken die aanvallers helpen bij het in kaart brengen van de applicatiestructuur. Verhoogt de effectiviteit van andere aanvallen. Directe impact is beperkt.

#### Aanbevolen oplossing

```csharp
// Program.cs — vervang regel 39 door:
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/error");
}
```

---

## Positieve observaties

De volgende beveiligingsmaatregelen zijn aanwezig en correct geïmplementeerd:

- **Output encoding in de frontend**: `app.js` implementeert een correcte `escapeHtml()`-functie (regels 7–14) die bij vrijwel alle DOM-renders wordt gebruikt.
- **Berichtinhoud via `textContent`**: De berichttekst wordt via `textContent` (niet `innerHTML`) ingeschreven (app.js regel 157), wat stored XSS elimineert voor de berichtinhoud.
- **EF Core LINQ voor de meeste queries**: Inbox, export en gebruikerslijst gebruiken geparameteriseerde LINQ-queries die SQL-injectie verhinderen.
- **Authenticatiecontrole op meeste endpoints**: Endpoints controleren of `X-User` niet leeg is en geven een 401 terug.
- **Gitleaks geconfigureerd**: `.gitleaks.toml` is aanwezig voor secret scanning.
- **Inputvalidatie op model**: `Message.cs` gebruikt `[StringLength]` en `[Required]`-annotaties.

### Bijkomende observatie (buiten top 5)

Regel 101 in `app.js` gebruikt `$('#searched').html('Je zocht naar: ' + q)` waarbij `q` de ruwe waarde is van `params.get('q')` — niet HTML-escaped. Dit is een **reflected XSS**-risico in de frontend: een aanvaller kan een link construeren met een kwaadaardige `q`-parameter die JavaScript uitvoert in de browser van het slachtoffer. Dit werd niet opgenomen in de top 5 omdat het een pure client-side aanval is (de q-waarde komt via de hash-URL, niet via de server), maar het verdient aandacht.

---

## Samenvatting

| Ernst | Aantal |
|-------|--------|
| CRITICAL | 1 |
| HIGH | 2 |
| MEDIUM | 1 |
| LOW | 1 |
| **Totaal** | **5** |

---

## Beperkingen van de analyse

- **Geen dynamische analyse**: Deze review is uitsluitend gebaseerd op statische code-analyse. Er zijn geen aanvallen uitgevoerd op de live server.
- **Geen SCA**: Er is geen Software Composition Analysis uitgevoerd. De geaccepteerde bevinding voor `Newtonsoft.Json 12.0.1` (GHSA-5crp-9r3c-p9vr) valt buiten de scope.
- **Geen infrastructuuranalyse**: SSH-configuratie, firewallregels en netwerksegmentatie zijn niet geanalyseerd.
- **Geen runtime-configuratie**: Omgevingsvariabelen die `appsettings.json` overschrijven zijn niet geanalyseerd.
- **Semgrep-vergelijking uitgesteld**: Per instructie wordt de vergelijking met Semgrep-bevindingen apart uitgevoerd.
- **Geaccepteerde bevindingen**: Bevinding 4 (hardcoded credentials) is reeds gedocumenteerd in `docs/accepted-findings.md` en tijdelijk geaccepteerd.
