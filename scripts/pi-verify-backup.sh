#!/usr/bin/env bash
set -euo pipefail

REPO_DIR="${REPO_DIR:-$HOME/dragon/IdeaEngine}"
BACKUP_ROOT="${BACKUP_ROOT:-$REPO_DIR/.tmp/pi-backups}"
BACKUP_SOURCE="${BACKUP_SOURCE:-}"

VOLUMES=(
  "dragon-postgres"
  "dragon-rabbitmq"
  "dragon-lgtm"
)

resolve_backup_source() {
  if [[ -n "${BACKUP_SOURCE}" ]]; then
    echo "${BACKUP_SOURCE}"
    return
  fi

  find "${BACKUP_ROOT}" -maxdepth 1 -type f -name 'dragon-pi-backup-*.tar.gz' | sort | tail -n 1
}

main() {
  local backup_source
  local listing_file

  backup_source="$(resolve_backup_source)"
  [[ -n "${backup_source}" ]] || {
    echo "No backup archive found. Set BACKUP_SOURCE to verify a specific archive." >&2
    exit 1
  }
  [[ -f "${backup_source}" ]] || {
    echo "Backup archive not found: ${backup_source}" >&2
    exit 1
  }

  tar -tzf "${backup_source}" >/dev/null
  echo "[pass] archive is readable: ${backup_source}"

  listing_file="$(mktemp)"
  trap 'rm -f "${listing_file}"' EXIT
  tar -tzf "${backup_source}" > "${listing_file}"

  if grep -Eq '/env\.snapshot$' "${listing_file}"; then
    echo "[pass] env snapshot present"
  else
    echo "[warn] env snapshot missing"
  fi

  if grep -Eq '/dragon-state\.tar\.gz$' "${listing_file}"; then
    echo "[pass] dragon state archive present"
  else
    echo "[warn] dragon state archive missing"
  fi

  if grep -Eq '/docker-compose\.yml$' "${listing_file}"; then
    echo "[pass] docker-compose snapshot present"
  else
    echo "[warn] docker-compose snapshot missing"
  fi

  for volume_name in "${VOLUMES[@]}"; do
    if grep -Eq "/${volume_name}\.tar\.gz$" "${listing_file}"; then
      echo "[pass] volume archive present: ${volume_name}"
    else
      echo "[warn] volume archive missing: ${volume_name}"
    fi
  done
}

main "$@"
