<#
.SYNOPSIS
  DI-T runner — T-0 fixture + T-1 matrix + T-2 inheritance.
.EXAMPLE
  .\runner.ps1
  .\runner.ps1 -Gateway http://localhost:5040
  .\runner.ps1 -SkipT2
#>
param(
    [string]$Gateway = "http://localhost:5040",
    [string]$DomainName = "odak",
    [switch]$SkipPermissions,
    [switch]$SkipT1,
    [switch]$SkipT2
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$failed = $false

Write-Host "=== DI-T runner ===" -ForegroundColor Cyan
Write-Host "Gateway: $Gateway"

Write-Host "`n[T-0] Ensure fixture..." -ForegroundColor Yellow
& (Join-Path $root "fixtures\Ensure-DiAuthFixture.ps1") -Gateway $Gateway -DomainName $DomainName
if ($LASTEXITCODE -and $LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

if ($SkipPermissions) {
    Write-Host "SkipPermissions set — done." -ForegroundColor Yellow
    exit 0
}

if (-not $SkipT1) {
    Write-Host "`n[T-1] Permission matrix..." -ForegroundColor Yellow
    & (Join-Path $root "suites\permissions\test-permission-matrix.ps1") `
        -Gateway $Gateway -DomainName $DomainName -SkipFixtureEnsure
    if ($LASTEXITCODE -and $LASTEXITCODE -ne 0) { $failed = $true }
}

if (-not $SkipT2) {
    Write-Host "`n[T-2] Inheritance / cache..." -ForegroundColor Yellow
    & (Join-Path $root "suites\permissions\test-inheritance.ps1") `
        -Gateway $Gateway -DomainName $DomainName -SkipFixtureEnsure
    if ($LASTEXITCODE -and $LASTEXITCODE -ne 0) { $failed = $true }
}

if ($failed) { exit 1 }
exit 0
