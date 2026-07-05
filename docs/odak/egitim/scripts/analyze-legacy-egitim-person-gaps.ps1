# Legacy egitim katilimcilari (employees_trainings) -> Keeper kullanici esleme analizi
#
# Onceki legacy-Keeper calismasini dikkate alir:
#   - legacy-kalite-user-id-map.json
#   - legacy-keeper-user-compare (matched)
#   - Keeper arsiv kullanicilari (legacy+{userId}@odak.local, legacy+emp{id}@, legacy-e{id})
#
# Usage (repo kokunden):
#   .\docs\odak\egitim\scripts\analyze-legacy-egitim-person-gaps.ps1
#   .\docs\odak\egitim\scripts\analyze-legacy-egitim-person-gaps.ps1 -SqlDumpPath "C:\...\01-kalite.sql"
#   .\docs\odak\egitim\scripts\analyze-legacy-egitim-person-gaps.ps1 -KeeperBaseUrl "http://192.168.20.20:5040"

param(
    [string]$SqlDumpPath = "",
    [string]$KeeperBaseUrl = "http://192.168.20.8:5040",
    [string]$KeeperPath = "/keeper/api",
    [string]$MapFile = "",
    [string]$CompareJsonPath = "",
    [string]$OutputDir = ""
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
$repoRoot = (Resolve-Path (Join-Path $scriptDir "../../../..")).Path
$siparisLib = Join-Path $repoRoot "docs/odak/siparis/scripts/lib"
$keeperLib = Join-Path $repoRoot "scripts/tests/MngKeeper/users/lib/LegacyArchiveUserCommon.ps1"

. (Join-Path $siparisLib "LegacySqlDumpCommon.ps1")
. (Join-Path $siparisLib "DgMigrationCommon.ps1")
. $keeperLib

if ([string]::IsNullOrWhiteSpace($OutputDir)) {
    $OutputDir = Join-Path $repoRoot "docs/odak/egitim/datasets"
}
if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
}
if ([string]::IsNullOrWhiteSpace($MapFile)) {
    $MapFile = Join-Path (Get-LegacyArchiveReportsDir) "legacy-kalite-user-id-map.json"
}
if ([string]::IsNullOrWhiteSpace($CompareJsonPath)) {
    $CompareJsonPath = Find-LatestLegacyCompareJson
}

function Normalize-Username {
    param([string]$Value)
    if ([string]::IsNullOrWhiteSpace($Value)) { return "" }
    return $Value.Trim().ToLowerInvariant()
}

function Fold-AsciiTurkish {
    param([string]$Value)
    if ([string]::IsNullOrWhiteSpace($Value)) { return "" }
    $normalized = $Value.ToLowerInvariant()
    $normalized = $normalized -replace '\s+', ' '
    foreach ($pair in @(
        @([char]0x00E7, 'c'), @([char]0x011F, 'g'), @([char]0x0131, 'i'), @([char]0x00F6, 'o'),
        @([char]0x015F, 's'), @([char]0x00FC, 'u'), @([char]0x00C7, 'c'), @([char]0x011E, 'g'),
        @([char]0x0130, 'i'), @([char]0x00D6, 'o'), @([char]0x015E, 's'), @([char]0x00DC, 'u')
    )) {
        $normalized = $normalized.Replace([string]$pair[0], [string]$pair[1])
    }
    return $normalized.Trim()
}

function Normalize-NameKey {
    param([string]$First, [string]$Last)
    return (Fold-AsciiTurkish "$First $Last".Trim())
}

function Get-NameWordParts {
    param([string]$First, [string]$Last)
    $full = Fold-AsciiTurkish "$First $Last".Trim()
    if ([string]::IsNullOrWhiteSpace($full)) {
        return @{ Full = ""; FirstLastKey = "" }
    }
    $words = @($full -split '\s+' | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    $first = if ($words.Count -gt 0) { $words[0] } else { "" }
    $last = if ($words.Count -gt 1) { $words[$words.Count - 1] } else { "" }
    $firstLastKey = if ($first -and $last) { "$first|$last" } else { "" }
    return @{ Full = $full; FirstLastKey = $firstLastKey }
}

function Test-LikelySamAccountName {
    param([string]$Username)
    $u = Normalize-Username $Username
    if ([string]::IsNullOrWhiteSpace($u)) { return $false }
    if ($u -match '\s') { return $false }
    if ($u -match '\$$') { return $false }
    if ($u -match '^(administrator|admin|dummy|cnc|erp-)') { return $false }
    return $true
}

function Select-BestKeeperCandidate {
    param([array]$Candidates)
    if ($Candidates.Count -eq 1) { return $Candidates[0] }
    $scored = foreach ($c in $Candidates) {
        $score = 0
        if ($c.isActive) { $score += 4 }
        if (Test-LikelySamAccountName $c.username) { $score += 8 }
        if ($c.provisioningSource -eq 'Directory') { $score += 1 }
        [pscustomobject]@{ User = $c; Score = $score }
    }
    $best = @($scored | Sort-Object Score -Descending)[0]
    $same = @($scored | Where-Object { $_.Score -eq $best.Score })
    if ($same.Count -gt 1) { return $null }
    return $best.User
}

function Register-LegacyUserKeeper {
    param(
        [hashtable]$ByLegacyUserId,
        [string]$LegacyUserId,
        [string]$KeeperUserId,
        [string]$Source,
        [string]$Note = ""
    )
    if ([string]::IsNullOrWhiteSpace($LegacyUserId) -or [string]::IsNullOrWhiteSpace($KeeperUserId)) { return }
    if (-not $ByLegacyUserId.ContainsKey($LegacyUserId)) {
        $ByLegacyUserId[$LegacyUserId] = [ordered]@{
            keeperUserId = $KeeperUserId
            source       = $Source
            note         = $Note
        }
    }
}

function Register-LegacyEmployeeKeeper {
    param(
        [hashtable]$ByLegacyEmployeeId,
        [string]$LegacyEmployeeId,
        [string]$KeeperUserId,
        [string]$Source,
        [string]$Note = ""
    )
    if ([string]::IsNullOrWhiteSpace($LegacyEmployeeId) -or [string]::IsNullOrWhiteSpace($KeeperUserId)) { return }
    if (-not $ByLegacyEmployeeId.ContainsKey($LegacyEmployeeId)) {
        $ByLegacyEmployeeId[$LegacyEmployeeId] = [ordered]@{
            keeperUserId = $KeeperUserId
            source       = $Source
            note         = $Note
        }
    }
}

function Build-UnifiedLegacyPersonLookup {
    param(
        [array]$KeeperUsers,
        [hashtable]$MapEntries,
        [string]$ComparePath
    )
    $byLegacyUserId = @{}
    $byLegacyEmployeeId = @{}

    foreach ($key in $MapEntries.Keys) {
        $entry = $MapEntries[$key]
        $keeperId = [string]$entry.keeperUserId
        if (-not $keeperId) { continue }
        $uid = [string]$entry.legacyKaliteUserId
        $eid = [string]$entry.legacyKaliteEmployeeId
        if ($uid) {
            Register-LegacyUserKeeper -ByLegacyUserId $byLegacyUserId -LegacyUserId $uid `
                -KeeperUserId $keeperId -Source "legacy_map_file" -Note "legacyKaliteUserId"
        }
        elseif (-not $eid) {
            Register-LegacyUserKeeper -ByLegacyUserId $byLegacyUserId -LegacyUserId ([string]$key) `
                -KeeperUserId $keeperId -Source "legacy_map_file" -Note ([string]$entry.source)
        }
        if ($eid) {
            Register-LegacyEmployeeKeeper -ByLegacyEmployeeId $byLegacyEmployeeId -LegacyEmployeeId $eid `
                -KeeperUserId $keeperId -Source "legacy_map_file" -Note ([string]$entry.source)
        }
    }

    if ($ComparePath -and (Test-Path $ComparePath)) {
        $compare = Get-Content $ComparePath -Raw -Encoding UTF8 | ConvertFrom-Json
        foreach ($row in @($compare.matched)) {
            Register-LegacyUserKeeper -ByLegacyUserId $byLegacyUserId `
                -LegacyUserId ([string]$row.legacyId) `
                -KeeperUserId ([string]$row.keeperUserId) `
                -Source "compare_matched" `
                -Note ("@$($row.legacyUsername)")
        }
        Write-Host "  Compare matched: $(@($compare.matched).Count) ($ComparePath)" -ForegroundColor Gray
    }

    $archiveFromKeeper = 0
    foreach ($u in $KeeperUsers) {
        $keeperId = [string]$u.userId
        if (-not $keeperId) { continue }

        $cd = $u.customData
        if ($cd) {
            $uid = [string]$cd.legacyKaliteUserId
            if ($uid) {
                Register-LegacyUserKeeper -ByLegacyUserId $byLegacyUserId -LegacyUserId $uid `
                    -KeeperUserId $keeperId -Source "keeper_customData_user"
                $archiveFromKeeper++
            }
            $eid = [string]$cd.legacyKaliteEmployeeId
            if ($eid) {
                Register-LegacyEmployeeKeeper -ByLegacyEmployeeId $byLegacyEmployeeId -LegacyEmployeeId $eid `
                    -KeeperUserId $keeperId -Source "keeper_customData_employee"
                $archiveFromKeeper++
            }
        }

        $email = [string]$u.email
        if ($email -match '^legacy\+(\d+)@') {
            Register-LegacyUserKeeper -ByLegacyUserId $byLegacyUserId -LegacyUserId $matches[1] `
                -KeeperUserId $keeperId -Source "keeper_archive_email_user" -Note $email
            $archiveFromKeeper++
        }
        elseif ($email -match '^legacy\+emp(\d+)@') {
            Register-LegacyEmployeeKeeper -ByLegacyEmployeeId $byLegacyEmployeeId -LegacyEmployeeId $matches[1] `
                -KeeperUserId $keeperId -Source "keeper_archive_email_employee" -Note $email
            $archiveFromKeeper++
        }

        $uname = [string]$u.username
        if ($uname -match '^legacy-e(\d+)$') {
            Register-LegacyEmployeeKeeper -ByLegacyEmployeeId $byLegacyEmployeeId -LegacyEmployeeId $matches[1] `
                -KeeperUserId $keeperId -Source "keeper_archive_username_employee" -Note $uname
            $archiveFromKeeper++
        }
    }
    Write-Host "  Keeper arsiv/customData index: $archiveFromKeeper kayit" -ForegroundColor Gray

    return @{
        ByLegacyUserId     = $byLegacyUserId
        ByLegacyEmployeeId = $byLegacyEmployeeId
        LegacyUserCount    = $byLegacyUserId.Count
        LegacyEmployeeCount = $byLegacyEmployeeId.Count
    }
}

function Get-AllKeeperUsers {
    param([hashtable]$KeeperCtx)
    $all = New-Object System.Collections.Generic.List[object]
    $page = 1
    $pageSize = 200
    while ($true) {
        $uri = "$($KeeperCtx.KeeperBaseUrl)$($KeeperCtx.KeeperPath)/User?page=$page&pageSize=$pageSize"
        $raw = Invoke-RestMethod -Uri $uri -Headers $KeeperCtx.Headers -Method GET -SkipCertificateCheck
        $items = @()
        if ($raw.users) { $items = @($raw.users) }
        elseif ($raw.items) { $items = @($raw.items) }
        elseif ($raw -is [Array]) { $items = @($raw) }
        if (-not $items.Count) { break }
        foreach ($item in $items) { [void]$all.Add($item) }
        $totalPages = [int]$raw.totalPages
        if ($totalPages -le 0) { $totalPages = 1 }
        if ($page -ge $totalPages) { break }
        $page++
    }
    return [object[]]$all.ToArray()
}

function Build-KeeperNameIndex {
    param([array]$KeeperUsers)
    $byFull = @{}
    $byFirstLast = @{}
    $byUsername = @{}
    foreach ($u in $KeeperUsers) {
        $uname = Normalize-Username ([string]$u.username)
        if ($uname -and -not $byUsername.ContainsKey($uname)) {
            $byUsername[$uname] = $u
        }
        $parts = Get-NameWordParts -First ([string]$u.firstName) -Last ([string]$u.lastName)
        if ($parts.Full) {
            if (-not $byFull.ContainsKey($parts.Full)) { $byFull[$parts.Full] = @() }
            $byFull[$parts.Full] += $u
        }
        if ($parts.FirstLastKey) {
            if (-not $byFirstLast.ContainsKey($parts.FirstLastKey)) { $byFirstLast[$parts.FirstLastKey] = @() }
            $byFirstLast[$parts.FirstLastKey] += $u
        }
    }
    return @{ ByFull = $byFull; ByFirstLast = $byFirstLast; ByUsername = $byUsername }
}

function Resolve-EmployeeKeeperId {
    param(
        [string]$EmployeeId,
        [hashtable]$EmployeeById,
        [hashtable]$UserByEmployeeId,
        [hashtable]$LegacyLookup,
        [hashtable]$KeeperIndex
    )
    $emp = $EmployeeById[$EmployeeId]
    $displayName = if ($emp) { "$($emp.name) $($emp.surname)".Trim() } else { "" }
    $byUser = $LegacyLookup.ByLegacyUserId
    $byEmp = $LegacyLookup.ByLegacyEmployeeId

    if ($byEmp.ContainsKey($EmployeeId)) {
        $hit = $byEmp[$EmployeeId]
        return @{
            keeperUserId = [string]$hit.keeperUserId
            source       = [string]$hit.source
            displayName  = $displayName
            note         = [string]$hit.note
        }
    }

    $linkedUser = $UserByEmployeeId[$EmployeeId]
    if ($linkedUser) {
        $legacyUserId = [string]$linkedUser.id
        if ($byUser.ContainsKey($legacyUserId)) {
            $hit = $byUser[$legacyUserId]
            return @{
                keeperUserId = [string]$hit.keeperUserId
                source       = [string]$hit.source
                displayName  = $displayName
                note         = "kalite.users.id=$legacyUserId ($($linkedUser.username)); $($hit.note)"
            }
        }

        $uname = Normalize-Username ([string]$linkedUser.username)
        if ($uname -and $KeeperIndex.ByUsername.ContainsKey($uname)) {
            $ku = $KeeperIndex.ByUsername[$uname]
            return @{
                keeperUserId = [string]$ku.userId
                source       = "keeper_username_active"
                displayName  = $displayName
                note         = "users.id=$legacyUserId -> @$uname (AD/Directory veya mevcut Keeper)"
            }
        }

        return @{
            keeperUserId = $null
            source       = "legacy_user_not_provisioned"
            displayName  = $displayName
            note         = "kalite.users.id=$legacyUserId ($($linkedUser.username)) — Keeper arsiv/map yok"
        }
    }

    if (-not $emp) {
        return @{
            keeperUserId = $null
            source       = "missing_employee_row"
            displayName  = ""
            note         = "employees tablosunda kayit yok"
        }
    }

    return @{
        keeperUserId = $null
        source       = "no_legacy_user_link"
        displayName  = $displayName
        note         = "users.employee_id baglantisi yok; employee arsiv provision gerekir"
    }
}

$SqlDumpPath = Get-LegacySqlDumpPath -SqlDumpPath $SqlDumpPath
Write-Host "`n=== analyze-legacy-egitim-person-gaps ===" -ForegroundColor Cyan
Write-Host "SQL dump: $SqlDumpPath" -ForegroundColor Gray
Write-Host "Keeper:   $KeeperBaseUrl" -ForegroundColor Gray

Write-Host "`n[1] Legacy tablolar parse..." -ForegroundColor Yellow
$employeeRows = Read-SqlInsertRows -Path $SqlDumpPath -TableName "employees"
$userRows = Read-SqlInsertRows -Path $SqlDumpPath -TableName "users"
$partRows = Read-SqlInsertRows -Path $SqlDumpPath -TableName "employees_trainings"
Write-Host "  employees: $($employeeRows.Count)" -ForegroundColor Gray
Write-Host "  users: $($userRows.Count)" -ForegroundColor Gray
Write-Host "  employees_trainings: $($partRows.Count)" -ForegroundColor Gray

$EmployeeById = @{}
foreach ($fields in $employeeRows) {
    if ($fields.Count -lt 15) { continue }
    $id = [string]$fields[0]
    $EmployeeById[$id] = [pscustomobject]@{
        id        = $id
        name      = Limit-LegacyText $fields[8] 255
        surname   = Limit-LegacyText $fields[9] 255
        title     = Limit-LegacyText $fields[10] 255
        status    = [string]$fields[14]
        divisionId = [string]$fields[11]
    }
}

$UserByEmployeeId = @{}
foreach ($fields in $userRows) {
    if ($fields.Count -lt 17) { continue }
    $legacyEmpId = [string]$fields[7]
    if ([string]::IsNullOrWhiteSpace($legacyEmpId)) { continue }
    if (-not $UserByEmployeeId.ContainsKey($legacyEmpId)) {
        $UserByEmployeeId[$legacyEmpId] = [pscustomobject]@{
            id         = [string]$fields[0]
            username   = Limit-LegacyText $fields[5] 50
            name       = Limit-LegacyText $fields[8] 255
            surname    = Limit-LegacyText $fields[9] 255
            status     = [string]$fields[15]
            employeeId = $legacyEmpId
        }
    }
}

$usageByEmployee = @{}
foreach ($fields in $partRows) {
    if ($fields.Count -lt 3) { continue }
    $empId = [string]$fields[2]
    if ([string]::IsNullOrWhiteSpace($empId)) { continue }
    if (-not $usageByEmployee.ContainsKey($empId)) {
        $usageByEmployee[$empId] = [ordered]@{
            employeeId         = $empId
            participationCount   = 0
            distinctTrainingIds  = New-Object System.Collections.Generic.HashSet[int]
        }
    }
    $usageByEmployee[$empId].participationCount++
    [void]$usageByEmployee[$empId].distinctTrainingIds.Add([int]$fields[1])
}

Write-Host "`n[2] Keeper kullanicilari..." -ForegroundColor Yellow
$keeperCtx = Initialize-ProdKeeperAuthContext -KeeperBaseUrl $KeeperBaseUrl -KeeperPath $KeeperPath
$keeperUsers = Get-AllKeeperUsers -KeeperCtx $keeperCtx
Write-Host "  Keeper users: $($keeperUsers.Count)" -ForegroundColor Gray
$keeperIndex = Build-KeeperNameIndex -KeeperUsers $keeperUsers

Write-Host "`n[3] Legacy map + compare + Keeper arsiv index..." -ForegroundColor Yellow
$mapState = Load-LegacyKaliteUserIdMap -MapFile $MapFile
$mapEntriesForLookup = @{}
foreach ($key in $mapState.entries.Keys) { $mapEntriesForLookup[$key] = $mapState.entries[$key] }
$repairedKeys = Repair-LegacyKaliteUserIdMapEmployeeKeys -Entries $mapEntriesForLookup
if ($repairedKeys -gt 0) {
    Write-Host "  Map repair (bellek): $repairedKeys employee kaydi e{id} anahtarina" -ForegroundColor Yellow
}
Write-Host "  Map entries: $($mapEntriesForLookup.Count) ($MapFile)" -ForegroundColor Gray
$legacyLookup = Build-UnifiedLegacyPersonLookup -KeeperUsers $keeperUsers -MapEntries $mapEntriesForLookup -ComparePath $CompareJsonPath
Write-Host "  Unified legacyUserId->Keeper: $($legacyLookup.LegacyUserCount)" -ForegroundColor Gray
Write-Host "  Unified legacyEmployeeId->Keeper: $($legacyLookup.LegacyEmployeeCount)" -ForegroundColor Gray

Write-Host "`n[4] Esleme..." -ForegroundColor Yellow
$resolved = @()
$stats = @{
    totalDistinctEmployees = $usageByEmployee.Count
    matched                = 0
    unmatched              = 0
    ambiguous              = 0
    missingEmployeeRow     = 0
    totalParticipations    = ($partRows.Count)
}

foreach ($empId in ($usageByEmployee.Keys | Sort-Object { [int]$_ })) {
    $usage = $usageByEmployee[$empId]
    $match = Resolve-EmployeeKeeperId `
        -EmployeeId $empId `
        -EmployeeById $EmployeeById `
        -UserByEmployeeId $UserByEmployeeId `
        -LegacyLookup $legacyLookup `
        -KeeperIndex $keeperIndex

    $row = [ordered]@{
        employeeId           = $empId
        displayName          = $match.displayName
        legacyUserId         = if ($UserByEmployeeId.ContainsKey($empId)) { $UserByEmployeeId[$empId].id } else { $null }
        legacyUsername       = if ($UserByEmployeeId.ContainsKey($empId)) { $UserByEmployeeId[$empId].username } else { $null }
        employeeStatus       = if ($EmployeeById.ContainsKey($empId)) { $EmployeeById[$empId].status } else { $null }
        employeeTitle        = if ($EmployeeById.ContainsKey($empId)) { $EmployeeById[$empId].title } else { $null }
        participationCount   = $usage.participationCount
        distinctTrainingCount = $usage.distinctTrainingIds.Count
        keeperUserId         = $match.keeperUserId
        matchSource          = $match.source
        note                 = $match.note
        migrationReady       = [bool]$match.keeperUserId
    }
    $resolved += $row

    switch ($match.source) {
        "missing_employee_row" { $stats.missingEmployeeRow++ ; $stats.unmatched++ }
        { $_ -in @("no_legacy_user_link", "legacy_user_not_provisioned") } { $stats.unmatched++ }
        default { $stats.matched++ }
    }
}

$gaps = @($resolved | Where-Object { -not $_.migrationReady })
$ready = @($resolved | Where-Object { $_.migrationReady })

$report = [ordered]@{
    generatedAt = (Get-Date -Format "yyyy-MM-dd HH:mm:ss")
    egitimNoPolicy = [ordered]@{
        format  = "EGTM{yyyy}/{legacyTrainingId}"
        note    = "Legacy uygulamada ayri egitim no yok; liste baslik (title) ve URL id kullanir. Migrasyonda yil + legacy id ile sabit, izlenebilir numara."
        yearFrom = "COALESCE(training_date, planned_date, created)"
    }
    source = [ordered]@{
        sqlDump              = $SqlDumpPath
        keeperBaseUrl        = $KeeperBaseUrl
        legacyMapFile        = $MapFile
        compareJson          = $CompareJsonPath
        keeperUserCount      = $keeperUsers.Count
        unifiedLegacyUsers   = $legacyLookup.LegacyUserCount
        unifiedLegacyEmployees = $legacyLookup.LegacyEmployeeCount
    }
    stats = [ordered]@{
        distinctEmployeesInTrainings = $stats.totalDistinctEmployees
        migrationReady               = $stats.matched
        gaps                         = $gaps.Count
        unmatched                    = $stats.unmatched
        ambiguousName                = $stats.ambiguous
        missingEmployeeRow           = $stats.missingEmployeeRow
        totalParticipationRows       = $stats.totalParticipations
        participationsBlocked        = ($gaps | ForEach-Object { $_.participationCount } | Measure-Object -Sum).Sum
        participationsReady          = ($ready | ForEach-Object { $_.participationCount } | Measure-Object -Sum).Sum
    }
    gaps = $gaps
    readySummary = @($ready | Group-Object matchSource | ForEach-Object {
        [ordered]@{ matchSource = $_.Name; count = $_.Count }
    })
    employees = $resolved
}

$jsonPath = Join-Path $OutputDir "legacy-egitim-person-gap-report.json"
Write-Utf8JsonFile -Path $jsonPath -Object $report -Depth 8

Write-Host "`n=== SONUC ===" -ForegroundColor Cyan
Write-Host "Farkli calisan (katilimda): $($stats.totalDistinctEmployees)" -ForegroundColor White
Write-Host "Keeper hazir:               $($stats.matched)" -ForegroundColor Green
Write-Host "Eksik / belirsiz:           $($gaps.Count)" -ForegroundColor $(if ($gaps.Count) { "Yellow" } else { "Green" })
Write-Host "Bloklu katilim satiri:      $($report.stats.participationsBlocked)" -ForegroundColor $(if ($report.stats.participationsBlocked) { "Yellow" } else { "Green" })
Write-Host "`nRapor: $jsonPath" -ForegroundColor Cyan

if ($gaps.Count -gt 0) {
    Write-Host "`nEksik kisiler (ilk 20):" -ForegroundColor Yellow
    $gaps | Select-Object -First 20 | ForEach-Object {
        Write-Host "  employee $($_.employeeId) | $($_.displayName) | $($_.participationCount) katilim | $($_.matchSource) | $($_.note)" -ForegroundColor Yellow
    }
}
