#!/usr/bin/env bash
set -euo pipefail

REPO_DIR="${REPO_DIR:-$HOME/dragon/IdeaEngine}"
SERVICE_NAME="${SERVICE_NAME:-dragon-idea-engine}"
MAX_SERVICE_RESTARTS="${MAX_SERVICE_RESTARTS:-5}"
MAX_FAILED_ISSUES="${MAX_FAILED_ISSUES:-0}"
MAX_ACTIONABLE_QUARANTINED="${MAX_ACTIONABLE_QUARANTINED:-0}"
MAX_DELAYED_RETRY_MINUTES="${MAX_DELAYED_RETRY_MINUTES:-0}"
MAX_PENDING_GITHUB_RETRY_OVERDUE_MINUTES="${MAX_PENDING_GITHUB_RETRY_OVERDUE_MINUTES:-0}"
ALLOW_HEALTH_STATES="${ALLOW_HEALTH_STATES:-healthy,idle}"
REPORT_FILE=""

require_command() {
  local command_name="$1"
  command -v "${command_name}" >/dev/null 2>&1 || {
    echo "Missing required command: ${command_name}" >&2
    exit 2
  }
}

cleanup() {
  if [[ -n "${REPORT_FILE}" && -f "${REPORT_FILE}" ]]; then
    rm -f "${REPORT_FILE}"
  fi
}

main() {
  require_command python3

  REPORT_FILE="$(mktemp)"
  trap cleanup EXIT
  "${REPO_DIR}/scripts/pi-report.sh" --json > "${REPORT_FILE}"

  python3 - "${REPORT_FILE}" "${MAX_SERVICE_RESTARTS}" "${MAX_FAILED_ISSUES}" "${MAX_ACTIONABLE_QUARANTINED}" "${MAX_DELAYED_RETRY_MINUTES}" "${MAX_PENDING_GITHUB_RETRY_OVERDUE_MINUTES}" "${ALLOW_HEALTH_STATES}" <<'PY'
import json
import sys
from datetime import datetime, timezone

report_file = sys.argv[1]
max_service_restarts = int(sys.argv[2])
max_failed_issues = int(sys.argv[3])
max_actionable_quarantined = int(sys.argv[4])
max_delayed_retry_minutes = int(sys.argv[5])
max_pending_github_retry_overdue_minutes = int(sys.argv[6])
allow_health_states = {item.strip() for item in sys.argv[7].split(",") if item.strip()}

with open(report_file, "r", encoding="utf-8") as handle:
    report = json.load(handle)
problems = []

service = report.get("service") or {}
endpoints = report.get("endpoints") or {}
status = report.get("status") or {}
rollup = status.get("rollup") or {}

service_active = service.get("active", "unknown")
service_result = service.get("result", "unknown")
service_restart_count_raw = service.get("restartCount", "0")

try:
    service_restart_count = int(service_restart_count_raw)
except (TypeError, ValueError):
    service_restart_count = 0

if service_active not in {"active", "inactive"}:
    problems.append(f"service state is {service_active}")

if service_result not in {"success", "unknown"}:
    problems.append(f"service result is {service_result}")

if service_restart_count > max_service_restarts:
    problems.append(f"service restart count {service_restart_count} exceeds {max_service_restarts}")

if endpoints.get("health") != "reachable":
    problems.append("health endpoint unreachable")

if endpoints.get("status") != "reachable":
    problems.append("status endpoint unreachable")

worker_health = status.get("health", "unknown")
if allow_health_states and worker_health not in allow_health_states:
    problems.append(f"worker health is {worker_health}")

failed_issues = int(rollup.get("failedIssues", 0) or 0)
if failed_issues > max_failed_issues:
    problems.append(f"failed issues {failed_issues} exceeds {max_failed_issues}")

issues = status.get("issues") or []
actionable_quarantined = sum(
    1 for issue in issues
    if str(issue.get("overallStatus", "")).lower() == "quarantined" and int(issue.get("queuedJobCount") or 0) > 0
)
if actionable_quarantined > max_actionable_quarantined:
    problems.append(
        f"actionable quarantined issues {actionable_quarantined} exceeds {max_actionable_quarantined}"
    )

next_delayed_retry_at = status.get("nextDelayedRetryAt")
next_wake_reason = status.get("nextWakeReason")
wait_signal = status.get("waitSignal") or ""
triage_summary = status.get("triageSummary") or ""
delayed_retry_minutes = 0
if next_wake_reason == "delayed-provider-retry" and next_delayed_retry_at:
    try:
        delayed_retry_at = datetime.fromisoformat(next_delayed_retry_at.replace("Z", "+00:00"))
        delayed_retry_minutes = max(0, (delayed_retry_at - datetime.now(timezone.utc)).total_seconds() / 60)
    except ValueError:
        delayed_retry_minutes = 0

if max_delayed_retry_minutes > 0 and delayed_retry_minutes > max_delayed_retry_minutes:
    scope_suffix = f" across {provider_backoff_issue_count} issue(s)" if provider_backoff_issue_count > 0 else ""
    problems.append(
        f"delayed retry wait {int(delayed_retry_minutes)}m exceeds {max_delayed_retry_minutes}m{scope_suffix}"
    )

pending_github_sync_retry_state = status.get("pendingGithubSyncRetryState") or "none"
pending_github_sync_retry_overdue_minutes = int(status.get("pendingGithubSyncRetryOverdueMinutes") or 0)
provider_backoff_issue_count = int(status.get("providerBackoffIssueCount") or 0)
overdue_writeback_issue_count = int(status.get("overdueWritebackIssueCount") or 0)

if max_pending_github_retry_overdue_minutes > 0 and pending_github_sync_retry_overdue_minutes > max_pending_github_retry_overdue_minutes:
    scope_suffix = f" across {overdue_writeback_issue_count} issue(s)" if overdue_writeback_issue_count > 0 else ""
    problems.append(
        f"pending GitHub retry overdue {pending_github_sync_retry_overdue_minutes}m exceeds {max_pending_github_retry_overdue_minutes}m{scope_suffix}"
    )

if problems:
    print("[alert] Dragon Pi check failed")
    for problem in problems:
        print(f"- {problem}")
    sys.exit(1)

print("[ok] Dragon Pi check passed")
print(f"service_active={service_active}")
print(f"worker_health={worker_health}")
print(f"failed_issues={failed_issues}")
print(f"actionable_quarantined={actionable_quarantined}")
print(f"delayed_retry_minutes={int(delayed_retry_minutes)}")
print(f"triage_summary={triage_summary or 'none'}")
print(f"wait_signal={wait_signal or 'none'}")
print(f"provider_backoff_issue_count={provider_backoff_issue_count}")
print(f"overdue_writeback_issue_count={overdue_writeback_issue_count}")
print(f"pending_github_sync_retry_state={pending_github_sync_retry_state}")
print(f"pending_github_sync_retry_overdue_minutes={pending_github_sync_retry_overdue_minutes}")
PY
}

main "$@"
