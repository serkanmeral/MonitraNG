# SIEM U4 — firewall deny sec_events → alarm.raised → Workflow Event Trigger E2E
param(
    [string]$Gateway = "http://192.168.20.20:5040",
    [string]$Domain = "odak",
    [int]$Threshold = 3
)

$ErrorActionPreference = "Stop"
$tokenScript = Join-Path $PSScriptRoot "..\tests\MngDataGateway\auth\get-token.ps1"
$token = & $tokenScript -KeeperBaseUrl $Gateway -DomainName $Domain -Username "odak_admin" -Password "Admin123!"
$hdr = @{ Authorization = "Bearer $token"; "X-Domain-Name" = $Domain; "Content-Type" = "application/json" }
$wf = "$Gateway/workflow/api/v1"
$alarm = "$Gateway/alarm/api/v1"
$reactor = "$Gateway/reactor/api/v1/ingest/sec-events"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path
$fixtureDir = Join-Path $repoRoot "tests/fixtures/siem"
$ruleFixture = Join-Path $fixtureDir "alarm_rules/u4_firewall_deny_spike.json"

function Read-Fixture([string]$Name) {
    $path = Join-Path $fixtureDir $Name
    if (-not (Test-Path $path)) { throw "Fixture eksik: $path" }
    return (Get-Content -Path $path -Raw).TrimEnd()
}

Write-Host "=== SIEM U4 alarm.raised -> Workflow E2E ===" -ForegroundColor Cyan

Write-Host "`n1) Workflow (alarm.raised + denied_flow filter -> write.log)..." -ForegroundColor Cyan
$wfKey = "siem-u4-workflow-e2e"
$defs = @((Invoke-RestMethod -Uri "$wf/definitions" -Headers $hdr))
$def = $defs | Where-Object { $_.key -eq $wfKey } | Select-Object -First 1
if (-not $def) {
    $def = Invoke-RestMethod -Uri "$wf/definitions" -Method POST -Headers $hdr -Body (@{
        key  = $wfKey
        name = "SIEM U4 Workflow E2E"
    } | ConvertTo-Json)
}

$verBody = @{
    entryNodeId = "manual_1"
    nodes       = @(
        @{ id = "manual_1"; type = "manual.trigger"; config = @{} },
        @{
            id     = "log_1"
            type   = "write.log"
            config = @{
                message = "SIEM U4 deny dstIp={{event.context.dstIp}} srcIp={{event.context.srcIp}} count={{event.context.windowCount}} severity={{event.severity}}"
            }
        }
    )
    edges = @(@{ fromNodeId = "manual_1"; toNodeId = "log_1"; edgeKey = "default" })
    triggers = @(@{
        type             = "event"
        enabled          = $true
        filterExpression = "event.severity >= 6 && event.context.key == 'denied_flow'"
        config           = @{ eventType = "alarm.raised" }
    })
} | ConvertTo-Json -Depth 10

$ver = Invoke-RestMethod -Uri "$wf/definitions/$($def.id)/versions" -Method POST -Headers $hdr -Body $verBody
Invoke-RestMethod -Uri "$wf/versions/$($ver.id)/publish" -Method POST -Headers $hdr | Out-Null
Write-Host "   workflowId=$($def.id)" -ForegroundColor DarkGray
Start-Sleep -Seconds 5

Write-Host "`n2) U4 correlation rule (threshold=$Threshold, cooldown=0)..." -ForegroundColor Cyan
$ruleTemplate = Get-Content -Path $ruleFixture -Raw | ConvertFrom-Json
$rule = Invoke-RestMethod -Uri "$alarm/rules" -Method POST -Headers $hdr -Body (@{
    name             = "U4 WF E2E $(Get-Date -Format 'HHmmss')"
    type             = $ruleTemplate.type
    matchKey         = $ruleTemplate.matchKey
    groupByFields    = @($ruleTemplate.groupByFields)
    windowMinutes    = $ruleTemplate.windowMinutes
    threshold        = $Threshold
    severity         = $ruleTemplate.severity
    cooldownMinutes  = 0
    dedupKeyTemplate = $ruleTemplate.dedupKeyTemplate
} | ConvertTo-Json -Depth 5)
Write-Host "   ruleId=$($rule.id)" -ForegroundColor DarkGray

$firewallRaw = Read-Fixture "firewall_deny.syslog.txt"
$receivedAt = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
$dstIp = "10.0.0.$((Get-Random -Minimum 10 -Maximum 250))"
$firewallLine = $firewallRaw -replace 'DST=10\.0\.0\.10', "DST=$dstIp"

Write-Host "`n3) POST $Threshold x denied_flow sec-events (dstIp=$dstIp)..." -ForegroundColor Yellow
for ($i = 0; $i -lt $Threshold; $i++) {
    $body = @{
        items = @(
            @{
                receivedAt = $receivedAt
                source     = @{ type = "firewall"; product = "generic-syslog"; host = "fw01" }
                raw        = $firewallLine
            }
        )
    } | ConvertTo-Json -Depth 8
    $ingest = Invoke-RestMethod -Uri $reactor -Method POST -Headers $hdr -Body $body -TimeoutSec 60
    if ($ingest.accepted -lt 1) { throw "Ingest basarisiz: $($ingest | ConvertTo-Json -Compress)" }
    Start-Sleep -Milliseconds 400
}
Write-Host "   Ingest tamam" -ForegroundColor Green

Write-Host "`n4) Alarm raised bekleniyor..." -ForegroundColor Cyan
$alarmFound = $false
for ($i = 0; $i -lt 20; $i++) {
    Start-Sleep -Seconds 2
    $page = Invoke-RestMethod -Uri "$alarm/alarms?openOnly=true&minSeverity=6" -Headers $hdr
    $items = @($page.items)
    if ($items.Count -eq 0 -and $page -is [array]) { $items = @($page) }
    $match = $items | Where-Object {
        $_.ruleId -eq $rule.id -and ($_.status -eq 0 -or $_.status -eq "Active")
    } | Select-Object -First 1
    if ($match) {
        Write-Host "   Alarm raised: $($match.id) severity=$($match.severity)" -ForegroundColor Green
        $alarmFound = $true
        break
    }
}
if (-not $alarmFound) {
    Write-Host "FAIL: U4 alarm raised yok — workflow tetiklenmez" -ForegroundColor Red
    exit 1
}

Write-Host "`n5) Workflow instance (Completed) bekleniyor..." -ForegroundColor Cyan
$detail = $null
for ($i = 0; $i -lt 45; $i++) {
    Start-Sleep -Seconds 2
    $runs = Invoke-RestMethod -Uri "$wf/runs?workflowId=$($def.id)&limit=3" -Headers $hdr
    $items = @($runs)
    if ($items.Count -eq 0) { continue }

    $candidate = $items | Where-Object { $_.status -eq 2 -or $_.status -eq "Completed" } | Select-Object -First 1
    if (-not $candidate) {
        $candidate = $items[0]
        if ($candidate.status -eq 3 -or $candidate.status -eq "Failed") {
            $detail = Invoke-RestMethod -Uri "$wf/runs/$($candidate.id)" -Headers $hdr
            throw "Workflow Failed: $(($detail.executions | Where-Object { $_.status -eq 2 } | ForEach-Object { "$($_.nodeId): $($_.errorMessage)" }) -join '; ')"
        }
        continue
    }

    $detail = Invoke-RestMethod -Uri "$wf/runs/$($candidate.id)" -Headers $hdr
    if ($detail.instance.status -eq 2) { break }
    if ($detail.instance.status -eq 3) {
        throw "Workflow Failed: instance=$($candidate.id)"
    }
}

if ($null -eq $detail -or $detail.instance.status -ne 2) {
    $recent = Invoke-RestMethod -Uri "$wf/runs?workflowId=$($def.id)&limit=3" -Headers $hdr
    Write-Host "FAIL: Workflow Completed degil. Son runlar:" -ForegroundColor Red
    @($recent) | ForEach-Object { Write-Host "   id=$($_.id) status=$($_.status)" -ForegroundColor DarkYellow }
    exit 1
}

$logExec = $detail.executions | Where-Object { $_.nodeId -eq "log_1" -and $_.status -eq 1 } | Select-Object -First 1
if (-not $logExec) {
    Write-Host "FAIL: log_1 node Success degil" -ForegroundColor Red
    exit 1
}

Write-Host "   run=$($detail.instance.id) log_1 OK dstIp=$dstIp" -ForegroundColor Green
Write-Host "`nOK SIEM U4 sec_events -> alarm.raised -> workflow PASS" -ForegroundColor Green
exit 0
