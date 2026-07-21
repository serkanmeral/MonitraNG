<#
.SYNOPSIS
  Idempotent DI auth fixture: root + FolderA/FolderB + sample docs + ACL.
.DESCRIPTION
  Creates under Document Intelligence (local/test gateway only — never prod by default).
  ACL:
    root   — admins=all; developers=view; testers=view
    FolderA — developers=view,create,edit,upload,download (+ admins=all)
    FolderB — testers=view,download (+ admins=all)
#>
param(
    [string]$Gateway = "http://localhost:5040",
    [string]$DomainName = "odak",
    [switch]$ForceRecreate
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "..\auth\DiAuthCommon.ps1")

$config = Get-DiAuthPersonas (Join-Path $PSScriptRoot "..\auth\personas.json")
$fx = $config.fixture
$adminToken = Get-DiPersonaToken -Persona Admin -Gateway $Gateway -DomainName $DomainName -PersonasConfig $config

function Invoke-Admin {
    param([string]$Method, [string]$Path, [object]$Body = $null)
    return Invoke-DiDocs -Gateway $Gateway -Token $adminToken -Method $Method -Path $Path -Body $Body
}

function Get-ChildFolderByName {
    param([string]$ParentId, [string]$Name)
    $q = if ($ParentId) { "?parentId=$ParentId&limit=200" } else { "?limit=200" }
    $r = Invoke-Admin -Method GET -Path "/resources/children$q"
    if ($r.StatusCode -ne 200) { throw "children failed: $($r.StatusCode) $($r.Content)" }
    $data = ConvertFrom-DiJson $r.Content
    $items = @($data.items)
    if (-not $items -and $data.Items) { $items = @($data.Items) }
    foreach ($it in $items) {
        $type = $it.type; if (-not $type) { $type = $it.Type }
        $n = $it.name; if (-not $n) { $n = $it.Name }
        $id = $it.id; if (-not $id) { $id = $it.Id }
        if ($type -eq "folder" -and $n -eq $Name) { return $id }
    }
    return $null
}

function Get-ChildDocByName {
    param([string]$ParentId, [string]$Name)
    $q = "?parentId=$ParentId&limit=200"
    $r = Invoke-Admin -Method GET -Path "/resources/children$q"
    if ($r.StatusCode -ne 200) { throw "children failed: $($r.StatusCode)" }
    $data = ConvertFrom-DiJson $r.Content
    $items = @($data.items)
    if (-not $items -and $data.Items) { $items = @($data.Items) }
    foreach ($it in $items) {
        $n = $it.name; if (-not $n) { $n = $it.Name }
        $id = $it.id; if (-not $id) { $id = $it.Id }
        if ($n -eq $Name) { return $id }
    }
    return $null
}

function Ensure-Folder {
    param([string]$ParentId, [string]$Name)
    $existing = Get-ChildFolderByName -ParentId $ParentId -Name $Name
    if ($existing -and -not $ForceRecreate) { return $existing }

    $body = @{ name = $Name; description = "DI-T auth fixture (do not use in prod)" }
    if ($ParentId) { $body.parentId = $ParentId }
    $r = Invoke-Admin -Method POST -Path "/resources/folder" -Body $body
    if ($r.StatusCode -notin 200, 201) {
        throw "Create folder '$Name' failed: $($r.StatusCode) $($r.Content)"
    }
    $created = ConvertFrom-DiJson $r.Content
    $id = $created.id; if (-not $id) { $id = $created.Id }
    return $id
}

function Ensure-NativeDoc {
    param([string]$ParentId, [string]$Name)
    $existing = Get-ChildDocByName -ParentId $ParentId -Name $Name
    if ($existing -and -not $ForceRecreate) { return $existing }

    $docNo = ("AUTH-{0}-{1}" -f ($Name -replace "[^A-Za-z0-9]", ""), (Get-Date -Format "HHmmss"))
    $body = @{
        parentId    = $ParentId
        name        = $Name
        documentNo  = $docNo
        description = "DI-T sample"
    }
    $r = Invoke-Admin -Method POST -Path "/resources/documents/native" -Body $body
    if ($r.StatusCode -notin 200, 201) {
        throw "Create native doc '$Name' failed: $($r.StatusCode) $($r.Content)"
    }
    $created = ConvertFrom-DiJson $r.Content
    $id = $created.id; if (-not $id) { $id = $created.Id }
    return $id
}

function Set-FolderAcl {
    param(
        [string]$FolderId,
        [object[]]$Groups
    )
    # Break inheritance (idempotent if already broken)
    $br = Invoke-Admin -Method POST -Path "/resources/$FolderId/permissions/break-inheritance"
    if ($br.StatusCode -notin 200, 201) {
        throw "break-inheritance $FolderId failed: $($br.StatusCode) $($br.Content)"
    }

    $payload = @{ groups = @($Groups) }
    $sr = Invoke-Admin -Method PUT -Path "/resources/$FolderId/permissions" -Body $payload
    if ($sr.StatusCode -ne 200) {
        throw "set permissions $FolderId failed: $($sr.StatusCode) $($sr.Content)"
    }
}

Write-Host "DI-T fixture ensure @ $Gateway" -ForegroundColor Cyan

$rootId = Ensure-Folder -ParentId $null -Name $fx.rootName
$folderAId = Ensure-Folder -ParentId $rootId -Name $fx.folderAName
$folderBId = Ensure-Folder -ParentId $rootId -Name $fx.folderBName

$allAdmin = @("view", "create", "edit", "delete", "upload", "download", "move", "share")
$editorA = @("view", "create", "edit", "upload", "download")
$viewDl = @("view", "download")

Set-FolderAcl -FolderId $rootId -Groups @(
    @{ groupName = "admins"; permissions = $allAdmin },
    @{ groupName = "developers"; permissions = @("view") },
    @{ groupName = "testers"; permissions = @("view") }
)

Set-FolderAcl -FolderId $folderAId -Groups @(
    @{ groupName = "admins"; permissions = $allAdmin },
    @{ groupName = "developers"; permissions = $editorA }
)

Set-FolderAcl -FolderId $folderBId -Groups @(
    @{ groupName = "admins"; permissions = $allAdmin },
    @{ groupName = "testers"; permissions = $viewDl }
)

$docAId = Ensure-NativeDoc -ParentId $folderAId -Name $fx.sampleDocAName
$docBId = Ensure-NativeDoc -ParentId $folderBId -Name $fx.sampleDocBName

$state = [ordered]@{
    gateway   = $Gateway
    domain    = $DomainName
    updatedAt = (Get-Date).ToString("o")
    rootId    = $rootId
    folderAId = $folderAId
    folderBId = $folderBId
    docAId    = $docAId
    docBId    = $docBId
}
$statePath = Get-DiFixtureStatePath
($state | ConvertTo-Json -Depth 5) | Set-Content -Path $statePath -Encoding UTF8

Write-Host "Fixture OK" -ForegroundColor Green
Write-Host "  root     = $rootId"
Write-Host "  FolderA  = $folderAId"
Write-Host "  FolderB  = $folderBId"
Write-Host "  docA     = $docAId"
Write-Host "  docB     = $docBId"
Write-Host "  state    = $statePath"

return $state
