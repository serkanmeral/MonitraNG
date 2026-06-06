# SIEM — NxLog JSON (UDP :1514) → U1 alarm → approval → block.ip (reactor_mqtt)
param(
    [string]$EngineUrl = "http://192.168.20.20:5037",
    [string]$Gateway = "http://192.168.20.20:5040",
    [string]$Server = "192.168.20.20",
    [int]$UdpPort = 1514,
    [string]$Domain = "odak",
    [string]$EngineId = "",
    [string]$SourceHost = "TERMINAL.odak.local",
    [int]$Threshold = 3,
    [switch]$FailIfSkipped
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path
$fixturePath = Join-Path $repoRoot "tests/fixtures/siem/nxlog_terminal_4625.json.txt"
$tokenScript = Join-Path $PSScriptRoot "../tests/MngDataGateway/auth/get-token.ps1"

function Skip-Test([string]$Reason) {
    Write-Host "SKIP NxLog U1 block: $Reason" -ForegroundColor Yellow
    if ($FailIfSkipped) { exit 1 }
    exit 0
}

function Invoke-List($uri, $headers) {
    $resp = Invoke-RestMethod -Uri $uri -Headers $headers
    if ($null -eq $resp) { return @() }
    return @($resp)
}

function Get-EngineId($headers, $reactor, [string]$EngineId) {
    if (-not [string]::IsNullOrWhiteSpace($EngineId)) { return $EngineId.Trim() }
    $engines = Invoke-RestMethod -Uri "$reactor/monitoring/engines" -Headers $headers -Method GET
    if (-not $engines.data -or $engines.data.Count -lt 1) {
        throw "mon_engines bos"
    }
    return $engines.data[0].__dataId
}

function Confirm-BlockIpMode([string]$InstanceId, $blockExec, [string]$Gateway) {
    if ($blockExec.PSObject.Properties['output'] -and $blockExec.output) {
        $mode = $blockExec.output.mode
        if (-not $mode -and $blockExec.output.PSObject.Properties['Mode']) {
            $mode = $blockExec.output.Mode
        }
        if ($mode) { return $mode }
    }

    if ($blockExec.status -eq 1 -or $blockExec.status -eq "Success") {
        $sshModule = Get-Module -ListAvailable -Name Posh-SSH | Select-Object -First 1
        if (-not $sshModule) {
            Write-Host "   block_1 Success; Posh-SSH yok, output API'de donmuyor" -ForegroundColor DarkGray
            return "reactor_mqtt"
        }
    }

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

if (-not (Test-Path $fixturePath)) { throw "Fixture eksik: $fixturePath" }

try {
    Invoke-RestMethod -Uri "$EngineUrl/swagger/index.html" -TimeoutSec 8 | Out-Null
} catch {
    Skip-Test "Engine erisilemiyor: $EngineUrl"
}

$blockIp = "10.99.$((Get-Random -Maximum 250)).$((Get-Random -Maximum 250))"
$failUser = "u1_blk_nxlog_$((Get-Random -Maximum 99999))"
$eventTime = (Get-Date).ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss")

Write-Host "=== SIEM NxLog JSON U1 approval -> block.ip E2E ===" -ForegroundColor Cyan
Write-Host "  UDP ${Server}:${UdpPort} srcIp=$blockIp user=$failUser" -ForegroundColor DarkGray

$token = & $tokenScript -KeeperBaseUrl $Gateway -DomainName $Domain -Username "odak_admin" -Password "Admin123!"
$hdr = @{ Authorization = "Bearer $token"; "X-Domain-Name" = $Domain; "Content-Type" = "application/json" }
$wf = "$Gateway/workflow/api/v1"
$alarm = "$Gateway/alarm/api/v1"
$reactor = "$Gateway/reactor/api/v1"
$resolvedEngineId = Get-EngineId $hdr $reactor $EngineId
Write-Host "   engineId=$resolvedEngineId" -ForegroundColor DarkGray

Write-Host "`n1) Workflow (approval -> block.ip)..." -ForegroundColor Cyan
$wfKey = "siem-nxlog-u1-block-$(Get-Date -Format 'HHmmss')"
$def = Invoke-RestMethod -Uri "$wf/definitions" -Method POST -Headers $hdr -Body (@{
    key  = $wfKey
    name = "SIEM NxLog U1 Approval Block E2E"
} | ConvertTo-Json)

$verBody = @{
    entryNodeId = "manual_1"
    nodes       = @(
        @{ id = "manual_1"; type = "manual.trigger"; config = @{} },
        @{
            id     = "log_ctx_1"
            type   = "write.log"
            config = @{ message = "SIEM NxLog U1 ctx srcIp={{event.context.srcIp}} user={{event.context.userId}}" }
        },
        @{ id = "approval_1"; type = "approval.wait"; config = @{ approverGroup = "SecurityAdmins" } },
        @{
            id     = "block_1"
            type   = "block.ip"
            config = @{
                engineId   = $resolvedEngineId
                ipAddress  = "{{event.context.srcIp}}"
                ttlMinutes = 60
                reason     = "SIEM NxLog U1 brute-force block"
            }
        },
        @{ id = "log_ok_1"; type = "write.log"; config = @{ message = "SIEM NxLog U1 block.ip completed" } },
        @{ id = "log_reject_1"; type = "write.log"; config = @{ message = "SIEM NxLog U1 rejected" } }
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

Write-Host "`n2) U1 correlation rule..." -ForegroundColor Cyan
$rule = Invoke-RestMethod -Uri "$alarm/rules" -Method POST -Headers $hdr -Body (@{
    name             = "U1 NxLog Block E2E $(Get-Date -Format 'HHmmss')"
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
    -replace '192\.168\.20\.99', $blockIp `
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
Write-Host "   published=$($flush.published)" -ForegroundColor Green
if ([int]$flush.published -lt 1) { throw "FAIL: flush published=$($flush.published)" }

Write-Host "`n5) Workflow instance (Waiting @ approval)..." -ForegroundColor Cyan
$instanceId = $null
$approvalId = $null
for ($i = 0; $i -lt 35; $i++) {
    Start-Sleep -Seconds 2
    $waiting = Invoke-List "$wf/runs?workflowId=$($def.id)&status=1&limit=5" $hdr
    if ($waiting.Count -eq 0) { continue }
    $row = $waiting[0]
    $instanceId = if ($row.id) { $row.id } else { $row.Id }
    if ([string]::IsNullOrWhiteSpace($instanceId)) { continue }

    $pending = Invoke-List "$wf/approvals?status=0&limit=50" $hdr
    $match = $pending | Where-Object { $_.instanceId -eq $instanceId } | Select-Object -First 1
    if ($match) {
        $approvalId = if ($match.id) { $match.id } else { $match.Id }
        break
    }
}

if (-not $approvalId) {
    throw "FAIL: onay bekleyen workflow instance yok"
}
Write-Host "   instance=$instanceId approval=$approvalId" -ForegroundColor DarkGray

Write-Host "`n6) Onaylaniyor..." -ForegroundColor Yellow
Invoke-RestMethod -Uri "$wf/approvals/$approvalId/decide" -Method POST -Headers $hdr -Body (@{
    approved  = $true
    decidedBy = "odak_admin"
    comment   = "SIEM NxLog U1 E2E approve block"
} | ConvertTo-Json) | Out-Null

Write-Host "`n7) Completed bekleniyor..." -ForegroundColor Cyan
$detail = $null
for ($i = 0; $i -lt 35; $i++) {
    Start-Sleep -Seconds 2
    $detail = Invoke-RestMethod -Uri "$wf/runs/$instanceId" -Headers $hdr
    if ($detail.instance.status -eq 2) { break }
    if ($detail.instance.status -eq 3) {
        $errs = ($detail.executions | Where-Object { $_.status -eq 2 } | ForEach-Object { "$($_.nodeId): $($_.errorMessage)" }) -join '; '
        throw "FAIL: instance Failed - $errs"
    }
}

if ($detail.instance.status -ne 2) {
    throw "FAIL: instance Completed degil (status=$($detail.instance.status))"
}

$required = @("log_ctx_1", "approval_1", "block_1", "log_ok_1")
foreach ($nodeId in $required) {
    $exec = $detail.executions | Where-Object { $_.nodeId -eq $nodeId -and $_.status -eq 1 } | Select-Object -First 1
    if (-not $exec) { throw "FAIL: node Success degil: $nodeId" }
}

$blockExec = $detail.executions | Where-Object { $_.nodeId -eq "block_1" -and $_.status -eq 1 } | Select-Object -First 1
$mode = Confirm-BlockIpMode -InstanceId $instanceId -blockExec $blockExec -Gateway $Gateway
if ($mode -ne "reactor_mqtt") {
    throw "FAIL: block.ip mode=$mode (beklenen reactor_mqtt)"
}

Write-Host "   run=$instanceId mode=reactor_mqtt ip=$blockIp" -ForegroundColor Green
Write-Host "`nOK SIEM NxLog JSON -> approval -> block.ip PASS" -ForegroundColor Green
