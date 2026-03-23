#!/usr/bin/env bash
set -euo pipefail

SERVICE_NAME="${SERVICE_NAME:-dragon-idea-engine}"
BACKUP_SERVICE_NAME="${BACKUP_SERVICE_NAME:-dragon-backup}"
UPDATE_SERVICE_NAME="${UPDATE_SERVICE_NAME:-dragon-update}"
ALERT_SERVICE_NAME="${ALERT_SERVICE_NAME:-dragon-alert-check}"
TAIL_ALL="${TAIL_ALL:-false}"

UNITS=(
  "${SERVICE_NAME}.service"
)

usage() {
  cat <<EOF
Usage:
  pi-tail-logs.sh
  pi-tail-logs.sh --all
  pi-tail-logs.sh --unit dragon-update.service
  pi-tail-logs.sh --no-follow -n 100

Defaults:
  Follows ${SERVICE_NAME}.service

Options:
  --all         Follow ${SERVICE_NAME}.service plus backup/update/alert services
  --unit NAME   Add a specific unit to the journalctl call
  --help        Show this help

Any other arguments are passed through to journalctl.
EOF
}

main() {
  local -a journal_args
  journal_args=(--follow)

  while [[ $# -gt 0 ]]; do
    case "$1" in
      --all)
        TAIL_ALL="true"
        shift
        ;;
      --unit)
        [[ $# -ge 2 ]] || {
          echo "--unit requires a value" >&2
          exit 1
        }
        UNITS+=("$2")
        shift 2
        ;;
      --help)
        usage
        exit 0
        ;;
      *)
        journal_args+=("$1")
        shift
        ;;
    esac
  done

  if [[ "${TAIL_ALL}" == "true" ]]; then
    UNITS+=(
      "${BACKUP_SERVICE_NAME}.service"
      "${UPDATE_SERVICE_NAME}.service"
      "${ALERT_SERVICE_NAME}.service"
    )
  fi

  sudo journalctl "${journal_args[@]}" "${UNITS[@]/#/-u }"
}

main "$@"
