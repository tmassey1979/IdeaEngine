#!/usr/bin/env bash
set -euo pipefail

REPO_DIR="${REPO_DIR:-$HOME/dragon/IdeaEngine}"
SERVICE_NAME="${SERVICE_NAME:-dragon-idea-engine}"
STATUS_URL="${STATUS_URL:-http://127.0.0.1:5078/status}"
USE_SYSTEMD="${USE_SYSTEMD:-true}"
RUN_PREFLIGHT="${RUN_PREFLIGHT:-true}"
FOLLOW_LOGS="${FOLLOW_LOGS:-false}"

usage() {
  cat <<EOF
Usage:
  pi-ensure-running.sh
  pi-ensure-running.sh --follow
  pi-ensure-running.sh --skip-preflight
  pi-ensure-running.sh --compose

Defaults:
  verifies the Pi is ready
  leaves the service alone if it already looks healthy

Options:
  --follow           Follow logs after starting or restarting
  --skip-preflight   Skip the preflight check
  --compose          Use docker compose instead of systemd
  --help             Show this help
EOF
}

endpoint_reachable() {
  curl -fsS "${STATUS_URL}" >/dev/null 2>&1
}

service_active() {
  systemctl is-active "${SERVICE_NAME}.service" >/dev/null 2>&1
}

main() {
  local -a delegated_args

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

  delegated_args=()
  if [[ "${RUN_PREFLIGHT}" != "true" ]]; then
    delegated_args+=(--skip-preflight)
  fi
  if [[ "${FOLLOW_LOGS}" == "true" ]]; then
    delegated_args+=(--follow)
  fi

  if [[ "${USE_SYSTEMD}" == "true" ]]; then
    if service_active && endpoint_reachable; then
      echo "${SERVICE_NAME}.service already looks healthy; no action taken."
      if [[ "${FOLLOW_LOGS}" == "true" ]]; then
        exec sudo journalctl -u "${SERVICE_NAME}.service" -f
      fi
      exit 0
    fi

    if service_active; then
      "${REPO_DIR}/scripts/pi-restart.sh" "${delegated_args[@]}"
    else
      "${REPO_DIR}/scripts/pi-start.sh" "${delegated_args[@]}"
    fi
    exit 0
  fi

  if endpoint_reachable; then
    echo "Docker Compose stack already appears healthy; no action taken."
    if [[ "${FOLLOW_LOGS}" == "true" ]]; then
      exec bash -lc "cd '${REPO_DIR}' && docker compose logs -f"
    fi
    exit 0
  fi

  "${REPO_DIR}/scripts/pi-restart.sh" --compose "${delegated_args[@]}"
}

main "$@"
