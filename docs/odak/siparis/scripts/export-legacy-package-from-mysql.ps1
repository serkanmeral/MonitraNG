# Legacy Kalite — tek is paketi + kalemler JSON export (native local MySQL, tam kolonlar)
#
# Usage:
#   .\export-legacy-package-from-mysql.ps1 -PackageNo "2018-004"
#   .\export-legacy-package-from-mysql.ps1 -PackageNo "2020-039" -OutputFile .\datasets\legacy-package-2020-039.json
#
# Onkosul: kalite-legacy-local MySQL :3307 (bkz. NATIVE_LOCAL_PLAN.md)

param(
    [Parameter(Mandatory = $true)]
    [string]$PackageNo,

    [string]$LegacyMySqlHost = "127.0.0.1",
    [int]$LegacyMySqlPort = 3307,
    [string]$LegacyMySqlUser = "root",
    [string]$LegacyMySqlPassword = "",
    [string]$LegacyDatabase = "kalite",

    [string]$OutputFile = ""
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
. (Join-Path $scriptDir "lib/LegacyMysqlCommon.ps1")

if ([string]::IsNullOrEmpty($OutputFile)) {
    $safeName = ($PackageNo -replace '[^\w\-]', '_')
    $OutputFile = Join-Path $scriptDir "..\datasets\legacy-package-$safeName.json"
}

$escapedNo = Escape-LegacySqlString -Value $PackageNo

$pkgSql = @"
SELECT p.id, p.package_no, p.name, p.customer_id, p.status,
       p.polink, p.porlink, p.po_version,
       p.responsible, p.design_responsible, p.manufacture_responsible, p.contact_id,
       p.part_count, p.stock_count, p.shipped_count,
       p.begin_date, p.delivery_date, p.address, p.payment_detail, p.notes,
       p.created, p.created_by,
       f.name AS firm_name
FROM packages p
LEFT JOIN firms f ON f.id = p.customer_id
WHERE p.package_no = '$escapedNo'
LIMIT 1;
"@

$lineSql = @"
SELECT pi.id, pi.package_id, pi.number, pi.customer_project_no, pi.customer_po_no,
       pi.customer_po_item_no, pi.description, pi.po_item_rev_no, pi.customer_job_no,
       pi.count, pi.unit, pi.unit_cost, pi.total_cost, pi.currency, pi.quality_reqs,
       pi.isfai, pi.faicomp, pi.shipment_date, pi.shipment_address
FROM packageitems pi
INNER JOIN packages p ON p.id = pi.package_id
WHERE p.package_no = '$escapedNo'
ORDER BY pi.number;
"@

Write-Host "Export (MySQL): $PackageNo -> $OutputFile" -ForegroundColor Cyan
Write-Host "  Host: ${LegacyMySqlHost}:${LegacyMySqlPort}" -ForegroundColor Gray

$pkgCols = @(
    "id", "package_no", "name", "customer_id", "status",
    "polink", "porlink", "po_version",
    "responsible", "design_responsible", "manufacture_responsible", "contact_id",
    "part_count", "stock_count", "shipped_count",
    "begin_date", "delivery_date", "address", "payment_detail", "notes",
    "created", "created_by", "firm_name"
)
$lineCols = @(
    "id", "package_id", "number", "customer_project_no", "customer_po_no",
    "customer_po_item_no", "description", "po_item_rev_no", "customer_job_no",
    "count", "unit", "unit_cost", "total_cost", "currency", "quality_reqs",
    "isfai", "faicomp", "shipment_date", "shipment_address"
)

$queryParams = @{
    Host     = $LegacyMySqlHost
    Port     = $LegacyMySqlPort
    User     = $LegacyMySqlUser
    Password = $LegacyMySqlPassword
    Database = $LegacyDatabase
}

$pkgRaw = @(Invoke-LegacyMySqlQuery -Sql $pkgSql @queryParams)
if (-not $pkgRaw -or $pkgRaw.Count -eq 0) { throw "Paket bulunamadi: $PackageNo" }
$pkg = (Convert-LegacyTsvRows -Lines $pkgRaw -Columns $pkgCols)[0]

$lineRaw = @(Invoke-LegacyMySqlQuery -Sql $lineSql @queryParams)
$lines = Convert-LegacyTsvRows -Lines $lineRaw -Columns $lineCols

$export = @{
    exportedAt = (Get-Date).ToUniversalTime().ToString("o")
    packageNo  = $PackageNo
    package    = $pkg
    items      = $lines
    source     = @{
        engine = "mysql"
        host   = $LegacyMySqlHost
        port   = $LegacyMySqlPort
        db     = $LegacyDatabase
        script = "export-legacy-package-from-mysql.ps1"
    }
}

$export | ConvertTo-Json -Depth 6 | Set-Content -Path $OutputFile -Encoding UTF8
Write-Host "OK: $($lines.Count) kalem -> $OutputFile" -ForegroundColor Green
