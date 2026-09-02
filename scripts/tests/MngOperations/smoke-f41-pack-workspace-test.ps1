# Smoke: F4-1 paketten ince OC workspace iskeleti (Odak test)
param(
    [string]$Gateway = "http://192.168.20.20:5040",
    [string]$TokenFile = "$env:TEMP\operationcore_dg_token.txt",
    [switch]$KeepArtifacts
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../../..")).Path
$loadToken = Join-Path $repoRoot "docs/odak/operationcore/scripts/load-operationcore-token.ps1"

$stamp = Get-Date -Format "yyyyMMddHHmmss"
$code = "F41-$stamp"

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
        TimeoutSec           = 60
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
    $uri = "$Gateway/data/api/v1/data/$Collection`?limit=50"
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
    foreach ($ds in @("op_boards", "op_forms", "op_profiles", "op_work_item_types", "op_state_flows")) {
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

$token = Get-Token
$script:Headers = @{ Authorization = "Bearer $token" }
$script:LastStatus = 0
$projectIds = @()
$workspaceIds = @()

Write-Host "F4-1 smoke  code=$code  gateway=$Gateway" -ForegroundColor Cyan

try {
    $empty = @(Invoke-Ops -Method POST -Path "/projects" -Body @{
            code   = "$code-E"
            name   = "F4-1 empty $stamp"
            status = "active"
        } -ExpectStatus @(201, 200))[0]
    $emptyId = [string]$empty.id
    $projectIds += $emptyId
    Assert-True (-not $empty.workspaceId) "bos proje workspaceId bos"

    $preview = @(Invoke-Ops -Path "/projects/$emptyId/packs/pmo/preview?intent=apply")[0]
    Assert-True ($preview.workspaceAction -eq "create") "onizleme workspaceAction=create"
    Assert-True ([string]$preview.workspaceName -eq "PM $code-E") "onizleme workspaceName=$($preview.workspaceName)"

    $applied = @(Invoke-Ops -Method POST -Path "/projects/$emptyId/packs/pmo?mode=skip")[0]
    Assert-True ($applied.workspaceCreated -eq $true) "apply workspaceCreated=true"
    $wsId = [string]$applied.workspaceId
    Assert-True ($wsId) "apply workspaceId=$wsId"
    $workspaceIds += $wsId

    $detail = @(Invoke-Ops -Path "/projects/$emptyId")[0]
    $linked = [string]$detail.project.workspaceId
    if (-not $linked) { $linked = [string]$detail.workspaceId }
    Assert-True ($linked -eq $wsId) "proje workspaceId bagli"

    $form = @(Invoke-Ops -Path "/runtime/work-items/form?mode=create&workspaceId=$wsId")[0]
    $typeCount = 0
    if ($form.types) { $typeCount = @($form.types).Count }
    Assert-True ($script:LastStatus -eq 200) "form create runtime 200"
    Assert-True ($typeCount -ge 1) "form types=$typeCount"

    $reapply = @(Invoke-Ops -Method POST -Path "/projects/$emptyId/packs/pmo?mode=skip")[0]
    Assert-True ($reapply.workspaceCreated -eq $false) "reapply workspaceCreated=false"
    Assert-True ([string]$reapply.workspaceId -eq $wsId) "reapply ayni workspace"

    $preview2 = @(Invoke-Ops -Path "/projects/$emptyId/packs/architecture/preview?intent=apply")[0]
    Assert-True ($preview2.workspaceAction -eq "skip") "ikinci paket onizleme workspace skip"

    $packed = @(Invoke-Ops -Method POST -Path "/projects" -Body @{
            code     = "$code-P"
            name     = "F4-1 packed $stamp"
            status   = "active"
            packCode = "pmo"
        } -ExpectStatus @(201, 200))[0]
    $packedId = [string]$packed.id
    $projectIds += $packedId
    $packedWs = [string]$packed.workspaceId
    Assert-True ($packedWs) "packCode ile create workspaceId=$packedWs"
    Assert-True ($packedWs -ne $wsId) "ikinci proje ayri workspace"
    $workspaceIds += $packedWs

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

    Write-Host "F4-1 smoke OK" -ForegroundColor Green
}
catch {
    Write-Host "F4-1 smoke FAIL: $($_.Exception.Message)" -ForegroundColor Red
    if (-not $KeepArtifacts) {
        foreach ($id in $projectIds) {
            try { Invoke-Ops -Method DELETE -Path "/projects/$id" -ExpectStatus @(204, 200, 404) | Out-Null } catch { }
        }
        foreach ($id in $workspaceIds) { try { Remove-PackWorkspace $id } catch { } }
    }
    throw
}
