# Odak Uretim master Automated Forms — @side_menu kayitlari
#
# Usage (repo kokunden):
#   .\docs\odak\operationcore\scripts\get-operationcore-token.ps1
#   .\docs\odak\dynamicforms\scripts\patch-odak-master-side-menu.ps1

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
    Authorization = "Bearer $token"
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

function Upsert-MenuItem {
    param(
        [array]$AllItems,
        [scriptblock]$FindExisting,
        [hashtable]$Body,
        [string]$Label
    )

    $existing = $AllItems | Where-Object $FindExisting | Select-Object -First 1
    $id = Get-ItemId $existing

    if (-not $existing) {
        Write-Host "POST $Label (order=$($Body.order))..." -ForegroundColor Yellow
        $created = Invoke-MenuPost -Body $Body
        $newId = Get-ItemId $created
        if ([string]::IsNullOrEmpty($newId) -and $Body.pageCode) {
            $refreshed = Invoke-RestMethod -Uri $listUri -Headers $headers -Method GET
            $refreshedItems = Get-MenuItems $refreshed
            $createdRow = $refreshedItems | Where-Object { $_.pageCode -eq $Body.pageCode } | Select-Object -First 1
            $newId = Get-ItemId $createdRow
            $created = $createdRow
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

$headerRow = $items | Where-Object {
    $_.itemType -eq "header" -and (
        $_.pageCode -eq "dynamicForms.menuHeader" -or
        ($_.header -and ($_.header -eq "Dinamik Formlar" -or $_.header -eq "Dynamic Forms"))
    )
} | Select-Object -First 1

$headerId = Get-ItemId $headerRow
if ([string]::IsNullOrEmpty($headerId)) {
    $headerOrder = [Math]::Max($maxOrder + 1, 230)
    Write-Host "Dinamik Formlar header bulunamadi; olusturuluyor..." -ForegroundColor Yellow
    $headerResult = Upsert-MenuItem -AllItems $items -Label "Dinamik Formlar header" -FindExisting {
        $false
    } -Body @{
        order     = $headerOrder
        itemType  = "header"
        level     = 0
        parentId  = $null
        pageType  = "manager"
        pageCode  = "dynamicForms.menuHeader"
        header    = "Dinamik Formlar"
        disabled  = $false
    }
    $headerId = $headerResult.id
}
else {
    Write-Host "Dinamik Formlar header: $headerId" -ForegroundColor Gray
}

if ([string]::IsNullOrEmpty($headerId)) {
    Write-Host "Header id alinamadi; cikiliyor." -ForegroundColor Red
    exit 1
}

$childOrders = @()
foreach ($row in $items) {
    if ("$($row.parentId)" -eq "$headerId" -and $null -ne $row.order) {
        $childOrders += [int]$row.order
    }
}
$nextOrder = if ($childOrders.Count -gt 0) { ($childOrders | Measure-Object -Maximum).Maximum + 1 } else { [Math]::Max($maxOrder + 1, 231) }

$defaultPerms = [ordered]@{
    groups = [ordered]@{
        admins = [ordered]@{ view = $true; create = $true; update = $true; delete = $true; export = $true }
        managers = [ordered]@{ view = $true; create = $true; update = $true; delete = $true; export = $true }
        users = [ordered]@{ view = $true; create = $true; update = $true; delete = $false; export = $false }
        guests = [ordered]@{ view = $true; create = $false; update = $false; delete = $false; export = $false }
    }
}

$menuEntries = @(
    @{
        Label    = "Musteriler"
        PageCode = "dynamicForms.odakMusteriler.menuTitle"
        To       = "/apps/automated-forms/view/odak-musteriler-form"
        Icon     = "BuildingStoreIcon"
        Order    = $nextOrder
    },
    @{
        Label    = "Urun Gruplari"
        PageCode = "dynamicForms.odakUrunGruplari.menuTitle"
        To       = "/apps/automated-forms/view/odak-urun-gruplari-form"
        Icon     = "CategoryIcon"
        Order    = $nextOrder + 1
    },
    @{
        Label    = "Urunler"
        PageCode = "dynamicForms.odakUrunler.menuTitle"
        To       = "/apps/automated-forms/view/odak-urunler-form"
        Icon     = "PackageIcon"
        Order    = $nextOrder + 2
    }
)

foreach ($entry in $menuEntries) {
    Upsert-MenuItem -AllItems $items -Label $entry.Label -FindExisting {
        $_.pageCode -eq $entry.PageCode -or $_.to -eq $entry.To
    } -Body @{
        order       = $entry.Order
        itemType    = "item"
        level       = 1
        parentId    = $headerId
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
Write-Host "  /apps/automated-forms/view/odak-musteriler-form" -ForegroundColor Gray
Write-Host "  /apps/automated-forms/view/odak-urun-gruplari-form" -ForegroundColor Gray
Write-Host "  /apps/automated-forms/view/odak-urunler-form" -ForegroundColor Gray
