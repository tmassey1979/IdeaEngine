# Raspberry Pi Setup

This repo now includes a bootstrap script for a fresh Raspberry Pi OS or Debian-based host:

```bash
curl -fsSL https://raw.githubusercontent.com/tmassey1979/IdeaEngine/feature/github-run-until-idle-sync/scripts/setup-pi.sh -o setup-pi.sh
chmod +x setup-pi.sh
./setup-pi.sh
```

If you want the guided all-in-one path instead:

```bash
curl -fsSL https://raw.githubusercontent.com/tmassey1979/IdeaEngine/feature/github-run-until-idle-sync/scripts/pi-bootstrap-all.sh -o pi-bootstrap-all.sh
chmod +x pi-bootstrap-all.sh
./pi-bootstrap-all.sh
```

What it does:

- installs Docker Engine and the Docker Compose plugin
- installs Git and GitHub CLI
- adds your user to the `docker` group
- clones or updates this repo
- creates `.env` from `.env.docker.example` if it does not exist
- installs and enables a `systemd` service for the Docker Compose stack by default

Defaults:

- repo branch: `feature/github-run-until-idle-sync`
- install directory: `$HOME/dragon/IdeaEngine`
- env file: `$HOME/dragon/IdeaEngine/.env`

Useful overrides:

```bash
REPO_BRANCH=main ./setup-pi.sh
INSTALL_ROOT=/srv/dragon ./setup-pi.sh
AUTO_START=true ./setup-pi.sh
INSTALL_SYSTEMD_SERVICE=false AUTO_START=true ./setup-pi.sh
INSTALL_BACKUP_TIMER=false ./setup-pi.sh
INSTALL_UPDATE_TIMER=true ./setup-pi.sh
INSTALL_ALERT_TIMER=true ./setup-pi.sh

AUTO_START_STACK=false ./pi-bootstrap-all.sh
RUN_HEALTHCHECK_AT_END=false ./pi-bootstrap-all.sh
```

After the script finishes:

1. Configure `.env`:

```bash
~/dragon/IdeaEngine/scripts/configure-pi-env.sh
```

2. Start the service:

```bash
dragon-start
dragon-start --follow
```

3. Verify the nightly backup timer:

```bash
systemctl list-timers dragon-backup.timer
```

4. Optional: verify the scheduled update timer if you enabled it:

```bash
systemctl list-timers dragon-update.timer
```

5. Optional: verify the scheduled alert timer if you enabled it:

```bash
systemctl list-timers dragon-alert-check.timer
```

Routine maintenance:

```bash
dragon-report
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
dragon-update
dragon-backup
dragon-backup-and-verify
dragon-verify-backup
dragon-restore-and-verify
dragon-firstaid
dragon-alert-check
dragon-alert-notify
dragon-configure-alerts
dragon-ops-summary
dragon-reinstall-service
dragon-tail-logs
dragon-status-dashboard
dragon-watch-status
dragon-doctor
dragon-share-status
dragon-report --json
~/dragon/IdeaEngine/scripts/pi-report.sh
~/dragon/IdeaEngine/scripts/healthcheck-pi.sh
~/dragon/IdeaEngine/scripts/update-pi.sh
~/dragon/IdeaEngine/scripts/collect-pi-diagnostics.sh
~/dragon/IdeaEngine/scripts/backup-pi.sh
~/dragon/IdeaEngine/scripts/cleanup-pi.sh
```

What those do:

- `configure-pi-env.sh` creates or updates `.env` from prompts or exported environment variables
- `pi-self-test.sh` validates Pi script syntax and installed shortcut wrappers without touching the running workload
- `pi-refresh-tooling.sh` reinstalls the Pi shortcut commands and then runs the self-test so the local operator toolkit stays current after pulls
- `pi-daily-routine.sh` runs tooling refresh, preflight, and the status dashboard in one read-only operator check
- `pi-evening-routine.sh` captures a share-status bundle, runs backup and cleanup, and prints a final report in one end-of-day flow
- `pi-weekly-routine.sh` refreshes tooling, runs the standard update flow, and finishes with the daily routine snapshot
- `pi-verify-backup.sh` checks that a backup archive is readable and contains the expected snapshot files and volume dumps
- `pi-backup-and-verify.sh` creates a backup archive and immediately verifies that the resulting archive is readable and complete
- `pi-restore-and-verify.sh` verifies a backup archive, restores it, and then waits for the Pi to come back healthy
- `install-pi-aliases.sh` installs shortcut commands like `dragon-report`, `dragon-self-test`, `dragon-refresh-tooling`, `dragon-daily-routine`, `dragon-evening-routine`, `dragon-weekly-routine`, `dragon-health`, `dragon-preflight`, `dragon-start`, `dragon-start-and-wait`, `dragon-stop`, `dragon-stop-and-wait`, `dragon-wait-stopped`, `dragon-restart`, `dragon-restart-and-wait`, `dragon-ensure-running`, `dragon-wait-healthy`, `dragon-update`, `dragon-backup`, `dragon-backup-and-verify`, `dragon-verify-backup`, `dragon-restore-and-verify`, `dragon-diagnostics`, `dragon-firstaid`, `dragon-alert-check`, `dragon-alert-notify`, `dragon-configure-alerts`, `dragon-ops-summary`, `dragon-reinstall-service`, `dragon-tail-logs`, `dragon-status-dashboard`, `dragon-watch-status`, `dragon-doctor`, and `dragon-share-status`
- `pi-uninstall.sh` disables installed services and timers, removes the shortcut commands, and can optionally remove the repo checkout
- `pi-reset-state.sh` preserves the install but clears `.dragon` runtime state, with optional backup-first and diagnostics cleanup
- `pi-firstaid.sh` runs a standard recovery flow: report, diagnostics capture, optional backup, and state reset
- `pi-alert-check.sh` evaluates `pi-report.sh --json` and exits nonzero for unhealthy states so you can plug it into timers, cron, or external monitoring
- `pi-alert-notify.sh` sends an alert payload to a configured webhook and is called automatically by the alert-check service when the check fails
- `configure-pi-alerts.sh` updates alert webhook and threshold settings in `.env` without re-running the full env configurator, including the maximum delayed provider retry window before alerting
- `pi-ops-summary.sh` prints the main service, timer, recovery, backup, and journal commands on one screen
- `pi-reinstall-service.sh` re-renders and reinstalls the current repo's systemd service and timers without running a full update cycle
- `pi-tail-logs.sh` follows the main service journal by default and can include backup/update/alert service logs with `--all`
- `pi-status-dashboard.sh` gives a richer human-readable status page with report highlights, timer state, alert-check result, latest activity, and the next commands to run
- `pi-watch-status.sh` refreshes the status dashboard on an interval so you can monitor the Pi from one terminal
- `pi-service-doctor.sh` interprets the current Pi status and suggests the most likely next commands when service health, timers, or queue state need attention
- `pi-share-status.sh` writes a lightweight support bundle with the report JSON, a plain-text loop/status snapshot, dashboard, doctor output, alert-check output, git status, and recent service logs
- `pi-preflight.sh` checks whether the Pi is ready to start: Docker, Compose, core repo files, credentials, disk space, and installed systemd units
- `pi-start.sh` runs preflight by default and then starts the main systemd service, with optional `--follow`, `--skip-preflight`, or `--compose` modes
- `pi-start-and-wait.sh` starts the Pi service and then waits until it becomes healthy or times out
- `pi-stop.sh` stops the main systemd service or compose stack and can optionally print service status afterward
- `pi-stop-and-wait.sh` stops the Pi service and then waits until it becomes inactive and the status endpoint is down
- `pi-wait-until-stopped.sh` stops the Pi service and then waits until the service is inactive and the status endpoint is down
- `pi-restart.sh` runs preflight by default and then restarts the main systemd service or compose stack, with optional log following
- `pi-restart-and-wait.sh` restarts the Pi service and then waits until it becomes healthy or times out
- `pi-ensure-running.sh` checks whether the service already looks healthy and only starts or restarts when needed
- `pi-wait-until-healthy.sh` waits for endpoints and alert-check health, with timeout and polling controls, and can ensure the service is running first
- `pi-report.sh` prints a concise service health view, including restart/result signals, backup/update/alert timers, worker state, queue, activity, compose, and backup summary, and supports `--json` for machine-readable output
- `healthcheck-pi.sh` verifies Docker, the installed service, `.env`, and the backend health/status endpoints
- `update-pi.sh` optionally backs up first, refuses dirty checkouts by default, then pulls the latest branch, refreshes the service file, restarts the stack, and runs the health check
- `collect-pi-diagnostics.sh` writes a timestamped diagnostics bundle with service state, compose state, logs, and backend snapshots
- `backup-pi.sh` creates a timestamped backup of `.env`, `.dragon`, and Docker volumes
- `restore-pi.sh` restores `.env`, `.dragon`, and Docker volumes from a chosen backup
- `cleanup-pi.sh` prunes old diagnostics folders and unpacked backup directories under `.tmp`

Backup and restore:

```bash
~/dragon/IdeaEngine/scripts/backup-pi.sh
BACKUP_SOURCE=~/dragon/IdeaEngine/.tmp/pi-backups/dragon-pi-backup-YYYYMMDD-HHMMSS.tar.gz \
  ~/dragon/IdeaEngine/scripts/restore-pi.sh
```

Uninstall:

```bash
~/dragon/IdeaEngine/scripts/pi-uninstall.sh
REMOVE_REPO_DIR=true ~/dragon/IdeaEngine/scripts/pi-uninstall.sh
```

Reset runtime state:

```bash
~/dragon/IdeaEngine/scripts/pi-reset-state.sh
RESET_DIAGNOSTICS=true ~/dragon/IdeaEngine/scripts/pi-reset-state.sh
```

First aid recovery:

```bash
dragon-firstaid
COLLECT_DIAGNOSTICS=false BACKUP_BEFORE_RESET=false dragon-firstaid
```

Alert-friendly check:

```bash
dragon-alert-check
ALLOW_HEALTH_STATES=healthy,idle,attention MAX_FAILED_ISSUES=1 dragon-alert-check
MAX_DELAYED_RETRY_MINUTES=15 dragon-alert-check
MAX_PENDING_GITHUB_RETRY_OVERDUE_MINUTES=10 dragon-alert-check
```

Optional webhook notification:

```bash
ALERT_WEBHOOK_URL=https://example.invalid/webhook dragon-alert-notify
```

Webhook payloads now distinguish `provider-backoff`, `ready-github-writeback-retry`, and `overdue-github-writeback-retry` in `alertCause`.

Configure alert settings:

```bash
dragon-configure-alerts
ALERT_WEBHOOK_URL_VALUE=https://example.invalid/webhook PROMPT_IF_MISSING=false dragon-configure-alerts
MAX_DELAYED_RETRY_MINUTES_VALUE=15 PROMPT_IF_MISSING=false dragon-configure-alerts
MAX_PENDING_GITHUB_RETRY_OVERDUE_MINUTES_VALUE=10 PROMPT_IF_MISSING=false dragon-configure-alerts
```

Ops cheat sheet:

```bash
dragon-ops-summary
```

Reinstall service and timers from the current checkout:

```bash
dragon-reinstall-service
INSTALL_UPDATE_TIMER=false INSTALL_ALERT_TIMER=false dragon-reinstall-service
```

Tail logs:

```bash
dragon-tail-logs
dragon-tail-logs --all
```

Status dashboard:

```bash
dragon-status-dashboard
dragon-watch-status --interval 30
dragon-doctor
dragon-share-status
dragon-preflight
dragon-start --follow
dragon-start-and-wait --timeout 600
dragon-stop --status
dragon-stop-and-wait --timeout 120
dragon-wait-stopped --timeout 120
dragon-restart --follow
dragon-restart-and-wait --timeout 600
dragon-ensure-running
dragon-self-test
dragon-refresh-tooling
dragon-daily-routine
dragon-evening-routine
dragon-weekly-routine
dragon-backup-and-verify
dragon-verify-backup
dragon-restore-and-verify
dragon-wait-healthy --timeout 600
```

Notes:

- If the script adds your user to the `docker` group, log out and back in before running Docker without `sudo`.
- `AUTO_START=true` only works once the required credentials are present in `.env`.
- The installed service runs `docker compose up --build` from the repo checkout and restarts automatically on boot.
- The setup script installs shortcut commands into `$HOME/.local/bin` when `scripts/install-pi-aliases.sh` is available.
- Backup and restore stop the service by default to reduce the chance of inconsistent volume snapshots.
- The setup script installs a nightly `dragon-backup.timer` by default.
- Scheduled self-updates are available through `dragon-update.timer`, but installation is opt-in with `INSTALL_UPDATE_TIMER=true`.
- Scheduled alert checks are available through `dragon-alert-check.timer`, but installation is opt-in with `INSTALL_ALERT_TIMER=true`.
- Set `ALERT_WEBHOOK_URL` in `.env` if you want the alert-check service to send webhook notifications on failures.
- Set `MAX_DELAYED_RETRY_MINUTES` in `.env` if you want long provider backoff windows to count as alertable health drift; `0` disables that check.
- Set `MAX_PENDING_GITHUB_RETRY_OVERDUE_MINUTES` in `.env` if you want pending GitHub writeback retries to become alertable once they are overdue; `0` disables that check.
- `dragon-report`, `dragon-status-dashboard`, `dragon-alert-check`, and `dragon-doctor` now surface the backend-owned `waitSignal`, so provider backoff and writeback replay waits use the same wording across UI, Pi tooling, alerts, and GitHub summaries.
- `dragon-report` and `dragon-status-dashboard` also show top-level replay-pressure counts from backend status: `providerBackoffIssueCount` and `overdueWritebackIssueCount`.
- `dragon-alert-check` prints those replay-pressure counts too, and webhook notifications now include them so external monitoring can tell whether replay pressure is isolated or affecting multiple issues.
- `dragon-doctor` uses the same counts in its findings, so provider backoff and overdue writeback diagnoses now say how many issues are affected when that scope is known.
- `update-pi.sh` creates a backup before updating by default and exits if the checkout is dirty unless `ALLOW_DIRTY_WORKTREE=true`.
- `backup-pi.sh` keeps the newest `7` backup archives by default; override with `BACKUP_RETENTION_COUNT`.
- `backup-pi.sh` runs `cleanup-pi.sh` by default after successful backup creation.
