# Mevcut WI'ya eksik legacy kalemleri ekler (tam paket yeniden migrate etmez).
#
# Usage:
#   .\migrate-package-lines-only.ps1 -LegacyJsonPath .\datasets\legacy-package-2018-004-full.json -WorkItemId ab18500e-7dc3-43f4-bc2c-338377450d7c
#   .\migrate-package-lines-only.ps1 -LegacyJsonPath ... -WorkItemKey ODF-0035

param(
    [Parameter(Mandatory = $true)]
    [string]$LegacyJsonPath,

    [string]$WorkItemId = "",
    [string]$WorkItemKey = "",

    [string]$BaseUrl = "http://192.168.20.20:5040",
    [switch]$UseGateway = $true,
    [string]$MappingFile = "",

    [switch]$DryRun
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
$repoRoot = (Resolve-Path (Join-Path $scriptDir "../../../..")).Path

if ([string]::IsNullOrEmpty($MappingFile)) {
    $MappingFile = Join-Path $scriptDir "..\datasets\migration-mapping-poc.json"
}

$dataPath = if ($UseGateway) { "/data/api/v1/data" } else { "/api/v1/data" }
$ocTokenScript = Join-Path $repoRoot "docs/odak/operationcore/scripts/load-operationcore-token.ps1"

if (-not (Test-Path $LegacyJsonPath)) { throw "Legacy JSON yok: $LegacyJsonPath" }

$legacy = Get-Content $LegacyJsonPath -Raw -Encoding UTF8 | ConvertFrom-Json
$legacyPackageId = [string]$legacy.package.id
$packageNo = [string]$legacy.package.package_no

if ([string]::IsNullOrEmpty($WorkItemId) -and [string]::IsNullOrEmpty($WorkItemKey)) {
    if (Test-Path $MappingFile) {
        $reg = Get-Content $MappingFile -Raw -Encoding UTF8 | ConvertFrom-Json
        $entries = if ($reg.migrations) { @($reg.migrations) } else { @($reg) }
        $hit = $entries | Where-Object { [string]$_.legacyPackageId -eq $legacyPackageId -or [string]$_.legacyPackageNo -eq $packageNo } | Select-Object -First 1
        if ($hit) {
            $WorkItemId = [string]$hit.workItemId
            $WorkItemKey = [string]$hit.workItemKey
        }
    }
}
if ([string]::IsNullOrEmpty($WorkItemId)) { throw "WorkItemId bulunamadi — -WorkItemId veya mapping dosyasi gerekli." }

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
    try { return ([datetime]$Value).ToUniversalTime().ToString("o") }
    catch { return $null }
}

Write-Host "`n=== migrate-package-lines-only ===" -ForegroundColor Cyan
Write-Host "Package: $packageNo -> WI $WorkItemKey ($WorkItemId)" -ForegroundColor Cyan
Write-Host "Items in JSON: $(@($legacy.items).Count)" -ForegroundColor Cyan
Write-Host "DryRun: $DryRun`n" -ForegroundColor Cyan

# Mevcut kalemler
$existingFilter = "parentWorkItemId eq '$WorkItemId'"
$existingLines = Invoke-Dg -Method GET -Uri "$BaseUrl$dataPath/odak_siparis_kalemleri?limit=500&filter=$([Uri]::EscapeDataString($existingFilter))"
$existingItems = @()
if ($existingLines -is [Array]) { $existingItems = @($existingLines | Where-Object { $_ -ne $null }) }
elseif ($existingLines.items) { $existingItems = @($existingLines.items) }
elseif ($existingLines.data) { $existingItems = @($existingLines.data) }

$existingLineIds = @{}
foreach ($e in $existingItems) {
    if ($e.legacyLineId) { $existingLineIds[[string]$e.legacyLineId] = $true }
    if ($e.lineNo) { $existingLineIds["no:$($e.lineNo)"] = $true }
}

Write-Host "Mevcut kalem: $($existingItems.Count)" -ForegroundColor Gray

$added = 0
$skipped = 0
foreach ($item in @($legacy.items)) {
    $legacyLineId = [string]$item.id
    $lineNo = [string]$item.number
    if ($existingLineIds.ContainsKey($legacyLineId) -or $existingLineIds.ContainsKey("no:$lineNo")) {
        Write-Host "  SKIP kalem $lineNo (legacyLineId=$legacyLineId)" -ForegroundColor DarkGray
        $skipped++
        continue
    }

    $lineBody = @{
        parentWorkItemId  = $WorkItemId
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
        legacyLineId      = $legacyLineId
        legacyPackageId   = $legacyPackageId
        shippedQuantity   = 0
    }

    if ($DryRun) {
        Write-Host "  [DRY] kalem $lineNo" -ForegroundColor Yellow
        $added++
        continue
    }

    $lineJson = $lineBody | ConvertTo-Json -Depth 6 -Compress
    $lineResp = Invoke-Dg -Method POST -Uri "$BaseUrl$dataPath/odak_siparis_kalemleri" -Body $lineJson
    $lineId = Get-DataId $lineResp
    Write-Host "  OK: kalem $lineNo -> $lineId" -ForegroundColor Green
    $added++
}

Write-Host "`nBitti: +$added yeni, $skipped atlandi (toplam hedef: $(@($legacy.items).Count))" -ForegroundColor Cyan
