# MO → Notifier uctan uca: work item gecisi + template mail
#
# Kullanim:
#   .\docs\odak\operationcore\scripts\get-operationcore-token.ps1
#   .\docs\odak\notifications\scripts\seed-op-mail-notification-policy.ps1
#   .\docs\odak\notifications\scripts\smoke-mail-transition.ps1
#
# Not: Gonderim Notifier SMTP uzerinden; inbox kontrolu manuel.

param(
    [string]$GatewayUrl = "http://192.168.20.20:5040",
    [string]$MoBaseUrl = "",
    [string]$WorkspaceId = "",
    [string]$BoardId = "",
    [string]$TypeId = "",
    [string]$AssigneePersonId = "",
    [string]$AssigneeEmail = "serkan.meral@outlook.com",
    [string]$AssigneeConfigFile = "",
    [string]$NotifierPreviewUrl = "http://192.168.20.20:5040/notifier/api/v1/notifications/preview-template"
)

$ErrorActionPreference = "Stop"
$ocScripts = Join-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) "operationcore/scripts"
$demoSeedFile = Join-Path $ocScripts "operationcore-demo-seed.json"
if ([string]::IsNullOrEmpty($AssigneeConfigFile)) {
    $AssigneeConfigFile = Join-Path (Split-Path $PSScriptRoot -Parent) "datasets/odak_mail_test_assignee.json"
}
$tokenFile = "$env:TEMP\operationcore_dg_token.txt"

function Resolve-AssigneeFromKeeper {
    param(
        [string]$Gateway,
        [hashtable]$AuthHeaders,
        [string]$Email,
        [string]$FallbackPersonId
    )
    if (-not [string]::IsNullOrWhiteSpace($FallbackPersonId)) {
        return @{ PersonId = $FallbackPersonId; Email = $Email; Source = "config" }
    }
    if ([string]::IsNullOrWhiteSpace($Email)) { return $null }

    $page = 1
    do {
        $uri = "$Gateway/keeper/api/User?page=$page&pageSize=100&searchTerm=$([Uri]::EscapeDataString(($Email -split '@')[0]))"
        $resp = Invoke-RestMethod -Uri $uri -Headers $AuthHeaders -Method GET
        $users = @($resp.users)
        if ($users.Count -eq 0) { break }
        $match = $users | Where-Object {
            $_.email -and ($_.email.Trim().ToLowerInvariant() -eq $Email.Trim().ToLowerInvariant())
        } | Select-Object -First 1
        if ($match) {
            return @{
                PersonId = $match.userId
                Email    = $match.email
                Username = $match.username
                Source   = "keeper-search"
            }
        }
        $page++
    } while ($users.Count -ge 100)

    return $null
}

if ([string]::IsNullOrEmpty($MoBaseUrl)) {
    $MoBaseUrl = "$GatewayUrl/operations"
}

if (-not (Test-Path $tokenFile)) {
    throw "Token yok. Once: .\docs\odak\operationcore\scripts\get-operationcore-token.ps1"
}
$token = (Get-Content $tokenFile -Raw).Trim()
if ([string]::IsNullOrEmpty($token)) { throw "Token dosyasi bos." }

if ([string]::IsNullOrEmpty($WorkspaceId) -and (Test-Path $demoSeedFile)) {
    $demo = Get-Content $demoSeedFile -Raw | ConvertFrom-Json
    $WorkspaceId = $demo.workspaceId
    $BoardId = $demo.boardId
    $TypeId = $demo.typeId
}

$headers = @{
    Authorization  = "Bearer $token"
    "Content-Type" = "application/json"
}

$moParams = @{
    Headers     = $headers
    ErrorAction = "Stop"
}

Write-Host "=== Mail transition smoke ===" -ForegroundColor Cyan
Write-Host "MO: $MoBaseUrl" -ForegroundColor Gray

# 1) MO health
try {
    $health = Invoke-RestMethod -Uri "$MoBaseUrl/api/v1/health/live" -Method GET @moParams
    Write-Host "[1] MO health: OK" -ForegroundColor Green
}
catch {
    throw "MO health basarisiz: $($_.Exception.Message)"
}

# 2) Assignee — varsayilan: serkan.meral@outlook.com (LDAP'ta email'i olan test kullanicisi)
$fallbackPersonId = ""
if ([string]::IsNullOrEmpty($AssigneePersonId) -and (Test-Path $AssigneeConfigFile)) {
    $cfg = Get-Content $AssigneeConfigFile -Raw | ConvertFrom-Json
    if ($cfg.personId) { $fallbackPersonId = $cfg.personId.Trim() }
    if ([string]::IsNullOrWhiteSpace($AssigneeEmail) -and $cfg.email) { $AssigneeEmail = $cfg.email.Trim() }
}

if ([string]::IsNullOrEmpty($AssigneePersonId)) {
    $resolved = Resolve-AssigneeFromKeeper -Gateway $GatewayUrl -AuthHeaders $headers -Email $AssigneeEmail -FallbackPersonId $fallbackPersonId
    if ($resolved) {
        $AssigneePersonId = $resolved.PersonId
        $who = if ($resolved.Username) { $resolved.Username } else { "config" }
        Write-Host "[2] Assignee: $who id=$AssigneePersonId email=$($resolved.Email) ($($resolved.Source))" -ForegroundColor Green
    }
    else {
        Write-Host "[2] UYARI: $AssigneeEmail icin Keeper kullanici bulunamadi; assignee bos kalabilir" -ForegroundColor Yellow
    }
}
else {
    Write-Host "[2] Assignee (param): $AssigneePersonId" -ForegroundColor Green
}

# 3) Work item olustur
$title = "Mail smoke $(Get-Date -Format 'yyyy-MM-dd HHmmss')"
$createBody = @{
    workspaceId = $WorkspaceId
    typeId      = $TypeId
    title       = $title
    boardId     = $BoardId
}
if (-not [string]::IsNullOrEmpty($AssigneePersonId)) {
    $createBody.assignee = $AssigneePersonId
}

$created = Invoke-RestMethod -Uri "$MoBaseUrl/api/v1/work-items" -Method POST -Body ($createBody | ConvertTo-Json -Compress) @moParams
$wiId = $created.workItem.id
if (-not $wiId) { $wiId = $created.workItem.dataId }
$wiKey = $created.workItem.key
Write-Host "[3] Work item: $wiKey ($wiId)" -ForegroundColor Green

# 4) Gecis oncesi preview (Notifier context ornegi)
$previewBody = @{
    templateKey = "work-item-transitioned"
    context     = @{
        actor      = @{ displayName = "Smoke Test" }
        workItem   = @{ key = $wiKey; title = $title }
        transition = @{ key = "start_progress"; fromState = "Open"; toState = "In Progress" }
        domain     = @{ name = "odak"; displayName = "Odak"; logoUrl = $null }
        event      = @{ type = "WorkItemTransitioned"; timestamp = (Get-Date).ToUniversalTime().ToString("o") }
    }
} | ConvertTo-Json -Depth 10 -Compress

try {
    $preview = Invoke-RestMethod -Uri $NotifierPreviewUrl -Method POST -Headers $headers -Body $previewBody
    $subj = if ($preview.subject) { $preview.subject } else { $preview.Subject }
    Write-Host "[4] Notifier preview OK — subject: $subj" -ForegroundColor Green
}
catch {
    Write-Host "[4] Notifier preview atlandi: $($_.Exception.Message)" -ForegroundColor Yellow
}

# 5) Gecis
$profile = Invoke-RestMethod -Uri "$MoBaseUrl/api/v1/runtime/work-items/$wiId/profile" @moParams
if ($profile.actions.Count -lt 1) {
    throw "Gecis aksiyonu yok (actions=0)"
}

$tk = $profile.actions[0].transitionKey
Write-Host "[5] Transition: $tk ..." -ForegroundColor Cyan
Invoke-RestMethod -Uri "$MoBaseUrl/api/v1/work-items/$wiId/transitions/$tk" -Method POST -Body "{}" @moParams | Out-Null
Write-Host "[5] Transition OK — mail dispatch best-effort (policy + assignee email gerekli)" -ForegroundColor Green

Write-Host ""
Write-Host "Smoke tamamlandi." -ForegroundColor Green
Write-Host "  workItemKey=$wiKey transition=$tk" -ForegroundColor Gray
Write-Host "  Inbox kontrolu: $AssigneeEmail" -ForegroundColor Gray
Write-Host "  Seq/log: Application=MngOperations.Api veya MngNotifier.Api" -ForegroundColor Gray
