# Smoke: F4-3 paketten kural / SLA / pano (Odak test)
param(
    [string]$Gateway = "http://192.168.20.20:5040",
    [string]$TokenFile = "$env:TEMP\operationcore_dg_token.txt",
    [switch]$KeepArtifacts
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../../..")).Path
$loadToken = Join-Path $repoRoot "docs/odak/operationcore/scripts/load-operationcore-token.ps1"

$stamp = Get-Date -Format "yyyyMMddHHmmss"
$code = "F43-$stamp"

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
    foreach ($ds in @("op_dashboards", "op_sla_policies", "op_rules", "op_boards", "op_forms", "op_profiles", "op_work_item_types", "op_state_flows")) {
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

Write-Host "F4-3 smoke  code=$code  gateway=$Gateway" -ForegroundColor Cyan

try {
    $catalog = @(Invoke-Ops -Path "/job-packs")[0]
    $pmo = @($catalog) | Where-Object { $_.code -eq "pmo" } | Select-Object -First 1
    Assert-True ($null -ne $pmo) "pmo katalogda"
    Assert-True ([int]$pmo.ruleCount -ge 1) "pmo ruleCount=$($pmo.ruleCount)"
    Assert-True ([int]$pmo.slaCount -eq 0) "pmo slaCount=$($pmo.slaCount)"
    Assert-True ([int]$pmo.dashboardCount -ge 1) "pmo dashboardCount=$($pmo.dashboardCount)"
    Assert-True ([string]$pmo.version -eq "1.1.1") "pmo version=1.1.1"

    $created = @(Invoke-Ops -Method POST -Path "/projects" -Body @{
            code     = $code
            name     = "F4-3 oc $stamp"
            status   = "active"
            packCode = "pmo"
        } -ExpectStatus @(201, 200))[0]
    $projectId = [string]$created.id
    $projectIds += $projectId
    $wsId = [string]$created.workspaceId
    Assert-True ($projectId) "create id=$projectId"
    Assert-True ($wsId) "workspaceId=$wsId"
    $workspaceIds += $wsId

    $rules = @(Get-DgItems -Collection "op_rules" -Filter "workspaceId:eq:$wsId")
    $sla = @(Get-DgItems -Collection "op_sla_policies" -Filter "workspaceId:eq:$wsId")
    $dash = @(Get-DgItems -Collection "op_dashboards" -Filter "workspaceId:eq:$wsId")
    Assert-True ($rules.Count -ge 1) "op_rules=$($rules.Count)"
    Assert-True ($sla.Count -eq 0) "op_sla_policies=$($sla.Count)"
    Assert-True ($dash.Count -ge 1) "op_dashboards=$($dash.Count)"
    Assert-True (($rules | Where-Object { [string]$_.name -like "PMO *" }).Count -ge 1) "PMO kural adi"

    $preview = @(Invoke-Ops -Path "/projects/$projectId/packs/pmo/preview?intent=apply")[0]
    Assert-True ([int]$preview.ruleCreateCount -eq 0) "reapply onizleme ruleCreate=0"
    Assert-True ([int]$preview.slaCreateCount -eq 0) "reapply onizleme slaCreate=0"
    Assert-True ([int]$preview.dashboardCreateCount -eq 0) "reapply onizleme dashboardCreate=0"

    $reapply = @(Invoke-Ops -Method POST -Path "/projects/$projectId/packs/pmo?mode=skip")[0]
    Assert-True ([int]$reapply.rulesCreated -eq 0) "reapply rulesCreated=0"
    Assert-True ([int]$reapply.slaCreated -eq 0) "reapply slaCreated=0"
    Assert-True ([int]$reapply.dashboardsCreated -eq 0) "reapply dashboardsCreated=0"

    $rules2 = @(Get-DgItems -Collection "op_rules" -Filter "workspaceId:eq:$wsId")
    Assert-True ($rules2.Count -eq $rules.Count) "reapply kural sayisi ayni=$($rules2.Count)"

    $detached = @(Invoke-Ops -Method DELETE -Path "/projects/$projectId/packs/pmo")[0]
    Assert-True ($null -ne $detached) "sökme OK"
    $rulesAfter = @(Get-DgItems -Collection "op_rules" -Filter "workspaceId:eq:$wsId")
    Assert-True ($rulesAfter.Count -eq $rules.Count) "sökme kurali silmez"

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

    Write-Host "F4-3 smoke OK" -ForegroundColor Green
}
catch {
    Write-Host "F4-3 smoke FAIL: $($_.Exception.Message)" -ForegroundColor Red
    if (-not $KeepArtifacts) {
        foreach ($id in $projectIds) {
            try { Invoke-Ops -Method DELETE -Path "/projects/$id" -ExpectStatus @(204, 200, 404) | Out-Null } catch { }
        }
        foreach ($id in $workspaceIds) { try { Remove-PackWorkspace $id } catch { } }
    }
    throw
}
