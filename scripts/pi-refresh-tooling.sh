#!/usr/bin/env bash
set -euo pipefail

REPO_DIR="${REPO_DIR:-$HOME/dragon/IdeaEngine}"
BIN_DIR="${BIN_DIR:-$HOME/.local/bin}"

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

  run_step "Install shortcut commands" env \
    REPO_DIR="${REPO_DIR}" \
    BIN_DIR="${BIN_DIR}" \
    "${REPO_DIR}/scripts/install-pi-aliases.sh"

  run_step "Run Pi self-test" env \
    REPO_DIR="${REPO_DIR}" \
    BIN_DIR="${BIN_DIR}" \
    "${REPO_DIR}/scripts/pi-self-test.sh"
}

main "$@"
