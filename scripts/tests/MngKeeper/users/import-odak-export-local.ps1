# Import Odak Keeper export (groups.json + users.json) into LOCAL domain.
# Order: groups first (by name map), then users with Local password + groupIds by name.
#
# Usage (repo root or this folder):
#   pwsh -File .\scripts\tests\MngKeeper\users\import-odak-export-local.ps1
#   pwsh -File .\scripts\tests\MngKeeper\users\import-odak-export-local.ps1 -WhatIf
#   pwsh -File .\scripts\tests\MngKeeper\users\import-odak-export-local.ps1 -Password 'Sm123!?' -AdminPassword 'Admin123!'
#
# Uses MngKeeper DIRECT (gateway may be unhealthy): http://localhost:5001

param(
    [string]$ExportDir = "",
    [string]$KeeperBaseUrl = "http://localhost:5001",
    [string]$DomainName = "odak",
    [string]$AdminUsername = "odak_admin",
    [string]$AdminPassword = "Admin123!",
    [string]$Password = "Sm123!?",
    [switch]$WhatIf,
    [switch]$SkipInactiveUsers
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
$repoRoot = (Resolve-Path (Join-Path $scriptDir "../../../..")).Path

if ([string]::IsNullOrWhiteSpace($ExportDir)) {
    $ExportDir = Join-Path $repoRoot "docs/odak/exports/odak-keeper-20260711"
}

$usersPath = Join-Path $ExportDir "users.json"
$groupsPath = Join-Path $ExportDir "groups.json"
if (-not (Test-Path $usersPath)) { throw "users.json yok: $usersPath" }
if (-not (Test-Path $groupsPath)) { throw "groups.json yok: $groupsPath" }

Write-Host "Export: $ExportDir" -ForegroundColor Cyan
Write-Host "Keeper: $KeeperBaseUrl  domain=$DomainName" -ForegroundColor Cyan
if ($WhatIf) { Write-Host "WhatIf: no writes" -ForegroundColor Yellow }

# --- token ---
$tokenBody = @{ username = $AdminUsername; password = $AdminPassword; domain = $DomainName } | ConvertTo-Json
$tokenResp = Invoke-RestMethod -Uri "$KeeperBaseUrl/api/auth/token" -Method POST -Body $tokenBody -ContentType "application/json" -TimeoutSec 60
$token = $tokenResp.accessToken
if ([string]::IsNullOrWhiteSpace($token)) { throw "Token alinamadi" }
$headers = @{
    Authorization = "Bearer $token"
    "Content-Type" = "application/json"
    "X-Domain-Name" = $DomainName
}
Write-Host "Token OK ($AdminUsername)" -ForegroundColor Green

function Get-AllPaged {
    param([string]$Path, [string]$ArrayProp)
    $all = [System.Collections.Generic.List[object]]::new()
    $page = 1
    $pageSize = 100
    do {
        $url = "$KeeperBaseUrl$Path`?page=$page&pageSize=$pageSize"
        $resp = Invoke-RestMethod -Uri $url -Headers $headers -Method GET -TimeoutSec 120
        $items = $null
        if ($resp.PSObject.Properties.Name -contains $ArrayProp) { $items = @($resp.$ArrayProp) }
        elseif ($resp.PSObject.Properties.Name -contains "items") { $items = @($resp.items) }
        else { $items = @() }
        foreach ($i in $items) { $all.Add($i) }
        $totalPages = 1
        if ($resp.totalPages) { $totalPages = [int]$resp.totalPages }
        elseif ($resp.TotalPages) { $totalPages = [int]$resp.TotalPages }
        $page++
    } while ($page -le $totalPages)
    return $all
}

# --- load export ---
$exportGroups = (Get-Content $groupsPath -Raw -Encoding UTF8 | ConvertFrom-Json).groups
$exportUsers = (Get-Content $usersPath -Raw -Encoding UTF8 | ConvertFrom-Json).users
Write-Host "Export groups=$($exportGroups.Count) users=$($exportUsers.Count)" -ForegroundColor Cyan

# --- existing local ---
$localGroups = Get-AllPaged -Path "/api/group" -ArrayProp "groups"
$localUsers = Get-AllPaged -Path "/api/user" -ArrayProp "users"
Write-Host "Local now groups=$($localGroups.Count) users=$($localUsers.Count)" -ForegroundColor Cyan

$nameToGroupId = @{}
foreach ($lg in $localGroups) {
    $n = [string]$lg.name
    if ($n) { $nameToGroupId[$n.ToLowerInvariant()] = [string]$lg.groupId }
}

$existingUsernames = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($lu in $localUsers) {
    if ($lu.username) { [void]$existingUsernames.Add([string]$lu.username) }
}

$report = [ordered]@{
    startedAt = (Get-Date).ToUniversalTime().ToString("o")
    exportDir = $ExportDir
    groupsCreated = 0
    groupsSkippedExisting = 0
    groupsFailed = 0
    usersCreated = 0
    usersSkippedExisting = 0
    usersSkippedInactive = 0
    usersFailed = 0
    failures = [System.Collections.Generic.List[string]]::new()
}

# --- create groups ---
Write-Host "`n=== Groups ===" -ForegroundColor Yellow
foreach ($eg in $exportGroups) {
    $name = [string]$eg.name
    if ([string]::IsNullOrWhiteSpace($name)) { continue }
    $key = $name.ToLowerInvariant()
    if ($nameToGroupId.ContainsKey($key)) {
        $report.groupsSkippedExisting++
        continue
    }
    $bodyObj = @{
        name = $name
        description = if ($eg.description) { [string]$eg.description } else { "" }
        permissions = @($eg.permissions)
        isActive = [bool]($eg.isActive)
    }
    if ($WhatIf) {
        Write-Host "WhatIf CREATE group: $name" -ForegroundColor DarkGray
        $report.groupsCreated++
        continue
    }
    try {
        $json = $bodyObj | ConvertTo-Json -Depth 6 -Compress
        $created = Invoke-RestMethod -Uri "$KeeperBaseUrl/api/group" -Method POST -Headers $headers -Body $json -TimeoutSec 120
        $gid = $created.groupId
        if (-not $gid -and $created.GroupId) { $gid = $created.GroupId }
        if (-not $gid) { throw "groupId yok: $($created | ConvertTo-Json -Compress)" }
        $nameToGroupId[$key] = [string]$gid
        $report.groupsCreated++
        Write-Host "  + group $name -> $gid" -ForegroundColor Green
    } catch {
        $report.groupsFailed++
        $msg = "group '$name': $($_.Exception.Message)"
        if ($_.ErrorDetails.Message) { $msg += " | $($_.ErrorDetails.Message)" }
        $report.failures.Add($msg)
        Write-Host "  ! $msg" -ForegroundColor Red
    }
}

# refresh name map from API in case of skips
$localGroups = Get-AllPaged -Path "/api/group" -ArrayProp "groups"
$nameToGroupId = @{}
foreach ($lg in $localGroups) {
    $n = [string]$lg.name
    if ($n) { $nameToGroupId[$n.ToLowerInvariant()] = [string]$lg.groupId }
}
Write-Host "Group map size=$($nameToGroupId.Count) created=$($report.groupsCreated) skipped=$($report.groupsSkippedExisting) failed=$($report.groupsFailed)"

# --- create users ---
Write-Host "`n=== Users ===" -ForegroundColor Yellow
foreach ($eu in $exportUsers) {
    $username = [string]$eu.username
    if ([string]::IsNullOrWhiteSpace($username)) { continue }

    if ($SkipInactiveUsers -and -not $eu.isActive) {
        $report.usersSkippedInactive++
        continue
    }

    if ($existingUsernames.Contains($username)) {
        $report.usersSkippedExisting++
        continue
    }

    $groupIds = [System.Collections.Generic.List[string]]::new()
    foreach ($gn in @($eu.groups)) {
        if (-not $gn) { continue }
        $gk = ([string]$gn).ToLowerInvariant()
        if ($nameToGroupId.ContainsKey($gk)) {
            $gid = $nameToGroupId[$gk]
            if (-not $groupIds.Contains($gid)) { $groupIds.Add($gid) }
        }
    }

    $gender = 0
    if ($null -ne $eu.gender) {
        if ($eu.gender -is [int]) { $gender = [int]$eu.gender }
        elseif ($eu.gender -match '^\d+$') { $gender = [int]$eu.gender }
        elseif ([string]$eu.gender -eq 'Male') { $gender = 1 }
        elseif ([string]$eu.gender -eq 'Female') { $gender = 2 }
    }

    $bodyObj = @{
        username = $username
        email = if ($eu.email) { [string]$eu.email } else { "$username@odak.local" }
        password = $Password
        firstName = if ($eu.firstName) { [string]$eu.firstName } else { $username }
        lastName = if ($eu.lastName) { [string]$eu.lastName } else { "-" }
        title = $eu.title
        department = $eu.department
        gender = $gender
        phoneNumber = $eu.phoneNumber
        photoUrl = $eu.photoUrl
        groupIds = @($groupIds)
        isActive = [bool]($eu.isActive)
    }

    if ($WhatIf) {
        Write-Host "WhatIf CREATE user: $username groups=$($groupIds.Count) active=$($eu.isActive) src=$($eu.provisioningSource)" -ForegroundColor DarkGray
        $report.usersCreated++
        continue
    }

    try {
        $json = $bodyObj | ConvertTo-Json -Depth 6 -Compress
        $created = Invoke-RestMethod -Uri "$KeeperBaseUrl/api/user" -Method POST -Headers $headers -Body $json -TimeoutSec 180
        if ($created.isSuccess -eq $false) {
            $err = $created.errorMessage
            if (-not $err) { $err = "isSuccess=false" }
            throw $err
        }
        $uid = $created.userId
        if (-not $uid) { $uid = $created.UserId }
        [void]$existingUsernames.Add($username)
        $report.usersCreated++
        Write-Host "  + user $username -> $uid (was $($eu.provisioningSource))" -ForegroundColor Green
    } catch {
        $report.usersFailed++
        $msg = "user '$username': $($_.Exception.Message)"
        if ($_.ErrorDetails.Message) { $msg += " | $($_.ErrorDetails.Message)" }
        $report.failures.Add($msg)
        Write-Host "  ! $msg" -ForegroundColor Red
    }
}

$report.finishedAt = (Get-Date).ToUniversalTime().ToString("o")
$reportPath = Join-Path $ExportDir "import-local-report-$(Get-Date -Format 'yyyyMMdd-HHmmss').json"
$report | ConvertTo-Json -Depth 6 | Set-Content -Path $reportPath -Encoding UTF8

Write-Host "`n=== Done ===" -ForegroundColor Cyan
Write-Host ($report | ConvertTo-Json -Depth 4)
Write-Host "Report: $reportPath" -ForegroundColor Cyan
if ($report.groupsFailed -gt 0 -or $report.usersFailed -gt 0) { exit 2 }
exit 0
