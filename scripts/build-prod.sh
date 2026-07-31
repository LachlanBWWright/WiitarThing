#!/usr/bin/env bash

source "$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)/_common.sh"
require_dotnet

dotnet restore WiitarThing.sln --verbosity normal
dotnet build WiitarThing.sln --configuration Release --no-restore --verbosity minimal "$@"
