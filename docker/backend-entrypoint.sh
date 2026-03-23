#!/usr/bin/env bash
set -euo pipefail

ROOT="${DRAGON_ROOT:-/workspace}"
STATUS_PREFIX="${DRAGON_STATUS_PREFIX:-http://+:5078/}"
STATUS_FILE="${DRAGON_STATUS_FILE:-/workspace/.dragon/status/runtime-status.json}"
RUN_MODE="${DRAGON_RUN_MODE:-watch}"
POLL_SECONDS="${DRAGON_POLL_SECONDS:-30}"
MAX_PASSES="${DRAGON_MAX_PASSES:-10}"
IDLE_PASSES="${DRAGON_IDLE_PASSES:-2}"
MAX_CYCLES="${DRAGON_MAX_CYCLES:-100}"
GITHUB_OWNER="${DRAGON_GITHUB_OWNER:-}"
GITHUB_REPO="${DRAGON_GITHUB_REPO:-}"
SYNC_GITHUB="${DRAGON_SYNC_GITHUB:-false}"
RELEASE_QUARANTINED_ON_START="${DRAGON_RELEASE_QUARANTINED_ON_START:-false}"
STATUS_PID=""

cleanup() {
  if [[ -n "${STATUS_PID}" ]]; then
    kill "${STATUS_PID}" 2>/dev/null || true
    wait "${STATUS_PID}" 2>/dev/null || true
  fi
}

trap cleanup EXIT INT TERM

if [[ ! -d "${ROOT}" ]]; then
  echo "Dragon root directory does not exist: ${ROOT}" >&2
  exit 1
fi

echo "Starting Dragon status server on ${STATUS_PREFIX}"
dotnet /app/Dragon.Backend.Cli.dll serve-status --root "${ROOT}" --prefix "${STATUS_PREFIX}" --snapshot-file "${STATUS_FILE}" &
STATUS_PID=$!

base_args=(--root "${ROOT}")
status_args=(--status-out "${STATUS_FILE}")

run_worker() {
  case "${RUN_MODE}" in
    watch)
      dotnet /app/Dragon.Backend.Cli.dll run-watch \
        "${base_args[@]}" \
        "${status_args[@]}" \
        --poll-seconds "${POLL_SECONDS}" \
        --max-passes "${MAX_PASSES}" \
        --idle-passes "${IDLE_PASSES}" \
        --max-cycles "${MAX_CYCLES}"
      ;;
    polling)
      dotnet /app/Dragon.Backend.Cli.dll run-polling \
        "${base_args[@]}" \
        "${status_args[@]}" \
        --max-passes "${MAX_PASSES}" \
        --idle-passes "${IDLE_PASSES}" \
        --max-cycles "${MAX_CYCLES}"
      ;;
    idle)
      dotnet /app/Dragon.Backend.Cli.dll run-until-idle \
        "${base_args[@]}" \
        "${status_args[@]}" \
        --max-cycles "${MAX_CYCLES}"
      ;;
    github-watch)
      if [[ -z "${GITHUB_OWNER}" || -z "${GITHUB_REPO}" ]]; then
        echo "DRAGON_GITHUB_OWNER and DRAGON_GITHUB_REPO are required for github-watch mode." >&2
        return 1
      fi

      github_args=(
        --owner "${GITHUB_OWNER}"
        --repo "${GITHUB_REPO}"
      )

      if [[ "${SYNC_GITHUB}" == "true" ]]; then
        github_args+=(--sync-github)
      fi

      dotnet /app/Dragon.Backend.Cli.dll github-run-watch \
        "${github_args[@]}" \
        "${base_args[@]}" \
        "${status_args[@]}" \
        --poll-seconds "${POLL_SECONDS}" \
        --max-passes "${MAX_PASSES}" \
        --idle-passes "${IDLE_PASSES}" \
        --max-cycles "${MAX_CYCLES}"
      ;;
    github-polling)
      if [[ -z "${GITHUB_OWNER}" || -z "${GITHUB_REPO}" ]]; then
        echo "DRAGON_GITHUB_OWNER and DRAGON_GITHUB_REPO are required for github-polling mode." >&2
        return 1
      fi

      github_args=(
        --owner "${GITHUB_OWNER}"
        --repo "${GITHUB_REPO}"
      )

      if [[ "${SYNC_GITHUB}" == "true" ]]; then
        github_args+=(--sync-github)
      fi

      dotnet /app/Dragon.Backend.Cli.dll github-run-polling \
        "${github_args[@]}" \
        "${base_args[@]}" \
        "${status_args[@]}" \
        --max-passes "${MAX_PASSES}" \
        --idle-passes "${IDLE_PASSES}" \
        --max-cycles "${MAX_CYCLES}"
      ;;
    github-idle)
      if [[ -z "${GITHUB_OWNER}" || -z "${GITHUB_REPO}" ]]; then
        echo "DRAGON_GITHUB_OWNER and DRAGON_GITHUB_REPO are required for github-idle mode." >&2
        return 1
      fi

      github_args=(
        --owner "${GITHUB_OWNER}"
        --repo "${GITHUB_REPO}"
      )

      if [[ "${SYNC_GITHUB}" == "true" ]]; then
        github_args+=(--sync-github)
      fi

      dotnet /app/Dragon.Backend.Cli.dll github-run-until-idle \
        "${github_args[@]}" \
        "${base_args[@]}" \
        "${status_args[@]}" \
        --max-cycles "${MAX_CYCLES}"
      ;;
    *)
      echo "Unsupported DRAGON_RUN_MODE: ${RUN_MODE}" >&2
      return 1
      ;;
  esac
}

echo "Starting Dragon worker in ${RUN_MODE} mode"

if [[ "${RELEASE_QUARANTINED_ON_START}" == "true" ]]; then
  case "${RUN_MODE}" in
    github-watch|github-polling|github-idle)
      if [[ -z "${GITHUB_OWNER}" || -z "${GITHUB_REPO}" ]]; then
        echo "DRAGON_GITHUB_OWNER and DRAGON_GITHUB_REPO are required to release quarantined work in GitHub modes." >&2
        exit 1
      fi

      echo "Releasing quarantined workflows before worker start"
      release_args=(--owner "${GITHUB_OWNER}" --repo "${GITHUB_REPO}" "${base_args[@]}" "${status_args[@]}")
      if [[ "${SYNC_GITHUB}" == "true" ]]; then
        release_args+=(--sync-github)
      fi
      dotnet /app/Dragon.Backend.Cli.dll github-release-quarantined "${release_args[@]}"
      ;;
    *)
      echo "Releasing quarantined workflows before worker start"
      dotnet /app/Dragon.Backend.Cli.dll release-quarantined "${base_args[@]}" "${status_args[@]}"
      ;;
  esac
fi

run_worker
