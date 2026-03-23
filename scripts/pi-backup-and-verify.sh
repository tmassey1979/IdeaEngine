#!/usr/bin/env bash
set -euo pipefail

REPO_DIR="${REPO_DIR:-$HOME/dragon/IdeaEngine}"
BACKUP_ROOT="${BACKUP_ROOT:-$REPO_DIR/.tmp/pi-backups}"
TIMESTAMP="${TIMESTAMP:-$(date +%Y%m%d-%H%M%S)}"
ARCHIVE_NAME="${ARCHIVE_NAME:-dragon-pi-backup-$TIMESTAMP.tar.gz}"

main() {
  [[ -d "${REPO_DIR}/scripts" ]] || {
    echo "Repo scripts directory not found at ${REPO_DIR}/scripts" >&2
    exit 1
  }

  env \
    REPO_DIR="${REPO_DIR}" \
    BACKUP_ROOT="${BACKUP_ROOT}" \
    TIMESTAMP="${TIMESTAMP}" \
    ARCHIVE_NAME="${ARCHIVE_NAME}" \
    "${REPO_DIR}/scripts/backup-pi.sh"

  env \
    REPO_DIR="${REPO_DIR}" \
    BACKUP_ROOT="${BACKUP_ROOT}" \
    BACKUP_SOURCE="${BACKUP_ROOT}/${ARCHIVE_NAME}" \
    "${REPO_DIR}/scripts/pi-verify-backup.sh"
}

main "$@"
