# Automation commands

Run these commands from the repository root. Each script also works when invoked
by absolute path or from another working directory.

| Command name | Invocation | Purpose |
| --- | --- | --- |
| `dev` | `./scripts/dev.sh` | Build and launch the Debug desktop app. |
| `restore` | `./scripts/restore.sh` | Restore solution dependencies. |
| `lint` | `./scripts/lint.sh` | Verify .NET formatting and the exception policy. |
| `test` | `./scripts/test.sh` | Run the shared xUnit tests in Release mode. |
| `build-prod` | `./scripts/build-prod.sh` | Restore and compile the Release solution. |
| `ci` | `./scripts/ci.sh` | Reproduce the CI restore, four builds, tests, and policy check. |
| `release-smoke` | `./scripts/release-smoke.sh` | Test and publish a self-contained Windows release, then verify its executable. |

Additional arguments are forwarded to the primary `dotnet` command where useful.
For example, `./scripts/test.sh --filter ResultTests` runs a subset of tests.

The repository is pinned to the .NET SDK declared in `global.json`. App builds,
launches, and release publishing are intended to run on Windows because the app
targets WinUI and a Windows runtime. The lint and CI commands also require
PowerShell (`pwsh`) for the existing exception-policy check.
