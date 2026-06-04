# SIEM CI gate — parser/registry unit tests (Odak E2E olmadan, PR/push kapisi)
# Kullanım (repo kökünden): .\scripts\ci\test-siem-unit-gate.ps1
param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path
Set-Location $repoRoot

Write-Host "=== SIEM unit gate (MngReactor SecEvents) ===" -ForegroundColor Cyan

dotnet restore MngReactor/MngReactor.sln
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

dotnet build MngReactor/MngReactor.sln --no-restore -c $Configuration
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

dotnet test MngReactor/MngReactor.sln --no-build -c $Configuration --verbosity normal --filter "FullyQualifiedName~Tests.Services.SecEvents"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "`nOK SIEM unit gate PASS" -ForegroundColor Green
exit 0
