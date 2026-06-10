# Odak — kategori taksonomisini uygula (CATEGORIES.md sirasi)
# Usage:
#   pwsh -File docs/odak/dataset_fixing/scripts/apply-category-taxonomy-odak.ps1
#   pwsh -File ... -DryRun
#
# Sira: yeni kategoriler → bayrak/rename → dataset tasi → bos kategori sil

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
    $ManifestPath = Join-Path $scriptDir "category-taxonomy-manifest.json"
}

$loadTokenScript = Join-Path $repoRoot "docs/odak/operationcore/scripts/load-operationcore-token.ps1"
if (-not (Test-Path $loadTokenScript)) {
    Write-Host "Token script bulunamadi: $loadTokenScript" -ForegroundColor Red
    exit 1
}
if (-not (Test-Path $ManifestPath)) {
    Write-Host "Manifest bulunamadi: $ManifestPath" -ForegroundColor Red
    exit 1
}

$token = (& $loadTokenScript).Trim()
if ([string]::IsNullOrWhiteSpace($token)) { Write-Host "Token alinamadi." -ForegroundColor Red; exit 1 }
$script:Bearer = $token

$datasetsPath   = if ($UseGateway) { "/data/api/v1/datasets" } else { "/api/v1/datasets" }
$categoriesPath = if ($UseGateway) { "/data/api/v1/dataset-categories" } else { "/api/v1/dataset-categories" }

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

function Get-ListItems {
    param($obj)
    if ($null -eq $obj) { return @() }
    foreach ($p in @('items', 'Items', 'data', 'Data')) {
        if ($null -ne $obj.$p) { return @($obj.$p) }
    }
    return @()
}

function Invoke-OdakApi {
    param(
        [string]$Method,
        [string]$RelativePath,
        [object]$Body = $null
    )
    $uri = '{0}{1}' -f $BaseUrl.TrimEnd('/'), $RelativePath
    $json = if ($null -ne $Body) { $Body | ConvertTo-Json -Depth 20 -Compress:$false } else { $null }

    if ($DryRun) {
        Write-Host "  [DRY] $Method $RelativePath" -ForegroundColor DarkYellow
        if ($json) { Write-Host "        $json" -ForegroundColor DarkGray }
        return @{ ok = $true; dry = $true; object = $null }
    }

    try {
        $params = @{
            Uri             = $uri
            Method          = $Method
            Headers         = @{
                Authorization = "Bearer $script:Bearer"
                Accept        = 'application/json'
            }
            UseBasicParsing = $true
        }
        if ($PSVersionTable.PSVersion.Major -ge 6) { $params['SkipCertificateCheck'] = $true }
        if ($json) {
            $params['ContentType'] = 'application/json; charset=utf-8'
            $params['Body'] = [System.Text.Encoding]::UTF8.GetBytes($json)
        }
        $r = Invoke-WebRequest @params
        $obj = $null
        if ($r.Content) {
            try { $obj = $r.Content | ConvertFrom-Json } catch { $obj = $null }
        }
        return @{ ok = $true; dry = $false; object = $obj; status = [int]$r.StatusCode }
    } catch {
        $detail = $_.Exception.Message
        try {
            $stream = $_.Exception.Response.GetResponseStream()
            if ($stream) {
                $reader = New-Object System.IO.StreamReader($stream)
                $detail = $reader.ReadToEnd()
            }
        } catch { }
        return @{ ok = $false; dry = $false; error = $detail; object = $null }
    }
}

function Invoke-OdakGet {
    param([string]$RelativePath)
    $r = Invoke-OdakApi -Method GET -RelativePath $RelativePath
    if (-not $r.ok) { return $null }
    return $r.object
}

function Get-AllCategories {
    $all = @()
    $page = 1
    do {
        $path = '{0}?pageNumber={1}&pageSize=100' -f $categoriesPath, $page
        $resp = Invoke-OdakGet -RelativePath $path
        if ($null -eq $resp) { break }
        $items = Get-ListItems $resp
        $all += $items
        if ($items.Count -lt 100) { break }
        $page++
    } while ($page -le 20)
    return $all
}

function Get-AllDatasets {
    $all = @()
    $page = 1
    do {
        $path = '{0}?pageNumber={1}&pageSize=100' -f $datasetsPath, $page
        $resp = Invoke-OdakGet -RelativePath $path
        if ($null -eq $resp) { break }
        $items = Get-ListItems $resp
        $all += $items
        if ($items.Count -lt 100) { break }
        $page++
    } while ($page -le 20)
    return $all
}

function Find-CategoryByName {
    param([array]$Categories, [string]$Name)
    foreach ($c in $Categories) {
        $n = Get-Prop $c @('categoryName', 'CategoryName')
        if ($n -eq $Name) { return $c }
    }
    return $null
}

function Get-CategoryId {
    param($Category)
    [string](Get-Prop $Category @('dataId', 'DataId', '__dataId'))
}

function Ensure-Category {
    param(
        [string]$Name,
        [string]$Description,
        [bool]$IsSystem,
        [ref]$CategoriesRef
    )
    $existing = Find-CategoryByName -Categories $CategoriesRef.Value -Name $Name
    if ($null -eq $existing) {
        Write-Host "  + Olustur: $Name (system=$IsSystem)" -ForegroundColor Cyan
        $body = @{
            CategoryName        = $Name
            CategoryDescription = $Description
            IsSystemCategory    = $IsSystem
        }
        $r = Invoke-OdakApi -Method POST -RelativePath $categoriesPath -Body $body
        if (-not $r.ok) {
            Write-Host "    HATA: $($r.error)" -ForegroundColor Red
            return $null
        }
        $id = Get-Prop $r.object @('dataId', 'DataId')
        if (-not $id) { $id = Get-Prop $r.object.data @('dataId', 'DataId') }
        if (-not $DryRun) { $CategoriesRef.Value = Get-AllCategories }
        return $id
    }

    $id = Get-CategoryId $existing
    $curSys = [bool](Get-Prop $existing @('isSystemCategory', 'IsSystemCategory'))
    $curDesc = [string](Get-Prop $existing @('categoryDescription', 'CategoryDescription'))
    $needsUpdate = ($curSys -ne $IsSystem) -or ($Description -and $curDesc -ne $Description)

    if ($needsUpdate) {
        Write-Host "  ~ Guncelle: $Name (system=$IsSystem)" -ForegroundColor Yellow
        $body = @{}
        if ($Description) { $body['CategoryDescription'] = $Description }
        $body['IsSystemCategory'] = $IsSystem
        $r = Invoke-OdakApi -Method PUT -RelativePath ('{0}/{1}' -f $categoriesPath, $id) -Body $body
        if (-not $r.ok) { Write-Host "    HATA: $($r.error)" -ForegroundColor Red }
        if (-not $DryRun) { $CategoriesRef.Value = Get-AllCategories }
    } else {
        Write-Host "  = Mevcut: $Name" -ForegroundColor DarkGreen
    }
    return $id
}

function Update-CategoryFromManifest {
    param(
        $Entry,
        [ref]$CategoriesRef
    )
    $match = [string]$Entry.matchName
    $cat = Find-CategoryByName -Categories $CategoriesRef.Value -Name $match
    if ($null -eq $cat) {
        Write-Host "  ! Bulunamadi: $match" -ForegroundColor Red
        return
    }
    $id = Get-CategoryId $cat
    $newName = if ($Entry.renameTo) { [string]$Entry.renameTo } else { $match }
    $body = @{}
    if ($Entry.renameTo) { $body['CategoryName'] = $newName }
    if ($Entry.categoryDescription) { $body['CategoryDescription'] = [string]$Entry.categoryDescription }
    if ($null -ne $Entry.isSystemCategory) { $body['IsSystemCategory'] = [bool]$Entry.isSystemCategory }

    Write-Host "  ~ $match → $newName (system=$($Entry.isSystemCategory))" -ForegroundColor Yellow
    $r = Invoke-OdakApi -Method PUT -RelativePath ('{0}/{1}' -f $categoriesPath, $id) -Body $body
    if (-not $r.ok) { Write-Host "    HATA: $($r.error)" -ForegroundColor Red }
    if (-not $DryRun) { $CategoriesRef.Value = Get-AllCategories }
}

function Set-DatasetCategory {
    param(
        [string]$DatasetName,
        [string]$CategoryId,
        [hashtable]$DatasetByName
    )
    if (-not $DatasetByName.ContainsKey($DatasetName)) {
        Write-Host "    ! Dataset yok: $DatasetName" -ForegroundColor Red
        return
    }
    $ds = $DatasetByName[$DatasetName]
    $curCat = [string](Get-Prop $ds @('category', 'Category'))
    if ($curCat -eq $CategoryId) {
        Write-Host "    = $DatasetName (zaten dogru kategori)" -ForegroundColor DarkGray
        return
    }
    Write-Host "    → $DatasetName" -ForegroundColor White
    $encoded = [Uri]::EscapeDataString($DatasetName)
    $body = @{ Category = $CategoryId }
    $r = Invoke-OdakApi -Method PUT -RelativePath ('{0}/{1}' -f $datasetsPath, $encoded) -Body $body
    if (-not $r.ok) { Write-Host "      HATA: $($r.error)" -ForegroundColor Red }
}

function Remove-CategoryByName {
    param(
        [string]$Name,
        [ref]$CategoriesRef
    )
    $cat = Find-CategoryByName -Categories $CategoriesRef.Value -Name $Name
    if ($null -eq $cat) {
        Write-Host "  - Zaten yok: $Name" -ForegroundColor DarkGray
        return
    }
    $id = Get-CategoryId $cat
    Write-Host "  - Sil: $Name" -ForegroundColor Magenta
    $r = Invoke-OdakApi -Method DELETE -RelativePath ('{0}/{1}' -f $categoriesPath, $id)
    if (-not $r.ok) { Write-Host "    HATA: $($r.error)" -ForegroundColor Red }
    else { if (-not $DryRun) { $CategoriesRef.Value = Get-AllCategories } }
}

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Kategori Taksonomisi Uygulama (Odak)" -ForegroundColor Cyan
if ($DryRun) { Write-Host "MOD: DRY RUN" -ForegroundColor Yellow }
Write-Host "Manifest: $ManifestPath" -ForegroundColor Gray
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

$categories = @(Get-AllCategories)
$catRef = [ref]$categories

# ① Yeni kategoriler
Write-Host "[1/4] Yeni kategoriler" -ForegroundColor Cyan
foreach ($c in $manifest.createCategories) {
    $null = Ensure-Category -Name $c.categoryName -Description $c.categoryDescription `
        -IsSystem ([bool]$c.isSystemCategory) -CategoriesRef $catRef
}
Write-Host ""

# ② Mevcut kategoriler — bayrak / rename
Write-Host "[2/4] Kategori guncelleme (bayrak, rename)" -ForegroundColor Cyan
foreach ($u in $manifest.updateCategories) {
    Update-CategoryFromManifest -Entry $u -CategoriesRef $catRef
}
# Mevcut sistem kategorileri manifest'te assign var — bayrak zaten dogru olmali
foreach ($name in @('System Datasets', 'Monitoring', 'WidgetDatasets', 'BusinessDatasets')) {
    $entry = $manifest.datasetAssignments.PSObject.Properties[$name]
    if ($null -eq $entry) { continue }
    $isSys = $name -ne 'BusinessDatasets' -and $name -ne 'ReferenceDatasets'
    if ($name -eq 'BusinessDatasets' -or $name -eq 'ReferenceDatasets') { $isSys = $false }
    if ($name -in @('System Datasets', 'Monitoring', 'WidgetDatasets')) { $isSys = $true }
    $cat = Find-CategoryByName -Categories $catRef.Value -Name $name
    if ($cat) {
        $cur = [bool](Get-Prop $cat @('isSystemCategory', 'IsSystemCategory'))
        if ($cur -ne $isSys) {
            $id = Get-CategoryId $cat
            Write-Host "  ~ $name system=$isSys" -ForegroundColor Yellow
            Invoke-OdakApi -Method PUT -RelativePath ('{0}/{1}' -f $categoriesPath, $id) -Body @{ IsSystemCategory = $isSys } | Out-Null
            if (-not $DryRun) { $catRef.Value = Get-AllCategories }
        }
    }
}
Write-Host ""

# ③ Dataset category tasima
Write-Host "[3/4] Dataset kategori atamalari" -ForegroundColor Cyan
$categories = @($catRef.Value)
$datasets = @(Get-AllDatasets)
$dsByName = @{}
foreach ($d in $datasets) {
    $n = [string](Get-Prop $d @('name', 'Name'))
    if ($n) { $dsByName[$n] = $d }
}

$catIdByName = @{}
foreach ($prop in $manifest.datasetAssignments.PSObject.Properties) {
    $catName = $prop.Name
    $cat = Find-CategoryByName -Categories $categories -Name $catName
    if ($null -eq $cat) {
        Write-Host "  ! Kategori bulunamadi: $catName" -ForegroundColor Red
        continue
    }
    $catIdByName[$catName] = Get-CategoryId $cat
    Write-Host "  [$catName]" -ForegroundColor Cyan
    foreach ($dsName in @($prop.Value)) {
        Set-DatasetCategory -DatasetName ([string]$dsName) -CategoryId $catIdByName[$catName] -DatasetByName $dsByName
    }
}
Write-Host ""

# ④ Bos / artik kullanilmayan kategorileri sil
Write-Host "[4/4] Kategori silme" -ForegroundColor Cyan
foreach ($delName in @($manifest.deleteCategories)) {
    Remove-CategoryByName -Name ([string]$delName) -CategoriesRef $catRef
}

Write-Host ""
Write-Host "Tamamlandi." -ForegroundColor Green
if (-not $DryRun) {
    Write-Host "Dogrulama: pwsh -File audit-datasets-odak.ps1 -UpdateInventoryMarkdown" -ForegroundColor Gray
}
