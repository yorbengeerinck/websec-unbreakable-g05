# Y9: rollback

Datum: 2026-11-11. Gebaseerd op de huidige opzet: `deploy` in `.github/workflows/security.yml` draait op de self-hosted runner met
`docker compose --env-file /etc/unbreakable/.env up -d --build`, gevolgd door een health check via de WAF. De app-image wordt lokaal gebouwd (`build: ./app`); Keycloak, OpenFGA en BunkerWeb staan op een vaste versie (Keycloak en OpenFGA ook op digest).

`main` is beschermd (PR, 1 goedkeuring, vier checks, geen bypass). Elke rollback via git gaat dus ook via een PR.

## Niveau 1: terug naar het oude gedrag zonder code te wijzigen (na de cutover)

De overgangsschakelaar `AUTH_MODE` bepaalt of de API `legacy` (X-User) of `oidc` (JWT + OpenFGA) draait.

    # in /etc/unbreakable/.env: AUTH_MODE=legacy   (rechten blijven runner:runner 600)
    docker compose --env-file /etc/unbreakable/.env up -d app
    curl -s https://g05.ipv6.thelastgym.be/health      # verwacht: ok

Snelste route, geen build, geen merge. Het is een noodmaatregel: legacy heeft de zwakheden van opdracht 3 terug (zie plan §8.4). Gebruik het enkel om de dienst te herstellen en los de fout daarna op.

## Niveau 2: vorige versie van de code

1. `git revert -m 1 <merge-commit>` op een nieuwe branch (voor PR #14: `e2ae52d`), PR openen, laten goedkeuren, mergen.
2. De pipeline bouwt en deployt de vorige toestand; de health check moet slagen.
3. Alleen in noodgeval, als de pipeline of runner niet beschikbaar is: op de server in een schone checkout van de vorige commit `docker compose --env-file /etc/unbreakable/.env up -d --build` uitvoeren. Daarna dezelfde wijziging alsnog via een PR op `main` zetten, zodat de repo en de server niet uiteenlopen.

## Niveau 3: infrastructuur (Keycloak, OpenFGA, BunkerWeb)

Wijzigingen in de images staan in `docker-compose.yml`. Terugdraaien = de vorige versie of digest terugzetten via een PR (of niveau 2). Heeft een upgrade data gewijzigd, dan eerst herstellen uit de backup (zie hieronder).

## Data en schema

- **Schema van de app:** `SchemaUpgrade` voegt enkel toe (tabel `Users`, kolommen en indexen) en verwijdert niets. De vorige code ziet de extra kolommen niet. Controleer na een rollback altijd `/health` en `/api/messages`.
- **OpenFGA-model:** modelversies zijn onveranderlijk. Terug naar een ouder model = `OPENFGA_MODEL_ID` in `.env` op het vorige ID zetten en de app herstarten (huidig model: `01M754QT1SFXBR8VFJHYJ1WD1M`). Let op: tuples die onder een nieuwer model zijn geschreven, kan het oudere model niet kennen. Controleer daarna met `fga tuple read` en een paar autorisatietests.
- **Databanken:** herstellen uit `/var/backups/unbreakable/<datum>/*.sql`. De restore van app en OpenFGA is bewezen in `y9-backup-restore.md`. **App-databank en OpenFGA-databank worden altijd samen hersteld**, uit dezelfde backup. Een restore van alleen de app-databank breekt de autorisatie.

## Controle na elke rollback

1. `curl -s https://g05.ipv6.thelastgym.be/health` geeft `ok`.
2. `curl -s -o /dev/null -w '%{http_code}\n' https://g05.ipv6.thelastgym.be/api/messages` geeft 401 (oidc) of het legacy-gedrag (legacy).
3. `https://g05.ipv6.thelastgym.be/auth/admin/` geeft 403.
4. Noteer wat er teruggedraaid is en waarom in het logboek.
