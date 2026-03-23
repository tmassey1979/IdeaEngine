#!/usr/bin/env bash
set -euo pipefail

REPO_DIR="${REPO_DIR:-$HOME/dragon/IdeaEngine}"
BIN_DIR="${BIN_DIR:-$HOME/.local/bin}"
RUN_TOOLING_REFRESH="${RUN_TOOLING_REFRESH:-true}"
RUN_PREFLIGHT="${RUN_PREFLIGHT:-true}"
SHOW_DASHBOARD="${SHOW_DASHBOARD:-true}"

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

  if [[ "${RUN_TOOLING_REFRESH}" == "true" ]]; then
    run_step "Refresh tooling" env \
      REPO_DIR="${REPO_DIR}" \
      BIN_DIR="${BIN_DIR}" \
      "${REPO_DIR}/scripts/pi-refresh-tooling.sh"
  fi

  if [[ "${RUN_PREFLIGHT}" == "true" ]]; then
    run_step "Preflight" env \
      REPO_DIR="${REPO_DIR}" \
      "${REPO_DIR}/scripts/pi-preflight.sh"
  fi

  if [[ "${SHOW_DASHBOARD}" == "true" ]]; then
    run_step "Status dashboard" env \
      REPO_DIR="${REPO_DIR}" \
      "${REPO_DIR}/scripts/pi-status-dashboard.sh"
  fi
}

main "$@"
