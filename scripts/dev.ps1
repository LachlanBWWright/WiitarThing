[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release', 'DebugLB', 'ReleaseLB')]
    [string]$Configuration = 'Debug',
    [switch]$NoBuild,
    [switch]$CaptureUiGallery,
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$AppArguments = @()
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$project = Join-Path $repoRoot 'WiitarThing\WiitarThing.csproj'
$targetFramework = 'net10.0-windows10.0.26100.0'
$exe = Join-Path $repoRoot "WiitarThing\bin\$Configuration\$targetFramework\win-x64\WiitarThing.exe"

Set-Location $repoRoot

if ($CaptureUiGallery -or ($AppArguments -contains '--capture-ui-gallery')) {
    & (Join-Path $PSScriptRoot 'capture-ui-gallery.ps1') -Configuration $Configuration -NoBuild:$NoBuild
    return
}

if (-not $NoBuild) {
    Write-Host "Building WiitarThing ($Configuration)..."
    & dotnet build $project --configuration $Configuration
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet build failed with exit code $LASTEXITCODE"
    }
}

if (-not (Test-Path -LiteralPath $exe)) {
    throw "Executable not found: $exe. Run without -NoBuild."
}

$arguments = @($AppArguments)
if ($CaptureUiGallery -and -not ($arguments -contains '--capture-ui-gallery')) {
    $arguments += '--capture-ui-gallery'
}

Write-Host "Launching $exe"
if ($arguments.Count -gt 0) {
    Start-Process -FilePath $exe -ArgumentList $arguments -WorkingDirectory (Split-Path $exe)
} else {
    Start-Process -FilePath $exe -WorkingDirectory (Split-Path $exe)
}
