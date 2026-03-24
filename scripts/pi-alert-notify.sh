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

def describe_wait_signal(status: dict) -> str | None:
    wait_signal = status.get("waitSignal")
    if wait_signal:
        return wait_signal
    replay_priority_summary = status.get("replayPrioritySummary")
    if replay_priority_summary:
        return replay_priority_summary
    replay_priority_reason = status.get("replayPriorityReason")
    pending_github_retry_overdue_minutes = int(status.get("pendingGithubSyncRetryOverdueMinutes") or 0)
    pending_github_retry_state = status.get("pendingGithubSyncRetryState")
    next_wake_reason = status.get("nextWakeReason")
    delayed_retry_urgency = status.get("delayedRetryUrgency")
    if replay_priority_reason == "overdue-github-writeback-retry" or pending_github_retry_overdue_minutes >= 15:
        return "prioritizing overdue writeback replay"
    if replay_priority_reason == "ready-github-writeback-retry" or pending_github_retry_state == "ready now":
        return "writeback replay ready"
    if replay_priority_reason == "provider-backoff" or next_wake_reason == "delayed-provider-retry":
        return "provider backoff (long)" if delayed_retry_urgency == "alert" else "provider backoff"
    if next_wake_reason == "poll-interval":
        return "routine poll wait"
    return None

with open(report_file, "r", encoding="utf-8") as handle:
    report = json.load(handle)

status = report.get("status") or {}
latest = status.get("latestActivity") or {}
wait_signal = describe_wait_signal(status)
pending_github_sync = status.get("pendingGithubSync") or []
pending_github_sync_next_retry = status.get("pendingGithubSyncNextRetryAt")
pending_github_sync_last_attempt = pending_github_sync[0].get("lastAttemptedAt") if pending_github_sync else None
pending_github_sync_retry_state = status.get("pendingGithubSyncRetryState")
pending_github_sync_retry_overdue_minutes = int(status.get("pendingGithubSyncRetryOverdueMinutes") or 0)
provider_backoff_issue_count = int(status.get("providerBackoffIssueCount") or 0)
overdue_writeback_issue_count = int(status.get("overdueWritebackIssueCount") or 0)
alert_cause = status.get("replayPriorityReason")
if not alert_cause:
    if pending_github_sync_retry_overdue_minutes >= 15:
        alert_cause = "overdue-github-writeback-retry"
    elif pending_github_sync_retry_state == "ready now":
        alert_cause = "ready-github-writeback-retry"
    elif status.get("nextWakeReason") == "delayed-provider-retry":
        alert_cause = "provider-backoff"

payload = {
    "source": alert_source,
    "message": alert_message,
    "timestamp": report.get("timestamp"),
    "alertCause": alert_cause,
    "replayPrioritySummary": status.get("replayPrioritySummary"),
    "service": report.get("service"),
    "timers": report.get("timers"),
    "endpoints": report.get("endpoints"),
    "workerHealth": status.get("health"),
    "attentionSummary": status.get("attentionSummary"),
    "waitSignalBackend": status.get("waitSignal"),
    "nextWakeReason": status.get("nextWakeReason"),
    "waitSignal": wait_signal,
    "nextDelayedRetryAt": status.get("nextDelayedRetryAt"),
    "pendingGithubSyncNextRetryAt": pending_github_sync_next_retry,
    "pendingGithubSyncLastAttemptAt": pending_github_sync_last_attempt,
    "pendingGithubSyncRetryState": pending_github_sync_retry_state,
    "pendingGithubSyncRetryOverdueMinutes": pending_github_sync_retry_overdue_minutes,
    "providerBackoffIssueCount": provider_backoff_issue_count,
    "overdueWritebackIssueCount": overdue_writeback_issue_count,
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
