# Smoke: F2-14 asama kapisi is kilidi (Odak test)
param(
    [string]$Gateway = "http://192.168.20.20:5040",
    [string]$TokenFile = "$env:TEMP\operationcore_dg_token.txt",
    [switch]$KeepArtifacts
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../../..")).Path
$loadToken = Join-Path $repoRoot "docs/odak/operationcore/scripts/load-operationcore-token.ps1"

$stamp = Get-Date -Format "yyyyMMddHHmmss"
$code = "F214-$stamp"

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

function Get-Name($row) {
    $v = [string]$row.name
    if (-not $v) { $v = [string]$row.Name }
    return $v
}

function Get-RowId($row) {
    foreach ($k in @("id", "Id")) {
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

function Get-GateLocked($row) {
    if ($null -ne $row.gateLocked) { return [bool]$row.gateLocked }
    if ($null -ne $row.GateLocked) { return [bool]$row.GateLocked }
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
        throw "HTTP $script:LastStatus $Method documents$Path"
    }
    return , $result
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

$token = Get-Token
$script:Headers = @{ Authorization = "Bearer $token" }
$script:LastStatus = 0
$projectIds = @()
$workspaceIds = @()
$hubId = $null
$kickDocId = $null
$kapsamDocId = $null

Write-Host "F2-14 smoke  code=$code  gateway=$Gateway" -ForegroundColor Cyan

try {
    $created = @(Invoke-Ops -Method POST -Path "/projects" -Body @{
            code     = $code
            name     = "F2-14 gate $stamp"
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
    $baslatma = Find-WbsByName $rows "Başlatma"
    if ($null -eq $baslatma) { $baslatma = Find-WbsByName $rows "Baslatma" }
    $kick = Find-WbsByName $rows "Kick-off"
    $kapsam = Find-WbsByName $rows "Kapsam onayı"
    if ($null -eq $kapsam) { $kapsam = Find-WbsByName $rows "Kapsam onayi" }
    Assert-True ($null -ne $baslatma) "WBS Başlatma"
    Assert-True ($null -ne $kick) "WBS Kick-off"
    Assert-True ($null -ne $kapsam) "WBS Kapsam onayı"
    $kickWi = Get-WorkItemId $kick
    $kapsamWi = Get-WorkItemId $kapsam
    Assert-True ($kickWi) "Kick-off WI=$kickWi"
    Assert-True ($kapsamWi) "Kapsam WI=$kapsamWi"

    $gate = @(Invoke-Ops -Method POST -Path "/projects/$projectId/stage-gates" -Body @{
            name     = "Başlatma kapısı"
            wbsId    = (Get-RowId $baslatma)
            status   = "open"
            criteria = @()
        } -ExpectStatus @(201, 200))[0]
    $gateId = Get-RowId $gate
    Assert-True ($gateId) "gate id=$gateId"
    $locksWork = $false
    if ($null -ne $gate.locksWork) { $locksWork = [bool]$gate.locksWork }
    elseif ($null -ne $gate.LocksWork) { $locksWork = [bool]$gate.LocksWork }
    Assert-True ($locksWork) "gate.locksWork"

    $detail = @(Invoke-Ops -Path "/projects/$projectId")[0]
    $rows = @(Get-WbsRows $detail)
    $baslatma = Find-WbsByName $rows "Başlatma"
    if ($null -eq $baslatma) { $baslatma = Find-WbsByName $rows "Baslatma" }
    $kick = Find-WbsByName $rows "Kick-off"
    $kapsam = Find-WbsByName $rows "Kapsam onayı"
    if ($null -eq $kapsam) { $kapsam = Find-WbsByName $rows "Kapsam onayi" }
    Assert-True (Get-GateLocked $baslatma) "Başlatma gateLocked"
    Assert-True (Get-GateLocked $kick) "Kick-off gateLocked (alt agac)"
    Assert-True (Get-GateLocked $kapsam) "Kapsam gateLocked (alt agac)"

    Invoke-Ops -Method POST -Path "/work-items/$kickWi/transitions/start_progress" -Body @{} | Out-Null
    Assert-True ($true) "start_progress kilit altinda serbest"

    $blocked = @(Invoke-Ops -Method POST -Path "/work-items/$kickWi/transitions/resolve" -Body @{
            fields = @{ description = "kapi smoke" }
        } -ExpectStatus @(409))[0]
    Assert-True ((Get-Code $blocked) -eq "GATE_LOCKED") "resolve GATE_LOCKED"

    $passed = @(Invoke-Ops -Method PUT -Path "/stage-gates/$gateId" -Body @{ status = "passed" })[0]
    $locksAfter = $true
    if ($null -ne $passed.locksWork) { $locksAfter = [bool]$passed.locksWork }
    elseif ($null -ne $passed.LocksWork) { $locksAfter = [bool]$passed.LocksWork }
    Assert-True (-not $locksAfter) "passed kilit kalkar"

    $detail = @(Invoke-Ops -Path "/projects/$projectId")[0]
    $kick = Find-WbsByName @(Get-WbsRows $detail) "Kick-off"
    Assert-True (-not (Get-GateLocked $kick)) "Kick-off gateLocked=false"

    $docsRoot = $null
    foreach ($name in @("Dokumanlar", "Dökümanlar", "Documents")) {
        $docsRoot = Find-Folder -Name $name
        if ($docsRoot) { break }
    }
    Assert-True ($null -ne $docsRoot -and $docsRoot.id) "Dokumanlar koku"
    $projectsId = Ensure-Folder -Name "Projeler" -ParentId ([string]$docsRoot.id)
    $hubId = Ensure-Folder -Name $code -ParentId $projectsId
    Invoke-Ops -Method PUT -Path "/projects/$projectId" -Body @{ diFolderId = $hubId } | Out-Null
    $kickMd = @(Invoke-Doc -Method POST -Path "/markdown" -Body @{
            parentId = $hubId
            title    = "Kick-off kanit"
            content  = "F2-14"
            isDraft  = $false
        })[0]
    $kickDocId = [string]$kickMd.id
    $kapsamMd = @(Invoke-Doc -Method POST -Path "/markdown" -Body @{
            parentId = $hubId
            title    = "Kapsam kanit"
            content  = "F2-14"
            isDraft  = $false
        })[0]
    $kapsamDocId = [string]$kapsamMd.id
    Invoke-Ops -Method POST -Path "/wbs/$(Get-RowId $kick)/evidence" -Body @{ resourceId = $kickDocId } | Out-Null
    Invoke-Ops -Method POST -Path "/wbs/$(Get-RowId $kapsam)/evidence" -Body @{ resourceId = $kapsamDocId } | Out-Null
    Assert-True ($true) "yaprak kanit baglandi (F2-15 uyumu)"

    Invoke-Ops -Method POST -Path "/work-items/$kickWi/transitions/resolve" -Body @{
        fields = @{ description = "kapi smoke" }
    } | Out-Null
    Assert-True ($true) "resolve kapı geçince serbest"

    $failGate = @(Invoke-Ops -Method POST -Path "/projects/$projectId/stage-gates" -Body @{
            name   = "Kapsam reddi"
            wbsId  = (Get-RowId $kapsam)
            status = "failed"
            note   = "eksik kanit"
        } -ExpectStatus @(201, 200))[0]
    Assert-True ([bool]$failGate.locksWork -or [bool]$failGate.LocksWork) "failed locksWork"

    Invoke-Ops -Method POST -Path "/work-items/$kapsamWi/transitions/start_progress" -Body @{} | Out-Null
    $blockedFail = @(Invoke-Ops -Method POST -Path "/work-items/$kapsamWi/transitions/resolve" -Body @{
            fields = @{ description = "kapi smoke" }
        } -ExpectStatus @(409))[0]
    Assert-True ((Get-Code $blockedFail) -eq "GATE_LOCKED") "failed gate resolve kilidi"

    Invoke-Ops -Method PUT -Path "/stage-gates/$(Get-RowId $failGate)" -Body @{
        status = "waived"
        note   = "sponsor feragat"
    } | Out-Null
    Invoke-Ops -Method POST -Path "/work-items/$kapsamWi/transitions/resolve" -Body @{
        fields = @{ description = "kapi smoke" }
    } | Out-Null
    Assert-True ($true) "waive sonrasi resolve serbest"

    if (-not $KeepArtifacts) {
        foreach ($id in $projectIds) {
            Invoke-Ops -Method DELETE -Path "/projects/$id" -ExpectStatus @(204, 200, 404) | Out-Null
        }
        $projectIds = @()
        foreach ($id in $workspaceIds) { Remove-PackWorkspace $id }
        $workspaceIds = @()
        foreach ($doc in @($kickDocId, $kapsamDocId)) {
            if ($doc) {
                try { Invoke-Doc -Method DELETE -Path "/$doc" -ExpectStatus @(200, 204, 404) | Out-Null } catch { }
            }
        }
        if ($hubId) {
            try { Invoke-Doc -Method DELETE -Path "/$hubId`?force=true" -ExpectStatus @(200, 204, 404) | Out-Null } catch { }
        }
        Write-Host "  cleanup OK" -ForegroundColor Green
    }
    else {
        Write-Host "KeepArtifacts: projeler birakildi $($projectIds -join ',')" -ForegroundColor Yellow
    }

    Write-Host "F2-14 smoke OK" -ForegroundColor Green
}
catch {
    Write-Host "F2-14 smoke FAIL: $($_.Exception.Message)" -ForegroundColor Red
    if (-not $KeepArtifacts) {
        foreach ($id in $projectIds) {
            try { Invoke-Ops -Method DELETE -Path "/projects/$id" -ExpectStatus @(204, 200, 404) | Out-Null } catch { }
        }
        foreach ($id in $workspaceIds) { try { Remove-PackWorkspace $id } catch { } }
        foreach ($doc in @($kickDocId, $kapsamDocId)) {
            if ($doc) {
                try { Invoke-Doc -Method DELETE -Path "/$doc" -ExpectStatus @(200, 204, 404) | Out-Null } catch { }
            }
        }
        if ($hubId) {
            try { Invoke-Doc -Method DELETE -Path "/$hubId`?force=true" -ExpectStatus @(200, 204, 404) | Out-Null } catch { }
        }
    }
    throw
}
