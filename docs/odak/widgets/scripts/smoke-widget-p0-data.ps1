# P0 widget template veri kaynaklari — smoke (Odak API Gateway)
#
# Kullanim (repo kokunden):
#   .\docs\odak\operationcore\scripts\get-operationcore-token.ps1
#   .\docs\odak\widgets\scripts\smoke-widget-p0-data.ps1

param(
    [string]$BaseUrl = "http://192.168.20.20:5040",
    [string]$Domain = "odak",
    [string]$Token = "",
    [string]$LoadTokenScript = ""
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
    Authorization  = "Bearer $Token"
    "X-Domain-Name" = $Domain
    "Content-Type"  = "application/json"
}

function Get-JwtPayload {
    param([string]$Jwt)
    $parts = $Jwt.Split('.')
    if ($parts.Count -lt 2) { return $null }
    $payload = $parts[1]
    $pad = 4 - ($payload.Length % 4)
    if ($pad -lt 4) { $payload += ('=' * $pad) }
    try {
        $bytes = [Convert]::FromBase64String($payload.Replace('-', '+').Replace('_', '/'))
        return [Text.Encoding]::UTF8.GetString($bytes) | ConvertFrom-Json
    }
    catch { return $null }
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
Write-Host "Widget P0 data smoke ($BaseUrl, domain=$Domain)" -ForegroundColor Cyan
Write-Host ''

$jwt = Get-JwtPayload -Jwt $Token
$personId = $jwt.mng_person_id
if (-not $personId) { $personId = $jwt.sub }
Write-Host "MngPersonId: $personId" -ForegroundColor Gray
Write-Host ''

$results = @()

# 1) alarm.open-count-stat
$r1 = Invoke-SmokeGet -Label 'alarm.open-count-stat (dashboard-snapshot)' -Uri "$BaseUrl/alarm/api/v1/alarms/dashboard-snapshot?rangeHours=24&openLimit=5"
if ($r1.Ok) {
    $openTotal = $r1.Data.openTotal
    Write-Host "  OK $($r1.Label) openTotal=$openTotal" -ForegroundColor Green
    $results += [pscustomobject]@{ Template = 'alarm.open-count-stat'; Status = 'OK'; Detail = "openTotal=$openTotal" }
}
else {
    Write-Host "  FAIL $($r1.Label) HTTP $($r1.Code)" -ForegroundColor Red
    $results += [pscustomobject]@{ Template = 'alarm.open-count-stat'; Status = 'FAIL'; Detail = $r1.Error }
}

# 2) siem.events-total-stat
$r2 = Invoke-SmokeGet -Label 'siem.events-total-stat (dashboard-summary)' -Uri "$BaseUrl/reactor/api/v1/sec-events/dashboard-summary?rangeHours=24&excludeUnknown=true"
if ($r2.Ok) {
    $total = $r2.Data.eventsTotal
    Write-Host "  OK $($r2.Label) eventsTotal=$total" -ForegroundColor Green
    $results += [pscustomobject]@{ Template = 'siem.events-total-stat'; Status = 'OK'; Detail = "eventsTotal=$total" }
}
else {
    Write-Host "  FAIL $($r2.Label) HTTP $($r2.Code)" -ForegroundColor Red
    $results += [pscustomobject]@{ Template = 'siem.events-total-stat'; Status = 'FAIL'; Detail = $r2.Error }
}

# 3) siem.login-failed-stat
if ($r2.Ok) {
    $loginFailed = $r2.Data.byAction.login_failed
    if ($null -eq $loginFailed) { $loginFailed = 0 }
    Write-Host "  OK siem.login-failed-stat (byAction.login_failed) count=$loginFailed" -ForegroundColor Green
    $results += [pscustomobject]@{ Template = 'siem.login-failed-stat'; Status = 'OK'; Detail = "login_failed=$loginFailed" }
}
else {
    $results += [pscustomobject]@{ Template = 'siem.login-failed-stat'; Status = 'SKIP'; Detail = 'summary failed' }
}

# 4) oc.my-assigned-table
if ($personId) {
    $r4 = Invoke-SmokePost -Label 'oc.my-assigned-table (wi_assigned_open)' -Uri "$BaseUrl/data/api/v1/data/op_work_items/queries/wi_assigned_open" -Body @{ assignee = $personId }
    if ($r4.Ok) {
        $items = @($r4.Data)
        if ($r4.Data.items) { $items = @($r4.Data.items) }
        $count = $items.Count
        Write-Host "  OK $($r4.Label) items=$count" -ForegroundColor Green
        $results += [pscustomobject]@{ Template = 'oc.my-assigned-table'; Status = 'OK'; Detail = "items=$count" }
    }
    else {
        Write-Host "  FAIL $($r4.Label) HTTP $($r4.Code)" -ForegroundColor Red
        $results += [pscustomobject]@{ Template = 'oc.my-assigned-table'; Status = 'FAIL'; Detail = $r4.Error }
    }
}
else {
    Write-Host "  SKIP oc.my-assigned-table - mng_person_id yok" -ForegroundColor Yellow
    $results += [pscustomobject]@{ Template = 'oc.my-assigned-table'; Status = 'SKIP'; Detail = 'no mng_person_id in token' }
}

# 5) di.folder-children-table
$rBoot = Invoke-SmokeGet -Label 'DI bootstrap' -Uri "$BaseUrl/documents/api/v1/resources/bootstrap"
$folderId = $null
if ($rBoot.Ok -and $rBoot.Data.children -and $rBoot.Data.children.items -and $rBoot.Data.children.items.Count -gt 0) {
    $folderId = $rBoot.Data.children.items[0].id
}
elseif ($rBoot.Ok -and $rBoot.Data.tree -and $rBoot.Data.tree.Count -gt 0) {
    $folderId = $rBoot.Data.tree[0].id
}
if ($folderId) {
    $childUri = "$BaseUrl/documents/api/v1/resources/children?parentId=$folderId" + '&limit=5'
    $r5 = Invoke-SmokeGet -Label 'di.folder-children-table (children)' -Uri $childUri
    if ($r5.Ok) {
        $items = @($r5.Data.items)
        if (-not $items -and $r5.Data -is [array]) { $items = @($r5.Data) }
        Write-Host "  OK $($r5.Label) items=$($items.Count)" -ForegroundColor Green
        $results += [pscustomobject]@{ Template = 'di.folder-children-table'; Status = 'OK'; Detail = "items=$($items.Count)" }
    }
    else {
        Write-Host "  FAIL $($r5.Label) HTTP $($r5.Code)" -ForegroundColor Red
        $results += [pscustomobject]@{ Template = 'di.folder-children-table'; Status = 'FAIL'; Detail = $r5.Error }
    }
}
else {
    Write-Host "  SKIP di.folder-children-table - root folder bulunamadi" -ForegroundColor Yellow
    $results += [pscustomobject]@{ Template = 'di.folder-children-table'; Status = 'SKIP'; Detail = 'no root folder' }
}

# 6) di.quick-link-banner — statik, API yok
Write-Host "  OK di.quick-link-banner (statik, API gerekmez)" -ForegroundColor Green
$results += [pscustomobject]@{ Template = 'di.quick-link-banner'; Status = 'OK'; Detail = 'static banner' }

Write-Host ''
Write-Host 'Ozet:' -ForegroundColor Cyan
$results | Format-Table -AutoSize

$fail = @($results | Where-Object { $_.Status -eq 'FAIL' }).Count
if ($fail -gt 0) {
    Write-Host "$fail P0 kontrol basarisiz." -ForegroundColor Red
    exit 1
}
Write-Host 'P0 smoke tamam.' -ForegroundColor Green
