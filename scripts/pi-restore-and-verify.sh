#!/usr/bin/env bash
set -euo pipefail

REPO_DIR="${REPO_DIR:-$HOME/dragon/IdeaEngine}"
BACKUP_ROOT="${BACKUP_ROOT:-$REPO_DIR/.tmp/pi-backups}"
BACKUP_SOURCE="${BACKUP_SOURCE:-}"
TIMEOUT_SECONDS="${TIMEOUT_SECONDS:-300}"
INTERVAL_SECONDS="${INTERVAL_SECONDS:-10}"
SKIP_ALERT_CHECK="${SKIP_ALERT_CHECK:-false}"

main() {
  local -a wait_args

  [[ -d "${REPO_DIR}/scripts" ]] || {
    echo "Repo scripts directory not found at ${REPO_DIR}/scripts" >&2
    exit 1
  }

  env \
    REPO_DIR="${REPO_DIR}" \
    BACKUP_ROOT="${BACKUP_ROOT}" \
    BACKUP_SOURCE="${BACKUP_SOURCE}" \
    "${REPO_DIR}/scripts/pi-verify-backup.sh"

  env \
    REPO_DIR="${REPO_DIR}" \
    BACKUP_ROOT="${BACKUP_ROOT}" \
    BACKUP_SOURCE="${BACKUP_SOURCE}" \
    "${REPO_DIR}/scripts/restore-pi.sh"

  wait_args=(--skip-ensure-running --timeout "${TIMEOUT_SECONDS}" --interval "${INTERVAL_SECONDS}")
  if [[ "${SKIP_ALERT_CHECK}" == "true" ]]; then
    wait_args+=(--skip-alert-check)
  fi

  env \
    REPO_DIR="${REPO_DIR}" \
    "${REPO_DIR}/scripts/pi-wait-until-healthy.sh" "${wait_args[@]}"
}

main "$@"
