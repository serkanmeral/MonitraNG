# SIEM Faz 1 S4.1 — UDP syslog → Engine → Reactor sec_events
param(
    [string]$EngineUrl = "http://192.168.20.20:5037",
    [string]$Server = "192.168.20.20",
    [int]$UdpPort = 5514,
    [string]$User = "odak",
    [string]$Domain = "odak",
    [switch]$VerifyOdakMongo,
    [switch]$FailIfSkipped
)

$ErrorActionPreference = "Stop"
Import-Module Posh-SSH -Force
. (Join-Path $PSScriptRoot "OdakSshCommon.ps1")

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path
$fixturePath = Join-Path $repoRoot "tests/fixtures/siem/firewall_deny.syslog.txt"
if (-not (Test-Path $fixturePath)) { throw "Fixture eksik: $fixturePath" }
$syslogLine = (Get-Content $fixturePath -Raw).TrimEnd()

function Skip-Test([string]$Reason) {
    Write-Host "SKIP S4.1: $Reason" -ForegroundColor Yellow
    if ($FailIfSkipped) { exit 1 }
    exit 0
}

Write-Host "=== SIEM S4.1 syslog -> Engine -> Reactor ===" -ForegroundColor Cyan

Write-Host "`n1) Engine health..." -ForegroundColor Cyan
try {
    Invoke-RestMethod -Uri "$EngineUrl/swagger/index.html" -TimeoutSec 8 | Out-Null
} catch {
    Skip-Test $_.Exception.Message
}

Write-Host "`n2) UDP syslog gönder (port $UdpPort)..." -ForegroundColor Yellow
$udp = New-Object System.Net.Sockets.UdpClient
try {
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($syslogLine)
    [void]$udp.Send($bytes, $bytes.Length, $Server, $UdpPort)
    Write-Host "   Gönderildi: $($syslogLine.Substring(0, [Math]::Min(72, $syslogLine.Length)))..." -ForegroundColor DarkGray
} finally {
    $udp.Close()
}

Start-Sleep -Seconds 1

Write-Host "`n3) Engine flush..." -ForegroundColor Cyan
$flush = Invoke-RestMethod -Uri "$EngineUrl/api/SecEvents/flush" -Method POST -TimeoutSec 60
Write-Host "   accepted=$($flush.accepted) published=$($flush.published)" -ForegroundColor Green
if ($flush.accepted -lt 1) { throw "FAIL S4.1: Engine flush accepted=$($flush.accepted)" }

if (-not $VerifyOdakMongo) {
    Write-Host "`nOK S4.1 Engine syslog path (Mongo dogrulama atlandi)" -ForegroundColor Green
    exit 0
}

Write-Host "`n4) Odak Mongo dogrulama..." -ForegroundColor Cyan
Start-Sleep -Seconds 2
$cred = Get-OdakSshCredential -User $User -Server $Server
$session = New-SSHSession -ComputerName $Server -Credential $cred -AcceptKey

$mongoJs = @"
const coll = db.getSiblingDB('mng_$Domain').sec_events;
const cutoff = new Date(Date.now() - 3600000);
const denied = coll.countDocuments({
  'event.action': 'denied_flow',
  'network.srcIp': '203.0.113.5',
  ingestedAt: { `$gte: cutoff }
});
print('S41=' + JSON.stringify({ denied }));
"@

$mongoResult = Invoke-OdakMongoJsonEval -SshSession $session -JavaScript $mongoJs
$mongoLine = @($mongoResult.Output) | ForEach-Object {
    if ($_ -match 'S41=(\{.+?\})') { return $matches[1] }
} | Select-Object -First 1
Remove-SSHSession -SessionId $session.SessionId | Out-Null

if (-not $mongoLine) { throw "Mongo eval cikti alinamadi" }
$counts = $mongoLine | ConvertFrom-Json
if ($counts.denied -lt 1) { throw "FAIL S4.1: Odak Mongo'da denied_flow kaydi yok" }

Write-Host "`nOK S4.1 syslog -> Engine -> Reactor PASS" -ForegroundColor Green
exit 0
