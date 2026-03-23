#!/usr/bin/env bash
set -euo pipefail

REPO_DIR="${REPO_DIR:-$HOME/dragon/IdeaEngine}"
TIMEOUT_SECONDS="${TIMEOUT_SECONDS:-300}"
INTERVAL_SECONDS="${INTERVAL_SECONDS:-10}"
USE_SYSTEMD="${USE_SYSTEMD:-true}"
SKIP_ALERT_CHECK="${SKIP_ALERT_CHECK:-false}"

usage() {
  cat <<EOF
Usage:
  pi-start-and-wait.sh
  pi-start-and-wait.sh --timeout 600 --interval 15
  pi-start-and-wait.sh --skip-alert-check
  pi-start-and-wait.sh --compose

Defaults:
  starts the Pi service
  waits until the service looks healthy

Options:
  --timeout N         Maximum seconds to wait
  --interval N        Seconds between health checks
  --skip-alert-check  Only require reachable endpoints, not a passing alert check
  --compose           Use docker compose instead of systemd
  --help              Show this help
EOF
}

main() {
  local timeout="${TIMEOUT_SECONDS}"
  local interval="${INTERVAL_SECONDS}"
  local -a wait_args
  local -a start_args

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
      --skip-alert-check)
        SKIP_ALERT_CHECK="true"
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

  wait_args=(--timeout "${timeout}" --interval "${interval}")
  start_args=()

  if [[ "${SKIP_ALERT_CHECK}" == "true" ]]; then
    wait_args+=(--skip-alert-check)
  fi

  if [[ "${USE_SYSTEMD}" != "true" ]]; then
    start_args+=(--compose)
    wait_args+=(--compose)
  fi

  "${REPO_DIR}/scripts/pi-start.sh" "${start_args[@]}"
  "${REPO_DIR}/scripts/pi-wait-until-healthy.sh" --skip-ensure-running "${wait_args[@]}"
}

main "$@"
