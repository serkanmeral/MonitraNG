# SIEM P0 baseline — Reactor sec-events ingest throughput + opsiyonel U1 detection lag
param(
    [string]$Gateway = "http://192.168.20.20:5040",
    [string]$Server = "192.168.20.20",
    [string]$User = "odak",
    [string]$Domain = "odak",
    [string]$Profile = "P0",
    [int]$DurationSec = 30,
    [int]$TargetEps = 20,
    [int]$BatchSize = 1,
    [double]$MinAchievedEpsRatio = 0.8,
    [int]$IngestP95TargetMs = 1000,
    [double]$MaxErrorRate = 0.05,
    [double]$MaxDropRate = 0.05,
    [switch]$Soak,
    [switch]$IncludeDetectionLag,
    [string]$OutputJson = ""
)

if ($Soak) {
    $DurationSec = 300
    $TargetEps = 50
    $BatchSize = 5
    $MinAchievedEpsRatio = 0.8
    $IncludeDetectionLag = $false
}

$ErrorActionPreference = "Stop"
Import-Module Posh-SSH -Force
. (Join-Path $PSScriptRoot "OdakSshCommon.ps1")

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path
$fixtureDir = Join-Path $repoRoot "tests/fixtures/siem"
$ingestUrl = "$Gateway/reactor/api/v1/ingest/sec-events"
$alarmUrl = "$Gateway/alarm/api/v1"
$runId = "bench-$Profile-$(Get-Date -Format 'yyyyMMddHHmmss')"

function Get-Percentile([double[]]$Values, [double]$Percentile) {
    if ($Values.Count -eq 0) { return $null }
    $sorted = @($Values | Sort-Object)
    $rank = [math]::Ceiling(($Percentile / 100.0) * $sorted.Count) - 1
    if ($rank -lt 0) { $rank = 0 }
    if ($rank -ge $sorted.Count) { $rank = $sorted.Count - 1 }
    return $sorted[$rank]
}

function Read-Fixture([string]$Name) {
    $path = Join-Path $fixtureDir $Name
    if (-not (Test-Path $path)) { throw "Fixture eksik: $path" }
    return (Get-Content -Path $path -Raw).TrimEnd()
}

function New-IngestBody([string]$Kind) {
    $receivedAt = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
    if ($Kind -eq "firewall") {
        $line = (Read-Fixture "firewall_deny.syslog.txt") -replace 'SRC=203\.0\.113\.5', "SRC=203.0.113.$((Get-Random -Maximum 250))"
        return @{
            receivedAt = $receivedAt
            source     = @{ type = "firewall"; product = "generic-syslog"; host = "$runId-fw" }
            raw        = $line
        }
    }
    $obj = Read-Fixture "windows_4625_failed_logon.json" | ConvertFrom-Json
    $obj.TimeCreated = $receivedAt
    $obj.TargetUserName = "$runId-user"
    $obj.IpAddress = "10.77.$((Get-Random -Maximum 250)).$(Get-Random -Maximum 250)"
    return @{
        receivedAt = $receivedAt
        source     = @{ type = "ad"; product = "windows"; host = "$runId-dc" }
        raw        = $obj
    }
}

Write-Host "=== SIEM $Profile $(if ($Soak) { 'soak' } else { 'baseline' }) ($DurationSec s @ target ${TargetEps} evt/s, batch=$BatchSize) ===" -ForegroundColor Cyan
Write-Host "   runId=$runId" -ForegroundColor DarkGray

$tokenScript = Join-Path $PSScriptRoot "..\tests\MngDataGateway\auth\get-token.ps1"
$token = & $tokenScript -KeeperBaseUrl $Gateway -DomainName $Domain -Username "odak_admin" -Password "Admin123!"
$hdr = @{ Authorization = "Bearer $token"; "X-Domain-Name" = $Domain; "Content-Type" = "application/json" }

$cred = Get-OdakSshCredential -User $User -Server $Server
$session = New-SSHSession -ComputerName $Server -Credential $cred -AcceptKey
$ingestStarted = Get-Date

function Get-SecEventTotalCount {
    $js = @"
const coll = db.getSiblingDB('mng_$Domain').sec_events;
print(JSON.stringify({ count: coll.countDocuments({}) }));
"@
    $r = Invoke-OdakMongoJsonEval -SshSession $session -JavaScript $js
    $line = @($r.Output) | Where-Object { $_ -match '"count"' } | Select-Object -Last 1
    if (-not $line) { return -1 }
    if ($line -match '"count"\s*:\s*(\d+)') { return [int]$Matches[1] }
    try { return [int]($line | ConvertFrom-Json).count } catch { return -1 }
}

$mongoBefore = Get-SecEventTotalCount
Write-Host "`n1) Ingest load (mongo total before=$mongoBefore)..." -ForegroundColor Yellow

$latencies = New-Object System.Collections.Generic.List[double]
$errors = 0
$accepted = 0
$rejected = 0
$eventsAttempted = 0
$requestsPerSec = [math]::Max(1, [math]::Ceiling($TargetEps / [math]::Max(1, $BatchSize)))
$intervalMs = [math]::Max(1, [int](1000.0 / $requestsPerSec))
$deadline = (Get-Date).AddSeconds($DurationSec)
$sendIndex = 0
$nextProgress = (Get-Date).AddSeconds(30)

while ((Get-Date) -lt $deadline) {
    $items = @()
    for ($b = 0; $b -lt $BatchSize; $b++) {
        $kind = if ((($sendIndex + $b) % 10) -lt 7) { "firewall" } else { "login_failed" }
        $items += New-IngestBody $kind
    }
    $eventsAttempted += $items.Count
    $body = (@{ items = $items } | ConvertTo-Json -Depth 8)

    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    try {
        $resp = Invoke-RestMethod -Uri $ingestUrl -Method POST -Headers $hdr -Body $body -TimeoutSec 30
        $sw.Stop()
        $latencies.Add([double]$sw.ElapsedMilliseconds)
        $accepted += [int]$resp.accepted
        $rejected += [int]$resp.rejected
        if ($resp.accepted -lt 1) { $errors++ }
    } catch {
        $sw.Stop()
        $latencies.Add([double]$sw.ElapsedMilliseconds)
        $errors++
    }

    $sendIndex++
    if ((Get-Date) -ge $nextProgress) {
        $elapsed = [math]::Max(0.001, ((Get-Date) - $ingestStarted).TotalSeconds)
        $epsSoFar = [math]::Round($accepted / $elapsed, 1)
        Write-Host "   ... ${elapsed}s elapsed, accepted=$accepted (~${epsSoFar} evt/s)" -ForegroundColor DarkGray
        $nextProgress = (Get-Date).AddSeconds(30)
    }
    Start-Sleep -Milliseconds $intervalMs
}

$ingestEnded = Get-Date
$elapsedSec = [math]::Max(0.001, ($ingestEnded - $ingestStarted).TotalSeconds)
Start-Sleep -Seconds 3
$mongoAfter = Get-SecEventTotalCount
if ($mongoBefore -lt 0 -or $mongoAfter -lt 0) {
    $mongoSaved = $accepted
    $mongoVerify = "fallback_accepted"
} else {
    $mongoSaved = [math]::Max(0, $mongoAfter - $mongoBefore)
    $mongoVerify = "total_count_delta"
}

$ms = @($latencies)
$p50 = Get-Percentile $ms 50
$p95 = Get-Percentile $ms 95
$maxMs = if ($ms.Count -gt 0) { ($ms | Measure-Object -Maximum).Maximum } else { $null }
$achievedEps = [math]::Round($accepted / $elapsedSec, 2)
$minAchievedEps = [math]::Round($TargetEps * $MinAchievedEpsRatio, 2)
$errorRate = if ($sendIndex -gt 0) { [math]::Round($errors / $sendIndex, 4) } else { 0 }
$dropRate = if ($eventsAttempted -gt 0) {
    [math]::Round(($eventsAttempted - $accepted) / $eventsAttempted, 4)
} else { 0 }
$ingestPass = ($null -ne $p95) -and ($p95 -le $IngestP95TargetMs) -and ($errorRate -le $MaxErrorRate) -and ($dropRate -le $MaxDropRate)
$throughputPass = $achievedEps -ge $minAchievedEps
$gatePass = $ingestPass -and $throughputPass

Write-Host "   requests=$sendIndex events=$eventsAttempted accepted=$accepted rejected=$rejected achievedEps=$achievedEps (min=$minAchievedEps)" -ForegroundColor Green
Write-Host "   ingest P50=$([math]::Round($p50,0))ms P95=$([math]::Round($p95,0))ms max=$maxMs ms errorRate=$errorRate dropRate=$dropRate" -ForegroundColor $(if ($ingestPass) { "Green" } else { "Yellow" })
Write-Host "   mongo saved=$mongoSaved (after=$mongoAfter)" -ForegroundColor DarkGray

$detection = $null
if ($IncludeDetectionLag) {
    Write-Host "`n2) U1 detection lag (3x login_failed)..." -ForegroundColor Yellow
    $rule = Invoke-RestMethod -Uri "$alarmUrl/rules" -Method POST -Headers $hdr -Body (@{
        name             = "Bench lag $runId"
        type             = "correlation"
        matchKey         = "login_failed"
        groupByFields    = @("userId", "srcIp")
        windowMinutes    = 5
        threshold        = 3
        severity         = 7
        cooldownMinutes  = 0
        dedupKeyTemplate = "{ruleId}:{groupKey}"
    } | ConvertTo-Json -Depth 5)

    $lagUser = "$runId-lag"
    $lagIp = "10.66.0.$((Get-Random -Maximum 250))"
    $failRaw = Read-Fixture "windows_4625_failed_logon.json"
    $lagStart = Get-Date

    for ($i = 0; $i -lt 3; $i++) {
        $obj = $failRaw | ConvertFrom-Json
        $obj.TimeCreated = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
        $obj.TargetUserName = $lagUser
        $obj.IpAddress = $lagIp
        $body = @{
            items = @(@{
                receivedAt = $obj.TimeCreated
                source     = @{ type = "ad"; product = "windows"; host = "$runId-lag-dc" }
                raw        = $obj
            })
        } | ConvertTo-Json -Depth 8
        Invoke-RestMethod -Uri $ingestUrl -Method POST -Headers $hdr -Body $body -TimeoutSec 30 | Out-Null
        Start-Sleep -Milliseconds 200
    }

    $lagMs = $null
    for ($i = 0; $i -lt 30; $i++) {
        Start-Sleep -Seconds 1
        $page = Invoke-RestMethod -Uri "$alarmUrl/alarms?openOnly=true&minSeverity=7" -Headers $hdr
        $items = @($page.items)
        if ($items.Count -eq 0 -and $page -is [array]) { $items = @($page) }
        $match = $items | Where-Object { $_.ruleId -eq $rule.id } | Select-Object -First 1
        if ($match) {
            $lagMs = [long](((Get-Date) - $lagStart).TotalMilliseconds)
            break
        }
    }

    $detection = @{
        ruleId           = $rule.id
        lagMs            = $lagMs
        targetLagP95Ms   = 60000
        pass             = ($null -ne $lagMs) -and ($lagMs -le 60000)
    }
    Write-Host "   detection lagMs=$lagMs (target <=60000)" -ForegroundColor $(if ($detection.pass) { "Green" } else { "Red" })
}

Remove-SSHSession -SessionId $session.SessionId | Out-Null

$overallPass = $gatePass -and (($null -eq $detection) -or $detection.pass)

$report = [ordered]@{
    profile      = $Profile
    mode         = if ($Soak) { "soak" } else { "baseline" }
    runId        = $runId
    generatedAt  = (Get-Date).ToUniversalTime().ToString("o")
    environment  = @{
        gateway   = $Gateway
        domain    = $Domain
        server    = $Server
    }
    durationSec  = $DurationSec
    targetEps    = $TargetEps
    batchSize    = $BatchSize
    achievedEps  = $achievedEps
    gate         = @{
        minAchievedEps   = $minAchievedEps
        maxDropRate      = $MaxDropRate
        ingestP95TargetMs = $IngestP95TargetMs
        throughputPass   = $throughputPass
        ingestPass       = $ingestPass
        pass             = $gatePass
    }
    ingest       = @{
        requests      = $sendIndex
        eventsAttempted = $eventsAttempted
        accepted      = $accepted
        rejected      = $rejected
        errorRate     = $errorRate
        dropRate      = $dropRate
        p50Ms         = $p50
        p95Ms         = $p95
        maxMs         = $maxMs
        p95TargetMs   = $IngestP95TargetMs
        pass          = $ingestPass
    }
    mongo        = @{
        beforeCount  = $mongoBefore
        afterCount   = $mongoAfter
        savedDelta   = $mongoSaved
        verifyMethod = $mongoVerify
    }
    detectionLag = $detection
    pass         = $overallPass
    notes        = if ($Soak) {
        "P0 soak kapisi: 5dk, hedef 50 evt/s (%80), drop<5%, ingest P95<1s."
    } else {
        "P0 lab baseline; tam soak icin -Soak kullanin."
    }
}

if ([string]::IsNullOrWhiteSpace($OutputJson)) {
    $benchDir = Join-Path $repoRoot "docs/odak/monitoring/benchmarks"
    if (-not (Test-Path $benchDir)) { New-Item -ItemType Directory -Path $benchDir -Force | Out-Null }
    $dateTag = (Get-Date -Format "yyyy-MM-dd")
    $suffix = if ($Soak) { "soak" } else { $Profile }
    $OutputJson = Join-Path $benchDir "benchmark-$suffix-$dateTag.json"
}

$report | ConvertTo-Json -Depth 6 | Set-Content -Path $OutputJson -Encoding UTF8
Write-Host "`nJSON: $OutputJson" -ForegroundColor Cyan
Write-Host "PASS=$overallPass" -ForegroundColor $(if ($overallPass) { "Green" } else { "Yellow" })

if (-not $overallPass) { exit 1 }
exit 0
