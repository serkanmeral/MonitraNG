# Smoke: F4-5 ozet WBS ust is + yaprak parentItemId (Odak test)
param(
    [string]$Gateway = "http://192.168.20.20:5040",
    [string]$TokenFile = "$env:TEMP\operationcore_dg_token.txt",
    [switch]$KeepArtifacts
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../../..")).Path
$loadToken = Join-Path $repoRoot "docs/odak/operationcore/scripts/load-operationcore-token.ps1"

$stamp = Get-Date -Format "yyyyMMddHHmmss"
$code = "F45-$stamp"

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
        TimeoutSec           = 180
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

function Get-WbsRows($detail) {
    if ($detail.wbs) { return @($detail.wbs) }
    if ($detail.Wbs) { return @($detail.Wbs) }
    return @()
}

function Get-Kind($row) {
    $kind = [string]$row.kind
    if (-not $kind) { $kind = [string]$row.Kind }
    return $kind
}

function Get-RowId($row) {
    foreach ($k in @("id", "Id", "__dataId", "dataId")) {
        $v = [string]$row.$k
        if (-not [string]::IsNullOrWhiteSpace($v)) { return $v }
    }
    return ""
}

function Get-WorkItemId($row) {
    $v = [string]$row.workItemId
    if (-not $v) { $v = [string]$row.WorkItemId }
    return $v
}

function Get-ParentId($row) {
    $v = [string]$row.parentId
    if (-not $v) { $v = [string]$row.ParentId }
    return $v
}

function Get-ParentItemId($row) {
    $p = $row.parentItemId
    if ($null -eq $p) { $p = $row.ParentItemId }
    if ($null -eq $p) { return "" }
    if ($p -is [string]) { return [string]$p }
    foreach ($k in @("__dataId", "dataId", "id", "Id")) {
        $v = [string]$p.$k
        if (-not [string]::IsNullOrWhiteSpace($v)) { return $v }
    }
    return [string]$p
}

$token = Get-Token
$script:Headers = @{ Authorization = "Bearer $token" }
$script:LastStatus = 0
$projectIds = @()
$workspaceIds = @()

Write-Host "F4-5 smoke  code=$code  gateway=$Gateway" -ForegroundColor Cyan

try {
    $created = @(Invoke-Ops -Method POST -Path "/projects" -Body @{
            code     = $code
            name     = "F4-5 parent $stamp"
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
    $rows = @(Get-WbsRows $detail)
    Assert-True ($rows.Count -ge 12) "WBS=$($rows.Count)"
    $bound = @($rows | Where-Object { -not [string]::IsNullOrWhiteSpace((Get-WorkItemId $_)) })
    Assert-True ($bound.Count -eq $rows.Count) "tum WBS bagli $($bound.Count)/$($rows.Count)"

    $summaries = @($rows | Where-Object { (Get-Kind $_) -eq "summary" })
    Assert-True ($summaries.Count -ge 4) "ozet WBS=$($summaries.Count)"
    $sumBound = @($summaries | Where-Object { -not [string]::IsNullOrWhiteSpace((Get-WorkItemId $_)) })
    Assert-True ($sumBound.Count -eq $summaries.Count) "ozetler bagli $($sumBound.Count)/$($summaries.Count)"

    $items = @(Get-DgItems -Collection "op_work_items" -Filter "workspaceId:eq:$wsId")
    Assert-True ($items.Count -ge $rows.Count) "op_work_items=$($items.Count) >= $($rows.Count)"

    $byWbsId = @{}
    foreach ($row in $rows) { $byWbsId[(Get-RowId $row)] = $row }
    $byWiId = @{}
    foreach ($wi in $items) { $byWiId[(Get-RowId $wi)] = $wi }

    $linked = 0
    foreach ($row in $rows) {
        if ((Get-Kind $row) -eq "summary") { continue }
        $parentWbsId = Get-ParentId $row
        if ([string]::IsNullOrWhiteSpace($parentWbsId)) { continue }
        $parent = $byWbsId[$parentWbsId]
        if ($null -eq $parent) { continue }
        $childWi = Get-WorkItemId $row
        $parentWi = Get-WorkItemId $parent
        $wi = $byWiId[$childWi]
        Assert-True ($null -ne $wi) "yaprak WI var $childWi"
        $got = Get-ParentItemId $wi
        Assert-True ($got -eq $parentWi) "parentItemId $got == ozet WI $parentWi"
        $linked++
    }
    Assert-True ($linked -ge 8) "yaprak parent bag=$linked"

    $preview = @(Invoke-Ops -Path "/projects/$projectId/packs/pmo/preview?intent=apply")[0]
    Assert-True ([int]$preview.workItemCreateCount -eq 0) "reapply onizleme workItemCreate=0"
    Assert-True ([int]$preview.workItemSkipCount -ge $rows.Count) "reapply onizleme workItemSkip=$($preview.workItemSkipCount)"

    $reapply = @(Invoke-Ops -Method POST -Path "/projects/$projectId/packs/pmo?mode=skip")[0]
    Assert-True ([int]$reapply.workItemsCreated -eq 0) "reapply workItemsCreated=0"

    $detachPreview = @(Invoke-Ops -Path "/projects/$projectId/packs/pmo/preview?intent=detach")[0]
    Assert-True ([int]$detachPreview.workItemRemoveCount -ge $rows.Count) "sokme onizleme workItemRemove=$($detachPreview.workItemRemoveCount)"

    $detached = @(Invoke-Ops -Method DELETE -Path "/projects/$projectId/packs/pmo")[0]
    Assert-True ([int]$detached.workItemsRemoved -ge $rows.Count) "sokme workItemsRemoved=$($detached.workItemsRemoved)"

    $after = @(Invoke-Ops -Path "/projects/$projectId")[0]
    $rowsAfter = @(Get-WbsRows $after)
    Assert-True ($rowsAfter.Count -eq 0) "sokme WBS yok"

    $itemsAfter = @(Get-DgItems -Collection "op_work_items" -Filter "workspaceId:eq:$wsId")
    Assert-True ($itemsAfter.Count -eq 0) "sokme is kayitlarini siler ($($itemsAfter.Count))"

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

    Write-Host "F4-5 smoke OK" -ForegroundColor Green
}
catch {
    Write-Host "F4-5 smoke FAIL: $($_.Exception.Message)" -ForegroundColor Red
    if (-not $KeepArtifacts) {
        foreach ($id in $projectIds) {
            try { Invoke-Ops -Method DELETE -Path "/projects/$id" -ExpectStatus @(204, 200, 404) | Out-Null } catch { }
        }
        foreach ($id in $workspaceIds) { try { Remove-PackWorkspace $id } catch { } }
    }
    throw
}
