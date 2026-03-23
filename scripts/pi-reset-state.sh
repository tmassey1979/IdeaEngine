#!/usr/bin/env bash
set -euo pipefail

REPO_DIR="${REPO_DIR:-$HOME/dragon/IdeaEngine}"
SERVICE_NAME="${SERVICE_NAME:-dragon-idea-engine}"
BACKUP_BEFORE_RESET="${BACKUP_BEFORE_RESET:-true}"
STOP_SERVICE="${STOP_SERVICE:-true}"
RESTART_SERVICE="${RESTART_SERVICE:-true}"
RESET_DIAGNOSTICS="${RESET_DIAGNOSTICS:-false}"

stop_service_if_requested() {
  if [[ "${STOP_SERVICE}" == "true" ]]; then
    sudo systemctl stop "${SERVICE_NAME}.service" || true
  fi
}

restart_service_if_requested() {
  if [[ "${STOP_SERVICE}" == "true" && "${RESTART_SERVICE}" == "true" ]]; then
    sudo systemctl start "${SERVICE_NAME}.service" || true
  fi
}

backup_if_requested() {
  if [[ "${BACKUP_BEFORE_RESET}" == "true" && -x "${REPO_DIR}/scripts/backup-pi.sh" ]]; then
    echo "Creating backup before state reset..."
    STOP_SERVICE="${STOP_SERVICE}" RESTART_SERVICE="false" "${REPO_DIR}/scripts/backup-pi.sh"
  fi
}

main() {
  [[ -d "${REPO_DIR}" ]] || {
    echo "Repo directory not found: ${REPO_DIR}" >&2
    exit 1
  }

  backup_if_requested
  stop_service_if_requested
  trap restart_service_if_requested EXIT

  if [[ -d "${REPO_DIR}/.dragon" ]]; then
    rm -rf "${REPO_DIR}/.dragon"
    echo "Removed ${REPO_DIR}/.dragon"
  fi

  if [[ "${RESET_DIAGNOSTICS}" == "true" && -d "${REPO_DIR}/.tmp/pi-diagnostics" ]]; then
    rm -rf "${REPO_DIR}/.tmp/pi-diagnostics"
    echo "Removed ${REPO_DIR}/.tmp/pi-diagnostics"
  fi

  mkdir -p "${REPO_DIR}/.dragon/state" "${REPO_DIR}/.dragon/status"
  echo "State reset complete."
}

main "$@"
