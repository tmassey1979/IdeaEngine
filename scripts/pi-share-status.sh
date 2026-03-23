#!/usr/bin/env bash
set -euo pipefail

REPO_DIR="${REPO_DIR:-$HOME/dragon/IdeaEngine}"
OUTPUT_ROOT="${OUTPUT_ROOT:-$REPO_DIR/.tmp/pi-share-status}"
TIMESTAMP="${TIMESTAMP:-$(date +%Y%m%d-%H%M%S)}"
OUTPUT_DIR="${OUTPUT_DIR:-$OUTPUT_ROOT/$TIMESTAMP}"
SERVICE_NAME="${SERVICE_NAME:-dragon-idea-engine}"

write_command_output() {
  local filename="$1"
  shift
  {
    "$@"
  } > "${OUTPUT_DIR}/${filename}" 2>&1 || true
}

main() {
  [[ -d "${REPO_DIR}/scripts" ]] || {
    echo "Repo scripts directory not found at ${REPO_DIR}/scripts" >&2
    exit 1
  }

  mkdir -p "${OUTPUT_DIR}"

  cat > "${OUTPUT_DIR}/README.txt" <<EOF
Dragon Pi share-status bundle
timestamp=${TIMESTAMP}
repo_dir=${REPO_DIR}

Contents:
- report.json: machine-readable pi-report snapshot
- dashboard.txt: human-readable dashboard summary
- doctor.txt: guided next-step summary
- alert-check.txt: alert check output
- git-status.txt: repo git status
- service-status.txt: systemd status for ${SERVICE_NAME}.service
- recent-journal.txt: recent journal lines for ${SERVICE_NAME}.service
EOF

  write_command_output "report.json" "${REPO_DIR}/scripts/pi-report.sh" --json
  write_command_output "dashboard.txt" "${REPO_DIR}/scripts/pi-status-dashboard.sh"
  write_command_output "doctor.txt" "${REPO_DIR}/scripts/pi-service-doctor.sh"
  write_command_output "alert-check.txt" "${REPO_DIR}/scripts/pi-alert-check.sh"
  write_command_output "git-status.txt" git -C "${REPO_DIR}" status --short
  write_command_output "service-status.txt" systemctl --no-pager --full status "${SERVICE_NAME}.service"
  write_command_output "recent-journal.txt" journalctl -u "${SERVICE_NAME}.service" -n 200 --no-pager

  echo "Saved share-status bundle to ${OUTPUT_DIR}"
}

main "$@"
