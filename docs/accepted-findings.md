# Geaccepteerde bevindingen – g05

## 1. Doel en werkwijze

Dit document beschrijft de beveiligingsbevindingen van de oorspronkelijke Unbreakable-applicatie, de bewust geaccepteerde risico's en de verbeteringen die tijdens het project zijn uitgevoerd.

De oorspronkelijke baseline dateert van **6 oktober 2026** en werd opgesteld voor de ongewijzigde starter-app.

De bevindingen worden beoordeeld op basis van hun ernst, mogelijke impact en de context van de applicatie als onderwijsproject in een labomgeving.

Een geaccepteerde bevinding betekent niet dat de kwetsbaarheid onschadelijk is. Het betekent dat het risico voorlopig bewust wordt aanvaard en gedocumenteerd.

## 2. SCA – Dependency vulnerability scanning

**DSOMM: Patch Management | Dependency vulnerability scanning**

### SCA-01 – Kwetsbare Newtonsoft.Json-versie

- **Dependency:** Newtonsoft.Json 12.0.1
- **Severity:** High
- **Advisory:** GHSA-5crp-9r3c-p9vr
- **Status:** Tijdelijk geaccepteerd als baseline

**Motivatie:**

De kwetsbaarheid is reëel. De dependency wordt voorlopig behouden binnen de onderwijsomgeving om de oorspronkelijke baseline en het gedrag van de securitypipeline te demonstreren.

Dit vormt geen bewijs dat de dependency veilig is. Een update naar een niet-kwetsbare versie moet worden uitgevoerd voordat de applicatie voor echte gegevens of productiegebruik wordt ingezet.

Nieuwe High- en Critical-bevindingen mogen niet automatisch als baseline worden behandeld.

## 3. SAST en aanvullende AI-review

**DSOMM: Secure Build | SAST**

### SAST-01 – SQL-injectie in zoekfunctionaliteit

- **Bestand:** `app/Program.cs`
- **Locatie:** Zoekfunctionaliteit met `FromSqlRaw`
- **Gevonden door:** AI-securityreview
- **Status:** Tijdelijk geaccepteerd

**Motivatie:**

De zoekfunctionaliteit gebruikt dynamisch opgebouwde SQL, waardoor SQL-injectie mogelijk is.

De kwetsbaarheid blijft voorlopig bestaan als onderdeel van de oorspronkelijke oefenapplicatie. Een WAF kan bepaalde aanvallen detecteren of blokkeren, maar is geen vervanging voor geparameteriseerde SQL-query's.

**Aanbevolen oplossing:** gebruik geparameteriseerde query's.

### SAST-02 – X-User header spoofing

- **Bestand:** `app/Program.cs`
- **Gevonden door:** AI-securityreview
- **Status:** Tijdelijk geaccepteerd

**Motivatie:**

De applicatie vertrouwt op een door de client aangeleverde `X-User`-header zonder betrouwbare authenticatie.

Een gebruiker kan daardoor mogelijk een andere identiteit opgeven.

Dit risico wordt voorlopig behouden binnen de labomgeving.

**Aanbevolen oplossing:** implementeer server-side authenticatie en autorisatie.

### SAST-03 – Te ruime CORS-configuratie

- **Bestand:** `app/Program.cs`
- **Configuratie:** `AllowAnyOrigin`
- **Status:** Tijdelijk geaccepteerd

**Motivatie:**

De applicatie staat cross-originverzoeken van willekeurige origins toe.

Deze configuratie is onnodig ruim. Het risico wordt voorlopig geaccepteerd voor de onderwijsomgeving.

**Aanbevolen oplossing:** beperk CORS tot expliciet toegestane origins.

### SAST-04 – Developer exception page

- **Bestand:** `app/Program.cs`
- **Gevonden door:** Semgrep
- **Oorspronkelijke status:** Waarschuwing
- **Huidige status:** Aangepast

**Oorspronkelijk probleem:**

De developer exception page werd zonder omgevingscontrole ingeschakeld.

**Uitgevoerde verbetering:**

```csharp
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
```

Hierdoor wordt de gedetailleerde foutpagina alleen geactiveerd in de ontwikkelomgeving.

De melding is verdwenen uit de daaropvolgende Semgrep-scan.

### SAST-05 – Docker-container zonder expliciete niet-rootgebruiker

- **Bestand:** `app/Dockerfile`
- **Gevonden door:** Semgrep
- **Oorspronkelijke severity:** ERROR
- **Huidige status:** Opgelost

**Oorspronkelijk probleem:**

De Dockerfile specificeerde geen niet-rootgebruiker.

De securitypipeline detecteerde deze nieuwe blokkerende bevinding en faalde.

**Uitgevoerde verbetering:**

```dockerfile
USER $APP_UID
```

Na deze wijziging verdween de bevinding en slaagde de volgende SAST-controle.

Dit is een concreet voorbeeld van een kwetsbaarheid die door de pipeline werd tegengehouden en vervolgens werd verholpen.

### SAST-06 – Wijzigbare GitHub Actions-versietags

- **Bestand:** `.github/workflows/security.yml`
- **Gevonden door:** Semgrep
- **Oorspronkelijk aantal:** 8 waarschuwingen
- **Huidige status:** Opgelost

**Oorspronkelijk probleem:**

GitHub Actions werden aangeroepen via wijzigbare versietags, bijvoorbeeld `@v4`.

**Uitgevoerde verbetering:**

De gebruikte actions zijn vastgezet op volledige commit-SHA's.

Hierdoor verwijzen de workflowstappen naar specifieke versies van de actions, wat het risico op onverwachte wijzigingen verkleint.

De acht waarschuwingen verdwenen uit de volgende Semgrep-scan.

### SAST-07 – Handmatige HTML-escaping

- **Bestand:** `app/wwwroot/js/app.js`
- **Semgrep-regel:** `javascript.audit.detect-replaceall-sanitization`
- **Aantal bevindingen:** 4
- **Severity:** INFO
- **Status:** Bewust geaccepteerd na codebeoordeling

**Beschrijving:**

Semgrep signaleert het gebruik van meerdere `replaceAll()`-aanroepen voor handmatige HTML-escaping.

De functie `escapeHtml()` vervangt de volgende tekens:

- `&` door `&amp;`
- `<` door `&lt;`
- `>` door `&gt;`
- `"` door `&quot;`
- `'` door `&#39;`

**Beoordeling:**

De functie escapt de relevante speciale tekens voor gewone HTML-tekst en gequote HTML-attributen.

De vier INFO-meldingen verwijzen naar dezelfde escapingfunctie en vormen niet automatisch vier afzonderlijke exploiteerbare kwetsbaarheden.

Tijdens de aanvullende codebeoordeling werden wel onveilige toepassingen van HTML-rendering vastgesteld.

**Uitgevoerde verbeteringen:**

1. De zoekterm wordt met jQuery `.text()` weergegeven in plaats van `.html()`.
2. Het berichtonderwerp in de inbox wordt via `escapeHtml()` weergegeven.
3. Het berichtonderwerp op de detailpagina wordt eveneens via `escapeHtml()` weergegeven.

**Motivatie voor acceptatie:**

De resterende meldingen betreffen de implementatie van de handmatige escapingfunctie. De functie is voor de onderzochte HTML-contexten correct en de geïdentificeerde onveilige toepassingen zijn aangepast.

De INFO-meldingen worden daarom bewust geaccepteerd als beoordeelde waarschuwingen.

Deze acceptatie garandeert niet dat de volledige applicatie vrij is van XSS-kwetsbaarheden. Bij verdere ontwikkeling verdienen veilige DOM-methoden zoals `textContent` en `.text()` de voorkeur boven het handmatig opbouwen van HTML.

## 4. Secret scanning – Gitleaks

**DSOMM: Secrets Management | Secret scanning**

### SEC-01 – Hardcoded databasewachtwoord

- **Locaties:** `docker-compose.yml` en `app/appsettings.json`
- **Status:** Tijdelijk geaccepteerd en allowlisted

**Motivatie:**

Het databasewachtwoord is opgenomen in de configuratiebestanden van de onderwijsapplicatie.

Dit is een reëel beveiligingsrisico, omdat geheimen in broncode of configuratiebestanden kunnen worden blootgesteld.

Het bestaande wachtwoord is voorlopig geaccepteerd als onderdeel van de labbaseline.

**Aanbevolen oplossing:** vervang het gedeelde wachtwoord en beheer databasegegevens buiten de repository, bijvoorbeeld via een geschikt secretsmechanisme.

### SEC-02 – Voorbeeld-API-sleutel

- **Bestand:** `app/appsettings.json`
- **Locatie:** Oorspronkelijk regel 15
- **Detectie:** `generic-api-key`
- **Status:** Geaccepteerd als fictieve labwaarde

**Motivatie:**

De gedetecteerde sleutel is volgens de projectdocumentatie een fictieve waarde die uitsluitend voor de labopdracht is toegevoegd.

De waarde wordt niet gebruikt als echte productie-API-sleutel.

Daarom is deze opgenomen in de Gitleaks-allowlist.

Nieuwe secrets buiten de bestaande uitzonderingen moeten door de secret-scanningcontrole worden beoordeeld en mogen niet ongemerkt worden geaccepteerd.

## 5. Samenvatting van de Semgrep-verbeteringen

| Bevinding | Oorspronkelijk | Na verbeteringen |
|---|---|---|
| Docker non-root | 1 ERROR | 0 |
| Wijzigbare GitHub Actions-tags | 8 WARNING | 0 |
| Developer exception page | 1 WARNING | 0 |
| Handmatige HTML-escaping | 4 INFO | 4 INFO, beoordeeld |
| **Totaal** | **14** | **4** |

De laatste onderzochte Semgrep-run rapporteerde:

```text
Semgrep: 4 findings; 4 not in reviewed baseline; 0 new blocking findings.
PASS: No new high-severity findings.
```

De vier resterende INFO-meldingen zijn inhoudelijk beoordeeld en in dit document verantwoord.

**Opmerking:** documentatie van een geaccepteerde bevinding is niet hetzelfde als opname in de technische baseline van `scripts/check-semgrep.py`. De technische baseline moet afzonderlijk overeenkomen met de daadwerkelijk goedgekeurde bevindingen.

## 6. Conclusie

De securitypipeline heeft aantoonbaar geholpen bij het identificeren en oplossen van beveiligingsproblemen.

De oorspronkelijke scan bevatte 14 Semgrep-bevindingen. Na gerichte verbeteringen bleven vier INFO-meldingen over.

De SQL-injectie, header-spoofing, ruime CORS-configuratie en hardcoded databasegegevens blijven afzonderlijke, bewust geaccepteerde risico's van de onderwijsapplicatie.

Een geslaagde pipeline betekent dat de ingestelde beveiligingscontroles zijn geslaagd, niet dat de applicatie volledig veilig is.