#!/usr/bin/env bash
set -euo pipefail

REPO_DIR="${REPO_DIR:-$HOME/dragon/IdeaEngine}"
ENV_FILE="${ENV_FILE:-$REPO_DIR/.env}"
SERVICE_NAME="${SERVICE_NAME:-dragon-idea-engine}"
BACKUP_TIMER_NAME="${BACKUP_TIMER_NAME:-dragon-backup}"
UPDATE_TIMER_NAME="${UPDATE_TIMER_NAME:-dragon-update}"
ALERT_TIMER_NAME="${ALERT_TIMER_NAME:-dragon-alert-check}"
MIN_FREE_GB="${MIN_FREE_GB:-5}"

PASS_COUNT=0
WARN_COUNT=0
FAIL_COUNT=0

pass() {
  PASS_COUNT=$((PASS_COUNT + 1))
  echo "[pass] $1"
}

warn() {
  WARN_COUNT=$((WARN_COUNT + 1))
  echo "[warn] $1"
}

fail() {
  FAIL_COUNT=$((FAIL_COUNT + 1))
  echo "[fail] $1"
}

require_repo_file() {
  local path="$1"
  local label="$2"
  if [[ -f "${path}" ]]; then
    pass "${label} present"
  else
    fail "${label} missing: ${path}"
  fi
}

check_command() {
  local command_name="$1"
  if command -v "${command_name}" >/dev/null 2>&1; then
    pass "command available: ${command_name}"
  else
    fail "missing required command: ${command_name}"
  fi
}

read_env_value() {
  local key="$1"
  if [[ ! -f "${ENV_FILE}" ]]; then
    return
  fi

  local line
  line="$(grep -E "^${key}=" "${ENV_FILE}" | tail -n 1 || true)"
  echo "${line#*=}"
}

check_env_value() {
  local key="$1"
  local description="$2"
  local value
  value="$(read_env_value "${key}")"
  if [[ -n "${value}" ]]; then
    pass "${description} configured"
  else
    warn "${description} is empty in ${ENV_FILE}"
  fi
}

check_github_token() {
  local github_token gh_token
  github_token="$(read_env_value GITHUB_TOKEN)"
  gh_token="$(read_env_value GH_TOKEN)"

  if [[ -n "${github_token}" || -n "${gh_token}" ]]; then
    pass "GitHub token configured"
  else
    warn "GITHUB_TOKEN and GH_TOKEN are both empty in ${ENV_FILE}"
  fi
}

check_disk_space() {
  local available_kb available_gb
  available_kb="$(df -Pk "${REPO_DIR}" | awk 'NR==2 { print $4 }')"
  available_gb=$((available_kb / 1024 / 1024))

  if [[ "${available_gb}" -ge "${MIN_FREE_GB}" ]]; then
    pass "disk space looks healthy (${available_gb} GB free)"
  else
    warn "only ${available_gb} GB free under ${REPO_DIR}; recommended minimum is ${MIN_FREE_GB} GB"
  fi
}

check_docker_group() {
  if id -nG "${USER}" | grep -qw docker; then
    pass "current user is in the docker group"
  else
    warn "current user is not in the docker group; Docker commands may require sudo"
  fi
}

check_compose_plugin() {
  if docker compose version >/dev/null 2>&1; then
    pass "Docker Compose plugin available"
  else
    fail "Docker Compose plugin is not available"
  fi
}

check_systemd_unit_state() {
  local unit_name="$1"
  local label="$2"

  if systemctl list-unit-files "${unit_name}" --no-legend 2>/dev/null | grep -q "^${unit_name}"; then
    pass "${label} installed"
  else
    warn "${label} not installed"
    return
  fi

  if systemctl is-enabled "${unit_name}" >/dev/null 2>&1; then
    pass "${label} enabled"
  else
    warn "${label} not enabled"
  fi
}

print_summary() {
  echo
  echo "Preflight summary: ${PASS_COUNT} pass, ${WARN_COUNT} warn, ${FAIL_COUNT} fail"

  if [[ "${FAIL_COUNT}" -gt 0 ]]; then
    echo "Preflight failed. Resolve the failing items before starting the Pi service." >&2
    exit 1
  fi

  if [[ "${WARN_COUNT}" -gt 0 ]]; then
    echo "Preflight completed with warnings."
  else
    echo "Preflight looks good."
  fi
}

main() {
  [[ -d "${REPO_DIR}" ]] || {
    echo "Repo directory not found: ${REPO_DIR}" >&2
    exit 1
  }

  check_command docker
  check_command git
  check_command systemctl
  check_compose_plugin
  check_docker_group

  require_repo_file "${REPO_DIR}/docker-compose.yml" "docker-compose.yml"
  require_repo_file "${REPO_DIR}/backend/Dockerfile" "backend Dockerfile"
  require_repo_file "${REPO_DIR}/scripts/setup-pi.sh" "setup-pi.sh"
  require_repo_file "${ENV_FILE}" ".env file"

  check_env_value OPENAI_API_KEY "OpenAI API key"
  check_github_token
  check_env_value DRAGON_GITHUB_OWNER "GitHub owner"
  check_env_value DRAGON_GITHUB_REPO "GitHub repo"
  check_env_value DRAGON_RUN_MODE "run mode"
  check_env_value DRAGON_RELEASE_QUARANTINED_ON_START "release quarantined on start setting"

  check_disk_space

  check_systemd_unit_state "${SERVICE_NAME}.service" "main service"
  check_systemd_unit_state "${BACKUP_TIMER_NAME}.timer" "backup timer"
  check_systemd_unit_state "${UPDATE_TIMER_NAME}.timer" "update timer"
  check_systemd_unit_state "${ALERT_TIMER_NAME}.timer" "alert timer"

  print_summary
}

main "$@"
