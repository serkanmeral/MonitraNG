# Operation Core — @side_menu kaydi (Odak DG uzerinden)
# Header: Operasyon
#   - Operasyon Merkezi (user) → /apps/operation-core/workspace
#   - Bekleyen onaylar (manager) → /apps/operation-core/approvals
#   - Tanımlamalar (manager, parent)
#       - Sistem tanımlaması (manager) → /apps/operation-core/admin/definitions
#       - Workspace tanımlaması (manager) → /apps/operation-core/admin/workspace-definitions
#       - Zamanlanmış job'lar (manager) → /apps/operation-core/admin/scheduled-jobs
# Alarm sayfalari: patch-alarm-center-side-menu.ps1
# Usage (repo kokunden):
#   .\docs\odak\operationcore\scripts\get-operationcore-token.ps1
#   .\docs\odak\operationcore\scripts\patch-oc-side-menu.ps1

param(
    [string]$BaseUrl = "http://192.168.20.20:5040",
    [switch]$WhatIf = $false
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot

$loadTokenScript = Join-Path $scriptDir "load-operationcore-token.ps1"
if (-not (Test-Path $loadTokenScript)) {
    Write-Host "load-operationcore-token.ps1 bulunamadi." -ForegroundColor Red
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

# Eski duz "Yapılandırma" kaydini kaldir (level 1, tanimlamalar parent'i degil)
$legacyFlat = $items | Where-Object {
    $_.pageCode -eq "operationCore.definitions.menuTitle" -and
    [int]$_.level -eq 1 -and
    $_.to -eq "/apps/operation-core/admin/definitions"
} | Select-Object -First 1

if ($legacyFlat) {
    $legacyId = Get-ItemId $legacyFlat
    Write-Host "DELETE eski duz Yapilandirma kaydi (id=$legacyId)..." -ForegroundColor Yellow
    Invoke-MenuDelete -Id $legacyId
    $items = $items | Where-Object { (Get-ItemId $_) -ne $legacyId }
}

$maxOrder = 0
foreach ($row in $items) {
    if ($null -ne $row.order) {
        $o = [int]$row.order
        if ($o -gt $maxOrder) { $maxOrder = $o }
    }
}

$headerOrder = [Math]::Max($maxOrder + 1, 174)
$workspaceItemOrder = $headerOrder + 1
$approvalsOrder = $headerOrder + 2
$definitionsParentOrder = $headerOrder + 3
$systemDefinitionsOrder = $headerOrder + 4
$workspaceDefinitionsOrder = $headerOrder + 5
$scheduledJobsOrder = $headerOrder + 6

# --- Header ---
$headerResult = Upsert-MenuItem -AllItems $items -Label "Operasyon header" -FindExisting {
    $_.itemType -eq "header" -and (
        $_.pageCode -eq "operationCore.menuHeader" -or
        ($_.header -and ($_.header -eq "Operasyon" -or $_.header -eq "Operations"))
    )
} -Body @{
    order     = $headerOrder
    itemType  = "header"
    level     = 0
    parentId  = $null
    pageType  = "user"
    pageCode  = "operationCore.menuHeader"
    header    = "Operasyon"
    disabled  = $false
}

$headerId = $headerResult.id
if ([string]::IsNullOrEmpty($headerId)) {
    Write-Host "Header id alinamadi; cikiliyor." -ForegroundColor Red
    exit 1
}

# --- Operasyon Merkezi ---
Upsert-MenuItem -AllItems $items -Label "Operasyon Merkezi" -FindExisting {
    $_.pageCode -eq "operationCore.menuTitle" -or $_.to -eq "/apps/operation-core/workspace"
} -Body @{
    order     = $workspaceItemOrder
    itemType  = "item"
    level     = 1
    parentId  = $headerId
    pageType  = "user"
    pageCode  = "operationCore.menuTitle"
    title     = "Operasyon Merkezi"
    icon      = "ClipboardIcon"
    iconType  = "tabler"
    to        = "/apps/operation-core/workspace"
    type      = "internal"
    disabled  = $false
} | Out-Null

# --- Bekleyen onaylar (workflow approval.wait — operasyon inbox) ---
Upsert-MenuItem -AllItems $items -Label "Bekleyen onaylar" -FindExisting {
    $_.pageCode -eq "operationCore.adminApprovals.menuTitle" -or
    $_.to -eq "/apps/operation-core/approvals" -or
    $_.to -eq "/apps/operation-core/admin/approvals"
} -Body @{
    order     = $approvalsOrder
    itemType  = "item"
    level     = 1
    parentId  = $headerId
    pageType  = "manager"
    pageCode  = "operationCore.adminApprovals.menuTitle"
    title     = "Bekleyen onaylar"
    icon      = "FileCheckIcon"
    iconType  = "tabler"
    to        = "/apps/operation-core/approvals"
    type      = "internal"
    disabled  = $false
} | Out-Null

# --- Tanımlamalar (parent) ---
$definitionsParentResult = Upsert-MenuItem -AllItems $items -Label "Tanimlamalar" -FindExisting {
    $_.pageCode -eq "operationCore.definitions.menuParent"
} -Body @{
    order     = $definitionsParentOrder
    itemType  = "item"
    level     = 1
    parentId  = $headerId
    pageType  = "manager"
    pageCode  = "operationCore.definitions.menuParent"
    title     = "Tanımlamalar"
    icon      = "SettingsIcon"
    iconType  = "tabler"
    to        = "#"
    type      = "internal"
    disabled  = $false
}

$definitionsParentId = $definitionsParentResult.id
if ([string]::IsNullOrEmpty($definitionsParentId)) {
    Write-Host "Tanimlamalar parent id alinamadi; cikiliyor." -ForegroundColor Red
    exit 1
}

# --- Sistem tanımlaması ---
Upsert-MenuItem -AllItems $items -Label "Sistem tanimlamasi" -FindExisting {
    $_.pageCode -eq "operationCore.definitions.systemMenuTitle" -or
    ($_.to -eq "/apps/operation-core/admin/definitions" -and [int]$_.level -eq 2)
} -Body @{
    order     = $systemDefinitionsOrder
    itemType  = "item"
    level     = 2
    parentId  = $definitionsParentId
    pageType  = "manager"
    pageCode  = "operationCore.definitions.systemMenuTitle"
    title     = "Sistem tanımlaması"
    icon      = "AdjustmentsHorizontalIcon"
    iconType  = "tabler"
    to        = "/apps/operation-core/admin/definitions"
    type      = "internal"
    disabled  = $false
} | Out-Null

# --- Workspace tanımlaması ---
Upsert-MenuItem -AllItems $items -Label "Workspace tanimlamasi" -FindExisting {
    $_.pageCode -eq "operationCore.definitions.workspaceMenuTitle" -or
    $_.to -eq "/apps/operation-core/admin/workspace-definitions"
} -Body @{
    order     = $workspaceDefinitionsOrder
    itemType  = "item"
    level     = 2
    parentId  = $definitionsParentId
    pageType  = "manager"
    pageCode  = "operationCore.definitions.workspaceMenuTitle"
    title     = "Workspace tanımlaması"
    icon      = "LayoutIcon"
    iconType  = "tabler"
    to        = "/apps/operation-core/admin/workspace-definitions"
    type      = "internal"
    disabled  = $false
} | Out-Null

# --- Zamanlanmış job'lar (SW-6 admin explorer) ---
Upsert-MenuItem -AllItems $items -Label "Zamanlanmis joblar" -FindExisting {
    $_.pageCode -eq "operationCore.adminScheduledJobs.menuTitle" -or
    $_.to -eq "/apps/operation-core/admin/scheduled-jobs"
} -Body @{
    order     = $scheduledJobsOrder
    itemType  = "item"
    level     = 2
    parentId  = $definitionsParentId
    pageType  = "manager"
    pageCode  = "operationCore.adminScheduledJobs.menuTitle"
    title     = "Zamanlanmış job'lar"
    icon      = "CalendarIcon"
    iconType  = "tabler"
    to        = "/apps/operation-core/admin/scheduled-jobs"
    type      = "internal"
    disabled  = $false
} | Out-Null

# --- Eski OC is akislari kaydini kaldir (Otomasyon Merkezi'ne tasindi) ---
$legacyWorkflowItems = $items | Where-Object {
    $_.pageCode -eq "operationCore.adminWorkflows.menuTitle" -or
    $_.to -eq "/apps/operation-core/admin/workflows"
}
foreach ($legacyRow in $legacyWorkflowItems) {
    $legacyId = Get-ItemId $legacyRow
    if (-not [string]::IsNullOrEmpty($legacyId)) {
        Write-Host "DELETE eski OC is akislari kaydi (id=$legacyId)..." -ForegroundColor Yellow
        Invoke-MenuDelete -Id $legacyId
    }
}

Write-Host "`nTamamlandi. UI'da menu gormek icin sayfayi yenileyin veya cikis/giris yapin." -ForegroundColor Cyan
