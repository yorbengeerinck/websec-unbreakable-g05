# Y5 en Y7: auditlog (PR #19) en opruimen van de legacy-modus (PR #20)

Datum: 17 november 2026. Geen geheimen in dit bestand.

## PR #19: auditlog

Elke autorisatiebeslissing logt nu: actie, `sub`, object, beslissing, `count`, `model_id`, `latency_ms` en `correlation_id`. Nooit body, ciphertext of token. Een fout in OpenFGA (503) geeft een aparte regel met `decision=error`; de actie is dan methode plus routepatroon (geen ruwe URL, zodat een client geen tekst in het log kan duwen).

Voorbeeld uit de tests (lokaal, 13 van 13 geslaagd):

    authz action=delete sub=sub-jalil message=1 decision=deny count=(null) model_id=test latency_ms=1 correlation_id=...
    authz action=GET /api/messages sub=sub-jalil message=(null) decision=error ... model_id=test latency_ms=3 correlation_id=...
      Unbreakable.Auth.PdpUnavailableException: fake PDP is down

Aanleiding: bij T22 en T23 stond er enkel een foutmelding in het log en geen auditregel (zie `p6-t21-t22-cutover.md`).

CI van PR #19: alle zes verplichte checks groen; deploy-run 44110516974.

## PR #20: opruimen (plan §8.4)

Verwijderd: `app/Api/LegacyApi.cs`, `app/Auth/AuthSettings.cs`, `GET /api/config`, de `X-User`-lezing, `AUTH_MODE` (compose en `.env.example`) en de modus-takken in de frontend (`api.js`, `app.js`; geen naamkeuze meer). De app draait enkel nog met JWT en OpenFGA. De test `Config_IsPublic_AndReportsOidc` is weggevallen; 12 van 12 tests geslaagd (lokaal tegen een Postgres 17 op `127.0.0.1`).

Deploy-run op `main`: 44111415900, groen (inclusief smoke-test).

Rollback: revert van PR #20 op GitHub (er is geen `AUTH_MODE=legacy` meer).

## Live bewijs na de deploy van PR #19 en #20 (17 nov, na 15:00)

**Browser** (privévenster, `https://g05.ipv6.thelastgym.be/`, DevTools Network, filter All): de pagina stuurt door naar Keycloak (`auth?client_id=unbreakable-spa&redirect_uri=...`, status 302), laadt `auth.js` en `keycloak.js`, haalt een `token` op (200) en doet daarna `messages` (200), `users` (200), `messages` (POST, 201) en `messages` (200). Er is **geen** aanvraag naar `/api/config`. De inbox van `yorben` verschijnt na het inloggen.
Screenshots: `y7-opruimen-netwerk-1.png` en `y7-opruimen-netwerk-2.png`.

**Serverlog** (`docker compose ... logs --tail 20 app | grep authz`), uitgevoerd na dezelfde aanvragen; `sub` is de Keycloak-`sub` van `yorben`, het model-ID is geen geheim:

    authz action=list   sub=e9a8fbf9-... message=(null) decision=allow count=0 model_id=01M754QT1SFXBR8VFJHYJ1WD1M latency_ms=230 correlation_id=0HNQ4NKNI3TIU:00000001
    authz action=list   sub=e9a8fbf9-... message=(null) decision=allow count=0 model_id=01M754QT1SFXBR8VFJHYJ1WD1M latency_ms=1   correlation_id=0HNQ4NKNI3TJA:00000001
    authz action=create sub=e9a8fbf9-... message=18      decision=allow count=(null) model_id=01M754QT1SFXBR8VFJHYJ1WD1M latency_ms=116 correlation_id=0HNQ4NKNI3TJC:00000001
    authz action=list   sub=e9a8fbf9-... message=(null) decision=allow count=0 model_id=01M754QT1SFXBR8VFJHYJ1WD1M latency_ms=2   correlation_id=0HNQ4NKNI3TJD:00000001

De lege inbox van `yorben` (count=0) is correct: hij is zelf sender van bericht 18, en het model geeft alleen de recipient leesrecht.

**Tuples van bericht 18** (`fga tuple read --object message:18`), vóór het verwijderen, 15:19:48Z:

    message:18  sender     user:e9a8fbf9-40e9-4460-9976-b7e08b2ec8e7
    message:18  recipient  user:d496df03-736a-44ec-8016-e1714bb6d564

Het verwijderen (T10 live) en de controle erna staan in `p6-t10-delete.md` zodra dat gedaan is.
