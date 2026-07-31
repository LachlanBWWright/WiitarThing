#!/usr/bin/env bash

source "$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)/_common.sh"
require_dotnet

dotnet run --project WiitarThing/WiitarThing.csproj --configuration Debug -- "$@"
