#!/usr/bin/env bash
set -euo pipefail

REPO_DIR="${REPO_DIR:-$HOME/dragon/IdeaEngine}"
BIN_DIR="${BIN_DIR:-$HOME/.local/bin}"

install_wrapper() {
  local command_name="$1"
  local script_name="$2"
  local wrapper_path="${BIN_DIR}/${command_name}"

  cat > "${wrapper_path}" <<EOF
#!/usr/bin/env bash
set -euo pipefail
exec "${REPO_DIR}/scripts/${script_name}" "\$@"
EOF

  chmod +x "${wrapper_path}"
  echo "Installed ${command_name} -> ${REPO_DIR}/scripts/${script_name}"
}

print_path_hint() {
  case ":${PATH}:" in
    *":${BIN_DIR}:"*)
      echo "${BIN_DIR} is already on PATH."
      ;;
    *)
      echo
      echo "Add ${BIN_DIR} to PATH if needed:"
      echo "  export PATH=\"${BIN_DIR}:\$PATH\""
      ;;
  esac
}

main() {
  [[ -d "${REPO_DIR}/scripts" ]] || {
    echo "Repo scripts directory not found at ${REPO_DIR}/scripts" >&2
    exit 1
  }

  mkdir -p "${BIN_DIR}"

  install_wrapper "dragon-report" "pi-report.sh"
  install_wrapper "dragon-health" "healthcheck-pi.sh"
  install_wrapper "dragon-preflight" "pi-preflight.sh"
  install_wrapper "dragon-start" "pi-start.sh"
  install_wrapper "dragon-stop" "pi-stop.sh"
  install_wrapper "dragon-restart" "pi-restart.sh"
  install_wrapper "dragon-ensure-running" "pi-ensure-running.sh"
  install_wrapper "dragon-update" "update-pi.sh"
  install_wrapper "dragon-backup" "backup-pi.sh"
  install_wrapper "dragon-diagnostics" "collect-pi-diagnostics.sh"
  install_wrapper "dragon-firstaid" "pi-firstaid.sh"
  install_wrapper "dragon-alert-check" "pi-alert-check.sh"
  install_wrapper "dragon-alert-notify" "pi-alert-notify.sh"
  install_wrapper "dragon-configure-alerts" "configure-pi-alerts.sh"
  install_wrapper "dragon-ops-summary" "pi-ops-summary.sh"
  install_wrapper "dragon-reinstall-service" "pi-reinstall-service.sh"
  install_wrapper "dragon-tail-logs" "pi-tail-logs.sh"
  install_wrapper "dragon-status-dashboard" "pi-status-dashboard.sh"
  install_wrapper "dragon-watch-status" "pi-watch-status.sh"
  install_wrapper "dragon-doctor" "pi-service-doctor.sh"
  install_wrapper "dragon-share-status" "pi-share-status.sh"

  print_path_hint
}

main "$@"
