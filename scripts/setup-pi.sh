#!/usr/bin/env bash
set -euo pipefail

REPO_URL="${REPO_URL:-https://github.com/tmassey1979/IdeaEngine.git}"
REPO_BRANCH="${REPO_BRANCH:-feature/github-run-until-idle-sync}"
INSTALL_ROOT="${INSTALL_ROOT:-$HOME/dragon}"
REPO_DIR="${REPO_DIR:-$INSTALL_ROOT/IdeaEngine}"
ENV_FILE="${ENV_FILE:-$REPO_DIR/.env}"
AUTO_START="${AUTO_START:-false}"
INSTALL_SYSTEMD_SERVICE="${INSTALL_SYSTEMD_SERVICE:-true}"
SERVICE_NAME="${SERVICE_NAME:-dragon-idea-engine}"
SERVICE_TEMPLATE="${SERVICE_TEMPLATE:-$REPO_DIR/docker/dragon-compose.service}"
SERVICE_PATH="${SERVICE_PATH:-/etc/systemd/system/${SERVICE_NAME}.service}"
INSTALL_BACKUP_TIMER="${INSTALL_BACKUP_TIMER:-true}"
BACKUP_SERVICE_NAME="${BACKUP_SERVICE_NAME:-dragon-backup}"
BACKUP_SERVICE_TEMPLATE="${BACKUP_SERVICE_TEMPLATE:-$REPO_DIR/docker/dragon-backup.service}"
BACKUP_SERVICE_PATH="${BACKUP_SERVICE_PATH:-/etc/systemd/system/${BACKUP_SERVICE_NAME}.service}"
BACKUP_TIMER_NAME="${BACKUP_TIMER_NAME:-dragon-backup}"
BACKUP_TIMER_TEMPLATE="${BACKUP_TIMER_TEMPLATE:-$REPO_DIR/docker/dragon-backup.timer}"
BACKUP_TIMER_PATH="${BACKUP_TIMER_PATH:-/etc/systemd/system/${BACKUP_TIMER_NAME}.timer}"
INSTALL_UPDATE_TIMER="${INSTALL_UPDATE_TIMER:-false}"
UPDATE_SERVICE_NAME="${UPDATE_SERVICE_NAME:-dragon-update}"
UPDATE_SERVICE_TEMPLATE="${UPDATE_SERVICE_TEMPLATE:-$REPO_DIR/docker/dragon-update.service}"
UPDATE_SERVICE_PATH="${UPDATE_SERVICE_PATH:-/etc/systemd/system/${UPDATE_SERVICE_NAME}.service}"
UPDATE_TIMER_NAME="${UPDATE_TIMER_NAME:-dragon-update}"
UPDATE_TIMER_TEMPLATE="${UPDATE_TIMER_TEMPLATE:-$REPO_DIR/docker/dragon-update.timer}"
UPDATE_TIMER_PATH="${UPDATE_TIMER_PATH:-/etc/systemd/system/${UPDATE_TIMER_NAME}.timer}"

require_command() {
  local command_name="$1"
  if ! command -v "${command_name}" >/dev/null 2>&1; then
    echo "Missing required command: ${command_name}" >&2
    exit 1
  fi
}

require_root() {
  if [[ "${EUID}" -ne 0 ]]; then
    echo "This step requires sudo/root." >&2
    exit 1
  fi
}

install_base_packages() {
  require_root
  export DEBIAN_FRONTEND=noninteractive
  apt-get update
  apt-get install -y --no-install-recommends \
    ca-certificates \
    curl \
    git \
    gnupg \
    lsb-release
}

install_docker() {
  if command -v docker >/dev/null 2>&1; then
    echo "Docker already installed."
    return
  fi

  require_root
  install -m 0755 -d /etc/apt/keyrings
  curl -fsSL https://download.docker.com/linux/debian/gpg -o /etc/apt/keyrings/docker.asc
  chmod a+r /etc/apt/keyrings/docker.asc

  local codename
  codename="$(. /etc/os-release && echo "${VERSION_CODENAME}")"
  echo \
    "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.asc] https://download.docker.com/linux/debian ${codename} stable" \
    > /etc/apt/sources.list.d/docker.list

  apt-get update
  apt-get install -y --no-install-recommends \
    docker-ce \
    docker-ce-cli \
    containerd.io \
    docker-buildx-plugin \
    docker-compose-plugin

  systemctl enable docker
  systemctl start docker
}

install_github_cli() {
  if command -v gh >/dev/null 2>&1; then
    echo "GitHub CLI already installed."
    return
  fi

  require_root
  install -m 0755 -d /etc/apt/keyrings
  curl -fsSL https://cli.github.com/packages/githubcli-archive-keyring.gpg \
    | dd of=/etc/apt/keyrings/githubcli-archive-keyring.gpg
  chmod go+r /etc/apt/keyrings/githubcli-archive-keyring.gpg
  echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/githubcli-archive-keyring.gpg] https://cli.github.com/packages stable main" \
    > /etc/apt/sources.list.d/github-cli.list

  apt-get update
  apt-get install -y --no-install-recommends gh
}

ensure_docker_group() {
  require_root
  local target_user
  target_user="${SUDO_USER:-$USER}"

  if id -nG "${target_user}" | grep -qw docker; then
    echo "User ${target_user} is already in the docker group."
    return
  fi

  usermod -aG docker "${target_user}"
  echo "Added ${target_user} to the docker group. Log out and back in for group membership to fully apply."
}

clone_or_update_repo() {
  mkdir -p "${INSTALL_ROOT}"

  if [[ -d "${REPO_DIR}/.git" ]]; then
    git -C "${REPO_DIR}" fetch origin
  else
    git clone "${REPO_URL}" "${REPO_DIR}"
  fi

  git -C "${REPO_DIR}" checkout "${REPO_BRANCH}"
  git -C "${REPO_DIR}" pull --ff-only origin "${REPO_BRANCH}"
}

ensure_env_file() {
  if [[ -f "${ENV_FILE}" ]]; then
    echo ".env already exists at ${ENV_FILE}"
    return
  fi

  cp "${REPO_DIR}/.env.docker.example" "${ENV_FILE}"
  echo "Created ${ENV_FILE} from .env.docker.example"
  echo "Edit ${ENV_FILE} to set OPENAI_API_KEY and GITHUB_TOKEN or GH_TOKEN before starting the stack."
}

install_systemd_service() {
  if [[ "${INSTALL_SYSTEMD_SERVICE}" != "true" ]]; then
    return
  fi

  if [[ ! -f "${SERVICE_TEMPLATE}" ]]; then
    echo "Missing service template: ${SERVICE_TEMPLATE}" >&2
    exit 1
  fi

  local rendered
  rendered="$(mktemp)"
  sed "s|__REPO_DIR__|${REPO_DIR}|g" "${SERVICE_TEMPLATE}" > "${rendered}"
  sudo install -m 0644 "${rendered}" "${SERVICE_PATH}"
  rm -f "${rendered}"

  sudo systemctl daemon-reload
  sudo systemctl enable "${SERVICE_NAME}.service"
  echo "Installed and enabled systemd service ${SERVICE_NAME}.service"
}

install_backup_timer() {
  if [[ "${INSTALL_BACKUP_TIMER}" != "true" ]]; then
    return
  fi

  if [[ ! -f "${BACKUP_SERVICE_TEMPLATE}" ]]; then
    echo "Missing backup service template: ${BACKUP_SERVICE_TEMPLATE}" >&2
    exit 1
  fi

  if [[ ! -f "${BACKUP_TIMER_TEMPLATE}" ]]; then
    echo "Missing backup timer template: ${BACKUP_TIMER_TEMPLATE}" >&2
    exit 1
  fi

  local rendered_service
  rendered_service="$(mktemp)"
  sed "s|__REPO_DIR__|${REPO_DIR}|g" "${BACKUP_SERVICE_TEMPLATE}" > "${rendered_service}"
  sudo install -m 0644 "${rendered_service}" "${BACKUP_SERVICE_PATH}"
  rm -f "${rendered_service}"

  sudo install -m 0644 "${BACKUP_TIMER_TEMPLATE}" "${BACKUP_TIMER_PATH}"
  sudo systemctl daemon-reload
  sudo systemctl enable "${BACKUP_TIMER_NAME}.timer"
  echo "Installed and enabled backup timer ${BACKUP_TIMER_NAME}.timer"
}

install_update_timer() {
  if [[ "${INSTALL_UPDATE_TIMER}" != "true" ]]; then
    return
  fi

  if [[ ! -f "${UPDATE_SERVICE_TEMPLATE}" ]]; then
    echo "Missing update service template: ${UPDATE_SERVICE_TEMPLATE}" >&2
    exit 1
  fi

  if [[ ! -f "${UPDATE_TIMER_TEMPLATE}" ]]; then
    echo "Missing update timer template: ${UPDATE_TIMER_TEMPLATE}" >&2
    exit 1
  fi

  local rendered_service
  rendered_service="$(mktemp)"
  sed "s|__REPO_DIR__|${REPO_DIR}|g" "${UPDATE_SERVICE_TEMPLATE}" > "${rendered_service}"
  sudo install -m 0644 "${rendered_service}" "${UPDATE_SERVICE_PATH}"
  rm -f "${rendered_service}"

  sudo install -m 0644 "${UPDATE_TIMER_TEMPLATE}" "${UPDATE_TIMER_PATH}"
  sudo systemctl daemon-reload
  sudo systemctl enable "${UPDATE_TIMER_NAME}.timer"
  echo "Installed and enabled update timer ${UPDATE_TIMER_NAME}.timer"
}

print_next_steps() {
  cat <<EOF

Pi bootstrap complete.

Repo: ${REPO_DIR}
Env:  ${ENV_FILE}
Service: ${SERVICE_NAME}.service
Backup timer: ${BACKUP_TIMER_NAME}.timer
Update timer: ${UPDATE_TIMER_NAME}.timer

Next steps:
1. Edit ${ENV_FILE} and set OPENAI_API_KEY plus GITHUB_TOKEN or GH_TOKEN.
2. Authenticate GitHub CLI if you want local gh access: gh auth login
3. Start the stack:
   sudo systemctl start ${SERVICE_NAME}
4. Follow logs:
   sudo journalctl -u ${SERVICE_NAME} -f
5. Check backup timer:
   systemctl list-timers ${BACKUP_TIMER_NAME}.timer
6. Optional: enable scheduled updates during setup with INSTALL_UPDATE_TIMER=true
EOF
}

start_stack_if_requested() {
  if [[ "${AUTO_START}" != "true" ]]; then
    return
  fi

  if grep -Eq '^OPENAI_API_KEY=$' "${ENV_FILE}" || grep -Eq '^(GITHUB_TOKEN|GH_TOKEN)=$' "${ENV_FILE}"; then
    echo "AUTO_START=true was requested, but ${ENV_FILE} still has empty required credentials." >&2
    exit 1
  fi

  cd "${REPO_DIR}"
  if [[ "${INSTALL_SYSTEMD_SERVICE}" == "true" ]]; then
    sudo systemctl start "${SERVICE_NAME}.service"
  else
    docker compose up --build -d
  fi
}

main() {
  echo "Preparing Raspberry Pi host for Dragon Idea Engine..."
  sudo bash -lc "$(declare -f install_base_packages); install_base_packages"
  sudo bash -lc "$(declare -f install_docker); $(declare -f install_base_packages); install_docker"
  sudo bash -lc "$(declare -f install_github_cli); install_github_cli"
  sudo bash -lc "$(declare -f ensure_docker_group); ensure_docker_group"

  require_command git
  clone_or_update_repo
  ensure_env_file
  install_systemd_service
  install_backup_timer
  install_update_timer

  if docker compose version >/dev/null 2>&1; then
    echo "Docker Compose plugin is available."
  else
    echo "Docker Compose plugin is not available." >&2
    exit 1
  fi

  start_stack_if_requested
  print_next_steps
}

main "$@"
