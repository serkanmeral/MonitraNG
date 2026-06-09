# @dashboards temizligi — whitelist disindaki kayitlari siler
#
# Kullanim (repo kokunden):
#   .\docs\odak\operationcore\scripts\get-operationcore-token.ps1
#   .\docs\odak\widgets\scripts\cleanup-dashboards.ps1 -WhatIf
#   .\docs\odak\widgets\scripts\cleanup-dashboards.ps1
#
# Varsayilan keep listesi: docs/odak/widgets/datasets/dashboards_keep_v1.json
#   seed-alarm-overview, seed-siem-overview, seed-oc-workspace, siem-center

param(
    [string]$BaseUrl = "http://192.168.20.20:5040",
    [string]$KeepFile = "",
    [string[]]$KeepSlugs = @(),
    [switch]$WhatIf
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../../../..")).Path
if ([string]::IsNullOrEmpty($KeepFile)) {
    $KeepFile = Join-Path $repoRoot "docs/odak/widgets/datasets/dashboards_keep_v1.json"
}

$tokenFile = "$env:TEMP\operationcore_dg_token.txt"
$token = $null
if (Test-Path $tokenFile) {
    $token = (Get-Content $tokenFile -Raw).Trim()
}
if ([string]::IsNullOrEmpty($token)) {
    $loadScript = Join-Path $repoRoot "docs/odak/operationcore/scripts/load-operationcore-token.ps1"
    if (Test-Path $loadScript) {
        $null = & $loadScript -AutoRefresh:$false 2>&1
        if (Test-Path $tokenFile) {
            $token = (Get-Content $tokenFile -Raw).Trim()
        }
    }
}
if ([string]::IsNullOrEmpty($token)) {
    Write-Host "Token bulunamadi. Once get-operationcore-token.ps1 calistirin." -ForegroundColor Red
    exit 1
}

$headers = @{
    Authorization  = "Bearer $token"
    "Content-Type" = "application/json"
}
$dataPath = "/data/api/v1/data"

function Get-RowId($row) {
    if ($null -eq $row) { return $null }
    if ($row.__dataId) { return [string]$row.__dataId }
    if ($row.dataId) { return [string]$row.dataId }
    return $null
}

function Get-AllDashboards {
    $all = @()
    $skip = 0
    $limit = 100
    while ($true) {
        $uri = "$BaseUrl$dataPath/@dashboards?skip=$skip&limit=$limit&sort=order,name"
        $r = Invoke-RestMethod -Uri $uri -Headers $headers -Method GET -TimeoutSec 30
        $items = @()
        if ($r -is [array]) { $items = @($r) }
        elseif ($r.items) { $items = @($r.items) }
        elseif ($r.data) { $items = @($r.data) }
        if ($items.Count -eq 0) { break }
        $all += $items
        if ($items.Count -lt $limit) { break }
        $skip += $limit
    }
    return $all
}

$keepSet = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
if ($KeepSlugs.Count -gt 0) {
    foreach ($s in $KeepSlugs) { [void]$keepSet.Add([string]$s) }
}
elseif (Test-Path $KeepFile) {
    $cfg = Get-Content $KeepFile -Raw -Encoding UTF8 | ConvertFrom-Json
    foreach ($s in $cfg.keepSlugs) { [void]$keepSet.Add([string]$s) }
}
else {
    Write-Host "Keep listesi bulunamadi: $KeepFile" -ForegroundColor Red
    exit 1
}

Write-Host ''
Write-Host "Dashboard temizligi ($BaseUrl)$(if ($WhatIf) { ' [WhatIf]' })" -ForegroundColor Cyan
Write-Host ''
Write-Host "Korunan slug'lar:" -ForegroundColor Green
$keepSet | Sort-Object | ForEach-Object { Write-Host "  + $_" -ForegroundColor Green }
Write-Host ''

$dashboards = Get-AllDashboards
Write-Host "Toplam $($dashboards.Count) dashboard bulundu." -ForegroundColor Gray
Write-Host ''

$removed = 0
$kept = 0

foreach ($dash in $dashboards) {
    $slug = [string]$dash.slug
    if ([string]::IsNullOrWhiteSpace($slug)) { $slug = [string]$dash.name }
    $title = [string]$dash.title
    if ([string]::IsNullOrWhiteSpace($title)) { $title = [string]$dash.name }
    $id = Get-RowId $dash

    if ($keepSet.Contains($slug)) {
        Write-Host "  KORU  $slug — $title" -ForegroundColor DarkGreen
        $kept++
        continue
    }

    if ($WhatIf) {
        Write-Host "  [WhatIf] SIL  $slug — $title ($id)" -ForegroundColor DarkYellow
        $removed++
        continue
    }

    try {
        Invoke-RestMethod -Uri "$BaseUrl$dataPath/@dashboards/$id" -Headers $headers -Method DELETE -TimeoutSec 30 | Out-Null
        Write-Host "  SIL   $slug — $title" -ForegroundColor Red
        $removed++
    }
    catch {
        Write-Host "  HATA  $slug — $($_.Exception.Message)" -ForegroundColor Red
    }
}

Write-Host ''
Write-Host "Ozet: $kept korundu, $removed silindi$(if ($WhatIf) { ' (WhatIf)' })." -ForegroundColor Cyan
Write-Host ''
Write-Host 'Calisma panolari:' -ForegroundColor Gray
Write-Host '  /dashboards/seed-alarm-overview' -ForegroundColor Gray
Write-Host '  /dashboards/seed-siem-overview' -ForegroundColor Gray
Write-Host '  /dashboards/seed-oc-workspace' -ForegroundColor Gray
Write-Host '  /apps/siem-center (siem-center layout meta)' -ForegroundColor Gray
Write-Host ''
