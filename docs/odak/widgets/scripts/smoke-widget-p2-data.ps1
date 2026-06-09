# P2 widget template veri kaynaklari — smoke (Odak API Gateway)
# P2 sablonlar backend hazir olunca activate-widget-p2-templates.ps1 ile acilir.
#
# Kullanim:
#   .\docs\odak\operationcore\scripts\get-operationcore-token.ps1
#   .\docs\odak\widgets\scripts\smoke-widget-p2-data.ps1
#   .\docs\odak\widgets\scripts\smoke-widget-p2-data.ps1 -WorkspaceId "<uuid>"

param(
    [string]$BaseUrl = "http://192.168.20.20:5040",
    [string]$Domain = "odak",
    [string]$Token = "",
    [string]$LoadTokenScript = "",
    [string]$WorkspaceId = ""
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../../../..")).Path

if ([string]::IsNullOrEmpty($Token)) { $Token = $env:WIDGET_TOKEN }
if ([string]::IsNullOrEmpty($Token)) {
    if ([string]::IsNullOrEmpty($LoadTokenScript)) {
        $LoadTokenScript = Join-Path $repoRoot "docs/odak/operationcore/scripts/load-operationcore-token.ps1"
    }
    if (Test-Path $LoadTokenScript) { $Token = & $LoadTokenScript }
}
if ([string]::IsNullOrEmpty($Token)) {
    Write-Host "Token bulunamadi." -ForegroundColor Red
    exit 1
}

$headers = @{
    Authorization   = "Bearer $Token"
    "X-Domain-Name" = $Domain
    "Content-Type"  = "application/json"
}

function Invoke-SmokeGet {
    param([string]$Label, [string]$Uri)
    try {
        $r = Invoke-RestMethod -Uri $Uri -Method GET -Headers $headers -TimeoutSec 30
        return @{ Ok = $true; Label = $Label; Data = $r }
    }
    catch {
        $code = $null
        try { $code = [int]$_.Exception.Response.StatusCode } catch { }
        return @{ Ok = $false; Label = $Label; Code = $code; Error = $_.Exception.Message }
    }
}

function Invoke-SmokePost {
    param([string]$Label, [string]$Uri, [object]$Body)
    $json = ($Body | ConvertTo-Json -Compress)
    try {
        $r = Invoke-RestMethod -Uri $Uri -Method POST -Headers $headers -Body $json -TimeoutSec 30
        return @{ Ok = $true; Label = $Label; Data = $r }
    }
    catch {
        $code = $null
        try { $code = [int]$_.Exception.Response.StatusCode } catch { }
        return @{ Ok = $false; Label = $Label; Code = $code; Error = $_.Exception.Message }
    }
}

Write-Host ''
Write-Host "Widget P2 data smoke ($BaseUrl, domain=$Domain)" -ForegroundColor Cyan
Write-Host ''

if ([string]::IsNullOrEmpty($WorkspaceId)) {
    try {
        $wsUri = "$BaseUrl/data/api/v1/data/op_workspaces?limit=1"
        $ws = Invoke-RestMethod -Uri $wsUri -Method GET -Headers $headers -TimeoutSec 20
        $first = $null
        if ($ws -is [array] -and $ws.Count -gt 0) { $first = $ws[0] }
        elseif ($ws.items -and $ws.items.Count -gt 0) { $first = $ws.items[0] }
        if ($first) {
            $WorkspaceId = $first.__dataId
            if (-not $WorkspaceId) { $WorkspaceId = $first.dataId }
            Write-Host "WorkspaceId (auto): $WorkspaceId" -ForegroundColor Gray
        }
    }
    catch {
        Write-Host "WorkspaceId auto-resolve basarisiz" -ForegroundColor DarkYellow
    }
}

$results = @()

# 1) alarm.trend-area — mngalarm:alarms/trend-buckets
$r1 = Invoke-SmokeGet -Label 'alarm.trend-area' -Uri "$BaseUrl/alarm/api/v1/alarms/trend-buckets?rangeHours=24"
if ($r1.Ok) {
    $items = @($r1.Data.items)
    if (-not $items -and $r1.Data.buckets) { $items = @($r1.Data.buckets) }
    Write-Host "  OK $($r1.Label) buckets=$($items.Count)" -ForegroundColor Green
    $results += [pscustomobject]@{ Template = 'alarm.trend-area'; Status = 'OK'; Detail = "buckets=$($items.Count)" }
}
else {
    $detail = if ($r1.Code -eq 404) { 'API yok (404)' } else { "HTTP $($r1.Code)" }
    Write-Host "  BLOCK $($r1.Label) $detail" -ForegroundColor Yellow
    $results += [pscustomobject]@{ Template = 'alarm.trend-area'; Status = 'BLOCK'; Detail = $detail }
}

# 2) oc.work-items-by-state — wi_count_by_state
if ([string]::IsNullOrEmpty($WorkspaceId)) {
    Write-Host '  SKIP oc.work-items-by-state - WorkspaceId yok' -ForegroundColor Yellow
    $results += [pscustomobject]@{ Template = 'oc.work-items-by-state'; Status = 'SKIP'; Detail = 'needs -WorkspaceId' }
}
else {
    $r2 = Invoke-SmokePost -Label 'oc.work-items-by-state' -Uri "$BaseUrl/data/api/v1/data/op_work_items/queries/wi_count_by_state" -Body @{
        workspaceId = $WorkspaceId
    }
    if ($r2.Ok) {
        $items = @($r2.Data.items)
        Write-Host "  OK $($r2.Label) states=$($items.Count)" -ForegroundColor Green
        $results += [pscustomobject]@{ Template = 'oc.work-items-by-state'; Status = 'OK'; Detail = "states=$($items.Count)" }
    }
    else {
        $detail = if ($r2.Code -eq 404) { 'query yok (404)' } else { "HTTP $($r2.Code)" }
        Write-Host "  BLOCK $($r2.Label) $detail" -ForegroundColor Yellow
        $results += [pscustomobject]@{ Template = 'oc.work-items-by-state'; Status = 'BLOCK'; Detail = $detail }
    }
}

# 3) di.recent-updates-list — mngdocument:resources/recent
$r3 = Invoke-SmokeGet -Label 'di.recent-updates-list' -Uri "$BaseUrl/documents/api/v1/resources/recent?limit=5"
if ($r3.Ok) {
    $items = @($r3.Data.items)
    Write-Host "  OK $($r3.Label) items=$($items.Count)" -ForegroundColor Green
    $results += [pscustomobject]@{ Template = 'di.recent-updates-list'; Status = 'OK'; Detail = "items=$($items.Count)" }
}
else {
    $detail = if ($r3.Code -eq 404) { 'API yok (404)' } else { "HTTP $($r3.Code)" }
    Write-Host "  BLOCK $($r3.Label) $detail" -ForegroundColor Yellow
    $results += [pscustomobject]@{ Template = 'di.recent-updates-list'; Status = 'BLOCK'; Detail = $detail }
}

# 4) di.draft-count-stat — mngdocument:resources/drafts
$r4 = Invoke-SmokeGet -Label 'di.draft-count-stat' -Uri "$BaseUrl/documents/api/v1/resources/drafts?limit=1"
if ($r4.Ok) {
    $total = $r4.Data.total
    if ($null -eq $total -and $r4.Data.items) { $total = @($r4.Data.items).Count }
    Write-Host "  OK $($r4.Label) total=$total" -ForegroundColor Green
    $results += [pscustomobject]@{ Template = 'di.draft-count-stat'; Status = 'OK'; Detail = "total=$total" }
}
else {
    $detail = if ($r4.Code -eq 404) { 'API yok (404)' } else { "HTTP $($r4.Code)" }
    Write-Host "  BLOCK $($r4.Label) $detail" -ForegroundColor Yellow
    $results += [pscustomobject]@{ Template = 'di.draft-count-stat'; Status = 'BLOCK'; Detail = $detail }
}

Write-Host ''
Write-Host 'Ozet:' -ForegroundColor Cyan
$results | Format-Table -AutoSize

$ok = @($results | Where-Object { $_.Status -eq 'OK' }).Count
$block = @($results | Where-Object { $_.Status -in @('BLOCK', 'FAIL') }).Count
Write-Host ''
if ($ok -eq $results.Count) {
    Write-Host 'P2 smoke tamam — activate-widget-p2-templates.ps1 calistirilabilir.' -ForegroundColor Green
    exit 0
}
Write-Host "P2: $ok/$($results.Count) hazir, $block backend bekliyor (BLOCK/SKIP normal)." -ForegroundColor Yellow
exit 0
