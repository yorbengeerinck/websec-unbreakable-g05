# Bewijzen Yorben (opdracht 4, groep g05)

Elk bestand hoort bij een taak uit `PLAN-opdracht4-g05.md`. Er staan geen wachtwoorden of tokens in. Stand: 17 november 2026.

| Bestand | Taak | Wat het aantoont | Status |
|---|---|---|---|
| `y2-github-contributors.md` | Y2 | Contributorslijst na het herschrijven van de historiek (enkel de twee teamleden) | klaar |
| `y3-backchannel-discovery.md` | Y3 / Y5 | Interne discovery geeft een interne `jwks_uri`; verklaring van de issuer van `master` | klaar |
| `y3b-admin-tunnel.md` (+ 2 screenshots) | Y3b | Admin-console enkel via SSH-tunnel, publiek `403` door de WAF | klaar |
| `y4-openfga-bootstrap.md` | Y4 | OpenFGA-store en model aangemaakt, model teruggelezen, ID's in `.env` | klaar (T22 bewezen, zie `p6-t21-t22-cutover.md`) |
| `y5-y7-tests-lokaal.md` | Y5-Y7 | 13 van 13 API-tests geslaagd op de eigen machine | klaar |
| `y7-auditlog-opruimen.md` | Y5 / Y7 | Auditlog met `model_id`, latency, correlation-id en foutgevallen (PR #19); legacy-modus verwijderd (PR #20) | klaar (+ 2 screenshots) |
| `y8-ci-pr14.md` | Y8 | CI van PR #14 en de pipeline op `main` na de merge (inclusief deploy) | klaar |
| `y8-branch-protection.md` (+ 2 screenshots) | Y8 | Regels op `main` en een geweigerde directe push (`GH006`) | klaar |
| `y8-pipeline-pr15.md` | Y8 | CI-run van PR #15 en de eerste deploy met smoke-test | klaar |
| `y8-client-ip-bunkerweb.md` | Y8 | Client-IP: na `userland-proxy: false` toont BunkerWeb het echte IPv6-adres; het domein heeft geen IPv4 | klaar (27 nov) |
| `y9-backup-restore.md` | Y9 | Rechten en poorten, backup van de drie databanken, restore-test | klaar |
| `y9-restore-na-cutover.md` | Y9 | Restore-test herhaald na de cutover: app, OpenFGA (tuples) en Keycloak samen hersteld, tellingen gelijk | klaar |
| `y9-rollback.md` | Y9 | Rollback in drie niveaus, data en schema, controle achteraf | klaar |
| `p6-t10-delete.md` (+ screenshot) | P6 | T5 en T10 live: tuples na POST, DELETE als sender geeft 204, tuples daarna weg | klaar |
| `p6-t21-t22-cutover.md` | P6 | Cutover naar `oidc`, tuples (T5), rechten, T21, T22 en T23 (503) | klaar; statuscodes T7 tot T9, T10 en de nieuwe restore-test volgen |
| `y10-logboek-bevindingen.md` | Y10 | Bevindingen en fixes, en de levensduur van `AUTH_MODE` | klaar |

## Screenshots van 17 november (P6, teruggevonden op 27 november)

| Bestand | Bewijst |
|---|---|
| `p6-t5-tuples-message17.png` | T5: `tuple read` toont `sender` en `recipient` voor `message:17` met de `sub` van de gebruikers |
| `p6-t22-fout-model-id-commando.png` | T22: app herstart met een fout `OPENFGA_MODEL_ID` |
| `p6-t22-fout-model-id-503-browser.png` | T22: de app geeft 503 "de autorisatieservice is niet bereikbaar" |
| `p6-503-fail-closed-browser.png` | Fail closed: dezelfde 503-melding in de browser (T21 of T23, zie opmerking) |
| `p6-503-fail-closed-devtools.png` | Fail closed: `GET /api/messages` geeft 503 in DevTools (T21 of T23, zie opmerking) |

Opmerking: de laatste twee zijn niet gelabeld met T21 of T23 omdat ik uit het scherm zelf niet kan afleiden welke test het was. Hernoem ze zodra je het zeker weet.
