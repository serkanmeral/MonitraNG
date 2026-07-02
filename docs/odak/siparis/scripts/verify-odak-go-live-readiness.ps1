# Odak Siparis — canli gecis hazirlik dogrulamasi (BLOCKER / WARN)
#
# Kontroller:
#   BLOCKER: paket, kalem, musteri sayilari (MySQL/dump vs DG)
#   BLOCKER: sevkiyat, NCR, CAPA sayilari (DG >= legacy veya esit)
#   WARN:    PO PDF kapsami, Turkce mojibake ornegi, genel sevkiyat/NCR
#
# Usage:
#   .\verify-odak-go-live-readiness.ps1
#   .\verify-odak-go-live-readiness.ps1 -UseSqlDump -BaseUrl "http://192.168.20.8:5040"
#   $env:MNG_OC_USE_PROD_TOKEN = "1"; .\verify-odak-go-live-readiness.ps1
#   .\verify-odak-go-live-readiness.ps1 -Strict   # WARN de BLOCKER sayilir
#
# Cikis: 0 = hazir, 1 = BLOCKER var

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
    [int]$ExpectedLines = 2769,
    [int]$MinPoPdfCoveragePercent = 85,
    [int]$MojibakeSampleSize = 120,
    [double]$MaxMojibakeRatio = 0.05,

    [string]$PoCandidatesJsonPath = "",
    [switch]$Strict,
    [switch]$SkipMojibakeCheck,
    [switch]$SkipPoPdfCheck
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
$repoRoot = (Resolve-Path (Join-Path $scriptDir "../../../..")).Path
. (Join-Path $scriptDir "lib/LegacySqlDumpCommon.ps1")
. (Join-Path $scriptDir "lib/DgMigrationCommon.ps1")

$dataPath = if ($UseGateway) { "/data/api/v1/data" } else { "/api/v1/data" }
$ocTokenScript = Join-Path $repoRoot "docs/odak/operationcore/scripts/load-operationcore-token.ps1"
$reportPath = Join-Path $scriptDir "..\datasets\odak-go-live-readiness-report.json"

if ([string]::IsNullOrEmpty($PoCandidatesJsonPath)) {
    $PoCandidatesJsonPath = Join-Path $scriptDir "..\datasets\legacy-po-candidates.json"
}

$token = & $ocTokenScript
if ([string]::IsNullOrEmpty($token)) { throw "Token alinamadi." }

$headers = @{
    "Authorization" = "Bearer $token"
    "Content-Type"  = "application/json"
}
[System.Net.ServicePointManager]::ServerCertificateValidationCallback = { $true }

function Get-DgItemsFromResponse {
    param($Raw)
    if ($Raw -is [Array]) { return @($Raw) }
    if ($Raw.items) { return @($Raw.items) }
    if ($Raw.data) { return @($Raw.data) }
    if ($Raw.__dataId -or $Raw.dataId) { return @($Raw) }
    return @()
}

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

function Test-LooksLikeMojibake {
    param([string]$Text)
    if ([string]::IsNullOrWhiteSpace($Text)) { return $false }
    return ($Text -match '[?�]|Ã.|Ä.|Å.|â€|ï¿½')
}

function Get-DgSampleTexts {
    param(
        [string]$Dataset,
        [string[]]$Fields,
        [int]$MaxRows = 40
    )
    $texts = @()
    $skip = 0
    $limit = 100
    while ($texts.Count -lt $MaxRows) {
        $uri = '{0}{1}/{2}?skip={3}&limit={4}' -f $BaseUrl, $dataPath, $Dataset, $skip, $limit
        $raw = Invoke-Dg -Method GET -Uri $uri
        $items = Get-DgItemsFromResponse $raw
        if (-not $items.Count) { break }
        foreach ($item in $items) {
            foreach ($f in $Fields) {
                $v = [string]$item.$f
                if (-not [string]::IsNullOrWhiteSpace($v)) {
                    $texts += $v
                    if ($texts.Count -ge $MaxRows) { return $texts }
                }
            }
        }
        if ($items.Count -lt $limit) { break }
        $skip += $limit
    }
    return $texts
}

function Count-PackagesWithPoDocument {
    $count = 0
    $skip = 0
    $limit = 200
    while ($true) {
        $uri = '{0}{1}/odak_is_paketleri?skip={2}&limit={3}' -f $BaseUrl, $dataPath, $skip, $limit
        $raw = Invoke-Dg -Method GET -Uri $uri
        $items = Get-DgItemsFromResponse $raw
        if (-not $items.Count) { break }
        foreach ($item in $items) {
            $po = $item.poDocument
            if ($null -eq $po) { continue }
            if ($po -is [string] -and -not [string]::IsNullOrWhiteSpace($po)) { $count++; continue }
            if ($po.fileId -or $po.url -or $po.path -or $po.__dataId) { $count++ }
        }
        if ($items.Count -lt $limit) { break }
        $skip += $limit
    }
    return $count
}

$queryParams = @{
    MySqlHost = $LegacyMySqlHost
    Port     = $LegacyMySqlPort
    User     = $LegacyMySqlUser
    Password = $LegacyMySqlPassword
    Database = $LegacyDatabase
}

Write-Host "`n=== verify-odak-go-live-readiness ===" -ForegroundColor Cyan
Write-Host "DG: $BaseUrl  Strict: $Strict`n" -ForegroundColor Gray

if ($UseSqlDump) {
    $dumpPath = Get-LegacySqlDumpPath -SqlDumpPath $SqlDumpPath
    $mysqlPkg = @(Read-SqlInsertRows -Path $dumpPath -TableName "packages").Count
    $mysqlLines = @(Read-SqlInsertRows -Path $dumpPath -TableName "packageitems").Count
    $mysqlFirms = @((Read-SqlInsertRows -Path $dumpPath -TableName "firms") | Where-Object { [string]$_[2] -eq '1' }).Count
    $mysqlShipments = @(Read-SqlInsertRows -Path $dumpPath -TableName "shipments").Count
    $mysqlShipItems = @(Read-SqlInsertRows -Path $dumpPath -TableName "shipmentitems").Count
    $mysqlNcr = @(Read-SqlInsertRows -Path $dumpPath -TableName "ncs").Count
    $mysqlCapa = @(Read-SqlInsertRows -Path $dumpPath -TableName "cpas").Count
    Write-Host "Kaynak: SQL dump ($dumpPath)" -ForegroundColor Gray
}
else {
    $mysqlPkg = [int](Invoke-LegacyMySqlQuery -Sql "SELECT COUNT(*) FROM packages;" @queryParams)
    $mysqlLines = [int](Invoke-LegacyMySqlQuery -Sql "SELECT COUNT(*) FROM packageitems;" @queryParams)
    $mysqlFirms = [int](Invoke-LegacyMySqlQuery -Sql "SELECT COUNT(*) FROM firms WHERE is_customer=1;" @queryParams)
    $mysqlShipments = [int](Invoke-LegacyMySqlQuery -Sql "SELECT COUNT(*) FROM shipments;" @queryParams)
    $mysqlShipItems = [int](Invoke-LegacyMySqlQuery -Sql "SELECT COUNT(*) FROM shipmentitems;" @queryParams)
    $mysqlNcr = [int](Invoke-LegacyMySqlQuery -Sql "SELECT COUNT(*) FROM ncs;" @queryParams)
    $mysqlCapa = [int](Invoke-LegacyMySqlQuery -Sql "SELECT COUNT(*) FROM cpas;" @queryParams)
}

$dgPkg = Get-TotalCount -DatasetName "odak_is_paketleri"
$dgLines = Get-TotalCount -DatasetName "odak_siparis_kalemleri"
$dgFirms = Get-TotalCount -DatasetName "odak_musteriler"
$dgShipments = Get-TotalCount -DatasetName "odak_sevkiyatlar"
$dgShipItems = Get-TotalCount -DatasetName "odak_sevkiyat_kalemleri"
$dgNcr = Get-TotalCount -DatasetName "odak_ncr"
$dgCapa = Get-TotalCount -DatasetName "odak_capa"

$blockers = @()
$warnings = @()

function Add-Check {
    param(
        [string]$Id,
        [string]$Level,
        [string]$Message,
        [bool]$Passed
    )
    if ($Passed) { return }
    if ($Level -eq "BLOCKER") { $script:blockers += @{ id = $Id; message = $Message } }
    else { $script:warnings += @{ id = $Id; message = $Message } }
}

function Show-CountRow {
    param(
        [string]$Label,
        [int]$Legacy,
        [int]$Dg,
        [string]$Rule = "eq"
    )
    $ok = switch ($Rule) {
        "eq" { $Dg -eq $Legacy }
        "gte" { $Dg -ge $Legacy }
        default { $Dg -eq $Legacy }
    }
    $tag = if ($ok) { "OK" } else { "FARK" }
    $color = if ($ok) { "Green" } else { "Yellow" }
    Write-Host ("{0,-28} Legacy={1,5}  DG={2,5}  [{3}] ({4})" -f $Label, $Legacy, $Dg, $tag, $Rule) -ForegroundColor $color
    return $ok
}

Write-Host "--- Temel veri (BLOCKER) ---" -ForegroundColor Cyan
$pkgOk = Show-CountRow -Label "is_paketleri" -Legacy $mysqlPkg -Dg $dgPkg -Rule "eq"
Add-Check -Id "packages" -Level "BLOCKER" -Message "Paket sayisi eslesmiyor (Legacy=$mysqlPkg DG=$dgPkg beklenen~$ExpectedPackages)" -Passed:$pkgOk

$linesOk = Show-CountRow -Label "siparis_kalemleri" -Legacy $mysqlLines -Dg $dgLines -Rule "eq"
if (-not $linesOk -and $dgLines -ge ($ExpectedLines - 10)) {
    Write-Host "  Not: DG kalem sayisi beklenen araliga yakin ($ExpectedLines hedef)." -ForegroundColor Gray
}
Add-Check -Id "lines" -Level "BLOCKER" -Message "Kalem sayisi eslesmiyor (Legacy=$mysqlLines DG=$dgLines hedef~$ExpectedLines)" -Passed:$linesOk

$firmsOk = Show-CountRow -Label "musteriler" -Legacy $mysqlFirms -Dg $dgFirms -Rule "eq"
Add-Check -Id "firms" -Level "BLOCKER" -Message "Musteri sayisi eslesmiyor (Legacy=$mysqlFirms DG=$dgFirms)" -Passed:$firmsOk

Write-Host "`n--- Operasyon verisi (BLOCKER: DG >= Legacy) ---" -ForegroundColor Cyan
$shipOk = Show-CountRow -Label "sevkiyatlar" -Legacy $mysqlShipments -Dg $dgShipments -Rule "gte"
Add-Check -Id "shipments" -Level "BLOCKER" -Message "Sevkiyat migrasyonu eksik (Legacy=$mysqlShipments DG=$dgShipments)" -Passed:$shipOk

$shipItemOk = Show-CountRow -Label "sevkiyat_kalemleri" -Legacy $mysqlShipItems -Dg $dgShipItems -Rule "gte"
Add-Check -Id "shipment_items" -Level "BLOCKER" -Message "Sevkiyat kalemi eksik (Legacy=$mysqlShipItems DG=$dgShipItems)" -Passed:$shipItemOk

$ncrOk = Show-CountRow -Label "ncr" -Legacy $mysqlNcr -Dg $dgNcr -Rule "gte"
Add-Check -Id "ncr" -Level "BLOCKER" -Message "NCR migrasyonu eksik (Legacy=$mysqlNcr DG=$dgNcr)" -Passed:$ncrOk

$capaOk = Show-CountRow -Label "capa" -Legacy $mysqlCapa -Dg $dgCapa -Rule "gte"
Add-Check -Id "capa" -Level "BLOCKER" -Message "CAPA migrasyonu eksik (Legacy=$mysqlCapa DG=$dgCapa)" -Passed:$capaOk

Write-Host "`n--- PO PDF (WARN) ---" -ForegroundColor Cyan
$poCandidates = 0
$poWithDoc = 0
$poCoverage = 100.0
if (-not $SkipPoPdfCheck) {
    if (Test-Path $PoCandidatesJsonPath) {
        $poExport = Get-Content $PoCandidatesJsonPath -Raw -Encoding UTF8 | ConvertFrom-Json
        if ($poExport.candidates) { $poCandidates = @($poExport.candidates).Count }
        elseif ($poExport -is [Array]) { $poCandidates = $poExport.Count }
    }
    $poWithDoc = Count-PackagesWithPoDocument
    if ($poCandidates -gt 0) {
        $poCoverage = [math]::Round(100.0 * $poWithDoc / $poCandidates, 1)
    }
    Write-Host "PO aday (export): $poCandidates  DG poDocument: $poWithDoc  Kapsam: $poCoverage%" -ForegroundColor $(if ($poCoverage -ge $MinPoPdfCoveragePercent) { "Green" } else { "Yellow" })
    $poOk = ($poCandidates -eq 0) -or ($poCoverage -ge $MinPoPdfCoveragePercent)
    Add-Check -Id "po_pdf" -Level "WARN" -Message "PO PDF kapsami dusuk ($poCoverage% < $MinPoPdfCoveragePercent%)" -Passed:$poOk
}
else {
    Write-Host "PO PDF kontrolu atlandi (-SkipPoPdfCheck)" -ForegroundColor Gray
}

Write-Host "`n--- Turkce metin (WARN) ---" -ForegroundColor Cyan
$mojibakeChecked = 0
$mojibakeHits = 0
$mojibakeRatio = 0.0
if (-not $SkipMojibakeCheck) {
    $perDataset = [math]::Max(15, [int]($MojibakeSampleSize / 5))
    $samples = @()
    $samples += Get-DgSampleTexts -Dataset "odak_is_paketleri" -Fields @("name", "notes", "deliveryAddress") -MaxRows $perDataset
    $samples += Get-DgSampleTexts -Dataset "odak_musteriler" -Fields @("unvan") -MaxRows $perDataset
    $samples += Get-DgSampleTexts -Dataset "odak_siparis_kalemleri" -Fields @("description", "notes") -MaxRows $perDataset
    $samples += Get-DgSampleTexts -Dataset "odak_ncr" -Fields @("descriptor", "explanation", "notes") -MaxRows $perDataset
    $samples += Get-DgSampleTexts -Dataset "odak_sevkiyatlar" -Fields @("notes", "headerDescription", "shipmentAddress") -MaxRows $perDataset
    $mojibakeChecked = $samples.Count
    $mojibakeHits = @($samples | Where-Object { Test-LooksLikeMojibake $_ }).Count
    if ($mojibakeChecked -gt 0) {
        $mojibakeRatio = [math]::Round($mojibakeHits / $mojibakeChecked, 4)
    }
    Write-Host "Orneklenen metin: $mojibakeChecked  Mojibake: $mojibakeHits  Oran: $mojibakeRatio (max $MaxMojibakeRatio)" -ForegroundColor $(if ($mojibakeRatio -le $MaxMojibakeRatio) { "Green" } else { "Yellow" })
    if ($mojibakeHits -gt 0) {
        Write-Host "  Onarim: repair-odak-package-text.ps1 ; repair-odak-musteri-unvan.ps1 ; migrate-legacy-ncs-to-dg.ps1 -RepairText ; migrate-legacy-shipments-to-dg.ps1 -RepairText" -ForegroundColor Gray
    }
    $mojOk = ($mojibakeChecked -eq 0) -or ($mojibakeRatio -le $MaxMojibakeRatio)
    Add-Check -Id "mojibake" -Level "WARN" -Message "Turkce mojibake orani yuksek ($mojibakeHits/$mojibakeChecked)" -Passed:$mojOk
}
else {
    Write-Host "Mojibake kontrolu atlandi (-SkipMojibakeCheck)" -ForegroundColor Gray
}

if ($Strict) {
    foreach ($w in $warnings) {
        $blockers += $w
    }
    $warnings = @()
}

Write-Host "`n--- Ozet ---" -ForegroundColor Cyan
Write-Host "BLOCKER: $($blockers.Count)" -ForegroundColor $(if ($blockers.Count -eq 0) { "Green" } else { "Red" })
Write-Host "WARN   : $($warnings.Count)" -ForegroundColor $(if ($warnings.Count -eq 0) { "Green" } else { "Yellow" })

foreach ($b in $blockers) {
    Write-Host "  [BLOCKER] $($b.id): $($b.message)" -ForegroundColor Red
}
foreach ($w in $warnings) {
    Write-Host "  [WARN] $($w.id): $($w.message)" -ForegroundColor Yellow
}

$report = @{
    checkedAt   = (Get-Date).ToUniversalTime().ToString("o")
    baseUrl     = $BaseUrl
    strict      = [bool]$Strict
    legacy      = @{
        packages = $mysqlPkg
        lines    = $mysqlLines
        firms    = $mysqlFirms
        shipments = $mysqlShipments
        shipmentItems = $mysqlShipItems
        ncr      = $mysqlNcr
        capa     = $mysqlCapa
    }
    dg          = @{
        packages = $dgPkg
        lines    = $dgLines
        firms    = $dgFirms
        shipments = $dgShipments
        shipmentItems = $dgShipItems
        ncr      = $dgNcr
        capa     = $dgCapa
        poDocumentPackages = $poWithDoc
    }
    poPdf       = @{
        candidates = $poCandidates
        coveragePercent = $poCoverage
    }
    mojibake    = @{
        sampled = $mojibakeChecked
        hits    = $mojibakeHits
        ratio   = $mojibakeRatio
    }
    blockers    = $blockers
    warnings    = $warnings
    ready       = ($blockers.Count -eq 0)
}
$report | ConvertTo-Json -Depth 6 | Set-Content -Path $reportPath -Encoding UTF8
Write-Host "`nRapor: $reportPath" -ForegroundColor Gray

if ($blockers.Count -gt 0) {
    Write-Host "`nCANLI GECIS HAZIR DEGIL — BLOCKER cozulmeli." -ForegroundColor Red
    exit 1
}

Write-Host "`nVeri dogrulamasi GECTI — UAT walkthrough ve kullanici egitimi kaldi." -ForegroundColor Green
if ($warnings.Count -gt 0) {
    Write-Host "WARN maddeleri go-live oncesi gozden gecirilmeli." -ForegroundColor Yellow
}
exit 0
