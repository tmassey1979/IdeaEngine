#!/usr/bin/env bash
set -euo pipefail

REPO_DIR="${REPO_DIR:-$HOME/dragon/IdeaEngine}"
BIN_DIR="${BIN_DIR:-$HOME/.local/bin}"
SERVICE_NAME="${SERVICE_NAME:-dragon-idea-engine}"
BACKUP_SERVICE_NAME="${BACKUP_SERVICE_NAME:-dragon-backup}"
BACKUP_TIMER_NAME="${BACKUP_TIMER_NAME:-dragon-backup}"
UPDATE_SERVICE_NAME="${UPDATE_SERVICE_NAME:-dragon-update}"
UPDATE_TIMER_NAME="${UPDATE_TIMER_NAME:-dragon-update}"
ALERT_SERVICE_NAME="${ALERT_SERVICE_NAME:-dragon-alert-check}"
ALERT_TIMER_NAME="${ALERT_TIMER_NAME:-dragon-alert-check}"
REMOVE_REPO_DIR="${REMOVE_REPO_DIR:-false}"

WRAPPERS=(
  "dragon-report"
  "dragon-health"
  "dragon-update"
  "dragon-backup"
  "dragon-diagnostics"
  "dragon-firstaid"
  "dragon-alert-check"
  "dragon-alert-notify"
  "dragon-configure-alerts"
  "dragon-ops-summary"
  "dragon-reinstall-service"
  "dragon-tail-logs"
)

disable_unit_if_present() {
  local unit_name="$1"

  if systemctl list-unit-files "${unit_name}" --no-legend 2>/dev/null | grep -Fq "${unit_name}"; then
    sudo systemctl stop "${unit_name}" 2>/dev/null || true
    sudo systemctl disable "${unit_name}" 2>/dev/null || true
    sudo rm -f "/etc/systemd/system/${unit_name}"
    echo "Removed ${unit_name}"
  fi
}

remove_wrapper_if_present() {
  local command_name="$1"
  local wrapper_path="${BIN_DIR}/${command_name}"

  if [[ -f "${wrapper_path}" ]]; then
    rm -f "${wrapper_path}"
    echo "Removed ${wrapper_path}"
  fi
}

main() {
  disable_unit_if_present "${SERVICE_NAME}.service"
  disable_unit_if_present "${BACKUP_TIMER_NAME}.timer"
  disable_unit_if_present "${BACKUP_SERVICE_NAME}.service"
  disable_unit_if_present "${UPDATE_TIMER_NAME}.timer"
  disable_unit_if_present "${UPDATE_SERVICE_NAME}.service"
  disable_unit_if_present "${ALERT_TIMER_NAME}.timer"
  disable_unit_if_present "${ALERT_SERVICE_NAME}.service"

  sudo systemctl daemon-reload

  for wrapper in "${WRAPPERS[@]}"; do
    remove_wrapper_if_present "${wrapper}"
  done

  if [[ "${REMOVE_REPO_DIR}" == "true" && -d "${REPO_DIR}" ]]; then
    rm -rf "${REPO_DIR}"
    echo "Removed repo directory ${REPO_DIR}"
  fi

  echo "Pi uninstall complete."
}

main "$@"
