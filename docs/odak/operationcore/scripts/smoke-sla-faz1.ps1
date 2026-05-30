# Operation Core — SLA Faz 1 smoke (SLA-1 DoD)
# Ref: docs/odak/operationcore/mngoperations/SLA_FAZ1_PLAN.md
#
# Usage (repo root):
#   .\docs\odak\operationcore\scripts\smoke-sla-faz1.ps1
#   .\docs\odak\operationcore\scripts\smoke-sla-faz1.ps1 -WithTransition
#   .\docs\odak\operationcore\scripts\smoke-sla-faz1.ps1 -TestBreachQuery

param(
    [string]$GatewayBaseUrl = "http://192.168.20.20:5040",
    [string]$MoBaseUrl = "",
    [string]$WorkspaceId = "",
    [string]$BoardId = "",
    [string]$TypeId = "",
    [switch]$WithTransition,
    [switch]$TestBreachQuery
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
$dataPath = "/data/api/v1/data"

if ([string]::IsNullOrEmpty($MoBaseUrl)) {
    $MoBaseUrl = "$($GatewayBaseUrl.TrimEnd('/'))/operations"
}

function Get-OcDataId {
    param($Value)
    if ($null -eq $Value) { return $null }
    if ($Value -is [string]) { return $Value.Trim() }
    if ($Value.PSObject.Properties['__dataId']) { return [string]$Value.__dataId }
    return $null
}

function Get-DgList {
    param($Result)
    if ($null -eq $Result) { return @() }
    if ($Result -is [System.Array]) { return @($Result | Where-Object { $null -ne $_ }) }
    return @($Result)
}

function Assert-True {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) { throw $Message }
}

Write-Host ""
Write-Host "SLA Faz 1 smoke (SLA-1)" -ForegroundColor Cyan
Write-Host "  MO: $MoBaseUrl" -ForegroundColor Gray
Write-Host ""

$loadTokenScript = Join-Path $scriptDir "load-operationcore-token.ps1"
$token = & $loadTokenScript
if ([string]::IsNullOrEmpty($token)) { throw "Token alinamadi." }

$headers = @{
    Authorization  = "Bearer $token"
    "Content-Type" = "application/json"
}
$moParams = @{ Headers = $headers; ErrorAction = "Stop" }

$seedFile = Join-Path $scriptDir "operationcore-demo-seed.json"
if ([string]::IsNullOrEmpty($WorkspaceId) -and (Test-Path $seedFile)) {
    $seed = Get-Content $seedFile -Raw | ConvertFrom-Json
    if ([string]::IsNullOrEmpty($WorkspaceId)) { $WorkspaceId = $seed.workspaceId }
}

Assert-True (-not [string]::IsNullOrEmpty($WorkspaceId)) "WorkspaceId gerekli (param veya operationcore-demo-seed.json)."

Write-Host "0) Board/type cozumleme (DG)..." -ForegroundColor Yellow
$boards = Get-DgList (Invoke-RestMethod -Uri "$GatewayBaseUrl$dataPath/op_boards?filter=workspaceId:eq:$WorkspaceId&limit=10" @moParams)
Assert-True ($boards.Count -ge 1) "Workspace icin board bulunamadi."
if ([string]::IsNullOrEmpty($BoardId)) {
    $BoardId = Get-OcDataId $boards[0]
}
$types = Get-DgList (Invoke-RestMethod -Uri "$GatewayBaseUrl$dataPath/op_work_item_types?limit=50" @moParams)
Assert-True ($types.Count -ge 1) "Work item type bulunamadi."
if ([string]::IsNullOrEmpty($TypeId)) {
    $pickedType = $types | Where-Object { $_.isActive -ne $false } | Select-Object -First 1
    if (-not $pickedType) { $pickedType = $types[0] }
    $TypeId = Get-OcDataId $pickedType
}
Assert-True (-not [string]::IsNullOrEmpty($BoardId)) "boardId cozulemedi."
Assert-True (-not [string]::IsNullOrEmpty($TypeId)) "typeId cozulemedi."
Write-Host "   board=$BoardId type=$TypeId" -ForegroundColor Gray

Write-Host "1) op_sla_policies (workspace=$WorkspaceId)..." -ForegroundColor Yellow
$policies = Get-DgList (Invoke-RestMethod -Uri "$GatewayBaseUrl$dataPath/op_sla_policies?filter=workspaceId:eq:$WorkspaceId&limit=20" @moParams)
Assert-True ($policies.Count -ge 1) "En az bir SLA policy bekleniyor (workspace veya global)."
$activePolicies = @($policies | Where-Object { $_.isActive -ne $false })
Write-Host "   OK — $($policies.Count) policy ($($activePolicies.Count) aktif)" -ForegroundColor Green
foreach ($p in $activePolicies | Select-Object -First 3) {
    $policyId = Get-OcDataId $p
    Write-Host "      - $($p.name) ($policyId) resp=$($p.responseTargetMinutes)m resolve=$($p.resolveTargetMinutes)m" -ForegroundColor Gray
}

Write-Host "2) POST work-items (MO create + SLA hesabi)..." -ForegroundColor Yellow
$createBody = @{
    workspaceId = $WorkspaceId
    boardId     = $BoardId
    typeId      = $TypeId
    title       = "SLA smoke $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
    description = "SLA Faz1 smoke — otomatik"
} | ConvertTo-Json -Compress

$created = Invoke-RestMethod -Uri "$MoBaseUrl/api/v1/work-items" -Method POST -Body $createBody @moParams
$wiId = $created.workItem.id
if ([string]::IsNullOrEmpty($wiId)) { $wiId = Get-OcDataId $created.workItem.dataId }
Assert-True (-not [string]::IsNullOrEmpty($wiId)) "workItem.id bos."
$wiKey = $created.workItem.key
Write-Host "   OK — $wiKey ($wiId)" -ForegroundColor Green

Write-Host "3) GET profile → sla DTO..." -ForegroundColor Yellow
$profile = Invoke-RestMethod -Uri "$MoBaseUrl/api/v1/runtime/work-items/$wiId/profile" @moParams
$sla = $profile.sla
Assert-True ($null -ne $sla) "profile.sla null."
Assert-True (-not [string]::IsNullOrEmpty($sla.slaPolicyId)) "profile.sla.slaPolicyId bos."
Assert-True (-not [string]::IsNullOrEmpty($sla.responseDueAt)) "profile.sla.responseDueAt bos."
Assert-True (-not [string]::IsNullOrEmpty($sla.resolveDueAt)) "profile.sla.resolveDueAt bos."
Assert-True (-not [string]::IsNullOrEmpty($sla.calculatedAt)) "profile.sla.calculatedAt bos."
Write-Host "   OK — policy=$($sla.slaPolicyId)" -ForegroundColor Green
Write-Host "      responseDueAt=$($sla.responseDueAt)" -ForegroundColor Gray
Write-Host "      resolveDueAt=$($sla.resolveDueAt)" -ForegroundColor Gray
Write-Host "      breached resp=$($sla.responseBreached) resolve=$($sla.resolveBreached)" -ForegroundColor Gray

Write-Host "4) DG op_work_items snapshot..." -ForegroundColor Yellow
$dg = Invoke-RestMethod -Uri "$GatewayBaseUrl$dataPath/op_work_items/$wiId" @moParams
$dgPolicyId = Get-OcDataId $dg.slaPolicyId
Assert-True (-not [string]::IsNullOrEmpty($dgPolicyId)) "op_work_items.slaPolicyId bos."
Assert-True ($null -ne $dg.sla) "op_work_items.sla null."
Assert-True (-not [string]::IsNullOrEmpty($dg.sla.responseDueAt)) "op_work_items.sla.responseDueAt bos."
Assert-True (-not [string]::IsNullOrEmpty($dg.sla.resolveDueAt)) "op_work_items.sla.resolveDueAt bos."
Assert-True ($dgPolicyId -eq $sla.slaPolicyId) "DG policy id profile ile uyusmuyor."
Write-Host "   OK — slaPolicyId=$dgPolicyId" -ForegroundColor Green

if ($WithTransition) {
    Write-Host "5) Transition (opsiyonel)..." -ForegroundColor Yellow
    if ($profile.actions.Count -lt 1) {
        Write-Host "   ATLA — profile.actions bos" -ForegroundColor Yellow
    }
    else {
        $tk = $profile.actions[0].transitionKey
        $calcBefore = $sla.calculatedAt
        Invoke-RestMethod -Uri "$MoBaseUrl/api/v1/work-items/$wiId/transitions/$tk" -Method POST -Body "{}" @moParams | Out-Null
        $profileAfter = Invoke-RestMethod -Uri "$MoBaseUrl/api/v1/runtime/work-items/$wiId/profile" @moParams
        Assert-True ($null -ne $profileAfter.sla) "transition sonrasi profile.sla null."
        Write-Host "   OK — transition '$tk', calculatedAt=$($profileAfter.sla.calculatedAt)" -ForegroundColor Green
    }
}
else {
    Write-Host "5) Transition atlandi (-WithTransition ile calistirin)" -ForegroundColor Gray
}

if ($TestBreachQuery) {
    Write-Host "6) Query wi_sla_response_breach (Faz 1.5 — soft)..." -ForegroundColor Yellow
    $queryBody = @{
        dataset    = "op_work_items"
        parameters = @{ workspaceId = $WorkspaceId }
        skip       = 0
        take       = 5
    } | ConvertTo-Json -Depth 5 -Compress
    try {
        $breach = Invoke-RestMethod -Uri "$MoBaseUrl/api/v1/runtime/queries/wi_sla_response_breach/execute" -Method POST -Body $queryBody @moParams
        Write-Host "   OK — total=$($breach.total)" -ForegroundColor Green
    }
    catch {
        $msg = if ($_.ErrorDetails.Message) { $_.ErrorDetails.Message } else { $_.Exception.Message }
        Write-Host "   UYARI — query henuz hazir degil veya hata: $msg" -ForegroundColor Yellow
    }
}

Write-Host ""
Write-Host "SLA-1 smoke tamam — $wiKey" -ForegroundColor Cyan
Write-Host ""
