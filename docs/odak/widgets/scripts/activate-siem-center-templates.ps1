# SIEM Güvenlik Paneli — @widget_templates isActive=true (SIEM_CENTER_TEMPLATE_MAP)
#
# Eksik sablon uyarisi: siem.open-alarms-stat, siem.events-hourly-trend, alarm.recent-table
# Genelde P1 seed isActive=false; bu script panel + designer icin acar.
#
#   .\docs\odak\operationcore\scripts\get-operationcore-token.ps1
#   .\docs\odak\widgets\scripts\smoke-widget-p1-data.ps1
#   .\docs\odak\widgets\scripts\activate-siem-center-templates.ps1
#   .\docs\odak\widgets\scripts\activate-siem-center-templates.ps1 -Force

param(
    [string]$BaseUrl = "http://192.168.20.20:5040",
    [string]$Domain = "odak",
    [switch]$Force
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../../../..")).Path
$tokenFile = "$env:TEMP\operationcore_dg_token.txt"

$token = $null
if (Test-Path $tokenFile) {
    $token = (Get-Content $tokenFile -Raw).Trim()
}
if ([string]::IsNullOrEmpty($token) -and -not [string]::IsNullOrEmpty($env:WIDGET_TOKEN)) {
    $token = $env:WIDGET_TOKEN.Trim()
}
if ([string]::IsNullOrEmpty($token)) {
    $loadTokenScript = Join-Path $repoRoot "docs/odak/operationcore/scripts/load-operationcore-token.ps1"
    if (Test-Path $loadTokenScript) { $token = & $loadTokenScript }
}
if ([string]::IsNullOrEmpty($token)) {
    Write-Host "Token bulunamadi." -ForegroundColor Red
    exit 1
}

$headers = @{
    Authorization   = "Bearer $token"
    "X-Domain-Name" = $Domain
    "Content-Type"  = "application/json"
}

# Mng.Ui/utils/widgets/siemCenterWidgets.ts — SIEM_CENTER_TEMPLATE_MAP
$templateIds = @(
    'siem.events-total-stat',
    'siem.login-failed-stat',
    'siem.open-alarms-stat',
    'siem.events-hourly-trend',
    'alarm.recent-table',
    'siem.scenario-cards'
)

$p1SmokeRequired = @(
    'siem.open-alarms-stat',
    'siem.events-hourly-trend',
    'alarm.recent-table'
)

if (-not $Force) {
    Write-Host "P1 smoke (SIEM panel sablonlari)..." -ForegroundColor Cyan
    $p1Smoke = Join-Path $PSScriptRoot "smoke-widget-p1-data.ps1"
    $smokeOut = & $p1Smoke -BaseUrl $BaseUrl -Domain $Domain -Token $token 2>&1 | Out-String
    Write-Host $smokeOut
    foreach ($tid in $p1SmokeRequired) {
        $pattern = [regex]::Escape($tid) + '.*\sOK'
        if ($smokeOut -notmatch $pattern) {
            Write-Host "  UYARI: $tid smoke OK degil — yine de aktive edilebilir (-Force)" -ForegroundColor Yellow
        }
    }
}

$dataPath = "/data/api/v1/data/@widget_templates"

function Set-TemplateActive([string]$templateId) {
    $filter = [uri]::EscapeDataString("templateId:eq:$templateId")
    $listUri = "$BaseUrl$dataPath`?filter=$filter&limit=1"
    $list = Invoke-RestMethod -Uri $listUri -Headers $headers -Method GET -TimeoutSec 30
    $row = $null
    if ($list -is [array] -and $list.Count -gt 0) { $row = $list[0] }
    elseif ($list.items -and $list.items.Count -gt 0) { $row = $list.items[0] }

    if (-not $row) {
        Write-Host "  SKIP $templateId — kayit yok (setup-widget-templates-datasets.ps1)" -ForegroundColor Yellow
        return
    }

    if ($row.isActive -eq $true) {
        Write-Host "  OK $templateId zaten aktif" -ForegroundColor Gray
        return
    }

    $id = $row.__dataId
    if (-not $id) { $id = $row.dataId }
    $body = @{}
    foreach ($prop in $row.PSObject.Properties) {
        if ($prop.Name -notmatch '^_' -and $prop.Name -ne 'dataId') {
            $body[$prop.Name] = $prop.Value
        }
    }
    $body['isActive'] = $true
    if ($body.ContainsKey('category') -and $body['category'] -is [System.Management.Automation.PSCustomObject]) {
        $cat = $body['category']
        if ($cat.__dataId) { $body['category'] = [string]$cat.__dataId }
        elseif ($cat.dataId) { $body['category'] = [string]$cat.dataId }
    }

    $json = ($body | ConvertTo-Json -Depth 30 -Compress)
    Invoke-RestMethod -Uri "$BaseUrl$dataPath/$id" -Headers $headers -Method PUT -Body $json -TimeoutSec 30 | Out-Null
    Write-Host "  OK $templateId isActive=true" -ForegroundColor Green
}

Write-Host ''
Write-Host "SIEM center template aktivasyonu ($BaseUrl)" -ForegroundColor Cyan
Write-Host ''

foreach ($tid in $templateIds) {
    Set-TemplateActive $tid
}

Write-Host ''
Write-Host "Tamamlandi. UI: /apps/siem-center (Ctrl+F5)" -ForegroundColor Green
