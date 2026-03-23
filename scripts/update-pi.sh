#!/usr/bin/env bash
set -euo pipefail

REPO_DIR="${REPO_DIR:-$HOME/dragon/IdeaEngine}"
REPO_BRANCH="${REPO_BRANCH:-feature/github-run-until-idle-sync}"
SERVICE_NAME="${SERVICE_NAME:-dragon-idea-engine}"
INSTALL_SYSTEMD_SERVICE="${INSTALL_SYSTEMD_SERVICE:-true}"
SERVICE_TEMPLATE="${SERVICE_TEMPLATE:-$REPO_DIR/docker/dragon-compose.service}"
SERVICE_PATH="${SERVICE_PATH:-/etc/systemd/system/${SERVICE_NAME}.service}"
RUN_HEALTHCHECK="${RUN_HEALTHCHECK:-true}"

require_command() {
  local command_name="$1"
  if ! command -v "${command_name}" >/dev/null 2>&1; then
    echo "Missing required command: ${command_name}" >&2
    exit 1
  fi
}

require_repo() {
  if [[ ! -d "${REPO_DIR}/.git" ]]; then
    echo "Repository not found at ${REPO_DIR}" >&2
    exit 1
  fi
}

refresh_service_file() {
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
}

main() {
  require_command git
  require_command docker
  require_repo

  echo "Updating Dragon Idea Engine in ${REPO_DIR}..."
  git -C "${REPO_DIR}" fetch origin
  git -C "${REPO_DIR}" checkout "${REPO_BRANCH}"
  git -C "${REPO_DIR}" pull --ff-only origin "${REPO_BRANCH}"

  refresh_service_file

  if [[ "${INSTALL_SYSTEMD_SERVICE}" == "true" ]]; then
    echo "Restarting ${SERVICE_NAME}.service..."
    sudo systemctl restart "${SERVICE_NAME}.service"
    sudo systemctl --no-pager --full status "${SERVICE_NAME}.service" || true
  else
    echo "Restarting Docker Compose stack directly..."
    (
      cd "${REPO_DIR}"
      docker compose down
      docker compose up --build -d
    )
  fi

  if [[ "${RUN_HEALTHCHECK}" == "true" ]]; then
    "${REPO_DIR}/scripts/healthcheck-pi.sh"
  fi
}

main "$@"
