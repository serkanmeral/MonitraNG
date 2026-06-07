# AN-4 - Alarm bildirim politikasi API + dispatch smoke
# On kosul: mngalarm deploy, odak_admin token, istege bagli mail sablonlari seed
param(
    [string]$GatewayUrl = "http://192.168.20.20:5040",
    [string]$Domain = "odak",
    [string]$RecipientPersonId = "6a0f8fd13d6ba5d774ee37c7",
    [switch]$SkipObservation,
    [switch]$KeepPolicy
)

$ErrorActionPreference = "Stop"
$alarmBase = "$($GatewayUrl.TrimEnd('/'))/alarm/api/v1"
$tokenFile = "$env:TEMP\operationcore_dg_token.txt"
$ocScripts = Join-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) "operationcore\scripts"
$loadTokenScript = Join-Path $ocScripts "load-operationcore-token.ps1"

if (-not (Test-Path $loadTokenScript)) {
    throw "load-operationcore-token.ps1 bulunamadi: $loadTokenScript"
}
$token = & $loadTokenScript
if ([string]::IsNullOrWhiteSpace($token)) {
    throw "Token alinamadi. Once get-operationcore-token.ps1 calistirin."
}

$headers = @{
    Authorization    = "Bearer $token"
    "X-Domain-Name"  = $Domain
    "Content-Type"   = "application/json"
}
$alarmParams = @{ Headers = $headers; ErrorAction = "Stop" }

Write-Host "=== Alarm notification policy smoke ===" -ForegroundColor Cyan

# 1) Liste
$list = Invoke-RestMethod -Uri "$alarmBase/notification-policies" -Method GET @alarmParams
$count = if ($list -is [System.Array]) { $list.Count } else { 0 }
Write-Host "[1] GET notification-policies -> $count kayit" -ForegroundColor Green

# 2) Ornek politika olustur
$policyName = "Smoke policy $(Get-Date -Format 'yyyyMMdd-HHmmss')"
$createBody = @{
    name                = $policyName
    description         = "AN-4 smoke - otomatik olusturuldu"
    eventType           = "AlarmRaised"
    channels            = @("inApp", "email")
    recipientPersonIds  = @($RecipientPersonId)
    emailTemplateKey    = "alarm-raised"
    settings            = @{ pushToast = $true; toastSeverity = "warning" }
    cooldownMinutes     = 0
    priority            = 90
    isActive            = $true
} | ConvertTo-Json -Compress

$created = Invoke-RestMethod -Uri "$alarmBase/notification-policies" -Method POST -Body $createBody @alarmParams
$policyId = $created.id
if (-not $policyId) { $policyId = $created.Id }
Write-Host "[2] POST policy -> id=$policyId name=$policyName" -ForegroundColor Green

# 3) Guncelle
$updateBody = (@{
    description = "AN-4 smoke - guncellendi"
    settings    = @{ pushToast = $true; toastSeverity = "error" }
} | ConvertTo-Json -Compress)
Invoke-RestMethod -Uri "$alarmBase/notification-policies/$policyId" -Method PUT -Body $updateBody @alarmParams | Out-Null
Write-Host "[3] PUT policy -> OK" -ForegroundColor Green

if (-not $SkipObservation) {
    Write-Host "[4] Observation ingest (cpu_usage=95)..." -ForegroundColor Yellow
    $ingestBody = @{
        domainName = $Domain
        key        = "cpu_usage"
        value      = 95
        kind       = "metric"
    } | ConvertTo-Json -Compress
    try {
        $ingest = Invoke-RestMethod -Uri "$alarmBase/dev/observations/ingest" -Method POST -Body $ingestBody @alarmParams
        Write-Host "    raised=$($ingest.alarmsRaised) updated=$($ingest.alarmsUpdated)" -ForegroundColor Green
        Write-Host "    Tarayici: odak_admin ile toaster + zil badge + inbox kontrol edin (Ctrl+F5)" -ForegroundColor Gray
        Write-Host "    E-posta: alarm-raised sablonu seed edilmisse gelen kutusu kontrol edin" -ForegroundColor Gray
    }
    catch {
        Write-Host "    ingest atlandi veya hata: $($_.Exception.Message)" -ForegroundColor DarkYellow
    }
}
else {
    Write-Host "[4] Observation ingest atlandi (-SkipObservation)" -ForegroundColor DarkGray
}

if (-not $KeepPolicy) {
    Invoke-RestMethod -Uri "$alarmBase/notification-policies/$policyId" -Method DELETE @alarmParams | Out-Null
    Write-Host "[5] DELETE policy -> OK (temizlendi)" -ForegroundColor Green
}
else {
    Write-Host "[5] Policy birakildi (-KeepPolicy): $policyId" -ForegroundColor Yellow
}

Write-Host "`nTamamlandi. UI: /apps/alarm-center/notification-policies" -ForegroundColor Cyan
