# Odak Siparis — PackageCreated bildirim E2E (prod/test)
# 1) ornek is paketi olustur (DG)
# 2) politikalari yukle
# 3) alici e-postalarini coz (Keeper by-ids)
# 4) send-template (UI dispatch ile ayni payload)
#
# Usage:
#   $env:MNG_OC_USE_PROD_TOKEN = "1"
#   .\test-odak-package-created-notification-e2e.ps1 -BaseUrl "http://192.168.20.8:5040"

param(
    [string]$BaseUrl = "http://192.168.20.20:5040",
    [switch]$SkipPackageCreate,
    [string]$PackageNo = ""
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../../../..")).Path
$loadTokenScript = Join-Path $repoRoot "docs/odak/operationcore/scripts/load-operationcore-token.ps1"
$token = & $loadTokenScript
if ([string]::IsNullOrEmpty($token)) { throw "Token alinamadi." }

$dataPath = "/data/api/v1/data"
$keeperPath = "/keeper/api"
$notifierPath = "/notifier/api/v1/notifications/send-template"

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

function Unwrap-DgData($Response) {
    if ($null -eq $Response) { return $null }
    if ($Response.success -eq $true -and $null -ne $Response.data) { return $Response.data }
    return $Response
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

function Get-DataId($row) {
    if ($row.__dataId) { return [string]$row.__dataId }
    if ($row.dataId) { return [string]$row.dataId }
    return $null
}

Write-Host "`n=== Odak PackageCreated notification E2E ===" -ForegroundColor Cyan
Write-Host "Gateway: $BaseUrl`n" -ForegroundColor Gray

$pkgNo = if ($PackageNo) { $PackageNo } else { "E2E-NOTIF-$(Get-Date -Format 'yyyyMMdd-HHmmss')" }
$legacyId = ([guid]::NewGuid().ToString("N"))

$packageRow = $null
if (-not $SkipPackageCreate) {
    Write-Host "[1/4] Is paketi olusturuluyor: $pkgNo" -ForegroundColor Yellow
    $createBody = @{
        legacyPackageId = $legacyId
        packageNo       = $pkgNo
        name            = "Bildirim E2E test paketi"
        status          = "open"
        lineCount       = 0
    }
    $created = Invoke-Api -Method POST -Uri "$BaseUrl$dataPath/odak_is_paketleri" -Body $createBody
    $packageRow = Unwrap-DgData $created
    $pkgId = Get-DataId $packageRow
    Write-Host "  OK: packageId=$pkgId legacyPackageId=$legacyId" -ForegroundColor Green
}
else {
    Write-Host "[1/4] Paket olusturma atlandi (-SkipPackageCreate)" -ForegroundColor Yellow
    $packageRow = @{
        packageNo = $pkgNo
        name    = "Bildirim E2E test paketi"
        status  = "open"
        __dataId = "skipped"
    }
}

Write-Host "[2/4] PackageCreated politikalari" -ForegroundColor Yellow
$policies = Get-DgItems (Invoke-Api -Method GET -Uri "$BaseUrl$dataPath/odak_siparis_notification_policies?limit=50")
$active = @($policies | Where-Object { $_.eventType -eq "PackageCreated" -and $_.isActive -ne $false })
if (-not $active.Count) { throw "Aktif PackageCreated politikasi yok." }
Write-Host "  $($active.Count) politika" -ForegroundColor Green

$displayNo = $pkgNo
$timestamp = (Get-Date).ToUniversalTime().ToString("o")
$mailContext = @{
    event = @{ type = "PackageCreated"; timestamp = $timestamp }
    package = @{
        id          = (Get-DataId $packageRow)
        packageNo   = $pkgNo
        displayNo   = $displayNo
        name        = "Bildirim E2E test paketi"
        status      = "open"
    }
}

$sent = 0
$failed = 0

Write-Host "[3/4] Alici e-postalari cozuluyor" -ForegroundColor Yellow
Write-Host "[4/4] send-template" -ForegroundColor Yellow

foreach ($policy in $active) {
    $templateKey = [string]$policy.emailTemplateKey
    if ([string]::IsNullOrWhiteSpace($templateKey)) {
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
        templateKey = $templateKey.Trim()
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

Write-Host "`nOzet: $sent gonderildi, $failed basarisiz | PaketNo=$pkgNo" -ForegroundColor Cyan
if ($failed -gt 0) { exit 1 }
exit 0
