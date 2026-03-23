#!/usr/bin/env bash
set -euo pipefail

REPO_DIR="${REPO_DIR:-$HOME/dragon/IdeaEngine}"
TIMEOUT_SECONDS="${TIMEOUT_SECONDS:-300}"
INTERVAL_SECONDS="${INTERVAL_SECONDS:-10}"
ENSURE_RUNNING_FIRST="${ENSURE_RUNNING_FIRST:-true}"
SKIP_ALERT_CHECK="${SKIP_ALERT_CHECK:-false}"
USE_SYSTEMD="${USE_SYSTEMD:-true}"

usage() {
  cat <<EOF
Usage:
  pi-wait-until-healthy.sh
  pi-wait-until-healthy.sh --timeout 600 --interval 15
  pi-wait-until-healthy.sh --skip-ensure-running
  pi-wait-until-healthy.sh --skip-alert-check
  pi-wait-until-healthy.sh --compose

Defaults:
  timeout: ${TIMEOUT_SECONDS} seconds
  interval: ${INTERVAL_SECONDS} seconds
  ensures the service is running before polling

Options:
  --timeout N             Maximum seconds to wait
  --interval N            Seconds between checks
  --skip-ensure-running   Do not call pi-ensure-running.sh first
  --skip-alert-check      Only require reachable endpoints, not a passing alert check
  --compose               Use compose mode when ensuring the service is running
  --help                  Show this help
EOF
}

endpoint_health_ok() {
  local report_file
  report_file="$(mktemp)"

  if ! "${REPO_DIR}/scripts/pi-report.sh" --json > "${report_file}" 2>/dev/null; then
    rm -f "${report_file}"
    return 1
  fi

  python3 - "${report_file}" <<'PY'
import json
import sys

with open(sys.argv[1], "r", encoding="utf-8") as handle:
    report = json.load(handle)

endpoints = report.get("endpoints") or {}
service = report.get("service") or {}

ok = (
    endpoints.get("health") == "reachable"
    and endpoints.get("status") == "reachable"
    and service.get("active") in {"active", "inactive"}
)

raise SystemExit(0 if ok else 1)
PY
  local rc=$?
  rm -f "${report_file}"
  return "${rc}"
}

main() {
  local timeout="${TIMEOUT_SECONDS}"
  local interval="${INTERVAL_SECONDS}"
  local start_time now elapsed
  local -a ensure_args

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
      --skip-ensure-running)
        ENSURE_RUNNING_FIRST="false"
        shift
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

  [[ "${timeout}" =~ ^[0-9]+$ ]] || {
    echo "Timeout must be a whole number of seconds" >&2
    exit 1
  }
  [[ "${interval}" =~ ^[0-9]+$ ]] || {
    echo "Interval must be a whole number of seconds" >&2
    exit 1
  }

  ensure_args=()
  if [[ "${USE_SYSTEMD}" != "true" ]]; then
    ensure_args+=(--compose)
  fi

  if [[ "${ENSURE_RUNNING_FIRST}" == "true" ]]; then
    "${REPO_DIR}/scripts/pi-ensure-running.sh" "${ensure_args[@]}"
  fi

  start_time="$(date +%s)"
  while true; do
    if endpoint_health_ok; then
      if [[ "${SKIP_ALERT_CHECK}" == "true" ]]; then
        echo "Pi endpoints are reachable."
        exit 0
      fi

      if "${REPO_DIR}/scripts/pi-alert-check.sh" >/dev/null 2>&1; then
        echo "Pi is healthy."
        exit 0
      fi
    fi

    now="$(date +%s)"
    elapsed=$((now - start_time))
    if [[ "${elapsed}" -ge "${timeout}" ]]; then
      echo "Timed out waiting for the Pi to become healthy after ${elapsed} seconds." >&2
      echo "Try: dragon-report, dragon-status-dashboard, or dragon-tail-logs --all" >&2
      exit 1
    fi

    echo "Waiting for Pi health... ${elapsed}s elapsed"
    sleep "${interval}"
  done
}

main "$@"
