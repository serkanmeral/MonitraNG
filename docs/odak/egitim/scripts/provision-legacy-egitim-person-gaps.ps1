# Egitim katilimci gap raporundaki eksik kisileri Keeper Local arsiv kullanicisi olarak olusturur.
# Siparis paket sorumlulari ile ayni kalip (provision-legacy-archive-users / employee-archive).
#
# Onkosul:
#   .\analyze-legacy-egitim-person-gaps.ps1
#   docs/odak/operationcore/scripts/get-operationcore-token-prod.ps1
#
# Usage (repo kokunden):
#   .\docs\odak\egitim\scripts\provision-legacy-egitim-person-gaps.ps1 -DryRun
#   .\docs\odak\egitim\scripts\provision-legacy-egitim-person-gaps.ps1

param(
    [string]$GapReportPath = "",
    [string]$KeeperBaseUrl = "http://192.168.20.8:5040",
    [string]$KeeperPath = "/keeper/api",
    [string]$MapOutputFile = "",
    [string]$ImportBatch = "",
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
$repoRoot = (Resolve-Path (Join-Path $scriptDir "../../../..")).Path
$keeperLib = Join-Path $repoRoot "scripts/tests/MngKeeper/users/lib/LegacyArchiveUserCommon.ps1"
. $keeperLib
. (Join-Path $repoRoot "docs/odak/siparis/scripts/lib/DgMigrationCommon.ps1")

if ([string]::IsNullOrWhiteSpace($GapReportPath)) {
    $GapReportPath = Join-Path $scriptDir "..\datasets\legacy-egitim-person-gap-report.json"
}
if (-not (Test-Path $GapReportPath)) {
    throw "Gap raporu yok: $GapReportPath — once analyze-legacy-egitim-person-gaps.ps1 calistirin."
}
if ([string]::IsNullOrWhiteSpace($ImportBatch)) {
    $ImportBatch = Get-LegacyImportBatchId
}

$report = Get-Content $GapReportPath -Raw -Encoding UTF8 | ConvertFrom-Json
$gaps = @($report.gaps)
if (-not $gaps.Count) {
    Write-Host "Gap yok — provision gerekmiyor." -ForegroundColor Green
    exit 0
}

$keeper = Initialize-ProdKeeperAuthContext -KeeperBaseUrl $KeeperBaseUrl -KeeperPath $KeeperPath

function Invoke-KeeperApi {
    param([string]$Method, [string]$RelativePath, [object]$Body = $null)
    $uri = "$($keeper.KeeperBaseUrl)$($keeper.KeeperPath)$RelativePath"
    $params = @{ Uri = $uri; Method = $Method; Headers = $keeper.Headers }
    if ($null -ne $Body) {
        $params.Body = ($Body | ConvertTo-Json -Depth 10 -Compress)
        $params.ContentType = "application/json; charset=utf-8"
    }
    return Invoke-RestMethod @params -SkipCertificateCheck
}

function Get-AllKeeperUsernames {
    $set = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    $page = 1
    do {
        $resp = Invoke-KeeperApi -Method GET -RelativePath "/User?page=$page&pageSize=200"
        foreach ($u in @($resp.users)) {
            if ($u.username) { $null = $set.Add([string]$u.username) }
        }
        $totalPages = [int]$resp.totalPages
        if ($totalPages -le 0) { $totalPages = 1 }
        $page++
    } while ($page -le $totalPages)
    return $set
}

function Test-KeeperUsernameExists {
    param(
        [System.Collections.Generic.HashSet[string]]$UsernameSet,
        [string]$Username
    )
    if ([string]::IsNullOrWhiteSpace($Username)) { return $false }
    return $UsernameSet.Contains($Username)
}

function Add-KeeperUsernameToSet {
    param(
        [System.Collections.Generic.HashSet[string]]$UsernameSet,
        [string]$Username
    )
    if ([string]::IsNullOrWhiteSpace($Username)) { return }
    $null = $UsernameSet.Add($Username)
}

Write-Host "`n=== provision-legacy-egitim-person-gaps ===" -ForegroundColor Cyan
Write-Host "Gap raporu: $GapReportPath ($($gaps.Count) kisi)" -ForegroundColor Gray
Write-Host "Import batch: $ImportBatch | DryRun: $DryRun" -ForegroundColor Gray

$mapState = Load-LegacyKaliteUserIdMap -MapFile $MapOutputFile
$mapEntries = @{}
foreach ($key in $mapState.entries.Keys) { $mapEntries[$key] = $mapState.entries[$key] }
$movedEmployeeKeys = Repair-LegacyKaliteUserIdMapEmployeeKeys -Entries $mapEntries
if ($movedEmployeeKeys -gt 0) {
    Write-Host "Map: $movedEmployeeKeys employee kaydi e{id} anahtarina tasindi (user/employee id cakismasi onlemi)" -ForegroundColor Yellow
}

$existingUsernames = Get-AllKeeperUsernames
$stats = @{ userCreated = 0; employeeCreated = 0; mappedExisting = 0; skipped = 0; failed = 0 }
$log = [System.Collections.ArrayList]::new()

function Register-UserMap {
    param([string]$LegacyUserId, [hashtable]$Entry)
    if (-not $LegacyUserId) { return }
    $copy = @{}
    foreach ($k in $Entry.Keys) { $copy[$k] = $Entry[$k] }
    $mapEntries[$LegacyUserId] = $copy
}

function Register-EmployeeMap {
    param([string]$LegacyEmployeeId, [hashtable]$Entry)
    if (-not $LegacyEmployeeId) { return }
    $copy = @{}
    foreach ($k in $Entry.Keys) { $copy[$k] = $Entry[$k] }
    $storageKey = Get-LegacyEmployeeMapStorageKey -LegacyKaliteEmployeeId $LegacyEmployeeId
    $mapEntries[$storageKey] = $copy
}

function Test-EmployeeMapEntryReady {
    param([string]$LegacyEmployeeId)
    $storageKey = Get-LegacyEmployeeMapStorageKey -LegacyKaliteEmployeeId $LegacyEmployeeId
    if (-not $mapEntries.ContainsKey($storageKey)) { return $false }
    $entry = $mapEntries[$storageKey]
    $keeperId = [string]$entry.keeperUserId
    if (-not $keeperId -or $keeperId -eq "DRY-RUN") { return $false }
    return [string]$entry.legacyKaliteEmployeeId -eq $LegacyEmployeeId
}

function Invoke-ArchiveUserCreate {
    param(
        [string]$Username,
        [string]$Email,
        [hashtable]$NameParts,
        [hashtable]$CustomData
    )
    $createBody = @{
        username   = $Username
        email      = $Email
        firstName  = $NameParts.FirstName
        lastName   = $NameParts.LastName
        isActive   = $false
        groupIds   = @()
        customData = $CustomData
    }
    if ($DryRun) { return "DRY-RUN" }
    $createResp = Invoke-KeeperApi -Method POST -RelativePath "/User" -Body $createBody
    if (-not $createResp.isSuccess) { throw $createResp.errorMessage }
    $userId = [string]$createResp.userId
    if ([string]::IsNullOrWhiteSpace($userId)) { throw "userId bos" }
    $updateBody = @{
        username             = $Username
        email                = $Email
        firstName            = $NameParts.FirstName
        lastName             = $NameParts.LastName
        isActive             = $false
        includeInApplication = $false
        groupIds             = $null
        customData           = $CustomData
    }
    $updateResp = Invoke-KeeperApi -Method PUT -RelativePath "/User/$userId" -Body $updateBody
    if (-not $updateResp.isSuccess) {
        Write-Host "  UYARI includeInApplication: $($updateResp.errorMessage)" -ForegroundColor Yellow
    }
    return $userId
}

foreach ($gap in $gaps) {
    $empId = [string]$gap.employeeId
    $displayName = [string]$gap.displayName
    $source = [string]$gap.matchSource

    if ($source -eq "legacy_user_not_provisioned") {
        $legacyUserId = [string]$gap.legacyUserId
        $username = [string]$gap.legacyUsername
        if ([string]::IsNullOrWhiteSpace($username)) { $username = "legacy-u$legacyUserId" }

        if ($mapEntries.ContainsKey($legacyUserId) -and $mapEntries[$legacyUserId].keeperUserId -and $mapEntries[$legacyUserId].keeperUserId -ne "DRY-RUN") {
            Write-Host "[SKIP] user $legacyUserId — map'te mevcut" -ForegroundColor DarkGray
            $stats.skipped++
            continue
        }

        if (Test-LegacyUsernameExcluded -Username $username) {
            Write-Host "[SKIP] user $legacyUserId $username — excluded" -ForegroundColor DarkYellow
            $stats.skipped++
            continue
        }

        if (Test-KeeperUsernameExists -UsernameSet $existingUsernames -Username $username) {
            Write-Host "[SKIP] user $legacyUserId $username — username Keeper'da mevcut (manuel map gerekebilir)" -ForegroundColor Yellow
            $stats.skipped++
            [void]$log.Add([pscustomobject]@{
                action = "skipped_username_conflict"
                legacyUserId = $legacyUserId
                username = $username
            })
            continue
        }

        $nameParts = Split-LegacyDisplayName -LegacyName $displayName
        $email = Get-LegacyArchiveSyntheticEmail -LegacyKaliteUserId $legacyUserId
        $customData = New-LegacyImportCustomData `
            -LegacyKaliteUserId $legacyUserId `
            -LegacyKaliteUsername $username `
            -LegacyImportBatch $ImportBatch `
            -LegacyImportSource "kalite.users.egitim"

        try {
            if ($DryRun) {
                Write-Host "[DRY] CREATE user $legacyUserId @$username ($displayName)" -ForegroundColor Cyan
            }
            else {
                Write-Host "[CREATE] user $legacyUserId @$username ($displayName)..." -ForegroundColor Yellow
            }
            $keeperUserId = Invoke-ArchiveUserCreate -Username $username -Email $email -NameParts $nameParts -CustomData $customData
            Register-UserMap -LegacyUserId $legacyUserId -Entry @{
                legacyKaliteUserId   = $legacyUserId
                legacyKaliteUsername = $username
                legacyName           = $displayName
                keeperUserId         = $keeperUserId
                source               = if ($DryRun) { "legacy_import_dry_run" } else { "legacy_egitim_import" }
                legacyImport         = $true
            }
            Add-KeeperUsernameToSet -UsernameSet $existingUsernames -Username $username
            $stats.userCreated++
            if (-not $DryRun) { Write-Host "  OK -> $keeperUserId" -ForegroundColor Green }
        }
        catch {
            $stats.failed++
            [void]$log.Add([pscustomobject]@{ kind = "user"; legacyUserId = $legacyUserId; username = $username; error = $_.Exception.Message })
            Write-Host "[FAIL] user $legacyUserId @$username — $($_.Exception.Message)" -ForegroundColor Red
        }
        continue
    }

    if ($source -eq "no_legacy_user_link") {
        if (Test-EmployeeMapEntryReady -LegacyEmployeeId $empId) {
            Write-Host "[SKIP] employee $empId — map'te mevcut" -ForegroundColor DarkGray
            $stats.skipped++
            continue
        }

        $username = Get-LegacyEmployeeArchiveUsername -LegacyKaliteEmployeeId $empId
        if (Test-KeeperUsernameExists -UsernameSet $existingUsernames -Username $username) {
            Write-Host "[SKIP] employee $empId $username — username mevcut" -ForegroundColor Yellow
            $stats.skipped++
            continue
        }

        $nameParts = Split-LegacyDisplayName -LegacyName $displayName
        $email = "legacy+emp$empId@odak.local"
        $customData = New-LegacyEmployeeImportCustomData `
            -LegacyKaliteEmployeeId $empId `
            -LegacyEmployeeName $displayName `
            -LegacyImportBatch $ImportBatch

        try {
            if ($DryRun) {
                Write-Host "[DRY] CREATE employee $empId -> $username ($displayName)" -ForegroundColor Cyan
            }
            else {
                Write-Host "[CREATE] employee $empId -> $username ($displayName)..." -ForegroundColor Yellow
            }
            $keeperUserId = Invoke-ArchiveUserCreate -Username $username -Email $email -NameParts $nameParts -CustomData $customData
            Register-EmployeeMap -LegacyEmployeeId $empId -Entry @{
                legacyKaliteEmployeeId = $empId
                legacyEmployeeName     = $displayName
                username               = $username
                keeperUserId           = $keeperUserId
                source                 = if ($DryRun) { "legacy_employee_import_dry_run" } else { "legacy_egitim_employee_import" }
                legacyImport           = $true
            }
            Add-KeeperUsernameToSet -UsernameSet $existingUsernames -Username $username
            $stats.employeeCreated++
            if (-not $DryRun) { Write-Host "  OK -> $keeperUserId" -ForegroundColor Green }
        }
        catch {
            $stats.failed++
            [void]$log.Add([pscustomobject]@{ kind = "employee"; employeeId = $empId; username = $username; error = $_.Exception.Message })
            Write-Host "[FAIL] employee $empId $username — $($_.Exception.Message)" -ForegroundColor Red
        }
        continue
    }

    Write-Host "[SKIP] employee $empId — bilinmeyen matchSource: $source" -ForegroundColor DarkYellow
    $stats.skipped++
}

if (-not $DryRun) {
    $saved = Save-LegacyKaliteUserIdMap -Entries $mapEntries -MapFile $MapOutputFile -Meta @{
        importBatch   = $ImportBatch
        gapReportPath = $GapReportPath
        egitimProvision = $stats
    }
    Write-Host "`nMap kaydedildi: $saved" -ForegroundColor Green
}
else {
    Write-Host "`n(DryRun — map yazilmadi)" -ForegroundColor DarkGray
}

Write-Host "`nOzet: userCreated=$($stats.userCreated) employeeCreated=$($stats.employeeCreated) mappedExisting=$($stats.mappedExisting) skipped=$($stats.skipped) failed=$($stats.failed)" -ForegroundColor Cyan

if ($log.Count -gt 0 -and -not $DryRun) {
    $logPath = Join-Path (Get-LegacyArchiveReportsDir) "legacy-egitim-provision-log_$((Get-Date -Format 'yyyyMMdd_HHmmss')).json"
    Write-Utf8JsonFile -Path $logPath -Object ([object[]]$log.ToArray()) -Depth 6
    Write-Host "Log: $logPath" -ForegroundColor Gray
}

if ($stats.failed -gt 0) { exit 1 }
