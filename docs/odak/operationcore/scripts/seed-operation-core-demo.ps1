# Operation Core — demo metadata zinciri (DG uzerinden)
# Ref: docs/odak/operationcore/mngoperations/MVP_CHECKLIST.md
#
# Olusturulan zincir:
#   op_states -> op_workspaces -> op_state_flows -> op_work_item_types -> op_forms -> op_boards
#
# Usage (repo kokunden):
#   .\docs\odak\operationcore\scripts\get-operationcore-token.ps1
#   .\docs\odak\operationcore\scripts\seed-operation-core-demo.ps1
#   .\docs\odak\operationcore\scripts\seed-operation-core-demo.ps1 -SmokeTest
#   .\docs\odak\operationcore\scripts\seed-operation-core-demo.ps1 -SmokeTest -MoBaseUrl "http://192.168.20.20:5040/operations"

param(
    [string]$BaseUrl = "http://192.168.20.20:5040",
    [string]$MoBaseUrl = "http://localhost:5086",
    [switch]$UseGateway = $true,
    [switch]$SmokeTest = $false,
    [string]$OutputFile = ""
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
if ([string]::IsNullOrEmpty($OutputFile)) {
    $OutputFile = Join-Path $scriptDir "operationcore-demo-seed.json"
}

$dataPath = if ($UseGateway) { "/data/api/v1/data" } else { "/api/v1/data" }
$demoTag = "OC Demo"

$loadTokenScript = Join-Path $scriptDir "load-operationcore-token.ps1"
if (-not (Test-Path $loadTokenScript)) {
    Write-Host "load-operationcore-token.ps1 bulunamadi: $loadTokenScript" -ForegroundColor Red
    exit 1
}

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
    try {
        $params = @{ Uri = $uri; Method = "POST"; Body = $json } + $irmParams
        return Invoke-RestMethod @params
    }
    catch {
        Write-Host "  DG POST $Collection hata: $($_.Exception.Message)" -ForegroundColor Red
        if ($_.ErrorDetails.Message) { Write-Host "  $($_.ErrorDetails.Message)" -ForegroundColor Gray }
        throw
    }
}

function Invoke-DgGet {
    param([string]$Collection, [string]$Filter = "", [int]$Limit = 100)
    $uri = "$BaseUrl$dataPath/$Collection`?limit=$Limit"
    if (-not [string]::IsNullOrEmpty($Filter)) { $uri += "&filter=" + [Uri]::EscapeDataString($Filter) }
    try {
        $params = @{ Uri = $uri; Method = "GET" } + $irmParams
        return Invoke-RestMethod @params
    }
    catch {
        Write-Host "  DG GET $Collection hata: $($_.Exception.Message)" -ForegroundColor Red
        return $null
    }
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
        if ($msg -match "mevcut|already|zaten|duplicate|unique|Bad Request") {
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
Write-Host "Operation Core Demo Seed" -ForegroundColor Cyan
Write-Host "Gateway: $BaseUrl" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

# --- 1. States ---
Write-Host "[1] op_states..." -ForegroundColor Yellow
$stateOpenId = Find-OrCreate -Collection "op_states" -Filter "name:eq:$demoTag Open" -Label "Open state" -Body @{
    name     = "$demoTag Open"
    category = "open"
    isInitial = $true
    isStart   = $true
    color    = "#4CAF50"
}
$stateProgressId = Find-OrCreate -Collection "op_states" -Filter "name:eq:$demoTag In Progress" -Label "In Progress state" -Body @{
    name     = "$demoTag In Progress"
    category = "in_progress"
    color    = "#2196F3"
}
$stateDoneId = Find-OrCreate -Collection "op_states" -Filter "name:eq:$demoTag Done" -Label "Done state" -Body @{
    name     = "$demoTag Done"
    category = "closed"
    isClosed = $true
    color    = "#9E9E9E"
}

# --- 2. Workspace (flow sonra baglanacak) ---
Write-Host "[2] op_workspaces..." -ForegroundColor Yellow
$workspaceName = "$demoTag Workspace"
$workspaceId = Find-OrCreate -Collection "op_workspaces" -Filter "name:eq:$workspaceName" -Label "Workspace" -Body @{
    name               = $workspaceName
    workspaceType      = "team"
    description        = "Operation Core Faz 1 demo workspace"
    workItemKeyPrefix  = "OCD"
    workItemKeyFormat  = "{prefix}-{seq:D4}"
    workItemSequenceStart = 1
}

# --- 3. State flow ---
Write-Host "[3] op_state_flows..." -ForegroundColor Yellow
$flowName = "$demoTag Default Flow"
$transitions = @(
    @{
        transitionKey = "start_progress"
        fromStateId   = $stateOpenId
        toStateId     = $stateProgressId
        label         = "Baslat"
        order         = 0
    },
    @{
        transitionKey = "resolve"
        fromStateId   = $stateProgressId
        toStateId     = $stateDoneId
        label         = "Kapat"
        order         = 1
    },
    @{
        transitionKey = "reopen"
        fromStateId   = $stateDoneId
        toStateId     = $stateOpenId
        label         = "Yeniden ac"
        order         = 2
    }
)

$flowId = Find-OrCreate -Collection "op_state_flows" -Filter "name:eq:$flowName" -Label "State flow" -Body @{
    name            = $flowName
    workspaceId     = $workspaceId
    initialStateId  = $stateOpenId
    isDefault       = $true
    isActive        = $true
    transitions     = $transitions
}

# --- 4. Work item type ---
Write-Host "[4] op_work_item_types..." -ForegroundColor Yellow
$typeName = "$demoTag Task"
$typeId = Find-OrCreate -Collection "op_work_item_types" -Filter "name:eq:$typeName" -Label "Work item type" -Body @{
    name                = $typeName
    category            = "task"
    workspaceId         = $workspaceId
    defaultStateFlowId  = $flowId
}

# --- 5. Form ---
Write-Host "[5] op_forms..." -ForegroundColor Yellow
$formName = "$demoTag Create Form"
$formLayout = @{
    sections = @(
        @{
            key    = "main"
            title  = "Temel bilgiler"
            fields = @("title", "description", "typeId", "assignee", "priorityId", "boardId")
        }
    )
}
$formFieldBehaviors = @{
    title       = @{ visible = $true; required = $true }
    description = @{ visible = $true }
    typeId      = @{ visible = $true; required = $true }
    assignee    = @{ visible = $true }
    priorityId  = @{ visible = $true }
    boardId     = @{ visible = $true }
}

$formId = Find-OrCreate -Collection "op_forms" -Filter "name:eq:$formName" -Label "Form" -Body @{
    name               = $formName
    workspaceId        = $workspaceId
    defaultTypeId      = $typeId
    defaultStateFlowId = $flowId
    defaultStateId     = $stateOpenId
    isDefault          = $true
    layout             = $formLayout
    fieldBehaviors     = $formFieldBehaviors
}

# --- 6. Board ---
Write-Host "[6] op_boards..." -ForegroundColor Yellow
$boardName = "$demoTag Board"
$boardId = Find-OrCreate -Collection "op_boards" -Filter "name:eq:$boardName" -Label "Board" -Body @{
    name                = $boardName
    workspaceId         = $workspaceId
    viewType            = "list"
    defaultStateFlowId  = $flowId
    defaultFormId       = $formId
    visibleFields       = @("title", "assignee", "priorityId", "key")
    config              = @{
        columns = @(
            @{ stateId = $stateOpenId; title = "Acik"; queryKey = "wi_board_column" },
            @{ stateId = $stateProgressId; title = "Devam"; queryKey = "wi_board_column" },
            @{ stateId = $stateDoneId; title = "Tamam"; queryKey = "wi_board_column" }
        )
    }
}

# --- 7. Validation rule (resolve icin description zorunlu) ---
Write-Host "[7] op_rules..." -ForegroundColor Yellow
$ruleName = "$demoTag Resolve Requires Description"
$ruleId = Find-OrCreate -Collection "op_rules" -Filter "name:eq:$ruleName" -Label "Validation rule" -Body @{
    name          = $ruleName
    workspaceId   = $workspaceId
    ruleType      = "validation"
    trigger       = "WorkItemTransition"
    transitionKey = "resolve"
    applyMode     = "pre"
    conditions    = @{ field = "description"; cmp = "empty" }
    errorMessage  = "Kapatmadan once aciklama (description) girilmelidir."
    isActive      = $true
    priority      = 100
}

# --- 8. Profile ---
Write-Host "[8] op_profiles..." -ForegroundColor Yellow
$profileName = "$demoTag Work Item Profile"
$profileFieldBehaviors = @{
    title       = @{ visible = $true; readonly = $false; required = $true }
    description = @{ visible = $true; readonly = $false }
    assignee    = @{ visible = $true }
    priorityId  = @{ visible = $true }
    typeId      = @{ visible = $true; readonly = $true }
    boardId     = @{ visible = $true; readonly = $true }
}
$profileActions = @(
    @{ transitionKey = "start_progress"; order = 0; label = "Baslat" },
    @{ transitionKey = "resolve"; order = 1; label = "Kapat" }
)
$profileId = Find-OrCreate -Collection "op_profiles" -Filter "name:eq:$profileName" -Label "Profile" -Body @{
    name           = $profileName
    workspaceId    = $workspaceId
    defaultTypeId  = $typeId
    isDefault      = $true
    fieldBehaviors = $profileFieldBehaviors
    actions        = $profileActions
    header         = @{ showBreadcrumb = $true; showKey = $true }
    sidebar        = @{ showSla = $true; showWatchers = $true }
    panels         = @{ timeline = @{ enabled = $true }; comments = @{ enabled = $true } }
    layout         = @{
        sections = @(
            @{ key = "summary"; title = "Ozet"; fields = @("title", "description", "assignee", "priorityId", "typeId", "boardId", "key") }
        )
    }
}

# --- 9. SLA policy ---
Write-Host "[9] op_sla_policies..." -ForegroundColor Yellow
$slaName = "$demoTag Default SLA"
$slaPolicyId = Find-OrCreate -Collection "op_sla_policies" -Filter "name:eq:$slaName" -Label "SLA policy" -Body @{
    name                    = $slaName
    workspaceId             = $workspaceId
    typeId                  = $typeId
    responseTargetMinutes   = 60
    resolveTargetMinutes    = 480
    isActive                = $true
    priority                = 10
}

# --- 11. Dashboard ---
Write-Host "[11] op_dashboards..." -ForegroundColor Yellow
$dashboardName = "$demoTag Workspace Dashboard"
$dashboardLayout = @{
    type = "rows"
    rows = @(
        @{ cols = @(
            @{ widgetId = "open_count"; span = 12; spanMd = 4; spanLg = 3 },
            @{ widgetId = "in_progress_count"; span = 12; spanMd = 4; spanLg = 3 },
            @{ widgetId = "sla_response_breach"; span = 12; spanMd = 4; spanLg = 3 }
        ) },
        @{ cols = @(
            @{ widgetId = "by_priority"; span = 12; spanMd = 6 },
            @{ widgetId = "my_assigned"; span = 12; spanMd = 6 }
        ) }
    )
}
$dashboardWidgets = @(
        @{
            key       = "open_count"
            type      = "summaryCard"
            title     = "Acik isler"
            dataset   = "op_work_items"
            queryKey  = "wi_by_workspace_and_state"
            parameters = @{
                workspaceId = $workspaceId
                stateId     = $stateOpenId
            }
            take = 200
        },
        @{
            key       = "in_progress_count"
            type      = "summaryCard"
            title     = "Devam eden"
            dataset   = "op_work_items"
            queryKey  = "wi_by_workspace_and_state"
            parameters = @{
                workspaceId = $workspaceId
                stateId     = $stateProgressId
            }
            take = 200
        },
        @{
            key       = "my_assigned"
            type      = "list"
            title     = "Bana atanan acik isler"
            dataset   = "op_work_items"
            queryKey  = "wi_assigned_open"
            parameters = @{
                assignee = "{{currentUser}}"
            }
            take = 10
        },
        @{
            key       = "sla_response_breach"
            type      = "summaryCard"
            title     = "SLA yanit ihlali"
            dataset   = "op_work_items"
            queryKey  = "wi_sla_response_breach"
            parameters = @{
                workspaceId = $workspaceId
                asOf        = "{{asOf}}"
            }
            take = 100
        },
        @{
            key       = "by_priority"
            type      = "chart"
            title     = "Acik islerin oncelige gore dagilimi"
            chartType = "donut"
            groupBy   = "priorityId"
            dataset   = "op_work_items"
            queryKey  = "wi_by_workspace_and_state"
            parameters = @{
                workspaceId = $workspaceId
                stateId     = $stateOpenId
            }
        }
)

$dashboardBody = @{
    name        = $dashboardName
    description = "Operation Core Faz 1 demo landing dashboard"
    workspaceId = $workspaceId
    scope       = "workspace"
    isDefault   = $true
    isActive    = $true
    layout      = $dashboardLayout
    widgets     = $dashboardWidgets
}

$dashboardId = Find-OrCreate -Collection "op_dashboards" -Filter "name:eq:$dashboardName" -Label "Dashboard" -Body $dashboardBody

# Find-OrCreate mevcut kaydi SKIP eder; yeni layout/widget setini (chart widget dahil) PUT ile senkronla.
try {
    Invoke-DgPut -Collection "op_dashboards" -Id $dashboardId -Body $dashboardBody | Out-Null
    Write-Host "  SYNC: dashboard layout+widgets guncellendi ($dashboardId)" -ForegroundColor Green
}
catch {
    Write-Host "  WARN: dashboard PUT sync atlandi: $($_.Exception.Message)" -ForegroundColor DarkYellow
}

# --- 12. Notification policies ---
Write-Host "[12] op_notification_policies..." -ForegroundColor Yellow
$policyCreatedName = "$demoTag WorkItem Created"
$policyCreatedId = Find-OrCreate -Collection "op_notification_policies" -Filter "name:eq:$policyCreatedName" -Label "Notification policy (created)" -Body @{
    name                     = $policyCreatedName
    workspaceId              = $workspaceId
    typeId                   = $typeId
    eventType                = "WorkItemCreated"
    channels                 = @("inApp")
    recipients               = @("assignee", "watchers")
    excludeActor             = $true
    isActive                 = $true
    priority                 = 10
}
$policyTransitionName = "$demoTag WorkItem Transitioned"
$policyTransitionId = Find-OrCreate -Collection "op_notification_policies" -Filter "name:eq:$policyTransitionName" -Label "Notification policy (transitioned)" -Body @{
    name                     = $policyTransitionName
    workspaceId              = $workspaceId
    typeId                   = $typeId
    eventType                = "WorkItemTransitioned"
    channels                 = @("inApp")
    recipients               = @("assignee", "watchers")
    excludeActor             = $false
    isActive                 = $true
    priority                 = 20
}

# --- 13. Workspace guncelle (flow + enabled types) ---
Write-Host "[13] op_workspaces guncelleme..." -ForegroundColor Yellow
try {
    Invoke-DgPut -Collection "op_workspaces" -Id $workspaceId -Body @{
        defaultStateFlowId = $flowId
        enabledTypeIds     = @($typeId)
    } | Out-Null
    Write-Host "  OK: workspace metadata baglandi" -ForegroundColor Green
}
catch {
    Write-Host "  UYARI: workspace patch basarisiz: $($_.Exception.Message)" -ForegroundColor Yellow
}

$seed = @{
    seededAt      = (Get-Date).ToUniversalTime().ToString("o")
    gatewayUrl    = $BaseUrl
    workspaceId   = $workspaceId
    stateFlowId   = $flowId
    typeId        = $typeId
    formId        = $formId
    boardId       = $boardId
    ruleId        = $ruleId
    profileId     = $profileId
    slaPolicyId   = $slaPolicyId
    dashboardId   = $dashboardId
    notificationPolicies = @{
        workItemCreated    = $policyCreatedId
        workItemTransitioned = $policyTransitionId
    }
    states        = @{
        open       = $stateOpenId
        inProgress = $stateProgressId
        done       = $stateDoneId
    }
    transitions   = @{
        startProgress = "start_progress"
        resolve       = "resolve"
        reopen        = "reopen"
    }
}

$seed | ConvertTo-Json -Depth 10 | Out-File -FilePath $OutputFile -Encoding utf8
Write-Host "`nSeed ozeti: $OutputFile" -ForegroundColor Cyan

if ($SmokeTest) {
    Write-Host "`n[Smoke] MngOperations API ($MoBaseUrl)..." -ForegroundColor Yellow
    $moHeaders = @{
        "Authorization" = "Bearer $token"
        "Content-Type"  = "application/json"
    }
    $moParams = @{
        Headers     = $moHeaders
        ErrorAction = "Stop"
    }
    if ($MoBaseUrl.StartsWith("https://") -and (Get-Command Invoke-RestMethod -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Parameters | Where-Object { $_.Name -eq "SkipCertificateCheck" })) {
        $moParams.SkipCertificateCheck = $true
    }

    try {
        $formCtx = Invoke-RestMethod -Uri "$MoBaseUrl/api/v1/runtime/work-items/form?mode=create&workspaceId=$workspaceId" @moParams
        Write-Host "  OK: form create runtime (types=$($formCtx.types.Count), initialState=$($formCtx.initialStateId))" -ForegroundColor Green

        $createBody = @{
            workspaceId = $workspaceId
            typeId      = $typeId
            title       = "$demoTag smoke $(Get-Date -Format 'HHmmss')"
            boardId     = $boardId
        } | ConvertTo-Json -Compress

        $created = Invoke-RestMethod -Uri "$MoBaseUrl/api/v1/work-items" -Method POST -Body $createBody @moParams
        $wiId = $created.workItem.id
        if (-not $wiId) { $wiId = $created.workItem.dataId }
        Write-Host "  OK: work item create -> $($created.workItem.key)" -ForegroundColor Green

        $profile = Invoke-RestMethod -Uri "$MoBaseUrl/api/v1/runtime/work-items/$wiId/profile" @moParams
        $fieldCount = @($profile.fields.PSObject.Properties).Count
        Write-Host "  OK: profile (actions=$($profile.actions.Count), sla=$($null -ne $profile.sla), fields=$fieldCount, segments=$($profile.stateSegments.Count))" -ForegroundColor Green

        $segments = Invoke-RestMethod -Uri "$MoBaseUrl/api/v1/runtime/work-items/$wiId/state-segments" @moParams
        if ($segments.total -lt 1) { throw "state-segments bos (create sonrasi en az 1 segment bekleniyor)" }
        Write-Host "  OK: state-segments after create (total=$($segments.total))" -ForegroundColor Green

        $boardCtx = Invoke-RestMethod -Uri "$MoBaseUrl/api/v1/runtime/boards/$boardId" @moParams
        Write-Host "  OK: board (columns=$($boardCtx.columns.Count))" -ForegroundColor Green

        $queryBody = @{
            dataset    = "op_work_items"
            parameters = @{ workspaceId = $workspaceId; stateId = $stateOpenId; boardId = $boardId }
            skip       = 0
            take       = 20
        } | ConvertTo-Json -Depth 5 -Compress
        $query = Invoke-RestMethod -Uri "$MoBaseUrl/api/v1/runtime/queries/wi_board_column/execute" -Method POST -Body $queryBody @moParams
        Write-Host "  OK: query wi_board_column (items=$($query.items.Count))" -ForegroundColor Green

        $dashboardCtx = Invoke-RestMethod -Uri "$MoBaseUrl/api/v1/runtime/dashboards/$dashboardId" @moParams
        $executedWidgets = @($dashboardCtx.widgets | Where-Object { $null -ne $_.execution -and $_.execution.success -eq $true })
        if ($executedWidgets.Count -lt 2) { throw "dashboard widget execute bekleniyor (>=2 basarili)" }
        Write-Host "  OK: dashboard (widgets=$($dashboardCtx.widgets.Count), executed=$($executedWidgets.Count))" -ForegroundColor Green

        if ($profile.actions.Count -gt 0) {
            $tk = $profile.actions[0].transitionKey
            $trBody = "{}"
            Invoke-RestMethod -Uri "$MoBaseUrl/api/v1/work-items/$wiId/transitions/$tk" -Method POST -Body $trBody @moParams | Out-Null
            Write-Host "  OK: transition $tk" -ForegroundColor Green

            $segmentsAfter = Invoke-RestMethod -Uri "$MoBaseUrl/api/v1/runtime/work-items/$wiId/state-segments" @moParams
            $closedCount = @($segmentsAfter.items | Where-Object { $null -ne $_.leftAt }).Count
            if ($closedCount -lt 1) { throw "transition sonrasi kapali segment bekleniyor" }
            Write-Host "  OK: state-segments after transition (total=$($segmentsAfter.total), closed=$closedCount)" -ForegroundColor Green
        }

        $seed.smokeTest = @{
            workItemId = $wiId
            workItemKey = $created.workItem.key
            passedAt = (Get-Date).ToUniversalTime().ToString("o")
        }
        $seed | ConvertTo-Json -Depth 10 | Out-File -FilePath $OutputFile -Encoding utf8
    }
    catch {
        Write-Host "  Smoke test basarisiz: $($_.Exception.Message)" -ForegroundColor Red
        if ($_.ErrorDetails.Message) { Write-Host "  $($_.ErrorDetails.Message)" -ForegroundColor Gray }
        exit 1
    }
}

Write-Host "`nTamamlandi." -ForegroundColor Green
Write-Host "workspaceId=$workspaceId boardId=$boardId" -ForegroundColor Gray
