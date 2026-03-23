#!/usr/bin/env bash
set -euo pipefail

REPO_DIR="${REPO_DIR:-$HOME/dragon/IdeaEngine}"
SERVICE_NAME="${SERVICE_NAME:-dragon-idea-engine}"
BACKUP_ROOT="${BACKUP_ROOT:-$REPO_DIR/.tmp/pi-backups}"
BACKUP_SOURCE="${BACKUP_SOURCE:-}"
STOP_SERVICE="${STOP_SERVICE:-true}"
RESTART_SERVICE="${RESTART_SERVICE:-true}"

VOLUMES=(
  "dragon-postgres"
  "dragon-rabbitmq"
  "dragon-lgtm"
)

require_command() {
  local command_name="$1"
  command -v "${command_name}" >/dev/null 2>&1 || {
    echo "Missing required command: ${command_name}" >&2
    exit 1
  }
}

stop_service_if_requested() {
  if [[ "${STOP_SERVICE}" == "true" ]]; then
    sudo systemctl stop "${SERVICE_NAME}.service" || true
  fi
}

restart_service_if_requested() {
  if [[ "${STOP_SERVICE}" == "true" && "${RESTART_SERVICE}" == "true" ]]; then
    sudo systemctl start "${SERVICE_NAME}.service" || true
  fi
}

restore_volume() {
  local volume_name="$1"
  local archive_file="$2"

  docker run --rm \
    -v "${volume_name}:/volume" \
    -v "${RESTORE_DIR}:/restore" \
    alpine:3.20 \
    sh -lc "rm -rf /volume/* /volume/.[!.]* /volume/..?* 2>/dev/null || true; tar -xzf \"/restore/${archive_file}\" -C /volume"
}

resolve_restore_dir() {
  if [[ -z "${BACKUP_SOURCE}" ]]; then
    echo "Set BACKUP_SOURCE to either a backup directory or a .tar.gz archive." >&2
    exit 1
  fi

  if [[ -d "${BACKUP_SOURCE}" ]]; then
    RESTORE_DIR="${BACKUP_SOURCE}"
    return
  fi

  if [[ -f "${BACKUP_SOURCE}" ]]; then
    RESTORE_DIR="$(mktemp -d)"
    tar -xzf "${BACKUP_SOURCE}" -C "${RESTORE_DIR}"
    local child
    child="$(find "${RESTORE_DIR}" -mindepth 1 -maxdepth 1 -type d | head -n 1)"
    [[ -n "${child}" ]] || {
      echo "Could not unpack restore archive: ${BACKUP_SOURCE}" >&2
      exit 1
    }
    RESTORE_DIR="${child}"
    return
  fi

  echo "Backup source not found: ${BACKUP_SOURCE}" >&2
  exit 1
}

main() {
  require_command docker
  require_command tar
  [[ -d "${REPO_DIR}" ]] || {
    echo "Repo directory not found: ${REPO_DIR}" >&2
    exit 1
  }

  resolve_restore_dir
  echo "Restoring backup from ${RESTORE_DIR}"

  stop_service_if_requested
  trap restart_service_if_requested EXIT

  if [[ -f "${RESTORE_DIR}/env.snapshot" ]]; then
    cp "${RESTORE_DIR}/env.snapshot" "${REPO_DIR}/.env"
  fi

  if [[ -f "${RESTORE_DIR}/dragon-state.tar.gz" ]]; then
    rm -rf "${REPO_DIR}/.dragon"
    tar -xzf "${RESTORE_DIR}/dragon-state.tar.gz" -C "${REPO_DIR}"
  fi

  for volume_name in "${VOLUMES[@]}"; do
    if [[ -f "${RESTORE_DIR}/${volume_name}.tar.gz" ]]; then
      echo "Restoring volume ${volume_name}..."
      restore_volume "${volume_name}" "${volume_name}.tar.gz"
    fi
  done
}

main "$@"
