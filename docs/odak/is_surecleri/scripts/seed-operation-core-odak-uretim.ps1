# Operation Core — Odak Uretim workspace seed (DG + opsiyonel MO smoke/demo)
#
# Ref: docs/odak/is_surecleri/referans/ODAK_URETIM_WORKSPACE_TASLAK.md
#
# Usage (repo kokunden):
#   .\docs\odak\operationcore\scripts\get-operationcore-token.ps1
#   .\docs\odak\is_surecleri\scripts\setup-odak-master-datasets.ps1
#   .\docs\odak\is_surecleri\scripts\seed-odak-master-data.ps1
#   .\docs\odak\is_surecleri\scripts\seed-operation-core-odak-uretim.ps1
#   .\docs\odak\is_surecleri\scripts\seed-operation-core-odak-uretim.ps1 -SmokeTest -SeedDemo

param(
    [string]$BaseUrl = "http://192.168.20.20:5040",
    [string]$MoBaseUrl = "http://192.168.20.20:5040/operations",
    [switch]$UseGateway = $true,
    [switch]$SmokeTest = $false,
    [switch]$SeedDemo = $false,
    [switch]$ReloadMetadataCache = $false,
    [string]$OutputFile = ""
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
$repoRoot = (Resolve-Path (Join-Path $scriptDir "../../../..")).Path
$ocScripts = Join-Path $repoRoot "docs/odak/operationcore/scripts"
if ([string]::IsNullOrEmpty($OutputFile)) {
    $OutputFile = Join-Path $scriptDir "../seed/odak-uretim-seed.json"
}

$dataPath = if ($UseGateway) { "/data/api/v1/data" } else { "/api/v1/data" }
$tag = "Odak Uretim"
$workspaceName = "Odak Uretim"

$loadTokenScript = Join-Path $ocScripts "load-operationcore-token.ps1"
$token = & $loadTokenScript
if ([string]::IsNullOrEmpty($token)) { throw "Token alinamadi." }

$headers = @{
    "Authorization" = "Bearer $token"
    "Content-Type"  = "application/json"
}
[System.Net.ServicePointManager]::ServerCertificateValidationCallback = { $true }
$irmParams = @{ Headers = $headers; ErrorAction = "Stop" }
if ($BaseUrl.StartsWith("https://") -and (Get-Command Invoke-RestMethod -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Parameters | Where-Object { $_.Name -eq "SkipCertificateCheck" })) {
    $irmParams.SkipCertificateCheck = $true
}

function Invoke-DgPost {
    param([string]$Collection, [object]$Body)
    $uri = "$BaseUrl$dataPath/$Collection"
    $json = if ($Body -is [string]) { $Body } else { $Body | ConvertTo-Json -Depth 25 -Compress }
    $params = @{ Uri = $uri; Method = "POST"; Body = $json } + $irmParams
    return Invoke-RestMethod @params
}

function Invoke-DgGet {
    param([string]$Collection, [string]$Filter = "", [int]$Limit = 100)
    $uri = "$BaseUrl$dataPath/$Collection`?limit=$Limit"
    if (-not [string]::IsNullOrEmpty($Filter)) { $uri += "&filter=" + [Uri]::EscapeDataString($Filter) }
    $params = @{ Uri = $uri; Method = "GET" } + $irmParams
    return Invoke-RestMethod @params
}

function Invoke-DgPut {
    param([string]$Collection, [string]$Id, [object]$Body)
    $uri = "$BaseUrl$dataPath/$Collection/$Id"
    $json = $Body | ConvertTo-Json -Depth 25 -Compress
    $params = @{ Uri = $uri; Method = "PUT"; Body = $json } + $irmParams
    return Invoke-RestMethod @params
}

function Get-DataId {
    param($Response)
    if (-not $Response) { return $null }
    $d = $Response.data; if (-not $d) { $d = $Response.Data }; if (-not $d) { $d = $Response }
    $id = $d.__dataId; if (-not $id) { $id = $d.dataId }; if (-not $id) { $id = $d.DataId }
    return $id
}

function Get-Items {
    param($Response)
    if (-not $Response) { return @() }
    if ($Response -is [Array]) { return $Response }
    foreach ($prop in @("data", "Data", "items", "Items", "results", "Results")) {
        if ($null -ne $Response.$prop) {
            $items = $Response.$prop
            if ($items -is [Array]) { return $items }
            return @($items)
        }
    }
    return @($Response)
}

function Find-OrCreate {
    param([string]$Collection, [string]$Filter, [object]$Body, [string]$Label)
    $existing = @(Get-Items (Invoke-DgGet -Collection $Collection -Filter $Filter -Limit 5))
    if ($existing.Count -gt 0) {
        $id = $existing[0].__dataId; if (-not $id) { $id = $existing[0].dataId }
        Write-Host "  SKIP: $Label ($id)" -ForegroundColor Yellow
        return $id
    }
    try {
        $created = Invoke-DgPost -Collection $Collection -Body $Body
        $id = Get-DataId $created
        Write-Host "  OK: $Label -> $id" -ForegroundColor Green
        return $id
    }
    catch {
        $retry = @(Get-Items (Invoke-DgGet -Collection $Collection -Filter $Filter -Limit 5))
        if ($retry.Count -gt 0) {
            $id = $retry[0].__dataId; if (-not $id) { $id = $retry[0].dataId }
            Write-Host "  SKIP: $Label (duplicate -> $id)" -ForegroundColor Yellow
            return $id
        }
        throw
    }
}

function Sync-Record {
    param([string]$Collection, [string]$Id, [object]$Body, [string]$Label)
    try {
        Invoke-DgPut -Collection $Collection -Id $Id -Body $Body | Out-Null
        Write-Host "  SYNC: $Label ($Id)" -ForegroundColor Green
    }
    catch {
        Write-Host "  WARN: SYNC $Label — $($_.Exception.Message)" -ForegroundColor DarkYellow
    }
}

function New-LookupOptions {
    param(
        [string]$DatasetName,
        [string]$LabelField = "ad",
        [string[]]$SearchFields = @("ad"),
        [string]$Filter = "aktif:eq:true",
        [hashtable]$DependsOn = $null
    )
    $lookup = @{
        source       = "dataset"
        presentation = "autocomplete"
        valueField   = "__dataId"
        labelField   = $LabelField
        searchFields = $SearchFields
        pageSize     = 50
    }
    if ($Filter) { $lookup.filter = $Filter }
    if ($DependsOn) { $lookup.dependsOn = $DependsOn }
    return @{ lookup = $lookup }
}

function New-StaticSelectOptions {
    param([array]$Items)
    return @{
        lookup = @{
            source       = "static"
            presentation = "dropdown"
            staticItems  = $Items
        }
    }
}

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "Odak Uretim — Operation Core seed" -ForegroundColor Cyan
Write-Host "Gateway: $BaseUrl" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

# --- 1. States (ana emir) ---
Write-Host "[1] op_states (ana emir)..." -ForegroundColor Yellow
$stNew = Find-OrCreate -Collection "op_states" -Filter "name:eq:$tag - Yeni" -Label "Yeni" -Body @{
    name = "$tag - Yeni"; category = "open"; isInitial = $true; isStart = $true; color = "info"; sortOrder = 10
}
$stPlanned = Find-OrCreate -Collection "op_states" -Filter "name:eq:$tag - Planlandi" -Label "Planlandi" -Body @{
    name = "$tag - Planlandi"; category = "open"; color = "primary"; sortOrder = 20
}
$stProduction = Find-OrCreate -Collection "op_states" -Filter "name:eq:$tag - Uretimde" -Label "Uretimde" -Body @{
    name = "$tag - Uretimde"; category = "in_progress"; color = "warning"; sortOrder = 30
}
$stQuality = Find-OrCreate -Collection "op_states" -Filter "name:eq:$tag - Kalite kontrol" -Label "Kalite kontrol" -Body @{
    name = "$tag - Kalite kontrol"; category = "in_progress"; color = "info"; sortOrder = 40
}
$stQualityHold = Find-OrCreate -Collection "op_states" -Filter "name:eq:$tag - Kalite bekliyor" -Label "Kalite bekliyor" -Body @{
    name = "$tag - Kalite bekliyor"; category = "on_hold"; color = "secondary"; sortOrder = 50
}
$stStorage = Find-OrCreate -Collection "op_states" -Filter "name:eq:$tag - Depoda" -Label "Depoda" -Body @{
    name = "$tag - Depoda"; category = "in_progress"; color = "primary"; sortOrder = 60
}
$stShipPrep = Find-OrCreate -Collection "op_states" -Filter "name:eq:$tag - Sevkiyat hazir" -Label "Sevkiyat hazir" -Body @{
    name = "$tag - Sevkiyat hazir"; category = "in_progress"; color = "warning"; sortOrder = 70
}
$stShipped = Find-OrCreate -Collection "op_states" -Filter "name:eq:$tag - Sevk edildi" -Label "Sevk edildi" -Body @{
    name = "$tag - Sevk edildi"; category = "closed"; color = "success"; sortOrder = 80
}
$stClosed = Find-OrCreate -Collection "op_states" -Filter "name:eq:$tag - Kapandi" -Label "Kapandi" -Body @{
    name = "$tag - Kapandi"; category = "closed"; isClosed = $true; isTerminal = $true; color = "secondary"; sortOrder = 90
}

# --- 2. States (NCR) ---
Write-Host "[2] op_states (NCR)..." -ForegroundColor Yellow
$stNcrOpen = Find-OrCreate -Collection "op_states" -Filter "name:eq:$tag - NCR Acik" -Label "NCR Acik" -Body @{
    name = "$tag - NCR Acik"; category = "open"; isInitial = $true; color = "error"; sortOrder = 110
}
$stNcrContain = Find-OrCreate -Collection "op_states" -Filter "name:eq:$tag - Kontrol altinda" -Label "Kontrol altinda" -Body @{
    name = "$tag - Kontrol altinda"; category = "in_progress"; color = "warning"; sortOrder = 120
}
$stNcrReview = Find-OrCreate -Collection "op_states" -Filter "name:eq:$tag - Degerlendirme" -Label "Degerlendirme" -Body @{
    name = "$tag - Degerlendirme"; category = "in_progress"; color = "info"; sortOrder = 130
}
$stNcrDecided = Find-OrCreate -Collection "op_states" -Filter "name:eq:$tag - Karar verildi" -Label "Karar verildi" -Body @{
    name = "$tag - Karar verildi"; category = "closed"; allowReopen = $true; color = "success"; sortOrder = 140
}
$stNcrClosed = Find-OrCreate -Collection "op_states" -Filter "name:eq:$tag - NCR Kapandi" -Label "NCR Kapandi" -Body @{
    name = "$tag - NCR Kapandi"; category = "closed"; isClosed = $true; isTerminal = $true; color = "secondary"; sortOrder = 150
}

# --- 3. States (CAPA) ---
Write-Host "[3] op_states (CAPA)..." -ForegroundColor Yellow
$stCapaOpen = Find-OrCreate -Collection "op_states" -Filter "name:eq:$tag - CAPA Acik" -Label "CAPA Acik" -Body @{
    name = "$tag - CAPA Acik"; category = "open"; isInitial = $true; color = "error"; sortOrder = 210
}
$stCapaRoot = Find-OrCreate -Collection "op_states" -Filter "name:eq:$tag - Kok neden" -Label "Kok neden" -Body @{
    name = "$tag - Kok neden"; category = "in_progress"; color = "warning"; sortOrder = 220
}
$stCapaPlan = Find-OrCreate -Collection "op_states" -Filter "name:eq:$tag - Aksiyon plani" -Label "Aksiyon plani" -Body @{
    name = "$tag - Aksiyon plani"; category = "in_progress"; color = "info"; sortOrder = 230
}
$stCapaImpl = Find-OrCreate -Collection "op_states" -Filter "name:eq:$tag - Uygulama" -Label "Uygulama" -Body @{
    name = "$tag - Uygulama"; category = "in_progress"; color = "primary"; sortOrder = 240
}
$stCapaVerify = Find-OrCreate -Collection "op_states" -Filter "name:eq:$tag - Dogrulama" -Label "Dogrulama" -Body @{
    name = "$tag - Dogrulama"; category = "in_progress"; color = "info"; sortOrder = 250
}
$stCapaClosed = Find-OrCreate -Collection "op_states" -Filter "name:eq:$tag - CAPA Kapandi" -Label "CAPA Kapandi" -Body @{
    name = "$tag - CAPA Kapandi"; category = "closed"; isClosed = $true; isTerminal = $true; color = "secondary"; sortOrder = 260
}

$allStateIds = @(
    $stNew, $stPlanned, $stProduction, $stQuality, $stQualityHold, $stStorage, $stShipPrep, $stShipped, $stClosed,
    $stNcrOpen, $stNcrContain, $stNcrReview, $stNcrDecided, $stNcrClosed,
    $stCapaOpen, $stCapaRoot, $stCapaPlan, $stCapaImpl, $stCapaVerify, $stCapaClosed
)

# --- 4. Priorities ---
Write-Host "[4] op_priorities..." -ForegroundColor Yellow
$prioUrgentId = Find-OrCreate -Collection "op_priorities" -Filter "name:eq:ODF - Acil" -Label "Acil" -Body @{
    name = "ODF - Acil"; level = "1"; sortOrder = 10; color = "error"
}
$prioHighId = Find-OrCreate -Collection "op_priorities" -Filter "name:eq:ODF - Yuksek" -Label "Yuksek" -Body @{
    name = "ODF - Yuksek"; level = "2"; sortOrder = 20; color = "warning"
}
$prioNormalId = Find-OrCreate -Collection "op_priorities" -Filter "name:eq:ODF - Normal" -Label "Normal" -Body @{
    name = "ODF - Normal"; level = "3"; sortOrder = 30; color = "info"
}
$prioLowId = Find-OrCreate -Collection "op_priorities" -Filter "name:eq:ODF - Dusuk" -Label "Dusuk" -Body @{
    name = "ODF - Dusuk"; level = "4"; sortOrder = 40; color = "secondary"
}
$allPriorityIds = @($prioUrgentId, $prioHighId, $prioNormalId, $prioLowId)

# --- 5. Pool fields ---
Write-Host "[5] op_fields..." -ForegroundColor Yellow
$optQuality = New-StaticSelectOptions @(
    @{ value = "uygun"; label = "Uygun" },
    @{ value = "uygunsuz"; label = "Uygunsuz" },
    @{ value = "sartli"; label = "Sartli" }
)
$optNcrSource = New-StaticSelectOptions @(
    @{ value = "girdi"; label = "Girdi muayene" },
    @{ value = "proses"; label = "Proses ici" },
    @{ value = "final"; label = "Final muayene" },
    @{ value = "musteri"; label = "Musteri iadesi" },
    @{ value = "denetim"; label = "Denetim" }
)
$optDisposition = New-StaticSelectOptions @(
    @{ value = "kullan"; label = "Kullan" },
    @{ value = "yeniden_isle"; label = "Yeniden isle" },
    @{ value = "tamir"; label = "Tamir" },
    @{ value = "hurda"; label = "Hurda" },
    @{ value = "iade"; label = "Tedarikciye iade" }
)

$fieldCustomerId = Find-OrCreate -Collection "op_fields" -Filter "key:eq:customerId" -Label "customerId" -Body @{
    key = "customerId"; label = "Musteri"; fieldType = "relation"; scope = "pool"; category = "classification"
    relationDatasetName = "odak_musteriler"
    options = (New-LookupOptions -DatasetName "odak_musteriler" -LabelField "unvan" -SearchFields @("unvan", "kod"))
}
$fieldProductGroupId = Find-OrCreate -Collection "op_fields" -Filter "key:eq:productGroupId" -Label "productGroupId" -Body @{
    key = "productGroupId"; label = "Urun grubu"; fieldType = "relation"; scope = "pool"; category = "classification"
    relationDatasetName = "odak_urun_gruplari"
    options = (New-LookupOptions -DatasetName "odak_urun_gruplari" -LabelField "ad" -SearchFields @("ad", "kod"))
}
$fieldProductId = Find-OrCreate -Collection "op_fields" -Filter "key:eq:productId" -Label "productId" -Body @{
    key = "productId"; label = "Urun / parca"; fieldType = "relation"; scope = "pool"; category = "classification"
    relationDatasetName = "odak_urunler"
    options = (New-LookupOptions -DatasetName "odak_urunler" -LabelField "ad" -SearchFields @("ad", "partNumber") -DependsOn @{
            fieldKey       = "productGroupId"
            filterTemplate = "urunGrubuId={{parentValue}}"
        })
}
$fieldCustomerOrderRef = Find-OrCreate -Collection "op_fields" -Filter "key:eq:customerOrderRef" -Label "customerOrderRef" -Body @{
    key = "customerOrderRef"; label = "Musteri siparis no"; fieldType = "text"; scope = "pool"; category = "classification"
}
$fieldQuantity = Find-OrCreate -Collection "op_fields" -Filter "key:eq:quantity" -Label "quantity" -Body @{
    key = "quantity"; label = "Miktar"; fieldType = "number"; scope = "pool"; category = "technical"
}
$fieldPlannedDate = Find-OrCreate -Collection "op_fields" -Filter "key:eq:plannedDate" -Label "plannedDate" -Body @{
    key = "plannedDate"; label = "Planlanan bitis"; fieldType = "datetime"; scope = "pool"; category = "technical"
}
$fieldWorkCenter = Find-OrCreate -Collection "op_fields" -Filter "key:eq:workCenter" -Label "workCenter" -Body @{
    key = "workCenter"; label = "Is istasyonu"; fieldType = "text"; scope = "pool"; category = "technical"
}
$fieldLotSerial = Find-OrCreate -Collection "op_fields" -Filter "key:eq:lotSerial" -Label "lotSerial" -Body @{
    key = "lotSerial"; label = "Lot / seri"; fieldType = "text"; scope = "pool"; category = "technical"
}
$fieldQualityResult = Find-OrCreate -Collection "op_fields" -Filter "key:eq:qualityResult" -Label "qualityResult" -Body @{
    key = "qualityResult"; label = "Kalite sonucu"; fieldType = "select"; scope = "pool"; category = "resolution"; options = $optQuality
}
$fieldQualityNotes = Find-OrCreate -Collection "op_fields" -Filter "key:eq:qualityNotes" -Label "qualityNotes" -Body @{
    key = "qualityNotes"; label = "Kalite notu"; fieldType = "text"; scope = "pool"; category = "resolution"
}
$fieldStorageLocation = Find-OrCreate -Collection "op_fields" -Filter "key:eq:storageLocation" -Label "storageLocation" -Body @{
    key = "storageLocation"; label = "Depo lokasyonu"; fieldType = "text"; scope = "pool"; category = "technical"
}
$fieldPackagingOk = Find-OrCreate -Collection "op_fields" -Filter "key:eq:packagingOk" -Label "packagingOk" -Body @{
    key = "packagingOk"; label = "Paketleme OK"; fieldType = "bool"; scope = "pool"; category = "technical"
}
$fieldWaybillNo = Find-OrCreate -Collection "op_fields" -Filter "key:eq:waybillNo" -Label "waybillNo" -Body @{
    key = "waybillNo"; label = "Irsaliye no"; fieldType = "text"; scope = "pool"; category = "technical"
}
$fieldShipmentNotes = Find-OrCreate -Collection "op_fields" -Filter "key:eq:shipmentNotes" -Label "shipmentNotes" -Body @{
    key = "shipmentNotes"; label = "Sevkiyat notu"; fieldType = "text"; scope = "pool"; category = "technical"
}
$fieldNcrSource = Find-OrCreate -Collection "op_fields" -Filter "key:eq:ncrSource" -Label "ncrSource" -Body @{
    key = "ncrSource"; label = "Tespit asamasi"; fieldType = "select"; scope = "pool"; category = "classification"; options = $optNcrSource
}
$fieldDefectDescription = Find-OrCreate -Collection "op_fields" -Filter "key:eq:defectDescription" -Label "defectDescription" -Body @{
    key = "defectDescription"; label = "Uygunsuzluk tanimi"; fieldType = "text"; scope = "pool"; category = "resolution"
}
$fieldAffectedQty = Find-OrCreate -Collection "op_fields" -Filter "key:eq:affectedQty" -Label "affectedQty" -Body @{
    key = "affectedQty"; label = "Etkilenen adet"; fieldType = "number"; scope = "pool"; category = "technical"
}
$fieldContainmentAction = Find-OrCreate -Collection "op_fields" -Filter "key:eq:containmentAction" -Label "containmentAction" -Body @{
    key = "containmentAction"; label = "Acil kontrol"; fieldType = "text"; scope = "pool"; category = "resolution"
}
$fieldDisposition = Find-OrCreate -Collection "op_fields" -Filter "key:eq:disposition" -Label "disposition" -Body @{
    key = "disposition"; label = "Disposition"; fieldType = "select"; scope = "pool"; category = "resolution"; options = $optDisposition
}
$fieldDispositionReason = Find-OrCreate -Collection "op_fields" -Filter "key:eq:dispositionReason" -Label "dispositionReason" -Body @{
    key = "dispositionReason"; label = "Disposition gerekcesi"; fieldType = "text"; scope = "pool"; category = "resolution"
}
$fieldRootCause = Find-OrCreate -Collection "op_fields" -Filter "key:eq:rootCause" -Label "rootCause" -Body @{
    key = "rootCause"; label = "Kok neden analizi"; fieldType = "text"; scope = "pool"; category = "resolution"
}
$fieldCorrectiveAction = Find-OrCreate -Collection "op_fields" -Filter "key:eq:correctiveAction" -Label "correctiveAction" -Body @{
    key = "correctiveAction"; label = "Duzeltici faaliyet"; fieldType = "text"; scope = "pool"; category = "resolution"
}
$fieldPreventiveAction = Find-OrCreate -Collection "op_fields" -Filter "key:eq:preventiveAction" -Label "preventiveAction" -Body @{
    key = "preventiveAction"; label = "Onleyici faaliyet"; fieldType = "text"; scope = "pool"; category = "resolution"
}
$fieldEffectivenessCheck = Find-OrCreate -Collection "op_fields" -Filter "key:eq:effectivenessCheck" -Label "effectivenessCheck" -Body @{
    key = "effectivenessCheck"; label = "Etkinlik dogrulamasi"; fieldType = "text"; scope = "pool"; category = "resolution"
}
$fieldCapaTargetDate = Find-OrCreate -Collection "op_fields" -Filter "key:eq:capaTargetDate" -Label "capaTargetDate" -Body @{
    key = "capaTargetDate"; label = "Hedef tarih"; fieldType = "datetime"; scope = "pool"; category = "technical"
}

# Relation alanlari — lookup labelField sync (liste + form icin kalici cozum).
Write-Host "[5b] op_fields relation sync..." -ForegroundColor Yellow
foreach ($fieldPatch in @(
        @{
            Id    = $fieldCustomerId
            Label = "customerId lookup"
            Body  = @{
                key = "customerId"; label = "Musteri"; fieldType = "relation"; scope = "pool"; category = "classification"
                relationDatasetName = "odak_musteriler"
                options = (New-LookupOptions -DatasetName "odak_musteriler" -LabelField "unvan" -SearchFields @("unvan", "kod"))
            }
        },
        @{
            Id    = $fieldProductGroupId
            Label = "productGroupId lookup"
            Body  = @{
                key = "productGroupId"; label = "Urun grubu"; fieldType = "relation"; scope = "pool"; category = "classification"
                relationDatasetName = "odak_urun_gruplari"
                options = (New-LookupOptions -DatasetName "odak_urun_gruplari" -LabelField "ad" -SearchFields @("ad", "kod"))
            }
        },
        @{
            Id    = $fieldProductId
            Label = "productId lookup"
            Body  = @{
                key = "productId"; label = "Urun / parca"; fieldType = "relation"; scope = "pool"; category = "classification"
                relationDatasetName = "odak_urunler"
                options = (New-LookupOptions -DatasetName "odak_urunler" -LabelField "ad" -SearchFields @("ad", "partNumber") -DependsOn @{
                        fieldKey       = "productGroupId"
                        filterTemplate = "urunGrubuId={{parentValue}}"
                    })
            }
        }
    )) {
    Sync-Record -Collection "op_fields" -Id $fieldPatch.Id -Label $fieldPatch.Label -Body $fieldPatch.Body
}

$enabledFieldIds = @(
    $fieldCustomerId, $fieldProductGroupId, $fieldProductId, $fieldCustomerOrderRef,
    $fieldQuantity, $fieldPlannedDate, $fieldWorkCenter, $fieldLotSerial,
    $fieldQualityResult, $fieldQualityNotes, $fieldStorageLocation, $fieldPackagingOk,
    $fieldWaybillNo, $fieldShipmentNotes,
    $fieldNcrSource, $fieldDefectDescription, $fieldAffectedQty, $fieldContainmentAction,
    $fieldDisposition, $fieldDispositionReason,
    $fieldRootCause, $fieldCorrectiveAction, $fieldPreventiveAction, $fieldEffectivenessCheck, $fieldCapaTargetDate
)

# --- 6. Work item types ---
Write-Host "[6] op_work_item_types..." -ForegroundColor Yellow
$typeOrderId = Find-OrCreate -Collection "op_work_item_types" -Filter "name:eq:Uretim emri" -Label "Uretim emri" -Body @{
    name = "Uretim emri"; category = "operational"; color = "primary"; icon = "PackageIcon"; sortOrder = 10
}
$typeNcrId = Find-OrCreate -Collection "op_work_item_types" -Filter "name:eq:Uygunsuzluk (NCR)" -Label "NCR" -Body @{
    name = "Uygunsuzluk (NCR)"; category = "incident"; color = "error"; icon = "AlertCircleIcon"; sortOrder = 20
}
$typeCapaId = Find-OrCreate -Collection "op_work_item_types" -Filter "name:eq:Duzeltici faaliyet (CAPA)" -Label "CAPA" -Body @{
    name = "Duzeltici faaliyet (CAPA)"; category = "problem"; color = "warning"; icon = "BugIcon"; sortOrder = 30
}

# --- 7. Workspace ---
Write-Host "[7] op_workspaces..." -ForegroundColor Yellow
$workspaceId = Find-OrCreate -Collection "op_workspaces" -Filter "name:eq:$workspaceName" -Label "Workspace" -Body @{
    name                  = $workspaceName
    workspaceType         = "operational"
    description           = "Siparisten sevkiyata uretim operasyonlari — Odak Kompozit"
    workItemKeyPrefix     = "ODF"
    workItemKeyFormat     = "{prefix}-{seq:D4}"
    workItemSequenceStart = 1
    enabledTypeIds        = @($typeOrderId, $typeNcrId, $typeCapaId)
    enabledFieldIds       = $enabledFieldIds
    enabledStateIds       = $allStateIds
    enabledPriorityIds    = $allPriorityIds
}

# --- 8. State flows ---
Write-Host "[8] op_state_flows..." -ForegroundColor Yellow
$mainFlowName = "$tag - Ana Akis"
$mainTransitions = @(
    @{ transitionKey = "plan"; fromStateId = $stNew; toStateId = $stPlanned; label = "Planla"; order = 0 },
    @{ transitionKey = "start_production"; fromStateId = $stPlanned; toStateId = $stProduction; label = "Uretime al"; order = 1 },
    @{ transitionKey = "skip_to_production"; fromStateId = $stNew; toStateId = $stProduction; label = "Dogrudan uretime al"; order = 2 },
    @{ transitionKey = "send_to_quality"; fromStateId = $stProduction; toStateId = $stQuality; label = "Kaliteye gonder"; order = 3 },
    @{ transitionKey = "hold_quality"; fromStateId = $stQuality; toStateId = $stQualityHold; label = "Uygunsuzluk — bekle"; order = 4 },
    @{ transitionKey = "resume_from_hold"; fromStateId = $stQualityHold; toStateId = $stQuality; label = "Tekrar kaliteye al"; order = 5 },
    @{ transitionKey = "approve_quality"; fromStateId = $stQuality; toStateId = $stStorage; label = "Kalite onayi"; order = 6 },
    @{ transitionKey = "move_to_ship_prep"; fromStateId = $stStorage; toStateId = $stShipPrep; label = "Sevkiyata hazirla"; order = 7 },
    @{ transitionKey = "ship"; fromStateId = $stShipPrep; toStateId = $stShipped; label = "Sevk et"; order = 8 },
    @{ transitionKey = "close_order"; fromStateId = $stShipped; toStateId = $stClosed; label = "Kapat"; order = 9 },
    @{ transitionKey = "cancel"; fromStateId = $stNew; toStateId = $stClosed; label = "Iptal et"; order = 10 },
    @{ transitionKey = "cancel_planned"; fromStateId = $stPlanned; toStateId = $stClosed; label = "Iptal et"; order = 11 }
)
$mainFlowId = Find-OrCreate -Collection "op_state_flows" -Filter "name:eq:$mainFlowName" -Label "Ana akis" -Body @{
    name = $mainFlowName; workspaceId = $workspaceId; initialStateId = $stNew
    isDefault = $true; isActive = $true; transitions = $mainTransitions
}
Sync-Record -Collection "op_state_flows" -Id $mainFlowId -Label "Ana akis sync" -Body @{
    name = $mainFlowName; workspaceId = $workspaceId; initialStateId = $stNew
    isDefault = $true; isActive = $true; transitions = $mainTransitions
}

$ncrFlowName = "$tag - NCR"
$ncrTransitions = @(
    @{ transitionKey = "contain"; fromStateId = $stNcrOpen; toStateId = $stNcrContain; label = "Kontrol altina al"; order = 0 },
    @{ transitionKey = "review"; fromStateId = $stNcrContain; toStateId = $stNcrReview; label = "Degerlendir"; order = 1 },
    @{ transitionKey = "decide"; fromStateId = $stNcrReview; toStateId = $stNcrDecided; label = "Karar ver"; order = 2 },
    @{ transitionKey = "close_ncr"; fromStateId = $stNcrDecided; toStateId = $stNcrClosed; label = "Kapat"; order = 3 },
    @{ transitionKey = "reopen_ncr"; fromStateId = $stNcrDecided; toStateId = $stNcrReview; label = "Yeniden ac"; order = 4 }
)
$ncrFlowId = Find-OrCreate -Collection "op_state_flows" -Filter "name:eq:$ncrFlowName" -Label "NCR akis" -Body @{
    name = $ncrFlowName; workspaceId = $workspaceId; initialStateId = $stNcrOpen
    isDefault = $false; isActive = $true; transitions = $ncrTransitions
}
Sync-Record -Collection "op_state_flows" -Id $ncrFlowId -Label "NCR akis sync" -Body @{
    name = $ncrFlowName; workspaceId = $workspaceId; initialStateId = $stNcrOpen
    isDefault = $false; isActive = $true; transitions = $ncrTransitions
}

$capaFlowName = "$tag - CAPA"
$capaTransitions = @(
    @{ transitionKey = "analyze_root"; fromStateId = $stCapaOpen; toStateId = $stCapaRoot; label = "Kok neden analizi"; order = 0 },
    @{ transitionKey = "plan_action"; fromStateId = $stCapaRoot; toStateId = $stCapaPlan; label = "Aksiyon planla"; order = 1 },
    @{ transitionKey = "implement"; fromStateId = $stCapaPlan; toStateId = $stCapaImpl; label = "Uygula"; order = 2 },
    @{ transitionKey = "verify"; fromStateId = $stCapaImpl; toStateId = $stCapaVerify; label = "Dogrula"; order = 3 },
    @{ transitionKey = "close_capa"; fromStateId = $stCapaVerify; toStateId = $stCapaClosed; label = "Kapat"; order = 4 }
)
$capaFlowId = Find-OrCreate -Collection "op_state_flows" -Filter "name:eq:$capaFlowName" -Label "CAPA akis" -Body @{
    name = $capaFlowName; workspaceId = $workspaceId; initialStateId = $stCapaOpen
    isDefault = $false; isActive = $true; transitions = $capaTransitions
}
Sync-Record -Collection "op_state_flows" -Id $capaFlowId -Label "CAPA akis sync" -Body @{
    name = $capaFlowName; workspaceId = $workspaceId; initialStateId = $stCapaOpen
    isDefault = $false; isActive = $true; transitions = $capaTransitions
}

foreach ($pair in @(
        @{ Id = $typeOrderId; FlowId = $mainFlowId; Name = "Uretim emri"; Cat = "operational" },
        @{ Id = $typeNcrId; FlowId = $ncrFlowId; Name = "Uygunsuzluk (NCR)"; Cat = "incident" },
        @{ Id = $typeCapaId; FlowId = $capaFlowId; Name = "Duzeltici faaliyet (CAPA)"; Cat = "problem" }
    )) {
    Invoke-DgPut -Collection "op_work_item_types" -Id $pair.Id -Body @{
        name = $pair.Name; category = $pair.Cat; defaultStateFlowId = $pair.FlowId
    } | Out-Null
}

# --- 9. Forms ---
Write-Host "[9] op_forms..." -ForegroundColor Yellow
$formOrderName = "$tag - Yeni emir"
$formOrderId = Find-OrCreate -Collection "op_forms" -Filter "name:eq:$formOrderName" -Label "Form emir" -Body @{
    name = $formOrderName; workspaceId = $workspaceId; defaultTypeId = $typeOrderId
    defaultStateFlowId = $mainFlowId; defaultStateId = $stNew; isDefault = $true
    layout = @{
        sections = @(
            @{
                key = "general"; title = "Genel"; fields = @("title", "description", "typeId", "priorityId", "customerId", "customerOrderRef")
            },
            @{
                key = "product"; title = "Urun"; fields = @("productGroupId", "productId", "quantity", "plannedDate")
            }
        )
    }
    fieldBehaviors = @{
        title = @{ visible = $true; required = $true }
        typeId = @{ visible = $true; required = $true }
        priorityId = @{ visible = $true; required = $true }
        customerId = @{ visible = $true }
        productGroupId = @{ visible = $true }
        productId = @{ visible = $true }
        quantity = @{ visible = $true }
    }
}
Sync-Record -Collection "op_forms" -Id $formOrderId -Label "Form emir sync" -Body @{
    name = $formOrderName; workspaceId = $workspaceId; defaultTypeId = $typeOrderId
    defaultStateFlowId = $mainFlowId; defaultStateId = $stNew; isDefault = $true
    layout = @{
        sections = @(
            @{ key = "general"; title = "Genel"; fields = @("title", "description", "typeId", "priorityId", "customerId", "customerOrderRef") },
            @{ key = "product"; title = "Urun"; fields = @("productGroupId", "productId", "quantity", "plannedDate") }
        )
    }
    fieldBehaviors = @{
        title = @{ visible = $true; required = $true }
        typeId = @{ visible = $true; required = $true }
        priorityId = @{ visible = $true; required = $true }
    }
}

$formNcrName = "$tag - NCR kaydi"
$formNcrId = Find-OrCreate -Collection "op_forms" -Filter "name:eq:$formNcrName" -Label "Form NCR" -Body @{
    name = $formNcrName; workspaceId = $workspaceId; defaultTypeId = $typeNcrId
    defaultStateFlowId = $ncrFlowId; defaultStateId = $stNcrOpen; isDefault = $false
    layout = @{
        sections = @(
            @{ key = "ncr"; title = "Uygunsuzluk"; fields = @("title", "typeId", "priorityId", "ncrSource", "defectDescription", "affectedQty", "containmentAction", "lotSerial") }
        )
    }
    fieldBehaviors = @{
        title = @{ visible = $true; required = $true }
        defectDescription = @{ visible = $true; required = $true }
        ncrSource = @{ visible = $true; required = $true }
    }
}

$formCapaName = "$tag - CAPA kaydi"
$formCapaId = Find-OrCreate -Collection "op_forms" -Filter "name:eq:$formCapaName" -Label "Form CAPA" -Body @{
    name = $formCapaName; workspaceId = $workspaceId; defaultTypeId = $typeCapaId
    defaultStateFlowId = $capaFlowId; defaultStateId = $stCapaOpen; isDefault = $false
    layout = @{
        sections = @(
            @{ key = "capa"; title = "CAPA"; fields = @("title", "typeId", "priorityId", "rootCause", "correctiveAction", "preventiveAction", "capaTargetDate") }
        )
    }
    fieldBehaviors = @{
        title = @{ visible = $true; required = $true }
    }
}

# --- 10. Boards ---
Write-Host "[10] op_boards..." -ForegroundColor Yellow

# Liste/kanban kapsami: ana akis durumlari (Kapandi dahil — filtreyle daraltilir).
# MO board list sorgusu stateId $in scope + boardId eslesmesi kullanir.
$mainBoardScopeColumns = @(
    @{ stateId = $stNew; title = "Yeni"; queryKey = "wi_board_column" },
    @{ stateId = $stPlanned; title = "Planlandi"; queryKey = "wi_board_column" },
    @{ stateId = $stProduction; title = "Uretimde"; queryKey = "wi_board_column" },
    @{ stateId = $stQuality; title = "Kalite"; queryKey = "wi_board_column" },
    @{ stateId = $stQualityHold; title = "Kalite bekliyor"; queryKey = "wi_board_column" },
    @{ stateId = $stStorage; title = "Depoda"; queryKey = "wi_board_column" },
    @{ stateId = $stShipPrep; title = "Sevkiyat hazir"; queryKey = "wi_board_column" },
    @{ stateId = $stShipped; title = "Sevk edildi"; queryKey = "wi_board_column" },
    @{ stateId = $stClosed; title = "Kapandi"; queryKey = "wi_board_column" }
)
$mainBoardListColumns = @(
    @{ key = "key"; label = "No"; sortable = $true; filterable = $false },
    @{ key = "title"; label = "Baslik"; sortable = $true; filterable = $true },
    @{ key = "stateId"; label = "Durum"; sortable = $true; filterable = $true },
    @{ key = "priorityId"; label = "Oncelik"; sortable = $true; filterable = $true },
    @{ key = "typeId"; label = "Tip"; sortable = $true; filterable = $true },
    @{ key = "customerId"; label = "Musteri"; sortable = $false; filterable = $true },
    @{ key = "productId"; label = "Urun / parca"; sortable = $false; filterable = $true },
    @{ key = "quantity"; label = "Miktar"; sortable = $true; filterable = $true },
    @{ key = "plannedDate"; label = "Planlanan bitis"; sortable = $true; filterable = $true; format = "date" },
    @{ key = "assignee"; label = "Atanan"; sortable = $false; filterable = $true }
)
$mainBoardProdBody = @{
    name = "$tag - Uretim panosu"
    workspaceId = $workspaceId
    viewType = "list"
    defaultStateFlowId = $mainFlowId
    defaultFormId = $formOrderId
    isDefault = $true
    visibleFields = @("key", "title", "typeId", "priorityId", "assignee", "stateId", "customerId", "productId", "quantity", "plannedDate")
    config = @{
        columns = $mainBoardScopeColumns
        listColumns = $mainBoardListColumns
        defaultSort = @{ field = "lastStateChangeAt"; direction = "desc" }
    }
}

$boardProdName = "$tag - Uretim panosu"
$boardProdId = Find-OrCreate -Collection "op_boards" -Filter "name:eq:$boardProdName" -Label "Uretim panosu" -Body $mainBoardProdBody
Sync-Record -Collection "op_boards" -Id $boardProdId -Label "Uretim panosu sync" -Body $mainBoardProdBody

$boardQualityName = "$tag - Kalite kuyrugu"
$boardQualityId = Find-OrCreate -Collection "op_boards" -Filter "name:eq:$boardQualityName" -Label "Kalite kuyrugu" -Body @{
    name = $boardQualityName; workspaceId = $workspaceId; viewType = "list"
    defaultStateFlowId = $ncrFlowId; defaultFormId = $formNcrId; isDefault = $false
    visibleFields = @("key", "title", "typeId", "stateId", "defectDescription", "ncrSource")
}

$boardShipName = "$tag - Depo sevkiyat"
$boardShipId = Find-OrCreate -Collection "op_boards" -Filter "name:eq:$boardShipName" -Label "Depo sevkiyat" -Body @{
    name = $boardShipName; workspaceId = $workspaceId; viewType = "list"
    defaultStateFlowId = $mainFlowId; defaultFormId = $formOrderId; isDefault = $false
    visibleFields = @("key", "title", "stateId", "storageLocation", "waybillNo", "packagingOk")
}

# --- 11. Profile ---
Write-Host "[11] op_profiles..." -ForegroundColor Yellow
$profileName = "$tag - Kayit profili"
$profileId = Find-OrCreate -Collection "op_profiles" -Filter "name:eq:$profileName" -Label "Profile" -Body @{
    name = $profileName; workspaceId = $workspaceId; defaultTypeId = $typeOrderId; isDefault = $true
    fieldBehaviors = @{
        title = @{ visible = $true; required = $true }
        typeId = @{ visible = $true; readonly = $true }
        priorityId = @{ visible = $true }
        customerId = @{ visible = $true }
        productId = @{ visible = $true }
        qualityResult = @{ visible = $true }
        disposition = @{ visible = $true }
    }
    actions = @(
        @{ transitionKey = "plan"; order = 0; label = "Planla" },
        @{ transitionKey = "start_production"; order = 1; label = "Uretime al" },
        @{ transitionKey = "send_to_quality"; order = 2; label = "Kaliteye gonder" },
        @{ transitionKey = "approve_quality"; order = 3; label = "Kalite onayi" },
        @{ transitionKey = "move_to_ship_prep"; order = 4; label = "Sevkiyata hazirla" },
        @{ transitionKey = "ship"; order = 5; label = "Sevk et" },
        @{ transitionKey = "close_order"; order = 6; label = "Kapat" }
    )
    header = @{ showBreadcrumb = $true; showKey = $true }
    sidebar = @{ showSla = $false; showWatchers = $true }
    panels = @{ timeline = @{ enabled = $true }; comments = @{ enabled = $true } }
    layout = @{
        sections = @(
            @{ key = "summary"; title = "Ozet"; fields = @("title", "description", "typeId", "priorityId", "assignee", "key") },
            @{ key = "order"; title = "Siparis / urun"; fields = @("customerId", "customerOrderRef", "productGroupId", "productId", "quantity", "plannedDate", "workCenter", "lotSerial") },
            @{ key = "quality"; title = "Kalite"; fields = @("qualityResult", "qualityNotes", "ncrSource", "defectDescription", "disposition", "dispositionReason") },
            @{ key = "logistics"; title = "Depo / sevkiyat"; fields = @("storageLocation", "packagingOk", "waybillNo", "shipmentNotes") },
            @{ key = "capa"; title = "CAPA"; fields = @("rootCause", "correctiveAction", "preventiveAction", "effectivenessCheck", "capaTargetDate") }
        )
    }
}

# --- 12. Rules ---
Write-Host "[12] op_rules..." -ForegroundColor Yellow
Find-OrCreate -Collection "op_rules" -Filter "name:eq:$tag - NCR disposition zorunlu" -Label "NCR rule" -Body @{
    name = "$tag - NCR disposition zorunlu"; workspaceId = $workspaceId; ruleType = "validation"
    trigger = "WorkItemTransition"; transitionKey = "close_ncr"; applyMode = "pre"
    typeId = $typeNcrId
    conditions = @{ field = "disposition"; cmp = "empty" }
    errorMessage = "NCR kapatmadan once disposition secilmelidir."
    isActive = $true; priority = 100
} | Out-Null

Find-OrCreate -Collection "op_rules" -Filter "name:eq:$tag - CAPA etkinlik zorunlu" -Label "CAPA rule" -Body @{
    name = "$tag - CAPA etkinlik zorunlu"; workspaceId = $workspaceId; ruleType = "validation"
    trigger = "WorkItemTransition"; transitionKey = "close_capa"; applyMode = "pre"
    typeId = $typeCapaId
    conditions = @{ field = "effectivenessCheck"; cmp = "empty" }
    errorMessage = "CAPA kapatmadan once etkinlik dogrulamasi girilmelidir."
    isActive = $true; priority = 100
} | Out-Null

# --- 13. Dashboard ---
Write-Host "[13] op_dashboards..." -ForegroundColor Yellow
$dashboardName = "$tag - Ozet pano"
$dashboardLayout = @{
    rows = @(
        @{ cols = @(
                @{ widgetId = "open_orders"; md = 4; lg = 4 },
                @{ widgetId = "quality_hold"; md = 4; lg = 4 },
                @{ widgetId = "open_ncr"; md = 4; lg = 4 }
            )
        },
        @{ cols = @(@{ widgetId = "recent_orders"; md = 12; lg = 12 }) }
    )
}
$dashboardWidgets = @(
    @{
        key = "open_orders"; type = "summaryCard"; title = "Acik uretim emirleri"
        dataset = "op_work_items"; queryKey = "wi_by_workspace_and_state"
        parameters = @{ workspaceId = $workspaceId; stateId = $stProduction }
        take = 100
    },
    @{
        key = "quality_hold"; type = "summaryCard"; title = "Kalite bekleyen"
        dataset = "op_work_items"; queryKey = "wi_by_workspace_and_state"
        parameters = @{ workspaceId = $workspaceId; stateId = $stQualityHold }
        take = 100
    },
    @{
        key = "open_ncr"; type = "summaryCard"; title = "Acik NCR"
        dataset = "op_work_items"; queryKey = "wi_by_workspace_and_state"
        parameters = @{ workspaceId = $workspaceId; stateId = $stNcrOpen }
        take = 100
    },
    @{
        key = "recent_orders"; type = "list"; title = "Uretimdeki emirler"
        dataset = "op_work_items"; queryKey = "wi_board_column"
        parameters = @{ workspaceId = $workspaceId; boardId = $boardProdId; stateId = $stProduction }
        take = 8
    }
)
$dashboardBody = @{
    name = $dashboardName; description = "Odak Uretim ozet dashboard"; workspaceId = $workspaceId
    scope = "workspace"; isDefault = $true; isActive = $true
    layout = $dashboardLayout; widgets = $dashboardWidgets
}
$dashboardId = Find-OrCreate -Collection "op_dashboards" -Filter "name:eq:$dashboardName" -Label "Dashboard" -Body $dashboardBody
Sync-Record -Collection "op_dashboards" -Id $dashboardId -Label "Dashboard sync" -Body $dashboardBody

Invoke-DgPut -Collection "op_boards" -Id $boardProdId -Body @{
    defaultDashboardId = $dashboardId
} | Out-Null

# --- 14. Workspace final sync ---
Write-Host "[14] op_workspaces sync..." -ForegroundColor Yellow
Invoke-DgPut -Collection "op_workspaces" -Id $workspaceId -Body @{
    defaultStateFlowId = $mainFlowId
    enabledTypeIds     = @($typeOrderId, $typeNcrId, $typeCapaId)
    enabledFieldIds    = $enabledFieldIds
    enabledStateIds    = $allStateIds
    enabledPriorityIds = $allPriorityIds
} | Out-Null

# --- 15. MO metadata cache ---
if ($ReloadMetadataCache) {
    Write-Host "[15] MO metadata cache reload..." -ForegroundColor Yellow
    try {
        $moUri = "$MoBaseUrl/api/v1/workspaces/$workspaceId/metadata-cache/reload"
        $moParams = @{ Uri = $moUri; Method = "POST"; Headers = $headers; ErrorAction = "Stop" }
        if ($MoBaseUrl.StartsWith("https://") -and $irmParams.ContainsKey("SkipCertificateCheck")) { $moParams.SkipCertificateCheck = $true }
        Invoke-RestMethod @moParams | Out-Null
        Write-Host "  OK: metadata cache reload" -ForegroundColor Green
    }
    catch {
        Write-Host "  WARN: cache reload — $($_.Exception.Message)" -ForegroundColor DarkYellow
    }
}

$summary = @{
    tag           = $tag
    workspaceId   = $workspaceId
    workspaceName = $workspaceName
    mainFlowId    = $mainFlowId
    ncrFlowId     = $ncrFlowId
    capaFlowId    = $capaFlowId
    boardProdId   = $boardProdId
    boardQualityId = $boardQualityId
    boardShipId   = $boardShipId
    formOrderId   = $formOrderId
    formNcrId     = $formNcrId
    formCapaId    = $formCapaId
    profileId     = $profileId
    dashboardId   = $dashboardId
    types         = @{ order = $typeOrderId; ncr = $typeNcrId; capa = $typeCapaId }
    states        = @{
        new = $stNew; planned = $stPlanned; production = $stProduction; quality = $stQuality
        qualityHold = $stQualityHold; storage = $stStorage; shipPrep = $stShipPrep
        shipped = $stShipped; closed = $stClosed
        ncrOpen = $stNcrOpen; ncrClosed = $stNcrClosed
        capaOpen = $stCapaOpen; capaClosed = $stCapaClosed
    }
    priorities    = @{ urgent = $prioUrgentId; high = $prioHighId; normal = $prioNormalId; low = $prioLowId }
    seededAt      = (Get-Date).ToUniversalTime().ToString("o")
    gatewayUrl    = $BaseUrl
}
$summary | ConvertTo-Json -Depth 8 | Set-Content -Path $OutputFile -Encoding UTF8

# --- Smoke / Demo via MO ---
if ($SmokeTest -or $SeedDemo) {
    Write-Host "`n[MO] Smoke / demo..." -ForegroundColor Yellow
    $moParams = @{ Headers = $headers; ErrorAction = "Stop" }
    if ($MoBaseUrl.StartsWith("https://") -and $irmParams.ContainsKey("SkipCertificateCheck")) { $moParams.SkipCertificateCheck = $true }

    $masterIdsPath = Join-Path $scriptDir "../seed/odak_master_ids.json"
    $productId = $null
    $customerId = $null
    $productGroupId = $null
    if (Test-Path $masterIdsPath) {
        $mids = Get-Content $masterIdsPath -Raw -Encoding UTF8 | ConvertFrom-Json
        if ($mids.urunler.'ODK-CMP-1001') { $productId = $mids.urunler.'ODK-CMP-1001' }
        if ($mids.musteriler.'MUS-001') { $customerId = $mids.musteriler.'MUS-001' }
        if ($mids.urunGruplari.'UG-KOM') { $productGroupId = $mids.urunGruplari.'UG-KOM' }
    }

    if ($SmokeTest) {
        $createBody = @{
            workspaceId = $workspaceId
            typeId      = $typeOrderId
            title       = "$tag smoke $(Get-Date -Format 'HHmmss')"
            boardId     = $boardProdId
            fields      = @{ priorityId = $prioNormalId }
        } | ConvertTo-Json -Depth 6 -Compress
        $created = Invoke-RestMethod -Uri "$MoBaseUrl/api/v1/work-items" -Method POST -Body $createBody @moParams
        Write-Host "  OK: smoke create -> $($created.workItem.key)" -ForegroundColor Green
        $summary.smokeTest = @{ workItemKey = $created.workItem.key; workItemId = $created.workItem.id }
    }

    if ($SeedDemo) {
        # ODF-0001 uretim emri (planlandi)
        $body1 = @{
            workspaceId = $workspaceId
            typeId      = $typeOrderId
            title       = "Kanat kabuk paneli A — 10 adet"
            boardId     = $boardProdId
            fields      = @{
                priorityId       = $prioNormalId
                customerId       = $customerId
                customerOrderRef = "PO-2026-0142"
                productGroupId   = $productGroupId
                productId        = $productId
                quantity         = 10
                plannedDate      = (Get-Date).AddDays(14).ToUniversalTime().ToString("o")
            }
        } | ConvertTo-Json -Depth 8 -Compress
        $wi1 = Invoke-RestMethod -Uri "$MoBaseUrl/api/v1/work-items" -Method POST -Body $body1 @moParams
        $wi1Id = $wi1.workItem.id
        Write-Host "  OK: demo emir -> $($wi1.workItem.key)" -ForegroundColor Green

        Invoke-RestMethod -Uri "$MoBaseUrl/api/v1/work-items/$wi1Id/transitions/plan" -Method POST -Body "{}" @moParams | Out-Null
        Invoke-RestMethod -Uri "$MoBaseUrl/api/v1/work-items/$wi1Id/transitions/start_production" -Method POST -Body "{}" @moParams | Out-Null
        Invoke-RestMethod -Uri "$MoBaseUrl/api/v1/work-items/$wi1Id/transitions/send_to_quality" -Method POST -Body (@{ fields = @{ workCenter = "Otomatik layup hattı"; lotSerial = "LOT-2026-0421" } } | ConvertTo-Json -Compress) @moParams | Out-Null
        Write-Host "  OK: demo emir -> kalite kontrol" -ForegroundColor Green

        # NCR
        $bodyNcr = @{
            workspaceId  = $workspaceId
            typeId       = $typeNcrId
            title        = "Layup bosluk hatasi — 2 adet"
            parentItemId = $wi1Id
            boardId      = $boardQualityId
            fields       = @{
                priorityId         = $prioHighId
                ncrSource          = "proses"
                defectDescription  = "Yuzeyde bosluk / delaminasyon tespit edildi"
                affectedQty        = 2
                containmentAction  = "Etkilenen 2 adet ayirildi, hat durduruldu"
                lotSerial          = "LOT-2026-0421"
            }
        } | ConvertTo-Json -Depth 8 -Compress
        $ncr = Invoke-RestMethod -Uri "$MoBaseUrl/api/v1/work-items" -Method POST -Body $bodyNcr @moParams
        $ncrId = $ncr.workItem.id
        Write-Host "  OK: demo NCR -> $($ncr.workItem.key)" -ForegroundColor Green

        Invoke-RestMethod -Uri "$MoBaseUrl/api/v1/work-items/$wi1Id/transitions/hold_quality" -Method POST -Body (@{ fields = @{ qualityResult = "uygunsuz"; qualityNotes = "NCR acildi" } } | ConvertTo-Json -Compress) @moParams | Out-Null

        # CAPA
        $bodyCapa = @{
            workspaceId  = $workspaceId
            typeId       = $typeCapaId
            title        = "Layup proses parametresi duzeltmesi"
            parentItemId = $ncrId
            fields       = @{
                priorityId        = $prioHighId
                rootCause         = "Vakum basinci hedef degerin altinda kaldi"
                correctiveAction  = "Proses parametreleri guncellendi ve operator egitildi"
                capaTargetDate    = (Get-Date).AddDays(7).ToUniversalTime().ToString("o")
            }
        } | ConvertTo-Json -Depth 8 -Compress
        $capa = Invoke-RestMethod -Uri "$MoBaseUrl/api/v1/work-items" -Method POST -Body $bodyCapa @moParams
        Write-Host "  OK: demo CAPA -> $($capa.workItem.key)" -ForegroundColor Green

        # ODF-0002 sorunsuz emir (depoda)
        $body2 = @{
            workspaceId = $workspaceId
            typeId      = $typeOrderId
            title       = "Spoiler yuzeyi C — 5 adet (sorunsuz)"
            boardId     = $boardProdId
            fields      = @{
                priorityId       = $prioNormalId
                customerOrderRef = "PO-2026-0155"
                quantity         = 5
            }
        } | ConvertTo-Json -Depth 6 -Compress
        $wi2 = Invoke-RestMethod -Uri "$MoBaseUrl/api/v1/work-items" -Method POST -Body $body2 @moParams
        $wi2Id = $wi2.workItem.id
        foreach ($tk in @("plan", "start_production", "send_to_quality", "approve_quality")) {
            $extra = @{}
            if ($tk -eq "send_to_quality") { $extra = @{ fields = @{ lotSerial = "LOT-2026-0430"; workCenter = "Pres hattı" } } }
            if ($tk -eq "approve_quality") { $extra = @{ fields = @{ qualityResult = "uygun"; qualityNotes = "Final muayene OK" } } }
            $pb = if ($extra.Count -gt 0) { $extra | ConvertTo-Json -Compress } else { "{}" }
            Invoke-RestMethod -Uri "$MoBaseUrl/api/v1/work-items/$wi2Id/transitions/$tk" -Method POST -Body $pb @moParams | Out-Null
        }
        Write-Host "  OK: demo emir 2 -> depoda ($($wi2.workItem.key))" -ForegroundColor Green

        $summary.demo = @{
            order1 = @{ id = $wi1Id; key = $wi1.workItem.key }
            ncr    = @{ id = $ncrId; key = $ncr.workItem.key }
            capa   = @{ id = $capa.workItem.id; key = $capa.workItem.key }
            order2 = @{ id = $wi2Id; key = $wi2.workItem.key }
        }
    }

    $summary | ConvertTo-Json -Depth 10 | Set-Content -Path $OutputFile -Encoding UTF8
}

Write-Host "`nTamamlandi. Ozet: $OutputFile" -ForegroundColor Cyan
Write-Host "UI: Operasyon Merkezi -> '$workspaceName' -> '$boardProdName'" -ForegroundColor Cyan
