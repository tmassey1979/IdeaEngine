#!/usr/bin/env bash
set -euo pipefail

REPO_DIR="${REPO_DIR:-$HOME/dragon/IdeaEngine}"
RUN_SHARE_STATUS="${RUN_SHARE_STATUS:-true}"
RUN_BACKUP="${RUN_BACKUP:-true}"
RUN_CLEANUP="${RUN_CLEANUP:-true}"
SHOW_REPORT_AT_END="${SHOW_REPORT_AT_END:-true}"

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

  if [[ "${RUN_SHARE_STATUS}" == "true" ]]; then
    run_step "Capture share-status bundle" env \
      REPO_DIR="${REPO_DIR}" \
      "${REPO_DIR}/scripts/pi-share-status.sh"
  fi

  if [[ "${RUN_BACKUP}" == "true" ]]; then
    run_step "Create backup" env \
      REPO_DIR="${REPO_DIR}" \
      "${REPO_DIR}/scripts/backup-pi.sh"
  fi

  if [[ "${RUN_CLEANUP}" == "true" ]]; then
    run_step "Cleanup temp artifacts" env \
      REPO_DIR="${REPO_DIR}" \
      "${REPO_DIR}/scripts/cleanup-pi.sh"
  fi

  if [[ "${SHOW_REPORT_AT_END}" == "true" ]]; then
    run_step "Final report" env \
      REPO_DIR="${REPO_DIR}" \
      "${REPO_DIR}/scripts/pi-report.sh"
  fi
}

main "$@"
