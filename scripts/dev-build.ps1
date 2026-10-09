[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release', 'DebugLB', 'ReleaseLB')]
    [string]$Configuration = 'Debug'
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Set-Location $repoRoot

& dotnet build '.\WiitarThing\WiitarThing.csproj' --configuration $Configuration
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

Write-Host "Build complete: WiitarThing\bin\$Configuration\net10.0-windows10.0.26100.0\win-x64\WiitarThing.exe"
