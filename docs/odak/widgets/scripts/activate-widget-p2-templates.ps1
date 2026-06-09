# P2 widget sablonlarini isActive=true yap (yalnizca smoke OK olanlar)
#
#   .\docs\odak\widgets\scripts\activate-widget-p2-templates.ps1
#   .\docs\odak\widgets\scripts\activate-widget-p2-templates.ps1 -Force  # smoke atla (dikkat)

param(
    [string]$BaseUrl = "http://192.168.20.20:5040",
    [string]$Domain = "odak",
    [string]$WorkspaceId = "",
    [switch]$Force
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../../../..")).Path
$loadTokenScript = Join-Path $repoRoot "docs/odak/operationcore/scripts/load-operationcore-token.ps1"

$token = $env:WIDGET_TOKEN
if ([string]::IsNullOrEmpty($token) -and (Test-Path $loadTokenScript)) {
    $token = & $loadTokenScript
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

$ready = @()

if ($Force) {
    $ready = @('alarm.trend-area', 'oc.work-items-by-state', 'di.recent-updates-list', 'di.draft-count-stat')
    Write-Host "Force modu — tum P2 sablonlar hedefleniyor." -ForegroundColor Yellow
}
else {
    Write-Host "P2 smoke calistiriliyor..." -ForegroundColor Cyan
    $p2Smoke = Join-Path $PSScriptRoot "smoke-widget-p2-data.ps1"
    $smokeOut = & $p2Smoke -BaseUrl $BaseUrl -Domain $Domain -WorkspaceId $WorkspaceId 2>&1 | Out-String
    Write-Host $smokeOut
    if ($smokeOut -match 'alarm\.trend-area.*\sOK') { $ready += 'alarm.trend-area' }
    if ($smokeOut -match 'oc\.work-items-by-state.*\sOK') { $ready += 'oc.work-items-by-state' }
    if ($smokeOut -match 'di\.recent-updates-list.*\sOK') { $ready += 'di.recent-updates-list' }
    if ($smokeOut -match 'di\.draft-count-stat.*\sOK') { $ready += 'di.draft-count-stat' }
}

if ($ready.Count -eq 0) {
    Write-Host "Aktive edilecek P2 sablon yok (backend BLOCK)." -ForegroundColor Yellow
    exit 0
}

$dataPath = "/data/api/v1/data/@widget_templates"

Write-Host ''
Write-Host "P2 template aktivasyonu ($($ready.Count) sablon)" -ForegroundColor Cyan

foreach ($tid in $ready) {
    $filter = [uri]::EscapeDataString("templateId:eq:$tid")
    $listUri = "$BaseUrl$dataPath`?filter=$filter&limit=1"
    $list = Invoke-RestMethod -Uri $listUri -Headers $headers -Method GET -TimeoutSec 30
    $row = $null
    if ($list -is [array] -and $list.Count -gt 0) { $row = $list[0] }
    elseif ($list.items -and $list.items.Count -gt 0) { $row = $list.items[0] }

    if (-not $row) {
        Write-Host "  SKIP $tid — kayit yok" -ForegroundColor Yellow
        continue
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
    Write-Host "  OK $tid isActive=true" -ForegroundColor Green
}

Write-Host ''
Write-Host "Tamamlandi." -ForegroundColor Green
