# OC Demo Workspace — bildirim / tanım denetimi + opsiyonel test WI
param(
    [string]$GatewayUrl = "http://192.168.20.20:5040",
    [switch]$CreateTestWorkItem,
    [switch]$WithTransition,
    [string]$AssigneePersonId = "6a0f8fd13d6ba5d774ee37c7"
)

$ErrorActionPreference = "Stop"
$MoBaseUrl = "$($GatewayUrl.TrimEnd('/'))/operations"
$ocScripts = Join-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) "operationcore/scripts"
$seedFile = Join-Path $ocScripts "operationcore-demo-seed.json"
$tokenFile = "$env:TEMP\operationcore_dg_token.txt"

if (-not (Test-Path $tokenFile)) {
    throw "Token yok. Once: .\docs\odak\operationcore\scripts\get-operationcore-token.ps1"
}
$token = (Get-Content $tokenFile -Raw).Trim()
$headers = @{
    Authorization  = "Bearer $token"
    "Content-Type" = "application/json"
}
$moParams = @{ Headers = $headers; ErrorAction = "Stop" }

$seed = Get-Content $seedFile -Raw | ConvertFrom-Json
$wsId = $seed.workspaceId

function Get-DgList {
    param([string]$Uri)
    $r = Invoke-RestMethod -Uri $Uri -Headers $headers
    if ($r -is [System.Array]) { return @($r | Where-Object { $null -ne $_ }) }
    if ($r.data) { return @($r.data | Where-Object { $null -ne $_ }) }
    if ($r.items) { return @($r.items | Where-Object { $null -ne $_ }) }
    return @()
}

function Get-DataId($obj) {
    if ($null -eq $obj) { return $null }
    if ($obj -is [string]) { return $obj.Trim() }
    if ($obj.PSObject.Properties["__dataId"]) { return [string]$obj.__dataId }
    if ($obj.PSObject.Properties["dataId"]) { return [string]$obj.dataId }
    return $null
}

function Format-PolicyPushToast($settings) {
    if ($null -eq $settings) { return "pushToast=(yok)" }
    if ($settings.pushToast -eq $true) { return "pushToast=EVET" }
    if ($settings.pushToast -eq $false) { return "pushToast=hayir" }
    return "pushToast=(yok)"
}

$issues = @()
$warnings = @()
$ok = @()

Write-Host ""
Write-Host "=== OC Demo Workspace denetimi ===" -ForegroundColor Cyan
Write-Host "  workspaceId: $wsId" -ForegroundColor Gray
Write-Host ""

# Workspace
$ws = Invoke-RestMethod -Uri "$GatewayUrl/data/api/v1/data/op_workspaces/$wsId" @moParams
if ($ws.isActive -eq $false) { $issues += "Workspace pasif (isActive=false)" }
else { $ok += "Workspace aktif: $($ws.name)" }

# Board / type / flow
$boards = Get-DgList "$GatewayUrl/data/api/v1/data/op_boards?filter=workspaceId:eq:$wsId&limit=10"
$boardId = $seed.boardId
$typeId = $seed.typeId
if ($boards.Count -lt 1) { $issues += "Workspace icin board yok" }
else { $ok += "Board sayisi: $($boards.Count) (seed boardId=$boardId)" }

$types = Get-DgList "$GatewayUrl/data/api/v1/data/op_work_item_types?limit=50"
$typeOk = $types | Where-Object { (Get-DataId $_) -eq $typeId -or $_.__dataId -eq $typeId }
if (-not $typeOk) { $warnings += "Seed typeId ($typeId) DG listesinde bulunamadi" }
else { $ok += "Work item tipi mevcut: $($typeOk.name)" }

$flows = Get-DgList "$GatewayUrl/data/api/v1/data/op_state_flows?filter=workspaceId:eq:$wsId&limit=10"
if ($flows.Count -lt 1) { $issues += "State flow tanimi yok" }
else { $ok += "State flow: $($flows.Count) kayit" }

# Transitions (seed state flow)
$flowId = $seed.stateFlowId
try {
    $flow = Invoke-RestMethod -Uri "$GatewayUrl/data/api/v1/data/op_state_flows/$flowId" @moParams
    $transCount = @($flow.transitions).Count
    if ($transCount -lt 1) { $issues += "Seed state flow transitions bos" }
    else { $ok += "Gecis sayisi (seed flow): $transCount (ornek: start_progress, resolve)" }
}
catch {
    $warnings += "Seed state flow okunamadi: $flowId"
}

# Notification policies
$policies = Get-DgList "$GatewayUrl/data/api/v1/data/op_notification_policies?filter=workspaceId:eq:$wsId&limit=50"
Write-Host "Bildirim politikaları ($($policies.Count)):" -ForegroundColor Yellow
$hasEmailTransition = $false
$hasInAppTransition = $false
$hasPushToast = $false
foreach ($p in $policies) {
    $id = Get-DataId $p
    $channels = @($p.channels)
    $recipients = @($p.recipients) -join ", "
    $pushLabel = Format-PolicyPushToast $p.settings
    $active = if ($p.isActive -eq $false) { "PASIF" } else { "aktif" }
    Write-Host "  [$active] $($p.name)" -ForegroundColor $(if ($p.isActive -eq $false) { "DarkGray" } else { "White" })
    Write-Host "       event=$($p.eventType) channels=$($channels -join '+') recipients=$recipients" -ForegroundColor Gray
    Write-Host "       emailTemplate=$($p.emailTemplateKey) $pushLabel transitionKey=$($p.transitionKey)" -ForegroundColor Gray

    if ($p.isActive -ne $false -and $p.eventType -eq "WorkItemTransitioned") {
        if ($channels -contains "email") {
            $hasEmailTransition = $true
            if ([string]::IsNullOrWhiteSpace($p.emailTemplateKey)) {
                $issues += "Aktif WorkItemTransitioned policy '$($p.name)' email kanalinda ama emailTemplateKey bos"
            }
        }
        if ($channels -contains "inApp") {
            $hasInAppTransition = $true
            if ($p.settings.pushToast -eq $true) { $hasPushToast = $true }
        }
    }
}

if (-not $hasEmailTransition) {
    $warnings += "Aktif WorkItemTransitioned + email policy yok (mail testi icin seed-op-mail-notification-policy.ps1)"
}
else { $ok += "Mail gecisi policy mevcut (email kanali)" }

if (-not $hasInAppTransition) {
    $warnings += "Aktif WorkItemTransitioned + inApp policy yok (toaster/inbox testi icin UI'dan ekleyin)"
}
elseif (-not $hasPushToast) {
    $warnings += "inApp policy var ama hicbirinde settings.pushToast=true yok (toaster icin checkbox isaretleyin)"
}
else { $ok += "inApp + pushToast policy mevcut" }

# Mail template
$templates = Get-DgList "$GatewayUrl/data/api/v1/data/@mail_templates?limit=100"
$wiTpl = @($templates | Where-Object {
    ($_.templateKey -eq "work-item-transitioned") -or ($_.TemplateKey -eq "work-item-transitioned")
})
if ($wiTpl.Count -lt 1) { $issues += "@mail_templates icinde work-item-transitioned yok" }
else {
    $t = $wiTpl[0]
    $tActive = $t.isActive -ne $false
    if (-not $tActive) { $issues += "work-item-transitioned sablonu pasif" }
    else { $ok += "Mail sablonu work-item-transitioned aktif" }
}

# SLA
$sla = Get-DgList "$GatewayUrl/data/api/v1/data/op_sla_policies?filter=workspaceId:eq:$wsId&limit=10"
if ($sla.Count -lt 1) { $warnings += "SLA policy yok (WI olusur ama SLA bos olabilir)" }
else { $ok += "SLA policy: $($sla.Count)" }

# MO + Hub hint
try {
    Invoke-RestMethod -Uri "$MoBaseUrl/api/v1/health/live" @moParams | Out-Null
    $ok += "MngOperations ayakta"
}
catch { $issues += "MngOperations health basarisiz" }

# Assignee / odak_admin
$cfgPath = Join-Path (Split-Path $PSScriptRoot -Parent) "datasets/odak_mail_test_assignee.json"
$cfg = Get-Content $cfgPath -Raw | ConvertFrom-Json
Write-Host ""
Write-Host "Test kullanicisi (odak_admin):" -ForegroundColor Cyan
Write-Host "  personId=$($cfg.personId) email=$($cfg.email)" -ForegroundColor Gray
$ok += "Mail test assignee: $($cfg.email) (policy recipients=assignee ise WI assignee bu olmali)"

Write-Host ""
Write-Host "--- Ozet ---" -ForegroundColor Cyan
foreach ($line in $ok) { Write-Host "  OK  $line" -ForegroundColor Green }
foreach ($line in $warnings) { Write-Host "  UYARI  $line" -ForegroundColor Yellow }
foreach ($line in $issues) { Write-Host "  SORUN  $line" -ForegroundColor Red }

if ($issues.Count -gt 0) {
    Write-Host ""
    Write-Host "Duzeltme onerileri:" -ForegroundColor Yellow
    if ($issues -match "work-item-transitioned") {
        Write-Host "  .\docs\odak\notifications\scripts\setup-notifier-datasets.ps1" -ForegroundColor Gray
    }
    if ($warnings -match "seed-op-mail") {
        Write-Host "  .\docs\odak\notifications\scripts\seed-op-mail-notification-policy.ps1" -ForegroundColor Gray
    }
    Write-Host "  UI: Workspace Tanimlari > Bildirim politikaları > inApp + Anlik toaster" -ForegroundColor Gray
}

if ($CreateTestWorkItem) {
    Write-Host ""
    Write-Host "=== Test is kaydi olusturuluyor ===" -ForegroundColor Cyan
    $title = "Bildirim testi $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
    $createBody = @{
        workspaceId = $wsId
        boardId     = $boardId
        typeId      = $typeId
        title       = $title
        description = "Hub/toast/mail testi - odak_admin assignee. Agent arka plan."
        assignee    = $AssigneePersonId
    } | ConvertTo-Json -Compress

    $created = Invoke-RestMethod -Uri "$MoBaseUrl/api/v1/work-items" -Method POST -Body $createBody @moParams
    $wiId = $created.workItem.id
    if ([string]::IsNullOrEmpty($wiId)) { $wiId = Get-DataId $created.workItem }
    $wiKey = $created.workItem.key
    Write-Host "  Olusturuldu: $wiKey ($wiId)" -ForegroundColor Green
    Write-Host "  Profil: http://192.168.20.20:3000/apps/operation-core/work-items/$wiId/profile" -ForegroundColor Gray

    if ($WithTransition) {
        $profile = Invoke-RestMethod -Uri "$MoBaseUrl/api/v1/runtime/work-items/$wiId/profile" @moParams
        if ($profile.actions.Count -lt 1) {
            Write-Host "  Gecis atlandi - profile.actions bos" -ForegroundColor Yellow
        }
        else {
            $tk = $profile.actions[0].transitionKey
            $label = $profile.actions[0].label
            Write-Host "  Gecis uygulaniyor: $tk ($label) ..." -ForegroundColor Cyan
            Invoke-RestMethod -Uri "$MoBaseUrl/api/v1/work-items/$wiId/transitions/$tk" -Method POST -Body "{}" @moParams | Out-Null
            Write-Host "  Gecis tamam (mail/inApp dispatch best-effort)" -ForegroundColor Green
            Write-Host "  Inbox: zil ikonu | Mail: $($cfg.email)" -ForegroundColor Gray
        }
    }
    else {
        Write-Host "  Gecis icin: -WithTransition" -ForegroundColor Gray
    }
}

Write-Host ""
if ($issues.Count -eq 0) {
    Write-Host "Denetim: TEMEL TANIMLAR UYGUN" -ForegroundColor Green
    exit 0
}
Write-Host "Denetim: $($issues.Count) kritik sorun, $($warnings.Count) uyari" -ForegroundColor Yellow
exit 1
