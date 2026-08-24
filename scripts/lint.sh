#!/usr/bin/env bash

source "$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)/_common.sh"
require_dotnet

dotnet format WiitarThing.sln --verify-no-changes --verbosity minimal "$@"
run_exception_policy
