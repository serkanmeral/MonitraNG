# SIEM Engine — UDP syslog throughput benchmark (Engine -> Reactor -> Mongo)
param(
    [string]$EngineUrl = "http://192.168.20.20:5037",
    [string]$Server = "192.168.20.20",
    [string]$User = "odak",
    [string]$Domain = "odak",
    [int]$UdpPort = 5514,
    [int]$DurationSec = 60,
    [int]$TargetEps = 30,
    [int]$FlushEvery = 25,
    [string]$OutputJson = ""
)

$ErrorActionPreference = "Stop"
Import-Module Posh-SSH -Force
. (Join-Path $PSScriptRoot "OdakSshCommon.ps1")

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path
$fixturePath = Join-Path $repoRoot "tests/fixtures/siem/firewall_deny.syslog.txt"
$runId = "syslog-bench-$(Get-Date -Format 'yyyyMMddHHmmss')"

function Get-Percentile([double[]]$Values, [double]$Percentile) {
    if ($Values.Count -eq 0) { return $null }
    $sorted = @($Values | Sort-Object)
    $rank = [math]::Ceiling(($Percentile / 100.0) * $sorted.Count) - 1
    if ($rank -lt 0) { $rank = 0 }
    if ($rank -ge $sorted.Count) { $rank = $sorted.Count - 1 }
    return $sorted[$rank]
}

if (-not (Test-Path $fixturePath)) { throw "Fixture eksik: $fixturePath" }
$syslogTemplate = (Get-Content $fixturePath -Raw).TrimEnd()

Write-Host "=== SIEM Engine syslog UDP benchmark ($DurationSec s @ $TargetEps evt/s) ===" -ForegroundColor Cyan
Write-Host "   runId=$runId port=$UdpPort" -ForegroundColor DarkGray

try {
    Invoke-RestMethod -Uri "$EngineUrl/swagger/index.html" -TimeoutSec 8 | Out-Null
} catch {
    throw "Engine ayakta degil: $EngineUrl"
}

$cred = Get-OdakSshCredential -User $User -Server $Server
$session = New-SSHSession -ComputerName $Server -Credential $cred -AcceptKey

function Get-SecEventTotalCount {
    $js = @"
const coll = db.getSiblingDB('mng_$Domain').sec_events;
print(JSON.stringify({ count: coll.countDocuments({}) }));
"@
    $r = Invoke-OdakMongoJsonEval -SshSession $session -JavaScript $js
    $line = @($r.Output) | Where-Object { $_ -match '"count"' } | Select-Object -Last 1
    if (-not $line) { return -1 }
    if ($line -match '"count"\s*:\s*(\d+)') { return [int]$Matches[1] }
    return -1
}

$mongoBefore = Get-SecEventTotalCount
$udp = New-Object System.Net.Sockets.UdpClient
$flushLatencies = New-Object System.Collections.Generic.List[double]
$sent = 0
$flushAccepted = 0
$errors = 0
$intervalMs = [math]::Max(1, [int](1000.0 / [math]::Max(1, $TargetEps)))
$deadline = (Get-Date).AddSeconds($DurationSec)
$started = Get-Date
$sinceFlush = 0

while ((Get-Date) -lt $deadline) {
    $line = $syslogTemplate -replace 'SRC=203\.0\.113\.5', "SRC=203.0.113.$((Get-Random -Maximum 250))"
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($line)
    try {
        [void]$udp.Send($bytes, $bytes.Length, $Server, $UdpPort)
        $sent++
        $sinceFlush++
    } catch {
        $errors++
    }

    if ($sinceFlush -ge $FlushEvery) {
        $sw = [System.Diagnostics.Stopwatch]::StartNew()
        try {
            $flush = Invoke-RestMethod -Uri "$EngineUrl/api/SecEvents/flush" -Method POST -TimeoutSec 120
            $sw.Stop()
            $flushLatencies.Add([double]$sw.ElapsedMilliseconds)
            $flushAccepted += [int]$flush.accepted
        } catch {
            $sw.Stop()
            $errors++
        }
        $sinceFlush = 0
    }

    Start-Sleep -Milliseconds $intervalMs
}

if ($sinceFlush -gt 0) {
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    try {
        $flush = Invoke-RestMethod -Uri "$EngineUrl/api/SecEvents/flush" -Method POST -TimeoutSec 120
        $sw.Stop()
        $flushLatencies.Add([double]$sw.ElapsedMilliseconds)
        $flushAccepted += [int]$flush.accepted
    } catch {
        $errors++
    }
}

$udp.Close()
Start-Sleep -Seconds 3
$mongoAfter = Get-SecEventTotalCount
Remove-SSHSession -SessionId $session.SessionId | Out-Null

$elapsedSec = [math]::Max(0.001, ((Get-Date) - $started).TotalSeconds)
$achievedEps = [math]::Round($sent / $elapsedSec, 2)
$mongoSaved = if ($mongoBefore -ge 0 -and $mongoAfter -ge 0) { [math]::Max(0, $mongoAfter - $mongoBefore) } else { -1 }
$flushMs = @($flushLatencies)
$flushP95 = Get-Percentile $flushMs 95
$minTarget = [math]::Round($TargetEps * 0.5, 2)
$pass = ($achievedEps -ge $minTarget) -and ($errors -eq 0) -and ($mongoSaved -ge ($sent * 0.9))

Write-Host "   sent=$sent flushAccepted=$flushAccepted achievedEps=$achievedEps mongoSaved=$mongoSaved" -ForegroundColor $(if ($pass) { "Green" } else { "Yellow" })
Write-Host "   flush P95=$([math]::Round($flushP95,0))ms errors=$errors" -ForegroundColor DarkGray

$report = [ordered]@{
    profile     = "engine-syslog"
    runId       = $runId
    generatedAt = (Get-Date).ToUniversalTime().ToString("o")
    environment = @{ engineUrl = $EngineUrl; server = $Server; domain = $Domain; udpPort = $UdpPort }
    durationSec = $DurationSec
    targetEps   = $TargetEps
    achievedEps = $achievedEps
    syslog      = @{ sent = $sent; flushAccepted = $flushAccepted; flushP95Ms = $flushP95; errors = $errors }
    mongo       = @{ beforeCount = $mongoBefore; afterCount = $mongoAfter; savedDelta = $mongoSaved }
    pass        = $pass
    notes       = "UDP syslog -> Engine queue -> flush -> Reactor. Lab hedef: >= %50 target EPS, mongo ~sent."
}

if ([string]::IsNullOrWhiteSpace($OutputJson)) {
    $benchDir = Join-Path $repoRoot "docs/odak/monitoring/benchmarks"
    if (-not (Test-Path $benchDir)) { New-Item -ItemType Directory -Path $benchDir -Force | Out-Null }
    $OutputJson = Join-Path $benchDir ("benchmark-engine-syslog-{0}.json" -f (Get-Date -Format "yyyy-MM-dd"))
}

$report | ConvertTo-Json -Depth 6 | Set-Content -Path $OutputJson -Encoding UTF8
Write-Host "JSON: $OutputJson" -ForegroundColor Cyan
Write-Host "PASS=$pass" -ForegroundColor $(if ($pass) { "Green" } else { "Yellow" })
if (-not $pass) { exit 1 }
exit 0
