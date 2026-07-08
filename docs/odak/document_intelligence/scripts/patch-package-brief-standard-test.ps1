# Test ortami: PACKAGE-BRIEF-STD — PPTX binary sync + publish
#
# Onemli: patch yalnizca parametre guncellemez; seed PPTX dosyasini da sunucuya yukler.
#
# Kullanim:
#   .\docs\odak\document_intelligence\scripts\patch-package-brief-standard-test.ps1

param(
    [string]$BaseUrl = "http://192.168.20.20:5040",
    [string]$TemplateCode = "PACKAGE-BRIEF-STD",
    [string]$Token = $env:DI_TOKEN,
    [switch]$WhatIf = $false,
    [switch]$SkipPublish = $false,
    [switch]$SkipBinarySync = $false
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
$repoRoot = (Resolve-Path (Join-Path $scriptDir "../../../..")).Path
$seedFile = Join-Path $repoRoot "docs/odak/document_intelligence/datasets/seed-designer-template-package-brief-standard.json"
$buildScript = Join-Path $scriptDir "build-package-brief-seed-pptx.ps1"
$seedScript = Join-Path $scriptDir "seed-designer-template-package-brief-standard.ps1"
. (Join-Path $scriptDir "lib/Convert-DiTemplateParameters.ps1")

$token = $Token
if ([string]::IsNullOrEmpty($token)) {
    $loadTokenScript = Join-Path $scriptDir "..\..\operationcore\scripts\load-operationcore-token.ps1"
    if (Test-Path $loadTokenScript) { $token = & $loadTokenScript }
}
if ([string]::IsNullOrEmpty($token)) { throw "Token yok." }
$token = $token.Trim()

$seed = [IO.File]::ReadAllText($seedFile, [Text.UTF8Encoding]::new($false)) | ConvertFrom-Json
$tplSeed = $seed.template

$headers = @{
    Authorization = "Bearer $token"
    "Content-Type" = "application/json"
}
$categoriesBase = "$BaseUrl/documents/api/v1/template-categories"
$templatesBase = "$BaseUrl/documents/api/v1/templates"
$utf8 = [System.Text.Encoding]::UTF8

function Invoke-Json {
    param([string]$Method, [string]$Uri, [hashtable]$Body = $null)
    if ($Body) {
        $json = $Body | ConvertTo-Json -Depth 12 -Compress
        $bytes = $utf8.GetBytes($json)
        return Invoke-RestMethod -Uri $Uri -Headers $headers -Method $Method -Body $bytes -ContentType "application/json; charset=utf-8"
    }
    return Invoke-RestMethod -Uri $Uri -Headers $headers -Method $Method
}

function Find-CategoryByPath {
    param([string[]]$Path)
    $tree = Invoke-Json -Method GET -Uri "$categoriesBase/tree"
    $roots = if ($tree -is [System.Array]) { @($tree) } else { @($tree) }
    $nodes = $roots
    $found = $null
    foreach ($segment in $Path) {
        $found = $null
        foreach ($n in $nodes) {
            if ($n.name -eq $segment) { $found = $n; break }
        }
        if (-not $found) { throw "Kategori bulunamadi: $segment" }
        $nodes = if ($found.children) { @($found.children) } else { @() }
    }
    return [string]$found.id
}

Write-Host "Package Brief publish patch -> $BaseUrl ($TemplateCode)" -ForegroundColor Cyan

if (-not $SkipBinarySync) {
    if ($WhatIf) {
        Write-Host "WhatIf: build-package-brief-seed-pptx + seed -Replace" -ForegroundColor Yellow
    } else {
        Write-Host "PPTX binary sync (build + seed -Replace)..." -ForegroundColor Cyan
        & $buildScript
        & $seedScript -BaseUrl $BaseUrl -Replace -Token $token
        Write-Host "OK PPTX binary sunucuya yuklendi" -ForegroundColor Green
    }
}

$categoryPath = @($seed.categoryPath | ForEach-Object { [string]$_ })
$categoryId = Find-CategoryByPath -Path $categoryPath
$listUri = "$templatesBase" + "?categoryId=" + [Uri]::EscapeDataString($categoryId)
$list = Invoke-Json -Method GET -Uri $listUri
$tpl = @($list.items) | Where-Object { $_.code -eq $TemplateCode } | Select-Object -First 1
if (-not $tpl) { throw "Sablon bulunamadi: $TemplateCode" }

Write-Host "Sablon: $($tpl.name) id=$($tpl.id) status=$($tpl.status)" -ForegroundColor DarkGray

if ($WhatIf) {
    Write-Host "WhatIf PUT parameters + publish" -ForegroundColor Yellow
    exit 0
}

$params = ConvertTo-DiTemplateParameterEntries -Parameters @($tplSeed.parameters)
$paramBody = @{
    parameters = $params
    primaryContextType = [string]$tplSeed.primaryContextType
    generationProfile = [string]$tplSeed.generationProfile
}

Invoke-Json -Method PUT -Uri "$templatesBase/$($tpl.id)/parameters" -Body $paramBody | Out-Null
Write-Host "OK parameters guncellendi" -ForegroundColor Green

if (-not $SkipPublish) {
    Invoke-Json -Method POST -Uri "$templatesBase/$($tpl.id)/publish" -Body @{} | Out-Null
    Write-Host "OK published" -ForegroundColor Green
}

Write-Host "Patch tamam." -ForegroundColor Cyan
