# Vergelijking AI-securityreview en Semgrep SAST

**Project:** `yorbengeerinck/websec-unbreakable-g05`  
**Datum oorspronkelijke vergelijking:** 10 oktober 2026  
**Laatste documentatie-update:** 12 oktober 2026  
**Branch Semgrep:** `dev`  
**Doel:** Stap 4 van opdracht 2 — maximaal vijf AI-bevindingen vergelijken met de SAST-tool.

## 1. Methode en bronnen

- **Semgrep (oorspronkelijke vergelijking):** GitHub Actions-job `sast-semgrep`, Semgrep 1.180.0, commando `semgrep scan --config auto --error`. Er werden 22 Git-tracked bestanden gescand met 288 regels. De scan leverde 13 meldingen op en de job eindigde met exitcode 1.
- **AI-agent:** Google Antigravity, Claude Sonnet 4.6 (Thinking), met de OWASP Secure Agent Playbook-play `code-review-security` en de bijbehorende skill. De agent voerde een statische review van de applicatiecode uit en rapporteerde vijf bevindingen.
- **Bronbestanden:** `docs/semgrep-bevindingen.md`, `docs/ai-security-review.md` en de GitHub Actions-logs.

De vergelijking gaat over **wat in deze specifieke oorspronkelijke Semgrep-run gerapporteerd is**. Een niet-gerapporteerde kwetsbaarheid betekent niet dat Semgrep die onder alle configuraties onmogelijk kan vinden. De AI-review is evenmin een dynamische penetratietest.

De oorspronkelijke scan wordt gebruikt als vergelijkingsbasis. Latere scans, na onze beveiligingsverbeteringen, worden afzonderlijk besproken.

## 2. De vijf AI-bevindingen vergeleken

| # | AI-bevinding | AI-ernst | Oorspronkelijke Semgrep-run | Beoordeling |
|---|---|---|---|---|
| 1 | SQL-injectie in `app/Program.cs` (zoekendpoint, `FromSqlRaw`) | Critical | Niet gerapporteerd | **AI-only**; code toont SQL-stringconcatenatie met gebruikersinvoer. Exacte impact vereist verificatie. |
| 2 | Identiteit te vervalsen via vrij instelbare `X-User`-header | High | Niet gerapporteerd | **AI-only**; geen echte server-side authenticatie zichtbaar in de onderzochte code. |
| 3 | Ontbrekende eigendomscontrole bij `GET` en `DELETE /api/messages/{id}` (IDOR) | High | Niet gerapporteerd | **AI-only**; endpoints zoeken op ID zonder autorisatiecontrole. |
| 4 | Hardcoded databasewachtwoord en API-sleutel in configuratie | Medium | Niet gerapporteerd door **Semgrep SAST** | **AI-only binnen deze SAST-vergelijking**; secret scanning met Gitleaks is een afzonderlijke pipelinestap. De bevinding is als labbaseline gedocumenteerd in `docs/accepted-findings.md`. |
| 5 | `UseDeveloperExceptionPage()` onvoorwaardelijk actief | Low | Wel gerapporteerd: 1 melding | **Overlap**; beide tools signaleren mogelijk lekken van interne foutinformatie. |

**Samenvatting:** vier van de vijf AI-bevindingen stonden niet in de oorspronkelijke Semgrep-SAST-log; één kwam overeen met Semgrep.

Dit zegt iets over deze configuratie en scan, niet over de algemene nauwkeurigheid van beide tools.

## 3. Wat Semgrep wel vond en de AI-top vijf niet

| Semgrep-groep | Aantal meldingen | Locatie | Interpretatie |
|---|---:|---|---|
| Wijzigbare GitHub Actions-tags (`@v4`, `@v5`, enz.) | 7 | `.github/workflows/security.yml` | Supply-chain-hardening: acties vastzetten op gecontroleerde commit-SHA's. Niet opgenomen in de AI-top vijf. |
| Ontbrekende expliciete niet-rootgebruiker | 1 | `app/Dockerfile` | Container-hardeningprobleem. Niet opgenomen in de AI-top vijf. |
| Developer exception page | 1 | `app/Program.cs` | Komt overeen met AI-bevinding 5. |
| Handmatige HTML-escaping met `replaceAll()` | 4 | `app/wwwroot/js/app.js` | Waarschuwing over zelfgeschreven escaping. Vier meldingen zijn niet noodzakelijk vier afzonderlijke kwetsbaarheden. |

**Totaal oorspronkelijke vergelijking:** 13 Semgrep-meldingen verdeeld over vier soorten problemen.

De AI-agent onderzocht vooral concrete kwetsbaarheden in applicatielogica, terwijl Semgrep ook configuratie- en hardeningregels activeerde.

## 4. Kwaliteitscontrole: mogelijke onjuistheden en beperkingen

### SQL-injectie: kwetsbaarheid aannemelijk, demonstratie niet bewezen

De AI identificeerde het risicovolle patroon waarbij `q` en de gebruikersnaam in een SQL-string worden samengevoegd en vervolgens aan `FromSqlRaw` worden doorgegeven.

**De specifieke voorbeeldpayload en de conclusie dat deze meteen alle berichten teruggeeft, zijn niet aangetoond** door de getoonde SQL.

We nemen daarom de codebevinding over, maar niet de onbewezen exploitclaim of verdergaande database-impact zonder aparte verificatie.

### HTML-escaping: beoordeling en latere verbeteringen

Semgrep signaleerde handmatig escapen via `replaceAll()`.

De bestaande functie `escapeHtml()` vervangt de speciale HTML-tekens `&`, `<`, `>`, `"` en `'` door HTML-entiteiten.

Deze functie is geschikt voor de onderzochte gewone HTML-tekstcontexten, maar vormt geen universele bescherming voor alle mogelijke XSS-contexten.

Tijdens de aanvullende codebeoordeling hebben we drie verbeteringen aangebracht in `app/wwwroot/js/app.js`:

**1. Zoekterm als tekst weergeven**

Oorspronkelijk:

```javascript
window.jQuery('#searched').html('Je zocht naar: ' + q);
```

Aangepast:

```javascript
window.jQuery('#searched').text('Je zocht naar: ' + q);
```

Hierdoor wordt de zoekterm niet meer als HTML geïnterpreteerd.

**2. Onderwerp in de inbox escapen**

Oorspronkelijk:

```javascript
${m.subject}
```

Aangepast:

```javascript
${escapeHtml(m.subject)}
```

**3. Onderwerp op de berichtdetailpagina escapen**

Ook hier werd `${m.subject}` vervangen door `${escapeHtml(m.subject)}`.

**Beoordeling na de aanpassingen:**

De vier INFO-meldingen over handmatige HTML-escaping blijven aanwezig, maar de geïdentificeerde onveilige toepassingen zijn aangepast.

De resterende meldingen zijn daarom bewust beoordeeld en gedocumenteerd in `docs/accepted-findings.md`.

Dit is geen garantie dat de volledige applicatie vrij is van XSS.

### Extra AI-observatie buiten de top vijf

De AI vermeldde in een aanvullende opmerking een mogelijk DOM-XSS-probleem bij:

```javascript
$('#searched').html('Je zocht naar: ' + q)
```

Deze observatie was **geen zesde officiële AI-bevinding**, aangezien de opdracht maximaal vijf bevindingen vraagt.

De code is ondertussen aangepast naar `.text()`.

De oorspronkelijke bereikbaarheid en impact zijn niet afzonderlijk met een dynamische test aangetoond.

### Hardcoded geheimen

Dat Semgrep SAST deze geheimen niet meldde, betekent niet dat de volledige pipeline ze niet detecteert.

Gitleaks is hiervoor de specifieke secret-scanningtool.

De bestaande labwaarden zijn gedocumenteerd in `docs/accepted-findings.md` en worden tijdelijk geaccepteerd binnen de onderwijsomgeving.

### Overige beperkingen

- Geen dynamische exploitatie of live-serveraanvallen uitgevoerd tijdens deze vergelijking.
- De oorspronkelijke Semgrep-run was beperkt tot de gebruikte `--config auto`-regels en gescande bestanden.
- De AI-bevindingen zijn door een taalmodel gegenereerd en moeten tegen de broncode worden gecontroleerd.
- De ernstclassificaties van de AI zijn niet automatisch gelijk aan de severities die de Semgrep-regels gebruiken.

## 5. Latere verbeteringen aan de securitypipeline

Na de oorspronkelijke vergelijking hebben we verschillende problemen opgelost.

### Docker-container als niet-rootgebruiker

In `app/Dockerfile` werd toegevoegd:

```dockerfile
USER $APP_UID
```

Hierdoor draait de applicatie met de ingebouwde niet-rootgebruiker van de .NET-containerimage.

De oorspronkelijke Semgrep ERROR-melding verdween.

### Developer exception page beperken

In `app/Program.cs` werd de foutpagina beperkt tot de ontwikkelomgeving:

```csharp
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
```

Hierdoor verdween de betreffende Semgrep-waarschuwing.

### GitHub Actions vastzetten op commit-SHA's

De gebruikte GitHub Actions werden vastgezet op volledige commit-SHA's in plaats van wijzigbare versietags.

Hierdoor verdwenen de waarschuwingen over wijzigbare action-referenties.

### Eigen Semgrep-baselinecontrole

We hebben `scripts/check-semgrep.py` toegevoegd.

Dit script vergelijkt `semgrep-results.json` met `.semgrep-baseline.json`.

Nieuwe bevindingen met severity `ERROR`, `HIGH` of `CRITICAL` blokkeren de pipeline wanneer ze niet in de goedgekeurde baseline staan.

De baseline bevat momenteel:

```json
{
  "accepted_findings": []
}
```

Er zijn dus geen technische Semgrep-uitzonderingen ingesteld.

De vier resterende INFO-meldingen worden wel gerapporteerd, maar blokkeren niet automatisch.

## 6. Resultaat na de beveiligingsverbeteringen

De laatste gecontroleerde Semgrep-run rapporteerde:

```text
Semgrep: 4 findings; 4 not in reviewed baseline; 0 new blocking findings.
PASS: No new high-severity findings.
```

De vier resterende meldingen hebben severity INFO en verwijzen naar handmatige HTML-escaping.

De oorspronkelijke blokkerende Docker-bevinding en de waarschuwingen over GitHub Actions en de developer exception page zijn verdwenen.

**Belangrijk:** dit is een latere scan en geen vervanging van de oorspronkelijke Semgrep-run die voor de vergelijking met de AI-agent werd gebruikt.

## 7. Conclusie

De combinatie van **Semgrep en een AI-code-review** leverde in dit project aanvullende inzichten op.

De AI vond belangrijke logische beveiligingsproblemen die in de oorspronkelijke Semgrep-run niet werden gerapporteerd, waaronder authenticatie- en autorisatieproblemen.

Semgrep wees juist op GitHub Actions-supply-chain-risico's, containerinstellingen en handmatige HTML-escaping die niet in de vijf geselecteerde AI-bevindingen stonden.

De resultaten hebben ook geleid tot concrete verbeteringen aan de applicatie en de pipeline.

**Huidige beslissing:** beide analyses blijven onderdeel van de beveiligingsdocumentatie. De pipeline gebruikt inmiddels een eigen controle die nieuwe ernstige Semgrep-bevindingen blokkeert. Bestaande en bewust geaccepteerde risico's zijn afzonderlijk gedocumenteerd.

Een geslaagde pipeline betekent niet dat alle applicatiekwetsbaarheden zijn opgelost.

## 8. Herkomst van de Semgrep-resultaten

De vergelijking tussen de AI-securityreview en Semgrep is gebaseerd op de **oorspronkelijke Semgrep-resultaten van vóór de beveiligingsaanpassingen**.

Voor de vergelijking werden de bevindingen uit de GitHub Actions-logs gebruikt.

De bijbehorende JSON-artifacts kunnen als aanvullende bron dienen voor exacte regelnummers, Semgrep-regel-ID's en ernstclassificaties.

De latere Semgrep-resultaten worden alleen gebruikt om aan te tonen welke problemen na de verbeteringen nog aanwezig waren.

## 9. Bewijsstukken

- `docs/ai-security-review.md` — AI-rapport met OWASP-play, vijf bevindingen en beperkingen.
- `docs/semgrep-bevindingen.md` — uitsplitsing van de oorspronkelijke 13 Semgrep-meldingen.
- `docs/accepted-findings.md` — onderbouwing van geaccepteerde baselinebevindingen.
- `scripts/check-semgrep.py` — controle op nieuwe ernstige Semgrep-bevindingen.
- `.semgrep-baseline.json` — technische baseline voor goedgekeurde Semgrep-uitzonderingen.
- GitHub Actions — oorspronkelijke `dev`-run van `sast-semgrep`; exacte runlink nog toevoegen.
- [Semgrep-artifact van de latere scan](https://github.com/yorbengeerinck/websec-unbreakable-g05/actions/runs/38188987372/artifacts/11720816298) — resultaten na de beveiligingsverbeteringen.