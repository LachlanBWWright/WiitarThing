[Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSAvoidAssignmentToAutomaticVariable', '', Scope='Script', Target='*')]
Param(
    [string[]]$Paths = @("WiitarThing", "Nintroller", "Shared")
)

$ErrorActionPreference = "Stop"

$patterns = @{
    ThrowNewException = "\bthrow\s+new\s+Exception\b"
    EmptyCatch = "catch\s*(\(\s*[^)]*\))?\s*\{\s*\}"
    BroadCatch = "catch\s*\(\s*Exception(?:\s+\w+)?\s*\)"
    NotImplemented = "\bNotImplementedException\b"
}

$allowlistByPattern = @{
    BroadCatch = @(
        "Shared/Windows/WinBtStream.cs",   # Stream + native boundary
        "WiitarThing/SingleInstance.cs",   # IPC + Win32 boundary
        "WiitarThing/App.xaml.cs",         # crash boundary
        "WiitarThing/Windows/ErrorWindow.xaml.cs", # crash display boundary
        "WiitarThing/UserPrefs.cs",        # preferences external API boundary
        "WiitarThing/Components/DeviceControl.xaml.cs", # profile file boundary
        "WiitarThing/Windows/MainWindow.xaml.cs", # UI boundary logging
        "Nintroller/Nintroller.cs"         # subscriber callback boundary
    )
    NotImplemented = @(
        "Nintroller/ControllerStructs.cs", # explicit placeholders pending parser expansion
        "Nintroller/WiiDrums.cs",          # explicit placeholder
        "Nintroller/WiiGuitar.cs"          # explicit placeholder
    )
}

function Get-SourceFiles {
    param([string[]]$ScanPaths)

    $files = New-Object System.Collections.Generic.List[string]
    foreach ($path in $ScanPaths)
    {
        if (-not (Test-Path $path))
        {
            continue
        }

        Get-ChildItem -Path $path -Recurse -File -Include *.cs, *.vb |
            Where-Object {
                $_.FullName -notmatch "[\\/]obj[\\/]" -and
                $_.FullName -notmatch "[\\/]bin[\\/]" -and
                $_.FullName -notmatch "[\\/]TestApp[\\/]"
            } |
            ForEach-Object {
                [void]$files.Add($_.FullName)
            }
    }

    return $files
}

function Get-WorkspaceRelativePath {
    param([string]$AbsolutePath)

    $root = (Get-Location).Path
    $path = [System.IO.Path]::GetFullPath($AbsolutePath)

    if ($path.StartsWith($root, [System.StringComparison]::OrdinalIgnoreCase))
    {
        $trimmed = $path.Substring($root.Length).TrimStart([char]92, [char]47)
        return $trimmed.Replace('\', '/')
    }

    return $path.Replace('\', '/')
}

$policyHits = @()
$allFiles = Get-SourceFiles -ScanPaths $Paths

foreach ($patternName in $patterns.Keys)
{
    $pattern = $patterns[$patternName]
    $patternHits = Select-String -Path $allFiles -Pattern $pattern -AllMatches
    foreach ($hit in $patternHits)
    {
        $relativePath = Get-WorkspaceRelativePath -AbsolutePath $hit.Path
        $lineText = $hit.Line.Trim()
        $policyHits += [PSCustomObject]@{
            Pattern = $patternName
            Path = $relativePath
            Line = $hit.LineNumber
            Text = $lineText
        }
    }
}

if (-not $policyHits -or $policyHits.Count -eq 0)
{
    Write-Host "No exception policy matches found in scanned paths."
    exit 0
}

$violations = @()
foreach ($hit in $policyHits)
{
    $filePath = $hit.Path.Replace('\\', '/')
    $patternName = $hit.Pattern
    $isAllowed = $false

    if ($allowlistByPattern.ContainsKey($patternName))
    {
        foreach ($allowed in $allowlistByPattern[$patternName])
        {
            if ($filePath.EndsWith($allowed, [System.StringComparison]::OrdinalIgnoreCase))
            {
                $isAllowed = $true
                break
            }
        }
    }

    if (-not $isAllowed)
    {
        $violations += $hit
    }
}

if ($violations.Count -gt 0)
{
    Write-Host "Exception policy violations found:" -ForegroundColor Red
    $violations |
        Sort-Object Path, Line, Pattern |
        ForEach-Object {
            Write-Host ("  {0}:{1} [{2}] {3}" -f $_.Path, $_.Line, $_.Pattern, $_.Text)
        }
    Write-Host ""
    Write-Host "If a location is an approved external/framework boundary, add it to scripts/check-exception-policy.ps1 allowlist."
    exit 1
}

Write-Host "Exception policy check passed."
exit 0
