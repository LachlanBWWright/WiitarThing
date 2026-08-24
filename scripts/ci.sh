#!/usr/bin/env bash

source "$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)/_common.sh"
require_dotnet

dotnet restore WiitarThing.sln --verbosity normal

for configuration in Debug Release DebugLB ReleaseLB; do
  dotnet build WiitarThing.sln \
    --configuration "$configuration" \
    --no-restore \
    --verbosity minimal
done

dotnet test Shared.Tests/Shared.Tests.csproj \
  --configuration Release \
  --no-build \
  --verbosity minimal

run_exception_policy
