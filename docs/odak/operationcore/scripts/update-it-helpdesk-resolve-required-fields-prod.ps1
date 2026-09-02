# IT Destek — resolve gecisine resolutionSummary requiredFields ekle (Production)
# Cozum ozeti validation kurali zaten var; gecis dialog'unda alan yoktu.
#
#   .\get-operationcore-token-prod.ps1
#   .\update-it-helpdesk-resolve-required-fields-prod.ps1

param(
    [string]$BaseUrl = "http://192.168.20.8:5040",
    [string]$MoBaseUrl = "http://192.168.20.8:5040",
    [switch]$UseGateway = $true
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
$dataPath = if ($UseGateway) { "/data/api/v1/data" } else { "/api/v1/data" }
$workspaceName = "IT Destek"
$flowName = "IT Destek - Standard Flow"
$resolveKey = "resolve"
$requiredField = "resolutionSummary"

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

function ConvertTo-PlainTransition {
    param($Tr)
    $fromId = Get-IdFromRef $Tr.fromStateId
    $toId = Get-IdFromRef $Tr.toStateId
    $req = @()
    if ($Tr.requiredFields) {
        $req = @($Tr.requiredFields | ForEach-Object { "$_" } | Where-Object { $_ })
    }
    $plain = @{
        transitionKey = [string]$Tr.transitionKey
        fromStateId   = $fromId
        toStateId     = $toId
        label         = [string]$Tr.label
        order         = [int]$Tr.order
    }
    if ($req.Count -gt 0) {
        $plain.requiredFields = $req
    }
    if ($Tr.permissions -and $Tr.permissions.groups) {
        $groups = @($Tr.permissions.groups | ForEach-Object { Get-IdFromRef $_ } | Where-Object { $_ })
        if ($groups.Count -gt 0) {
            $plain.permissions = @{ groups = $groups }
        }
    }
    return $plain
}

Write-Host "=== IT Destek resolve requiredFields guncelleme ===" -ForegroundColor Cyan

$wsItems = @(Get-Items (Invoke-DgGet -Collection "op_workspaces" -Filter "name:eq:$workspaceName" -Limit 5))
if ($wsItems.Count -eq 0) {
    Write-Host "Workspace bulunamadi: $workspaceName" -ForegroundColor Red
    exit 1
}
$workspaceId = $wsItems[0].__dataId
Write-Host "  Workspace: $workspaceId" -ForegroundColor Gray

$flowFilter = "workspaceId:eq:$workspaceId,name:eq:$flowName"
$flowItems = @(Get-Items (Invoke-DgGet -Collection "op_state_flows" -Filter $flowFilter -Limit 5))
if ($flowItems.Count -eq 0) {
    Write-Host "State flow bulunamadi: $flowName" -ForegroundColor Red
    exit 1
}
$flow = $flowItems[0]
$flowId = $flow.__dataId
Write-Host "  Flow: $flowId" -ForegroundColor Gray

$transitions = @()
$resolveFound = $false
$alreadyOk = $false
foreach ($tr in @($flow.transitions)) {
    $plain = ConvertTo-PlainTransition $tr
    if ($plain.transitionKey -eq $resolveKey) {
        $resolveFound = $true
        $existing = @($plain.requiredFields)
        if ($requiredField -in $existing) {
            $alreadyOk = $true
            Write-Host "  resolve zaten requiredFields icinde: $($existing -join ', ')" -ForegroundColor Green
        }
        else {
            $merged = @($existing + $requiredField | Select-Object -Unique)
            $plain.requiredFields = $merged
            Write-Host "  resolve requiredFields: $($merged -join ', ')" -ForegroundColor Cyan
        }
    }
    $transitions += $plain
}

if (-not $resolveFound) {
    Write-Host "resolve gecisi bulunamadi." -ForegroundColor Red
    exit 1
}

if (-not $alreadyOk) {
    $putBody = @{
        name           = $flow.name
        workspaceId    = $workspaceId
        initialStateId = Get-IdFromRef $flow.initialStateId
        isDefault      = [bool]$flow.isDefault
        isActive       = [bool]$flow.isActive
        transitions    = $transitions
    }
    Invoke-DgPut -Collection "op_state_flows" -Id $flowId -Body $putBody | Out-Null
    Write-Host "  State flow guncellendi." -ForegroundColor Green
}
else {
    Write-Host "  Degisiklik gerekmedi." -ForegroundColor Gray
}

# Dogrulama
$verify = @(Get-Items (Invoke-DgGet -Collection "op_state_flows" -Filter "id:eq:$flowId" -Limit 1))
if ($verify.Count -eq 0) {
    $verify = @(Get-Items (Invoke-DgGet -Collection "op_state_flows" -Filter $flowFilter -Limit 1))
}
$resolveTr = @($verify[0].transitions) | Where-Object { $_.transitionKey -eq $resolveKey } | Select-Object -First 1
$verifyFields = @($resolveTr.requiredFields)
if ($requiredField -in $verifyFields) {
    Write-Host "  Dogrulama OK: resolve.requiredFields = [$($verifyFields -join ', ')]" -ForegroundColor Green
}
else {
    Write-Host "  Dogrulama BASARISIZ: resolve.requiredFields = [$($verifyFields -join ', ')]" -ForegroundColor Red
    exit 1
}

# MO metadata cache reload
Write-Host "[cache] MO metadata-cache reload..." -ForegroundColor Yellow
$reloadUris = @(
    "$MoBaseUrl/operations/api/v1/workspaces/$workspaceId/metadata-cache/reload",
    "$MoBaseUrl/api/operations/v1/workspaces/$workspaceId/metadata-cache/reload",
    "$MoBaseUrl/api/v1/workspaces/$workspaceId/metadata-cache/reload"
)
$reloaded = $false
foreach ($uri in $reloadUris) {
    try {
        Invoke-RestMethod -Uri $uri -Method POST @irmParams | Out-Null
        Write-Host "  Reload OK: $uri" -ForegroundColor Green
        $reloaded = $true
        break
    }
    catch {
        Write-Host "  Denendi (basarisiz): $uri" -ForegroundColor DarkGray
    }
}
if (-not $reloaded) {
    Write-Host "  UYARI: metadata cache reload basarisiz — UI'da eski akis gorunebilir." -ForegroundColor Yellow
    Write-Host "  Manuel: POST .../workspaces/$workspaceId/metadata-cache/reload" -ForegroundColor Gray
}

Write-Host "`nTamamlandi. Profilde 'Coz' tiklaninca Cozum ozeti alani gorunmeli." -ForegroundColor Cyan
