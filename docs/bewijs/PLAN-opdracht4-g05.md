# Opdracht 4 – Authenticatie en autorisatie: plan van aanpak (g05)

**Groep:** Yorben Geerinck · Jalil Tchatikpi · Pros Batiller
**Repo:** https://github.com/yorbengeerinck/websec-unbreakable-g05
**App (na lab 4):** https://g05.ipv6.thelastgym.be
**Keycloak (na lab 4):** https://g05.ipv6.thelastgym.be/auth (pad op de app-hostnaam, zie voortgang)
**OpenFGA:** intern only, `http://openfga:8080` (geen publieke hostname)
**Bron opdracht:** `CSAlabo4/Opdracht 4 Identity en autorisatie.pdf` (leidend) + `Opdracht 4 theorie v2.pdf`, slide 37 (architectuur)

> **Versie 2 – definitief ontwerp.** De vorige versie van dit plan koos OPA. De echte opdracht vraagt expliciet om **OpenFGA** op te zetten en OPA alleen te gebruiken als **vergelijkingspunt** in het verslag. Dit plan is daarop herwerkt. De taakverdeling is ook gewijzigd: **Jalil doet Keycloak én de frontend**, **Pros doet OpenFGA, het threat model, de validatie en het verslag**, **Yorben doet infra, API, PEP en deploy**.

---

## Voortgang en afwijkingen (stand 11 november 2026)

*Bron: de opencode-sessie "Plan voor csalabo4 per persoon" en de GitHub-Actions-runs. Alleen het werkpakket van Yorben is al gestart.*

| Stap | Status | Bewijs / opmerking |
|---|---|---|
| Y0 Voorbereiding | klaar | Branch `opdracht4-infra`; de lokale clone liep 12 commits achter en is bijgewerkt. |
| Y1 Baseline opruimen | **klaar en live** | PR #4 gemerged (`35f3aca` + `c912c02`); deploy groen, health 200, `/api/messages` zonder `X-User` geeft 401, `search?q=%` geeft `[]`. |
| Y2 Compose: Keycloak, OpenFGA, DB's, netwerken | **klaar en live** | PR #5, #6 en de proxy-fix gemerged. Op de server: Keycloak, OpenFGA en hun DB's draaien; alleen BunkerWeb publiek (80/443) en `bw-ui` op `127.0.0.1:7000`; netwerken `bw-services`, `backend`, `authz`, `iam` bestaan. `/auth/realms/master/.well-known/openid-configuration` en `/auth/admin/master/console/` geven 200. |
| Y3 Keycloak operationeel | **klaar (Yorbens deel)** | Eigen admins met MFA, tijdelijke `admin` en `KC_BOOTSTRAP_ADMIN_*` weg. Admin-console enkel via SSH-tunnel; publieke `/auth/admin` geeft 403 in BunkerWeb (PR #13). Backchannel-discovery getest (`y3-backchannel-discovery.md`). Bewijs voor Y3b staat klaar (`y3b-admin-tunnel.md`). De realm `unbreakable` en de discovery-test zijn van Jalil (J1). |
| Y4 OpenFGA bootstrap | **klaar en uitgevoerd op de server** | Store `01M754QT18P8SDPB0P0S0YK41G`, model `01M754QT1SFXBR8VFJHYJ1WD1M`, in `/etc/unbreakable/.env`; model teruggelezen (`y4-openfga-bootstrap.md`). Modelupgrade gedocumenteerd. Open: T22 (verkeerd model-ID geeft 503), bij de cutover. |
| Y5 JWT-validatie, Y6 Users-schema, Y7 PEP + OpenFGA | **gemerged en live (PR #14), schakelaar op `legacy`** | `dotnet build` en 13 van 13 tests lokaal en in CI (`backend-tests`); live op de server in `legacy`-modus (health ok). Open: tests tegen een echte OpenFGA-container, T22, T23, `fga tuple read` (P6), en de cutover naar `oidc` (§8.4). |
| Y8 pipeline en branch protection | **klaar, wacht op merge van PR #15** | Branch protection op `main` met bewezen geweigerde directe push (`y8-branch-protection.md`); verplichte checks: `backend-tests`, `sast-semgrep`, `sca-dotnet`, `secret-gitleaks`, `openfga-model`, `compose-validate`. PR #15 voegt toe: modeltest-job (validatie, plus de tests van Pros zodra `model.fga.yaml` bestaat), `docker compose config` en app-build, smoke-test na deploy en `concurrency`. Goedkeuring door Jalil of Pros volgt. Les: Semgrep `--strict` faalt op shell-constructies die zijn bash-parser niet kent (`case ... 80\|443)`); die zijn vervangen door `grep`. Nog te doen: check voor de discovery van realm `unbreakable` in de smoke-test zodra J1 klaar is. |
| Y9 Deploy, secrets, backup | **klaar** | Rechten en poorten gecontroleerd, backup van app-, Keycloak- en OpenFGA-databank, restore bewezen, rollback gedocumenteerd (`y9-backup-restore.md`, `y9-rollback.md`). Rotatie van de overige geheimen is een bewuste keuze, zie Y9. |
| Y10 Overdracht | **bezig** | Bewijs staat in `bewijs/yorben/` (OneDrive-map), zie de README daar. |
| Jalil (J0-J9), Pros (P0-P9) | nog niet gestart | - |

**Herschikking (11 nov):** het plan is zo herschreven dat elk werkpakket zonder anderen uit te voeren is. Zie §8: eigen bestanden, contract met eigen stand-ins, eigen bewijs en een overgangsschakelaar (`AUTH_MODE`) zodat PR's in willekeurige volgorde mogen. De OpenFGA-bootstrap (Y4) is van Yorben; de realm en de discovery-test zijn van Jalil.

**Gevonden tijdens Y3 (BunkerWeb en Keycloak-console):**

- `ALLOWED_METHODS` stond standaard op `GET|POST|HEAD|OPTIONS`: PUT/DELETE gaven 405 (PR #8). Elke 405/403/429 telt mee voor *bad behavior* (10 in korte tijd = ban van 24 uur, opheffen met `bwcli unban <ip>`).
- `LIMIT_REQ_RATE` stond op 2r/s en gaf 429 op de console; verhoogd naar 10r/s (PR #9).
- CRS-regels 932130 en 933135 geven een false positive op de rolbeschrijving `${role_admin}` (403 bij het toewijzen van de admin-rol). Een CRS-uitzondering via `CUSTOM_CONF_MODSEC_CRS_*` kwam niet als bestand in de BunkerWeb-container terecht; de reload faalde en de scheduler viel terug op de oude config. Die uitzondering is weer verwijderd. De rol is toegewezen buiten de WAF om.
- BunkerWeb vervangt foutantwoorden (ook die van Keycloak) door zijn eigen foutpagina; daardoor toont de console lege foutmeldingen. Echte oorzaak altijd in de BunkerWeb-logs en de Keycloak-logs zoeken.

**Openstaand na Y2:**

1. `/auth/admin` en `/auth/realms/master` afschermen voor het publiek (Y3/J3). Tot dan is de admin-console publiek bereikbaar, beveiligd met eigen accounts en MFA. Voorstel: Keycloak ook op `127.0.0.1:8081` publiceren, `KC_HOSTNAME_ADMIN` daarop zetten, beheer via SSH-tunnel, en de paden in BunkerWeb blokkeren.
2. ~~BunkerWeb-CSP breekt de Keycloak-loginpagina~~: gecontroleerd, login en alle consolepagina's werken.
3. `Referrer-Policy` live is nu `no-referrer` (bevestigd in de headers van `/auth/`).
4. `OPENFGA_STORE_ID` en `OPENFGA_MODEL_ID` blijven leeg tot het bootstrap-script (Y4).

**DNS (opgelost via pad):** `auth.g05.ipv6.thelastgym.be` bestaat niet en het domein wordt niet door de groep beheerd. Daarom draait Keycloak onder **`https://g05.ipv6.thelastgym.be/auth`** (`KC_HTTP_RELATIVE_PATH=/auth`, `KC_HOSTNAME=https://${APP_DOMAIN}/auth`, tweede BunkerWeb-regel `/auth/`). Geen extra DNS-record en geen `AUTH_DOMAIN` meer nodig. Alle `auth.g05...`-URL's verderop zijn vervangen door `g05.../auth`.

**Afwijkingen van het plan (en waarom):**

1. **Newtonsoft.Json verwijderd i.p.v. geüpgraded** (Y1): de export gebruikt nu `System.Text.Json`; `dotnet list package --vulnerable --include-transitive` meldt niets. De SCA-job verliest zijn `grep -v Newtonsoft.Json`-uitzondering.
2. **Extra in Y1:** `AllowedHosts` beperkt tot `g05.ipv6.thelastgym.be;auth.g05.ipv6.thelastgym.be;localhost` (de `auth`-host is overbodig sinds Keycloak onder `/auth` draait); `UseForwardedHeaders` met `KnownIPNetworks = fd05:5:0:1::/64`, `ForwardLimit = 1`; connectionstring verplicht via `ConnectionStrings__Unbreakable` (app start niet zonder = fail closed).
3. **Semgrep gepind op 1.182.1 (Python 3.13)**: versie 1.185.0 crashte met `semgrep-core exited with -11` (segfault, vóór het scannen). Lokaal slaagden `main` en de branch beide met dezelfde scanner, dus het lag niet aan onze code.
4. **Runner-rechten op `/etc/unbreakable/.env`:** `root:root 600` liet de deploy falen (`permission denied`). De self-hosted runner draait als gebruiker `runner`; het bestand is nu `runner:runner 600`.
5. **Netwerknamen:** het plan noemt `edge` en `data`, maar de bestaande netwerken heten `bw-services` (met IPv6-subnet `fd05:5:0:1::/64`) en `backend`. Hernoemen laat Docker een tweede netwerk met hetzelfde subnet aanmaken terwijl het oude nog bestaat (`pool overlaps`). We behouden daarom de bestaande namen; in dit plan betekent `edge` = `bw-services` en `data` = `backend`. De gebruikte netwerknamen in `docker-compose.yml` worden hersteld via een aparte fix-PR (branch `opdracht4-netwerknamen`, commit `cd53cbc`).
6. **Gitleaks-vals alarm:** `generic-api-key` sloeg aan op de JDBC-URL `KC_DB_URL` (de match was de staart `5432/keycloak`). Oplossing: aparte variabelen `KC_DB_URL`-onderdelen i.p.v. één URL en de PR tot één schone commit gesquasht, omdat gitleaks alle commits van een PR scant. De `.gitleaks.toml` bleef ongewijzigd (geen extra allowlist).
7. **Referrer-Policy:** de app zet `no-referrer`, maar live komt `strict-origin-when-cross-origin` aan. BunkerWeb overschrijft de header met zijn eigen standaard. `REFERRER_POLICY: "no-referrer"` staat al bij `bw-scheduler` in de compose; controleer na de volgende deploy of de header nu klopt en zo niet, of de instelling in de BunkerWeb-UI/DB de env-waarde overschrijft.
8. **Gepinde images (Y2):** OpenFGA `v1.22.1@sha256:b852071314f1fda1194d341a6ebac0a4f619b3cd87a6626d2ccc7048af4ec671`; Keycloak `26.8.2@sha256:723b1d6e254c0fc96d5cffec860c7e0744d6a3277822b865bb538cf891575e41`.
9. **Repo-locatie:** de werkende repo is `C:\Users\yorbe\Desktop\websec-unbreakable-g05`; de map met `(1610261423)` in de naam is een oude kopie en niet gebruiken.
11. **BunkerWeb-proxyregel:** `REVERSE_PROXY_HOST_2` mag **geen pad** bevatten (`http://keycloak:8080`, niet `.../auth/`). Met een pad in `proxy_pass` vervangt nginx de volledige request-URI, waardoor elk `/auth/*`-verzoek `/auth/` werd (302 naar `/auth/admin/`). Opgelost met de proxy-fix; Keycloak verwacht `/auth` zelf via `KC_HTTP_RELATIVE_PATH`.
12. **Account:** `gh` moet op `yorbengeerinck` staan (het andere account, `geerinckyorben-ux`, mag geen PR's aanmaken).

---

## 0. Doel en uitgangspunt

De opdracht vertrekt van de app uit lab 3, waar de API iedereen gelooft die een naam in `X-User` zet. We voegen echte authenticatie en centrale autorisatie toe:

```
Browser -> SPA -> API -> PEP -> PDP (OpenFGA)
                         OIDC provider (Keycloak)
                         Messages in de database

1. Login: authorization code flow + PKCE
2. Call with token
4. May it?
5. Data

Only the recipient can read a message.
Only the sender can delete it.
Authorization rules must be centralized.
```

We vertrekken van de bestaande app uit lab 1–3:

| Lab | Wat we al hebben | Blijft / wordt aangepast |
|---|---|---|
| 1 | Architectuur, threat model, ASVS, DSOMM | Threat model wordt uitgebreid met IdP en PDP |
| 2 | Pipeline (SCA/SAST/secrets), BunkerWeb + HTTPS, self-hosted deploy | Pipeline krijgt modeltests en smoke-tests; WAF krijgt auth-regels |
| 3 | OTP mod 37 + HMAC in de browser, enkel ciphertext in de DB | Blijft; `sender` komt nu uit het token i.p.v. `X-User` |
| 4 | Nog niets | Keycloak + JWT-validatie + PEP/PDP (OpenFGA) + tuples |

**Belangrijk:** lab 4 is geen nieuwe app. We vervangen de onveilige lab 2/3-identiteit (`X-User`, naam kiezen in de SPA, `localStorage`) door echte authenticatie, en we halen **alle** autorisatielogica uit de API. De OTP/HMAC-functionaliteit blijft werken.

**Wat de opdrachtgever echt beoordeelt:** begrip van **ABAC vs ReBAC** en de afweging **OPA vs OpenFGA**. De code is middel, niet doel. Daarom krijgen §3.3–§3.5 en §7.7 in dit plan meer gewicht dan de implementatiedetails.

---

## 1. Scope en acceptatiecriteria

### 1.1 Letterlijk uit de opdracht

De PDF somt zeven punten op. Elk punt krijgt hier een concreet acceptatiecriterium.

| # | Opdracht | Ons antwoord | Bewijs |
|---|---|---|---|
| 1 | Identity provider: Keycloak of Auth0, met verantwoording | **Keycloak** (self-hosted) | §3.2 + verslag |
| 2 | SPA logt in met OIDC code flow + PKCE; API controleert token en haalt user eruit; welke claim en waarom | Keycloak + `keycloak-js`, PKCE S256; API valideert volledig; **`sub` voor autorisatie, `preferred_username` voor weergave** | §4.3, T2/T3 |
| 3 | PDP opzetten (OpenFGA) en diepgaand vergelijken met OPA; waar haalt de PDP haar informatie? | OpenFGA + ReBAC-model; vergelijking in §3.4; tuple-store als bron | §3.3–§3.5 |
| 4 | API vraagt bij elk verzoek op een bericht de PDP; tuples wegschrijven bij elk nieuw bericht; SDK | `Check` / `ListObjects` / `Write` via `OpenFga.Sdk` | §4.6, T4–T13 |
| 5 | Autorisatieregels uit de API halen; ook inbox, zoeken en export | Alle zichtbaarheid via PDP; 0 vergelijkingen op `sender`/`recipient` in de API | §5.5, T14–T17 |
| 6 | Testen | Modeltests + integratietests + handmatige matrix | §7.6, T1–T28 |
| 7 | Threat model met IdP en PDP + nieuwe dreigingen | STRIDE uitgebreid | §7.5 |

**"Wanneer is het goed?" uit de PDF, letterlijk vertaald naar criteria:**

- [ ] Je logt in via de identity provider; de API weet wie je bent uit het token.
- [ ] Alleen de ontvanger leest; alleen de afzender verwijdert; de beslissing komt van de PDP.
- [ ] Dat geldt ook voor inbox, zoeken en export.
- [ ] In de API-code staat geen autorisatieregel meer.
- [ ] Bewijs met DevTools/curl.
- [ ] Keuzes zijn verdedigbaar.
- [ ] Threat model toont IdP, PDP en nieuwe dreigingen.

### 1.2 Secure-basis-extra's die we meenemen

- [ ] Geen `X-User`, geen naam kiezen, geen `localStorage`-identiteit meer.
- [ ] API valideert **signature, issuer, audience, expiry, algoritme, azp** van elk token.
- [ ] PKCE met **S256**, exacte redirect-URI's, geen implicit flow, geen password grant.
- [ ] Korte access tokens, refresh-rotatie, correcte logout.
- [ ] **Fail closed**: PDP onbereikbaar = geen data, geen allow (`503`).
- [ ] **Least privilege**: alleen BunkerWeb publiek; app, DB's, OpenFGA en Keycloak-admin niet.
- [ ] OpenFGA beveiligd met **preshared key** en alleen op interne netwerken.
- [ ] Geen secrets in Git, images of logs; `.env` buiten de repo.
- [ ] Elke autorisatiebeslissing wordt gelogd (zonder inhoud of token).
- [ ] Modeltests en negatieve tests (deny) in de pipeline.
- [ ] Threat model bijgewerkt; verslag met bewijs.

### 1.3 Bewust buiten scope

| Niet doen | Waarom |
|---|---|
| OPA in productie naast OpenFGA | De opdracht vraagt OpenFGA; OPA komt alleen in de vergelijking en als demo |
| Fijnmazige rollen in Keycloak voor berichten | Dat zou de autorisatie weer naar de IdP verplaatsen; relaties horen in de PDP |
| MFA voor gewone gebruikers | Stretch; admin-MFA is verplicht |
| Server-side zoeken in berichtinhoud | De body is ciphertext (lab 3); content-zoeken gebeurt client-side na decryptie |
| Outbox-pattern / distributed transaction | Productie-oplossing; we documenteren het venster en de compensatie |

---

## 2. Architectuur

### 2.1 Componenten en rollen

| Component | Rol in lab 4 | Waar |
|---|---|---|
| Browser / SPA | Login via Keycloak, access token meesturen, UI, decryptie | Internet |
| BunkerWeb | TLS, WAF, rate limiting, enige publieke ingang | Docker `edge` |
| API + **PEP** | Valideert JWT, vraagt PDP, enforce't beslissing | Docker `edge` + `app` + `data` |
| **PDP** = OpenFGA | Centrale ReBAC-beslissingen: `can_read`, `can_delete` | Docker `app` + `authz` (intern) |
| OpenFGA-DB | Tuple-store (PostgreSQL) | Docker `authz` (intern) |
| Keycloak | OIDC provider, gebruikers, sessies, tokens | Docker `edge` + `iam` |
| Keycloak-DB | Gebruikers, wachtwoordhashes, sessies | Docker `iam` (intern) |
| App-DB | Berichten, ciphertext + metadata + `Users`-mapping | Docker `data` (intern) |
| Git + PR + CI | **PAP**: model, tuples-scripts en realm-config versiebeheer, tests | GitHub |

### 2.2 AAA en PEP/PDP/PAP/PIP

| Rol | Invulling | Waarom daar |
|---|---|---|
| **Authentication** | Keycloak (OIDC, code + PKCE) | Eén plek voor wachtwoorden, sessies, MFA, brute-force |
| **Authorization** | OpenFGA (ReBAC) | Relaties tussen user en bericht, niet in de API |
| **PEP** | Endpoint-filter in de API | De API is de enige die bij de data kan; de PEP voert alleen uit |
| **PDP** | OpenFGA-container | Beslist op basis van tuples, niet op basis van code in de app |
| **PAP** | Git + PR-review + pipeline | Model en realm zijn code; wijzigingen zijn reviewbaar |
| **PIP** | Keycloak-claims (`sub`) + `Users`-tabel + OpenFGA-tuples | De PDP haalt zijn relaties uit de tuple-store |
| **Policy repository** | `deploy/openfga/model.fga` + `model.fga.yaml` | Versiebeheer, testbaar in CI |

**Antwoord op "Waar haalt de PDP de informatie om te beslissen?"** OpenFGA beslist **niet** op berichtinhoud en **niet** op user-attributen. De PDP kent alleen **relaties**: `user:<sub> sender message:<id>` en `user:<sub> recipient message:<id>`. Die tuples schrijft de API bij elke nieuwe boodschap (§4.5). Het subject komt uit het token, het object uit de route, de relatie uit het endpoint. De berichtinhoud en de `Users`-tabel blijven in de app-DB; Keycloak levert alleen de identiteit. Dat is precies het verschil met ABAC: de beslissing hangt aan een **relatie**, niet aan een **attribuut**.

### 2.3 Diagram

```mermaid
flowchart LR
  subgraph Internet
    U([Browser SPA])
    ADM([Beheerder via SSH tunnel])
  end

  subgraph Edge["Docker edge-net"]
    WAF[BunkerWeb<br/>TLS + WAF + rate limit]
  end

  subgraph AppNet["Docker app-net (internal)"]
    API[API + PEP<br/>ASP.NET]
    FGA[(OpenFGA PDP<br/>ReBAC model + tuples)]
  end

  subgraph AuthzNet["Docker authz-net (internal)"]
    FGADB[(PostgreSQL OpenFGA<br/>tuple store)]
  end

  subgraph DataNet["Docker data-net (internal)"]
    DB[(PostgreSQL app<br/>ciphertext + Users)]
  end

  subgraph IamNet["Docker iam-net (internal)"]
    KC[Keycloak<br/>OIDC provider]
    KCDB[(PostgreSQL Keycloak<br/>users + sessions)]
  end

  U -- "1. HTTPS: SPA + /api" --> WAF
  U -- "2. OIDC code + PKCE" --> WAF
  WAF -- "3. HTTP intern" --> API
  WAF -- "4. HTTP intern" --> KC
  API -- "5. JWT-validatie (JWKS)" --> KC
  API -- "6. Check / ListObjects / Write" --> FGA
  FGA -- "7. tuples" --> FGADB
  API -- "8. SQL" --> DB
  KC -- "9. SQL" --> KCDB
  ADM -. "SSH tunnel / kcadm.sh" .-> KC
```

### 2.4 Netwerken en poorten

| Netwerk | Wie | Publiek? |
|---|---|---|
| `edge` (in compose: `bw-services`) | BunkerWeb, API, Keycloak | Alleen BunkerWeb 80/443 |
| `app` | API, OpenFGA | Nee (`internal: true`) |
| `authz` | OpenFGA, OpenFGA-DB | Nee (`internal: true`) |
| `data` (in compose: `backend`) | API, app-DB | Nee (`internal: true`) |
| `iam` | Keycloak, Keycloak-DB | Nee (`internal: true`) |

- Publiek: **alleen** `80/tcp` en `443/tcp` van BunkerWeb (en SSH voor beheer).
- App: `expose: 8080`, geen `ports`.
- Keycloak: `expose: 8080`, geen `ports`; optioneel `127.0.0.1:8081:8080` voor admin via SSH-tunnel.
- OpenFGA: `expose: 8080`, **geen** `ports`, **geen** playground, **geen** metrics naar buiten.
- PostgreSQL (app, keycloak, openfga): `expose: 5432`, geen `ports`.

### 2.5 De vijf verzoekstromen

| # | Stroom | Wat er gebeurt |
|---|---|---|
| 1 | **Login** | SPA → Keycloak authorize (`code_challenge`, `state`, `nonce`) → code → token-endpoint met `code_verifier` → access token in geheugen |
| 2 | **Object lezen** | SPA → API met Bearer → PEP: rij ophalen → `Check(sub, can_read, message:id)` → allow: DTO, deny: `404` |
| 3 | **Lijst (inbox/zoek/export)** | SPA → API → `ListObjects(sub, can_read, message)` → id's → SQL `WHERE Id IN (...)` → alleen die rijen |
| 4 | **Bericht sturen** | SPA → API → `sender` uit token, `recipient` resolven via `Users` → rij insert + commit → tuples `sender` + `recipient` |
| 5 | **Verwijderen** | SPA → API → PEP: rij ophalen → `Check(sub, can_delete, message:id)` → allow: rij + tuples verwijderen, deny: `404` |

---

## 3. Kernbeslissingen

### 3.1 Overzicht

| Beslissing | Keuze | Waarom |
|---|---|---|
| OIDC provider | **Keycloak** (26.x, image op digest) | Self-hosted, gratis, EU/soeverein, past bij de labomgeving; Auth0 voegt een externe afhankelijkheid en dataverwerking toe |
| PDP | **OpenFGA** | De regels zijn relaties; `ListObjects` lost inbox/zoek/export native op; de opdracht vraagt het expliciet |
| Policyparadigma | **ReBAC** (relatie-gebaseerd) | "Alleen de ontvanger", "alleen de afzender" zijn per definitie relaties |
| PEP | **Endpoint-filter in de API** | De PEP moet de user en het object kennen; de API is de handhavingsgrens. Logisch apart, fysiek in het API-proces |
| Autorisatie-identiteit | **`sub`** uit het token | Immutable en niet herbruikbaar; tuples blijven geldig bij rename/recreate |
| Weergavenaam | **`preferred_username`** | Leesbaar in de UI; nooit gebruikt voor een beslissing |
| Tokenopslag | **In geheugen** via `keycloak-js` | Geen token in `localStorage`/`sessionStorage`; XSS kan het moeilijker stelen |
| Geen beslissing mogelijk | **Fail closed** | OpenFGA down/timeout/parse-fout = `503`, nooit `allow` |
| Niet-geautoriseerde toegang | **404** (niet 403) | Voorkomt dat een aanvaller het bestaan van een bericht aftast |
| Model pinning | **`OPENFGA_MODEL_ID` vast in `.env`** | Een nieuw model verandert beslissingen niet stilzwijgend |
| Secrets | **`/etc/unbreakable/.env`**, `chmod 600` | Buiten de repo, niet in images; deploy met `--env-file` |

### 3.2 Waarom Keycloak en niet Auth0?

- **Zelf gehost, geen vendor lock-in.** De opdracht laat beide toe; Keycloak draait in onze eigen compose naast de app die we al hebben.
- **Data blijft bij ons.** Geen gebruikersnamen, e-mails of sessies naar een derde partij. Past bij de EU/soevereiniteitslijn van lab 1.
- **Gratis, zonder limieten.** Auth0 rekent per MAU en per feature (organisaties, MFA); in een lab met vaste accounts is dat onnodige complexiteit.
- **Volledige controle over hardening.** We kunnen PKCE afdwingen, redirect-URI's exact instellen, brute-force detection aanzetten en `/admin` op de WAF blokkeren.
- **Realistisch voor de klant.** Keycloak is de standaard in Belgische/EU-enterpriseomgevingen; de kennis is overdraagbaar.

**Kosten/risico's die we aanvaarden:** wij zijn verantwoordelijk voor patching, backups en beschikbaarheid van de IdP. Dat staat in het threat model en in §10.

### 3.3 ABAC vs ReBAC – de kern van het verslag

| | **ABAC** (attribute-based) | **ReBAC** (relationship-based) |
|---|---|---|
| Beslissing op basis van | Attributen van subject, resource, actie en omgeving (afdeling, classificatie, tijd, IP, clearance) | Relaties in een graph (eigenaar, lid, ontvanger, ouder-map) |
| Natuurlijke vraag | "Mag een user met attribuut X een resource met attribuut Y doen?" | "Heeft deze user een relatie R met dit object?" |
| Sterk in | Context: "enkel tijdens kantooruren", "enkel van een Belgisch IP", "clearance ≥ geheim" | Delen, eigenaarschap, hiërarchieën, groepen, "wie mag dit zien?" |
| Zwak in | De **filtervraag**: "welke van deze 10.000 documenten mag ik zien?" wordt een scan van alle resources | Omgevingsfactoren; die moeten als contextual tuple of in een aparte laag |
| Databron | Attributen (vaak uit IdP, HR, CMDB) | Relatietuples (Zanzibar-model) |
| Consistentie | Attributen kunnen verouderen | Tuples moeten synchroon blijven met de brondata |

**Onze twee regels zijn pure relaties:**

1. "Je mag enkel de berichten zien die **naar jou** gestuurd zijn." → `recipient`
2. "Enkel **wie een bericht verstuurde**, kan het verwijderen." → `sender`

Er is geen enkel attribuut nodig: geen rol, geen afdeling, geen tijd, geen IP. Een ABAC-policy zou hier `message.recipient == subject.id` schrijven — dat is een relatie die je als attribuut vermomt. ReBAC maakt de relatie expliciet, testbaar en opvraagbaar.

**De beslissende factor is de filtervraag.** De opdracht vraagt niet alleen "mag ik dít bericht lezen?", maar ook "welke berichten zie ik in mijn inbox, bij het zoeken en bij de export?". In ABAC moet je daarvoor alle berichten naar de policy sturen (of de policy per bericht aanroepen). In ReBAC is dat één `ListObjects`-call: de PDP geeft de verzameling objecten terug waarvoor de relatie geldt. Dat is precies het probleem dat Zanzibar oplost, en het is de reden waarom OpenFGA hier beter past dan een pure ABAC-engine.

**Waar ABAC dan weer beter is:** als de klant later "enkel tijdens kantooruren", "enkel vanaf het bedrijfsnetwerk" of "enkel met clearanceniveau X" vraagt, hoort dat niet in een relatiegraph. Dat is een reële uitbreiding; we benoemen dat als grens (§3.4, "hybride").

### 3.4 OPA vs OpenFGA – vergelijking en verdict

| Criterium | **OPA** | **OpenFGA** |
|---|---|---|
| Paradigma | Algemene policy-engine (Rego), hoofdzakelijk ABAC | ReBAC / Zanzibar, relatiegraph |
| Databron | JSON-`input` per request + externe `data` (bundles, API) | Tuples in de eigen store (PostgreSQL) |
| "Mag user X object Y doen?" | `POST /v1/data/<policy>` met input | `Check` |
| "Welke objecten mag X zien?" | **Niet native**: partial evaluation of alle kandidaten in de input duwen | **`ListObjects`** (en `StreamedListObjects` zonder 1000-limiet) |
| State | Stateless; jij levert de data aan | Stateful: tuples leven in de PDP-store |
| Consistentie | Jij bepaalt hoe vers je data is | Eventueel consistent; `HIGHERCONSISTENCY` per call |
| Onderhoud | Policy + data-pipeline (wie vult de data?) | Model + tuple-lifecycle (wie schrijft/verwijdert tuples?) |
| Testen | `opa test` (unit) | `fga model test` (model + tuples + check/list-objects assertions) |
| Modelwijziging | Policy-bestand vervangen | Nieuw immutable model-ID; app pint het ID |
| Foutmodus | Verkeerde input → verkeerde beslissing | Ontbrekende tuple → deny (fail closed); een onterechte tuple → onterechte allow |
| Deployment | Eén stateless container | Container + database + migraties |
| Leercurve | Rego (eigen taal, valkuilen) | DSL + tuple-denken (eenvoudiger voor deze twee regels) |
| Beste fit | Infra-policies, admission control, rate limiting, regels met veel context | Fine-grained app-authorisatie, delen, inboxen, "wie mag dit zien" |

**Verdict voor dit project: OpenFGA.**

1. **De regels zijn relaties** (§3.3). ReBAC is het natuurlijke model.
2. **Inbox, zoeken en export zijn filtervragen.** `ListObjects` beantwoordt ze in één call; OPA kan dat niet zonder de kandidaten zelf aan te leveren — dan doet de API alsnog het zware werk en wordt de PDP een dure `if`.
3. **De opdracht vraagt het.** OpenFGA opzetten is een expliciete eis; OPA is het vergelijkingspunt.
4. **De tuple-store is de single source of truth voor "wie hoort bij dit bericht".** Bij OPA zouden we die relaties uit de app-DB moeten aanleveren, waardoor de app opnieuw bepaalt welke data de beslissing stuurt.

**Waar OPA in dit project beter zou zijn (en dus in het verslag moet staan):**

- Als de klant **contextuele** regels toevoegt: "enkel tijdens kantooruren", "enkel vanaf het bedrijfsnetwerk", "enkel met MFA in de laatste 15 minuten". OpenFGA heeft wel **conditions** (CEL), maar die hangen aan een tuple en zijn niet bedoeld als algemene omgevingspolicy: je moet tijd/IP bij elke call als context meegeven en per check evalueren. OPA is daar van nature beter in.
- Als dezelfde policy **meerdere systemen** moet afdekken (API, WAF, Kubernetes-admission, CI). OPA is één engine voor alles; OpenFGA is app-specifiek.
- Als je **geen extra stateful component** wil beheren. OPA is stateless; OpenFGA heeft een database, migraties en tuple-consistentie.

**Hybride als toekomstbeeld:** OpenFGA voor de relaties ("wie mag dit bericht zien"), OPA als front-door voor context ("vanaf welk netwerk, welke MFA-leeftijd"). We bouwen dat niet, maar het is het eerlijke antwoord op "is OpenFGA altijd de beste keuze?".

### 3.5 Waarom `sub` en niet `preferred_username`?

De opdracht vraagt expliciet welke claim we als gebruikersnaam nemen en waarom.

| Claim | Eigenschap | Gebruik bij ons |
|---|---|---|
| `sub` | Immutable, uniek per user per realm, nooit hergebruikt | **Autorisatie**: OpenFGA-subject, `Users`-sleutel, auditlog |
| `preferred_username` | Mensleesbaar, **wijzigbaar** door een admin | **Weergave**: UI, "van Yorben", e-mailachtige labels |
| `email` | Wijzigbaar, kan leeg zijn | Niet gebruiken |
| `name` | Vrij tekstveld | Niet gebruiken |

**Reden:** als we `preferred_username` als autorisatie-identiteit gebruiken, dan kan een admin een gebruiker hernoemen of een naam hergebruiken, en erft de nieuwe persoon alle tuples van de oude. Met `sub` kan dat niet: de tuple `user:8f14e45f-… recipient message:42` blijft naar dezelfde persoon wijzen, ook na een rename.

**Consequentie voor de databank:** de berichtenlijst bewaart naast de weergavenaam ook `SenderSub` en `RecipientSub`. De weergavenaam in de rij is een **snapshot** van de naam op het moment van verzenden (zo blijft "van Yorben" kloppen als hij later hernoemd wordt); de autorisatie gebruikt altijd de sub. De `Users`-tabel mapt `Sub` → `Username` voor de ontvangerkeuze en de weergave.

**Fallback als de tijd dringt:** dan gebruiken we `preferred_username` overal, zetten we "Edit username" uit in Keycloak, en noteren we de sub-migratie als restrisico. Dit is een bewuste, gedocumenteerde afwijking — geen stille vereenvoudiging.

### 3.6 Fail closed en 404

- **Fail closed:** elke fout aan de PDP-kant (timeout, 5xx, auth-fout, parse-fout, ongeldig model-ID) wordt `PdpUnavailableException` → `503`. Nooit een `catch` die `true` teruggeeft.
- **404 bij deny:** zowel "bestaat niet" als "mag niet" geven `404`. Een `403` zou verraden dat het bericht bestaat.
- **Rij is de bron van bestaan, PDP is de bron van toegang.** We halen altijd eerst de rij op (bestaat het?), dan pas `Check`. Na een delete verdwijnt de rij, dus een verouderde tuple kan nooit een verwijderd bericht terugtoveren.

---

## 4. Contract tussen de drie werkpakketten

Dit contract staat vast. Zo kunnen Yorben, Jalil en Pros parallel werken zonder elkaar te blokkeren.

### 4.1 Hostnames en omgeving

| Variabele | Waarde (lab) |
|---|---|
| `APP_DOMAIN` | `g05.ipv6.thelastgym.be` |
| `AUTH_DOMAIN` | *(vervallen; Keycloak draait onder `/auth` op `APP_DOMAIN`)* |
| `OIDC_REALM` | `unbreakable` |
| `OIDC_AUTHORITY` | `https://g05.ipv6.thelastgym.be/auth/realms/unbreakable` |
| `OIDC_AUDIENCE` | `unbreakable-api` |
| `SPA_CLIENT_ID` | `unbreakable-spa` |
| `OPENFGA_API_URL` | `http://openfga:8080` |
| `OPENFGA_STORE_ID` | door bootstrap gegenereerd |
| `OPENFGA_MODEL_ID` | door bootstrap gegenereerd, vastgepind |
| `OPENFGA_API_TOKEN` | preshared key uit `.env` |
| `APP_KNOWN_USERS` | `Yorben,Jalil,Pros` |

### 4.2 API-endpoints en PDP-gebruik

| Methode | Pad | Auth | PDP | Regel |
|---|---|---|---|---|
| GET | `/health` | geen | — | publiek |
| GET | `/api/config` | geen | — | publiek; `{"authMode":"legacy"\|"oidc"}`; overgangsschakelaar (§8.4), vervalt na de opruim-PR |
| GET | `/api/me` | Bearer | — | geeft `sub`, `preferred_username`, `exp` |
| GET | `/api/users` | Bearer | — | alleen gebruikersnamen voor de keuzelijst |
| GET | `/api/messages` | Bearer | `ListObjects(can_read)` | inbox = alleen ontvangen |
| GET | `/api/messages/{id}` | Bearer | `Check(can_read)` | alleen ontvanger |
| POST | `/api/messages` | Bearer | `Write(sender, recipient)` | geauthenticeerd; ontvanger moet bestaan |
| DELETE | `/api/messages/{id}` | Bearer | `Check(can_delete)` + `Delete(tuples)` | alleen afzender |
| GET | `/api/messages/search` | Bearer | `ListObjects(can_read)` | alleen ontvanger; metadata-filter |
| GET | `/api/messages/export` | Bearer | `ListObjects(can_read)` | alleen ontvanger |

**Toelichting per endpoint:**

- **`POST /api/messages`:** er is nog geen object, dus er valt niets te `Check`en. De PDP wordt hier gebruikt om de **relaties te schrijven** (dat is punt 4 van de opdracht). De enige voorwaarde is "geauthenticeerd", en dat is authenticatie, geen autorisatie. Dit staat expliciet zo in het verslag.
- **`/api/users`:** levert alleen `username` (geen `sub`, geen e-mail) en is voor elke ingelogde gebruiker beschikbaar. Het is directorydata, geen berichtdata. Bewuste, gedocumenteerde scope-keuze.
- **`/api/messages/search`:** de body is ciphertext, dus de server kan niet in de inhoud zoeken. De server filtert op **metadata** (`Sender`, `SentAt`) binnen de set die `ListObjects` teruggeeft. Inhoudelijk zoeken gebeurt client-side na decryptie. De **zichtbaarheid** komt altijd van de PDP.
- **`/api/messages/export`:** exporteert exact dezelfde set als de inbox (`ListObjects`). Geen tweede regel, geen tweede code pad.

**Foutcodes:**

- `401` = geen/ongeldig/verlopen token.
- `404` = bericht bestaat niet **of** PDP zegt nee.
- `503` = PDP onbereikbaar/timeout (fail closed).
- Nooit berichtinhoud, ciphertext of tokens in foutmeldingen of logs.

### 4.3 Tokenvalidatie en claims

**Wat de API allemaal controleert (en waarom):**

| Controle | Instelling | Waarom |
|---|---|---|
| Signature | JWKS van de issuer, `kid`-lookup, key-rotation | Zonder signature is elk token namaak |
| Algoritme | allowlist **RS256** only | Voorkomt `none` en HS256-verwarring |
| `iss` | exact `https://g05.ipv6.thelastgym.be/auth/realms/unbreakable` | Token van een andere realm/host weigeren |
| `aud` | moet `unbreakable-api` bevatten | Een token voor een andere API is niet geldig hier |
| `exp` / `nbf` / `iat` | lifetime + 30 s clock skew | Verlopen of toekomstige tokens weigeren |
| `azp` | `unbreakable-spa` | Token moet door onze SPA verkregen zijn |
| `sub` | aanwezig en niet leeg | Dit is de autorisatie-identiteit |
| `typ` | `Bearer` | Verkeerd tokentype weigeren |
| Metadata | `RequireHttpsMetadata=false` **alleen** intern | JWKS-fetch gaat over het geïsoleerde Docker-netwerk |

**Niet gecontroleerd (bewust, gedocumenteerd):** `jti`-replay-cache. JWT's zijn stateless; we mitigeren met korte lifetimes (5 min) en TLS. Dat is een restrisico in het threat model.

**Claimgebruik in de app:**

| Claim | Gebruik |
|---|---|
| `sub` | OpenFGA-subject, `Users`-sleutel, auditlog |
| `preferred_username` | Weergave in de UI en in de berichtenlijst |
| `exp` | `/api/me`, debugging |
| `aud`, `iss`, `azp` | alleen validatie |

### 4.4 Het OpenFGA-model

`deploy/openfga/model.fga`:

```
model
  schema 1.1

type user

type message
  relations
    define sender: [user]
    define recipient: [user]
    define can_read: recipient
    define can_delete: sender
```

**Waarom zo klein?** Elke extra relatie is een extra regel die iemand moet kunnen uitleggen. `can_read` en `can_delete` zijn **computed usersets**: ze verwijzen naar de directe relatie. Daardoor is er geen tweede plek waar "ontvanger" of "afzender" gedefinieerd is.

**Bewust niet gemodelleerd:**

- Geen `can_send` (zie §4.2: geen object om te checken).
- Geen rollen of groepen (de klant vraagt ze niet; YAGNI).
- Geen conditions (geen tijd/IP-regels in scope).
- Geen `can_list`: `ListObjects` met `can_read` doet hetzelfde.

### 4.5 Tuples en hun levenscyclus

**Tuplevorm:**

| Tuple | Betekenis |
|---|---|
| `user:<senderSub> sender message:<id>` | deze user heeft dit bericht verstuurd |
| `user:<recipientSub> recipient message:<id>` | deze user heeft dit bericht ontvangen |

**Schrijven bij POST (verplicht door de opdracht):**

```
1. INSERT Messages (SenderSub, RecipientSub, Sender, Recipient, Body, KeyLine, Mac, SentAt)
2. COMMIT                                  → rij bestaat, nog geen tuple = onzichtbaar
3. Id = gegenereerde identity
4. Write([ sender-tuple, recipient-tuple ])  ← transactioneel: alles of niets
5. bij fout in 4: DELETE rij (compensatie) en antwoord 503
   lukt de compensatie niet: weesrij (onzichtbaar, fail closed) + log/alert
```

**Verwijderen bij DELETE:**

```
1. Check(sub, can_delete, message:id)  → nee = 404
2. DELETE Messages WHERE Id = @id + COMMIT
3. Delete([ sender-tuple, recipient-tuple ])   ← OnMissingDeletes = Ignore
4. bij fout in 3: wees-tuples (onschadelijk) + log voor reconciliatie
```

**Waarom deze volgorde?** We schrijven **nooit een tuple vóór de rij gecommit is**. Anders zou de PDP een bericht kunnen toestaan dat in de databank niet bestaat. In deze volgorde is de rij altijd de eerste zekerheid:

- faalt de tuple-write, dan is het bericht onzichtbaar voor iedereen (fail closed);
- faalt de tuple-delete, dan blijven er tuples staan naar een rij die niet meer bestaat — `GET` geeft `404` en de inbox-join vindt niets.

Het venster is dus altijd "even geen toegang" of "toegang tot iets dat niet bestaat", nooit "toegang tot iets dat er wel is maar niet mag".

**Bekende vensters (expliciet in het verslag):**

| Venster | Gevolg | Ernst |
|---|---|---|
| Crash tussen INSERT-commit en tuple-write | Weesrij, onzichtbaar voor iedereen | Beschikbaarheid, geen lek |
| Tuple-write faalt en compensatie-delete faalt | Weesrij | Beschikbaarheid, geen lek |
| Tuple-write gelukt, daarna crash | Consistent | — |
| Crash tussen rij-delete en tuple-delete | Wees-tuples | Geen lek: rij bestaat niet en id's worden niet hergebruikt |
| Gelijktijdige DELETE van hetzelfde bericht | Tweede delete is idempotent (`OnMissingDeletes = Ignore`) | Geen probleem |

**Productiealternatief:** outbox-pattern met een reconciler die tuple-writes **retryt** in plaats van compenseert, plus een periodieke vergelijking tussen `Messages` en de tuple-store. We documenteren het, we bouwen het niet.

### 4.6 SDK-aanroepen (contract voor Yorben)

```csharp
// configuratie (één singleton, nooit per request)
var configuration = new ClientConfiguration {
    ApiUrl = cfg["OPENFGA_API_URL"],
    StoreId = cfg["OPENFGA_STORE_ID"],
    AuthorizationModelId = cfg["OPENFGA_MODEL_ID"],   // vastgepind
    Credentials = new Credentials {
        Method = CredentialsMethod.ApiToken,          // preshared key als Bearer
        Config = new CredentialsConfig { ApiToken = cfg["OPENFGA_API_TOKEN"] }
    }
};
var fga = new OpenFgaClient(configuration);

// 1. Object lezen
var check = await fga.Check(new ClientCheckRequest {
    User = $"user:{sub}", Relation = "can_read", Object = $"message:{id}"
}, new ClientCheckOptions { Consistency = ConsistencyPreference.HIGHERCONSISTENCY });
if (!check.Allowed) return Results.NotFound();

// 2. Inbox / zoeken / export
var list = await fga.ListObjects(new ClientListObjectsRequest {
    User = $"user:{sub}", Relation = "can_read", Type = "message"
}, new ClientListObjectsOptions { Consistency = ConsistencyPreference.HIGHERCONSISTENCY });
var ids = list.Objects.Select(o => int.Parse(o[(o.IndexOf(':') + 1)..])).ToHashSet();

// 3. Tuples schrijven bij POST
await fga.Write(new ClientWriteRequest {
    Writes = new List<ClientTupleKey> {
        new() { User = $"user:{senderSub}",    Relation = "sender",    Object = $"message:{id}" },
        new() { User = $"user:{recipientSub}", Relation = "recipient", Object = $"message:{id}" }
    }
});

// 4. Tuples verwijderen bij DELETE (idempotent)
await fga.Write(new ClientWriteRequest {
    Deletes = new List<ClientTupleKeyWithoutCondition> {
        new() { User = $"user:{senderSub}",    Relation = "sender",    Object = $"message:{id}" },
        new() { User = $"user:{recipientSub}", Relation = "recipient", Object = $"message:{id}" }
    }
}, new ClientWriteOptions {
    Conflict = new ConflictOptions { OnMissingDeletes = OnMissingDeletes.Ignore }
});
```

**Afspraken:**

- Eén `OpenFgaClient` als singleton; niet per request aanmaken.
- `AuthorizationModelId` altijd expliciet meesturen (config of per call).
- `Consistency = HIGHERCONSISTENCY` op `Check` en `ListObjects`, zodat de testmatrix deterministisch is. Kost latency; noteer de afweging.
- `ListObjects` heeft een limiet (1000). Voor de labschaal ruim voldoende; boven die grens `StreamedListObjects` gebruiken.
- Elke `ApiException`/`HttpRequestException`/timeout → `PdpUnavailableException` → `503`.

### 4.7 Frontend-contract

- `auth.js` exporteert: `initAuth()`, `getToken()`, `getSub()`, `getUsername()`, `login()`, `logout()`.
- `api.js` stuurt **altijd** `Authorization: Bearer <token>`; nooit `X-User`.
- De SPA toont `getUsername()`; de gebruiker kan de afzender niet wijzigen.
- De MAC-invoer gebruikt `getUsername()` als `sender` voor de berekening; de API zet `sender` uit het token.
- De ontvangerkeuze stuurt een **username**; de API resolvet die naar een `sub` via de `Users`-tabel.
- Geen token in URL, `localStorage`, `sessionStorage` of DOM.

### 4.8 Git- en reviewregels

- Branches: `opdracht4-infra` (Yorben), `opdracht4-api-authz` (Yorben), `opdracht4-iam` (Jalil), `opdracht4-frontend-oidc` (Jalil), `opdracht4-openfga` (Pros), `opdracht4-threatmodel` (Pros).
- Nooit rechtstreeks naar `main`; elke wijziging via PR.
- Branch protection op `main`: PR verplicht, minstens 1 approval, status checks verplicht.
- Mergevolgorde: willekeurig; de overgangsschakelaar (§8.4) zorgt dat geen enkele PR de live app breekt.
- Elke PR: `docker compose config` groen, build groen, `fga model test` groen, geen secrets.

---

## 5. Werkpakket Yorben – infra, API, PEP, OpenFGA-integratie, deploy

**Rol:** infrastructuur, backend, PEP, SDK-integratie, databankschema, pipeline, deploy.
**Kan starten:** meteen en alleen uitvoeren (zie §8).
**Afstemming:** geen; enkel contractwijzigingen in §4 melden.

### Y0. Voorbereiding en branch

- [x] `git checkout main && git pull`
- [x] `git checkout -b opdracht4-infra`
- [ ] Lees dit plan en de PDF; bevestig de contracten uit §4 in de groep. *(open: bevestig in de groep dat het plan en de contracten uit §4 gelden.)*
- [x] Inventariseer alle plekken met `X-User`, `CurrentUser()`, `localStorage` en `AllowAnyOrigin`.

**Klaar wanneer:** branch bestaat, contract is gedeeld, niemand twijfelt over hostnames/poorten/claims/tuples.

### Y1. Baseline opruimen (lab 2/3-schuld)

- [x] `Newtonsoft.Json` van 12.0.1 naar 13.0.3+ (uiteindelijk verwijderd, zie afwijking 1); `dotnet list package --vulnerable` controleren.
- [x] `.gitignore` aanvullen:

```gitignore
.env
.env.*
!.env.example
*.pem
*.key
codeboek*.txt
hmac*.txt
```

- [x] Verwijder de fake `ubk_live_...`-key uit `appsettings.json`; verplaats echte waarden naar env. Haal daarna de allowlist-entry uit `.gitleaks.toml`, zodat een echte key in de toekomst wél faalt.
- [x] Verwijder `AllowAnyOrigin`; SPA en API zijn same-origin via BunkerWeb. Als CORS nodig blijft: alleen `https://g05.ipv6.thelastgym.be`.
- [x] Fix de SQLi in `/api/messages/search`: geen `FromSqlRaw` met stringconcatenatie; gebruik LINQ/`EF.Functions.ILike` met parameters.
- [x] Voeg `UseForwardedHeaders` toe (achter BunkerWeb); beperk `KnownNetworks`/`KnownProxies` tot het Docker-netwerk van BunkerWeb.
- [x] Voeg security headers toe: `X-Content-Type-Options`, `Referrer-Policy`, `X-Frame-Options`, CSP-report-only.

**Klaar (live sinds PR #4).** *Klaar wanneer:* build groen, geen nieuwe SCA/SAST-findings, SQLi niet meer reproduceerbaar.

### Y2. Docker Compose: Keycloak, OpenFGA, databases, netwerken

- [x] Maak `/etc/unbreakable/.env` op de server (eigenaar `runner`, `chmod 600`); `.env.example` met placeholders in de repo. *(`.env.example` klaar; serverbestand mist nog de nieuwe variabelen.)* *(gedaan: `runner:runner`, 600, alle variabelen aanwezig, zie `y9-backup-restore.md`.)*
- [x] Voeg toe aan `docker-compose.yml`:

```yaml
  openfga-migrate:
    image: openfga/openfga:<PIN>@sha256:<DIGEST>
    command: migrate
    environment:
      OPENFGA_DATASTORE_ENGINE: postgres
      OPENFGA_DATASTORE_URI: postgres://openfga:${OPENFGA_DB_PASSWORD}@openfga-db:5432/openfga?sslmode=disable
    depends_on:
      openfga-db: { condition: service_healthy }
    networks: [authz]
    restart: "no"

  openfga:
    image: openfga/openfga:<PIN>@sha256:<DIGEST>
    command: run
    environment:
      OPENFGA_DATASTORE_ENGINE: postgres
      OPENFGA_DATASTORE_URI: postgres://openfga:${OPENFGA_DB_PASSWORD}@openfga-db:5432/openfga?sslmode=disable
      OPENFGA_AUTHN_METHOD: preshared
      OPENFGA_AUTHN_PRESHARED_KEYS: ${OPENFGA_API_TOKEN}
      OPENFGA_PLAYGROUND_ENABLED: "false"
      OPENFGA_LOG_FORMAT: json
    expose: ["8080"]
    depends_on:
      openfga-migrate: { condition: service_completed_successfully }
    networks: [app, authz]
    restart: unless-stopped

  openfga-db:
    image: postgres:17
    environment:
      POSTGRES_DB: openfga
      POSTGRES_USER: openfga
      POSTGRES_PASSWORD: ${OPENFGA_DB_PASSWORD}
    volumes: [openfgadbdata:/var/lib/postgresql/data]
    networks: [authz]
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U openfga -d openfga"]
      interval: 5s
      timeout: 3s
      retries: 10
```

- [x] Keycloak + `keycloak-db` zoals in de vorige versie (image op digest, `KC_DB=postgres`, `KC_HOSTNAME=https://auth...`, `KC_PROXY_HEADERS=xforwarded`, `KC_HTTP_ENABLED=true`, `KC_HEALTH_ENABLED=true`).
- [x] Netwerken `edge` (= bestaande `bw-services`), `app` (`internal: true`), `authz` (`internal: true`), `data` (= bestaande `backend`, `internal: true`), `iam` (`internal: true`).
- [x] `app`: `expose: 8080`, geen `ports`; `depends_on` op `openfga` (`service_started`) en `keycloak`; `restart: unless-stopped`; resource limits.
- [x] `bunkerweb`: alleen 80/443 publiek; `depends_on` app + keycloak.
- [x] Geen healthcheck op OpenFGA als de image geen shell/curl heeft; de API is fail closed, dus een gemiste healthcheck is geen veiligheidsrisico. Noteer de keuze.
- [x] Test: `docker compose config` (**gedaan, exit 0**), `docker compose up -d` (**nog te doen: deploy #40 faalde op ontbrekende variabelen**), `docker ps`, `docker network inspect`. *(gedaan 11 nov: stack draait, deploy groen, `docker ps` toont enkel 80/443 publiek.)*

> **Nooit `docker compose down -v`**: dat wist `bwdata` (Let's Encrypt), de app-DB, de Keycloak-DB én de OpenFGA-DB. Gebruik `down` zonder `-v`; maak eerst een backup.

**Klaar wanneer:** Keycloak, OpenFGA en app starten; alleen BunkerWeb heeft publieke poorten; OpenFGA/DB's/Keycloak-admin zijn niet van buiten bereikbaar.

### Y3. Keycloak operationeel (samen met Jalil)

- [x] DNS: niet nodig; Keycloak draait onder `/auth` op de app-hostnaam (het domein wordt niet door ons beheerd).
- [x] Eerste admin via bootstrap-variabelen; daarna eigen admins (yorben, jalil, pros) met MFA, bootstrap-admin en `KC_BOOTSTRAP_ADMIN_*` verwijderd.
- [→] Realm `unbreakable` en de discovery-test zijn verplaatst naar J1 (Jalil, eigen acceptatie). Yorben hoeft hier niet op te wachten.

**Klaar (Yorbens deel):** Keycloak draait onder `/auth`, eigen admins met MFA, bootstrap-admin weg, admin-console enkel via de tunnel.

### Y3b. Admin-console afschermen (klaar: stap A tot D uitgevoerd en bewezen)

**Doel:** de Keycloak-admin-console is nu publiek bereikbaar op `https://g05.ipv6.thelastgym.be/auth/admin/`. Beveiligd door eigen accounts en MFA, maar de console hoort niet op internet te staan. Na deze stap is beheer enkel nog mogelijk via een SSH-tunnel; de gewone gebruikerslogin blijft publiek.

**Ontwerp**

| Pad / onderdeel | Publiek (via BunkerWeb) | Via SSH-tunnel |
|---|---|---|
| `/auth/realms/unbreakable/…` (login en tokens van de app) | ja | n.v.t. |
| `/auth/admin/…` (admin-console en admin-API) | **nee (geblokkeerd)** | ja, `http://localhost:8081/auth/admin/` |
| `/auth/realms/master/…` (login van de admins) | **voorlopig ja (zie opmerking)** | ja |

**Stappen (in deze volgorde, elk een eigen kleine PR, na elke deploy controleren)**

- [x] **A. Poort lokaal publiceren (commit `059746a`, branch `opdracht4-admin-tunnel`).** In `docker-compose.yml` bij `keycloak`: `ports: ["127.0.0.1:8081:8080"]`. Enkel op loopback; BunkerWeb blijft de enige service met publieke poorten (80/443). Controle op de server: `sudo docker ps --format 'table {{.Names}}\t{{.Ports}}'` en `ss -ltnp | grep 8081` (moet `127.0.0.1:8081` zijn).
- [x] **B. Admin-hostname instellen (zelfde commit).** `KC_HOSTNAME_ADMIN: "http://localhost:8081/auth"` zodat de console via de tunnel werkt (de publieke `KC_HOSTNAME` blijft voor gebruikerslogins). Getest via de tunnel op 11 nov: werkt, maar alleen samen met de Frontend URL van de master-realm (zie "Oplossing tunnelfout" hieronder).
- [x] **C. (PR #13, gedeployed 11 nov: scheduler "Successfully reloaded", `/auth/admin/` geeft 403.) Pas daarna de publieke admin-paden blokkeren, en eerst enkel `/auth/admin`.** In `bw-scheduler`: `BLACKLIST_URI: "^/auth/admin"` (BunkerWeb: lijst van PCRE-regexen, gescheiden door spaties; de instelling bestaat in de documentatie, **het effect is nog niet op onze server getest**). Zo is de console en de admin-REST-API niet meer publiek, terwijl de loginpagina van de master-realm (`/auth/realms/master`) bereikbaar blijft; daarvoor is een wachtwoord plus MFA nodig. Blokkeer `/auth/realms/master` **pas** als de tunnelsessie bewezen werkt zonder dat de browser naar de publieke URL wordt teruggestuurd. Controle na de deploy: `curl -s -o /dev/null -w '%{http_code}\n' https://g05.ipv6.thelastgym.be/auth/admin/` moet 403 geven, `…/auth/realms/unbreakable/.well-known/openid-configuration` moet 200 blijven geven, en de scheduler mag niet terugvallen (`grep -iE "failing over|config check failed|Successfully reloaded"`).
- [x] **D. Valideren (enkel Yorbens deel), gedaan op 11 nov, zie `y3b-admin-tunnel.md`:** admin-console enkel via tunnel bewijzen met screenshots in `bewijs/yorben/` in de OneDrive-map (niet in de repo). De gebruikerslogin van de app hoort bij J9.

**Bekende valkuilen (gevonden bij het uitzoeken)**

- `KC_HOSTNAME_ADMIN` is achter een reverse proxy gevoelig: in meldingen voor Keycloak 26 komen blanco adminpagina's, redirect-lussen en omleidingen naar de publieke hostname voor. Als de console na het inloggen terugstuurt naar `https://g05.ipv6.thelastgym.be/auth/admin/…`, werkt de tunnel niet zodra dat pad geblokkeerd is.
- **Eerste tunneltest (11 nov):** `http://localhost:8081/auth/admin/master/console/` gaf "Timeout when waiting for 3rd party check iframe message". De console laadt een iframe (`login-status-iframe.html`) van de auth-server-URL van de master-realm; die stond nog op de publieke hostname. Bevestigd met `curl -sI https://g05.ipv6.thelastgym.be/auth/realms/master/protocol/openid-connect/login-status-iframe.html`: die geeft `x-frame-options: SAMEORIGIN`. De pagina op `localhost:8081` is een andere origin, dus de browser weigert het iframe. De header komt uit de eigen realm-instellingen van Keycloak (Realm settings → Security defenses → Headers) en wordt bewust niet versoepeld.
- **Oplossing tunnelfout (11 nov, getest, werkt):** de *Frontend URL* van de **master**-realm op `http://localhost:8081/auth` zetten, zodat console en iframe dezelfde origin hebben. In onze Keycloak 26.8.2-UI stond het veld niet op de verwachte plek (tab Security defenses is fout; General bevatte het niet), dus is het via de database gezet:
  ```bash
  sudo docker exec -it websec-unbreakable-g05-keycloak-db-1 psql -U keycloak -d keycloak -c "insert into realm_attribute(name, realm_id, value) select 'frontendUrl', id, 'http://localhost:8081/auth' from realm where name='master' on conflict (name, realm_id) do update set value=excluded.value;"
  sudo docker restart websec-unbreakable-g05-keycloak-1
  ```
  Herstart is nodig door de realm-cache. Daarna werkt de console via `http://localhost:8081/auth/admin/`.
- **Gevolg:** de master-realm (admin-login en admin-console) hoort nu bij de tunnel. De publieke `/auth/admin/` en `/auth/realms/master/...` kunnen daardoor niet meer goed werken of terugsturen naar `localhost`. De `unbreakable`-realm en de app-login blijven op de publieke URL werken (controleren na elke wijziging).
- **Terugdraaien** (enkel als de tunnel niet meer werkt of de publieke master-login weer nodig is):
  ```bash
  sudo docker exec -it websec-unbreakable-g05-keycloak-db-1 psql -U keycloak -d keycloak -c "delete from realm_attribute where name='frontendUrl' and realm_id=(select id from realm where name='master');"
  sudo docker restart websec-unbreakable-g05-keycloak-1
  ```
- Test daarom stap B **vóór** stap C. Gaat de tunnel niet goed, dan revert je de PR van stap A+B (de publieke console blijft ondertussen gewoon werken).
- Keycloak raadt zelf aan de admin-endpoints niet publiek bloot te stellen en dit via de reverse proxy te regelen.

**Test van de tunnel (na de deploy van stap A+B)**

1. Tunnel openen (zie hieronder) en `http://localhost:8081/auth/admin/` openen.
2. Inloggen met het eigen admin-account en MFA.
3. Controleren dat je na het inloggen **op `localhost:8081`** blijft (adresbalk), en dat *Users* en *Realm roles* openen.
4. Pas dan stap C klaarzetten.

**Voorzorgen (geleerd tijdens Y3)**

- Wijzig BunkerWeb-instellingen in kleine stappen. Een ongeldige of niet-aangekomen `CUSTOM_CONF_…` laat de config-test falen; de scheduler valt dan terug op de oude config en latere wijzigingen worden niet toegepast. Controleer na elke deploy: `sudo docker logs websec-unbreakable-g05-bw-scheduler-1 --since 5m 2>&1 | grep -iE "failing over|config check failed|Successfully reloaded"`.
- Elke 403/405/429 telt mee voor *bad behavior*: 10 binnen korte tijd geeft een ban van 24 uur. Opheffen: `sudo docker exec websec-unbreakable-g05-bw-scheduler-1 bwcli unban <ip>`.
- Houd tijdens stap A-C altijd een tweede toegangsweg: de SSH-tunnel moet bewezen werken vóór de publieke paden dicht gaan.

**Beheer via SSH-tunnel (iedere beheerder, vanaf zijn eigen PC)**

1. In PowerShell (SSH-gegevens uit `SSHCredentials.xlsx`, niet in het plan of de chat):
   ```powershell
   ssh -L 8081:127.0.0.1:8081 -J student@77.42.8.69 root@2a02:1810:a531:7602::105
   ```
2. Laat dat venster open staan.
3. Open in de browser `http://localhost:8081/auth/admin/` en log in met het eigen admin-account (met MFA).
4. Sluit het SSH-venster als je klaar bent; dan is de console weer onbereikbaar.

> Beheer gaat nu **enkel via de tunnel** (zie Oplossing tunnelfout). Stap C (`BLACKLIST_URI` op `^/auth/admin`) is uitgevoerd (PR #13), dus de console is publiek geblokkeerd en enkel nog via de tunnel bereikbaar.

### Y4. OpenFGA bootstrap en configuratie (Yorben, alleen)

OpenFGA draait al (Y2). Het model is vastgelegd in §4.4; Pros hoeft niets te leveren.

- [x] `deploy/openfga/model.fga`: §4.4 **letterlijk** (Pros test zijn eigen kopie). Aangemaakt op branch `opdracht4-api-authz`.
- [x] `deploy/openfga/bootstrap.sh` aangemaakt (uitvoerbaar; zelfde inhoud als hieronder):

```bash
#!/usr/bin/env bash
set -euo pipefail
: "${FGA_API_URL:?}"
: "${FGA_API_TOKEN:?}"

OUT=$(fga store create \
        --api-url "$FGA_API_URL" \
        --api-token "$FGA_API_TOKEN" \
        --name unbreakable \
        --model deploy/openfga/model.fga)

STORE_ID=$(echo "$OUT" | jq -r '.store.id')
MODEL_ID=$(echo "$OUT" | jq -r '.model.authorization_model_id')

printf 'OPENFGA_STORE_ID=%s\nOPENFGA_MODEL_ID=%s\n' "$STORE_ID" "$MODEL_ID"
```

- [x] Voer het uit op de server (via SSH-tunnel of vanaf een tijdelijke container in `authz`): *(gedaan 11 nov met de `openfga/cli`-container op het `authz`-netwerk, zie `y4-openfga-bootstrap.md`.)*

```bash
export FGA_API_URL=http://openfga:8080
export FGA_API_TOKEN="$OPENFGA_API_TOKEN"
./deploy/openfga/bootstrap.sh   # maakt store + model, print STORE_ID en MODEL_ID
```

- [x] Zet `OPENFGA_STORE_ID` en `OPENFGA_MODEL_ID` in `/etc/unbreakable/.env`; herstart de app. *(gedaan; app herstart, health ok.)*
- [ ] Controleer dat de app met een verkeerd model-ID **fail closed** is (T22). *(open, hoort bij de cutover: in de `legacy`-modus wordt het model-ID niet gebruikt. Stappen staan in het gesprek; bewijs volgt als `p6-t22-t23.md`.)*
- [x] Documenteer de modelupgrade *(11 nov, zie de procedure hieronder; de commando's volgen dezelfde `openfga/cli`-container als bij de bootstrap.)*

  **Procedure modelupgrade** (een model is onveranderlijk; een wijziging maakt een nieuwe versie met een nieuw ID in dezelfde store):

  1. Wijzig `deploy/openfga/model.fga` in een PR. Pros keurt goed; CI draait `model validate` en de modeltests.
  2. Maak vooraf een backup van app- en OpenFGA-databank (zie Y9).
  3. Schrijf het nieuwe model naar de bestaande store en noteer het nieuwe ID:
     ```bash
     set -a; . /etc/unbreakable/.env; set +a
     docker run --rm --network websec-unbreakable-g05_authz \
       -v "$PWD/deploy/openfga/model.fga:/model.fga:ro" \
       -e FGA_API_URL=http://openfga:8080 -e FGA_API_TOKEN="$OPENFGA_API_TOKEN" \
       openfga/cli model write --store-id "$OPENFGA_STORE_ID" --file /model.fga
     ```
     Controleer de flags van de gepinde CLI-versie met `... openfga/cli model write --help`.
  4. Controleer het nieuwe model: `... openfga/cli model get --store-id ... --model-id <nieuw ID>` en `... tuple read --store-id ...` (bestaande tuples moeten bij het nieuwe model nog geldig zijn).
  5. Zet het nieuwe ID in `/etc/unbreakable/.env`. Bewaar eigenaar en rechten: na `sed -i` altijd `chown runner:runner /etc/unbreakable/.env && chmod 600 /etc/unbreakable/.env`.
  6. Herstart enkel de app: `docker compose --env-file /etc/unbreakable/.env up -d app`, en controleer `/health`.
  7. Herhaal de testmatrix (T4 tot T23) en noteer datum, oud en nieuw model-ID en uitkomst in het logboek.
  8. **Terugdraaien:** het vorige model-ID terugzetten en de app herstarten. Het oude model blijft in de store bestaan. Tuples die onder het nieuwe model zijn geschreven, kent het oude model mogelijk niet; controleer dat met `tuple read` en een paar autorisatietests.

**Klaar wanneer:** store en model bestaan, de app kent beide ID's, en een modelwijziging is reproduceerbaar.

### Y5. API: JWT-validatie en identiteit

> Test met JWT's van een eigen testsleutel (drie testgebruikers met vaste `sub`); de echte realm is niet nodig. Bouw `Auth__Mode` en `GET /api/config` (§8.4): standaard `legacy` = huidig gedrag.

**Status (11 nov): gemerged (PR #14) en live in de `legacy`-modus; `dotnet build` en 13 van 13 tests groen, lokaal en in CI.**
- `app/Auth/OidcSetup.cs`: JwtBearer met RS256-allowlist, `iss`, `aud`, `exp` (30 s skew), `sub`, `azp = unbreakable-spa` en `typ = Bearer` (via `OnTokenValidated`), `MapInboundClaims = false`. Configuratie uit `Oidc__MetadataAddress/Issuer/Audience`; ontbreekt iets, dan start de app niet.
- `app/Program.cs`: modus `Auth__Mode`; in `oidc` ook `UseAuthentication/UseAuthorization/UseRateLimiter`. `GET /api/config` is publiek.
- `app/Api/OidcApi.cs`: groep `/api` met `RequireAuthorization()`; `GET /api/me` (sub, preferred_username, exp); `CreateMessageRequest` zonder `sender/id/subject/sub's`.
- Rate limiting: 120 verzoeken per minuut per gebruiker, 20 per minuut op `POST /api/messages` (`app/Auth/OidcRateLimiting.cs`).
- `compose`: `KC_HOSTNAME_BACKCHANNEL_DYNAMIC: "true"` bij Keycloak, zodat `jwks_uri` intern is (anders haalt de app de sleutels via de publieke URL en de WAF op). Controleer na deploy dat `http://keycloak:8080/auth/realms/unbreakable/.well-known/openid-configuration` een interne `jwks_uri` toont en `issuer` de publieke URL blijft.
- [x] Lokaal `dotnet build` en `dotnet test` (zie hieronder), dan PR. *(gedaan: 13 van 13 groen, ook in CI.)*
- [x] `packages.lock.json` van `app/` en het testproject opnieuw laten genereren en mee committen (`dotnet restore`). *(gedaan in commit `cfcbadb`.)*

- [x] Voeg `Microsoft.AspNetCore.Authentication.JwtBearer` toe (versie pinnen). *(10.0.11.)*
- [x] Configureer: *(afwijking: instellingen komen uit de omgeving en `OnTokenValidated` controleert ook `azp` en `typ`.)*

```csharp
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MetadataAddress =
            "http://keycloak:8080/realms/unbreakable/.well-known/openid-configuration";
        options.RequireHttpsMetadata = false; // alleen intern Docker-netwerk
        options.Audience = "unbreakable-api";
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = "https://g05.ipv6.thelastgym.be/auth/realms/unbreakable",
            ValidateAudience = true,
            ValidAudience = "unbreakable-api",
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            ValidAlgorithms = new[] { "RS256" },
            NameClaimType = "preferred_username"
        };
    });
```

> `RequireHttpsMetadata=false` mag alleen omdat de metadata/JWKS-fetch over het interne, geïsoleerde Docker-netwerk gaat. Alle clientverkeer is HTTPS via BunkerWeb. Noteer dit in het verslag.

- [x] Voeg `app.UseAuthentication(); app.UseAuthorization();` toe.
- [x] `var api = app.MapGroup("/api").RequireAuthorization();`
- [x] Voeg `GET /api/me` toe: `sub`, `preferred_username`, `exp` (geen token).
- [x] Vervang `CurrentUser(request)` volledig: *(in de oidc-modus; de legacy-handlers staan er tijdelijk naast, zie §8.4.)*

```csharp
static string CurrentSub(HttpContext ctx) =>
    ctx.User.FindFirst("sub")?.Value ?? string.Empty;

static string CurrentDisplayName(HttpContext ctx) =>
    ctx.User.FindFirst("preferred_username")?.Value ?? string.Empty;
```

- [ ] Verwijder elke `X-User`-lezing en de `AllowAnyOrigin`-CORS. *(deels: in de oidc-modus heeft `X-User` geen effect en CORS staat dicht; de legacy-handlers worden pas verwijderd in de opruim-PR van §8.4.)*
- [x] Voeg een `CreateMessageRequest`-DTO toe: alleen `recipient` (username), `keyLine`, `body`, `mac`. De server zet `sender`, `senderSub`, `recipientSub`, `subject`, `sentAt`, `id`. Mass assignment onmogelijk.
- [x] Behoud de lab 3-validatie: `body` 40 tekens `A-Z0-9_`, `mac` 64 hex, `keyLine` 1–50, `409` bij hergebruik. *(getest.)*
- [x] Voeg rate limiting toe op `POST /api/messages` en algemene limieten.

**Klaar wanneer:** zonder token `401`; met geldig token je eigen `sub`; `X-User` heeft geen effect; body-velden `sender`/`id`/`subject` worden genegeerd.

### Y6. Databankschema: `Users` en sub-kolommen

**Status (11 nov): gemerged (PR #14); de schemawijziging is uitgevoerd op de server (tabel `Users` bestaat, backup en restore bewezen).** Gekozen aanpak: geen EF-migraties (het project gebruikt `EnsureCreated`), maar idempotente SQL in `app/Data/SchemaUpgrade.cs` (`CREATE TABLE/INDEX IF NOT EXISTS`, `ADD COLUMN IF NOT EXISTS`), zodat een schone en een bestaande databank hetzelfde schema krijgen. `User` (`Sub` PK, `Username` uniek, max 40) in `app/Models/User.cs`; `Message.SenderSub/RecipientSub` met `[JsonIgnore]`. De tabel Users wordt bijgehouden door `UserDirectory` bij elke geauthenticeerde request (cache per sub). Backfill: oude rijen krijgen hun sub zodra de gebruiker inlogt, maar blijven **onzichtbaar** (geen tuples; fail closed). Bewuste beperking: een ontvanger moet minstens één keer ingelogd zijn om op naam gevonden te worden.

- [x] Voeg een migratie toe: *(afwijking: idempotente SQL in `SchemaUpgrade.cs` in plaats van EF-migraties; tabel `Users` bestaat op de server.)*

```csharp
public class User {
    public string Sub { get; set; } = "";          // PK, uit Keycloak
    public string Username { get; set; } = "";     // uniek, weergave
    public DateTime CreatedAt { get; set; }
}

// Messages: bestaande kolommen blijven, plus
public string SenderSub { get; set; } = "";
public string RecipientSub { get; set; } = "";
```

- [x] `Sender`/`Recipient` blijven als **weergave-snapshot** op het moment van verzenden.
- [x] Upsert de `Users`-rij bij de eerste login (`/api/me`) op basis van `sub` + `preferred_username`.
- [x] Backfill voor lab 3-rijen: match `Sender`/`Recipient` op `Users.Username` en vul de sub-kolommen. Rijen zonder match: laat NULL en behandel als ontoegankelijk (fail closed) of verwijder lab 3-testdata. *(code klaar; de 3 bestaande rijen worden gekoppeld zodra hun gebruikers voor het eerst inloggen.)*
- [x] Index op `Messages.RecipientSub` en `Messages.SenderSub`.
- [x] Voeg een unieke index toe op `Users.Username` (case-sensitive) zodat de ontvanger-resolutie deterministisch is.

**Klaar wanneer:** de migratie op een schone DB en op de bestaande DB slaagt, en oude rijen correct gemapt zijn.

### Y7. PEP + OpenFGA: centrale autorisatie

> **Eigenaarschap (§8.2):** Yorben bezit `deploy/openfga/model.fga` (§4.4 letterlijk) en de PEP-integratie; Pros bezit `model.fga.yaml` en de modeltests. Je test je PEP tegen een lokale OpenFGA-container met het model uit §4.4 en een fake `IAuthorizationService`, niet tegen Pros' werk. De overgangsschakelaar uit §8.4 (`Auth__Mode`, `/api/config`) hoort bij deze stap en bij Y5.

**Status (11 nov): gemerged (PR #14); tests met een stand-in voor OpenFGA zijn groen. Open: tests met een echte OpenFGA (T22, T23, `fga tuple read`), die horen bij de cutover.** Afwijkingen van de tekst hieronder:
- De PEP heet `IMessageAuthorizer` (interface in `app/Auth/IMessageAuthorizer.cs`) met implementatie `OpenFgaMessageAuthorizer`; de naam `IAuthorizationService` bestaat al in ASP.NET Core.
- Objectroutes gebruiken geen endpoint-filter maar een `CanReadAsync`/`CanDeleteAsync`-aanroep in de handler; collecties gebruiken `ReadableMessageIdsAsync` en daarna `WHERE Id IN (...)`.
- Elke fout van OpenFGA (ook een timeout van 3 s) wordt een `PdpUnavailableException`, middleware zet die om in 503. Bij `POST` wordt de rij na een tuple-fout teruggedraaid (compensatie); bij `DELETE` zijn mislukte tuple-deletes onschadelijk.
- Aanpassing `search`: de body is ciphertext, dus `q` zoekt op afzendernaam, plus `from`, `after`, `before` (alles binnen de set van `ListObjects`).
- Auditlog: `authz action=... sub=... message=... decision=...`; nooit body of token.
- Tests: `tests/Unbreakable.Tests` (xunit, `WebApplicationFactory`, JWT's met een eigen testsleutel, stand-in voor OpenFGA met de semantiek van §4.4, echte Postgres via `TEST_CONNECTIONSTRING`). Dekken T1, T4 tot T20, T13 tot T16, T21 (PDP down) en T36. **Nog niet gedekt:** echte-OpenFGA-tests (T22, T23, `fga tuple read`); die doe je na Y4 op de server.
- CI: job `backend-tests` (Postgres-service) in `.github/workflows/security.yml`; `deploy` hangt ervan af.

Lokaal uitvoeren (PowerShell, Postgres draait lokaal of via Docker):
```powershell
$env:TEST_CONNECTIONSTRING = "Host=localhost;Port=5432;Database=unbreakable_test;Username=postgres;Password=postgres"
dotnet build .\app\Unbreakable.csproj
dotnet test .\tests\Unbreakable.Tests\Unbreakable.Tests.csproj
```

- [x] Maak `OpenFgaPep` (één service, geen verspreide checks): *(heet `IMessageAuthorizer` / `OpenFgaMessageAuthorizer`.)*

```csharp
public sealed class OpenFgaPep(OpenFgaClient fga, ILogger<OpenFgaPep> log)
{
    public async Task<bool> CanReadAsync(string sub, int id, CancellationToken ct) =>
        await CheckAsync(sub, "can_read", id, ct);

    public async Task<bool> CanDeleteAsync(string sub, int id, CancellationToken ct) =>
        await CheckAsync(sub, "can_delete", id, ct);

    private async Task<bool> CheckAsync(string sub, string relation, int id, CancellationToken ct)
    {
        try
        {
            var r = await fga.Check(new ClientCheckRequest {
                User = $"user:{sub}", Relation = relation, Object = $"message:{id}"
            }, new ClientCheckOptions {
                Consistency = ConsistencyPreference.HIGHERCONSISTENCY
            }, ct);
            return r.Allowed;
        }
        catch (Exception ex) when (ex is ApiException or HttpRequestException or TaskCanceledException)
        {
            throw new PdpUnavailableException("OpenFGA niet bereikbaar", ex); // → 503
        }
    }
}
```

- [x] Implementeer de PEP als **endpoint-filter** voor objectroutes: *(afwijking: aanroep in de handler via `IMessageAuthorizer`; allow, deny=404 en PDP-fout=503 zijn getest.)*

```csharp
api.MapGet("/messages/{id:int}", GetMessage)
   .AddEndpointFilter(new PdpObjectFilter("can_read"));

api.MapDelete("/messages/{id:int}", DeleteMessage)
   .AddEndpointFilter(new PdpObjectFilter("can_delete"));
```

  De filter leest `id` uit de route, haalt `sub` uit `ctx.User`, roept `Check`, en:
  - allow → handler;
  - deny → `404`;
  - PDP-fout → `503`.

- [x] Collecties: `ListObjects` in de handler, daarna SQL `WHERE Id IN (...)`:

```csharp
var ids = await pep.ReadableMessageIdsAsync(sub, ct);
var inbox = await db.Messages
    .Where(m => ids.Contains(m.Id))
    .OrderByDescending(m => m.SentAt)
    .ToListAsync(ct);
```

- [x] `search`: dezelfde `ids`, plus een **geparameteriseerd** metadata-filter (`Sender`, `SentAt`). Geen `ILike` op `Body` (ciphertext). *(zoekt op afzender en datum, niet op de ciphertext.)*
- [x] `export`: dezelfde `ids`, geordend op `SentAt`.
- [x] **Geen enkele** `if (sub == m.RecipientSub)`-achtige logica in de API. Grep hierop vóór de PR. *(afgedekt door test T36.)*
- [ ] Auditlogging: tijd, `sub`, actie, object, allow/deny, latency, `model_id`, correlation-id. **Geen** body/ciphertext/token. *(deels: `sub`, actie, object en allow/deny/error staan in het log; latency, `model_id` en correlation-id ontbreken nog.)*
- [ ] Integratietests met een echte OpenFGA-container (testcompose): *(open: de huidige tests gebruiken een stand-in voor OpenFGA. Verkeerd model-ID en OpenFGA-down worden op de server getest bij de cutover.)*
  - ontvanger leest, afzender niet;
  - afzender verwijdert, ontvanger niet;
  - derde krijgt `404`;
  - `ListObjects` geeft alleen ontvangen berichten;
  - OpenFGA gestopt → `503`;
  - verkeerd model-ID → `503`;
  - geen token → `401`.

**Klaar wanneer:** `fga model test` groen is, de API in alle gevallen via OpenFGA beslist, deny-tests slagen, en OpenFGA-down fail closed is.

### Y8. Pipeline en branch protection

- [x] Voeg een **build-stap** toe (lab 3-les: de pipeline bouwde niet). *(in PR #15: `docker compose build app`; live na de merge.)*
- [x] Voeg een **modeltest-job** toe: *(in PR #15: `model validate`; de tests van Pros draaien zodra `model.fga.yaml` bestaat.)*

```yaml
  openfga-model-test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@<SHA>
      - name: OpenFGA model tests
        run: |
          curl -fsSL https://github.com/openfga/cli/releases/download/<PIN>/fga_<PIN>_linux_amd64.tar.gz \
            | tar -xz fga
          ./fga model test --tests deploy/openfga/model.fga.yaml
```

  Alternatief: `openfga/action-openfga-test@<SHA>` met `store-file-path`.

- [x] Voeg `docker compose config` toe als validatiestap. *(in PR #15.)*
- [x] Voeg een **smoke-test-job** na deploy toe: *(in PR #15 als stap in `deploy`; de realm-discovery komt erbij na J1.)*
  - `https://g05.../health` → 200;
  - `https://g05.../api/messages` zonder token → 401;
  - `https://auth.../realms/unbreakable/.well-known/openid-configuration` → 200;
  - `https://auth.../admin` → 403;
  - `docker ps` toont geen publieke 8080/8081/5432.
- [x] `deploy` heeft `needs: [sca-dotnet, sast-semgrep, secret-gitleaks, openfga-model-test, build]`. *(in PR #15: `openfga-model` en `compose-validate` erbij.)*
- [x] `permissions: contents: read`, actions gepind op SHA, `concurrency` zodat twee deploys elkaar niet overlappen. *(in PR #15: `concurrency` erbij; de rest stond er al.)*
- [x] Branch protection: PR + 1 approval + status checks; geen force-push naar `main`. *(gedaan 11 nov, bewezen met een geweigerde push, zie `y8-branch-protection.md`.)*
- [ ] Optioneel: images taggen met commit-SHA en rollback via vorige tag. *(niet gedaan, optioneel; de rollback staat in `y9-rollback.md`.)*

**Klaar wanneer:** een bewust kapotte modeltest de deploy skipt; een compileerfout de PR rood maakt; de groene run alle jobs toont.

### Y9. Deploy, secrets en backup

- [x] Deploy via merge naar `main`; controleer de runner-logs. `/etc/unbreakable/.env` moet leesbaar zijn voor de runner-gebruiker (`runner:runner`, `chmod 600`), niet `root:root`. *(gedaan: `.env` is `runner:runner` 600.)*
- [x] Bootstrap-admin van Keycloak verwijderd (zie Y3). Rotatie van de overige geheimen (DB-wachtwoorden, OpenFGA-preshared key) is **niet uitgevoerd, bewuste keuze (11 nov):** geen enkele waarde is ooit getoond, gedeeld of in een log terechtgekomen (alleen de variabelenamen zijn gecontroleerd), de databanken en OpenFGA hebben geen gepubliceerde poort, en `.env` is `runner:runner 600` en niet in de repo. Heroverweeg dit zodra een waarde wel zichtbaar wordt, bijvoorbeeld in een terminal, screenshot of chat.
- [x] `docker inspect` controleren: geen secrets in image/commandline; `.env` niet in de repo. *(gedaan.)*
- [x] Backup: `pg_dump` van app-DB, Keycloak-DB **en OpenFGA-DB**. OpenFGA-tuples zijn de autorisatiewaarheid; een restore van alleen de app-DB breekt de autorisatie. *(drie dumps, zie `y9-backup-restore.md`.)*
- [x] Restore minstens één keer testen (app + OpenFGA samen). *(app en OpenFGA samen hersteld, tellingen gelijk.)*
- [x] Documenteer rollback: `git revert` + redeploy, of vorige image-tag. Let op: een oude modelversie kan tuples bevatten die het nieuwe model niet kent. *(zie `y9-rollback.md`.)*
- [x] Noteer de groene Actions-run-URL voor het verslag. *(zie `y8-ci-pr14.md`.)*

**Klaar wanneer:** prod draait, secrets geroteerd zijn, backup/restore bewezen is, rollback gedocumenteerd is.

### Y10. Overdracht aan Pros

- [ ] Zet je eigen bewijs in `bewijs/yorben/` in de OneDrive-map (niet in de repo) (zie P6). Niemand wacht hierop; Pros leest het pas bij de eindcontrole (P9). *(bijna: Y2 tot Y9 staan erin, zie de README; de P6-rijen volgen na de cutover.)*
- [ ] Los de bevindingen uit de validatie op; noteer elke fix in het logboek.

---

## 6. Werkpakket Jalil – Keycloak (IAM) + SPA/OIDC/PKCE

**Rol:** identity provider volledig (realm, clients, users, hardening) én frontend (loginflow, tokenbeheer, UI, frontendtests).
**Kan starten:** meteen en alleen uitvoeren: Keycloak draait al en je beheert via de SSH-tunnel je eigen realm (zie §8).
**Afstemming:** geen; enkel contractwijzigingen in §4 melden.

> Dit is het grootste pakket. Prioriteer in deze volgorde: **realm + clients → login werkend → hardening → UI-polish**.

### Toegang tot Keycloak (Keycloak draait; beheer enkel via SSH-tunnel)

| | |
|---|---|
| **Admin-console** | `http://localhost:8081/auth/admin/` (enkel via de SSH-tunnel; het publieke `/auth/admin/` geeft 403) |
| **Gebruiker** | je eigen admin-account in de master-realm (aangemaakt door Yorben; het tijdelijke wachtwoord krijg je **privé**, nooit in het plan of de groepschat) |
| **MFA** | zet het aan via `http://localhost:8081/auth/realms/master/account/` → *Signing in* |
| **Master-realm** | enkel voor beheer; de app-realm `unbreakable` maak je zelf aan (J1) |
| **Issuer** | `https://g05.ipv6.thelastgym.be/auth/realms/unbreakable` |
| **Discovery** | `https://g05.ipv6.thelastgym.be/auth/realms/unbreakable/.well-known/openid-configuration` (200 zodra de realm bestaat) |

**Tunnel openen** (PowerShell, SSH-gegevens uit `SSHCredentials.xlsx`):

```powershell
ssh -L 8081:127.0.0.1:8081 -J student@77.42.8.69 root@2a02:1810:a531:7602::105
```

Laat dat venster open en ga naar `http://localhost:8081/auth/admin/`. Via de tunnel gaat het verkeer niet door de WAF, dus de 403's op rolwijzigingen van vroeger komen daar niet voor.

**Eerste dingen na het inloggen:**

- [ ] Kies een nieuw wachtwoord en zet MFA aan.
- [ ] Maak de realm `unbreakable` aan (J1) en daarna pas clients en gebruikers.
- [ ] Bewijs zelf dat de realm werkt: discovery-URL geeft 200.

> Je hoeft op niemand te wachten: Keycloak draait, de tunnel werkt en de publieke console is al afgeschermd.

### J0. Voorbereiding

- [ ] `git checkout main && git pull`
- [ ] `git checkout -b opdracht4-iam` (Keycloak) en `opdracht4-frontend-oidc` (SPA).
- [ ] Lees §4 (contract), de PDF en de bestaande `api.js`, `app.js`, `crypto.js`.
- [ ] Maak een lijst van alle plekken waar nu `currentUser()`, `setUser()`, `localStorage` of `X-User` gebruikt wordt.

### J1. Realm-ontwerp

- [ ] Realm: `unbreakable`, `enabled=true`, `sslRequired=external`.
- [ ] Login-instellingen:
  - registratie uit;
  - login met e-mail uit (gebruikersnamen `Yorben`, `Jalil`, `Pros`);
  - **Edit username uit** (belangrijk: `preferred_username` mag niet wijzigen);
  - "Remember me" uit of beperkt;
  - brute-force detection aan: 5 pogingen, tijdelijke lockout;
  - wachtwoordpolicy: min. 12 tekens, niet gelijk aan username/e-mail, geen triviale wachtwoorden.
- [ ] Tokens/sessies:
  - access token 5 min;
  - SSO idle 30 min;
  - SSO max 10 uur;
  - refresh-tokenrotatie aan (`revokeRefreshToken=true`, `refreshTokenMaxReuse=0`).
- [ ] Clients:
  - `unbreakable-spa`: public, standard flow, **PKCE S256 verplicht**, implicit uit, direct access grants uit, exacte redirect-URI's (`https://g05.../` en `https://g05.../index.html`), web origin `https://g05...`, post-logout-URI exact.
  - `unbreakable-api`: bearer-only (geen flows), audience `unbreakable-api`.
- [ ] Audience-mapper op de SPA-client: `included.custom.audience=unbreakable-api`, access token claim aan, ID-token claim uit.
- [ ] Rollen: realm-rol `user` voor alle drie; **geen** berichtrechten in rollen (die horen in OpenFGA).
- [ ] Users: `Yorben`, `Jalil`, `Pros`, enabled, tijdelijke wachtwoorden **buiten de repo**, eerste login verplicht wachtwoord wijzigen.
- [ ] Admin: eigen admin-account (niet `admin`), sterk wachtwoord, MFA; bootstrap-admin verwijderen.
- [ ] MFA voor gewone users: stretch. Als je het aanzet, documenteer de TOTP-stap in de testinstructies.

**Klaar wanneer:** realm/clients/users bestaan, de discovery-URL 200 geeft (`curl -s https://g05.ipv6.thelastgym.be/auth/realms/unbreakable/.well-known/openid-configuration | jq '{issuer, code_challenge_methods_supported}'`, issuer `https://g05.ipv6.thelastgym.be/auth/realms/unbreakable` en `S256` aanwezig), en de config in een gesanitiseerde `deploy/keycloak/realm-unbreakable.json` staat (geen wachtwoorden/hashes).

### J2. Realm als code en bootstrap

- [ ] Zet de realm-config in `deploy/keycloak/realm-unbreakable.json` (clients, mappers, policies, tokeninstellingen; **geen** users/wachtwoorden).
- [ ] Documenteer de bootstrap: `--import-realm` of `kcadm.sh`-scripts.
- [ ] Users aanmaken via `kcadm.sh` met wachtwoorden uit `.env` of via de adminconsole; nooit in Git.
- [ ] Test: import op een schone Keycloak-DB werkt; bestaande realm wordt niet overschreven.
- [ ] Test: na verwijderen bootstrap-admin kan de eigen admin inloggen.

**Klaar wanneer:** een schone omgeving reproduceerbaar is opgezet en de admin-toegang bewezen is.

### J3. Keycloak-hardening verifiëren

- [ ] Discovery: `issuer`, endpoints, `code_challenge_methods_supported` bevat `S256`.
- [ ] PKCE verplicht: authorize zonder `code_challenge` → fout.
- [ ] Implicit uit: `response_type=token` → fout.
- [ ] Password grant uit: `grant_type=password` → fout.
- [ ] Redirect-URI-manipulatie: andere host/pad → fout.
- [ ] Brute force: 5 foute logins → lockout; admin-events tonen dit.
- [ ] Logout: refresh token werkt niet meer; access token verloopt binnen 5 min.
- [ ] Adminconsole: `/admin` vanaf internet → 403; via `kcadm.sh`/tunnel werkt het.
- [ ] Keycloak-DB: wachtwoorden zijn hashes (argon2/bcrypt), geen plaintext; sessies aanwezig.
- [ ] Keycloak-versie en CVE's checken; image pin en updateplan noteren.
- [ ] Logs: geen wachtwoorden/tokens; Authorization-header niet gelogd.

**Klaar wanneer:** alle punten bewezen zijn met commando-output/screenshots.

### J4. `keycloak-js` vendoren

- [ ] Kies **`keycloak-js`** (zelfde versie als de Keycloak-server) en zet het bestand in `app/wwwroot/vendor/keycloak.js`. Geen CDN (supply chain + CSP).
- [ ] Voeg licentie/versie toe in `vendor/README.md`.
- [ ] Laad het als classic script vóór de module, of maak een kleine ESM-wrapper. Geen `eval`, geen inline script.

**Klaar wanneer:** de library vanaf de eigen origin laadt en de CSP hem toestaat.

### J5. `auth.js` – Keycloak initialiseren

```js
const keycloak = new Keycloak({
    url: 'https://g05.ipv6.thelastgym.be/auth',
    realm: 'unbreakable',
    clientId: 'unbreakable-spa'
});

export async function initAuth() {
    const authenticated = await keycloak.init({
        onLoad: 'login-required',
        pkceMethod: 'S256',
        flow: 'standard',
        checkLoginIframe: false,
        redirectUri: window.location.origin + '/'
    });
    if (!authenticated) await login();
    setInterval(() => {
        keycloak.updateToken(30).catch(() => login());
    }, 20000);
}

export function getToken()    { return keycloak.token; }
export function getSub()      { return keycloak.tokenParsed?.sub || ''; }
export function getUsername() { return keycloak.tokenParsed?.preferred_username || ''; }
export function login()       { return keycloak.login({ redirectUri: window.location.origin + '/' }); }
export function logout()      { return keycloak.logout({ redirectUri: window.location.origin + '/' }); }
```

- [ ] `onLoad: 'login-required'`: de gebruiker moet inloggen vóór de SPA data toont.
- [ ] Geen token in `localStorage`/`sessionStorage`; `keycloak-js` houdt het token in geheugen.
- [ ] Vang `onTokenExpired` op en refresh of login opnieuw.
- [ ] Verifieer in de Network-tab: authorize-request bevat `code_challenge` + `code_challenge_method=S256`; token-request bevat `code_verifier`.

**Klaar wanneer:** login via Keycloak werkt, refresh werkt, en er staat nergens een token in de URL of storage.

### J6. `api.js` – Bearer-token

- [ ] Verwijder `USER_KEY`, `currentUser()` en `setUser()`.
- [ ] Importeer `getToken`, `getUsername`, `getSub` uit `auth.js`.
- [ ] Voeg aan elke request toe: `Authorization: Bearer ${getToken()}`.
- [ ] Bij `401`: `await login()` of een duidelijke "sessie verlopen"-melding.
- [ ] Behoud de foutdetail-weergave uit lab 3, maar toon geen interne stacktraces.
- [ ] Geen `X-User` meer.
- [ ] `cache: 'no-store'` voor API-calls.

**Klaar wanneer:** elke API-call een Bearer-token stuurt en zonder token netjes naar login gaat.

### J7. `app.js` – login/UI/identiteit

- [ ] Verwijder het "Wie ben je?"-formulier en `setUser`.
- [ ] Toon bovenaan: `Je bent <strong>${getUsername()}</strong> (<a id="logout">uitloggen</a>)`.
- [ ] Logout-knop die `logout()` aanroept.
- [ ] Route-guard: geen token/username → login; na logout → login.
- [ ] In `renderNew`: gebruik `sender: getUsername()` **alleen** als invoer voor de MAC-berekening. `api.send()` stuurt enkel `recipient`, `keyLine`, `body` en `mac`; de server zet `sender` uit het token.
- [ ] De gebruiker kan de afzender niet meer vrij invullen.
- [ ] Verwijder de `#/`-naamkeuze; `#/` gaat naar de inbox of login.
- [ ] Houd `escapeHtml`/`textContent` voor alle serverdata; geen `innerHTML` met ongevalideerde data.
- [ ] Zoeken: client-side over de gedecrypteerde berichten (de body is ciphertext); de server filtert alleen metadata. Documenteer dit in de UI ("zoeken gebeurt lokaal").
- [ ] Export: via de beveiligde API; geen tweede code pad.

**Klaar wanneer:** de SPA geen naamkeuze meer heeft, de juiste gebruiker toont, en logout correct werkt.

### J8. `index.html` en CSP

- [ ] Laad `auth.js`/`app.js` als modules; geen inline event handlers of inline scripts.
- [ ] Strikte CSP:
  - `default-src 'self'`;
  - `script-src 'self'`;
  - `style-src 'self'` (Bootstrap lokaal; geen CDN);
  - `connect-src 'self'` (Keycloak is same-origin onder `/auth`);
  - `form-action 'self'`;
  - `frame-ancestors 'none'`.
- [ ] Start met `Content-Security-Policy-Report-Only`; daarna enforce.
- [ ] Geen externe fonts/scripts die de CSP zouden verruimen.

**Klaar wanneer:** de console geen CSP-fouten toont en de loginredirect werkt.

### J9. Frontendtests en bewijs

- [ ] `node --check` op alle JS-bestanden (of via de Node-container).
- [ ] Test in Chromium en Firefox, gewoon + incognito.
- [ ] Test: login → inbox → nieuw bericht → verzenden → lezen → logout.
- [ ] Test: refresh van de pagina (sessie blijft via Keycloak-SSO).
- [ ] Test: logout → terug naar login; oude UI-state weg.
- [ ] Test: 401-afhandeling (token verwijderen in DevTools of wachten tot expiry).
- [ ] Test: ontvangerkeuze werkt met gebruikersnamen (geen subs in de UI).
- [ ] Screenshots voor Pros:
  - authorize-request met `code_challenge` + `S256`;
  - token-request met `code_verifier`;
  - API-request met `Authorization: Bearer` (token afschermen);
  - geen token in URL/storage;
  - logout.
- [ ] PR naar `main`; groene pipeline en een review (die je niet blokkeert). Mergen mag meteen: de overgangsschakelaar (§8.4) zorgt dat je de live app niet breekt. Bouw beide UI-modi (legacy en OIDC) en kies via `GET /api/config`; test tegen de mock-API in `frontend-tests/mock-api/`.

**Klaar wanneer:** de volledige flow werkt, de screenshots er zijn, en de PR groen is.

---

## 7. Werkpakket Pros – OpenFGA-model, vergelijking, threat model, validatie, verslag

**Rol:** PDP-model en tuple-ontwerp, modeltests, de ABAC/ReBAC- en OPA/OpenFGA-analyse, threat model, validatie, verslag.
**Kan starten:** meteen en alleen uitvoeren: model testen op je eigen kopie van §4.4 (zie §8).
**Afstemming:** geen; enkel contractwijzigingen in §4 melden.

### P0. Voorbereiding

- [ ] `git checkout main && git pull`
- [ ] `git checkout -b opdracht4-openfga`
- [ ] Lees de PDF, §3.3–§3.5 van dit plan, en de OpenFGA-docs over `Check`, `ListObjects`, `Write` en model testing.
- [ ] Speel met `play.fga.dev` en `play.openpolicyagent.org`; noteer twee concrete verschillen voor het verslag.

### P1. Model en tuples ontwerpen

- [ ] Model = §4.4 letterlijk. Het bestand `deploy/openfga/model.fga` is van Yorben (Y4); jij test met een eigen kopie. Een modelwijziging gaat eerst via §4.4.
- [ ] Documenteer per relatie: wie schrijft de tuple, wanneer, en wat gebeurt er bij een fout.
- [ ] Bepaal de object-ID-conventie: `message:<id>` met de databank-identity. Geen hergebruik van id's (BIGSERIAL), dus geen tuple-vervuiling na delete.
- [ ] Schrijf de tuple-matrix in `deploy/openfga/README.md`:

| Actie | Tuple | Door wie |
|---|---|---|
| POST | `user:<senderSub> sender message:<id>` | API na insert |
| POST | `user:<recipientSub> recipient message:<id>` | API na insert |
| DELETE | beide verwijderen | API na rij-delete |

**Klaar wanneer:** het model en de tuple-matrix door Yorben en Jalil zijn gereviewd.

### P2. Modeltests (`model.fga.yaml`)

```yaml
name: unbreakable-messages
model_file: model.fga

tuples:
  - { user: user:sub-yorben, relation: sender,    object: message:1 }
  - { user: user:sub-jalil,  relation: recipient, object: message:1 }
  - { user: user:sub-pros,   relation: sender,    object: message:2 }
  - { user: user:sub-yorben, relation: recipient, object: message:2 }
  - { user: user:sub-jalil,  relation: sender,    object: message:3 }
  - { user: user:sub-pros,   relation: recipient, object: message:3 }

tests:
  - name: ontvanger leest, afzender niet, derde niet
    check:
      - user: user:sub-jalil
        object: message:1
        assertions: { can_read: true, can_delete: false }
      - user: user:sub-yorben
        object: message:1
        assertions: { can_read: false, can_delete: true }
      - user: user:sub-pros
        object: message:1
        assertions: { can_read: false, can_delete: false }

  - name: onbekende gebruiker heeft nergens toegang
    check:
      - user: user:sub-onbekend
        object: message:1
        assertions: { can_read: false, can_delete: false }

  - name: inbox bevat enkel ontvangen berichten
    list_objects:
      - user: user:sub-yorben
        type: message
        assertions: { can_read: [message:2] }
      - user: user:sub-jalil
        type: message
        assertions: { can_read: [message:1] }
      - user: user:sub-pros
        type: message
        assertions: { can_read: [message:3] }
      - user: user:sub-onbekend
        type: message
        assertions: { can_read: [] }

  - name: afzender kan enkel eigen berichten verwijderen
    list_objects:
      - user: user:sub-yorben
        type: message
        assertions: { can_delete: [message:1] }
      - user: user:sub-jalil
        type: message
        assertions: { can_delete: [message:3] }
```

- [ ] `fga model test --tests deploy/openfga/model.fga.yaml` lokaal groen.
- [ ] Voeg minstens één **negatieve** test per relatie toe (die heb je hierboven al: afzender mag niet lezen, ontvanger mag niet verwijderen).
- [ ] Noteer in het verslag dat deze tests de **policy** testen, niet de API.

**Klaar wanneer:** alle checks en list-objects slagen, inclusief de deny-cases.

### P3. Bootstrap-script (verplaatst naar Y4)

Het bootstrap-script en `model.fga` zijn van Yorben (Y4), zodat de serverkant niet op Pros hoeft te wachten. Pros reviewt het script wanneer het er is; dat blokkeert niets.

### P4. Tuple-consistentie en herstel

- [ ] Documenteer de dual-write-vensters uit §4.5 en de gekozen compensatie.
- [ ] Schrijf een **reconciliatie-query** (geen job, wel een runbook):

```sql
-- rijen zonder tuple (zouden onzichtbaar moeten zijn)
SELECT m."Id" FROM "Messages" m
LEFT JOIN ... -- vergelijk met fga tuple read --output-format=simple-json
```

- [ ] Leg uit waarom een weesrij **veilig** is (fail closed) en een wees-tuple **onschadelijk** (rij bestaat niet).
- [ ] Noteer het productie-alternatief (outbox + reconciler) in het verslag als toekomstwerk.

**Klaar wanneer:** het runbook bestaat en de redenering in het verslag staat.

### P5. Threat model uitbreiden

- [ ] Voeg de nieuwe flows toe aan het diagram:
  - browser → Keycloak (authorize, code + PKCE);
  - browser → API (Bearer token);
  - API → Keycloak (JWKS/metadata);
  - API → OpenFGA (`Check`, `ListObjects`, `Write`);
  - OpenFGA → OpenFGA-DB;
  - Keycloak → Keycloak-DB;
  - admin → Keycloak via SSH-tunnel/`kcadm.sh`.
- [ ] Voeg de nieuwe trust boundaries toe: Internet, edge/WAF, app, authz, data, IAM.
- [ ] Vul de STRIDE-tabel aan met minstens deze dreigingen, elk met een maatregel.

**Identity provider (Keycloak)**

| Dreiging | Maatregel |
|---|---|
| Token diefstal via XSS | Token in geheugen, strikte CSP, geen inline scripts |
| Token replay | Korte lifetime (5 min), TLS; `jti`-cache als toekomstwerk |
| PKCE-downgrade / ontbrekende PKCE | `pkce.code.challenge.method=S256` verplicht op de client |
| Redirect-URI-manipulatie / open redirect | Exacte redirect-URI's, geen wildcards |
| Mix-up / issuer-verwarring | Exacte `iss`-check, één realm |
| JWT-algorithm-confusion / `none` | Algoritme-allowlist RS256 |
| Audience-confusion | `aud=unbreakable-api` verplicht |
| Keycloak-admin exposure | `/admin` geblokkeerd op de WAF; admin enkel via tunnel |
| Brute force / credential stuffing | Brute-force detection, lockout, sterke wachtwoordpolicy |
| User enumeration | Generieke foutmeldingen, registratie uit |
| Refresh-token diefstal | Rotatie, `refreshTokenMaxReuse=0` |
| Session fixation / logout-zwakte | Korte SSO-idle; logout invalideert het refresh token |
| Keycloak-CVE / patchachterstand | Image pin, CVE-check, updateplan |

**PDP (OpenFGA)**

| Dreiging | Maatregel |
|---|---|
| Tuple poisoning (tuples schrijven buiten de API) | Preshared key, alleen intern netwerk, key in `.env`, rotatie |
| OpenFGA-API of playground publiek | Geen `ports`, playground uit, alleen `app`/`authz` |
| Model-downgrade (laatste model i.p.v. gepind model) | `OPENFGA_MODEL_ID` vast in `.env`, test T22 |
| Stale tuple na delete | De rij is de bestaansbron; `GET` geeft `404` |
| Missing tuple na POST | Fail closed + compensatie + runbook (P4) |
| Eventual consistency | `HIGHERCONSISTENCY` op `Check`/`ListObjects` |
| OpenFGA-down | Fail closed → `503`, timeouts, geen retry-storm |
| OpenFGA-DB-compromis | DB alleen op `authz`, least privilege, versleutelde backup |
| `ListObjects`-limiet / pagination | `StreamedListObjects` boven 1000; documenteren |
| DoS op OpenFGA | Rate limiting, timeouts, resource limits |

- [ ] Werk de oude dreigingen bij:
  - #1 (spoofing login): nu Keycloak + brute force + wachtwoordpolicy;
  - #4 (repudiation): OIDC geeft authenticiteit, maar de gedeelde HMAC-sleutel blijft een beperking;
  - #6 (IDOR): nu PDP/PEP;
  - #11 (metadata): blijft zichtbaar;
  - #16/#17 (afzender/HMAC): `sender` komt nu uit het token, maar de gedeelde HMAC-sleutel geeft nog geen non-repudiation;
  - #19 (DELETE zonder auth): nu alleen afzender via PDP.
- [ ] Voeg data stores toe: Keycloak-DB (hashes/sessies), OpenFGA-DB (tuples), app-DB (ciphertext + `Users`), browser (token in geheugen), logs (subs/IP/paden, authorize-codes in query).
- [ ] Noteer restrisico's: SPA-token in geheugen (XSS), gedeelde HMAC-sleutel, metadata, Keycloak-admin, model/tuple-fout, WAF-bypass, Docker-socket, geen `jti`-replay-cache.
- [ ] Mermaid-diagram + tabellen in `docs/threat-model.md`; screenshot voor het verslag.

**Klaar wanneer:** het threat model alle nieuwe flows, dreigingen en maatregelen bevat en de oude dreigingen bijgewerkt zijn.

### P6. Validatie (ieder zijn eigen rijen)

Elke persoon voert enkel zijn eigen rijen uit en legt het bewijs in `docs/bewijs/<naam>/`. Zo wacht niemand op een ander:

| Eigenaar | Rijen | Hoe |
|---|---|---|
| **Yorben** | T1, T4 tot T23, T33, T34, T36 | geautomatiseerd in het backend-testproject met testtokens (drie gebruikers, vaste `sub`) en een lokale OpenFGA; de rest op de server (`fga tuple read`, OpenFGA stoppen, verkeerd model-ID, logs, pipeline) |
| **Jalil** | T2, T3, T27 tot T30 | op zijn eigen realm en frontend; T3 met een token uit zijn realm |
| **Pros** | T24, T25, T26, T31, T32, T35 | op de bestaande live infrastructuur (poorten, WAF, adminblokkade, databankinhoud) en `fga model test` |

Gebruik twee browsers/incognito. Noteer per test: nummer, gebruiker, message-id, resultaat, screenshot.

Gebruik twee browsers/incognito. Noteer per test: nummer, gebruiker, message-id, resultaat, screenshot.

| # | Test | Verwacht |
|---|---|---|
| T1 | `GET /api/messages` zonder token | 401 |
| T2 | Login via Keycloak | authorize met `code_challenge` + `S256`; token met `code_verifier`; geen token in URL |
| T3 | JWT inspecteren | `iss`, `aud=unbreakable-api`, `azp=unbreakable-spa`, `sub`, korte `exp` |
| T4 | Yorben stuurt naar Jalil | 201; `sender=Yorben`; body bevat geen `sender` nodig |
| T5 | **OpenFGA-tuples na POST** | `fga tuple read` toont `sender` + `recipient` voor het nieuwe id |
| T6 | Jalil leest | 200 |
| T7 | Yorben leest hetzelfde bericht | 404 |
| T8 | Pros leest hetzelfde bericht | 404 |
| T9 | Jalil (ontvanger) probeert DELETE | 404 |
| T10 | Yorben (afzender) DELETE | 204; rij weg **en** tuples weg |
| T11 | Na DELETE opnieuw lezen | 404 |
| T12 | IDOR: id wijzigen als andere user | 404 |
| T13 | `X-User: Yorben` zonder token | 401 |
| T14 | Token met verkeerde audience/issuer | 401 |
| T15 | Verlopen token | 401 |
| T16 | Token met `alg=none` (handmatig) | 401 |
| T17 | Inbox Jalil | alleen berichten met `recipient`-tuple; geen verzonden berichten |
| T18 | Zoeken als Jalil | alleen uit de `ListObjects`-set; geen vreemde berichten |
| T19 | Export als Jalil | exact dezelfde set als de inbox |
| T20 | Export als Yorben | geen berichten die hij alleen verzond |
| T21 | OpenFGA stoppen | API geeft 503, geen data (fail closed) |
| T22 | Verkeerd `OPENFGA_MODEL_ID` | API geeft 503 (fail closed) |
| T23 | OpenFGA-preshared key fout | API geeft 503, geen data |
| T24 | OpenFGA direct vanaf internet | geen verbinding (poort niet gepubliceerd) |
| T25 | `fga model test` | groen; deny-tests slagen |
| T26 | `https://auth.../admin` vanaf internet | 403 |
| T27 | 5 foute wachtwoorden | lockout/vertraging |
| T28 | Logout + refresh token | refresh faalt; access token verloopt ≤5 min |
| T29 | PKCE downgrade (zonder challenge) | Keycloak weigert |
| T30 | Redirect-URI-manipulatie | Keycloak weigert |
| T31 | `docker ps` / `ss -tlnp` | alleen 80/443 publiek; geen 8080/8081/5432 |
| T32 | App-DB / OpenFGA-DB | app: ciphertext; Keycloak: hashes; OpenFGA: tuples |
| T33 | Logs | geen Authorization-header, token of berichtinhoud |
| T34 | Pipeline | modeltest/build/smoke groen; kapotte modeltest skipt deploy |
| T35 | WAF | SQLi/XSS → 403; auth-endpoints rate-limited |
| T36 | Grep op autorisatielogica | geen `RecipientSub ==` / `SenderSub ==` in de API |

**Bewijs:** screenshots van Network-tab, JWT-claims (signature blurren), `fga query check`/`list-objects`-output, `fga tuple read` vóór/na POST en DELETE, allow/deny-matrix, DB-queries, pipeline-run, WAF 403, poorten.

### P7. De vergelijking schrijven (kern van het verslag)

- [ ] Gebruik §3.3 en §3.4 als basis, maar maak het **eigen** en concreet:
  - twee voorbeelden waarin ABAC en ReBAC tot een ander antwoord leiden;
  - één concreet voorbeeld waarin OPA beter is (tijd/IP/MFA-context);
  - waarom `ListObjects` de inbox/zoek/export oplost en OPA niet;
  - wat de PDP weet (tuples) en wat niet (inhoud, attributen);
  - het verdict: OpenFGA voor deze opdracht, met de grens benoemd.
- [ ] Vermijd algemene marketingtaal; gebruik onze eigen cijfers (aantal tuples, aantal calls per inbox-load, latency).

**Klaar wanneer:** de vergelijking minstens 350 van de 1200 woorden vult en door iemand anders naverteld kan worden.

### P8. Verslag (max. 1200 woorden, excl. screenshots/threat model/AI-verantwoording)

| Hoofdstuk | Inhoud | Budget | Screenshots |
|---|---|---|---|
| Inleiding | Doel, repo, groene Actions-run | 80 | — |
| 1. Architectuur | SPA/API/PEP/PDP/Keycloak, AAA, netwerksegmentatie | 120 | diagram |
| 2. IdP-keuze | Keycloak vs Auth0 | 120 | — |
| 3. OIDC + PKCE + tokenvalidatie | code flow, verifier/challenge, state/nonce, wat de API controleert, `sub` vs `preferred_username` | 250 | T2/T3 |
| 4. ABAC vs ReBAC | paradigma's, onze twee regels, filtervraag | 200 | — |
| 5. OPA vs OpenFGA + verdict | vergelijking, `ListObjects`, wanneer OPA beter is | 200 | — |
| 6. Koppeling | PEP, tuples bij POST/DELETE, inbox/zoek/export, fail closed, 404, consistentievensters | 150 | T5/T17–T20 |
| 7. Testen | T1–T36 samengevat, allow/deny-matrix | 60 | T4–T13 |
| 8. Conclusie/reflectie | wat werkt, wat niet, restrisico's | 60 | — |
| Threat model | apart document, telt niet mee | — | diagram/tabel |
| AI-verantwoording | per persoon, telt niet mee | — | — |

- [ ] PDF met schooltemplate, ≤1200 woorden, alle screenshots, link naar repo en groene run.
- [ ] **Woordenteller**: tel na het schrijven en schrap liever in hoofdstuk 1 dan in 4/5.

**Klaar wanneer:** PDF klaar is, de telling klopt, en elke bewering naar een test of screenshot verwijst.

### P9. Eindcontrole

- [ ] Alle acceptatiecriteria uit §1.1 en §1.2 afgevinkt.
- [ ] Geen `X-User`, geen naamkeuze, geen token in storage/URL.
- [ ] Overgangsschakelaar (`AUTH_MODE`, `/api/config`, legacy-pad) volledig verwijderd (checklist in §8.4).
- [ ] Alle API-routes authenticated; objecttoegang via PEP → OpenFGA.
- [ ] Geen autorisatievergelijkingen in de API (T36).
- [ ] Tuples correct bij POST en DELETE (T5/T10).
- [ ] Inbox/zoek/export via `ListObjects` (T17–T20).
- [ ] Fail closed bewezen (T21–T23).
- [ ] Keycloak-admin niet publiek; brute force/PKCE/redirects bewezen.
- [ ] Alleen 80/443 publiek; OpenFGA/DB's/Keycloak-admin intern.
- [ ] Geen secrets in Git/images/logs; Gitleaks groen.
- [ ] Pipeline groen; modeltests en smoke-tests groen.
- [ ] Threat model + verslag + screenshots klaar.

---

## 8. Samenhang en planning

### 8.1 Onafhankelijkheidsregels (herschreven op 11 nov)

**Doel:** elk werkpakket is volledig uit te voeren, te testen en te mergen zonder dat een ander eerst iets afwerkt. Daarvoor vier regels:

1. **Eigen bestanden.** Elk bestand of elke map heeft één eigenaar (§8.2). Niemand werkt in andermans bestanden, dus geen mergeconflicten en geen "wacht even op mijn PR".
2. **Contract in plaats van collega.** Alles wat je van een ander zou nodig hebben staat al vast in §4 (hostnamen, endpoints, claims, OpenFGA-model, tuples). Je bouwt en test ertegen met een eigen stand-in (§8.2).
3. **Eigen bewijs.** Ieder voert zijn eigen testrijen uit (P6) en legt het bewijs in `docs/bewijs/<naam>/`. Niemand wacht op een ander voor screenshots.
4. **Merge in willekeurige volgorde.** Door de overgangsschakelaar (§8.4) breekt geen enkele PR de live app.

Wijzigt iemand het contract, dan eerst §4 aanpassen en melden. Dat is de enige afstemming die nodig blijft.

### 8.2 Eigenaarschap en stand-ins

| | Yorben | Jalil | Pros |
|---|---|---|---|
| **Eigen bestanden** | `app/**` behalve `app/wwwroot/**`; `docker-compose.yml`; `.github/**`; `deploy/openfga/model.fga`; `deploy/openfga/bootstrap.sh`; server-`.env` | `deploy/keycloak/**`; `app/wwwroot/**` (incl. `index.html` met CSP als meta-tag en `lib/keycloak-js`); `frontend-tests/**` | `deploy/openfga/model.fga.yaml`; `deploy/openfga/README.md`; `docs/**` (threat model, vergelijking, verslag); `scripts/validatie/**` |
| **Taken** | Y4, Y5, Y6, Y7, Y8, Y9, Y10 | J1 tot J9 (realm, Keycloak, frontend) | P1, P2, P4, P5, P6 (eigen rijen), P7, P8, P9 |
| **Stand-in voor de anderen** | JWT's met een eigen testsleutel voor drie testgebruikers met vaste `sub`; lokale OpenFGA-container met het model uit §4.4; fake `IAuthorizationService` voor unit tests | mock-API volgens §4.2 in `frontend-tests/mock-api/` (incl. `GET /api/config`); eigen realm als IdP | het model uit §4.4 in een eigen kopie voor `fga model test`; testgebruikers met vaste `sub` |
| **Bewijs dat jouw deel af is** | testproject groen in de pipeline; `fga tuple read`; `AUTH_MODE=oidc` live | discovery van `unbreakable` geeft 200; frontendtests groen | `fga model test` groen; threat model; verslag |

### 8.3 Planning per persoon (alles alleen uitvoerbaar)

**Yorben**
1. **Y6** databankschema. 2. **Y5** JWT-validatie, getest met de eigen testsleutel. 3. **Y4** `model.fga` (§4.4 letterlijk) + `bootstrap.sh`, zelf uitvoeren op de server, ID's in `/etc/unbreakable/.env`. 4. **Y7** PEP + SDK met de overgangsschakelaar (§8.4). 5. **Y8** pipeline en branch protection. 6. **Y9** deploy, secrets, backup. 7. Eigen P6-rijen en bewijs in `bewijs/yorben/` in de OneDrive-map. 8. Cutover: `AUTH_MODE=oidc` (§8.4).

**Jalil**
1. **J1** realm `unbreakable` aanmaken en zelf bewijzen (discovery 200). 2. **J2** realm als code. 3. **J4** `keycloak-js` vendoren. 4. **J5 tot J8** login, Bearer, UI en CSP tegen de mock-API én zijn eigen realm; beide UI-modi uit §8.4. 5. **J3** hardening. 6. **J9** frontendtests en eigen P6-rijen, bewijs in `docs/bewijs/jalil/`.

**Pros**
1. **P1** tuple-matrix en object-ID-conventie. 2. **P2** modeltests op zijn eigen kopie van §4.4. 3. **P4** runbook. 4. **P5** threat model. 5. **P6** eigen rijen (enkel wat al nu op de live infrastructuur te bewijzen is) en bewijs in `docs/bewijs/pros/`. 6. **P7** vergelijking en **P8** verslag schrijven (met plaatshouders voor bewijs). 7. **P9** eindcontrole is de laatste stap: die leest enkel de bewijsmappen, en niemand wacht erop.

### 8.4 Overgangsschakelaar: cutover zonder wachten (goedgekeurd door Yorben op 11 nov, op voorwaarde dat het gedocumenteerd is en achteraf aantoonbaar verwijderd wordt)

- De API heeft een instelling `Auth__Mode` met `legacy` (huidig gedrag, standaard) of `oidc` (JWT + PEP). Beide codepaden zitten in één build tot de opruim-PR.
- Nieuw publiek endpoint `GET /api/config` → `{"authMode":"legacy"|"oidc"}`. Staat in §4.2.
- De frontend leest `/api/config` bij het laden en toont de legacy-UI of de OIDC-loginflow.
- `docker-compose.yml` geeft door: `Auth__Mode: "${AUTH_MODE:-legacy}"`.
- **Cutover doet Yorben alleen:** hij controleert zelf (1) discovery van `unbreakable` geeft 200, (2) `OPENFGA_STORE_ID` en `OPENFGA_MODEL_ID` staan in `.env`, (3) de frontend-PR staat in `main`. Dan zet hij `AUTH_MODE=oidc` in `/etc/unbreakable/.env` en herstart de app. Terugdraaien is dezelfde regel terugzetten.
- Na een geslaagde cutover verwijdert een opruim-PR (Yorben, Jalil) het legacy-pad en de `X-User`-code. Testen T13 en T36 gelden pas dan.
- Gevolg: API-PR, frontend-PR, realm en model mogen in willekeurige volgorde mergen.

**Tijdelijk en verplicht te verwijderen (opruim-PR, eigenaar Yorben, reviewer Jalil).** De schakelaar is een overgangsmaatregel en mag niet in de eindoplevering blijven. Het legacy-pad vertrouwt een door de client opgegeven naam (`X-User`), dus het is precies wat de opdracht wil uitsluiten. De opruim-PR is pas klaar als dit allemaal af is:

- [ ] Cutover is gebeurd (`AUTH_MODE=oidc` op de server) en de OIDC-flow is bewezen met T2, T3 en T4.
- [ ] In `app/`: legacy-codepad en `X-User`-verwerking verwijderd; `Auth__Mode`, de modus-logica en `GET /api/config` weg. De API kent enkel nog JWT + PEP.
- [ ] In `app/wwwroot/`: legacy-UI en het lezen van `/api/config` verwijderd (Jalil); de frontend start altijd de OIDC-login.
- [ ] In `docker-compose.yml`, `.env.example` en `/etc/unbreakable/.env`: `AUTH_MODE` / `Auth__Mode` verwijderd.
- [ ] Het endpoint `/api/config` uit de tabel in §4.2 en deze §8.4 verwijderd of als "verwijderd op <datum>" gemarkeerd.
- [ ] Controle: `grep -rniE "X-User|AUTH_MODE|Auth__Mode|authMode|api/config|legacy" app deploy docker-compose.yml .env.example` geeft **geen** treffers (op een eventuele bewuste vermelding in de documentatie na).
- [ ] T13 (`X-User` zonder token → 401) en T36 (geen autorisatievergelijkingen in de API) zijn groen en vastgelegd in `docs/bewijs/`.
- [ ] Vermelding in het verslag/het logboek dat de schakelaar bestond, wanneer ze is verwijderd en waarom.

Deze opruiming staat ook in §11 (definition of done) en bij P9 (eindcontrole).

### 8.5 Reviewafspraken

- Yorben reviewt Jalil (frontend/token) en Pros (model/tuples).
- Jalil reviewt Yorben (API/PEP) op correctheid van het contract.
- Pros reviewt beide op security-aspecten en documentatie.
- Niemand merget zijn eigen PR zonder review. Een review blokkeert niemand: je gaat verder met je volgende taak.
- Houd PR's klein en review meteen na melding in de groep; blijft een review een halve dag liggen, dan mag de derde persoon reviewen zodat niemand wacht.

---

## 9. Secure-basis checklist

### 9.1 Authenticatie

- [ ] OIDC authorization code flow + PKCE S256; geen implicit, geen password grant.
- [ ] Exacte redirect-URI's en web origins; geen wildcards.
- [ ] `state` en `nonce` worden door de library gevalideerd.
- [ ] Korte access tokens (5 min), refresh-rotatie, correcte logout.
- [ ] Geen token in URL, `localStorage`, `sessionStorage` of DOM.
- [ ] Keycloak-admin niet publiek; MFA voor admin.
- [ ] Brute-force detection + wachtwoordpolicy.
- [ ] `sub` is de autorisatie-identiteit; `preferred_username` alleen weergave.
- [ ] Username-editing uit in Keycloak.

### 9.2 Autorisatie

- [ ] Alle `/api`-routes vereisen een geldig token.
- [ ] Elk object (lezen/verwijderen) gaat via PEP → OpenFGA.
- [ ] Geen `if (sub == recipientSub)`-logica in de API.
- [ ] Model in Git, met tests; PR-review op modelwijzigingen.
- [ ] Model-ID vastgepind; modelwijziging = bewuste actie.
- [ ] Fail closed bij PDP-fout/timeout/verkeerd model.
- [ ] 404 bij geen toegang (geen bestaanslek).
- [ ] Auditlog per beslissing, zonder inhoud/token.
- [ ] `sender` komt uit het token, nooit uit de body/header.
- [ ] `recipient` gevalideerd tegen de `Users`-tabel.
- [ ] Tuples geschreven bij POST en verwijderd bij DELETE.
- [x] OpenFGA alleen intern bereikbaar; preshared key buiten de repo. *(bewezen: geen gepubliceerde poort, `y9-backup-restore.md`.)*

### 9.3 Transport en netwerk

- [x] Alleen 80/443 publiek; app/DB's/OpenFGA/Keycloak-admin intern. *(bewezen: `docker ps`, `y9-backup-restore.md`; Keycloak-admin enkel via tunnel, `y3b-admin-tunnel.md`.)*
- [ ] TLS 1.2/1.3, HSTS, geldige certs.
- [x] Interne netwerken `internal: true`; geen onnodige publicaties. *(`app`, `authz`, `backend`, `iam` zijn `internal: true`.)*
- [ ] WAF + rate limiting op auth- en API-endpoints.
- [ ] Security headers/CSP; start report-only, dan enforce.
- [x] Geen directe app-poort (8080) meer. *(poort 8080 is niet gepubliceerd.)*
- [ ] OpenFGA-playground en metrics uit.

### 9.4 Secrets en data

- [x] `.env` buiten de repo, `chmod 600`; `.env.example` met placeholders. *(bewezen: `y9-backup-restore.md`.)*
- [ ] Geen secrets in Git, images, logs of prompts.
- [ ] Gitleaks groen; allowlist alleen voor bewezen lab-fakes.
- [ ] App-DB: alleen ciphertext; Keycloak-DB: hashes; OpenFGA-DB: tuples.
- [ ] Aparte DB-users/rollen; least privilege.
- [ ] Backup/restore getest (app **en** OpenFGA); rotatie van wachtwoorden en preshared key.

### 9.5 Operatie en supply chain

- [ ] Images/actions gepind (digest/SHA); minimale permissions.
- [ ] Build-, model- en smoke-tests in de pipeline; deploy alleen na groen.
- [x] Branch protection; geen directe push naar `main`. *(bewezen: `y8-branch-protection.md`.)*
- [ ] Healthchecks, resource limits, `restart: unless-stopped`.
- [ ] Keycloak/OpenFGA/app patchstrategie; CVE-check.
- [x] Rollback gedocumenteerd (revert of vorige image-tag + vorige model-ID). *(`y9-rollback.md`.)*
- [ ] Docker-socket/runner least privilege (lab 2-maatregel blijft).

### 9.6 Logging en privacy

- [ ] Geen berichtinhoud, ciphertext, tokens of wachtwoorden in logs.
- [ ] Authorization-header niet loggen; authorize-codes in query redigeren/beperken.
- [ ] Auditlogs met sub/actie/object/allow-deny/tijd/latency/model-ID.
- [ ] Log-retentie en toegang beperkt.
- [ ] Foutmeldingen lekken geen interne details.

---

## 10. Risico's en fallbacks

| Risico | Kans | Impact | Maatregel/fallback |
|---|---|---|---|
| DNS/Let's Encrypt voor `auth...` duurt te lang | Middel | Hoog | Vraag DNS dag 0 aan; BunkerWeb staging; als het echt niet kan, path-based `/auth` of tijdelijke docentoplossing |
| Keycloak-admin lockout | Laag | Hoog | Bootstrap-admin pas verwijderen na test van tweede admin; `kcadm.sh`-toegang; recovery documenteren |
| CORS/token-endpoint werkt niet in browser | Middel | Middel | Web origin exact instellen; test met curl en browser; Keycloak-logs bekijken |
| CSP breekt `keycloak-js`/Bootstrap | Middel | Middel | Start report-only; script/style lokaal; geen inline; stap voor stap enforce |
| WAF blokkeert OIDC/token-requests | Middel | Middel | ModSecurity-tuning; token-endpoint uitsluiten van te agressieve regels; WAF niet globaal uitzetten |
| **Tuple-write faalt na insert** | Middel | Middel | Compensatie-delete + fail closed; weesrij is onzichtbaar; runbook P4 |
| **Tuple-delete faalt na rij-delete** | Laag | Laag | Wees-tuple onschadelijk; `OnMissingDeletes=Ignore`; reconciliatie |
| **Model-ID niet gepind** | Middel | Hoog | `OPENFGA_MODEL_ID` in `.env`; test T22; review op modelwijzigingen |
| **OpenFGA-preshared key lekt** | Laag | Hoog | `.env` 600, niet loggen, rotatie, alleen intern netwerk |
| OpenFGA-down | Laag | Middel | Fail closed + timeouts; geen retry-storm; healthcheck/alerts |
| `ListObjects` > 1000 objecten | Laag | Laag | `StreamedListObjects`; pagination documenteren |
| OpenFGA-eventual-consistency | Middel | Laag | `HIGHERCONSISTENCY` op Check/ListObjects; latency meten |
| OpenFGA/Keycloak-CVE | Laag | Hoog | Nieuwste stabiele versie, image pin, updateplan, admin niet publiek |
| Tijdgebrek door dubbele rol Jalil | Middel | Hoog | Prioriteit: realm + login > hardening > UI-polish; hardening mag naar dag 3 |
| Tokens in logs | Middel | Hoog | Geen token in query; Authorization-header niet loggen; authorize-code redigeren |
| Tijdgebrek algemeen | Middel | Hoog | Eerst happy path + deny-tests; MFA/extra's als stretch; duidelijke taakverdeling |

---

## 11. Definition of done

- [ ] Login via Keycloak (code + PKCE) werkt in de browser.
- [ ] API accepteert alleen geldige tokens (signature/iss/aud/exp/RS256/azp) en weigert al de rest.
- [ ] `X-User` en de naamkeuze zijn volledig verwijderd.
- [ ] De overgangsschakelaar (`AUTH_MODE`, `/api/config`, legacy-pad) is volledig verwijderd en bewezen met de grep uit §8.4.
- [ ] Alleen de ontvanger leest; alleen de afzender verwijdert; derden krijgen 404.
- [ ] Alle autorisatiebeslissingen lopen via PEP → OpenFGA; modeltests groen.
- [ ] Tuples worden bij elk nieuw bericht geschreven en bij delete verwijderd.
- [ ] Inbox, zoeken en export krijgen hun zichtbaarheid uit `ListObjects`.
- [ ] Geen autorisatievergelijkingen meer in de API-code (T36).
- [ ] OpenFGA-down of verkeerd model = 503, geen data.
- [ ] Keycloak hardening bewezen (PKCE, redirects, brute force, korte tokens, admin-blok).
- [x] Alleen 80/443 publiek; OpenFGA/DB's/Keycloak-admin intern. *(bewezen: `y9-backup-restore.md`.)*
- [ ] Geen secrets in Git/images/logs; `.env` op de server; Gitleaks groen.
- [ ] Pipeline: build + modeltests + smoke-tests groen; deploy alleen na groen.
- [ ] Threat model bijgewerkt; validatie met screenshots; verslag ≤1200 woorden.
- [ ] ABAC-vs-ReBAC en OPA-vs-OpenFGA staan uitgewerkt in het verslag.
- [ ] AI-verantwoording per persoon.
- [ ] Backup/restore (app + OpenFGA) en rollback getest.
- [ ] Iedereen kan de flow, de tokens, de PEP/PDP, het model en de tuples uitleggen zonder notas.

---

## 12. Appendix – nuttige commando's

### 12.1 OpenFGA lokaal testen

```bash
# CLI installeren (pin de versie)
# https://github.com/openfga/cli/releases

# Modeltests (geen server nodig)
fga model test --tests deploy/openfga/model.fga.yaml

# Model valideren/syntax checken
fga model validate --file deploy/openfga/model.fga
```

### 12.2 Store bootstrap en model-ID

```bash
export FGA_API_URL=http://openfga:8080
export FGA_API_TOKEN="$OPENFGA_API_TOKEN"

# Store + model in één keer
fga store create --name unbreakable --model deploy/openfga/model.fga

# Later: alleen het model bijwerken (nieuwe immutabele versie)
fga model write --file deploy/openfga/model.fga

# Actuele model-ID
fga model get --field id --format json
```

### 12.3 Tuples debuggen

```bash
# Eén tuple schrijven (zoals de API doet)
fga tuple write user:<senderSub> sender message:1
fga tuple write user:<recipientSub> recipient message:1

# Alle tuples lezen
fga tuple read

# Beslissingen naspelen
fga query check user:<recipientSub> can_read message:1
fga query check user:<senderSub> can_delete message:1
fga query list-objects user:<recipientSub> can_read message

# Opruimen
fga tuple delete user:<senderSub> sender message:1
```

### 12.4 OpenFGA vanuit het API-netwerk testen

```bash
# Vervang <project> door de compose-projectnaam
docker run --rm --network <project>_app curlimages/curl:latest \
  -s -X POST http://openfga:8080/stores/$OPENFGA_STORE_ID/check \
  -H "Authorization: Bearer $OPENFGA_API_TOKEN" \
  -H 'Content-Type: application/json' \
  -d '{"tuple_key":{"user":"user:<sub>","relation":"can_read","object":"message:1"},"authorization_model_id":"'$OPENFGA_MODEL_ID'"}'
# verwacht: {"allowed":true}
```

> Zonder `Authorization`-header moet dit `401` geven. Test dat expliciet: het bewijst dat de preshared key werkt.

### 12.5 Keycloak beheren via `kcadm.sh`

```bash
# Inloggen als admin (wachtwoord uit .env, niet in shell history)
docker compose exec keycloak /opt/keycloak/bin/kcadm.sh config credentials \
  --server http://localhost:8080 --realm master \
  --user "$KC_ADMIN_USER" --password "$KC_ADMIN_PASSWORD"

# Realm aanmaken (als je niet importeert)
docker compose exec keycloak /opt/keycloak/bin/kcadm.sh create realms \
  -s realm=unbreakable -s enabled=true -s sslRequired=external

# Client aanmaken (indicatief; controleer de exacte JSON-quoting)
docker compose exec keycloak /opt/keycloak/bin/kcadm.sh create clients -r unbreakable \
  -s clientId=unbreakable-spa -s publicClient=true -s standardFlowEnabled=true \
  -s implicitFlowEnabled=false -s directAccessGrantsEnabled=false \
  -s 'redirectUris=["https://g05.ipv6.thelastgym.be/","https://g05.ipv6.thelastgym.be/index.html"]' \
  -s 'webOrigins=["https://g05.ipv6.thelastgym.be"]' \
  -s 'attributes={"pkce.code.challenge.method":"S256"}'

# Gebruiker + tijdelijk wachtwoord
docker compose exec keycloak /opt/keycloak/bin/kcadm.sh create users -r unbreakable \
  -s username=Yorben -s enabled=true -s emailVerified=true
docker compose exec keycloak /opt/keycloak/bin/kcadm.sh set-password -r unbreakable \
  --username Yorben --new-password "$YORBEN_PASSWORD" --temporary
```

> Gebruik de adminconsole of de realm-JSON voor complexe configuratie; `kcadm.sh` is handig voor users, checks en recovery.

### 12.6 JWT inspecteren (zonder naar een website te sturen)

```bash
TOKEN='...'
python -c "import base64,json,sys; p=sys.argv[1].split('.')[1]; p+='='*(-len(p)%4); print(json.dumps(json.loads(base64.urlsafe_b64decode(p)), indent=2))" "$TOKEN"
```

> Plak echte tokens **niet** in jwt.io of een andere externe site. De API verifieert de signature; lokaal decoderen is alleen voor debuggen.

### 12.7 Smoke-tests

```bash
curl -s -o /dev/null -w '%{http_code}\n' https://g05.ipv6.thelastgym.be/health
# 200

curl -s -o /dev/null -w '%{http_code}\n' https://g05.ipv6.thelastgym.be/api/messages
# 401

curl -s -o /dev/null -w '%{http_code}\n' \
  https://g05.ipv6.thelastgym.be/auth/realms/unbreakable/.well-known/openid-configuration
# 200

curl -s -o /dev/null -w '%{http_code}\n' https://g05.ipv6.thelastgym.be/auth/admin
# 403
```

### 12.8 Poorten en netwerken controleren

```bash
docker ps --format 'table {{.Names}}\t{{.Ports}}'
ss -tlnp | grep -E ':(80|443|8080|8081|5432)\b'
```

Verwacht: alleen 80/443 (en SSH) publiek; 8081 eventueel op `127.0.0.1`; geen 8080/5432.

### 12.9 PKCE handmatig testen (voor het verslag)

```bash
VERIFIER=$(openssl rand -base64 32 | tr '+/' '-_' | tr -d '=')
CHALLENGE=$(printf %s "$VERIFIER" | openssl dgst -binary -sha256 | openssl base64 -A | tr '+/' '-_' | tr -d '=')

echo "Open:
https://g05.ipv6.thelastgym.be/auth/realms/unbreakable/protocol/openid-connect/auth?client_id=unbreakable-spa&response_type=code&scope=openid%20profile&redirect_uri=https%3A%2F%2Fg05.ipv6.thelastgym.be%2F&code_challenge=$CHALLENGE&code_challenge_method=S256&state=test"
```

Na login krijg je `?code=...&state=test`. Wissel de code in:

```bash
curl -s -X POST https://g05.ipv6.thelastgym.be/auth/realms/unbreakable/protocol/openid-connect/token \
  -H 'Content-Type: application/x-www-form-urlencoded' \
  -d "grant_type=authorization_code&client_id=unbreakable-spa&code=$CODE&redirect_uri=https%3A%2F%2Fg05.ipv6.thelastgym.be%2F&code_verifier=$VERIFIER" | jq
```

Verwacht: `access_token`, `refresh_token`, `token_type: Bearer`, `expires_in: 300`.

### 12.10 BunkerWeb multisite (indicatief)

> **Let op:** dit is het alternatief met een eigen hostnaam `auth.g05...` en werkt alleen als dat DNS-record bestaat. Zonder record draait Keycloak onder `/auth` (zie "Voortgang en afwijkingen"): dan zijn `/auth/admin` en `/auth/realms/master` de paden die je moet afschermen (J3/Y3), zodra de eerste setup klaar is. Tot die tijd is de admin-console via de publieke site bereikbaar; gebruik daarom een sterk `KC_BOOTSTRAP_ADMIN_PASSWORD` en zet de afscherming er direct na Y3 op.

```yaml
services:
  bunkerweb:
    environment:
      MULTISITE: "yes"
      SERVER_NAME: "g05.ipv6.thelastgym.be auth.g05.ipv6.thelastgym.be"
      AUTO_LETS_ENCRYPT: "yes"
      EMAIL_LETS_ENCRYPT: "${LETSENCRYPT_EMAIL}"
      USE_STAGING_LETS_ENCRYPT: "yes"   # eerst staging, daarna "no"

      g05.ipv6.thelastgym.be_USE_REVERSE_PROXY: "yes"
      g05.ipv6.thelastgym.be_REVERSE_PROXY_HOST: "http://app:8080"

      auth.g05.ipv6.thelastgym.be_USE_REVERSE_PROXY: "yes"
      auth.g05.ipv6.thelastgym.be_REVERSE_PROXY_HOST: "http://keycloak:8080"
```

- Zet `USE_STAGING_LETS_ENCRYPT` pas op `"no"` na een geslaagde staging-run.
- Blokkeer `/admin` en `/realms/master` op de auth-site; test dat `https://auth.../admin` vanaf internet `403` geeft.
- Voeg rate limiting toe op `/realms/*/protocol/openid-connect/token` en op `POST /api/messages`.
- Laat BunkerWeb geen `Authorization`-header loggen en beperk/redigeer de query string voor authorize-requests (daar staat de eenmalige `code` in).

---

## 13. AI-verantwoording (aanvullen per persoon)

> Voor dit plan en de implementatie gebruikten we **[tool/model]** op **[datum]**. Gebruik: opdracht samenvatten, architectuur/taakverdeling voorstellen, OpenFGA-model en SDK-aanroepen uitzoeken, testmatrix en threat-modelaanvulling. De AI heeft geen echte wachtwoorden, tokens of codeboeklijnen gezien. Wij hebben alle voorstellen gelezen, aangepast en getest; de verantwoordelijkheid ligt bij ons.

Vul per persoon aan:

- **Yorben:** welke prompts, welke code overgenomen/aangepast (compose, JWT, PEP, SDK), welke tests zelf uitgevoerd.
- **Jalil:** idem voor Keycloak-realm/hardening en de frontend/loginflow.
- **Pros:** idem voor het OpenFGA-model, de vergelijking, het threat model en het verslag.

---

**Einde plan.**
