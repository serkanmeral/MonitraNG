# P4-B — engine.command / block.ip node (Reactor mqtt/publish on Odak)
param(
    [string]$Gateway = "http://192.168.20.20:5040",
    [string]$Domain = "odak"
)

$ErrorActionPreference = "Stop"
$tokenScript = Join-Path $PSScriptRoot "..\tests\MngDataGateway\auth\get-token.ps1"
$token = & $tokenScript -KeeperBaseUrl $Gateway -DomainName $Domain -Username "odak_admin" -Password "Admin123!"
$hdr = @{ Authorization = "Bearer $token"; "X-Domain-Name" = $Domain; "Content-Type" = "application/json" }
$wf = "$Gateway/workflow/api/v1"

Write-Host "1) Workflow manual -> block.ip -> log..." -ForegroundColor Cyan
$def = Invoke-RestMethod -Uri "$wf/definitions" -Method POST -Headers $hdr -Body (@{
    key  = "p4-engine-cmd-$(Get-Random)"
    name = "P4 Engine Command E2E"
} | ConvertTo-Json)

$verBody = @{
    entryNodeId = "manual_1"
    nodes       = @(
        @{ id = "manual_1"; type = "manual.trigger"; config = @{} },
        @{
            id     = "block_1"
            type   = "block.ip"
            config = @{
                engineId  = "odak-engine-e2e"
                ipAddress = "10.99.0.99"
                ttlMinutes = 60
                reason    = "P4 E2E test block"
            }
        },
        @{ id = "log_1"; type = "write.log"; config = @{ message = "after engine command" } }
    )
    edges = @(
        @{ fromNodeId = "manual_1"; toNodeId = "block_1"; edgeKey = "default" },
        @{ fromNodeId = "block_1"; toNodeId = "log_1"; edgeKey = "default" }
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
        $detail.executions | Where-Object { $_.status -eq 2 } | ForEach-Object {
            Write-Host "  node $($_.nodeId): $($_.errorMessage)" -ForegroundColor Red
        }
        exit 1
    }
}

if ($detail.instance.status -ne 2) {
    Write-Host "FAIL: not Completed (status=$($detail.instance.status))" -ForegroundColor Red
    exit 1
}

$blockExec = $detail.executions | Where-Object { $_.nodeId -eq "block_1" -and $_.status -eq 1 } | Select-Object -First 1
$logExec = $detail.executions | Where-Object { $_.nodeId -eq "log_1" -and $_.status -eq 1 } | Select-Object -First 1
if (-not $blockExec -or -not $logExec) {
    Write-Host "FAIL: block.ip or log node missing success" -ForegroundColor Red
    exit 1
}

$mode = $null
if ($blockExec.outputJson) {
    try {
        $out = $blockExec.outputJson | ConvertFrom-Json
        $mode = $out.mode
    } catch { }
}

if ($mode -ne "reactor_mqtt") {
    Write-Host "FAIL: block.ip mode=$mode (beklenen reactor_mqtt)" -ForegroundColor Red
    Write-Host "  outputJson: $($blockExec.outputJson)" -ForegroundColor DarkGray
    exit 1
}

Write-Host "OK P4 engine.command E2E (run $instanceId, mode=reactor_mqtt)" -ForegroundColor Green
exit 0
