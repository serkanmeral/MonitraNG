# IT Destek — Yeni kayit formu + kayit profili guncelleme (Production)
# - Kaldir: impact, urgency, requestCategory, affectedUser
# - Ekle: affectedGroups (personGroups, multi)
#
#   .\get-operationcore-token-prod.ps1
#   .\update-it-helpdesk-new-form-prod.ps1

param(
    [string]$BaseUrl = "http://192.168.20.8:5040",
    [switch]$UseGateway = $true
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
$dataPath = if ($UseGateway) { "/data/api/v1/data" } else { "/api/v1/data" }
$workspaceName = "IT Destek"
$formName = "IT Destek - Yeni kayit"

$loadTokenScript = Join-Path $scriptDir "load-operationcore-token-prod.ps1"
$token = & $loadTokenScript
if ([string]::IsNullOrEmpty($token)) {
    Write-Host "Token alinamadi." -ForegroundColor Red
    exit 1
}

$headers = @{
    "Authorization" = "Bearer $token"
    "Content-Type"  = "application/json"
}
[System.Net.ServicePointManager]::ServerCertificateValidationCallback = { $true }
$irmParams = @{ Headers = $headers; ErrorAction = "Stop" }

function Invoke-DgGet {
    param([string]$Collection, [string]$Filter = "", [int]$Limit = 100)
    $uri = "$BaseUrl$dataPath/$Collection`?limit=$Limit"
    if (-not [string]::IsNullOrEmpty($Filter)) { $uri += "&filter=" + [Uri]::EscapeDataString($Filter) }
    return Invoke-RestMethod -Uri $uri -Method GET @irmParams
}

function Invoke-DgPost {
    param([string]$Collection, [object]$Body)
    $uri = "$BaseUrl$dataPath/$Collection"
    $json = $Body | ConvertTo-Json -Depth 25 -Compress
    return Invoke-RestMethod -Uri $uri -Method POST -Body $json @irmParams
}

function Invoke-DgPut {
    param([string]$Collection, [string]$Id, [object]$Body)
    $uri = "$BaseUrl$dataPath/$Collection/$Id"
    $json = $Body | ConvertTo-Json -Depth 25 -Compress
    return Invoke-RestMethod -Uri $uri -Method PUT -Body $json @irmParams
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

function Get-IdFromRef {
    param($Value)
    if ($null -eq $Value) { return $null }
    if ($Value -is [string]) { return $Value }
    return $Value.__dataId
}

Write-Host "=== IT Destek yeni kayit formu guncelleme ===" -ForegroundColor Cyan

# 1. affectedGroups pool alani
Write-Host "[1] op_fields — affectedGroups..." -ForegroundColor Yellow
$existingField = @(Get-Items (Invoke-DgGet -Collection "op_fields" -Filter "key:eq:affectedGroups" -Limit 5))
if ($existingField.Count -gt 0) {
    $affectedGroupsFieldId = $existingField[0].__dataId
    Write-Host "  Mevcut alan: $affectedGroupsFieldId" -ForegroundColor Gray
    Invoke-DgPut -Collection "op_fields" -Id $affectedGroupsFieldId -Body @{
        key         = "affectedGroups"
        label       = "Etkilenen gruplar"
        fieldType   = "personGroups"
        scope       = "pool"
        category    = "assignment"
        cardinality = "multi"
    } | Out-Null
}
else {
    $created = Invoke-DgPost -Collection "op_fields" -Body @{
        key         = "affectedGroups"
        label       = "Etkilenen gruplar"
        fieldType   = "personGroups"
        scope       = "pool"
        category    = "assignment"
        cardinality = "multi"
    }
    $affectedGroupsFieldId = $created.__dataId
    if (-not $affectedGroupsFieldId) { $affectedGroupsFieldId = $created.data.__dataId }
    Write-Host "  Olusturuldu: $affectedGroupsFieldId" -ForegroundColor Green
}

# 2. Workspace — enabledFieldIds
Write-Host "[2] op_workspaces — enabledFieldIds..." -ForegroundColor Yellow
$wsFilter = "name:eq:$workspaceName"
$wsItems = @(Get-Items (Invoke-DgGet -Collection "op_workspaces" -Filter $wsFilter -Limit 5))
if ($wsItems.Count -eq 0) {
    Write-Host "Workspace bulunamadi: $workspaceName" -ForegroundColor Red
    exit 1
}
$ws = $wsItems[0]
$workspaceId = $ws.__dataId
$enabledFieldIds = @($ws.enabledFieldIds | ForEach-Object { Get-IdFromRef $_ } | Where-Object { $_ })
if ($affectedGroupsFieldId -notin $enabledFieldIds) {
    $enabledFieldIds += $affectedGroupsFieldId
}

$wsPutBody = @{
    name                  = $ws.name
    workspaceType         = $ws.workspaceType
    description           = $ws.description
    workItemKeyPrefix     = $ws.workItemKeyPrefix
    workItemKeyFormat     = $ws.workItemKeyFormat
    workItemSequenceStart = $ws.workItemSequenceStart
    viewGroups            = @($ws.viewGroups | ForEach-Object { Get-IdFromRef $_ })
    editGroups            = @($ws.editGroups | ForEach-Object { Get-IdFromRef $_ })
    adminGroups           = @($ws.adminGroups | ForEach-Object { Get-IdFromRef $_ })
    enabledTypeIds        = @($ws.enabledTypeIds | ForEach-Object { Get-IdFromRef $_ })
    enabledPriorityIds    = @($ws.enabledPriorityIds | ForEach-Object { Get-IdFromRef $_ })
    enabledStateIds       = @($ws.enabledStateIds | ForEach-Object { Get-IdFromRef $_ })
    enabledFieldIds       = $enabledFieldIds
    defaultStateFlowId    = Get-IdFromRef $ws.defaultStateFlowId
}
if ($ws.settings) { $wsPutBody.settings = $ws.settings }
Invoke-DgPut -Collection "op_workspaces" -Id $workspaceId -Body $wsPutBody | Out-Null
Write-Host "  Workspace guncellendi ($workspaceId), enabledFieldIds: $($enabledFieldIds.Count)" -ForegroundColor Green

# 3. Form
Write-Host "[3] op_forms — $formName..." -ForegroundColor Yellow
$formFilter = "workspaceId:eq:$workspaceId,name:eq:$formName"
$formItems = @(Get-Items (Invoke-DgGet -Collection "op_forms" -Filter $formFilter -Limit 5))
if ($formItems.Count -eq 0) {
    Write-Host "Form bulunamadi: $formName" -ForegroundColor Red
    exit 1
}
$form = $formItems[0]
$formId = $form.__dataId

$newLayoutFields = @(
    "title", "description", "typeId", "priorityId", "labels", "affectedAsset", "affectedGroups"
)

$formBody = @{
    name               = $form.name
    workspaceId        = $workspaceId
    defaultTypeId      = Get-IdFromRef $form.defaultTypeId
    defaultStateFlowId = Get-IdFromRef $form.defaultStateFlowId
    defaultStateId     = Get-IdFromRef $form.defaultStateId
    defaultPriorityId  = Get-IdFromRef $form.defaultPriorityId
    isDefault          = $form.isDefault
    layout             = @{
        sections = @(
            @{
                key    = "main"
                title  = "Talep bilgileri"
                cols   = 12
                fields = $newLayoutFields
            }
        )
        sectionOrder = @("main")
        sectionCols  = @{ main = 12 }
        dialogMaxWidth = if ($form.layout.dialogMaxWidth) { $form.layout.dialogMaxWidth } else { 920 }
    }
    fieldBehaviors     = @{
        title           = @{ visible = $true; required = $true }
        description     = @{ visible = $true; required = $true }
        typeId          = @{ visible = $true; required = $true }
        priorityId      = @{ visible = $true }
        labels          = @{ visible = $true }
        assignee        = @{ visible = $false }
        impact          = @{ visible = $false }
        urgency         = @{ visible = $false }
        requestCategory = @{ visible = $false }
        affectedUser    = @{ visible = $false }
        affectedAsset   = @{ visible = $true }
        affectedGroups  = @{ visible = $true }
    }
    defaultValues      = if ($form.defaultValues) { $form.defaultValues } else { @{ priorityId = Get-IdFromRef $form.defaultPriorityId } }
}

Invoke-DgPut -Collection "op_forms" -Id $formId -Body $formBody | Out-Null
Write-Host "  Form guncellendi ($formId)" -ForegroundColor Green
Write-Host "  Alanlar: $($newLayoutFields -join ', ')" -ForegroundColor Cyan

# 4. Profile
Write-Host "[4] op_profiles — IT Destek - Kayit profili..." -ForegroundColor Yellow
$profileName = "IT Destek - Kayit profili"
$profileFilter = "workspaceId:eq:$workspaceId,name:eq:$profileName"
$profileItems = @(Get-Items (Invoke-DgGet -Collection "op_profiles" -Filter $profileFilter -Limit 5))
if ($profileItems.Count -eq 0) {
    Write-Host "  UYARI: Profil bulunamadi: $profileName" -ForegroundColor Yellow
}
else {
    $profile = $profileItems[0]
    $profileId = $profile.__dataId
    $profileSummaryFields = @(
        "title", "description", "typeId", "priorityId", "assignee", "labels", "key", "affectedAsset", "affectedGroups"
    )
    $profileBody = @{
        name           = $profile.name
        workspaceId    = $workspaceId
        defaultTypeId  = Get-IdFromRef $profile.defaultTypeId
        isDefault      = $profile.isDefault
        fieldBehaviors = @{
            title             = @{ visible = $true; required = $true }
            description       = @{ visible = $true }
            typeId            = @{ visible = $true; readonly = $true }
            priorityId        = @{ visible = $true }
            assignee          = @{ visible = $true }
            labels            = @{ visible = $true }
            impact            = @{ visible = $false }
            urgency           = @{ visible = $false }
            requestCategory   = @{ visible = $false }
            affectedUser      = @{ visible = $false }
            affectedAsset     = @{ visible = $true }
            affectedGroups    = @{ visible = $true }
            resolutionSummary = @{ visible = $true }
        }
        actions        = $profile.actions
        header         = $profile.header
        sidebar        = $profile.sidebar
        panels         = $profile.panels
        layout         = @{
            sections = @(
                @{ key = "summary"; title = "Ozet"; fields = $profileSummaryFields },
                @{ key = "resolution"; title = "Cozum"; fields = @("resolutionSummary") }
            )
        }
    }
    Invoke-DgPut -Collection "op_profiles" -Id $profileId -Body $profileBody | Out-Null
    Write-Host "  Profil guncellendi ($profileId)" -ForegroundColor Green
    Write-Host "  Ozet alanlari: $($profileSummaryFields -join ', ')" -ForegroundColor Cyan
}

Write-Host "`nTamamlandi. MO metadata cache yenilemek icin:" -ForegroundColor Yellow
Write-Host "  POST $BaseUrl/api/operations/v1/metadata-cache/reload" -ForegroundColor Gray
