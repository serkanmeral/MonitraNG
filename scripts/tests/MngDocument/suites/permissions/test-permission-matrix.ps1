<#
.SYNOPSIS
  T-1 permission matrix: persona x resource x action -> expected status / presence.
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

if (-not $SkipFixtureEnsure) {
    & (Join-Path $suiteRoot "..\..\fixtures\Ensure-DiAuthFixture.ps1") -Gateway $Gateway -DomainName $DomainName | Out-Null
}

$statePath = Get-DiFixtureStatePath
if (-not (Test-Path $statePath)) {
    throw "Fixture state missing: $statePath — run Ensure-DiAuthFixture.ps1 first"
}
$state = Get-Content $statePath -Raw -Encoding UTF8 | ConvertFrom-Json

$tokens = @{}
foreach ($name in @("Admin", "EditorA", "ViewerB", "Outsider", "Cross")) {
    Write-Host "Token: $name ..." -ForegroundColor DarkGray
    $tokens[$name] = Get-DiPersonaToken -Persona $name -Gateway $Gateway -DomainName $DomainName -PersonasConfig $config
}

$results = [System.Collections.Generic.List[object]]::new()

function Add-Result {
    param($Persona, $Action, $Resource, $Expected, $Actual, $Detail, [Nullable[bool]]$PassOverride = $null)
    $expText = if ($Expected -is [array]) { ($Expected -join "|") } else { "$Expected" }
    $pass = if ($null -ne $PassOverride) {
        [bool]$PassOverride
    } else {
        ($Expected -eq $Actual) -or ($Expected -is [array] -and ($Expected -contains $Actual))
    }
    $results.Add([pscustomobject]@{
            Persona  = $Persona
            Action   = $Action
            Resource = $Resource
            Expected = $expText
            Actual   = "$Actual"
            Pass     = [bool]$pass
            Detail   = $Detail
        }) | Out-Null
    $mark = if ($pass) { "PASS" } else { "FAIL" }
    $color = if ($pass) { "Green" } else { "Red" }
    Write-Host ("[{0}] {1,-9} {2,-10} {3,-12} exp={4} got={5} {6}" -f $mark, $Persona, $Action, $Resource, $expText, $Actual, $Detail) -ForegroundColor $color
}

function Test-Status {
    param($Persona, $Action, $Resource, $ExpectedStatuses, [scriptblock]$Call)
    $r = & $Call
    $detail = ""
    if ($r.Content -and $r.Content.Length -lt 180) { $detail = $r.Content }
    Add-Result -Persona $Persona -Action $Action -Resource $Resource -Expected $ExpectedStatuses -Actual $r.StatusCode -Detail $detail
}

function Test-TreeContains {
    param($Persona, $ResourceLabel, $FolderId, [bool]$ShouldContain)
    $tok = $tokens[$Persona]
    $r = Invoke-DiDocs -Gateway $Gateway -Token $tok -Method GET -Path "/resources/tree"
    if ($r.StatusCode -ne 200) {
        Add-Result -Persona $Persona -Action "tree" -Resource $ResourceLabel -Expected 200 -Actual $r.StatusCode -Detail "tree call failed"
        return
    }
    $nodes = ConvertFrom-DiJson $r.Content
    $found = Find-DiFolderInTree -TreeNodes $nodes -FolderId $FolderId
    $actual = if ($found) { "present" } else { "absent" }
    $expected = if ($ShouldContain) { "present" } else { "absent" }
    Add-Result -Persona $Persona -Action "tree" -Resource $ResourceLabel -Expected $expected -Actual $actual -Detail $FolderId
}

Write-Host "`n=== T-1 Permission matrix ===" -ForegroundColor Cyan
Write-Host "Gateway: $Gateway"
Write-Host "FolderA=$($state.folderAId) FolderB=$($state.folderBId)"

# --- tree presence ---
Test-TreeContains -Persona Admin -ResourceLabel FolderA -FolderId $state.folderAId -ShouldContain $true
Test-TreeContains -Persona Admin -ResourceLabel FolderB -FolderId $state.folderBId -ShouldContain $true
Test-TreeContains -Persona EditorA -ResourceLabel FolderA -FolderId $state.folderAId -ShouldContain $true
Test-TreeContains -Persona EditorA -ResourceLabel FolderB -FolderId $state.folderBId -ShouldContain $false
Test-TreeContains -Persona Cross -ResourceLabel FolderB -FolderId $state.folderBId -ShouldContain $false
Test-TreeContains -Persona ViewerB -ResourceLabel FolderB -FolderId $state.folderBId -ShouldContain $true
Test-TreeContains -Persona ViewerB -ResourceLabel FolderA -FolderId $state.folderAId -ShouldContain $false
Test-TreeContains -Persona Outsider -ResourceLabel FolderA -FolderId $state.folderAId -ShouldContain $false
Test-TreeContains -Persona Outsider -ResourceLabel FolderB -FolderId $state.folderBId -ShouldContain $false

# --- list (children): unauthorized folderId often returns 200 + empty (filter), not 403 ---
function Test-ListAccess {
    param($Persona, $FolderId, $Label, [bool]$ShouldSeeContent)
    $r = Invoke-DiDocs -Gateway $Gateway -Token $tokens[$Persona] -Method GET -Path "/resources/children?parentId=$FolderId&limit=50"
    if ($r.StatusCode -ne 200) {
        # Some builds may 403 — accept as denied when ShouldSeeContent=false
        if (-not $ShouldSeeContent -and $r.StatusCode -in 403, 404) {
            Add-Result -Persona $Persona -Action "list" -Resource $Label -Expected "denied_or_empty" -Actual "denied($($r.StatusCode))" -Detail "" -PassOverride $true
            return
        }
        Add-Result -Persona $Persona -Action "list" -Resource $Label -Expected 200 -Actual $r.StatusCode -Detail $r.Content
        return
    }
    $data = ConvertFrom-DiJson $r.Content
    $total = 0
    if ($null -ne $data.total) { $total = [int]$data.total }
    elseif ($null -ne $data.Total) { $total = [int]$data.Total }
    $items = @($data.items); if (-not $items) { $items = @($data.Items) }
    $count = if ($total -gt 0) { $total } else { $items.Count }
    if ($ShouldSeeContent) {
        Add-Result -Persona $Persona -Action "list" -Resource $Label -Expected "nonempty" -Actual "count=$count" -Detail "" -PassOverride ($count -ge 1)
    } else {
        Add-Result -Persona $Persona -Action "list" -Resource $Label -Expected "empty" -Actual "count=$count" -Detail "" -PassOverride ($count -eq 0)
    }
}

Test-ListAccess -Persona EditorA -FolderId $state.folderAId -Label FolderA -ShouldSeeContent $true
Test-ListAccess -Persona EditorA -FolderId $state.folderBId -Label FolderB -ShouldSeeContent $false
Test-ListAccess -Persona ViewerB -FolderId $state.folderBId -Label FolderB -ShouldSeeContent $true
Test-ListAccess -Persona ViewerB -FolderId $state.folderAId -Label FolderA -ShouldSeeContent $false
Test-ListAccess -Persona Outsider -FolderId $state.folderAId -Label FolderA -ShouldSeeContent $false

# --- get ---
foreach ($case in @(
        @{ P = "EditorA"; Id = $state.docAId; Label = "docA"; Exp = 200 },
        @{ P = "EditorA"; Id = $state.docBId; Label = "docB"; Exp = @(403, 404) },
        @{ P = "Cross"; Id = $state.docBId; Label = "docB"; Exp = @(403, 404) },
        @{ P = "ViewerB"; Id = $state.docBId; Label = "docB"; Exp = 200 },
        @{ P = "ViewerB"; Id = $state.docAId; Label = "docA"; Exp = @(403, 404) },
        @{ P = "Outsider"; Id = $state.docAId; Label = "docA"; Exp = @(403, 404) },
        @{ P = "Outsider"; Id = $state.docBId; Label = "docB"; Exp = @(403, 404) },
        @{ P = "Admin"; Id = $state.docAId; Label = "docA"; Exp = 200 }
    )) {
    Test-Status -Persona $case.P -Action "get" -Resource $case.Label -ExpectedStatuses $case.Exp -Call {
        Invoke-DiDocs -Gateway $Gateway -Token $tokens[$case.P] -Method GET -Path "/resources/$($case.Id)"
    }
}

# --- download (export/pdf): ACL gate — authorized may be 200/400/503 (Gotenberg); denied = 403/404 ---
foreach ($case in @(
        @{ P = "EditorA"; Id = $state.docAId; Label = "docA"; Allowed = $true },
        @{ P = "ViewerB"; Id = $state.docBId; Label = "docB"; Allowed = $true },
        @{ P = "ViewerB"; Id = $state.docAId; Label = "docA"; Allowed = $false },
        @{ P = "EditorA"; Id = $state.docBId; Label = "docB"; Allowed = $false },
        @{ P = "Outsider"; Id = $state.docAId; Label = "docA"; Allowed = $false }
    )) {
    $r = Invoke-DiDocs -Gateway $Gateway -Token $tokens[$case.P] -Method GET -Path "/resources/$($case.Id)/export/pdf" -TimeoutSec 120
    if ($case.Allowed) {
        $pass = $r.StatusCode -ne 403
        Add-Result -Persona $case.P -Action "download" -Resource $case.Label `
            -Expected "not-403" -Actual $r.StatusCode -Detail "ACL passed if not 403" -PassOverride $pass
    } else {
        Add-Result -Persona $case.P -Action "download" -Resource $case.Label -Expected @(403, 404) -Actual $r.StatusCode -Detail ""
    }
}

# --- upload: ACL gate (DG may 400 on file type; FORBIDDEN=403 is the ACL signal) ---
$stamp = Get-Date -Format "yyyyMMddHHmmss"
foreach ($case in @(
        @{ P = "EditorA"; Fid = $state.folderAId; Label = "FolderA"; Allowed = $true },
        @{ P = "ViewerB"; Fid = $state.folderBId; Label = "FolderB"; Allowed = $false },
        @{ P = "Outsider"; Fid = $state.folderAId; Label = "FolderA"; Allowed = $false },
        @{ P = "EditorA"; Fid = $state.folderBId; Label = "FolderB"; Allowed = $false }
    )) {
    $name = "auth_up_$($case.P)_$stamp.txt"
    $contentB64 = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes("di-auth-upload"))
    $r = Invoke-DiDocs -Gateway $Gateway -Token $tokens[$case.P] -Method POST -Path "/resources/file" -Body @{
        parentId         = $case.Fid
        name             = $name
        originalFileName = $name
        extension        = ".txt"
        mimeType         = "text/plain"
        content          = $contentB64
    }
    if ($case.Allowed) {
        $pass = $r.StatusCode -in 200, 201 -or ($r.StatusCode -eq 400 -and ($r.Content -notmatch "FORBIDDEN"))
        $detail = if ($r.Content) { $r.Content.Substring(0, [Math]::Min(120, $r.Content.Length)) } else { "" }
        Add-Result -Persona $case.P -Action "upload" -Resource $case.Label `
            -Expected "acl-ok(200|201|400!FORBIDDEN)" -Actual $r.StatusCode -Detail $detail -PassOverride $pass
    } else {
        Add-Result -Persona $case.P -Action "upload" -Resource $case.Label -Expected @(403, 404) -Actual $r.StatusCode -Detail ""
    }
}

$passCount = @($results | Where-Object Pass).Count
$failCount = @($results | Where-Object { -not $_.Pass }).Count
Write-Host "`n=== Summary: $passCount PASS / $failCount FAIL / $($results.Count) total ===" -ForegroundColor $(if ($failCount -eq 0) { "Green" } else { "Red" })

$reportPath = Join-Path $env:TEMP "di_auth_t1_report.json"
($results | ConvertTo-Json -Depth 5) | Set-Content $reportPath -Encoding UTF8
Write-Host "Report: $reportPath"

if ($failCount -gt 0) { exit 1 }
exit 0
