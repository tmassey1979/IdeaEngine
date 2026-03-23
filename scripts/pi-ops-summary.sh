#!/usr/bin/env bash
set -euo pipefail

REPO_DIR="${REPO_DIR:-$HOME/dragon/IdeaEngine}"
SERVICE_NAME="${SERVICE_NAME:-dragon-idea-engine}"
BACKUP_TIMER_NAME="${BACKUP_TIMER_NAME:-dragon-backup}"
UPDATE_TIMER_NAME="${UPDATE_TIMER_NAME:-dragon-update}"
ALERT_TIMER_NAME="${ALERT_TIMER_NAME:-dragon-alert-check}"

cat <<EOF
Dragon Pi Ops Summary

Repo:
  ${REPO_DIR}

Core commands:
  dragon-report
  dragon-report --json
  dragon-self-test
  dragon-refresh-tooling
  dragon-daily-routine
  dragon-evening-routine
  dragon-weekly-routine
  dragon-health
  dragon-preflight
  dragon-start
  dragon-start-and-wait
  dragon-stop
  dragon-stop-and-wait
  dragon-wait-stopped
  dragon-restart
  dragon-restart-and-wait
  dragon-ensure-running
  dragon-wait-healthy
  dragon-tail-logs
  dragon-update
  dragon-backup
  dragon-backup-and-verify
  dragon-verify-backup
  dragon-firstaid
  dragon-alert-check
  dragon-configure-alerts
  dragon-watch-status
  dragon-doctor
  dragon-share-status

Service control:
  sudo systemctl start ${SERVICE_NAME}
  sudo systemctl stop ${SERVICE_NAME}
  sudo systemctl restart ${SERVICE_NAME}
  sudo systemctl status ${SERVICE_NAME} --no-pager
  dragon-start --follow
  dragon-start-and-wait
  dragon-stop --status
  dragon-stop-and-wait
  dragon-wait-stopped
  dragon-restart --follow
  dragon-restart-and-wait
  dragon-ensure-running
  dragon-wait-healthy
  dragon-tail-logs
  dragon-tail-logs --all

Timers:
  systemctl list-timers ${BACKUP_TIMER_NAME}.timer
  systemctl list-timers ${UPDATE_TIMER_NAME}.timer
  systemctl list-timers ${ALERT_TIMER_NAME}.timer

Recovery:
  dragon-firstaid
  dragon-self-test
  dragon-refresh-tooling
  dragon-daily-routine
  dragon-doctor
  dragon-share-status
  ~/dragon/IdeaEngine/scripts/pi-reset-state.sh
  ~/dragon/IdeaEngine/scripts/collect-pi-diagnostics.sh

Backup and restore:
  dragon-backup
  dragon-backup-and-verify
  dragon-verify-backup
  dragon-evening-routine
  dragon-weekly-routine
  ~/dragon/IdeaEngine/scripts/restore-pi.sh

Alerting:
  dragon-alert-check
  dragon-alert-notify
  dragon-configure-alerts

Useful journals:
  sudo journalctl -u ${SERVICE_NAME} -n 200 --no-pager
  sudo journalctl -u ${BACKUP_TIMER_NAME}.service -n 200 --no-pager
  sudo journalctl -u ${UPDATE_TIMER_NAME}.service -n 200 --no-pager
  sudo journalctl -u ${ALERT_TIMER_NAME}.service -n 200 --no-pager
EOF
