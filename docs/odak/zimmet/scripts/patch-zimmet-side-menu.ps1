# Zimmet Automated Forms — @side_menu kayitlari
#
# Yapi:
#   Dinamik Formlar (header)
#     └── Zimmet Yonetimi (parent)
#           ├── Demirbaslar
#           ├── Urun Katalogu
#           ├── Urun Gruplari
#           ├── Depolar
#           └── Depo Lokasyonlari
#
# Usage (repo kokunden):
#   .\docs\odak\operationcore\scripts\get-operationcore-token.ps1
#   .\docs\odak\zimmet\scripts\patch-zimmet-side-menu.ps1

param(
    [string]$BaseUrl = "http://192.168.20.20:5040",
    [switch]$WhatIf = $false
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
$ocScriptDir = Join-Path (Split-Path (Split-Path $scriptDir -Parent) -Parent) "operationcore\scripts"
$loadTokenScript = Join-Path $ocScriptDir "load-operationcore-token.ps1"

if (-not (Test-Path $loadTokenScript)) {
    Write-Host "load-operationcore-token.ps1 bulunamadi: $loadTokenScript" -ForegroundColor Red
    exit 1
}

$token = & $loadTokenScript
if ([string]::IsNullOrEmpty($token)) {
    Write-Host "Token alinamadi." -ForegroundColor Red
    exit 1
}

$headers = @{
    Authorization  = "Bearer $token"
    "Content-Type" = "application/json"
}

$dataPath = "/data/api/v1/data/@side_menu"
$listUri = "$BaseUrl$dataPath`?limit=10000&sort=order:asc"

function Get-ItemId($row) {
    if ($null -eq $row) { return $null }
    if ($row.__dataId) { return [string]$row.__dataId }
    if ($row.dataId) { return [string]$row.dataId }
    if ($row.DataId) { return [string]$row.DataId }
    if ($row.id) { return [string]$row.id }
    return $null
}

function Get-MenuItems($response) {
    if ($null -eq $response) { return @() }
    if ($response -is [System.Array]) { return ,$response }
    if ($null -ne $response.items) { return ,@($response.items) }
    return ,@($response)
}

Write-Host "Side menu listeleniyor..." -ForegroundColor Cyan
$list = Invoke-RestMethod -Uri $listUri -Headers $headers -Method GET
$items = Get-MenuItems $list
Write-Host "  $($items.Count) kayit" -ForegroundColor Gray

function Invoke-MenuPut {
    param([string]$Id, [hashtable]$Body)
    if ($WhatIf) {
        Write-Host "WhatIf PUT $Id -> $($Body | ConvertTo-Json -Compress)" -ForegroundColor Yellow
        return
    }
    $json = $Body | ConvertTo-Json -Depth 12 -Compress
    Invoke-RestMethod -Uri "$BaseUrl$dataPath/$Id" -Headers $headers -Method PUT -Body $json | Out-Null
}

function Invoke-MenuPost {
    param([hashtable]$Body)
    if ($WhatIf) {
        Write-Host "WhatIf POST -> $($Body | ConvertTo-Json -Compress)" -ForegroundColor Yellow
        return $null
    }
    $json = $Body | ConvertTo-Json -Depth 12 -Compress
    return Invoke-RestMethod -Uri "$BaseUrl$dataPath" -Headers $headers -Method POST -Body $json
}

function Find-MenuIdByPageCode {
    param([string]$PageCode)
    if ([string]::IsNullOrWhiteSpace($PageCode)) { return $null }
    $uri = "$BaseUrl$dataPath`?limit=5&filter=$([Uri]::EscapeDataString("pageCode:eq:$PageCode"))"
    try {
        $resp = Invoke-RestMethod -Uri $uri -Headers $headers -Method GET
        $rows = Get-MenuItems $resp
        if ($rows.Count -gt 0) { return Get-ItemId $rows[0] }
    }
    catch {
        Write-Host "  WARN filter lookup $PageCode -> $($_.Exception.Message)" -ForegroundColor DarkYellow
    }
    return $null
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
            $existing = $AllItems | Where-Object { (Get-ItemId $_) -eq $id } | Select-Object -First 1
        }
    }

    if (-not $existing -and -not $id) {
        Write-Host "POST $Label (order=$($Body.order))..." -ForegroundColor Yellow
        $created = Invoke-MenuPost -Body $Body
        $newId = Get-ItemId $created
        if ([string]::IsNullOrEmpty($newId) -and $Body.pageCode) {
            $newId = Find-MenuIdByPageCode -PageCode ([string]$Body.pageCode)
        }
        Write-Host "  OK $Label id=$newId" -ForegroundColor Green
        return @{ id = $newId; row = $created }
    }

    $needsFix = $false
    foreach ($key in $Body.Keys) {
        $expected = $Body[$key]
        $actual = $existing.$key
        if ($null -eq $expected -and [string]::IsNullOrEmpty($actual)) { continue }
        if ("$actual" -ne "$expected") {
            $needsFix = $true
            break
        }
    }

    if ($needsFix) {
        Write-Host "PUT $Label (id=$id)..." -ForegroundColor Yellow
        Invoke-MenuPut -Id $id -Body $Body
        Write-Host "  OK $Label guncellendi" -ForegroundColor Green
    }
    else {
        Write-Host "SKIP: $Label zaten dogru (id=$id)" -ForegroundColor Green
    }

    return @{ id = $id; row = $existing }
}

$maxOrder = 0
foreach ($row in $items) {
    if ($null -ne $row.order) {
        $o = [int]$row.order
        if ($o -gt $maxOrder) { $maxOrder = $o }
    }
}

# --- Dinamik Formlar header ---
$dfHeaderId = Find-MenuIdByPageCode -PageCode "dynamicForms.menuHeader"
if ([string]::IsNullOrEmpty($dfHeaderId)) {
    $dfHeaderRow = $items | Where-Object {
        $_.itemType -eq "header" -and (
            $_.pageCode -eq "dynamicForms.menuHeader" -or
            ($_.header -and ($_.header -eq "Dinamik Formlar" -or $_.header -eq "Dynamic Forms"))
        )
    } | Select-Object -First 1
    $dfHeaderId = Get-ItemId $dfHeaderRow
}

if ([string]::IsNullOrEmpty($dfHeaderId)) {
    $headerOrder = [Math]::Max($maxOrder + 1, 230)
    Write-Host "Dinamik Formlar header bulunamadi; olusturuluyor..." -ForegroundColor Yellow
    $dfHeaderResult = Upsert-MenuItem -AllItems $items -Label "Dinamik Formlar header" -FindExisting { $false } -Body @{
        order    = $headerOrder
        itemType = "header"
        level    = 0
        parentId = $null
        pageType = "manager"
        pageCode = "dynamicForms.menuHeader"
        header   = "Dinamik Formlar"
        disabled = $false
    }
    $dfHeaderId = $dfHeaderResult.id
}
else {
    Write-Host "Dinamik Formlar header: $dfHeaderId" -ForegroundColor Gray
}

if ([string]::IsNullOrEmpty($dfHeaderId)) {
    Write-Host "Dinamik Formlar header id alinamadi; cikiliyor." -ForegroundColor Red
    exit 1
}

$childOrders = @()
foreach ($row in $items) {
    if ("$($row.parentId)" -eq "$dfHeaderId" -and $null -ne $row.order) {
        $childOrders += [int]$row.order
    }
}
$baseOrder = if ($childOrders.Count -gt 0) { ($childOrders | Measure-Object -Maximum).Maximum + 1 } else { 231 }

$defaultPerms = [ordered]@{
    groups = [ordered]@{
        admins   = [ordered]@{ view = $true; create = $true; update = $true; delete = $true; export = $true }
        managers = [ordered]@{ view = $true; create = $true; update = $true; delete = $true; export = $true }
        users    = [ordered]@{ view = $true; create = $true; update = $true; delete = $false; export = $false }
        guests   = [ordered]@{ view = $true; create = $false; update = $false; delete = $false; export = $false }
    }
}

# --- Zimmet Yonetimi (parent) ---
$zimmetParentResult = Upsert-MenuItem -AllItems $items -Label "Zimmet Yonetimi" -FindExisting {
    $_.pageCode -eq "dynamicForms.zimmet.menuParent"
} -Body @{
    order    = $baseOrder
    itemType = "item"
    level    = 1
    parentId = $dfHeaderId
    pageType = "user"
    pageCode = "dynamicForms.zimmet.menuParent"
    title    = "Zimmet Yönetimi"
    icon     = "DeviceLaptopIcon"
    iconType = "tabler"
    to       = "#"
    type     = "internal"
    disabled = $false
}

$zimmetParentId = $zimmetParentResult.id
if ([string]::IsNullOrEmpty($zimmetParentId)) {
    Write-Host "Zimmet parent id alinamadi; cikiliyor." -ForegroundColor Red
    exit 1
}

$menuEntries = @(
    @{
        Label    = "Demirbaslar"
        PageCode = "dynamicForms.zimmet.demirbaslar.menuTitle"
        To       = "/apps/automated-forms/view/zimmet-demirbaslar-form"
        Icon     = "BarcodeIcon"
        Order    = $baseOrder + 1
    },
    @{
        Label    = "Urun Katalogu"
        PageCode = "dynamicForms.zimmet.urunler.menuTitle"
        To       = "/apps/automated-forms/view/zimmet-urunler-form"
        Icon     = "PackageIcon"
        Order    = $baseOrder + 2
    },
    @{
        Label    = "Urun Gruplari"
        PageCode = "dynamicForms.zimmet.urunGruplari.menuTitle"
        To       = "/apps/automated-forms/view/zimmet-urun-gruplari-form"
        Icon     = "CategoryIcon"
        Order    = $baseOrder + 3
    },
    @{
        Label    = "Depolar"
        PageCode = "dynamicForms.zimmet.depolar.menuTitle"
        To       = "/apps/automated-forms/view/zimmet-depolar-form"
        Icon     = "BuildingWarehouseIcon"
        Order    = $baseOrder + 4
    },
    @{
        Label    = "Depo Lokasyonlari"
        PageCode = "dynamicForms.zimmet.lokasyonlar.menuTitle"
        To       = "/apps/automated-forms/view/zimmet-depo-lokasyonlari-form"
        Icon     = "MapPinIcon"
        Order    = $baseOrder + 5
    }
)

foreach ($entry in $menuEntries) {
    Upsert-MenuItem -AllItems $items -Label $entry.Label -FindExisting {
        $_.pageCode -eq $entry.PageCode -or $_.to -eq $entry.To
    } -Body @{
        order       = $entry.Order
        itemType    = "item"
        level       = 2
        parentId    = $zimmetParentId
        pageType    = "user"
        pageCode    = $entry.PageCode
        title       = $entry.Label
        icon        = $entry.Icon
        iconType    = "tabler"
        to          = $entry.To
        type        = "internal"
        disabled    = $false
        permissions = $defaultPerms
    } | Out-Null
}

Write-Host "`nTamamlandi. UI'da menu gormek icin sayfayi yenileyin veya cikis/giris yapin." -ForegroundColor Cyan
Write-Host "  Dinamik Formlar > Zimmet Yonetimi > ..." -ForegroundColor Gray
