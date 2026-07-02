# odak_is_paketleri — legacy SQL dump kaynagindan Turkce karakter onarimi (name, notes, deliveryAddress)
#
# Sorun: bazi prod kayitlarda name "ERMAN KARAKO�-C�ZDAN" gibi (Ç/Ü -> ? veya �)
# Kaynak: %USERPROFILE%\kalite-legacy-docker\db\init\01-kalite.sql (UTF-8)
#
# Usage:
#   $env:MNG_OC_USE_PROD_TOKEN = "1"
#   .\docs\odak\operationcore\scripts\get-operationcore-token-prod.ps1
#   .\docs\odak\siparis\scripts\repair-odak-package-text.ps1 -DryRun
#   .\docs\odak\siparis\scripts\repair-odak-package-text.ps1
#   .\docs\odak\siparis\scripts\repair-odak-package-text.ps1 -Fields name
#   .\docs\odak\siparis\scripts\repair-odak-package-text.ps1 -BaseUrl "http://192.168.20.8:5040"

param(
    [string]$SqlDumpPath = "",
    [string]$BaseUrl = "http://192.168.20.8:5040",
    [switch]$UseGateway = $true,
    [ValidateSet("name", "notes", "deliveryAddress")]
    [string[]]$Fields = @("name", "notes", "deliveryAddress"),
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

$legacyFieldMap = @{
    name            = @{ index = 4; max = 500 }
    deliveryAddress = @{ index = 10; max = 500 }
    notes           = @{ index = 11; max = 2000 }
}

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

function Get-LegacyFieldValue {
    param(
        [object[]]$PackageRow,
        [string]$FieldName
    )
    $meta = $legacyFieldMap[$FieldName]
    if (-not $meta) { return $null }
    $raw = $PackageRow[$meta.index]
    if ($null -eq $raw) { return $null }
    return Limit-LegacyText $raw $meta.max
}

function Test-FieldNeedsRepair {
    param(
        [string]$Current,
        [string]$Target
    )
    if ([string]::IsNullOrWhiteSpace($Target)) { return $false }
    $current = if ($null -eq $Current) { "" } else { [string]$Current }
    if ($current -ceq $Target) { return $false }
    if (Test-SuspiciousLegacyText $current) { return $true }
    if ($current.Length -lt $Target.Length) { return $true }
    return $false
}

$sqlPath = Get-LegacySqlDumpPath -SqlDumpPath $SqlDumpPath
Write-Host "`n=== repair-odak-package-text ===" -ForegroundColor Cyan
Write-Host "SQL: $sqlPath" -ForegroundColor Gray
Write-Host "DG:  $BaseUrl" -ForegroundColor Gray
Write-Host "Fields: $($Fields -join ', ')" -ForegroundColor Gray
Write-Host "DryRun: $DryRun`n" -ForegroundColor Gray

$packageRows = @(Read-SqlInsertRows -Path $sqlPath -TableName "packages")
$sourceByLegacyId = @{}
foreach ($p in $packageRows) {
    $legacyId = [string]$p[0]
    if ([string]::IsNullOrWhiteSpace($legacyId)) { continue }
    $entry = @{
        packageNo = if ($p[1]) { [string]$p[1] } else { $legacyId }
    }
    foreach ($fieldName in $Fields) {
        $entry[$fieldName] = Get-LegacyFieldValue -PackageRow $p -FieldName $fieldName
    }
    $sourceByLegacyId[$legacyId] = $entry
}
Write-Host "Legacy paket (SQL): $($sourceByLegacyId.Count)" -ForegroundColor Green

$dgRows = @(Get-AllRows -Dataset "odak_is_paketleri")
Write-Host "DG odak_is_paketleri: $($dgRows.Count)" -ForegroundColor Green

$report = @{
    scanned      = $dgRows.Count
    patchedRows  = 0
    patchedFields = 0
    skipped      = 0
    noSource     = 0
    alreadyOk    = 0
    dryRun       = [bool]$DryRun
    fields       = @($Fields)
    changes      = @()
}

foreach ($row in $dgRows) {
    $id = Get-RowId $row
    $legacyId = [string]$row.legacyPackageId
    if (-not $id -or [string]::IsNullOrWhiteSpace($legacyId)) {
        $report.skipped++
        continue
    }
    if (-not $sourceByLegacyId.ContainsKey($legacyId)) {
        $hasSuspicious = $false
        foreach ($fieldName in $Fields) {
            if (Test-SuspiciousLegacyText ([string]$row.$fieldName)) { $hasSuspicious = $true; break }
        }
        if ($hasSuspicious) { $report.noSource++ }
        else { $report.alreadyOk++ }
        continue
    }

    $source = $sourceByLegacyId[$legacyId]
    $patchBody = @{}
    $fieldChanges = @{}

    foreach ($fieldName in $Fields) {
        $target = [string]$source[$fieldName]
        if ([string]::IsNullOrWhiteSpace($target)) { continue }
        $current = [string]$row.$fieldName
        if (-not (Test-FieldNeedsRepair -Current $current -Target $target)) { continue }
        $patchBody[$fieldName] = $target
        $fieldChanges[$fieldName] = @{ from = $current; to = $target }
    }

    if ($patchBody.Count -eq 0) {
        $report.alreadyOk++
        continue
    }

    $change = @{
        dataId          = $id
        legacyPackageId = $legacyId
        packageNo       = [string]$row.packageNo
        fields          = $fieldChanges
    }
    $report.changes += $change

    $label = if ($row.packageNo) { [string]$row.packageNo } else { $legacyId }
    if ($DryRun) {
        Write-Host "[DRY] $label ($id)" -ForegroundColor Yellow
        foreach ($fieldName in $patchBody.Keys) {
            Write-Host "  $fieldName : $($fieldChanges[$fieldName].from) -> $($fieldChanges[$fieldName].to)" -ForegroundColor Yellow
        }
    }
    else {
        Invoke-Dg -Method PUT -Uri "$BaseUrl$dataPath/odak_is_paketleri/$id" -Body $patchBody | Out-Null
        Write-Host "PATCH $label ($id)" -ForegroundColor Green
        foreach ($fieldName in $patchBody.Keys) {
            Write-Host "  $fieldName : $($fieldChanges[$fieldName].from) -> $($fieldChanges[$fieldName].to)" -ForegroundColor Green
        }
    }

    $report.patchedRows++
    $report.patchedFields += $patchBody.Count
}

$reportPath = Join-Path $scriptDir "..\datasets\repair-odak-package-text-report.json"
Write-Utf8JsonFile -Path $reportPath -Object $report -Depth 8

Write-Host "`nOzet: rows=$($report.patchedRows) fields=$($report.patchedFields) alreadyOk=$($report.alreadyOk) noSource=$($report.noSource) skipped=$($report.skipped)" -ForegroundColor Cyan
Write-Host "Rapor: $reportPath" -ForegroundColor Gray
