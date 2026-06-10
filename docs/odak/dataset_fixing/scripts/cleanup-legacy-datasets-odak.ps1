# Odak — legacy dataset / AF form / side menu temizligi
# Usage:
#   pwsh -File docs/odak/dataset_fixing/scripts/cleanup-legacy-datasets-odak.ps1
#   pwsh -File ... -DryRun

param(
    [string]$BaseUrl = "http://192.168.20.20:5040",
    [switch]$UseGateway,
    [switch]$DryRun,
    [string]$ManifestPath = ""
)

$ErrorActionPreference = "Stop"
if (-not $PSBoundParameters.ContainsKey('UseGateway')) { $UseGateway = $true }

$scriptDir = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $PSCommandPath }
$repoRoot = (Resolve-Path (Join-Path $scriptDir "../../../..")).Path
if ([string]::IsNullOrWhiteSpace($ManifestPath)) {
    $ManifestPath = Join-Path $scriptDir "legacy-cleanup-manifest.json"
}

$loadTokenScript = Join-Path $repoRoot "docs/odak/operationcore/scripts/load-operationcore-token.ps1"
$token = (& $loadTokenScript).Trim()
if ([string]::IsNullOrWhiteSpace($token)) { Write-Host "Token alinamadi." -ForegroundColor Red; exit 1 }
$script:Bearer = $token

$dataPath       = if ($UseGateway) { "/data/api/v1/data" } else { "/api/v1/data" }
$datasetsPath   = if ($UseGateway) { "/data/api/v1/datasets" } else { "/api/v1/datasets" }
$categoriesPath = if ($UseGateway) { "/data/api/v1/dataset-categories" } else { "/api/v1/dataset-categories" }
$dsAutomatedForms = '@automated_forms'
$dsSideMenu = '@side_menu'

$manifest = Get-Content $ManifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
[System.Net.ServicePointManager]::ServerCertificateValidationCallback = { $true }

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

function Invoke-OdakApi {
    param([string]$Method, [string]$RelativePath, [object]$Body = $null)
    $uri = '{0}{1}' -f $BaseUrl.TrimEnd('/'), $RelativePath
    $json = if ($null -ne $Body) { $Body | ConvertTo-Json -Depth 20 } else { $null }
    if ($DryRun) {
        Write-Host "  [DRY] $Method $RelativePath" -ForegroundColor DarkYellow
        return @{ ok = $true; dry = $true }
    }
    try {
        $params = @{
            Uri             = $uri
            Method          = $Method
            Headers         = @{ Authorization = "Bearer $script:Bearer"; Accept = 'application/json' }
            UseBasicParsing = $true
        }
        if ($PSVersionTable.PSVersion.Major -ge 6) { $params['SkipCertificateCheck'] = $true }
        if ($json) {
            $params['ContentType'] = 'application/json; charset=utf-8'
            $params['Body'] = [System.Text.Encoding]::UTF8.GetBytes($json)
        }
        $r = Invoke-WebRequest @params
        return @{ ok = $true; status = [int]$r.StatusCode; content = $r.Content }
    } catch {
        $detail = $_.Exception.Message
        try {
            $stream = $_.Exception.Response.GetResponseStream()
            if ($stream) { $detail = (New-Object System.IO.StreamReader($stream)).ReadToEnd() }
        } catch { }
        return @{ ok = $false; error = $detail }
    }
}

function Invoke-OdakGet {
    param([string]$RelativePath)
    $r = Invoke-OdakApi -Method GET -RelativePath $RelativePath
    if (-not $r.ok -or $DryRun) { return $null }
    if (-not $r.content) { return $null }
    return $r.content | ConvertFrom-Json
}

function Get-DataArray {
    param([string]$DatasetName)
    $enc = [Uri]::EscapeDataString($DatasetName)
    $resp = Invoke-OdakGet -RelativePath ('{0}/{1}?limit=10000' -f $dataPath, $enc)
    if ($null -eq $resp) { return @() }
    if ($resp -is [System.Array]) { return @($resp) }
    foreach ($p in @('items', 'Items', 'data', 'Data')) {
        if ($null -ne $resp.$p) { return @($resp.$p) }
    }
    return @($resp)
}

function Test-WildcardMatch {
    param([string]$Value, [string]$Pattern)
    if ([string]::IsNullOrWhiteSpace($Value)) { return $false }
    $regex = '^' + [regex]::Escape($Pattern).Replace('\*', '.*') + '$'
    return $Value -match $regex
}

function Test-SideMenuLegacy {
    param($Item)
    $to = [string](Get-Prop $Item @('to', 'To', 'route', 'Route', 'path', 'Path'))
    $pageCode = [string](Get-Prop $Item @('pageCode', 'PageCode'))
    $title = [string](Get-Prop $Item @('title', 'Title', 'header', 'Header'))

    foreach ($p in @($manifest.sideMenuToPatterns)) {
        if (Test-WildcardMatch -Value $to -Pattern $p) { return $true }
    }
    foreach ($p in @($manifest.sideMenuPageCodePatterns)) {
        if (Test-WildcardMatch -Value $pageCode -Pattern $p) { return $true }
    }
    # tst AF formlari — formCode route icinde
    if ($to -match 'automated-forms/view/(tst_genres|tst_publishers|tst_books)') { return $true }
    if ($title -match 'Kitap|Book|Yayinci|Turler|Genres|Publishers' -and $to -match 'automated-forms') { return $true }
    return $false
}

function Remove-DataRecord {
    param([string]$DatasetName, [string]$DataId, [string]$Label)
    $encDs = [Uri]::EscapeDataString($DatasetName)
    $path = '{0}/{1}/{2}' -f $dataPath, $encDs, $DataId
    Write-Host "  - $Label ($DataId)" -ForegroundColor Magenta
    $r = Invoke-OdakApi -Method DELETE -RelativePath $path
    if (-not $r.ok -and -not $r.dry) { Write-Host "    HATA: $($r.error)" -ForegroundColor Red }
}

function Remove-DatasetSchema {
    param([string]$Name)
    $enc = [Uri]::EscapeDataString($Name)
    Write-Host "  - schema: $Name" -ForegroundColor Magenta
    $r = Invoke-OdakApi -Method DELETE -RelativePath ('{0}/{1}' -f $datasetsPath, $enc)
    if (-not $r.ok -and -not $r.dry) { Write-Host "    HATA: $($r.error)" -ForegroundColor Red }
}

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Legacy Temizlik (Odak)" -ForegroundColor Cyan
if ($DryRun) { Write-Host "MOD: DRY RUN" -ForegroundColor Yellow }
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# [1] Automated Forms
Write-Host "[1/4] @automated_forms kayitlari" -ForegroundColor Cyan
$formCodes = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($fc in @($manifest.automatedFormCodes)) { [void]$formCodes.Add([string]$fc) }
$dsNames = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($dn in @($manifest.automatedFormDatasetNames)) { [void]$dsNames.Add([string]$dn) }

$afRecords = Get-DataArray -DatasetName $dsAutomatedForms
$afDeleted = 0
foreach ($af in $afRecords) {
    $fc = [string](Get-Prop $af @('formCode', 'FormCode'))
    $dn = [string](Get-Prop $af @('datasetName', 'DatasetName'))
    $id = [string](Get-Prop $af @('__dataId', 'dataId', 'DataId'))
    if (-not $id) { continue }
    if ($formCodes.Contains($fc) -or $dsNames.Contains($dn)) {
        Remove-DataRecord -DatasetName $dsAutomatedForms -DataId $id -Label "AF $fc"
        $afDeleted++
    }
}
Write-Host "  Silinen AF: $afDeleted" -ForegroundColor Green
Write-Host ""

# [2] Side menu
Write-Host "[2/4] @side_menu legacy kayitlari" -ForegroundColor Cyan
$menuItems = Get-DataArray -DatasetName $dsSideMenu
$menuToDelete = @($menuItems | Where-Object { Test-SideMenuLegacy -Item $_ })
Write-Host "  Eslesen menu: $($menuToDelete.Count)" -ForegroundColor Gray
foreach ($m in $menuToDelete) {
    $id = [string](Get-Prop $m @('__dataId', 'dataId', 'DataId'))
    $to = [string](Get-Prop $m @('to', 'route', 'path'))
    $title = [string](Get-Prop $m @('title', 'header'))
    if ($id) {
        Remove-DataRecord -DatasetName $dsSideMenu -DataId $id -Label "menu: $title → $to"
    }
}
Write-Host ""

# [3] Dataset schema
Write-Host "[3/4] Legacy dataset schema silme" -ForegroundColor Cyan
foreach ($name in @($manifest.datasetsToDelete)) {
    Remove-DatasetSchema -Name ([string]$name)
}
Write-Host ""

# [4] Bos LegacyDatasets kategorisi
if ($manifest.deleteLegacyCategoryWhenEmpty) {
    Write-Host "[4/4] LegacyDatasets kategorisi" -ForegroundColor Cyan
    if (-not $DryRun) {
        $allDs = Invoke-OdakGet -RelativePath ('{0}?pageNumber=1&pageSize=100' -f $datasetsPath)
        $items = @()
        if ($allDs.items) { $items = @($allDs.items) }
        elseif ($allDs.Items) { $items = @($allDs.Items) }
        $legacyName = [string]$manifest.legacyCategoryName
        $cats = Invoke-OdakGet -RelativePath ('{0}?pageNumber=1&pageSize=100' -f $categoriesPath)
        $catList = @()
        if ($cats.items) { $catList = @($cats.items) }
        elseif ($cats.Items) { $catList = @($cats.Items) }
        $legacyCat = $catList | Where-Object { (Get-Prop $_ @('categoryName', 'CategoryName')) -eq $legacyName } | Select-Object -First 1
        if ($legacyCat) {
            $legacyId = [string](Get-Prop $legacyCat @('dataId', 'DataId', '__dataId'))
            $remaining = @($items | Where-Object { [string](Get-Prop $_ @('category', 'Category')) -eq $legacyId })
            if ($remaining.Count -eq 0) {
                Write-Host "  - Kategori sil: $legacyName" -ForegroundColor Magenta
                Invoke-OdakApi -Method DELETE -RelativePath ('{0}/{1}' -f $categoriesPath, $legacyId) | Out-Null
            } else {
                Write-Host "  ! Kategori duruyor — $($remaining.Count) dataset kaldi" -ForegroundColor Yellow
            }
        }
    } else {
        Write-Host "  [DRY] Legacy kategori kontrolu atlandi" -ForegroundColor DarkYellow
    }
}

Write-Host ""
Write-Host "Tamamlandi." -ForegroundColor Green
Write-Host "Dogrulama: audit-datasets-odak.ps1 -UpdateInventoryMarkdown" -ForegroundColor Gray
