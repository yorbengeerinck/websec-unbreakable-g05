# Vergelijking AI-securityreview en Semgrep SAST

**Project:** `yorbengeerinck/websec-unbreakable-g05`  
**Datum:** 10 oktober 2026  
**Branch Semgrep:** `dev`  
**Doel:** stap 4 van opdracht 2 — maximaal vijf AI-bevindingen vergelijken met de SAST-tool.

## 1. Methode en bronnen

- **Semgrep:** GitHub Actions-job `sast-semgrep`, Semgrep 1.180.0, commando `semgrep scan --config auto --error`. Er zijn 22 Git-tracked bestanden gescand met 288 regels. De scan leverde 13 *blocking* meldingen op; de job eindigde daarom met exitcode 1.
- **AI-agent:** Google Antigravity, Claude Sonnet 4.6 (Thinking), met de OWASP Secure Agent Playbook-play `code-review-security` en de bijbehorende skill. De agent voerde een statische review van de applicatiecode uit en rapporteerde vijf bevindingen.
- **Bronbestanden:** `docs/semgrep-bevindingen.md` en `docs/ai-security-review.md`.

De vergelijking gaat over **wat in deze specifieke Semgrep-run gerapporteerd is**. Een niet-gerapporteerde kwetsbaarheid betekent niet dat Semgrep die onder alle configuraties onmogelijk kan vinden. De AI-review is evenmin een dynamische penetratietest.

## 2. De vijf AI-bevindingen vergeleken

| # | AI-bevinding | AI-ernst | Semgrep-run | Beoordeling |
|---|---|---|---|---|
| 1 | SQL-injectie in `app/Program.cs` (zoekendpoint, `FromSqlRaw`) | Critical | Niet gerapporteerd | **AI-only**; code toont SQL-stringconcatenatie met gebruikersinvoer. Exacte impact vereist verificatie. |
| 2 | Identiteit te vervalsen via vrij instelbare `X-User`-header | High | Niet gerapporteerd | **AI-only**; geen echte server-side authenticatie zichtbaar in de onderzochte code. |
| 3 | Ontbrekende eigendomscontrole bij `GET` en `DELETE /api/messages/{id}` (IDOR) | High | Niet gerapporteerd | **AI-only**; endpoints zoeken op ID zonder autorisatiecontrole. |
| 4 | Hardcoded databasewachtwoord en API-sleutel in configuratie | Medium | Niet gerapporteerd door **Semgrep SAST** | **AI-only binnen deze SAST-vergelijking**; secret scanning met Gitleaks is een afzonderlijke pipelinestap. De bevinding is eerder als lab-baseline gedocumenteerd in `docs/accepted-findings.md`. |
| 5 | `UseDeveloperExceptionPage()` onvoorwaardelijk actief | Low | Wel gerapporteerd: 1 melding | **Overlap**; beide tools signaleren mogelijk lekken van interne foutinformatie. |

**Samenvatting:** 4 van de 5 AI-bevindingen staan niet in de Semgrep-SAST-log; 1 komt overeen met Semgrep. Dit zegt iets over deze configuratie en scan, niet over de algemene nauwkeurigheid van beide tools.

## 3. Wat Semgrep wel vond en de AI-top vijf niet

| Semgrep-groep | Aantal meldingen | Locatie | Interpretatie |
|---|---:|---|---|
| Wijzigbare GitHub Actions-tags (`@v4`, `@v5`, enz.) | 7 | `.github/workflows/security.yml` | Supply-chain-hardening: acties vastzetten op gecontroleerde commit-SHA's. Niet opgenomen in de AI-top vijf. |
| Ontbrekende expliciete niet-rootgebruiker | 1 | `app/Dockerfile` | Potentieel container-hardeningprobleem; effectieve runtimegebruiker nog verifiëren. Niet opgenomen in de AI-top vijf. |
| Developer exception page | 1 | `app/Program.cs` | Komt overeen met AI-bevinding 5. |
| Handmatige HTML-escaping met `replaceAll()` | 4 | `app/wwwroot/js/app.js` | Waarschuwing over zelfgeschreven escaping. Vier meldingen zijn niet noodzakelijk vier kwetsbaarheden; exploitbaarheid nog niet vastgesteld. Niet opgenomen in de AI-top vijf. |

**Totaal:** 13 Semgrep-meldingen verdeeld over vier soorten problemen. De AI-agent onderzocht vooral concrete kwetsbaarheden in applicatielogica, terwijl Semgrep ook configuratie- en hardeningregels activeerde.

## 4. Kwaliteitscontrole: mogelijke onjuistheden en beperkingen

### SQL-injectie: kwetsbaarheid aannemelijk, demonstratie niet bewezen

De AI identificeerde terecht het risicovolle patroon waarbij `q` en de gebruikersnaam in een SQL-string worden samengevoegd en vervolgens aan `FromSqlRaw` worden doorgegeven. **De specifieke voorbeeldpayload en de conclusie dat deze meteen alle berichten teruggeeft, zijn echter niet aangetoond** door de getoonde SQL. We nemen daarom de codebevinding over, maar niet de onbewezen exploitclaim of verdergaande database-impact zonder aparte verificatie.

### HTML-escaping: niet automatisch een false positive

Semgrep signaleert handmatig escapen via `replaceAll()`. De AI beschrijft de `escapeHtml()`-functie als correct toegepast bij vrijwel alle renders. Beide observaties kunnen tegelijk kloppen: een handgemaakte escape-functie verdient controle, maar is niet per definitie exploiteerbaar. Daarom classificeren we deze vier meldingen voorlopig als **nader te beoordelen**, niet als vier bevestigde XSS-kwetsbaarheden of vier false positives.

### Extra AI-observatie buiten de top vijf

De AI vermeldt in een aanvullende opmerking een mogelijk DOM-XSS-probleem bij `$('#searched').html('Je zocht naar: ' + q)` in `app/wwwroot/js/app.js` (volgens het AI-rapport regel 101). Dit is **niet een zesde officiële AI-bevinding**: de opdracht vraagt maximaal vijf. De opmerking is nuttig voor vervolgcontrole, maar de bereikbaarheid en impact zijn nog niet afzonderlijk getest.

### Hardcoded geheimen

Dat Semgrep SAST deze geheimen niet meldt, is niet hetzelfde als zeggen dat de **volledige pipeline** ze niet detecteert: Gitleaks is hiervoor de specifieke secret-scanningtool. Ook is het onderscheid belangrijk tussen daadwerkelijk werkende productiesleutels en demonstratiegegevens voor de labomgeving. De aanvaarding in `docs/accepted-findings.md` moet met context en risicoafweging worden onderbouwd.

### Overige beperkingen

- Geen dynamische exploitatie of live-serveraanvallen uitgevoerd in deze vergelijking.
- De Semgrep-run was beperkt tot de gebruikte `--config auto`-regels en gescande bestanden.
- De AI-bevindingen zijn door een taalmodel gegenereerd en moeten tegen de broncode worden gecontroleerd.
- De ernstclassificaties van de AI zijn niet automatisch gelijk aan de severities die de Semgrep-regels gebruiken.

## 5. Conclusie

De combinatie van **Semgrep en een AI-code-review** levert in dit project meer dekking dan een van beide afzonderlijk. De AI vond belangrijke logische beveiligingsproblemen die in de huidige Semgrep-run niet werden gerapporteerd, waaronder authenticatie- en autorisatieproblemen. Semgrep wees juist op GitHub Actions-supply-chain-risico's, containerinstellingen en handmatige HTML-escaping die niet in de vijf geselecteerde AI-bevindingen stonden.

**Voorlopige beslissing:** behoud beide analyses in de pipeline/documentatie, behandel de gevonden risico's op basis van impact en context, en leg eventuele geaccepteerde bevindingen expliciet vast. De huidige `--error`-configuratie laat de pipeline falen op alle Semgrep-meldingen; dit moet nog worden aangepast aan de opdrachtvoorwaarde dat **nieuwe kritieke bevindingen** de pipeline blokkeren, zonder bestaande risico's stilzwijgend te negeren.

## 6. Bewijsstukken

- `docs/ai-security-review.md` — AI-rapport met OWASP-play, vijf bevindingen en beperkingen.
- `docs/semgrep-bevindingen.md` — uitsplitsing van de 13 Semgrep-meldingen.
- `docs/accepted-findings.md` — onderbouwing van geaccepteerde baselinebevindingen.
- GitHub Actions — de relevante `dev`-run van de `sast-semgrep`-job (link/nummer nog toevoegen aan het eindverslag).
