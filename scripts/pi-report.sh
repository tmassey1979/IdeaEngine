#!/usr/bin/env bash
set -euo pipefail

REPO_DIR="${REPO_DIR:-$HOME/dragon/IdeaEngine}"
SERVICE_NAME="${SERVICE_NAME:-dragon-idea-engine}"
BACKUP_TIMER_NAME="${BACKUP_TIMER_NAME:-dragon-backup}"
UPDATE_TIMER_NAME="${UPDATE_TIMER_NAME:-dragon-update}"
ALERT_TIMER_NAME="${ALERT_TIMER_NAME:-dragon-alert-check}"
STATUS_URL="${STATUS_URL:-http://127.0.0.1:5078/status}"
HEALTH_URL="${HEALTH_URL:-http://127.0.0.1:5078/health}"
STATUS_SNAPSHOT_PATH="${STATUS_SNAPSHOT_PATH:-$REPO_DIR/.dragon/status/runtime-status.json}"
BACKUP_ROOT="${BACKUP_ROOT:-$REPO_DIR/.tmp/pi-backups}"
OUTPUT_FORMAT="${OUTPUT_FORMAT:-text}"

STATUS_FILE=""
STATUS_SOURCE="unavailable"

print_heading() {
  local title="$1"
  echo
  echo "== ${title} =="
}

command_exists() {
  command -v "$1" >/dev/null 2>&1
}

cleanup() {
  if [[ -n "${STATUS_FILE}" && -f "${STATUS_FILE}" ]]; then
    rm -f "${STATUS_FILE}"
  fi
}

parse_args() {
  while [[ $# -gt 0 ]]; do
    case "$1" in
      --json)
        OUTPUT_FORMAT="json"
        ;;
      --text)
        OUTPUT_FORMAT="text"
        ;;
      *)
        echo "Unknown argument: $1" >&2
        exit 1
        ;;
    esac
    shift
  done
}

json_query() {
  local path="$1"
  local default_value="${2:-}"

  if [[ -z "${STATUS_FILE}" || ! -f "${STATUS_FILE}" ]]; then
    printf '%s\n' "${default_value}"
    return
  fi

  if command_exists jq; then
    jq -r --arg path "${path}" --arg defaultValue "${default_value}" '
      def walk_path($segments):
        reduce $segments[] as $segment (.;
          if . == null then null
          elif ($segment | test("^[0-9]+$")) then
            if type == "array" then .[$segment | tonumber] else null end
          else
            if type == "object" and has($segment) then .[$segment] else null end
          end
        );

      walk_path($path | split(".")) // $defaultValue
    ' "${STATUS_FILE}"
    return
  fi

  if command_exists python3; then
    python3 - "${STATUS_FILE}" "${path}" "${default_value}" <<'PY'
import json
import sys

status_file, path, default_value = sys.argv[1:4]

try:
    with open(status_file, "r", encoding="utf-8") as handle:
        value = json.load(handle)
    for segment in path.split("."):
        if isinstance(value, list):
            if not segment.isdigit():
                value = default_value
                break
            index = int(segment)
            value = value[index] if 0 <= index < len(value) else default_value
            continue
        if isinstance(value, dict):
            value = value.get(segment, default_value)
            continue
        value = default_value
        break
except Exception:
    value = default_value

if value is None:
    print("")
elif isinstance(value, (dict, list)):
    print(json.dumps(value))
else:
    print(value)
PY
    return
  fi

  printf '%s\n' "${default_value}"
}

emit_status_assignments() {
  if [[ -z "${STATUS_FILE}" || ! -f "${STATUS_FILE}" ]]; then
    return
  fi

  if command_exists python3; then
    python3 - "${STATUS_FILE}" <<'PY'
import json
import shlex
import sys

status_file = sys.argv[1]

with open(status_file, "r", encoding="utf-8") as handle:
    payload = json.load(handle)

issues = payload.get("issues") or []
quarantined_issues = [issue for issue in issues if str(issue.get("overallStatus", "")).lower() == "quarantined"]
actionable_quarantined = sum(1 for issue in quarantined_issues if int(issue.get("queuedJobCount") or 0) > 0)
inactive_quarantined = len(quarantined_issues) - actionable_quarantined
generated_at = payload.get("generatedAt", "")
pending_github_sync = payload.get("pendingGithubSync") or []
pending_github_sync_next_retry = payload.get("pendingGithubSyncNextRetryAt") or next((item.get("nextRetryAt", "") for item in pending_github_sync if item.get("nextRetryAt")), "")
pending_github_sync_retry_state = payload.get("pendingGithubSyncRetryState") or ""
pending_github_sync_retry_overdue_minutes = int(payload.get("pendingGithubSyncRetryOverdueMinutes") or 0)
wait_signal = ""
if payload.get("nextWakeReason") == "delayed-provider-retry":
    wait_signal = "provider backoff (long)" if payload.get("delayedRetryUrgency") == "alert" else "provider backoff"
elif pending_github_sync_retry_overdue_minutes >= 15:
    wait_signal = "prioritizing overdue writeback replay"
elif pending_github_sync_retry_state == "ready now":
    wait_signal = "writeback replay ready"
elif payload.get("nextWakeReason") == "poll-interval":
    wait_signal = "routine poll wait"

fields = {
    "GENERATED_AT": generated_at,
    "WORKER_MODE": payload.get("workerMode", ""),
    "WORKER_STATE": payload.get("workerState", ""),
    "WORKER_REASON": payload.get("workerCompletionReason", ""),
    "WORKER_ACTIVITY": payload.get("workerActivity", ""),
    "HEALTH": payload.get("health", ""),
    "ATTENTION_SUMMARY": payload.get("attentionSummary", ""),
    "NEXT_WAKE_REASON": payload.get("nextWakeReason", ""),
    "NEXT_DELAYED_RETRY_AT": payload.get("nextDelayedRetryAt", ""),
    "DELAYED_RETRY_URGENCY": payload.get("delayedRetryUrgency", ""),
    "DELAYED_RETRY_SUMMARY": payload.get("delayedRetrySummary", ""),
    "PENDING_GITHUB_SYNC_NEXT_RETRY": pending_github_sync_next_retry,
    "PENDING_GITHUB_SYNC_LAST_ATTEMPT": next((item.get("lastAttemptedAt", "") for item in pending_github_sync if item.get("lastAttemptedAt")), ""),
    "PENDING_GITHUB_SYNC_RETRY_STATE": pending_github_sync_retry_state,
    "PENDING_GITHUB_SYNC_RETRY_OVERDUE_MINUTES": payload.get("pendingGithubSyncRetryOverdueMinutes", 0),
    "WAIT_SIGNAL": wait_signal,
    "QUEUED_JOBS": payload.get("queuedJobs", 0),
    "FAILED_ISSUES": ((payload.get("rollup") or {}).get("failedIssues", 0)),
    "IN_PROGRESS_ISSUES": ((payload.get("rollup") or {}).get("inProgressIssues", 0)),
    "QUARANTINED_ISSUES": ((payload.get("rollup") or {}).get("quarantinedIssues", 0)),
    "VALIDATED_ISSUES": ((payload.get("rollup") or {}).get("validatedIssues", 0)),
    "ACTIONABLE_QUARANTINED_ISSUES": actionable_quarantined,
    "INACTIVE_QUARANTINED_ISSUES": inactive_quarantined,
    "QUEUE_DIRECTION": payload.get("queueDirection", ""),
    "QUEUE_DELTA": payload.get("queueDelta", 0),
    "QUEUE_COMPARED_AT": payload.get("queueComparedAt", ""),
    "LATEST_ISSUE_NUMBER": ((payload.get("latestActivity") or {}).get("issueNumber", "")),
    "LATEST_ISSUE_TITLE": ((payload.get("latestActivity") or {}).get("issueTitle", "")),
    "LATEST_ISSUE_STAGE": ((payload.get("latestActivity") or {}).get("currentStage", "")),
    "LATEST_ISSUE_SUMMARY": ((payload.get("latestActivity") or {}).get("summary", "")),
    "LATEST_ISSUE_RECORDED_AT": ((payload.get("latestActivity") or {}).get("recordedAt", "")),
}

for key, value in fields.items():
    print(f"{key}={shlex.quote(str(value))}")
PY
  fi
}

fetch_status() {
  STATUS_FILE="$(mktemp)"

  if command_exists curl && curl -fsS "${STATUS_URL}" > "${STATUS_FILE}" 2>/dev/null; then
    STATUS_SOURCE="http"
    return
  fi

  if [[ -f "${STATUS_SNAPSHOT_PATH}" ]]; then
    cp "${STATUS_SNAPSHOT_PATH}" "${STATUS_FILE}"
    STATUS_SOURCE="snapshot"
    return
  fi

  rm -f "${STATUS_FILE}"
  STATUS_FILE=""
}

service_state() {
  local subcommand="$1"
  if command_exists systemctl; then
    local result
    result="$(systemctl "${subcommand}" "${SERVICE_NAME}.service" 2>/dev/null || true)"
    if [[ -n "${result}" ]]; then
      echo "${result}"
    else
      echo "unknown"
    fi
  else
    echo "unavailable"
  fi
}

service_property() {
  local property_name="$1"
  if command_exists systemctl; then
    local result
    result="$(systemctl show "${SERVICE_NAME}.service" --property "${property_name}" --value 2>/dev/null || true)"
    if [[ -n "${result}" ]]; then
      echo "${result}"
    else
      echo "unknown"
    fi
  else
    echo "unavailable"
  fi
}

timer_state() {
  local subcommand="$1"
  local timer_name="$2"
  if command_exists systemctl; then
    local result
    result="$(systemctl "${subcommand}" "${timer_name}.timer" 2>/dev/null || true)"
    if [[ -n "${result}" ]]; then
      echo "${result}"
    else
      echo "unknown"
    fi
  else
    echo "unavailable"
  fi
}

timer_schedule() {
  local timer_name="$1"
  local label_prefix="$2"
  if ! command_exists systemctl; then
    echo "systemctl unavailable"
    return
  fi

  local next_elapse
  local last_trigger
  next_elapse="$(systemctl show "${timer_name}.timer" --property NextElapseUSec --value 2>/dev/null || true)"
  last_trigger="$(systemctl show "${timer_name}.timer" --property LastTriggerUSec --value 2>/dev/null || true)"

  echo "${label_prefix}_next=${next_elapse:-unknown}"
  echo "${label_prefix}_last=${last_trigger:-unknown}"
}

compose_summary() {
  if [[ ! -d "${REPO_DIR}" ]] || ! command_exists docker; then
    echo "docker compose unavailable"
    return
  fi

  local output
  output="$(cd "${REPO_DIR}" && docker compose ps --format json 2>/dev/null || true)"
  if [[ -z "${output}" ]]; then
    echo "docker compose ps returned no data"
    return
  fi

  if command_exists python3; then
    python3 - <<'PY' <<<"${output}"
import json
import sys

raw = sys.stdin.read().strip()
if not raw:
    print("docker compose ps returned no data")
    raise SystemExit(0)

try:
    data = json.loads(raw)
except json.JSONDecodeError:
    lines = [line for line in raw.splitlines() if line.strip()]
    data = [json.loads(line) for line in lines]

if isinstance(data, dict):
    data = [data]

for item in data:
    name = item.get("Name", "unknown")
    state = item.get("State", "unknown")
    health = item.get("Health", "")
    status = item.get("Status", "")
    summary = f"{name}: {state}"
    if health:
      summary += f" ({health})"
    if status:
      summary += f" - {status}"
    print(summary)
PY
    return
  fi

  echo "${output}"
}

compose_summary_json() {
  if [[ ! -d "${REPO_DIR}" ]] || ! command_exists docker; then
    echo '[]'
    return
  fi

  local output
  output="$(cd "${REPO_DIR}" && docker compose ps --format json 2>/dev/null || true)"
  if [[ -z "${output}" ]]; then
    echo '[]'
    return
  fi

  if command_exists python3; then
    python3 - <<'PY' <<<"${output}"
import json
import sys

raw = sys.stdin.read().strip()
if not raw:
    print("[]")
    raise SystemExit(0)

try:
    data = json.loads(raw)
except json.JSONDecodeError:
    lines = [line for line in raw.splitlines() if line.strip()]
    data = [json.loads(line) for line in lines]

if isinstance(data, dict):
    data = [data]

normalized = []
for item in data:
    normalized.append({
        "name": item.get("Name", "unknown"),
        "state": item.get("State", "unknown"),
        "health": item.get("Health", ""),
        "status": item.get("Status", ""),
    })

print(json.dumps(normalized))
PY
    return
  fi

  echo '[]'
}

newest_backup() {
  if [[ ! -d "${BACKUP_ROOT}" ]]; then
    echo "none found"
    return
  fi

  local latest
  latest="$(find "${BACKUP_ROOT}" -maxdepth 1 -type f -name 'dragon-pi-backup-*.tar.gz' | sort | tail -n 1)"
  if [[ -z "${latest}" ]]; then
    echo "none found"
    return
  fi

  ls -lh "${latest}" 2>/dev/null || echo "${latest}"
}

print_endpoint_status() {
  local url="$1"
  local label="$2"

  if command_exists curl && curl -fsS "${url}" >/dev/null 2>&1; then
    echo "${label}: reachable"
  else
    echo "${label}: unreachable"
  fi
}

recent_service_failures() {
  if ! command_exists journalctl; then
    echo "journalctl unavailable"
    return
  fi

  local lines
  lines="$(journalctl -u "${SERVICE_NAME}.service" -n 80 --no-pager 2>/dev/null | grep -Ei "error|failed|exception|fatal|panic|crash" | tail -n 5 || true)"
  if [[ -z "${lines}" ]]; then
    echo "recent_service_failures: none found"
    return
  fi

  while IFS= read -r line; do
    [[ -n "${line}" ]] || continue
    echo "recent_service_failure: ${line}"
  done <<< "${lines}"
}

recent_service_failures_json() {
  if ! command_exists journalctl; then
    echo '[]'
    return
  fi

  local lines
  lines="$(journalctl -u "${SERVICE_NAME}.service" -n 80 --no-pager 2>/dev/null | grep -Ei "error|failed|exception|fatal|panic|crash" | tail -n 5 || true)"
  if [[ -z "${lines}" ]]; then
    echo '[]'
    return
  fi

  if command_exists python3; then
    python3 - <<'PY' <<<"${lines}"
import json
import sys

lines = [line for line in sys.stdin.read().splitlines() if line.strip()]
print(json.dumps(lines))
PY
    return
  fi

  echo '[]'
}

emit_json_report() {
  local compose_json failure_json newest_backup_value
  compose_json="$(compose_summary_json)"
  failure_json="$(recent_service_failures_json)"
  newest_backup_value="$(newest_backup)"

  python3 - "${STATUS_FILE:-}" "${STATUS_SOURCE}" "${REPO_DIR}" "${SERVICE_NAME}" "${BACKUP_TIMER_NAME}" "${UPDATE_TIMER_NAME}" "${ALERT_TIMER_NAME}" "${service_active}" "${service_enabled}" "${service_substate}" "${service_result}" "${service_exec_status}" "${service_restart_count}" "${backup_timer_active}" "${backup_timer_enabled}" "${backup_timer_next}" "${backup_timer_last}" "${update_timer_active}" "${update_timer_enabled}" "${update_timer_next}" "${update_timer_last}" "${alert_timer_active}" "${alert_timer_enabled}" "${alert_timer_next}" "${alert_timer_last}" "${health_endpoint_state}" "${status_endpoint_state}" "${compose_json}" "${failure_json}" "${newest_backup_value}" <<'PY'
import json
import os
import sys

(
    status_file,
    status_source,
    repo_dir,
    service_name,
    backup_timer_name,
    update_timer_name,
    alert_timer_name,
    service_active,
    service_enabled,
    service_substate,
    service_result,
    service_exec_status,
    service_restart_count,
    backup_timer_active,
    backup_timer_enabled,
    backup_timer_next,
    backup_timer_last,
    update_timer_active,
    update_timer_enabled,
    update_timer_next,
    update_timer_last,
    alert_timer_active,
    alert_timer_enabled,
    alert_timer_next,
    alert_timer_last,
    health_endpoint_state,
    status_endpoint_state,
    compose_json,
    failure_json,
    newest_backup_value,
) = sys.argv[1:]

status_payload = None
if status_file and os.path.isfile(status_file):
    try:
        with open(status_file, "r", encoding="utf-8") as handle:
            status_payload = json.load(handle)
    except Exception:
        status_payload = None

report = {
    "timestamp": os.popen("date -Is").read().strip(),
    "repoDir": repo_dir,
    "statusSource": status_source,
    "service": {
        "name": service_name,
        "active": service_active,
        "enabled": service_enabled,
        "substate": service_substate,
        "result": service_result,
        "execMainStatus": service_exec_status,
        "restartCount": service_restart_count,
        "recentFailures": json.loads(failure_json),
    },
    "timers": {
        "backup": {
            "name": backup_timer_name,
            "active": backup_timer_active,
            "enabled": backup_timer_enabled,
            "next": backup_timer_next,
            "last": backup_timer_last,
        },
        "update": {
            "name": update_timer_name,
            "active": update_timer_active,
            "enabled": update_timer_enabled,
            "next": update_timer_next,
            "last": update_timer_last,
        },
        "alert": {
            "name": alert_timer_name,
            "active": alert_timer_active,
            "enabled": alert_timer_enabled,
            "next": alert_timer_next,
            "last": alert_timer_last,
        },
    },
    "endpoints": {
        "health": health_endpoint_state,
        "status": status_endpoint_state,
    },
    "compose": json.loads(compose_json or "[]"),
    "newestBackup": newest_backup_value,
    "status": status_payload,
}

print(json.dumps(report, indent=2))
PY
}

endpoint_state() {
  local url="$1"

  if command_exists curl && curl -fsS "${url}" >/dev/null 2>&1; then
    echo "reachable"
  else
    echo "unreachable"
  fi
}

main() {
  parse_args "$@"
  trap cleanup EXIT

  fetch_status

  local service_active service_enabled
  local service_substate service_result service_exec_status service_restart_count
  local backup_timer_active backup_timer_enabled
  local update_timer_active update_timer_enabled
  local alert_timer_active alert_timer_enabled
  local backup_timer_next backup_timer_last
  local update_timer_next update_timer_last
  local alert_timer_next alert_timer_last
  local health_endpoint_state status_endpoint_state
  service_active="$(service_state is-active)"
  service_enabled="$(service_state is-enabled)"
  service_substate="$(service_property SubState)"
  service_result="$(service_property Result)"
  service_exec_status="$(service_property ExecMainStatus)"
  service_restart_count="$(service_property NRestarts)"
  backup_timer_active="$(timer_state is-active "${BACKUP_TIMER_NAME}")"
  backup_timer_enabled="$(timer_state is-enabled "${BACKUP_TIMER_NAME}")"
  update_timer_active="$(timer_state is-active "${UPDATE_TIMER_NAME}")"
  update_timer_enabled="$(timer_state is-enabled "${UPDATE_TIMER_NAME}")"
  alert_timer_active="$(timer_state is-active "${ALERT_TIMER_NAME}")"
  alert_timer_enabled="$(timer_state is-enabled "${ALERT_TIMER_NAME}")"
  backup_timer_next="$(systemctl show "${BACKUP_TIMER_NAME}.timer" --property NextElapseUSec --value 2>/dev/null || true)"
  backup_timer_last="$(systemctl show "${BACKUP_TIMER_NAME}.timer" --property LastTriggerUSec --value 2>/dev/null || true)"
  update_timer_next="$(systemctl show "${UPDATE_TIMER_NAME}.timer" --property NextElapseUSec --value 2>/dev/null || true)"
  update_timer_last="$(systemctl show "${UPDATE_TIMER_NAME}.timer" --property LastTriggerUSec --value 2>/dev/null || true)"
  alert_timer_next="$(systemctl show "${ALERT_TIMER_NAME}.timer" --property NextElapseUSec --value 2>/dev/null || true)"
  alert_timer_last="$(systemctl show "${ALERT_TIMER_NAME}.timer" --property LastTriggerUSec --value 2>/dev/null || true)"
  backup_timer_next="${backup_timer_next:-unknown}"
  backup_timer_last="${backup_timer_last:-unknown}"
  update_timer_next="${update_timer_next:-unknown}"
  update_timer_last="${update_timer_last:-unknown}"
  alert_timer_next="${alert_timer_next:-unknown}"
  alert_timer_last="${alert_timer_last:-unknown}"
  health_endpoint_state="$(endpoint_state "${HEALTH_URL}")"
  status_endpoint_state="$(endpoint_state "${STATUS_URL}")"

  if [[ "${OUTPUT_FORMAT}" == "json" ]]; then
    emit_json_report
    return
  fi

  print_heading "Dragon Pi Report"
  echo "timestamp: $(date -Is)"
  echo "repo_dir: ${REPO_DIR}"
  echo "status_source: ${STATUS_SOURCE}"

  print_heading "Systemd"
  echo "service_active: ${service_active}"
  echo "service_enabled: ${service_enabled}"
  echo "service_substate: ${service_substate}"
  echo "service_result: ${service_result}"
  echo "service_exec_main_status: ${service_exec_status}"
  echo "service_restart_count: ${service_restart_count}"
  echo "backup_timer_active: ${backup_timer_active}"
  echo "backup_timer_enabled: ${backup_timer_enabled}"
  echo "backup_timer_next=${backup_timer_next}"
  echo "backup_timer_last=${backup_timer_last}"
  echo "update_timer_active: ${update_timer_active}"
  echo "update_timer_enabled: ${update_timer_enabled}"
  echo "update_timer_next=${update_timer_next}"
  echo "update_timer_last=${update_timer_last}"
  echo "alert_timer_active: ${alert_timer_active}"
  echo "alert_timer_enabled: ${alert_timer_enabled}"
  echo "alert_timer_next=${alert_timer_next}"
  echo "alert_timer_last=${alert_timer_last}"
  recent_service_failures

  print_heading "Endpoints"
  echo "health: ${health_endpoint_state}"
  echo "status: ${status_endpoint_state}"

  print_heading "Worker"
  if [[ -n "${STATUS_FILE}" && -f "${STATUS_FILE}" ]] && command_exists python3; then
    # shellcheck disable=SC1090
    source <(emit_status_assignments)
    echo "generated_at: ${GENERATED_AT:-unknown}"
    echo "worker_mode: ${WORKER_MODE:-unknown}"
    echo "worker_state: ${WORKER_STATE:-unknown}"
    if [[ -n "${WORKER_REASON:-}" && "${WORKER_REASON}" != "None" ]]; then
      echo "worker_completion_reason: ${WORKER_REASON}"
    fi
    if [[ -n "${WORKER_ACTIVITY:-}" && "${WORKER_ACTIVITY}" != "None" ]]; then
      echo "worker_activity: ${WORKER_ACTIVITY}"
    fi
    echo "health: ${HEALTH:-unknown}"
    echo "attention_summary: ${ATTENTION_SUMMARY:-none}"
    if [[ -n "${WAIT_SIGNAL:-}" && "${WAIT_SIGNAL}" != "None" ]]; then
      echo "wait_signal: ${WAIT_SIGNAL}"
    fi
    if [[ -n "${NEXT_DELAYED_RETRY_AT:-}" && "${NEXT_DELAYED_RETRY_AT}" != "None" ]]; then
      echo "next_delayed_retry_at: ${NEXT_DELAYED_RETRY_AT}"
    fi
    if [[ -n "${PENDING_GITHUB_SYNC_NEXT_RETRY:-}" && "${PENDING_GITHUB_SYNC_NEXT_RETRY}" != "None" ]]; then
      echo "pending_github_sync_next_retry_at: ${PENDING_GITHUB_SYNC_NEXT_RETRY}"
    fi
    if [[ -n "${PENDING_GITHUB_SYNC_RETRY_STATE:-}" && "${PENDING_GITHUB_SYNC_RETRY_STATE}" != "None" ]]; then
      echo "pending_github_sync_retry_state: ${PENDING_GITHUB_SYNC_RETRY_STATE}"
    fi
    if [[ "${PENDING_GITHUB_SYNC_RETRY_OVERDUE_MINUTES:-0}" != "0" ]]; then
      echo "pending_github_sync_retry_overdue_minutes: ${PENDING_GITHUB_SYNC_RETRY_OVERDUE_MINUTES}"
    fi
    if [[ -n "${PENDING_GITHUB_SYNC_LAST_ATTEMPT:-}" && "${PENDING_GITHUB_SYNC_LAST_ATTEMPT}" != "None" ]]; then
      echo "pending_github_sync_last_attempt_at: ${PENDING_GITHUB_SYNC_LAST_ATTEMPT}"
    fi
    if [[ -n "${DELAYED_RETRY_SUMMARY:-}" && "${DELAYED_RETRY_SUMMARY}" != "None" ]]; then
      echo "delayed_retry_summary: ${DELAYED_RETRY_SUMMARY}"
    fi
    echo "queued_jobs: ${QUEUED_JOBS:-0}"
    echo "failed_issues: ${FAILED_ISSUES:-0}"
    echo "in_progress_issues: ${IN_PROGRESS_ISSUES:-0}"
    echo "quarantined_issues: ${QUARANTINED_ISSUES:-0}"
    echo "actionable_quarantined_issues: ${ACTIONABLE_QUARANTINED_ISSUES:-0}"
    echo "inactive_quarantined_issues: ${INACTIVE_QUARANTINED_ISSUES:-0}"
    echo "validated_issues: ${VALIDATED_ISSUES:-0}"
    echo "queue_direction: ${QUEUE_DIRECTION:-unknown}"
    echo "queue_delta: ${QUEUE_DELTA:-0}"
    if [[ -n "${QUEUE_COMPARED_AT:-}" ]]; then
      echo "queue_compared_at: ${QUEUE_COMPARED_AT}"
    fi
  elif [[ -n "${STATUS_FILE}" && -f "${STATUS_FILE}" ]]; then
    echo "status_json_available: yes"
    echo "worker_activity: $(json_query workerActivity unknown)"
    echo "health: $(json_query health unknown)"
    echo "next_wake_reason: $(json_query nextWakeReason '')"
    if [[ "$(json_query pendingGithubSyncRetryOverdueMinutes 0)" != "0" ]] && [[ "$(json_query pendingGithubSyncRetryOverdueMinutes 0)" -ge 15 ]]; then
      echo "wait_signal: prioritizing overdue writeback replay"
    elif [[ "$(json_query pendingGithubSyncRetryState '')" == "ready now" ]]; then
      echo "wait_signal: writeback replay ready"
    elif [[ "$(json_query nextWakeReason '')" == "delayed-provider-retry" ]]; then
      if [[ "$(json_query delayedRetryUrgency '')" == "alert" ]]; then
        echo "wait_signal: provider backoff (long)"
      else
        echo "wait_signal: provider backoff"
      fi
    elif [[ "$(json_query nextWakeReason '')" == "poll-interval" ]]; then
      echo "wait_signal: routine poll wait"
    fi
    echo "next_delayed_retry_at: $(json_query nextDelayedRetryAt '')"
    echo "pending_github_sync_next_retry_at: $(json_query pendingGithubSyncNextRetryAt '')"
    echo "pending_github_sync_retry_state: $(json_query pendingGithubSyncRetryState '')"
    echo "pending_github_sync_retry_overdue_minutes: $(json_query pendingGithubSyncRetryOverdueMinutes 0)"
    echo "pending_github_sync_last_attempt_at: $(json_query pendingGithubSync.0.lastAttemptedAt '')"
    echo "delayed_retry_urgency: $(json_query delayedRetryUrgency '')"
    echo "delayed_retry_summary: $(json_query delayedRetrySummary '')"
    echo "queued_jobs: $(json_query queuedJobs 0)"
    echo "quarantined_issues: $(json_query rollup.quarantinedIssues 0)"
    echo "validated_issues: $(json_query rollup.validatedIssues 0)"
  else
    echo "status_json_available: no"
  fi

  print_heading "Latest Activity"
  if [[ -n "${STATUS_FILE}" && -f "${STATUS_FILE}" ]]; then
    local issue_number issue_title issue_stage issue_summary issue_recorded_at
    issue_number="$(json_query latestActivity.issueNumber "")"
    issue_title="$(json_query latestActivity.issueTitle "")"
    issue_stage="$(json_query latestActivity.currentStage "")"
    issue_summary="$(json_query latestActivity.summary "")"
    issue_recorded_at="$(json_query latestActivity.recordedAt "")"

    if [[ -n "${issue_number}" || -n "${issue_title}" ]]; then
      echo "issue: #${issue_number} ${issue_title}"
      echo "stage: ${issue_stage:-unknown}"
      echo "recorded_at: ${issue_recorded_at:-unknown}"
      if [[ -n "${issue_summary}" ]]; then
        echo "summary: ${issue_summary//$'\n'/ }"
      fi
    else
      echo "No latest activity recorded."
    fi
  else
    echo "No status snapshot available."
  fi

  print_heading "Compose"
  compose_summary

  print_heading "Backups"
  newest_backup
}

main "$@"
