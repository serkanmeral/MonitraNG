# Alarm Merkezi — @side_menu kaydi (Odak DG uzerinden)
# Header: Alarm Merkezi
#   - Açık alarmlar (manager) → /apps/alarm-center/alarms
#   - Alarm kuralları (manager) → /apps/alarm-center/rules
# Usage (repo kokunden):
#   .\docs\odak\operationcore\scripts\get-operationcore-token.ps1
#   .\docs\odak\alarm\scripts\patch-alarm-center-side-menu.ps1

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

# Eski OC altindaki alarm kayitlarini kaldir
$legacyAlarmItems = $items | Where-Object {
    $_.pageCode -eq "operationCore.adminAlarms.menuTitle" -or
    $_.pageCode -eq "operationCore.adminAlarmRules.menuTitle" -or
    $_.to -eq "/apps/operation-core/admin/alarms" -or
    $_.to -eq "/apps/operation-core/admin/alarm-rules"
}
foreach ($legacyRow in $legacyAlarmItems) {
    $legacyId = Get-ItemId $legacyRow
    if (-not [string]::IsNullOrEmpty($legacyId)) {
        Write-Host "DELETE eski OC alarm kaydi (id=$legacyId, to=$($legacyRow.to))..." -ForegroundColor Yellow
        Invoke-MenuDelete -Id $legacyId
        $items = $items | Where-Object { (Get-ItemId $_) -ne $legacyId }
    }
}

$maxOrder = 0
foreach ($row in $items) {
    if ($null -ne $row.order) {
        $o = [int]$row.order
        if ($o -gt $maxOrder) { $maxOrder = $o }
    }
}

$headerOrder = [Math]::Max($maxOrder + 1, 221)
$alarmsItemOrder = $headerOrder + 1
$rulesItemOrder = $headerOrder + 2
$policiesItemOrder = $headerOrder + 3

# --- Header: Alarm Merkezi ---
$headerResult = Upsert-MenuItem -AllItems $items -Label "Alarm Merkezi header" -FindExisting {
    $_.itemType -eq "header" -and (
        $_.pageCode -eq "alarmCenter.menuHeader" -or
        ($_.header -and ($_.header -eq "Alarm Merkezi" -or $_.header -eq "Alarm Center"))
    )
} -Body @{
    order     = $headerOrder
    itemType  = "header"
    level     = 0
    parentId  = $null
    pageType  = "manager"
    pageCode  = "alarmCenter.menuHeader"
    header    = "Alarm Merkezi"
    disabled  = $false
}

$headerId = $headerResult.id
if ([string]::IsNullOrEmpty($headerId)) {
    Write-Host "Header id alinamadi; cikiliyor." -ForegroundColor Red
    exit 1
}

# --- Açık alarmlar ---
Upsert-MenuItem -AllItems $items -Label "Acik alarmlar" -FindExisting {
    $_.pageCode -eq "alarmCenter.alarms.menuTitle" -or
    $_.to -eq "/apps/alarm-center/alarms"
} -Body @{
    order     = $alarmsItemOrder
    itemType  = "item"
    level     = 1
    parentId  = $headerId
    pageType  = "manager"
    pageCode  = "alarmCenter.alarms.menuTitle"
    title     = "Açık alarmlar"
    icon      = "AlertCircleIcon"
    iconType  = "tabler"
    to        = "/apps/alarm-center/alarms"
    type      = "internal"
    disabled  = $false
} | Out-Null

# --- Alarm kuralları ---
Upsert-MenuItem -AllItems $items -Label "Alarm kurallari" -FindExisting {
    $_.pageCode -eq "alarmCenter.rules.menuTitle" -or
    $_.to -eq "/apps/alarm-center/rules"
} -Body @{
    order     = $rulesItemOrder
    itemType  = "item"
    level     = 1
    parentId  = $headerId
    pageType  = "manager"
    pageCode  = "alarmCenter.rules.menuTitle"
    title     = "Alarm kuralları"
    icon      = "AdjustmentsIcon"
    iconType  = "tabler"
    to        = "/apps/alarm-center/rules"
    type      = "internal"
    disabled  = $false
} | Out-Null

# --- Bildirim politikaları ---
Upsert-MenuItem -AllItems $items -Label "Bildirim politikalari" -FindExisting {
    $_.pageCode -eq "alarmCenter.notificationPolicies.menuTitle" -or
    $_.to -eq "/apps/alarm-center/notification-policies"
} -Body @{
    order     = $policiesItemOrder
    itemType  = "item"
    level     = 1
    parentId  = $headerId
    pageType  = "manager"
    pageCode  = "alarmCenter.notificationPolicies.menuTitle"
    title     = "Bildirim politikaları"
    icon      = "BellRingIcon"
    iconType  = "tabler"
    to        = "/apps/alarm-center/notification-policies"
    type      = "internal"
    disabled  = $false
} | Out-Null

Write-Host "`nTamamlandi. UI'da menu gormek icin sayfayi yenileyin veya cikis/giris yapin." -ForegroundColor Cyan
