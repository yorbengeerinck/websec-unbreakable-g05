# Bewijs Jalil (J3, J5 tot J9)

De J3-tests zijn uitgevoerd op 17 november 2026, de login-, token- en CSP-bewijzen op 27 november 2026. Alles tegen de realm `unbreakable`, client `unbreakable-spa`. Er staan geen wachtwoorden, tokens of cookies in dit bewijs. Waar een gebruikersnaam en wachtwoord nodig waren voor een test die moest falen, zijn verzonnen waarden gebruikt. Waar een `state`, `nonce` of `code` zichtbaar was, is die weggeblurd.

Pad naar Keycloak. De publieke route loopt via BunkerWeb op `https://g05.ipv6.thelastgym.be/auth`. De directe route (SSH-tunnel naar `localhost:8081`) gaat langs BunkerWeb heen. BunkerWeb vervangt 4xx-antwoorden van Keycloak door zijn eigen foutpagina. Waar de Keycloak-melding zelf nodig was, is daarom de directe route gebruikt.

## J3, Keycloak-hardening

| Bestand | Test | Resultaat |
|---|---|---|
| `J3-01-pkce-zonder-challenge.png` | authorize-request zonder `code_challenge` | Keycloak stuurt terug met `error=invalid_request` en `Missing parameter: code_challenge_method`. Geen loginformulier, geen code. |
| `J3-02-implicit-uit.png` | authorize-request met `response_type=token` (Network-tab) | Status 302, `Location` met `error=unauthorized_client` en `Implicit flow is disabled for the client`. |
| `J3-02a-client-capability-config.png` | instellingen van `unbreakable-spa` | Standard flow aan. Implicit flow en Direct access grants uit. Require PKCE aan met methode S256. |
| `J3-03-password-grant-uit.png` | POST naar het token-endpoint met `grant_type=password` (directe route) | `400` met `unauthorized_client` en `Client not allowed for direct access grants`. Via de publieke route geeft dezelfde request een 400 met de BunkerWeb-foutpagina. |
| `J3-04-redirect-manipulatie.png` | authorize-request met `redirect_uri=https://evil.example.com/` (directe route) | Keycloak-foutpagina `Invalid parameter: redirect_uri`. Geen loginformulier. |
| `J3-05-brute-force-events.png` | 6 mislukte logins voor `jalil` met verzonnen wachtwoorden | Events toont 5 keer `LOGIN_ERROR`, daarna `USER_DISABLED_BY_TEMPORARY_LOCKOUT`, daarna nog 2 keer `LOGIN_ERROR` tijdens de lockout. |
| `J3-06-discovery.json` | `/.well-known/openid-configuration` | `issuer` is `https://g05.ipv6.thelastgym.be/auth/realms/unbreakable`. `code_challenge_methods_supported` bevat `S256`. |
| `J3-07-logout-event.png` | uitloggen in de app | In User events staat een `LOGOUT` voor `unbreakable-spa`, vlak na `LOGIN` en `CODE_TO_TOKEN` van dezelfde sessie. |
| `J3-08-admin-403.png` | `https://g05.ipv6.thelastgym.be/auth/admin/` vanaf het internet | Status 403 met de BunkerWeb-foutpagina. Via de SSH-tunnel (`localhost:8081`) is de console wel bereikbaar. |

In het screenshot van J3-05 staan onderaan ook twee `LOGIN_ERROR`-regels zonder gebruiker. Dat zijn de redirect-tests van J3-04. De poging via de publieke route (BunkerWeb-pagina, 15:05:39 UTC) staat dus ook als fout in Keycloak. Dat laat zien dat de weigering van Keycloak komt en niet van de WAF.

### Logout en refresh token

Het refresh token is niet rechtstreeks getest. Een refresh token hangt aan de Keycloak-sessie, dus na het `LOGOUT`-event (J3-07) is er geen sessie meer waarmee het kan werken. Dit is een indirect bewijs. Het access token leeft 300 seconden (`levensduur_s` in J9-01), en refresh-tokenrotatie staat aan in de realm (`revokeRefreshToken` aan, `refreshTokenMaxReuse` 0).

### Keycloak-versie, CVE's en updates

- Draaiende versie is Keycloak 26.8.2, image-tag in `docker-compose.yml`: `<TAG INVULLEN>`.
- 26.8.2 is uitgebracht op 5 november 2026 en bevat beveiligingsfixes volgens de release notes op keycloak.org.
- CVE-check op 27 november 2026. Release notes van 26.8.2 gelezen en geen nieuwere release gevonden.
- Updateplan: bij elke nieuwe release de release notes en de migration guide lezen, eerst testen met een kopie van de realm-export, daarna de tag in `docker-compose.yml` aanpassen. Rollback is de vorige tag terugzetten.

## J5 tot J9, login, token, CSP en logout

| Bestand | Test | Resultaat |
|---|---|---|
| `J5-01-authorize-pkce.png` | authorize-request van de SPA (Network, Payload) | `response_type=code`, `code_challenge` en `code_challenge_method=S256`, plus `state` en `nonce`. |
| `J5-02-token-verifier.png` | token-request na de redirect (Network, Payload) | `grant_type=authorization_code` met `code_verifier`. De `code` is weggeblurd. |
| `J6-01-bearer-header.png` | API-call `GET /api/messages` | Header `Authorization: Bearer ...` met status 200. Het token is weggeblurd. |
| `J9-01-jwt-claims.png` | claims van het access token (T3), lokaal gedecodeerd | `iss` is de realm `unbreakable`, `aud` bevat `unbreakable-api`, `azp` is `unbreakable-spa`, `preferred_username` is `jalil`, levensduur 300 seconden. |
| `J8-01-geen-csp-fouten.png` | Issues-paneel en Console op de live site | 0 fouten en 0 waarschuwingen in Issues. Geen CSP-schendingen. |
| `J7-01-logout.png` | klikken op uitloggen | De app stuurt terug naar het Keycloak-loginscherm. |

Opmerkingen.
- `aud` bevat naast `unbreakable-api` ook `account`. Dat komt van de standaard client scope `roles` van Keycloak. De API eist dat `unbreakable-api` erin zit, en de inbox laadt, dus de API accepteert het token.
- De Console toont alleen gele waarschuwingen over de `Permissions-Policy`-header (onbekende functies in de lijst die BunkerWeb meestuurt). Dat zijn geen CSP-meldingen.
- De enige info-melding in Issues (`Session History Item Has Been Marked Skippable`) komt van de redirect naar Keycloak en terug.

## Hoe je dit moet lezen

- De discovery toont ook `plain`, `implicit` en `password` (`code_challenge_methods_supported`, `grant_types_supported`, `response_types_supported`). Dat zijn de mogelijkheden van Keycloak als geheel. Voor `unbreakable-spa` zijn implicit en password uit (J3-02, J3-02a, J3-03) en staat PKCE vast op S256. Het advertisen van `plain` is een realm-brede instelling die niet te verwijderen is, zie `deploy/keycloak/README.md` onder bekende beperkingen.
- De lockout is tijdelijk (ongeveer 1 minuut bij de eerste lockout). Keycloak toont tijdens de lockout dezelfde melding als bij een fout wachtwoord, zodat niet te zien is of een account bestaat. Het bewijs zit daarom in de Events en niet in het loginscherm.
- Alle events tonen als client-IP `172.20.0.1`, het interne Docker-adres van BunkerWeb. Keycloak ziet dus niet het echte IP van de bezoeker. De brute-force-beveiliging werkt per gebruiker en wordt hierdoor niet beïnvloed, maar IP-gebonden logging of limieten zouden niet kloppen. Dit is doorgegeven als aandachtspunt voor Yorben.

