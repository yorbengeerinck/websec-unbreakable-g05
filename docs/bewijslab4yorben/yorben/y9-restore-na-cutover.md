# Y9: restore-test na de cutover (met tuples en gevulde Users-tabel)

Datum: 17 november 2026, server g05. Geen geheimen in dit bestand.

De eerste restore-test (`y9-backup-restore.md`, 11 nov) liep nog in `AUTH_MODE=legacy`: `Users` en `tuple` waren leeg, dus de autorisatiewaarheid was niet bewezen. Deze test herhaalt het na de cutover, met echte tuples.

## Backup

Map `/var/backups/unbreakable/20261117-1528-post-oidc/` (map 700, bestanden 600, eigenaar root), gemaakt na T10 (bericht 18 verwijderd):

| Dump | Regels |
|---|---|
| `db.sql` (app) | 140 |
| `keycloak-db.sql` | 6570 |
| `openfga-db.sql` | 279 |

## Restore

Wegwerpcontainer `postgres:17` (zelfde hoofdversie als de bron), `--network none`, geen gepubliceerde poort, `ON_ERROR_STOP=1`:

    docker run -d --name restore-test --network none -e POSTGRES_HOST_AUTH_METHOD=trust postgres:17
    # databanken appdb, fgadb en kcdb aangemaakt; dumps via `docker exec -i ... psql` geladen

Alle drie de dumps zijn zonder fout geladen. De container is daarna verwijderd (controle: 0 containers `restore-test`).

## Rijtellingen

| Databank | Tabel | Bron | Restore |
|---|---|---|---|
| app | `Messages` | 4 | 4 |
| app | `Users` | 2 | 2 |
| OpenFGA | `tuple` | 2 | 2 |
| OpenFGA | `authorization_model` | 1 | 1 |
| Keycloak | `realm` | 2 (`master`, `unbreakable`) | 2 |
| Keycloak | `user_entity` | 6 | 6 |

De bronwaarden (`Messages` 4, `Users` 2, `tuple` 2, `authorization_model` 1) zijn vlak voor de restore met `psql` opgevraagd. De Keycloak-bronwaarden zijn niet apart opgevraagd; de restore geeft 6 en 2, wat past bij de drie gebruikers, het adminaccount en de interne gebruikers die Keycloak zelf aanmaakt.

De berichtenreeks werd na het laden op 18 gezet (`setval ... 18`), dus een nieuw bericht na een restore krijgt geen id dat al gebruikt is.

## Wat dit aantoont

- App, OpenFGA en Keycloak kunnen samen hersteld worden, en de tuples (de autorisatiewaarheid) komen terug met hetzelfde aantal als in de bron.
- De 2 tuples horen bij bericht 17; bericht 18 is eerder verwijderd samen met zijn tuples (`p6-t10-delete.md`).

## Beperking

De Keycloak-aantallen zijn niet vooraf met een query van de bron vergeleken; ze zijn enkel op het herstelde exemplaar afgelezen. Een volledige restore van Keycloak (login met de herstelde realm) is niet getest.
