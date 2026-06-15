# Odak Siparis — Is Paketi WI tipi + pano + form (incremental seed)
#
# Mevcut Odak Uretim workspace uzerine calisir; odak-uretim-seed.json guncellenir.
#
# Usage (repo kokunden):
#   .\docs\odak\operationcore\scripts\get-operationcore-token.ps1
#   .\docs\odak\siparis\scripts\seed-odak-siparis-package-type.ps1
#   .\docs\odak\siparis\scripts\seed-odak-siparis-package-type.ps1 -ReloadMetadataCache

param(
    [string]$BaseUrl = "http://192.168.20.20:5040",
    [string]$MoBaseUrl = "http://192.168.20.20:5040/operations",
    [string]$SeedFile = "",
    [switch]$ReloadMetadataCache = $true
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
$repoRoot = (Resolve-Path (Join-Path $scriptDir "../../../..")).Path
$ocScripts = Join-Path $repoRoot "docs/odak/operationcore/scripts"

if ([string]::IsNullOrEmpty($SeedFile)) {
    $SeedFile = Join-Path $repoRoot "docs/odak/is_surecleri/seed/odak-uretim-seed.json"
}
if (-not (Test-Path $SeedFile)) {
    throw "Seed dosyasi yok: $SeedFile — once seed-operation-core-odak-uretim.ps1 calistirin."
}

$dataPath = "/data/api/v1/data"
$tag = "Odak Uretim"
$loadTokenScript = Join-Path $ocScripts "load-operationcore-token.ps1"
$token = & $loadTokenScript
if ([string]::IsNullOrEmpty($token)) { throw "Token alinamadi." }

$headers = @{
    Authorization  = "Bearer $token"
    "Content-Type" = "application/json"
}
[System.Net.ServicePointManager]::ServerCertificateValidationCallback = { $true }
$irmParams = @{ Headers = $headers; ErrorAction = "Stop" }
if ($BaseUrl.StartsWith("https://") -and (Get-Command Invoke-RestMethod).Parameters.ContainsKey("SkipCertificateCheck")) {
    $irmParams.SkipCertificateCheck = $true
}

$seed = Get-Content $SeedFile -Raw -Encoding UTF8 | ConvertFrom-Json
$workspaceId = [string]$seed.workspaceId
$mainFlowId = [string]$seed.mainFlowId
$typeOrderId = [string]$seed.types.order
$stNew = [string]$seed.states.new
$stPlanned = [string]$seed.states.planned
$stProduction = [string]$seed.states.production
$stQuality = [string]$seed.states.quality
$stQualityHold = [string]$seed.states.qualityHold
$stStorage = [string]$seed.states.storage
$stShipPrep = [string]$seed.states.shipPrep
$stShipped = [string]$seed.states.shipped
$stClosed = [string]$seed.states.closed
$prioNormalId = [string]$seed.priorities.normal

function Invoke-DgGet {
    param([string]$Collection, [string]$Filter = "", [int]$Limit = 10)
    $uri = "$BaseUrl$dataPath/$Collection`?limit=$Limit"
    if (-not [string]::IsNullOrEmpty($Filter)) { $uri += "&filter=" + [Uri]::EscapeDataString($Filter) }
    return Invoke-RestMethod -Uri $uri @irmParams
}

function Invoke-DgPost {
    param([string]$Collection, [object]$Body)
    $json = if ($Body -is [string]) { $Body } else { $Body | ConvertTo-Json -Depth 25 -Compress }
    return Invoke-RestMethod -Uri "$BaseUrl$dataPath/$Collection" -Method POST -Body $json @irmParams
}

function Invoke-DgPut {
    param([string]$Collection, [string]$Id, [object]$Body)
    $json = $Body | ConvertTo-Json -Depth 25 -Compress
    return Invoke-RestMethod -Uri "$BaseUrl$dataPath/$Collection/$Id" -Method PUT -Body $json @irmParams
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
    if ($Response -is [Array]) { return @($Response) }
    foreach ($prop in @("data", "Data", "items", "Items")) {
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
    $created = Invoke-DgPost -Collection $Collection -Body $Body
    $id = Get-DataId $created
    Write-Host "  OK: $Label -> $id" -ForegroundColor Green
    return $id
}

function Sync-Record {
    param([string]$Collection, [string]$Id, [object]$Body, [string]$Label)
    Invoke-DgPut -Collection $Collection -Id $Id -Body $Body | Out-Null
    Write-Host "  SYNC: $Label" -ForegroundColor Cyan
}

Write-Host "`n=== seed-odak-siparis-package-type ===" -ForegroundColor Cyan
Write-Host "Workspace: $workspaceId" -ForegroundColor Gray

# --- Pool fields (is paketi) ---
Write-Host "[1] op_fields (is paketi)..." -ForegroundColor Yellow
$fieldLegacyPackageId = Find-OrCreate -Collection "op_fields" -Filter "key:eq:legacyPackageId" -Label "legacyPackageId" -Body @{
    key = "legacyPackageId"; label = "Legacy paket id"; fieldType = "text"; scope = "pool"; category = "classification"
}
$fieldPackageNo = Find-OrCreate -Collection "op_fields" -Filter "key:eq:packageNo" -Label "packageNo" -Body @{
    key = "packageNo"; label = "Is paketi no (legacy)"; fieldType = "text"; scope = "pool"; category = "classification"
}
$fieldBeginDate = Find-OrCreate -Collection "op_fields" -Filter "key:eq:beginDate" -Label "beginDate" -Body @{
    key = "beginDate"; label = "Baslangic tarihi"; fieldType = "datetime"; scope = "pool"; category = "technical"
}
$fieldAddress = Find-OrCreate -Collection "op_fields" -Filter "key:eq:address" -Label "address" -Body @{
    key = "address"; label = "Teslimat adresi"; fieldType = "text"; scope = "pool"; category = "technical"
}

# --- Work item type ---
Write-Host "[2] op_work_item_types (Is Paketi)..." -ForegroundColor Yellow
$typePackageId = Find-OrCreate -Collection "op_work_item_types" -Filter "name:eq:Is Paketi" -Label "Is Paketi" -Body @{
    name = "Is Paketi"; category = "operational"; color = "success"; icon = "BriefcaseIcon"; sortOrder = 5
}
Sync-Record -Collection "op_work_item_types" -Id $typePackageId -Label "Is Paketi flow" -Body @{
    name = "Is Paketi"; category = "operational"; defaultStateFlowId = $mainFlowId
    color = "success"; icon = "BriefcaseIcon"; sortOrder = 5
}

# --- Form ---
Write-Host "[3] op_forms (yeni is paketi)..." -ForegroundColor Yellow
$formPackageName = "$tag - Yeni is paketi"
$formPackageLayout = @{
    sections = @(
        @{
            key = "package"
            title = "Is paketi"
            fields = @(
                "title", "typeId", "priorityId", "customerId", "packageNo", "customerOrderRef",
                "beginDate", "plannedDate", "address", "description"
            )
        }
    )
}
$formPackageBody = @{
    name = $formPackageName
    workspaceId = $workspaceId
    defaultTypeId = $typePackageId
    defaultStateFlowId = $mainFlowId
    defaultStateId = $stNew
    isDefault = $false
    layout = $formPackageLayout
    fieldBehaviors = @{
        title = @{ visible = $true; required = $true }
        typeId = @{ visible = $true; required = $true; defaultValue = $typePackageId }
        priorityId = @{ visible = $true; required = $false; defaultValue = $prioNormalId }
        customerId = @{ visible = $true; required = $true }
        packageNo = @{ visible = $true; required = $false }
        customerOrderRef = @{ visible = $true; required = $false }
        beginDate = @{ visible = $true; required = $false }
        plannedDate = @{ visible = $true; required = $false }
        address = @{ visible = $true; required = $false }
    }
}
$formPackageId = Find-OrCreate -Collection "op_forms" -Filter "name:eq:$formPackageName" -Label "Form is paketi" -Body $formPackageBody
Sync-Record -Collection "op_forms" -Id $formPackageId -Label "Form is paketi sync" -Body $formPackageBody

# --- Board ---
Write-Host "[4] op_boards (Is Paketleri panosu)..." -ForegroundColor Yellow
$boardScopeColumns = @(
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
$boardListColumns = @(
    @{ key = "key"; label = "OC No"; sortable = $true; filterable = $false },
    @{ key = "packageNo"; label = "Is paketi no"; sortable = $true; filterable = $true },
    @{ key = "title"; label = "Is paketi ismi"; sortable = $true; filterable = $true },
    @{ key = "customerId"; label = "Musteri"; sortable = $false; filterable = $true },
    @{ key = "customerOrderRef"; label = "Musteri PO"; sortable = $true; filterable = $true },
    @{ key = "stateId"; label = "Durum"; sortable = $true; filterable = $true },
    @{ key = "beginDate"; label = "Baslangic"; sortable = $true; filterable = $true; format = "date" },
    @{ key = "plannedDate"; label = "Termin"; sortable = $true; filterable = $true; format = "date" },
    @{ key = "closedAt"; label = "Kapanis"; sortable = $true; filterable = $true; format = "date" },
    @{ key = "typeId"; label = "Tip"; sortable = $true; filterable = $true }
)
$boardPackageName = "$tag - Is Paketleri panosu"
$boardPackageBody = @{
    name = $boardPackageName
    workspaceId = $workspaceId
    viewType = "list"
    defaultStateFlowId = $mainFlowId
    defaultFormId = $formPackageId
    isDefault = $false
    visibleFields = @("key", "packageNo", "title", "customerId", "customerOrderRef", "stateId", "beginDate", "plannedDate")
    config = @{
        columns = $boardScopeColumns
        listColumns = $boardListColumns
        defaultSort = @{ field = "key"; direction = "desc" }
    }
}
$boardPackageId = Find-OrCreate -Collection "op_boards" -Filter "name:eq:$boardPackageName" -Label "Is Paketleri panosu" -Body $boardPackageBody
Sync-Record -Collection "op_boards" -Id $boardPackageId -Label "Is Paketleri panosu sync" -Body $boardPackageBody

# --- Workspace enabled types/fields ---
Write-Host "[5] op_workspaces sync..." -ForegroundColor Yellow
$wsRow = Invoke-DgGet -Collection "op_workspaces" -Filter "name:eq:Odak Uretim" -Limit 1
$ws = @(Get-Items $wsRow)[0]
$enabledTypes = @($ws.enabledTypeIds | ForEach-Object {
        if ($_ -is [string]) { $_ } else { $_.__dataId ?? $_.dataId ?? $_ }
    }) | Where-Object { $_ }
$enabledFields = @($ws.enabledFieldIds | ForEach-Object {
        if ($_ -is [string]) { $_ } else { $_.__dataId ?? $_.dataId ?? $_ }
    }) | Where-Object { $_ }

foreach ($tid in @($typePackageId)) {
    if ($enabledTypes -notcontains $tid) { $enabledTypes += $tid }
}
foreach ($fid in @($fieldLegacyPackageId, $fieldPackageNo, $fieldBeginDate, $fieldAddress)) {
    if ($enabledFields -notcontains $fid) { $enabledFields += $fid }
}

Invoke-DgPut -Collection "op_workspaces" -Id $workspaceId -Body @{
    enabledTypeIds = $enabledTypes
    enabledFieldIds = $enabledFields
} | Out-Null
Write-Host "  OK: workspace types/fields guncellendi" -ForegroundColor Green

# --- Metadata cache ---
if ($ReloadMetadataCache) {
    Write-Host "[6] MO metadata cache reload..." -ForegroundColor Yellow
    try {
        $moUri = "$MoBaseUrl/api/v1/workspaces/$workspaceId/metadata-cache/reload"
        $moParams = @{ Uri = $moUri; Method = "POST" } + $irmParams
        $reload = Invoke-RestMethod @moParams
        Write-Host "  OK: keysRemoved=$($reload.keysRemoved)" -ForegroundColor Green
    }
    catch {
        Write-Host "  WARN: $($_.Exception.Message)" -ForegroundColor Yellow
    }
}

# --- Update seed JSON ---
Write-Host "[7] odak-uretim-seed.json guncelle..." -ForegroundColor Yellow
$seedObj = Get-Content $SeedFile -Raw -Encoding UTF8 | ConvertFrom-Json
if (-not $seedObj.types) { $seedObj | Add-Member -NotePropertyName types -NotePropertyValue (@{}) }
$seedObj.types | Add-Member -NotePropertyName package -NotePropertyValue $typePackageId -Force
$seedObj | Add-Member -NotePropertyName boardPackageId -NotePropertyValue $boardPackageId -Force
$seedObj | Add-Member -NotePropertyName formPackageId -NotePropertyValue $formPackageId -Force
$seedObj | Add-Member -NotePropertyName packageFields -NotePropertyValue @{
    legacyPackageId = $fieldLegacyPackageId
    packageNo = $fieldPackageNo
    beginDate = $fieldBeginDate
    address = $fieldAddress
} -Force
$seedObj | ConvertTo-Json -Depth 10 | Set-Content -Path $SeedFile -Encoding UTF8

Write-Host "`nOK: Is Paketi seed tamamlandi." -ForegroundColor Green
Write-Host "  typePackageId:  $typePackageId" -ForegroundColor Cyan
Write-Host "  boardPackageId: $boardPackageId" -ForegroundColor Cyan
Write-Host "  formPackageId:  $formPackageId" -ForegroundColor Cyan
Write-Host "`nSonraki: Mng.Ui/utils/odakSiparisConfig.ts icindeki package* id'leri seed ciktisiyla guncelleyin." -ForegroundColor Yellow
Write-Host "  patch-odak-siparis-board-list.ps1 -BoardId $boardPackageId" -ForegroundColor Yellow
