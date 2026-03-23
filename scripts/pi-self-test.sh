#!/usr/bin/env bash
set -euo pipefail

REPO_DIR="${REPO_DIR:-$HOME/dragon/IdeaEngine}"
BIN_DIR="${BIN_DIR:-$HOME/.local/bin}"

PASS_COUNT=0
WARN_COUNT=0
FAIL_COUNT=0

pass() {
  PASS_COUNT=$((PASS_COUNT + 1))
  echo "[pass] $1"
}

warn() {
  WARN_COUNT=$((WARN_COUNT + 1))
  echo "[warn] $1"
}

fail() {
  FAIL_COUNT=$((FAIL_COUNT + 1))
  echo "[fail] $1"
}

check_script_syntax() {
  local script_path="$1"
  if bash -n "${script_path}" >/dev/null 2>&1; then
    pass "syntax ok: $(basename "${script_path}")"
  else
    fail "syntax failed: ${script_path}"
  fi
}

check_wrapper() {
  local command_name="$1"
  local expected_script="$2"
  local wrapper_path="${BIN_DIR}/${command_name}"

  if [[ ! -f "${wrapper_path}" ]]; then
    warn "wrapper missing: ${command_name}"
    return
  fi

  if grep -Fq "${REPO_DIR}/scripts/${expected_script}" "${wrapper_path}"; then
    pass "wrapper ok: ${command_name}"
  else
    warn "wrapper does not target expected script: ${command_name}"
  fi
}

print_summary() {
  echo
  echo "Self-test summary: ${PASS_COUNT} pass, ${WARN_COUNT} warn, ${FAIL_COUNT} fail"
  if [[ "${FAIL_COUNT}" -gt 0 ]]; then
    exit 1
  fi
}

main() {
  local -a scripts
  local -a wrappers

  [[ -d "${REPO_DIR}/scripts" ]] || {
    echo "Repo scripts directory not found at ${REPO_DIR}/scripts" >&2
    exit 1
  }

  scripts=(
    "configure-pi-env.sh"
    "configure-pi-alerts.sh"
    "setup-pi.sh"
    "update-pi.sh"
    "healthcheck-pi.sh"
    "pi-preflight.sh"
    "pi-start.sh"
    "pi-start-and-wait.sh"
    "pi-stop.sh"
    "pi-stop-and-wait.sh"
    "pi-wait-until-stopped.sh"
    "pi-restart.sh"
    "pi-restart-and-wait.sh"
    "pi-ensure-running.sh"
    "pi-wait-until-healthy.sh"
    "pi-report.sh"
    "pi-status-dashboard.sh"
    "pi-watch-status.sh"
    "pi-service-doctor.sh"
    "pi-share-status.sh"
    "pi-alert-check.sh"
    "pi-alert-notify.sh"
    "pi-firstaid.sh"
    "pi-reset-state.sh"
    "pi-reinstall-service.sh"
    "pi-tail-logs.sh"
    "pi-uninstall.sh"
    "pi-ops-summary.sh"
    "backup-pi.sh"
    "restore-pi.sh"
    "cleanup-pi.sh"
    "collect-pi-diagnostics.sh"
    "install-pi-aliases.sh"
  )

  wrappers=(
    "dragon-report:pi-report.sh"
    "dragon-self-test:pi-self-test.sh"
    "dragon-health:healthcheck-pi.sh"
    "dragon-preflight:pi-preflight.sh"
    "dragon-start:pi-start.sh"
    "dragon-start-and-wait:pi-start-and-wait.sh"
    "dragon-stop:pi-stop.sh"
    "dragon-stop-and-wait:pi-stop-and-wait.sh"
    "dragon-wait-stopped:pi-wait-until-stopped.sh"
    "dragon-restart:pi-restart.sh"
    "dragon-restart-and-wait:pi-restart-and-wait.sh"
    "dragon-ensure-running:pi-ensure-running.sh"
    "dragon-wait-healthy:pi-wait-until-healthy.sh"
    "dragon-update:update-pi.sh"
    "dragon-backup:backup-pi.sh"
    "dragon-diagnostics:collect-pi-diagnostics.sh"
    "dragon-firstaid:pi-firstaid.sh"
    "dragon-alert-check:pi-alert-check.sh"
    "dragon-alert-notify:pi-alert-notify.sh"
    "dragon-configure-alerts:configure-pi-alerts.sh"
    "dragon-ops-summary:pi-ops-summary.sh"
    "dragon-reinstall-service:pi-reinstall-service.sh"
    "dragon-tail-logs:pi-tail-logs.sh"
    "dragon-status-dashboard:pi-status-dashboard.sh"
    "dragon-watch-status:pi-watch-status.sh"
    "dragon-doctor:pi-service-doctor.sh"
    "dragon-share-status:pi-share-status.sh"
  )

  for script_name in "${scripts[@]}"; do
    check_script_syntax "${REPO_DIR}/scripts/${script_name}"
  done

  for wrapper in "${wrappers[@]}"; do
    check_wrapper "${wrapper%%:*}" "${wrapper#*:}"
  done

  print_summary
}

main "$@"
