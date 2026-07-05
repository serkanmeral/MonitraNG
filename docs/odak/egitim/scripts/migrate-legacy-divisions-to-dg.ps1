# Legacy divisions -> odak_birimler (DG, idempotent)
#
# Usage:
#   .\export-legacy-egitim-from-sql.ps1
#   .\migrate-legacy-divisions-to-dg.ps1
#   .\migrate-legacy-divisions-to-dg.ps1 -DryRun

param(
    [string]$LegacyExportPath = "",
    [string]$BaseUrl = "http://192.168.20.8:5040",
    [switch]$UseGateway = $true,
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
$repoRoot = (Resolve-Path (Join-Path $scriptDir "../../../..")).Path
. (Join-Path $repoRoot "docs/odak/siparis/scripts/lib/DgMigrationCommon.ps1")
. (Join-Path $scriptDir "lib/LegacyEgitimMigrationCommon.ps1")

if ([string]::IsNullOrWhiteSpace($LegacyExportPath)) {
    $LegacyExportPath = Get-LegacyEgitimExportPath -ScriptDir $scriptDir
}
if (-not (Test-Path $LegacyExportPath)) {
    throw "Export JSON yok: $LegacyExportPath — once export-legacy-egitim-from-sql.ps1"
}

$dg = Initialize-LegacyEgitimDgContext -RepoRoot $repoRoot -BaseUrl $BaseUrl -UseGateway:$UseGateway
$ctx = $dg.AuthContext
$dataPath = $dg.DataPath
$dataset = "odak_birimler"
$mappingPath = Get-LegacyEgitimDivisionMappingPath -ScriptDir $scriptDir

function Invoke-Dg {
    param([string]$Method, [string]$Uri, [object]$Body = $null)
    Invoke-DgMigrationApi -AuthContext $ctx -Method $Method -Uri $Uri -Body $Body -RetryOnUnauthorized
}

$raw = Get-Content $LegacyExportPath -Raw -Encoding UTF8 | ConvertFrom-Json
$divisions = @($raw.divisions)
if (-not $divisions.Count) { throw "Export'ta division yok: $LegacyExportPath" }

Write-Host "`n=== migrate-legacy-divisions-to-dg ===" -ForegroundColor Cyan
Write-Host "Kaynak: $LegacyExportPath ($($divisions.Count) birim)" -ForegroundColor Gray
Write-Host "BaseUrl: $($dg.BaseUrl)  DryRun: $DryRun`n" -ForegroundColor Gray

$existingMap = @{}
$skip = 0
$limit = 500
while ($true) {
    $uri = "{0}{1}/{2}?skip={3}&limit={4}" -f $dg.BaseUrl, $dataPath, $dataset, $skip, $limit
    $items = Get-DgMigrationItems (Invoke-Dg -Method GET -Uri $uri)
    foreach ($item in $items) {
        $legacy = [string]$item.legacyDivisionId
        if (-not $legacy) { continue }
        $id = Get-DgMigrationDataId $item
        if ($id) { $existingMap[$legacy] = $id }
    }
    if ($items.Count -lt $limit) { break }
    $skip += $limit
}

$mapping = @{}
$created = 0
$skipped = 0
$failed = 0

foreach ($div in $divisions) {
    $legacyId = [string]$div.legacyDivisionId
    if ($existingMap.ContainsKey($legacyId)) {
        $mapping[$legacyId] = $existingMap[$legacyId]
        $skipped++
        continue
    }

    $body = @{
        legacyDivisionId = $legacyId
        kod              = [string]$div.kod
        ad               = [string]$div.ad
        aktif            = [bool]$div.aktif
    }

    if ($DryRun) {
        Write-Host "[DRY] division $legacyId -> $($body.ad)" -ForegroundColor Yellow
        $mapping[$legacyId] = "DRY-RUN"
        $created++
        continue
    }

    try {
        $resp = Invoke-Dg -Method POST -Uri "$($dg.BaseUrl)$dataPath/$dataset" -Body $body
        $dgId = Get-DgMigrationDataId $resp
        if (-not $dgId) { throw "dataId bos" }
        $mapping[$legacyId] = $dgId
        $existingMap[$legacyId] = $dgId
        $created++
    }
    catch {
        $failed++
        Write-Host "[FAIL] division $legacyId — $($_.Exception.Message)" -ForegroundColor Red
    }
}

if (-not $DryRun) {
    Write-Utf8JsonFile -Path $mappingPath -Object @{
        generatedAt = (Get-Date -Format "yyyy-MM-dd HH:mm:ss")
        dataset     = $dataset
        divisions   = $mapping
    } -Depth 4
    Write-Host "Mapping: $mappingPath ($($mapping.Count) kayit)" -ForegroundColor Green
}

Write-Host "`nOzet: created=$created skipped=$skipped failed=$failed mapped=$($mapping.Count)" -ForegroundColor Cyan
if ($failed -gt 0) { exit 1 }
