# Odak Siparis hub — @side_menu kayitlari
#
# Usage (repo kokunden):
#   .\docs\odak\operationcore\scripts\get-operationcore-token.ps1
#   .\docs\odak\siparis\scripts\patch-odak-siparis-side-menu.ps1
#   $env:MNG_OC_USE_PROD_TOKEN = "1"; .\docs\odak\operationcore\scripts\get-operationcore-token-prod.ps1
#   .\docs\odak\siparis\scripts\patch-odak-siparis-side-menu.ps1 -BaseUrl "http://192.168.20.8:5040"

param(
    [string]$BaseUrl = "http://192.168.20.20:5040",
    [switch]$WhatIf = $false
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
$ocScriptDir = Join-Path (Split-Path (Split-Path $scriptDir -Parent) -Parent) "operationcore\scripts"
$loadTokenScript = Join-Path $ocScriptDir "load-operationcore-token.ps1"
. (Join-Path $scriptDir "lib\DgMigrationCommon.ps1")

if (-not (Test-Path $loadTokenScript)) { throw "Token script yok: $loadTokenScript" }

$token = & $loadTokenScript
if ([string]::IsNullOrEmpty($token)) { throw "Token alinamadi." }

$headers = @{
    Authorization  = "Bearer $token"
}

$dataPath = "/data/api/v1/data/@side_menu"
$listUri = "$BaseUrl$dataPath`?limit=10000&sort=order:asc"

function Get-ItemId($row) {
    if ($null -eq $row) { return $null }
    if ($row.__dataId) { return [string]$row.__dataId }
    if ($row.dataId) { return [string]$row.dataId }
    return $null
}

function Get-MenuItems($response) {
    if ($null -eq $response) { return @() }
    if ($response -is [System.Array]) { return ,$response }
    if ($null -ne $response.items) { return ,@($response.items) }
    if ($response -is [string]) {
        # DG bazen JSON dizisini string dondurur; permissions icinde guests/Guests cakismasi parse'i bozar.
        return @()
    }
    return ,@($response)
}

function Find-MenuIdByPageCode {
    param([string]$PageCode)
    if ([string]::IsNullOrWhiteSpace($PageCode)) { return $null }
    $uri = "$BaseUrl$dataPath`?limit=5&filter=pageCode:eq:$PageCode"
    try {
        $raw = (Invoke-WebRequest -Uri $uri -Headers $headers -Method GET).Content
        if ($raw -notmatch [regex]::Escape($PageCode)) { return $null }
        if ($raw -match '"__dataId"\s*:\s*"([^"]+)"') {
            return $matches[1]
        }
    }
    catch {
        Write-Host "  WARN filter lookup $PageCode -> $($_.Exception.Message)" -ForegroundColor DarkYellow
    }
    return $null
}

Write-Host "Side menu listeleniyor..." -ForegroundColor Cyan
$list = Invoke-RestMethod -Uri $listUri -Headers $headers -Method GET
$items = Get-MenuItems $list
Write-Host "  $($items.Count) kayit (limit 10000)" -ForegroundColor Gray

function Invoke-MenuPut {
    param([string]$Id, [hashtable]$Body)
    if ($WhatIf) { Write-Host "WhatIf PUT $Id" -ForegroundColor Yellow; return }
    $json = ConvertTo-Utf8JsonBody -Object $Body -Depth 12
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($json)
    Invoke-RestMethod -Uri "$BaseUrl$dataPath/$Id" -Headers $headers -Method PUT -Body $bytes -ContentType "application/json; charset=utf-8" | Out-Null
}

function Invoke-MenuPost {
    param([hashtable]$Body)
    if ($WhatIf) { Write-Host "WhatIf POST $($Body.pageCode)" -ForegroundColor Yellow; return $null }
    $json = ConvertTo-Utf8JsonBody -Object $Body -Depth 12
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($json)
    return Invoke-RestMethod -Uri "$BaseUrl$dataPath" -Headers $headers -Method POST -Body $bytes -ContentType "application/json; charset=utf-8"
}

function Upsert-MenuItem {
    param(
        [array]$AllItems,
        [scriptblock]$FindExisting,
        [hashtable]$Body,
        [string]$Label
    )
    $existing = $AllItems | Where-Object $FindExisting | Select-Object -First 1
    $id = Get-ItemId $existing
    if (-not $id -and $Body.pageCode) {
        $id = Find-MenuIdByPageCode -PageCode ([string]$Body.pageCode)
        if ($id) {
            Write-Host "  FOUND via filter: $($Body.pageCode) -> $id" -ForegroundColor Gray
        }
    }

    if (-not $id) {
        Write-Host "POST $Label..." -ForegroundColor Yellow
        try {
            $created = Invoke-MenuPost -Body $Body
            $newId = Get-ItemId $created
            if ([string]::IsNullOrEmpty($newId) -and $Body.pageCode) {
                $newId = Find-MenuIdByPageCode -PageCode ([string]$Body.pageCode)
            }
            Write-Host "  OK $Label -> $newId" -ForegroundColor Green
            return $newId
        }
        catch {
            if ($Body.pageCode) {
                $fallbackId = Find-MenuIdByPageCode -PageCode ([string]$Body.pageCode)
                if ($fallbackId) {
                    Write-Host "  DUPLICATE -> PUT $Label ($fallbackId)" -ForegroundColor Yellow
                    Invoke-MenuPut -Id $fallbackId -Body $Body
                    Write-Host "  SYNC $Label" -ForegroundColor Green
                    return $fallbackId
                }
            }
            throw
        }
    }

    Write-Host "PUT/SYNC $Label ($id)..." -ForegroundColor Yellow
    Invoke-MenuPut -Id $id -Body $Body
    Write-Host "  SYNC $Label" -ForegroundColor Green
    return $id
}

$maxOrder = ($items | ForEach-Object { [int]$_.order } | Measure-Object -Maximum).Maximum
if (-not $maxOrder) { $maxOrder = 300 }

$defaultPerms = [ordered]@{
    groups = [ordered]@{
        admins   = [ordered]@{ view = $true; create = $true; update = $true; delete = $true; export = $true }
        managers = [ordered]@{ view = $true; create = $true; update = $true; delete = $true; export = $true }
        users    = [ordered]@{ view = $true; create = $true; update = $true; delete = $false; export = $false }
        guests   = [ordered]@{ view = $true; create = $false; update = $false; delete = $false; export = $false }
    }
}

$headerId = Upsert-MenuItem -AllItems $items -Label "Odak Siparis header" -FindExisting {
    $_.pageCode -eq "odakSiparis.menuHeader"
} -Body @{
    order       = 266
    itemType    = "header"
    level       = 0
    parentId    = $null
    pageType    = "user"
    pageCode    = "odakSiparis.menuHeader"
    header      = "Odak Sipariş"
    disabled    = $false
    type        = "internal"
    permissions = $defaultPerms
}

Upsert-MenuItem -AllItems $items -Label "Is Paketleri" -FindExisting {
    $_.pageCode -eq "odakSiparis.packages.menuTitle" -or $_.to -eq "/apps/odak-siparis/packages"
} -Body @{
    order       = 267
    itemType    = "item"
    level       = 1
    parentId    = $headerId
    pageType    = "user"
    pageCode    = "odakSiparis.packages.menuTitle"
    title       = "İş Paketleri"
    icon        = "ClipboardListIcon"
    iconType    = "tabler"
    to          = "/apps/odak-siparis/packages"
    type        = "internal"
    disabled    = $false
    permissions = $defaultPerms
} | Out-Null

Upsert-MenuItem -AllItems $items -Label "Aktörler" -FindExisting {
    $_.pageCode -eq "odakSiparis.customers.menuTitle" -or $_.to -eq "/apps/odak-siparis/customers"
} -Body @{
    order       = 268
    itemType    = "item"
    level       = 1
    parentId    = $headerId
    pageType    = "user"
    pageCode    = "odakSiparis.customers.menuTitle"
    title       = "Aktörler"
    icon        = "BuildingIcon"
    iconType    = "tabler"
    to          = "/apps/odak-siparis/customers"
    type        = "internal"
    disabled    = $false
    permissions = $defaultPerms
} | Out-Null

Upsert-MenuItem -AllItems $items -Label "Sevkiyat Listesi" -FindExisting {
    $_.pageCode -eq "odakSiparis.globalShipments.menuTitle" -or $_.to -eq "/apps/odak-siparis/shipments"
} -Body @{
    order       = 269
    itemType    = "item"
    level       = 1
    parentId    = $headerId
    pageType    = "user"
    pageCode    = "odakSiparis.globalShipments.menuTitle"
    title       = "Sevkiyat Listesi"
    icon        = "TruckDeliveryIcon"
    iconType    = "tabler"
    to          = "/apps/odak-siparis/shipments"
    type        = "internal"
    disabled    = $false
    permissions = $defaultPerms
} | Out-Null

Upsert-MenuItem -AllItems $items -Label "Uygunsuzluklar" -FindExisting {
    $_.pageCode -eq "odakSiparis.globalNcr.menuTitle" -or $_.to -eq "/apps/odak-siparis/quality/ncr"
} -Body @{
    order       = 270
    itemType    = "item"
    level       = 1
    parentId    = $headerId
    pageType    = "user"
    pageCode    = "odakSiparis.globalNcr.menuTitle"
    title       = "Uygunsuzluklar"
    icon        = "AlertTriangleIcon"
    iconType    = "tabler"
    to          = "/apps/odak-siparis/quality/ncr"
    type        = "internal"
    disabled    = $false
    permissions = $defaultPerms
} | Out-Null

Write-Host "`nTamamlandi -> /apps/odak-siparis/packages , /apps/odak-siparis/customers , /apps/odak-siparis/shipments , /apps/odak-siparis/quality/ncr" -ForegroundColor Cyan
