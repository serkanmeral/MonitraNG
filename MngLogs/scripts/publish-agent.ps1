#Requires -Version 7.0
<#
.SYNOPSIS
  Publishes MngLogs.Agent for machine-wide Windows Service / future MSI (GPO) layout.

.DESCRIPTION
  Output is a folder suitable for copying to Program Files and registering as a service.
  Default: win-x64 self-contained (no separate .NET runtime GPO prerequisite).

  AD / GPO notes (P0.2 MSI will follow the same rules):
  - Per-machine install only (Program Files + LocalSystem).
  - No interactive UI during install; config via system.json / MSI properties / MST.
  - Data lives under %ProgramData%\MngLogs\Agent (survives upgrade/uninstall of binaries).
#>
param(
    [string] $Configuration = "Release",
    [string] $Runtime = "win-x64",
    [switch] $FrameworkDependent,
    [switch] $SkipFrontend,
    [string] $OutputDir = ""
)

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$agentProj = Join-Path $root "Presentation\MngLogs.Agent\MngLogs.Agent.csproj"

if (-not $OutputDir) {
    $OutputDir = Join-Path $root "artifacts\agent\$Runtime"
}

if (-not $SkipFrontend) {
    Write-Host "==> Building frontend (wwwroot)"
    & (Join-Path $PSScriptRoot "build-frontend.ps1")
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

if (Test-Path $OutputDir) {
    Remove-Item $OutputDir -Recurse -Force
}
New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null

$selfContained = -not $FrameworkDependent
Write-Host "==> dotnet publish ($Configuration, $Runtime, self-contained=$selfContained)"
$publishArgs = @(
    "publish", $agentProj,
    "-c", $Configuration,
    "-r", $Runtime,
    "--self-contained", ($selfContained.ToString().ToLowerInvariant()),
    "-o", $OutputDir,
    "-p:PublishSingleFile=false",
    "-p:DebugType=None",
    "-p:DebugSymbols=false"
)
& dotnet @publishArgs
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$exe = Join-Path $OutputDir "MngLogs.Agent.exe"
if (-not (Test-Path $exe)) {
    Write-Error "Publish succeeded but MngLogs.Agent.exe not found at $exe"
    exit 1
}

Write-Host ""
Write-Host "Publish OK: $OutputDir"
Write-Host "Next (elevated):"
Write-Host "  .\scripts\install-windows-service.ps1 -SourceDir `"$OutputDir`" -CollectorUrl http://collector:5091 -ApiKey '***'"
exit 0
