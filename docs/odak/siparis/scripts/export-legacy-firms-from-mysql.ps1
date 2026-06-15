# Legacy Kalite — musteri firmalari JSON export (is_customer=1)
#
# Usage:
#   .\export-legacy-firms-from-mysql.ps1
#   .\export-legacy-firms-from-mysql.ps1 -OutputFile .\datasets\legacy-firms-customers.json

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
    $OutputFile = Join-Path $scriptDir "..\datasets\legacy-firms-customers.json"
}

$sql = @"
SELECT f.id, f.name, f.is_customer, f.is_supplier, f.country
FROM firms f
WHERE f.is_customer = 1
ORDER BY f.id;
"@

Write-Host "Export firms (customers) -> $OutputFile" -ForegroundColor Cyan

$queryParams = @{
    Host     = $LegacyMySqlHost
    Port     = $LegacyMySqlPort
    User     = $LegacyMySqlUser
    Password = $LegacyMySqlPassword
    Database = $LegacyDatabase
}

$cols = @("id", "name", "is_customer", "is_supplier", "country")
$raw = @(Invoke-LegacyMySqlQuery -Sql $sql @queryParams)
$firms = Convert-LegacyTsvRows -Lines $raw -Columns $cols

$export = @{
    exportedAt = (Get-Date).ToUniversalTime().ToString("o")
    count      = $firms.Count
    firms      = $firms
    source     = @{
        engine = "mysql"
        host   = $LegacyMySqlHost
        port   = $LegacyMySqlPort
        db     = $LegacyDatabase
    }
}

$export | ConvertTo-Json -Depth 5 | Set-Content -Path $OutputFile -Encoding UTF8
Write-Host "OK: $($firms.Count) musteri -> $OutputFile" -ForegroundColor Green
