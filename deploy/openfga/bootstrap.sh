#!/usr/bin/env bash
# Maakt de OpenFGA-store en het autorisatiemodel aan en print de ID's.
# Gebruik (vanuit de repo-root):
#   export FGA_API_URL=http://openfga:8080
#   export FGA_API_TOKEN="$OPENFGA_API_TOKEN"
#   ./deploy/openfga/bootstrap.sh
# Zet daarna OPENFGA_STORE_ID en OPENFGA_MODEL_ID in /etc/unbreakable/.env.
set -euo pipefail
: "${FGA_API_URL:?FGA_API_URL ontbreekt}"
: "${FGA_API_TOKEN:?FGA_API_TOKEN ontbreekt}"

OUT=$(fga store create \
        --api-url "$FGA_API_URL" \
        --api-token "$FGA_API_TOKEN" \
        --name unbreakable \
        --model deploy/openfga/model.fga)

STORE_ID=$(echo "$OUT" | jq -r '.store.id')
MODEL_ID=$(echo "$OUT" | jq -r '.model.authorization_model_id')

printf 'OPENFGA_STORE_ID=%s\nOPENFGA_MODEL_ID=%s\n' "$STORE_ID" "$MODEL_ID"
