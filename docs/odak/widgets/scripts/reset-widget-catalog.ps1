# @widgets + @widget_categories temizligi ve domain kategori seed (Widget Designer baslangic)
#
# @widget_templates DOKUNULMAZ (sistem sablon katalogu).
# Dashboard widget instance'lari (siem-center.*, widgets-demo.*) silinir — gerekirse
# setup-widget-demo-dashboard.ps1 / setup-siem-center-dashboard.ps1 ile yeniden kurulur.
#
# Kullanim (repo kokunden):
#   .\docs\odak\operationcore\scripts\get-operationcore-token.ps1
#   .\docs\odak\widgets\scripts\reset-widget-catalog.ps1
#   .\docs\odak\widgets\scripts\reset-widget-catalog.ps1 -WhatIf

param(
    [string]$BaseUrl = "http://192.168.20.20:5040",
    [switch]$UseGateway = $true,
    [string]$Token = "",
    [string]$LoadTokenScript = "",
    [switch]$WhatIf,
    [switch]$SkipReseedCategories
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../../../..")).Path

if ([string]::IsNullOrEmpty($Token)) {
    if ([string]::IsNullOrEmpty($LoadTokenScript)) {
        $LoadTokenScript = Join-Path $repoRoot "docs/odak/operationcore/scripts/load-operationcore-token.ps1"
    }
    if (Test-Path $LoadTokenScript) {
        $Token = & $LoadTokenScript
    }
}

$categoriesSeedFile = Join-Path $repoRoot "docs/odak/widgets/datasets/widget_categories_seed_v1.json"
$dataPath = if ($UseGateway) { "/data/api/v1/data" } else { "/api/v1/data" }

if ([string]::IsNullOrEmpty($Token)) {
    Write-Host "Token bulunamadi. Once:" -ForegroundColor Red
    Write-Host "  .\docs\odak\operationcore\scripts\get-operationcore-token.ps1" -ForegroundColor Yellow
    exit 1
}

$headers = @{
    "Authorization" = "Bearer $Token"
    "Content-Type"  = "application/json"
}
[System.Net.ServicePointManager]::ServerCertificateValidationCallback = { $true }

function Invoke-Dg {
    param(
        [string]$Uri,
        [string]$Method = "GET",
        [string]$BodyJson = ""
    )
    try {
        $irm = @{ Uri = $Uri; Method = $Method; Headers = $headers }
        if ($Uri.StartsWith("https://")) { $irm.SkipCertificateCheck = $true }
        if ($Method -ne "GET" -and $BodyJson) {
            $irm.Body = $BodyJson
            $irm.ContentType = "application/json; charset=utf-8"
        }
        $response = Invoke-RestMethod @irm
        return @{ Ok = $true; Data = $response }
    }
    catch {
        $code = $null
        $msg = $_.Exception.Message
        try { $code = [int]$_.Exception.Response.StatusCode } catch { }
        return @{ Ok = $false; Code = $code; Body = $msg }
    }
}

function Get-DatasetRecords {
    param([string]$DatasetName)
    $all = @()
    $skip = 0
    $limit = 100
    while ($true) {
        $uri = "$BaseUrl$dataPath/${DatasetName}?skip=$skip&limit=$limit&sort=order,name"
        $r = Invoke-Dg -Uri $uri
        if (-not $r.Ok) {
            if ($r.Code -eq 404) { break }
            throw "Liste alinamadi ($DatasetName): $($r.Body)"
        }
        $items = @()
        if ($r.Data -is [array]) { $items = @($r.Data) }
        elseif ($r.Data.items) { $items = @($r.Data.items) }
        elseif ($r.Data.data) { $items = @($r.Data.data) }
        if ($items.Count -eq 0) { break }
        $all += $items
        if ($items.Count -lt $limit) { break }
        $skip += $limit
    }
    return $all
}

function Remove-DatasetRecords {
    param(
        [string]$DatasetName,
        [array]$Records
    )
    $removed = 0
    foreach ($rec in $Records) {
        $id = $rec.__dataId
        if ([string]::IsNullOrEmpty($id)) { $id = $rec.dataId }
        if ([string]::IsNullOrEmpty($id)) { continue }
        $name = $rec.name
        if ([string]::IsNullOrEmpty($name)) { $name = $rec.title }
        if ([string]::IsNullOrEmpty($name)) { $name = $id }

        if ($WhatIf) {
            Write-Host "  [WhatIf] DELETE $DatasetName/$name ($id)" -ForegroundColor DarkGray
            $removed++
            continue
        }

        $uri = "$BaseUrl$dataPath/${DatasetName}/$id"
        $dr = Invoke-Dg -Uri $uri -Method DELETE
        if ($dr.Ok) {
            Write-Host "  Silindi: $DatasetName/$name" -ForegroundColor Green
            $removed++
        }
        else {
            Write-Host "  HATA silme $name HTTP $($dr.Code) $($dr.Body)" -ForegroundColor Red
        }
    }
    return $removed
}

Write-Host ''
Write-Host "Widget katalog reset ($BaseUrl)$(if ($WhatIf) { ' [WhatIf]' })" -ForegroundColor Cyan
Write-Host ''

Write-Host '1) @widgets kayitlari listeleniyor...' -ForegroundColor Yellow
$widgets = Get-DatasetRecords -DatasetName '@widgets'
Write-Host "   $($widgets.Count) widget bulundu" -ForegroundColor Gray

Write-Host '2) @widget_categories kayitlari listeleniyor...' -ForegroundColor Yellow
$categories = Get-DatasetRecords -DatasetName '@widget_categories'
Write-Host "   $($categories.Count) kategori bulundu" -ForegroundColor Gray

Write-Host '3) @widgets siliniyor...' -ForegroundColor Yellow
$wDel = Remove-DatasetRecords -DatasetName '@widgets' -Records $widgets
Write-Host "   $wDel widget silindi" -ForegroundColor Cyan

Write-Host '4) @widget_categories siliniyor...' -ForegroundColor Yellow
$cDel = Remove-DatasetRecords -DatasetName '@widget_categories' -Records $categories
Write-Host "   $cDel kategori silindi" -ForegroundColor Cyan

if (-not $SkipReseedCategories) {
    Write-Host '5) Domain kategorileri seed ediliyor...' -ForegroundColor Yellow
    $seed = Get-Content $categoriesSeedFile -Raw -Encoding UTF8 | ConvertFrom-Json
    $catUri = "$BaseUrl$dataPath/@widget_categories"
    foreach ($rec in $seed) {
        $body = ($rec | ConvertTo-Json -Depth 10 -Compress)
        if ($WhatIf) {
            Write-Host "  [WhatIf] POST kategori $($rec.name) - $($rec.description)" -ForegroundColor DarkGray
            continue
        }
        $sr = Invoke-Dg -Uri $catUri -Method POST -BodyJson $body
        if ($sr.Ok) {
            Write-Host "  $($rec.name) ($($rec.description)) OK" -ForegroundColor Green
        }
        else {
            if ($sr.Code -eq 400 -and $sr.Body -match 'unique|mevcut|already|duplicate|exists') {
                Write-Host "  $($rec.name) zaten var (atlandi)" -ForegroundColor DarkGray
            }
            else {
                Write-Host "  HATA $($rec.name) HTTP $($sr.Code) $($sr.Body)" -ForegroundColor Red
            }
        }
    }
}
else {
    Write-Host '5) Kategori seed atlandi (-SkipReseedCategories)' -ForegroundColor DarkGray
}

Write-Host ''
Write-Host 'Tamamlandi.' -ForegroundColor Cyan
Write-Host '  @widget_templates dokunulmadi (sablon katalogu).' -ForegroundColor Gray
Write-Host '  Sonraki: /apps/widgets - Widget Designer ile yeni widget olusturun.' -ForegroundColor Gray
Write-Host ''
