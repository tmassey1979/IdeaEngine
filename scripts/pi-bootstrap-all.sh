#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_DIR="${REPO_DIR:-$HOME/dragon/IdeaEngine}"
AUTO_START_STACK="${AUTO_START_STACK:-true}"
RUN_HEALTHCHECK_AT_END="${RUN_HEALTHCHECK_AT_END:-true}"

run_step() {
  local label="$1"
  shift
  echo
  echo "==> ${label}"
  "$@"
}

main() {
  run_step "Host setup" "${SCRIPT_DIR}/setup-pi.sh"

  if [[ -d "${REPO_DIR}/scripts" ]]; then
    run_step "Environment configuration" "${REPO_DIR}/scripts/configure-pi-env.sh"
  else
    run_step "Environment configuration" "${SCRIPT_DIR}/configure-pi-env.sh"
  fi

  if [[ "${AUTO_START_STACK}" == "true" ]]; then
    if [[ -x "${REPO_DIR}/scripts/pi-start-and-wait.sh" ]]; then
      run_step "Start and wait" "${REPO_DIR}/scripts/pi-start-and-wait.sh"
    else
      echo
      echo "==> Starting service"
      sudo systemctl start dragon-idea-engine
    fi
  fi

  if [[ "${RUN_HEALTHCHECK_AT_END}" == "true" ]]; then
    if [[ -x "${REPO_DIR}/scripts/healthcheck-pi.sh" ]]; then
      run_step "Health check" "${REPO_DIR}/scripts/healthcheck-pi.sh"
    else
      run_step "Health check" "${SCRIPT_DIR}/healthcheck-pi.sh"
    fi
  fi

  echo
  echo "Pi bootstrap workflow complete."
  echo "Follow logs with: sudo journalctl -u dragon-idea-engine -f"
}

main "$@"
