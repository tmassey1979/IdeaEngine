#!/usr/bin/env bash
set -euo pipefail

REPO_DIR="${REPO_DIR:-$HOME/dragon/IdeaEngine}"
RUN_REPORT_FIRST="${RUN_REPORT_FIRST:-true}"
COLLECT_DIAGNOSTICS="${COLLECT_DIAGNOSTICS:-true}"
BACKUP_BEFORE_RESET="${BACKUP_BEFORE_RESET:-true}"
RESET_STATE="${RESET_STATE:-true}"
RESET_DIAGNOSTICS="${RESET_DIAGNOSTICS:-false}"
RESTART_SERVICE="${RESTART_SERVICE:-true}"

run_step() {
  local label="$1"
  shift
  echo
  echo "==> ${label}"
  "$@"
}

main() {
  [[ -d "${REPO_DIR}/scripts" ]] || {
    echo "Repo scripts directory not found at ${REPO_DIR}/scripts" >&2
    exit 1
  }

  if [[ "${RUN_REPORT_FIRST}" == "true" && -x "${REPO_DIR}/scripts/pi-report.sh" ]]; then
    run_step "Pi report" "${REPO_DIR}/scripts/pi-report.sh"
  fi

  if [[ "${COLLECT_DIAGNOSTICS}" == "true" && -x "${REPO_DIR}/scripts/collect-pi-diagnostics.sh" ]]; then
    run_step "Diagnostics bundle" "${REPO_DIR}/scripts/collect-pi-diagnostics.sh"
  fi

  if [[ "${RESET_STATE}" == "true" && -x "${REPO_DIR}/scripts/pi-reset-state.sh" ]]; then
    run_step "State reset" env \
      BACKUP_BEFORE_RESET="${BACKUP_BEFORE_RESET}" \
      RESET_DIAGNOSTICS="${RESET_DIAGNOSTICS}" \
      RESTART_SERVICE="${RESTART_SERVICE}" \
      "${REPO_DIR}/scripts/pi-reset-state.sh"
  fi

  echo
  echo "Pi first aid complete."
}

main "$@"
