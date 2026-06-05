# SIEM lab sifirlama — test sec_events, alarm kayitlari, E2E kurallari; operasyonel paket yeniden yuklenir
param(
    [string]$Gateway = "http://192.168.20.20:5040",
    [string]$Server = "192.168.20.20",
    [string]$Domain = "odak",
    [switch]$Apply,
    [switch]$SkipReseed,
    [switch]$SkipQueues
)

$ErrorActionPreference = "Stop"
Import-Module Posh-SSH -Force -ErrorAction Stop
. (Join-Path $PSScriptRoot "OdakSshCommon.ps1")

Write-Host "=== SIEM lab reset ($Domain) ===" -ForegroundColor Cyan
if (-not $Apply) {
    Write-Host "   Dry-run — silme/yukleme icin -Apply" -ForegroundColor Yellow
}

# 1) E2E + benchmark alarm kurallari (siem-mvp-v1 korunur)
Write-Host "`n1) E2E / benchmark alarm kurallari..." -ForegroundColor Cyan
$purgeArgs = @{ Gateway = $Gateway; Domain = $Domain }
if ($Apply) { $purgeArgs.Apply = $true }
& (Join-Path $PSScriptRoot "purge-siem-e2e-alarm-rules.ps1") @purgeArgs
if ($LASTEXITCODE -ne 0) { throw "purge-siem-e2e-alarm-rules failed" }

# 2) Mongo — sec_events, baseline, alarm instance/state
Write-Host "`n2) Mongo SIEM verileri..." -ForegroundColor Cyan
Initialize-OdakSshEnvironment -Server $Server
$cred = Get-OdakSshCredential -Server $Server
$session = New-SSHSession -ComputerName $Server -Credential $cred -AcceptKey

try {
    $dbName = "mng_$Domain"
    $collections = @(
        "sec_events",
        "sec_flow_baseline",
        "@mon_alarms",
        "@mon_alarm_correlation_windows",
        "@mon_alarm_observation_activity"
    )

    $mongoJs = @"
const dbName = '$dbName';
const names = $($collections | ConvertTo-Json -Compress);
const dbx = db.getSiblingDB(dbName);
const out = {};
for (const name of names) {
  const coll = dbx.getCollection(name);
  const before = coll.countDocuments();
  out[name] = { before, deleted: 0 };
  if ($($Apply.IsPresent.ToString().ToLower())) {
    const r = coll.deleteMany({});
    out[name].deleted = r.deletedCount;
    out[name].after = coll.countDocuments();
  }
}
print(JSON.stringify(out));
"@

    $r = Invoke-OdakMongoJsonEval -SshSession $session -JavaScript $mongoJs
    $text = ($r.Output -join "`n")
    if ($text -match '(\{"sec_events".+\})') {
        $stats = $Matches[1] | ConvertFrom-Json
        foreach ($prop in $stats.PSObject.Properties) {
            $c = $prop.Name
            $v = $prop.Value
            if ($Apply) {
                Write-Host "   $c : $($v.before) -> $($v.after) (silinen $($v.deleted))" -ForegroundColor Green
            } else {
                Write-Host "   WOULD CLEAR $c docs=$($v.before)" -ForegroundColor DarkGray
            }
        }
    } else {
        Write-Host "   Mongo cikti okunamadi:" -ForegroundColor Yellow
        $r.Output | ForEach-Object { Write-Host "   $_" -ForegroundColor DarkGray }
    }
}
finally {
    Remove-SSHSession -SessionId $session.SessionId | Out-Null
}

# 3) MQ kuyruklari
if (-not $SkipQueues) {
    Write-Host "`n3) Workflow / observation kuyruklari..." -ForegroundColor Cyan
    $qArgs = @{ Server = $Server }
    if ($Apply) { $qArgs.Apply = $true }
    & (Join-Path $PSScriptRoot "purge-workflow-queues.ps1") @qArgs
}

# 4) Operasyonel paket
if (-not $SkipReseed) {
    Write-Host "`n4) siem-mvp-v1 paket kurallari..." -ForegroundColor Cyan
    if ($Apply) {
        & (Join-Path $PSScriptRoot "seed-siem-alarm-rule-pack.ps1") -Gateway $Gateway -Domain $Domain -Replace
        if ($LASTEXITCODE -ne 0) { throw "seed-siem-alarm-rule-pack failed" }
    } else {
        Write-Host "   WOULD RUN seed-siem-alarm-rule-pack.ps1 -Replace" -ForegroundColor DarkGray
    }
}

Write-Host ""
if ($Apply) {
    Write-Host "OK SIEM lab reset tamam — kendi log/alarmlariniz icin hazir" -ForegroundColor Green
} else {
    Write-Host "OK dry-run tamam (-Apply ile uygula)" -ForegroundColor Green
}
