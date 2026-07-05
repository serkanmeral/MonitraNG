# Legacy Kalite employees -> Keeper Local arsiv kullanicisi
#
# packages.*_responsible alanlari bazen users.id degil employees.id referans eder.
#
# Kullanim:
#   .\resolve-unknown-legacy-person-ids.ps1
#   .\provision-legacy-employee-archive-users.ps1 -DryRun
#   .\provision-legacy-employee-archive-users.ps1

param(
    [string]$ResolvedFile = "",
    [string]$KeeperBaseUrl = "http://192.168.20.8:5040",
    [string]$KeeperPath = "/keeper/api",
    [string]$MapOutputFile = "",
    [string]$ImportBatch = "",
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "lib/LegacyArchiveUserCommon.ps1")

if ([string]::IsNullOrWhiteSpace($ResolvedFile)) {
    $ResolvedFile = Join-Path (Get-LegacyArchiveReportsDir) "legacy-unknown-person-ids-resolved.json"
}
if (-not (Test-Path $ResolvedFile)) {
    throw "Resolved dosya yok: $ResolvedFile"
}
if ([string]::IsNullOrWhiteSpace($ImportBatch)) {
    $ImportBatch = Get-LegacyImportBatchId
}

# employee 225 = Ahmet Emin Gezer -> Keeper agezer (users.id 164)
$employeeKeeperManualMap = @{
    "225" = @{
        keeperUserId = "6a2257d16723c2bd54eec39a"
        note         = "Ahmet Emin Gezer — employees.id=225, users.agezer"
    }
}

$resolved = Get-Content $ResolvedFile -Raw -Encoding UTF8 | ConvertFrom-Json
$employees = @($resolved.employees)
if (-not $employees.Count) { throw "employees listesi bos: $ResolvedFile" }

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

Write-Host "=== Legacy employee arsiv provision ===" -ForegroundColor Cyan
Write-Host "Kaynak: $ResolvedFile ($($employees.Count) employee)" -ForegroundColor Gray

$mapState = Load-LegacyKaliteUserIdMap -MapFile $MapOutputFile
$mapEntries = @{}
foreach ($key in $mapState.entries.Keys) {
    $mapEntries[$key] = $mapState.entries[$key]
}

$stats = @{ created = 0; manual = 0; skipped = 0; failed = 0; dryRun = [bool]$DryRun }

foreach ($emp in $employees) {
    $empId = [string]$emp.id
    $displayName = "$($emp.name) $($emp.surname)".Trim()
    $legacyKey = $empId

    if ($mapEntries.ContainsKey($legacyKey) -and $mapEntries[$legacyKey].keeperUserId) {
        Write-Host "[SKIP] employee $empId — map'te mevcut" -ForegroundColor DarkGray
        $stats.skipped++
        continue
    }

    if ($employeeKeeperManualMap.ContainsKey($empId)) {
        $manual = $employeeKeeperManualMap[$empId]
        $mapEntries[$legacyKey] = [ordered]@{
            legacyKaliteEmployeeId = $empId
            legacyEmployeeName     = $displayName
            keeperUserId           = [string]$manual.keeperUserId
            source                 = "employee_manual_mapping"
            legacyImport           = $false
            note                   = [string]$manual.note
        }
        $stats.manual++
        Write-Host "[MANUAL] employee $empId ($displayName) -> $($manual.keeperUserId)" -ForegroundColor Magenta
        continue
    }

    $username = Get-LegacyEmployeeArchiveUsername -LegacyKaliteEmployeeId $empId
    $nameParts = Split-LegacyDisplayName -LegacyName $displayName
    $email = "legacy+emp$empId@odak.local"
    $customData = New-LegacyEmployeeImportCustomData `
        -LegacyKaliteEmployeeId $empId `
        -LegacyEmployeeName $displayName `
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
        Write-Host "[DRY] CREATE employee $empId -> $username ($displayName)" -ForegroundColor Cyan
        $mapEntries[$legacyKey] = [ordered]@{
            legacyKaliteEmployeeId = $empId
            legacyEmployeeName     = $displayName
            keeperUserId           = "DRY-RUN"
            username               = $username
            source                 = "legacy_employee_import_dry_run"
            legacyImport           = $true
            customData             = $customData
        }
        $stats.created++
        continue
    }

    try {
        $createResp = Invoke-KeeperApi -Method POST -RelativePath "/User" -Body $createBody
        if (-not $createResp.isSuccess) { throw $createResp.errorMessage }
        $userId = [string]$createResp.userId

        $updateBody = @{
            username             = $username
            email                = $email
            firstName            = $nameParts.FirstName
            lastName             = $nameParts.LastName
            isActive             = $false
            includeInApplication = $false
            groupIds             = $null
            customData           = $customData
        }
        $updateResp = Invoke-KeeperApi -Method PUT -RelativePath "/User/$userId" -Body $updateBody
        if (-not $updateResp.isSuccess) {
            Write-Host "  UYARI: includeInApplication: $($updateResp.errorMessage)" -ForegroundColor Yellow
        }

        $mapEntries[$legacyKey] = [ordered]@{
            legacyKaliteEmployeeId = $empId
            legacyEmployeeName     = $displayName
            username               = $username
            keeperUserId           = $userId
            source                 = "legacy_employee_import"
            legacyImport           = $true
            customData             = $customData
        }
        $stats.created++
        Write-Host "[OK] employee $empId $username -> $userId ($displayName)" -ForegroundColor Green
    }
    catch {
        $stats.failed++
        Write-Host "[FAIL] employee $empId — $($_.Exception.Message)" -ForegroundColor Red
    }
}

if (-not $DryRun) {
    $saved = Save-LegacyKaliteUserIdMap -Entries $mapEntries -MapFile $MapOutputFile -Meta @{
        importBatch       = $ImportBatch
        employeeProvision = $stats
        resolvedFile      = $ResolvedFile
    }
    Write-Host "`nMap guncellendi: $saved" -ForegroundColor Green
}

Write-Host "`nOzet: created=$($stats.created) manual=$($stats.manual) skipped=$($stats.skipped) failed=$($stats.failed)" -ForegroundColor Cyan
if ($stats.failed -gt 0) { exit 1 }
