# SIEM U1 — sec_events → correlation alarm → alarm.raised → Workflow Event Trigger E2E
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

function Read-Fixture([string]$Name) {
    $path = Join-Path $fixtureDir $Name
    if (-not (Test-Path $path)) { throw "Fixture eksik: $path" }
    return (Get-Content -Path $path -Raw).TrimEnd()
}

Write-Host "=== SIEM U1 alarm.raised -> Workflow E2E ===" -ForegroundColor Cyan

Write-Host "`n1) Workflow (alarm.raised + login_failed filter -> write.log)..." -ForegroundColor Cyan
$wfKey = "siem-u1-wf-e2e-$(Get-Date -Format 'HHmmss')"
$def = Invoke-RestMethod -Uri "$wf/definitions" -Method POST -Headers $hdr -Body (@{
    key  = $wfKey
    name = "SIEM U1 Workflow E2E"
} | ConvertTo-Json)

$verBody = @{
    entryNodeId = "manual_1"
    nodes       = @(
        @{ id = "manual_1"; type = "manual.trigger"; config = @{} },
        @{
            id     = "log_1"
            type   = "write.log"
            config = @{ message = "SIEM U1 alarm user={{event.context.userId}} srcIp={{event.context.srcIp}} severity={{event.severity}}" }
        }
    )
    edges = @(@{ fromNodeId = "manual_1"; toNodeId = "log_1"; edgeKey = "default" })
    triggers = @(@{
        type             = "event"
        enabled          = $true
        filterExpression = "event.severity >= 7 && event.context.key == 'login_failed'"
        config           = @{ eventType = "alarm.raised" }
    })
} | ConvertTo-Json -Depth 10

$ver = Invoke-RestMethod -Uri "$wf/definitions/$($def.id)/versions" -Method POST -Headers $hdr -Body $verBody
Invoke-RestMethod -Uri "$wf/versions/$($ver.id)/publish" -Method POST -Headers $hdr | Out-Null
Write-Host "   workflowId=$($def.id)" -ForegroundColor DarkGray
Start-Sleep -Seconds 5

Write-Host "`n2) U1 correlation rule (threshold=$Threshold, cooldown=0)..." -ForegroundColor Cyan
$ruleName = "U1 WF E2E $(Get-Date -Format 'HHmmss')"
$rule = Invoke-RestMethod -Uri "$alarm/rules" -Method POST -Headers $hdr -Body (@{
    name             = $ruleName
    type             = "correlation"
    matchKey         = "login_failed"
    groupByFields    = @("userId", "srcIp")
    windowMinutes    = 5
    threshold        = $Threshold
    severity         = 7
    cooldownMinutes  = 0
    dedupKeyTemplate = "{ruleId}:{groupKey}"
} | ConvertTo-Json -Depth 5)
Write-Host "   ruleId=$($rule.id)" -ForegroundColor DarkGray

$windowsRaw = Read-Fixture "windows_4625_failed_logon.json"
$receivedAt = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ")

Write-Host "`n3) POST $Threshold x login_failed sec-events..." -ForegroundColor Yellow
for ($i = 0; $i -lt $Threshold; $i++) {
    $windowsObj = $windowsRaw | ConvertFrom-Json
    $windowsObj.TimeCreated = $receivedAt
    $body = @{
        items = @(
            @{
                receivedAt = $receivedAt
                source     = @{ type = "ad"; product = "windows"; host = "dc01" }
                raw        = $windowsObj
            }
        )
    } | ConvertTo-Json -Depth 8
    $ingest = Invoke-RestMethod -Uri $reactor -Method POST -Headers $hdr -Body $body -TimeoutSec 60
    if ($ingest.accepted -lt 1) { throw "Ingest basarisiz: $($ingest | ConvertTo-Json -Compress)" }
    Start-Sleep -Milliseconds 400
}
Write-Host "   Ingest tamam" -ForegroundColor Green

Write-Host "`n4) Workflow instance (Completed) bekleniyor..." -ForegroundColor Cyan
$detail = $null
for ($i = 0; $i -lt 30; $i++) {
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
    Write-Host "FAIL: Workflow Completed degil (mngworkflow-worker EventTrigger acik mi?)" -ForegroundColor Red
    exit 1
}

$logExec = $detail.executions | Where-Object { $_.nodeId -eq "log_1" -and $_.status -eq 1 } | Select-Object -First 1
if (-not $logExec) {
    Write-Host "FAIL: log_1 node Success degil" -ForegroundColor Red
    exit 1
}

Write-Host "   run=$($detail.instance.id) log_1 OK" -ForegroundColor Green
Write-Host "`nOK SIEM U1 sec_events -> alarm.raised -> workflow PASS" -ForegroundColor Green
exit 0
