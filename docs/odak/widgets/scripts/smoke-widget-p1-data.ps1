# P1 widget template veri kaynaklari — smoke (Odak API Gateway)
# P0: smoke-widget-p0-data.ps1
#
# Kullanim (repo kokunden):
#   .\docs\odak\operationcore\scripts\get-operationcore-token.ps1
#   .\docs\odak\widgets\scripts\smoke-widget-p1-data.ps1
#   .\docs\odak\widgets\scripts\smoke-widget-p1-data.ps1 -WorkspaceId "<uuid>"

param(
    [string]$BaseUrl = "http://192.168.20.20:5040",
    [string]$Domain = "odak",
    [string]$Token = "",
    [string]$LoadTokenScript = "",
    [string]$WorkspaceId = "",
    [string]$StateId = ""
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../../../..")).Path

if ([string]::IsNullOrEmpty($Token)) {
    $Token = $env:WIDGET_TOKEN
}
if ([string]::IsNullOrEmpty($Token)) {
    if ([string]::IsNullOrEmpty($LoadTokenScript)) {
        $LoadTokenScript = Join-Path $repoRoot "docs/odak/operationcore/scripts/load-operationcore-token.ps1"
    }
    if (Test-Path $LoadTokenScript) {
        $Token = & $LoadTokenScript
    }
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
Write-Host "Widget P1 data smoke ($BaseUrl, domain=$Domain)" -ForegroundColor Cyan
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
        Write-Host "WorkspaceId auto-resolve basarisiz: $($_.Exception.Message)" -ForegroundColor DarkYellow
    }
}

$results = @()

# 1) alarm.severity-distribution-donut — scenarioRollup
$r1 = Invoke-SmokeGet -Label 'alarm.severity-distribution-donut' -Uri "$BaseUrl/alarm/api/v1/alarms/dashboard-snapshot?rangeHours=24&openLimit=5"
if ($r1.Ok) {
    $rollup = @($r1.Data.scenarioRollup)
    Write-Host "  OK $($r1.Label) scenarioRollup=$($rollup.Count)" -ForegroundColor Green
    $results += [pscustomobject]@{ Template = 'alarm.severity-distribution-donut'; Status = 'OK'; Detail = "rollup=$($rollup.Count)" }
}
else {
    Write-Host "  FAIL $($r1.Label) HTTP $($r1.Code)" -ForegroundColor Red
    $results += [pscustomobject]@{ Template = 'alarm.severity-distribution-donut'; Status = 'FAIL'; Detail = $r1.Error }
}

# 2) alarm.recent-table — openAlarms
if ($r1.Ok) {
    $alarms = @($r1.Data.openAlarms)
    Write-Host "  OK alarm.recent-table openAlarms=$($alarms.Count)" -ForegroundColor Green
    $results += [pscustomobject]@{ Template = 'alarm.recent-table'; Status = 'OK'; Detail = "rows=$($alarms.Count)" }
}
else {
    $results += [pscustomobject]@{ Template = 'alarm.recent-table'; Status = 'SKIP'; Detail = 'snapshot failed' }
}

# 3) siem.open-alarms-stat
if ($r1.Ok) {
    Write-Host "  OK siem.open-alarms-stat openTotal=$($r1.Data.openTotal)" -ForegroundColor Green
    $results += [pscustomobject]@{ Template = 'siem.open-alarms-stat'; Status = 'OK'; Detail = "openTotal=$($r1.Data.openTotal)" }
}

# 4) siem.events-hourly-trend
$r4 = Invoke-SmokeGet -Label 'siem.events-hourly-trend' -Uri "$BaseUrl/reactor/api/v1/sec-events/dashboard-summary?rangeHours=24&excludeUnknown=true"
if ($r4.Ok) {
    $hourly = @($r4.Data.hourly)
    Write-Host "  OK $($r4.Label) hourly=$($hourly.Count)" -ForegroundColor Green
    $results += [pscustomobject]@{ Template = 'siem.events-hourly-trend'; Status = 'OK'; Detail = "hourly=$($hourly.Count)" }
}
else {
    Write-Host "  FAIL $($r4.Label) HTTP $($r4.Code)" -ForegroundColor Red
    $results += [pscustomobject]@{ Template = 'siem.events-hourly-trend'; Status = 'FAIL'; Detail = $r4.Error }
}

# 5) siem.recent-events-table
$r5 = Invoke-SmokeGet -Label 'siem.recent-events-table' -Uri "$BaseUrl/reactor/api/v1/sec-events?limit=5&excludeUnknown=true"
if ($r5.Ok) {
    $items = @($r5.Data.items)
    if (-not $items -and $r5.Data -is [array]) { $items = @($r5.Data) }
    Write-Host "  OK $($r5.Label) items=$($items.Count)" -ForegroundColor Green
    $results += [pscustomobject]@{ Template = 'siem.recent-events-table'; Status = 'OK'; Detail = "items=$($items.Count)" }
}
else {
    Write-Host "  FAIL $($r5.Label) HTTP $($r5.Code)" -ForegroundColor Red
    $results += [pscustomobject]@{ Template = 'siem.recent-events-table'; Status = 'FAIL'; Detail = $r5.Error }
}

# 6) di.recent-search-list
$r6 = Invoke-SmokeGet -Label 'di.recent-search-list' -Uri "$BaseUrl/documents/api/v1/resources/search?q=*&skip=0&limit=5"
if ($r6.Ok) {
    $items = @($r6.Data.items)
    Write-Host "  OK $($r6.Label) items=$($items.Count)" -ForegroundColor Green
    $results += [pscustomobject]@{ Template = 'di.recent-search-list'; Status = 'OK'; Detail = "items=$($items.Count)" }
}
else {
    Write-Host "  FAIL $($r6.Label) HTTP $($r6.Code)" -ForegroundColor Red
    $results += [pscustomobject]@{ Template = 'di.recent-search-list'; Status = 'FAIL'; Detail = $r6.Error }
}

# 7-8) MO queryRef - workspaceId gerekir
if ([string]::IsNullOrEmpty($WorkspaceId)) {
    Write-Host '  SKIP oc.sla-breach-stat / oc.open-work-queue-table - WorkspaceId verilmedi' -ForegroundColor Yellow
    $results += [pscustomobject]@{ Template = 'oc.sla-breach-stat'; Status = 'SKIP'; Detail = 'needs -WorkspaceId' }
    $results += [pscustomobject]@{ Template = 'oc.open-work-queue-table'; Status = 'SKIP'; Detail = 'needs -WorkspaceId' }
}
else {
    $asOf = (Get-Date).ToUniversalTime().ToString('o')
    $r7 = Invoke-SmokePost -Label 'oc.sla-breach-stat' -Uri "$BaseUrl/data/api/v1/data/op_work_items/queries/wi_sla_response_breach" -Body @{
        workspaceId = $WorkspaceId
        asOf        = $asOf
    }
    if ($r7.Ok) {
        $total = $r7.Data.total
        if ($null -eq $total -and $r7.Data.items) { $total = @($r7.Data.items).Count }
        Write-Host "  OK $($r7.Label) total=$total" -ForegroundColor Green
        $results += [pscustomobject]@{ Template = 'oc.sla-breach-stat'; Status = 'OK'; Detail = "total=$total" }
    }
    else {
        Write-Host "  FAIL $($r7.Label) HTTP $($r7.Code)" -ForegroundColor Red
        $results += [pscustomobject]@{ Template = 'oc.sla-breach-stat'; Status = 'FAIL'; Detail = $r7.Error }
    }

    $queueBody = @{ workspaceId = $WorkspaceId }
    if (-not [string]::IsNullOrEmpty($StateId)) {
        $queueBody.stateId = $StateId
    }
    else {
        try {
            $wiUri = "$BaseUrl/data/api/v1/data/op_work_items?limit=1&filter=workspaceId:eq:$WorkspaceId"
            $wi = Invoke-RestMethod -Uri $wiUri -Method GET -Headers $headers -TimeoutSec 20
            $wiRow = $null
            if ($wi -is [array] -and $wi.Count -gt 0) { $wiRow = $wi[0] }
            elseif ($wi.items -and $wi.items.Count -gt 0) { $wiRow = $wi.items[0] }
            if ($wiRow -and $wiRow.stateId) {
                if ($wiRow.stateId -is [string]) {
                    $StateId = $wiRow.stateId
                }
                elseif ($wiRow.stateId.__dataId) {
                    $StateId = $wiRow.stateId.__dataId
                }
                if ($StateId) {
                    $queueBody.stateId = $StateId
                    Write-Host "  StateId (auto): $StateId" -ForegroundColor Gray
                }
            }
        }
        catch {
            Write-Host "  StateId auto-resolve basarisiz" -ForegroundColor DarkYellow
        }
    }
    $r8 = Invoke-SmokePost -Label 'oc.open-work-queue-table' -Uri "$BaseUrl/data/api/v1/data/op_work_items/queries/wi_by_workspace_and_state" -Body $queueBody
    if ($r8.Ok) {
        $items = @($r8.Data.items)
        if (-not $items -and ($r8.Data -is [array])) { $items = @($r8.Data) }
        Write-Host "  OK $($r8.Label) items=$($items.Count)" -ForegroundColor Green
        $results += [pscustomobject]@{ Template = 'oc.open-work-queue-table'; Status = 'OK'; Detail = "items=$($items.Count)" }
    }
    else {
        Write-Host "  FAIL $($r8.Label) HTTP $($r8.Code)" -ForegroundColor Red
        $results += [pscustomobject]@{ Template = 'oc.open-work-queue-table'; Status = 'FAIL'; Detail = $r8.Error }
    }
}

Write-Host ''
Write-Host 'Ozet:' -ForegroundColor Cyan
$results | Format-Table -AutoSize

$fail = @($results | Where-Object { $_.Status -eq 'FAIL' }).Count
if ($fail -gt 0) {
    Write-Host ('{0} P1 kontrol basarisiz.' -f $fail) -ForegroundColor Red
    exit 1
}
Write-Host 'P1 smoke tamam (SKIP kayitlari opsiyonel).' -ForegroundColor Green
