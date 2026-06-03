# Operatör smoke — checkpoint C4/C5 API + oc_live (tarayıcı öncesi otomasyon)
param(
    [string]$Gateway = "http://192.168.20.20:5040",
    [string]$UiBase = "http://192.168.20.20:3000",
    [string]$Domain = "odak"
)

$ErrorActionPreference = "Stop"
$tokenScript = Join-Path $PSScriptRoot "..\tests\MngDataGateway\auth\get-token.ps1"
$token = & $tokenScript -KeeperBaseUrl $Gateway -DomainName $Domain -Username "odak_admin" -Password "Admin123!"
$hdr = @{ Authorization = "Bearer $token"; "X-Domain-Name" = $Domain }

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

Write-Host "1) Platform health..." -ForegroundColor Cyan
Assert-Status "gateway health" "$Gateway/health"
Assert-Status "oc_live" "$UiBase/api/operations/v1/health/live"
Assert-Status "ui root" "$UiBase/"

Write-Host "2) Alarm API..." -ForegroundColor Cyan
Assert-Status "GET alarms" "$Gateway/alarm/api/v1/alarms?openOnly=true&limit=5" $hdr
Assert-Status "GET rules" "$Gateway/alarm/api/v1/rules" $hdr

Write-Host "3) Workflow API..." -ForegroundColor Cyan
Assert-Status "GET approvals" "$Gateway/workflow/api/v1/approvals?status=Pending" $hdr

Write-Host "4) OC admin routes (SPA shell)..." -ForegroundColor Cyan
$routes = @(
    "/apps/operation-core/admin/approvals",
    "/apps/operation-core/admin/alarms",
    "/apps/operation-core/admin/alarm-rules"
)
foreach ($route in $routes) {
    Assert-Status "UI $route" "$UiBase$route"
}

Write-Host "`nOK operator smoke (API + oc_live + admin routes)" -ForegroundColor Green
Write-Host "Manuel: odak_admin ile menuden onay/alarmlar/kurallar ekranlarini acin." -ForegroundColor DarkGray
exit 0
