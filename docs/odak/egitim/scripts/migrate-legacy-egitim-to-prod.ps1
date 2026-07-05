# Legacy Kalite egitim -> Odak DG tam migrasyon (production)
#
# Sira: export -> birimler -> egitimler -> katilimlar
#
# Onkosul:
#   - docs/odak/egitim/scripts/setup-odak-egitim-datasets.ps1 (prod dataset)
#   - analyze-legacy-egitim-person-gaps.ps1 (106/106 hazir)
#   - %USERPROFILE%\kalite-legacy-docker\db\init\01-kalite.sql
#
# Usage (repo kokunden):
#   $env:MNG_OC_USE_PROD_TOKEN = "1"
#   .\docs\odak\operationcore\scripts\get-operationcore-token-prod.ps1
#   .\docs\odak\egitim\scripts\migrate-legacy-egitim-to-prod.ps1
#   .\docs\odak\egitim\scripts\migrate-legacy-egitim-to-prod.ps1 -DryRun
#   .\docs\odak\egitim\scripts\migrate-legacy-egitim-to-prod.ps1 -StartFromStep 2

param(
    [string]$BaseUrl = "http://192.168.20.8:5040",
    [string]$SqlDumpPath = "",
    [string]$LegacyExportPath = "",
    [switch]$DryRun,
    [switch]$SkipPersonGapCheck,
    [int]$StartFromStep = 0
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
$repoRoot = (Resolve-Path (Join-Path $scriptDir "../../../..")).Path
. (Join-Path $scriptDir "lib/LegacyEgitimMigrationCommon.ps1")

$getProdToken = Join-Path $repoRoot "docs/odak/operationcore/scripts/get-operationcore-token-prod.ps1"
$gapReport = Get-LegacyEgitimPersonGapReportPath -ScriptDir $scriptDir

function Invoke-Step {
    param(
        [string]$Title,
        [scriptblock]$Action
    )
    Write-Host "`n========================================" -ForegroundColor Cyan
    Write-Host $Title -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan
    & $Action
    $global:LASTEXITCODE = 0
}

Write-Host "`n=== migrate-legacy-egitim-to-prod ===" -ForegroundColor Green
Write-Host "Hedef DG : $BaseUrl" -ForegroundColor Green
Write-Host "DryRun   : $DryRun" -ForegroundColor Green
if ($StartFromStep -gt 0) {
    Write-Host "StartFromStep: $StartFromStep`n" -ForegroundColor Yellow
}

$env:MNG_OC_USE_PROD_TOKEN = "1"
if (Test-Path $getProdToken) {
    & $getProdToken | Out-Null
}

if (-not $SkipPersonGapCheck -and (Test-Path $gapReport)) {
    $gap = Get-Content $gapReport -Raw -Encoding UTF8 | ConvertFrom-Json
    $gapCount = [int]$gap.stats.gaps
    if ($gapCount -gt 0) {
        throw "Kisi gap var ($gapCount). Once analyze + provision-legacy-egitim-person-gaps.ps1 calistirin."
    }
    Write-Host "Kisi gap kontrolu OK (106/106)" -ForegroundColor Green
}

$commonArgs = @{
    BaseUrl    = $BaseUrl
    UseGateway = $true
}
if ($DryRun) { $commonArgs.DryRun = $true }
if ($LegacyExportPath) { $commonArgs.LegacyExportPath = $LegacyExportPath }

if ($StartFromStep -le 0) {
    Invoke-Step "1/4 Export (SQL dump -> JSON)" {
        $exportArgs = @{}
        if ($SqlDumpPath) { $exportArgs.SqlDumpPath = $SqlDumpPath }
        if ($LegacyExportPath) { $exportArgs.OutputFile = $LegacyExportPath }
        & (Join-Path $scriptDir "export-legacy-egitim-from-sql.ps1") @exportArgs
    }
}

if ($StartFromStep -le 1) {
    Invoke-Step "2/4 Birimler -> odak_birimler" {
        & (Join-Path $scriptDir "migrate-legacy-divisions-to-dg.ps1") @commonArgs
    }
}

if ($StartFromStep -le 2) {
    Invoke-Step "3/4 Egitimler -> odak_egitimler" {
        & (Join-Path $scriptDir "migrate-legacy-trainings-to-dg.ps1") @commonArgs
    }
}

if ($StartFromStep -le 3) {
    Invoke-Step "4/4 Katilimlar -> odak_egitim_katilimlari" {
        & (Join-Path $scriptDir "migrate-legacy-participations-to-dg.ps1") @commonArgs
    }
}

if (-not $DryRun) {
    Invoke-Step "Dogrulama (DG sayim)" {
        . (Join-Path $repoRoot "docs/odak/siparis/scripts/lib/DgMigrationCommon.ps1")
        $dg = Initialize-LegacyEgitimDgContext -RepoRoot $repoRoot -BaseUrl $BaseUrl
        $ctx = $dg.AuthContext
        $dataPath = $dg.DataPath
        $exportPath = if ($LegacyExportPath) { $LegacyExportPath } else { Get-LegacyEgitimExportPath -ScriptDir $scriptDir }
        $expected = Get-Content $exportPath -Raw -Encoding UTF8 | ConvertFrom-Json

        foreach ($pair in @(
            @{ Dataset = "odak_birimler"; Expected = [int]$expected.stats.divisions }
            @{ Dataset = "odak_egitimler"; Expected = [int]$expected.stats.trainings }
            @{ Dataset = "odak_egitim_katilimlari"; Expected = [int]$expected.stats.participations }
        )) {
            $count = 0
            $skip = 0
            $limit = 500
            while ($true) {
                $uri = "{0}{1}/{2}?skip={3}&limit={4}" -f $dg.BaseUrl, $dataPath, $pair.Dataset, $skip, $limit
                $items = Get-DgMigrationItems (Invoke-DgMigrationApi -AuthContext $ctx -Method GET -Uri $uri -RetryOnUnauthorized)
                $count += $items.Count
                if ($items.Count -lt $limit) { break }
                $skip += $limit
            }
            $ok = $count -ge $pair.Expected
            $color = if ($ok) { "Green" } else { "Yellow" }
            Write-Host "  $($pair.Dataset): $count / $($pair.Expected)" -ForegroundColor $color
        }
    }
}

Write-Host "`nEgitim migrasyonu tamamlandi." -ForegroundColor Green
