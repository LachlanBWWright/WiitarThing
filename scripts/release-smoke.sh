#!/usr/bin/env bash

source "$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)/_common.sh"
require_dotnet

runtime_identifier="${RUNTIME_IDENTIFIER:-win-x64}"
release_dir="${RELEASE_DIR:-artifacts/release/WiitarThing-$runtime_identifier}"

dotnet restore WiitarThing.sln --verbosity normal
dotnet test Shared.Tests/Shared.Tests.csproj \
  --configuration Release \
  --no-restore \
  --verbosity minimal
dotnet publish WiitarThing/WiitarThing.csproj \
  --configuration Release \
  --runtime "$runtime_identifier" \
  --self-contained true \
  --output "$release_dir" \
  --verbosity minimal \
  "$@"

if [[ ! -f "$release_dir/WiitarThing.exe" ]]; then
  echo "Release smoke test failed: $release_dir/WiitarThing.exe was not produced." >&2
  exit 1
fi

echo "Release smoke test passed: $release_dir"
