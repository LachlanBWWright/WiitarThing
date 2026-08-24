#!/usr/bin/env bash

set -Eeuo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd -- "$SCRIPT_DIR/.." && pwd)"

export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=true

cd "$REPO_ROOT"

require_command() {
  if ! command -v "$1" >/dev/null 2>&1; then
    echo "Required command not found: $1" >&2
    exit 127
  fi
}

require_dotnet() {
  require_command dotnet
}

run_exception_policy() {
  if command -v pwsh >/dev/null 2>&1; then
    pwsh -NoProfile -File "$SCRIPT_DIR/check-exception-policy.ps1"
  elif [[ "${OS:-}" == "Windows_NT" ]] && command -v powershell.exe >/dev/null 2>&1; then
    powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$SCRIPT_DIR/check-exception-policy.ps1"
  else
    echo "PowerShell (pwsh) is required for the exception policy check." >&2
    exit 127
  fi
}
