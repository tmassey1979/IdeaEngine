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

def describe_wake_reason(value: str | None) -> str | None:
    if value == "delayed-provider-retry":
        return "waiting for delayed provider retry"
    if value == "poll-interval":
        return "scheduled poll interval"
    return value

with open(report_file, "r", encoding="utf-8") as handle:
    report = json.load(handle)

with open(alert_file, "r", encoding="utf-8") as handle:
    alert_lines = [line.rstrip() for line in handle if line.strip()]

service = report.get("service") or {}
timers = report.get("timers") or {}
status = report.get("status") or {}
rollup = status.get("rollup") or {}
latest = status.get("latestActivity") or {}
next_wake_reason = describe_wake_reason(status.get("nextWakeReason"))
delayed_retry_urgency = status.get("delayedRetryUrgency")
pending_github_sync = status.get("pendingGithubSync") or []
pending_github_sync_next_retry = ""
pending_github_sync_last_attempt = ""
pending_github_sync_retry_state = ""
if pending_github_sync:
    pending_github_sync_next_retry = pending_github_sync[0].get("nextRetryAt", "")
    pending_github_sync_last_attempt = pending_github_sync[0].get("lastAttemptedAt", "")
    if pending_github_sync_next_retry:
        try:
            from datetime import datetime, timezone

            retry_at = datetime.fromisoformat(pending_github_sync_next_retry.replace("Z", "+00:00"))
            generated_at = status.get("generatedAt") or ""
            generated = datetime.fromisoformat(generated_at.replace("Z", "+00:00")) if generated_at else datetime.now(timezone.utc)
            remaining = int((retry_at - generated).total_seconds())
            if remaining <= 0:
                pending_github_sync_retry_state = "ready now"
            else:
                hours, remainder = divmod(remaining, 3600)
                minutes, seconds = divmod(remainder, 60)
                parts = []
                if hours:
                    parts.append(f"{hours}h")
                if minutes:
                    parts.append(f"{minutes}m")
                if seconds or not parts:
                    parts.append(f"{seconds}s")
                pending_github_sync_retry_state = f"next retry in {' '.join(parts)}"
        except Exception:
            pending_github_sync_retry_state = "scheduled"
wait_signal = None
if next_wake_reason == "waiting for delayed provider retry":
    wait_signal = "provider backoff"
    if delayed_retry_urgency == "alert":
        wait_signal = "provider backoff (long)"
elif next_wake_reason == "scheduled poll interval":
    wait_signal = "routine poll wait"

print("Dragon Pi Status Dashboard")
print()
print("Health")
print(f"  service: {service.get('active', 'unknown')} ({service.get('result', 'unknown')})")
print(f"  worker: {status.get('health', 'unknown')}")
print(f"  alert_check: {alert_status}")
print(f"  attention: {status.get('attentionSummary', 'none')}")
if wait_signal:
    print(f"  wait_signal: {wait_signal}")
if next_wake_reason:
    print(f"  next_wake_reason: {next_wake_reason}")
if status.get("nextDelayedRetryAt"):
    print(f"  next_delayed_retry: {status.get('nextDelayedRetryAt')}")
if status.get("delayedRetryUrgency"):
    print(f"  delayed_retry_urgency: {status.get('delayedRetryUrgency')}")
if status.get("delayedRetrySummary"):
    print(f"  delayed_retry: {status.get('delayedRetrySummary')}")
if pending_github_sync_next_retry:
    print(f"  pending_github_sync_next_retry: {pending_github_sync_next_retry}")
if pending_github_sync_retry_state:
    print(f"  pending_github_sync_retry_state: {pending_github_sync_retry_state}")
if pending_github_sync_last_attempt:
    print(f"  pending_github_sync_last_attempt: {pending_github_sync_last_attempt}")
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
