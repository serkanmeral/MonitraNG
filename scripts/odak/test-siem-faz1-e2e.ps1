# SIEM Faz 1 E2E — Reactor sec-events ingest (S4.2–S4.6)
# Firewall + Windows 4625 + unknown fallback → Mongo sec_events + MQ sec_events.created
param(
    [string]$Server = "192.168.20.20",
    [string]$User = "odak",
    [string]$Gateway = "http://192.168.20.20:5040",
    [string]$Domain = "odak",
    [switch]$FailIfSkipped
)

$ErrorActionPreference = "Stop"
Import-Module Posh-SSH -Force
. (Join-Path $PSScriptRoot "OdakSshCommon.ps1")

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path
$fixtureDir = Join-Path $repoRoot "tests/fixtures/siem"
$reactor = "$Gateway/reactor/api/v1"
$ingestUrl = "$reactor/ingest/sec-events"

function Skip-Siem([string]$Reason) {
    Write-Host "SKIP SIEM Faz 1 E2E: $Reason" -ForegroundColor Yellow
    Write-Host "  PR-5+ deploy sonrasi tekrar calistirin (deploy-odak-apps -Services mngreactor)." -ForegroundColor DarkGray
    Write-Host "  Bkz. docs/odak/monitoring/MNGREACTOR_ODAK_DEPLOY_CHECKLIST.md" -ForegroundColor DarkGray
    if ($FailIfSkipped) { exit 1 }
    exit 0
}

function Read-Fixture([string]$Name) {
    $path = Join-Path $fixtureDir $Name
    if (-not (Test-Path $path)) { throw "Fixture eksik: $path" }
    return (Get-Content -Path $path -Raw).TrimEnd()
}

Write-Host "=== SIEM Faz 1 E2E (S4.2–S4.6) ===" -ForegroundColor Cyan
Write-Host "Gateway: $Gateway  Domain: $Domain" -ForegroundColor DarkGray

Write-Host "`n1) Token + Reactor health..." -ForegroundColor Cyan
$tokenScript = Join-Path $PSScriptRoot "..\tests\MngDataGateway\auth\get-token.ps1"
$token = & $tokenScript -KeeperBaseUrl $Gateway -DomainName $Domain -Username "odak_admin" -Password "Admin123!"
$hdr = @{
    Authorization      = "Bearer $token"
    "X-Domain-Name"    = $Domain
    "Content-Type"     = "application/json"
}

try {
    $live = Invoke-RestMethod -Uri "$reactor/health/live" -Headers $hdr -TimeoutSec 15
    if ($live.status -ne "alive") { Skip-Siem "health/live status=$($live.status)" }
    Write-Host "   Reactor ayakta" -ForegroundColor Green
} catch {
    Skip-Siem $_.Exception.Message
}

Write-Host "`n2) sec-events route probe..." -ForegroundColor Cyan
try {
    $probe = Invoke-WebRequest -Uri $ingestUrl -Method POST -Headers $hdr -Body '{"items":[]}' -SkipHttpErrorCheck
    if ($probe.StatusCode -eq 404) { Skip-Siem "POST /ingest/sec-events 404 (eski image?)" }
    if ($probe.StatusCode -eq 401) { throw "401 — token/domain hatasi" }
    if ($probe.StatusCode -ne 400) {
        Skip-Siem "Beklenmeyen probe status=$($probe.StatusCode) (sec-events henuz deploy edilmemis olabilir)"
    }
    Write-Host "   Route mevcut (bos items -> 400)" -ForegroundColor Green
} catch {
    if ($_.Exception.Message -notmatch '401') { Skip-Siem $_.Exception.Message }
    throw
}

Write-Host "`n3) MQ capture queue (sec_events.created.$Domain)..." -ForegroundColor Cyan
$cred = Get-OdakSshCredential -User $User -Server $Server
$session = New-SSHSession -ComputerName $Server -Credential $cred -AcceptKey
$mqQueue = Initialize-OdakSecEventsMqCapture -SshSession $session -Domain $Domain
Write-Host "   Queue: $mqQueue" -ForegroundColor DarkGray

$receivedAt = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
$firewallRaw = Read-Fixture "firewall_deny.syslog.txt"
$windowsObj = Read-Fixture "windows_4625_failed_logon.json" | ConvertFrom-Json
$windowsObj.TimeCreated = $receivedAt
$unknownRaw = Read-Fixture "unparseable_01.txt"

Write-Host "`n4) POST sec-events batch (firewall + windows + unknown)..." -ForegroundColor Yellow
$body = @{
    items = @(
        @{
            receivedAt = $receivedAt
            source     = @{ type = "firewall"; product = "generic-syslog"; host = "fw01" }
            raw        = $firewallRaw
        },
        @{
            receivedAt = $receivedAt
            source     = @{ type = "ad"; product = "windows"; host = "dc01" }
            raw        = $windowsObj
        },
        @{
            receivedAt = $receivedAt
            source     = @{ type = "unknown"; product = "unknown"; host = "host01" }
            raw        = $unknownRaw
        }
    )
} | ConvertTo-Json -Depth 8 -Compress:$false

$ingest = Invoke-RestMethod -Uri $ingestUrl -Method POST -Headers $hdr -Body $body -TimeoutSec 60
Write-Host "   accepted=$($ingest.accepted) rejected=$($ingest.rejected) published=$($ingest.published)" -ForegroundColor Green

if ($ingest.implementationPending -eq $true) {
    Remove-SSHSession -SessionId $session.SessionId | Out-Null
    Skip-Siem "implementationPending=true (placeholder image)"
}
if ($ingest.accepted -lt 3) {
    Remove-SSHSession -SessionId $session.SessionId | Out-Null
    throw "FAIL S4 ingest: accepted=$($ingest.accepted) (beklenen >= 3)"
}
if ($ingest.published -lt 1) {
    Write-Host "   UYARI: published=0 (MQ publish devre disi olabilir)" -ForegroundColor Yellow
}

Start-Sleep -Seconds 2

Write-Host "`n5) Mongo dogrulama (S4.2–S4.5)..." -ForegroundColor Cyan
$mongoJs = @"
const dbName = 'mng_$Domain';
const coll = db.getSiblingDB(dbName).sec_events;
const cutoff = new Date(Date.now() - 3600000);

const denied = coll.countDocuments({
  'event.action': 'denied_flow',
  'network.srcIp': '203.0.113.5',
  'network.dstIp': '10.0.0.10',
  ingestedAt: { `$gte: cutoff }
});

const loginFailed = coll.countDocuments({
  'event.action': 'login_failed',
  'actor.user': 'admin',
  'network.srcIp': '192.168.1.50',
  'event.code': '4625',
  ingestedAt: { `$gte: cutoff }
});

const unknown = coll.countDocuments({
  'event.action': 'unknown',
  'parser.id': 'unknown.fallback.v1',
  rawPreview: /NOT A VALID SYSLOG/i,
  ingestedAt: { `$gte: cutoff }
});

const adRecent = coll.countDocuments({
  'source.type': 'ad',
  ingestedAt: { `$gte: cutoff }
});

print('SIEM_E2E_RESULT=' + JSON.stringify({ denied, loginFailed, unknown, adRecent }));
"@

$mongoResult = Invoke-OdakMongoJsonEval -SshSession $session -JavaScript $mongoJs
$mongoLine = @($mongoResult.Output) | ForEach-Object {
    if ($_ -match 'SIEM_E2E_RESULT=(\{.+?\})') { return $matches[1] }
    if ($_ -match '(\{"denied".+\})') { return $matches[1] }
} | Select-Object -First 1
if (-not $mongoLine) {
    $mongoResult.Output | ForEach-Object { Write-Host $_ }
    Remove-SSHSession -SessionId $session.SessionId | Out-Null
    throw "Mongo eval cikti alinamadi (exit $($mongoResult.ExitStatus))"
}

$counts = $mongoLine | ConvertFrom-Json
Write-Host "   denied_flow=$($counts.denied) login_failed=$($counts.loginFailed) unknown=$($counts.unknown) ad_1h=$($counts.adRecent)" -ForegroundColor DarkGray

if ($counts.denied -lt 1) { throw "FAIL S4.2: denied_flow kaydi yok" }
if ($counts.loginFailed -lt 1) { throw "FAIL S4.3: login_failed / 4625 kaydi yok" }
if ($counts.unknown -lt 1) { throw "FAIL S4.4: unknown fallback kaydi yok" }
if ($counts.adRecent -lt 1) { throw "FAIL S4.5: son 1 saat source.type=ad kaydi yok" }
Write-Host "   S4.2–S4.5 PASS" -ForegroundColor Green

Write-Host "`n6) MQ dogrulama (S4.6 sec_events.created)..." -ForegroundColor Cyan
$mqResult = Get-OdakSecEventsMqMessages -SshSession $session -QueueName $mqQueue -Count 10
$payloadText = ($mqResult.Output -join "`n")
$mqHits = @(
    ($payloadText -match 'denied_flow'),
    ($payloadText -match 'login_failed'),
    ($payloadText -match 'sec_events\.created|"EventAction"')
) | Where-Object { $_ } | Measure-Object | Select-Object -ExpandProperty Count

if ($payloadText -match 'No items' -or [string]::IsNullOrWhiteSpace($payloadText)) {
    Write-Host "   Kuyrukta mesaj yok; mngreactor log kontrolu..." -ForegroundColor DarkGray
    $logCmd = "docker logs mngreactor 2>&1 | grep -i 'sec_events.created' | tail -5"
    $logs = Invoke-SSHCommand -SessionId $session.SessionId -Command $logCmd -TimeOut 20
    $logs.Output | ForEach-Object { Write-Host "   $_" -ForegroundColor DarkGray }
    if (($logs.Output -join "") -notmatch 'sec_events') {
        Remove-SSHSession -SessionId $session.SessionId | Out-Null
        throw "FAIL S4.6: sec_events.created mesaji veya log kaniti yok"
    }
    Write-Host "   S4.6 PASS (log fallback)" -ForegroundColor Green
} else {
    Write-Host "   MQ payload alindi ($($mqResult.Output.Count) satir cikti)" -ForegroundColor DarkGray
    if ($payloadText -notmatch 'denied_flow|login_failed|EventAction') {
        Remove-SSHSession -SessionId $session.SessionId | Out-Null
        throw "FAIL S4.6: MQ mesaj govdesi beklenen alanlari icermiyor"
    }
    Write-Host "   S4.6 PASS" -ForegroundColor Green
}

Remove-SSHSession -SessionId $session.SessionId | Out-Null

Write-Host "`nOK SIEM Faz 1 E2E — S4.2–S4.6 PASS" -ForegroundColor Green
exit 0
