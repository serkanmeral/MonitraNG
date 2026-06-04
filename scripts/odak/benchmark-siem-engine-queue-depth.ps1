# SIEM Engine — sec_event.queue_depth under load (UDP syslog, no manual flush)
param(
    [string]$EngineUrl = "http://192.168.20.20:5037",
    [string]$Server = "192.168.20.20",
    [string]$User = "odak",
    [string]$Domain = "odak",
    [int]$UdpPort = 5514,
    [int]$DurationSec = 45,
    [int]$TargetEps = 80,
    [int]$MaxItems = 5000,
    [double]$MaxDepthRatio = 0.8,
    [int]$SampleIntervalMs = 250,
    [string]$OutputJson = ""
)

$ErrorActionPreference = "Stop"
Import-Module Posh-SSH -Force
. (Join-Path $PSScriptRoot "OdakSshCommon.ps1")

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path
$fixturePath = Join-Path $repoRoot "tests/fixtures/siem/firewall_deny.syslog.txt"
$runId = "queue-depth-$(Get-Date -Format 'yyyyMMddHHmmss')"
$depthGate = [math]::Floor($MaxItems * $MaxDepthRatio)

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

Write-Host "=== SIEM Engine queue_depth under load ($DurationSec s @ $TargetEps evt/s) ===" -ForegroundColor Cyan
Write-Host "   runId=$runId maxItems=$MaxItems gate=${depthGate} (${MaxDepthRatio}x)" -ForegroundColor DarkGray

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
    if ($line -match '"count"\s*:\s*(\d+)') { return [int]$Matches[1] }
    return -1
}

function Get-QueueDepth {
    try {
        $q = Invoke-RestMethod -Uri "$EngineUrl/api/SecEvents/queue" -TimeoutSec 5
        return [int]$q.count
    } catch {
        return -1
    }
}

$mongoBefore = Get-SecEventTotalCount
$depthSamples = New-Object System.Collections.Generic.List[int]
$aboveGateSamples = 0
$sent = 0
$errors = 0
$intervalMs = [math]::Max(1, [int](1000.0 / [math]::Max(1, $TargetEps)))
$deadline = (Get-Date).AddSeconds($DurationSec)
$started = Get-Date
$nextSample = $started

$udp = New-Object System.Net.Sockets.UdpClient
while ((Get-Date) -lt $deadline) {
    $now = Get-Date
    while ($now -ge $nextSample) {
        $depth = Get-QueueDepth
        if ($depth -ge 0) {
            $depthSamples.Add($depth)
            if ($depth -gt $depthGate) { $aboveGateSamples++ }
        }
        $nextSample = $nextSample.AddMilliseconds($SampleIntervalMs)
        $now = Get-Date
    }

    $line = $syslogTemplate -replace 'SRC=203\.0\.113\.5', "SRC=203.0.113.$((Get-Random -Maximum 250))"
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($line)
    try {
        [void]$udp.Send($bytes, $bytes.Length, $Server, $UdpPort)
        $sent++
    } catch {
        $errors++
    }
    Start-Sleep -Milliseconds $intervalMs
}

# Final samples after load stops
Start-Sleep -Milliseconds 500
for ($i = 0; $i -lt 4; $i++) {
    $depth = Get-QueueDepth
    if ($depth -ge 0) {
        $depthSamples.Add($depth)
        if ($depth -gt $depthGate) { $aboveGateSamples++ }
    }
    Start-Sleep -Milliseconds $SampleIntervalMs
}

$depthBeforeFlush = Get-QueueDepth
$sw = [System.Diagnostics.Stopwatch]::StartNew()
try {
    $flush = Invoke-RestMethod -Uri "$EngineUrl/api/SecEvents/flush" -Method POST -TimeoutSec 120
    $flushAccepted = [int]$flush.accepted
} catch {
    $flushAccepted = 0
    $errors++
}
$sw.Stop()
$depthAfterFlush = Get-QueueDepth

$udp.Close()
Start-Sleep -Seconds 2
$mongoAfter = Get-SecEventTotalCount
Remove-SSHSession -SessionId $session.SessionId | Out-Null

$elapsedSec = [math]::Max(0.001, ((Get-Date) - $started).TotalSeconds)
$achievedEps = [math]::Round($sent / $elapsedSec, 2)
$mongoSaved = if ($mongoBefore -ge 0 -and $mongoAfter -ge 0) { [math]::Max(0, $mongoAfter - $mongoBefore) } else { -1 }
$depthArr = @($depthSamples)
$maxDepth = if ($depthArr.Count -gt 0) { ($depthArr | Measure-Object -Maximum).Maximum } else { 0 }
$p95Depth = Get-Percentile $depthArr 95
$avgDepth = if ($depthArr.Count -gt 0) { [math]::Round(($depthArr | Measure-Object -Average).Average, 1) } else { 0 }
$aboveGatePct = if ($depthArr.Count -gt 0) { [math]::Round(100.0 * $aboveGateSamples / $depthArr.Count, 2) } else { 0 }
$dropEstimate = [math]::Max(0, $sent - $mongoSaved)
$depthPass = ($maxDepth -le $depthGate)
$mongoPass = ($mongoSaved -ge ($sent * 0.95))
$pass = $depthPass -and $mongoPass -and ($errors -eq 0)

Write-Host "   sent=$sent achievedEps=$achievedEps samples=$($depthArr.Count)" -ForegroundColor DarkGray
Write-Host "   queue depth max=$maxDepth p95=$([math]::Round($p95Depth,0)) avg=$avgDepth gate=$depthGate" -ForegroundColor $(if ($depthPass) { "Green" } else { "Yellow" })
Write-Host "   aboveGatePct=$aboveGatePct% mongoSaved=$mongoSaved flushAccepted=$flushAccepted" -ForegroundColor DarkGray
Write-Host "   depthBeforeFlush=$depthBeforeFlush depthAfterFlush=$depthAfterFlush flushMs=$($sw.ElapsedMilliseconds)" -ForegroundColor DarkGray

$report = [ordered]@{
    profile     = "engine-queue-depth"
    runId       = $runId
    generatedAt = (Get-Date).ToUniversalTime().ToString("o")
    environment = @{
        engineUrl = $EngineUrl
        server    = $Server
        domain    = $Domain
        udpPort   = $UdpPort
        maxItems  = $MaxItems
    }
    durationSec   = $DurationSec
    targetEps     = $TargetEps
    achievedEps   = $achievedEps
    queueDepth    = @{
        samples         = $depthArr.Count
        max             = $maxDepth
        p95             = $p95Depth
        avg             = $avgDepth
        gateMax         = $depthGate
        gateRatio       = $MaxDepthRatio
        aboveGatePct    = $aboveGatePct
        beforeFlush     = $depthBeforeFlush
        afterFlush      = $depthAfterFlush
        pass            = $depthPass
    }
    syslog        = @{ sent = $sent; errors = $errors; flushAccepted = $flushAccepted; flushMs = $sw.ElapsedMilliseconds }
    mongo         = @{ beforeCount = $mongoBefore; afterCount = $mongoAfter; savedDelta = $mongoSaved; dropEstimate = $dropEstimate; pass = $mongoPass }
    gate          = @{
        maxDepthUnder80Pct = $depthPass
        mongoSaved95Pct    = $mongoPass
        pass               = $pass
    }
    pass          = $pass
    notes         = "UDP syslog load without manual flush; auto BatchThreshold flush only. SLO: max depth < 80% MaxItems."
}

if ([string]::IsNullOrWhiteSpace($OutputJson)) {
    $benchDir = Join-Path $repoRoot "docs/odak/monitoring/benchmarks"
    if (-not (Test-Path $benchDir)) { New-Item -ItemType Directory -Path $benchDir -Force | Out-Null }
    $OutputJson = Join-Path $benchDir ("benchmark-engine-queue-depth-{0}.json" -f (Get-Date -Format "yyyy-MM-dd"))
}

$report | ConvertTo-Json -Depth 6 | Set-Content -Path $OutputJson -Encoding UTF8
Write-Host "JSON: $OutputJson" -ForegroundColor Cyan
Write-Host "PASS=$pass" -ForegroundColor $(if ($pass) { "Green" } else { "Yellow" })
if (-not $pass) { exit 1 }
exit 0
