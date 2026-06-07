# T1-T3 - inApp toaster smoke (deploy sonrasi)
# On kosul: mnghub + mngoperations + mngui deploy, odak_admin token
param(
    [string]$GatewayUrl = "http://192.168.20.20:5040",
    [string]$HubDirectUrl = "http://192.168.20.20:5020",
    [string]$AssigneePersonId = "6a0f8fd13d6ba5d774ee37c7",
    [switch]$SkipWorkItem
)

$ErrorActionPreference = "Stop"
$MoBaseUrl = "$($GatewayUrl.TrimEnd('/'))/operations"
$tokenFile = "$env:TEMP\operationcore_dg_token.txt"
$ocScripts = Join-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) "operationcore/scripts"
$seedFile = Join-Path $ocScripts "operationcore-demo-seed.json"

if (-not (Test-Path $tokenFile)) {
    throw "Token yok. Once: .\docs\odak\operationcore\scripts\get-operationcore-token.ps1"
}
$token = (Get-Content $tokenFile -Raw).Trim()
$headers = @{
    Authorization  = "Bearer $token"
    "Content-Type" = "application/json"
}
$moParams = @{ Headers = $headers; ErrorAction = "Stop" }

Write-Host "=== In-app toaster smoke ===" -ForegroundColor Cyan

# 1) Hub internal API (gateway /hub veya direkt 5020)
$probeUrls = @(
    "$GatewayUrl/hub/api/v1/internal/user-notify",
    "$HubDirectUrl/api/v1/internal/user-notify"
)
$hubOk = $false
$body = @{
    userId  = $AssigneePersonId
    payload = @{
        title            = "Hub smoke $(Get-Date -Format 'HH:mm:ss')"
        message          = "Deploy sonrasi probe - tarayicida toaster beklenir"
        notificationType = "SmokeProbe"
    }
} | ConvertTo-Json -Compress

foreach ($url in $probeUrls) {
    try {
        $r = Invoke-WebRequest -Method POST -Uri $url -Headers $headers -Body $body -UseBasicParsing
        if ($r.StatusCode -eq 202 -or $r.StatusCode -eq 200) {
            Write-Host "[1] Hub user-notify OK ($url) -> $($r.StatusCode)" -ForegroundColor Green
            $hubOk = $true
            break
        }
    }
    catch {
        $code = $_.Exception.Response.StatusCode.value__
        Write-Host "[1] $url -> HTTP $code" -ForegroundColor DarkGray
    }
}
if (-not $hubOk) {
    Write-Host "[1] HATA: user-notify endpoint yok - mnghub deploy gerekli" -ForegroundColor Red
    exit 1
}

Write-Host "  Tarayici: odak_admin oturumu acikken sag ust toaster gorunmeli (Ctrl+F5)" -ForegroundColor Gray

if ($SkipWorkItem) { exit 0 }

$seed = Get-Content $seedFile -Raw | ConvertFrom-Json
$createBody = @{
    workspaceId = $seed.workspaceId
    boardId     = $seed.boardId
    typeId      = $seed.typeId
    title       = "Toast smoke $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
    assignee    = $AssigneePersonId
} | ConvertTo-Json -Compress

$created = Invoke-RestMethod -Uri "$MoBaseUrl/api/v1/work-items" -Method POST -Body $createBody @moParams
$wiId = $created.workItem.id
if (-not $wiId) { $wiId = $created.workItem.dataId }
$wiKey = $created.workItem.key
Write-Host "[2] Work item: $wiKey ($wiId)" -ForegroundColor Green

$profile = Invoke-RestMethod -Uri "$MoBaseUrl/api/v1/runtime/work-items/$wiId/profile" @moParams
if ($profile.actions.Count -lt 1) {
    Write-Host "[3] Gecis yok - yalnizca Hub probe tamamlandi" -ForegroundColor Yellow
    exit 0
}
$tk = $profile.actions[0].transitionKey
Invoke-RestMethod -Uri "$MoBaseUrl/api/v1/work-items/$wiId/transitions/$tk" -Method POST -Body "{}" @moParams | Out-Null
Write-Host "[3] Transition $tk -> inApp+toast+mail (policy)" -ForegroundColor Green
Write-Host "  Profil: http://192.168.20.20:3000/apps/operation-core/work-items/$wiId/profile" -ForegroundColor Gray
