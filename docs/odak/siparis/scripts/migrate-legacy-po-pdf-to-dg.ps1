# Legacy MUSTERI_PO PDF -> DG odak_is_paketleri.poDocument (file alani)
#
# Usage:
#   .\export-legacy-po-candidates-from-mysql.ps1 -Limit 20
#   .\migrate-legacy-po-pdf-to-dg.ps1 -DryRun
#   .\migrate-legacy-po-pdf-to-dg.ps1 -PackageNos "2018-019","2023-027"
#   .\migrate-legacy-po-pdf-to-dg.ps1 -Limit 10
#
# Onkosul: legacy uploads (Yonetim/MUSTERI_PO), Odak DG token, odak_is_paketleri dataset

param(
    [string[]]$PackageNos = @(),
    [int]$Limit = 10,
    [switch]$All,
    [string]$LegacyUploadRoot = "",
    [string]$CandidatesJsonPath = "",

    [string]$BaseUrl = "http://192.168.20.20:5040",
    [switch]$UseGateway = $true,
    [switch]$DryRun,
    [switch]$SkipExisting,
    [switch]$Force,

    [string]$LegacyMySqlHost = "127.0.0.1",
    [int]$LegacyMySqlPort = 3307,
    [string]$LegacyMySqlUser = "root",
    [string]$LegacyMySqlPassword = "",
    [string]$LegacyDatabase = "kalite",

    [int]$MaxFileBytes = 26214400  # 25 MB — DG file alan limiti
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
$repoRoot = (Resolve-Path (Join-Path $scriptDir "../../../..")).Path
. (Join-Path $scriptDir "lib/LegacyMysqlCommon.ps1")
. (Join-Path $scriptDir "lib/LegacyPoFileCommon.ps1")
. (Join-Path $scriptDir "lib/DgMigrationCommon.ps1")

if ([string]::IsNullOrWhiteSpace($LegacyUploadRoot)) {
    $LegacyUploadRoot = Join-Path $env:USERPROFILE "kalite-legacy-local\uploads"
    if (-not (Test-Path $LegacyUploadRoot)) {
        $alt = Join-Path $env:USERPROFILE "kalite-legacy-docker\uploads"
        if (Test-Path $alt) { $LegacyUploadRoot = $alt }
    }
}

if ([string]::IsNullOrEmpty($CandidatesJsonPath)) {
    $CandidatesJsonPath = Join-Path $scriptDir "..\datasets\legacy-po-candidates.json"
}

$dataPath = if ($UseGateway) { "/data/api/v1/data" } else { "/api/v1/data" }
$dataset = "odak_is_paketleri"
$ocTokenScript = Join-Path $repoRoot "docs/odak/operationcore/scripts/load-operationcore-token.ps1"
$reportPath = Join-Path $scriptDir "..\datasets\legacy-po-pdf-migration-report.json"

$token = & $ocTokenScript
if ([string]::IsNullOrEmpty($token)) { throw "Token alinamadi." }

$headers = @{
    "Authorization" = "Bearer $token"
    "Content-Type"  = "application/json"
}
[System.Net.ServicePointManager]::ServerCertificateValidationCallback = { $true }

function Invoke-Dg {
    param([string]$Method, [string]$Uri, [object]$Body = $null)
    $skipCert = $Uri.StartsWith("https://") -and (Get-Command Invoke-RestMethod).Parameters.ContainsKey("SkipCertificateCheck")
    return Invoke-DgRestMethod -Method $Method -Uri $Uri -Headers $headers -Body $Body -JsonDepth 8 -SkipCertificateCheck:$skipCert
}

function Get-DgDataId {
    param($Response)
    if (-not $Response) { return $null }
    $d = $Response.data; if (-not $d) { $d = $Response.Data }; if (-not $d) { $d = $Response }
    $id = $d.__dataId; if (-not $id) { $id = $d.dataId }; if (-not $id) { $id = $d.DataId }
    return $id
}

function Find-DgPackageByNo {
    param([string]$PackageNo)
    if ([string]::IsNullOrWhiteSpace($PackageNo)) { return $null }
    $filter = [Uri]::EscapeDataString("packageNo:eq:$PackageNo")
    $uri = "{0}{1}/{2}?filter={3}&limit=1" -f $BaseUrl, $dataPath, $dataset, $filter
    $raw = Invoke-Dg -Method GET -Uri $uri
    $items = @()
    if ($raw -is [Array]) { $items = @($raw) }
    elseif ($raw.items) { $items = @($raw.items) }
    elseif ($raw.data) { $items = @($raw.data) }
    elseif ($raw.__dataId -or $raw.dataId) { $items = @($raw) }
    if (-not $items.Count) { return $null }
    return $items[0]
}

function Load-LegacyPoCandidatesFromMySql {
    param([string[]]$OnlyPackageNos, [int]$RowLimit)
    $whereExtra = ""
    if ($OnlyPackageNos -and $OnlyPackageNos.Count -gt 0) {
        $escaped = @($OnlyPackageNos | ForEach-Object { "'$(Escape-LegacySqlString -Value $_)'" })
        $inList = $escaped -join ","
        $whereExtra = " AND p.package_no IN ($inList)"
    }
    $sql = @"
SELECT JSON_OBJECT(
  'legacyPackageId', p.id,
  'packageNo', p.package_no,
  'polink', p.polink,
  'porlink', p.porlink,
  'poVersion', p.po_version,
  'status', p.status
) AS row_json
FROM packages p
WHERE p.polink IS NOT NULL AND TRIM(p.polink) <> ''$whereExtra
ORDER BY p.id DESC
LIMIT $RowLimit;
"@
    return @(Invoke-LegacyMySqlJsonRows -Sql $sql -MySqlHost $LegacyMySqlHost -Port $LegacyMySqlPort `
        -User $LegacyMySqlUser -Password $LegacyMySqlPassword -Database $LegacyDatabase)
}

Write-Host "`n=== migrate-legacy-po-pdf-to-dg ===" -ForegroundColor Cyan
Write-Host "Upload root: $LegacyUploadRoot" -ForegroundColor Gray
Write-Host "DG: $BaseUrl$dataPath/$dataset" -ForegroundColor Gray

$rowLimit = if ($All) { 999999 } else { $Limit }

if (-not (Test-Path $LegacyUploadRoot)) {
    Write-Host "Uyari: Upload root yok — dosya bulunamadi hatalari beklenir." -ForegroundColor Yellow
}

$candidates = @()
if ($PackageNos -and $PackageNos.Count -gt 0) {
    $candidates = Load-LegacyPoCandidatesFromMySql -OnlyPackageNos $PackageNos -RowLimit ([Math]::Max($PackageNos.Count, $rowLimit))
}
elseif (Test-Path $CandidatesJsonPath) {
    $json = Get-Content $CandidatesJsonPath -Raw -Encoding UTF8 | ConvertFrom-Json
    if ($json.candidates) {
        $candidates = if ($All) { @($json.candidates) } else { @($json.candidates | Select-Object -First $Limit) }
    }
}
else {
    $candidates = Load-LegacyPoCandidatesFromMySql -OnlyPackageNos @() -RowLimit $rowLimit
}

if (-not $candidates.Count) {
    throw "PO adayi bulunamadi. export-legacy-po-candidates-from-mysql.ps1 calistirin veya -PackageNos verin."
}

$report = @{
    migratedAt       = (Get-Date).ToUniversalTime().ToString("o")
    dryRun           = [bool]$DryRun
    legacyUploadRoot = $LegacyUploadRoot
    baseUrl          = $BaseUrl
    results          = @()
    summary          = @{ ok = 0; skipped = 0; failed = 0; fileNotFound = 0; dgNotFound = 0 }
}

foreach ($c in $candidates) {
    $packageNo = [string]$c.packageNo
    $polink = [string]$c.polink
    $poVersion = if ($null -ne $c.poVersion) { [string]$c.poVersion } else { "" }
    $entry = [ordered]@{
        packageNo = $packageNo
        polink    = $polink
        poVersion = if ([string]::IsNullOrWhiteSpace($poVersion)) { $null } else { $poVersion }
        status    = "pending"
        message   = $null
    }

    try {
        $dgPkg = Find-DgPackageByNo -PackageNo $packageNo
        if (-not $dgPkg) {
            $entry.status = "dgNotFound"
            $entry.message = "DG paketi yok: $packageNo"
            $report.summary.dgNotFound++
            $report.results += $entry
            Write-Host "[DG yok] $packageNo" -ForegroundColor Yellow
            continue
        }

        $dataId = $dgPkg.__dataId; if (-not $dataId) { $dataId = $dgPkg.dataId }
        $entry.dgDataId = [string]$dataId

        if ($SkipExisting -and -not $Force -and (Test-DgHasStoredPoDocument $dgPkg)) {
            $entry.status = "skipped"
            $entry.message = "poDocument zaten dolu"
            $report.summary.skipped++
            $report.results += $entry
            Write-Host "[atla] $packageNo — poDocument mevcut" -ForegroundColor DarkGray
            continue
        }

        $pdfPath = Resolve-LegacyPoPdfPath -UploadRoot $LegacyUploadRoot -Polink $polink -PackageNo $packageNo -PoVersion $poVersion
        if (-not $pdfPath) {
            $expected = Get-LegacyPoRelativePdfPath -Polink $polink -PackageNo $packageNo -PoVersion $poVersion
            $entry.status = "fileNotFound"
            $entry.message = "PDF yok: $expected"
            $entry.expectedRelativePath = $expected
            $report.summary.fileNotFound++
            $report.results += $entry
            Write-Host "[dosya yok] $packageNo -> $expected" -ForegroundColor Yellow
            continue
        }

        $fileInfo = Get-Item $pdfPath
        if ($fileInfo.Length -gt $MaxFileBytes) {
            $entry.status = "failed"
            $entry.message = "Dosya cok buyuk: $($fileInfo.Length) bytes"
            $report.summary.failed++
            $report.results += $entry
            Write-Host "[buyuk] $packageNo ($($fileInfo.Length) B)" -ForegroundColor Red
            continue
        }

        $originalFileName = Get-LegacyPoOriginalFileName -PackageNo $packageNo -PoVersion $poVersion
        $entry.fileName = $originalFileName
        $entry.fileBytes = $fileInfo.Length

        $body = @{
            poDocumentPath         = $polink
            poDocumentPathRedacted = if ($c.porlink) { [string]$c.porlink } else { $null }
            poVersion              = if ([string]::IsNullOrWhiteSpace($poVersion)) { $null } else { $poVersion.Trim() }
            poDocument             = @{
                content          = [Convert]::ToBase64String([IO.File]::ReadAllBytes($pdfPath))
                originalFileName = $originalFileName
            }
        }

        if ($DryRun) {
            $entry.status = "dryRun"
            $entry.message = "Yuklenecek: $originalFileName"
            $report.summary.ok++
            $report.results += $entry
            Write-Host "[dry-run] $packageNo -> $originalFileName ($($fileInfo.Length) B)" -ForegroundColor Cyan
            continue
        }

        $uri = "{0}{1}/{2}/{3}" -f $BaseUrl, $dataPath, $dataset, [Uri]::EscapeDataString([string]$dataId)
        Invoke-Dg -Method PUT -Uri $uri -Body $body | Out-Null

        $entry.status = "ok"
        $entry.message = "Yuklendi"
        $report.summary.ok++
        $report.results += $entry
        Write-Host "[ok] $packageNo -> $originalFileName" -ForegroundColor Green
    }
    catch {
        $entry.status = "failed"
        $entry.message = $_.Exception.Message
        $report.summary.failed++
        $report.results += $entry
        Write-Host "[hata] $packageNo — $($_.Exception.Message)" -ForegroundColor Red
    }
}

Write-Utf8JsonFile -Path $reportPath -Object $report -Depth 6
Write-Host "`nOzet: ok=$($report.summary.ok) skipped=$($report.summary.skipped) fileNotFound=$($report.summary.fileNotFound) dgNotFound=$($report.summary.dgNotFound) failed=$($report.summary.failed)" -ForegroundColor Cyan
Write-Host "Rapor: $reportPath" -ForegroundColor Gray
