# Odak Uretim — tamamlanmis (kapali) referans uretim emri
# Profil "Detaylar" sekmesinde tum lifecycle alanlarini dogrulamak icin.
#
# On kosul:
#   .\docs\odak\operationcore\scripts\get-operationcore-token.ps1
#   .\docs\odak\is_surecleri\scripts\seed-operation-core-odak-uretim.ps1  (metadata)
#   .\docs\odak\is_surecleri\scripts\setup-odak-master-datasets.ps1 + seed-odak-master-data.ps1
#
# Kullanim:
#   .\docs\odak\is_surecleri\scripts\seed-odak-closed-reference-order.ps1
#   .\docs\odak\is_surecleri\scripts\seed-odak-closed-reference-order.ps1 -Force   # yeniden olustur (yeni kayit)

param(
    [string]$MoBaseUrl = "http://192.168.20.20:5040/operations",
    [switch]$Force = $false
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
$repoRoot = (Resolve-Path (Join-Path $scriptDir "../../../..")).Path
$ocScripts = Join-Path $repoRoot "docs/odak/operationcore/scripts"
$seedSummaryPath = Join-Path $scriptDir "../seed/odak-uretim-seed.json"
$masterIdsPath = Join-Path $scriptDir "../seed/odak_master_ids.json"
$refOutputPath = Join-Path $scriptDir "../seed/odak-closed-reference.json"

$refTitle = "[REFERANS] Tamamlanmis uretim emri — profil kontrolu"
$refPo = "PO-REF-KAPALI-001"

$loadTokenScript = Join-Path $ocScripts "load-operationcore-token.ps1"
$token = & $loadTokenScript
if ([string]::IsNullOrEmpty($token)) { throw "Token alinamadi. get-operationcore-token.ps1 calistirin." }

$headers = @{
    Authorization = "Bearer $token"
    "Content-Type" = "application/json"
}
$moParams = @{ Headers = $headers; ErrorAction = "Stop" }
if ($MoBaseUrl.StartsWith("https://")) { $moParams.SkipCertificateCheck = $true }

function Invoke-MoTransition {
    param(
        [string]$WorkItemId,
        [string]$TransitionKey,
        [hashtable]$Fields = @{}
    )
    $body = if ($Fields.Count -gt 0) {
        @{ fields = $Fields } | ConvertTo-Json -Depth 8 -Compress
    } else {
        "{}"
    }
    Invoke-RestMethod -Uri "$MoBaseUrl/api/v1/work-items/$WorkItemId/transitions/$TransitionKey" -Method POST -Body $body @moParams | Out-Null
}

function Test-ReferenceExists {
    param([string]$WorkItemId)
    try {
        $pv = Invoke-RestMethod -Uri "$MoBaseUrl/api/v1/runtime/work-items/$WorkItemId/profile-view" @moParams
        return $null -ne $pv.profile.workItem.id
    } catch {
        return $false
    }
}

if (-not (Test-Path $seedSummaryPath)) {
    throw "Once seed-operation-core-odak-uretim.ps1 calistirin. Beklenen: $seedSummaryPath"
}
$seed = Get-Content $seedSummaryPath -Raw -Encoding UTF8 | ConvertFrom-Json

$workspaceId = $seed.workspaceId
$typeOrderId = $seed.types.order
$boardProdId = $seed.boardProdId
$prioNormalId = $seed.priorities.normal

$productId = $null
$customerId = $null
$productGroupId = $null
if (Test-Path $masterIdsPath) {
    $mids = Get-Content $masterIdsPath -Raw -Encoding UTF8 | ConvertFrom-Json
    if ($mids.urunler.'ODK-CMP-1001') { $productId = $mids.urunler.'ODK-CMP-1001' }
    if ($mids.musteriler.'MUS-001') { $customerId = $mids.musteriler.'MUS-001' }
    if ($mids.urunGruplari.'UG-KOM') { $productGroupId = $mids.urunGruplari.'UG-KOM' }
}
if (-not $productId -or -not $customerId -or -not $productGroupId) {
    throw "Master id eksik. setup-odak-master-datasets.ps1 + seed-odak-master-data.ps1 calistirin."
}

if ((Test-Path $refOutputPath) -and -not $Force) {
    $existing = Get-Content $refOutputPath -Raw -Encoding UTF8 | ConvertFrom-Json
    if ($existing.workItemId -and (Test-ReferenceExists -WorkItemId $existing.workItemId)) {
        Write-Host "`nReferans kayit zaten mevcut (yeniden olusturmak icin -Force):" -ForegroundColor Yellow
        Write-Host "  Key:  $($existing.workItemKey)" -ForegroundColor Cyan
        Write-Host "  Id:   $($existing.workItemId)" -ForegroundColor Gray
        Write-Host "  PO:   $refPo" -ForegroundColor Gray
        Write-Host "  UI:   Operasyon Merkezi -> Odak Uretim -> profil -> $($existing.workItemKey)" -ForegroundColor Cyan
        exit 0
    }
}

Write-Host "`n[REFERANS] Kapali uretim emri olusturuluyor..." -ForegroundColor Yellow

$qty = 8
$plannedDate = (Get-Date).AddDays(-21).ToUniversalTime().ToString("o")

$createBody = @{
    workspaceId = $workspaceId
    typeId      = $typeOrderId
    title       = $refTitle
    boardId     = $boardProdId
    fields      = @{
        priorityId       = $prioNormalId
        orderType        = "seri"
        customerId       = $customerId
        customerOrderRef = $refPo
        description      = "Profil detay sekmesi referans kaydi — tum asama alanlari dolu, durum: Kapali."
    }
} | ConvertTo-Json -Depth 8 -Compress

$created = Invoke-RestMethod -Uri "$MoBaseUrl/api/v1/work-items" -Method POST -Body $createBody @moParams
$wiId = $created.workItem.id
$wiKey = $created.workItem.key
Write-Host "  Olusturuldu: $wiKey" -ForegroundColor Green

Invoke-MoTransition -WorkItemId $wiId -TransitionKey "plan" -Fields @{
    productGroupId = $productGroupId
    productId      = $productId
    quantity       = $qty
    plannedDate    = $plannedDate
    orderType      = "seri"
}

Invoke-MoTransition -WorkItemId $wiId -TransitionKey "start_production" -Fields @{
    workCenter           = "Otomatik layup hatti"
    productionStartNote  = "Referans seed — uretim baslatildi"
}

Invoke-MoTransition -WorkItemId $wiId -TransitionKey "send_to_quality" -Fields @{
    lotSerial    = "LOT-REF-2026-001"
    producedQty  = $qty
}

Invoke-MoTransition -WorkItemId $wiId -TransitionKey "approve_quality" -Fields @{
    qualityResult      = "uygun"
    qualityNotes       = "Final muayene uygun — referans kayit"
    inspectionType     = "kombine"
    acceptedQty        = $qty
    rejectedQty        = 0
    measurementSummary = "Olculer tolerans icinde (referans)"
}

Invoke-MoTransition -WorkItemId $wiId -TransitionKey "move_to_ship_prep" -Fields @{
    storageLocation = "DEPO-A / Raf 12"
    packagingOk     = $true
}

Invoke-MoTransition -WorkItemId $wiId -TransitionKey "ship_partial" -Fields @{
    waybillNo      = "IRS-REF-2026-8842"
    shipmentQty    = $qty
    shipmentNotes  = "Tam sevkiyat — referans emri"
}

Invoke-MoTransition -WorkItemId $wiId -TransitionKey "ship_complete"
Invoke-MoTransition -WorkItemId $wiId -TransitionKey "close_order"

$profile = Invoke-RestMethod -Uri "$MoBaseUrl/api/v1/runtime/work-items/$wiId/profile-view" @moParams
$stateName = $profile.profile.workItem.stateId
$displayCount = @($profile.displayForm.fields.PSObject.Properties).Count
$sampleFields = @("productGroupId", "workCenter", "acceptedQty", "storageLocation", "waybillNo")
$filled = @()
foreach ($fk in $sampleFields) {
    $v = $profile.displayForm.fields.$fk.value
    if ($null -ne $v -and "$v".Length -gt 0) { $filled += $fk }
}

Write-Host "  Akis tamamlandi -> durum id: $stateName" -ForegroundColor Green
Write-Host "  displayForm alan sayisi: $displayCount" -ForegroundColor Gray
Write-Host "  ornek dolu alanlar: $($filled -join ', ')" -ForegroundColor Gray

$refDoc = @{
    workItemId    = $wiId
    workItemKey   = $wiKey
    title         = $refTitle
    customerOrderRef = $refPo
    workspaceId   = $workspaceId
    boardId       = $boardProdId
    quantity      = $qty
    stateId       = $stateName
    displayFormFieldCount = $displayCount
    seededAt      = (Get-Date).ToUniversalTime().ToString("o")
    moBaseUrl     = $MoBaseUrl
    uiHint        = "Operasyon Merkezi -> Odak Uretim -> Uretim panosu -> $wiKey -> Detaylar"
}
$refDoc | ConvertTo-Json -Depth 6 | Set-Content -Path $refOutputPath -Encoding UTF8

Write-Host "`nTamamlandi." -ForegroundColor Cyan
Write-Host "  Key:  $wiKey" -ForegroundColor White
Write-Host "  PO:   $refPo" -ForegroundColor White
Write-Host "  Ozet: $refOutputPath" -ForegroundColor Gray
Write-Host "  UI:   $refTitle ($wiKey) — Detaylar sekmesinde Ozet / Siparis / Uretim / Kalite / Depo bolumlerini kontrol edin." -ForegroundColor Cyan
