#!/usr/bin/env bash
set -euo pipefail

REPO_DIR="${REPO_DIR:-$HOME/dragon/IdeaEngine}"
TMP_ROOT="${TMP_ROOT:-$REPO_DIR/.tmp}"
DIAGNOSTICS_ROOT="${DIAGNOSTICS_ROOT:-$TMP_ROOT/pi-diagnostics}"
BACKUP_ROOT="${BACKUP_ROOT:-$TMP_ROOT/pi-backups}"
DIAGNOSTICS_RETENTION_COUNT="${DIAGNOSTICS_RETENTION_COUNT:-10}"
BACKUP_DIR_RETENTION_COUNT="${BACKUP_DIR_RETENTION_COUNT:-3}"
DRY_RUN="${DRY_RUN:-false}"

remove_path() {
  local path="$1"
  if [[ "${DRY_RUN}" == "true" ]]; then
    echo "[dry-run] remove ${path}"
    return
  fi

  rm -rf "${path}"
  echo "Removed ${path}"
}

prune_directories() {
  local root="$1"
  local retention="$2"
  local pattern="${3:-*}"

  [[ -d "${root}" ]] || return
  [[ "${retention}" =~ ^[0-9]+$ ]] || return
  [[ "${retention}" -ge 0 ]] || return

  mapfile -t entries < <(find "${root}" -mindepth 1 -maxdepth 1 -type d -name "${pattern}" | sort)
  if [[ "${#entries[@]}" -le "${retention}" ]]; then
    return
  fi

  local remove_count=$(( ${#entries[@]} - retention ))
  for entry in "${entries[@]:0:${remove_count}}"; do
    remove_path "${entry}"
  done
}

main() {
  [[ -d "${REPO_DIR}" ]] || {
    echo "Repo directory not found: ${REPO_DIR}" >&2
    exit 1
  }

  prune_directories "${DIAGNOSTICS_ROOT}" "${DIAGNOSTICS_RETENTION_COUNT}"
  prune_directories "${BACKUP_ROOT}" "${BACKUP_DIR_RETENTION_COUNT}"
}

main "$@"
