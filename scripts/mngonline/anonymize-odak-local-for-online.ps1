# Anonymize local odak users (Turkish names) + deactivate AD/noise groups.
# Preserves __dataId / userId / groupId. Uses Keeper Update APIs (Keycloak synced).
#
# Preserve usernames: serkan.meral, odak_admin
# serkan.meral email -> sermeral@gmail.com
#
# Usage (repo root):
#   .\scripts\mngonline\anonymize-odak-local-for-online.ps1 -WhatIf
#   .\scripts\mngonline\anonymize-odak-local-for-online.ps1
#   .\scripts\mngonline\anonymize-odak-local-for-online.ps1 -KeeperBaseUrl http://localhost:5001

param(
    [string]$KeeperBaseUrl = "http://localhost:5001",
    [string]$DomainName = "odak",
    [string]$AdminUsername = "odak_admin",
    [string]$AdminPassword = "Admin123!",
    [string[]]$PreserveUsernames = @("serkan.meral", "odak_admin"),
    [string]$SerkanEmail = "sermeral@gmail.com",
    [int]$Seed = 20260720,
    [switch]$WhatIf,
    [switch]$SkipGroups,
    [switch]$SkipUsers
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path
$artifactDir = Join-Path $repoRoot "docs/monitrang/deploy/mngonline/artifacts"
if (-not (Test-Path $artifactDir)) { New-Item -ItemType Directory -Path $artifactDir -Force | Out-Null }

# Meaningful groups stay active; everything else -> isActive=false
$ActiveGroupNames = @(
    "admins", "managers", "users", "guests", "developers", "testers", "viewers",
    "IK Users", "Kalite Users", "Kalite Yonetici Group", "Planlama Users", "Satin Alma Users",
    "Depo Users", "Erp Users", "BT Users", "DBA Users", "Idare Users", "Talasli Users",
    "Tasarım Users", "Yonetim Users", "MonitraNG Admins", "MonitraNG Users", "RDP_Yetkili"
) | ForEach-Object { $_.ToLowerInvariant() }

$namesPath = Join-Path $PSScriptRoot "turkish-names.json"
$namesJson = Get-Content -Path $namesPath -Raw -Encoding UTF8 | ConvertFrom-Json
$FirstNames = @($namesJson.firstNames)
$LastNames = @($namesJson.lastNames)
if ($FirstNames.Count -lt 10 -or $LastNames.Count -lt 10) {
    throw "turkish-names.json eksik veya okunamadi: $namesPath"
}

function ConvertTo-AsciiUsernamePart {
    param([string]$Text)
    if ([string]::IsNullOrWhiteSpace($Text)) { return "x" }
    # Explicit codepoints avoid file-encoding issues on Windows PowerShell.
    $map = @{
        ([char]0x00E7) = "c"; ([char]0x00C7) = "c"   # ç Ç
        ([char]0x011F) = "g"; ([char]0x011E) = "g"   # ğ Ğ
        ([char]0x0131) = "i"; ([char]0x0130) = "i"   # ı İ
        ([char]0x00F6) = "o"; ([char]0x00D6) = "o"   # ö Ö
        ([char]0x015F) = "s"; ([char]0x015E) = "s"   # ş Ş
        ([char]0x00FC) = "u"; ([char]0x00DC) = "u"   # ü Ü
    }
    $sb = New-Object System.Text.StringBuilder
    foreach ($ch in $Text.ToCharArray()) {
        if ($map.ContainsKey($ch)) { [void]$sb.Append($map[$ch]) }
        elseif ($ch -match "[A-Za-z0-9]") { [void]$sb.Append(([string]$ch).ToLowerInvariant()) }
    }
    $out = $sb.ToString()
    if ([string]::IsNullOrWhiteSpace($out)) { return "user" }
    return $out
}

function Get-AllPaged {
    param([string]$Path, [string]$ArrayProp, [hashtable]$Headers, [string]$BaseUrl)
    $all = [System.Collections.Generic.List[object]]::new()
    $page = 1
    $pageSize = 100
    do {
        $url = "$BaseUrl$Path`?page=$page&pageSize=$pageSize"
        $resp = Invoke-RestMethod -Uri $url -Headers $Headers -Method GET -TimeoutSec 120
        $items = @()
        if ($resp.PSObject.Properties.Name -contains $ArrayProp) { $items = @($resp.$ArrayProp) }
        elseif ($resp.PSObject.Properties.Name -contains "items") { $items = @($resp.items) }
        foreach ($i in $items) { $all.Add($i) }
        $totalPages = 1
        if ($resp.totalPages) { $totalPages = [int]$resp.totalPages }
        elseif ($resp.TotalPages) { $totalPages = [int]$resp.TotalPages }
        $page++
    } while ($page -le $totalPages)
    return $all
}

Write-Host "=== anonymize-odak-local-for-online ===" -ForegroundColor Cyan
Write-Host "Keeper=$KeeperBaseUrl domain=$DomainName WhatIf=$WhatIf Seed=$Seed"

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

$rng = [System.Random]::new($Seed)
$preserveSet = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($p in $PreserveUsernames) { [void]$preserveSet.Add($p) }

$report = [ordered]@{
    startedAt = (Get-Date).ToUniversalTime().ToString("o")
    domain = $DomainName
    seed = $Seed
    whatIf = [bool]$WhatIf
    groupsDeactivated = 0
    groupsLeftActive = 0
    groupsFailed = 0
    usersAnonymized = 0
    usersPreserved = 0
    usersFailed = 0
    userMap = [System.Collections.Generic.List[object]]::new()
    failures = [System.Collections.Generic.List[string]]::new()
}

# --- Groups ---
if (-not $SkipGroups) {
    Write-Host "`n=== Groups ===" -ForegroundColor Yellow
    $groups = Get-AllPaged -Path "/api/group" -ArrayProp "groups" -Headers $headers -BaseUrl $KeeperBaseUrl
    Write-Host "Loaded groups=$($groups.Count)"
    foreach ($g in $groups) {
        $name = [string]$g.name
        $gid = [string]$g.groupId
        if (-not $gid) { $gid = [string]$g.id }
        if ([string]::IsNullOrWhiteSpace($name) -or [string]::IsNullOrWhiteSpace($gid)) { continue }

        $keepActive = $ActiveGroupNames.Contains($name.ToLowerInvariant())
        $desiredActive = $keepActive
        $currentActive = [bool]$g.isActive

        if ($desiredActive) {
            $report.groupsLeftActive++
            if ($currentActive) { continue }
            # ensure active
        }
        else {
            if (-not $currentActive) {
                $report.groupsDeactivated++
                continue
            }
        }

        $bodyObj = @{
            name = $name
            description = if ($null -ne $g.description) { [string]$g.description } else { "" }
            permissions = @($g.permissions)
            isActive = $desiredActive
            includeInApplication = if ($null -ne $g.includeInApplication) { [bool]$g.includeInApplication } else { $true }
        }

        if ($WhatIf) {
            Write-Host "WhatIf group '$name' isActive: $currentActive -> $desiredActive" -ForegroundColor DarkGray
            if (-not $desiredActive) { $report.groupsDeactivated++ } else { $report.groupsLeftActive++ }
            continue
        }

        try {
            $json = $bodyObj | ConvertTo-Json -Depth 6 -Compress
            $null = Invoke-RestMethod -Uri "$KeeperBaseUrl/api/group/$gid" -Method PUT -Headers $headers -Body $json -TimeoutSec 120
            if ($desiredActive) {
                Write-Host "  ~ group ACTIVE  $name" -ForegroundColor Green
            }
            else {
                Write-Host "  ~ group PASSIVE $name" -ForegroundColor Yellow
                $report.groupsDeactivated++
            }
        }
        catch {
            $report.groupsFailed++
            $msg = "group '$name': $($_.Exception.Message)"
            if ($_.ErrorDetails.Message) { $msg += " | $($_.ErrorDetails.Message)" }
            $report.failures.Add($msg)
            Write-Host "  ! $msg" -ForegroundColor Red
        }
    }
}

# --- Users ---
if (-not $SkipUsers) {
    Write-Host "`n=== Users ===" -ForegroundColor Yellow
    $users = Get-AllPaged -Path "/api/user" -ArrayProp "users" -Headers $headers -BaseUrl $KeeperBaseUrl
    Write-Host "Loaded users=$($users.Count)"

    $usedUsernames = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($u in $users) {
        if ($u.username) { [void]$usedUsernames.Add([string]$u.username) }
    }
    # Free preserveds stay; for anonymized we remove old name from set when renaming
    foreach ($p in $PreserveUsernames) { [void]$usedUsernames.Add($p) }

    function New-UniqueTurkishIdentity {
        param([System.Random]$Rng, [System.Collections.Generic.HashSet[string]]$Used)
        for ($attempt = 0; $attempt -lt 500; $attempt++) {
            $fn = $FirstNames[$Rng.Next(0, $FirstNames.Count)]
            $ln = $LastNames[$Rng.Next(0, $LastNames.Count)]
            $base = "$(ConvertTo-AsciiUsernamePart $fn).$(ConvertTo-AsciiUsernamePart $ln)"
            $candidate = $base
            $n = 2
            while ($Used.Contains($candidate)) {
                $candidate = "$base$n"
                $n++
                if ($n -gt 99) { break }
            }
            if (-not $Used.Contains($candidate)) {
                return [pscustomobject]@{ FirstName = $fn; LastName = $ln; Username = $candidate; Email = "$candidate@example.local" }
            }
        }
        throw "Unique Turkish username uretilemedi"
    }

    foreach ($u in $users) {
        $uid = [string]$u.userId
        if (-not $uid) { $uid = [string]$u.id }
        $oldUsername = [string]$u.username
        if ([string]::IsNullOrWhiteSpace($uid) -or [string]::IsNullOrWhiteSpace($oldUsername)) { continue }

        if ($preserveSet.Contains($oldUsername)) {
            $report.usersPreserved++
            $newEmail = [string]$u.email
            $needEmailFix = $false
            if ($oldUsername -eq "serkan.meral" -and $newEmail -ne $SerkanEmail) {
                $newEmail = $SerkanEmail
                $needEmailFix = $true
            }

            $mapEntry = [ordered]@{
                userId = $uid
                preserved = $true
                username = $oldUsername
                email = $newEmail
                firstName = [string]$u.firstName
                lastName = [string]$u.lastName
            }
            $report.userMap.Add($mapEntry)

            if (-not $needEmailFix) { continue }

            $bodyPreserve = @{
                username = $oldUsername
                email = $newEmail
                firstName = [string]$u.firstName
                lastName = [string]$u.lastName
                title = $u.title
                department = $u.department
                gender = if ($null -ne $u.gender) { $u.gender } else { 0 }
                phoneNumber = $u.phoneNumber
                photoUrl = $u.photoUrl
                isActive = [bool]$u.isActive
                includeInApplication = if ($null -ne $u.includeInApplication) { [bool]$u.includeInApplication } else { $true }
                # omit groupIds -> preserve
            }

            if ($WhatIf) {
                Write-Host "WhatIf PRESERVE email fix $oldUsername -> $newEmail" -ForegroundColor DarkGray
                continue
            }

            try {
                $json = $bodyPreserve | ConvertTo-Json -Depth 6 -Compress
                $null = Invoke-RestMethod -Uri "$KeeperBaseUrl/api/user/$uid" -Method PUT -Headers $headers -Body $json -TimeoutSec 180
                Write-Host "  ~ preserve $oldUsername email=$newEmail" -ForegroundColor Green
            }
            catch {
                $report.usersFailed++
                $msg = "preserve '$oldUsername': $($_.Exception.Message)"
                if ($_.ErrorDetails.Message) { $msg += " | $($_.ErrorDetails.Message)" }
                $report.failures.Add($msg)
                Write-Host "  ! $msg" -ForegroundColor Red
            }
            continue
        }

        # Release old username from used set for reuse by others after rename
        [void]$usedUsernames.Remove($oldUsername)
        $identity = New-UniqueTurkishIdentity -Rng $rng -Used $usedUsernames
        [void]$usedUsernames.Add($identity.Username)

        $mapEntry = [ordered]@{
            userId = $uid
            preserved = $false
            oldUsername = $oldUsername
            oldFirstName = [string]$u.firstName
            oldLastName = [string]$u.lastName
            oldEmail = [string]$u.email
            username = $identity.Username
            firstName = $identity.FirstName
            lastName = $identity.LastName
            email = $identity.Email
        }
        $report.userMap.Add($mapEntry)

        $bodyObj = @{
            username = $identity.Username
            email = $identity.Email
            firstName = $identity.FirstName
            lastName = $identity.LastName
            title = $null
            department = $null
            gender = if ($null -ne $u.gender) { $u.gender } else { 0 }
            phoneNumber = $null
            photoUrl = $null
            isActive = [bool]$u.isActive
            includeInApplication = if ($null -ne $u.includeInApplication) { [bool]$u.includeInApplication } else { $true }
        }

        if ($WhatIf) {
            Write-Host "WhatIf ANON $oldUsername -> $($identity.Username) ($($identity.FirstName) $($identity.LastName))" -ForegroundColor DarkGray
            $report.usersAnonymized++
            continue
        }

        try {
            $json = $bodyObj | ConvertTo-Json -Depth 6 -Compress
            $resp = Invoke-RestMethod -Uri "$KeeperBaseUrl/api/user/$uid" -Method PUT -Headers $headers -Body $json -TimeoutSec 180
            if ($resp.isSuccess -eq $false) {
                $err = $resp.errorMessage
                if (-not $err) { $err = "isSuccess=false" }
                throw $err
            }
            $report.usersAnonymized++
            Write-Host "  ~ $oldUsername -> $($identity.Username)" -ForegroundColor Green
        }
        catch {
            $report.usersFailed++
            # rollback username reservation on failure
            [void]$usedUsernames.Remove($identity.Username)
            [void]$usedUsernames.Add($oldUsername)
            $msg = "user '$oldUsername' ($uid): $($_.Exception.Message)"
            if ($_.ErrorDetails.Message) { $msg += " | $($_.ErrorDetails.Message)" }
            $report.failures.Add($msg)
            Write-Host "  ! $msg" -ForegroundColor Red
        }
    }
}

$report.finishedAt = (Get-Date).ToUniversalTime().ToString("o")
$stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$reportPath = Join-Path $artifactDir "anonymize-report-$stamp.json"
# Avoid committing PII of old names? Report is needed for remap audit - store locally; add artifacts to gitignore note
$report | ConvertTo-Json -Depth 8 | Set-Content -Path $reportPath -Encoding UTF8

Write-Host "`n=== Done ===" -ForegroundColor Cyan
Write-Host "groupsDeactivated=$($report.groupsDeactivated) groupsLeftActive=$($report.groupsLeftActive) groupsFailed=$($report.groupsFailed)"
Write-Host "usersAnonymized=$($report.usersAnonymized) usersPreserved=$($report.usersPreserved) usersFailed=$($report.usersFailed)"
Write-Host "Report: $reportPath"
if ($report.groupsFailed -gt 0 -or $report.usersFailed -gt 0) { exit 2 }
exit 0
