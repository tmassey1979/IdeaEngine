#!/usr/bin/env bash
set -euo pipefail

REPO_DIR="${REPO_DIR:-$HOME/dragon/IdeaEngine}"
SERVICE_NAME="${SERVICE_NAME:-dragon-idea-engine}"
STATUS_URL="${STATUS_URL:-http://127.0.0.1:5078/status}"
TIMEOUT_SECONDS="${TIMEOUT_SECONDS:-180}"
INTERVAL_SECONDS="${INTERVAL_SECONDS:-5}"
STOP_FIRST="${STOP_FIRST:-true}"
USE_SYSTEMD="${USE_SYSTEMD:-true}"

usage() {
  cat <<EOF
Usage:
  pi-wait-until-stopped.sh
  pi-wait-until-stopped.sh --timeout 120 --interval 5
  pi-wait-until-stopped.sh --skip-stop
  pi-wait-until-stopped.sh --compose

Defaults:
  timeout: ${TIMEOUT_SECONDS} seconds
  interval: ${INTERVAL_SECONDS} seconds
  stops the service before polling

Options:
  --timeout N    Maximum seconds to wait
  --interval N   Seconds between checks
  --skip-stop    Do not call pi-stop.sh first
  --compose      Use compose mode when stopping
  --help         Show this help
EOF
}

service_inactive() {
  if [[ "${USE_SYSTEMD}" != "true" ]]; then
    return 0
  fi

  local state
  state="$(systemctl is-active "${SERVICE_NAME}.service" 2>/dev/null || true)"
  [[ "${state}" == "inactive" || "${state}" == "failed" || "${state}" == "unknown" ]]
}

status_endpoint_down() {
  ! curl -fsS "${STATUS_URL}" >/dev/null 2>&1
}

main() {
  local timeout="${TIMEOUT_SECONDS}"
  local interval="${INTERVAL_SECONDS}"
  local start_time now elapsed
  local -a stop_args

  [[ -d "${REPO_DIR}/scripts" ]] || {
    echo "Repo scripts directory not found at ${REPO_DIR}/scripts" >&2
    exit 1
  }

  while [[ $# -gt 0 ]]; do
    case "$1" in
      --timeout)
        [[ $# -ge 2 ]] || {
          echo "--timeout requires a value" >&2
          exit 1
        }
        timeout="$2"
        shift 2
        ;;
      --interval)
        [[ $# -ge 2 ]] || {
          echo "--interval requires a value" >&2
          exit 1
        }
        interval="$2"
        shift 2
        ;;
      --skip-stop)
        STOP_FIRST="false"
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

  [[ "${timeout}" =~ ^[0-9]+$ ]] || {
    echo "Timeout must be a whole number of seconds" >&2
    exit 1
  }
  [[ "${interval}" =~ ^[0-9]+$ ]] || {
    echo "Interval must be a whole number of seconds" >&2
    exit 1
  }

  stop_args=()
  if [[ "${USE_SYSTEMD}" != "true" ]]; then
    stop_args+=(--compose)
  fi

  if [[ "${STOP_FIRST}" == "true" ]]; then
    "${REPO_DIR}/scripts/pi-stop.sh" "${stop_args[@]}"
  fi

  start_time="$(date +%s)"
  while true; do
    if service_inactive && status_endpoint_down; then
      echo "Pi is stopped."
      exit 0
    fi

    now="$(date +%s)"
    elapsed=$((now - start_time))
    if [[ "${elapsed}" -ge "${timeout}" ]]; then
      echo "Timed out waiting for the Pi to stop after ${elapsed} seconds." >&2
      echo "Try: dragon-report or dragon-tail-logs --all" >&2
      exit 1
    fi

    echo "Waiting for Pi shutdown... ${elapsed}s elapsed"
    sleep "${interval}"
  done
}

main "$@"
