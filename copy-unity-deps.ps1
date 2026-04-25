# copy-unity-deps.ps1
# Called by build-unity-dll.bat.
# Finds System.Text.Json.dll and System.Text.Encodings.Web.dll in the installed
# .NET 8 runtime and copies them to the Unity Plugins folder.

param(
    [string]$Output = "UnityVisualizer\Assets\Plugins"
)

$runtimes = & dotnet --list-runtimes 2>&1
$match = $runtimes |
    Where-Object   { $_ -match "^Microsoft\.NETCore\.App 8\." } |
    Select-Object  -Last 1

if (-not $match) {
    Write-Error "[copy-unity-deps] ERROR: Microsoft.NETCore.App 8.x runtime not found."
    exit 1
}

if ($match -notmatch "Microsoft\.NETCore\.App ([\d\.]+) \[(.+?)\]") {
    Write-Error "[copy-unity-deps] ERROR: Could not parse runtime entry: $match"
    exit 1
}

$version     = $Matches[1]
$basePath    = $Matches[2].Trim()
$runtimePath = Join-Path $basePath $version

Write-Host "[copy-unity-deps] Runtime path: $runtimePath"

$needed = @(
    "System.Text.Json.dll",
    "System.Text.Encodings.Web.dll"
)

foreach ($asm in $needed) {
    $src = Join-Path $runtimePath $asm
    if (Test-Path $src) {
        Copy-Item $src (Join-Path $Output $asm) -Force
        Write-Host "[copy-unity-deps] Copied $asm"
    } else {
        Write-Warning "[copy-unity-deps] $asm not found at $src - skipping"
    }
}

Write-Host "[copy-unity-deps] Done."
