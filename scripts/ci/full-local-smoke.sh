#!/usr/bin/env bash
set -Eeuo pipefail

script_dir=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
candidate_context=$(cd "$script_dir/../.." && pwd)
base_context=$(realpath "${1:-$candidate_context}")
suffix="local-$$"
database_name="roast66-full-smoke-db-$suffix"
coverage_directory=$(mktemp -d /tmp/roast66-full-smoke-coverage.XXXXXX)

cleanup() {
  local status=$?
  set +e
  if [ "$status" -eq 0 ]; then
    echo "Full local smoke passed. Coverage report: $coverage_directory"
  else
    if docker inspect "$database_name" >/dev/null 2>&1; then
      echo "Disposable PostgreSQL log:" >&2
      docker logs "$database_name" >&2 2>/dev/null
    fi
    echo "Full local smoke failed. Coverage artifacts remain at $coverage_directory" >&2
  fi
  docker rm -f "$database_name" >/dev/null 2>&1
  exit "$status"
}
trap cleanup EXIT

for dependency in docker dotnet npm npx python3; do
  if ! command -v "$dependency" >/dev/null 2>&1; then
    echo "Required command is unavailable: $dependency" >&2
    exit 1
  fi
done

if [ ! -f "$base_context/Dockerfile.backend" ]; then
  echo "Baseline checkout must contain Dockerfile.backend: $base_context" >&2
  exit 1
fi

echo "Starting disposable PostgreSQL 17 for backend release contracts"
docker run --rm -d \
  --name "$database_name" \
  -p "127.0.0.1::5432" \
  -e POSTGRES_DB=postgres \
  -e POSTGRES_USER=postgres \
  -e POSTGRES_PASSWORD=postgres \
  postgres:17 >/dev/null

for _ in $(seq 1 30); do
  if docker exec "$database_name" pg_isready -U postgres -d postgres >/dev/null 2>&1; then
    break
  fi
  sleep 1
done
docker exec "$database_name" pg_isready -U postgres -d postgres >/dev/null
database_port=$(docker inspect "$database_name" \
  --format '{{(index (index .NetworkSettings.Ports "5432/tcp") 0).HostPort}}')

echo "Running coverage reporter tests"
PYTHONDONTWRITEBYTECODE=1 python3 -m unittest discover \
  -s "$candidate_context/scripts/ci/tests" -p 'test_*.py' -v

echo "Running the complete backend suite with PostgreSQL and coverage"
dotnet restore "$candidate_context/Roast66.sln"
REQUIRE_POSTGRES_INTEGRATION_TESTS=true \
POSTGRES_INTEGRATION_CONNECTION_STRING="Host=127.0.0.1;Port=$database_port;Database=postgres;Username=postgres;Password=postgres" \
  dotnet test "$candidate_context/Roast66.sln" \
    --configuration Release \
    --verbosity minimal \
    --settings "$candidate_context/coverlet.runsettings" \
    --collect:"XPlat Code Coverage" \
    --results-directory "$coverage_directory"
python3 "$candidate_context/scripts/ci/backend_coverage.py" "$coverage_directory"

echo "Running frontend tests, lint, production build, and dependency audit"
npm --prefix "$candidate_context/roast66" ci
npm --prefix "$candidate_context/roast66" test
npm --prefix "$candidate_context/roast66" run lint
npm --prefix "$candidate_context/roast66" run build
npm --prefix "$candidate_context/roast66" audit --audit-level=high

echo "Running the Render migration and browser release smoke"
"$candidate_context/scripts/ci/render-smoke.sh" \
  "$candidate_context" \
  "$base_context"
