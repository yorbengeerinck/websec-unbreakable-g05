# Y4: OpenFGA-store en autorisatiemodel aangemaakt

Datum: 2026-11-11, uitgevoerd op de server g05 vanuit de repo op commit e2ae52d.
Model: `deploy/openfga/model.fga` (plan §4.4, letterlijk). Het token komt uit `/etc/unbreakable/.env` en is niet getoond.

Commando (CLI-container op het interne netwerk `websec-unbreakable-g05_authz`, OpenFGA heeft geen publieke poort):

    docker run --rm --network websec-unbreakable-g05_authz \
      -v "$PWD/deploy/openfga/model.fga:/model.fga:ro" \
      -e FGA_API_URL=http://openfga:8080 \
      -e FGA_API_TOKEN="$OPENFGA_API_TOKEN" \
      openfga/cli store create --name unbreakable --model /model.fga

CLI-image: `openfga/cli@sha256:31a565d14d1c5ca8170d3a2225eb66517d9f490539967bcb3421613c5e2538a4`

Uitvoer:

    {"store":{"created_at":"2026-11-11T11:08:54.706762Z","id":"01M754QT18P8SDPB0P0S0YK41G","name":"unbreakable","updated_at":"2026-11-11T11:08:54.706762Z"},"model":{"authorization_model_id":"01M754QT1SFXBR8VFJHYJ1WD1M"}}

| Instelling | Waarde |
|---|---|
| `OPENFGA_STORE_ID` | `01M754QT18P8SDPB0P0S0YK41G` |
| `OPENFGA_MODEL_ID` | `01M754QT1SFXBR8VFJHYJ1WD1M` |

Store- en model-ID zijn geen geheimen. Het API-token (`OPENFGA_API_TOKEN`) staat enkel in `/etc/unbreakable/.env`.

## Controle: model uit de store teruglezen

Commando:

    docker run --rm --network websec-unbreakable-g05_authz \
      -e FGA_API_URL=http://openfga:8080 -e FGA_API_TOKEN="$OPENFGA_API_TOKEN" \
      openfga/cli model get --store-id 01M754QT18P8SDPB0P0S0YK41G --model-id 01M754QT1SFXBR8VFJHYJ1WD1M

Uitvoer:

    model
      schema 1.1

    type user

    type message
      relations
        define can_delete: sender
        define can_read: recipient
        define recipient: [user]
        define sender: [user]

Het opgeslagen model komt overeen met plan §4.4: enkel de ontvanger leest (`can_read: recipient`), enkel de afzender verwijdert (`can_delete: sender`).

## Env-file en herstart

`OPENFGA_STORE_ID` en `OPENFGA_MODEL_ID` zijn toegevoegd aan `/etc/unbreakable/.env`, daarna `docker compose up -d app` (container `app-1` gestart). `AUTH_MODE` staat nog op `legacy`.
