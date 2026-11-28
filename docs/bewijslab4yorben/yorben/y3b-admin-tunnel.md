# Y3b: Keycloak-admin enkel via SSH-tunnel

Datum: 2026-11-11. Instelling: BunkerWeb `BLACKLIST_URI: "^/auth/admin"` (publiek geblokkeerd, PR #13); Keycloak-poort enkel gepubliceerd op `127.0.0.1:8081`, `KC_HOSTNAME_ADMIN=http://localhost:8081/auth`.

## 1. Via de SSH-tunnel: admin-console werkt

Screenshot: `y3b-admin-console-via-tunnel.png`

- Adres: `http://localhost:8081/auth/admin/master/console/` (lokale poort 8081 doorgestuurd naar poort 8081 op de server).
- De console laadt en toont "Welcome to Keycloak" voor de master realm, ingelogd als de persoonlijke admin-account van Yorben (niet het verwijderde bootstrapaccount).

## 2. Publiek: geblokkeerd door de WAF

Screenshot: `y3b-publiek-403.png`

- Adres: `https://g05.ipv6.thelastgym.be/auth/admin`
- Resultaat: `403 Forbidden`, foutpagina van BunkerWeb (de WAF antwoordt; de webserver erachter wordt niet bereikt). Request-ID `b3b3af5a4965c3416dea3d3ff82046b4`, tijdstip 2026-11-11 11:55:57 UTC.

## Conclusie

De beheerinterface is niet meer via het internet bereikbaar. Ze is enkel bereikbaar voor wie een SSH-sessie naar de server heeft (tunnel). De gebruikerslogin van de app (`/auth/realms/...`) blijft publiek bereikbaar en valt niet onder deze regel.
