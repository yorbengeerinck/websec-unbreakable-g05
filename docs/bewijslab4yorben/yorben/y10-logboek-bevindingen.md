# Y10: logboek van bevindingen en fixes (Yorben)

Geen geheimen in dit bestand. Data zijn 2026; PR-nummers zijn die van `yorbengeerinck/websec-unbreakable-g05`.

## De overgangsschakelaar `AUTH_MODE` (plan §8.4)

Bestond van PR #14 (gemerged op 11 nov) tot PR #20 (gemerged op 17 nov). Reden: de werkpakketten van Yorben (API en autorisatie), Jalil (realm en frontend) en Pros (tests en verslag) moesten onafhankelijk kunnen opleveren. In `legacy` bleef de oude app draaien, in `oidc` gold JWT plus OpenFGA. De cutover naar `oidc` gebeurde op 17 nov na een backup; PR #20 verwijderde `LegacyApi`, `AuthSettings`, `/api/config`, de `X-User`-lezing en `AUTH_MODE`. Rollback sindsdien: revert van PR #20.

## Bevindingen en fixes

| Datum | Bevinding | Oorzaak | Fix |
|---|---|---|---|
| 11 nov | Restore met `postgres:16` faalde (`unrecognized configuration parameter "transaction_timeout"`) | Dumps komen uit Postgres 17 | Restore met dezelfde hoofdversie (17); `y9-backup-restore.md` |
| 11 nov | Dubbele lege `OPENFGA_*`-regels in `/etc/unbreakable/.env`; `sed -i` als root veranderde eigenaar | Bootstrap-uitvoer dubbel toegevoegd | Regels verwijderd, `chown runner:runner` en `chmod 600` hersteld |
| 11 nov | Directe push naar `main` kon nog (bypass) | Beheerders konden de regel omzeilen | "Do not allow bypassing" aan; push geweigerd met `GH006` (`y8-branch-protection.md`) |
| 15 nov | `sast-semgrep` faalde op PR #15 met exit code 3 | Semgrep `--strict` kan de bash-constructie `case ... 80\|443)` niet parsen | Vervangen door een `grep`-controle |
| 15 nov | Deploy-run van PR #15 rood op de smoke-test | De test verwachtte een publieke master-discovery; die is bewust 403 (admin enkel via tunnel) | PR #18: test controleert de app-realm (200, juiste issuer) en dat `master` 403 geeft; `y8-pipeline-pr15.md` |
| 17 nov | Geen knop "Update branch" bij PR #16 en #17; verplichte checks `openfga-model` en `compose-validate` ontbraken | Branches liepen 5 commits achter en de repo vereist geen up-to-date branches | `main` in de branch gemergd en gepusht; alle checks groen |
| 17 nov | Geen auditregel bij een OpenFGA-fout (T22/T23) | Auditlog logde enkel geslaagde beslissingen | PR #19: `decision=error` en velden `model_id`, `latency_ms`, `correlation_id`; `y7-auditlog-opruimen.md` |
| 17 nov | Eerste T23-poging ongeldig (`OPENFGA_API_TOKEN` overschreven) | Die variabele voedt twee services; OpenFGA werd ook met het foute token herstart | Tweede poging met een extra compose-bestand dat alleen de app raakt; 503 bewezen (`p6-t21-t22-cutover.md`) |
| 17 nov | Dumps van 0 bytes bij de backup vóór de cutover | Commando draaide niet in de projectmap van `docker-compose.yml` | Opnieuw in de juiste map; dumps niet leeg (140, 6570, 279 regels) |
| 17 nov | Container `pgtest` (Postgres, `trust`, poort 5432 op alle interfaces) kort op de server | Pc-commando's in de SSH-sessie geplakt | Container weg; poorten gecontroleerd (80 en 443 publiek, 7000 en 8081 op `127.0.0.1`, geen 5432); testcontainers voortaan op `127.0.0.1` |
| 17 nov | BunkerWeb logde alle IPv4-bezoekers als `172.20.0.1` (IP-bans zouden iedereen raken) | Docker `userland-proxy` maskeert het bron-IP | `/etc/docker/daemon.json` met `{ "userland-proxy": false }` en Docker herstart; IPv6 toont nu het echte adres; `y8-client-ip-bunkerweb.md` |
| 27 nov | Smoke-test in de deploy faalde: `master` gaf 200 waar 403 verwacht werd | `BLACKLIST_URI` blokkeerde enkel `/auth/admin`; de master-login stond bewust publiek (`voorlopig ja` in het plan) | Blokkade uitgebreid met `^/auth/realms/master` (op de server; repo-wijziging nog te committen). Gecontroleerd: scheduler reload, master 403, admin 403, unbreakable 200, tunnel-login werkt |

## Niet gedaan of bewust niet

- Integratietests met een echte OpenFGA-container (testcompose): de tests gebruiken een stand-in met de semantiek van §4.4. Het gedrag met een echte OpenFGA is live bewezen (T5, T10, T21, T22, T23).
- Images taggen met de commit-SHA (optioneel); de rollback staat in `y9-rollback.md`.
- Statuscodes van de geweigerde aanvragen (T7 tot T9) zijn niet live vastgelegd; de geautomatiseerde tests dekken ze.
