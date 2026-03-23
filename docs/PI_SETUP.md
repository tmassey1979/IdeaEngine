# Raspberry Pi Setup

This repo now includes a bootstrap script for a fresh Raspberry Pi OS or Debian-based host:

```bash
curl -fsSL https://raw.githubusercontent.com/tmassey1979/IdeaEngine/feature/github-run-until-idle-sync/scripts/setup-pi.sh -o setup-pi.sh
chmod +x setup-pi.sh
./setup-pi.sh
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
```

After the script finishes:

1. Edit `.env` and set `OPENAI_API_KEY`.
2. Set either `GITHUB_TOKEN` or `GH_TOKEN`.
3. Start the service:

```bash
sudo systemctl start dragon-idea-engine
sudo journalctl -u dragon-idea-engine -f
```

Routine maintenance:

```bash
~/dragon/IdeaEngine/scripts/healthcheck-pi.sh
~/dragon/IdeaEngine/scripts/update-pi.sh
```

What those do:

- `healthcheck-pi.sh` verifies Docker, the installed service, `.env`, and the backend health/status endpoints
- `update-pi.sh` pulls the latest branch, refreshes the service file, restarts the stack, and runs the health check

Notes:

- If the script adds your user to the `docker` group, log out and back in before running Docker without `sudo`.
- `AUTO_START=true` only works once the required credentials are present in `.env`.
- The installed service runs `docker compose up --build` from the repo checkout and restarts automatically on boot.
