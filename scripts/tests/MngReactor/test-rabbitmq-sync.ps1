# RabbitMQ -> MngReactor -> MQTT Sync Test
# DataGateway'de mon_engines/mon_agents/mon_assets degisince RabbitMQ'ya event gidiyor,
# MngReactor dinleyip MQTT sync tetikliyor. Bu script bu akisi test eder.
#
# Onkosullar:
#   - mng_common (MongoDB, RabbitMQ) calisiyor
#   - mng_apps (mngdatagateway, mngreactor) calisiyor
#   - mon_engines, mon_agents, mon_assets publish_mode = "basic"
#   - Test domain'de en az 1 engine var
#
# Kullanim: .\test-rabbitmq-sync.ps1

param(
    [string]$BaseUrl = "https://localhost:5040",
    [string]$Domain = "meral",
    [switch]$SkipLogCheck = $false
)

$ErrorActionPreference = "Stop"
[System.Net.ServicePointManager]::ServerCertificateValidationCallback = { $true }

$scriptPath = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $PSCommandPath }
$loadTokenScript = Join-Path $scriptPath "auth\load-token.ps1"
if (-not (Test-Path $loadTokenScript)) {
    Write-Host "Hata: load-token.ps1 bulunamadi: $loadTokenScript" -ForegroundColor Red
    exit 1
}

# Gateway uzerinden Reactor ve DG'ye erisim (veya direkt port)
# Gateway (5040) uzerinden: /data -> DG, /reactor -> Reactor. DG direkt (5010): /api/v1
$isGateway = $BaseUrl -match ":5040"
$DataGatewayUrl = if ($isGateway) { "$BaseUrl/data/api/v1" } else { "$BaseUrl/api/v1" }
$ReactorUrl = if ($isGateway) { "$BaseUrl/reactor/api/v1" } else { "http://localhost:5003/api/v1" }

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "RabbitMQ -> MngReactor Sync Test" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Reactor: $ReactorUrl"
Write-Host "DG:     $DataGatewayUrl"
Write-Host "Domain: $Domain"
Write-Host ""

# [1] Token al
Write-Host "[1] Token aliniyor..." -ForegroundColor Yellow
$token = & $loadTokenScript
if ([string]::IsNullOrEmpty($token)) {
    Write-Host "  HATA: Token alinamadi." -ForegroundColor Red
    exit 1
}
Write-Host "  OK" -ForegroundColor Green

$headers = @{
    "Authorization" = "Bearer $token"
    "Content-Type"  = "application/json"
}

# [2] Engine listesi
Write-Host "[2] mon_engines listesi aliniyor..." -ForegroundColor Yellow
$engines = $null
$getParams = @{ Uri = "$DataGatewayUrl/data/mon_engines?filter=status:eq:active&limit=5"; Headers = $headers; Method = "GET"; ErrorAction = "Stop" }
if (Get-Command Invoke-RestMethod | Select-Object -ExpandProperty Parameters | Where-Object { $_.Name -eq "SkipCertificateCheck" }) {
    $getParams.SkipCertificateCheck = $true
}
try {
    $engines = Invoke-RestMethod @getParams
} catch {
    Write-Host "  HATA: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

$engineList = @()
if ($engines -is [array]) { $engineList = $engines }
elseif ($engines.data) { $engineList = $engines.data }
elseif ($engines.items) { $engineList = $engines.items }

if ($engineList.Count -eq 0) {
    Write-Host "  HATA: mon_engines bos. Onceden bir engine olusturun." -ForegroundColor Red
    exit 1
}

$engine = $engineList[0]
$engineId = if ($engine.__dataId) { $engine.__dataId } else { $engine.dataId }
Write-Host "  OK: engineId = $engineId (name: $($engine.name))" -ForegroundColor Green

# [3] Engine'i guncelle (description - zararsiz alan)
$oldDesc = if ($engine.description) { $engine.description } else { "" }
$newDesc = "Sync test - $(Get-Date -Format 'HH:mm:ss')"
Write-Host "[3] Engine guncelleniyor (description: $newDesc)..." -ForegroundColor Yellow

$updateBody = @{
    __dataId = $engineId
    name = $engine.name
    description = $newDesc
    status = $engine.status
    domain = $engine.domain
    username = $engine.username
    sendSchedule = $engine.sendSchedule
    configSyncPeriodMinutes = $engine.configSyncPeriodMinutes
} | ConvertTo-Json -Compress

$hasSkipCert = Get-Command Invoke-RestMethod | Select-Object -ExpandProperty Parameters | Where-Object { $_.Name -eq "SkipCertificateCheck" }
$invokeParams = @{ Uri = "$DataGatewayUrl/data/mon_engines/$engineId"; Headers = $headers; Method = "PUT"; Body = $updateBody; ErrorAction = "Stop" }
if ($hasSkipCert) { $invokeParams.SkipCertificateCheck = $true }
try {
    $result = Invoke-RestMethod @invokeParams
    Write-Host "  OK: Engine guncellendi" -ForegroundColor Green
} catch {
    Write-Host "  HATA: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# [4] Kisa bekle - RabbitMQ event, Reactor isleme, log yazma
Write-Host "[4] 3 saniye bekleniyor (RabbitMQ -> Reactor)..." -ForegroundColor Yellow
Start-Sleep -Seconds 3

# [5] MngReactor loglarinda "Monitoring sync" var mi?
if (-not $SkipLogCheck) {
    Write-Host "[5] MngReactor container loglari kontrol ediliyor..." -ForegroundColor Yellow
    $logs = docker logs mngreactor 2>&1 | Select-Object -Last 100
    $syncLines = $logs | Where-Object { $_ -match "Monitoring sync" }

    if ($syncLines) {
        Write-Host "  PASS: 'Monitoring sync' loglari bulundu:" -ForegroundColor Green
        $syncLines | ForEach-Object { Write-Host "    $_" }
    } else {
        Write-Host "  UYARI: 'Monitoring sync' logu gorulmedi." -ForegroundColor Yellow
        Write-Host "  - RabbitMQ baglantisi dogru mu? (MngReactorSettings__RabbitMQ__Host)" -ForegroundColor Gray
        Write-Host "  - mon_engines publish_mode = basic mi?" -ForegroundColor Gray
        Write-Host "  - Son 20 log satiri:" -ForegroundColor Gray
        ($logs | Select-Object -Last 20) | ForEach-Object { Write-Host "    $_" }
    }
} else {
    Write-Host "[5] Log kontrolu atlandi (-SkipLogCheck)" -ForegroundColor Gray
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Test tamamlandi" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "MngEngine calisiyorsa ve MQTT ile bagliysa, loglarda su mesaji gormelisiniz:" -ForegroundColor White
Write-Host "  [INF] MQTT sync mesaji alindi, config sync baslatiliyor..." -ForegroundColor Gray
Write-Host "  [INF] MQTT tetikli config sync tamamlandi. Agent=X, job'lar yeniden zamanlandi" -ForegroundColor Gray
Write-Host ""
