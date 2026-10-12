# Interne documentatie – Unbreakable G05

## Doel

Dit document beschrijft de wijzigingen die we aan de oorspronkelijke applicatie en de beveiligingspipeline hebben aangebracht.

Het is bedoeld als naslagwerk voor onszelf: welke code hebben we aangepast, waarom was dat nodig en wat is het resultaat?

---

## 1. GitHub Actions-securitypipeline

**Bestand:** `.github/workflows/security.yml`

We hebben vier jobs ingericht:

| Job | Functie |
|---|---|
| `sca-dotnet` | Controleert .NET-dependencies op bekende kwetsbaarheden |
| `sast-semgrep` | Scant broncode en configuratie met Semgrep |
| `secret-gitleaks` | Controleert op mogelijke gelekte geheimen |
| `deploy` | Deployt de applicatie na geslaagde securitycontroles |

### SCA – Dependency scanning

```bash
dotnet restore ./app/Unbreakable.csproj
dotnet list ./app/Unbreakable.csproj package --vulnerable --include-transitive 2>&1 | tee sca.txt
```

Met `dotnet restore` worden de dependencies hersteld. De tweede opdracht controleert zowel directe als transitieve dependencies op bekende kwetsbaarheden.

De bestaande kwetsbaarheid in `Newtonsoft.Json 12.0.1` wordt tijdelijk uitgezonderd:

```bash
NEW=$(grep -E "High|Critical|Severe" sca.txt | grep -v "Newtonsoft.Json" || true)
```

**Waarom?**

Deze kwetsbaarheid was al aanwezig in de oorspronkelijke starter-app en is voor de labopdracht als baseline geaccepteerd.

**Beperking:** de uitzondering filtert op de pakketnaam, niet op een specifieke versie of advisory. Daardoor kan ze te ruim zijn.

### SAST – Semgrep

```bash
semgrep scan --config auto --json --output semgrep-results.json --strict
python scripts/check-semgrep.py
```

De eerste opdracht scant de code en schrijft alle resultaten naar `semgrep-results.json`.

De tweede opdracht voert onze eigen controle uit op nieuwe ernstige bevindingen.

### Secret scanning – Gitleaks

Gitleaks zoekt naar mogelijke wachtwoorden, tokens en API-sleutels.

De bestaande labwaarden zijn opgenomen in de Gitleaks-allowlist en gedocumenteerd in `docs/accepted-findings.md`.

Nieuwe niet-uitgezonderde secrets moeten door de controle worden tegengehouden.

### Automatische deployment

```yaml
deploy:
  needs: [sca-dotnet, sast-semgrep, secret-gitleaks]
```

De deployment kan pas starten nadat de drie securityjobs zijn geslaagd.

De deploy-job wordt alleen uitgevoerd bij een push naar `main` in de juiste repository.

De deployment gebeurt via een self-hosted GitHub Actions-runner op de server.

---

## 2. Eigen Semgrep-controlescript

**Bestand:** `scripts/check-semgrep.py`

We hebben een Python-script toegevoegd dat de Semgrep-resultaten vergelijkt met een goedgekeurde baseline.

### Welke meldingen blokkeren?

```python
BLOCKING_SEVERITIES = {'ERROR', 'CRITICAL', 'HIGH'}
```

Nieuwe bevindingen met deze ernst laten de pipeline falen.

INFO- en WARNING-meldingen worden gerapporteerd, maar blokkeren niet automatisch.

### Hoe herkennen we een bevinding?

```python
def identity(finding):
    return (
        finding['check_id'],
        finding['path'],
        finding['start']['line'],
        finding['end']['line'],
    )
```

We identificeren een bevinding op basis van de Semgrep-regel, het bestand en de start- en eindregel.

Als de code verschuift, kan een eerder geaccepteerde bevinding opnieuw beoordeeld moeten worden.

### Wanneer faalt de pipeline?

```python
blocking = [
    f for f in new
    if f['extra']['severity'].upper() in BLOCKING_SEVERITIES
]

if blocking:
    print('FAIL: New high-severity findings require review.')
    return 1
```

De exitcode `1` zorgt ervoor dat de GitHub Actions-job mislukt.

### Waarom is de baseline leeg?

**Bestand:** `.semgrep-baseline.json`

```json
{
  "accepted_findings": []
}
```

Er zijn momenteel geen technisch goedgekeurde Semgrep-uitzonderingen.

De vier resterende INFO-meldingen blokkeren niet. Ze zijn inhoudelijk beoordeeld en staan in `docs/accepted-findings.md`.

---

## 3. Docker-container als niet-rootgebruiker

**Bestand:** `app/Dockerfile`

### Oorspronkelijk probleem

Semgrep rapporteerde een ERROR omdat de Dockerfile geen expliciete niet-rootgebruiker instelde.

### Aanpassing

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /out ./
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Unbreakable.dll"]
```

De belangrijkste toevoeging:

```dockerfile
USER $APP_UID
```

Hiermee draait de applicatie met de ingebouwde niet-rootgebruiker van de .NET-containerimage.

**Resultaat:** de Semgrep ERROR verdween en de SAST-job slaagde.

---

## 4. Developer exception page beveiligen

**Bestand:** `app/Program.cs`

### Oorspronkelijke situatie

De developer exception page werd zonder controle op de omgeving ingeschakeld.

Hierdoor kon de applicatie gedetailleerde foutinformatie tonen buiten de ontwikkelomgeving.

### Aanpassing

```csharp
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
```

**Waarom?**

Gedetailleerde ontwikkelaarsfouten horen alleen in de ontwikkelomgeving zichtbaar te zijn.

**Resultaat:** de Semgrep-waarschuwing verdween.

---

## 5. GitHub Actions vastzetten op commit-SHA's

**Bestand:** `.github/workflows/security.yml`

### Oorspronkelijke situatie

Actions werden via versietags gebruikt:

```yaml
- uses: actions/checkout@v4
```

### Aanpassing

We gebruiken nu volledige commit-SHA's:

```yaml
- uses: actions/checkout@11d5960a326750d5838078e36cf38b85af677262 # v4.3.0
```

**Waarom?**

Een versietag kan wijzigen. Een volledige SHA verwijst naar één specifieke commit.

**Resultaat:** acht Semgrep-waarschuwingen verdwenen.

---

## 6. XSS-risico's verminderen

**Bestand:** `app/wwwroot/js/app.js`

We hebben drie toepassingen van onveilige HTML-rendering aangepast.

### 6.1 Zoekterm weergeven

**Voor:**

```javascript
window.jQuery('#searched').html('Je zocht naar: ' + q);
```

**Na:**

```javascript
window.jQuery('#searched').text('Je zocht naar: ' + q);
```

**Waarom?**

`.html()` interpreteert de inhoud als HTML. `.text()` geeft de inhoud weer als gewone tekst.

### 6.2 Onderwerp in inbox

**Voor:**

```javascript
<td><a href="#/bericht/${m.id}">${m.subject}</a></td>
```

**Na:**

```javascript
<td><a href="#/bericht/${m.id}">${escapeHtml(m.subject)}</a></td>
```

### 6.3 Onderwerp op berichtdetailpagina

**Voor:**

```javascript
<h1>${m.subject}</h1>
```

**Na:**

```javascript
<h1>${escapeHtml(m.subject)}</h1>
```

In beide gevallen wordt het berichtonderwerp ge-escaped voordat het als HTML wordt ingevoegd.

### 6.4 Bestaande escapeHtml-functie

```javascript
function escapeHtml(text) {
    return String(text)
        .replaceAll('&', '&amp;')
        .replaceAll('<', '&lt;')
        .replaceAll('>', '&gt;')
        .replaceAll('"', '&quot;')
        .replaceAll("'", '&#39;');
}
```

Deze functie zet speciale tekens om naar HTML-entiteiten.

Semgrep rapporteert nog vier INFO-meldingen over de handmatige escaping.

We hebben de functie beoordeeld en de geïdentificeerde onveilige toepassingen aangepast.

De functie is geen algemene oplossing voor alle mogelijke XSS-contexten. Waar mogelijk gebruiken we liever `.text()` of `textContent`.

---

## 7. Resultaten van Semgrep

| Bevinding | Oorspronkelijk | Na aanpassingen |
|---|---:|---:|
| Docker zonder niet-rootgebruiker | 1 ERROR | 0 |
| Wijzigbare GitHub Actions-tags | 8 WARNING | 0 |
| Developer exception page | 1 WARNING | 0 |
| Handmatige HTML-escaping | 4 INFO | 4 INFO |
| **Totaal** | **14** | **4** |

Laatste gecontroleerde resultaat:

```text
Semgrep: 4 findings; 4 not in reviewed baseline; 0 new blocking findings.
PASS: No new high-severity findings.
```

De vier resterende INFO-meldingen zijn gedocumenteerd in `docs/accepted-findings.md`.

---

## 8. AI-securityreview

**Bestanden:**

- `docs/ai-security-review.md`
- `docs/ai-vs-semgrep.md`

We hebben een AI-securityreview uitgevoerd met het OWASP Secure Agent Playbook als leidraad.

De AI-review identificeerde vijf belangrijke bevindingen:

| Bevinding | Ernst |
|---|---|
| SQL-injectie via `FromSqlRaw` | Critical |
| Identiteitsspoofing via `X-User` | High |
| Ontbrekende berichtautorisatie (IDOR) | High |
| Hardcoded databasegegevens en API-sleutel | Medium |
| Developer exception page | Low |

Deze bevindingen zijn vergeleken met de oorspronkelijke Semgrep-scan.

De developer exception page was een duidelijke overlap. Andere problemen, zoals SQL-injectie en IDOR, werden in de onderzochte Semgrep-resultaten niet gemeld.

**Belangrijk:** de AI-review en Semgrep gebruiken verschillende analysemethoden. Een bevinding die één methode mist, is daardoor niet automatisch onbestaand.

---

## 9. Nog openstaande beveiligingsproblemen

De volgende risico's zijn nog niet volledig opgelost:

- SQL-injectie in de zoekfunctionaliteit.
- Spoofing via de `X-User`-header.
- Ontbrekende autorisatie bij bepaalde berichtoperaties.
- Ruime CORS-configuratie.
- Hardcoded databasegegevens.
- De kwetsbare Newtonsoft.Json-dependency.

De risico's zijn gedocumenteerd, maar moeten bij een echte productieapplicatie worden aangepakt.

Een WAF biedt aanvullende bescherming, maar vervangt geen veilige applicatiecode.

---

## 10. Huidige stand van zaken

| Onderdeel | Status |
|---|---|
| Stap 2 – Securitypipeline | Uitgevoerd, met gedocumenteerde beperkingen |
| Stap 3 – Automatische deployment | Geconfigureerd; laatste wijzigingen op `main` nog te controleren |
| Stap 4 – AI-securityreview | Uitgevoerd |
| Stap 5 – BunkerWeb WAF + HTTPS | Nog uit te voeren |
| Stap 6 – Threat model | Wordt parallel uitgewerkt |

Een groene pipeline betekent dat de ingestelde controles slagen. Het betekent niet dat de applicatie volledig veilig is.