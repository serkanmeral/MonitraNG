# parallel.fork → two log branches → instance Completed when both finish
param(
    [string]$Gateway = "http://192.168.20.20:5040",
    [string]$Domain = "odak"
)

$ErrorActionPreference = "Stop"
$tokenScript = Join-Path $PSScriptRoot "..\tests\MngDataGateway\auth\get-token.ps1"
$token = & $tokenScript -KeeperBaseUrl $Gateway -DomainName $Domain -Username "odak_admin" -Password "Admin123!"
$hdr = @{ Authorization = "Bearer $token"; "X-Domain-Name" = $Domain; "Content-Type" = "application/json" }
$wf = "$Gateway/workflow/api/v1"

Write-Host "1) Workflow parallel.fork E2E..." -ForegroundColor Cyan
$def = Invoke-RestMethod -Uri "$wf/definitions" -Method POST -Headers $hdr -Body (@{
    key  = "parallel-fork-$(Get-Random)"
    name = "Parallel Fork E2E"
} | ConvertTo-Json)

$verBody = @{
    entryNodeId = "manual_1"
    nodes       = @(
        @{ id = "manual_1"; type = "manual.trigger"; config = @{} },
        @{ id = "fork_1"; type = "parallel.fork"; config = @{ branches = @("branch_a", "branch_b") } },
        @{ id = "log_a"; type = "write.log"; config = @{ message = "parallel branch a" } },
        @{ id = "log_b"; type = "write.log"; config = @{ message = "parallel branch b" } }
    )
    edges = @(
        @{ fromNodeId = "manual_1"; toNodeId = "fork_1"; edgeKey = "default" },
        @{ fromNodeId = "fork_1"; toNodeId = "log_a"; edgeKey = "branch_a" },
        @{ fromNodeId = "fork_1"; toNodeId = "log_b"; edgeKey = "branch_b" }
    )
    triggers = @()
} | ConvertTo-Json -Depth 10

$ver = Invoke-RestMethod -Uri "$wf/definitions/$($def.id)/versions" -Method POST -Headers $hdr -Body $verBody
Invoke-RestMethod -Uri "$wf/versions/$($ver.id)/publish" -Method POST -Headers $hdr | Out-Null

Write-Host "2) Start manual run..." -ForegroundColor Yellow
$run = Invoke-RestMethod -Uri "$wf/runs" -Method POST -Headers $hdr -Body (@{
    workflowId = $def.id; triggerType = "manual"
} | ConvertTo-Json)
$instanceId = $run.instanceId

Write-Host "3) Wait for Completed..." -ForegroundColor Yellow
$detail = $null
for ($i = 0; $i -lt 24; $i++) {
    Start-Sleep -Seconds 3
    $detail = Invoke-RestMethod -Uri "$wf/runs/$instanceId" -Headers $hdr
    if ($detail.instance.status -eq 2) { break }
    if ($detail.instance.status -eq 3) {
        Write-Host "FAIL: instance Failed" -ForegroundColor Red
        exit 1
    }
}

if ($detail.instance.status -ne 2) {
    Write-Host "FAIL: not Completed (status=$($detail.instance.status))" -ForegroundColor Red
    exit 1
}

$okA = $detail.executions | Where-Object { $_.nodeId -eq "log_a" -and $_.status -eq 1 }
$okB = $detail.executions | Where-Object { $_.nodeId -eq "log_b" -and $_.status -eq 1 }
if (-not $okA -or -not $okB) {
    Write-Host "FAIL: parallel branches did not both succeed" -ForegroundColor Red
    exit 1
}

Write-Host "OK parallel.fork E2E (run $instanceId)" -ForegroundColor Green
exit 0
