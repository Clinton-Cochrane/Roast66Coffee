#!/usr/bin/env bash
set -Eeuo pipefail

if [ "$#" -eq 0 ]; then
  echo "Usage: scripts/ci/with-postgres.sh <command> [args...]" >&2
  exit 2
fi

script_dir=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
repository_root=$(cd "$script_dir/../.." && pwd)
suffix="${GITHUB_RUN_ID:-local}-${GITHUB_RUN_ATTEMPT:-1}-$$"
container_name="roast66-postgres-tests-$suffix"
admin_database="roast66_integration_admin"
admin_username="roast66_test_admin"
admin_password="roast66-disposable-test"

cleanup() {
  local status=$?
  set +e
  if [ "$status" -ne 0 ] && docker inspect "$container_name" >/dev/null 2>&1; then
    echo "Disposable PostgreSQL log:" >&2
    docker logs "$container_name" >&2 2>/dev/null
  fi
  docker rm -f "$container_name" >/dev/null 2>&1
  exit "$status"
}
trap cleanup EXIT

if ! command -v docker >/dev/null 2>&1; then
  echo "Required command is unavailable: docker" >&2
  exit 1
fi

echo "Starting disposable PostgreSQL 17 integration database"
docker run --rm -d \
  --name "$container_name" \
  -p "127.0.0.1::5432" \
  -e "POSTGRES_DB=$admin_database" \
  -e "POSTGRES_USER=$admin_username" \
  -e "POSTGRES_PASSWORD=$admin_password" \
  -v "$repository_root/docker/postgres/init-local-roles.sql:/docker-entrypoint-initdb.d/10-local-roles.sql:ro" \
  postgres:17 \
  postgres -c "roast66.test_run_id=$suffix" >/dev/null

for _ in $(seq 1 30); do
  if docker exec "$container_name" \
      pg_isready -U "$admin_username" -d "$admin_database" >/dev/null 2>&1; then
    break
  fi
  sleep 1
done
docker exec "$container_name" \
  pg_isready -U "$admin_username" -d "$admin_database" >/dev/null

database_port=$(docker inspect "$container_name" \
  --format '{{(index (index .NetworkSettings.Ports "5432/tcp") 0).HostPort}}')

REQUIRE_POSTGRES_INTEGRATION_TESTS=true \
POSTGRES_INTEGRATION_RUN_ID="$suffix" \
POSTGRES_INTEGRATION_CONNECTION_STRING="Host=127.0.0.1;Port=$database_port;Database=$admin_database;Username=$admin_username;Password=$admin_password" \
  "$@"
