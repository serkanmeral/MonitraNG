# Legacy shipments + shipmentitems -> odak_sevkiyatlar + odak_sevkiyat_kalemleri (DG, idempotent)
#
# Usage:
#   .\export-legacy-shipments-from-mysql.ps1
#   .\migrate-legacy-shipments-to-dg.ps1
#   .\migrate-legacy-shipments-to-dg.ps1 -DryRun
#   .\migrate-legacy-shipments-to-dg.ps1 -PackageNo "2023-027"   # tek paket POC
#   .\migrate-legacy-shipments-to-dg.ps1 -RepairText   # mevcut kayitlarda metin alanlarini duzelt

param(
    [string]$LegacyJsonPath = "",
    [string]$BaseUrl = "http://192.168.20.20:5040",
    [switch]$UseGateway = $true,
    [switch]$DryRun,
    [switch]$RepairText,
    [string]$PackageNo = ""
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
$repoRoot = (Resolve-Path (Join-Path $scriptDir "../../../..")).Path
. (Join-Path $scriptDir "lib/DgMigrationCommon.ps1")
. (Join-Path $scriptDir "lib/UpdateOdakLineShippedQuantities.ps1")

if ([string]::IsNullOrEmpty($LegacyJsonPath)) {
    $LegacyJsonPath = Join-Path $scriptDir "..\datasets\legacy-shipments-export.json"
}

$dataPath = if ($UseGateway) { "/data/api/v1/data" } else { "/api/v1/data" }
$ocTokenScript = Join-Path $repoRoot "docs/odak/operationcore/scripts/load-operationcore-token.ps1"
$reportPath = Join-Path $scriptDir "..\datasets\legacy-shipments-migration-report.json"

if (-not (Test-Path $LegacyJsonPath)) {
    throw "JSON yok: $LegacyJsonPath — once export-legacy-shipments-from-mysql.ps1"
}

$token = (& $ocTokenScript).Trim()
if ([string]::IsNullOrEmpty($token)) { throw "Token alinamadi." }

function Update-MigrationToken {
    param([switch]$ForceRefresh)
    if ($ForceRefresh) {
        Write-Host "  Token yenileniyor (Keycloak)..." -ForegroundColor Yellow
        $script:token = (& $ocTokenScript -AutoRefresh).Trim()
    }
    else {
        $tokenFile = if ($env:MNG_OC_USE_PROD_TOKEN -eq "1") { "$env:TEMP\operationcore_dg_token_prod.txt" } else { "$env:TEMP\operationcore_dg_token.txt" }
        if (Test-Path $tokenFile) {
            $script:token = (Get-Content $tokenFile -Raw).Trim()
        }
        else {
            $script:token = (& $ocTokenScript -AutoRefresh:$false).Trim()
        }
        if ([string]::IsNullOrEmpty($script:token)) {
            Write-Host "  Token yenileniyor (Keycloak)..." -ForegroundColor Yellow
            $script:token = (& $ocTokenScript -AutoRefresh).Trim()
        }
    }
    if ([string]::IsNullOrEmpty($script:token)) { throw "Token alinamadi." }
    $script:headers["Authorization"] = "Bearer $($script:token)"
}

$script:headers = @{
    "Authorization" = "Bearer $token"
    "Content-Type"  = "application/json"
}
$script:token = $token
[System.Net.ServicePointManager]::ServerCertificateValidationCallback = { $true }

function Invoke-Dg {
    param([string]$Method, [string]$Uri, [object]$Body = $null)
    for ($attempt = 0; $attempt -lt 2; $attempt++) {
        # Proactive: reload token from file before each call (avoids stale JWT without Keycloak round-trip)
        Update-MigrationToken
        $skipCert = $Uri.StartsWith("https://") -and (Get-Command Invoke-RestMethod).Parameters.ContainsKey("SkipCertificateCheck")
        try {
            return Invoke-DgRestMethod -Method $Method -Uri $Uri -Headers $script:headers -Body $Body -JsonDepth 10 -SkipCertificateCheck:$skipCert
        }
        catch {
            $detail = [string]$_.Exception.Message
            if ($attempt -eq 0 -and ($detail -match '401|Unauthorized')) {
                Write-Host "  401 — Keycloak token yenileniyor..." -ForegroundColor Yellow
                Update-MigrationToken -ForceRefresh
                continue
            }
            throw
        }
    }
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
Write-Host "DryRun: $DryRun  RepairText: $RepairText" -ForegroundColor Gray

$packageMap = Load-LegacyIdMap -InvokeDg ${function:Invoke-Dg} -BaseUrl $BaseUrl -DataPath $dataPath -Dataset "odak_is_paketleri" -LegacyField "legacyPackageId"
Write-Host "Prod sevkiyat map yukleniyor..." -ForegroundColor Gray
$shipmentMap = Load-LegacyIdMap -InvokeDg ${function:Invoke-Dg} -BaseUrl $BaseUrl -DataPath $dataPath -Dataset "odak_sevkiyatlar" -LegacyField "legacyShipmentId"
Write-Host "  prod sevkiyat: $($shipmentMap.Count)" -ForegroundColor Gray
if ($RepairText) {
    Write-Host "RepairText: kalem map atlaniyor" -ForegroundColor Gray
    $lineMap = @{}
    $shipmentItemMap = @{}
}
else {
    $lineMap = Load-LegacyIdMap -InvokeDg ${function:Invoke-Dg} -BaseUrl $BaseUrl -DataPath $dataPath -Dataset "odak_siparis_kalemleri" -LegacyField "legacyLineId"
    Write-Host "  kalem map: $($lineMap.Count)" -ForegroundColor Gray
    Write-Host "Prod sevkiyat kalemi map yukleniyor..." -ForegroundColor Gray
    $shipmentItemMap = Load-LegacyIdMap -InvokeDg ${function:Invoke-Dg} -BaseUrl $BaseUrl -DataPath $dataPath -Dataset "odak_sevkiyat_kalemleri" -LegacyField "legacyShipmentItemId"
    Write-Host "  prod sevkiyat kalemi: $($shipmentItemMap.Count)" -ForegroundColor Gray
}

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
$repaired = 0
$affectedPackages = @{}

$shipIndex = 0
foreach ($s in $shipments) {
    $shipIndex++
    if ($shipIndex % 100 -eq 0) {
        Write-Host "  sevkiyat $shipIndex / $($shipments.Count) (ok=$ok skip=$skip fail=$fail)" -ForegroundColor DarkGray
    }
    if ($shipIndex % 500 -eq 0) {
        Update-MigrationToken -ForceRefresh
    }
    $legacyShipId = [string]$s.id
    if ($RepairText -and -not $shipmentMap.ContainsKey($legacyShipId)) { continue }
    $legacyPkgId = [string]$s.package_id
    $isGeneral = [string]::IsNullOrWhiteSpace($legacyPkgId)

    $existingShipId = if ($shipmentMap.ContainsKey($legacyShipId)) { $shipmentMap[$legacyShipId] } else {
        Find-DgByLegacyId -Map $shipmentMap -LegacyId $legacyShipId -Dataset "odak_sevkiyatlar" -LegacyField "legacyShipmentId"
    }

    $dgPackageId = $null
    if (-not $isGeneral) {
        $dgPackageId = $packageMap[$legacyPkgId]
        if (-not $dgPackageId -and -not ($RepairText -and $existingShipId)) {
            $results += @{ legacyShipmentId = $legacyShipId; status = "skip"; message = "DG paket yok legacyPackageId=$legacyPkgId" }
            $skip++; continue
        }
    }

    $qcf = $qcfByShipment[$legacyShipId]
    $body = @{
        recordScope       = if ($isGeneral) { "Genel" } else { "Paketli" }
        legacyShipmentId  = $legacyShipId
        waybillNo         = if ($s.shipment_no) { Limit-LegacyText $s.shipment_no 64 } elseif ($s.bill_no) { Limit-LegacyText $s.bill_no 64 } else { $null }
        status            = Map-ShipmentStatus ([string]$s.status)
        controlType       = if ($s.inspection_type) { Limit-LegacyText $s.inspection_type 200 } else { $null }
        shipmentAddress   = if ($s.address) { Limit-LegacyText $s.address 500 } else { $null }
        notes             = if ($s.notes) { Limit-LegacyText $s.notes 2000 } elseif ($s.descript) { Limit-LegacyText $s.descript 2000 } else { $null }
        qcfStatus         = Map-QcfStatus $qcf
        qcfReferenceNo    = if ($qcf -and $qcf.qcf_no) { Limit-LegacyText $qcf.qcf_no 64 } elseif ($qcf -and $qcf.form_no) { Limit-LegacyText $qcf.form_no 64 } else { $null }
        qcfNotes          = $null
    }
    if (-not $isGeneral) {
        $body.parentPackageId = $dgPackageId
    }
    if ($isGeneral -and $s.descript) {
        $body.headerDescription = Limit-LegacyText $s.descript 2000
    }
    if ($s.shipment_date) {
        $d = To-IsoDate ([string]$s.shipment_date)
        if ($d) { $body.shipmentDate = $d }
    }

    if ($DryRun) {
        if ($existingShipId -and $RepairText) { $repaired++ }
        $results += @{ legacyShipmentId = $legacyShipId; packageId = $dgPackageId; status = "dry-run"; waybillNo = $body.waybillNo }
        $ok++; continue
    }

    try {
        if ($existingShipId -and $RepairText) {
            $textFields = @("waybillNo", "controlType", "shipmentAddress", "notes", "headerDescription", "qcfReferenceNo", "qcfStatus")
            $patch = @{}
            foreach ($f in $textFields) {
                if ($body.ContainsKey($f) -and $null -ne $body[$f]) { $patch[$f] = $body[$f] }
            }
            if ($patch.Count -gt 0) {
                Invoke-Dg -Method PUT -Uri ('{0}{1}/odak_sevkiyatlar/{2}' -f $BaseUrl, $dataPath, $existingShipId) -Body $patch | Out-Null
                $repaired++
            }
            $results += @{ legacyShipmentId = $legacyShipId; status = "repaired"; waybillNo = $body.waybillNo }
            $ok++; continue
        }

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

        function Upsert-ShipmentItem {
            param([string]$LegacyItemId, [hashtable]$LineBody)
            $existingItemId = if ($shipmentItemMap.ContainsKey($LegacyItemId)) {
                $shipmentItemMap[$LegacyItemId]
            }
            else {
                Find-DgByLegacyId -Map $shipmentItemMap -LegacyId $LegacyItemId -Dataset "odak_sevkiyat_kalemleri" -LegacyField "legacyShipmentItemId"
            }
            if ($existingItemId) {
                Invoke-Dg -Method PUT -Uri ('{0}{1}/odak_sevkiyat_kalemleri/{2}' -f $BaseUrl, $dataPath, $existingItemId) -Body $LineBody | Out-Null
            }
            else {
                $createdItem = Invoke-Dg -Method POST -Uri ('{0}{1}/odak_sevkiyat_kalemleri' -f $BaseUrl, $dataPath) -Body $LineBody
                $newId = Get-DataId $createdItem
                if ($newId) { $shipmentItemMap[$LegacyItemId] = [string]$newId }
            }
        }

        $shipItems = @($itemsByShipment[$legacyShipId])
        $lineIndex = 0
        foreach ($it in $shipItems) {
            $legacyItemId = [string]$it.id
            $lineIndex++

            if ($isGeneral) {
                $desc = if ($s.descript) { Limit-LegacyText $s.descript 500 } else { "Sevkiyat kalemi $lineIndex" }
                $lineBody = @{
                    parentShipmentId     = $dgShipId
                    lineMode             = "Serbest"
                    shippedQuantity      = [double]$it.shipment_count
                    lineDescription      = $desc
                    legacyShipmentItemId = $legacyItemId
                }
                Upsert-ShipmentItem -LegacyItemId $legacyItemId -LineBody $lineBody
                continue
            }

            $legacyLineId = [string]$it.packageitem_id
            $dgLineId = $lineMap[$legacyLineId]
            if (-not $dgLineId) {
                $results += @{ legacyShipmentItemId = $legacyItemId; status = "warn"; message = "kalem yok legacyLineId=$legacyLineId" }
                continue
            }

            $lineBody = @{
                parentShipmentId      = $dgShipId
                lineMode              = "SiparisKalemi"
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

            Upsert-ShipmentItem -LegacyItemId $legacyItemId -LineBody $lineBody
        }

        if (-not $isGeneral -and $dgPackageId) {
            $affectedPackages[$dgPackageId] = $true
        }
        $results += @{ legacyShipmentId = $legacyShipId; dgShipmentId = $dgShipId; status = "ok"; waybillNo = $body.waybillNo; recordScope = $body.recordScope }
        $ok++
    }
    catch {
        $results += @{ legacyShipmentId = $legacyShipId; status = "error"; message = $_.Exception.Message }
        $fail++
    }
}

# Tamamlanan sevkiyatlardan kalem shippedQuantity guncelle (list API)
if (-not $DryRun) {
    Invoke-OdakLineShippedQuantityBackfill -InvokeDg ${function:Invoke-Dg} -BaseUrl $BaseUrl -DataPath $dataPath | Out-Null
}

$report = @{
    migratedAt = (Get-Date).ToUniversalTime().ToString("o")
    dryRun     = [bool]$DryRun
    repairText = [bool]$RepairText
    packageNo  = $PackageNo
    ok         = $ok
    skip       = $skip
    fail       = $fail
    repaired   = $repaired
    results    = $results
}
$report | ConvertTo-Json -Depth 6 | Set-Content -Path $reportPath -Encoding UTF8

Write-Host "`nTamamlandi: OK=$ok SKIP=$skip FAIL=$fail REPAIRED=$repaired" -ForegroundColor Cyan
Write-Host "Rapor: $reportPath" -ForegroundColor Gray
