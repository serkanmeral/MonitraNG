# Test ortami: LINE-ACTIVITY-STD generationProfile + primaryContextType guncelle ve publish et.
#
# Kullanim:
#   .\docs\odak\document_intelligence\scripts\patch-line-activity-standard-test.ps1
#   .\docs\odak\document_intelligence\scripts\patch-line-activity-standard-test.ps1 -WhatIf

param(
    [string]$BaseUrl = "http://192.168.20.20:5040",
    [string]$TemplateCode = "LINE-ACTIVITY-STD",
    [string]$Token = $env:DI_TOKEN,
    [switch]$WhatIf = $false,
    [switch]$SkipPublish = $false
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
$repoRoot = (Resolve-Path (Join-Path $scriptDir "../../../..")).Path
$seedFile = Join-Path $repoRoot "docs/odak/document_intelligence/datasets/seed-designer-template-line-activity-standard.json"

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
    if ([string]::IsNullOrWhiteSpace($Uri)) { throw "Invoke-Json: URI bos (Method=$Method)" }
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

Write-Host "Line Activity profil patch -> $BaseUrl ($TemplateCode)" -ForegroundColor Cyan
$categoryPath = @($seed.categoryPath | ForEach-Object { [string]$_ })
$categoryId = Find-CategoryByPath -Path $categoryPath
$listUri = "$templatesBase" + "?categoryId=" + [Uri]::EscapeDataString($categoryId)
$list = Invoke-Json -Method GET -Uri $listUri
$tpl = @($list.items) | Where-Object { $_.code -eq $TemplateCode } | Select-Object -First 1
if (-not $tpl) { throw "Sablon bulunamadi: $TemplateCode" }

Write-Host "Sablon: $($tpl.name) id=$($tpl.id) status=$($tpl.status)" -ForegroundColor DarkGray
$detail = Invoke-Json -Method GET -Uri "$templatesBase/$($tpl.id)"

if ($detail.status -eq "published") {
    Write-Host "Sablon zaten published. generationProfile=$($detail.generationProfile) primaryContext=$($detail.primaryContextType)" -ForegroundColor Yellow
    if ($detail.generationProfile -eq $tplSeed.generationProfile -and $detail.primaryContextType -eq $tplSeed.primaryContextType) {
        Write-Host "Profil alanlari guncel, cikiliyor." -ForegroundColor Green
        exit 0
    }
    throw "Published sablon parametreleri API uzerinden guncellenemez. Tasarimcida draft'a alin veya -Replace ile yeniden seed edin."
}

$params = @()
foreach ($p in @($tplSeed.parameters)) {
    $entry = @{
        key = [string]$p.key
        label = [string]$p.label
        dataType = [string]$p.dataType
        valueSourceMode = [string]$p.valueSourceMode
    }
    if ($p.defaultValue) { $entry.defaultValue = [string]$p.defaultValue }
    if ($p.format) { $entry.format = [string]$p.format }
    if ($p.contextBinding) {
        $cb = @{ path = [string]$p.contextBinding.path }
        if ($p.contextBinding.fallbackPath) { $cb.fallbackPath = [string]$p.contextBinding.fallbackPath }
        if ($p.contextBinding.format) { $cb.format = [string]$p.contextBinding.format }
        $entry.contextBinding = $cb
    }
    if ($p.incremental) {
        $entry.incremental = @{
            format = [string]$p.incremental.format
            startValue = [int]$p.incremental.startValue
            incrementStep = [int]$p.incremental.incrementStep
            scopeKey = [string]$p.incremental.scopeKey
            resetPolicy = [string]$p.incremental.resetPolicy
        }
    }
    $params += $entry
}

$paramBody = @{ parameters = $params }
if ($tplSeed.primaryContextType) { $paramBody.primaryContextType = [string]$tplSeed.primaryContextType }
if ($tplSeed.generationProfile) { $paramBody.generationProfile = [string]$tplSeed.generationProfile }

if ($WhatIf) {
    Write-Host "WhatIf PUT parameters + publish (generationProfile=$($tplSeed.generationProfile))" -ForegroundColor Yellow
    exit 0
}

Invoke-Json -Method PUT -Uri "$templatesBase/$($tpl.id)/parameters" -Body $paramBody | Out-Null
Write-Host "OK parameters (generationProfile=$($tplSeed.generationProfile))" -ForegroundColor Green

if (-not $SkipPublish) {
    Invoke-Json -Method POST -Uri "$templatesBase/$($tpl.id)/publish" | Out-Null
    Write-Host "OK publish" -ForegroundColor Green
}

$verify = Invoke-Json -Method GET -Uri "$templatesBase/$($tpl.id)"
Write-Host "Dogrulama: status=$($verify.status) generationProfile=$($verify.generationProfile) primaryContext=$($verify.primaryContextType)" -ForegroundColor Cyan
