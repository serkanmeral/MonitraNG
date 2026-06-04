# SIEM Faz 2.5 — Linux auth syslog (UDP) → Engine → Reactor linux.auth.v1
param(
    [string]$EngineUrl = "http://192.168.20.20:5037",
    [string]$Gateway = "http://192.168.20.20:5040",
    [string]$Server = "192.168.20.20",
    [int]$UdpPort = 5514,
    [string]$Domain = "odak",
    [switch]$FailIfSkipped
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path
$fixturePath = Join-Path $repoRoot "tests/fixtures/siem/linux_sshd_failed_password.syslog.txt"
$tokenScript = Join-Path $PSScriptRoot "../tests/MngDataGateway/auth/get-token.ps1"

function Skip-Test([string]$Reason) {
    Write-Host "SKIP Linux rsyslog: $Reason" -ForegroundColor Yellow
    if ($FailIfSkipped) { exit 1 }
    exit 0
}

if (-not (Test-Path $fixturePath)) { throw "Fixture eksik: $fixturePath" }
$syslogLine = (Get-Content $fixturePath -Raw).TrimEnd()

Write-Host "=== SIEM Faz 2.5 Linux rsyslog auth E2E ===" -ForegroundColor Cyan

Write-Host "`n1) Engine health..." -ForegroundColor Cyan
try {
    Invoke-RestMethod -Uri "$EngineUrl/swagger/index.html" -TimeoutSec 8 | Out-Null
} catch {
    Skip-Test $_.Exception.Message
}
Start-Sleep -Seconds 2

function Send-SyslogLine {
    $udp = New-Object System.Net.Sockets.UdpClient
    try {
        $bytes = [System.Text.Encoding]::UTF8.GetBytes($syslogLine)
        [void]$udp.Send($bytes, $bytes.Length, $Server, $UdpPort)
    } finally {
        $udp.Close()
    }
}

Write-Host "`n2) UDP syslog (linux auth fixture, port $UdpPort)..." -ForegroundColor Yellow
Send-SyslogLine
Write-Host "   Gönderildi: $($syslogLine.Substring(0, [Math]::Min(72, $syslogLine.Length)))..." -ForegroundColor DarkGray

Start-Sleep -Seconds 1

Write-Host "`n3) Engine flush..." -ForegroundColor Cyan
$flush = $null
for ($attempt = 1; $attempt -le 3; $attempt++) {
    try {
        $flush = Invoke-RestMethod -Uri "$EngineUrl/api/SecEvents/flush" -Method POST -TimeoutSec 60
    } catch {
        if ($attempt -eq 3) { Skip-Test $_.Exception.Message }
        Start-Sleep -Seconds 2
        continue
    }
    if ($flush.accepted -ge 1) { break }
    if ($attempt -lt 3) {
        Write-Host "   accepted=0, yeniden gönderiliyor (deneme $attempt)..." -ForegroundColor Yellow
        Send-SyslogLine
        Start-Sleep -Seconds 2
    }
}
Write-Host "   accepted=$($flush.accepted) published=$($flush.published)" -ForegroundColor Green
if ($flush.accepted -lt 1) { throw "FAIL: Engine flush accepted=$($flush.accepted)" }

Start-Sleep -Seconds 2

Write-Host "`n4) Reactor query (linux.auth.v1)..." -ForegroundColor Cyan
$token = & $tokenScript -KeeperBaseUrl $Gateway -DomainName $Domain -Username "odak_admin" -Password "Admin123!"
$hdr = @{ Authorization = "Bearer $token"; "X-Domain-Name" = $Domain }
$q = Invoke-RestMethod -Uri "$Gateway/reactor/api/v1/sec-events?limit=10&eventAction=login_failed&sourceType=endpoint" -Headers $hdr
$match = @($q.items) | Where-Object {
    $_.parserId -eq "linux.auth.v1" -and $_.networkSrcIp -eq "192.168.50.22" -and $_.sourceHost -eq "app01"
} | Select-Object -First 1

if (-not $match) {
    throw "FAIL: linux.auth.v1 kaydi bulunamadi (sourceHost=app01, srcIp=192.168.50.22). mngengine deploy gerekebilir."
}

Write-Host "   OK parserId=$($match.parserId) host=$($match.sourceHost) srcIp=$($match.networkSrcIp)" -ForegroundColor Green
Write-Host "`nOK SIEM Linux rsyslog auth E2E PASS" -ForegroundColor Green
exit 0
