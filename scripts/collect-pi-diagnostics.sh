#!/usr/bin/env bash
set -euo pipefail

REPO_DIR="${REPO_DIR:-$HOME/dragon/IdeaEngine}"
SERVICE_NAME="${SERVICE_NAME:-dragon-idea-engine}"
STATUS_URL="${STATUS_URL:-http://127.0.0.1:5078/status}"
HEALTH_URL="${HEALTH_URL:-http://127.0.0.1:5078/health}"
OUTPUT_ROOT="${OUTPUT_ROOT:-$REPO_DIR/.tmp/pi-diagnostics}"
TIMESTAMP="${TIMESTAMP:-$(date +%Y%m%d-%H%M%S)}"
OUTPUT_DIR="${OUTPUT_DIR:-$OUTPUT_ROOT/$TIMESTAMP}"

mkdir -p "${OUTPUT_DIR}"

write_section() {
  local filename="$1"
  shift
  {
    echo "# $filename"
    echo
    "$@"
  } > "${OUTPUT_DIR}/${filename}" 2>&1 || true
}

write_text() {
  local filename="$1"
  shift
  {
    printf '%s\n' "$@"
  } > "${OUTPUT_DIR}/${filename}" 2>&1 || true
}

collect_if_present() {
  local filename="$1"
  local path="$2"
  if [[ -f "${path}" ]]; then
    cp "${path}" "${OUTPUT_DIR}/${filename}"
  fi
}

write_text "summary.txt" \
  "Dragon Pi diagnostics bundle" \
  "timestamp=${TIMESTAMP}" \
  "repo_dir=${REPO_DIR}" \
  "service_name=${SERVICE_NAME}" \
  "status_url=${STATUS_URL}" \
  "health_url=${HEALTH_URL}"

write_section "uname.txt" uname -a
write_section "os-release.txt" cat /etc/os-release
write_section "disk-usage.txt" df -h
write_section "memory.txt" free -h
write_section "uptime.txt" uptime
write_section "docker-version.txt" docker version
write_section "docker-info.txt" docker info
write_section "compose-version.txt" docker compose version
write_section "service-status.txt" systemctl --no-pager --full status "${SERVICE_NAME}.service"
write_section "service-journal.txt" journalctl -u "${SERVICE_NAME}.service" -n 400 --no-pager

if [[ -d "${REPO_DIR}" ]]; then
  write_section "git-status.txt" git -C "${REPO_DIR}" status --short
  write_section "git-branch.txt" git -C "${REPO_DIR}" branch --show-current
  write_section "compose-ps.txt" bash -lc "cd '${REPO_DIR}' && docker compose ps"
  write_section "compose-config.txt" bash -lc "cd '${REPO_DIR}' && docker compose config"
  write_section "compose-logs.txt" bash -lc "cd '${REPO_DIR}' && docker compose logs --tail 300"
  collect_if_present "env.snapshot" "${REPO_DIR}/.env"
  collect_if_present "runtime-status.json" "${REPO_DIR}/.dragon/status/runtime-status.json"
  collect_if_present "issues.json" "${REPO_DIR}/.dragon/state/issues.json"
fi

write_section "health-endpoint.txt" curl -fsS "${HEALTH_URL}"
write_section "status-endpoint.json" curl -fsS "${STATUS_URL}"

echo "Saved diagnostics to ${OUTPUT_DIR}"
