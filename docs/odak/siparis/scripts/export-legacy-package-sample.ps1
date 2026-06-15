# Legacy Kalite — tek is paketi + kalemler JSON export (POC migrasyon girdisi)
#
# Usage:
#   Sunucuda veya lokal MySQL'den export edin; migrate-packages-poc.ps1 -LegacyJsonPath ile verin.
#   Ornek: .\export-legacy-package-sample.ps1 -PackageNo "2026-022"
#
# Not: mysql CLI yoksa bu dosyayi referans alarak elle doldurabilirsiniz.

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
if ([string]::IsNullOrEmpty($OutputFile)) {
    $safeName = ($PackageNo -replace '[^\w\-]', '_')
    $OutputFile = Join-Path $scriptDir "..\datasets\legacy-package-$safeName.json"
}

function Invoke-LegacyQuery {
    param([string]$Sql)
    $mysql = Get-Command mysql -ErrorAction SilentlyContinue
    if (-not $mysql) {
        throw "mysql CLI bulunamadi. PATH'e MySQL client ekleyin veya legacy-package JSON'u elle olusturun."
    }
    $args = @("-h", $LegacyMySqlHost, "-P", $LegacyMySqlPort, "-u", $LegacyMySqlUser, $LegacyDatabase, "-N", "-B", "-e", $Sql)
    if ($LegacyMySqlPassword) { $args = @("-h", $LegacyMySqlHost, "-P", $LegacyMySqlPort, "-u", $LegacyMySqlUser, "-p$LegacyMySqlPassword") + $args[4..($args.Length - 1)] }
    $raw = & mysql @args 2>&1
    if ($LASTEXITCODE -ne 0) { throw "MySQL hatasi: $raw" }
    return $raw
}

$escapedNo = $PackageNo -replace '''', ''''''
$pkgSql = @"
SELECT p.id, p.package_no, p.name, p.customer_id, p.status, p.begin_date, p.delivery_date,
       p.address, p.notes, p.part_count, p.po_version, f.name AS firm_name
FROM packages p
LEFT JOIN firms f ON f.id = p.customer_id
WHERE p.package_no = '$escapedNo'
LIMIT 1;
"@

$lineSql = @"
SELECT pi.id, pi.package_id, pi.number, pi.customer_project_no, pi.customer_po_no,
       pi.customer_po_item_no, pi.description, pi.po_item_rev_no, pi.customer_job_no,
       pi.count, pi.unit, pi.unit_cost, pi.total_cost, pi.currency, pi.quality_reqs,
       pi.isfai, pi.shipment_date, pi.shipment_address
FROM packageitems pi
INNER JOIN packages p ON p.id = pi.package_id
WHERE p.package_no = '$escapedNo'
ORDER BY pi.number;
"@

Write-Host "Export: $PackageNo -> $OutputFile" -ForegroundColor Cyan

# Tab-separated parse helper
function Parse-TsvRows {
    param([string[]]$Lines, [string[]]$Columns)
    $rows = @()
    foreach ($line in $Lines) {
        if ([string]::IsNullOrWhiteSpace($line)) { continue }
        $parts = $line -split "`t"
        $obj = [ordered]@{}
        for ($i = 0; $i -lt $Columns.Count; $i++) {
            $val = if ($i -lt $parts.Count) { $parts[$i] } else { $null }
            if ($val -eq "NULL" -or $val -eq "\N") { $val = $null }
            $obj[$Columns[$i]] = $val
        }
        $rows += [pscustomobject]$obj
    }
    return $rows
}

$pkgCols = @("id", "package_no", "name", "customer_id", "status", "begin_date", "delivery_date", "address", "notes", "part_count", "po_version", "firm_name")
$lineCols = @("id", "package_id", "number", "customer_project_no", "customer_po_no", "customer_po_item_no", "description", "po_item_rev_no", "customer_job_no", "count", "unit", "unit_cost", "total_cost", "currency", "quality_reqs", "isfai", "shipment_date", "shipment_address")

$pkgRaw = @(Invoke-LegacyQuery -Sql $pkgSql)
if (-not $pkgRaw -or $pkgRaw.Count -eq 0) { throw "Paket bulunamadi: $PackageNo" }
$pkg = (Parse-TsvRows -Lines $pkgRaw -Columns $pkgCols)[0]

$lineRaw = @(Invoke-LegacyQuery -Sql $lineSql)
$lines = Parse-TsvRows -Lines $lineRaw -Columns $lineCols

$export = @{
    exportedAt = (Get-Date).ToUniversalTime().ToString("o")
    packageNo  = $PackageNo
    package    = $pkg
    items      = $lines
}

$export | ConvertTo-Json -Depth 6 | Set-Content -Path $OutputFile -Encoding UTF8
Write-Host "OK: $($lines.Count) kalem -> $OutputFile" -ForegroundColor Green
