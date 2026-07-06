# Faz 2.2: sec_events -> sec_events_hourly_rollup backfill (prod/test)
param(
    [string]$Server = '192.168.20.8',
    [string]$Domain = 'odak',
    [int]$RangeHours = 168,
    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'OdakSshCommon.ps1')
Import-Module Posh-SSH -Force

Initialize-OdakSshEnvironment -Server $Server
$cred = Get-OdakSshCredential -Server $Server
$session = New-SSHSession -ComputerName $Server -Credential $cred -AcceptKey

$dbName = "mng_$Domain"
$normalizedDomain = $Domain.ToLowerInvariant()
Write-Host "=== Rollup backfill ($dbName, son ${RangeHours}s) ===" -ForegroundColor Cyan
if ($DryRun) { Write-Host 'DRY RUN — yazim yok' -ForegroundColor Yellow }

$js = @"
const dbName = '$dbName';
const domain = '$normalizedDomain';
const hours = $RangeHours;
const dryRun = $($DryRun.IsPresent.ToString().ToLower());
const from = new Date(Date.now() - hours * 3600 * 1000);
const src = db.getSiblingDB(dbName).sec_events;
const dst = db.getSiblingDB(dbName).sec_events_hourly_rollup;

dst.createIndex({ domain: 1, hourStart: 1 }, { name: 'idx_domain_hourStart', background: true });

const actionRows = src.aggregate([
  { `$match: { ingestedAt: { `$gte: from }, 'event.action': { `$ne: 'unknown' } } },
  { `$group: {
      _id: {
        hour: { `$dateTrunc: { date: '`$ingestedAt', unit: 'hour', timezone: 'UTC' } },
        action: '`$event.action'
      },
      count: { `$sum: 1 }
  }}
]).toArray();

const newFlowRows = src.aggregate([
  { `$match: { ingestedAt: { `$gte: from }, baseline: { newFlowPair: true } } },
  { `$group: {
      _id: { `$dateTrunc: { date: '`$ingestedAt', unit: 'hour', timezone: 'UTC' } },
      count: { `$sum: 1 }
  }}
]).toArray();

const byHour = {};
for (const row of actionRows) {
  const hour = row._id.hour;
  const key = hour.toISOString();
  if (!byHour[key]) {
    byHour[key] = { hourStart: hour, eventsTotal: 0, newFlowCount: 0, byAction: {} };
  }
  byHour[key].eventsTotal += row.count;
  byHour[key].byAction[row._id.action] = (byHour[key].byAction[row._id.action] || 0) + row.count;
}
for (const row of newFlowRows) {
  const key = row._id.toISOString();
  if (!byHour[key]) {
    byHour[key] = { hourStart: row._id, eventsTotal: 0, newFlowCount: 0, byAction: {} };
  }
  byHour[key].newFlowCount = row.count;
}

const ops = [];
for (const key of Object.keys(byHour).sort()) {
  const bucket = byHour[key];
  const id = domain + '|' + bucket.hourStart.toISOString();
  ops.push({
    replaceOne: {
      filter: { _id: id },
      replacement: {
        _id: id,
        domain,
        hourStart: bucket.hourStart,
        eventsTotal: bucket.eventsTotal,
        newFlowCount: bucket.newFlowCount,
        byAction: bucket.byAction,
        updatedAt: new Date()
      },
      upsert: true
    }
  });
}

let written = 0;
if (!dryRun && ops.length > 0) {
  const CHUNK = 500;
  for (let i = 0; i < ops.length; i += CHUNK) {
    const chunk = ops.slice(i, i + CHUNK);
    const r = dst.bulkWrite(chunk, { ordered: false });
    written += (r.upsertedCount || 0) + (r.modifiedCount || 0);
  }
}

print(JSON.stringify({
  dbName,
  rangeHours: hours,
  from: from.toISOString(),
  hourBuckets: ops.length,
  dryRun,
  written
}, null, 2));
"@

Write-Host 'Aggregation calisiyor (buyuk koleksiyonda birkaç dakika)...' -ForegroundColor Yellow
$r = Invoke-OdakMongoJsonEval -SshSession $session -JavaScript $js
$raw = ($r.Output -join "`n").Trim()
$jsonLine = if ($raw -match '(\{[\s\S]*\})\s*$') { $Matches[1] } else { $raw }
if (-not $jsonLine -or $jsonLine.Length -lt 10) {
    Write-Host ($r.Output -join "`n") -ForegroundColor Red
    throw 'Backfill ciktisi alinamadi'
}

$result = $jsonLine | ConvertFrom-Json
Write-Host "Saat bucket: $($result.hourBuckets) | Yazilan: $($result.written) | DryRun: $($result.dryRun)" -ForegroundColor Green

Remove-SSHSession -SessionId $session.SessionId | Out-Null
Write-Host "`nBackfill tamam. Deploy sonrasi dashboard-summary rollup kullanir." -ForegroundColor Green
