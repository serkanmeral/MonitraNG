# Mevcut odak_sevkiyatlar / odak_sevkiyat_kalemleri / odak_ncr kayitlarina recordScope + lineMode backfill
#
# Usage:
#   .\docs\odak\operationcore\scripts\get-operationcore-token.ps1
#   .\docs\odak\siparis\scripts\backfill-odak-record-scope.ps1
#   .\docs\odak\siparis\scripts\backfill-odak-record-scope.ps1 -DryRun

param(
    [string]$BaseUrl = "http://192.168.20.20:5040",
    [switch]$UseGateway = $true,
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
$repoRoot = (Resolve-Path (Join-Path $scriptDir "../../../..")).Path
. (Join-Path $scriptDir "lib/DgMigrationCommon.ps1")

$dataPath = if ($UseGateway) { "/data/api/v1/data" } else { "/api/v1/data" }
$ocTokenScript = Join-Path $repoRoot "docs/odak/operationcore/scripts/load-operationcore-token.ps1"
$auth = Initialize-DgMigrationHeaders -TokenScriptPath $ocTokenScript

function Invoke-Dg {
    param([string]$Method, [string]$Uri, [object]$Body = $null)
    Invoke-DgMigrationApi -AuthContext $auth -Method $Method -Uri $Uri -Body $Body -RetryOnUnauthorized | Out-Null
}

function Get-AllRows {
    param([string]$Dataset)
    $all = @()
    $skip = 0
    $limit = 500
    while ($true) {
        $uri = '{0}{1}/{2}?skip={3}&limit={4}' -f $BaseUrl, $dataPath, $Dataset, $skip, $limit
        $raw = Invoke-DgMigrationApi -AuthContext $auth -Method GET -Uri $uri -RetryOnUnauthorized
        $items = @()
        if ($raw -is [Array]) { $items = @($raw) }
        elseif ($raw.items) { $items = @($raw.items) }
        elseif ($raw.data) { $items = @($raw.data) }
        if (-not $items.Count) { break }
        $all += $items
        if ($items.Count -lt $limit) { break }
        $skip += $limit
    }
    return $all
}

function Get-RowId($row) {
    $id = $row.__dataId; if (-not $id) { $id = $row.dataId }
    return [string]$id
}

$report = @{
    shipmentsPatched = 0
    shipmentLinesPatched = 0
    ncrsPatched = 0
    dryRun = [bool]$DryRun
}

Write-Host "Backfill recordScope / lineMode..." -ForegroundColor Cyan

$shipments = @(Get-AllRows -Dataset "odak_sevkiyatlar")
foreach ($s in $shipments) {
    $id = Get-RowId $s
    if (-not $id) { continue }
    $scope = [string]$s.recordScope
    $pkgId = Get-RelationId $s.parentPackageId
    $targetScope = if ($pkgId) { "Paketli" } else { "Genel" }
    if ($scope -eq $targetScope) { continue }
    $patch = @{ recordScope = $targetScope }
    if ($DryRun) {
        Write-Host "  [DRY] shipment $id -> $targetScope" -ForegroundColor Yellow
    }
    else {
        Invoke-Dg -Method PUT -Uri "$BaseUrl$dataPath/odak_sevkiyatlar/$id" -Body $patch
    }
    $report.shipmentsPatched++
}

$lines = @(Get-AllRows -Dataset "odak_sevkiyat_kalemleri")
foreach ($line in $lines) {
    $id = Get-RowId $line
    if (-not $id) { continue }
    $mode = [string]$line.lineMode
    $lineId = Get-RelationId $line.parentLineId
    $targetMode = if ($lineId) { "SiparisKalemi" } else { "Serbest" }
    if ($mode -eq $targetMode) { continue }
    $patch = @{ lineMode = $targetMode }
    if ($DryRun) {
        Write-Host "  [DRY] shipment line $id -> $targetMode" -ForegroundColor Yellow
    }
    else {
        Invoke-Dg -Method PUT -Uri "$BaseUrl$dataPath/odak_sevkiyat_kalemleri/$id" -Body $patch
    }
    $report.shipmentLinesPatched++
}

$ncrs = @(Get-AllRows -Dataset "odak_ncr")
foreach ($nc in $ncrs) {
    $id = Get-RowId $nc
    if (-not $id) { continue }
    $scope = [string]$nc.recordScope
    $pkgId = Get-RelationId $nc.parentPackageId
    $targetScope = if ($pkgId) { "Paketli" } else { "Genel" }
    if ($scope -eq $targetScope) { continue }
    $patch = @{ recordScope = $targetScope }
    if ($DryRun) {
        Write-Host "  [DRY] ncr $id -> $targetScope" -ForegroundColor Yellow
    }
    else {
        Invoke-Dg -Method PUT -Uri "$BaseUrl$dataPath/odak_ncr/$id" -Body $patch
    }
    $report.ncrsPatched++
}

$reportPath = Join-Path $scriptDir "..\datasets\backfill-record-scope-report.json"
Write-Utf8JsonFile -Path $reportPath -Object $report -Depth 4

Write-Host "`nTamamlandi:" -ForegroundColor Green
Write-Host "  shipments: $($report.shipmentsPatched)" -ForegroundColor Gray
Write-Host "  shipment lines: $($report.shipmentLinesPatched)" -ForegroundColor Gray
Write-Host "  ncrs: $($report.ncrsPatched)" -ForegroundColor Gray
Write-Host "  rapor: $reportPath" -ForegroundColor Gray
