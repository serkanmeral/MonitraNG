# Odak Siparis — müşteri portföy dashboard (@dashboards kaydi)
#
# Usage:
#   .\docs\odak\siparis\scripts\setup-odak-musteriler-dashboard.ps1
#
# UI: http://192.168.20.20:3000/dashboards/odak-musteriler

param(
    [string]$BaseUrl = "http://192.168.20.20:5040",
    [switch]$WhatIf = $false
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
$repoRoot = (Resolve-Path (Join-Path $scriptDir "../../../..")).Path
$seedFile = Join-Path $scriptDir "..\datasets\odak_musteriler_dashboard_seed.json"
$loadTokenScript = Join-Path $repoRoot "docs/odak/operationcore/scripts/load-operationcore-token.ps1"

$token = & $loadTokenScript
if ([string]::IsNullOrEmpty($token)) { throw "Token alinamadi." }

$headers = @{
    Authorization  = "Bearer $token"
    "Content-Type" = "application/json"
}

$seed = Get-Content $seedFile -Raw -Encoding UTF8 | ConvertFrom-Json
$record = $seed.record
$slug = [string]$record.slug
$dataPath = "/data/api/v1/data/@dashboards"

function Get-RowId($row) {
    if ($null -eq $row) { return $null }
    if ($row.__dataId) { return [string]$row.__dataId }
    if ($row.dataId) { return [string]$row.dataId }
    return $null
}

Write-Host "`n=== setup-odak-musteriler-dashboard ===" -ForegroundColor Cyan
Write-Host "Slug: $slug -> /dashboards/$slug`n" -ForegroundColor Cyan

$listUri = "$BaseUrl$dataPath`?limit=1&filter=slug:eq:$slug"
$list = Invoke-RestMethod -Uri $listUri -Headers $headers -Method GET
$existing = $null
if ($list.items -and $list.items.Count -gt 0) { $existing = $list.items[0] }

$body = @{
    name        = $record.name
    title       = $record.title
    description = $record.description
    slug        = $record.slug
    isDefault   = [bool]$record.isDefault
    isActive    = [bool]$record.isActive
    order       = [int]$record.order
    layout      = $record.layout
    permissions = $record.permissions
}
$json = $body | ConvertTo-Json -Depth 12 -Compress

if ($WhatIf) {
    Write-Host "[DRY] Dashboard upsert slug=$slug" -ForegroundColor Yellow
    exit 0
}

$id = Get-RowId $existing
if ($id) {
    Invoke-RestMethod -Uri "$BaseUrl$dataPath/$id" -Headers $headers -Method PUT -Body $json | Out-Null
    Write-Host "OK: Dashboard guncellendi ($id)" -ForegroundColor Green
}
else {
    $created = Invoke-RestMethod -Uri "$BaseUrl$dataPath" -Headers $headers -Method POST -Body $json
    $newId = Get-RowId $created
    Write-Host "OK: Dashboard olusturuldu ($newId)" -ForegroundColor Green
}

Write-Host "`nTamamlandi -> /dashboards/$slug" -ForegroundColor Cyan
