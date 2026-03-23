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
sudo systemctl start dragon-idea-engine
sudo journalctl -u dragon-idea-engine -f
```

3. Verify the nightly backup timer:

```bash
systemctl list-timers dragon-backup.timer
```

4. Optional: verify the scheduled update timer if you enabled it:

```bash
systemctl list-timers dragon-update.timer
```

Routine maintenance:

```bash
~/dragon/IdeaEngine/scripts/pi-report.sh
~/dragon/IdeaEngine/scripts/healthcheck-pi.sh
~/dragon/IdeaEngine/scripts/update-pi.sh
~/dragon/IdeaEngine/scripts/collect-pi-diagnostics.sh
~/dragon/IdeaEngine/scripts/backup-pi.sh
~/dragon/IdeaEngine/scripts/cleanup-pi.sh
```

What those do:

- `configure-pi-env.sh` creates or updates `.env` from prompts or exported environment variables
- `pi-report.sh` prints a concise service, backup/update timer, worker, queue, activity, compose, and backup summary
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

Notes:

- If the script adds your user to the `docker` group, log out and back in before running Docker without `sudo`.
- `AUTO_START=true` only works once the required credentials are present in `.env`.
- The installed service runs `docker compose up --build` from the repo checkout and restarts automatically on boot.
- Backup and restore stop the service by default to reduce the chance of inconsistent volume snapshots.
- The setup script installs a nightly `dragon-backup.timer` by default.
- Scheduled self-updates are available through `dragon-update.timer`, but installation is opt-in with `INSTALL_UPDATE_TIMER=true`.
- `update-pi.sh` creates a backup before updating by default and exits if the checkout is dirty unless `ALLOW_DIRTY_WORKTREE=true`.
- `backup-pi.sh` keeps the newest `7` backup archives by default; override with `BACKUP_RETENTION_COUNT`.
- `backup-pi.sh` runs `cleanup-pi.sh` by default after successful backup creation.
