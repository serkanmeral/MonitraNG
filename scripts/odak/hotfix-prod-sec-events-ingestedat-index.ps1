# Prod hotfix: sec_events ingestedAt index (SIEM dashboard-summary performance)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'OdakSshCommon.ps1')
Import-Module Posh-SSH -Force

$Server = '192.168.20.8'
$cred = Get-OdakSshCredential -Server $Server
$session = New-SSHSession -ComputerName $Server -Credential $cred -AcceptKey

Write-Host '=== Prod sec_events index hotfix ===' -ForegroundColor Cyan

$js = @'
const dbName = 'mng_odak';
const coll = db.getSiblingDB(dbName).getCollection('sec_events');

function hasIndex(name) {
  return coll.getIndexes().some(i => i.name === name);
}

const results = [];

if (!hasIndex('idx_ingestedAt_desc')) {
  results.push({ action: 'create', name: 'idx_ingestedAt_desc', ok: coll.createIndex({ ingestedAt: -1 }, { name: 'idx_ingestedAt_desc', background: true }) });
} else {
  results.push({ action: 'skip', name: 'idx_ingestedAt_desc', reason: 'already exists' });
}

if (!hasIndex('idx_ingestedAt_eventAction')) {
  results.push({ action: 'create', name: 'idx_ingestedAt_eventAction', ok: coll.createIndex({ ingestedAt: -1, 'event.action': 1 }, { name: 'idx_ingestedAt_eventAction', background: true }) });
} else {
  results.push({ action: 'skip', name: 'idx_ingestedAt_eventAction', reason: 'already exists' });
}

const indexes = coll.getIndexes().map(i => ({ name: i.name, key: i.key }));
print(JSON.stringify({ results, indexes }, null, 2));
'@

Write-Host 'Creating indexes (background) — may take a few minutes on 3.4M docs...' -ForegroundColor Yellow
$r = Invoke-OdakMongoJsonEval -SshSession $session -JavaScript $js -RemoteAppsDir '/home/odak/MonitraNG/ApplicationResources/mng_apps'
Write-Host ($r.Output -join "`n")

Write-Host "`n=== Verify dashboard-summary latency ===" -ForegroundColor Cyan
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$env:MNG_OC_USE_PROD_TOKEN = '1'
$token = & (Join-Path $repoRoot 'docs/odak/operationcore/scripts/load-operationcore-token.ps1')
$hdr = @{ Authorization = "Bearer $token"; 'X-Domain-Name' = 'odak' }
$Base = 'http://192.168.20.8:5040'
$Ui = 'http://192.168.20.8:3000'

foreach ($label in @('gateway', 'ui-proxy')) {
    $url = if ($label -eq 'gateway') {
        "$Base/reactor/api/v1/sec-events/dashboard-summary?rangeHours=24"
    } else {
        "$Ui/api/reactor/v1/sec-events/dashboard-summary?rangeHours=24"
    }
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    try {
        $resp = Invoke-WebRequest -Uri $url -Headers $hdr -UseBasicParsing -TimeoutSec 60
        $sw.Stop()
        Write-Host "  OK $label in $($sw.Elapsed.TotalSeconds.ToString('F2'))s -> $($resp.StatusCode) len=$($resp.Content.Length)" -ForegroundColor Green
    } catch {
        $sw.Stop()
        $code = if ($_.Exception.Response) { [int]$_.Exception.Response.StatusCode } else { 'ERR' }
        Write-Host "  FAIL $label after $($sw.Elapsed.TotalSeconds.ToString('F2'))s -> $code $($_.Exception.Message)" -ForegroundColor Red
    }
}

Remove-SSHSession -SessionId $session.SessionId | Out-Null
Write-Host "`nHotfix tamamlandi." -ForegroundColor Green
