# Legacy Kalite — PO PDF aday paketlerini listeler (polink dolu)
#
# Usage:
#   .\export-legacy-po-candidates-from-mysql.ps1
#   .\export-legacy-po-candidates-from-mysql.ps1 -Limit 20 -OutputFile .\datasets\legacy-po-candidates.json
#
# Onkosul: kalite-legacy-local MySQL :3307

param(
    [int]$Limit = 50,
    [string]$LegacyUploadRoot = "",

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
. (Join-Path $scriptDir "lib/LegacyPoFileCommon.ps1")

if ([string]::IsNullOrWhiteSpace($LegacyUploadRoot)) {
    $LegacyUploadRoot = Join-Path $env:USERPROFILE "kalite-legacy-local\uploads"
    if (-not (Test-Path $LegacyUploadRoot)) {
        $alt = Join-Path $env:USERPROFILE "kalite-legacy-docker\uploads"
        if (Test-Path $alt) { $LegacyUploadRoot = $alt }
    }
}

if ([string]::IsNullOrEmpty($OutputFile)) {
    $OutputFile = Join-Path $scriptDir "..\datasets\legacy-po-candidates.json"
}

$sql = @"
SELECT JSON_OBJECT(
  'legacyPackageId', p.id,
  'packageNo', p.package_no,
  'polink', p.polink,
  'porlink', p.porlink,
  'poVersion', p.po_version,
  'status', p.status,
  'name', p.name
) AS row_json
FROM packages p
WHERE p.polink IS NOT NULL AND TRIM(p.polink) <> ''
ORDER BY p.id DESC
LIMIT $Limit;
"@

$rows = Invoke-LegacyMySqlJsonRows -Sql $sql -MySqlHost $LegacyMySqlHost -Port $LegacyMySqlPort `
    -User $LegacyMySqlUser -Password $LegacyMySqlPassword -Database $LegacyDatabase

$candidates = @()
foreach ($row in $rows) {
    $packageNo = [string]$row.packageNo
    $polink = [string]$row.polink
    $poVersion = if ($null -ne $row.poVersion) { [string]$row.poVersion } else { "" }
    $relative = Get-LegacyPoRelativePdfPath -Polink $polink -PackageNo $packageNo -PoVersion $poVersion
    $absolute = Resolve-LegacyPoPdfPath -UploadRoot $LegacyUploadRoot -Polink $polink -PackageNo $packageNo -PoVersion $poVersion
    $candidates += [ordered]@{
        legacyPackageId = [string]$row.legacyPackageId
        packageNo       = $packageNo
        polink          = $polink
        porlink         = if ($row.porlink) { [string]$row.porlink } else { $null }
        poVersion       = if ([string]::IsNullOrWhiteSpace($poVersion)) { $null } else { $poVersion }
        legacyStatus    = [string]$row.status
        name            = if ($row.name) { [string]$row.name } else { $null }
        expectedRelativePath = $relative
        fileExists      = [bool]$absolute
        absolutePath    = $absolute
    }
}

$export = @{
    exportedAt         = (Get-Date).ToUniversalTime().ToString("o")
    legacyUploadRoot   = $LegacyUploadRoot
    uploadRootExists   = Test-Path $LegacyUploadRoot
    candidateCount     = $candidates.Count
    fileFoundCount     = @($candidates | Where-Object { $_.fileExists }).Count
    candidates         = $candidates
}

Write-Utf8JsonFile -Path $OutputFile -Object $export -Depth 6
Write-Host "PO adaylari: $($candidates.Count) | dosya mevcut: $($export.fileFoundCount)" -ForegroundColor Green
Write-Host "Cikti: $OutputFile" -ForegroundColor Gray
if ($export.fileFoundCount -eq 0) {
    Write-Host "Uyari: uploads klasorunde PDF bulunamadi. sync-legacy-from-server.ps1 veya MUSTERI_PO kopyasi gerekli." -ForegroundColor Yellow
}
