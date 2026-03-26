#!/usr/bin/env bash
set -euo pipefail

API_HEALTH_URL="${API_HEALTH_URL:-https://roast66-api.onrender.com/api/health}"

echo "Checking ${API_HEALTH_URL}"
response="$(curl -sS -w '\n%{http_code}' "${API_HEALTH_URL}")"
body="$(printf '%s' "${response}" | sed '$d')"
status="$(printf '%s' "${response}" | tail -n 1)"

if [[ "${status}" != "200" ]]; then
  echo "Health check failed with status ${status}"
  echo "${body}"
  exit 1
fi

echo "OK: ${body}"
