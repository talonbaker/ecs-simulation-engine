<#
.SYNOPSIS
    Build and run the ECS Simulation CLI, saving output to sim-runs/.

.DESCRIPTION
    Builds ECSCli in Release mode, runs it with the given arguments, and writes
    the report output to sim-runs/run-<timestamp>.txt.  The sim-runs/ directory
    is gitignored so output files are never committed.

    By default runs in quiet mode (report only, no live snapshots).
    Pass -Snapshots to include live state snapshots in the output.

.PARAMETER Duration
    Game-seconds to simulate.  Default: 86400 (one full game-day).

.PARAMETER TimeScale
    Simulation speed multiplier.  Default: uses SimConfig.json value.

.PARAMETER Ticks
    Run for exactly this many ticks instead of a duration.

.PARAMETER Snapshots
    Include live snapshots in the output (off by default — very verbose).

.PARAMETER NoReport
    Skip the end-of-run balancing report.

.PARAMETER ExtraArgs
    Any additional flags passed verbatim to ECSCli.

.EXAMPLE
    .\scripts\run-sim.ps1
    .\scripts\run-sim.ps1 -Duration 172800
    .\scripts\run-sim.ps1 -Ticks 100000 -Snapshots
#>

param(
    [double]  $Duration  = 86400,   # one game-day
    [float]   $TimeScale = 0,       # 0 = use SimConfig.json default
    [long]    $Ticks     = 0,       # 0 = use Duration
    [switch]  $Snapshots,           # include live snapshots (verbose)
    [switch]  $NoReport,
    [string]  $ExtraArgs = ""
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# ── UTF-8 output so box-drawing and block characters survive the pipe ──────────
$OutputEncoding = [Console]::OutputEncoding = [System.Text.Encoding]::UTF8

# ── Paths ─────────────────────────────────────────────────────────────────────
$RepoRoot  = Split-Path $PSScriptRoot -Parent
$CliProj   = Join-Path $RepoRoot "ECSCli\ECSCli.csproj"
$RunsDir   = Join-Path $RepoRoot "sim-runs"
$Timestamp = Get-Date -Format "yyyy-MM-dd_HH-mm-ss"
$OutFile   = Join-Path $RunsDir "run-$Timestamp.txt"

# ── Build ─────────────────────────────────────────────────────────────────────
Write-Host "Building ECSCli (Release)..." -ForegroundColor Cyan
dotnet build $CliProj -c Release -v quiet
if ($LASTEXITCODE -ne 0) { Write-Error "Build failed."; exit 1 }

# ── Assemble args ─────────────────────────────────────────────────────────────
$cliArgs = @()

if ($Ticks -gt 0) {
    $cliArgs += "--ticks"; $cliArgs += "$Ticks"
} else {
    $cliArgs += "--duration"; $cliArgs += "$Duration"
}

if ($TimeScale -gt 0)  { $cliArgs += "--timescale"; $cliArgs += "$TimeScale" }
if (-not $Snapshots)   { $cliArgs += "--quiet" }        # quiet by default
if ($NoReport)         { $cliArgs += "--no-report" }
if ($ExtraArgs)        { $cliArgs += $ExtraArgs -split " " }

$argString = $cliArgs -join " "

# ── Run ───────────────────────────────────────────────────────────────────────
Write-Host ""
Write-Host "Running simulation..." -ForegroundColor Cyan
Write-Host "  Args    : $argString"
Write-Host "  Output  : $OutFile"
Write-Host ""

New-Item -ItemType Directory -Force -Path $RunsDir | Out-Null

dotnet run --project $CliProj -c Release -- @cliArgs 2>&1 |
    Tee-Object -Variable simOutput
$simOutput | Out-File -FilePath $OutFile -Encoding utf8

if ($LASTEXITCODE -ne 0) {
    Write-Warning "Simulation exited with code $LASTEXITCODE"
}

Write-Host ""
Write-Host "Saved to: $OutFile" -ForegroundColor Green
