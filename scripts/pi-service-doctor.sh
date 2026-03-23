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
  [[ -d "${REPO_DIR}/scripts" ]] || {
    echo "Repo scripts directory not found at ${REPO_DIR}/scripts" >&2
    exit 1
  }

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

issues = []
actions = []

service_active = service.get("active", "unknown")
service_result = service.get("result", "unknown")
restart_count = service.get("restartCount")
worker_health = status.get("health", "unknown")
queued_jobs = status.get("queuedJobs", 0)
failed_issues = rollup.get("failedIssues", 0)
quarantined_issues = rollup.get("quarantinedIssues", 0)
next_wake_reason = status.get("nextWakeReason")
next_delayed_retry_at = status.get("nextDelayedRetryAt")
delayed_retry_urgency = status.get("delayedRetryUrgency")
wait_signal = status.get("waitSignal")

if service_active not in {"active", "inactive"}:
    issues.append(f"service state is {service_active}")
    actions.append("dragon-tail-logs --all")
elif service_active == "inactive" and service_result not in {"success", "unknown"}:
    issues.append(f"service is inactive with result={service_result}")
    actions.append("dragon-tail-logs --all")

if isinstance(restart_count, int) and restart_count > 0:
    issues.append(f"service restart count is {restart_count}")
    actions.append("dragon-tail-logs --all")

if worker_health not in {"healthy", "idle"}:
    issues.append(f"worker health is {worker_health}")

if wait_signal:
    if "provider backoff" in wait_signal.lower():
        delayed_retry_issue = f"worker wait signal: {wait_signal}"
        if delayed_retry_urgency == "alert":
            issues.append(f"{delayed_retry_issue} (long backoff)")
            actions.append("dragon-status-dashboard")
            actions.append("dragon-tail-logs --all")
        else:
            issues.append(delayed_retry_issue)
            actions.append("dragon-report")
    elif "writeback replay" in wait_signal.lower():
        issues.append(f"worker wait signal: {wait_signal}")
        actions.append("dragon-status-dashboard")
        if "overdue" in wait_signal.lower() or "prioritized" in wait_signal.lower():
            actions.append("dragon-tail-logs --all")
elif next_wake_reason == "delayed-provider-retry":
    delayed_retry_issue = "worker is waiting on delayed provider retry"
    if delayed_retry_urgency == "alert":
        issues.append(f"{delayed_retry_issue} (long backoff)")
        actions.append("dragon-status-dashboard")
        actions.append("dragon-tail-logs --all")
    else:
        issues.append(delayed_retry_issue)
        actions.append("dragon-report")
elif next_wake_reason == "poll-interval" and worker_health in {"healthy", "idle"} and service_active == "active":
    actions.append("dragon-report")

if failed_issues > 0:
    issues.append(f"{failed_issues} failed issue(s) need review")
    actions.append("dragon-status-dashboard")

if quarantined_issues > 0:
    issues.append(f"{quarantined_issues} quarantined issue(s) need intervention")
    actions.append("dragon-firstaid")

if queued_jobs > 0 and service_active != "active":
    issues.append(f"{queued_jobs} queued job(s) exist while the service is not active")
    actions.append("sudo systemctl start dragon-idea-engine")

for timer_key in ("backup", "update", "alert"):
    timer = timers.get(timer_key) or {}
    if timer.get("enabled") in {"disabled", "not-found"}:
        issues.append(f"{timer_key} timer is {timer.get('enabled')}")
        actions.append("dragon-reinstall-service")

deduped_actions = []
seen = set()
for action in actions:
    if action not in seen:
        seen.add(action)
        deduped_actions.append(action)

severity = "healthy"
if alert_status == "failing" or issues:
    severity = "attention"
if service_active not in {"active", "inactive"} or service_result not in {"success", "unknown"}:
    severity = "critical"

print("Dragon Pi Service Doctor")
print()
print(f"Status: {severity}")
print(f"Service: {service_active} ({service_result})")
print(f"Worker: {worker_health}")
print(f"Alert check: {alert_status}")
if next_wake_reason:
    print(f"Next wake reason: {next_wake_reason}")
if wait_signal:
    print(f"Wait signal: {wait_signal}")
if next_delayed_retry_at:
    print(f"Next delayed retry: {next_delayed_retry_at}")
print()
print("Findings")
if issues:
    for item in issues:
        print(f"  - {item}")
else:
    print("  - no immediate issues detected")
print()
print("Recommended Next Commands")
if deduped_actions:
    for action in deduped_actions[:6]:
        print(f"  - {action}")
else:
    print("  - dragon-report")
    print("  - dragon-tail-logs --all")
print()
print("Alert Check Output")
if alert_lines:
    for line in alert_lines[:8]:
        print(f"  {line}")
else:
    print("  no output")
PY
}

main "$@"
