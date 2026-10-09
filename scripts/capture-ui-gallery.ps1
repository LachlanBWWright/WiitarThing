Param(
    [ValidateSet('Debug', 'Release', 'DebugLB', 'ReleaseLB')]
    [string]$Configuration = 'Debug',
    [switch]$NoBuild
)

$ErrorActionPreference = 'Stop'
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$project = Join-Path $workspace 'WiitarThing/WiitarThing.csproj'
if (-not $NoBuild) {
    & dotnet build $project -c $Configuration -v minimal
    if ($LASTEXITCODE -ne 0) { throw 'Gallery application build failed.' }
}

$buildDirectory = Join-Path $workspace "WiitarThing/bin/$Configuration/net10.0-windows10.0.26100.0/win-x64"
$galleryDirectory = Join-Path $workspace 'artifacts/ui-gallery-app'
$captureDirectory = Join-Path $workspace 'artifacts/ui-gallery'
if (-not (Test-Path -LiteralPath (Join-Path $buildDirectory 'WiitarThing.exe'))) {
    throw "Build output is missing: $buildDirectory"
}

# Preserve normal build output and its administrator manifest. The copied app
# only renders fixtures and uses an asInvoker manifest for unattended UI checks.
New-Item -ItemType Directory -Force -Path $galleryDirectory | Out-Null
Get-ChildItem -LiteralPath $buildDirectory | Copy-Item -Destination $galleryDirectory -Recurse -Force
$manifestPath = Join-Path $galleryDirectory 'gallery.manifest'
$manifest = [IO.File]::ReadAllText((Join-Path $workspace 'WiitarThing/app.manifest'))
$manifest = $manifest.Replace('<requestedExecutionLevel level="requireAdministrator" uiAccess="false" />',
    '<requestedExecutionLevel level="asInvoker" uiAccess="false" />')
[IO.File]::WriteAllText($manifestPath, $manifest)

$sdkBin = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits/10/bin'
$manifestTool = Get-ChildItem -LiteralPath $sdkBin -Directory |
    Sort-Object Name -Descending |
    ForEach-Object { Join-Path $_.FullName 'x64/mt.exe' } |
    Where-Object { Test-Path -LiteralPath $_ } |
    Select-Object -First 1
if (-not $manifestTool) { throw 'Windows SDK x64 mt.exe is required for gallery capture.' }
$galleryExe = Join-Path $galleryDirectory 'WiitarThing.exe'
& $manifestTool '-nologo' '-manifest' $manifestPath "-outputresource:$galleryExe;#1"
if ($LASTEXITCODE -ne 0) { throw 'Failed to set the gallery executable manifest.' }

$expectedStates = @(
    'empty', 'detected-devices', 'connected-controllers', 'driver-warning', 'sync-in-progress',
    'disconnected-controller'
)
$started = Get-Date
[IO.File]::WriteAllText((Join-Path $galleryDirectory 'WiitarStartup.log'), '')
$process = Start-Process -FilePath $galleryExe -ArgumentList '--capture-ui-gallery' -WindowStyle Hidden -PassThru
if (-not $process.WaitForExit(60000)) {
    Stop-Process -Id $process.Id -Force
    throw 'UI gallery capture timed out after 60 seconds.'
}
if ($process.ExitCode -ne 0) {
    $logPath = Join-Path $galleryDirectory 'WiitarStartup.log'
    if (Test-Path -LiteralPath $logPath) { Get-Content -LiteralPath $logPath -Tail 30 | Write-Output }
    throw "UI gallery capture failed with exit code $($process.ExitCode)."
}
foreach ($state in $expectedStates) {
    $screenshot = Get-Item -LiteralPath (Join-Path $captureDirectory "$state.png")
    if ($screenshot.LastWriteTime -lt $started -or $screenshot.Length -eq 0) {
        throw "Gallery screenshot was not freshly captured: $state"
    }
}
Write-Output "UI gallery passed: $($expectedStates.Count) fresh screenshots and runtime control checks."
