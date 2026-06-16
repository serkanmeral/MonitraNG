# Legacy MySQL vs DG migrasyon dogrulama (825 paket / 2769 kalem hedef)
#
# Usage:
#   .\verify-legacy-dg-migration.ps1
#   .\verify-legacy-dg-migration.ps1 -ExpectedPackages 825 -ExpectedLines 2769

param(
    [string]$BaseUrl = "http://192.168.20.20:5040",
    [switch]$UseGateway = $true,

    [string]$LegacyMySqlHost = "127.0.0.1",
    [int]$LegacyMySqlPort = 3307,
    [string]$LegacyMySqlUser = "root",
    [string]$LegacyMySqlPassword = "",
    [string]$LegacyDatabase = "kalite",
    [string]$SqlDumpPath = "",

    [switch]$UseSqlDump,

    [int]$ExpectedPackages = 825,
    [int]$ExpectedLines = 2769
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
$repoRoot = (Resolve-Path (Join-Path $scriptDir "../../../..")).Path
. (Join-Path $scriptDir "lib/LegacySqlDumpCommon.ps1")
. (Join-Path $scriptDir "lib/DgMigrationCommon.ps1")

$dataPath = if ($UseGateway) { "/data/api/v1/data" } else { "/api/v1/data" }
$ocTokenScript = Join-Path $repoRoot "docs/odak/operationcore/scripts/load-operationcore-token.ps1"

$token = & $ocTokenScript
if ([string]::IsNullOrEmpty($token)) { throw "Token alinamadi." }

$headers = @{
    "Authorization" = "Bearer $token"
    "Content-Type"  = "application/json"
}
[System.Net.ServicePointManager]::ServerCertificateValidationCallback = { $true }

function Invoke-Dg {
    param([string]$Method, [string]$Uri)
    $p = @{ Uri = $Uri; Method = $Method; Headers = $headers; ErrorAction = "Stop" }
    if ($Uri.StartsWith("https://") -and (Get-Command Invoke-RestMethod).Parameters.ContainsKey("SkipCertificateCheck")) {
        $p.SkipCertificateCheck = $true
    }
    return Invoke-RestMethod @p
}

function Get-TotalCount {
    param([string]$DatasetName)
    return Get-DgTotalCount -Headers $headers -BaseUrl $BaseUrl -DataPath $dataPath -Dataset $DatasetName
}

$queryParams = @{
    MySqlHost = $LegacyMySqlHost
    Port     = $LegacyMySqlPort
    User     = $LegacyMySqlUser
    Password = $LegacyMySqlPassword
    Database = $LegacyDatabase
}

Write-Host "`n=== verify-legacy-dg-migration ===" -ForegroundColor Cyan

if ($UseSqlDump) {
    . (Join-Path $scriptDir "lib/LegacySqlDumpCommon.ps1")
    $dumpPath = Get-LegacySqlDumpPath -SqlDumpPath $SqlDumpPath
    $mysqlPkg = @(Read-SqlInsertRows -Path $dumpPath -TableName "packages").Count
    $mysqlLines = @(Read-SqlInsertRows -Path $dumpPath -TableName "packageitems").Count
    $firmRows = Read-SqlInsertRows -Path $dumpPath -TableName "firms"
    $mysqlFirms = @($firmRows | Where-Object { [string]$_[2] -eq '1' }).Count
    $mysqlOrphan = 0
    Write-Host "Kaynak: SQL dump ($dumpPath)" -ForegroundColor Gray
}
else {
    $mysqlPkg = [int](Invoke-LegacyMySqlQuery -Sql "SELECT COUNT(*) FROM packages;" @queryParams)
    $mysqlLines = [int](Invoke-LegacyMySqlQuery -Sql "SELECT COUNT(*) FROM packageitems;" @queryParams)
    $mysqlFirms = [int](Invoke-LegacyMySqlQuery -Sql "SELECT COUNT(*) FROM firms WHERE is_customer=1;" @queryParams)
    $orphanSql = @"
SELECT COUNT(*)
FROM packageitems pi
LEFT JOIN packages p ON p.id = pi.package_id
WHERE p.id IS NULL;
"@
    $mysqlOrphan = [int](Invoke-LegacyMySqlQuery -Sql $orphanSql @queryParams)
}

$dgPkg = Get-TotalCount -DatasetName "odak_is_paketleri"
$dgLines = Get-TotalCount -DatasetName "odak_siparis_kalemleri"
$dgFirms = Get-TotalCount -DatasetName "odak_musteriler"

function Show-Row {
    param([string]$Label, [int]$Mysql, [int]$Dg, [int]$Expected)
    $match = if ($Dg -eq $Mysql) { "OK" } else { "FARK" }
    $exp = if ($Expected -gt 0 -and $Mysql -ne $Expected) { " (beklenen MySQL=$Expected)" } else { "" }
    Write-Host ("{0,-22} MySQL={1,5}  DG={2,5}  [{3}]{4}" -f $Label, $Mysql, $Dg, $match, $exp)
}

Show-Row -Label "packages" -Mysql $mysqlPkg -Dg $dgPkg -Expected $ExpectedPackages
Show-Row -Label "packageitems" -Mysql $mysqlLines -Dg $dgLines -Expected $ExpectedLines
Show-Row -Label "firms (customer)" -Mysql $mysqlFirms -Dg $dgFirms -Expected 0

Write-Host ""
Write-Host "MySQL orphan kalemler (package yok): $mysqlOrphan" -ForegroundColor $(if ($mysqlOrphan -eq 0) { "Green" } else { "Yellow" })

$allOk = ($dgPkg -eq $mysqlPkg) -and ($dgLines -eq $mysqlLines)
Write-Host ""
if ($allOk) {
    Write-Host "Dogrulama BASARILI - DG sayilari MySQL ile eslesiyor." -ForegroundColor Green
    exit 0
}
Write-Host "Dogrulama TAMAMLANMADI - migrasyon devam ediyor olabilir." -ForegroundColor Yellow
exit 1
