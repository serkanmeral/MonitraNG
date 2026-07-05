# Legacy Kalite egitim tablolari -> JSON export (SQL dump)
#
# Usage (repo kokunden):
#   .\docs\odak\egitim\scripts\export-legacy-egitim-from-sql.ps1
#   .\docs\odak\egitim\scripts\export-legacy-egitim-from-sql.ps1 -OutputFile .\docs\odak\egitim\datasets\legacy-egitim-export.json

param(
    [string]$SqlDumpPath = "",
    [string]$OutputFile = ""
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
$repoRoot = (Resolve-Path (Join-Path $scriptDir "../../../..")).Path
. (Join-Path $repoRoot "docs/odak/siparis/scripts/lib/LegacySqlDumpCommon.ps1")
. (Join-Path $repoRoot "docs/odak/siparis/scripts/lib/DgMigrationCommon.ps1")
. (Join-Path $scriptDir "lib/LegacyEgitimMigrationCommon.ps1")

$SqlDumpPath = Get-LegacySqlDumpPath -SqlDumpPath $SqlDumpPath
if ([string]::IsNullOrWhiteSpace($OutputFile)) {
    $OutputFile = Get-LegacyEgitimExportPath -ScriptDir $scriptDir
}

Write-Host "`n=== export-legacy-egitim-from-sql ===" -ForegroundColor Cyan
Write-Host "SQL dump: $SqlDumpPath" -ForegroundColor Gray

$divisionRows = Read-SqlInsertRows -Path $SqlDumpPath -TableName "divisions"
$trainingRows = Read-SqlInsertRows -Path $SqlDumpPath -TableName "trainings"
$partRows = Read-SqlInsertRows -Path $SqlDumpPath -TableName "employees_trainings"

$divisions = @()
foreach ($fields in $divisionRows) {
    if ($fields.Count -lt 2) { continue }
    $legacyId = [string]$fields[0]
    $ad = Limit-LegacyText $fields[1] 100
    if ([string]::IsNullOrWhiteSpace($ad)) { continue }
    $divisions += [pscustomobject]@{
        legacyDivisionId = $legacyId
        kod              = Format-DivisionKod -LegacyDivisionId $legacyId
        ad               = $ad
        aktif            = $true
    }
}

$trainings = @()
foreach ($fields in $trainingRows) {
    if ($fields.Count -lt 16) { continue }
    $legacyId = [string]$fields[0]
    $legacyDivisionId = [string]$fields[1]
    if ([string]::IsNullOrWhiteSpace($legacyDivisionId)) { $legacyDivisionId = $null }

    $baslik = Limit-LegacyText $fields[2] 255
    $konu = Limit-LegacyText $fields[3] 2000
    if ([string]::IsNullOrWhiteSpace($baslik)) {
        $baslik = if ($konu) { Limit-LegacyText $konu 255 } else { "Egitim $legacyId" }
    }

    $planlananTarih = Convert-LegacySqlDateTime $fields[5]
    $gerceklesenTarih = Convert-LegacySqlDateTime $fields[6]
    $created = Convert-LegacySqlDateTime $fields[12]

    $sureRaw = [string]$fields[7]
    $sureDakika = $null
    if ($sureRaw -match '^\d+$') { $sureDakika = [int]$sureRaw }

    $toplamRaw = [string]$fields[11]
    $toplamCalisanSayisi = 0
    if ($toplamRaw -match '^\d+$') { $toplamCalisanSayisi = [int]$toplamRaw }

    $trainings += [pscustomobject]@{
        legacyTrainingId      = $legacyId
        legacyDivisionId      = $legacyDivisionId
        egitimNo              = Build-EgitimNo -LegacyTrainingId $legacyId -GerceklesenTarih $gerceklesenTarih -PlanlananTarih $planlananTarih -Created $created
        baslik                = $baslik
        konu                  = $konu
        egitimVeren           = Limit-LegacyText $fields[4] 100
        planlananTarih        = $planlananTarih
        gerceklesenTarih      = $gerceklesenTarih
        sureDakika            = $sureDakika
        konum                 = Limit-LegacyText $fields[8] 255
        egitimAmaci           = Limit-LegacyText $fields[9] 500
        degerlendirmeYontemi  = Limit-LegacyText $fields[10] 500
        toplamCalisanSayisi   = $toplamCalisanSayisi
        durum                 = Resolve-LegacyTrainingDurum -GerceklesenTarih $gerceklesenTarih
        legacyCreated         = $created
    }
}

$participations = @()
foreach ($fields in $partRows) {
    if ($fields.Count -lt 5) { continue }
    $legacyId = [string]$fields[0]
    $legacyTrainingId = [string]$fields[1]
    $legacyEmployeeId = [string]$fields[2]
    if ([string]::IsNullOrWhiteSpace($legacyTrainingId) -or [string]::IsNullOrWhiteSpace($legacyEmployeeId)) { continue }

    $effectiveRaw = $fields[4]
    $etkin = $null
    if ($null -ne $effectiveRaw -and -not [string]::IsNullOrWhiteSpace([string]$effectiveRaw)) {
        $etkin = Test-LegacyBoolField -Value $effectiveRaw -DefaultTrue:$false
    }

    $participations += [pscustomobject]@{
        legacyEmployeeTrainingId = $legacyId
        legacyTrainingId         = $legacyTrainingId
        legacyEmployeeId         = $legacyEmployeeId
        katildi                  = Test-LegacyBoolField -Value $fields[3] -DefaultTrue
        etkin                    = $etkin
    }
}

$export = @{
    exportedAt     = (Get-Date).ToUniversalTime().ToString("o")
    egitimNoPolicy = @{
        format   = "EGTM{yyyy}/{legacyTrainingId}"
        yearFrom = "COALESCE(training_date, planned_date, created)"
    }
    stats = @{
        divisions      = $divisions.Count
        trainings      = $trainings.Count
        participations = $participations.Count
    }
    source = @{
        engine  = "sql-dump"
        sqlDump = $SqlDumpPath
    }
    divisions      = @($divisions)
    trainings      = @($trainings)
    participations = @($participations)
}

Write-Utf8JsonFile -Path $OutputFile -Object $export -Depth 8
Write-Host "OK: $($divisions.Count) birim, $($trainings.Count) egitim, $($participations.Count) katilim -> $OutputFile" -ForegroundColor Green
