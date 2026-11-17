# Keycloak realm `unbreakable`

Realm-config voor opdracht 4 (authenticatie). Eigenaar: Jalil.
Dit bestand beschrijft hoe de realm gebouwd wordt en hoe je hem opnieuw opzet.

## Bestanden

- `realm-unbreakable.json`: realm, clients, mappers, tokeninstellingen en policies. Bevat **geen** users en **geen** wachtwoorden.

## Wat staat erin

| Onderdeel | Waarde |
|---|---|
| Realm | `unbreakable`, `sslRequired=external`, registratie uit, login met e-mail uit, edit username uit, remember me uit |
| Access token | 5 min |
| SSO | idle 30 min, max 10 uur |
| Refresh token | rotatie aan (`revokeRefreshToken=true`, `refreshTokenMaxReuse=0`) |
| Brute force | aan, lockout na 5 foute pogingen |
| Password policy | `length(12) and notUsername and notEmail` |
| Client `unbreakable-spa` | public, standard flow, **PKCE S256**, implicit uit, direct access grants uit, exacte redirect-URI's, audience-mapper naar `unbreakable-api` |
| Client `unbreakable-api` | bearer-only, geen flows |
| Realm role | `user` (geen berichtrechten, die zitten in OpenFGA) |

## Realm importeren

### Optie 1, via de admin console (gebruikt voor de live omgeving)

1. Open de tunnel en ga naar `http://localhost:8081/auth/admin/`.
2. Klik op de realm-dropdown, kies *Create realm*.
3. Upload `realm-unbreakable.json` bij *Resource file* en klik op *Create*.

### Optie 2, bij het opstarten (`--import-realm`)

Keycloak importeert alle `*.json`-bestanden in `/opt/keycloak/data/import` bij `start --import-realm`.
Een realm die al bestaat wordt **niet** overschreven. De import van een bestaande realm wordt overgeslagen.
Dit vraagt een volume en een extra startoptie in `docker-compose.yml`. Dat bestand is van Yorben, dus wijzigingen daaraan gaan via hem.

## Users aanmaken

Users staan bewust niet in de JSON, zodat er geen wachtwoorden in Git komen.

- Gebruik de admin console of `kcadm.sh` (zie het plan, appendix 12.5).
- Usernames zijn altijd lowercase: `yorben`, `jalil`, `pros`. Keycloak slaat usernames in kleine letters op, dus `preferred_username` in het token is ook lowercase.
- Elke user krijgt een **tijdelijk** wachtwoord (*Temporary* aan) en de realm role `user`.
- Wachtwoorden gaan nooit in Git, in een issue of in de groepschat. Geef ze privé door.

## Bekende beperkingen

- De password policy heeft geen `passwordBlacklist`, want die vraagt een bestand op de server.
- `bearerOnly` staat in Keycloak 26 niet meer in de UI voor nieuwe clients. Via import werkt het nog.
- De realm laat `plain` nog toe als algemene PKCE-methode. De client `unbreakable-spa` dwingt `S256` af via het attribuut `pkce.code.challenge.method`.
- Eenmalige installatie: de `frontendUrl` van de **master**-realm is via de database op `http://localhost:8081/auth` gezet (zie het plan, Y3b). Dat raakt `unbreakable` niet.

## Status van de tests (J2)

- Import via de admin console op de live server: gelukt, discovery geeft 200 met de juiste `issuer` en `S256`.
- Eigen admin kan inloggen nadat de bootstrap-admin weg is: gelukt (zie Y3).
- Import op een schone Keycloak-DB en het niet-overschrijven van een bestaande realm: nog niet getest. De documentatie van Keycloak beschrijft het gedrag, maar het is hier niet bewezen.
