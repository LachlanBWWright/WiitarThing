# Automation commands

Run these commands from the repository root. Each script also works when invoked
by absolute path or from another working directory.

| Command name | Invocation | Purpose |
| --- | --- | --- |
| `dev` | `./scripts/dev.sh` | Build and launch the Debug desktop app without Visual Studio. |
| `dev-build` | `powershell -File .\scripts\dev-build.ps1` | Build the desktop app without launching it. |
| `capture-ui-gallery` | `powershell -File .\scripts\capture-ui-gallery.ps1` | Validate real device-row bindings and capture six UI states without an elevation prompt. |
| `restore` | `./scripts/restore.sh` | Restore solution dependencies. |
| `lint` | `./scripts/lint.sh` | Verify .NET formatting and the exception policy. |
| `test` | `./scripts/test.sh` | Run shared and application workflow xUnit tests in Release mode. |
| `build-prod` | `./scripts/build-prod.sh` | Restore and compile the Release solution. |
| `ci` | `./scripts/ci.sh` | Reproduce the CI restore, four builds, tests, and policy check. |
| `release-smoke` | `./scripts/release-smoke.sh` | Test and publish a self-contained Windows release, then verify its executable. |

Additional arguments are forwarded to the primary `dotnet` command where useful.
For example, `./scripts/test.sh --filter ResultTests` runs a subset of tests.

For a normal Windows developer run:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\dev.ps1
```

The script builds the app and launches the generated executable directly. Useful
options include `-Configuration Release`, `-NoBuild`, and `-CaptureUiGallery`.
Arguments after the script name are passed to the app.

The repository is pinned to the .NET SDK declared in `global.json`. App builds,
launches, and release publishing are intended to run on Windows because the app
targets WinUI and a Windows runtime. The lint and CI commands also require
PowerShell (`pwsh`) for the existing exception-policy check.
