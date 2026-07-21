<#
.SYNOPSIS
  T-2 — inheritance break/restore + ACL change visibility (cache invalidate).
.DESCRIPTION
  Under FolderA creates child `_T2_Child` (inherits A).
  1) EditorA sees child (inherited)
  2) Break + ACL admins-only → EditorA loses access; FolderA sibling still OK
  3) Restore inheritance → EditorA sees child again
  4) Immediate post-ACL get by EditorA proves snapshot invalidate (not stale cache)
#>
param(
    [string]$Gateway = "http://localhost:5040",
    [string]$DomainName = "odak",
    [switch]$SkipFixtureEnsure
)

$ErrorActionPreference = "Stop"
$suiteRoot = $PSScriptRoot
. (Join-Path $suiteRoot "..\..\auth\DiAuthCommon.ps1")

$config = Get-DiAuthPersonas (Join-Path $suiteRoot "..\..\auth\personas.json")
$childName = "_T2_Child"

if (-not $SkipFixtureEnsure) {
    & (Join-Path $suiteRoot "..\..\fixtures\Ensure-DiAuthFixture.ps1") -Gateway $Gateway -DomainName $DomainName | Out-Null
}

$statePath = Get-DiFixtureStatePath
if (-not (Test-Path $statePath)) { throw "Fixture state missing: $statePath" }
$state = Get-Content $statePath -Raw -Encoding UTF8 | ConvertFrom-Json

$adminTok = Get-DiPersonaToken -Persona Admin -Gateway $Gateway -DomainName $DomainName -PersonasConfig $config
$editorTok = Get-DiPersonaToken -Persona EditorA -Gateway $Gateway -DomainName $DomainName -PersonasConfig $config

$results = [System.Collections.Generic.List[object]]::new()

function Add-Result {
    param($Action, $Expected, $Actual, $Detail, [Nullable[bool]]$PassOverride = $null)
    $expText = if ($Expected -is [array]) { ($Expected -join "|") } else { "$Expected" }
    $pass = if ($null -ne $PassOverride) { [bool]$PassOverride } else {
        ($Expected -eq $Actual) -or ($Expected -is [array] -and ($Expected -contains $Actual))
    }
    $results.Add([pscustomobject]@{
            Action   = $Action
            Expected = $expText
            Actual   = "$Actual"
            Pass     = [bool]$pass
            Detail   = $Detail
        }) | Out-Null
    $mark = if ($pass) { "PASS" } else { "FAIL" }
    $color = if ($pass) { "Green" } else { "Red" }
    Write-Host ("[{0}] {1,-28} exp={2} got={3} {4}" -f $mark, $Action, $expText, $Actual, $Detail) -ForegroundColor $color
}

function Invoke-Admin([string]$Method, [string]$Path, [object]$Body = $null) {
    Invoke-DiDocs -Gateway $Gateway -Token $adminTok -Method $Method -Path $Path -Body $Body
}

function Invoke-Editor([string]$Method, [string]$Path, [object]$Body = $null) {
    Invoke-DiDocs -Gateway $Gateway -Token $editorTok -Method $Method -Path $Path -Body $Body
}

function Find-ChildId {
    $r = Invoke-Admin GET "/resources/children?parentId=$($state.folderAId)&limit=200"
    if ($r.StatusCode -ne 200) { throw "list FolderA failed: $($r.StatusCode)" }
    $data = ConvertFrom-DiJson $r.Content
    $items = @($data.items); if (-not $items) { $items = @($data.Items) }
    foreach ($it in $items) {
        $n = $it.name; if (-not $n) { $n = $it.Name }
        $id = $it.id; if (-not $id) { $id = $it.Id }
        $type = $it.type; if (-not $type) { $type = $it.Type }
        if ($type -eq "folder" -and $n -eq $childName) { return $id }
    }
    return $null
}

function Ensure-T2Child {
    $existing = Find-ChildId
    if ($existing) {
        # Start from inherited state when possible
        $pr = Invoke-Admin GET "/resources/$existing/permissions"
        $dto = ConvertFrom-DiJson $pr.Content
        if ($dto.inheritanceBroken -eq $true -or $dto.InheritanceBroken -eq $true) {
            $null = Invoke-Admin POST "/resources/$existing/permissions/restore-inheritance"
        }
        return $existing
    }
    $cr = Invoke-Admin POST "/resources/folder" @{
        parentId    = $state.folderAId
        name        = $childName
        description = "DI-T T-2 inheritance child"
    }
    if ($cr.StatusCode -notin 200, 201) { throw "create child failed: $($cr.StatusCode) $($cr.Content)" }
    $created = ConvertFrom-DiJson $cr.Content
    $id = $created.id; if (-not $id) { $id = $created.Id }
    return $id
}

Write-Host "`n=== T-2 Inheritance / cache ===" -ForegroundColor Cyan
$childId = Ensure-T2Child
Write-Host "Child=$childId under FolderA=$($state.folderAId)"

# --- 1) Inherited: EditorA can get child; permissions show inheritanceBroken=false ---
$perm1 = Invoke-Admin GET "/resources/$childId/permissions"
$p1 = ConvertFrom-DiJson $perm1.Content
$broken1 = [bool]($p1.inheritanceBroken -eq $true -or $p1.InheritanceBroken -eq $true)
Add-Result -Action "inherit.flag" -Expected $false -Actual $broken1 -Detail "child should inherit" -PassOverride (-not $broken1)

$g1 = Invoke-Editor GET "/resources/$childId"
Add-Result -Action "inherit.editor.get" -Expected 200 -Actual $g1.StatusCode -Detail "inherited ACL from FolderA"

# --- 2) Break + admins-only ACL ---
$br = Invoke-Admin POST "/resources/$childId/permissions/break-inheritance"
Add-Result -Action "break.status" -Expected 200 -Actual $br.StatusCode -Detail ""
$brDto = ConvertFrom-DiJson $br.Content
$broken2 = [bool]($brDto.inheritanceBroken -eq $true -or $brDto.InheritanceBroken -eq $true)
Add-Result -Action "break.flag" -Expected $true -Actual $broken2 -Detail "" -PassOverride $broken2

$set = Invoke-Admin PUT "/resources/$childId/permissions" @{
    groups = @(
        @{
            groupName   = "admins"
            permissions = @("view", "create", "edit", "delete", "upload", "download", "move", "share")
        }
    )
}
Add-Result -Action "break.setAcl" -Expected 200 -Actual $set.StatusCode -Detail "admins-only"

# Immediate EditorA get — must be 403 (cache invalidated on set)
$g2 = Invoke-Editor GET "/resources/$childId"
Add-Result -Action "break.editor.get" -Expected @(403, 404) -Actual $g2.StatusCode -Detail "immediate after ACL (invalidate)"

# Sibling FolderA / docA still visible
$gA = Invoke-Editor GET "/resources/$($state.docAId)"
Add-Result -Action "sibling.docA.get" -Expected 200 -Actual $gA.StatusCode -Detail "FolderA unaffected"

$tree = Invoke-Editor GET "/resources/tree"
$nodes = ConvertFrom-DiJson $tree.Content
$childInTree = Find-DiFolderInTree -TreeNodes $nodes -FolderId $childId
Add-Result -Action "break.editor.tree" -Expected "absent" -Actual $(if ($childInTree) { "present" } else { "absent" }) -Detail $childId `
    -PassOverride (-not $childInTree)

$folderAInTree = Find-DiFolderInTree -TreeNodes $nodes -FolderId $state.folderAId
Add-Result -Action "sibling.FolderA.tree" -Expected "present" -Actual $(if ($folderAInTree) { "present" } else { "absent" }) -Detail "" `
    -PassOverride $folderAInTree

# --- 3) Restore inheritance ---
$rs = Invoke-Admin POST "/resources/$childId/permissions/restore-inheritance"
Add-Result -Action "restore.status" -Expected 200 -Actual $rs.StatusCode -Detail ""
$rsDto = ConvertFrom-DiJson $rs.Content
$broken3 = [bool]($rsDto.inheritanceBroken -eq $true -or $rsDto.InheritanceBroken -eq $true)
Add-Result -Action "restore.flag" -Expected $false -Actual $broken3 -Detail "" -PassOverride (-not $broken3)

$g3 = Invoke-Editor GET "/resources/$childId"
Add-Result -Action "restore.editor.get" -Expected 200 -Actual $g3.StatusCode -Detail "immediate after restore (invalidate)"

$passCount = @($results | Where-Object Pass).Count
$failCount = @($results | Where-Object { -not $_.Pass }).Count
Write-Host "`n=== T-2 Summary: $passCount PASS / $failCount FAIL / $($results.Count) total ===" -ForegroundColor $(if ($failCount -eq 0) { "Green" } else { "Red" })

$reportPath = Join-Path $env:TEMP "di_auth_t2_report.json"
($results | ConvertTo-Json -Depth 5) | Set-Content $reportPath -Encoding UTF8
Write-Host "Report: $reportPath"

if ($failCount -gt 0) { exit 1 }
exit 0
