# D3 — SIEM Güvenlik Paneli @dashboards kaydi (surfaceKind=siem-center)
#
# Kullanim (repo kokunden):
#   .\docs\odak\operationcore\scripts\get-operationcore-token.ps1
#   .\docs\odak\widgets\scripts\setup-siem-center-dashboard.ps1
#
# Dogrulama:
#   .\docs\odak\widgets\scripts\smoke-siem-center-dashboard.ps1

param(
    [string]$BaseUrl = "http://192.168.20.20:5040",
    [switch]$WhatIf = $false
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../../../..")).Path
$seedFile = Join-Path $repoRoot "docs/odak/widgets/datasets/siem_center_dashboard_seed.json"
$loadTokenScript = Join-Path $repoRoot "docs/odak/operationcore/scripts/load-operationcore-token.ps1"

$token = $env:WIDGET_TOKEN
if ([string]::IsNullOrEmpty($token) -and (Test-Path $loadTokenScript)) {
    $token = & $loadTokenScript
}
if ([string]::IsNullOrEmpty($token)) {
    Write-Host "Token bulunamadi. Once get-operationcore-token.ps1 calistirin." -ForegroundColor Red
    exit 1
}

$headers = @{
    Authorization  = "Bearer $token"
    "Content-Type" = "application/json"
}

$dataPath = "/data/api/v1/data/@dashboards"
$seed = Get-Content $seedFile -Raw -Encoding UTF8 | ConvertFrom-Json
$body = $seed.record

function Get-RowId($row) {
    if ($null -eq $row) { return $null }
    if ($row.__dataId) { return [string]$row.__dataId }
    if ($row.dataId) { return [string]$row.dataId }
    return $null
}

Write-Host ''
Write-Host "SIEM center dashboard seed ($BaseUrl)" -ForegroundColor Cyan
Write-Host ''

$slug = $body.slug
$listUri = "$BaseUrl$dataPath`?filter=slug:eq:$slug&limit=1"
$list = Invoke-RestMethod -Uri $listUri -Headers $headers -Method GET -TimeoutSec 30
$existing = $null
if ($list -is [array] -and $list.Count -gt 0) { $existing = $list[0] }
elseif ($list.items -and $list.items.Count -gt 0) { $existing = $list.items[0] }

$json = ($body | ConvertTo-Json -Depth 30 -Compress)

if ($existing) {
    $id = Get-RowId $existing
    Write-Host "Mevcut kayit bulundu (slug=$slug, id=$id) — PUT sync" -ForegroundColor Yellow
    if ($WhatIf) {
        Write-Host "WhatIf PUT $id" -ForegroundColor DarkYellow
    }
    else {
        Invoke-RestMethod -Uri "$BaseUrl$dataPath/$id" -Headers $headers -Method PUT -Body $json -TimeoutSec 30 | Out-Null
        Write-Host "  OK layout.meta.surfaceKind=siem-center guncellendi" -ForegroundColor Green
    }
}
else {
    Write-Host "Yeni @dashboards kaydi — POST slug=$slug" -ForegroundColor Yellow
    if ($WhatIf) {
        Write-Host "WhatIf POST" -ForegroundColor DarkYellow
    }
    else {
        $created = Invoke-RestMethod -Uri "$BaseUrl$dataPath" -Headers $headers -Method POST -Body $json -TimeoutSec 30
        $id = Get-RowId $created
        Write-Host "  OK olusturuldu id=$id" -ForegroundColor Green
    }
}

Write-Host ''
Write-Host "Sonraki: smoke-siem-center-dashboard.ps1" -ForegroundColor Cyan
