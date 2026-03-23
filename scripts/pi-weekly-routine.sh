#!/usr/bin/env bash
set -euo pipefail

REPO_DIR="${REPO_DIR:-$HOME/dragon/IdeaEngine}"
BIN_DIR="${BIN_DIR:-$HOME/.local/bin}"
RUN_TOOLING_REFRESH_FIRST="${RUN_TOOLING_REFRESH_FIRST:-true}"
RUN_UPDATE="${RUN_UPDATE:-true}"
RUN_DAILY_ROUTINE_AT_END="${RUN_DAILY_ROUTINE_AT_END:-true}"

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

  if [[ "${RUN_TOOLING_REFRESH_FIRST}" == "true" ]]; then
    run_step "Refresh tooling" env \
      REPO_DIR="${REPO_DIR}" \
      BIN_DIR="${BIN_DIR}" \
      "${REPO_DIR}/scripts/pi-refresh-tooling.sh"
  fi

  if [[ "${RUN_UPDATE}" == "true" ]]; then
    run_step "Update Pi stack" env \
      REPO_DIR="${REPO_DIR}" \
      "${REPO_DIR}/scripts/update-pi.sh"
  fi

  if [[ "${RUN_DAILY_ROUTINE_AT_END}" == "true" ]]; then
    run_step "Daily routine snapshot" env \
      REPO_DIR="${REPO_DIR}" \
      BIN_DIR="${BIN_DIR}" \
      RUN_TOOLING_REFRESH=false \
      "${REPO_DIR}/scripts/pi-daily-routine.sh"
  fi
}

main "$@"
