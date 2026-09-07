#!/usr/bin/env bash
set -Eeuo pipefail

script_dir=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
default_repository_root=$(cd "$script_dir/../.." && pwd)
scope="${1:-}"
repository_root="${2:-$default_repository_root}"
browser_check="${3:-}"
failures=()

usage() {
  echo "Usage: scripts/ci/local-preflight.sh <compose|full> [repository-root] [--require-browser]" >&2
}

add_failure() {
  failures+=("$1")
}

check_command() {
  local command_name=$1
  if ! command -v "$command_name" >/dev/null 2>&1; then
    add_failure "Required command is unavailable: $command_name"
    return 1
  fi
}

check_docker() {
  if ! check_command docker; then
    return
  fi
  if ! docker info >/dev/null 2>&1; then
    add_failure "Docker is installed, but the Docker daemon is not usable. Start Docker and retry."
  fi
  if ! docker compose version >/dev/null 2>&1; then
    add_failure "Docker Compose is unavailable. Install or enable the Docker Compose plugin and retry."
  fi
}

trim_whitespace() {
  local value=$1
  value="${value#"${value%%[![:space:]]*}"}"
  value="${value%"${value##*[![:space:]]}"}"
  printf '%s' "$value"
}

read_env_value() {
  local file=$1
  local key=$2
  local line name value
  ENV_VALUE_FOUND=false
  ENV_VALUE=""
  ENV_VALUE_SINGLE_QUOTED=false

  while IFS= read -r line || [ -n "$line" ]; do
    line="${line%$'\r'}"
    line=$(trim_whitespace "$line")
    if [ -z "$line" ] || [[ "$line" == \#* ]] || [[ "$line" != *=* ]]; then
      continue
    fi
    name=$(trim_whitespace "${line%%=*}")
    name="${name#export }"
    if [ "$name" != "$key" ]; then
      continue
    fi

    value=$(trim_whitespace "${line#*=}")
    ENV_VALUE_SINGLE_QUOTED=false
    if [[ "$value" == \'*\' ]] && [ "${#value}" -ge 2 ]; then
      value="${value:1:${#value}-2}"
      ENV_VALUE_SINGLE_QUOTED=true
    elif [[ "$value" == \"*\" ]] && [ "${#value}" -ge 2 ]; then
      value="${value:1:${#value}-2}"
    fi
    ENV_VALUE_FOUND=true
    ENV_VALUE=$value
  done <"$file"
}

check_required_env_file() {
  local relative_path=$1
  local example_path=$2
  if [ ! -f "$repository_root/$relative_path" ]; then
    add_failure "Missing required local environment file: $relative_path. Create it from the checked-in example: cp $example_path $relative_path"
    return 1
  fi
}

check_owner_password() {
  local backend_env="$repository_root/CoffeeShopApi/.env"
  local password source_key
  if [ ! -f "$backend_env" ]; then
    return
  fi

  read_env_value "$backend_env" Bootstrap__Password
  if [ "$ENV_VALUE_FOUND" = true ]; then
    password=$ENV_VALUE
    source_key=Bootstrap__Password
  else
    read_env_value "$backend_env" Admin__Password
    if [ "$ENV_VALUE_FOUND" = true ]; then
      password=$ENV_VALUE
      source_key=Admin__Password
    else
      password=$(sed -n \
        's/.*DevelopmentPassword = "\([^"]*\)";.*/\1/p' \
        "$repository_root/CoffeeShopApi/SecurityConfiguration.cs" | head -n 1)
      source_key="the Development Admin password default"
      if [ -z "$password" ]; then
        add_failure "Could not derive the Development Admin password default from CoffeeShopApi/SecurityConfiguration.cs."
        return
      fi
    fi
  fi

  if [ "$ENV_VALUE_SINGLE_QUOTED" = false ] && [[ "$password" == *'$'* ]]; then
    add_failure "$source_key uses Compose interpolation. Set it to a literal single-quoted value so the effective local Owner password can be validated safely."
    return
  fi

  local unmet=()
  if [ "${#password}" -lt 12 ]; then unmet+=("at least 12 characters"); fi
  if [[ ! "$password" =~ [[:digit:]] ]]; then unmet+=("a digit"); fi
  if [[ ! "$password" =~ [[:lower:]] ]]; then unmet+=("a lowercase letter"); fi
  if [[ ! "$password" =~ [[:upper:]] ]]; then unmet+=("an uppercase letter"); fi
  if [[ ! "$password" =~ [^[:alnum:]] ]]; then unmet+=("a non-alphanumeric character"); fi

  if [ "${#unmet[@]}" -gt 0 ]; then
    local index requirements=${unmet[0]}
    for ((index = 1; index < ${#unmet[@]}; index++)); do
      requirements+=", ${unmet[$index]}"
    done
    add_failure "The effective local Owner password selected from $source_key does not satisfy the current Identity password policy; it needs $requirements. Update $source_key in CoffeeShopApi/.env."
  fi
  unset password ENV_VALUE
}

check_compose_scope() {
  check_docker
  check_required_env_file .env env.example || true
  check_required_env_file CoffeeShopApi/.env CoffeeShopApi/.env.example || true
  check_required_env_file roast66/.env roast66/.env.example || true
  check_owner_password
}

check_dotnet_version() {
  local project_file="$repository_root/CoffeeShopApi/CoffeeShopApi.csproj"
  local required_major active_version active_major
  required_major=$(sed -n 's:.*<TargetFramework>net\([0-9][0-9]*\)\..*:\1:p' "$project_file" | head -n 1)
  if [ -z "$required_major" ]; then
    add_failure "Could not derive the required .NET SDK from CoffeeShopApi/CoffeeShopApi.csproj."
    return
  fi
  if ! check_command dotnet; then
    return
  fi
  active_version=$(dotnet --version 2>/dev/null || true)
  active_major=${active_version%%.*}
  if [[ ! "$active_major" =~ ^[0-9]+$ ]] || [ "$active_major" -lt "$required_major" ]; then
    add_failure ".NET SDK $required_major or newer is required to build net$required_major.0; active SDK is ${active_version:-unknown}. Install a compatible SDK or update the active SDK selection."
  fi
}

check_node_version() {
  local version_file="$repository_root/.node-version"
  local required_version required_major active_version active_major
  required_version=$(tr -d '[:space:]' <"$version_file")
  required_major=${required_version%%.*}
  if [[ ! "$required_major" =~ ^[0-9]+$ ]]; then
    add_failure "Could not derive the required Node major version from .node-version."
    return
  fi
  if ! check_command node; then
    return
  fi
  active_version=$(node --version 2>/dev/null || true)
  active_major=${active_version#v}
  active_major=${active_major%%.*}
  if [[ ! "$active_major" =~ ^[0-9]+$ ]] || [ "$active_major" -ne "$required_major" ]; then
    add_failure "Node $required_major.x is required by .node-version; active version is ${active_version:-unknown}. Activate Node $required_version and retry."
  fi
}

check_playwright_browser() {
  local browser_path
  browser_path=$(
    cd "$repository_root/roast66"
    node --input-type=module -e \
      'import { chromium } from "@playwright/test"; process.stdout.write(chromium.executablePath());' \
      2>/dev/null || true
  )
  if [ -z "$browser_path" ] || [ ! -x "$browser_path" ]; then
    add_failure "Playwright's managed Chromium is unavailable. Run: cd roast66 && npx playwright install chromium"
  fi
}

check_full_scope() {
  local command_name
  check_docker
  check_dotnet_version
  check_node_version
  for command_name in npm npx python3 rg curl realpath; do
    check_command "$command_name" || true
  done
  if [ "$browser_check" = --require-browser ]; then
    check_playwright_browser
  fi
}

if [ "$scope" != compose ] && [ "$scope" != full ]; then
  usage
  exit 2
fi
if [ -n "$browser_check" ] && { [ "$scope" != full ] || [ "$browser_check" != --require-browser ]; }; then
  usage
  exit 2
fi
if [ ! -d "$repository_root" ]; then
  echo "Repository root does not exist: $repository_root" >&2
  exit 2
fi

if [ "$scope" = compose ]; then
  check_compose_scope
else
  check_full_scope
fi

if [ "${#failures[@]}" -gt 0 ]; then
  echo "Local preflight found ${#failures[@]} problem(s):" >&2
  printf '  - %s\n' "${failures[@]}" >&2
  exit 1
fi

if [ "$scope" = compose ]; then
  echo "Compose/local-stack preflight passed."
else
  echo "Full-local-smoke preflight passed."
fi
