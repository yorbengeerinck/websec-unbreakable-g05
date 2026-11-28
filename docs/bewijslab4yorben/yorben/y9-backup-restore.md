# Y9: secrets, rechten, backup en restore

Datum: 2026-11-11, uitgevoerd op de server g05 als root. Er staan geen waarden van geheimen in dit bestand.

## 1. Secrets en rechten

| Controle | Resultaat |
|---|---|
| `/etc/unbreakable/.env` | `-rw------- runner runner` (600, eigenaar is de runner-gebruiker, niet root) |
| Variabelen in het bestand (enkel namen getoond) | `APP_DOMAIN`, `BW_DB_PASSWORD`, `BW_UI_ADMIN_PASSWORD`, `DB_PASSWORD`, `KC_DB_PASSWORD`, `OPENFGA_DB_PASSWORD`, `OPENFGA_API_TOKEN`, `OPENFGA_STORE_ID`, `OPENFGA_MODEL_ID` |
| `.env` in de repo | niet aanwezig (`git ls-files`), geen enkele commit bevat het (`git log --all -- .env`) |
| Geheimen in de commandline/entrypoint van containers | geen (`docker inspect`, enkel `[start]` en `[postgres]`) |
| `KC_BOOTSTRAP_ADMIN_*` | niet meer aanwezig; de tijdelijke Keycloak-admin is verwijderd (zie Y3) |

Opruiming: `OPENFGA_STORE_ID` en `OPENFGA_MODEL_ID` stonden dubbel (lege regels 8 en 9). Die zijn verwijderd; daarna zijn eigenaar `runner:runner` en rechten 600 hersteld.

## 2. Publiek bereikbare poorten (`docker ps`)

| Container | Poorten |
|---|---|
| bunkerweb | `0.0.0.0:80` en `0.0.0.0:443` (enige publieke ingang) |
| keycloak | enkel `127.0.0.1:8081` |
| bw-ui | enkel `127.0.0.1:7000` |
| app, openfga, db, keycloak-db, openfga-db, bw-db | niet gepubliceerd |

## 3. Backup

Map: `/var/backups/unbreakable/20261111-1119/` (map 700, bestanden 600, eigenaar root).

    for svc in db keycloak-db openfga-db; do
      docker compose exec -T $svc sh -c 'pg_dump -U "${POSTGRES_USER:-postgres}" -d "${POSTGRES_DB:-${POSTGRES_USER:-postgres}}" --no-owner' > $D/$svc.sql
    done

| Dump | Regels | `CREATE TABLE` |
|---|---|---|
| `db.sql` (app) | 137 | 2 |
| `keycloak-db.sql` | 5687 | 101 |
| `openfga-db.sql` (OpenFGA, bevat de autorisatiewaarheid) | 271 | 6 |

## 4. Restore-test (app en OpenFGA samen)

Eerste poging in `postgres:16` mislukte met `unrecognized configuration parameter "transaction_timeout"`, omdat de dumps uit Postgres 17 komen. `ON_ERROR_STOP` stopte, er werd niets geladen. Herstel: dezelfde hoofdversie als de bron (17).

    docker run -d --name restore-test -e POSTGRES_HOST_AUTH_METHOD=trust postgres:17
    # dumps laden in appdb en fgadb, rijtellingen vergelijken met de bron, container verwijderen

De wegwerpcontainer had geen gepubliceerde poorten en is na de test verwijderd. Rijtellingen:

| Databank | Tabel | Bron | Restore |
|---|---|---|---|
| app | `Messages` | 3 | 3 |
| app | `Users` | 0 | 0 |
| OpenFGA | `store` | 1 | 1 |
| OpenFGA | `authorization_model` | 1 | 1 |
| OpenFGA | `goose_db_version` | 7 | 7 |
| OpenFGA | `tuple`, `assertion`, `changelog` | 0 | 0 |

Beperking: `Users` en `tuple` zijn leeg omdat de app nog op `AUTH_MODE=legacy` draait. Na de cutover moet deze test herhaald worden, zodat ook de tuples (de autorisatiewaarheid) bewezen terugkomen. Een restore van enkel de app-databank breekt de autorisatie; app en OpenFGA worden dus samen teruggezet.
