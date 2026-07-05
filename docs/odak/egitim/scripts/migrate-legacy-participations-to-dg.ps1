# Legacy employees_trainings -> odak_egitim_katilimlari (DG, idempotent)
#
# Usage:
#   .\analyze-legacy-egitim-person-gaps.ps1
#   .\migrate-legacy-trainings-to-dg.ps1
#   .\migrate-legacy-participations-to-dg.ps1
#   .\migrate-legacy-participations-to-dg.ps1 -DryRun

param(
    [string]$LegacyExportPath = "",
    [string]$TrainingMappingPath = "",
    [string]$GapReportPath = "",
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
if ([string]::IsNullOrWhiteSpace($TrainingMappingPath)) {
    $TrainingMappingPath = Get-LegacyEgitimTrainingMappingPath -ScriptDir $scriptDir
}
if ([string]::IsNullOrWhiteSpace($GapReportPath)) {
    $GapReportPath = Get-LegacyEgitimPersonGapReportPath -ScriptDir $scriptDir
}
if (-not (Test-Path $LegacyExportPath)) { throw "Export JSON yok: $LegacyExportPath" }
if (-not (Test-Path $TrainingMappingPath)) { throw "Training mapping yok: $TrainingMappingPath" }
if (-not (Test-Path $GapReportPath)) { throw "Gap raporu yok: $GapReportPath" }

$dg = Initialize-LegacyEgitimDgContext -RepoRoot $repoRoot -BaseUrl $BaseUrl -UseGateway:$UseGateway
$ctx = $dg.AuthContext
$dataPath = $dg.DataPath
$dataset = "odak_egitim_katilimlari"

function Invoke-Dg {
    param([string]$Method, [string]$Uri, [object]$Body = $null)
    Invoke-DgMigrationApi -AuthContext $ctx -Method $Method -Uri $Uri -Body $Body -RetryOnUnauthorized
}

$raw = Get-Content $LegacyExportPath -Raw -Encoding UTF8 | ConvertFrom-Json
$participations = @($raw.participations)
$trMapRaw = Get-Content $TrainingMappingPath -Raw -Encoding UTF8 | ConvertFrom-Json
$trMap = @{}
$trSource = $trMapRaw.trainings
if (-not $trSource) { $trSource = $trMapRaw }
foreach ($prop in $trSource.PSObject.Properties) {
    $trMap[$prop.Name] = [string]$prop.Value
}
$keeperByEmployee = Load-EmployeeKeeperMapFromGapReport -GapReportPath $GapReportPath

Write-Host "`n=== migrate-legacy-participations-to-dg ===" -ForegroundColor Cyan
Write-Host "Kaynak: $LegacyExportPath ($($participations.Count) katilim)" -ForegroundColor Gray
Write-Host "Keeper map: $($keeperByEmployee.Count) employee" -ForegroundColor Gray
Write-Host "BaseUrl: $($dg.BaseUrl)  DryRun: $DryRun`n" -ForegroundColor Gray

$existingByLegacy = @{}
$existingByPair = @{}
$skip = 0
$limit = 500
while ($true) {
    $uri = "{0}{1}/{2}?skip={3}&limit={4}" -f $dg.BaseUrl, $dataPath, $dataset, $skip, $limit
    $items = Get-DgMigrationItems (Invoke-Dg -Method GET -Uri $uri)
    foreach ($item in $items) {
        $legacyPartId = [string]$item.legacyEmployeeTrainingId
        if ($legacyPartId) { $existingByLegacy[$legacyPartId] = $item }
        $parentId = [string]$item.parentTrainingId
        $personId = [string]$item.personelId
        if ($parentId -and $personId) { $existingByPair["$parentId|$personId"] = $item }
    }
    if ($items.Count -lt $limit) { break }
    $skip += $limit
}

$created = 0
$skipped = 0
$failed = 0
$noTraining = 0
$noKeeper = 0

foreach ($part in $participations) {
    $legacyPartId = [string]$part.legacyEmployeeTrainingId
    if ($existingByLegacy.ContainsKey($legacyPartId)) {
        $skipped++
        continue
    }

    $legacyTrainingId = [string]$part.legacyTrainingId
    if (-not $trMap.ContainsKey($legacyTrainingId)) {
        $noTraining++
        continue
    }
    $parentTrainingId = $trMap[$legacyTrainingId]

    $legacyEmployeeId = [string]$part.legacyEmployeeId
    if (-not $keeperByEmployee.ContainsKey($legacyEmployeeId)) {
        $noKeeper++
        Write-Host "[SKIP] katilim $legacyPartId — keeper yok employee=$legacyEmployeeId" -ForegroundColor Yellow
        continue
    }
    $personelId = $keeperByEmployee[$legacyEmployeeId]

    $pairKey = "$parentTrainingId|$personelId"
    if ($existingByPair.ContainsKey($pairKey)) {
        $skipped++
        continue
    }

    $body = @{
        legacyEmployeeTrainingId = $legacyPartId
        parentTrainingId         = $parentTrainingId
        personelId               = $personelId
        katildi                  = [bool]$part.katildi
    }
    if ($null -ne $part.etkin) { $body.etkin = [bool]$part.etkin }

    if ($DryRun) {
        $created++
        if ($created -le 5) {
            Write-Host "[DRY] part $legacyPartId training=$legacyTrainingId employee=$legacyEmployeeId" -ForegroundColor Yellow
        }
        continue
    }

    try {
        $resp = Invoke-Dg -Method POST -Uri "$($dg.BaseUrl)$dataPath/$dataset" -Body $body
        $dgId = Get-DgMigrationDataId $resp
        if (-not $dgId) { throw "dataId bos" }
        $existingByLegacy[$legacyPartId] = $true
        $existingByPair[$pairKey] = $true
        $created++
        if ($created % 100 -eq 0) { Write-Host "  ... $created olusturuldu" -ForegroundColor Gray }
    }
    catch {
        $failed++
        if ($failed -le 10) {
            Write-Host "[FAIL] part $legacyPartId — $($_.Exception.Message)" -ForegroundColor Red
        }
    }
}

Write-Host "`nOzet: created=$created skipped=$skipped failed=$failed noTraining=$noTraining noKeeper=$noKeeper" -ForegroundColor Cyan
if ($failed -gt 0) { exit 1 }
if ($noKeeper -gt 0) { exit 1 }
