# Cybersecurity Advanced — Opdracht 2: Pipeline, AI-agent en WAF

**Project:** Unbreakable — groep 05  
**Repository:** https://github.com/yorbengeerinck/websec-unbreakable-g05  
**Status van dit verslag:** werkdocument, bijgewerkt op 10 oktober 2026. Nog niet alle onderdelen zijn afgerond of opnieuw gevalideerd.

## 1. Doel en aanpak

Het doel van deze opdracht is de beveiliging van de Unbreakable-webapplicatie te verbeteren en te controleren met een geautomatiseerde securitypipeline, een AI-securityreview en een Web Application Firewall (WAF). De opdracht omvat ook automatische deployment na een geslaagde pipeline en een bijgewerkt threat model.

Dit verslag documenteert stappen 2 tot en met 6 van de opdracht. Waar nog geen testbewijs bestaat, wordt dat expliciet vermeld.

## 2. Securitypipeline (stap 2)

### 2.1 Opzet

De GitHub Actions-workflow staat in `.github/workflows/security.yml`. De pipeline omvat:

- **SCA:** controle op kwetsbare .NET-afhankelijkheden.
- **SAST:** statische codeanalyse met Semgrep.
- **Secret scanning:** controle met Gitleaks.
- **Deployment:** afzonderlijke job die afhankelijk is van de securitycontroles en bij een push naar `main` de applicatie via een self-hosted runner uitrolt.

De workflow is tijdens het werk aangepast om ook op de ontwikkelbranch `dev` te draaien. Voor de SAST-job is de verouderde Semgrep-action vervangen door installatie via Python en het commando `semgrep scan --config auto --error`.

### 2.2 Huidige bevindingen en beperkingen

Een Semgrep-run met versie 1.180.0 onderzocht 22 bestanden en rapporteerde **13 blokkerende bevindingen**:

| Categorie | Aantal | Locatie / toelichting |
|---|---:|---|
| GitHub Actions met versie-tags in plaats van vaste commit-SHA's | 7 | `.github/workflows/security.yml` |
| Docker-container zonder expliciete niet-rootgebruiker | 1 | `app/Dockerfile` |
| Developer exception page | 1 | `app/Program.cs` |
| Handmatige HTML-escaping met `replaceAll()` | 4 | `app/wwwroot/js/app.js` |
| **Totaal** | **13** | |

Deze bevindingen zijn **niet automatisch allemaal kritieke kwetsbaarheden**. Vooral de vier meldingen rond HTML-escaping vereisen contextbeoordeling. De huidige opdracht vereist dat *nieuwe kritieke* bevindingen de pipeline blokkeren. De huidige instelling `--error` blokkeert echter op alle gerapporteerde bevindingen. **De baseline-/blokkeerlogica moet daarom nog worden aangepast en getest.**

Er bestaat al een document `docs/accepted-findings.md` met eerder geaccepteerde baselinebevindingen en motivering. De bestaande SCA-controle bevat onder andere een geaccepteerde kwetsbare versie van `Newtonsoft.Json 12.0.1`. De Gitleaks-configuratie gebruikt `.gitleaks.toml`. De correcte werking van de volledige pipeline na de recente Semgrep-wijziging moet nog worden aangetoond.

### 2.3 DSOMM-koppeling

De controles sluiten aan bij de DSOMM-activiteiten voor een veilige build- en ontwikkelstraat: dependencycontrole (SCA), statische analyse (SAST) en detectie van geheimen in de broncode. In de definitieve versie moeten de exacte gebruikte DSOMM-activiteiten en hun implementatie in de workflow worden gecontroleerd en onderbouwd.

### 2.4 Nog aan te tonen

- Nieuwe **kritieke** bevinding leidt aantoonbaar tot een mislukte pipeline.
- Eerder beoordeelde baselinebevindingen worden correct behandeld, zonder nieuwe problemen blind te negeren.
- Alle scans en de deploymentjob werken in de definitieve workflow.
- Link naar een **recente, succesvolle** GitHub Actions-run toevoegen.

### SAST – Detectie en oplossing van een Docker-beveiligingsrisico

Na het implementeren van de nieuwe Semgrep-controle hebben we de security pipeline uitgevoerd op de `dev`-branch.

**1. Detectie**

Semgrep analyseerde de broncode en rapporteerde 14 bevindingen, waaronder één met ernstniveau `ERROR`.

- **Regel:** `dockerfile.security.missing-user-entrypoint`
- **Bestand:** `app/Dockerfile`
- **Locatie:** regel 12
- **Risico:** De applicatie kon standaard met rootrechten binnen de Docker-container draaien.

**2. Automatische blokkering**

Ons Python-script (`scripts/check-semgrep.py`) vergeleek de scanresultaten met de baseline.

Omdat de Docker-bevinding nog niet in de baseline was opgenomen en de ernst `ERROR` had, werd de pipeline automatisch geblokkeerd met exitcode 1.

Hiermee hebben we aangetoond dat de SAST-controle nieuwe ernstige bevindingen kan tegenhouden.

**3. Beveiligingsverbetering**

In `app/Dockerfile` hebben we de volgende instructie toegevoegd:

```dockerfile
USER $APP_UID
```

De officiële .NET-containerimage bevat een ingebouwde niet-rootgebruiker. Met deze instructie draait de applicatie onder die gebruiker in plaats van standaard als root.

Dit volgt het *principle of least privilege*: processen krijgen niet meer rechten dan noodzakelijk.

**4. Verwacht resultaat**

Na deze aanpassing verwachten we dat Semgrep de melding `dockerfile.security.missing-user-entrypoint` niet langer rapporteert.

De nieuwe scan en een succesvolle deployment moeten dit nog bevestigen.

**Conclusie**

Deze test toont aan hoe onze security pipeline wordt gebruikt om een beveiligingsrisico te detecteren, een deployment te blokkeren en vervolgens de applicatie te verbeteren voordat de wijziging wordt uitgerold.

## 3. Automatische deployment (stap 3)

De applicatie is eerder op de server uitgerold via Docker Compose. De workflow gebruikt een self-hosted runner voor groep 05. De deploymentjob is bedoeld om alleen na geslaagde controles en bij een push naar `main` te draaien.

Een eerder succesvolle run is beschikbaar op:

https://github.com/yorbengeerinck/websec-unbreakable-g05/actions/runs/37529522116

**Let op:** deze run bewijst niet dat de recent aangepaste Semgrep-job en de uiteindelijke WAF-configuratie correct werken. Na het herstellen van de pipeline moet een nieuwe succesvolle run worden gelinkt en moet worden getest dat een push naar `main` de applicatie daadwerkelijk bijwerkt.

## 4. AI-agent en vergelijking met SAST (stap 4)

### 4.1 Gebruikte AI-agent en methode

Voor de AI-securityreview werd **Google Antigravity** gebruikt, volgens het aangeleverde AI-rapport met **Claude Sonnet 4.6 (Thinking)** als model. De agent kreeg de instructie de officiële OWASP Secure Agent Playbook-play `code-review-security` te volgen:

`secure-agent-playbook/plugins/code-security-skills/plays/code-review-security.md`

Ook de bijbehorende skill werd opgegeven. De analyse was statisch: er zijn volgens het rapport geen aanvallen op de live server uitgevoerd. De agent mocht geen applicatiecode wijzigen en maximaal vijf bevindingen rapporteren. Het resultaat staat in `docs/ai-security-review.md`.

### 4.2 Vijf AI-bevindingen

| Nr. | AI-bevinding | Ernst volgens AI | Bestand |
|---|---|---|---|
| 1 | SQL-injectie door stringopbouw en `FromSqlRaw` | Critical | `app/Program.cs` (85–89) |
| 2 | Identiteitsvervalsing via vrij instelbare `X-User`-header | High | `app/Program.cs` (147–150) |
| 3 | Ontbrekende eigendomscontrole op GET/DELETE van berichten (IDOR) | High | `app/Program.cs` (109–113, 132–143) |
| 4 | Hardcoded databasecredentials en API-sleutel | Medium | `app/appsettings.json`, `docker-compose.yml` |
| 5 | Onvoorwaardelijk gebruik van `UseDeveloperExceptionPage()` | Low | `app/Program.cs` (39) |

De eerste drie bevindingen komen rechtstreeks voort uit de getoonde backendlogica en zijn belangrijk om te remediëren. De vierde bevinding is al als tijdelijk geaccepteerde labbevinding beschreven. Dat betekent niet dat zulke geheimen veilig zijn voor productiegebruik.

### 4.3 Vergelijking AI versus Semgrep

| Bevinding | AI | Semgrep in aangeleverde run | Beoordeling |
|---|---|---|---|
| SQL-injectie (`FromSqlRaw`) | Ja | Niet gemeld | AI signaleert een relevant probleem dat deze Semgrep-run miste. |
| Vertrouwen op `X-User` | Ja | Niet gemeld | AI signaleert ontbrekende echte authenticatie. |
| IDOR bij berichten | Ja | Niet gemeld | AI signaleert ontbrekende objectautorisatie. |
| Hardcoded credentials | Ja | Niet gemeld | Reeds bekende labbevinding; secret scanning moet apart worden beoordeeld. |
| Developer exception page | Ja | Ja | Overlap tussen AI en SAST. |
| Mutable GitHub Actions-tags | Nee | Ja (7) | Alleen door Semgrep in deze vergelijking. |
| Geen niet-rootgebruiker in Dockerfile | Nee | Ja (1) | Alleen door Semgrep in deze vergelijking. |
| Handmatige HTML-escaping | Nee als top-5 | Ja (4) | Nader onderzoeken: een patroonmelding bewijst op zichzelf geen XSS. |

**Interpretatie:** de twee methoden vullen elkaar aan. De AI-review redeneert over authenticatie, autorisatie en gegevensstromen, terwijl Semgrep in deze configuratie vooral concrete code- en configuratiepatronen rapporteerde. Een afwezige Semgrep-melding betekent niet dat het probleem veilig is.

### 4.4 Kritische beoordeling van de AI-uitvoer

Het AI-rapport bevat ook claims die niet zonder verificatie mogen worden overgenomen:

- Het aangeleverde SQL-injectievoorbeeld toont niet overtuigend aan dat de specifieke invoer daadwerkelijk *alle* berichten retourneert. De onveilige stringopbouw blijft wel een duidelijke kwetsbaarheid; het precieze aanvalseffect vraagt aparte validatie.
- Het rapport noemt buiten de top vijf een mogelijk DOM-/reflected-XSS-risico bij `$('#searched').html('Je zocht naar: ' + q)` in `app.js`. Dit moet apart gecontroleerd worden; het is geen zesde bevestigde top-5-bevinding.
- De ernst en praktische impact van hardcoded credentials hangen af van de bereikbaarheid van de betrokken diensten en of de sleutels echt actief zijn.

### 4.5 Conclusie stap 4

De AI-agent heeft vijf bevindingen gerapporteerd volgens de opgegeven OWASP-play. De vergelijking met de beschikbare Semgrep-run is uitgevoerd. De AI-review voegt vooral contextuele bevindingen over SQL-injectie, authenticatie en autorisatie toe; Semgrep signaleert aanvullende workflow-, container- en frontendpatronen. Verdere verificatie is nodig voordat alle claims als bewezen exploit worden beschouwd.

## 5. BunkerWeb WAF en HTTPS (stap 5) — nog uit te voeren

Dit onderdeel mag pas als afgerond worden beschouwd nadat de pipeline betrouwbaar werkt. De vereiste eindtoestand is:

- BunkerWeb staat vóór de webapplicatie als WAF/reverse proxy.
- HTTPS werkt met een geldig Let's Encrypt-certificaat.
- De applicatie is uitsluitend via de WAF bereikbaar.
- Directe toegang via poort 8080 is afgesloten.
- Een gecontroleerde test toont dat de WAF een aanval blokkeert.
- Een alternatieve formuliertest toont het verwachte gedrag aan.

**Bewijs nog toe te voegen:** relevante configuratie, screenshots of logs van HTTPS, netwerktoegang en WAF-blokkering. Er is op dit moment geen bewijs opgenomen dat deze configuratie al gerealiseerd is.

## 6. Bijgewerkt threat model (stap 6) — nog uit te voeren

Het bestaande model staat in `docs/threat-model.md`. Het moet worden bijgewerkt met de GitHub Actions-pipeline, self-hosted runner, deploymentpad, BunkerWeb WAF en de verbinding naar de applicatie.

Het definitieve diagram moet per gegevensstroom het **protocol** en de **authenticatie** aangeven. Relevante bedreigingen, waaronder ongeautoriseerde deployment, gecompromitteerde CI-afhankelijkheden, omzeiling van de WAF en onjuist beheer van geheimen, moeten zichtbaar aan de onderdelen of stromen gekoppeld zijn.

**Status:** diagram en dreigingsanalyse moeten nog worden aangepast en beoordeeld.

## 7. AI-verantwoording

Bij deze opdracht is AI gebruikt voor de securityreview via Google Antigravity met de OWASP-play `code-review-security`, en voor ondersteuning bij het structureren van de analyse en dit verslag via ChatGPT. De AI-uitvoer wordt niet als automatisch correct beschouwd: bevindingen worden vergeleken met de daadwerkelijke broncode en de SAST-resultaten. De student blijft verantwoordelijk voor validatie, configuratie en ingediende conclusies.

## 8. Eindcontrole vóór indiening

- [ ] Baseline-/blokkeerlogica van de pipeline werkt volgens de opdracht.
- [ ] Recente geslaagde pipeline-run met link toegevoegd.
- [ ] Push naar `main` deployt automatisch en aantoonbaar.
- [x] AI-review met OWASP-play uitgevoerd en gedocumenteerd.
- [x] AI-resultaten vergeleken met de beschikbare Semgrep-run.
- [ ] BunkerWeb en Let's Encrypt HTTPS werkend.
- [ ] Rechtstreekse toegang via poort 8080 afgesloten en getest.
- [ ] WAF-blokkering en alternatieve formuliertest bewezen.
- [ ] Threat model bijgewerkt met alle vereiste stromen en bedreigingen.
- [ ] Bewijsstukken en finale AI-verantwoording nagekeken.

---

*Dit is een tussentijds verslag, geen verklaring dat de volledige opdracht al voltooid is.*
