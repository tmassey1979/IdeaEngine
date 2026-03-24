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
pending_github_sync_next_retry = status.get("pendingGithubSyncNextRetryAt") or ""
pending_github_sync_last_attempt = ""
pending_github_sync_retry_state = status.get("pendingGithubSyncRetryState") or ""
pending_github_sync_retry_overdue_minutes = int(status.get("pendingGithubSyncRetryOverdueMinutes") or 0)
replay_priority_reason = status.get("replayPriorityReason")
replay_priority_summary = status.get("replayPrioritySummary")
provider_backoff_issue_count = int(status.get("providerBackoffIssueCount") or 0)
overdue_writeback_issue_count = int(status.get("overdueWritebackIssueCount") or 0)
wait_signal = status.get("waitSignal")
if pending_github_sync:
    pending_github_sync_last_attempt = pending_github_sync[0].get("lastAttemptedAt", "")
if not wait_signal and replay_priority_summary:
    wait_signal = replay_priority_summary
elif not wait_signal and (replay_priority_reason == "overdue-github-writeback-retry" or pending_github_sync_retry_overdue_minutes >= 15):
    wait_signal = "prioritizing overdue writeback replay"
elif not wait_signal and (replay_priority_reason == "ready-github-writeback-retry" or pending_github_sync_retry_state == "ready now"):
    wait_signal = "writeback replay ready"
elif not wait_signal and (replay_priority_reason == "provider-backoff" or next_wake_reason == "waiting for delayed provider retry"):
    wait_signal = "provider backoff"
    if delayed_retry_urgency == "alert":
        wait_signal = "provider backoff (long)"
elif not wait_signal and next_wake_reason == "scheduled poll interval":
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
if replay_priority_reason:
    print(f"  replay_priority_reason: {replay_priority_reason}")
if replay_priority_summary:
    print(f"  replay_priority_summary: {replay_priority_summary}")
if provider_backoff_issue_count > 0:
    print(f"  provider_backoff_issue_count: {provider_backoff_issue_count}")
if overdue_writeback_issue_count > 0:
    print(f"  overdue_writeback_issue_count: {overdue_writeback_issue_count}")
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
