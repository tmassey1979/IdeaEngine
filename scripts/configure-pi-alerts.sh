#!/usr/bin/env bash
set -euo pipefail

REPO_DIR="${REPO_DIR:-$HOME/dragon/IdeaEngine}"
ENV_FILE="${ENV_FILE:-$REPO_DIR/.env}"
TEMPLATE_FILE="${TEMPLATE_FILE:-$REPO_DIR/.env.docker.example}"
PROMPT_IF_MISSING="${PROMPT_IF_MISSING:-true}"

ALERT_WEBHOOK_URL_VALUE="${ALERT_WEBHOOK_URL_VALUE:-${ALERT_WEBHOOK_URL:-}}"
MAX_SERVICE_RESTARTS_VALUE="${MAX_SERVICE_RESTARTS_VALUE:-}"
MAX_FAILED_ISSUES_VALUE="${MAX_FAILED_ISSUES_VALUE:-}"
MAX_ACTIONABLE_QUARANTINED_VALUE="${MAX_ACTIONABLE_QUARANTINED_VALUE:-}"
ALLOW_HEALTH_STATES_VALUE="${ALLOW_HEALTH_STATES_VALUE:-}"

fail() {
  echo "$1" >&2
  exit 1
}

ensure_env_file() {
  if [[ -f "${ENV_FILE}" ]]; then
    return
  fi

  [[ -f "${TEMPLATE_FILE}" ]] || fail "Template file not found: ${TEMPLATE_FILE}"
  cp "${TEMPLATE_FILE}" "${ENV_FILE}"
  echo "Created ${ENV_FILE} from ${TEMPLATE_FILE}"
}

read_existing_value() {
  local key="$1"
  if [[ ! -f "${ENV_FILE}" ]]; then
    return
  fi

  local line
  line="$(grep -E "^${key}=" "${ENV_FILE}" | tail -n 1 || true)"
  echo "${line#*=}"
}

prompt_value() {
  local prompt_text="$1"
  local current_value="${2:-}"
  local input=""

  if [[ "${PROMPT_IF_MISSING}" != "true" ]]; then
    echo "${current_value}"
    return
  fi

  if [[ -n "${current_value}" ]]; then
    read -r -p "${prompt_text} [${current_value}]: " input
  else
    read -r -p "${prompt_text}: " input
  fi

  if [[ -n "${input}" ]]; then
    echo "${input}"
  else
    echo "${current_value}"
  fi
}

set_env_value() {
  local key="$1"
  local value="$2"
  local tmp_file

  tmp_file="$(mktemp)"
  awk -v key="${key}" -v value="${value}" '
    BEGIN { updated = 0 }
    index($0, key "=") == 1 {
      print key "=" value
      updated = 1
      next
    }
    { print }
    END {
      if (!updated) {
        print key "=" value
      }
    }
  ' "${ENV_FILE}" > "${tmp_file}"
  mv "${tmp_file}" "${ENV_FILE}"
}

main() {
  [[ -d "${REPO_DIR}" ]] || fail "Repo directory not found: ${REPO_DIR}"
  ensure_env_file

  local current_webhook current_restarts current_failed current_quarantined current_health
  current_webhook="$(read_existing_value ALERT_WEBHOOK_URL)"
  current_restarts="$(read_existing_value MAX_SERVICE_RESTARTS)"
  current_failed="$(read_existing_value MAX_FAILED_ISSUES)"
  current_quarantined="$(read_existing_value MAX_ACTIONABLE_QUARANTINED)"
  current_health="$(read_existing_value ALLOW_HEALTH_STATES)"

  local webhook restarts failed quarantined health_states
  webhook="${ALERT_WEBHOOK_URL_VALUE:-$(prompt_value "Alert webhook URL (leave blank to disable)" "${current_webhook}")}"
  restarts="${MAX_SERVICE_RESTARTS_VALUE:-$(prompt_value "Max service restarts before alert" "${current_restarts:-5}")}"
  failed="${MAX_FAILED_ISSUES_VALUE:-$(prompt_value "Max failed issues before alert" "${current_failed:-0}")}"
  quarantined="${MAX_ACTIONABLE_QUARANTINED_VALUE:-$(prompt_value "Max actionable quarantined issues before alert" "${current_quarantined:-0}")}"
  health_states="${ALLOW_HEALTH_STATES_VALUE:-$(prompt_value "Allowed worker health states (comma-separated)" "${current_health:-healthy,idle}")}"

  set_env_value ALERT_WEBHOOK_URL "${webhook}"
  set_env_value MAX_SERVICE_RESTARTS "${restarts}"
  set_env_value MAX_FAILED_ISSUES "${failed}"
  set_env_value MAX_ACTIONABLE_QUARANTINED "${quarantined}"
  set_env_value ALLOW_HEALTH_STATES "${health_states}"

  echo "Updated ${ENV_FILE}"
}

main "$@"
