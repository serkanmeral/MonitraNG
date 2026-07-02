# Odak Siparis — GlobalShipmentCreated bildirim E2E (prod/test)
# 1) GlobalShipmentCreated politikalari
# 2) odak-global-shipment-created sablonu var mi
# 3) alici e-postalarini coz (Keeper by-ids)
# 4) send-template (UI dispatch ile ayni payload)
#
# Usage:
#   $env:MNG_OC_USE_PROD_TOKEN = "1"
#   .\docs\odak\siparis\scripts\test-odak-global-shipment-created-notification-e2e.ps1 -BaseUrl "http://192.168.20.8:5040"

param(
    [string]$BaseUrl = "http://192.168.20.20:5040",
    [string]$WaybillNo = "E2E-GS-$(Get-Date -Format 'yyyyMMdd-HHmmss')"
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../../../..")).Path
$loadTokenScript = Join-Path $repoRoot "docs/odak/operationcore/scripts/load-operationcore-token.ps1"
$token = & $loadTokenScript
if ([string]::IsNullOrEmpty($token)) { throw "Token alinamadi." }

$dataPath = "/data/api/v1/data"
$keeperPath = "/keeper/api"
$notifierPath = "/notifier/api/v1/notifications/send-template"
$templateKey = "odak-global-shipment-created"

$headers = @{
    Authorization  = "Bearer $token"
    "Content-Type" = "application/json"
}

function Invoke-Api {
    param([string]$Method, [string]$Uri, [object]$Body = $null)
    $p = @{ Uri = $Uri; Method = $Method; Headers = $headers; ErrorAction = "Stop" }
    if ($null -ne $Body) {
        $p.Body = ($Body | ConvertTo-Json -Depth 20 -Compress)
        $p.ContentType = "application/json"
    }
    return Invoke-RestMethod @p
}

function Get-DgItems($Response) {
    if ($null -eq $Response) { return @() }
    if ($Response -is [Array]) { return @($Response) }
    if ($Response.items) { return @($Response.items) }
    if ($Response.success -eq $true -and $Response.data) {
        $d = $Response.data
        if ($d -is [Array]) { return @($d) }
        return @($d)
    }
    return @($Response)
}

Write-Host "`n=== Odak GlobalShipmentCreated notification E2E ===" -ForegroundColor Cyan
Write-Host "Gateway: $BaseUrl`n" -ForegroundColor Gray

Write-Host "[1/4] Mail sablonu: $templateKey" -ForegroundColor Yellow
$templates = Get-DgItems (Invoke-Api -Method GET -Uri "$BaseUrl$dataPath/@mail_templates?limit=200")
$tpl = @($templates | Where-Object { [string]$_.templateKey -eq $templateKey -and $_.isActive -ne $false })
if (-not $tpl.Count) {
    Write-Host "  FAIL: Sablon bulunamadi veya pasif." -ForegroundColor Red
    Write-Host "  Cozum: .\docs\odak\siparis\scripts\seed-odak-siparis-mail-templates.ps1 -BaseUrl `"$BaseUrl`"" -ForegroundColor Yellow
    exit 1
}
Write-Host "  OK: $($tpl[0].name)" -ForegroundColor Green

Write-Host "[2/4] GlobalShipmentCreated politikalari" -ForegroundColor Yellow
$policies = Get-DgItems (Invoke-Api -Method GET -Uri "$BaseUrl$dataPath/odak_siparis_notification_policies?limit=100")
$active = @($policies | Where-Object { $_.eventType -eq "GlobalShipmentCreated" -and $_.isActive -ne $false })
if (-not $active.Count) {
    Write-Host "  FAIL: Aktif GlobalShipmentCreated politikasi yok." -ForegroundColor Red
    Write-Host "  Cozum: Sevkiyat ayarlari > Bildirimler sekmesinden politika ekleyin." -ForegroundColor Yellow
    exit 1
}
Write-Host "  $($active.Count) politika" -ForegroundColor Green

$timestamp = (Get-Date).ToUniversalTime().ToString("o")
$mailContext = @{
    event = @{ type = "GlobalShipmentCreated"; timestamp = $timestamp }
    package = $null
    shipment = @{
        id                 = "e2e-global-shipment-id"
        waybillNo          = $WaybillNo
        headerDescription  = "E2E genel sevkiyat bildirim testi"
        shipmentDate       = (Get-Date -Format "dd.MM.yyyy")
        status             = "Planlandi"
        recordScope        = "Genel"
        controlType        = "—"
        lineCount          = 1
    }
    changedFields = @()
}

$sent = 0
$failed = 0

Write-Host "[3/4] Alici e-postalari cozuluyor" -ForegroundColor Yellow
Write-Host "[4/4] send-template" -ForegroundColor Yellow

foreach ($policy in $active) {
    $key = [string]$policy.emailTemplateKey
    if ([string]::IsNullOrWhiteSpace($key)) {
        Write-Host "  SKIP: $($policy.name) — templateKey bos" -ForegroundColor Yellow
        continue
    }

    $ids = @($policy.recipientPersonIds | Where-Object { $_ })
    if (-not $ids.Count) {
        Write-Host "  SKIP: $($policy.name) — alici yok" -ForegroundColor Yellow
        continue
    }

    $usersResp = Invoke-Api -Method POST -Uri "$BaseUrl$keeperPath/user/by-ids" -Body @{ Ids = $ids }
    $users = @($usersResp.users)
    $emails = @($users | ForEach-Object { [string]$_.email } | Where-Object { $_ -match '@' } | Select-Object -Unique)
    if (-not $emails.Count) {
        Write-Host "  SKIP: $($policy.name) — e-posta cozulemedi (ids=$($ids -join ','))" -ForegroundColor Red
        $failed++
        continue
    }

    $subject = if ($policy.emailSubject) { [string]$policy.emailSubject } else { $null }
    $sendBody = @{
        templateKey = $key.Trim()
        to          = $emails
        context     = $mailContext
    }
    if ($subject) { $sendBody.subject = $subject }

    try {
        $r = Invoke-Api -Method POST -Uri "$BaseUrl$notifierPath" -Body $sendBody
        Write-Host "  OK: $($policy.name) -> $($emails -join ', ') status=$($r.status) id=$($r.notificationId)" -ForegroundColor Green
        $sent++
    }
    catch {
        $detail = $_.ErrorDetails.Message
        Write-Host "  FAIL: $($policy.name) -> $($_.Exception.Message)" -ForegroundColor Red
        if ($detail) { Write-Host "        $detail" -ForegroundColor DarkRed }
        $failed++
    }
}

Write-Host "`nOzet: $sent gonderildi, $failed basarisiz | WaybillNo=$WaybillNo" -ForegroundColor Cyan
if ($failed -gt 0) { exit 1 }
if ($sent -eq 0) { exit 1 }
exit 0
