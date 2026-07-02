# odak_musteriler.unvan — legacy SQL dump kaynagindan Turkce karakter onarimi
#
# Sorun: bazi prod kayitlarda unvan "D?KSAN ISIL ISLEM A.S." gibi (O/Ü/I -> ?)
# Kaynak: %USERPROFILE%\kalite-legacy-docker\db\init\01-kalite.sql (UTF-8)
#
# Usage:
#   $env:MNG_OC_USE_PROD_TOKEN = "1"
#   .\docs\odak\operationcore\scripts\get-operationcore-token-prod.ps1
#   .\docs\odak\siparis\scripts\repair-odak-musteri-unvan.ps1 -DryRun
#   .\docs\odak\siparis\scripts\repair-odak-musteri-unvan.ps1
#   .\docs\odak\siparis\scripts\repair-odak-musteri-unvan.ps1 -BaseUrl "http://192.168.20.8:5040"

param(
    [string]$SqlDumpPath = "",
    [string]$BaseUrl = "http://192.168.20.8:5040",
    [switch]$UseGateway = $true,
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
$repoRoot = (Resolve-Path (Join-Path $scriptDir "../../../..")).Path
. (Join-Path $scriptDir "lib/DgMigrationCommon.ps1")
. (Join-Path $scriptDir "lib/LegacySqlDumpCommon.ps1")

$dataPath = if ($UseGateway) { "/data/api/v1/data" } else { "/api/v1/data" }
$ocTokenScript = Join-Path $repoRoot "docs/odak/operationcore/scripts/load-operationcore-token.ps1"
$auth = Initialize-DgMigrationHeaders -TokenScriptPath $ocTokenScript

function Invoke-Dg {
    param([string]$Method, [string]$Uri, [object]$Body = $null)
    Invoke-DgMigrationApi -AuthContext $auth -Method $Method -Uri $Uri -Body $Body -RetryOnUnauthorized
}

function Get-AllRows {
    param([string]$Dataset)
    $all = @()
    $skip = 0
    $limit = 500
    while ($true) {
        $uri = '{0}{1}/{2}?skip={3}&limit={4}' -f $BaseUrl, $dataPath, $Dataset, $skip, $limit
        $raw = Invoke-Dg -Method GET -Uri $uri
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

function Test-SuspiciousUnvan {
    param([string]$Text)
    return Test-SuspiciousLegacyText $Text
}

$sqlPath = Get-LegacySqlDumpPath -SqlDumpPath $SqlDumpPath
Write-Host "`n=== repair-odak-musteri-unvan ===" -ForegroundColor Cyan
Write-Host "SQL: $sqlPath" -ForegroundColor Gray
Write-Host "DG:  $BaseUrl" -ForegroundColor Gray
Write-Host "DryRun: $DryRun`n" -ForegroundColor Gray

$firmRows = @(Read-SqlInsertRows -Path $sqlPath -TableName "firms")
$sourceByLegacyId = @{}
foreach ($f in $firmRows) {
    $legacyId = [string]$f[0]
    $isCustomer = [string]$f[2]
    if ($isCustomer -ne '1') { continue }
    $unvan = Limit-LegacyText $f[4] 500
    if ([string]::IsNullOrWhiteSpace($unvan)) { continue }
    $sourceByLegacyId[$legacyId] = $unvan.Trim()
}
Write-Host "Legacy musteri unvan (SQL): $($sourceByLegacyId.Count)" -ForegroundColor Green

$dgRows = @(Get-AllRows -Dataset "odak_musteriler")
Write-Host "DG odak_musteriler: $($dgRows.Count)" -ForegroundColor Green

$report = @{
    scanned     = $dgRows.Count
    patched     = 0
    skipped     = 0
    noSource    = 0
    alreadyOk   = 0
    dryRun      = [bool]$DryRun
    changes     = @()
}

foreach ($row in $dgRows) {
    $id = Get-RowId $row
    $legacyId = [string]$row.legacyFirmId
    $current = [string]$row.unvan
    if (-not $id -or [string]::IsNullOrWhiteSpace($legacyId)) {
        $report.skipped++
        continue
    }
    if (-not $sourceByLegacyId.ContainsKey($legacyId)) {
        if (Test-SuspiciousUnvan $current) { $report.noSource++ }
        else { $report.alreadyOk++ }
        continue
    }
    $target = $sourceByLegacyId[$legacyId]
    if ($current -ceq $target) {
        $report.alreadyOk++
        continue
    }
    if (-not (Test-SuspiciousUnvan $current) -and ($current.Length -ge $target.Length)) {
        $report.alreadyOk++
        continue
    }

    $change = @{
        dataId       = $id
        legacyFirmId = $legacyId
        kod          = [string]$row.kod
        from         = $current
        to           = $target
    }
    $report.changes += $change

    if ($DryRun) {
        Write-Host "[DRY] $($row.kod) | $current -> $target" -ForegroundColor Yellow
    }
    else {
        Invoke-Dg -Method PUT -Uri "$BaseUrl$dataPath/odak_musteriler/$id" -Body @{ unvan = $target } | Out-Null
        Write-Host "PATCH $($row.kod) | $current -> $target" -ForegroundColor Green
    }
    $report.patched++
}

$reportPath = Join-Path $scriptDir "..\datasets\repair-odak-musteri-unvan-report.json"
Write-Utf8JsonFile -Path $reportPath -Object $report -Depth 6

Write-Host "`nOzet: patched=$($report.patched) alreadyOk=$($report.alreadyOk) noSource=$($report.noSource) skipped=$($report.skipped)" -ForegroundColor Cyan
Write-Host "Rapor: $reportPath" -ForegroundColor Gray
