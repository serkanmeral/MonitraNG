# SIEM — FortiGate syslog (UDP/Engine :541) → firewall.vendor.v1 ingest smoke
param(
    [string]$EngineUrl = "http://192.168.20.20:5037",
    [string]$Gateway = "http://192.168.20.20:5040",
    [string]$Server = "192.168.20.20",
    [int]$UdpPort = 541,
    [string]$Domain = "odak",
    [string]$FwHost = "FGT-ODAK",
    [switch]$FailIfSkipped
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path
$fixturePath = Join-Path $repoRoot "tests/fixtures/siem/fortigate_traffic_deny.syslog.txt"
$tokenScript = Join-Path $PSScriptRoot "../tests/MngDataGateway/auth/get-token.ps1"

function Skip-Test([string]$Reason) {
    Write-Host "SKIP FortiGate syslog: $Reason" -ForegroundColor Yellow
    if ($FailIfSkipped) { exit 1 }
    exit 0
}

if (-not (Test-Path $fixturePath)) { throw "Fixture eksik: $fixturePath" }

$srcIp = "203.0.113.$((Get-Random -Maximum 200))"
$line = (Get-Content $fixturePath -Raw).TrimEnd() -replace '203\.0\.113\.5', $srcIp

Write-Host "=== SIEM FortiGate syslog ingest (UDP $UdpPort) ===" -ForegroundColor Cyan
Write-Host "  ${Server}:${UdpPort} srcIp=$srcIp host=$FwHost" -ForegroundColor DarkGray

try {
    Invoke-RestMethod -Uri "$EngineUrl/swagger/index.html" -TimeoutSec 8 | Out-Null
} catch {
    Skip-Test "Engine erisilemiyor: $EngineUrl"
}

$udp = New-Object System.Net.Sockets.UdpClient
try {
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($line)
    [void]$udp.Send($bytes, $bytes.Length, $Server, $UdpPort)
} finally {
    $udp.Close()
}

Write-Host "`n1) Engine flush..." -ForegroundColor Cyan
$flush = Invoke-RestMethod -Uri "$EngineUrl/api/SecEvents/flush" -Method POST -TimeoutSec 90
Write-Host "   accepted=$($flush.accepted) published=$($flush.published)" -ForegroundColor Green
if ($flush.accepted -lt 1) { throw "FAIL: flush accepted=$($flush.accepted)" }

Start-Sleep -Seconds 2

$token = & $tokenScript -KeeperBaseUrl $Gateway -DomainName $Domain -Username "odak_admin" -Password "Admin123!"
$hdr = @{ Authorization = "Bearer $token"; "X-Domain-Name" = $Domain }
$q = Invoke-RestMethod -Uri "$Gateway/reactor/api/v1/sec-events?limit=10&eventAction=denied_flow&sourceProduct=fortigate" -Headers $hdr
$match = @($q.items) | Where-Object {
    $_.parserId -eq "firewall.vendor.v1" -and $_.networkSrcIp -eq $srcIp
} | Select-Object -First 1

if (-not $match) {
    throw "FAIL: firewall.vendor.v1 kaydi bulunamadi (srcIp=$srcIp port=$UdpPort)"
}

Write-Host "   OK parser=$($match.parserId) action=$($match.eventAction) srcIp=$($match.networkSrcIp) host=$($match.sourceHost)" -ForegroundColor Green
Write-Host "`nOK FortiGate syslog UDP ingest PASS" -ForegroundColor Green
