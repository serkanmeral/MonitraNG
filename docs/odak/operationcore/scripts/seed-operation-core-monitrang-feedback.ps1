# MonitraNG Geri Bildirim workspace seed (Production)
# Ref: docs/odak/operationcore/reference/MONITRANG_FEEDBACK_WORKSPACE.md
#
#   .\get-operationcore-token-prod.ps1
#   .\seed-operation-core-monitrang-feedback.ps1

param(
    [string]$BaseUrl = "http://192.168.20.8:5040",
    [switch]$UseGateway = $true,
    [string]$OutputFile = ""
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
if ([string]::IsNullOrEmpty($OutputFile)) {
    $OutputFile = Join-Path $scriptDir "operationcore-monitrang-feedback-seed.json"
}

$dataPath = if ($UseGateway) { "/data/api/v1/data" } else { "/api/v1/data" }
$workspaceName = "MonitraNG Geri Bildirim"
$tag = "MNG Geri Bildirim"
$ocGroupName = "MonitraNG Users"
$adminGroupName = "admins"
$stateSep = " - "

$resolveScript = Join-Path $scriptDir "resolve-odak-group-ids-prod.ps1"
if (-not (Test-Path $resolveScript)) {
    Write-Host "resolve-odak-group-ids-prod.ps1 bulunamadi" -ForegroundColor Red
    exit 1
}
Write-Host "Grup ID'leri cozuluyor (prod Mongo @groups)..." -ForegroundColor Gray
$groupMap = & $resolveScript -Names @($ocGroupName, $adminGroupName)
$ocGroupId = $groupMap.$ocGroupName
$adminGroupId = $groupMap.$adminGroupName
if ([string]::IsNullOrEmpty($ocGroupId) -or [string]::IsNullOrEmpty($adminGroupId)) {
    Write-Host "MonitraNG Users veya admins grup ID alinamadi." -ForegroundColor Red
    exit 1
}
Write-Host "  $ocGroupName -> $ocGroupId" -ForegroundColor Green
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
Write-Host "MonitraNG Geri Bildirim Seed (PROD)" -ForegroundColor Cyan
Write-Host "Gateway: $BaseUrl" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

# --- 1. States ---
Write-Host "[1] op_states..." -ForegroundColor Yellow
$stateNewId = Find-OrCreate -Collection "op_states" -Filter "name:eq:$tag$stateSep Yeni" -Label "Yeni" -Body @{
    name = "$tag$stateSep Yeni"; category = "open"; isInitial = $true; isStart = $true; color = "info"; sortOrder = 10
}
$stateReviewId = Find-OrCreate -Collection "op_states" -Filter "name:eq:$tag$stateSep Inceleniyor" -Label "Inceleniyor" -Body @{
    name = "$tag$stateSep Inceleniyor"; category = "in_progress"; color = "primary"; sortOrder = 20
}
$stateWaitingId = Find-OrCreate -Collection "op_states" -Filter "name:eq:$tag$stateSep Bilgi bekleniyor" -Label "Bilgi bekleniyor" -Body @{
    name = "$tag$stateSep Bilgi bekleniyor"; category = "on_hold"; color = "secondary"; sortOrder = 30
}
$statePlannedId = Find-OrCreate -Collection "op_states" -Filter "name:eq:$tag$stateSep Planlandi" -Label "Planlandi" -Body @{
    name = "$tag$stateSep Planlandi"; category = "in_progress"; color = "warning"; sortOrder = 40
}
$stateDoneId = Find-OrCreate -Collection "op_states" -Filter "name:eq:$tag$stateSep Tamamlandi" -Label "Tamamlandi" -Body @{
    name = "$tag$stateSep Tamamlandi"; category = "closed"; allowReopen = $true; color = "success"; sortOrder = 50
}
$stateRejectedId = Find-OrCreate -Collection "op_states" -Filter "name:eq:$tag$stateSep Reddedildi" -Label "Reddedildi" -Body @{
    name = "$tag$stateSep Reddedildi"; category = "closed"; isClosed = $true; isTerminal = $true; color = "error"; sortOrder = 60
}

# --- 2. Priorities (global) ---
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

# --- 3. Fields ---
Write-Host "[3] op_fields..." -ForegroundColor Yellow
$fieldAppModuleId = Find-OrCreate -Collection "op_fields" -Filter "key:eq:appModule" -Label "appModule" -Body @{
    key = "appModule"; label = "Modul / menu"; fieldType = "text"; scope = "pool"; category = "classification"
}
$fieldPageUrlId = Find-OrCreate -Collection "op_fields" -Filter "key:eq:pageUrl" -Label "pageUrl" -Body @{
    key = "pageUrl"; label = "Sayfa adresi"; fieldType = "text"; scope = "pool"; category = "technical"
}
$fieldEnvironmentId = Find-OrCreate -Collection "op_fields" -Filter "key:eq:environment" -Label "environment" -Body @{
    key = "environment"; label = "Ortam"; fieldType = "text"; scope = "pool"; category = "technical"
}
$fieldStepsId = Find-OrCreate -Collection "op_fields" -Filter "key:eq:stepsToReproduce" -Label "stepsToReproduce" -Body @{
    key = "stepsToReproduce"; label = "Yeniden uretme adimlari"; fieldType = "text"; scope = "pool"; category = "technical"
}
$fieldExpectedId = Find-OrCreate -Collection "op_fields" -Filter "key:eq:expectedBehavior" -Label "expectedBehavior" -Body @{
    key = "expectedBehavior"; label = "Beklenen davranis"; fieldType = "text"; scope = "pool"; category = "technical"
}
$fieldActualId = Find-OrCreate -Collection "op_fields" -Filter "key:eq:actualBehavior" -Label "actualBehavior" -Body @{
    key = "actualBehavior"; label = "Gerceklesen davranis"; fieldType = "text"; scope = "pool"; category = "technical"
}
$fieldResolutionId = Find-OrCreate -Collection "op_fields" -Filter "key:eq:resolutionSummary" -Label "resolutionSummary" -Body @{
    key = "resolutionSummary"; label = "Cozum / karar ozeti"; fieldType = "text"; scope = "pool"; category = "resolution"
}
$fieldScreenshotId = Find-OrCreate -Collection "op_fields" -Filter "key:eq:screenshot" -Label "screenshot" -Body @{
    key = "screenshot"; label = "Ekran goruntusu"; fieldType = "file"; scope = "pool"; category = "technical"
}

# --- 4. Types ---
Write-Host "[4] op_work_item_types..." -ForegroundColor Yellow
$typeBugId = Find-OrCreate -Collection "op_work_item_types" -Filter "name:eq:$tag - Uygulama hatasi" -Label "Bug type" -Body @{
    name = "$tag - Uygulama hatasi"; category = "incident"; color = "error"; icon = "BugIcon"; sortOrder = 10
}
$typeSuggestionId = Find-OrCreate -Collection "op_work_item_types" -Filter "name:eq:$tag - Oneri" -Label "Suggestion type" -Body @{
    name = "$tag - Oneri"; category = "service_request"; color = "info"; icon = "BulbIcon"; sortOrder = 20
}

# --- 5. Workspace ---
Write-Host "[5] op_workspaces..." -ForegroundColor Yellow
$workspaceId = Find-OrCreate -Collection "op_workspaces" -Filter "name:eq:$workspaceName" -Label "Workspace" -Body @{
    name                  = $workspaceName
    workspaceType         = "service_desk"
    description           = "MonitraNG hata ve oneri kayitlari - yalnizca MonitraNG Users"
    workItemKeyPrefix     = "MNG"
    workItemKeyFormat     = "{prefix}-{seq:D4}"
    workItemSequenceStart = 1
    viewGroups            = @($ocGroupId)
    editGroups            = @($ocGroupId)
    adminGroups           = @($adminGroupId)
    enabledTypeIds        = @($typeBugId, $typeSuggestionId)
    enabledPriorityIds    = @($prioP1Id, $prioP2Id, $prioP3Id, $prioP4Id)
    enabledFieldIds       = @(
        $fieldAppModuleId, $fieldPageUrlId, $fieldEnvironmentId, $fieldStepsId,
        $fieldExpectedId, $fieldActualId, $fieldResolutionId, $fieldScreenshotId
    )
    enabledStateIds       = @(
        $stateNewId, $stateReviewId, $stateWaitingId, $statePlannedId, $stateDoneId, $stateRejectedId
    )
}

# Update groups if workspace existed without them
Write-Host "  PUT workspace groups..." -ForegroundColor Gray
Invoke-DgPut -Collection "op_workspaces" -Id $workspaceId -Body @{
    name                  = $workspaceName
    workspaceType         = "service_desk"
    description           = "MonitraNG hata ve oneri kayitlari - yalnizca MonitraNG Users"
    workItemKeyPrefix     = "MNG"
    workItemKeyFormat     = "{prefix}-{seq:D4}"
    workItemSequenceStart = 1
    viewGroups            = @($ocGroupId)
    editGroups            = @($ocGroupId)
    adminGroups           = @($adminGroupId)
    enabledTypeIds        = @($typeBugId, $typeSuggestionId)
    enabledPriorityIds    = @($prioP1Id, $prioP2Id, $prioP3Id, $prioP4Id)
    enabledFieldIds       = @(
        $fieldAppModuleId, $fieldPageUrlId, $fieldEnvironmentId, $fieldStepsId,
        $fieldExpectedId, $fieldActualId, $fieldResolutionId, $fieldScreenshotId
    )
    enabledStateIds       = @(
        $stateNewId, $stateReviewId, $stateWaitingId, $statePlannedId, $stateDoneId, $stateRejectedId
    )
} | Out-Null

# --- 6. State flow ---
Write-Host "[6] op_state_flows..." -ForegroundColor Yellow
$flowName = "$tag - Akis"
$transitions = @(
    @{ transitionKey = "triage"; fromStateId = $stateNewId; toStateId = $stateReviewId; label = "Incelemeye al"; order = 0 },
    @{ transitionKey = "need_info"; fromStateId = $stateReviewId; toStateId = $stateWaitingId; label = "Bilgi iste"; order = 1 },
    @{ transitionKey = "info_provided"; fromStateId = $stateWaitingId; toStateId = $stateReviewId; label = "Bilgi verildi"; order = 2 },
    @{ transitionKey = "plan"; fromStateId = $stateReviewId; toStateId = $statePlannedId; label = "Planla"; order = 3 },
    @{
        transitionKey  = "complete_from_review"
        fromStateId    = $stateReviewId
        toStateId      = $stateDoneId
        label          = "Tamamla"
        order          = 4
        requiredFields = @("resolutionSummary")
    },
    @{
        transitionKey  = "complete_from_planned"
        fromStateId    = $statePlannedId
        toStateId      = $stateDoneId
        label          = "Tamamla"
        order          = 5
        requiredFields = @("resolutionSummary")
    },
    @{ transitionKey = "reject"; fromStateId = $stateReviewId; toStateId = $stateRejectedId; label = "Reddet"; order = 6 },
    @{ transitionKey = "reopen"; fromStateId = $stateDoneId; toStateId = $stateReviewId; label = "Yeniden ac"; order = 7 }
)
$flowBody = @{
    name           = $flowName
    workspaceId    = $workspaceId
    initialStateId = $stateNewId
    isDefault      = $true
    isActive       = $true
    transitions    = $transitions
}
$flowId = Find-OrCreate -Collection "op_state_flows" -Filter "name:eq:$flowName" -Label "State flow" -Body $flowBody
Write-Host "  PUT state flow (requiredFields on complete)..." -ForegroundColor Gray
Invoke-DgPut -Collection "op_state_flows" -Id $flowId -Body $flowBody | Out-Null

foreach ($pair in @(
        @{ Id = $typeBugId; Name = "$tag - Uygulama hatasi"; Category = "incident" },
        @{ Id = $typeSuggestionId; Name = "$tag - Oneri"; Category = "service_request" }
    )) {
    Invoke-DgPut -Collection "op_work_item_types" -Id $pair.Id -Body @{
        name               = $pair.Name
        category           = $pair.Category
        defaultStateFlowId = $flowId
    } | Out-Null
}

# --- 6.5 Tags (op_tags — labels alani op_labels degil op_tags kullanir) ---
Write-Host "[6.5] op_tags..." -ForegroundColor Yellow
$tagDefs = @(
    @{ Name = "MonitraNG"; Color = "success"; Description = "MonitraNG platformu" },
    @{ Name = "UI / Arayuz"; Color = "info"; Description = "Arayuz ve kullanici deneyimi" },
    @{ Name = "API / Veri"; Color = "primary"; Description = "API, dataset ve veri katmani" },
    @{ Name = "Yetki"; Color = "warning"; Description = "Oturum, rol ve yetki" },
    @{ Name = "Performans"; Color = "secondary"; Description = "Yavaslik ve kaynak kullanimi" },
    @{ Name = "Prod"; Color = "error"; Description = "Canli ortam" }
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
    defaultTypeId      = $typeBugId
    defaultStateFlowId = $flowId
    defaultStateId     = $stateNewId
    defaultPriorityId  = $prioP3Id
    isDefault          = $true
    layout             = @{
        sections = @(
            @{
                key    = "main"
                title  = "Kayit bilgileri"
                fields = @("title", "description", "typeId", "priorityId", "assignee", "labels", "appModule", "pageUrl", "environment")
            },
            @{
                key    = "detail"
                title  = "Hata / oneri detayi"
                fields = @("stepsToReproduce", "expectedBehavior", "actualBehavior", "screenshot")
            },
            @{
                key    = "resolution"
                title  = "Cozum"
                fields = @("resolutionSummary")
            }
        )
    }
    fieldBehaviors     = @{
        title              = @{ visible = $true; required = $true }
        description        = @{ visible = $true; required = $true }
        typeId             = @{ visible = $true; required = $true }
        priorityId         = @{ visible = $true }
        assignee           = @{ visible = $true }
        labels             = @{ visible = $true }
        appModule          = @{ visible = $true }
        pageUrl            = @{ visible = $true }
        environment        = @{ visible = $true }
        stepsToReproduce   = @{ visible = $true }
        expectedBehavior   = @{ visible = $true }
        actualBehavior     = @{ visible = $true }
        screenshot         = @{ visible = $true }
        # Olusturmada istege bagli; profil Detaylar + Tamamla dialog'unda gorunur.
        resolutionSummary  = @{ visible = $true }
    }
    defaultValues      = @{
        priorityId = $prioP3Id
    }
}
$formId = Find-OrCreate -Collection "op_forms" -Filter "name:eq:$formName" -Label "Form" -Body $formBody
Write-Host "  PUT form layout + defaults..." -ForegroundColor Gray
Invoke-DgPut -Collection "op_forms" -Id $formId -Body $formBody | Out-Null

# --- 8. Boards ---
Write-Host "[8] op_boards..." -ForegroundColor Yellow
$boardSubmitName = "Geri bildirim gonder"
$boardSubmitId = Find-OrCreate -Collection "op_boards" -Filter "name:eq:$boardSubmitName" -Label "Submit board" -Body @{
    name               = $boardSubmitName
    workspaceId        = $workspaceId
    viewType           = "list"
    defaultStateFlowId = $flowId
    defaultFormId      = $formId
    viewGroups         = @($ocGroupId)
    editGroups         = @($ocGroupId)
    visibleFields      = @("key", "title", "typeId", "priorityId", "assignee", "stateId")
    config             = @{
        columns = @(
            @{ stateId = $stateNewId; title = "Yeni"; queryKey = "wi_board_column" },
            @{ stateId = $stateReviewId; title = "Inceleniyor"; queryKey = "wi_board_column" }
        )
    }
}

$boardQueueName = "Inceleme kuyrugu"
$boardQueueId = Find-OrCreate -Collection "op_boards" -Filter "name:eq:$boardQueueName" -Label "Queue board" -Body @{
    name               = $boardQueueName
    workspaceId        = $workspaceId
    viewType           = "list"
    defaultStateFlowId = $flowId
    defaultFormId      = $formId
    viewGroups         = @($ocGroupId)
    editGroups         = @($ocGroupId)
    visibleFields      = @("key", "title", "typeId", "priorityId", "assignee", "stateId", "appModule")
    config             = @{
        columns = @(
            @{ stateId = $stateNewId; title = "Yeni"; queryKey = "wi_board_column" },
            @{ stateId = $stateReviewId; title = "Inceleniyor"; queryKey = "wi_board_column" },
            @{ stateId = $stateWaitingId; title = "Bilgi bekleniyor"; queryKey = "wi_board_column" },
            @{ stateId = $statePlannedId; title = "Planlandi"; queryKey = "wi_board_column" },
            @{ stateId = $stateDoneId; title = "Tamamlandi"; queryKey = "wi_board_column" },
            @{ stateId = $stateRejectedId; title = "Reddedildi"; queryKey = "wi_board_column" }
        )
    }
}

foreach ($boardPatch in @(
        @{
            Id = $boardSubmitId; Name = $boardSubmitName
            VisibleFields = @("key", "title", "typeId", "priorityId", "assignee", "stateId")
            Columns = @(
                @{ stateId = $stateNewId; title = "Yeni"; queryKey = "wi_board_column" },
                @{ stateId = $stateReviewId; title = "Inceleniyor"; queryKey = "wi_board_column" }
            )
        },
        @{
            Id = $boardQueueId; Name = $boardQueueName
            VisibleFields = @("key", "title", "typeId", "priorityId", "assignee", "labels", "stateId", "appModule")
            Columns = @(
                @{ stateId = $stateNewId; title = "Yeni"; queryKey = "wi_board_column" },
                @{ stateId = $stateReviewId; title = "Inceleniyor"; queryKey = "wi_board_column" },
                @{ stateId = $stateWaitingId; title = "Bilgi bekleniyor"; queryKey = "wi_board_column" },
                @{ stateId = $statePlannedId; title = "Planlandi"; queryKey = "wi_board_column" },
                @{ stateId = $stateDoneId; title = "Tamamlandi"; queryKey = "wi_board_column" },
                @{ stateId = $stateRejectedId; title = "Reddedildi"; queryKey = "wi_board_column" }
            )
        }
    )) {
    Invoke-DgPut -Collection "op_boards" -Id $boardPatch.Id -Body @{
        name               = $boardPatch.Name
        workspaceId        = $workspaceId
        viewType           = "list"
        defaultStateFlowId = $flowId
        defaultFormId      = $formId
        viewGroups         = @($ocGroupId)
        editGroups         = @($ocGroupId)
        visibleFields      = $boardPatch.VisibleFields
        config             = @{ columns = $boardPatch.Columns }
    } | Out-Null
}

# --- 9. Rules ---
Write-Host "[9] op_rules..." -ForegroundColor Yellow
$ruleComplete = "$tag - Cozum ozeti zorunlu (complete)"
$ruleCompleteBody = @{
    name          = $ruleComplete
    workspaceId   = $workspaceId
    ruleType      = "validation"
    trigger       = "WorkItemTransition"
    transitionKey = "complete_from_review"
    applyMode     = "pre"
    conditions    = @{
        op    = "and"
        items = @(@{ field = "resolutionSummary"; cmp = "empty" })
    }
    errorMessage  = "Tamamlamadan once cozum ozeti girilmelidir."
    isActive      = $true
    priority      = 100
}
$ruleCompleteId = Find-OrCreate -Collection "op_rules" -Filter "name:eq:$ruleComplete" -Label "Validation complete" -Body $ruleCompleteBody
Invoke-DgPut -Collection "op_rules" -Id $ruleCompleteId -Body $ruleCompleteBody | Out-Null

$rulePlanned = "$tag - Cozum ozeti zorunlu (planned)"
$rulePlannedBody = @{
    name          = $rulePlanned
    workspaceId   = $workspaceId
    ruleType      = "validation"
    trigger       = "WorkItemTransition"
    transitionKey = "complete_from_planned"
    applyMode     = "pre"
    conditions    = @{
        op    = "and"
        items = @(@{ field = "resolutionSummary"; cmp = "empty" })
    }
    errorMessage  = "Tamamlamadan once cozum ozeti girilmelidir."
    isActive      = $true
    priority      = 101
}
$rulePlannedId = Find-OrCreate -Collection "op_rules" -Filter "name:eq:$rulePlanned" -Label "Validation planned" -Body $rulePlannedBody
Invoke-DgPut -Collection "op_rules" -Id $rulePlannedId -Body $rulePlannedBody | Out-Null

# --- 10. Profile ---
Write-Host "[10] op_profiles..." -ForegroundColor Yellow
$profileName = "$tag - Kayit profili"
$profileBody = @{
    name           = $profileName
    workspaceId    = $workspaceId
    defaultTypeId  = $typeBugId
    isDefault      = $true
    fieldBehaviors = @{
        title              = @{ visible = $true; required = $true }
        description        = @{ visible = $true }
        typeId             = @{ visible = $true; readonly = $true }
        priorityId         = @{ visible = $true }
        assignee           = @{ visible = $true }
        labels             = @{ visible = $true }
        appModule          = @{ visible = $true }
        pageUrl            = @{ visible = $true }
        environment        = @{ visible = $true }
        stepsToReproduce   = @{ visible = $true }
        expectedBehavior   = @{ visible = $true }
        actualBehavior     = @{ visible = $true }
        screenshot         = @{ visible = $true }
        resolutionSummary  = @{ visible = $true }
    }
    actions        = @(
        @{ transitionKey = "triage"; order = 0; label = "Incelemeye al" },
        @{ transitionKey = "need_info"; order = 1; label = "Bilgi iste" },
        @{ transitionKey = "info_provided"; order = 2; label = "Bilgi verildi" },
        @{ transitionKey = "plan"; order = 3; label = "Planla" },
        @{ transitionKey = "complete_from_review"; order = 4; label = "Tamamla" },
        @{ transitionKey = "reject"; order = 5; label = "Reddet" },
        @{ transitionKey = "reopen"; order = 6; label = "Yeniden ac" }
    )
    header         = @{ showBreadcrumb = $true; showKey = $true }
    sidebar        = @{ showSla = $false; showWatchers = $true }
    panels         = @{ timeline = @{ enabled = $true }; comments = @{ enabled = $true } }
    layout         = @{
        sections = @(
            @{ key = "summary"; title = "Ozet"; fields = @("title", "description", "typeId", "priorityId", "assignee", "labels", "key", "appModule", "pageUrl", "environment") },
            @{ key = "bug"; title = "Hata detayi"; fields = @("stepsToReproduce", "expectedBehavior", "actualBehavior", "screenshot") },
            @{ key = "resolution"; title = "Cozum"; fields = @("resolutionSummary") }
        )
    }
}
$profileId = Find-OrCreate -Collection "op_profiles" -Filter "name:eq:$profileName" -Label "Profile" -Body $profileBody
Invoke-DgPut -Collection "op_profiles" -Id $profileId -Body $profileBody | Out-Null

# --- 11. Dashboard ---
Write-Host "[11] op_dashboards..." -ForegroundColor Yellow
$dashboardName = "$tag - Ozet pano"
$wsQuery = @{ workspaceId = $workspaceId }
$dashboardLayout = @{
    type = "rows"
    rows = @(
        @{ cols = @(
            @{ widgetId = "count_new"; span = 12; spanMd = 6; spanLg = 3 },
            @{ widgetId = "count_review"; span = 12; spanMd = 6; spanLg = 3 },
            @{ widgetId = "count_waiting"; span = 12; spanMd = 6; spanLg = 3 },
            @{ widgetId = "count_planned"; span = 12; spanMd = 6; spanLg = 3 }
        ) },
        @{ cols = @(
            @{ widgetId = "chart_type_new"; span = 12; spanMd = 6 },
            @{ widgetId = "chart_priority_review"; span = 12; spanMd = 6 }
        ) },
        @{ cols = @(
            @{ widgetId = "list_new"; span = 12; spanMd = 6 },
            @{ widgetId = "list_my_assigned"; span = 12; spanMd = 6 }
        ) }
    )
}
$dashboardWidgets = @(
    @{
        key        = "count_new"
        type       = "summaryCard"
        title      = "Yeni geri bildirim"
        icon       = "mdi-inbox-arrow-down"
        accentColor = "info"
        dataset    = "op_work_items"
        queryKey   = "wi_by_workspace_and_state"
        parameters = ($wsQuery + @{ stateId = $stateNewId })
        take       = 500
    },
    @{
        key        = "count_review"
        type       = "summaryCard"
        title      = "Inceleniyor"
        icon       = "mdi-magnify-scan"
        accentColor = "primary"
        dataset    = "op_work_items"
        queryKey   = "wi_by_workspace_and_state"
        parameters = ($wsQuery + @{ stateId = $stateReviewId })
        take       = 500
    },
    @{
        key        = "count_waiting"
        type       = "summaryCard"
        title      = "Bilgi bekleniyor"
        icon       = "mdi-help-circle-outline"
        accentColor = "warning"
        dataset    = "op_work_items"
        queryKey   = "wi_by_workspace_and_state"
        parameters = ($wsQuery + @{ stateId = $stateWaitingId })
        take       = 500
    },
    @{
        key        = "count_planned"
        type       = "summaryCard"
        title      = "Planlandi"
        icon       = "mdi-calendar-check"
        accentColor = "secondary"
        dataset    = "op_work_items"
        queryKey   = "wi_by_workspace_and_state"
        parameters = ($wsQuery + @{ stateId = $statePlannedId })
        take       = 500
    },
    @{
        key        = "chart_type_new"
        type       = "chart"
        title      = "Yeni kayitlar — tip dagilimi"
        chartType  = "donut"
        groupBy    = "typeId"
        dataset    = "op_work_items"
        queryKey   = "wi_by_workspace_and_state"
        parameters = ($wsQuery + @{ stateId = $stateNewId })
        take       = 500
    },
    @{
        key        = "chart_priority_review"
        type       = "chart"
        title      = "Inceleme kuyrugu — oncelik"
        chartType  = "bar"
        groupBy    = "priorityId"
        dataset    = "op_work_items"
        queryKey   = "wi_by_workspace_and_state"
        parameters = ($wsQuery + @{ stateId = $stateReviewId })
        take       = 500
    },
    @{
        key        = "list_new"
        type       = "list"
        title      = "Son yeni geri bildirimler"
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
    description = "MonitraNG Geri Bildirim workspace ozet panosu — durum kartlari, tip/oncelik grafikleri, listeler"
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
    boards        = @{ submit = $boardSubmitId; queue = $boardQueueId }
    dashboardId   = $dashboardId
    formId        = $formId
    gatewayUrl    = $BaseUrl
    seededAt      = (Get-Date).ToString("o")
    viewGroups    = @($ocGroupName)
    viewGroupIds  = @($ocGroupId)
    tags          = $tagIds
    states        = @{
        new      = $stateNewId
        review   = $stateReviewId
        waiting  = $stateWaitingId
        planned  = $statePlannedId
        done     = $stateDoneId
        rejected = $stateRejectedId
    }
    types         = @{ bug = $typeBugId; suggestion = $typeSuggestionId }
    priorities    = @{ p3 = $prioP3Id }
}
$summary | ConvertTo-Json -Depth 8 | Set-Content -Path $OutputFile -Encoding UTF8

Write-Host "`nTamamlandi. Ozet: $OutputFile" -ForegroundColor Cyan
Write-Host "UI: Operasyon Merkezi -> '$workspaceName'" -ForegroundColor Cyan
Write-Host "Boardlar: '$boardSubmitName', '$boardQueueName'" -ForegroundColor Cyan
Write-Host "Pano: '$dashboardName' (workspace hub -> Pano)" -ForegroundColor Cyan
