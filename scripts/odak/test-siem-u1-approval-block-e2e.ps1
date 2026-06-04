# SIEM U1 — sec_events → alarm → approval → block.ip (Reactor mqtt/publish)
param(
    [string]$Gateway = "http://192.168.20.20:5040",
    [string]$Domain = "odak",
    [string]$EngineId = "",
    [int]$Threshold = 3
)

$ErrorActionPreference = "Stop"
$tokenScript = Join-Path $PSScriptRoot "..\tests\MngDataGateway\auth\get-token.ps1"
$token = & $tokenScript -KeeperBaseUrl $Gateway -DomainName $Domain -Username "odak_admin" -Password "Admin123!"
$hdr = @{ Authorization = "Bearer $token"; "X-Domain-Name" = $Domain; "Content-Type" = "application/json" }
$wf = "$Gateway/workflow/api/v1"
$alarm = "$Gateway/alarm/api/v1"
$reactor = "$Gateway/reactor/api/v1"
$reactorIngest = "$reactor/ingest/sec-events"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path
$fixtureDir = Join-Path $repoRoot "tests/fixtures/siem"

function Invoke-List($uri) {
    $resp = Invoke-RestMethod -Uri $uri -Headers $hdr
    if ($null -eq $resp) { return @() }
    return @($resp)
}

function Read-Fixture([string]$Name) {
    $path = Join-Path $fixtureDir $Name
    if (-not (Test-Path $path)) { throw "Fixture eksik: $path" }
    return (Get-Content -Path $path -Raw).TrimEnd()
}

function Get-EngineId {
    if (-not [string]::IsNullOrWhiteSpace($EngineId)) { return $EngineId.Trim() }
    $engines = Invoke-RestMethod -Uri "$reactor/monitoring/engines" -Headers $hdr -Method GET
    if (-not $engines.data -or $engines.data.Count -lt 1) {
        throw "mon_engines bos — setup-mngengine-odak.ps1 -ApplyConfig calistirin"
    }
    return $engines.data[0].__dataId
}

function Confirm-BlockIpMode([string]$InstanceId, $blockExec) {
    $mode = $null
    if ($blockExec.PSObject.Properties['output']) {
        $mode = $blockExec.output.mode
    }
    if ($mode) { return $mode }

    Write-Host "   Reactor log dogrulama (mqtt publish)..." -ForegroundColor DarkGray
    Import-Module Posh-SSH -Force -ErrorAction Stop
    . (Join-Path $PSScriptRoot "OdakSshCommon.ps1")
    $cred = Get-OdakSshCredential -User "odak" -Server ([uri]$Gateway).Host
    $session = New-SSHSession -ComputerName ([uri]$Gateway).Host -Credential $cred -AcceptKey
    try {
        $logCmd = "docker logs mngworkflow-worker 2>&1 | grep -F 'Engine command published' | grep -F '$InstanceId' | tail -1"
        $log = Invoke-SSHCommand -SessionId $session.SessionId -Command $logCmd -TimeOut 30
        if (@($log.Output) -match 'Engine command published') { return "reactor_mqtt" }
    } finally {
        Remove-SSHSession -SessionId $session.SessionId | Out-Null
    }
    return $null
}

Write-Host "=== SIEM U1 approval -> block.ip E2E ===" -ForegroundColor Cyan
$resolvedEngineId = Get-EngineId
Write-Host "   engineId=$resolvedEngineId" -ForegroundColor DarkGray

Write-Host "`n1) Workflow (alarm.raised -> log -> approval -> block.ip -> log)..." -ForegroundColor Cyan
$wfKey = "siem-u1-block-e2e-$(Get-Date -Format 'HHmmss')"
$def = Invoke-RestMethod -Uri "$wf/definitions" -Method POST -Headers $hdr -Body (@{
    key  = $wfKey
    name = "SIEM U1 Approval Block E2E"
} | ConvertTo-Json)

$verBody = @{
    entryNodeId = "manual_1"
    nodes       = @(
        @{ id = "manual_1"; type = "manual.trigger"; config = @{} },
        @{
            id     = "log_ctx_1"
            type   = "write.log"
            config = @{ message = "SIEM U1 ctx srcIp={{event.context.srcIp}} user={{event.context.userId}}" }
        },
        @{ id = "approval_1"; type = "approval.wait"; config = @{ approverGroup = "SecurityAdmins" } },
        @{
            id     = "block_1"
            type   = "block.ip"
            config = @{
                engineId   = $resolvedEngineId
                ipAddress  = "{{event.context.srcIp}}"
                ttlMinutes = 60
                reason     = "SIEM U1 brute-force block"
            }
        },
        @{ id = "log_ok_1"; type = "write.log"; config = @{ message = "SIEM U1 block.ip completed" } },
        @{ id = "log_reject_1"; type = "write.log"; config = @{ message = "SIEM U1 rejected" } }
    )
    edges = @(
        @{ fromNodeId = "manual_1"; toNodeId = "log_ctx_1"; edgeKey = "default" },
        @{ fromNodeId = "log_ctx_1"; toNodeId = "approval_1"; edgeKey = "default" },
        @{ fromNodeId = "approval_1"; toNodeId = "block_1"; edgeKey = "approved" },
        @{ fromNodeId = "approval_1"; toNodeId = "log_reject_1"; edgeKey = "rejected" },
        @{ fromNodeId = "block_1"; toNodeId = "log_ok_1"; edgeKey = "default" }
    )
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
$rule = Invoke-RestMethod -Uri "$alarm/rules" -Method POST -Headers $hdr -Body (@{
    name             = "U1 Block E2E $(Get-Date -Format 'HHmmss')"
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
$blockIp = "10.99.0.$((Get-Random -Minimum 10 -Maximum 250))"

Write-Host "`n3) POST $Threshold x login_failed sec-events (srcIp=$blockIp)..." -ForegroundColor Yellow
for ($i = 0; $i -lt $Threshold; $i++) {
    $windowsObj = $windowsRaw | ConvertFrom-Json
    $windowsObj.TimeCreated = $receivedAt
    $windowsObj.IpAddress = $blockIp
    $body = @{
        items = @(
            @{
                receivedAt = $receivedAt
                source     = @{ type = "ad"; product = "windows"; host = "dc01" }
                raw        = $windowsObj
            }
        )
    } | ConvertTo-Json -Depth 8
    $ingest = Invoke-RestMethod -Uri $reactorIngest -Method POST -Headers $hdr -Body $body -TimeoutSec 60
    if ($ingest.accepted -lt 1) { throw "Ingest basarisiz: $($ingest | ConvertTo-Json -Compress)" }
    Start-Sleep -Milliseconds 400
}
Write-Host "   Ingest tamam" -ForegroundColor Green

Write-Host "`n4) Workflow instance (Waiting @ approval)..." -ForegroundColor Cyan
$instanceId = $null
$approvalId = $null
for ($i = 0; $i -lt 30; $i++) {
    Start-Sleep -Seconds 2
    $waiting = Invoke-List "$wf/runs?workflowId=$($def.id)&status=1&limit=5"
    if ($waiting.Count -eq 0) { continue }
    $row = $waiting[0]
    $instanceId = if ($row.id) { $row.id } else { $row.Id }
    if ([string]::IsNullOrWhiteSpace($instanceId)) { continue }

    $pending = Invoke-List "$wf/approvals?status=0&limit=50"
    $match = $pending | Where-Object { $_.instanceId -eq $instanceId } | Select-Object -First 1
    if ($match) {
        $approvalId = if ($match.id) { $match.id } else { $match.Id }
        break
    }
}

if (-not $approvalId) {
    Write-Host "FAIL: onay bekleyen workflow instance yok" -ForegroundColor Red
    exit 1
}
Write-Host "   instance=$instanceId approval=$approvalId" -ForegroundColor DarkGray

Write-Host "`n5) Onaylaniyor..." -ForegroundColor Yellow
Invoke-RestMethod -Uri "$wf/approvals/$approvalId/decide" -Method POST -Headers $hdr -Body (@{
    approved  = $true
    decidedBy = "odak_admin"
    comment   = "SIEM U1 E2E approve block"
} | ConvertTo-Json) | Out-Null

Write-Host "`n6) Completed bekleniyor..." -ForegroundColor Cyan
$detail = $null
for ($i = 0; $i -lt 30; $i++) {
    Start-Sleep -Seconds 2
    $detail = Invoke-RestMethod -Uri "$wf/runs/$instanceId" -Headers $hdr
    if ($detail.instance.status -eq 2) { break }
    if ($detail.instance.status -eq 3) {
        $errs = ($detail.executions | Where-Object { $_.status -eq 2 } | ForEach-Object { "$($_.nodeId): $($_.errorMessage)" }) -join '; '
        Write-Host "FAIL: instance Failed — $errs" -ForegroundColor Red
        exit 1
    }
}

if ($detail.instance.status -ne 2) {
    Write-Host "FAIL: instance Completed degil (status=$($detail.instance.status))" -ForegroundColor Red
    exit 1
}

$required = @("log_ctx_1", "approval_1", "block_1", "log_ok_1")
$failed = @()
foreach ($nodeId in $required) {
    $exec = $detail.executions | Where-Object { $_.nodeId -eq $nodeId -and $_.status -eq 1 } | Select-Object -First 1
    if (-not $exec) { $failed += $nodeId }
}
if ($failed.Count -gt 0) {
    Write-Host "FAIL: node Success degil: $($failed -join ', ')" -ForegroundColor Red
    exit 1
}

$blockExec = $detail.executions | Where-Object { $_.nodeId -eq "block_1" -and $_.status -eq 1 } | Select-Object -First 1
$mode = Confirm-BlockIpMode -InstanceId $instanceId -blockExec $blockExec
if ($mode -ne "reactor_mqtt") {
    Write-Host "FAIL: block.ip mode=$mode (beklenen reactor_mqtt)" -ForegroundColor Red
    exit 1
}

Write-Host "   run=$instanceId mode=reactor_mqtt ip=$blockIp" -ForegroundColor Green
Write-Host "`nOK SIEM U1 sec_events -> approval -> block.ip PASS" -ForegroundColor Green
exit 0
