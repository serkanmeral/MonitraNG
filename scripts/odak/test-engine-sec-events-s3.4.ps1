# MngEngine S3.4 — fixture batch → Reactor sec-events ingest
# Engine config (config.txt) ayarli olmali: ServerUrl=Reactor, TokenUrl, credentials
param(
    [string]$EngineUrl = "http://localhost:5037",
    [string]$Server = "192.168.20.20",
    [string]$User = "odak",
    [string]$Domain = "odak",
    [switch]$VerifyOdakMongo,
    [switch]$FailIfSkipped
)

$ErrorActionPreference = "Stop"
Import-Module Posh-SSH -Force
. (Join-Path $PSScriptRoot "OdakSshCommon.ps1")

function Skip-Engine([string]$Reason) {
    Write-Host "SKIP Engine S3.4: $Reason" -ForegroundColor Yellow
    Write-Host "  Engine ayakta ve config.txt (Reactor URL + token) uygulanmis olmali." -ForegroundColor DarkGray
    Write-Host "  Ornek: dotnet run --project MngEngine/MngEngine.Service/Presentation/MngEngine.Api" -ForegroundColor DarkGray
    if ($FailIfSkipped) { exit 1 }
    exit 0
}

Write-Host "=== MngEngine S3.4 fixture replay ===" -ForegroundColor Cyan
Write-Host "Engine: $EngineUrl  Domain: $Domain" -ForegroundColor DarkGray

Write-Host "`n1) Engine health..." -ForegroundColor Cyan
try {
    $health = Invoke-RestMethod -Uri "$EngineUrl/swagger/index.html" -Method GET -TimeoutSec 8
    Write-Host "   Engine erisilebilir" -ForegroundColor Green
} catch {
    Skip-Engine $_.Exception.Message
}

Write-Host "`n2) POST /api/SecEvents/replay-fixtures..." -ForegroundColor Yellow
try {
    $replay = Invoke-RestMethod -Uri "$EngineUrl/api/SecEvents/replay-fixtures" -Method POST -TimeoutSec 60
} catch {
    $body = $_.ErrorDetails.Message
    throw "Engine replay basarisiz: $body"
}

Write-Host "   accepted=$($replay.accepted) rejected=$($replay.rejected) published=$($replay.published)" -ForegroundColor Green
if ($replay.accepted -lt 3) {
    throw "FAIL S3.4: accepted=$($replay.accepted) (beklenen >= 3)"
}

if (-not $VerifyOdakMongo) {
    Write-Host "`nOK Engine S3.4 fixture replay (Odak Mongo dogrulama atlandi; -VerifyOdakMongo ile acin)" -ForegroundColor Green
    exit 0
}

Write-Host "`n3) Odak Mongo dogrulama..." -ForegroundColor Cyan
Start-Sleep -Seconds 2

$cred = Get-OdakSshCredential -User $User -Server $Server
$session = New-SSHSession -ComputerName $Server -Credential $cred -AcceptKey

$mongoJs = @"
const coll = db.getSiblingDB('mng_$Domain').sec_events;
const cutoff = new Date(Date.now() - 3600000);
const denied = coll.countDocuments({ 'event.action': 'denied_flow', ingestedAt: { `$gte: cutoff } });
const loginFailed = coll.countDocuments({ 'event.action': 'login_failed', 'event.code': '4625', ingestedAt: { `$gte: cutoff } });
print('SIEM_S34=' + JSON.stringify({ denied, loginFailed }));
"@

$mongoResult = Invoke-OdakMongoJsonEval -SshSession $session -JavaScript $mongoJs
$mongoLine = @($mongoResult.Output) | ForEach-Object {
    if ($_ -match 'SIEM_S34=(\{.+?\})') { return $matches[1] }
} | Select-Object -First 1

Remove-SSHSession -SessionId $session.SessionId | Out-Null

if (-not $mongoLine) { throw "Mongo eval cikti alinamadi" }
$counts = $mongoLine | ConvertFrom-Json
Write-Host "   denied_flow=$($counts.denied) login_failed=$($counts.loginFailed)" -ForegroundColor DarkGray

if ($counts.denied -lt 1 -or $counts.loginFailed -lt 1) {
    throw "FAIL: Odak Mongo'da Engine replay kayitlari bulunamadi"
}

Write-Host "`nOK Engine S3.4 + Odak Mongo dogrulama PASS" -ForegroundColor Green
exit 0
