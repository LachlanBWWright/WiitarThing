Param(
    [string[]]$Paths = @("WiitarThing", "Nintroller", "Shared")
)

$ErrorActionPreference = "Stop"

$pattern = "\b(throw|try|catch)\b"
$allowlist = @(
    "Shared/Windows/WinBtStream.cs",   # Stream/base framework boundary
    "WiitarThing/SingleInstance.cs",   # IPC + Win32 boundary
    "WiitarThing/App.xaml.cs",         # crash boundary
    "WiitarThing/Windows/ErrorWindow.xaml.cs" # crash display boundary
)

$matches = @()
foreach ($path in $Paths)
{
    if (-not (Test-Path $path))
    {
        continue
    }

    $result = rg -n --glob "*.cs" --glob "*.vb" $pattern $path
    if ($LASTEXITCODE -eq 0 -and $result)
    {
        $matches += $result
    }
}

if (-not $matches -or $matches.Count -eq 0)
{
    Write-Host "No throw/try/catch usage found in scanned paths."
    exit 0
}

$violations = @()
foreach ($line in $matches)
{
    # ripgrep format: path:line:text
    $firstColon = $line.IndexOf(":")
    if ($firstColon -lt 1)
    {
        continue
    }

    $filePath = $line.Substring(0, $firstColon).Replace('\\', '/')
    $isAllowed = $false

    foreach ($allowed in $allowlist)
    {
        if ($filePath.EndsWith($allowed))
        {
            $isAllowed = $true
            break
        }
    }

    if (-not $isAllowed)
    {
        $violations += $line
    }
}

if ($violations.Count -gt 0)
{
    Write-Host "Exception policy violations found:" -ForegroundColor Red
    $violations | ForEach-Object { Write-Host "  $_" }
    Write-Host ""
    Write-Host "If a location is an approved external/framework boundary, add it to scripts/check-exception-policy.ps1 allowlist."
    exit 1
}

Write-Host "Exception policy check passed."
exit 0
