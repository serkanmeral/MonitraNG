# Legacy arşiv Keeper kullanıcıları — ortak yardımcılar
# Kullanım: . (Join-Path $PSScriptRoot "lib/LegacyArchiveUserCommon.ps1")

$script:LegacyArchiveUsersDir = Split-Path $PSScriptRoot -Parent
$script:LegacyArchiveRepoRoot = (Resolve-Path (Join-Path $script:LegacyArchiveUsersDir "../../../..")).Path

$script:LegacyArchiveReportsDir = Join-Path $LegacyArchiveRepoRoot "docs/odak/eskiapp/reports"

function Get-LegacyArchiveReportsDir {
    return $script:LegacyArchiveReportsDir
}

function Get-LegacyImportBatchId {
    return (Get-Date -Format "yyyy-MM-dd")
}

function New-LegacyImportCustomData {
    param(
        [Parameter(Mandatory = $true)][string]$LegacyKaliteUserId,
        [Parameter(Mandatory = $true)][string]$LegacyKaliteUsername,
        [string]$LegacyImportSource = "kalite.users",
        [string]$LegacyImportBatch = "",
        [string]$PersonKind = "legacy_archive"
    )
    if ([string]::IsNullOrWhiteSpace($LegacyImportBatch)) {
        $LegacyImportBatch = Get-LegacyImportBatchId
    }
    return @{
        legacyImport         = $true
        legacyKaliteUserId   = [string]$LegacyKaliteUserId
        legacyKaliteUsername = [string]$LegacyKaliteUsername
        legacyImportSource   = [string]$LegacyImportSource
        legacyImportBatch    = [string]$LegacyImportBatch
        personKind           = [string]$PersonKind
    }
}

function New-LegacyEmployeeImportCustomData {
    param(
        [Parameter(Mandatory = $true)][string]$LegacyKaliteEmployeeId,
        [string]$LegacyEmployeeName = "",
        [string]$LegacyImportBatch = "",
        [string]$PersonKind = "legacy_archive"
    )
    if ([string]::IsNullOrWhiteSpace($LegacyImportBatch)) {
        $LegacyImportBatch = Get-LegacyImportBatchId
    }
    return @{
        legacyImport            = $true
        legacyKaliteEmployeeId  = [string]$LegacyKaliteEmployeeId
        legacyEmployeeName      = [string]$LegacyEmployeeName
        legacyImportSource      = "kalite.employees"
        legacyImportBatch       = [string]$LegacyImportBatch
        personKind              = [string]$PersonKind
    }
}

function Get-LegacyEmployeeArchiveUsername {
    param([string]$LegacyKaliteEmployeeId)
    return "legacy-e$LegacyKaliteEmployeeId"
}

function Get-LegacyEmployeeMapStorageKey {
    param([string]$LegacyKaliteEmployeeId)
    return "e$LegacyKaliteEmployeeId"
}

function Repair-LegacyKaliteUserIdMapEmployeeKeys {
    param([hashtable]$Entries)
    $toMove = @()
    foreach ($key in @($Entries.Keys)) {
        $entry = $Entries[$key]
        $eid = [string]$entry.legacyKaliteEmployeeId
        $uid = [string]$entry.legacyKaliteUserId
        if (-not $eid -or $uid) { continue }
        $targetKey = Get-LegacyEmployeeMapStorageKey -LegacyKaliteEmployeeId $eid
        if ([string]$key -eq $targetKey) { continue }
        $toMove += @{ From = [string]$key; To = $targetKey; Entry = $entry }
    }
    foreach ($move in $toMove) {
        if ($Entries.ContainsKey($move.To)) { continue }
        $Entries[$move.To] = $move.Entry
        $Entries.Remove($move.From) | Out-Null
    }
    return $toMove.Count
}

function Split-LegacyDisplayName {
    param([string]$LegacyName)
    $text = ($LegacyName ?? "").Trim()
    if ([string]::IsNullOrWhiteSpace($text)) {
        return @{ FirstName = "Legacy"; LastName = "User" }
    }
    $parts = @($text -split '\s+' | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    if ($parts.Count -eq 1) {
        return @{ FirstName = $parts[0]; LastName = "-" }
    }
    return @{
        FirstName = $parts[0]
        LastName  = ($parts[1..($parts.Count - 1)] -join " ")
    }
}

function Get-LegacyArchiveSyntheticEmail {
    param([string]$LegacyKaliteUserId)
    return "legacy+$LegacyKaliteUserId@odak.local"
}

function Find-LatestLegacyCompareJson {
    param([string]$ReportsDir = "")
    if ([string]::IsNullOrWhiteSpace($ReportsDir)) {
        $ReportsDir = Get-LegacyArchiveReportsDir
    }
    $files = @(Get-ChildItem -Path $ReportsDir -Filter "legacy-keeper-user-compare_*.json" -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -ne "legacy-keeper-user-compare_LATEST.json" } |
        Sort-Object LastWriteTime -Descending)
    if ($files.Count -gt 0) { return $files[0].FullName }
    $cache = Join-Path $ReportsDir "legacy-users-cache.json"
    if (Test-Path $cache) { return $cache }
    return $null
}

function Load-LegacyCompareReport {
    param([string]$CompareJsonPath = "")
    if ([string]::IsNullOrWhiteSpace($CompareJsonPath)) {
        $CompareJsonPath = Find-LatestLegacyCompareJson
    }
    if (-not $CompareJsonPath -or -not (Test-Path $CompareJsonPath)) {
        throw "Legacy compare JSON bulunamadi. Once compare-legacy-kalite-users.ps1 calistirin."
    }
    return Get-Content $CompareJsonPath -Raw -Encoding UTF8 | ConvertFrom-Json
}

function Build-LegacyKaliteUserLookup {
    param($CompareReport)
    $lookup = @{}
    foreach ($row in @($CompareReport.legacyOnly)) {
        $id = [string]$row.legacyId
        if (-not $id) { continue }
        $lookup[$id] = [pscustomobject]@{
            LegacyKaliteUserId   = $id
            LegacyKaliteUsername = [string]$row.legacyUsername
            LegacyName           = [string]$row.legacyName
            LegacyActive         = [bool]$row.legacyActive
            KeeperStatus         = "legacy_only"
            KeeperUserId         = $null
        }
    }
    foreach ($row in @($CompareReport.matched)) {
        $id = [string]$row.legacyId
        if (-not $id) { continue }
        $lookup[$id] = [pscustomobject]@{
            LegacyKaliteUserId   = $id
            LegacyKaliteUsername = [string]$row.legacyUsername
            LegacyName           = [string]$row.legacyName
            LegacyActive         = [bool]$row.legacyActive
            KeeperStatus         = "matched"
            KeeperUserId         = [string]$row.keeperUserId
        }
    }
    return $lookup
}

function Initialize-ProdDgAuthContext {
    param(
        [string]$BaseUrl = "http://192.168.20.8:5040",
        [switch]$UseGateway = $true
    )
    $libPath = Join-Path $LegacyArchiveRepoRoot "docs/odak/siparis/scripts/lib/DgMigrationCommon.ps1"
    if (-not (Test-Path $libPath)) { throw "DgMigrationCommon.ps1 bulunamadi: $libPath" }
    . $libPath
    $env:MNG_OC_USE_PROD_TOKEN = "1"
    $tokenScript = Join-Path $LegacyArchiveRepoRoot "docs/odak/operationcore/scripts/load-operationcore-token.ps1"
    $ctx = Initialize-DgMigrationHeaders -TokenScriptPath $tokenScript
    $dataPath = if ($UseGateway) { "/data/api/v1/data" } else { "/api/v1/data" }
    return @{
        AuthContext = $ctx
        BaseUrl     = $BaseUrl.TrimEnd("/")
        DataPath    = $dataPath
    }
}

function Invoke-ProdDgApi {
    param(
        [hashtable]$DgContext,
        [string]$Method,
        [string]$Uri,
        [object]$Body = $null
    )
    $libPath = Join-Path $LegacyArchiveRepoRoot "docs/odak/siparis/scripts/lib/DgMigrationCommon.ps1"
    if (-not (Get-Command Invoke-DgMigrationApi -ErrorAction SilentlyContinue)) {
        . $libPath
    }
    Invoke-DgMigrationApi -AuthContext $DgContext.AuthContext -Method $Method -Uri $Uri -Body $Body -RetryOnUnauthorized
}

function Initialize-ProdKeeperAuthContext {
    param(
        [string]$KeeperBaseUrl = "http://192.168.20.8:5040",
        [string]$KeeperPath = "/keeper/api",
        [string]$TokenFile = "$env:TEMP\operationcore_dg_token_prod.txt"
    )
    if (-not (Test-Path $TokenFile)) {
        $tokenScript = Join-Path $LegacyArchiveRepoRoot "docs/odak/operationcore/scripts/get-operationcore-token-prod.ps1"
        if (-not (Test-Path $tokenScript)) { throw "Prod token dosyasi yok: $TokenFile" }
        & $tokenScript | Out-Null
    }
    $token = (Get-Content $TokenFile -Raw).Trim()
    if ([string]::IsNullOrWhiteSpace($token)) { throw "Prod Keeper token bos: $TokenFile" }
    return @{
        Token       = $token
        KeeperBaseUrl = $KeeperBaseUrl.TrimEnd("/")
        KeeperPath  = $KeeperPath
        Headers     = @{
            Authorization = "Bearer $token"
            Accept        = "application/json"
            "Content-Type" = "application/json"
        }
    }
}

function Get-AllDgDatasetRows {
    param(
        [hashtable]$DgContext,
        [string]$Dataset,
        [int]$PageSize = 500
    )
    $all = New-Object System.Collections.Generic.List[object]
    $skip = 0
    while ($true) {
        $uri = "{0}{1}/{2}?skip={3}&limit={4}" -f $DgContext.BaseUrl, $DgContext.DataPath, $Dataset, $skip, $PageSize
        $raw = Invoke-ProdDgApi -DgContext $DgContext -Method GET -Uri $uri
        $items = @()
        if ($raw -is [Array]) { $items = @($raw) }
        elseif ($raw.items) { $items = @($raw.items) }
        elseif ($raw.data) { $items = @($raw.data) }
        elseif ($raw.__dataId -or $raw.dataId) { $items = @($raw) }
        if (-not $items.Count) { break }
        foreach ($item in $items) { [void]$all.Add($item) }
        if ($items.Count -lt $PageSize) { break }
        $skip += $PageSize
    }
    return [object[]]$all.ToArray()
}

function Load-LegacyKaliteUserIdMap {
    param([string]$MapFile = "")
    if ([string]::IsNullOrWhiteSpace($MapFile)) {
        $MapFile = Join-Path (Get-LegacyArchiveReportsDir) "legacy-kalite-user-id-map.json"
    }
    if (-not (Test-Path $MapFile)) {
        return @{
            generatedAt = $null
            entries     = @{}
            file        = $MapFile
        }
    }
    $raw = Get-Content $MapFile -Raw -Encoding UTF8 | ConvertFrom-Json
    $entries = @{}
    if ($raw.entries) {
        foreach ($prop in $raw.entries.PSObject.Properties) {
            $entries[$prop.Name] = $prop.Value
        }
    }
    return @{
        generatedAt = [string]$raw.generatedAt
        entries     = $entries
        file        = $MapFile
    }
}

function Save-LegacyKaliteUserIdMap {
    param(
        [hashtable]$Entries,
        [string]$MapFile = "",
        [object]$Meta = $null
    )
    if ([string]::IsNullOrWhiteSpace($MapFile)) {
        $MapFile = Join-Path (Get-LegacyArchiveReportsDir) "legacy-kalite-user-id-map.json"
    }
    $libPath = Join-Path $LegacyArchiveRepoRoot "docs/odak/siparis/scripts/lib/DgMigrationCommon.ps1"
    . $libPath
    $payload = @{
        generatedAt = (Get-Date -Format "yyyy-MM-dd HH:mm:ss")
        description = "legacyKaliteUserId -> Keeper userId (__dataId) mapping for Odak legacy archive users"
        meta        = $Meta
        entries     = $Entries
    }
    Write-Utf8JsonFile -Path $MapFile -Object $payload -Depth 8
    return $MapFile
}

function Test-LegacyUsernameExcluded {
    param([string]$Username)
    $u = ($Username ?? "").Trim().ToLowerInvariant()
    return $u -in @("admin", "administrator")
}

function Get-LegacyPersonIdFromPackageRow {
    param([object]$Value)
    if ($null -eq $Value -or $Value -eq "") { return $null }
    $text = [string]$Value
    if ([string]::IsNullOrWhiteSpace($text)) { return $null }
    if ($text -notmatch '^\d+$') { return $null }
    return $text
}

function Add-LegacyPersonRefUsage {
    param(
        [hashtable]$UsageByLegacyId,
        [string]$LegacyId,
        [string]$FieldName
    )
    if (-not $LegacyId) { return }
    if (-not $UsageByLegacyId.ContainsKey($LegacyId)) {
        $UsageByLegacyId[$LegacyId] = @{
            legacyKaliteUserId = $LegacyId
            packageCount       = 0
            fieldsUsed         = New-Object System.Collections.Generic.HashSet[string]
        }
    }
    $entry = $UsageByLegacyId[$LegacyId]
    $entry.packageCount++
    [void]$entry.fieldsUsed.Add($FieldName)
}
