#!/bin/sh
set -eu

assert_contains() {
  file=$1
  expected=$2
  if ! grep -Fq -- "$expected" "$file"; then
    echo "$file must contain: $expected" >&2
    exit 1
  fi
}

assert_absent() {
  file=$1
  unexpected=$2
  if grep -Fiq -- "$unexpected" "$file"; then
    echo "$file must not contain: $unexpected" >&2
    exit 1
  fi
}

test -f render.dev.yaml
test -f render.prod.yaml
test ! -e render.yaml

assert_contains render.dev.yaml "name: roast66-api"
assert_contains render.dev.yaml "name: roast66-web"
assert_contains render.dev.yaml "plan: free"
assert_contains render.dev.yaml "branch: dev"
assert_contains render.dev.yaml "ConnectionStrings__DefaultConnection"
assert_contains render.dev.yaml "ForwardedHeaders__Enabled"
assert_contains render.dev.yaml "ForwardedHeaders__KnownProxies"
assert_contains render.dev.yaml "ForwardedHeaders__KnownNetworks"

assert_contains render.prod.yaml "name: roast66-api-prod"
assert_contains render.prod.yaml "name: roast66-web-prod"
assert_contains render.prod.yaml "name: roast66-db-prod"
assert_contains render.prod.yaml "plan: 0.5c-512mb"
assert_contains render.prod.yaml "plan: 0.1c-256mb"
assert_contains render.prod.yaml 'postgresMajorVersion: "17"'
assert_contains render.prod.yaml "region: oregon"
assert_contains render.prod.yaml "connectionPool: none"
assert_contains render.prod.yaml "ipAllowList: []"
assert_contains render.prod.yaml "branch: prod"
assert_contains render.prod.yaml 'autoDeployTrigger: "off"'
assert_contains render.prod.yaml "healthCheckPath: /api/health/ready"
assert_contains render.prod.yaml "property: connectionString"
assert_contains render.prod.yaml 'value: "48"'
assert_contains render.prod.yaml "ForwardedHeaders__Enabled"
assert_contains render.prod.yaml "ForwardedHeaders__KnownProxies"
assert_contains render.prod.yaml "ForwardedHeaders__KnownNetworks"

for manifest in render.dev.yaml render.prod.yaml; do
  assert_absent "$manifest" "keepalive"
  assert_absent "$manifest" "supabaseheartbeat"
done

echo "Hosting configuration contracts passed."
