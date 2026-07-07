# dm_resources — lazy tree / documentNo için compound indeks sync (PUT)
# Prod'da setup script yalnızca POST yapar; mevcut dataset'e yeni indexList eklemek için bu script kullanılır.
#
#   .\docs\odak\operationcore\scripts\get-operationcore-token-prod.ps1
#   .\docs\odak\document_intelligence\scripts\patch-dm-resources-indexes.ps1
#   .\docs\odak\document_intelligence\scripts\patch-dm-resources-indexes.ps1 -BaseUrl "http://192.168.20.20:5040"

param(
    [string]$BaseUrl = "http://192.168.20.8:5040",
    [string]$Token = $env:DI_TOKEN,
    [switch]$WhatIf = $false
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../../../..")).Path
$datasetsFile = Join-Path $repoRoot "docs/odak/document_intelligence/datasets/documentintelligence_datasets_phase1.json"
$datasetName = "dm_resources"
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
$schema = $schemas | Where-Object { $_.name -eq $datasetName } | Select-Object -First 1
if (-not $schema) { throw "Schema missing: $datasetName" }

$targetIndexes = @($schema.indexList)
$requiredNames = @("idx_parentId_type", "idx_type_parentId", "idx_documentNo")

Write-Host ""
Write-Host "dm_resources index sync ($BaseUrl)" -ForegroundColor Cyan

$getUri = "$BaseUrl$datasetsPath/$datasetName"
try {
    $current = Invoke-RestMethod -Uri $getUri -Method GET -Headers $headers
}
catch {
    Write-Host "GET $datasetName basarisiz: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

$data = $current.data
if (-not $data) { $data = $current }

$merged = @()
$byName = @{}
if ($data.indexList) {
    foreach ($idx in $data.indexList) {
        $merged += $idx
        $byName[$idx.name] = $true
    }
}

$added = @()
foreach ($idx in $targetIndexes) {
    if ($requiredNames -notcontains $idx.name) { continue }
    if ($byName.ContainsKey($idx.name)) { continue }
    $merged += $idx
    $added += $idx.name
}

if ($added.Count -eq 0) {
    Write-Host "Tum hedef indeksler zaten mevcut: $($requiredNames -join ', ')" -ForegroundColor Green
    exit 0
}

Write-Host "Eklenecek indeksler: $($added -join ', ')" -ForegroundColor Yellow
Write-Host "Toplam indexList: $($merged.Count)" -ForegroundColor Gray

$bodyObj = @{
    IndexList = $merged
}
$bodyJson = $bodyObj | ConvertTo-Json -Depth 20 -Compress

if ($WhatIf) {
    Write-Host "WhatIf PUT $getUri" -ForegroundColor Yellow
    Write-Host $bodyJson
    exit 0
}

try {
    $bytes = $utf8.GetBytes($bodyJson)
    $r = Invoke-RestMethod -Uri $getUri -Method PUT -Headers $headers -Body $bytes -ContentType "application/json; charset=utf-8"
    Write-Host "OK guncellendi (dataId=$($r.dataId))" -ForegroundColor Green
}
catch {
    $msg = $_.ErrorDetails.Message
    if (-not $msg) { $msg = $_.Exception.Message }
    Write-Host "HATA: $msg" -ForegroundColor Red
    exit 1
}

Write-Host "Tamamlandi." -ForegroundColor Cyan
