# SIEM — FortiGate syslog (UDP :541) → U4 alarm → alarm.raised → Workflow E2E
param(
    [string]$EngineUrl = "http://192.168.20.20:5037",
    [string]$Gateway = "http://192.168.20.20:5040",
    [string]$Server = "192.168.20.20",
    [int]$UdpPort = 541,
    [string]$Domain = "odak",
    [int]$Threshold = 3,
    [switch]$FailIfSkipped
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path
$fixturePath = Join-Path $repoRoot "tests/fixtures/siem/fortigate_traffic_deny.syslog.txt"
$ruleFixture = Join-Path $repoRoot "tests/fixtures/siem/alarm_rules/u4_firewall_deny_spike.json"
$tokenScript = Join-Path $PSScriptRoot "../tests/MngDataGateway/auth/get-token.ps1"

function Skip-Test([string]$Reason) {
    Write-Host "SKIP FortiGate U4 workflow: $Reason" -ForegroundColor Yellow
    if ($FailIfSkipped) { exit 1 }
    exit 0
}

if (-not (Test-Path $fixturePath)) { throw "Fixture eksik: $fixturePath" }

try {
    Invoke-RestMethod -Uri "$EngineUrl/swagger/index.html" -TimeoutSec 8 | Out-Null
} catch {
    Skip-Test "Engine erisilemiyor: $EngineUrl"
}

$dstIp = "10.89.$((Get-Random -Maximum 250)).$((Get-Random -Maximum 250))"
$srcIpBase = 113
$lineTemplate = (Get-Content $fixturePath -Raw).TrimEnd() -replace 'dstip=10\.0\.0\.10', "dstip=$dstIp"

Write-Host "=== SIEM FortiGate U4 -> Workflow E2E (UDP $UdpPort) ===" -ForegroundColor Cyan
Write-Host "  dstIp=$dstIp threshold=$Threshold" -ForegroundColor DarkGray

$token = & $tokenScript -KeeperBaseUrl $Gateway -DomainName $Domain -Username "odak_admin" -Password "Admin123!"
$hdr = @{ Authorization = "Bearer $token"; "X-Domain-Name" = $Domain; "Content-Type" = "application/json" }
$wf = "$Gateway/workflow/api/v1"
$alarm = "$Gateway/alarm/api/v1"

Write-Host "`n1) Workflow (alarm.raised + denied_flow)..." -ForegroundColor Cyan
$wfKey = "siem-fortigate-u4-wf-$(Get-Date -Format 'HHmmss')"
$def = Invoke-RestMethod -Uri "$wf/definitions" -Method POST -Headers $hdr -Body (@{
    key  = $wfKey
    name = "SIEM FortiGate U4 Workflow E2E"
} | ConvertTo-Json)

$verBody = @{
    entryNodeId = "manual_1"
    nodes       = @(
        @{ id = "manual_1"; type = "manual.trigger"; config = @{} },
        @{
            id     = "log_1"
            type   = "write.log"
            config = @{
                message = "SIEM FortiGate U4 dstIp={{event.context.dstIp}} count={{event.context.windowCount}} severity={{event.severity}}"
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

Write-Host "`n2) U4 correlation rule..." -ForegroundColor Cyan
$ruleTemplate = Get-Content -Path $ruleFixture -Raw | ConvertFrom-Json
$rule = Invoke-RestMethod -Uri "$alarm/rules" -Method POST -Headers $hdr -Body (@{
    name             = "U4 FortiGate WF E2E $(Get-Date -Format 'HHmmss')"
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

function Send-Udp([string]$Line) {
    $udp = New-Object System.Net.Sockets.UdpClient
    try {
        $bytes = [System.Text.Encoding]::UTF8.GetBytes($Line)
        [void]$udp.Send($bytes, $bytes.Length, $Server, $UdpPort)
    } finally {
        $udp.Close()
    }
}

Write-Host "`n3) UDP x$Threshold denied_flow..." -ForegroundColor Yellow
for ($i = 0; $i -lt $Threshold; $i++) {
    $srcIp = "203.0.$srcIpBase.$((Get-Random -Maximum 250))"
    Send-Udp ($lineTemplate -replace 'srcip=203\.0\.113\.5', "srcip=$srcIp")
    Start-Sleep -Milliseconds 400
}

Write-Host "`n4) Engine flush..." -ForegroundColor Cyan
$totalPublished = 0
for ($attempt = 1; $attempt -le 3; $attempt++) {
    $flush = Invoke-RestMethod -Uri "$EngineUrl/api/SecEvents/flush" -Method POST -TimeoutSec 90
    $totalPublished += [int]$flush.published
    if ($totalPublished -ge $Threshold) { break }
    Send-Udp ($lineTemplate -replace 'srcip=203\.0\.113\.5', "srcip=203.0.$srcIpBase.$((Get-Random -Maximum 250))")
    Start-Sleep -Milliseconds 500
}
Write-Host "   toplam published=$totalPublished" -ForegroundColor Green
if ($totalPublished -lt $Threshold) {
    throw "FAIL: toplam published=$totalPublished"
}

Write-Host "`n5) Workflow instance (Completed) bekleniyor..." -ForegroundColor Cyan
$detail = $null
for ($i = 0; $i -lt 45; $i++) {
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
    throw "FAIL: Workflow Completed degil"
}

$logExec = $detail.executions | Where-Object { $_.nodeId -eq "log_1" -and $_.status -eq 1 } | Select-Object -First 1
if (-not $logExec) {
    throw "FAIL: log_1 node Success degil"
}

Write-Host "   run=$($detail.instance.id) log_1 OK dstIp=$dstIp" -ForegroundColor Green
Write-Host "`nOK SIEM FortiGate UDP -> alarm.raised -> workflow PASS" -ForegroundColor Green
