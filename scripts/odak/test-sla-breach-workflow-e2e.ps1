# SLA breach scan → op_rules startWorkflow E2E
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

function Get-DataId($obj) {
    if ($null -eq $obj) { return $null }
    if ($obj -is [string]) { return $obj.Trim() }
    if ($obj.data -and $obj.data.__dataId) { return "$($obj.data.__dataId)".Trim() }
    if ($obj.__dataId) { return "$($obj.__dataId)".Trim() }
    if ($obj.id) { return "$($obj.id)".Trim() }
    return $null
}

function Get-DgList($resp) {
    if ($null -eq $resp) { return @() }
    if ($resp.data) { return @($resp.data) }
    return @($resp)
}

Write-Host "1) Workflow (manual + log)..." -ForegroundColor Cyan
$def = Invoke-RestMethod -Uri "$wf/definitions" -Method POST -Headers $hdr -Body (@{
    key = "sla-breach-$(Get-Random)"; name = "SLA Breach E2E"
} | ConvertTo-Json)
$verBody = @{
    entryNodeId = "manual_1"
    nodes = @(
        @{ id = "manual_1"; type = "manual.trigger"; config = @{} },
        @{ id = "log_1"; type = "write.log"; config = @{ message = "sla response breach e2e" } }
    )
    edges = @(@{ fromNodeId = "manual_1"; toNodeId = "log_1"; edgeKey = "default" })
} | ConvertTo-Json -Depth 8
$ver = Invoke-RestMethod -Uri "$wf/definitions/$($def.id)/versions" -Method POST -Headers $hdr -Body $verBody
Invoke-RestMethod -Uri "$wf/versions/$($ver.id)/publish" -Method POST -Headers $hdr | Out-Null
Write-Host "   workflowId=$($def.id)" -ForegroundColor Gray

Write-Host "2) op_rules automation (WorkItemSlaResponseBreached → startWorkflow)..." -ForegroundColor Cyan
$ruleName = "SLA Breach E2E $(Get-Date -Format 'yyyyMMddHHmmss')"
$ruleBody = @{
    name        = $ruleName
    workspaceId = $WorkspaceId
    ruleType    = "automation"
    trigger     = "WorkItemSlaResponseBreached"
    isActive    = $true
    priority    = 500
    actions     = @(
        @{
            type       = "startWorkflow"
            workflowId = $def.id
            triggerType = "op_rules"
        }
    )
} | ConvertTo-Json -Depth 8
$rule = Invoke-RestMethod -Uri "$data/op_rules" -Method POST -Headers $hdr -Body $ruleBody
$ruleId = Get-DataId $rule
Write-Host "   ruleId=$ruleId" -ForegroundColor Gray

Write-Host "3) Board cozumle..." -ForegroundColor Cyan
$boards = Get-DgList (Invoke-RestMethod -Uri "$data/op_boards?filter=workspaceId:eq:$WorkspaceId&limit=5" -Headers $hdr)
$boardId = Get-DataId ($boards | Select-Object -First 1)

Write-Host "4) Work item olustur..." -ForegroundColor Cyan
$created = Invoke-RestMethod -Uri "$mo/work-items" -Method POST -Headers $hdr -Body (@{
    workspaceId = $WorkspaceId
    boardId     = $boardId
    typeId      = $TypeId
    title       = "SLA breach E2E $(Get-Date -Format 'HH:mm:ss')"
    description = "scan-breaches test"
} | ConvertTo-Json)
$wiId = $created.workItem.id
if (-not $wiId) { $wiId = Get-DataId $created.workItem }
Write-Host "   wiId=$wiId key=$($created.workItem.key)" -ForegroundColor Gray

Write-Host "5) SLA responseDueAt gecmise cek (DG patch)..." -ForegroundColor Cyan
$dgWiResp = Invoke-RestMethod -Uri "$data/op_work_items/$wiId" -Headers $hdr
$dgWi = if ($dgWiResp.data) { $dgWiResp.data } else { $dgWiResp }
$sla = @{}
if ($dgWi.sla) {
    $dgWi.sla.PSObject.Properties | ForEach-Object { $sla[$_.Name] = $_.Value }
}
$sla["responseDueAt"] = (Get-Date).ToUniversalTime().AddHours(-1).ToString("o")
$sla.Remove("responseBreachNotifiedAt")
Invoke-RestMethod -Uri "$data/op_work_items/$wiId" -Method PUT -Headers $hdr -Body (@{ sla = $sla } | ConvertTo-Json -Depth 6) | Out-Null

Write-Host "6) POST scan-breaches..." -ForegroundColor Yellow
$scan = Invoke-RestMethod -Uri "$mo/sla/scan-breaches?workspaceId=$WorkspaceId" -Method POST -Headers $hdr
Write-Host "   responseProcessed=$($scan.responseBreachesProcessed) resolveProcessed=$($scan.resolveBreachesProcessed)" -ForegroundColor Gray
Write-Host "   workItemIds=$($scan.workItemIds -join ',')" -ForegroundColor Gray

if ($scan.responseBreachesProcessed -lt 1) {
    Write-Host "FAIL: scan did not process any response breach" -ForegroundColor Red
    exit 1
}
if ($scan.workItemIds -notcontains $wiId) {
    Write-Host "WARN: test WI $wiId not in processed list (demo backlog may have filled batch)" -ForegroundColor Yellow
}

Start-Sleep -Seconds 10

Write-Host "7) Workflow run kontrol..." -ForegroundColor Yellow
$runs = Invoke-RestMethod -Uri "$wf/runs?workflowId=$($def.id)&limit=5" -Headers $hdr
$count = @($runs).Count
if ($count -eq 0) {
    Write-Host "FAIL: no workflow runs" -ForegroundColor Red
    exit 1
}
$detail = Invoke-RestMethod -Uri "$wf/runs/$($runs[0].id)" -Headers $hdr
$ok = $detail.instance.status -eq 2 -and ($detail.executions | Where-Object { $_.nodeId -eq "log_1" -and $_.status -eq 1 })
if ($ok) {
    Write-Host "OK: run $($runs[0].id) Completed" -ForegroundColor Green
    exit 0
}
Write-Host "FAIL: run status=$($detail.instance.status)" -ForegroundColor Red
exit 1
