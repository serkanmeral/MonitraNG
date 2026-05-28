# Operation Core — SW-3a smoke: Keeper token (odak_admin) → MO authorized probe
# Ref: docs/odak/operationcore/mngoperations/SCHEDULED_WORK_ITEMS.md §4.1
#
# Usage (repo root):
#   .\docs\odak\operationcore\scripts\smoke-sw-scheduler-keeper-token.ps1
#   .\docs\odak\operationcore\scripts\smoke-sw-scheduler-keeper-token.ps1 -TestMoHealth

param(
    [string]$KeeperBaseUrl = "http://192.168.20.20:5001",
    [string]$KeeperTokenPath = "/api/auth/token",
    [string]$GatewayBaseUrl = "http://192.168.20.20:5040",
    [string]$DomainName = "odak",
    [string]$Username = "odak_admin",
    [string]$Password = "Admin123!",
    [switch]$TestMoHealth
)

$ErrorActionPreference = "Stop"

Write-Host ""
Write-Host "SW-3a smoke — Keeper token (Operation Core scheduler service account)" -ForegroundColor Cyan
Write-Host ""

$tokenUri = "$($KeeperBaseUrl.TrimEnd('/'))$KeeperTokenPath"
$body = @{ username = $Username; password = $Password; domain = $DomainName } | ConvertTo-Json -Compress

Write-Host "1) POST $tokenUri" -ForegroundColor Yellow
Write-Host "   Domain: $DomainName  User: $Username"

$responseJson = curl.exe -s -w "`nHTTP:%{http_code}" -X POST `
    -H "Content-Type: application/json" `
    -d $body `
    $tokenUri 2>&1 | Out-String

$lines = ($responseJson.Trim() -split "`n")
$httpLine = $lines | Where-Object { $_ -match '^HTTP:' } | Select-Object -Last 1
$httpCode = if ($httpLine) { ($httpLine -replace 'HTTP:', '').Trim() } else { "?" }
$jsonPart = ($lines | Where-Object { $_ -notmatch '^HTTP:' }) -join "`n"

if ($httpCode -ne "200") {
    Write-Host "   HATA HTTP $httpCode" -ForegroundColor Red
    Write-Host $jsonPart
    exit 1
}

$tokenResponse = $jsonPart | ConvertFrom-Json
$accessToken = $tokenResponse.accessToken
if ([string]::IsNullOrEmpty($accessToken)) { $accessToken = $tokenResponse.access_token }
if ([string]::IsNullOrEmpty($accessToken)) {
    Write-Host "   HATA: accessToken yok" -ForegroundColor Red
    exit 1
}

Write-Host "   OK — token alindi ($($accessToken.Length) karakter)" -ForegroundColor Green

if ($TestMoHealth) {
    $moHealth = "$($GatewayBaseUrl.TrimEnd('/'))/operations/api/v1/health"
    Write-Host ""
    Write-Host "2) GET $moHealth (Bearer token)" -ForegroundColor Yellow
    $healthOut = curl.exe -s -w "`nHTTP:%{http_code}" -H "Authorization: Bearer $accessToken" $moHealth 2>&1 | Out-String
    $hLines = ($healthOut.Trim() -split "`n")
    $hCode = ($hLines | Where-Object { $_ -match '^HTTP:' } | Select-Object -Last 1) -replace 'HTTP:', ''
    $hBody = ($hLines | Where-Object { $_ -notmatch '^HTTP:' }) -join "`n"
    if ($hCode -eq "200") {
        Write-Host "   OK — MO health $hCode" -ForegroundColor Green
        if ($hBody.Length -lt 500) { Write-Host "   $hBody" -ForegroundColor Gray }
    } else {
        Write-Host "   Uyari — MO health HTTP $hCode (gateway/MO ayakta mi?)" -ForegroundColor Yellow
        Write-Host $hBody
    }
}

Write-Host ""
Write-Host "SW-3a smoke tamam. MngScheduler WorkItemScheduleOrchestration.ServiceAccount ayni degerleri kullanir." -ForegroundColor Cyan
Write-Host ""
