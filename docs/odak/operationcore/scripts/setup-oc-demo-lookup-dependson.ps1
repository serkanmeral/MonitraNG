# ulkeler + sehirler dataset + dependsOn lookup alanlari (OC Demo Workspace)
#
# Usage (repo kokunden):
#   .\docs\odak\operationcore\scripts\get-operationcore-token.ps1
#   .\docs\odak\operationcore\scripts\setup-oc-demo-lookup-dependson.ps1 -ReloadMetadataCache
#
# Onkosul: setup-oc-demo-tedarikci-lookup.ps1 veya OC Demo workspace seed

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
$ulkelerFile = Join-Path $repoRoot "docs/odak/operationcore/datasets/ulkeler_dataset.json"
$sehirlerFile = Join-Path $repoRoot "docs/odak/operationcore/datasets/sehirler_dataset.json"
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

function Get-DataId {
    param($Response)
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
    $items = @(Get-Items (Invoke-Dg -Method GET -Uri $listUri))
    $found = $items | Where-Object { $_.categoryName -eq $CategoryName } | Select-Object -First 1
    if ($found) {
        $id = $found.dataId; if (-not $id) { $id = $found.__dataId }
        Write-Host "  Category mevcut: $CategoryName ($id)" -ForegroundColor Yellow
        return $id
    }
    $cat = Get-Content $categoryFile -Raw -Encoding UTF8 | ConvertFrom-Json
    Invoke-Dg -Method POST -Uri "$BaseUrl$categoriesPath" -Body @{
        categoryName        = $cat.categoryName
        categoryDescription = $cat.categoryDescription
        isSystemCategory    = $false
    } | Out-Null
    $items2 = @(Get-Items (Invoke-Dg -Method GET -Uri $listUri))
    $found2 = $items2 | Where-Object { $_.categoryName -eq $CategoryName } | Select-Object -First 1
    if (-not $found2) { throw "Category olusturulamadi: $CategoryName" }
    $id = $found2.dataId; if (-not $id) { $id = $found2.__dataId }
    Write-Host "  OK: Category $CategoryName -> $id" -ForegroundColor Green
    return $id
}

function Ensure-Dataset {
    param([string]$JsonPath, [string]$CategoryId)
    $schema = Get-Content $JsonPath -Raw -Encoding UTF8 | ConvertFrom-Json
    $getUri = '{0}{1}/{2}' -f $BaseUrl, $datasetsPath, [Uri]::EscapeDataString($schema.name)
    try {
        $null = Invoke-Dg -Method GET -Uri $getUri
        Write-Host "  SKIP: dataset $($schema.name) zaten var" -ForegroundColor Yellow
        return
    }
    catch { }
    Invoke-Dg -Method POST -Uri "$BaseUrl$datasetsPath" -Body @{
        Name        = $schema.name
        Description = $schema.description
        Category    = $CategoryId
        ForceSchema = $schema.forceSchema
        Logging     = $schema.logging
        PublishMode = $schema.publish_mode
        Fields      = $schema.fields
        IndexList   = $schema.indexList
    } | Out-Null
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
        if ($items.Count -gt 0) { $script:WorkspaceId = $items[0].__dataId }
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

function Ensure-PoolField {
    param(
        [string]$Key,
        [string]$Label,
        [string]$RelationDataset,
        [hashtable]$LookupOptions,
        [string]$Description
    )
    $fieldId = Find-OrCreate -Collection "op_fields" -Filter "key:eq:$Key" -Label $Key -Body @{
        key                 = $Key
        label               = $Label
        fieldType           = "relation"
        scope               = "pool"
        category            = "classification"
        cardinality         = "single"
        description         = $Description
        workspaceId         = $WorkspaceId
        relationDatasetName = $RelationDataset
        options             = $LookupOptions
        isSystem            = $false
        isSensitive         = $false
    }
    try {
        Invoke-Dg -Method PUT -Uri "$BaseUrl$dataPath/op_fields/$fieldId" -Body @{
            label               = $Label
            fieldType           = "relation"
            scope               = "pool"
            category            = "classification"
            cardinality         = "single"
            description         = $Description
            workspaceId         = $WorkspaceId
            relationDatasetName = $RelationDataset
            options             = $LookupOptions
            isSystem            = $false
            isSensitive         = $false
        } | Out-Null
        Write-Host "  SYNC: $Key metadata guncellendi" -ForegroundColor Green
    }
    catch {
        Write-Host "  WARN: $Key PUT atlandi: $($_.Exception.Message)" -ForegroundColor DarkYellow
    }
    return $fieldId
}

function Add-FieldToLayout {
    param(
        [string]$EntityCollection,
        [string]$EntityId,
        [string[]]$FieldKeys,
        [switch]$ReadonlyProfile
    )
    if ([string]::IsNullOrEmpty($EntityId)) { return }
    $raw = Invoke-Dg -Method GET -Uri "$BaseUrl$dataPath/$EntityCollection/$EntityId"
    $entity = $raw.data; if (-not $entity) { $entity = $raw }
    $sections = @()
    if ($entity.layout -and $entity.layout.sections) {
        $sections = @($entity.layout.sections | ForEach-Object { $_ })
    }
    if ($sections.Count -eq 0) {
        $sections = @(@{ key = "main"; title = "Temel"; fields = @("title", "description") })
    }
    $main = $sections[0]
    $fieldList = [System.Collections.ArrayList]@()
    if ($main.fields) { [void]$fieldList.AddRange(@($main.fields)) }
    foreach ($fk in $FieldKeys) {
        if ($fieldList -notcontains $fk) { [void]$fieldList.Add($fk) }
    }
    $main.fields = @($fieldList)
    $sections[0] = $main
    $fieldBehaviors = @{}
    if ($entity.fieldBehaviors) {
        $entity.fieldBehaviors.PSObject.Properties | ForEach-Object { $fieldBehaviors[$_.Name] = $_.Value }
    }
    foreach ($fk in $FieldKeys) {
        if (-not $fieldBehaviors.ContainsKey($fk)) {
            $fieldBehaviors[$fk] = if ($ReadonlyProfile) { @{ visible = $true; readonly = $false } } else { @{ visible = $true } }
        }
    }
    Invoke-Dg -Method PUT -Uri "$BaseUrl$dataPath/$EntityCollection/$EntityId" -Body @{
        layout         = @{ sections = $sections }
        fieldBehaviors = $fieldBehaviors
    } | Out-Null
}

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "OC Demo — dependsOn lookup (ulke/sehir)" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

Write-Host "[1] Dataset kategori + ulkeler/sehirler..." -ForegroundColor Yellow
$categoryId = Ensure-DatasetCategory -CategoryName "BusinessDatasets"
Ensure-Dataset -JsonPath $ulkelerFile -CategoryId $categoryId
Ensure-Dataset -JsonPath $sehirlerFile -CategoryId $categoryId

Write-Host "[2] Seed ulkeler..." -ForegroundColor Yellow
$ulkeIds = @{}
$ulkeler = @(
    @{ kod = "TR"; ad = "Turkiye" },
    @{ kod = "DE"; ad = "Almanya" }
)
foreach ($u in $ulkeler) {
    $id = Find-OrCreate -Collection "ulkeler" -Filter "kod:eq:$($u.kod)" -Label "Ulke $($u.kod)" -Body @{
        kod = $u.kod; ad = $u.ad; isActive = $true
    }
    $ulkeIds[$u.kod] = $id
}

Write-Host "[3] Seed sehirler..." -ForegroundColor Yellow
$sehirler = @(
    @{ kod = "IST"; ad = "Istanbul"; ulke = "TR" },
    @{ kod = "ANK"; ad = "Ankara"; ulke = "TR" },
    @{ kod = "IZM"; ad = "Izmir"; ulke = "TR" },
    @{ kod = "BER"; ad = "Berlin"; ulke = "DE" },
    @{ kod = "MUC"; ad = "Munchen"; ulke = "DE" }
)
foreach ($s in $sehirler) {
    $ulkeId = $ulkeIds[$s.ulke]
    if (-not $ulkeId) { throw "Ulke id yok: $($s.ulke)" }
    $null = Find-OrCreate -Collection "sehirler" -Filter "kod:eq:$($s.kod)" -Label "Sehir $($s.kod)" -Body @{
        kod = $s.kod; ad = $s.ad; ulkeId = $ulkeId; isActive = $true
    }
}

Write-Host "[4] OC Demo Workspace..." -ForegroundColor Yellow
Resolve-DemoIds
if ([string]::IsNullOrEmpty($WorkspaceId)) { throw "OC Demo Workspace bulunamadi." }
Write-Host "  workspaceId=$WorkspaceId" -ForegroundColor Green

Write-Host "[5] op_fields ulkeId + sehirId..." -ForegroundColor Yellow
$ulkeLookup = @{
    lookup = @{
        source       = "dataset"
        presentation = "dropdown"
        valueField   = "__dataId"
        labelField   = "ad"
        pageSize     = 50
        filter       = "isActive:eq:true"
    }
}
$sehirLookup = @{
    lookup = @{
        source       = "dataset"
        presentation = "autocomplete"
        valueField   = "__dataId"
        labelField   = "ad"
        pageSize     = 50
        filter       = "isActive:eq:true"
        dependsOn    = @{
            fieldKey       = "ulkeId"
            filterTemplate = "ulkeId:eq:{{parentValue}}"
        }
    }
}
$ulkeFieldId = Ensure-PoolField -Key "ulkeId" -Label "Ulke" -RelationDataset "ulkeler" -LookupOptions $ulkeLookup -Description "OC dependsOn demo — ust alan"
$sehirFieldId = Ensure-PoolField -Key "sehirId" -Label "Sehir" -RelationDataset "sehirler" -LookupOptions $sehirLookup -Description "OC dependsOn demo — bagimli alan (ulkeId)"

Write-Host "[6] enabledFieldIds..." -ForegroundColor Yellow
$wsRaw = Invoke-Dg -Method GET -Uri "$BaseUrl$dataPath/op_workspaces/$WorkspaceId"
$ws = $wsRaw.data; if (-not $ws) { $ws = $wsRaw }
$enabled = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
if ($ws.enabledFieldIds) {
    foreach ($eid in @($ws.enabledFieldIds)) {
        if ($eid) { [void]$enabled.Add("$eid") }
    }
}
foreach ($fid in @($ulkeFieldId, $sehirFieldId)) {
    if ($fid) { [void]$enabled.Add("$fid") }
}
# tedarikciId alanini koru (onceki demo kurulumu)
$tedarikciRows = @(Get-Items (Invoke-Dg -Method GET -Uri "$BaseUrl$dataPath/op_fields?limit=5&filter=$([Uri]::EscapeDataString('key:eq:tedarikciId'))"))
if ($tedarikciRows.Count -gt 0) {
    $tid = Get-DataId $tedarikciRows[0]
    if ($tid) { [void]$enabled.Add($tid) }
}
Invoke-Dg -Method PUT -Uri "$BaseUrl$dataPath/op_workspaces/$WorkspaceId" -Body @{
    enabledFieldIds = @($enabled)
} | Out-Null
Write-Host "  OK: enabledFieldIds (+ ulkeId, sehirId; mevcut korundu)" -ForegroundColor Green

Write-Host "[7] Form + profile layout..." -ForegroundColor Yellow
Add-FieldToLayout -EntityCollection "op_forms" -EntityId $FormId -FieldKeys @("ulkeId", "sehirId")
Add-FieldToLayout -EntityCollection "op_profiles" -EntityId $ProfileId -FieldKeys @("ulkeId", "sehirId") -ReadonlyProfile
Write-Host "  OK: layout guncellendi" -ForegroundColor Green

if ($ReloadMetadataCache) {
    Write-Host "[8] MO metadata cache reload..." -ForegroundColor Yellow
    try {
        $moUri = "$MoBaseUrl/api/v1/workspaces/$WorkspaceId/metadata-cache/reload"
        $moParams = @{ Uri = $moUri; Method = "POST"; Headers = $headers; ErrorAction = "Stop" }
        if ($MoBaseUrl.StartsWith("https://") -and $irmParams.ContainsKey("SkipCertificateCheck")) {
            $moParams.SkipCertificateCheck = $true
        }
        $r = Invoke-RestMethod @moParams
        $removed = $r.keysRemoved; if ($null -eq $removed) { $removed = $r.KeysRemoved }
        Write-Host "  OK: cache reload (keysRemoved=$removed)" -ForegroundColor Green
    }
    catch {
        Write-Host "  WARN: cache reload: $($_.Exception.Message)" -ForegroundColor DarkYellow
    }
}

Write-Host "`nTamamlandi. Formda once Ulke, sonra Sehir secin; ulke degisince sehir temizlenmeli." -ForegroundColor Cyan
