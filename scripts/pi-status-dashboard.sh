#!/usr/bin/env bash
set -euo pipefail

REPO_DIR="${REPO_DIR:-$HOME/dragon/IdeaEngine}"
REPORT_FILE=""
ALERT_FILE=""

cleanup() {
  if [[ -n "${REPORT_FILE}" && -f "${REPORT_FILE}" ]]; then
    rm -f "${REPORT_FILE}"
  fi
  if [[ -n "${ALERT_FILE}" && -f "${ALERT_FILE}" ]]; then
    rm -f "${ALERT_FILE}"
  fi
}

main() {
  REPORT_FILE="$(mktemp)"
  ALERT_FILE="$(mktemp)"
  trap cleanup EXIT

  "${REPO_DIR}/scripts/pi-report.sh" --json > "${REPORT_FILE}"
  if "${REPO_DIR}/scripts/pi-alert-check.sh" > "${ALERT_FILE}" 2>&1; then
    ALERT_STATUS="passing"
  else
    ALERT_STATUS="failing"
  fi

  python3 - "${REPORT_FILE}" "${ALERT_FILE}" "${ALERT_STATUS}" <<'PY'
import json
import sys

report_file, alert_file, alert_status = sys.argv[1:4]

with open(report_file, "r", encoding="utf-8") as handle:
    report = json.load(handle)

with open(alert_file, "r", encoding="utf-8") as handle:
    alert_lines = [line.rstrip() for line in handle if line.strip()]

service = report.get("service") or {}
timers = report.get("timers") or {}
status = report.get("status") or {}
rollup = status.get("rollup") or {}
latest = status.get("latestActivity") or {}

print("Dragon Pi Status Dashboard")
print()
print("Health")
print(f"  service: {service.get('active', 'unknown')} ({service.get('result', 'unknown')})")
print(f"  worker: {status.get('health', 'unknown')}")
print(f"  alert_check: {alert_status}")
print(f"  attention: {status.get('attentionSummary', 'none')}")
print()
print("Queue")
print(f"  queued_jobs: {status.get('queuedJobs', 0)}")
print(f"  failed_issues: {rollup.get('failedIssues', 0)}")
print(f"  quarantined_issues: {rollup.get('quarantinedIssues', 0)}")
print(f"  validated_issues: {rollup.get('validatedIssues', 0)}")
print()
print("Timers")
for timer_name in ("backup", "update", "alert"):
    timer = timers.get(timer_name) or {}
    print(f"  {timer_name}: {timer.get('active', 'unknown')} / {timer.get('enabled', 'unknown')} / next={timer.get('next', 'unknown')}")
print()
print("Latest Activity")
if latest:
    print(f"  issue: #{latest.get('issueNumber', '')} {latest.get('issueTitle', '')}")
    print(f"  stage: {latest.get('currentStage', 'unknown')}")
    print(f"  recorded_at: {latest.get('recordedAt', 'unknown')}")
else:
    print("  none recorded")
print()
print("Alert Check Output")
if alert_lines:
    for line in alert_lines[:8]:
        print(f"  {line}")
else:
    print("  no output")
print()
print("Next Commands")
print("  dragon-report")
print("  dragon-firstaid")
print("  dragon-tail-logs --all")
print("  dragon-ops-summary")
PY
}

main "$@"
