# Legacy Kalite — shipments + shipmentitems + qcfs JSON export (MySQL JSON_OBJECT)
#
# Usage:
#   .\export-legacy-shipments-from-mysql.ps1
#
# Not: Kolon adlari legacy kalite DB ile uyumlu olmalidir. Farkli ortamda once:
#   DESCRIBE shipments; DESCRIBE shipmentitems; DESCRIBE qcfs;

param(
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
    $OutputFile = Join-Path $scriptDir "..\datasets\legacy-shipments-export.json"
}

$queryParams = @{
    MySqlHost = $LegacyMySqlHost
    Port      = $LegacyMySqlPort
    User      = $LegacyMySqlUser
    Password  = $LegacyMySqlPassword
    Database  = $LegacyDatabase
}

Write-Host "Export shipments + shipmentitems + qcfs -> $OutputFile" -ForegroundColor Cyan

$shipmentsSql = @"
SELECT JSON_OBJECT(
  'id', s.id,
  'package_id', s.package_id,
  'shipment_no', s.shipment_no,
  'bill_no', s.bill_no,
  'shipment_date', DATE_FORMAT(s.shipment_date, '%Y-%m-%d %H:%i:%s'),
  'inspection_type', s.inspection_type,
  'inspection_date', DATE_FORMAT(s.inspection_date, '%Y-%m-%d %H:%i:%s'),
  'status', s.status,
  'address', s.address,
  'descript', s.descript,
  'notes', s.notes
) FROM shipments s ORDER BY s.id;
"@

$itemsSql = @"
SELECT JSON_OBJECT(
  'id', si.id,
  'shipment_id', si.shipment_id,
  'packageitem_id', si.packageitem_id,
  'shipment_count', si.shipment_count
) FROM shipmentitems si ORDER BY si.id;
"@

$qcfsSql = @"
SELECT JSON_OBJECT(
  'id', q.id,
  'shipment_id', q.shipment_id,
  'package_id', q.package_id,
  'package_no', q.package_no,
  'form_no', q.form_no,
  'qcf_no', q.qcf_no,
  'result', q.result
) FROM qcfs q ORDER BY q.id;
"@

function Read-JsonRows {
    param([string]$Sql)
    try {
        return @(Invoke-LegacyMySqlJsonRows -Sql $Sql @queryParams)
    }
    catch {
        Write-Warning $_
        return @()
    }
}

$shipments = Read-JsonRows -Sql $shipmentsSql
Write-Host "  shipments: $($shipments.Count)" -ForegroundColor Gray
$items = Read-JsonRows -Sql $itemsSql
Write-Host "  shipmentitems: $($items.Count)" -ForegroundColor Gray
$qcfs = @()
try {
    $qcfs = Read-JsonRows -Sql $qcfsSql
    Write-Host "  qcfs: $($qcfs.Count)" -ForegroundColor Gray
}
catch {
    Write-Warning "qcfs export atlandi (tablo/kolon farkli olabilir): $_"
}

$payload = @{
    exportedAt     = (Get-Date).ToUniversalTime().ToString("o")
    exportFormat   = "mysql-json-object-v1"
    legacyDatabase = $LegacyDatabase
    shipmentsCount = $shipments.Count
    itemsCount     = $items.Count
    qcfsCount      = $qcfs.Count
    shipments      = $shipments
    shipmentitems  = $items
    qcfs           = $qcfs
    source         = @{
        engine = "mysql"
        host   = $LegacyMySqlHost
        port   = $LegacyMySqlPort
        db     = $LegacyDatabase
    }
}

Write-Utf8JsonFile -Path $OutputFile -Object $payload -Depth 8
Write-Host "OK: $($shipments.Count) shipments, $($items.Count) items, $($qcfs.Count) qcfs -> $OutputFile" -ForegroundColor Green
