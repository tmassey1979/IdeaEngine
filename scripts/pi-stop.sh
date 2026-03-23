#!/usr/bin/env bash
set -euo pipefail

REPO_DIR="${REPO_DIR:-$HOME/dragon/IdeaEngine}"
SERVICE_NAME="${SERVICE_NAME:-dragon-idea-engine}"
USE_SYSTEMD="${USE_SYSTEMD:-true}"
FOLLOW_STATUS="${FOLLOW_STATUS:-false}"

usage() {
  cat <<EOF
Usage:
  pi-stop.sh
  pi-stop.sh --status
  pi-stop.sh --compose

Defaults:
  stops the systemd service

Options:
  --status   Show service status after stopping
  --compose  Stop with docker compose instead of systemd
  --help     Show this help
EOF
}

main() {
  [[ -d "${REPO_DIR}" ]] || {
    echo "Repo directory not found: ${REPO_DIR}" >&2
    exit 1
  }

  while [[ $# -gt 0 ]]; do
    case "$1" in
      --status)
        FOLLOW_STATUS="true"
        shift
        ;;
      --compose)
        USE_SYSTEMD="false"
        shift
        ;;
      --help)
        usage
        exit 0
        ;;
      *)
        echo "Unknown argument: $1" >&2
        usage >&2
        exit 1
        ;;
    esac
  done

  if [[ "${USE_SYSTEMD}" == "true" ]]; then
    sudo systemctl stop "${SERVICE_NAME}.service"
    echo "Stopped ${SERVICE_NAME}.service"
    if [[ "${FOLLOW_STATUS}" == "true" ]]; then
      sudo systemctl status "${SERVICE_NAME}.service" --no-pager
    fi
  else
    (
      cd "${REPO_DIR}"
      docker compose down
    )
    echo "Stopped Docker Compose stack from ${REPO_DIR}"
  fi
}

main "$@"
