# Tum legacy is paketlerini DG'ye tasir (native MySQL export + migrate-legacy-package-to-dg)
#
# Usage:
#   .\docs\odak\operationcore\scripts\get-operationcore-token.ps1
#   .\setup-odak-siparis-datasets.ps1
#   .\migrate-legacy-firms-to-dg.ps1
#   .\migrate-legacy-all-packages-to-dg.ps1
#   .\migrate-legacy-all-packages-to-dg.ps1 -Limit 5 -DryRun

param(
    [string]$LegacyMySqlHost = "127.0.0.1",
    [int]$LegacyMySqlPort = 3307,
    [string]$LegacyMySqlUser = "root",
    [string]$LegacyMySqlPassword = "",
    [string]$LegacyDatabase = "kalite",

    [int]$Limit = 0,
    [int]$Skip = 0,
    [switch]$DryRun,
    [switch]$KeepExportFiles
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
. (Join-Path $scriptDir "lib/LegacyMysqlCommon.ps1")

$exportDir = Join-Path $scriptDir "..\datasets\export-batch"
if (-not (Test-Path $exportDir)) {
    New-Item -ItemType Directory -Path $exportDir -Force | Out-Null
}

$listSql = "SELECT package_no FROM packages ORDER BY package_no;"
$queryParams = @{
    MySqlHost = $LegacyMySqlHost
    Port     = $LegacyMySqlPort
    User     = $LegacyMySqlUser
    Password = $LegacyMySqlPassword
    Database = $LegacyDatabase
}

Write-Host "`n=== migrate-legacy-all-packages-to-dg ===" -ForegroundColor Cyan
$raw = @(Invoke-LegacyMySqlQuery -Sql $listSql @queryParams)
$packageNos = @($raw | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
Write-Host "MySQL packages: $($packageNos.Count)" -ForegroundColor Cyan

if ($Skip -gt 0) {
    $packageNos = $packageNos | Select-Object -Skip $Skip
}
if ($Limit -gt 0) {
    $packageNos = @($packageNos | Select-Object -First $Limit)
}

Write-Host "Islenecek: $($packageNos.Count) (Skip=$Skip Limit=$Limit DryRun=$DryRun)`n" -ForegroundColor Cyan

$ok = 0
$fail = 0
$i = 0

foreach ($no in $packageNos) {
    $i++
    $safeName = ($no -replace '[^\w\-]', '_')
    $jsonPath = Join-Path $exportDir "legacy-package-$safeName.json"

    Write-Host "[$i/$($packageNos.Count)] $no" -ForegroundColor Cyan
    try {
        & (Join-Path $scriptDir "export-legacy-package-from-mysql.ps1") `
            -PackageNo $no `
            -LegacyMySqlHost $LegacyMySqlHost `
            -LegacyMySqlPort $LegacyMySqlPort `
            -LegacyMySqlUser $LegacyMySqlUser `
            -LegacyMySqlPassword $LegacyMySqlPassword `
            -LegacyDatabase $LegacyDatabase `
            -OutputFile $jsonPath

        $migrateArgs = @{
            LegacyJsonPath = $jsonPath
        }
        if ($DryRun) { $migrateArgs.DryRun = $true }

        & (Join-Path $scriptDir "migrate-legacy-package-to-dg.ps1") @migrateArgs
        $ok++
    }
    catch {
        Write-Host "  HATA: $($_.Exception.Message)" -ForegroundColor Red
        $fail++
    }

    if (-not $KeepExportFiles -and (Test-Path $jsonPath)) {
        Remove-Item $jsonPath -Force -ErrorAction SilentlyContinue
    }
}

Write-Host "`nBitti: OK=$ok HATA=$fail" -ForegroundColor $(if ($fail -eq 0) { "Green" } else { "Yellow" })
Write-Host "Dogrulama: .\verify-legacy-dg-migration.ps1" -ForegroundColor Gray
