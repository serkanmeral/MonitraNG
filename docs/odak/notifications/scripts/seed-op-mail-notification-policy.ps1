# OC Demo workspace — WorkItemTransitioned mail policy (work-item-transitioned)
#
# Kullanim:
#   .\docs\odak\operationcore\scripts\get-operationcore-token.ps1
#   .\docs\odak\notifications\scripts\seed-op-mail-notification-policy.ps1
#
# Opsiyonel: -WorkspaceId (varsayilan operationcore-demo-seed.json)

param(
    [string]$BaseUrl = "http://192.168.20.20:5040",
    [string]$WorkspaceId = "",
    [string]$TypeId = "",
    [string]$PolicyName = "OC Demo Mail - WorkItem Transitioned"
)

$ErrorActionPreference = "Stop"
$ocScripts = Join-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) "operationcore/scripts"
$demoSeedFile = Join-Path $ocScripts "operationcore-demo-seed.json"
$tokenFile = "$env:TEMP\operationcore_dg_token.txt"

if (-not (Test-Path $tokenFile)) {
    throw "Token yok. Once: .\docs\odak\operationcore\scripts\get-operationcore-token.ps1"
}
$token = (Get-Content $tokenFile -Raw).Trim()
if ([string]::IsNullOrEmpty($token)) { throw "Token dosyasi bos." }

if ([string]::IsNullOrEmpty($WorkspaceId) -and (Test-Path $demoSeedFile)) {
    $demo = Get-Content $demoSeedFile -Raw | ConvertFrom-Json
    $WorkspaceId = $demo.workspaceId
    if ([string]::IsNullOrEmpty($TypeId)) { $TypeId = $demo.typeId }
}

if ([string]::IsNullOrEmpty($WorkspaceId)) {
    throw "WorkspaceId gerekli (-WorkspaceId veya operationcore-demo-seed.json)."
}

$headers = @{
    Authorization  = "Bearer $token"
    "Content-Type" = "application/json"
}

$dataPath = "/data/api/v1/data"
$collection = "op_notification_policies"

function Invoke-DgGet {
    param([string]$Filter, [int]$Limit = 5)
    $uri = "$BaseUrl$dataPath/$collection`?limit=$Limit&filter=" + [Uri]::EscapeDataString($Filter)
    return Invoke-RestMethod -Method GET -Uri $uri -Headers $headers
}

function Invoke-DgPost {
    param([object]$Body)
    $uri = "$BaseUrl$dataPath/$collection"
    $json = $Body | ConvertTo-Json -Depth 20 -Compress
    return Invoke-RestMethod -Method POST -Uri $uri -Headers $headers -Body $json
}

function Invoke-DgPut {
    param([string]$Id, [object]$Body)
    $uri = "$BaseUrl$dataPath/$collection/$Id"
    $json = $Body | ConvertTo-Json -Depth 20 -Compress
    return Invoke-RestMethod -Method PUT -Uri $uri -Headers $headers -Body $json
}

$policyBody = @{
    name                = $PolicyName
    workspaceId         = $WorkspaceId
    eventType           = "WorkItemTransitioned"
    channels            = @("email")
    recipients          = @("assignee")
    emailTemplateKey    = "work-item-transitioned"
    emailSubject        = $null
    transitionKey       = $null
    excludeActor        = $false
    isActive            = $true
    priority            = 50
}

if (-not [string]::IsNullOrEmpty($TypeId)) {
    $policyBody.typeId = $TypeId
}

Write-Host "Mail policy: $PolicyName (workspace=$WorkspaceId)" -ForegroundColor Cyan

$filter = "name:eq:$PolicyName"
$existing = Invoke-DgGet -Filter $filter
$items = @($existing.data)
if ($items.Count -eq 0) { $items = @($existing.items) }

if ($items.Count -gt 0) {
    $id = $items[0].__dataId
    if (-not $id) { $id = $items[0].dataId }
    Write-Host "  Mevcut policy guncelleniyor: $id" -ForegroundColor Yellow
    Invoke-DgPut -Id $id -Body $policyBody | Out-Null
    Write-Host "  OK — PUT $id" -ForegroundColor Green
}
else {
    $created = Invoke-DgPost -Body $policyBody
    $id = $created.data.__dataId
    if (-not $id) { $id = $created.__dataId }
    Write-Host "  OK — POST yeni policy $id" -ForegroundColor Green
}

Write-Host "Tamam. emailTemplateKey=work-item-transitioned, channels=email, recipients=assignee" -ForegroundColor Cyan
