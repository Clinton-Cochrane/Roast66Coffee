#!/usr/bin/env bash
set -Eeuo pipefail

script_dir=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
default_candidate=$(cd "$script_dir/../.." && pwd)
candidate_context=$(realpath "${1:-$default_candidate}")
base_context=$(realpath "${2:-$candidate_context}")

suffix="${GITHUB_RUN_ID:-local}-${GITHUB_RUN_ATTEMPT:-1}-$$"
network_name="roast66-release-$suffix"
database_name="roast66-release-db-$suffix"
api_name="roast66-release-api-$suffix"
candidate_image="roast66-api-candidate:$suffix"
base_image="roast66-api-base:$suffix"
api_port="${SMOKE_API_PORT:-18080}"
web_port="${SMOKE_WEB_PORT:-4173}"
menu_item="CI Smoke Latte"
menu_response=$(mktemp)
api_log=$(mktemp)
preview_log=$(mktemp)
preview_pid=""

cleanup() {
  local status=$?
  set +e
  if [ "$status" -ne 0 ]; then
    echo "Release smoke failed. Candidate API log:" >&2
    docker logs "$api_name" >&2 2>/dev/null
    echo "Built frontend preview log:" >&2
    sed -n '1,200p' "$preview_log" >&2 2>/dev/null
  fi
  if [ -n "$preview_pid" ]; then
    kill "$preview_pid" >/dev/null 2>&1
    wait "$preview_pid" >/dev/null 2>&1
  fi
  docker rm -f "$api_name" "$database_name" >/dev/null 2>&1
  docker network rm "$network_name" >/dev/null 2>&1
  docker image rm "$candidate_image" "$base_image" >/dev/null 2>&1
  rm -f "$menu_response" "$api_log" "$preview_log"
  exit "$status"
}
trap cleanup EXIT

wait_for_postgres() {
  for _ in $(seq 1 30); do
    if docker exec "$database_name" pg_isready -U roast66 -d coffeedb >/dev/null 2>&1; then
      return
    fi
    sleep 1
  done
  echo "PostgreSQL did not become ready." >&2
  return 1
}

start_candidate_api() {
  docker run -d \
    --name "$api_name" \
    --network "$network_name" \
    -p "127.0.0.1:$api_port:8080" \
    -e PORT=8080 \
    -e ASPNETCORE_ENVIRONMENT=Production \
    -e "ConnectionStrings__DefaultConnection=Host=$database_name;Port=5432;Database=coffeedb;Username=roast66;Password=roast66-test" \
    -e "AllowedOrigins=http://127.0.0.1:$web_port" \
    -e Admin__Username=smoke-admin \
    -e Admin__Password=SmokePassword_NotForProduction \
    -e Jwt__Key=SmokeJwtSigningKey_NotForProduction_AtLeast32Chars \
    -e Jwt__Issuer=Roast66Coffee \
    -e Jwt__Audience=Roast66Coffee \
    -e KeepAlive__Enabled=false \
    "$candidate_image" >/dev/null
}

wait_for_api() {
  for _ in $(seq 1 45); do
    if ! docker inspect "$api_name" --format '{{.State.Running}}' 2>/dev/null | grep -q true; then
      echo "Candidate API exited before becoming ready." >&2
      return 1
    fi
    if curl --fail --silent --show-error \
      "http://127.0.0.1:$api_port/api/health/ready" >/dev/null 2>&1; then
      return
    fi
    sleep 1
  done
  echo "Candidate API did not become ready." >&2
  return 1
}

assert_menu_api() {
  curl --fail --silent --show-error \
    "http://127.0.0.1:$api_port/api/menu" >"$menu_response"
  grep -Fq "\"name\":\"$menu_item\"" "$menu_response"
  grep -Fq '"isArchived":false' "$menu_response"
}

echo "Building the base backend image from $base_context"
docker build -f "$base_context/Dockerfile.backend" -t "$base_image" "$base_context"

echo "Building the candidate backend image from $candidate_context"
docker build -f "$candidate_context/Dockerfile.backend" -t "$candidate_image" "$candidate_context"

docker network create "$network_name" >/dev/null
docker run --rm -d \
  --name "$database_name" \
  --network "$network_name" \
  -e POSTGRES_DB=coffeedb \
  -e POSTGRES_USER=roast66 \
  -e POSTGRES_PASSWORD=roast66-test \
  -v "$candidate_context/docker/postgres/init-local-roles.sql:/docker-entrypoint-initdb.d/10-local-roles.sql:ro" \
  postgres:17 >/dev/null
wait_for_postgres

echo "Applying the base image migrations and loading representative data"
docker run --rm \
  --network "$network_name" \
  -e ASPNETCORE_ENVIRONMENT=Development \
  -e "ConnectionStrings__DefaultConnection=Host=$database_name;Port=5432;Database=coffeedb;Username=roast66;Password=roast66-test" \
  "$base_image" migrate
docker exec -i "$database_name" \
  psql -v ON_ERROR_STOP=1 -U roast66 -d coffeedb \
  <"$candidate_context/docker/postgres/render-smoke-fixture.sql"

echo "Confirming a failed migration prevents the candidate API from starting"
if docker run --rm \
  -e ASPNETCORE_ENVIRONMENT=Testing \
  "$candidate_image" >"$api_log" 2>&1; then
  echo "Candidate image unexpectedly started after its migration command failed." >&2
  exit 1
fi
grep -Fq 'The migrate command cannot run in Testing.' "$api_log"

echo "Starting the candidate image through its production entrypoint"
start_candidate_api
wait_for_api
assert_menu_api

docker logs "$api_name" >"$api_log" 2>&1
migration_line=$(grep -n -m1 'Database initialization successful.' "$api_log" | cut -d: -f1)
listening_line=$(grep -n -m1 'Now listening on:' "$api_log" | cut -d: -f1)
test -n "$migration_line"
test -n "$listening_line"
test "$migration_line" -lt "$listening_line"

echo "Building and serving the frontend with the candidate API URL"
if [ ! -d "$candidate_context/roast66/node_modules" ]; then
  npm --prefix "$candidate_context/roast66" ci
fi
VITE_API_URL="http://127.0.0.1:$api_port/api" \
VITE_USE_STATIC_MENU=false \
  npm --prefix "$candidate_context/roast66" run build
npm --prefix "$candidate_context/roast66" run preview -- \
  --host 127.0.0.1 --port "$web_port" >"$preview_log" 2>&1 &
preview_pid=$!

for _ in $(seq 1 30); do
  if curl --fail --silent --show-error \
    "http://127.0.0.1:$web_port/menu" >/dev/null 2>&1; then
    break
  fi
  sleep 1
done
curl --fail --silent --show-error \
  "http://127.0.0.1:$web_port/menu" >/dev/null

echo "Verifying the built menu in Chromium"
(
  cd "$candidate_context/roast66"
  PLAYWRIGHT_HTML_OPEN=never \
  SMOKE_WEB_URL="http://127.0.0.1:$web_port" \
  SMOKE_MENU_ITEM="$menu_item" \
    npx playwright test e2e/menu-smoke.spec.ts \
      --config=playwright.config.ts --project=chromium
)

echo "Restarting the candidate image to verify migration idempotence"
docker rm -f "$api_name" >/dev/null
start_candidate_api
wait_for_api
assert_menu_api

echo "Render release smoke passed."
