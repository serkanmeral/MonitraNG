# tedarikciler dataset + seed + OC Demo Workspace lookup alani / form
#
# Usage (repo kokunden):
#   .\docs\odak\operationcore\scripts\get-operationcore-token.ps1
#   .\docs\odak\operationcore\scripts\setup-oc-demo-tedarikci-lookup.ps1
#
# Opsiyonel MO cache reload:
#   .\docs\odak\operationcore\scripts\setup-oc-demo-tedarikci-lookup.ps1 -ReloadMetadataCache

param(
    [string]$BaseUrl = "http://192.168.20.20:5040",
    [string]$MoBaseUrl = "http://192.168.20.20:5040/operations",
    [switch]$UseGateway = $true,
    [switch]$ReloadMetadataCache = $false,
    [string]$WorkspaceId = "",
    [string]$FormId = "",
    [string]$ProfileId = ""
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
$repoRoot = (Resolve-Path (Join-Path $scriptDir "../../../..")).Path
$categoryFile = Join-Path $repoRoot "docs/odak/operationcore/datasets/tedarikciler_dataset_category.json"
$datasetFile = Join-Path $repoRoot "docs/odak/operationcore/datasets/tedarikciler_dataset.json"
$seedJson = Join-Path $scriptDir "operationcore-demo-seed.json"

$dataPath = if ($UseGateway) { "/data/api/v1/data" } else { "/api/v1/data" }
$datasetsPath = if ($UseGateway) { "/data/api/v1/datasets" } else { "/api/v1/datasets" }
$categoriesPath = if ($UseGateway) { "/data/api/v1/dataset-categories" } else { "/api/v1/dataset-categories" }
$demoTag = "OC Demo"

$loadTokenScript = Join-Path $scriptDir "load-operationcore-token.ps1"
if (-not (Test-Path $loadTokenScript)) { throw "Token script yok: $loadTokenScript" }
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

function Invoke-Dg {
    param(
        [Parameter(Mandatory = $true)][string]$Method,
        [Parameter(Mandatory = $true)][string]$Uri,
        [object]$Body = $null
    )
    $params = @{
        Uri         = $Uri
        Method      = $Method
        Headers     = $headers
        ErrorAction = "Stop"
    }
    if ($Uri.StartsWith("https://") -and $irmParams.ContainsKey("SkipCertificateCheck")) {
        $params.SkipCertificateCheck = $true
    }
    if ($null -ne $Body) {
        $params.Body = if ($Body -is [string]) { $Body } else { $Body | ConvertTo-Json -Depth 25 -Compress }
        $params.ContentType = "application/json"
    }
    return Invoke-RestMethod @params
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

function Get-DataId { param($Response)
    if (-not $Response) { return $null }
    $d = $Response.data; if (-not $d) { $d = $Response.Data }; if (-not $d) { $d = $Response }
    $id = $d.__dataId; if (-not $id) { $id = $d.dataId }; if (-not $id) { $id = $d.DataId }
    return $id
}

function Find-OrCreate {
    param([string]$Collection, [string]$Filter, [object]$Body, [string]$Label)
    $existing = @(Get-Items (Invoke-Dg -Method GET -Uri "$BaseUrl$dataPath/$Collection$(if ($Filter) { "?limit=5&filter=$([Uri]::EscapeDataString($Filter))" } else { "?limit=5" })"))
    if ($existing.Count -gt 0) {
        $id = $existing[0].__dataId; if (-not $id) { $id = $existing[0].dataId }
        Write-Host "  SKIP: $Label ($id)" -ForegroundColor Yellow
        return $id
    }
    $created = Invoke-Dg -Method POST -Uri "$BaseUrl$dataPath/$Collection" -Body $Body
    $id = Get-DataId $created
    Write-Host "  OK: $Label -> $id" -ForegroundColor Green
    return $id
}

function Ensure-DatasetCategory {
    param([string]$CategoryName)
    $listUri = '{0}{1}?pageSize=200' -f $BaseUrl, $categoriesPath
    $list = Invoke-Dg -Method GET -Uri $listUri
    $items = Get-Items $list
    $found = $items | Where-Object { $_.categoryName -eq $CategoryName } | Select-Object -First 1
    if ($found) {
        $id = $found.dataId; if (-not $id) { $id = $found.__dataId }
        Write-Host "  Category mevcut: $CategoryName ($id)" -ForegroundColor Yellow
        return $id
    }
    $cat = Get-Content $categoryFile -Raw -Encoding UTF8 | ConvertFrom-Json
    $body = @{
        categoryName        = $cat.categoryName
        categoryDescription = $cat.categoryDescription
        isSystemCategory    = $false
    }
    try {
        Invoke-Dg -Method POST -Uri "$BaseUrl$categoriesPath" -Body $body | Out-Null
    }
    catch {
        Write-Host "  Category POST (devam): $($_.Exception.Message)" -ForegroundColor DarkYellow
    }
    $list2 = Invoke-Dg -Method GET -Uri $listUri
    $found2 = @(Get-Items $list2) | Where-Object { $_.categoryName -eq $CategoryName } | Select-Object -First 1
    if (-not $found2) { throw "Category olusturulamadi: $CategoryName" }
    $id = $found2.dataId; if (-not $id) { $id = $found2.__dataId }
    Write-Host "  OK: Category $CategoryName -> $id" -ForegroundColor Green
    return $id
}

function Ensure-Dataset {
    param([string]$CategoryId)
    $schema = Get-Content $datasetFile -Raw -Encoding UTF8 | ConvertFrom-Json
    $getUri = '{0}{1}/{2}' -f $BaseUrl, $datasetsPath, [Uri]::EscapeDataString($schema.name)
    try {
        $null = Invoke-Dg -Method GET -Uri $getUri
        Write-Host "  SKIP: dataset $($schema.name) zaten var" -ForegroundColor Yellow
        return
    }
    catch { }
    $body = @{
        Name        = $schema.name
        Description = $schema.description
        Category    = $CategoryId
        ForceSchema = $schema.forceSchema
        Logging     = $schema.logging
        PublishMode = $schema.publish_mode
        Fields      = $schema.fields
        IndexList   = $schema.indexList
    }
    Invoke-Dg -Method POST -Uri "$BaseUrl$datasetsPath" -Body $body | Out-Null
    Write-Host "  OK: dataset $($schema.name) olusturuldu" -ForegroundColor Green
}

function Resolve-DemoIds {
    if (-not [string]::IsNullOrEmpty($WorkspaceId)) { return }
    if (Test-Path $seedJson) {
        $seed = Get-Content $seedJson -Raw -Encoding UTF8 | ConvertFrom-Json
        if ($seed.workspaceId) { $script:WorkspaceId = $seed.workspaceId }
        if ($seed.formId) { $script:FormId = $seed.formId }
        if ($seed.profileId) { $script:ProfileId = $seed.profileId }
    }
    if ([string]::IsNullOrEmpty($WorkspaceId)) {
        $wsName = "$demoTag Workspace"
        $items = @(Get-Items (Invoke-Dg -Method GET -Uri "$BaseUrl$dataPath/op_workspaces?limit=5&filter=$([Uri]::EscapeDataString("name:eq:$wsName"))"))
        if ($items.Count -gt 0) {
            $script:WorkspaceId = $items[0].__dataId
        }
    }
    if ([string]::IsNullOrEmpty($FormId) -and $WorkspaceId) {
        $formName = "$demoTag Create Form"
        $items = @(Get-Items (Invoke-Dg -Method GET -Uri "$BaseUrl$dataPath/op_forms?limit=5&filter=$([Uri]::EscapeDataString("name:eq:$formName"))"))
        if ($items.Count -gt 0) { $script:FormId = $items[0].__dataId }
    }
    if ([string]::IsNullOrEmpty($ProfileId) -and $WorkspaceId) {
        $profileName = "$demoTag Work Item Profile"
        $items = @(Get-Items (Invoke-Dg -Method GET -Uri "$BaseUrl$dataPath/op_profiles?limit=5&filter=$([Uri]::EscapeDataString("name:eq:$profileName"))"))
        if ($items.Count -gt 0) { $script:ProfileId = $items[0].__dataId }
    }
}

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "OC Demo — tedarikciler lookup kurulumu" -ForegroundColor Cyan
Write-Host "Gateway: $BaseUrl" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

# --- 1. Category + dataset ---
Write-Host "[1] Dataset kategori + tedarikciler..." -ForegroundColor Yellow
$categoryId = Ensure-DatasetCategory -CategoryName "BusinessDatasets"
Ensure-Dataset -CategoryId $categoryId

# --- 2. Seed tedarikciler ---
Write-Host "[2] tedarikciler seed..." -ForegroundColor Yellow
$suppliers = @(
    @{ kod = "TED-001"; unvan = "ABC Teknoloji A.S."; vergiNo = "1234567890"; sehir = "Istanbul"; email = "info@abc-teknoloji.com"; isActive = $true },
    @{ kod = "TED-002"; unvan = "Metro Endustri Ltd. Sti."; vergiNo = "2345678901"; sehir = "Ankara"; email = "satis@metro-endustri.com"; isActive = $true },
    @{ kod = "TED-003"; unvan = "Delta Lojistik A.S."; vergiNo = "3456789012"; sehir = "Izmir"; email = "iletisim@delta-lojistik.com"; isActive = $true },
    @{ kod = "TED-004"; unvan = "Beta Kimya San. Tic."; vergiNo = "4567890123"; sehir = "Bursa"; email = "destek@beta-kimya.com"; isActive = $true },
    @{ kod = "TED-005"; unvan = "Omega Elektronik"; vergiNo = "5678901234"; sehir = "Istanbul"; email = "siparis@omega-elektronik.com"; isActive = $true },
    @{ kod = "TED-006"; unvan = "Sigma Malzeme Tic. A.S."; vergiNo = "6789012345"; sehir = "Ankara"; email = "info@sigma-malzeme.com"; isActive = $true },
    @{ kod = "TED-007"; unvan = "Nova Otomasyon"; vergiNo = "7890123456"; sehir = "Kocaeli"; email = "teknik@nova-otomasyon.com"; isActive = $true },
    @{ kod = "TED-008"; unvan = "Penta Insaat Malzemeleri"; vergiNo = "8901234567"; sehir = "Antalya"; email = "satis@penta-insaat.com"; isActive = $false }
)
$seeded = 0
foreach ($s in $suppliers) {
    $null = Find-OrCreate -Collection "tedarikciler" -Filter "kod:eq:$($s.kod)" -Label "Tedarikci $($s.kod)" -Body $s
    $seeded++
}
Write-Host "  Toplam islem: $seeded kayit" -ForegroundColor Gray

# --- 3. Demo workspace ids ---
Write-Host "[3] OC Demo Workspace cozumleme..." -ForegroundColor Yellow
Resolve-DemoIds
if ([string]::IsNullOrEmpty($WorkspaceId)) { throw "OC Demo Workspace bulunamadi. Once seed-operation-core-demo.ps1 calistirin." }
Write-Host "  workspaceId=$WorkspaceId formId=$FormId profileId=$ProfileId" -ForegroundColor Green

# --- 4. op_fields tedarikciId ---
Write-Host "[4] op_fields tedarikciId..." -ForegroundColor Yellow
$lookupOptions = @{
    lookup = @{
        source       = "dataset"
        presentation = "autocomplete"
        valueField   = "__dataId"
        labelField   = "unvan"
        pageSize     = 50
        filter       = "isActive:eq:true"
    }
}
$fieldId = Find-OrCreate -Collection "op_fields" -Filter "key:eq:tedarikciId" -Label "tedarikciId" -Body @{
    key                  = "tedarikciId"
    label                = "Tedarikci"
    fieldType            = "relation"
    scope                = "pool"
    category             = "classification"
    cardinality          = "single"
    description          = "OC lookup demo — tedarikciler dataset"
    workspaceId          = $WorkspaceId
    relationDatasetName  = "tedarikciler"
    options              = $lookupOptions
    isSystem             = $false
    isSensitive          = $false
}

# Mevcut kayitta lookup metadata guncelle
try {
    Invoke-Dg -Method PUT -Uri "$BaseUrl$dataPath/op_fields/$fieldId" -Body @{
        label               = "Tedarikci"
        fieldType           = "relation"
        scope               = "pool"
        category            = "classification"
        cardinality         = "single"
        description         = "OC lookup demo — tedarikciler dataset"
        workspaceId         = $WorkspaceId
        relationDatasetName = "tedarikciler"
        options             = $lookupOptions
        isSystem            = $false
        isSensitive         = $false
    } | Out-Null
    Write-Host "  SYNC: op_fields lookup metadata guncellendi" -ForegroundColor Green
}
catch {
    Write-Host "  WARN: op_fields PUT atlandi: $($_.Exception.Message)" -ForegroundColor DarkYellow
}

# --- 5. Workspace enabledFieldIds ---
Write-Host "[5] op_workspaces enabledFieldIds..." -ForegroundColor Yellow
$wsRaw = Invoke-Dg -Method GET -Uri "$BaseUrl$dataPath/op_workspaces/$WorkspaceId"
$ws = $wsRaw.data; if (-not $ws) { $ws = $wsRaw }
$enabled = @()
if ($ws.enabledFieldIds) { $enabled = @($ws.enabledFieldIds) }
if ($enabled -notcontains $fieldId) { $enabled += $fieldId }
Invoke-Dg -Method PUT -Uri "$BaseUrl$dataPath/op_workspaces/$WorkspaceId" -Body @{
    enabledFieldIds = $enabled
} | Out-Null
Write-Host "  OK: enabledFieldIds (+ tedarikciId)" -ForegroundColor Green

# --- 6. Form layout ---
Write-Host "[6] op_forms layout..." -ForegroundColor Yellow
if ($FormId) {
    $formRaw = Invoke-Dg -Method GET -Uri "$BaseUrl$dataPath/op_forms/$FormId"
    $form = $formRaw.data; if (-not $form) { $form = $formRaw }
    $sections = @()
    if ($form.layout -and $form.layout.sections) {
        $sections = @($form.layout.sections | ForEach-Object { $_ })
    }
    if ($sections.Count -eq 0) {
        $sections = @(@{ key = "main"; title = "Temel bilgiler"; fields = @("title", "description", "typeId") })
    }
    $main = $sections[0]
    $fieldList = [System.Collections.ArrayList]@()
    if ($main.fields) { [void]$fieldList.AddRange(@($main.fields)) }
    if ($fieldList -notcontains "tedarikciId") {
        $insertAt = $fieldList.IndexOf("typeId")
        if ($insertAt -ge 0) { $insertAt += 1 } else { $insertAt = $fieldList.Count }
        if ($insertAt -gt $fieldList.Count) { $insertAt = $fieldList.Count }
        [void]$fieldList.Insert($insertAt, "tedarikciId")
    }
    $main.fields = @($fieldList)
    $sections[0] = $main
    $fieldBehaviors = @{}
    if ($form.fieldBehaviors) {
        $form.fieldBehaviors.PSObject.Properties | ForEach-Object { $fieldBehaviors[$_.Name] = $_.Value }
    }
    if (-not $fieldBehaviors.ContainsKey("tedarikciId")) {
        $fieldBehaviors["tedarikciId"] = @{ visible = $true }
    }
    Invoke-Dg -Method PUT -Uri "$BaseUrl$dataPath/op_forms/$FormId" -Body @{
        layout         = @{ sections = $sections }
        fieldBehaviors = $fieldBehaviors
    } | Out-Null
    Write-Host "  OK: form layout (+ tedarikciId)" -ForegroundColor Green
}

# --- 7. Profile layout ---
Write-Host "[7] op_profiles layout..." -ForegroundColor Yellow
if ($ProfileId) {
    $profRaw = Invoke-Dg -Method GET -Uri "$BaseUrl$dataPath/op_profiles/$ProfileId"
    $prof = $profRaw.data; if (-not $prof) { $prof = $profRaw }
    $sections = @()
    if ($prof.layout -and $prof.layout.sections) {
        $sections = @($prof.layout.sections | ForEach-Object { $_ })
    }
    if ($sections.Count -eq 0) {
        $sections = @(@{ key = "summary"; title = "Ozet"; fields = @("title", "description") })
    }
    $summary = $sections[0]
    $fields = @()
    if ($summary.fields) { $fields = @($summary.fields) }
    if ($fields -notcontains "tedarikciId") {
        $fields += "tedarikciId"
    }
    $summary.fields = $fields
    $sections[0] = $summary
    $fieldBehaviors = @{}
    if ($prof.fieldBehaviors) {
        $prof.fieldBehaviors.PSObject.Properties | ForEach-Object { $fieldBehaviors[$_.Name] = $_.Value }
    }
    if (-not $fieldBehaviors.ContainsKey("tedarikciId")) {
        $fieldBehaviors["tedarikciId"] = @{ visible = $true; readonly = $false }
    }
    Invoke-Dg -Method PUT -Uri "$BaseUrl$dataPath/op_profiles/$ProfileId" -Body @{
        layout         = @{ sections = $sections }
        fieldBehaviors = $fieldBehaviors
    } | Out-Null
    Write-Host "  OK: profile layout (+ tedarikciId)" -ForegroundColor Green
}

# --- 8. MO metadata cache reload ---
if ($ReloadMetadataCache) {
    Write-Host "[8] MO metadata cache reload..." -ForegroundColor Yellow
    try {
        $moUri = "$MoBaseUrl/api/v1/workspaces/$WorkspaceId/metadata-cache/reload"
        $moParams = @{
            Uri         = $moUri
            Method      = "POST"
            Headers     = $headers
            ErrorAction = "Stop"
        }
        if ($MoBaseUrl.StartsWith("https://") -and $irmParams.ContainsKey("SkipCertificateCheck")) {
            $moParams.SkipCertificateCheck = $true
        }
        $r = Invoke-RestMethod @moParams
        $removed = $r.keysRemoved; if ($null -eq $removed) { $removed = $r.KeysRemoved }
        Write-Host "  OK: cache reload (keysRemoved=$removed)" -ForegroundColor Green
    }
    catch {
        Write-Host "  WARN: cache reload basarisiz: $($_.Exception.Message)" -ForegroundColor DarkYellow
        Write-Host "  UI: Workspace Tanimlari -> Genel -> Runtime onbellegini yenile" -ForegroundColor Gray
    }
}
else {
    Write-Host "[8] MO cache reload atlandi (-ReloadMetadataCache ile acilir)" -ForegroundColor Gray
}

Write-Host "`nTamamlandi." -ForegroundColor Cyan
Write-Host "Test: Workspace Tanimlari -> OC Demo -> Formlar (onizleme) veya Board Yeni is" -ForegroundColor Gray
Write-Host "Alan: tedarikciId -> tedarikciler (aktif kayitlar, unvan ile autocomplete)" -ForegroundColor Gray
