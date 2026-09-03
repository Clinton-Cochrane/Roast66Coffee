#!/usr/bin/env bash
set -Eeuo pipefail

script_dir=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
candidate_context=$(cd "$script_dir/../.." && pwd)
base_context=$(realpath "${1:-$candidate_context}")
coverage_directory=$(mktemp -d /tmp/roast66-full-smoke-coverage.XXXXXX)

cleanup() {
  local status=$?
  set +e
  if [ "$status" -eq 0 ]; then
    echo "Full local smoke passed. Coverage report: $coverage_directory"
  else
    echo "Full local smoke failed. Coverage artifacts remain at $coverage_directory" >&2
  fi
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

echo "Verifying hosting configuration contracts"
(
  cd "$candidate_context"
  scripts/ci/verify-hosting-config.sh
)

echo "Running coverage reporter tests"
PYTHONDONTWRITEBYTECODE=1 python3 -m unittest discover \
  -s "$candidate_context/scripts/ci/tests" -p 'test_*.py' -v

echo "Running the complete backend suite with PostgreSQL and coverage"
dotnet restore "$candidate_context/Roast66.sln" \
  -p:NuGetAudit=true \
  -p:NuGetAuditMode=all \
  '-warnaserror:NU1901;NU1902;NU1903;NU1904'
dotnet list "$candidate_context/Roast66.sln" package \
  --vulnerable \
  --include-transitive
"$candidate_context/scripts/ci/with-postgres.sh" \
  dotnet test "$candidate_context/Roast66.sln" \
    --no-restore \
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
