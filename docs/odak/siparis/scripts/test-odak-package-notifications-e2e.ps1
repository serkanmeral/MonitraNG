# Odak Siparis — PackageUpdated + ShipmentCompleted bildirim E2E
#
# Usage:
#   $env:MNG_OC_USE_PROD_TOKEN = "1"
#   .\test-odak-package-notifications-e2e.ps1 -BaseUrl "http://192.168.20.8:5040"
#   .\test-odak-package-notifications-e2e.ps1 -Event PackageUpdated

param(
    [string]$BaseUrl = "http://192.168.20.20:5040",
    [ValidateSet("PackageUpdated", "ShipmentCompleted", "All")]
    [string]$Event = "All",
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
        $p.Body = ($Body | ConvertTo-Json -Depth 25 -Compress)
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
    if (-not $row) { return $null }
    if ($row.__dataId) { return [string]$row.__dataId }
    if ($row.dataId) { return [string]$row.dataId }
    return $null
}

function Test-PolicyMatch {
    param(
        $Policy,
        [string]$EventType,
        [string[]]$ChangedFields = @(),
        [string]$ShipmentPrev = $null,
        [string]$ShipmentNext = $null
    )
    if ($Policy.isActive -eq $false) { return $false }
    if ([string]$Policy.eventType -ne $EventType) { return $false }

    if ($EventType -eq "PackageUpdated") {
        $mode = if ($Policy.updateTriggerMode) { [string]$Policy.updateTriggerMode } else { "always" }
        if ($mode -eq "always") { return $true }
        $watched = @($Policy.watchedFields | Where-Object { $_ })
        if (-not $watched.Count) { return $false }
        foreach ($f in $watched) { if ($ChangedFields -contains $f) { return $true } }
        return $false
    }

    if ($EventType -eq "ShipmentCompleted") {
        $mode = if ($Policy.shipmentTriggerMode) { [string]$Policy.shipmentTriggerMode } else { "transition" }
        if ($mode -eq "always") { return $true }
        if ($mode -eq "toStatus") {
            $target = if ($Policy.targetStatus) { [string]$Policy.targetStatus } else { "Tamamlandi" }
            return ($ShipmentNext -eq $target)
        }
        $from = if ($Policy.fromStatus) { [string]$Policy.fromStatus } else { "Planlandi" }
        $to = if ($Policy.toStatus) { [string]$Policy.toStatus } else { "Tamamlandi" }
        return ($ShipmentPrev -eq $from -and $ShipmentNext -eq $to)
    }
    return $true
}

function Send-PolicyNotifications {
    param(
        [string]$EventType,
        [hashtable]$MailContext,
        [object[]]$Policies,
        [hashtable]$MatchContext
    )
    $sent = 0
    $failed = 0
    $skipped = 0

    foreach ($policy in $Policies) {
        if (-not (Test-PolicyMatch -Policy $policy -EventType $EventType @MatchContext)) {
            Write-Host "  SKIP (eslesmedi): $($policy.name)" -ForegroundColor DarkYellow
            $skipped++
            continue
        }

        $templateKey = [string]$policy.emailTemplateKey
        if ([string]::IsNullOrWhiteSpace($templateKey)) {
            Write-Host "  SKIP: $($policy.name) — templateKey bos" -ForegroundColor Yellow
            $skipped++
            continue
        }

        $ids = @($policy.recipientPersonIds | Where-Object { $_ })
        if (-not $ids.Count) {
            Write-Host "  SKIP: $($policy.name) — alici yok" -ForegroundColor Yellow
            $skipped++
            continue
        }

        $usersResp = Invoke-Api -Method POST -Uri "$BaseUrl$keeperPath/user/by-ids" -Body @{ Ids = $ids }
        $emails = @($usersResp.users | ForEach-Object { [string]$_.email } | Where-Object { $_ -match '@' } | Select-Object -Unique)
        if (-not $emails.Count) {
            Write-Host "  FAIL: $($policy.name) — e-posta cozulemedi" -ForegroundColor Red
            $failed++
            continue
        }

        $sendBody = @{
            templateKey = $templateKey.Trim()
            to          = $emails
            context     = $MailContext
        }
        if ($policy.emailSubject) { $sendBody.subject = [string]$policy.emailSubject }

        try {
            $r = Invoke-Api -Method POST -Uri "$BaseUrl$notifierPath" -Body $sendBody
            Write-Host "  OK: $($policy.name) -> $($emails -join ', ') status=$($r.status)" -ForegroundColor Green
            $sent++
        }
        catch {
            Write-Host "  FAIL: $($policy.name) -> $($_.Exception.Message)" -ForegroundColor Red
            if ($_.ErrorDetails.Message) { Write-Host "        $($_.ErrorDetails.Message)" -ForegroundColor DarkRed }
            $failed++
        }
    }
    return @{ sent = $sent; failed = $failed; skipped = $skipped }
}

Write-Host "`n=== Odak bildirim E2E (Updated + Shipment) ===" -ForegroundColor Cyan
Write-Host "Gateway: $BaseUrl | Event filter: $Event`n" -ForegroundColor Gray

$pkgNo = if ($PackageNo) { $PackageNo } else { "E2E-NOTIF-$(Get-Date -Format 'yyyyMMdd-HHmmss')" }
$legacyId = ([guid]::NewGuid().ToString("N"))

Write-Host "[setup] Is paketi olusturuluyor: $pkgNo" -ForegroundColor Yellow
$createBody = @{
    legacyPackageId = $legacyId
    packageNo       = $pkgNo
    name            = "Bildirim E2E test paketi"
    status          = "open"
    lineCount       = 0
    notes           = "E2E baslangic"
}
$created = Unwrap-DgData (Invoke-Api -Method POST -Uri "$BaseUrl$dataPath/odak_is_paketleri" -Body $createBody)
$packageId = Get-DataId $created
if (-not $packageId) { throw "Paket olusturulamadi." }
Write-Host "  packageId=$packageId" -ForegroundColor Green

$allPolicies = Get-DgItems (Invoke-Api -Method GET -Uri "$BaseUrl$dataPath/odak_siparis_notification_policies?limit=50")
$totalFailed = 0

# --- PackageUpdated ---
if ($Event -in @("All", "PackageUpdated")) {
    Write-Host "`n[PackageUpdated] Paket guncelleniyor (notes)..." -ForegroundColor Yellow
    $updateBody = @{
        legacyPackageId = $legacyId
        packageNo       = $pkgNo
        name            = "Bildirim E2E test paketi"
        status          = "open"
        lineCount       = 0
        notes           = "E2E guncelleme $(Get-Date -Format 'HH:mm:ss')"
    }
    Invoke-Api -Method PUT -Uri "$BaseUrl$dataPath/odak_is_paketleri/$packageId" -Body $updateBody | Out-Null
    Write-Host "  PUT OK" -ForegroundColor Green

    $updatedPolicies = @($allPolicies | Where-Object { $_.eventType -eq "PackageUpdated" -and $_.isActive -ne $false })
    Write-Host "  $($updatedPolicies.Count) aktif politika" -ForegroundColor Gray

    $ctx = @{
        event = @{ type = "PackageUpdated"; timestamp = (Get-Date).ToUniversalTime().ToString("o") }
        package = @{
            id        = $packageId
            packageNo = $pkgNo
            displayNo = $pkgNo
            name      = "Bildirim E2E test paketi"
            status    = "open"
        }
        changedFields = @("notes")
    }
    $match = @{ ChangedFields = @("notes") }
    $r = Send-PolicyNotifications -EventType "PackageUpdated" -MailContext $ctx -Policies $updatedPolicies -MatchContext $match
    Write-Host "  Ozet Updated: sent=$($r.sent) failed=$($r.failed) skipped=$($r.skipped)" -ForegroundColor Cyan
    $totalFailed += $r.failed
}

# --- ShipmentCompleted ---
if ($Event -in @("All", "ShipmentCompleted")) {
    Write-Host "`n[ShipmentCompleted] Sevkiyat Planlandi -> Tamamlandi..." -ForegroundColor Yellow

    $shipCreate = @{
        recordScope     = "Paketli"
        parentPackageId = $packageId
        status          = "Planlandi"
        headerDescription = "E2E sevkiyat test"
        waybillNo       = "E2E-IRS-$(Get-Date -Format 'HHmmss')"
    }
    $shipCreated = Unwrap-DgData (Invoke-Api -Method POST -Uri "$BaseUrl$dataPath/odak_sevkiyatlar" -Body $shipCreate)
    $shipmentId = Get-DataId $shipCreated
    if (-not $shipmentId) { throw "Sevkiyat olusturulamadi." }
    Write-Host "  shipmentId=$shipmentId (Planlandi)" -ForegroundColor Green

    $shipUpdate = @{
        recordScope       = "Paketli"
        parentPackageId   = $packageId
        status            = "Tamamlandi"
        headerDescription = "E2E sevkiyat test"
        waybillNo         = $shipCreate.waybillNo
    }
    Invoke-Api -Method PUT -Uri "$BaseUrl$dataPath/odak_sevkiyatlar/$shipmentId" -Body $shipUpdate | Out-Null
    Write-Host "  PUT OK (Tamamlandi)" -ForegroundColor Green

    $shipPolicies = @($allPolicies | Where-Object { $_.eventType -eq "ShipmentCompleted" -and $_.isActive -ne $false })
    Write-Host "  $($shipPolicies.Count) aktif politika" -ForegroundColor Gray

    $ctx = @{
        event = @{ type = "ShipmentCompleted"; timestamp = (Get-Date).ToUniversalTime().ToString("o") }
        package = @{
            id        = $packageId
            packageNo = $pkgNo
            displayNo = $pkgNo
            name      = "Bildirim E2E test paketi"
            status    = "open"
        }
        shipment = @{
            fromStatus = "Planlandi"
            toStatus   = "Tamamlandi"
        }
    }
    $match = @{ ShipmentPrev = "Planlandi"; ShipmentNext = "Tamamlandi" }
    $r = Send-PolicyNotifications -EventType "ShipmentCompleted" -MailContext $ctx -Policies $shipPolicies -MatchContext $match
    Write-Host "  Ozet Shipment: sent=$($r.sent) failed=$($r.failed) skipped=$($r.skipped)" -ForegroundColor Cyan
    $totalFailed += $r.failed
}

Write-Host "`nPaketNo=$pkgNo | Toplam basarisiz=$totalFailed" -ForegroundColor Cyan
if ($totalFailed -gt 0) { exit 1 }
exit 0
