# Remap persons / personGroups values in mng_odak (old Odak ids -> local Keeper ids).
#
#   pwsh -File .\scripts\tests\MngKeeper\users\remap-person-ids-local.ps1
#   pwsh -File .\scripts\tests\MngKeeper\users\remap-person-ids-local.ps1 -WhatIf

param(
    [string]$ExportDir = "",
    [string]$KeeperBaseUrl = "http://localhost:5001",
    [string]$DomainName = "odak",
    [string]$AdminUsername = "odak_admin",
    [string]$AdminPassword = "Admin123!",
    [string]$MongoUser = "admin",
    [string]$MongoPassword = "admin123",
    [switch]$WhatIf
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
$repoRoot = (Resolve-Path (Join-Path $scriptDir "../../../..")).Path
if ([string]::IsNullOrWhiteSpace($ExportDir)) {
    $ExportDir = Join-Path $repoRoot "docs/odak/exports/odak-keeper-20260711"
}

$usersPath = Join-Path $ExportDir "users.json"
$groupsPath = Join-Path $ExportDir "groups.json"
$jsPath = Join-Path $scriptDir "remap-person-ids-mng-odak.js"
if (-not (Test-Path $usersPath)) { throw "users.json yok: $usersPath" }
if (-not (Test-Path $groupsPath)) { throw "groups.json yok: $groupsPath" }
if (-not (Test-Path $jsPath)) { throw "JS yok: $jsPath" }

Write-Host "Building id maps..." -ForegroundColor Cyan
$tokenBody = @{ username = $AdminUsername; password = $AdminPassword; domain = $DomainName } | ConvertTo-Json
$token = (Invoke-RestMethod "$KeeperBaseUrl/api/auth/token" -Method POST -Body $tokenBody -ContentType "application/json").accessToken
$headers = @{ Authorization = "Bearer $token" }

function Get-AllPaged([string]$Path, [string]$Prop) {
    $all = [System.Collections.Generic.List[object]]::new()
    $page = 1
    do {
        $resp = Invoke-RestMethod "$KeeperBaseUrl$Path`?page=$page&pageSize=100" -Headers $headers
        foreach ($i in @($resp.$Prop)) { $all.Add($i) }
        $tp = 1
        if ($resp.totalPages) { $tp = [int]$resp.totalPages }
        $page++
    } while ($page -le $tp)
    return $all
}

$localUsers = Get-AllPaged "/api/user" "users"
$localGroups = Get-AllPaged "/api/group" "groups"
$exportUsers = (Get-Content $usersPath -Raw -Encoding UTF8 | ConvertFrom-Json).users
$exportGroups = (Get-Content $groupsPath -Raw -Encoding UTF8 | ConvertFrom-Json).groups

$userByName = @{}
foreach ($u in $localUsers) {
    if ($u.username) { $userByName[[string]$u.username.ToLowerInvariant()] = [string]$u.userId }
}
$groupByName = @{}
foreach ($g in $localGroups) {
    if ($g.name) { $groupByName[[string]$g.name.ToLowerInvariant()] = [string]$g.groupId }
}

$userRows = [System.Collections.Generic.List[object]]::new()
$userMiss = [System.Collections.Generic.List[string]]::new()
foreach ($eu in $exportUsers) {
    $oldId = [string]$eu.userId
    $name = [string]$eu.username
    if ([string]::IsNullOrWhiteSpace($oldId) -or [string]::IsNullOrWhiteSpace($name)) { continue }
    $key = $name.ToLowerInvariant()
    if ($userByName.ContainsKey($key)) {
        $userRows.Add([pscustomobject]@{ oldId = $oldId; newId = $userByName[$key]; username = $name })
    } else {
        $userMiss.Add($name)
    }
}

$groupRows = [System.Collections.Generic.List[object]]::new()
$groupMiss = [System.Collections.Generic.List[string]]::new()
foreach ($eg in $exportGroups) {
    $oldId = [string]$eg.groupId
    $name = [string]$eg.name
    if ([string]::IsNullOrWhiteSpace($oldId) -or [string]::IsNullOrWhiteSpace($name)) { continue }
    $key = $name.ToLowerInvariant()
    if ($groupByName.ContainsKey($key)) {
        $groupRows.Add([pscustomobject]@{ oldId = $oldId; newId = $groupByName[$key]; name = $name })
    } else {
        $groupMiss.Add($name)
    }
}

$mapDir = Join-Path $ExportDir "id-maps"
New-Item -ItemType Directory -Force -Path $mapDir | Out-Null
$userMapPath = Join-Path $mapDir "user-id-map.json"
$groupMapPath = Join-Path $mapDir "group-id-map.json"
$userRows | ConvertTo-Json -Depth 3 | Set-Content $userMapPath -Encoding UTF8
$groupRows | ConvertTo-Json -Depth 3 | Set-Content $groupMapPath -Encoding UTF8

Write-Host "userMap=$($userRows.Count) miss=$($userMiss.Count)"
Write-Host "groupMap=$($groupRows.Count) miss=$($groupMiss.Count)"
if ($userMiss.Count -gt 0) { Write-Host ("user miss: " + ($userMiss -join ', ')) -ForegroundColor Yellow }
if ($groupMiss.Count -gt 0) { Write-Host ("group miss: " + ($groupMiss -join ', ')) -ForegroundColor Yellow }

docker cp $userMapPath "mongo:/tmp/user-id-map.json"
docker cp $groupMapPath "mongo:/tmp/group-id-map.json"
docker cp $jsPath "mongo:/tmp/remap-person-ids-mng-odak.js"

$evalFlag = if ($WhatIf) { "var whatIfFlag=true" } else { "var whatIfFlag=false" }
Write-Host "Running mongosh remap (WhatIf=$WhatIf)..." -ForegroundColor Cyan

$out = docker exec mongo mongosh -u $MongoUser -p $MongoPassword --authenticationDatabase admin --quiet --eval $evalFlag /tmp/remap-person-ids-mng-odak.js 2>&1
$out | ForEach-Object { Write-Host $_ }

$text = ($out | Out-String)
if ($text -match '(?s)REPORT_JSON_BEGIN\s*(\{.*\})\s*REPORT_JSON_END') {
    $reportPath = Join-Path $mapDir ("remap-report-" + (Get-Date -Format "yyyyMMdd-HHmmss") + ".json")
    $Matches[1] | Set-Content $reportPath -Encoding UTF8
    Write-Host "Report: $reportPath" -ForegroundColor Green
} else {
    Write-Host "WARN: report JSON not parsed from mongosh output" -ForegroundColor Yellow
}

docker exec mongo rm -f /tmp/user-id-map.json /tmp/group-id-map.json /tmp/remap-person-ids-mng-odak.js 2>$null
Write-Host "Done." -ForegroundColor Cyan
