# P4-A E2E — correlation alarm → workflow → approval → workitem (SIEM §8 minimal)
param(
    [string]$Gateway = "http://192.168.20.20:5040",
    [string]$Domain = "odak",
    [string]$WorkspaceId = "f414462a-cd9e-427e-87e8-3cdff0502325",
    [string]$TypeId = "b00b8480-ae67-42f9-be85-d9641a3083d5"
)

$ErrorActionPreference = "Stop"
$tokenScript = Join-Path $PSScriptRoot "..\tests\MngDataGateway\auth\get-token.ps1"
$token = & $tokenScript -KeeperBaseUrl $Gateway -DomainName $Domain -Username "odak_admin" -Password "Admin123!"
$hdr = @{ Authorization = "Bearer $token"; "X-Domain-Name" = $Domain; "Content-Type" = "application/json" }
$wf = "$Gateway/workflow/api/v1"
$alarm = "$Gateway/alarm/api/v1"
$p4MatchKey = "auth_failure_p4_e2e"
$wfKey = "alarm-p4-approval-e2e"

function Invoke-List($uri) {
    $resp = Invoke-RestMethod -Uri $uri -Headers $hdr
    if ($null -eq $resp) { return @() }
    return @($resp)
}

function Send-AuthFailure([string]$userId) {
    Invoke-RestMethod -Uri "$alarm/dev/observations/ingest" -Method POST -Headers $hdr -Body (@{
        domainName = $Domain
        key        = $p4MatchKey
        kind       = "event"
        dimensions = @{ userId = $userId; srcIp = "10.99.0.42" }
    } | ConvertTo-Json)
}

Write-Host "1) P4 workflow (alarm.raised -> log -> approval -> workitem -> log)..." -ForegroundColor Cyan
$defs = Invoke-List "$wf/definitions"
$def = $defs | Where-Object { $_.key -eq $wfKey } | Select-Object -First 1
if (-not $def) {
    $def = Invoke-RestMethod -Uri "$wf/definitions" -Method POST -Headers $hdr -Body (@{
        key  = $wfKey
        name = "Alarm P4 Approval E2E"
    } | ConvertTo-Json)
}

$verBody = @{
    entryNodeId = "manual_1"
    nodes       = @(
        @{ id = "manual_1"; type = "manual.trigger"; config = @{} },
        @{ id = "log_ctx_1"; type = "write.log"; config = @{ message = "P4 alarm ctx srcIp={{event.context.srcIp}}" } },
        @{ id = "approval_1"; type = "approval.wait"; config = @{ approverGroup = "SecurityAdmins" } },
        @{
            id     = "wi_create_1"
            type   = "workitem.create"
            config = @{
                workspaceId = $WorkspaceId
                typeId      = $TypeId
                title       = "SIEM P4 {{event.context.srcIp}}"
                description = "Alarm approval E2E — correlation auth_failure"
            }
        },
        @{ id = "log_ok_1"; type = "write.log"; config = @{ message = "P4 approved path completed" } },
        @{ id = "log_reject_1"; type = "write.log"; config = @{ message = "P4 rejected" } }
    )
    edges = @(
        @{ fromNodeId = "manual_1"; toNodeId = "log_ctx_1"; edgeKey = "default" },
        @{ fromNodeId = "log_ctx_1"; toNodeId = "approval_1"; edgeKey = "default" },
        @{ fromNodeId = "approval_1"; toNodeId = "wi_create_1"; edgeKey = "approved" },
        @{ fromNodeId = "approval_1"; toNodeId = "log_reject_1"; edgeKey = "rejected" },
        @{ fromNodeId = "wi_create_1"; toNodeId = "log_ok_1"; edgeKey = "default" }
    )
    triggers = @(@{
        type             = "event"
        enabled          = $true
        filterExpression = "event.severity >= 6"
        config           = @{ eventType = "alarm.raised" }
    })
} | ConvertTo-Json -Depth 10

$ver = Invoke-RestMethod -Uri "$wf/definitions/$($def.id)/versions" -Method POST -Headers $hdr -Body $verBody
Invoke-RestMethod -Uri "$wf/versions/$($ver.id)/publish" -Method POST -Headers $hdr | Out-Null
Write-Host "   workflowId=$($def.id)" -ForegroundColor Gray
Start-Sleep -Seconds 5

Write-Host "2) Correlation rule ($p4MatchKey, threshold=3)..." -ForegroundColor Cyan
$rules = Invoke-List "$alarm/rules"
$rule = $rules | Where-Object { $_.matchKey -eq $p4MatchKey -and $_.type -eq "correlation" } | Select-Object -First 1
if (-not $rule) {
    Invoke-RestMethod -Uri "$alarm/rules?domainName=$Domain" -Method POST -Headers $hdr -Body (@{
        name            = "P4 auth failure correlation"
        type            = "correlation"
        matchKey        = $p4MatchKey
        threshold       = 3
        severity        = 6
        cooldownMinutes = 0
        windowMinutes   = 60
        groupByFields   = @("userId")
    } | ConvertTo-Json) | Out-Null
}

$userId = "p4-e2e-$(Get-Random)"
Write-Host "3) Sending 3 events (user=$userId)..." -ForegroundColor Yellow
$r1 = Send-AuthFailure $userId
$r2 = Send-AuthFailure $userId
$r3 = Send-AuthFailure $userId
Write-Host "   raised=$($r3.alarmsRaised) updated=$($r3.alarmsUpdated)"
if ($r3.alarmsRaised -lt 1 -and $r3.alarmsUpdated -lt 1) {
    Write-Host "FAIL: expected correlation alarm on 3rd event" -ForegroundColor Red
    exit 1
}

Write-Host "4) Waiting for workflow instance (Waiting @ approval)..." -ForegroundColor Yellow
$instanceId = $null
$approvalId = $null
for ($i = 0; $i -lt 24; $i++) {
    Start-Sleep -Seconds 5
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
    Write-Host "FAIL: no pending approval for workflow instance" -ForegroundColor Red
    exit 1
}
Write-Host "   instance=$instanceId approval=$approvalId" -ForegroundColor Gray

Write-Host "5) Approving..." -ForegroundColor Yellow
Invoke-RestMethod -Uri "$wf/approvals/$approvalId/decide" -Method POST -Headers $hdr -Body (@{
    approved  = $true
    decidedBy = "odak_admin"
    comment   = "P4 E2E approve"
} | ConvertTo-Json) | Out-Null

Write-Host "6) Waiting for Completed..." -ForegroundColor Yellow
$detail = $null
for ($i = 0; $i -lt 24; $i++) {
    Start-Sleep -Seconds 5
    $detail = Invoke-RestMethod -Uri "$wf/runs/$instanceId" -Headers $hdr
    if ($detail.instance.status -eq 2) { break }
    if ($detail.instance.status -eq 3) {
        Write-Host "FAIL: instance Failed" -ForegroundColor Red
        $detail.executions | Where-Object { $_.status -eq 2 } | ForEach-Object {
            Write-Host "  node=$($_.nodeId) err=$($_.errorMessage)" -ForegroundColor Red
        }
        exit 1
    }
}

if ($detail.instance.status -ne 2) {
    Write-Host "FAIL: instance not Completed (status=$($detail.instance.status))" -ForegroundColor Red
    exit 1
}

$required = @("log_ctx_1", "approval_1", "wi_create_1", "log_ok_1")
$failed = @()
foreach ($nodeId in $required) {
    $exec = $detail.executions | Where-Object { $_.nodeId -eq $nodeId -and $_.status -eq 1 } | Select-Object -First 1
    if (-not $exec) { $failed += $nodeId }
}

if ($failed.Count -gt 0) {
    Write-Host "FAIL: nodes not Success: $($failed -join ', ')" -ForegroundColor Red
    exit 1
}

Write-Host "OK: P4 alarm -> approval -> workitem E2E passed (run $instanceId)" -ForegroundColor Green
exit 0
