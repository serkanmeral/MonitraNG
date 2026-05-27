# Operation Core -  IT Help Desk referans seed (DG)
#
# Ref: docs/odak/operationcore/reference/IT_HELP_DESK_REFERENCE.md
#
# Usage (repo kokunden):
#   .\docs\odak\operationcore\scripts\get-operationcore-token.ps1
#   .\docs\odak\operationcore\scripts\seed-operation-core-helpdesk-reference.ps1

param(
    [string]$BaseUrl = "http://192.168.20.20:5040",
    [switch]$UseGateway = $true,
    [string]$OutputFile = ""
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
if ([string]::IsNullOrEmpty($OutputFile)) {
    $OutputFile = Join-Path $scriptDir "operationcore-helpdesk-seed.json"
}

$dataPath = if ($UseGateway) { "/data/api/v1/data" } else { "/api/v1/data" }
$tag = "IT Destek"

$loadTokenScript = Join-Path $scriptDir "load-operationcore-token.ps1"
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
if ($BaseUrl.StartsWith("https://") -and (Get-Command Invoke-RestMethod -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Parameters | Where-Object { $_.Name -eq "SkipCertificateCheck" })) {
    $irmParams.SkipCertificateCheck = $true
}

function Invoke-DgPost {
    param([string]$Collection, [object]$Body)
    $uri = "$BaseUrl$dataPath/$Collection"
    $json = if ($Body -is [string]) { $Body } else { $Body | ConvertTo-Json -Depth 20 -Compress }
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
    $json = $Body | ConvertTo-Json -Depth 20 -Compress
    $params = @{ Uri = $uri; Method = "PUT"; Body = $json } + $irmParams
    return Invoke-RestMethod @params
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
Write-Host "IT Help Desk Reference Seed" -ForegroundColor Cyan
Write-Host "Gateway: $BaseUrl" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

# --- 1. States ---
Write-Host "[1] op_states..." -ForegroundColor Yellow
$stateSep = " - "
$stateNewId = Find-OrCreate -Collection "op_states" -Filter "name:eq:$tag$stateSep Yeni" -Label "Yeni" -Body @{
    name = "$tag$stateSep Yeni"; category = "open"; isInitial = $true; isStart = $true; color = "info"; sortOrder = 10
}
$stateAssignedId = Find-OrCreate -Collection "op_states" -Filter "name:eq:$tag -  Atandi" -Label "Atandi" -Body @{
    name = "$tag -  Atandi"; category = "in_progress"; color = "primary"; sortOrder = 20
}
$stateProgressId = Find-OrCreate -Collection "op_states" -Filter "name:eq:$tag -  Islemde" -Label "Islemde" -Body @{
    name = "$tag -  Islemde"; category = "in_progress"; color = "warning"; sortOrder = 30
}
$stateWaitingId = Find-OrCreate -Collection "op_states" -Filter "name:eq:$tag -  Musteri bekleniyor" -Label "Musteri bekleniyor" -Body @{
    name = "$tag -  Musteri bekleniyor"; category = "on_hold"; color = "secondary"; sortOrder = 40
}
$stateResolvedId = Find-OrCreate -Collection "op_states" -Filter "name:eq:$tag -  Cozuldu" -Label "Cozuldu" -Body @{
    name = "$tag -  Cozuldu"; category = "closed"; allowReopen = $true; color = "success"; sortOrder = 50
}
$stateClosedId = Find-OrCreate -Collection "op_states" -Filter "name:eq:$tag -  Kapali" -Label "Kapali" -Body @{
    name = "$tag -  Kapali"; category = "closed"; isClosed = $true; isTerminal = $true; allowReopen = $true; color = "secondary"; sortOrder = 60
}

# --- 2. Priorities ---
Write-Host "[2] op_priorities..." -ForegroundColor Yellow
$prioP1Id = Find-OrCreate -Collection "op_priorities" -Filter "name:eq:P1 -  Kritik" -Label "P1" -Body @{
    name = "P1 -  Kritik"; level = "1"; sortOrder = 10; color = "error"
}
$prioP2Id = Find-OrCreate -Collection "op_priorities" -Filter "name:eq:P2 -  Yuksek" -Label "P2" -Body @{
    name = "P2 -  Yuksek"; level = "2"; sortOrder = 20; color = "warning"
}
$prioP3Id = Find-OrCreate -Collection "op_priorities" -Filter "name:eq:P3 -  Orta" -Label "P3" -Body @{
    name = "P3 -  Orta"; level = "3"; sortOrder = 30; color = "info"
}
$prioP4Id = Find-OrCreate -Collection "op_priorities" -Filter "name:eq:P4 -  Dusuk" -Label "P4" -Body @{
    name = "P4 -  Dusuk"; level = "4"; sortOrder = 40; color = "secondary"
}

# --- 3. Field pool (scope: pool; degerler op_work_items.extraFields icinde) ---
# impact / urgency -> core (op_work_items ust seviye); op_fields kaydi yok
Write-Host "[3] op_fields..." -ForegroundColor Yellow
$fieldAffectedUserId = Find-OrCreate -Collection "op_fields" -Filter "key:eq:affectedUser" -Label "affectedUser" -Body @{
    key = "affectedUser"; label = "Etkilenen kullanici"; fieldType = "persons"; scope = "pool"; category = "assignment"; cardinality = "single"
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
$workspaceName = $tag
$workspaceId = Find-OrCreate -Collection "op_workspaces" -Filter "name:eq:$workspaceName" -Label "Workspace" -Body @{
    name                  = $workspaceName
    workspaceType         = "service_desk"
    description           = "Kurumsal IT help desk -  olay, hizmet talebi ve problem yonetimi"
    workItemKeyPrefix     = "HD"
    workItemKeyFormat     = "{prefix}-{seq:D4}"
    workItemSequenceStart = 1
    enabledTypeIds        = @($typeIncidentId, $typeServiceId, $typeProblemId, $typeAccessId)
    enabledFieldIds       = @($fieldAffectedUserId, $fieldAffectedAssetId, $fieldRequestCategoryId, $fieldResolutionId)
}

# --- 6. State flow ---
Write-Host "[6] op_state_flows..." -ForegroundColor Yellow
$flowName = "$tag -  Standard Flow"
$transitions = @(
    @{ transitionKey = "assign"; fromStateId = $stateNewId; toStateId = $stateAssignedId; label = "Ata"; order = 0 },
    @{ transitionKey = "start_work"; fromStateId = $stateAssignedId; toStateId = $stateProgressId; label = "Isleme al"; order = 1 },
    @{ transitionKey = "start_from_new"; fromStateId = $stateNewId; toStateId = $stateProgressId; label = "Dogrudan isle"; order = 2 },
    @{ transitionKey = "wait_customer"; fromStateId = $stateProgressId; toStateId = $stateWaitingId; label = "Musteriden yanit bekle"; order = 3 },
    @{ transitionKey = "resume"; fromStateId = $stateWaitingId; toStateId = $stateProgressId; label = "Devam et"; order = 4 },
    @{ transitionKey = "resolve"; fromStateId = $stateProgressId; toStateId = $stateResolvedId; label = "Coz"; order = 5 },
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

# Link types to default flow
foreach ($pair in @(
        @{ Id = $typeIncidentId; Label = "incident flow" },
        @{ Id = $typeServiceId; Label = "service flow" },
        @{ Id = $typeProblemId; Label = "problem flow" },
        @{ Id = $typeAccessId; Label = "access flow" }
    )) {
    Write-Host "  PUT type defaultStateFlowId ($($pair.Label))..." -ForegroundColor Gray
    Invoke-DgPut -Collection "op_work_item_types" -Id $pair.Id -Body @{
        name = if ($pair.Id -eq $typeIncidentId) { "Olay (Incident)" }
               elseif ($pair.Id -eq $typeServiceId) { "Hizmet talebi" }
               elseif ($pair.Id -eq $typeProblemId) { "Problem kaydi" }
               else { "Erisim talebi" }
        category = if ($pair.Id -eq $typeIncidentId) { "incident" }
                     elseif ($pair.Id -eq $typeProblemId) { "problem" }
                     else { "service_request" }
        defaultStateFlowId = $flowId
    } | Out-Null
}

# --- 7. Form ---
Write-Host "[7] op_forms..." -ForegroundColor Yellow
$formName = "$tag -  Yeni kayit"
$formId = Find-OrCreate -Collection "op_forms" -Filter "name:eq:$formName" -Label "Form" -Body @{
    name               = $formName
    workspaceId        = $workspaceId
    defaultTypeId      = $typeIncidentId
    defaultStateFlowId = $flowId
    defaultStateId     = $stateNewId
    isDefault          = $true
    layout             = @{
        sections = @(
            @{
                key    = "main"
                title  = "Talep bilgileri"
                fields = @("title", "description", "typeId", "priorityId", "assignee", "impact", "urgency", "requestCategory", "affectedUser", "affectedAsset")
            }
        )
    }
    fieldBehaviors     = @{
        title           = @{ visible = $true; required = $true }
        description     = @{ visible = $true; required = $true }
        typeId          = @{ visible = $true; required = $true }
        priorityId      = @{ visible = $true; required = $true }
        assignee        = @{ visible = $true }
        impact          = @{ visible = $true }
        urgency         = @{ visible = $true }
        requestCategory = @{ visible = $true }
        affectedUser    = @{ visible = $true }
        affectedAsset   = @{ visible = $true }
    }
}

# --- 8. Board (list queue) ---
Write-Host "[8] op_boards..." -ForegroundColor Yellow
$boardName = "$tag -  Kuyruk"
$boardId = Find-OrCreate -Collection "op_boards" -Filter "name:eq:$boardName" -Label "Board" -Body @{
    name               = $boardName
    workspaceId        = $workspaceId
    viewType           = "list"
    defaultStateFlowId = $flowId
    defaultFormId      = $formId
    visibleFields      = @("key", "title", "typeId", "priorityId", "assignee", "stateId")
    config             = @{
        columns = @(
            @{ stateId = $stateNewId; title = "Yeni"; queryKey = "wi_board_column" },
            @{ stateId = $stateAssignedId; title = "Atandi"; queryKey = "wi_board_column" },
            @{ stateId = $stateProgressId; title = "Islemde"; queryKey = "wi_board_column" },
            @{ stateId = $stateWaitingId; title = "Beklemede"; queryKey = "wi_board_column" },
            @{ stateId = $stateResolvedId; title = "Cozuldu"; queryKey = "wi_board_column" }
        )
    }
}

# --- 9. Validation rule ---
Write-Host "[9] op_rules..." -ForegroundColor Yellow
$ruleName = "$tag -  Cozum ozeti zorunlu"
Find-OrCreate -Collection "op_rules" -Filter "name:eq:$ruleName" -Label "Validation rule" -Body @{
    name          = $ruleName
    workspaceId   = $workspaceId
    ruleType      = "validation"
    trigger       = "WorkItemTransition"
    transitionKey = "resolve"
    applyMode     = "pre"
    conditions    = @{ field = "resolutionSummary"; cmp = "empty" }
    errorMessage  = "Cozumden once cozum ozeti (resolutionSummary) girilmelidir."
    isActive      = $true
    priority      = 100
} | Out-Null

# --- 10. Profile ---
Write-Host "[10] op_profiles..." -ForegroundColor Yellow
$profileName = "$tag -  Kayit profili"
Find-OrCreate -Collection "op_profiles" -Filter "name:eq:$profileName" -Label "Profile" -Body @{
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
        resolutionSummary  = @{ visible = $true }
    }
    actions        = @(
        @{ transitionKey = "assign"; order = 0; label = "Ata" },
        @{ transitionKey = "start_work"; order = 1; label = "Isleme al" },
        @{ transitionKey = "wait_customer"; order = 2; label = "Musteri bekle" },
        @{ transitionKey = "resolve"; order = 3; label = "Coz" },
        @{ transitionKey = "close"; order = 4; label = "Kapat" }
    )
    header         = @{ showBreadcrumb = $true; showKey = $true }
    sidebar        = @{ showSla = $true; showWatchers = $true }
    panels         = @{ timeline = @{ enabled = $true }; comments = @{ enabled = $true } }
    layout         = @{
        sections = @(
            @{ key = "summary"; title = "Ozet"; fields = @("title", "description", "typeId", "priorityId", "assignee", "key", "impact", "urgency") },
            @{ key = "resolution"; title = "Cozum"; fields = @("resolutionSummary") }
        )
    }
} | Out-Null

# --- 11. SLA (incident demo) ---
Write-Host "[11] op_sla_policies..." -ForegroundColor Yellow
$slaName = "$tag -  Olay P1 SLA"
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

$summary = @{
    tag         = $tag
    workspaceId = $workspaceId
    flowId      = $flowId
    boardId     = $boardId
    formId      = $formId
    states      = @{
        new      = $stateNewId
        assigned = $stateAssignedId
        progress = $stateProgressId
        waiting  = $stateWaitingId
        resolved = $stateResolvedId
        closed   = $stateClosedId
    }
    types       = @{
        incident = $typeIncidentId
        service  = $typeServiceId
        problem  = $typeProblemId
        access   = $typeAccessId
    }
    priorities  = @{
        p1 = $prioP1Id
        p2 = $prioP2Id
        p3 = $prioP3Id
        p4 = $prioP4Id
    }
}
$summary | ConvertTo-Json -Depth 6 | Set-Content -Path $OutputFile -Encoding UTF8

Write-Host "`nTamamlandi. Ozet: $OutputFile" -ForegroundColor Cyan
Write-Host "UI: Operasyon Merkezi -> workspace '$workspaceName' -> board '$boardName'" -ForegroundColor Cyan
