#!/usr/bin/env bash
set -euo pipefail

REPO_DIR="${REPO_DIR:-$HOME/dragon/IdeaEngine}"
TIMEOUT_SECONDS="${TIMEOUT_SECONDS:-180}"
INTERVAL_SECONDS="${INTERVAL_SECONDS:-5}"
USE_SYSTEMD="${USE_SYSTEMD:-true}"

usage() {
  cat <<EOF
Usage:
  pi-stop-and-wait.sh
  pi-stop-and-wait.sh --timeout 120 --interval 5
  pi-stop-and-wait.sh --compose

Defaults:
  stops the Pi service
  waits until the service looks stopped

Options:
  --timeout N   Maximum seconds to wait
  --interval N  Seconds between stop checks
  --compose     Use docker compose instead of systemd
  --help        Show this help
EOF
}

main() {
  local timeout="${TIMEOUT_SECONDS}"
  local interval="${INTERVAL_SECONDS}"
  local -a wait_args
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

  wait_args=(--timeout "${timeout}" --interval "${interval}")
  stop_args=()

  if [[ "${USE_SYSTEMD}" != "true" ]]; then
    stop_args+=(--compose)
    wait_args+=(--compose)
  fi

  "${REPO_DIR}/scripts/pi-stop.sh" "${stop_args[@]}"
  "${REPO_DIR}/scripts/pi-wait-until-stopped.sh" --skip-stop "${wait_args[@]}"
}

main "$@"
