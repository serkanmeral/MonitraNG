# Legacy is paketi -> odak_is_paketleri + odak_siparis_kalemleri (MO yok, idempotent)
#
# Usage:
#   .\docs\odak\operationcore\scripts\get-operationcore-token.ps1
#   .\export-legacy-package-from-mysql.ps1 -PackageNo "2018-004"
#   .\migrate-legacy-package-to-dg.ps1 -LegacyJsonPath .\datasets\legacy-package-2018-004.json
#   .\migrate-legacy-package-to-dg.ps1 -LegacyJsonPath ... -DryRun

param(
    [Parameter(Mandatory = $true)]
    [string]$LegacyJsonPath,

    [string]$BaseUrl = "http://192.168.20.20:5040",
    [switch]$UseGateway = $true,
    [string]$FirmMappingFile = "",
    [string]$MappingOutputFile = "",
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
$repoRoot = (Resolve-Path (Join-Path $scriptDir "../../../..")).Path
. (Join-Path $scriptDir "lib/DgMigrationCommon.ps1")

if ([string]::IsNullOrEmpty($FirmMappingFile)) {
    $FirmMappingFile = Join-Path $scriptDir "..\datasets\migration-firm-mapping.json"
}
if ([string]::IsNullOrEmpty($MappingOutputFile)) {
    $MappingOutputFile = Join-Path $scriptDir "..\datasets\migration-mapping-dg.json"
}

$dataPath = if ($UseGateway) { "/data/api/v1/data" } else { "/api/v1/data" }
$ocTokenScript = Join-Path $repoRoot "docs/odak/operationcore/scripts/load-operationcore-token.ps1"

if (-not (Test-Path $LegacyJsonPath)) { throw "Legacy JSON yok: $LegacyJsonPath" }

$legacy = Get-Content $LegacyJsonPath -Raw -Encoding UTF8 | ConvertFrom-Json
$firmMap = @{}
if (Test-Path $FirmMappingFile) {
    $fm = Get-Content $FirmMappingFile -Raw -Encoding UTF8 | ConvertFrom-Json
    if ($fm.firms) {
        foreach ($prop in $fm.firms.PSObject.Properties) {
            $firmMap[$prop.Name] = [string]$prop.Value
        }
    }
}

$token = & $ocTokenScript
if ([string]::IsNullOrEmpty($token)) { throw "Token alinamadi." }

$headers = @{
    "Authorization" = "Bearer $token"
    "Content-Type"  = "application/json"
}
[System.Net.ServicePointManager]::ServerCertificateValidationCallback = { $true }

function Invoke-Dg {
    param([string]$Method, [string]$Uri, [object]$Body = $null)
    $p = @{ Uri = $Uri; Method = $Method; Headers = $headers; ErrorAction = "Stop" }
    if ($Uri.StartsWith("https://") -and (Get-Command Invoke-RestMethod).Parameters.ContainsKey("SkipCertificateCheck")) {
        $p.SkipCertificateCheck = $true
    }
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
    return $id
}

function Get-DgItems {
    param($Response)
    if ($Response -is [Array]) { return @($Response) }
    if ($Response.items) { return @($Response.items) }
    if ($Response.data) { return @($Response.data) }
    return @()
}

function To-IsoDate {
    param($Value)
    if ([string]::IsNullOrWhiteSpace($Value)) { return $null }
    try { return ([datetime]$Value).ToUniversalTime().ToString("o") }
    catch { return $null }
}

function To-IntOrNull {
    param($Value)
    if ($null -eq $Value -or $Value -eq "") { return $null }
    return [int]$Value
}

function To-DoubleOrNull {
    param($Value)
    if ($null -eq $Value -or $Value -eq "") { return $null }
    return [double]$Value
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

function Map-PackageStatus {
    param([string]$LegacyStatus)
    if ([string]$LegacyStatus -eq "1") { return "closed" }
    return "open"
}

function Resolve-CustomerId {
    param([string]$LegacyCustomerId)
    if ([string]::IsNullOrWhiteSpace($LegacyCustomerId)) { return $null }
    if ($firmMap.ContainsKey($LegacyCustomerId)) {
        return $firmMap[$LegacyCustomerId]
    }
    $filter = "legacyFirmId eq '$LegacyCustomerId'"
    try {
        $uri = "$BaseUrl$dataPath/odak_musteriler?limit=1&filter=$([Uri]::EscapeDataString($filter))"
        $items = Get-DgItems (Invoke-Dg -Method GET -Uri $uri)
        if ($items.Count -gt 0) {
            $id = $items[0].__dataId; if (-not $id) { $id = $items[0].dataId }
            return $id
        }
    }
    catch { }
    return $null
}

function Get-MigrationRegistry {
    if (-not (Test-Path $MappingOutputFile)) { return @() }
    try {
        $raw = Get-Content $MappingOutputFile -Raw -Encoding UTF8 | ConvertFrom-Json
        if ($raw.migrations) { return @($raw.migrations) }
    }
    catch { }
    return @()
}

function Save-MigrationEntry {
    param([hashtable]$Entry)
    $existing = @(Get-MigrationRegistry)
    $existing = @($existing | Where-Object { [string]$_.legacyPackageId -ne [string]$Entry.legacyPackageId })
    $existing += [pscustomobject]$Entry
    @{ migrations = $existing } | ConvertTo-Json -Depth 6 | Set-Content -Path $MappingOutputFile -Encoding UTF8
}

function Find-ExistingPackage {
    param([string]$LegacyPackageId)
    $filter = "legacyPackageId eq '$LegacyPackageId'"
    try {
        $uri = "$BaseUrl$dataPath/odak_is_paketleri?limit=1&filter=$([Uri]::EscapeDataString($filter))"
        $items = Get-DgItems (Invoke-Dg -Method GET -Uri $uri)
        if ($items.Count -gt 0) { return $items[0] }
    }
    catch { }
    return $null
}

$pkg = $legacy.package
$legacyPackageId = [string]$pkg.id
$packageNo = [string]$pkg.package_no

Write-Host "`n=== migrate-legacy-package-to-dg ===" -ForegroundColor Cyan
Write-Host "Legacy:  $LegacyJsonPath" -ForegroundColor Cyan
Write-Host "Package: $packageNo — $($pkg.name)" -ForegroundColor Cyan
Write-Host "Items:   $(@($legacy.items).Count)" -ForegroundColor Cyan
Write-Host "DryRun:  $DryRun`n" -ForegroundColor Cyan

$registryHit = @(Get-MigrationRegistry) | Where-Object {
    [string]$_.legacyPackageId -eq $legacyPackageId
} | Select-Object -First 1
if ($registryHit) {
    Write-Host "SKIP: $packageNo zaten migrate (registry -> $($registryHit.packageDataId))" -ForegroundColor Yellow
    exit 0
}

$existingPkg = Find-ExistingPackage -LegacyPackageId $legacyPackageId
if ($existingPkg) {
    $existingId = $existingPkg.__dataId; if (-not $existingId) { $existingId = $existingPkg.dataId }
    Write-Host "SKIP: legacyPackageId=$legacyPackageId zaten DG'de ($existingId)" -ForegroundColor Yellow
    exit 0
}

$customerId = Resolve-CustomerId -LegacyCustomerId ([string]$pkg.customer_id)
if (-not $customerId -and $pkg.customer_id) {
    Write-Host "WARN: Musteri eslesmedi (legacy customer_id=$($pkg.customer_id))" -ForegroundColor Yellow
}

$status = Map-PackageStatus -LegacyStatus ([string]$pkg.status)
$closedAt = if ($status -eq "closed") { To-IsoDate $pkg.delivery_date } else { $null }

$packageBody = @{
    legacyPackageId               = $legacyPackageId
    packageNo                     = $packageNo
    name                          = if ($pkg.name) { Limit-LegacyText $pkg.name 500 } else { "Is paketi $packageNo" }
    customerId                    = $customerId
    status                        = $status
    closedAt                      = $closedAt
    beginDate                     = To-IsoDate $pkg.begin_date
    deliveryDate                  = To-IsoDate $pkg.delivery_date
    deliveryAddress               = if ($pkg.address) { Limit-LegacyText $pkg.address 500 } else { $null }
    notes                         = if ($pkg.notes) { Limit-LegacyText $pkg.notes 2000 } else { $null }
    paymentDetail                 = if ($pkg.payment_detail) { Limit-LegacyText $pkg.payment_detail 500 } else { $null }
    partCount                     = To-IntOrNull $pkg.part_count
    stockCount                    = To-IntOrNull $pkg.stock_count
    shippedCount                  = To-IntOrNull $pkg.shipped_count
    poDocumentPath                = if ($pkg.polink) { [string]$pkg.polink } else { $null }
    poDocumentPathRedacted        = if ($pkg.porlink) { [string]$pkg.porlink } else { $null }
    poVersion                     = if ($pkg.po_version) { [string]$pkg.po_version } else { $null }
    legacyResponsibleId           = if ($pkg.responsible) { [string]$pkg.responsible } else { $null }
    legacyDesignResponsibleId     = if ($pkg.design_responsible) { [string]$pkg.design_responsible } else { $null }
    legacyManufactureResponsibleId = if ($pkg.manufacture_responsible) { [string]$pkg.manufacture_responsible } else { $null }
    legacyContactId               = if ($pkg.contact_id) { [string]$pkg.contact_id } else { $null }
    legacyCreatedAt               = To-IsoDate $pkg.created
    legacyCreatedBy               = if ($pkg.created_by) { [string]$pkg.created_by } else { $null }
    lineCount                     = 0
}

if ($DryRun) {
    Write-Host "[DRY] odak_is_paketleri:" -ForegroundColor Yellow
    $packageBody | ConvertTo-Json -Depth 4 | Write-Host
    Write-Host "[DRY] $($legacy.items.Count) kalem olusturulacak" -ForegroundColor Yellow
    exit 0
}

$pkgResp = Invoke-Dg -Method POST -Uri "$BaseUrl$dataPath/odak_is_paketleri" -Body $packageBody
$packageDataId = Get-DataId $pkgResp
Write-Host "OK: odak_is_paketleri -> $packageDataId" -ForegroundColor Green

$lineCount = 0
foreach ($item in @($legacy.items)) {
    $lineBody = @{
        parentPackageId   = $packageDataId
        lineNo            = [int]$item.number
        customerProjectNo = if ($item.customer_project_no) { Limit-LegacyText $item.customer_project_no 64 } else { $null }
        customerPoNo      = if ($item.customer_po_no) { Limit-LegacyText $item.customer_po_no 64 } else { $null }
        customerPoItemNo  = To-IntOrNull $item.customer_po_item_no
        description       = Limit-LegacyText $item.description 2000
        poItemRevNo       = if ($item.po_item_rev_no) { Limit-LegacyText $item.po_item_rev_no 32 } else { $null }
        customerJobNo     = if ($item.customer_job_no) { Limit-LegacyText $item.customer_job_no 64 } else { $null }
        quantity          = if ($null -ne $item.count -and $item.count -ne "") { [double]$item.count } else { 0 }
        unit              = Map-Unit ([string]$item.unit)
        unitCost          = To-DoubleOrNull $item.unit_cost
        totalCost         = To-DoubleOrNull $item.total_cost
        currency          = if ($item.currency) { [string]$item.currency } else { $null }
        qualityReqs       = if ($item.quality_reqs) { Limit-LegacyText $item.quality_reqs 1000 } else { $null }
        isFai             = [bool]([int]$item.isfai -eq 1)
        isFaiComplete     = [bool]([int]$item.faicomp -eq 1)
        shipmentDate      = To-IsoDate $item.shipment_date
        shipmentAddress   = if ($item.shipment_address) { Limit-LegacyText $item.shipment_address 500 } else { $null }
        legacyLineId      = [string]$item.id
        legacyPackageId   = $legacyPackageId
        shippedQuantity   = 0
    }
    $lineResp = Invoke-Dg -Method POST -Uri "$BaseUrl$dataPath/odak_siparis_kalemleri" -Body $lineBody
    $lineId = Get-DataId $lineResp
    Write-Host "  OK: kalem $($item.number) -> $lineId" -ForegroundColor Green
    $lineCount++
}

Invoke-Dg -Method PUT -Uri "$BaseUrl$dataPath/odak_is_paketleri/$packageDataId" -Body @{ lineCount = $lineCount } | Out-Null

$map = @{
    migratedAt      = (Get-Date).ToUniversalTime().ToString("o")
    legacyPackageNo = $packageNo
    legacyPackageId = $legacyPackageId
    packageDataId   = $packageDataId
    lineCount       = $lineCount
    legacyJsonPath  = (Resolve-Path $LegacyJsonPath).Path
    note            = "DG-only — odak_is_paketleri + odak_siparis_kalemleri"
}
Save-MigrationEntry -Entry $map

Write-Host "`nMapping guncellendi: $MappingOutputFile" -ForegroundColor Cyan
Write-Host "AF paket: /apps/automated-forms/view/odak-is-paketleri-form" -ForegroundColor Gray
Write-Host "AF kalemler: filter=parentPackageId:eq:$packageDataId" -ForegroundColor Gray
