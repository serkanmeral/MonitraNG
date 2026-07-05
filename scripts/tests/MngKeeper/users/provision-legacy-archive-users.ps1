# Legacy Kalite kullanicilarini Keeper Local arsiv kullanicisi olarak olusturur
#
# Onkosul:
#   analyze-legacy-package-person-refs.ps1
#   docs/odak/operationcore/scripts/get-operationcore-token-prod.ps1
#
# Kullanim:
#   .\provision-legacy-archive-users.ps1 -DryRun
#   .\provision-legacy-archive-users.ps1
#   .\provision-legacy-archive-users.ps1 -PersonRefsFile .\reports\legacy-package-person-refs_LATEST.json

param(
    [string]$PersonRefsFile = "",
    [string]$KeeperBaseUrl = "http://192.168.20.8:5040",
    [string]$KeeperPath = "/keeper/api",
    [string]$ManualMappingsFile = "",
    [string]$MapOutputFile = "",
    [string]$ImportBatch = "",
    [switch]$DryRun,
    [switch]$SkipAlreadyMapped
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "lib/LegacyArchiveUserCommon.ps1")

if ([string]::IsNullOrWhiteSpace($PersonRefsFile)) {
    $PersonRefsFile = Join-Path (Get-LegacyArchiveReportsDir) "legacy-package-person-refs_LATEST.json"
}
if (-not (Test-Path $PersonRefsFile)) {
    throw "Person refs dosyasi yok: $PersonRefsFile — once analyze-legacy-package-person-refs.ps1 calistirin."
}
if ([string]::IsNullOrWhiteSpace($ImportBatch)) {
    $ImportBatch = Get-LegacyImportBatchId
}

$refs = Get-Content $PersonRefsFile -Raw -Encoding UTF8 | ConvertFrom-Json
$keeper = Initialize-ProdKeeperAuthContext -KeeperBaseUrl $KeeperBaseUrl -KeeperPath $KeeperPath

function Invoke-KeeperApi {
    param(
        [string]$Method,
        [string]$RelativePath,
        [object]$Body = $null
    )
    $uri = "$($keeper.KeeperBaseUrl)$($keeper.KeeperPath)$RelativePath"
    $params = @{
        Uri     = $uri
        Method  = $Method
        Headers = $keeper.Headers
    }
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
        $resp = Invoke-KeeperApi -Method GET -RelativePath "/User?page=$page&pageSize=200&sortBy=username&sortOrder=asc"
        if (-not $resp.isSuccess) { throw "Keeper list hatasi: $($resp.errorMessage)" }
        foreach ($u in @($resp.users)) {
            if ($u.username) { [void]$set.Add([string]$u.username) }
        }
        $totalPages = [int]$resp.totalPages
        if ($totalPages -le 0) { $totalPages = 1 }
        $page++
    } while ($page -le $totalPages)
    return $set
}

Write-Host "=== Legacy arsiv Keeper kullanicisi provision ===" -ForegroundColor Cyan
Write-Host "Person refs: $PersonRefsFile" -ForegroundColor Gray
Write-Host "Import batch: $ImportBatch" -ForegroundColor Gray

$mapState = Load-LegacyKaliteUserIdMap -MapFile $MapOutputFile
$mapEntries = @{}
foreach ($key in $mapState.entries.Keys) {
    $mapEntries[$key] = $mapState.entries[$key]
}

$manualByLegacyId = @{}
if (-not [string]::IsNullOrWhiteSpace($ManualMappingsFile) -and (Test-Path $ManualMappingsFile)) {
    $manual = Get-Content $ManualMappingsFile -Raw -Encoding UTF8 | ConvertFrom-Json
    foreach ($m in @($manual.mappings)) {
        $manualByLegacyId[[string]$m.legacyKaliteUserId] = $m
    }
    Write-Host "Manuel mapping: $($manualByLegacyId.Count) kayit" -ForegroundColor Gray
}

# Zaten Keeper'da eslesenleri map'e yaz
foreach ($row in @($refs.alreadyMatchedInKeeper)) {
    $legacyId = [string]$row.legacyKaliteUserId
    if (-not $legacyId -or -not $row.keeperUserId) { continue }
    if (-not $mapEntries.ContainsKey($legacyId)) {
        $mapEntries[$legacyId] = [ordered]@{
            legacyKaliteUserId   = $legacyId
            legacyKaliteUsername = [string]$row.legacyKaliteUsername
            legacyName           = [string]$row.legacyName
            keeperUserId         = [string]$row.keeperUserId
            source               = "compare_matched"
            legacyImport         = $false
        }
    }
}

$existingUsernames = Get-AllKeeperUsernames
Write-Host "Keeper username havuzu: $($existingUsernames.Count)" -ForegroundColor Gray

$stats = @{
    created   = 0
    skipped   = 0
    mapped    = 0
    manual    = 0
    failed    = 0
    dryRun    = [bool]$DryRun
}
$log = [System.Collections.ArrayList]::new()

function Register-MapEntry {
    param(
        [string]$LegacyId,
        [hashtable]$Entry
    )
    $mapEntries[$LegacyId] = $Entry
}

foreach ($person in @($refs.personsToProvision)) {
    $legacyId = [string]$person.legacyKaliteUserId
    $username = [string]$person.legacyKaliteUsername
    $legacyName = [string]$person.legacyName

    if ($SkipAlreadyMapped -and $mapEntries.ContainsKey($legacyId)) {
        $stats.skipped++
        continue
    }

    if ($manualByLegacyId.ContainsKey($legacyId)) {
        $manual = $manualByLegacyId[$legacyId]
        $keeperUserId = [string]$manual.keeperUserId
        if ($keeperUserId) {
            Register-MapEntry -LegacyId $legacyId -Entry ([ordered]@{
                legacyKaliteUserId   = $legacyId
                legacyKaliteUsername = $username
                legacyName           = $legacyName
                keeperUserId         = $keeperUserId
                source               = "manual_mapping"
                note                 = [string]$manual.note
                legacyImport         = $false
            })
            $stats.manual++
            Write-Host "[MANUAL] $legacyId $username -> $keeperUserId" -ForegroundColor Magenta
            continue
        }
    }

    if ($mapEntries.ContainsKey($legacyId) -and $mapEntries[$legacyId].keeperUserId) {
        $stats.skipped++
        Write-Host "[SKIP] $legacyId $username — map'te mevcut" -ForegroundColor DarkGray
        continue
    }

    if (Test-LegacyUsernameExcluded -Username $username) {
        $stats.skipped++
        Write-Host "[SKIP] $legacyId $username — excluded username" -ForegroundColor DarkYellow
        continue
    }

    if ($existingUsernames.Contains($username)) {
        $stats.skipped++
        [void]$log.Add([pscustomobject]@{
            action = "skipped_username_conflict"
            legacyKaliteUserId = $legacyId
            username = $username
            legacyName = $legacyName
        })
        Write-Host "[SKIP] $legacyId $username — username Keeper'da mevcut (manuel mapping gerekebilir)" -ForegroundColor Yellow
        continue
    }

    $nameParts = Split-LegacyDisplayName -LegacyName $legacyName
    $email = Get-LegacyArchiveSyntheticEmail -LegacyKaliteUserId $legacyId
    $customData = New-LegacyImportCustomData `
        -LegacyKaliteUserId $legacyId `
        -LegacyKaliteUsername $username `
        -LegacyImportBatch $ImportBatch

    $createBody = @{
        username   = $username
        email      = $email
        firstName  = $nameParts.FirstName
        lastName   = $nameParts.LastName
        isActive   = $false
        groupIds   = @()
        customData = $customData
    }

    if ($DryRun) {
        Write-Host "[DRY] CREATE $legacyId $username ($legacyName)" -ForegroundColor Cyan
        $stats.created++
        Register-MapEntry -LegacyId $legacyId -Entry ([ordered]@{
            legacyKaliteUserId   = $legacyId
            legacyKaliteUsername = $username
            legacyName           = $legacyName
            keeperUserId         = "DRY-RUN"
            source               = "legacy_import_dry_run"
            legacyImport         = $true
            customData           = $customData
        })
        continue
    }

    try {
        $createResp = Invoke-KeeperApi -Method POST -RelativePath "/User" -Body $createBody
        if (-not $createResp.isSuccess) {
            throw $createResp.errorMessage
        }
        $userId = [string]$createResp.userId
        if ([string]::IsNullOrWhiteSpace($userId)) { throw "userId bos dondu" }

        $updateBody = @{
            username              = $username
            email                 = $email
            firstName             = $nameParts.FirstName
            lastName              = $nameParts.LastName
            isActive              = $false
            includeInApplication  = $false
            groupIds              = $null
            customData            = $customData
        }
        $updateResp = Invoke-KeeperApi -Method PUT -RelativePath "/User/$userId" -Body $updateBody
        if (-not $updateResp.isSuccess) {
            Write-Host "  UYARI: includeInApplication guncellenemedi: $($updateResp.errorMessage)" -ForegroundColor Yellow
        }

        Register-MapEntry -LegacyId $legacyId -Entry ([ordered]@{
            legacyKaliteUserId   = $legacyId
            legacyKaliteUsername = $username
            legacyName           = $legacyName
            keeperUserId         = $userId
            source               = "legacy_import"
            legacyImport         = $true
            customData           = $customData
        })
        $stats.created++
        Write-Host "[OK] $legacyId $username -> $userId" -ForegroundColor Green
    }
    catch {
        $stats.failed++
        [void]$log.Add([pscustomobject]@{
            action = "failed"
            legacyKaliteUserId = $legacyId
            username = $username
            error = [string]$_.Exception.Message
        })
        Write-Host "[FAIL] $legacyId $username — $($_.Exception.Message)" -ForegroundColor Red
    }
}

if (-not $DryRun) {
    $savedPath = Save-LegacyKaliteUserIdMap -Entries $mapEntries -MapFile $MapOutputFile -Meta @{
        importBatch    = $ImportBatch
        personRefsFile = $PersonRefsFile
        stats          = $stats
    }
    Write-Host "`nMap kaydedildi: $savedPath" -ForegroundColor Green
}
else {
    Write-Host "`n(DryRun — map dosyasi yazilmadi)" -ForegroundColor DarkGray
}

Write-Host "`nOzet: created=$($stats.created) manual=$($stats.manual) skipped=$($stats.skipped) failed=$($stats.failed)" -ForegroundColor Cyan

if ($log.Count -gt 0 -and -not $DryRun) {
    $logPath = Join-Path (Get-LegacyArchiveReportsDir) "legacy-archive-provision-log_$((Get-Date -Format 'yyyyMMdd_HHmmss')).json"
    $libPath = Join-Path $LegacyArchiveRepoRoot "docs/odak/siparis/scripts/lib/DgMigrationCommon.ps1"
    . $libPath
    Write-Utf8JsonFile -Path $logPath -Object ([object[]]$log.ToArray()) -Depth 6
    Write-Host "Log: $logPath" -ForegroundColor Gray
}

if ($stats.failed -gt 0) { exit 1 }
