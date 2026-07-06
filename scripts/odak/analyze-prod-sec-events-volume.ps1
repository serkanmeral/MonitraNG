# Faz 3.1: Prod sec_events hacim analizi (FortiGate allow/deny oranı, IT karar desteği)
param(
    [string]$Server = '192.168.20.8',
    [string]$Domain = 'odak',
    [int]$RangeHours = 24
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'OdakSshCommon.ps1')
Import-Module Posh-SSH -Force

Initialize-OdakSshEnvironment -Server $Server
$cred = Get-OdakSshCredential -Server $Server
$session = New-SSHSession -ComputerName $Server -Credential $cred -AcceptKey

$dbName = "mng_$Domain"
Write-Host "=== SIEM hacim analizi ($Domain, son ${RangeHours}s) ===" -ForegroundColor Cyan

$js = @"
const dbName = '$dbName';
const hours = $RangeHours;
const from = new Date(Date.now() - hours * 3600 * 1000);
const coll = db.getSiblingDB(dbName).sec_events;

const total = coll.countDocuments({ ingestedAt: { `$gte: from } });
const byAction = coll.aggregate([
  { `$match: { ingestedAt: { `$gte: from }, 'event.action': { `$ne: 'unknown' } } },
  { `$group: { _id: '`$event.action', count: { `$sum: 1 } } },
  { `$sort: { count: -1 } },
  { `$limit: 20 }
]).toArray();

const fortigate = coll.aggregate([
  { `$match: { ingestedAt: { `$gte: from }, 'source.type': 'firewall' } },
  { `$group: { _id: '`$event.action', count: { `$sum: 1 } } },
  { `$sort: { count: -1 } }
]).toArray();

const allowed = byAction.find(x => x._id === 'allowed_flow')?.count || 0;
const denied = byAction.find(x => x._id === 'denied_flow')?.count || 0;
const fgAllowed = fortigate.find(x => x._id === 'allowed_flow')?.count || 0;
const fgDenied = fortigate.find(x => x._id === 'denied_flow')?.count || 0;

print(JSON.stringify({
  dbName,
  rangeHours: hours,
  from: from.toISOString(),
  eventsTotal: total,
  topActions: byAction,
  fortigateByAction: fortigate,
  allowDenyRatio: {
    allowed_flow: allowed,
    denied_flow: denied,
    fortigate_allowed: fgAllowed,
    fortigate_denied: fgDenied,
    allowPct: total > 0 ? Math.round(1000 * allowed / total) / 10 : 0
  },
  recommendation: fgAllowed > fgDenied * 10
    ? 'FortiGate allow log acik olabilir — IT ile deny-only veya 1/N sample onerilir (Faz 3.1)'
    : 'Hacim profili makul; FortiGate deny-only acil degil'
}, null, 2));
"@

$r = Invoke-OdakMongoJsonEval -SshSession $session -JavaScript $js
$raw = ($r.Output -join "`n").Trim()
$jsonLine = if ($raw -match '(\{[\s\S]*\})\s*$') { $Matches[1] } else { $raw }
if (-not $jsonLine -or $jsonLine.Length -lt 10) {
    Write-Host ($r.Output -join "`n") -ForegroundColor Red
    throw 'Mongo analiz ciktisi alinamadi'
}

$report = $jsonLine | ConvertFrom-Json
Write-Host "`nToplam olay: $($report.eventsTotal)" -ForegroundColor White
Write-Host "allowed_flow: $($report.allowDenyRatio.allowed_flow) | denied_flow: $($report.allowDenyRatio.denied_flow)" -ForegroundColor Yellow
Write-Host "FortiGate allowed: $($report.allowDenyRatio.fortigate_allowed) | denied: $($report.allowDenyRatio.fortigate_denied)" -ForegroundColor Yellow
Write-Host "`nTop aksiyonlar:" -ForegroundColor Cyan
$report.topActions | ForEach-Object { Write-Host "  $($_.'_id'): $($_.count)" }

Write-Host "`n--- IT onerisi ---" -ForegroundColor Magenta
Write-Host $report.recommendation -ForegroundColor $(if ($report.recommendation -match 'allow log') { 'Red' } else { 'Green' })

Write-Host "`nFortiGate deny-only checklist icin:" -ForegroundColor DarkGray
Write-Host "  pwsh -File .\scripts\odak\fortigate-deny-only-it-checklist.ps1" -ForegroundColor DarkGray

Remove-SSHSession -SessionId $session.SessionId | Out-Null
Write-Host "`nAnaliz tamam." -ForegroundColor Green
