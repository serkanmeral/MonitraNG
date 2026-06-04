# Odak MngEngine — config.txt uygulama hatirlatmasi ve health kontrolu
param(
    [string]$EngineUrl = "http://192.168.20.20:5037",
    [switch]$WaitHealthy
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

Write-Host @"

Sonraki adim (bir kez):
  1) MngReactor UI veya API ile Engine icin config string uretin
  2) $EngineUrl uzerinden config string yapistirin
     - ServerUrl: http://192.168.20.20:5040/reactor (veya http://mngreactor:5003 container icinden)
     - TokenUrl: http://192.168.20.20:5040/keeper/api/auth/token
  3) config.txt volume'da kalir (mngengine_data)

SIEM test:
  pwsh scripts/odak/test-engine-syslog-s4.1.ps1 -EngineUrl $EngineUrl -VerifyOdakMongo
  pwsh scripts/odak/test-engine-sec-events-s3.4.ps1 -EngineUrl $EngineUrl -VerifyOdakMongo

"@ -ForegroundColor DarkGray
