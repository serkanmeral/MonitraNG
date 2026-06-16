# Legacy Kalite — ncs + cpas + cpas_ncs JSON export (MySQL JSON_OBJECT, tab-safe)
#
# Usage:
#   .\export-legacy-ncs-from-mysql.ps1

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
    $OutputFile = Join-Path $scriptDir "..\datasets\legacy-ncs-cpas.json"
}

$queryParams = @{
    MySqlHost = $LegacyMySqlHost
    Port      = $LegacyMySqlPort
    User      = $LegacyMySqlUser
    Password  = $LegacyMySqlPassword
    Database  = $LegacyDatabase
}

Write-Host "Export ncs + cpas (JSON_OBJECT) -> $OutputFile" -ForegroundColor Cyan

$ncsSql = @"
SELECT JSON_OBJECT(
  'id', n.id,
  'package_id', n.package_id,
  'nc_no', n.nc_no,
  'nc_date', DATE_FORMAT(n.nc_date, '%Y-%m-%d %H:%i:%s'),
  'control_type', n.control_type,
  'explanation', n.explanation,
  'part_count', n.part_count,
  'job_no', n.job_no,
  'product_code', n.product_code,
  'descriptor', n.descriptor,
  'nc_status', n.nc_status,
  'fai_status', n.fai_status,
  'error_code', n.error_code,
  'nc_action', n.nc_action,
  'responsible', n.responsible,
  'rework_count', n.rework_count,
  'repair_count', n.repair_count,
  'observe_count', n.observe_count,
  'scrap_count', n.scrap_count,
  'asis_count', n.asis_count,
  'return_count', n.return_count,
  'other_count', n.other_count,
  'closure_date', DATE_FORMAT(n.closure_date, '%Y-%m-%d %H:%i:%s'),
  'notes', n.notes
) FROM ncs n ORDER BY n.id;
"@

$cpasSql = @"
SELECT JSON_OBJECT(
  'id', c.id,
  'form_year', c.form_year,
  'form_no', c.form_no,
  'cpa_no', c.cpa_no,
  'cpa_date', DATE_FORMAT(c.cpa_date, '%Y-%m-%d %H:%i:%s'),
  'source', c.source,
  'descript', c.descript,
  'request_division', c.request_division,
  'nonconformity', c.nonconformity,
  'tecnique', c.tecnique,
  'error_code', c.error_code,
  'first_followup_date', DATE_FORMAT(c.first_followup_date, '%Y-%m-%d %H:%i:%s'),
  'second_followup_date', DATE_FORMAT(c.second_followup_date, '%Y-%m-%d %H:%i:%s'),
  'closed_date', DATE_FORMAT(c.closed_date, '%Y-%m-%d %H:%i:%s'),
  'result', c.result,
  'status', c.status
) FROM cpas c ORDER BY c.id;
"@

$linksSql = @"
SELECT JSON_OBJECT(
  'cpa_id', cn.cpa_id,
  'nc_id', cn.nc_id
) FROM cpas_ncs cn;
"@

try {
    $ncs = @(Invoke-LegacyMySqlJsonRows -Sql $ncsSql @queryParams)
}
catch {
    Write-Host "ncs export hatasi: $_" -ForegroundColor Yellow
    $ncs = @()
}

try {
    $cpas = @(Invoke-LegacyMySqlJsonRows -Sql $cpasSql @queryParams)
}
catch {
    Write-Host "cpas export hatasi: $_" -ForegroundColor Yellow
    $cpas = @()
}

try {
    $links = @(Invoke-LegacyMySqlJsonRows -Sql $linksSql @queryParams)
}
catch {
    Write-Host "cpas_ncs export hatasi: $_" -ForegroundColor Yellow
    $links = @()
}

$export = @{
    exportedAt = (Get-Date).ToUniversalTime().ToString("o")
    exportFormat = "mysql-json-object-v1"
    ncsCount   = $ncs.Count
    cpasCount  = $cpas.Count
    ncs        = $ncs
    cpas       = $cpas
    cpasNcs    = $links
    source     = @{
        engine = "mysql"
        host   = $LegacyMySqlHost
        port   = $LegacyMySqlPort
        db     = $LegacyDatabase
    }
}

Write-Utf8JsonFile -Path $OutputFile -Object $export -Depth 8
Write-Host "OK: $($ncs.Count) ncs, $($cpas.Count) cpas, $($links.Count) link -> $OutputFile" -ForegroundColor Green
