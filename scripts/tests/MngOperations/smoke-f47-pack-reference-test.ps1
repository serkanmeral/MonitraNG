# Smoke: F4-7 WBS is kaydi -> DI plan/kaynak belgesi bagi (Odak test)
param(
    [string]$Gateway = "http://192.168.20.20:5040",
    [string]$TokenFile = "$env:TEMP\operationcore_dg_token.txt",
    [switch]$KeepArtifacts
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../../..")).Path
$loadToken = Join-Path $repoRoot "docs/odak/operationcore/scripts/load-operationcore-token.ps1"

$stamp = Get-Date -Format "yyyyMMddHHmmss"
$code = "F47-$stamp"

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

function Invoke-Doc {
    param(
        [string]$Method = "GET",
        [string]$Path,
        [object]$Body = $null,
        [int[]]$ExpectStatus = @(200, 201, 204)
    )
    $uri = "$Gateway/documents/api/v1/resources$Path"
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
        $params.ContentType = "application/json; charset=utf-8"
        $params.Body = [System.Text.Encoding]::UTF8.GetBytes(($Body | ConvertTo-Json -Depth 8 -Compress))
    }
    $result = Invoke-RestMethod @params
    $script:LastStatus = [int]$status
    if ($ExpectStatus -notcontains $script:LastStatus) {
        $err = $null
        if ($result) {
            try { $err = $result | ConvertTo-Json -Compress -Depth 6 } catch { $err = [string]$result }
        }
        throw "HTTP $script:LastStatus $Method documents$Path : $err"
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

function Get-Items($response) {
    if ($null -eq $response) { return @() }
    if ($null -ne $response.items) { return @($response.items) }
    if ($response -is [Array]) { return @($response) }
    return @($response)
}

function Normalize-FolderName([string]$Name) {
    if ([string]::IsNullOrWhiteSpace($Name)) { return "" }
    $n = $Name.Trim().ToLowerInvariant()
    $n = $n.Replace([char]0x00F6, "o").Replace([char]0x00D6, "o")
    $n = $n.Replace([char]0x00FC, "u").Replace([char]0x00DC, "u")
    $n = $n.Replace([char]0x00E7, "c").Replace([char]0x00C7, "c")
    $n = $n.Replace([char]0x011F, "g").Replace([char]0x011E, "g")
    $n = $n.Replace([char]0x0131, "i").Replace([char]0x0130, "i")
    $n = $n.Replace([char]0x015F, "s").Replace([char]0x015E, "s")
    return $n
}

function Find-Folder {
    param([string]$Name, [string]$ParentId = $null)
    if ($ParentId) {
        $siblings = Get-Items (Invoke-Doc -Path "/children?parentId=$ParentId")
    }
    else {
        $siblings = Get-Items (Invoke-Doc -Path "/children")
    }
    $want = Normalize-FolderName $Name
    return $siblings | Where-Object {
        $isFolder = [string]$_.type -eq "folder" -or [string]::IsNullOrWhiteSpace([string]$_.type)
        $isFolder -and (Normalize-FolderName ([string]$_.name)) -eq $want
    } | Select-Object -First 1
}

function Ensure-Folder {
    param([string]$Name, [string]$ParentId = $null)
    $existing = Find-Folder -Name $Name -ParentId $ParentId
    if ($existing) { return [string]$existing.id }
    $body = @{ name = $Name }
    if ($ParentId) { $body.parentId = $ParentId }
    $created = @(Invoke-Doc -Method POST -Path "/folder" -Body $body)[0]
    return [string]$created.id
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

function Get-Name($row) {
    $v = [string]$row.name
    if (-not $v) { $v = [string]$row.Name }
    return $v
}

function Get-RowId($row) {
    foreach ($k in @("id", "Id", "__dataId", "dataId")) {
        $v = [string]$row.$k
        if (-not [string]::IsNullOrWhiteSpace($v)) { return $v }
    }
    return ""
}

function Get-HasEvidence($row) {
    if ($null -ne $row.hasEvidence) { return [bool]$row.hasEvidence }
    if ($null -ne $row.HasEvidence) { return [bool]$row.HasEvidence }
    return $false
}

function Get-HasReference($row) {
    if ($null -ne $row.hasReference) { return [bool]$row.hasReference }
    if ($null -ne $row.HasReference) { return [bool]$row.HasReference }
    return $false
}

function Get-Code($row) {
    $v = [string]$row.code
    if (-not $v) { $v = [string]$row.Code }
    return $v
}

function Find-WbsByName($rows, [string]$name) {
    return @($rows | Where-Object { (Get-Name $_) -eq $name } | Select-Object -First 1)[0]
}

function Get-StatusItems($pack) {
    if ($pack.items) { return @($pack.items) }
    if ($pack.Items) { return @($pack.Items) }
    return @()
}

function Test-Flag($row, [string]$flag) {
    $flags = @()
    if ($row.flags) { $flags = @($row.flags) }
    elseif ($row.Flags) { $flags = @($row.Flags) }
    return $flags -contains $flag
}

$token = Get-Token
$script:Headers = @{ Authorization = "Bearer $token" }
$script:LastStatus = 0
$projectIds = @()
$workspaceIds = @()
$hubId = $null
$docId = $null

Write-Host "F4-7 smoke  code=$code  gateway=$Gateway" -ForegroundColor Cyan

try {
    $created = @(Invoke-Ops -Method POST -Path "/projects" -Body @{
            code     = $code
            name     = "F4-7 reference $stamp"
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
    $plan = Find-WbsByName $rows "Proje planı"
    if ($null -eq $plan) { $plan = Find-WbsByName $rows "Proje plani" }
    Assert-True ($null -ne $plan) "WBS Proje planı"
    $planId = Get-RowId $plan
    Assert-True ($plan.workItemId -or $plan.WorkItemId) "Proje planı WI bagli"
    Assert-True (-not (Get-HasReference $plan)) "Proje plani baslangicta reference yok"

    $docsRoot = $null
    foreach ($name in @("Dokumanlar", "Dökümanlar", "Documents")) {
        $docsRoot = Find-Folder -Name $name
        if ($docsRoot) { break }
    }
    Assert-True ($null -ne $docsRoot -and $docsRoot.id) "Dokumanlar koku"
    $projectsId = Ensure-Folder -Name "Projeler" -ParentId ([string]$docsRoot.id)
    $hubId = Ensure-Folder -Name $code -ParentId $projectsId
    Assert-True ($hubId) "proje hub=$hubId"

    $md = @(Invoke-Doc -Method POST -Path "/markdown" -Body @{
            parentId = $hubId
            title    = "Proje plani belgesi"
            content  = "F4-7 reference"
            isDraft  = $false
        })[0]
    $docId = [string]$md.id
    Assert-True ($docId) "markdown=$docId"

    Invoke-Ops -Method PUT -Path "/projects/$projectId" -Body @{ diFolderId = $hubId } | Out-Null
    Assert-True ($true) "diFolderId baglandi"

    $docs = @(Invoke-Ops -Path "/projects/$projectId/documents")[0]
    $docList = Get-Items $docs
    $found = @($docList | Where-Object { (Get-RowId $_) -eq $docId } | Select-Object -First 1)[0]
    Assert-True ($null -ne $found) "liste markdown'i gorur"

    $qdocs = @(Invoke-Ops -Path ("/projects/$projectId/documents?q=" + [Uri]::EscapeDataString("plani")))[0]
    Assert-True ((Get-Items $qdocs).Count -ge 1) "belge arama"

    $status = @(Invoke-Ops -Path "/projects/$projectId/status")[0]
    $planStatus = @(Get-StatusItems $status | Where-Object { (Get-RowId $_) -eq $planId -or $_.wbsId -eq $planId -or $_.WbsId -eq $planId } | Select-Object -First 1)[0]
    Assert-True (Test-Flag $planStatus "missingReference") "status missingReference"

    $unbound = @(Invoke-Ops -Method POST -Path "/projects/$projectId/wbs" -Body @{
            name = "Bagimsiz plan"
            kind = "task"
        } -ExpectStatus @(201, 200))[0]
    $unboundId = Get-RowId $unbound
    $blockedWi = @(Invoke-Ops -Method POST -Path "/wbs/$unboundId/references" -Body @{
            resourceId = $docId
        } -ExpectStatus @(409))[0]
    Assert-True ((Get-Code $blockedWi) -eq "WI_UNBOUND") "WI_UNBOUND"

    $folderBlocked = @(Invoke-Ops -Method POST -Path "/wbs/$planId/references" -Body @{
            resourceId = $hubId
        } -ExpectStatus @(400))[0]
    Assert-True ((Get-Code $folderBlocked) -eq "DOC_FOLDER") "DOC_FOLDER"

    Invoke-Ops -Method POST -Path "/wbs/$planId/references" -Body @{
            resourceId = "missing-doc-id-0001"
        } -ExpectStatus @(404) | Out-Null
    Assert-True ($true) "sahte belge 404"

    $bound = @(Invoke-Ops -Method POST -Path "/wbs/$planId/references" -Body @{
            resourceId = $docId
        })[0]
    Assert-True (Get-HasReference $bound) "hasReference bind sonrasi"
    $count = 0
    if ($null -ne $bound.referenceCount) { $count = [int]$bound.referenceCount }
    elseif ($null -ne $bound.ReferenceCount) { $count = [int]$bound.ReferenceCount }
    Assert-True ($count -ge 1) "referenceCount=$count"

    $dup = @(Invoke-Ops -Method POST -Path "/wbs/$planId/references" -Body @{
            resourceId = $docId
        } -ExpectStatus @(409))[0]
    Assert-True ((Get-Code $dup) -eq "REFERENCE_BOUND") "REFERENCE_BOUND"

    $listed = Get-Items (Invoke-Ops -Path "/wbs/$planId/references")
    Assert-True ($listed.Count -ge 1) "GET references sayisi=$($listed.Count)"

    $after = @(Invoke-Ops -Path "/projects/$projectId/status")[0]
    $planAfter = @(Get-StatusItems $after | Where-Object { $_.wbsId -eq $planId -or $_.WbsId -eq $planId } | Select-Object -First 1)[0]
    Assert-True (-not (Test-Flag $planAfter "missingReference")) "reference sonrasi missingReference yok"

    Invoke-Ops -Method DELETE -Path "/wbs/$planId/references/$docId" | Out-Null
    $cleared = @(Invoke-Ops -Path "/projects/$projectId")[0]
    $planCleared = Find-WbsByName @(Get-WbsRows $cleared) "Proje planı"
    if ($null -eq $planCleared) { $planCleared = Find-WbsByName @(Get-WbsRows $cleared) "Proje plani" }
    Assert-True (-not (Get-HasReference $planCleared)) "unbind sonrasi hasReference=false"

    $statusBack = @(Invoke-Ops -Path "/projects/$projectId/status")[0]
    $planBack = @(Get-StatusItems $statusBack | Where-Object { $_.wbsId -eq $planId -or $_.WbsId -eq $planId } | Select-Object -First 1)[0]
    Assert-True (Test-Flag $planBack "missingReference") "unbind sonrasi missingReference"

    Invoke-Ops -Method DELETE -Path "/wbs/$planId/references/$docId" -ExpectStatus @(404) | Out-Null
    Assert-True ($true) "ikinci unbind 404"

    if (-not $KeepArtifacts) {
        foreach ($id in $projectIds) {
            Invoke-Ops -Method DELETE -Path "/projects/$id" -ExpectStatus @(204, 200, 404) | Out-Null
        }
        $projectIds = @()
        foreach ($id in $workspaceIds) { Remove-PackWorkspace $id }
        $workspaceIds = @()
        if ($docId) {
            try { Invoke-Doc -Method DELETE -Path "/$docId" -ExpectStatus @(200, 204, 404) | Out-Null } catch { }
        }
        if ($hubId) {
            try { Invoke-Doc -Method DELETE -Path "/$hubId`?force=true" -ExpectStatus @(200, 204, 404) | Out-Null } catch { }
        }
        Write-Host "  cleanup OK" -ForegroundColor Green
    }
    else {
        Write-Host "KeepArtifacts: proje=$projectId hub=$hubId" -ForegroundColor Yellow
    }

    Write-Host "F4-7 smoke PASSED" -ForegroundColor Green
}
catch {
    Write-Host "F4-7 smoke FAILED: $($_.Exception.Message)" -ForegroundColor Red
    if (-not $KeepArtifacts) {
        foreach ($id in $projectIds) {
            try { Invoke-Ops -Method DELETE -Path "/projects/$id" -ExpectStatus @(204, 200, 404) | Out-Null } catch { }
        }
        foreach ($id in $workspaceIds) { Remove-PackWorkspace $id }
        if ($docId) {
            try { Invoke-Doc -Method DELETE -Path "/$docId" -ExpectStatus @(200, 204, 404) | Out-Null } catch { }
        }
        if ($hubId) {
            try { Invoke-Doc -Method DELETE -Path "/$hubId`?force=true" -ExpectStatus @(200, 204, 404) | Out-Null } catch { }
        }
    }
    throw
}
