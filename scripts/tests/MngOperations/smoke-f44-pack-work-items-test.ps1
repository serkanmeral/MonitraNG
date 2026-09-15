# Smoke: F4-4 paketten yaprak WBS için OC iş kaydı (Odak test)
param(
    [string]$Gateway = "http://192.168.20.20:5040",
    [string]$TokenFile = "$env:TEMP\operationcore_dg_token.txt",
    [switch]$KeepArtifacts
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../../..")).Path
$loadToken = Join-Path $repoRoot "docs/odak/operationcore/scripts/load-operationcore-token.ps1"

$stamp = Get-Date -Format "yyyyMMddHHmmss"
$code = "F44-$stamp"

function Get-Token {
    $fresh = & $loadToken -AutoRefresh
    if ($fresh) { return $fresh.Trim() }
    if (Test-Path $TokenFile) {
        $t = (Get-Content $TokenFile -Raw).Trim()
        if ($t) { return $t }
    }
    throw "Token alinamadi."
}

function Invoke-Ops {
    param(
        [string]$Method = "GET",
        [string]$Path,
        [object]$Body = $null,
        [int[]]$ExpectStatus = @(200, 201, 204)
    )
    $uri = "$Gateway/operations/api/v1$Path"
    $status = 0
    $params = @{
        Uri                  = $uri
        Method               = $Method
        Headers              = $script:Headers
        TimeoutSec           = 120
        SkipCertificateCheck = $true
        SkipHttpErrorCheck   = $true
        StatusCodeVariable   = "status"
    }
    if ($null -ne $Body) {
        $params.ContentType = "application/json"
        $params.Body = ($Body | ConvertTo-Json -Depth 8 -Compress)
    }
    $result = Invoke-RestMethod @params
    $script:LastStatus = [int]$status
    if ($ExpectStatus -notcontains $script:LastStatus) {
        $err = $null
        if ($result) {
            try { $err = $result | ConvertTo-Json -Compress -Depth 6 } catch { $err = [string]$result }
        }
        throw "HTTP $script:LastStatus $Method $Path : $err"
    }
    return , $result
}

function Get-DgItems {
    param([string]$Collection, [string]$Filter)
    $uri = "$Gateway/data/api/v1/data/$Collection`?limit=100"
    if ($Filter) { $uri += "&filter=" + [Uri]::EscapeDataString($Filter) }
    $status = 0
    $result = Invoke-RestMethod -Uri $uri -Headers $script:Headers -SkipCertificateCheck -SkipHttpErrorCheck -StatusCodeVariable status -TimeoutSec 60
    if ([int]$status -ge 400) { return @() }
    if ($null -eq $result) { return @() }
    if ($result -is [Array]) { return @($result) }
    foreach ($prop in @("data", "Data", "items", "Items")) {
        if ($null -ne $result.$prop) { return @($result.$prop) }
    }
    return @($result)
}

function Remove-PackWorkspace {
    param([string]$WorkspaceId)
    if ([string]::IsNullOrWhiteSpace($WorkspaceId)) { return }
    foreach ($ds in @("op_work_items", "op_dashboards", "op_sla_policies", "op_rules", "op_boards", "op_forms", "op_profiles", "op_work_item_types", "op_state_flows")) {
        foreach ($row in @(Get-DgItems -Collection $ds -Filter "workspaceId:eq:$WorkspaceId")) {
            $id = $row.__dataId
            if (-not $id) { $id = $row.dataId }
            if (-not $id) { continue }
            try {
                Invoke-RestMethod -Uri "$Gateway/data/api/v1/data/$ds/$id" -Method DELETE -Headers $script:Headers -SkipCertificateCheck -SkipHttpErrorCheck -TimeoutSec 30 | Out-Null
            }
            catch { }
        }
    }
    try {
        Invoke-RestMethod -Uri "$Gateway/data/api/v1/data/op_workspaces/$WorkspaceId" -Method DELETE -Headers $script:Headers -SkipCertificateCheck -SkipHttpErrorCheck -TimeoutSec 30 | Out-Null
    }
    catch { }
}

function Assert-True($cond, [string]$msg) {
    if (-not $cond) { throw "FAIL: $msg" }
    Write-Host "  OK $msg" -ForegroundColor Green
}

function Get-LeafWbs($detail) {
    $rows = @()
    if ($detail.wbs) { $rows = @($detail.wbs) }
    elseif ($detail.Wbs) { $rows = @($detail.Wbs) }
    return @($rows | Where-Object {
            $kind = [string]$_.kind
            if (-not $kind) { $kind = [string]$_.Kind }
            $kind -in @("task", "milestone")
        })
}

$token = Get-Token
$script:Headers = @{ Authorization = "Bearer $token" }
$script:LastStatus = 0
$projectIds = @()
$workspaceIds = @()

Write-Host "F4-4 smoke  code=$code  gateway=$Gateway" -ForegroundColor Cyan

try {
    $created = @(Invoke-Ops -Method POST -Path "/projects" -Body @{
            code     = $code
            name     = "F4-4 wi $stamp"
            status   = "active"
            packCode = "pmo"
        } -ExpectStatus @(201, 200))[0]
    $projectId = [string]$created.id
    if (-not $projectId) { $projectId = [string]$created.Id }
    $projectIds += $projectId
    $wsId = [string]$created.workspaceId
    if (-not $wsId) { $wsId = [string]$created.WorkspaceId }
    Assert-True ($projectId) "create id=$projectId"
    Assert-True ($wsId) "workspaceId=$wsId"
    $workspaceIds += $wsId

    $detail = @(Invoke-Ops -Path "/projects/$projectId")[0]
    $leaves = @(Get-LeafWbs $detail)
    Assert-True ($leaves.Count -ge 8) "yaprak WBS=$($leaves.Count)"
    $bound = @($leaves | Where-Object {
            $wid = [string]$_.workItemId
            if (-not $wid) { $wid = [string]$_.WorkItemId }
            -not [string]::IsNullOrWhiteSpace($wid)
        })
    Assert-True ($bound.Count -eq $leaves.Count) "tum yapraklar bagli $($bound.Count)/$($leaves.Count)"

    $items = @(Get-DgItems -Collection "op_work_items" -Filter "workspaceId:eq:$wsId")
    Assert-True ($items.Count -ge $leaves.Count) "op_work_items=$($items.Count) >= $($leaves.Count)"

    $preview = @(Invoke-Ops -Path "/projects/$projectId/packs/pmo/preview?intent=apply")[0]
    Assert-True ([int]$preview.workItemCreateCount -eq 0) "reapply onizleme workItemCreate=0"
    Assert-True ([int]$preview.workItemSkipCount -ge $leaves.Count) "reapply onizleme workItemSkip=$($preview.workItemSkipCount)"

    $reapply = @(Invoke-Ops -Method POST -Path "/projects/$projectId/packs/pmo?mode=skip")[0]
    Assert-True ([int]$reapply.workItemsCreated -eq 0) "reapply workItemsCreated=0"

    $items2 = @(Get-DgItems -Collection "op_work_items" -Filter "workspaceId:eq:$wsId")
    Assert-True ($items2.Count -eq $items.Count) "reapply is sayisi ayni=$($items2.Count)"

    $detachPreview = @(Invoke-Ops -Path "/projects/$projectId/packs/pmo/preview?intent=detach")[0]
    Assert-True ([int]$detachPreview.workItemRemoveCount -ge $leaves.Count) "sökme onizleme workItemRemove=$($detachPreview.workItemRemoveCount)"

    $detached = @(Invoke-Ops -Method DELETE -Path "/projects/$projectId/packs/pmo")[0]
    Assert-True ([int]$detached.workItemsRemoved -ge $leaves.Count) "sökme workItemsRemoved=$($detached.workItemsRemoved)"

    $after = @(Invoke-Ops -Path "/projects/$projectId")[0]
    $leavesAfter = @(Get-LeafWbs $after)
    Assert-True ($leavesAfter.Count -eq 0) "sökme yaprak WBS yok"

    $itemsAfter = @(Get-DgItems -Collection "op_work_items" -Filter "workspaceId:eq:$wsId")
    Assert-True ($itemsAfter.Count -eq 0) "sökme is kayitlarini siler ($($itemsAfter.Count))"

    if (-not $KeepArtifacts) {
        foreach ($id in $projectIds) {
            Invoke-Ops -Method DELETE -Path "/projects/$id" -ExpectStatus @(204, 200, 404) | Out-Null
        }
        $projectIds = @()
        foreach ($id in $workspaceIds) { Remove-PackWorkspace $id }
        $workspaceIds = @()
        Write-Host "  cleanup OK" -ForegroundColor Green
    }
    else {
        Write-Host "KeepArtifacts: projeler birakildi $($projectIds -join ',')" -ForegroundColor Yellow
    }

    Write-Host "F4-4 smoke OK" -ForegroundColor Green
}
catch {
    Write-Host "F4-4 smoke FAIL: $($_.Exception.Message)" -ForegroundColor Red
    if (-not $KeepArtifacts) {
        foreach ($id in $projectIds) {
            try { Invoke-Ops -Method DELETE -Path "/projects/$id" -ExpectStatus @(204, 200, 404) | Out-Null } catch { }
        }
        foreach ($id in $workspaceIds) { try { Remove-PackWorkspace $id } catch { } }
    }
    throw
}
