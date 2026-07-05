# Legacy trainings -> odak_egitimler (DG, idempotent)
#
# Usage:
#   .\migrate-legacy-divisions-to-dg.ps1
#   .\migrate-legacy-trainings-to-dg.ps1
#   .\migrate-legacy-trainings-to-dg.ps1 -DryRun

param(
    [string]$LegacyExportPath = "",
    [string]$DivisionMappingPath = "",
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
if ([string]::IsNullOrWhiteSpace($DivisionMappingPath)) {
    $DivisionMappingPath = Get-LegacyEgitimDivisionMappingPath -ScriptDir $scriptDir
}
if (-not (Test-Path $LegacyExportPath)) {
    throw "Export JSON yok: $LegacyExportPath"
}
if (-not (Test-Path $DivisionMappingPath)) {
    throw "Division mapping yok: $DivisionMappingPath — once migrate-legacy-divisions-to-dg.ps1"
}

$dg = Initialize-LegacyEgitimDgContext -RepoRoot $repoRoot -BaseUrl $BaseUrl -UseGateway:$UseGateway
$ctx = $dg.AuthContext
$dataPath = $dg.DataPath
$dataset = "odak_egitimler"
$mappingPath = Get-LegacyEgitimTrainingMappingPath -ScriptDir $scriptDir

function Invoke-Dg {
    param([string]$Method, [string]$Uri, [object]$Body = $null)
    Invoke-DgMigrationApi -AuthContext $ctx -Method $Method -Uri $Uri -Body $Body -RetryOnUnauthorized
}

$raw = Get-Content $LegacyExportPath -Raw -Encoding UTF8 | ConvertFrom-Json
$trainings = @($raw.trainings)
$divMapRaw = Get-Content $DivisionMappingPath -Raw -Encoding UTF8 | ConvertFrom-Json
$divMap = @{}
$divSource = $divMapRaw.divisions
if (-not $divSource) { $divSource = $divMapRaw }
foreach ($prop in $divSource.PSObject.Properties) {
    $divMap[$prop.Name] = [string]$prop.Value
}

Write-Host "`n=== migrate-legacy-trainings-to-dg ===" -ForegroundColor Cyan
Write-Host "Kaynak: $LegacyExportPath ($($trainings.Count) egitim)" -ForegroundColor Gray
Write-Host "BaseUrl: $($dg.BaseUrl)  DryRun: $DryRun`n" -ForegroundColor Gray

$existingMap = @{}
$skip = 0
$limit = 500
while ($true) {
    $uri = "{0}{1}/{2}?skip={3}&limit={4}" -f $dg.BaseUrl, $dataPath, $dataset, $skip, $limit
    $items = Get-DgMigrationItems (Invoke-Dg -Method GET -Uri $uri)
    foreach ($item in $items) {
        $legacy = [string]$item.legacyTrainingId
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
$noDivision = 0

foreach ($tr in $trainings) {
    $legacyId = [string]$tr.legacyTrainingId
    if ($existingMap.ContainsKey($legacyId)) {
        $mapping[$legacyId] = $existingMap[$legacyId]
        $skipped++
        continue
    }

    $birimId = $null
    $legacyDivId = [string]$tr.legacyDivisionId
    if ($legacyDivId -and $divMap.ContainsKey($legacyDivId)) {
        $birimId = $divMap[$legacyDivId]
    }
    elseif ($legacyDivId) { $noDivision++ }

    $body = @{
        legacyTrainingId     = $legacyId
        egitimNo             = [string]$tr.egitimNo
        baslik               = [string]$tr.baslik
        konu                 = if ($tr.konu) { [string]$tr.konu } else { $null }
        egitimVeren          = if ($tr.egitimVeren) { [string]$tr.egitimVeren } else { $null }
        planlananTarih       = if ($tr.planlananTarih) { [string]$tr.planlananTarih } else { $null }
        gerceklesenTarih     = if ($tr.gerceklesenTarih) { [string]$tr.gerceklesenTarih } else { $null }
        sureDakika           = if ($null -ne $tr.sureDakika) { [int]$tr.sureDakika } else { $null }
        konum                = if ($tr.konum) { [string]$tr.konum } else { $null }
        egitimAmaci          = if ($tr.egitimAmaci) { [string]$tr.egitimAmaci } else { $null }
        degerlendirmeYontemi = if ($tr.degerlendirmeYontemi) { [string]$tr.degerlendirmeYontemi } else { $null }
        toplamCalisanSayisi  = [int]$tr.toplamCalisanSayisi
        durum                = [string]$tr.durum
    }
    if ($birimId) { $body.birimId = $birimId }

    if ($DryRun) {
        Write-Host "[DRY] training $legacyId $($body.egitimNo) -> $($body.baslik)" -ForegroundColor Yellow
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
        if ($created % 50 -eq 0) { Write-Host "  ... $created olusturuldu" -ForegroundColor Gray }
    }
    catch {
        $failed++
        Write-Host "[FAIL] training $legacyId — $($_.Exception.Message)" -ForegroundColor Red
    }
}

if (-not $DryRun) {
    Write-Utf8JsonFile -Path $mappingPath -Object @{
        generatedAt = (Get-Date -Format "yyyy-MM-dd HH:mm:ss")
        dataset     = $dataset
        trainings   = $mapping
    } -Depth 4
    Write-Host "Mapping: $mappingPath ($($mapping.Count) kayit)" -ForegroundColor Green
}

Write-Host "`nOzet: created=$created skipped=$skipped failed=$failed noDivision=$noDivision mapped=$($mapping.Count)" -ForegroundColor Cyan
if ($failed -gt 0) { exit 1 }
