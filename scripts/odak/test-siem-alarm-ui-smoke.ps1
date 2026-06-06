# SIEM + Alarm Merkezi UI smoke — deploy sonrasi API + SPA rotalari
param(
    [string]$Gateway = "http://192.168.20.20:5040",
    [string]$UiBase = "http://192.168.20.20:3000",
    [string]$Domain = "odak"
)

$ErrorActionPreference = "Stop"
$tokenScript = Join-Path $PSScriptRoot "..\tests\MngDataGateway\auth\get-token.ps1"
$token = & $tokenScript -KeeperBaseUrl $Gateway -DomainName $Domain -Username "odak_admin" -Password "Admin123!"
$hdr = @{ Authorization = "Bearer $token"; "X-Domain-Name" = $Domain; "Content-Type" = "application/json" }

function Assert-Status {
    param([string]$Label, [string]$Url, [hashtable]$Headers = @{}, [int[]]$Expected = @(200))
    $code = 0
    try {
        Invoke-WebRequest -Uri $Url -Headers $Headers -Method GET -UseBasicParsing -TimeoutSec 30 | Out-Null
        $code = 200
    } catch {
        if ($_.Exception.Response) {
            $code = [int]$_.Exception.Response.StatusCode
        } else {
            throw "$Label : $($_.Exception.Message)"
        }
    }
    if ($Expected -notcontains $code) {
        throw "$Label HTTP $code (expected $($Expected -join '/')) — $Url"
    }
    Write-Host "  OK $Label ($code)" -ForegroundColor Green
}

$to = (Get-Date).ToUniversalTime().ToString("o")
$from = (Get-Date).ToUniversalTime().AddDays(-7).ToString("o")

Write-Host "=== SIEM + Alarm UI smoke ===" -ForegroundColor Cyan

Write-Host "1) Platform..." -ForegroundColor Cyan
Assert-Status "gateway" "$Gateway/health"
Assert-Status "ui" "$UiBase/"

Write-Host "2) Alarm API (yeni filtreler)..." -ForegroundColor Cyan
Assert-Status "alarms open" "$Gateway/alarm/api/v1/alarms?openOnly=true&limit=5" $hdr
Assert-Status "alarms history" "$Gateway/alarm/api/v1/alarms?openOnly=false&from=$([uri]::EscapeDataString($from))&to=$([uri]::EscapeDataString($to))&limit=5" $hdr
Assert-Status "alarm dashboard" "$Gateway/alarm/api/v1/alarms/dashboard-snapshot?rangeHours=24" $hdr
Assert-Status "rules" "$Gateway/alarm/api/v1/rules" $hdr

Write-Host "3) Reactor sec-events..." -ForegroundColor Cyan
Assert-Status "sec-events query" "$Gateway/reactor/api/v1/sec-events?limit=5" $hdr

Write-Host "4) Lifecycle POST probe (Active alarm)..." -ForegroundColor Cyan
$page = Invoke-RestMethod -Uri "$Gateway/alarm/api/v1/alarms?openOnly=true&limit=20" -Headers $hdr
$active = @($page.items) | Where-Object { $_.status -eq 'Active' -or $_.status -eq 0 } | Select-Object -First 1
if ($active) {
    $id = $active.id
    $ack = Invoke-RestMethod -Uri "$Gateway/alarm/api/v1/alarms/$id/acknowledge" -Method POST -Headers $hdr
    if (-not $ack.context) { Write-Host "  WARN acknowledge: context bos" -ForegroundColor Yellow }
    else { Write-Host "  OK acknowledge + context ($id)" -ForegroundColor Green }
} else {
    Write-Host "  SKIP lifecycle (Active alarm yok)" -ForegroundColor DarkGray
}

Write-Host "5) UI routes..." -ForegroundColor Cyan
$routes = @(
    "/apps/siem-center",
    "/apps/siem-center/events",
    "/apps/alarm-center/alarms",
    "/apps/alarm-center/rules"
)
foreach ($route in $routes) {
    Assert-Status "UI $route" "$UiBase$route"
}

Write-Host "`nOK SIEM + Alarm UI smoke PASS" -ForegroundColor Green
exit 0
