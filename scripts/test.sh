#!/usr/bin/env bash

source "$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)/_common.sh"
require_dotnet

dotnet test Shared.Tests/Shared.Tests.csproj --configuration Release --verbosity minimal "$@"
