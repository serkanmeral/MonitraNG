#Requires -Version 7.0
<#
.SYNOPSIS
  Publishes the agent and builds a per-machine MSI (WiX 5) for AD GPO distribution.

.EXAMPLE
  .\build-msi.ps1 -SkipFrontend
  msiexec /i artifacts\msi\MngLogs.Agent.msi /qn COLLECTORURL=http://collector:5091 APIKEY=secret
#>
param(
    [string] $Configuration = "Release",
    [string] $Runtime = "win-x64",
    [string] $AgentVersion = "1.0.3",
    [switch] $SkipFrontend,
    [switch] $SkipPublish,
    [string] $PayloadDir = "",
    [string] $MsiOutDir = ""
)

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$repoRoot = Split-Path $root -Parent

if (-not $PayloadDir) {
    $PayloadDir = Join-Path $root "artifacts\agent\$Runtime"
}
if (-not $MsiOutDir) {
    $MsiOutDir = Join-Path $root "artifacts\msi"
}

if (-not $SkipPublish) {
    $publishArgs = @{
        Configuration = $Configuration
        Runtime       = $Runtime
        OutputDir     = $PayloadDir
    }
    if ($SkipFrontend) { $publishArgs.SkipFrontend = $true }
    & (Join-Path $PSScriptRoot "publish-agent.ps1") @publishArgs
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

$exe = Join-Path $PayloadDir "MngLogs.Agent.exe"
if (-not (Test-Path $exe)) {
    Write-Error "Payload missing: $exe"
    exit 1
}

New-Item -ItemType Directory -Path $MsiOutDir -Force | Out-Null
$setupProj = Join-Path $root "Presentation\MngLogs.Agent.Setup\MngLogs.Agent.Setup.wixproj"

Write-Host "==> Building MSI (WiX) version=$AgentVersion"
& dotnet build $setupProj `
    -c $Configuration `
    -p:PayloadDir=$PayloadDir `
    -p:AgentVersion=$AgentVersion `
    -p:OutputPath=$MsiOutDir\ `
    -p:BaseOutputPath=$MsiOutDir\
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$msi = Get-ChildItem -Path $MsiOutDir -Filter "*.msi" -Recurse | Sort-Object LastWriteTime -Descending | Select-Object -First 1
if (-not $msi) {
    Write-Error "MSI not found under $MsiOutDir"
    exit 1
}

$final = Join-Path $MsiOutDir "MngLogs.Agent-$AgentVersion.msi"
Copy-Item $msi.FullName $final -Force
Write-Host ""
Write-Host "MSI ready: $final"
Write-Host ""
Write-Host "Silent install (admin / GPO-style):"
Write-Host "  msiexec /i `"$final`" /qn /L*v `"$env:TEMP\mnglogs-agent-install.log`" COLLECTORURL=http://192.168.20.8:5091 APIKEY=your-key"
Write-Host ""
Write-Host "Uninstall:"
Write-Host "  msiexec /x `"$final`" /qn"
exit 0
