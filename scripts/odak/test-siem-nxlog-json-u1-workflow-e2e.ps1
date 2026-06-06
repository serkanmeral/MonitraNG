# SIEM — NxLog JSON (UDP/Engine) → U1 alarm → alarm.raised → Workflow E2E
param(
    [string]$EngineUrl = "http://192.168.20.20:5037",
    [string]$Gateway = "http://192.168.20.20:5040",
    [string]$Server = "192.168.20.20",
    [int]$UdpPort = 1514,
    [string]$Domain = "odak",
    [string]$SourceHost = "TERMINAL.odak.local",
    [int]$Threshold = 3,
    [switch]$FailIfSkipped
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path
$fixturePath = Join-Path $repoRoot "tests/fixtures/siem/nxlog_terminal_4625.json.txt"
$tokenScript = Join-Path $PSScriptRoot "../tests/MngDataGateway/auth/get-token.ps1"

function Skip-Test([string]$Reason) {
    Write-Host "SKIP NxLog U1 workflow: $Reason" -ForegroundColor Yellow
    if ($FailIfSkipped) { exit 1 }
    exit 0
}

if (-not (Test-Path $fixturePath)) { throw "Fixture eksik: $fixturePath" }

try {
    Invoke-RestMethod -Uri "$EngineUrl/swagger/index.html" -TimeoutSec 8 | Out-Null
} catch {
    Skip-Test "Engine erisilemiyor: $EngineUrl"
}

$srcIp = "10.78.$((Get-Random -Maximum 250)).$((Get-Random -Maximum 250))"
$failUser = "u1_wf_nxlog_$((Get-Random -Maximum 99999))"
$eventTime = (Get-Date).ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss")

Write-Host "=== SIEM NxLog JSON U1 -> Workflow E2E ===" -ForegroundColor Cyan
Write-Host "  UDP ${Server}:${UdpPort} user=$failUser srcIp=$srcIp" -ForegroundColor DarkGray

$token = & $tokenScript -KeeperBaseUrl $Gateway -DomainName $Domain -Username "odak_admin" -Password "Admin123!"
$hdr = @{ Authorization = "Bearer $token"; "X-Domain-Name" = $Domain; "Content-Type" = "application/json" }
$wf = "$Gateway/workflow/api/v1"
$alarm = "$Gateway/alarm/api/v1"

Write-Host "`n1) Workflow (alarm.raised + login_failed)..." -ForegroundColor Cyan
$wfKey = "siem-nxlog-u1-wf-$(Get-Date -Format 'HHmmss')"
$def = Invoke-RestMethod -Uri "$wf/definitions" -Method POST -Headers $hdr -Body (@{
    key  = $wfKey
    name = "SIEM NxLog U1 Workflow E2E"
} | ConvertTo-Json)

$verBody = @{
    entryNodeId = "manual_1"
    nodes       = @(
        @{ id = "manual_1"; type = "manual.trigger"; config = @{} },
        @{
            id     = "log_1"
            type   = "write.log"
            config = @{ message = "SIEM NxLog U1 user={{event.context.userId}} srcIp={{event.context.srcIp}}" }
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

Write-Host "`n2) U1 correlation rule..." -ForegroundColor Cyan
$rule = Invoke-RestMethod -Uri "$alarm/rules" -Method POST -Headers $hdr -Body (@{
    name             = "U1 NxLog WF E2E $(Get-Date -Format 'HHmmss')"
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

$template = Get-Content $fixturePath -Raw
$payload = ($template -replace 'probe_fail_user', $failUser) `
    -replace '192\.168\.20\.99', $srcIp `
    -replace '2026-06-06 10:27:29', $eventTime `
    -replace 'TERMINAL\.odak\.local', $SourceHost

function Send-Udp([string]$Json) {
    $udp = New-Object System.Net.Sockets.UdpClient
    try {
        $bytes = [System.Text.Encoding]::UTF8.GetBytes($Json)
        [void]$udp.Send($bytes, $bytes.Length, $Server, $UdpPort)
    } finally {
        $udp.Close()
    }
}

Write-Host "`n3) UDP x$Threshold login_failed..." -ForegroundColor Yellow
for ($i = 0; $i -lt $Threshold; $i++) {
    Send-Udp $payload
    Start-Sleep -Milliseconds 400
}

Write-Host "`n4) Engine flush..." -ForegroundColor Cyan
$flush = Invoke-RestMethod -Uri "$EngineUrl/api/SecEvents/flush" -Method POST -TimeoutSec 90
Write-Host "   accepted=$($flush.accepted) published=$($flush.published)" -ForegroundColor Green
if ($flush.published -lt 1) { throw "FAIL: flush published=$($flush.published)" }

Write-Host "`n5) Workflow instance (Completed) bekleniyor..." -ForegroundColor Cyan
$detail = $null
for ($i = 0; $i -lt 35; $i++) {
    Start-Sleep -Seconds 2
    $runs = Invoke-RestMethod -Uri "$wf/runs?workflowId=$($def.id)&limit=5" -Headers $hdr
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
    throw "FAIL: Workflow Completed degil (mngworkflow-worker EventTrigger acik mi?)"
}

$logExec = $detail.executions | Where-Object { $_.nodeId -eq "log_1" -and $_.status -eq 1 } | Select-Object -First 1
if (-not $logExec) {
    throw "FAIL: log_1 node Success degil"
}

Write-Host "   run=$($detail.instance.id) log_1 OK" -ForegroundColor Green
Write-Host "`nOK SIEM NxLog JSON -> alarm.raised -> workflow PASS" -ForegroundColor Green
