#!/usr/bin/env bash
set -euo pipefail

REPO_DIR="${REPO_DIR:-$HOME/dragon/IdeaEngine}"
WATCH_INTERVAL_SECONDS="${WATCH_INTERVAL_SECONDS:-15}"
ITERATIONS="${ITERATIONS:-0}"

usage() {
  cat <<EOF
Usage:
  pi-watch-status.sh
  pi-watch-status.sh --interval 30
  pi-watch-status.sh --iterations 1

Defaults:
  interval: ${WATCH_INTERVAL_SECONDS} seconds
  iterations: 0 (run until interrupted)
EOF
}

main() {
  local interval="${WATCH_INTERVAL_SECONDS}"
  local iterations="${ITERATIONS}"
  local count=0

  [[ -x "${REPO_DIR}/scripts/pi-status-dashboard.sh" ]] || {
    echo "Status dashboard script not found at ${REPO_DIR}/scripts/pi-status-dashboard.sh" >&2
    exit 1
  }

  while [[ $# -gt 0 ]]; do
    case "$1" in
      --interval)
        [[ $# -ge 2 ]] || {
          echo "--interval requires a value" >&2
          exit 1
        }
        interval="$2"
        shift 2
        ;;
      --iterations)
        [[ $# -ge 2 ]] || {
          echo "--iterations requires a value" >&2
          exit 1
        }
        iterations="$2"
        shift 2
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

  [[ "${interval}" =~ ^[0-9]+$ ]] || {
    echo "Interval must be a whole number of seconds" >&2
    exit 1
  }
  [[ "${iterations}" =~ ^[0-9]+$ ]] || {
    echo "Iterations must be a whole number" >&2
    exit 1
  }

  while true; do
    count=$((count + 1))
    if [[ -t 1 ]]; then
      clear
    fi

    echo "Dragon Pi Watch Status"
    echo "refresh=${count} interval=${interval}s time=$(date -Iseconds)"
    echo
    "${REPO_DIR}/scripts/pi-status-dashboard.sh"

    if [[ "${iterations}" -gt 0 && "${count}" -ge "${iterations}" ]]; then
      break
    fi

    sleep "${interval}"
  done
}

main "$@"
