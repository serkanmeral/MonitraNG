# Test ortami: PACKAGE-*-STD-V2 — binary sync + publish (V1'e dokunmaz)
#
# Kullanim:
#   .\docs\odak\document_intelligence\scripts\patch-package-media-v2-test.ps1 -BaseUrl "http://192.168.20.20:5040"

param(
    [string]$BaseUrl = "http://192.168.20.20:5040",
    [string]$Token = $env:DI_TOKEN,
    [switch]$WhatIf = $false,
    [switch]$SkipPublish = $false,
    [switch]$SkipBinarySync = $false
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
$repoRoot = (Resolve-Path (Join-Path $scriptDir "../../../..")).Path
$buildV2 = Join-Path $scriptDir "build-package-media-v2.ps1"
$seedMedia = Join-Path $scriptDir "seed-designer-template-package-media.ps1"

$v2Templates = @(
    @{
        Code     = "PACKAGE-DASHBOARD-STD-V2"
        SeedFile = "docs/odak/document_intelligence/datasets/seed-designer-template-package-dashboard-standard-v2.json"
    },
    @{
        Code     = "PACKAGE-BRIEF-STD-V2"
        SeedFile = "docs/odak/document_intelligence/datasets/seed-designer-template-package-brief-standard-v2.json"
    }
)

$token = $Token
if ([string]::IsNullOrEmpty($token)) {
    $loadTokenScript = Join-Path $scriptDir "..\..\operationcore\scripts\load-operationcore-token.ps1"
    if (Test-Path $loadTokenScript) { $token = & $loadTokenScript }
}
if ([string]::IsNullOrEmpty($token)) { throw "Token yok." }
$token = $token.Trim()

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

function Find-TemplateByCode {
    param([string]$CategoryId, [string]$Code)
    $uri = "$templatesBase" + "?categoryId=" + [Uri]::EscapeDataString($CategoryId)
    $res = Invoke-Json -Method GET -Uri $uri
    $items = @($res.items)
    foreach ($item in $items) {
        if ($item.code -eq $Code) { return $item }
    }
    return $null
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

$categoryId = Find-CategoryByPath -Path @("Operasyon Belgeleri", "Medya Paketi")

Write-Host "Package Media V2 patch -> $BaseUrl" -ForegroundColor Cyan

if (-not $SkipBinarySync) {
    if ($WhatIf) {
        Write-Host "WhatIf: build-package-media-v2 + seed -Replace (V2)" -ForegroundColor Yellow
    } else {
        Write-Host "V2 binary build..." -ForegroundColor Cyan
        & $buildV2
        foreach ($t in $v2Templates) {
            Write-Host "Seed $($t.Code)..." -ForegroundColor Cyan
            & $seedMedia -SeedFile $t.SeedFile -BaseUrl $BaseUrl -Replace -Token $token
        }
        Write-Host "OK V2 binary sunucuya yuklendi" -ForegroundColor Green
    }
}

if ($SkipPublish) {
    Write-Host "SkipPublish — publish atlandi." -ForegroundColor Yellow
    exit 0
}

foreach ($t in $v2Templates) {
    $existing = Find-TemplateByCode -CategoryId $categoryId -Code $t.Code
    if (-not $existing) {
        Write-Host "WARN $($t.Code) bulunamadi — once seed calistirin." -ForegroundColor Yellow
        continue
    }

    if ($WhatIf) {
        Write-Host "WhatIf publish $($t.Code) id=$($existing.id)" -ForegroundColor Yellow
        continue
    }

    Write-Host "Publish $($t.Code) id=$($existing.id)..." -ForegroundColor Cyan
    Invoke-Json -Method POST -Uri "$templatesBase/$($existing.id)/publish" -Body @{} | Out-Null
    Write-Host "OK publish $($t.Code)" -ForegroundColor Green
}

Write-Host "V2 patch tamam." -ForegroundColor Cyan
