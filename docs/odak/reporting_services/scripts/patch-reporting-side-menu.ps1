# Reporting — @side_menu kaydi (Odak DG uzerinden)
# Header: Raporlama
#   - Rapor tasarımcısı (user) → /apps/reporting
#
# Usage (repo kokunden):
#   .\docs\odak\operationcore\scripts\get-operationcore-token.ps1
#   .\docs\odak\reporting_services\scripts\patch-reporting-side-menu.ps1
#   .\docs\odak\reporting_services\scripts\patch-reporting-side-menu.ps1 -WhatIf

param(
    [string]$BaseUrl = "http://192.168.20.20:5040",
    [switch]$WhatIf = $false
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot

$token = $env:DI_TOKEN
if ([string]::IsNullOrEmpty($token)) {
    $loadTokenScript = Join-Path $scriptDir "..\..\operationcore\scripts\load-operationcore-token.ps1"
    if (Test-Path $loadTokenScript) {
        $token = & $loadTokenScript
    }
}
if ([string]::IsNullOrEmpty($token)) {
    Write-Host "Token alinamadi. Once get-operationcore-token.ps1 calistirin ya da `$env:DI_TOKEN set edin." -ForegroundColor Red
    exit 1
}

$headers = @{
    Authorization  = "Bearer $token"
    "Content-Type" = "application/json"
}

$dataPath = "/data/api/v1/data/@side_menu"
$listUri = "$BaseUrl$dataPath`?limit=10000&sort=order:asc"

function Get-RowValue($row, [string]$Key) {
    if ($null -eq $row) { return $null }
    if ($row -is [System.Collections.IDictionary]) {
        foreach ($candidate in @($Key, $Key.ToLowerInvariant(), $Key.ToUpperInvariant())) {
            if ($row.ContainsKey($candidate) -and $null -ne $row[$candidate]) {
                return $row[$candidate]
            }
        }
        return $null
    }
    $prop = $row.PSObject.Properties[$Key]
    if ($null -ne $prop) { return $prop.Value }
    return $null
}

function Get-ItemId($row) {
    foreach ($key in @("__dataId", "dataId", "DataId", "id")) {
        $val = Get-RowValue $row $key
        if (-not [string]::IsNullOrEmpty([string]$val)) { return [string]$val }
    }
    return $null
}

function ConvertTo-MenuRow($raw) {
    if ($null -eq $raw) { return $null }
    if ($raw -is [System.Collections.IDictionary]) {
        return [pscustomobject]@{
            __dataId = Get-RowValue $raw "__dataId"
            order    = Get-RowValue $raw "order"
            itemType = Get-RowValue $raw "itemType"
            pageType = Get-RowValue $raw "pageType"
            pageCode = Get-RowValue $raw "pageCode"
            header   = Get-RowValue $raw "header"
            title    = Get-RowValue $raw "title"
            icon     = Get-RowValue $raw "icon"
            iconType = Get-RowValue $raw "iconType"
            to       = Get-RowValue $raw "to"
            type     = Get-RowValue $raw "type"
            parentId = Get-RowValue $raw "parentId"
            level    = Get-RowValue $raw "level"
            disabled = Get-RowValue $raw "disabled"
        }
    }
    return $raw
}

function Get-SideMenuItems {
    $response = Invoke-WebRequest -Uri $listUri -Headers $headers -Method GET
    $parsed = $response.Content | ConvertFrom-Json -AsHashtable
    if ($null -eq $parsed) { return @() }

    $rawRows = @()
    if ($parsed -is [System.Collections.IDictionary] -and $parsed.ContainsKey("items")) {
        $rawRows = @($parsed["items"])
    }
    elseif ($parsed -is [System.Collections.IEnumerable] -and -not ($parsed -is [string])) {
        $rawRows = @($parsed)
    }
    else {
        $rawRows = @($parsed)
    }

    return @($rawRows | ForEach-Object { ConvertTo-MenuRow $_ } | Where-Object { $null -ne $_ })
}

Write-Host "Side menu listeleniyor ($BaseUrl)..." -ForegroundColor Cyan
$items = Get-SideMenuItems
Write-Host "  $($items.Count) kayit" -ForegroundColor Gray

function Invoke-MenuPut {
    param([string]$Id, [hashtable]$Body)
    if ($WhatIf) {
        Write-Host "WhatIf PUT $Id -> $($Body | ConvertTo-Json -Compress)" -ForegroundColor Yellow
        return
    }
    $json = $Body | ConvertTo-Json -Compress
    Invoke-RestMethod -Uri "$BaseUrl$dataPath/$Id" -Headers $headers -Method PUT -Body $json | Out-Null
}

function Invoke-MenuPost {
    param([hashtable]$Body)
    if ($WhatIf) {
        Write-Host "WhatIf POST -> $($Body | ConvertTo-Json -Compress)" -ForegroundColor Yellow
        return $null
    }
    $json = $Body | ConvertTo-Json -Compress
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
            $refreshedItems = Get-SideMenuItems
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

$existingHeader = $items | Where-Object {
    $_.itemType -eq "header" -and (
        $_.pageCode -eq "reporting.menuHeader" -or
        ($_.header -and ($_.header -eq "Raporlama" -or $_.header -eq "Reporting"))
    )
} | Select-Object -First 1

$headerOrder = if ($null -ne $existingHeader -and $null -ne $existingHeader.order) {
    [int]$existingHeader.order
} else {
    $maxOrder + 1
}
$designerItemOrder = $headerOrder + 1

$headerResult = Upsert-MenuItem -AllItems $items -Label "Raporlama header" -FindExisting {
    $_.itemType -eq "header" -and (
        $_.pageCode -eq "reporting.menuHeader" -or
        ($_.header -and ($_.header -eq "Raporlama" -or $_.header -eq "Reporting"))
    )
} -Body @{
    order    = $headerOrder
    itemType = "header"
    level    = 0
    parentId = $null
    pageType = "user"
    pageCode = "reporting.menuHeader"
    header   = "Raporlama"
    disabled = $false
}

$headerId = $headerResult.id
if ([string]::IsNullOrEmpty($headerId)) {
    Write-Host "Header id alinamadi; cikiliyor." -ForegroundColor Red
    exit 1
}

$items = Get-SideMenuItems

Upsert-MenuItem -AllItems $items -Label "Rapor tasarimcisi" -FindExisting {
    $_.pageCode -eq "reporting.menuTitle" -or
    $_.pageCode -eq "reporting.designer.menuTitle" -or
    $_.to -eq "/apps/reporting"
} -Body @{
    order    = $designerItemOrder
    itemType = "item"
    level    = 1
    parentId = $headerId
    pageType = "user"
    pageCode = "reporting.menuTitle"
    title    = "Rapor tasarımcısı"
    icon     = "ChartBarIcon"
    iconType = "tabler"
    to       = "/apps/reporting"
    type     = "internal"
    disabled = $false
} | Out-Null

Write-Host "`nTamamlandi. UI'da menu gormek icin sayfayi yenileyin veya cikis/giris yapin." -ForegroundColor Cyan
Write-Host "Not: Sayfa lokal npm run dev ile acilir; sunucu mngui deploy edilmediyse menü test UI'da görünür ama route lokalde olmalı." -ForegroundColor Gray
