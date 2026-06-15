# Odak Siparis hub — @side_menu kayitlari
#
# Usage (repo kokunden):
#   .\docs\odak\operationcore\scripts\get-operationcore-token.ps1
#   .\docs\odak\siparis\scripts\patch-odak-siparis-side-menu.ps1

param(
    [string]$BaseUrl = "http://192.168.20.20:5040",
    [switch]$WhatIf = $false
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
$ocScriptDir = Join-Path (Split-Path (Split-Path $scriptDir -Parent) -Parent) "operationcore\scripts"
$loadTokenScript = Join-Path $ocScriptDir "load-operationcore-token.ps1"

if (-not (Test-Path $loadTokenScript)) { throw "Token script yok: $loadTokenScript" }

$token = & $loadTokenScript
if ([string]::IsNullOrEmpty($token)) { throw "Token alinamadi." }

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

function Invoke-MenuPut {
    param([string]$Id, [hashtable]$Body)
    if ($WhatIf) { Write-Host "WhatIf PUT $Id" -ForegroundColor Yellow; return }
    $json = $Body | ConvertTo-Json -Depth 12 -Compress
    Invoke-RestMethod -Uri "$BaseUrl$dataPath/$Id" -Headers $headers -Method PUT -Body $json | Out-Null
}

function Invoke-MenuPost {
    param([hashtable]$Body)
    if ($WhatIf) { Write-Host "WhatIf POST" -ForegroundColor Yellow; return $null }
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
        Write-Host "POST $Label..." -ForegroundColor Yellow
        $created = Invoke-MenuPost -Body $Body
        $newId = Get-ItemId $created
        if ([string]::IsNullOrEmpty($newId)) {
            $refreshed = Invoke-RestMethod -Uri $listUri -Headers $headers -Method GET
            $row = @(Get-MenuItems $refreshed) | Where-Object { $_.pageCode -eq $Body.pageCode } | Select-Object -First 1
            $newId = Get-ItemId $row
        }
        Write-Host "  OK $Label -> $newId" -ForegroundColor Green
        return $newId
    }
    Write-Host "PUT/SKIP $Label ($id)..." -ForegroundColor Yellow
    Invoke-MenuPut -Id $id -Body $Body
    Write-Host "  SYNC $Label" -ForegroundColor Green
    return $id
}

$maxOrder = ($items | ForEach-Object { [int]$_.order } | Measure-Object -Maximum).Maximum
if (-not $maxOrder) { $maxOrder = 300 }

$defaultPerms = [ordered]@{
    groups = [ordered]@{
        admins = [ordered]@{ view = $true; create = $true; update = $true; delete = $true; export = $true }
        managers = [ordered]@{ view = $true; create = $true; update = $true; delete = $true; export = $true }
        users = [ordered]@{ view = $true; create = $true; update = $true; delete = $false; export = $false }
        guests = [ordered]@{ view = $true; create = $false; update = $false; delete = $false; export = $false }
    }
}

$headerId = Upsert-MenuItem -AllItems $items -Label "Odak Siparis header" -FindExisting {
    $_.pageCode -eq "odakSiparis.menuHeader"
} -Body @{
    order    = ($maxOrder + 1)
    itemType = "header"
    level    = 0
    parentId = $null
    pageType = "manager"
    pageCode = "odakSiparis.menuHeader"
    header   = "Odak Sipariş"
    disabled = $false
}

Upsert-MenuItem -AllItems $items -Label "Is Paketleri" -FindExisting {
    $_.pageCode -eq "odakSiparis.packages.menuTitle" -or $_.to -eq "/apps/odak-siparis/packages"
} -Body @{
    order       = ($maxOrder + 2)
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

Write-Host "`nTamamlandi -> /apps/odak-siparis/packages" -ForegroundColor Cyan
