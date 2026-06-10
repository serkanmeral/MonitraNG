# Odak DG — dataset + kategori envanteri
# Usage:
#   pwsh -File docs/odak/dataset_fixing/scripts/audit-datasets-odak.ps1
#   pwsh -File ... -UpdateInventoryMarkdown   # INVENTORY.md tablolarini gunceller
#
# Token: docs/odak/operationcore/scripts/load-operationcore-token.ps1

param(
    [string]$BaseUrl = "http://192.168.20.20:5040",
    [switch]$UseGateway,
    [int]$PageSize = 100,
    [switch]$UpdateInventoryMarkdown,
    [string]$OutputDir = ""
)

$ErrorActionPreference = "Stop"

# Gateway uzerinden DG (Odak varsayilan)
if (-not $PSBoundParameters.ContainsKey('UseGateway')) {
    $UseGateway = $true
}

$scriptDir = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $PSCommandPath }
$repoRoot = (Resolve-Path (Join-Path $scriptDir "../../../..")).Path
$loadTokenScript = Join-Path $repoRoot "docs/odak/operationcore/scripts/load-operationcore-token.ps1"

if (-not (Test-Path $loadTokenScript)) {
    Write-Host "Token script bulunamadi: $loadTokenScript" -ForegroundColor Red
    exit 1
}

$token = & $loadTokenScript
if ([string]::IsNullOrWhiteSpace($token)) {
    Write-Host "Token alinamadi." -ForegroundColor Red
    exit 1
}
$token = $token.Trim()
$script:Bearer = $token

$datasetsPath   = if ($UseGateway) { "/data/api/v1/datasets" } else { "/api/v1/datasets" }
$categoriesPath = if ($UseGateway) { "/data/api/v1/dataset-categories" } else { "/api/v1/dataset-categories" }
$dataPath       = if ($UseGateway) { "/data/api/v1/data" } else { "/api/v1/data" }

if ([string]::IsNullOrWhiteSpace($OutputDir)) {
    $OutputDir = Join-Path $scriptDir "../reports"
}
New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

[System.Net.ServicePointManager]::ServerCertificateValidationCallback = { $true }

function Invoke-OdakGet {
    param([string]$RelativePath)
    $uri = "$($BaseUrl.TrimEnd('/'))$RelativePath"
    try {
        $params = @{
            Uri             = $uri
            Headers         = @{ Authorization = "Bearer $script:Bearer"; Accept = "application/json" }
            UseBasicParsing = $true
        }
        if ($PSVersionTable.PSVersion.Major -ge 6) { $params['SkipCertificateCheck'] = $true }
        $r = Invoke-WebRequest @params
        if (-not $r.Content) { return $null }
        return $r.Content | ConvertFrom-Json
    } catch {
        $msg = $_.Exception.Message
        Write-Host "  GET fail: $RelativePath — $msg" -ForegroundColor Yellow
        return $null
    }
}

function Get-ListItems {
    param($obj)
    if ($null -eq $obj) { return @() }
    foreach ($p in @('items', 'Items', 'data', 'Data')) {
        if ($null -ne $obj.$p) { return @($obj.$p) }
    }
    return @()
}

function Get-Prop {
    param($obj, [string[]]$Names)
    if ($null -eq $obj) { return $null }
    foreach ($n in $Names) {
        $v = $null
        if ($obj -is [System.Collections.IDictionary]) {
            if ($obj.Contains($n)) { $v = $obj[$n] }
        } else {
            $p = $obj.PSObject.Properties[$n]
            if ($null -ne $p) { $v = $p.Value }
        }
        if ($null -ne $v -and "$v" -ne '') { return $v }
    }
    return $null
}

function Get-AllPaged {
    param(
        [string]$ListPath,
        [hashtable]$ExtraQuery = @{}
    )
    $all = [System.Collections.Generic.List[object]]::new()
    $page = 1
    $total = $null
    do {
        $qs = @("pageNumber=$page", "pageSize=$PageSize")
        foreach ($k in $ExtraQuery.Keys) { $qs += "$k=$($ExtraQuery[$k])" }
        $path = '{0}?{1}' -f $ListPath, ($qs -join '&')
        $resp = Invoke-OdakGet -RelativePath $path
        if ($null -eq $resp) { break }
        $items = Get-ListItems $resp
        foreach ($i in $items) { [void]$all.Add($i) }
        $total = Get-Prop $resp @('totalCount', 'TotalCount')
        if ($items.Count -lt $PageSize) { break }
        $page++
    } while ($page -le 50)
    return @{ Items = $all; TotalCount = $total }
}

function Get-DataRecords {
    param([string]$DatasetName, [int]$Limit = 10000)
    $encoded = [Uri]::EscapeDataString($DatasetName)
    $path = '{0}/{1}?limit={2}' -f $dataPath, $encoded, $Limit
    $resp = Invoke-OdakGet -RelativePath $path
    if ($null -eq $resp) { return @() }
    if ($resp -is [System.Array]) { return @($resp) }
    $items = Get-ListItems $resp
    if ($items.Count -gt 0) { return $items }
    if ($resp -is [System.Collections.IEnumerable] -and $resp -isnot [string]) {
        return @($resp)
    }
    return @($resp)
}

function Get-SuggestedType {
    param(
        [string]$Name,
        [bool]$IsSystemCategory,
        [string[]]$AfForms,
        [bool]$HasAtPrefix
    )
    $n = $Name.ToLowerInvariant()
    $platformCore = @('@side_menu', '@automated_forms', '@datasets')
    if ($platformCore -contains $n) { return 'A' }

    if ($n -match '^op_' -or $n -match '^@widget' -or $n -match '^@dashboard' -or
        $n -match '^@mail_' -or $n -match '^@notification' -or $n -match '^@user_' -or
        $n -match '^cht_' -or $n -match '^@wf_' -or $n -match '^wf_') {
        return 'B'
    }

    if ($n -match '^@(books|test|tst_|demo)' -or $n -match '^tst_') { return 'E' }

    if ($AfForms.Count -gt 0) { return 'C' }

    if ($HasAtPrefix) { return 'B' }

    return 'D'
}

function Get-SuggestedAction {
    param(
        [string]$SuggestedType,
        [bool]$IsSystemCategory,
        [string]$CategoryName
    )
    if ($SuggestedType -in @('A', 'B') -and -not $IsSystemCategory) {
        return 'move-category / mark-system-category'
    }
    if ($SuggestedType -eq 'E') { return 'review-delete' }
    if ($null -eq $CategoryName -or $CategoryName -eq '') { return 'assign-category' }
    return 'keep'
}

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Odak Dataset Envanteri" -ForegroundColor Cyan
Write-Host "BaseUrl: $BaseUrl  Gateway: $UseGateway" -ForegroundColor Gray
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

Write-Host "Kategoriler aliniyor..." -ForegroundColor Cyan
$catResult = Get-AllPaged -ListPath $categoriesPath
$categories = @($catResult.Items)
$catById = @{}
foreach ($c in $categories) {
    $id = Get-Prop $c @('dataId', 'DataId', '__dataId')
    if ($id) {
        $catById[$id] = @{
            dataId            = [string]$id
            categoryName      = [string](Get-Prop $c @('categoryName', 'CategoryName'))
            isSystemCategory  = [bool](Get-Prop $c @('isSystemCategory', 'IsSystemCategory'))
            categoryDescription = [string](Get-Prop $c @('categoryDescription', 'CategoryDescription'))
        }
    }
}
Write-Host "  $($categories.Count) kategori" -ForegroundColor Green

Write-Host "Dataset'ler aliniyor..." -ForegroundColor Cyan
$dsResult = Get-AllPaged -ListPath $datasetsPath
$datasets = @($dsResult.Items)
Write-Host "  $($datasets.Count) dataset" -ForegroundColor Green

Write-Host "@automated_forms kayitlari aliniyor..." -ForegroundColor Cyan
$dsAutomatedForms = '@automated_forms'
$dsSideMenu = '@side_menu'
$afRecords = @(Get-DataRecords -DatasetName $dsAutomatedForms)
$afByDataset = @{}
foreach ($af in $afRecords) {
    $dsName = Get-Prop $af @('datasetName', 'DatasetName')
    $formCode = Get-Prop $af @('formCode', 'FormCode')
    if (-not $dsName) { continue }
    if (-not $afByDataset.ContainsKey($dsName)) { $afByDataset[$dsName] = [System.Collections.Generic.List[string]]::new() }
    if ($formCode) { [void]$afByDataset[$dsName].Add([string]$formCode) }
}
Write-Host "  $($afRecords.Count) AF kaydi, $($afByDataset.Keys.Count) bagli dataset" -ForegroundColor Green

Write-Host "@side_menu kayitlari aliniyor..." -ForegroundColor Cyan
$menuRecords = @(Get-DataRecords -DatasetName $dsSideMenu)
$menuAfRoutes = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($m in $menuRecords) {
    $route = Get-Prop $m @('route', 'Route', 'path', 'Path')
    if ($route -and "$route" -match 'automated-forms/view/([^/?#]+)') {
        [void]$menuAfRoutes.Add($Matches[1])
    }
}
Write-Host "  $($menuRecords.Count) menu kaydi" -ForegroundColor Green

$formCodeToDataset = @{}
foreach ($af in $afRecords) {
    $fc = Get-Prop $af @('formCode', 'FormCode')
    $dn = Get-Prop $af @('datasetName', 'DatasetName')
    if ($fc -and $dn) { $formCodeToDataset[[string]$fc] = [string]$dn }
}

$sideMenuDatasets = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($fc in $menuAfRoutes) {
    if ($formCodeToDataset.ContainsKey($fc)) {
        [void]$sideMenuDatasets.Add($formCodeToDataset[$fc])
    }
}

$rows = [System.Collections.Generic.List[object]]::new()
foreach ($ds in $datasets) {
    $name = [string](Get-Prop $ds @('name', 'Name'))
    $catId = Get-Prop $ds @('category', 'Category')
    $catInfo = if ($catId -and $catById.ContainsKey([string]$catId)) { $catById[[string]$catId] } else { $null }
    $catName = if ($catInfo) { $catInfo.categoryName } else { $null }
    $isSysCat = if ($catInfo) { $catInfo.isSystemCategory } else { $false }
    $fieldsCount = Get-Prop $ds @('fieldsCount', 'FieldsCount')
    if ($null -eq $fieldsCount) {
        $fields = Get-Prop $ds @('fields', 'Fields')
        if ($fields) { $fieldsCount = @($fields).Count } else { $fieldsCount = 0 }
    }
    $afForms = if ($afByDataset.ContainsKey($name)) { @($afByDataset[$name]) } else { @() }
    $inSideMenu = $sideMenuDatasets.Contains($name)
    $hasAt = $name.StartsWith('@')
    $suggestedType = Get-SuggestedType -Name $name -IsSystemCategory $isSysCat -AfForms $afForms -HasAtPrefix $hasAt
    $action = Get-SuggestedAction -SuggestedType $suggestedType -IsSystemCategory $isSysCat -CategoryName $catName

    [void]$rows.Add([ordered]@{
        name               = $name
        description        = [string](Get-Prop $ds @('description', 'Description'))
        categoryId         = [string]$catId
        categoryName       = $catName
        isSystemCategory   = $isSysCat
        fieldsCount        = [int]$fieldsCount
        afFormCodes        = $afForms
        afFormCount        = $afForms.Count
        sideMenuViaAf      = $inSideMenu
        suggestedType      = $suggestedType
        suggestedAction    = $action
    })
}

$rowsSorted = $rows | Sort-Object { $_.name }

$summary = [ordered]@{
    auditedAt          = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
    baseUrl            = $BaseUrl
    useGateway         = [bool]$UseGateway
    totalCategories    = $categories.Count
    systemCategories   = @($catById.Values | Where-Object { $_.isSystemCategory }).Count
    totalDatasets      = $rowsSorted.Count
    uncategorized      = @($rowsSorted | Where-Object { -not $_.categoryName }).Count
    inSystemCategory   = @($rowsSorted | Where-Object { $_.isSystemCategory }).Count
    afLinkedDatasets   = @($rowsSorted | Where-Object { $_.afFormCount -gt 0 }).Count
    needsCategoryMove  = @($rowsSorted | Where-Object { $_.suggestedAction -like '*move-category*' }).Count
    reviewDelete       = @($rowsSorted | Where-Object { $_.suggestedAction -eq 'review-delete' }).Count
    bySuggestedType    = @{
        A = @($rowsSorted | Where-Object { $_.suggestedType -eq 'A' }).Count
        B = @($rowsSorted | Where-Object { $_.suggestedType -eq 'B' }).Count
        C = @($rowsSorted | Where-Object { $_.suggestedType -eq 'C' }).Count
        D = @($rowsSorted | Where-Object { $_.suggestedType -eq 'D' }).Count
        E = @($rowsSorted | Where-Object { $_.suggestedType -eq 'E' }).Count
    }
}

$report = [ordered]@{
    summary    = $summary
    categories = @($catById.Values | Sort-Object categoryName)
    datasets   = $rowsSorted
}

$stamp = Get-Date -Format "yyyyMMdd_HHmmss"
$jsonPath = Join-Path $OutputDir "audit_odak_$stamp.json"
$latestPath = Join-Path $OutputDir "audit_odak_latest.json"
$report | ConvertTo-Json -Depth 8 | Set-Content -Path $jsonPath -Encoding UTF8
$report | ConvertTo-Json -Depth 8 | Set-Content -Path $latestPath -Encoding UTF8

Write-Host ""
Write-Host "Ozet:" -ForegroundColor Cyan
Write-Host "  Kategori: $($summary.totalCategories) (sistem: $($summary.systemCategories))" -ForegroundColor White
Write-Host "  Dataset: $($summary.totalDatasets) (kategorisiz: $($summary.uncategorized), sistem kat.: $($summary.inSystemCategory))" -ForegroundColor White
Write-Host "  AF bagli: $($summary.afLinkedDatasets) | Tasima adayi: $($summary.needsCategoryMove) | Silme inceleme: $($summary.reviewDelete)" -ForegroundColor White
Write-Host ""
Write-Host "JSON: $jsonPath" -ForegroundColor Green
Write-Host "JSON (latest): $latestPath" -ForegroundColor Green

if ($UpdateInventoryMarkdown) {
    $invPath = Join-Path $scriptDir "../INVENTORY.md"
    $catTable = @("| categoryName | dataId | isSystemCategory | datasetCount | description |", "|---|---|---|---:|---|")
    foreach ($c in ($catById.Values | Sort-Object categoryName)) {
        $cnt = @($rowsSorted | Where-Object { $_.categoryId -eq $c.dataId }).Count
        $sys = if ($c.isSystemCategory) { 'true' } else { 'false' }
        $desc = ($c.categoryDescription -replace '\|', '/')
        $catTable += "| $($c.categoryName) | $($c.dataId) | $sys | $cnt | $desc |"
    }

    $dsTable = @("| name | categoryName | isSystemCategory | fields | AF forms | side menu | tip | aksiyon |", "|---|---|---|---:|---|---|---|---|")
    foreach ($r in $rowsSorted) {
        $sys = if ($r.isSystemCategory) { 'true' } else { 'false' }
        $cat = if ($r.categoryName) { $r.categoryName } else { '*(yok)*' }
        $af = if ($r.afFormCount -gt 0) { ($r.afFormCodes -join ', ') } else { '—' }
        $sm = if ($r.sideMenuViaAf) { 'yes' } else { '—' }
        $dsTable += "| ``$($r.name)`` | $cat | $sys | $($r.fieldsCount) | $af | $sm | $($r.suggestedType) | $($r.suggestedAction) |"
    }

    $md = @"
# Dataset Fixing — Canlı Envanter (Odak)

**Durum:** ✅ Envanter alındı  
**Ortam:** Odak · domain ``odak`` · ``$BaseUrl``  
**Son güncelleme:** $(Get-Date -Format "yyyy-MM-dd HH:mm") UTC+local

**Ham JSON:** [reports/audit_odak_latest.json](./reports/audit_odak_latest.json)

---

## Özet

| Metrik | Değer |
|--------|------:|
| Toplam kategori | $($summary.totalCategories) |
| Sistem kategorisi | $($summary.systemCategories) |
| Toplam dataset | $($summary.totalDatasets) |
| Kategorisiz | $($summary.uncategorized) |
| Sistem kategorisindeki dataset | $($summary.inSystemCategory) |
| AF'e bağlı dataset | $($summary.afLinkedDatasets) |
| Kategori taşıma adayı | $($summary.needsCategoryMove) |
| Silme inceleme (E tipi) | $($summary.reviewDelete) |

**Tip dağılımı (heuristic):** A=$($summary.bySuggestedType.A) · B=$($summary.bySuggestedType.B) · C=$($summary.bySuggestedType.C) · D=$($summary.bySuggestedType.D) · E=$($summary.bySuggestedType.E)

---

## Kategoriler

$($catTable -join "`n")

---

## Dataset'ler

$($dsTable -join "`n")

---

## Notlar

- ``suggestedType`` / ``suggestedAction`` otomatik heuristic; [PLAN.md](./PLAN.md) Faz 2'de manuel doğrulanmalı.
- ``side menu`` = Automated Form route üzerinden menüde görünen dataset (dolaylı).
- Tip açıklaması: [PLAN.md §2](./PLAN.md#2-dataset-sınıflandırma-matrisi).
"@
    Set-Content -Path $invPath -Value $md -Encoding UTF8
    Write-Host "INVENTORY.md guncellendi: $invPath" -ForegroundColor Green
}

Write-Host ""
Write-Host "Tamamlandi." -ForegroundColor Green
