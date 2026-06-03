# oc.workitem.created → workflow Event Trigger E2E
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
$data = "$Gateway/data/api/v1/data"
$mo = "$Gateway/operations/api/v1"
$wf = "$Gateway/workflow/api/v1"

function Get-DgList($resp) {
    if ($null -eq $resp) { return @() }
    if ($resp.data) { return @($resp.data) }
    return @($resp)
}

function Get-DataId($obj) {
    if ($null -eq $obj) { return $null }
    if ($obj -is [string]) { return $obj.Trim() }
    if ($obj.data -and $obj.data.__dataId) { return "$($obj.data.__dataId)".Trim() }
    if ($obj.__dataId) { return "$($obj.__dataId)".Trim() }
    return $null
}

Write-Host "1) Workflow (oc.workitem.created → log)..." -ForegroundColor Cyan
$def = Invoke-RestMethod -Uri "$wf/definitions" -Method POST -Headers $hdr -Body (@{
    key = "oc-wi-created-$(Get-Random)"; name = "OC WorkItem Created E2E"
} | ConvertTo-Json)
$verBody = @{
    entryNodeId = "manual_1"
    nodes = @(
        @{ id = "manual_1"; type = "manual.trigger"; config = @{} },
        @{ id = "log_1"; type = "write.log"; config = @{ message = "oc workitem created e2e" } }
    )
    edges = @(@{ fromNodeId = "manual_1"; toNodeId = "log_1"; edgeKey = "default" })
    triggers = @(@{
        type    = "event"
        enabled = $true
        config  = @{ eventType = "oc.workitem.created" }
    })
} | ConvertTo-Json -Depth 8
$ver = Invoke-RestMethod -Uri "$wf/definitions/$($def.id)/versions" -Method POST -Headers $hdr -Body $verBody
Invoke-RestMethod -Uri "$wf/versions/$($ver.id)/publish" -Method POST -Headers $hdr | Out-Null
Write-Host "   workflowId=$($def.id)" -ForegroundColor Gray
Start-Sleep -Seconds 5

Write-Host "2) Board cozumle + WI olustur..." -ForegroundColor Cyan
$boards = Get-DgList (Invoke-RestMethod -Uri "$data/op_boards?filter=workspaceId:eq:$WorkspaceId&limit=5" -Headers $hdr)
$boardId = Get-DataId ($boards | Select-Object -First 1)
$created = Invoke-RestMethod -Uri "$mo/work-items" -Method POST -Headers $hdr -Body (@{
    workspaceId = $WorkspaceId
    boardId     = $boardId
    typeId      = $TypeId
    title       = "OC event E2E $(Get-Date -Format 'HH:mm:ss')"
    description = "oc.workitem.created trigger test"
} | ConvertTo-Json)
Write-Host "   created $($created.workItem.key)" -ForegroundColor Gray

Write-Host "3) Workflow run bekleniyor..." -ForegroundColor Yellow
Start-Sleep -Seconds 15
$runs = Invoke-RestMethod -Uri "$wf/runs?workflowId=$($def.id)&limit=3" -Headers $hdr
$count = @($runs).Count
if ($count -eq 0) {
    Write-Host "FAIL: no runs for oc.workitem.created" -ForegroundColor Red
    exit 1
}
$detail = Invoke-RestMethod -Uri "$wf/runs/$($runs[0].id)" -Headers $hdr
$ok = $detail.instance.status -eq 2 -and ($detail.executions | Where-Object { $_.nodeId -eq "log_1" -and $_.status -eq 1 })
if ($ok) {
    Write-Host "OK: run $($runs[0].id) Completed" -ForegroundColor Green
    exit 0
}
Write-Host "FAIL: status=$($detail.instance.status)" -ForegroundColor Red
exit 1
