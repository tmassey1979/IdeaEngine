#!/usr/bin/env bash
set -euo pipefail

REPO_DIR="${REPO_DIR:-$HOME/dragon/IdeaEngine}"
ENV_FILE="${ENV_FILE:-$REPO_DIR/.env}"
TEMPLATE_FILE="${TEMPLATE_FILE:-$REPO_DIR/.env.docker.example}"
PROMPT_IF_MISSING="${PROMPT_IF_MISSING:-true}"

OPENAI_API_KEY_VALUE="${OPENAI_API_KEY_VALUE:-${OPENAI_API_KEY:-}}"
GITHUB_TOKEN_VALUE="${GITHUB_TOKEN_VALUE:-${GITHUB_TOKEN:-}}"
GH_TOKEN_VALUE="${GH_TOKEN_VALUE:-${GH_TOKEN:-}}"
DRAGON_GITHUB_OWNER_VALUE="${DRAGON_GITHUB_OWNER_VALUE:-}"
DRAGON_GITHUB_REPO_VALUE="${DRAGON_GITHUB_REPO_VALUE:-}"
DRAGON_RUN_MODE_VALUE="${DRAGON_RUN_MODE_VALUE:-}"
DRAGON_RELEASE_QUARANTINED_ON_START_VALUE="${DRAGON_RELEASE_QUARANTINED_ON_START_VALUE:-}"

fail() {
  echo "$1" >&2
  exit 1
}

ensure_env_file() {
  if [[ -f "${ENV_FILE}" ]]; then
    return
  fi

  [[ -f "${TEMPLATE_FILE}" ]] || fail "Template file not found: ${TEMPLATE_FILE}"
  cp "${TEMPLATE_FILE}" "${ENV_FILE}"
  echo "Created ${ENV_FILE} from ${TEMPLATE_FILE}"
}

prompt_value() {
  local prompt_text="$1"
  local secret="${2:-false}"
  local current_value="${3:-}"
  local input=""

  if [[ "${PROMPT_IF_MISSING}" != "true" ]]; then
    echo "${current_value}"
    return
  fi

  if [[ "${secret}" == "true" ]]; then
    read -r -s -p "${prompt_text}: " input
    echo >&2
  else
    if [[ -n "${current_value}" ]]; then
      read -r -p "${prompt_text} [${current_value}]: " input
    else
      read -r -p "${prompt_text}: " input
    fi
  fi

  if [[ -n "${input}" ]]; then
    echo "${input}"
  else
    echo "${current_value}"
  fi
}

read_existing_value() {
  local key="$1"
  if [[ ! -f "${ENV_FILE}" ]]; then
    return
  fi

  local line
  line="$(grep -E "^${key}=" "${ENV_FILE}" | tail -n 1 || true)"
  echo "${line#*=}"
}

set_env_value() {
  local key="$1"
  local value="$2"
  local tmp_file

  tmp_file="$(mktemp)"
  awk -v key="${key}" -v value="${value}" '
    BEGIN { updated = 0 }
    index($0, key "=") == 1 {
      print key "=" value
      updated = 1
      next
    }
    { print }
    END {
      if (!updated) {
        print key "=" value
      }
    }
  ' "${ENV_FILE}" > "${tmp_file}"
  mv "${tmp_file}" "${ENV_FILE}"
}

main() {
  [[ -d "${REPO_DIR}" ]] || fail "Repo directory not found: ${REPO_DIR}"
  ensure_env_file

  local current_openai current_github current_gh current_owner current_repo current_run_mode current_release
  current_openai="$(read_existing_value OPENAI_API_KEY)"
  current_github="$(read_existing_value GITHUB_TOKEN)"
  current_gh="$(read_existing_value GH_TOKEN)"
  current_owner="$(read_existing_value DRAGON_GITHUB_OWNER)"
  current_repo="$(read_existing_value DRAGON_GITHUB_REPO)"
  current_run_mode="$(read_existing_value DRAGON_RUN_MODE)"
  current_release="$(read_existing_value DRAGON_RELEASE_QUARANTINED_ON_START)"

  local openai github gh owner repo run_mode release
  openai="${OPENAI_API_KEY_VALUE:-$(prompt_value "OpenAI API key" true "${current_openai}")}"
  github="${GITHUB_TOKEN_VALUE:-$(prompt_value "GitHub token (leave blank if using GH_TOKEN)" true "${current_github}")}"
  gh="${GH_TOKEN_VALUE:-$(prompt_value "GH_TOKEN (leave blank if using GITHUB_TOKEN)" true "${current_gh}")}"
  owner="${DRAGON_GITHUB_OWNER_VALUE:-$(prompt_value "GitHub owner" false "${current_owner:-tmassey1979}")}"
  repo="${DRAGON_GITHUB_REPO_VALUE:-$(prompt_value "GitHub repo" false "${current_repo:-IdeaEngine}")}"
  run_mode="${DRAGON_RUN_MODE_VALUE:-$(prompt_value "Run mode" false "${current_run_mode:-github-watch}")}"
  release="${DRAGON_RELEASE_QUARANTINED_ON_START_VALUE:-$(prompt_value "Release quarantined on start (true/false)" false "${current_release:-true}")}"

  set_env_value OPENAI_API_KEY "${openai}"
  set_env_value GITHUB_TOKEN "${github}"
  set_env_value GH_TOKEN "${gh}"
  set_env_value DRAGON_GITHUB_OWNER "${owner}"
  set_env_value DRAGON_GITHUB_REPO "${repo}"
  set_env_value DRAGON_RUN_MODE "${run_mode}"
  set_env_value DRAGON_RELEASE_QUARANTINED_ON_START "${release}"

  echo "Updated ${ENV_FILE}"
}

main "$@"
