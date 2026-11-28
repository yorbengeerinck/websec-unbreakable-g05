# P6: cutover naar `AUTH_MODE=oidc`, tuples en fail closed (T5, T6 tot T9, T21, T22, T23)

Datum: 17 november 2026, server g05. Geen wachtwoorden of tokens in dit bestand.

## Cutover

1. Realm `unbreakable` live (discovery 200, issuer `https://g05.ipv6.thelastgym.be/auth/realms/unbreakable`). Users `yorben`, `jalil`, `pros` met rol `user`, tijdelijke wachtwoorden.
2. Backup van `db`, `keycloak-db` en `openfga-db` in `/var/backups/unbreakable/<datum>-pre-oidc/` (map 700, bestanden 600). Een eerste poging gaf 0-byte dumps omdat het commando niet in de projectmap liep; die zijn verwijderd en overgedaan, daarna niet meer leeg.
3. `AUTH_MODE=oidc` toegevoegd aan `/etc/unbreakable/.env` (eigenaar `runner`, rechten 600), `docker compose --env-file /etc/unbreakable/.env up -d app`.
4. Controle: `/health` = `ok`, `/api/config` = `{"authMode":"oidc"}`, `/api/messages` zonder token = 401. Logs zonder fouten (alleen de bekende `libgssapi`-melding en DataProtection-waarschuwingen).
5. Yorben kan inloggen met zijn user (PKCE-login, tijdelijk wachtwoord gewijzigd).

Rollback: `AUTH_MODE=legacy` en dezelfde `up -d app`.

## T5: tuples na POST

Voor het eerste bericht: `fga tuple read` gaf `"tuples": []` (nulmeting). Na een bericht van Yorben naar Jalil (bericht 17):

| object | relatie | gebruiker (Keycloak-`sub`) | timestamp |
|---|---|---|---|
| `message:17` | `sender` | `user:e9a8fbf9-...` | 2026-11-17T14:24:30Z |
| `message:17` | `recipient` | `user:d496df03-...` | 2026-11-17T14:24:30Z |

Beide tuples hebben dezelfde timestamp, dus ze zijn in één write aangemaakt. De identiteit is de `sub`, niet de gebruikersnaam. (Screenshot van `tuple read` door Yorben bewaard.)

## T6 tot T9: rechten (uitgevoerd in de app, door de drie gebruikers)

| Test | Uitkomst volgens de uitvoerders |
|---|---|
| Jalil (recipient) ziet bericht 17 | ja |
| Jalil probeert te verwijderen | niet toegestaan |
| Pros (derde gebruiker) ziet bericht 17 | nee |

Statuscodes zijn niet apart genoteerd; het plan verwacht 404 bij verboden toegang (T7 tot T9). Nog aan te vullen met DevTools-screenshots van Jalil en Pros.

## T22: verkeerd model-ID geeft 503

Commando (alleen voor dit ene `up`, `.env` niet gewijzigd):

    OPENFGA_MODEL_ID=01ARZ3NDEKTSV4RRFFQ69G5FAV docker compose --env-file /etc/unbreakable/.env up -d app

Resultaat: inbox toont `GET /messages gaf 503: de autorisatieservice is niet bereikbaar`. Geen berichten getoond. Fail closed bewezen. Daarna herstel zonder override; het model-ID in de container is gelijk aan `.env` (`01M754QT1SFXBR8VFJHYJ1WD1M`).

Opmerking: in het auditlog verscheen bij dit foutpad geen `authz`-regel (de grep gaf niets). Het log mist dus de foutgevallen en `model_id`, zie het open punt bij Y7.

## T21: OpenFGA gestopt geeft 503

    docker compose --env-file /etc/unbreakable/.env stop openfga

Resultaat: inbox geeft 503, geen data. Na `start openfga` werkt de inbox weer (gemeld door Yorben). Het model-ID in de app werd voor deze test gecontroleerd en was correct, dus de 503 komt van het gestopte OpenFGA.

## T23: verkeerde preshared key geeft 503

`OPENFGA_API_TOKEN` in `.env` voedt twee services: de app (`OpenFga__ApiToken`) en OpenFGA (`OPENFGA_AUTHN_PRESHARED_KEYS`).

**Eerste poging (ongeldig).** `OPENFGA_API_TOKEN=verkeerd docker compose ... up -d --force-recreate app` zette het foute token ook op OpenFGA, dat daardoor opnieuw werd aangemaakt. Beide kanten gebruikten dezelfde waarde, dus de aanroep slaagde (`decision=allow count=0`, lege inbox) en `curl` met `Bearer verkeerd` gaf 200. Dat is een testfout, geen fail-open. Na het herstel (OpenFGA herstart met het echte token) gaf het foute token 401 en zonder token ook 401, dus de authenticatie wordt afgedwongen.

**Tweede poging (geldig).** Alleen de app krijgt het foute token, via een tijdelijk extra compose-bestand (`/tmp/t23.yml`, daarna verwijderd):

    services:
      app:
        environment:
          OpenFga__ApiToken: "verkeerd"

    docker compose -f docker-compose.yml -f /tmp/t23.yml --env-file /etc/unbreakable/.env up -d app

OpenFGA bleef ongemoeid ("Up About a minute", niet herstart). Resultaat:

- Browser (DevTools): `GET /api/messages` geeft **503 (Service Unavailable)**; de pagina meldt "de autorisatieservice is niet bereikbaar".
- Logs: `Autorisatieservice (OpenFGA) niet bereikbaar of foutief; verzoek geweigerd.` en `PdpUnavailableException: OpenFGA gaf geen bruikbaar antwoord.` Geen `decision=allow`.
- Herstel: `docker compose --env-file /etc/unbreakable/.env up -d app`, token in de app is weer het echte token, `/health` = `ok`.

Fail closed bewezen voor T21, T22 en T23.

## Bevindingen

- Het auditlog (`authz action=... decision=...`) logt alleen geslaagde beslissingen. Bij een fout in OpenFGA staat er enkel een foutmelding, geen `authz`-regel met `decision=deny` of `error`. Voor het eindverslag: auditregel voor foutgevallen, plus `model_id`, latency en correlation-id (zie Y7).
- Testles: een wijziging van `OPENFGA_API_TOKEN` raakt twee services. T23 vraagt een override die alleen de app raakt.

## Incident: testcontainer per vergissing op de server

Bij het draaien van de auditlog-tests werden commando's bedoeld voor de eigen pc in de SSH-sessie van de server geplakt. Daardoor draaide kort een container `pgtest` (Postgres 17, `trust`-authenticatie, poort 5432 gepubliceerd op alle interfaces, lege testdatabase). Er stond geen data in, en `dotnet test` faalde daar (geen SDK). De container is gestopt en bestaat niet meer.

Controle achteraf (17 nov):

    docker ps -a --format '{{.Names}}' | grep -c pgtest      ->  0
    ss -tlnp | grep -E ':(80|443|5432|8081|7000)\b'
      0.0.0.0:80, 0.0.0.0:443, [::]:80, [::]:443  (bunkerweb)
      127.0.0.1:7000 (bw-ui), 127.0.0.1:8081 (keycloak)
      geen 5432

Les: testdatabases bind je altijd aan `127.0.0.1` (`-p 127.0.0.1:5432:5432`), en voer pc-commando's niet in de SSH-sessie uit.

## Nog te doen

- Statuscodes van T7 tot T9 met screenshot (Jalil en Pros).
- T10: DELETE als sender (204) en `tuple read` daarna.
- Restore-test herhalen, nu met tuples en gevulde `Users`-tabel.
