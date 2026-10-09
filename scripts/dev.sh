#!/usr/bin/env bash

source "$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)/_common.sh"
require_dotnet

if command -v pwsh >/dev/null 2>&1; then
  exec pwsh -NoProfile -File "$SCRIPT_DIR/dev.ps1" "$@"
elif [[ "${OS:-}" == "Windows_NT" ]] && command -v powershell.exe >/dev/null 2>&1; then
  exec powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$SCRIPT_DIR/dev.ps1" "$@"
else
  dotnet run --project WiitarThing/WiitarThing.csproj --configuration Debug -- "$@"
fi
