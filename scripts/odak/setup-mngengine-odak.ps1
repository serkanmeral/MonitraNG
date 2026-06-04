# Odak MngEngine — config.txt uygulama hatirlatmasi, opsiyonel otomatik config, health kontrolu
param(
    [string]$EngineUrl = "http://192.168.20.20:5037",
    [string]$Gateway = "http://192.168.20.20:5040",
    [string]$Domain = "odak",
    [string]$EngineId = "",
    [switch]$WaitHealthy,
    [switch]$ApplyConfig
)

$ErrorActionPreference = "Stop"

Write-Host "=== MngEngine Odak setup ===" -ForegroundColor Cyan
Write-Host "Engine URL: $EngineUrl" -ForegroundColor DarkGray

if ($WaitHealthy) {
    Write-Host "Health bekleniyor..." -ForegroundColor Yellow
    for ($i = 0; $i -lt 30; $i++) {
        try {
            Invoke-WebRequest -Uri "$EngineUrl/swagger/index.html" -TimeoutSec 5 | Out-Null
            Write-Host "Engine ayakta." -ForegroundColor Green
            break
        } catch {
            Start-Sleep -Seconds 3
        }
        if ($i -eq 29) { throw "Engine health timeout: $EngineUrl" }
    }
}

if ($ApplyConfig) {
    Write-Host "`nConfig string Reactor'dan alinip Engine'e uygulaniyor..." -ForegroundColor Cyan
    $tokenScript = Join-Path $PSScriptRoot "..\tests\MngDataGateway\auth\get-token.ps1"
    if (-not (Test-Path $tokenScript)) { throw "Token script bulunamadi: $tokenScript" }

    $token = & $tokenScript -KeeperBaseUrl $Gateway -DomainName $Domain -Username "odak_admin" -Password "Admin123!"
    if ([string]::IsNullOrWhiteSpace($token)) { throw "Keeper token alinamadi" }

    $reactor = "$Gateway/reactor"
    $headers = @{
        Authorization  = "Bearer $token"
        "Content-Type" = "application/json"
    }

    if ([string]::IsNullOrWhiteSpace($EngineId)) {
        Write-Host "  Engine listesi aliniyor..." -ForegroundColor DarkGray
        $engines = Invoke-RestMethod -Uri "$reactor/api/v1/monitoring/engines" -Headers $headers -Method GET
        if (-not $engines.data -or $engines.data.Count -lt 1) {
            throw "mon_engines bos. Once Reactor UI'dan engine olusturun."
        }
        $EngineId = $engines.data[0].__dataId
        Write-Host "  engineId=$EngineId" -ForegroundColor DarkGray
    }

    $configStr = Invoke-RestMethod -Uri "$reactor/api/v1/engine/config-string?engineId=$EngineId" -Headers $headers -Method GET
    if (-not $configStr.configString) { throw "configString bos (engineId=$EngineId)" }

    $applyBody = @{ configText = $configStr.configString } | ConvertTo-Json
    $apply = Invoke-RestMethod -Uri "$EngineUrl/api/Config" -Method POST -Body $applyBody -ContentType "application/json" -TimeoutSec 120
    if (-not $apply.result) {
        throw "Engine config uygulanamadi (Result=false)"
    }
    Write-Host "Config uygulandi (EngineId=$EngineId)." -ForegroundColor Green
}

Write-Host @"

Sonraki adim (bir kez, -ApplyConfig ile otomatik):
  1) MngReactor UI veya API ile Engine icin config string uretin
  2) $EngineUrl uzerinden config string yapistirin
     veya: pwsh scripts/odak/setup-mngengine-odak.ps1 -ApplyConfig
     - ServerUrl: http://192.168.20.20:5040/reactor (veya http://mngreactor:5003 container icinden)
     - TokenUrl: http://192.168.20.20:5040/keeper/api/auth/token
  3) config persist volume'da kalir (mngengine_data -> /app/persist)

SIEM test:
  pwsh scripts/odak/test-engine-syslog-s4.1.ps1 -EngineUrl $EngineUrl -VerifyOdakMongo -FailIfSkipped
  pwsh scripts/odak/test-engine-sec-events-s3.4.ps1 -EngineUrl $EngineUrl -VerifyOdakMongo -FailIfSkipped

"@ -ForegroundColor DarkGray
