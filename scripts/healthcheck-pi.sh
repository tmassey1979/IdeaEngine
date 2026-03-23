#!/usr/bin/env bash
set -euo pipefail

REPO_DIR="${REPO_DIR:-$HOME/dragon/IdeaEngine}"
SERVICE_NAME="${SERVICE_NAME:-dragon-idea-engine}"
STATUS_URL="${STATUS_URL:-http://127.0.0.1:5078/status}"
HEALTH_URL="${HEALTH_URL:-http://127.0.0.1:5078/health}"
ENV_FILE="${ENV_FILE:-$REPO_DIR/.env}"

pass() {
  echo "[pass] $1"
}

warn() {
  echo "[warn] $1"
}

fail() {
  echo "[fail] $1" >&2
  exit 1
}

require_command() {
  local command_name="$1"
  command -v "${command_name}" >/dev/null 2>&1 || fail "Missing required command: ${command_name}"
}

check_file() {
  local path="$1"
  local label="$2"
  [[ -f "${path}" ]] || fail "${label} not found: ${path}"
  pass "${label} present"
}

check_service() {
  if systemctl is-enabled "${SERVICE_NAME}.service" >/dev/null 2>&1; then
    pass "systemd service enabled"
  else
    warn "systemd service ${SERVICE_NAME}.service is not enabled"
  fi

  if systemctl is-active "${SERVICE_NAME}.service" >/dev/null 2>&1; then
    pass "systemd service active"
  else
    warn "systemd service ${SERVICE_NAME}.service is not active"
  fi
}

check_compose() {
  local output
  output="$(cd "${REPO_DIR}" && docker compose ps --format json 2>/dev/null || true)"
  if [[ -z "${output}" ]]; then
    warn "docker compose ps returned no JSON output"
    return
  fi

  pass "docker compose returned container state"
}

check_http_endpoint() {
  local url="$1"
  local label="$2"
  if curl -fsS "${url}" >/dev/null; then
    pass "${label} reachable"
  else
    warn "${label} not reachable at ${url}"
  fi
}

check_env_placeholders() {
  check_file "${ENV_FILE}" ".env file"

  if grep -Eq '^OPENAI_API_KEY=$' "${ENV_FILE}"; then
    warn "OPENAI_API_KEY is still empty in ${ENV_FILE}"
  else
    pass "OPENAI_API_KEY appears configured"
  fi

  if grep -Eq '^GITHUB_TOKEN=$' "${ENV_FILE}" && grep -Eq '^GH_TOKEN=$' "${ENV_FILE}"; then
    warn "GITHUB_TOKEN and GH_TOKEN are both empty in ${ENV_FILE}"
  else
    pass "A GitHub token appears configured"
  fi
}

main() {
  require_command docker
  require_command curl
  require_command systemctl

  [[ -d "${REPO_DIR}" ]] || fail "Repo directory not found: ${REPO_DIR}"

  check_file "${REPO_DIR}/docker-compose.yml" "docker-compose.yml"
  check_file "${REPO_DIR}/backend/Dockerfile" "backend Dockerfile"
  check_env_placeholders
  check_service
  check_compose
  check_http_endpoint "${HEALTH_URL}" "health endpoint"
  check_http_endpoint "${STATUS_URL}" "status endpoint"
}

main "$@"
