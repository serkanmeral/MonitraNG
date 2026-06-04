# SIEM B3 — rule pack seed smoke (idempotent skip + metadata doğrulama)
param(
    [string]$Gateway = "http://192.168.20.20:5040",
    [string]$Domain = "odak",
    [string]$PackageId = "siem-mvp-v1"
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path
$seedScript = Join-Path $PSScriptRoot "seed-siem-alarm-rule-pack.ps1"

Write-Host "=== SIEM B3 rule pack seed smoke ===" -ForegroundColor Cyan

& $seedScript -Gateway $Gateway -Domain $Domain -PackageId $PackageId
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$tokenScript = Join-Path $PSScriptRoot "..\tests\MngDataGateway\auth\get-token.ps1"
$token = & $tokenScript -KeeperBaseUrl $Gateway -DomainName $Domain -Username "odak_admin" -Password "Admin123!"
$hdr = @{ Authorization = "Bearer $token"; "X-Domain-Name" = $Domain }
$rules = @(Invoke-RestMethod -Uri "$Gateway/alarm/api/v1/rules" -Headers $hdr)

$packRules = @($rules) | Where-Object {
    $_.metadata -and $_.metadata.packageId -eq $PackageId
}
$scenarios = @($packRules | ForEach-Object { $_.metadata.scenarioId } | Sort-Object -Unique)
if ($scenarios.Count -lt 7) {
    Write-Host "FAIL: Beklenen 7 senaryo, bulunan=$($scenarios.Count) (packRules=$($packRules.Count))" -ForegroundColor Red
    exit 1
}

$u1 = $packRules | Where-Object { $_.metadata.scenarioId -eq "U1" } | Select-Object -First 1
if (-not $u1 -or $u1.metadata.threatTechniqueId -ne "T1110.001") {
    Write-Host "FAIL: U1 MITRE metadata eksik veya hatali" -ForegroundColor Red
    exit 1
}

Write-Host "   OK scenarios=$($scenarios.Count) U1 technique=$($u1.metadata.threatTechniqueId)" -ForegroundColor Green

& $seedScript -Gateway $Gateway -Domain $Domain -PackageId $PackageId | Out-Null
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "`nOK SIEM B3 rule pack seed smoke PASS" -ForegroundColor Green
exit 0
