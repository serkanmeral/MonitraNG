# BV Toplantı Tutanağı şablonunu Belge Tasarımcısına yazar.
# Varsayılan hedef yerel ağ geçididir. Odak adresi verilmez.
#
#   .\docs\odak\document_intelligence\scripts\build-bv-meeting-minutes-seed-docx.ps1
#   .\docs\odak\document_intelligence\scripts\seed-designer-template-bv-meeting-minutes.ps1

param(
    [string]$BaseUrl = "https://localhost:5040",
    [string]$Token = $env:DI_TOKEN,
    [switch]$WhatIf = $false,
    [switch]$Replace = $false
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
$repoRoot = (Resolve-Path (Join-Path $scriptDir "../../../..")).Path
$seedFile = Join-Path $repoRoot "docs/odak/document_intelligence/datasets/seed-designer-template-bv-meeting-minutes.json"
. (Join-Path $scriptDir "lib/Convert-DiTemplateParameters.ps1")

if ($BaseUrl -match "192\.168\.20\.8") {
    throw "Bu seed Odak adresine yazmaz."
}

$token = $Token
if ([string]::IsNullOrWhiteSpace($token)) {
    $tokenFile = Join-Path $env:TEMP "serkan_token.txt"
    if (Test-Path $tokenFile) { $token = (Get-Content -Raw $tokenFile).Trim() }
}
if ([string]::IsNullOrWhiteSpace($token)) {
    throw "Token yok. DI_TOKEN veya $env:TEMP\serkan_token.txt gerekli."
}

$seed = [IO.File]::ReadAllText($seedFile, [Text.UTF8Encoding]::new($false)) | ConvertFrom-Json
$tpl = $seed.template
$docxPath = Join-Path $repoRoot "docs/odak/document_intelligence/sample/$($tpl.docxFile)"
if (-not (Test-Path $docxPath)) {
    throw "DOCX yok: $docxPath"
}

$headers = @{ Authorization = "Bearer $($token.Trim())" }
$categoriesBase = "$($BaseUrl.TrimEnd('/'))/documents/api/v1/template-categories"
$templatesBase = "$($BaseUrl.TrimEnd('/'))/documents/api/v1/templates"
$utf8 = [System.Text.Encoding]::UTF8

function Invoke-Json {
    param([string]$Method, [string]$Uri, [hashtable]$Body)
    $params = @{
        Uri = $Uri
        Headers = $headers
        Method = $Method
        SkipCertificateCheck = $true
        AllowInsecureRedirect = $true
    }
    if ($Body) {
        $params.Body = $utf8.GetBytes(($Body | ConvertTo-Json -Depth 12 -Compress))
        $params.ContentType = "application/json; charset=utf-8"
    }
    if ($Method -eq "DELETE") {
        Invoke-RestMethod @params | Out-Null
        return $null
    }
    return Invoke-RestMethod @params
}

function Find-Child([array]$nodes, [string]$name) {
    foreach ($n in @($nodes)) {
        if ($n.name -eq $name) { return $n }
    }
    return $null
}

Write-Host "BV tutanak seed -> $BaseUrl" -ForegroundColor Cyan
$tree = @(Invoke-Json -Method GET -Uri "$categoriesBase/tree")
$root = Find-Child $tree $seed.categoryPath[0]
if (-not $root) {
    if ($WhatIf) { throw "WhatIf: kategori yok: $($seed.categoryPath[0])" }
    $root = Invoke-Json -Method POST -Uri $categoriesBase -Body @{
        name = [string]$seed.categoryPath[0]
        description = "Toplantı tutanağı şablonları."
    }
    Write-Host "OK kategori $($root.name)" -ForegroundColor Green
    $tree = @(Invoke-Json -Method GET -Uri "$categoriesBase/tree")
    $root = Find-Child $tree $seed.categoryPath[0]
}
$child = Find-Child $root.children $seed.categoryPath[1]
if (-not $child) {
    if ($WhatIf) { throw "WhatIf: kategori yok: $($seed.categoryPath[1])" }
    $child = Invoke-Json -Method POST -Uri $categoriesBase -Body @{
        name = [string]$seed.categoryPath[1]
        description = "BV toplantı tutanağı ve aynı iskeleti kullanan tutanaklar."
        parentId = [string]$root.id
    }
    Write-Host "OK kategori $($child.name)" -ForegroundColor Green
}
$categoryId = [string]$child.id

$existing = $null
$listed = Invoke-Json -Method GET -Uri ($templatesBase + "?categoryId=" + [Uri]::EscapeDataString($categoryId))
foreach ($item in @($listed.items)) {
    if ($item.code -eq $tpl.code -or $item.name -eq $tpl.name) { $existing = $item; break }
}
if ($existing) {
    if (-not $Replace) {
        Write-Host "SKIP: $($tpl.code) zaten var." -ForegroundColor Yellow
        exit 0
    }
    if (-not $WhatIf) {
        Invoke-Json -Method DELETE -Uri "$templatesBase/$($existing.id)" | Out-Null
        Write-Host "OK delete" -ForegroundColor Yellow
    }
}

$bytes = [IO.File]::ReadAllBytes($docxPath)
if ($WhatIf) {
    Write-Host "WhatIf POST $($tpl.name)" -ForegroundColor Yellow
    exit 0
}

$created = Invoke-Json -Method POST -Uri "$templatesBase/from-reference" -Body @{
    categoryId = $categoryId
    name = [string]$tpl.name
    description = [string]$tpl.description
    content = [Convert]::ToBase64String($bytes)
    fileName = [string]$tpl.docxFile
    size = $bytes.Length
}
$templateId = [string]$created.id
Write-Host "OK create" -ForegroundColor Green

Invoke-Json -Method PUT -Uri "$templatesBase/$templateId/metadata" -Body @{
    name = [string]$tpl.name
    code = [string]$tpl.code
} | Out-Null

$params = ConvertTo-DiTemplateParameterEntries -Parameters @($tpl.parameters)
Invoke-Json -Method PUT -Uri "$templatesBase/$templateId/parameters" -Body @{ parameters = $params } | Out-Null
Invoke-Json -Method PUT -Uri "$templatesBase/$templateId/letterhead" -Body @{ letterhead = $tpl.letterhead } | Out-Null
Invoke-Json -Method PUT -Uri "$templatesBase/$templateId/footer" -Body @{ footer = $tpl.footer } | Out-Null
Invoke-Json -Method POST -Uri "$templatesBase/$templateId/publish" | Out-Null
Write-Host "OK $($tpl.code) published" -ForegroundColor Green
