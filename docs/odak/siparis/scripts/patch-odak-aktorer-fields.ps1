# Odak — odak_musteriler aktor alanlari (isMusteri / isTedarikci) + mevcut kayit backfill
#
# Usage (repo kokunden):
#   .\docs\odak\operationcore\scripts\get-operationcore-token.ps1
#   .\docs\odak\siparis\scripts\patch-odak-aktorer-fields.ps1
#   .\docs\odak\siparis\scripts\patch-odak-aktorer-fields.ps1 -SkipSchema -SkipSideMenu

param(
    [string]$BaseUrl = "http://192.168.20.20:5040",
    [switch]$UseGateway = $true,
    [switch]$SkipSchema,
    [switch]$SkipBackfill,
    [switch]$SkipSideMenu,
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
$repoRoot = (Resolve-Path (Join-Path $scriptDir "../../../..")).Path
$datasetFile = Join-Path $repoRoot "docs/odak/is_surecleri/datasets/odak_musteriler_dataset.json"

$dataPath = if ($UseGateway) { "/data/api/v1/data" } else { "/api/v1/data" }
$datasetsPath = if ($UseGateway) { "/data/api/v1/datasets" } else { "/api/v1/datasets" }

$ocScripts = Join-Path $repoRoot "docs/odak/operationcore/scripts"
$loadTokenScript = Join-Path $ocScripts "load-operationcore-token.ps1"
if (-not (Test-Path $loadTokenScript)) { throw "Token script yok: $loadTokenScript" }
$token = & $loadTokenScript
if ([string]::IsNullOrEmpty($token)) { throw "Token alinamadi." }

$headers = @{
    Authorization  = "Bearer $token"
    "Content-Type" = "application/json"
}
[System.Net.ServicePointManager]::ServerCertificateValidationCallback = { $true }

function Invoke-Dg {
    param([string]$Method, [string]$Uri, [object]$Body = $null)
    $p = @{ Uri = $Uri; Method = $Method; Headers = $headers; ErrorAction = "Stop" }
    if ($Uri.StartsWith("https://") -and (Get-Command Invoke-RestMethod).Parameters.ContainsKey("SkipCertificateCheck")) {
        $p.SkipCertificateCheck = $true
    }
    if ($null -ne $Body) {
        $p.Body = if ($Body -is [string]) { $Body } else { $Body | ConvertTo-Json -Depth 25 -Compress }
        $p.ContentType = "application/json"
    }
    return Invoke-RestMethod @p
}

function Get-Items {
    param($Response)
    if (-not $Response) { return @() }
    if ($Response -is [Array]) { return @($Response) }
    foreach ($prop in @("data", "Data", "items", "Items", "results", "Results")) {
        if ($null -ne $Response.$prop) {
            $items = $Response.$prop
            if ($items -is [Array]) { return @($items) }
            return @($items)
        }
    }
    return @($Response)
}

function Get-DataId($row) {
    if (-not $row) { return $null }
    if ($row.__dataId) { return [string]$row.__dataId }
    if ($row.dataId) { return [string]$row.dataId }
    return $null
}

function Sync-DatasetSchema {
    if (-not (Test-Path $datasetFile)) { throw "Dataset JSON yok: $datasetFile" }
    $schema = Get-Content $datasetFile -Raw -Encoding UTF8 | ConvertFrom-Json
    $getUri = '{0}{1}/{2}' -f $BaseUrl, $datasetsPath, [Uri]::EscapeDataString($schema.name)

    $fields = @($schema.fields | ForEach-Object {
        $f = @{
            fieldType = $_.fieldType
            name      = $_.name
            title     = $_.title
            mandatory = $_.mandatory
            unique    = $_.unique
            isArray   = $_.isArray
        }
        if ($_.relationDataset) { $f.relationDataset = $_.relationDataset }
        if ($null -ne $_.defaultValue) { $f.defaultValue = $_.defaultValue }
        if ($_.options) { $f.options = $_.options }
        if ($_.validation) { $f.validation = $_.validation }
        $f
    })

    $body = @{
        Description = $schema.description
        ForceSchema = $schema.forceSchema
        Logging     = $schema.logging
        PublishMode = $schema.publish_mode
        Fields      = $fields
        IndexList   = @($schema.indexList)
    }

    if ($DryRun) {
        Write-Host "[DRY] Dataset schema PUT $($schema.name)" -ForegroundColor Yellow
        return
    }
    Invoke-Dg -Method PUT -Uri $getUri -Body $body | Out-Null
    Write-Host "OK: Dataset schema guncellendi -> $($schema.name)" -ForegroundColor Green
}

function Backfill-AktorFlags {
    $updated = 0
    $skipped = 0
    $skip = 0
    $limit = 200

    while ($true) {
        $uri = "$BaseUrl$dataPath/odak_musteriler?limit=$limit&skip=$skip&sort=kod:asc"
        $items = @(Get-Items (Invoke-Dg -Method GET -Uri $uri))
        if (-not $items.Count) { break }

        foreach ($row in $items) {
            $id = Get-DataId $row
            if (-not $id) { continue }

            $hasMusteri = $null -ne $row.PSObject.Properties['isMusteri']
            $hasTedarikci = $null -ne $row.PSObject.Properties['isTedarikci']
            if ($hasMusteri -and $hasTedarikci) {
                $skipped++
                continue
            }

            $patch = @{}
            if (-not $hasMusteri) { $patch.isMusteri = $true }
            if (-not $hasTedarikci) { $patch.isTedarikci = $false }

            if ($DryRun) {
                Write-Host "[DRY] PUT $id -> isMusteri=$($patch.isMusteri) isTedarikci=$($patch.isTedarikci)" -ForegroundColor Yellow
            }
            else {
                Invoke-Dg -Method PUT -Uri "$BaseUrl$dataPath/odak_musteriler/$id" -Body $patch | Out-Null
            }
            $updated++
        }

        if ($items.Count -lt $limit) { break }
        $skip += $limit
    }

    Write-Host "Backfill: $updated guncellendi, $skipped zaten tam" -ForegroundColor Green
}

function Patch-SideMenuTitle {
    $sideMenuPath = "$BaseUrl$dataPath/@side_menu"
    $listUri = "$sideMenuPath`?limit=10000&sort=order:asc"
    $list = Invoke-Dg -Method GET -Uri $listUri
    $items = Get-Items $list

    $target = $items | Where-Object {
        $_.pageCode -eq "odakSiparis.customers.menuTitle" -or $_.to -eq "/apps/odak-siparis/customers"
    } | Select-Object -First 1

    if (-not $target) {
        Write-Host "WARN: Siparis aktor menu kaydi bulunamadi — patch-odak-siparis-side-menu.ps1 calistirin" -ForegroundColor Yellow
        return
    }

    $id = Get-DataId $target
    if (-not $id) { throw "Menu id alinamadi" }

    $body = @{}
    foreach ($prop in $target.PSObject.Properties) {
        if ($prop.Name -in @('__dataId', 'dataId', '_id')) { continue }
        $body[$prop.Name] = $prop.Value
    }
    $body.title = "Aktörler"
    $body.pageCode = "odakSiparis.customers.menuTitle"

    if ($DryRun) {
        Write-Host "[DRY] Side menu PUT $id -> Aktörler" -ForegroundColor Yellow
        return
    }
    Invoke-Dg -Method PUT -Uri "$sideMenuPath/$id" -Body $body | Out-Null
    Write-Host "OK: Side menu -> Aktörler ($id)" -ForegroundColor Green
}

Write-Host "`n=== patch-odak-aktorer-fields ===" -ForegroundColor Cyan
Write-Host "Gateway: $BaseUrl | DryRun: $DryRun`n" -ForegroundColor Cyan

if (-not $SkipSchema) { Sync-DatasetSchema }
if (-not $SkipBackfill) { Backfill-AktorFlags }
if (-not $SkipSideMenu) { Patch-SideMenuTitle }

Write-Host "`nTamamlandi." -ForegroundColor Cyan
