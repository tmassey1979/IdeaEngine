#!/usr/bin/env bash
set -euo pipefail

REPO_DIR="${REPO_DIR:-$HOME/dragon/IdeaEngine}"
INSTALL_SYSTEMD_SERVICE="${INSTALL_SYSTEMD_SERVICE:-true}"
INSTALL_BACKUP_TIMER="${INSTALL_BACKUP_TIMER:-true}"
INSTALL_UPDATE_TIMER="${INSTALL_UPDATE_TIMER:-true}"
INSTALL_ALERT_TIMER="${INSTALL_ALERT_TIMER:-true}"

fail() {
  echo "$1" >&2
  exit 1
}

main() {
  [[ -d "${REPO_DIR}" ]] || fail "Repo directory not found: ${REPO_DIR}"
  [[ -x "${REPO_DIR}/scripts/setup-pi.sh" ]] || fail "setup-pi.sh not found under ${REPO_DIR}/scripts"

  echo "Reinstalling Dragon Pi systemd units from ${REPO_DIR}..."
  INSTALL_SYSTEMD_SERVICE="${INSTALL_SYSTEMD_SERVICE}" \
  INSTALL_BACKUP_TIMER="${INSTALL_BACKUP_TIMER}" \
  INSTALL_UPDATE_TIMER="${INSTALL_UPDATE_TIMER}" \
  INSTALL_ALERT_TIMER="${INSTALL_ALERT_TIMER}" \
  AUTO_START="false" \
  "${REPO_DIR}/scripts/setup-pi.sh"
}

main "$@"
