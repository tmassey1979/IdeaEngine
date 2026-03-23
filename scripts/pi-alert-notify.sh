#!/usr/bin/env bash
set -euo pipefail

REPO_DIR="${REPO_DIR:-$HOME/dragon/IdeaEngine}"
ALERT_WEBHOOK_URL="${ALERT_WEBHOOK_URL:-}"
ALERT_SOURCE="${ALERT_SOURCE:-dragon-alert-check}"
ALERT_MESSAGE="${ALERT_MESSAGE:-Dragon Pi alert triggered.}"

require_command() {
  local command_name="$1"
  command -v "${command_name}" >/dev/null 2>&1 || {
    echo "Missing required command: ${command_name}" >&2
    exit 2
  }
}

main() {
  if [[ -z "${ALERT_WEBHOOK_URL}" ]]; then
    echo "ALERT_WEBHOOK_URL is not configured; skipping notification."
    exit 0
  fi

  require_command curl
  require_command python3

  local report_file payload
  report_file="$(mktemp)"
  trap 'rm -f "${report_file}"' EXIT
  "${REPO_DIR}/scripts/pi-report.sh" --json > "${report_file}"

  payload="$(python3 - "${report_file}" "${ALERT_SOURCE}" "${ALERT_MESSAGE}" <<'PY'
import json
import sys

report_file, alert_source, alert_message = sys.argv[1:4]

with open(report_file, "r", encoding="utf-8") as handle:
    report = json.load(handle)

status = report.get("status") or {}
latest = status.get("latestActivity") or {}

payload = {
    "source": alert_source,
    "message": alert_message,
    "timestamp": report.get("timestamp"),
    "service": report.get("service"),
    "timers": report.get("timers"),
    "endpoints": report.get("endpoints"),
    "workerHealth": status.get("health"),
    "attentionSummary": status.get("attentionSummary"),
    "latestActivity": {
        "issueNumber": latest.get("issueNumber"),
        "issueTitle": latest.get("issueTitle"),
        "stage": latest.get("currentStage"),
        "recordedAt": latest.get("recordedAt"),
        "summary": latest.get("summary"),
    },
}

print(json.dumps(payload))
PY
)"

  curl -fsS -X POST \
    -H "Content-Type: application/json" \
    -d "${payload}" \
    "${ALERT_WEBHOOK_URL}" >/dev/null

  echo "Alert notification sent to configured webhook."
}

main "$@"
