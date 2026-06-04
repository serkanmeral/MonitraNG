# Odak MngEngine — config.txt uygulama hatirlatmasi, opsiyonel otomatik config, health kontrolu
param(
    [string]$EngineUrl = "http://192.168.20.20:5037",
    [string]$Gateway = "http://192.168.20.20:5040",
    [string]$Domain = "odak",
    [string]$EngineId = "",
    [string]$EngineServiceUsername = "odak_admin",
    [string]$EngineServicePassword = "Admin123!",
    [switch]$WaitHealthy,
    [switch]$ApplyConfig
)

$ErrorActionPreference = "Stop"

function Ensure-OdakReactorRsaKeys {
    param(
        [Parameter(Mandatory)]
        $SshSession,
        [string]$Server = "192.168.20.20",
        [System.Management.Automation.PSCredential]$Credential
    )

    $checkCmd = "docker exec mngreactor test -f /app/publicKey.pem && docker exec mngreactor test -f /app/privateKey.pem && echo KEYS_OK || echo KEYS_MISSING"
    $check = Invoke-SSHCommand -SessionId $SshSession.SessionId -Command $checkCmd -TimeOut 30
    if (@($check.Output) -match 'KEYS_OK') {
        Write-Host "Reactor RSA anahtarlari mevcut." -ForegroundColor DarkGray
        return
    }

    Write-Host "Reactor RSA anahtarlari eksik; uretiliyor..." -ForegroundColor Yellow
    $rsa = [System.Security.Cryptography.RSA]::Create(2048)
    $privLocal = Join-Path $env:TEMP "odak-privateKey.pem"
    $pubLocal = Join-Path $env:TEMP "odak-publicKey.pem"
    [IO.File]::WriteAllBytes($privLocal, $rsa.ExportRSAPrivateKey())
    [IO.File]::WriteAllBytes($pubLocal, $rsa.ExportRSAPublicKey())
    $rsa.Dispose()

    Import-Module Posh-SSH -Force
    Set-SCPItem -ComputerName $Server -Credential $Credential -Path $pubLocal -Destination "/home/odak/" -AcceptKey
    Set-SCPItem -ComputerName $Server -Credential $Credential -Path $privLocal -Destination "/home/odak/" -AcceptKey
    $remotePub = "/home/odak/$(Split-Path $pubLocal -Leaf)"
    $remotePriv = "/home/odak/$(Split-Path $privLocal -Leaf)"

    $installCmd = @"
set -e
docker cp '$remotePub' mngreactor:/app/publicKey.pem
docker cp '$remotePriv' mngreactor:/app/privateKey.pem
docker cp '$remotePriv' mngengine:/app/privateKey.pem
rm -f '$remotePub' '$remotePriv'
echo KEYS_INSTALLED
"@
    $install = Invoke-SSHCommand -SessionId $SshSession.SessionId -Command $installCmd -TimeOut 60
    if ($install.ExitStatus -ne 0 -or -not (@($install.Output) -match 'KEYS_INSTALLED')) {
        throw "RSA anahtarlari container'lara kopyalanamadi: $($install.Error)"
    }
    Remove-Item $privLocal, $pubLocal -Force -ErrorAction SilentlyContinue
    Write-Host "RSA anahtarlari mngreactor + mngengine'e yuklendi." -ForegroundColor Green
}

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
    Import-Module Posh-SSH -Force
    . (Join-Path $PSScriptRoot "OdakSshCommon.ps1")
    $cred = Get-OdakSshCredential -User "odak" -Server ([uri]$EngineUrl).Host
    $session = New-SSHSession -ComputerName ([uri]$EngineUrl).Host -Credential $cred -AcceptKey
    try {
        Ensure-OdakReactorRsaKeys -SshSession $session -Server ([uri]$EngineUrl).Host -Credential $cred
    } finally {
        Remove-SSHSession -SessionId $session.SessionId | Out-Null
    }

    $tokenScript = Join-Path $PSScriptRoot "..\tests\MngDataGateway\auth\get-token.ps1"
    if (-not (Test-Path $tokenScript)) { throw "Token script bulunamadi: $tokenScript" }

    $token = & $tokenScript -KeeperBaseUrl $Gateway -DomainName $Domain -Username "odak_admin" -Password "Admin123!"
    if ([string]::IsNullOrWhiteSpace($token)) { throw "Keeper token alinamadi" }

    $reactor = "$Gateway/reactor"
    $headers = @{
        Authorization  = "Bearer $token"
        "Content-Type" = "application/json"
        "X-Domain-Name" = $Domain
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

    Write-Host "  mon_engines kimlik bilgileri guncelleniyor (user=$EngineServiceUsername)..." -ForegroundColor DarkGray
    $dg = "$Gateway/data/api/v1/data/mon_engines/$EngineId"
    $engineRec = Invoke-RestMethod -Uri $dg -Headers $headers -Method GET
    $updateBody = @{
        __dataId                  = $EngineId
        name                      = $engineRec.name
        description               = $engineRec.description
        status                    = if ($engineRec.status) { $engineRec.status } else { "active" }
        username                  = $EngineServiceUsername
        password                  = $EngineServicePassword
        sendSchedule              = if ($engineRec.sendSchedule) { $engineRec.sendSchedule } else { "0 */2 * * *" }
        configSyncPeriodMinutes   = if ($engineRec.configSyncPeriodMinutes) { $engineRec.configSyncPeriodMinutes } else { 10 }
    } | ConvertTo-Json
    Invoke-RestMethod -Uri $dg -Headers $headers -Method PUT -Body $updateBody -ContentType "application/json" | Out-Null

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
