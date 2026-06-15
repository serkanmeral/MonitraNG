# POC: Tek legacy is paketini MonitraNG'ye tasir (packages + packageitems)
#
# Usage:
#   .\docs\odak\operationcore\scripts\get-operationcore-token.ps1
#   .\docs\odak\siparis\scripts\migrate-packages-poc.ps1 -LegacyJsonPath .\docs\odak\siparis\datasets\legacy-package-sample.json
#   .\docs\odak\siparis\scripts\migrate-packages-poc.ps1 -LegacyJsonPath ... -DryRun
#
# Girdi: export-legacy-package-sample.ps1 ciktisi veya ayni semada JSON

param(
    [Parameter(Mandatory = $true)]
    [string]$LegacyJsonPath,

    [string]$BaseUrl = "http://192.168.20.20:5040",
    [string]$MoBaseUrl = "http://192.168.20.20:5040/operations",
    [switch]$UseGateway = $true,

    [string]$SeedFile = "",
    [string]$MasterIdsFile = "",
    [string]$MappingOutputFile = "",

    [switch]$DryRun
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
$repoRoot = (Resolve-Path (Join-Path $scriptDir "../../../..")).Path

if ([string]::IsNullOrEmpty($SeedFile)) {
    $SeedFile = Join-Path $repoRoot "docs/odak/is_surecleri/seed/odak-uretim-seed.json"
}
if ([string]::IsNullOrEmpty($MasterIdsFile)) {
    $MasterIdsFile = Join-Path $repoRoot "docs/odak/is_surecleri/seed/odak_master_ids.json"
}
if ([string]::IsNullOrEmpty($MappingOutputFile)) {
    $MappingOutputFile = Join-Path $scriptDir "..\datasets\migration-mapping-poc.json"
}

$dataPath = if ($UseGateway) { "/data/api/v1/data" } else { "/api/v1/data" }
$ocTokenScript = Join-Path $repoRoot "docs/odak/operationcore/scripts/load-operationcore-token.ps1"

if (-not (Test-Path $LegacyJsonPath)) { throw "Legacy JSON yok: $LegacyJsonPath" }
if (-not (Test-Path $SeedFile)) { throw "Seed dosyasi yok: $SeedFile — once seed-operation-core-odak-uretim.ps1 calistirin." }

$legacy = Get-Content $LegacyJsonPath -Raw -Encoding UTF8 | ConvertFrom-Json
$seed = Get-Content $SeedFile -Raw -Encoding UTF8 | ConvertFrom-Json
$masterIds = $null
if (Test-Path $MasterIdsFile) {
    $masterIds = Get-Content $MasterIdsFile -Raw -Encoding UTF8 | ConvertFrom-Json
}

$token = & $ocTokenScript
if ([string]::IsNullOrEmpty($token)) { throw "Token alinamadi." }

$headers = @{
    "Authorization" = "Bearer $token"
    "Content-Type"  = "application/json"
}
[System.Net.ServicePointManager]::ServerCertificateValidationCallback = { $true }

function New-ApiParams {
    param([hashtable]$Extra = @{})
    $p = @{ Headers = $headers; ErrorAction = "Stop" } + $Extra
    if ($BaseUrl.StartsWith("https://") -and (Get-Command Invoke-RestMethod).Parameters.ContainsKey("SkipCertificateCheck")) {
        $p.SkipCertificateCheck = $true
    }
    return $p
}

$dgParams = New-ApiParams
$moParams = New-ApiParams

function Invoke-Dg {
    param([string]$Method, [string]$Uri, [object]$Body = $null)
    $p = @{ Uri = $Uri; Method = $Method } + $dgParams
    if ($null -ne $Body) {
        $p.Body = if ($Body -is [string]) { $Body } else { $Body | ConvertTo-Json -Depth 20 -Compress }
        $p.ContentType = "application/json"
    }
    return Invoke-RestMethod @p
}

function Get-DataId {
    param($Response)
    if (-not $Response) { return $null }
    $d = $Response.data; if (-not $d) { $d = $Response.Data }; if (-not $d) { $d = $Response }
    $id = $d.__dataId; if (-not $id) { $id = $d.dataId }; if (-not $id) { $id = $d.DataId }
    if (-not $id -and $Response.workItem) { $id = $Response.workItem.id }
    return $id
}

function Resolve-CustomerId {
    param([string]$LegacyCustomerId, [string]$FirmName)
    if ($masterIds -and $masterIds.musteriler.'MUS-001') {
        return $masterIds.musteriler.'MUS-001'
    }
    $filter = "unvan:contains:" + ($FirmName.Substring(0, [Math]::Min(20, $FirmName.Length)))
    try {
        $uri = "$BaseUrl$dataPath/odak_musteriler?limit=1&filter=$([Uri]::EscapeDataString($filter))"
        $res = Invoke-Dg -Method GET -Uri $uri
        $items = @($res.items); if (-not $items.Count -and $res.data) { $items = @($res.data) }
        if ($items.Count -gt 0) {
            $id = $items[0].__dataId; if (-not $id) { $id = $items[0].dataId }
            return $id
        }
    }
    catch { }
    return $null
}

function Map-Unit {
    param([string]$LegacyUnit)
    if ([string]::IsNullOrWhiteSpace($LegacyUnit)) { return "adet" }
    $u = $LegacyUnit.Trim().ToLowerInvariant()
    switch -Regex ($u) {
        "^(adet|ad|pcs|ea)$" { return "adet" }
        "^(takim|takım|set)$" { return "takim" }
        "^(kg|kilogram)$" { return "kg" }
        "^(m|metre)$" { return "m" }
        "^(m2|m²)$" { return "m2" }
        default { return "adet" }
    }
}

function To-IsoDate {
    param($Value)
    if ([string]::IsNullOrWhiteSpace($Value)) { return $null }
    try {
        return ([datetime]$Value).ToUniversalTime().ToString("o")
    }
    catch { return $null }
}

function Get-MigrationRegistry {
    if (-not (Test-Path $MappingOutputFile)) { return @() }
    try {
        $raw = Get-Content $MappingOutputFile -Raw -Encoding UTF8 | ConvertFrom-Json
        if ($raw.migrations) { return @($raw.migrations) }
        if ($raw.legacyPackageId -or $raw.legacyPackageNo) { return @($raw) }
    }
    catch { }
    return @()
}

function Save-MigrationEntry {
    param([hashtable]$Entry)
    $existing = @(Get-MigrationRegistry)
    $existing = @($existing | Where-Object {
        [string]$_.legacyPackageId -ne [string]$Entry.legacyPackageId
    })
    $existing += [pscustomobject]$Entry
    @{ migrations = $existing } | ConvertTo-Json -Depth 6 | Set-Content -Path $MappingOutputFile -Encoding UTF8
}

function Find-MigrationEntry {
    param([string]$LegacyPackageId, [string]$PackageNo)
    foreach ($e in (Get-MigrationRegistry)) {
        if ([string]$e.legacyPackageId -eq $LegacyPackageId) { return $e }
        if ([string]$e.legacyPackageNo -eq $PackageNo) { return $e }
    }
    return $null
}

Write-Host "`n=== migrate-packages-poc ===" -ForegroundColor Cyan
Write-Host "Legacy:  $LegacyJsonPath" -ForegroundColor Cyan
Write-Host "Package: $($legacy.package.package_no) — $($legacy.package.name)" -ForegroundColor Cyan
Write-Host "Items:   $(@($legacy.items).Count)" -ForegroundColor Cyan
Write-Host "DryRun:  $DryRun`n" -ForegroundColor Cyan

$pkg = $legacy.package
$legacyPackageId = [string]$pkg.id
$packageNo = [string]$pkg.package_no

$registryHit = Find-MigrationEntry -LegacyPackageId $legacyPackageId -PackageNo $packageNo
if ($registryHit) {
    Write-Host "SKIP: $packageNo zaten migrate (registry -> $($registryHit.workItemKey))" -ForegroundColor Yellow
    exit 0
}

# Idempotent: daha once migrate edildi mi? (DG OData filter — colon syntax bu alanda calismiyor)
$existingFilter = "legacyPackageId eq '$legacyPackageId'"
try {
    $existingLines = Invoke-Dg -Method GET -Uri "$BaseUrl$dataPath/odak_siparis_kalemleri?limit=5&filter=$([Uri]::EscapeDataString($existingFilter))"
    $existingItems = @()
    if ($existingLines -is [Array]) {
        $existingItems = @($existingLines | Where-Object { $_ -ne $null })
    }
    elseif ($existingLines.items) {
        $existingItems = @($existingLines.items | Where-Object { $_ -ne $null })
    }
    elseif ($existingLines.data) {
        $existingItems = @($existingLines.data | Where-Object { $_ -ne $null })
    }
    $existingItems = @($existingItems | Where-Object { [string]$_.legacyPackageId -eq $legacyPackageId })
    if ($existingItems.Count -gt 0 -and $existingItems[0].parentWorkItemId) {
        $parentWi = $existingItems[0].parentWorkItemId
        $parentKey = if ($parentWi -is [string]) { $parentWi } else { $parentWi.key }
        Write-Host "SKIP: legacyPackageId=$legacyPackageId zaten migrate (parentWorkItem=$parentKey)" -ForegroundColor Yellow
        exit 0
    }
}
catch { }

$customerId = Resolve-CustomerId -LegacyCustomerId ([string]$pkg.customer_id) -FirmName ([string]$pkg.firm_name)
if (-not $customerId) {
    Write-Host "WARN: Musteri eslesmedi — MUS-001 veya odak_musteriler lookup gerekir" -ForegroundColor Yellow
}

$workspaceId = $seed.workspaceId
$typeOrderId = if ($seed.types.package) { [string]$seed.types.package } else { [string]$seed.types.order }
$boardProdId = if ($seed.boardPackageId) { [string]$seed.boardPackageId } else { [string]$seed.boardProdId }
$prioNormalId = $seed.priorities.normal

$wiTitle = [string]$pkg.name
if ([string]::IsNullOrWhiteSpace($wiTitle)) { $wiTitle = "Is paketi $packageNo" }

$createBody = @{
    workspaceId = $workspaceId
    typeId      = $typeOrderId
    title       = $wiTitle
    boardId     = $boardProdId
    description = if ($pkg.notes) { [string]$pkg.notes } else { "Legacy migrate: $packageNo" }
    fields      = @{
        priorityId       = $prioNormalId
        orderType        = "seri"
        customerId       = $customerId
        packageNo        = $packageNo
        legacyPackageId  = $legacyPackageId
        customerOrderRef = $packageNo
        beginDate        = To-IsoDate $pkg.begin_date
        plannedDate      = To-IsoDate $pkg.delivery_date
        address          = if ($pkg.address) { [string]$pkg.address } else { $null }
    }
}

if ($DryRun) {
    Write-Host "[DRY] WI create:" -ForegroundColor Yellow
    $createBody | ConvertTo-Json -Depth 6 | Write-Host
}
else {
    $json = $createBody | ConvertTo-Json -Depth 10 -Compress
    $wiResp = Invoke-RestMethod -Uri "$MoBaseUrl/api/v1/work-items" -Method POST -Body $json @moParams
    $wiId = $wiResp.workItem.id
    $wiKey = $wiResp.workItem.key
    Write-Host "OK: WI -> $wiKey ($wiId)" -ForegroundColor Green

    $lineCount = 0
    foreach ($item in @($legacy.items)) {
        $lineBody = @{
            parentWorkItemId  = $wiId
            lineNo            = [int]$item.number
            customerProjectNo = if ($item.customer_project_no) { [string]$item.customer_project_no } else { $null }
            customerPoNo      = if ($item.customer_po_no) { [string]$item.customer_po_no } else { $null }
            customerPoItemNo  = if ($null -ne $item.customer_po_item_no -and $item.customer_po_item_no -ne "") { [int]$item.customer_po_item_no } else { $null }
            description       = [string]$item.description
            poItemRevNo       = if ($item.po_item_rev_no) { [string]$item.po_item_rev_no } else { $null }
            customerJobNo     = if ($item.customer_job_no) { [string]$item.customer_job_no } else { $null }
            quantity          = if ($null -ne $item.count -and $item.count -ne "") { [double]$item.count } else { 0 }
            unit              = Map-Unit ([string]$item.unit)
            unitCost          = if ($item.unit_cost -and $item.unit_cost -ne "") { [double]$item.unit_cost } else { $null }
            totalCost         = if ($item.total_cost -and $item.total_cost -ne "") { [double]$item.total_cost } else { $null }
            currency          = if ($item.currency) { [string]$item.currency } else { $null }
            qualityReqs       = if ($item.quality_reqs) { [string]$item.quality_reqs } else { $null }
            isFai             = [bool]([int]$item.isfai -eq 1)
            shipmentDate      = To-IsoDate $item.shipment_date
            shipmentAddress   = if ($item.shipment_address) { [string]$item.shipment_address } else { $null }
            legacyLineId      = [string]$item.id
            legacyPackageId   = $legacyPackageId
            shippedQuantity   = 0
        }
        $lineJson = $lineBody | ConvertTo-Json -Depth 6 -Compress
        $lineResp = Invoke-Dg -Method POST -Uri "$BaseUrl$dataPath/odak_siparis_kalemleri" -Body $lineJson
        $lineId = Get-DataId $lineResp
        Write-Host "  OK: kalem $($item.number) -> $lineId" -ForegroundColor Green
        $lineCount++
    }

    $map = @{
        migratedAt      = (Get-Date).ToUniversalTime().ToString("o")
        legacyPackageNo = $packageNo
        legacyPackageId = $legacyPackageId
        workItemId      = $wiId
        workItemKey     = $wiKey
        lineCount       = $lineCount
        legacyJsonPath  = (Resolve-Path $LegacyJsonPath).Path
        note            = "Faz 1 POC — Is Paketi WI + odak_siparis_kalemleri"
    }
    Save-MigrationEntry -Entry $map
    Write-Host "`nMapping guncellendi: $MappingOutputFile" -ForegroundColor Cyan
    Write-Host "AF kalemler: /apps/automated-forms/view/odak-siparis-kalemleri-form?filter=parentWorkItemId:eq:$wiId" -ForegroundColor Gray
}

Write-Host "`nPOC bitti." -ForegroundColor Cyan
