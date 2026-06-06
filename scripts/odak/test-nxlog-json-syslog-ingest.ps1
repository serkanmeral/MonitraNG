# SIEM — NxLog JSON syslog (UDP) → Engine → Reactor windows.nxlog-json.v1
param(
    [string]$EngineUrl = "http://192.168.20.20:5037",
    [string]$Gateway = "http://192.168.20.20:5040",
    [string]$Server = "192.168.20.20",
    [int]$UdpPort = 5514,
    [string]$Domain = "odak",
    [string]$ExpectedHost = "TERMINAL.odak.local",
    [string]$ExpectedUser = "probe_fail_user",
    [switch]$FailIfSkipped
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path
$fixturePath = Join-Path $repoRoot "tests/fixtures/siem/nxlog_terminal_4625.json.txt"
$tokenScript = Join-Path $PSScriptRoot "../tests/MngDataGateway/auth/get-token.ps1"

function Skip-Test([string]$Reason) {
    Write-Host "SKIP NxLog JSON syslog: $Reason" -ForegroundColor Yellow
    if ($FailIfSkipped) { exit 1 }
    exit 0
}

if (-not (Test-Path $fixturePath)) { throw "Fixture eksik: $fixturePath" }
$payload = (Get-Content $fixturePath -Raw).TrimEnd()

Write-Host "=== SIEM NxLog JSON syslog ingest ===" -ForegroundColor Cyan
Write-Host "  UDP ${Server}:${UdpPort}" -ForegroundColor Gray

Write-Host "`n1) Engine health..." -ForegroundColor Cyan
try {
    Invoke-RestMethod -Uri "$EngineUrl/swagger/index.html" -TimeoutSec 8 | Out-Null
} catch {
    Skip-Test $_.Exception.Message
}

function Send-UdpPayload {
    $udp = New-Object System.Net.Sockets.UdpClient
    try {
        $bytes = [System.Text.Encoding]::UTF8.GetBytes($payload)
        [void]$udp.Send($bytes, $bytes.Length, $Server, $UdpPort)
    } finally {
        $udp.Close()
    }
}

Write-Host "`n2) UDP NxLog JSON fixture..." -ForegroundColor Yellow
Send-UdpPayload

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
        Send-UdpPayload
        Start-Sleep -Seconds 2
    }
}
Write-Host "   accepted=$($flush.accepted) published=$($flush.published)" -ForegroundColor Green
if ($flush.accepted -lt 1) { throw "FAIL: Engine flush accepted=$($flush.accepted)" }

Start-Sleep -Seconds 2

Write-Host "`n4) Reactor query (windows.nxlog-json.v1)..." -ForegroundColor Cyan
$token = & $tokenScript -KeeperBaseUrl $Gateway -DomainName $Domain -Username "odak_admin" -Password "Admin123!"
$hdr = @{ Authorization = "Bearer $token"; "X-Domain-Name" = $Domain }
$q = Invoke-RestMethod -Uri "$Gateway/reactor/api/v1/sec-events?limit=20&eventAction=login_failed&sourceType=ad" -Headers $hdr
$match = @($q.items) | Where-Object {
    $_.parserId -eq "windows.nxlog-json.v1" `
        -and $_.actorUser -eq $ExpectedUser `
        -and $_.sourceHost -eq $ExpectedHost
} | Select-Object -First 1

if (-not $match) {
    throw "FAIL: windows.nxlog-json.v1 kaydi bulunamadi (user=$ExpectedUser host=$ExpectedHost)"
}

Write-Host "   OK parser=$($match.parserId) user=$($match.actorUser) host=$($match.sourceHost)" -ForegroundColor Green
Write-Host "`nOK NxLog JSON syslog ingest PASS" -ForegroundColor Green
