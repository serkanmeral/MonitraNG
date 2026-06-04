# SIEM WEF→WEC — Engine HTTP batch ingest E2E (S5)
param(
    [string]$EngineUrl = "http://192.168.20.20:5037",
    [string]$Server = "192.168.20.20",
    [string]$User = "odak",
    [string]$Domain = "odak",
    [int]$EventCount = 3,
    [switch]$VerifyOdakMongo,
    [switch]$FailIfSkipped
)

$ErrorActionPreference = "Stop"
Import-Module Posh-SSH -Force
. (Join-Path $PSScriptRoot "OdakSshCommon.ps1")

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path
$fixturePath = Join-Path $repoRoot "tests/fixtures/siem/windows_4625_failed_logon.json"
if (-not (Test-Path $fixturePath)) { throw "Fixture eksik: $fixturePath" }
$windowsTemplate = Get-Content $fixturePath -Raw | ConvertFrom-Json

function Skip-Test([string]$Reason) {
    Write-Host "SKIP WEC ingest: $Reason" -ForegroundColor Yellow
    if ($FailIfSkipped) { exit 1 }
    exit 0
}

Write-Host "=== SIEM S5 WEF->WEC->Engine wec-batch E2E ===" -ForegroundColor Cyan

Write-Host "`n1) Engine health..." -ForegroundColor Cyan
try {
    Invoke-RestMethod -Uri "$EngineUrl/swagger/index.html" -TimeoutSec 8 | Out-Null
} catch {
    Skip-Test $_.Exception.Message
}

$receivedAt = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
$items = @()
for ($i = 0; $i -lt $EventCount; $i++) {
    $ev = ($windowsTemplate | ConvertTo-Json -Depth 6 | ConvertFrom-Json)
    $ev.TimeCreated = $receivedAt
    $ev.TargetUserName = "wec_e2e_user_$i"
    $ev.IpAddress = "10.88.9.$((Get-Random -Minimum 10 -Maximum 210))"
    $items += @{
        receivedAt = $receivedAt
        source     = @{
            type    = "ad"
            product = "windows"
            host    = "WEC01.odak.local"
        }
        raw        = $ev
    }
}

$body = @{
    items     = $items
    autoFlush = $true
} | ConvertTo-Json -Depth 8

Write-Host "`n2) POST /api/SecEvents/wec-batch ($EventCount x 4625)..." -ForegroundColor Yellow
try {
    $ingest = Invoke-RestMethod -Uri "$EngineUrl/api/SecEvents/wec-batch" -Method POST -Body $body -ContentType "application/json" -TimeoutSec 90
} catch {
    $detail = $_.ErrorDetails.Message
    if ($detail -match '404' -or $detail -match 'Not Found') {
        Skip-Test "wec-batch endpoint yok — mngengine deploy gerekli"
    }
    throw "wec-batch basarisiz: $detail"
}

Write-Host "   enqueued=$($ingest.enqueued) flushed=$($ingest.flushed) accepted=$($ingest.accepted)" -ForegroundColor Green
if ($ingest.enqueued -lt $EventCount) { throw "FAIL: enqueued=$($ingest.enqueued)" }
if (-not $ingest.flushed -or $ingest.accepted -lt $EventCount) {
    throw "FAIL: flush/accepted beklenmiyor (flushed=$($ingest.flushed) accepted=$($ingest.accepted))"
}

if (-not $VerifyOdakMongo) {
    Write-Host "`nOK S5 WEC batch -> Engine -> Reactor (Mongo dogrulama atlandi)" -ForegroundColor Green
    exit 0
}

Write-Host "`n3) Odak Mongo dogrulama..." -ForegroundColor Cyan
Start-Sleep -Seconds 2
$cred = Get-OdakSshCredential -User $User -Server $Server
$session = New-SSHSession -ComputerName $Server -Credential $cred -AcceptKey

$mongoJs = @"
const coll = db.getSiblingDB('mng_$Domain').sec_events;
const cutoff = new Date(Date.now() - 3600000);
const loginFailed = coll.countDocuments({
  'event.action': 'login_failed',
  'event.code': '4625',
  'source.host': 'WEC01.odak.local',
  ingestedAt: { `$gte: cutoff }
});
print('S5=' + JSON.stringify({ loginFailed }));
"@

$mongoResult = Invoke-OdakMongoJsonEval -SshSession $session -JavaScript $mongoJs
$mongoLine = @($mongoResult.Output) | ForEach-Object {
    if ($_ -match 'S5=(\{.+?\})') { return $matches[1] }
} | Select-Object -First 1
Remove-SSHSession -SessionId $session.SessionId | Out-Null

if (-not $mongoLine) { throw "Mongo eval cikti alinamadi" }
$counts = $mongoLine | ConvertFrom-Json
Write-Host "   login_failed (WEC01)=$($counts.loginFailed)" -ForegroundColor DarkGray
if ($counts.loginFailed -lt $EventCount) {
    throw "FAIL S5: Mongo'da WEC login_failed kaydi yetersiz ($($counts.loginFailed) < $EventCount)"
}

Write-Host "`nOK S5 WEF->WEC->Engine->Reactor PASS" -ForegroundColor Green
exit 0
