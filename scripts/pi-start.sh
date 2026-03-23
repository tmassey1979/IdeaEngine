#!/usr/bin/env bash
set -euo pipefail

REPO_DIR="${REPO_DIR:-$HOME/dragon/IdeaEngine}"
SERVICE_NAME="${SERVICE_NAME:-dragon-idea-engine}"
USE_SYSTEMD="${USE_SYSTEMD:-true}"
RUN_PREFLIGHT="${RUN_PREFLIGHT:-true}"
FOLLOW_LOGS="${FOLLOW_LOGS:-false}"

usage() {
  cat <<EOF
Usage:
  pi-start.sh
  pi-start.sh --follow
  pi-start.sh --skip-preflight
  pi-start.sh --compose

Defaults:
  uses systemd service start
  runs pi-preflight.sh before starting

Options:
  --follow           Follow logs after starting
  --skip-preflight   Skip the preflight check
  --compose          Start with docker compose instead of systemd
  --help             Show this help
EOF
}

main() {
  [[ -d "${REPO_DIR}/scripts" ]] || {
    echo "Repo scripts directory not found at ${REPO_DIR}/scripts" >&2
    exit 1
  }

  while [[ $# -gt 0 ]]; do
    case "$1" in
      --follow)
        FOLLOW_LOGS="true"
        shift
        ;;
      --skip-preflight)
        RUN_PREFLIGHT="false"
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

  if [[ "${RUN_PREFLIGHT}" == "true" ]]; then
    "${REPO_DIR}/scripts/pi-preflight.sh"
  fi

  if [[ "${USE_SYSTEMD}" == "true" ]]; then
    sudo systemctl start "${SERVICE_NAME}.service"
    echo "Started ${SERVICE_NAME}.service"
  else
    (
      cd "${REPO_DIR}"
      docker compose up --build -d
    )
    echo "Started Docker Compose stack from ${REPO_DIR}"
  fi

  if [[ "${FOLLOW_LOGS}" == "true" ]]; then
    if [[ "${USE_SYSTEMD}" == "true" ]]; then
      exec sudo journalctl -u "${SERVICE_NAME}.service" -f
    else
      exec bash -lc "cd '${REPO_DIR}' && docker compose logs -f"
    fi
  fi
}

main "$@"
