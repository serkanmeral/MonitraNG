# Güvenlik Yönetimi — @side_menu kaydi (Odak DG uzerinden)
# Header: Güvenlik Yönetimi
#   - Alarm Merkezi → /apps/alarm-center/alarms
#   - SIEM Güvenlik Paneli → /apps/siem-center
# Not: Alarm kurallari ve güvenlik olaylari menude degil; sayfa icinden erisilir.
# Usage (repo kokunden):
#   .\docs\odak\operationcore\scripts\get-operationcore-token.ps1
#   .\docs\odak\monitoring\scripts\patch-siem-center-side-menu.ps1

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

function Invoke-MenuDelete {
    param([string]$Id)
    if ($WhatIf) {
        Write-Host "WhatIf DELETE $Id" -ForegroundColor Yellow
        return
    }
    Invoke-RestMethod -Uri "$BaseUrl$dataPath/$Id" -Headers $headers -Method DELETE | Out-Null
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

$automationHeader = $items | Where-Object {
    $_.itemType -eq "header" -and $_.pageCode -eq "automationCenter.menuHeader"
} | Select-Object -First 1

$legacyAlarmHeader = $items | Where-Object {
    $_.itemType -eq "header" -and $_.pageCode -eq "alarmCenter.menuHeader"
} | Select-Object -First 1

$maxOrder = 0
foreach ($row in $items) {
    if ($null -ne $row.order) {
        $o = [int]$row.order
        if ($o -gt $maxOrder) { $maxOrder = $o }
    }
}

$headerOrder = $null
if ($legacyAlarmHeader -and $automationHeader) {
    $headerOrder = [int]$legacyAlarmHeader.order
}
elseif ($legacyAlarmHeader) {
    $headerOrder = [int]$legacyAlarmHeader.order
}
if ($null -eq $headerOrder) {
    $headerOrder = [Math]::Max($maxOrder + 1, 224)
}
$alarmItemOrder = $headerOrder + 1
$siemItemOrder = $headerOrder + 2

# --- Header: Güvenlik Yönetimi ---
$headerResult = Upsert-MenuItem -AllItems $items -Label "Güvenlik Yönetimi header" -FindExisting {
    $_.itemType -eq "header" -and (
        $_.pageCode -eq "securityManagement.menuHeader" -or
        $_.pageCode -eq "siemCenter.menuHeader" -or
        $_.pageCode -eq "alarmCenter.menuHeader" -or
        ($_.header -and (
            $_.header -eq "Güvenlik Yönetimi" -or
            $_.header -eq "Güvenlik Merkezi" -or
            $_.header -eq "Alarm Merkezi" -or
            $_.header -eq "Security Management" -or
            $_.header -eq "Security Center" -or
            $_.header -eq "SIEM"
        ))
    )
} -Body @{
    order     = $headerOrder
    itemType  = "header"
    level     = 0
    parentId  = $null
    pageType  = "manager"
    pageCode  = "securityManagement.menuHeader"
    header    = "Güvenlik Yönetimi"
    disabled  = $false
}

$headerId = $headerResult.id
if ([string]::IsNullOrEmpty($headerId)) {
    Write-Host "Header id alinamadi; cikiliyor." -ForegroundColor Red
    exit 1
}

# --- Alarm Merkezi ---
Upsert-MenuItem -AllItems $items -Label "Alarm Merkezi" -FindExisting {
    $_.pageCode -eq "alarmCenter.menuTitle" -or
    $_.pageCode -eq "alarmCenter.alarms.menuTitle" -or
    $_.to -eq "/apps/alarm-center/alarms"
} -Body @{
    order     = $alarmItemOrder
    itemType  = "item"
    level     = 1
    parentId  = $headerId
    pageType  = "manager"
    pageCode  = "alarmCenter.menuTitle"
    title     = "Alarm Merkezi"
    icon      = "AlertCircleIcon"
    iconType  = "tabler"
    to        = "/apps/alarm-center/alarms"
    type      = "internal"
    disabled  = $false
} | Out-Null

# --- SIEM Güvenlik Paneli ---
Upsert-MenuItem -AllItems $items -Label "SIEM Güvenlik Paneli" -FindExisting {
    $_.pageCode -eq "siemCenter.dashboard.menuTitle" -or
    $_.to -eq "/apps/siem-center"
} -Body @{
    order     = $siemItemOrder
    itemType  = "item"
    level     = 1
    parentId  = $headerId
    pageType  = "manager"
    pageCode  = "siemCenter.dashboard.menuTitle"
    title     = "SIEM Güvenlik Paneli"
    icon      = "ShieldIcon"
    iconType  = "tabler"
    to        = "/apps/siem-center"
    type      = "internal"
    disabled  = $false
} | Out-Null

# --- Kaldir: eski alarm header, kurallar, olaylar menu ---
$removeRows = $items | Where-Object {
    ($_.itemType -eq "header" -and $_.pageCode -eq "alarmCenter.menuHeader" -and (Get-ItemId $_) -ne $headerId) -or
    $_.pageCode -eq "alarmCenter.rules.menuTitle" -or
    $_.to -eq "/apps/alarm-center/rules" -or
    $_.pageCode -eq "siemCenter.events.menuTitle" -or
    $_.to -eq "/apps/siem-center/events"
}
foreach ($row in $removeRows) {
    $rowId = Get-ItemId $row
    if ([string]::IsNullOrEmpty($rowId)) { continue }
    if ($rowId -eq $headerId) { continue }
    $label = if ($row.title) { $row.title } elseif ($row.header) { $row.header } else { $row.pageCode }
    Write-Host "DELETE eski menu: $label (id=$rowId)..." -ForegroundColor Yellow
    Invoke-MenuDelete -Id $rowId
    Write-Host "  OK kaldirildi" -ForegroundColor Green
}

Write-Host "`nTamamlandi. UI'da menu gormek icin sayfayi yenileyin veya cikis/giris yapin." -ForegroundColor Cyan
