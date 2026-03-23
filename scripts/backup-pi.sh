#!/usr/bin/env bash
set -euo pipefail

REPO_DIR="${REPO_DIR:-$HOME/dragon/IdeaEngine}"
SERVICE_NAME="${SERVICE_NAME:-dragon-idea-engine}"
BACKUP_ROOT="${BACKUP_ROOT:-$REPO_DIR/.tmp/pi-backups}"
TIMESTAMP="${TIMESTAMP:-$(date +%Y%m%d-%H%M%S)}"
BACKUP_DIR="${BACKUP_DIR:-$BACKUP_ROOT/$TIMESTAMP}"
STOP_SERVICE="${STOP_SERVICE:-true}"
RESTART_SERVICE="${RESTART_SERVICE:-true}"
ARCHIVE_NAME="${ARCHIVE_NAME:-dragon-pi-backup-$TIMESTAMP.tar.gz}"
BACKUP_RETENTION_COUNT="${BACKUP_RETENTION_COUNT:-7}"
RUN_CLEANUP="${RUN_CLEANUP:-true}"

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

backup_volume() {
  local volume_name="$1"
  local output_file="$2"

  docker run --rm \
    -v "${volume_name}:/volume:ro" \
    -v "${BACKUP_DIR}:/backup" \
    alpine:3.20 \
    sh -lc "tar -czf \"/backup/${output_file}\" -C /volume ."
}

main() {
  require_command docker
  require_command tar
  [[ -d "${REPO_DIR}" ]] || {
    echo "Repo directory not found: ${REPO_DIR}" >&2
    exit 1
  }

  mkdir -p "${BACKUP_DIR}"
  echo "Saving backup into ${BACKUP_DIR}"

  stop_service_if_requested
  trap restart_service_if_requested EXIT

  if [[ -f "${REPO_DIR}/.env" ]]; then
    cp "${REPO_DIR}/.env" "${BACKUP_DIR}/env.snapshot"
  fi

  if [[ -d "${REPO_DIR}/.dragon" ]]; then
    tar -czf "${BACKUP_DIR}/dragon-state.tar.gz" -C "${REPO_DIR}" .dragon
  fi

  if [[ -f "${REPO_DIR}/docker-compose.yml" ]]; then
    cp "${REPO_DIR}/docker-compose.yml" "${BACKUP_DIR}/docker-compose.yml"
  fi

  for volume_name in "${VOLUMES[@]}"; do
    echo "Backing up volume ${volume_name}..."
    backup_volume "${volume_name}" "${volume_name}.tar.gz"
  done

  tar -czf "${BACKUP_ROOT}/${ARCHIVE_NAME}" -C "${BACKUP_ROOT}" "${TIMESTAMP}"
  echo "Created archive ${BACKUP_ROOT}/${ARCHIVE_NAME}"

  if [[ "${BACKUP_RETENTION_COUNT}" =~ ^[0-9]+$ ]] && [[ "${BACKUP_RETENTION_COUNT}" -gt 0 ]]; then
    mapfile -t old_archives < <(find "${BACKUP_ROOT}" -maxdepth 1 -type f -name 'dragon-pi-backup-*.tar.gz' | sort)
    if [[ "${#old_archives[@]}" -gt "${BACKUP_RETENTION_COUNT}" ]]; then
      remove_count=$(( ${#old_archives[@]} - BACKUP_RETENTION_COUNT ))
      for archive_path in "${old_archives[@]:0:${remove_count}}"; do
        echo "Removing old backup ${archive_path}"
        rm -f "${archive_path}"
      done
    fi
  fi

  if [[ "${RUN_CLEANUP}" == "true" ]] && [[ -x "${REPO_DIR}/scripts/cleanup-pi.sh" ]]; then
    "${REPO_DIR}/scripts/cleanup-pi.sh"
  fi
}

main "$@"
