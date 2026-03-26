#!/usr/bin/env bash
set -euo pipefail

# Keeps API warm while admin dashboard is actively used.
# Requires ADMIN_JWT_TOKEN and API_BASE_URL.

API_BASE_URL="${API_BASE_URL:-https://roast66-api.onrender.com/api}"
PULSE_SECONDS="${PULSE_SECONDS:-240}"
SOURCE="${SOURCE:-ops-script}"

if [[ -z "${ADMIN_JWT_TOKEN:-}" ]]; then
  echo "ADMIN_JWT_TOKEN is required"
  exit 1
fi

echo "Starting keepalive pulse against ${API_BASE_URL}"
while true; do
  curl -sS -X POST "${API_BASE_URL}/ops/keepalive/heartbeat" \
    -H "Authorization: Bearer ${ADMIN_JWT_TOKEN}" \
    -H "Content-Type: application/json" \
    --data "{\"source\":\"${SOURCE}\"}" >/dev/null
  echo "pulse sent: $(date -u +"%Y-%m-%dT%H:%M:%SZ")"
  sleep "${PULSE_SECONDS}"
done
