# Faz 2 — Workspace mail policies UI smoke (Odak)
# On kosul: get-operationcore-token.ps1, mngui deploy
param(
    [string]$Gateway = "http://192.168.20.20:5040",
    [string]$UiBase = "http://192.168.20.20:3000",
    [string]$WorkspaceId = "",
    [string]$Server = "192.168.20.20"
)

$ErrorActionPreference = "Stop"
$ocScripts = Join-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) "operationcore/scripts"
$demoSeed = Join-Path $ocScripts "operationcore-demo-seed.json"
$tokenFile = "$env:TEMP\operationcore_dg_token.txt"

if (-not (Test-Path $tokenFile)) {
    throw "Token yok. Once: .\docs\odak\operationcore\scripts\get-operationcore-token.ps1"
}
if ([string]::IsNullOrEmpty($WorkspaceId) -and (Test-Path $demoSeed)) {
    $WorkspaceId = (Get-Content $demoSeed -Raw | ConvertFrom-Json).workspaceId
}
if ([string]::IsNullOrEmpty($WorkspaceId)) {
    throw "WorkspaceId gerekli."
}

$token = (Get-Content $tokenFile -Raw).Trim()
$headers = @{ Authorization = "Bearer $token" }

Write-Host "=== Mail policies UI smoke ===" -ForegroundColor Cyan

# 1) UI ayakta
try {
    $ui = Invoke-WebRequest -Uri $UiBase -UseBasicParsing -TimeoutSec 15
    Write-Host "[1] UI health: $($ui.StatusCode)" -ForegroundColor Green
} catch {
    throw "UI erisilemiyor: $UiBase"
}

# 2) DG — op_notification_policies listesi
$filter = [Uri]::EscapeDataString("workspaceId:eq:$WorkspaceId")
$uri = "$Gateway/data/api/v1/data/op_notification_policies?limit=50&filter=$filter"
$rows = Invoke-RestMethod -Method GET -Uri $uri -Headers $headers
$items = @()
if ($rows -is [System.Array]) {
    $items = $rows
} elseif ($rows.data) {
    $items = @($rows.data)
} elseif ($rows.items) {
    $items = @($rows.items)
} elseif ($rows) {
    $items = @($rows)
}
Write-Host "[2] DG policies (workspace=$WorkspaceId): $($items.Count) kayit" -ForegroundColor Green
$mailPolicies = @($items | Where-Object { $_.channels -contains 'email' })
Write-Host "    E-posta kanalli: $($mailPolicies.Count)" -ForegroundColor Gray
foreach ($p in $mailPolicies) {
    Write-Host "    - $($p.name) template=$($p.emailTemplateKey)" -ForegroundColor Gray
}

# 3) Deploy bundle — mail sekmesi bileseni
Import-Module Posh-SSH -Force
$repoRoot = Split-Path (Split-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) -Parent) -Parent
. (Join-Path $repoRoot "scripts/odak/OdakSshCommon.ps1")
Initialize-OdakSshEnvironment -Server $Server
$cred = Get-OdakSshCredential -Server $Server
$remote = "docker exec mngui sh -c 'grep -rl OcWorkspaceMailPoliciesExplorer /usr/share/nginx/html 2>/dev/null | head -1'"
$session = New-SSHSession -ComputerName $Server -Credential $cred -AcceptKey
$r = Invoke-SSHCommand -SessionId $session.SessionId -Command $remote -TimeOut 30
Remove-SSHSession -SessionId $session.SessionId | Out-Null
if ($r.Output -and $r.Output[0]) {
    Write-Host "[3] mngui bundle: mail policies bileseni bulundu" -ForegroundColor Green
    Write-Host "    $($r.Output[0])" -ForegroundColor DarkGray
} else {
    Write-Host "[3] UYARI: bundle icinde OcWorkspaceMailPoliciesExplorer bulunamadi" -ForegroundColor Yellow
}

$deepLink = "$UiBase/apps/operation-core/admin/workspace-definitions?workspaceId=$WorkspaceId&tab=mail"
Write-Host ""
Write-Host "Smoke tamamlandi." -ForegroundColor Cyan
Write-Host "  Tarayici: $deepLink" -ForegroundColor Gray
Write-Host "  odak_admin ile giris; politika listesi + Yeni politika modalini dogrulayin." -ForegroundColor Gray
