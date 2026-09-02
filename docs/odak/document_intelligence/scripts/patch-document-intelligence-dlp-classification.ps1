# Dilim 0 — dm_tags (kind/sensitivity/persistToFile) + dm_resources.classificationTagId şema yaması
#
# Canlı şemayı GET eder, eksik alanları ekler (tam replace yapmaz; boş queries 500 üretmesin).
#
#   .\docs\odak\document_intelligence\scripts\patch-document-intelligence-dlp-classification.ps1 -BaseUrl "http://192.168.20.20:5040"

param(
    [string]$BaseUrl = "http://192.168.20.20:5040",
    [string]$Token = $env:DI_TOKEN,
    [switch]$WhatIf = $false
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../../../..")).Path
$datasetsFile = Join-Path $repoRoot "docs/odak/document_intelligence/datasets/documentintelligence_datasets_phase1.json"
$isProd = $BaseUrl -match "192\.168\.20\.8"

$token = $Token
if ([string]::IsNullOrEmpty($token)) {
    $loadTokenScript = if ($isProd) {
        Join-Path $PSScriptRoot "..\..\operationcore\scripts\load-operationcore-token-prod.ps1"
    } else {
        Join-Path $PSScriptRoot "..\..\operationcore\scripts\load-operationcore-token.ps1"
    }
    if (Test-Path $loadTokenScript) { $token = & $loadTokenScript }
}
if ([string]::IsNullOrEmpty($token)) {
    Write-Host "Token yok. -Token veya OC token script." -ForegroundColor Red
    exit 1
}
$token = $token.Trim()

$headers = @{
    Authorization = "Bearer $token"
    "Content-Type" = "application/json"
}
$datasetsPath = "/data/api/v1/datasets"
$utf8 = [System.Text.Encoding]::UTF8

$schemas = Get-Content $datasetsFile -Raw -Encoding UTF8 | ConvertFrom-Json
$byName = @{}
foreach ($s in $schemas) { $byName[$s.name] = $s }

function Convert-CleanField {
    param($Field)
    $o = [ordered]@{
        fieldType = $Field.fieldType
        name      = [string]$Field.name
        title     = [string]$Field.title
        mandatory = [bool]$Field.mandatory
        unique    = [bool]$Field.unique
        isArray   = [bool]$Field.isArray
    }
    if ($null -ne $Field.defaultValue) { $o.defaultValue = $Field.defaultValue }
    if ($Field.relationDataset) { $o.relationDataset = $Field.relationDataset }
    if ($Field.incrementalOptions) { $o.incrementalOptions = $Field.incrementalOptions }
    if ($Field.datetimeOptions) { $o.datetimeOptions = $Field.datetimeOptions }
    if ($Field.validation) { $o.validation = $Field.validation }
    if ($Field.options) { $o.options = $Field.options }
    return [pscustomobject]$o
}

$targets = @("dm_tags", "dm_resources")
foreach ($name in $targets) {
    if (-not $byName.ContainsKey($name)) { throw "Schema missing: $name" }
    $schema = $byName[$name]
    $uri = "$BaseUrl$datasetsPath/$name"

    Write-Host ""
    Write-Host "PATCH dataset: $name" -ForegroundColor Cyan

    $current = Invoke-RestMethod -Uri $uri -Method GET -Headers $headers
    $liveNames = @($current.fields | ForEach-Object { $_.name })
    $wanted = @($schema.fields)
    $added = @()
    $merged = @($current.fields)
    foreach ($f in $wanted) {
        if ($liveNames -contains $f.name) { continue }
        $merged += $f
        $added += $f.name
    }

    if ($added.Count -eq 0) {
        Write-Host "  Alanlar zaten mevcut ($($liveNames.Count) field)." -ForegroundColor Green
        continue
    }

    Write-Host "  Eklenecek: $($added -join ', ')" -ForegroundColor Yellow
    $clean = @($merged | ForEach-Object { Convert-CleanField $_ })
    $bodyObj = @{ fields = $clean }
    $bodyJson = $bodyObj | ConvertTo-Json -Depth 20 -Compress

    if ($WhatIf) {
        Write-Host "  WhatIf PUT $uri" -ForegroundColor Yellow
        continue
    }

    try {
        $bytes = $utf8.GetBytes($bodyJson)
        $r = Invoke-RestMethod -Uri $uri -Method PUT -Headers $headers -Body $bytes -ContentType "application/json; charset=utf-8"
        Write-Host "  OK fieldsCount=$($r.fieldsCount) (dataId=$($r.dataId))" -ForegroundColor Green
    } catch {
        $msg = $_.ErrorDetails.Message
        if (-not $msg) { $msg = $_.Exception.Message }
        Write-Host "  HATA: $msg" -ForegroundColor Red
        exit 1
    }
}

Write-Host ""
Write-Host "Tamamlandi." -ForegroundColor Cyan
