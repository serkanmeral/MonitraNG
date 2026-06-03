# parallel.fork → branches → parallel.join → final log
param(
    [string]$Gateway = "http://192.168.20.20:5040",
    [string]$Domain = "odak"
)

$ErrorActionPreference = "Stop"
$tokenScript = Join-Path $PSScriptRoot "..\tests\MngDataGateway\auth\get-token.ps1"
$token = & $tokenScript -KeeperBaseUrl $Gateway -DomainName $Domain -Username "odak_admin" -Password "Admin123!"
$hdr = @{ Authorization = "Bearer $token"; "X-Domain-Name" = $Domain; "Content-Type" = "application/json" }
$wf = "$Gateway/workflow/api/v1"

Write-Host "1) Workflow parallel.fork + join E2E..." -ForegroundColor Cyan
$def = Invoke-RestMethod -Uri "$wf/definitions" -Method POST -Headers $hdr -Body (@{
    key  = "parallel-join-$(Get-Random)"
    name = "Parallel Join E2E"
} | ConvertTo-Json)

$verBody = @{
    entryNodeId = "manual_1"
    nodes       = @(
        @{ id = "manual_1"; type = "manual.trigger"; config = @{} },
        @{ id = "fork_1"; type = "parallel.fork"; config = @{ branches = @("branch_a", "branch_b") } },
        @{ id = "log_a"; type = "write.log"; config = @{ message = "branch a" } },
        @{ id = "log_b"; type = "write.log"; config = @{ message = "branch b" } },
        @{ id = "join_1"; type = "parallel.join"; config = @{} },
        @{ id = "log_final"; type = "write.log"; config = @{ message = "after join" } }
    )
    edges = @(
        @{ fromNodeId = "manual_1"; toNodeId = "fork_1"; edgeKey = "default" },
        @{ fromNodeId = "fork_1"; toNodeId = "log_a"; edgeKey = "branch_a" },
        @{ fromNodeId = "fork_1"; toNodeId = "log_b"; edgeKey = "branch_b" },
        @{ fromNodeId = "log_a"; toNodeId = "join_1"; edgeKey = "default" },
        @{ fromNodeId = "log_b"; toNodeId = "join_1"; edgeKey = "default" },
        @{ fromNodeId = "join_1"; toNodeId = "log_final"; edgeKey = "default" }
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

$okJoin = $detail.executions | Where-Object { $_.nodeId -eq "join_1" -and $_.status -eq 1 }
$okFinal = $detail.executions | Where-Object { $_.nodeId -eq "log_final" -and $_.status -eq 1 }
if (-not $okJoin -or -not $okFinal) {
    Write-Host "FAIL: join or final log missing" -ForegroundColor Red
    exit 1
}

Write-Host "OK parallel.join E2E (run $instanceId)" -ForegroundColor Green
exit 0
