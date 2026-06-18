# Legacy shipments + shipmentitems -> odak_sevkiyatlar + odak_sevkiyat_kalemleri (DG, idempotent)
#
# Usage:
#   .\export-legacy-shipments-from-mysql.ps1
#   .\migrate-legacy-shipments-to-dg.ps1
#   .\migrate-legacy-shipments-to-dg.ps1 -DryRun
#   .\migrate-legacy-shipments-to-dg.ps1 -PackageNo "2023-027"   # tek paket POC

param(
    [string]$LegacyJsonPath = "",
    [string]$BaseUrl = "http://192.168.20.20:5040",
    [switch]$UseGateway = $true,
    [switch]$DryRun,
    [string]$PackageNo = ""
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
$repoRoot = (Resolve-Path (Join-Path $scriptDir "../../../..")).Path
. (Join-Path $scriptDir "lib/DgMigrationCommon.ps1")

if ([string]::IsNullOrEmpty($LegacyJsonPath)) {
    $LegacyJsonPath = Join-Path $scriptDir "..\datasets\legacy-shipments-export.json"
}

$dataPath = if ($UseGateway) { "/data/api/v1/data" } else { "/api/v1/data" }
$ocTokenScript = Join-Path $repoRoot "docs/odak/operationcore/scripts/load-operationcore-token.ps1"
$reportPath = Join-Path $scriptDir "..\datasets\legacy-shipments-migration-report.json"

if (-not (Test-Path $LegacyJsonPath)) {
    throw "JSON yok: $LegacyJsonPath — once export-legacy-shipments-from-mysql.ps1"
}

$token = & $ocTokenScript
if ([string]::IsNullOrEmpty($token)) { throw "Token alinamadi." }

$headers = @{
    "Authorization" = "Bearer $token"
    "Content-Type"  = "application/json"
}
[System.Net.ServicePointManager]::ServerCertificateValidationCallback = { $true }

function Invoke-Dg {
    param([string]$Method, [string]$Uri, [object]$Body = $null)
    $p = @{ Uri = $Uri; Method = $Method; Headers = $headers; ErrorAction = "Stop" }
    if ($Uri.StartsWith("https://") -and (Get-Command Invoke-RestMethod).Parameters.ContainsKey("SkipCertificateCheck")) {
        $p.SkipCertificateCheck = $true
    }
    if ($null -ne $Body) {
        $p.Body = if ($Body -is [string]) { $Body } else { $Body | ConvertTo-Json -Depth 10 -Compress }
        $p.ContentType = "application/json"
    }
    return Invoke-RestMethod @p
}

function Get-DataId {
    param($Response)
    if (-not $Response) { return $null }
    $d = $Response.data; if (-not $d) { $d = $Response.Data }; if (-not $d) { $d = $Response }
    $id = $d.__dataId; if (-not $id) { $id = $d.dataId }; if (-not $id) { $id = $d.DataId }
    return $id
}

function To-IsoDate {
    param([string]$Value)
    if ([string]::IsNullOrWhiteSpace($Value) -or $Value -eq "NULL") { return $null }
    try { return ([datetime]$Value).ToUniversalTime().ToString("o") }
    catch { return $null }
}

function Map-ShipmentStatus {
    param([string]$LegacyStatus)
    $s = [string]$LegacyStatus
    if ($s -match 'Tamam') { return "Tamamlandi" }
    if ($s -match 'Iptal|İptal') { return "Iptal" }
    if ($s -match 'Plan') { return "Planlandi" }
    return "Planlandi"
}

function Map-QcfStatus {
    param($QcfRow)
    if (-not $QcfRow) { return "Yok" }
    $result = [string]$QcfRow.result
    if ($result -and $result -ne "0" -and $result -ne "NULL") { return "Tamamlandi" }
    if ($QcfRow.qcf_no -or $QcfRow.form_no) { return "Bekliyor" }
    return "Bekliyor"
}

function Find-DgByLegacyId {
    param(
        [hashtable]$Map,
        [string]$LegacyId,
        [string]$Dataset,
        [string]$LegacyField
    )
    if ($Map.ContainsKey($LegacyId)) { return $Map[$LegacyId] }
    $filter = "{0}:eq:{1}" -f $LegacyField, $LegacyId
    $uri = '{0}{1}/{2}?filter={3}&limit=1' -f $BaseUrl, $dataPath, $Dataset, [Uri]::EscapeDataString($filter)
    try {
        $raw = Invoke-Dg -Method GET -Uri $uri
        $items = @()
        if ($raw -is [Array]) { $items = @($raw) }
        elseif ($raw.items) { $items = @($raw.items) }
        if ($items.Count -gt 0) {
            $id = $items[0].__dataId; if (-not $id) { $id = $items[0].dataId }
            if ($id) { $Map[$LegacyId] = [string]$id; return [string]$id }
        }
    }
    catch { }
    return $null
}

$export = Get-Content $LegacyJsonPath -Raw -Encoding UTF8 | ConvertFrom-Json
$shipments = @($export.shipments)
$items = @($export.shipmentitems)
$qcfs = @($export.qcfs)

$qcfByShipment = @{}
foreach ($q in $qcfs) {
    $sid = [string]$q.shipment_id
    if ($sid) { $qcfByShipment[$sid] = $q }
}

$itemsByShipment = @{}
foreach ($it in $items) {
    $sid = [string]$it.shipment_id
    if (-not $itemsByShipment.ContainsKey($sid)) { $itemsByShipment[$sid] = @() }
    $itemsByShipment[$sid] += $it
}

Write-Host "`n=== migrate-legacy-shipments-to-dg ===" -ForegroundColor Cyan
Write-Host "Kaynak: $($shipments.Count) shipment, $($items.Count) item" -ForegroundColor Gray

$packageMap = Load-LegacyIdMap -InvokeDg ${function:Invoke-Dg} -BaseUrl $BaseUrl -DataPath $dataPath -Dataset "odak_is_paketleri" -LegacyField "legacyPackageId"
$lineMap = Load-LegacyIdMap -InvokeDg ${function:Invoke-Dg} -BaseUrl $BaseUrl -DataPath $dataPath -Dataset "odak_siparis_kalemleri" -LegacyField "legacyLineId"
$shipmentMap = @{}

if ($PackageNo) {
    $filterUri = '{0}{1}/odak_is_paketleri?filter={2}&limit=5' -f $BaseUrl, $dataPath, [Uri]::EscapeDataString("packageNo:eq:$PackageNo")
    $raw = Invoke-Dg -Method GET -Uri $filterUri
    $pkgRows = @()
    if ($raw -is [Array]) { $pkgRows = @($raw) }
    elseif ($raw.items) { $pkgRows = @($raw.items) }
    elseif ($raw.__dataId -or $raw.dataId) { $pkgRows = @($raw) }
    $pkgRow = $pkgRows | Where-Object { [string]$_.packageNo -eq $PackageNo } | Select-Object -First 1
    if (-not $pkgRow) { throw "DG paket bulunamadi: $PackageNo" }
    $targetLegacyPackageId = [string]$pkgRow.legacyPackageId
    if ([string]::IsNullOrWhiteSpace($targetLegacyPackageId)) {
        throw "legacyPackageId yok (packageNo=$PackageNo) — once paket migrasyonu calistirilmis olmali."
    }
    $shipments = @($shipments | Where-Object { [string]$_.package_id -eq $targetLegacyPackageId })
    Write-Host "PackageNo $PackageNo -> legacyPackageId=$targetLegacyPackageId, shipment=$($shipments.Count)" -ForegroundColor Gray
}

$results = @()
$ok = 0
$skip = 0
$fail = 0
$affectedPackages = @{}

foreach ($s in $shipments) {
    $legacyShipId = [string]$s.id
    $legacyPkgId = [string]$s.package_id
    if (-not $legacyPkgId) {
        $results += @{ legacyShipmentId = $legacyShipId; status = "skip"; message = "package_id yok" }
        $skip++; continue
    }

    $dgPackageId = $packageMap[$legacyPkgId]
    if (-not $dgPackageId) {
        $results += @{ legacyShipmentId = $legacyShipId; status = "skip"; message = "DG paket yok legacyPackageId=$legacyPkgId" }
        $skip++; continue
    }

    $existingShipId = Find-DgByLegacyId -Map $shipmentMap -LegacyId $legacyShipId -Dataset "odak_sevkiyatlar" -LegacyField "legacyShipmentId"

    $qcf = $qcfByShipment[$legacyShipId]
    $body = @{
        parentPackageId   = $dgPackageId
        legacyShipmentId  = $legacyShipId
        waybillNo         = if ($s.shipment_no) { [string]$s.shipment_no } elseif ($s.bill_no) { [string]$s.bill_no } else { $null }
        status            = Map-ShipmentStatus ([string]$s.status)
        controlType       = if ($s.inspection_type) { [string]$s.inspection_type } else { $null }
        shipmentAddress   = if ($s.address) { [string]$s.address } else { $null }
        notes             = if ($s.notes) { [string]$s.notes } else { if ($s.descript) { [string]$s.descript } else { $null } }
        qcfStatus         = Map-QcfStatus $qcf
        qcfReferenceNo    = if ($qcf -and $qcf.qcf_no) { [string]$qcf.qcf_no } elseif ($qcf -and $qcf.form_no) { [string]$qcf.form_no } else { $null }
        qcfNotes          = $null
    }
    if ($s.shipment_date) {
        $d = To-IsoDate ([string]$s.shipment_date)
        if ($d) { $body.shipmentDate = $d }
    }

    if ($DryRun) {
        $results += @{ legacyShipmentId = $legacyShipId; packageId = $dgPackageId; status = "dry-run"; waybillNo = $body.waybillNo }
        $ok++; continue
    }

    try {
        if ($existingShipId) {
            Invoke-Dg -Method PUT -Uri ('{0}{1}/odak_sevkiyatlar/{2}' -f $BaseUrl, $dataPath, $existingShipId) -Body $body | Out-Null
            $dgShipId = $existingShipId
        }
        else {
            $created = Invoke-Dg -Method POST -Uri ('{0}{1}/odak_sevkiyatlar' -f $BaseUrl, $dataPath) -Body $body
            $dgShipId = Get-DataId $created
            if ($dgShipId) { $shipmentMap[$legacyShipId] = $dgShipId }
        }

        if (-not $dgShipId) {
            throw "DG shipment id alinamadi"
        }

        # Mevcut satirlari sil (idempotent re-run)
        $filter = "parentShipmentId:eq:$dgShipId"
        $listUri = '{0}{1}/odak_sevkiyat_kalemleri?filter={2}&limit=500' -f $BaseUrl, $dataPath, [Uri]::EscapeDataString($filter)
        $existingLines = @()
        try {
            $lr = Invoke-Dg -Method GET -Uri $listUri
            if ($lr -is [Array]) { $existingLines = @($lr) }
            elseif ($lr.items) { $existingLines = @($lr.items) }
        }
        catch { }
        foreach ($el in $existingLines) {
            $eid = $el.__dataId; if (-not $eid) { $eid = $el.dataId }
            if ($eid) {
                Invoke-Dg -Method DELETE -Uri ('{0}{1}/odak_sevkiyat_kalemleri/{2}' -f $BaseUrl, $dataPath, $eid) | Out-Null
            }
        }

        $shipItems = @($itemsByShipment[$legacyShipId])
        foreach ($it in $shipItems) {
            $legacyItemId = [string]$it.id
            $legacyLineId = [string]$it.packageitem_id
            $dgLineId = $lineMap[$legacyLineId]
            if (-not $dgLineId) {
                $results += @{ legacyShipmentItemId = $legacyItemId; status = "warn"; message = "kalem yok legacyLineId=$legacyLineId" }
                continue
            }

            $lineBody = @{
                parentShipmentId      = $dgShipId
                parentPackageId       = $dgPackageId
                parentLineId          = $dgLineId
                shippedQuantity       = [double]$it.shipment_count
                legacyShipmentItemId  = $legacyItemId
            }

            # line snapshot
            try {
                $lineRow = Invoke-Dg -Method GET -Uri ('{0}{1}/odak_siparis_kalemleri/{2}' -f $BaseUrl, $dataPath, $dgLineId)
                if ($lineRow.lineNo) { $lineBody.lineNo = [int]$lineRow.lineNo }
                if ($lineRow.description) { $lineBody.lineDescription = [string]$lineRow.description }
            }
            catch { }

            Invoke-Dg -Method POST -Uri ('{0}{1}/odak_sevkiyat_kalemleri' -f $BaseUrl, $dataPath) -Body $lineBody | Out-Null
        }

        $affectedPackages[$dgPackageId] = $true
        $results += @{ legacyShipmentId = $legacyShipId; dgShipmentId = $dgShipId; status = "ok"; waybillNo = $body.waybillNo }
        $ok++
    }
    catch {
        $results += @{ legacyShipmentId = $legacyShipId; status = "error"; message = $_.Exception.Message }
        $fail++
    }
}

# Tamamlanan sevkiyatlardan kalem shippedQuantity guncelle
if (-not $DryRun -and $affectedPackages.Count -gt 0) {
    Write-Host "`nKalem sevk miktarlari guncelleniyor ($($affectedPackages.Count) paket)..." -ForegroundColor Yellow
    foreach ($pkgId in $affectedPackages.Keys) {
        $shUri = '{0}{1}/odak_sevkiyatlar?filter={2}&limit=500' -f $BaseUrl, $dataPath, [Uri]::EscapeDataString("parentPackageId:eq:$pkgId")
        $shipRows = @()
        try {
            $sr = Invoke-Dg -Method GET -Uri $shUri
            if ($sr -is [Array]) { $shipRows = @($sr) }
            elseif ($sr.items) { $shipRows = @($sr.items) }
        }
        catch { continue }

        $completedIds = @($shipRows | Where-Object { (Map-ShipmentStatus ([string]$_.status)) -eq "Tamamlandi" } | ForEach-Object {
            $id = $_.__dataId; if (-not $id) { $id = $_.dataId }
            $id
        } | Where-Object { $_ })

        $lineQty = @{}
        foreach ($cs in $completedIds) {
            $siUri = '{0}{1}/odak_sevkiyat_kalemleri?filter={2}&limit=500' -f $BaseUrl, $dataPath, [Uri]::EscapeDataString("parentShipmentId:eq:$cs")
            try {
                $sir = Invoke-Dg -Method GET -Uri $siUri
                $siRows = @()
                if ($sir -is [Array]) { $siRows = @($sir) }
                elseif ($sir.items) { $siRows = @($sir.items) }
                foreach ($si in $siRows) {
                    $lid = Get-RelationId $si.parentLineId
                    if (-not $lid) { continue }
                    $q = [double]$si.shippedQuantity
                    if (-not $lineQty.ContainsKey($lid)) { $lineQty[$lid] = 0.0 }
                    $lineQty[$lid] += $q
                }
            }
            catch { }
        }

        $lineUri = '{0}{1}/odak_siparis_kalemleri?filter={2}&limit=500' -f $BaseUrl, $dataPath, [Uri]::EscapeDataString("parentPackageId:eq:$pkgId")
        try {
            $lr = Invoke-Dg -Method GET -Uri $lineUri
            $lines = @()
            if ($lr -is [Array]) { $lines = @($lr) }
            elseif ($lr.items) { $lines = @($lr.items) }
            foreach ($line in $lines) {
                $lid = $line.__dataId; if (-not $lid) { $lid = $line.dataId }
                if (-not $lid) { continue }
                $shipped = if ($lineQty.ContainsKey([string]$lid)) { $lineQty[[string]$lid] } else { 0.0 }
                Invoke-Dg -Method PUT -Uri ('{0}{1}/odak_siparis_kalemleri/{2}' -f $BaseUrl, $dataPath, $lid) -Body @{ shippedQuantity = $shipped } | Out-Null
            }
        }
        catch { Write-Warning "Paket $pkgId kalem guncelleme: $_" }
    }
}

$report = @{
    migratedAt = (Get-Date).ToUniversalTime().ToString("o")
    dryRun     = [bool]$DryRun
    packageNo  = $PackageNo
    ok         = $ok
    skip       = $skip
    fail       = $fail
    results    = $results
}
$report | ConvertTo-Json -Depth 6 | Set-Content -Path $reportPath -Encoding UTF8

Write-Host "`nTamamlandi: OK=$ok SKIP=$skip FAIL=$fail" -ForegroundColor Cyan
Write-Host "Rapor: $reportPath" -ForegroundColor Gray
