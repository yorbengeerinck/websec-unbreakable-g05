# Y8: ziet BunkerWeb het echte client-IP? (test van 17 november 2026)

Aanleiding: Keycloak-events en de BunkerWeb-foutpagina toonden als client-IP altijd `172.20.0.1`. BunkerWeb bant op IP (bad behavior: 10 keer een 4xx geeft 24 uur ban). Zou BunkerWeb enkel dat adres zien, dan zou één ban iedereen raken.

## Waarnemingen (server g05, `docker logs websec-unbreakable-g05-bunkerweb-1`)

| Aanvraag | Bron zoals BunkerWeb die logt |
|---|---|
| Browser van Yorben (Chrome op Windows): inloggen bij Keycloak, `GET /js/app.js`, `POST .../openid-connect/token`, `GET /api/messages` | `172.20.0.1` (alle regels) |
| `curl https://g05.ipv6.thelastgym.be/health` vanaf de server zelf | `2a02:1810:a531:7602::105` (het echte IPv6-adres van de server) |

Overige controles:
- `ss -tlnp`: poort 80 en 443 hebben `docker-proxy` voor IPv4 (`0.0.0.0`, naar `172.20.0.3`) en IPv6 (`::`, naar `fd05:5:0:1::3`). Er is geen `/etc/docker/daemon.json`, dus `userland-proxy` staat aan.
- `bwcli bans`: geen enkele actieve ban op het moment van de test.
- `docker-compose.yml` zegt dat IPv6 gekozen is "zodat Docker ip6tables-DNAT gebruikt in plaats van docker-proxy" en BunkerWeb het echte IP ziet. Dat klopt voor IPv6 (zie tweede rij), niet voor IPv4.

## Conclusie

- **IPv4-bezoekers verschijnen allemaal als `172.20.0.1`.** De userland proxy van Docker maskeert het bron-IP. De browser van Yorben komt via IPv4 binnen.
- Een IPv6-aanvraag toont het echte adres, maar die test kwam van de server zelf. Een externe IPv6-client is nog niet getest.
- Gevolg: de IP-gebaseerde bescherming van BunkerWeb (bad-behavior-ban, limieten) geldt voor alle IPv4-bezoekers samen. Eén ban van `172.20.0.1` blokkeert elke IPv4-bezoeker, ook de groep zelf.
- De rate limiting van de app valt hier grotendeels buiten: die gebruikt de `sub` van het token en valt enkel terug op het IP als er geen gebruiker is.

## Ingreep (17 november 2026, ca. 16:00)

`/etc/docker/daemon.json` aangemaakt met `{ "userland-proxy": false }`, daarna `systemctl restart docker`. Alle 10 containers kwamen terug (`restart: unless-stopped`). Poorten 80/443 staan nog op `0.0.0.0` en `[::]`, 7000 en 8081 op `127.0.0.1`; `ss -tlnp` toont nu `dockerd` als eigenaar in plaats van `docker-proxy`.
Terugdraaien: `rm /etc/docker/daemon.json && systemctl restart docker`.

Let op: een `curl` vanaf de server zelf toont na de wijziging `fd05:5:0:1::1` (bridge-gateway). Dat is normaal voor lokaal gegenereerd verkeer en bewijst niets over echte bezoekers.

## Test na de ingreep (extern, vanaf de browser van Yorben)

Inloggen via `https://g05.ipv6.thelastgym.be/`, daarna `docker logs --since 2m websec-unbreakable-g05-bunkerweb-1 2>&1 | grep '^g05.ipv6'`:

| Aanvragen | Bron zoals BunkerWeb die logt |
|---|---|
| 7 regels (login, assets, token, API) | `2a02:1808:e4:d729:7dfc:476e:d85f:3f07` (echt IPv6-adres van Yorben) |

Resultaat: **IPv6 bewaart het echte client-IP.** Geen enkele regel toont nog `172.20.0.1`.

## Afsluiting (27 november 2026)

- **IPv6 (bewezen 17 nov):** na de ingreep toont BunkerWeb het echte IPv6-adres van de browser (`2a02:1808:…`) bij alle aanvragen. Geen enkele regel toont nog `172.20.0.1`.
- **IPv4 (niet van toepassing):** `nslookup -type=A g05.ipv6.thelastgym.be` (27 nov) geeft geen A-record, enkel een AAAA-record (`2a02:1810:a531:7602::105`). De site is dus enkel via IPv6 bereikbaar, en daarmee is het IPv6-resultaat de volledige oplossing. Het eerdere `172.20.0.1` kwam van de browser; de IPv4-route bestaat voor deze site niet.
- **Beheerconsole via tunnel:** SSH-tunnel naar `127.0.0.1:8081` werkt (27 nov, ingelogd op de server). De console zelf is eerder bewezen op 11 nov (`y3b-admin-tunnel.md`, screenshot `y3b-admin-console-via-tunnel.png`).
- **Publiek `/auth/admin/` geeft 403:** door Yorben getest in de browser op 27 nov. Het curl-commando op school gaf `000`, omdat die pc geen IPv6-adres heeft en de domeinnaam dus niet bereikt; dat is een netwerkbeperking van die locatie, geen serverprobleem.
- **Server:** `/etc/docker/daemon.json` = `{ "userland-proxy": false }`; alle 10 containers draaien (`Up 9 days`, de gezonde containers `healthy`). Terugdraaien: `rm /etc/docker/daemon.json && systemctl restart docker`.

Beslissing: we houden `userland-proxy: false` aan. Het is de enige aanpak die het echte client-IP bewaart voor de IP-gebaseerde bescherming van BunkerWeb.
