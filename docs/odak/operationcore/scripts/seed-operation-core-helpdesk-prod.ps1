# IT Destek workspace seed (Production)
# Ref: docs/odak/operationcore/reference/IT_HELP_DESK_WORKSPACE.md
#
#   .\get-operationcore-token-prod.ps1
#   .\seed-operation-core-helpdesk-prod.ps1

param(
    [string]$BaseUrl = "http://192.168.20.8:5040",
    [switch]$UseGateway = $true,
    [string]$OutputFile = ""
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
if ([string]::IsNullOrEmpty($OutputFile)) {
    $OutputFile = Join-Path $scriptDir "operationcore-helpdesk-prod-seed.json"
}

$dataPath = if ($UseGateway) { "/data/api/v1/data" } else { "/api/v1/data" }
$workspaceName = "IT Destek"
$tag = "IT Destek"
$usersGroupName = "users"
$itGroupName = "MonitraNG Users"
$adminGroupName = "admins"
$defaultAssigneeUsername = "serkan.meral"
$defaultAssigneePersonId = "6a2262026723c2bd54eec3c9"
$stateSep = " - "

$resolveScript = Join-Path $scriptDir "resolve-odak-group-ids-prod.ps1"
if (-not (Test-Path $resolveScript)) {
    Write-Host "resolve-odak-group-ids-prod.ps1 bulunamadi" -ForegroundColor Red
    exit 1
}
Write-Host "Grup ID'leri cozuluyor (prod Mongo @groups)..." -ForegroundColor Gray
$groupMap = & $resolveScript -Names @($usersGroupName, $itGroupName, $adminGroupName)
$usersGroupId = $groupMap.$usersGroupName
$itGroupId = $groupMap.$itGroupName
$adminGroupId = $groupMap.$adminGroupName
if ([string]::IsNullOrEmpty($usersGroupId) -or [string]::IsNullOrEmpty($itGroupId) -or [string]::IsNullOrEmpty($adminGroupId)) {
    Write-Host "users, MonitraNG Users veya admins grup ID alinamadi." -ForegroundColor Red
    exit 1
}
Write-Host "  $usersGroupName -> $usersGroupId" -ForegroundColor Green
Write-Host "  $itGroupName -> $itGroupId" -ForegroundColor Green
Write-Host "  $adminGroupName -> $adminGroupId" -ForegroundColor Green

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

function Invoke-DgPost {
    param([string]$Collection, [object]$Body)
    $uri = "$BaseUrl$dataPath/$Collection"
    $json = if ($Body -is [string]) { $Body } else { $Body | ConvertTo-Json -Depth 25 -Compress }
    return Invoke-RestMethod -Uri $uri -Method POST -Body $json @irmParams
}

function Invoke-DgGet {
    param([string]$Collection, [string]$Filter = "", [int]$Limit = 100)
    $uri = "$BaseUrl$dataPath/$Collection`?limit=$Limit"
    if (-not [string]::IsNullOrEmpty($Filter)) { $uri += "&filter=" + [Uri]::EscapeDataString($Filter) }
    return Invoke-RestMethod -Uri $uri -Method GET @irmParams
}

function Invoke-DgPut {
    param([string]$Collection, [string]$Id, [object]$Body)
    $uri = "$BaseUrl$dataPath/$Collection/$Id"
    $json = $Body | ConvertTo-Json -Depth 25 -Compress
    return Invoke-RestMethod -Uri $uri -Method PUT -Body $json @irmParams
}

function Get-DataId {
    param($Response)
    if (-not $Response) { return $null }
    $d = $Response.data
    if (-not $d) { $d = $Response.Data }
    if (-not $d) { $d = $Response }
    $id = $d.__dataId
    if (-not $id) { $id = $d.dataId }
    if (-not $id) { $id = $d.DataId }
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
    param(
        [string]$Collection,
        [string]$Filter,
        [object]$Body,
        [string]$Label
    )
    $existing = @(Get-Items (Invoke-DgGet -Collection $Collection -Filter $Filter -Limit 5))
    if ($existing.Count -gt 0) {
        $id = $existing[0].__dataId
        if (-not $id) { $id = $existing[0].dataId }
        Write-Host "  SKIP: $Label (mevcut $id)" -ForegroundColor Yellow
        return $id
    }
    try {
        $created = Invoke-DgPost -Collection $Collection -Body $Body
        $id = Get-DataId $created
        Write-Host "  OK: $Label -> $id" -ForegroundColor Green
        return $id
    }
    catch {
        $msg = $_.Exception.Message
        if ($_.ErrorDetails.Message) { $msg = "$msg $($_.ErrorDetails.Message)" }
        if ($msg -match "duplicate|unique|Bad Request") {
            $retry = @(Get-Items (Invoke-DgGet -Collection $Collection -Filter $Filter -Limit 5))
            if ($retry.Count -gt 0) {
                $id = $retry[0].__dataId
                if (-not $id) { $id = $retry[0].dataId }
                Write-Host "  SKIP: $Label (duplicate -> $id)" -ForegroundColor Yellow
                return $id
            }
        }
        throw
    }
}

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "IT Destek Seed (PROD)" -ForegroundColor Cyan
Write-Host "Gateway: $BaseUrl" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

# --- 1. States ---
Write-Host "[1] op_states..." -ForegroundColor Yellow
$stateNewId = Find-OrCreate -Collection "op_states" -Filter "name:eq:$tag$stateSep Yeni" -Label "Yeni" -Body @{
    name = "$tag$stateSep Yeni"; category = "open"; isInitial = $true; isStart = $true; color = "info"; sortOrder = 10
}
$stateAssignedId = Find-OrCreate -Collection "op_states" -Filter "name:eq:$tag$stateSep Atandi" -Label "Atandi" -Body @{
    name = "$tag$stateSep Atandi"; category = "in_progress"; color = "primary"; sortOrder = 20
}
$stateProgressId = Find-OrCreate -Collection "op_states" -Filter "name:eq:$tag$stateSep Islemde" -Label "Islemde" -Body @{
    name = "$tag$stateSep Islemde"; category = "in_progress"; color = "warning"; sortOrder = 30
}
$stateWaitingId = Find-OrCreate -Collection "op_states" -Filter "name:eq:$tag$stateSep Musteri bekleniyor" -Label "Musteri bekleniyor" -Body @{
    name = "$tag$stateSep Musteri bekleniyor"; category = "on_hold"; color = "secondary"; sortOrder = 40
}
$stateResolvedId = Find-OrCreate -Collection "op_states" -Filter "name:eq:$tag$stateSep Cozuldu" -Label "Cozuldu" -Body @{
    name = "$tag$stateSep Cozuldu"; category = "closed"; allowReopen = $true; color = "success"; sortOrder = 50
}
$stateClosedId = Find-OrCreate -Collection "op_states" -Filter "name:eq:$tag$stateSep Kapali" -Label "Kapali" -Body @{
    name = "$tag$stateSep Kapali"; category = "closed"; isClosed = $true; isTerminal = $true; allowReopen = $true; color = "secondary"; sortOrder = 60
}

# --- 2. Priorities (global, MNG seed ile paylasilir) ---
Write-Host "[2] op_priorities..." -ForegroundColor Yellow
$prioP1Id = Find-OrCreate -Collection "op_priorities" -Filter "name:eq:P1 - Kritik" -Label "P1" -Body @{
    name = "P1 - Kritik"; level = "1"; sortOrder = 10; color = "error"
}
$prioP2Id = Find-OrCreate -Collection "op_priorities" -Filter "name:eq:P2 - Yuksek" -Label "P2" -Body @{
    name = "P2 - Yuksek"; level = "2"; sortOrder = 20; color = "warning"
}
$prioP3Id = Find-OrCreate -Collection "op_priorities" -Filter "name:eq:P3 - Orta" -Label "P3" -Body @{
    name = "P3 - Orta"; level = "3"; sortOrder = 30; color = "info"
}
$prioP4Id = Find-OrCreate -Collection "op_priorities" -Filter "name:eq:P4 - Dusuk" -Label "P4" -Body @{
    name = "P4 - Dusuk"; level = "4"; sortOrder = 40; color = "secondary"
}

# --- 3. Field pool ---
Write-Host "[3] op_fields..." -ForegroundColor Yellow
$fieldAffectedUserId = Find-OrCreate -Collection "op_fields" -Filter "key:eq:affectedUser" -Label "affectedUser" -Body @{
    key = "affectedUser"; label = "Etkilenen kullanici"; fieldType = "persons"; scope = "pool"; category = "assignment"; cardinality = "single"
}
$fieldAffectedGroupsId = Find-OrCreate -Collection "op_fields" -Filter "key:eq:affectedGroups" -Label "affectedGroups" -Body @{
    key = "affectedGroups"; label = "Etkilenen gruplar"; fieldType = "personGroups"; scope = "pool"; category = "assignment"; cardinality = "multi"
}
$fieldAffectedAssetId = Find-OrCreate -Collection "op_fields" -Filter "key:eq:affectedAsset" -Label "affectedAsset" -Body @{
    key = "affectedAsset"; label = "Etkilenen varlik"; fieldType = "text"; scope = "pool"; category = "technical"
}
$fieldRequestCategoryId = Find-OrCreate -Collection "op_fields" -Filter "key:eq:requestCategory" -Label "requestCategory" -Body @{
    key = "requestCategory"; label = "Talep kategorisi"; fieldType = "text"; scope = "pool"; category = "classification"
}
$fieldResolutionId = Find-OrCreate -Collection "op_fields" -Filter "key:eq:resolutionSummary" -Label "resolutionSummary" -Body @{
    key = "resolutionSummary"; label = "Cozum ozeti"; fieldType = "text"; scope = "pool"; category = "resolution"
}

# --- 4. Work item types (global) ---
Write-Host "[4] op_work_item_types..." -ForegroundColor Yellow
$typeIncidentId = Find-OrCreate -Collection "op_work_item_types" -Filter "name:eq:Olay (Incident)" -Label "Incident type" -Body @{
    name = "Olay (Incident)"; category = "incident"; color = "error"; icon = "AlertCircleIcon"; sortOrder = 10
}
$typeServiceId = Find-OrCreate -Collection "op_work_item_types" -Filter "name:eq:Hizmet talebi" -Label "Service request type" -Body @{
    name = "Hizmet talebi"; category = "service_request"; color = "info"; icon = "TicketIcon"; sortOrder = 20
}
$typeProblemId = Find-OrCreate -Collection "op_work_item_types" -Filter "name:eq:Problem kaydi" -Label "Problem type" -Body @{
    name = "Problem kaydi"; category = "problem"; color = "warning"; icon = "BugIcon"; sortOrder = 30
}
$typeAccessId = Find-OrCreate -Collection "op_work_item_types" -Filter "name:eq:Erisim talebi" -Label "Access request type" -Body @{
    name = "Erisim talebi"; category = "service_request"; color = "primary"; icon = "KeyIcon"; sortOrder = 40
}

# --- 5. Workspace ---
Write-Host "[5] op_workspaces..." -ForegroundColor Yellow
$workspaceId = Find-OrCreate -Collection "op_workspaces" -Filter "name:eq:$workspaceName" -Label "Workspace" -Body @{
    name                  = $workspaceName
    workspaceType         = "service_desk"
    description           = "Kurumsal IT help desk - tum users talep acar, MonitraNG Users triyaj yapar"
    workItemKeyPrefix     = "HD"
    workItemKeyFormat     = "{prefix}-{seq:D4}"
    workItemSequenceStart = 1
    viewGroups            = @($usersGroupId)
    editGroups            = @($usersGroupId)
    adminGroups           = @($adminGroupId)
    enabledTypeIds        = @($typeIncidentId, $typeServiceId, $typeProblemId, $typeAccessId)
    enabledPriorityIds    = @($prioP1Id, $prioP2Id, $prioP3Id, $prioP4Id)
    enabledFieldIds       = @($fieldAffectedUserId, $fieldAffectedAssetId, $fieldRequestCategoryId, $fieldResolutionId, $fieldAffectedGroupsId)
    enabledStateIds       = @(
        $stateNewId, $stateAssignedId, $stateProgressId, $stateWaitingId, $stateResolvedId, $stateClosedId
    )
}

Write-Host "  PUT workspace groups..." -ForegroundColor Gray
Invoke-DgPut -Collection "op_workspaces" -Id $workspaceId -Body @{
    name                  = $workspaceName
    workspaceType         = "service_desk"
    description           = "Kurumsal IT help desk - tum users talep acar, MonitraNG Users triyaj yapar"
    workItemKeyPrefix     = "HD"
    workItemKeyFormat     = "{prefix}-{seq:D4}"
    workItemSequenceStart = 1
    viewGroups            = @($usersGroupId)
    editGroups            = @($usersGroupId)
    adminGroups           = @($adminGroupId)
    enabledTypeIds        = @($typeIncidentId, $typeServiceId, $typeProblemId, $typeAccessId)
    enabledPriorityIds    = @($prioP1Id, $prioP2Id, $prioP3Id, $prioP4Id)
    enabledFieldIds       = @($fieldAffectedUserId, $fieldAffectedAssetId, $fieldRequestCategoryId, $fieldResolutionId, $fieldAffectedGroupsId)
    enabledStateIds       = @(
        $stateNewId, $stateAssignedId, $stateProgressId, $stateWaitingId, $stateResolvedId, $stateClosedId
    )
} | Out-Null

# --- 6. State flow ---
Write-Host "[6] op_state_flows..." -ForegroundColor Yellow
$flowName = "$tag - Standard Flow"
$transitions = @(
    @{ transitionKey = "assign"; fromStateId = $stateNewId; toStateId = $stateAssignedId; label = "Ata"; order = 0 },
    @{ transitionKey = "start_work"; fromStateId = $stateAssignedId; toStateId = $stateProgressId; label = "Isleme al"; order = 1 },
    @{ transitionKey = "start_from_new"; fromStateId = $stateNewId; toStateId = $stateProgressId; label = "Dogrudan isle"; order = 2 },
    @{ transitionKey = "wait_customer"; fromStateId = $stateProgressId; toStateId = $stateWaitingId; label = "Musteriden yanit bekle"; order = 3 },
    @{ transitionKey = "resume"; fromStateId = $stateWaitingId; toStateId = $stateProgressId; label = "Devam et"; order = 4 },
    @{
        transitionKey  = "resolve"
        fromStateId    = $stateProgressId
        toStateId      = $stateResolvedId
        label          = "Coz"
        order          = 5
        requiredFields = @("resolutionSummary")
    },
    @{ transitionKey = "close"; fromStateId = $stateResolvedId; toStateId = $stateClosedId; label = "Kapat"; order = 6 },
    @{ transitionKey = "reopen"; fromStateId = $stateResolvedId; toStateId = $stateAssignedId; label = "Yeniden ac"; order = 7 },
    @{ transitionKey = "reopen_closed"; fromStateId = $stateClosedId; toStateId = $stateAssignedId; label = "Yeniden ac"; order = 8 }
)
$flowId = Find-OrCreate -Collection "op_state_flows" -Filter "name:eq:$flowName" -Label "State flow" -Body @{
    name           = $flowName
    workspaceId    = $workspaceId
    initialStateId = $stateNewId
    isDefault      = $true
    isActive       = $true
    transitions    = $transitions
}
Invoke-DgPut -Collection "op_state_flows" -Id $flowId -Body @{
    name           = $flowName
    workspaceId    = $workspaceId
    initialStateId = $stateNewId
    isDefault      = $true
    isActive       = $true
    transitions    = $transitions
} | Out-Null

foreach ($pair in @(
        @{ Id = $typeIncidentId; Name = "Olay (Incident)"; Category = "incident" },
        @{ Id = $typeServiceId; Name = "Hizmet talebi"; Category = "service_request" },
        @{ Id = $typeProblemId; Name = "Problem kaydi"; Category = "problem" },
        @{ Id = $typeAccessId; Name = "Erisim talebi"; Category = "service_request" }
    )) {
    Invoke-DgPut -Collection "op_work_item_types" -Id $pair.Id -Body @{
        name               = $pair.Name
        category           = $pair.Category
        defaultStateFlowId = $flowId
    } | Out-Null
}

# --- 6.5 Tags (op_tags — labels alani op_tags katalogunu kullanir) ---
Write-Host "[6.5] op_tags..." -ForegroundColor Yellow
$tagDefs = @(
    @{ Name = "Sifre degisimi"; Color = "warning"; Description = "Parola sifirlama veya degistirme" },
    @{ Name = "Hesap / Erisim"; Color = "primary"; Description = "Hesap acma, kapama, yetki ve erisim talepleri" },
    @{ Name = "Donanim"; Color = "secondary"; Description = "Bilgisayar, monitor, klavye vb. donanim" },
    @{ Name = "Yazilim"; Color = "info"; Description = "Uygulama kurulumu, lisans, guncelleme" },
    @{ Name = "Ag / VPN"; Color = "error"; Description = "Ag baglantisi, Wi-Fi, VPN" },
    @{ Name = "E-posta"; Color = "success"; Description = "Outlook, posta kutusu, iletim sorunlari" },
    @{ Name = "Yazici"; Color = "secondary"; Description = "Yazici, tarayici, coklu islev cihazi" },
    @{ Name = "Guvenlik"; Color = "error"; Description = "Guvenlik olayi, siber suphe, politika" },
    @{ Name = "Mobil / Telefon"; Color = "info"; Description = "Mobil cihaz, kurumsal hat, softphone" },
    @{ Name = "Genel"; Color = "secondary"; Description = "Diger veya siniflandirilamayan talepler" }
)
$tagIds = @{}
foreach ($td in $tagDefs) {
    $filter = "workspaceId:eq:$workspaceId,name:eq:$($td.Name)"
    $tid = Find-OrCreate -Collection "op_tags" -Filter $filter -Label "Tag $($td.Name)" -Body @{
        name        = $td.Name
        workspaceId = $workspaceId
        color       = $td.Color
        description = $td.Description
    }
    $tagIds[$td.Name] = $tid
}

# --- 7. Form ---
Write-Host "[7] op_forms..." -ForegroundColor Yellow
$formName = "$tag - Yeni kayit"
$formBody = @{
    name               = $formName
    workspaceId        = $workspaceId
    defaultTypeId      = $typeIncidentId
    defaultStateFlowId = $flowId
    defaultStateId     = $stateNewId
    defaultPriorityId  = $prioP3Id
    isDefault          = $true
    layout             = @{
        sections = @(
            @{
                key    = "main"
                title  = "Talep bilgileri"
                fields = @("title", "description", "typeId", "priorityId", "labels", "affectedAsset", "affectedGroups")
            }
        )
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
    defaultValues      = @{
        priorityId = $prioP3Id
    }
}
$formId = Find-OrCreate -Collection "op_forms" -Filter "name:eq:$formName" -Label "Form" -Body $formBody
Invoke-DgPut -Collection "op_forms" -Id $formId -Body $formBody | Out-Null

# --- 8. Boards ---
Write-Host "[8] op_boards..." -ForegroundColor Yellow
$boardSubmitName = "Talep olustur"
$boardSubmitId = Find-OrCreate -Collection "op_boards" -Filter "workspaceId:eq:$workspaceId,name:eq:$boardSubmitName" -Label "Submit board" -Body @{
    name               = $boardSubmitName
    workspaceId        = $workspaceId
    viewType           = "list"
    defaultStateFlowId = $flowId
    defaultFormId      = $formId
    viewGroups         = @($usersGroupId)
    editGroups         = @($usersGroupId)
    visibleFields      = @("key", "title", "typeId", "priorityId", "labels", "stateId")
    config             = @{
        columns = @(
            @{ stateId = $stateNewId; title = "Yeni"; queryKey = "wi_board_column" },
            @{ stateId = $stateAssignedId; title = "Atandi"; queryKey = "wi_board_column" }
        )
    }
}

$boardQueueName = "Agent kuyrugu"
$boardQueueId = Find-OrCreate -Collection "op_boards" -Filter "workspaceId:eq:$workspaceId,name:eq:$boardQueueName" -Label "Agent board" -Body @{
    name               = $boardQueueName
    workspaceId        = $workspaceId
    viewType           = "list"
    defaultStateFlowId = $flowId
    defaultFormId      = $formId
    viewGroups         = @($itGroupId)
    editGroups         = @($itGroupId)
    visibleFields      = @("key", "title", "typeId", "priorityId", "assignee", "labels", "stateId", "requestCategory", "affectedUser")
    config             = @{
        columns = @(
            @{ stateId = $stateNewId; title = "Yeni"; queryKey = "wi_board_column" },
            @{ stateId = $stateAssignedId; title = "Atandi"; queryKey = "wi_board_column" },
            @{ stateId = $stateProgressId; title = "Islemde"; queryKey = "wi_board_column" },
            @{ stateId = $stateWaitingId; title = "Beklemede"; queryKey = "wi_board_column" },
            @{ stateId = $stateResolvedId; title = "Cozuldu"; queryKey = "wi_board_column" },
            @{ stateId = $stateClosedId; title = "Kapali"; queryKey = "wi_board_column" }
        )
    }
}

foreach ($boardPatch in @(
        @{
            Id = $boardSubmitId; Name = $boardSubmitName
            ViewGroups = @($usersGroupId); EditGroups = @($usersGroupId)
            VisibleFields = @("key", "title", "typeId", "priorityId", "labels", "stateId")
            Columns = @(
                @{ stateId = $stateNewId; title = "Yeni"; queryKey = "wi_board_column" },
                @{ stateId = $stateAssignedId; title = "Atandi"; queryKey = "wi_board_column" }
            )
        },
        @{
            Id = $boardQueueId; Name = $boardQueueName
            ViewGroups = @($itGroupId); EditGroups = @($itGroupId)
            VisibleFields = @("key", "title", "typeId", "priorityId", "assignee", "labels", "stateId", "requestCategory", "affectedUser")
            Columns = @(
                @{ stateId = $stateNewId; title = "Yeni"; queryKey = "wi_board_column" },
                @{ stateId = $stateAssignedId; title = "Atandi"; queryKey = "wi_board_column" },
                @{ stateId = $stateProgressId; title = "Islemde"; queryKey = "wi_board_column" },
                @{ stateId = $stateWaitingId; title = "Beklemede"; queryKey = "wi_board_column" },
                @{ stateId = $stateResolvedId; title = "Cozuldu"; queryKey = "wi_board_column" },
                @{ stateId = $stateClosedId; title = "Kapali"; queryKey = "wi_board_column" }
            )
        }
    )) {
    Invoke-DgPut -Collection "op_boards" -Id $boardPatch.Id -Body @{
        name               = $boardPatch.Name
        workspaceId        = $workspaceId
        viewType           = "list"
        defaultStateFlowId = $flowId
        defaultFormId      = $formId
        viewGroups         = $boardPatch.ViewGroups
        editGroups         = $boardPatch.EditGroups
        visibleFields      = $boardPatch.VisibleFields
        config             = @{ columns = $boardPatch.Columns }
    } | Out-Null
}

# --- 9. Rules ---
Write-Host "[9] op_rules..." -ForegroundColor Yellow
$ruleName = "$tag - Cozum ozeti zorunlu"
$validationRuleBody = @{
    name          = $ruleName
    workspaceId   = $workspaceId
    ruleType      = "validation"
    trigger       = "WorkItemTransition"
    transitionKey = "resolve"
    applyMode     = "pre"
    conditions    = @{
        op    = "and"
        items = @(@{ field = "resolutionSummary"; cmp = "empty" })
    }
    errorMessage  = "Cozumden once cozum ozeti (resolutionSummary) girilmelidir."
    isActive      = $true
    priority      = 100
}
$validationRuleId = Find-OrCreate -Collection "op_rules" -Filter "name:eq:$ruleName" -Label "Validation rule" -Body $validationRuleBody
Invoke-DgPut -Collection "op_rules" -Id $validationRuleId -Body $validationRuleBody | Out-Null
Write-Host "  Validation rule -> $validationRuleId" -ForegroundColor Green

$assignRuleName = "$tag - Varsayilan atama ($defaultAssigneeUsername)"
$assignRuleBody = @{
    name        = $assignRuleName
    description = "Yeni IT talebi acildiginda assignee bos ise $defaultAssigneeUsername kullanicisina atanir"
    workspaceId = $workspaceId
    ruleType    = "default"
    trigger     = "WorkItemCreated"
    conditions  = @{
        op    = "and"
        items = @(@{ field = "assignee"; cmp = "empty" })
    }
    actions     = @(@{ type = "setAssignee"; assignee = $defaultAssigneePersonId })
    isActive    = $true
    priority    = 50
}
$assignRuleId = Find-OrCreate -Collection "op_rules" -Filter "name:eq:$assignRuleName" -Label "Default assignee rule" -Body $assignRuleBody
Invoke-DgPut -Collection "op_rules" -Id $assignRuleId -Body $assignRuleBody | Out-Null
Write-Host "  Default assignee rule -> $assignRuleId ($defaultAssigneePersonId)" -ForegroundColor Green

# --- 10. Profile ---
Write-Host "[10] op_profiles..." -ForegroundColor Yellow
$profileName = "$tag - Kayit profili"
$profileBody = @{
    name           = $profileName
    workspaceId    = $workspaceId
    defaultTypeId  = $typeIncidentId
    isDefault      = $true
    fieldBehaviors = @{
        title              = @{ visible = $true; required = $true }
        description        = @{ visible = $true }
        typeId             = @{ visible = $true; readonly = $true }
        priorityId         = @{ visible = $true }
        assignee           = @{ visible = $true }
        labels             = @{ visible = $true }
        impact             = @{ visible = $false }
        urgency            = @{ visible = $false }
        requestCategory    = @{ visible = $false }
        affectedUser       = @{ visible = $false }
        affectedAsset      = @{ visible = $true }
        affectedGroups     = @{ visible = $true }
        resolutionSummary  = @{ visible = $true }
    }
    actions        = @(
        @{ transitionKey = "assign"; order = 0; label = "Ata" },
        @{ transitionKey = "start_work"; order = 1; label = "Isleme al" },
        @{ transitionKey = "start_from_new"; order = 2; label = "Dogrudan isle" },
        @{ transitionKey = "wait_customer"; order = 3; label = "Musteri bekle" },
        @{ transitionKey = "resume"; order = 4; label = "Devam et" },
        @{ transitionKey = "resolve"; order = 5; label = "Coz" },
        @{ transitionKey = "close"; order = 6; label = "Kapat" },
        @{ transitionKey = "reopen"; order = 7; label = "Yeniden ac" }
    )
    header         = @{ showBreadcrumb = $true; showKey = $true }
    sidebar        = @{ showSla = $true; showWatchers = $true }
    panels         = @{ timeline = @{ enabled = $true }; comments = @{ enabled = $true } }
    layout         = @{
        sections = @(
            @{ key = "summary"; title = "Ozet"; fields = @("title", "description", "typeId", "priorityId", "assignee", "labels", "key", "affectedAsset", "affectedGroups") },
            @{ key = "resolution"; title = "Cozum"; fields = @("resolutionSummary") }
        )
    }
}
$profileId = Find-OrCreate -Collection "op_profiles" -Filter "name:eq:$profileName" -Label "Profile" -Body $profileBody
Invoke-DgPut -Collection "op_profiles" -Id $profileId -Body $profileBody | Out-Null

# --- 11. SLA ---
Write-Host "[11] op_sla_policies..." -ForegroundColor Yellow
$slaName = "$tag - Olay P1 SLA"
Find-OrCreate -Collection "op_sla_policies" -Filter "name:eq:$slaName" -Label "SLA policy" -Body @{
    name                  = $slaName
    workspaceId           = $workspaceId
    typeId                = $typeIncidentId
    priorityId            = $prioP1Id
    responseTargetMinutes = 15
    resolveTargetMinutes  = 240
    isActive              = $true
    priority              = 10
} | Out-Null

# --- 12. Dashboard ---
Write-Host "[12] op_dashboards..." -ForegroundColor Yellow
$dashboardName = "$tag - Ozet pano"
$wsQuery = @{ workspaceId = $workspaceId }
$dashboardLayout = @{
    type = "rows"
    rows = @(
        @{ cols = @(
            @{ widgetId = "count_new"; span = 12; spanMd = 6; spanLg = 3 },
            @{ widgetId = "count_assigned"; span = 12; spanMd = 6; spanLg = 3 },
            @{ widgetId = "count_progress"; span = 12; spanMd = 6; spanLg = 3 },
            @{ widgetId = "count_waiting"; span = 12; spanMd = 6; spanLg = 3 }
        ) },
        @{ cols = @(
            @{ widgetId = "sla_response_breach"; span = 12; spanMd = 6 },
            @{ widgetId = "chart_type_new"; span = 12; spanMd = 6 }
        ) },
        @{ cols = @(
            @{ widgetId = "chart_priority_progress"; span = 12; spanMd = 6 },
            @{ widgetId = "list_new"; span = 12; spanMd = 6 }
        ) },
        @{ cols = @(
            @{ widgetId = "list_my_assigned"; span = 12 }
        ) }
    )
}
$dashboardWidgets = @(
    @{
        key         = "count_new"
        type        = "summaryCard"
        title       = "Yeni talepler"
        icon        = "mdi-inbox-arrow-down"
        accentColor = "info"
        dataset     = "op_work_items"
        queryKey    = "wi_by_workspace_and_state"
        parameters  = ($wsQuery + @{ stateId = $stateNewId })
        take        = 500
    },
    @{
        key         = "count_assigned"
        type        = "summaryCard"
        title       = "Atandi"
        icon        = "mdi-account-check"
        accentColor = "primary"
        dataset     = "op_work_items"
        queryKey    = "wi_by_workspace_and_state"
        parameters  = ($wsQuery + @{ stateId = $stateAssignedId })
        take        = 500
    },
    @{
        key         = "count_progress"
        type        = "summaryCard"
        title       = "Islemde"
        icon        = "mdi-progress-wrench"
        accentColor = "warning"
        dataset     = "op_work_items"
        queryKey    = "wi_by_workspace_and_state"
        parameters  = ($wsQuery + @{ stateId = $stateProgressId })
        take        = 500
    },
    @{
        key         = "count_waiting"
        type        = "summaryCard"
        title       = "Musteri bekleniyor"
        icon        = "mdi-account-clock"
        accentColor = "secondary"
        dataset     = "op_work_items"
        queryKey    = "wi_by_workspace_and_state"
        parameters  = ($wsQuery + @{ stateId = $stateWaitingId })
        take        = 500
    },
    @{
        key         = "sla_response_breach"
        type        = "summaryCard"
        title       = "SLA yanit ihlali"
        icon        = "mdi-clock-alert"
        accentColor = "error"
        dataset     = "op_work_items"
        queryKey    = "wi_sla_response_breach"
        parameters  = ($wsQuery + @{ asOf = "{{asOf}}" })
        take        = 200
    },
    @{
        key        = "chart_type_new"
        type       = "chart"
        title      = "Yeni talepler — tip dagilimi"
        chartType  = "donut"
        groupBy    = "typeId"
        dataset    = "op_work_items"
        queryKey   = "wi_by_workspace_and_state"
        parameters = ($wsQuery + @{ stateId = $stateNewId })
        take       = 500
    },
    @{
        key        = "chart_priority_progress"
        type       = "chart"
        title      = "Islemde — oncelik dagilimi"
        chartType  = "bar"
        groupBy    = "priorityId"
        dataset    = "op_work_items"
        queryKey   = "wi_by_workspace_and_state"
        parameters = ($wsQuery + @{ stateId = $stateProgressId })
        take       = 500
    },
    @{
        key        = "list_new"
        type       = "list"
        title      = "Son yeni talepler"
        dataset    = "op_work_items"
        queryKey   = "wi_by_workspace_and_state"
        parameters = ($wsQuery + @{ stateId = $stateNewId })
        take       = 8
    },
    @{
        key        = "list_my_assigned"
        type       = "list"
        title      = "Bana atanan acik kayitlar"
        dataset    = "op_work_items"
        queryKey   = "wi_assigned_open"
        parameters = @{ assignee = "{{currentUser}}" }
        take       = 8
    }
)
$dashboardBody = @{
    name        = $dashboardName
    description = "IT Destek ozet panosu — durum kartlari, SLA ihlali, tip/oncelik grafikleri, listeler"
    workspaceId = $workspaceId
    scope       = "workspace"
    isDefault   = $true
    isActive    = $true
    layout      = $dashboardLayout
    widgets     = $dashboardWidgets
}
$dashboardId = Find-OrCreate -Collection "op_dashboards" -Filter "name:eq:$dashboardName" -Label "Dashboard" -Body $dashboardBody
Invoke-DgPut -Collection "op_dashboards" -Id $dashboardId -Body $dashboardBody | Out-Null
Write-Host "  Dashboard: $dashboardName -> $dashboardId" -ForegroundColor Green

$summary = @{
    workspaceName = $workspaceName
    workspaceId   = $workspaceId
    flowId        = $flowId
    boards        = @{ submit = $boardSubmitId; agent = $boardQueueId }
    dashboardId   = $dashboardId
    formId        = $formId
    gatewayUrl    = $BaseUrl
    seededAt      = (Get-Date).ToString("o")
    viewGroups    = @($usersGroupName)
    viewGroupIds  = @($usersGroupId)
    itGroups      = @($itGroupName)
    itGroupIds    = @($itGroupId)
    states        = @{
        new      = $stateNewId
        assigned = $stateAssignedId
        progress = $stateProgressId
        waiting  = $stateWaitingId
        resolved = $stateResolvedId
        closed   = $stateClosedId
    }
    types         = @{
        incident = $typeIncidentId
        service  = $typeServiceId
        problem  = $typeProblemId
        access   = $typeAccessId
    }
    priorities    = @{ p1 = $prioP1Id; p3 = $prioP3Id }
    tags          = $tagIds
    rules         = @{
        defaultAssignee = $assignRuleId
        assigneePersonId = $defaultAssigneePersonId
        assigneeUsername = $defaultAssigneeUsername
    }
}
$summary | ConvertTo-Json -Depth 8 | Set-Content -Path $OutputFile -Encoding UTF8

Write-Host "`nTamamlandi. Ozet: $OutputFile" -ForegroundColor Cyan
Write-Host "UI: Operasyon Merkezi -> '$workspaceName' (workspace agaci)" -ForegroundColor Cyan
Write-Host "Boardlar: '$boardSubmitName' (users), '$boardQueueName' (MonitraNG Users)" -ForegroundColor Cyan
Write-Host "Pano: '$dashboardName' (workspace hub -> Pano)" -ForegroundColor Cyan
