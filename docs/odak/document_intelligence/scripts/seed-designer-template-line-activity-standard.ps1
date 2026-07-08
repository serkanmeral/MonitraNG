# Kalem Activity standart şablon seed — ODK-LINE-ACTIVITY-template-seed.docx + parametreler
#
# Kullanım:
#   .\docs\odak\document_intelligence\scripts\seed-designer-template-categories.ps1
#   .\docs\odak\document_intelligence\scripts\seed-designer-template-line-activity-standard.ps1
#   .\docs\odak\document_intelligence\scripts\seed-designer-template-line-activity-standard.ps1 -BaseUrl "http://192.168.20.20:5040"

param(
    [string]$BaseUrl = "http://192.168.20.8:5040",
    [string]$Token = $env:DI_TOKEN,
    [switch]$WhatIf = $false,
    [switch]$Replace = $false
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
$repoRoot = (Resolve-Path (Join-Path $scriptDir "../../../..")).Path
$seedFile = Join-Path $repoRoot "docs/odak/document_intelligence/datasets/seed-designer-template-line-activity-standard.json"
$cocSeedDocx = Join-Path $repoRoot "docs/odak/document_intelligence/sample/ODK-COC-template-seed.docx"
. (Join-Path $scriptDir "lib/Convert-DiTemplateParameters.ps1")

$token = $Token
$isProd = $BaseUrl -match "192\.168\.20\.8"
if ([string]::IsNullOrEmpty($token)) {
    $loadTokenScript = if ($isProd) {
        Join-Path $scriptDir "..\..\operationcore\scripts\load-operationcore-token-prod.ps1"
    } else {
        Join-Path $scriptDir "..\..\operationcore\scripts\load-operationcore-token.ps1"
    }
    if (Test-Path $loadTokenScript) { $token = & $loadTokenScript }
}
if ([string]::IsNullOrEmpty($token)) {
    Write-Host "Token yok. -Token, `$env:DI_TOKEN veya OC token script." -ForegroundColor Red
    exit 1
}
$token = $token.Trim()

if (-not (Test-Path $seedFile)) { throw "Seed dosyasi yok: $seedFile" }

$seed = [IO.File]::ReadAllText($seedFile, [Text.UTF8Encoding]::new($false)) | ConvertFrom-Json
$tpl = $seed.template
$docxPath = Join-Path $repoRoot "docs/odak/document_intelligence/sample/$($tpl.docxFile)"
if (-not (Test-Path $docxPath)) {
    throw "DOCX yok: $docxPath (once build-line-activity-seed-docx.ps1 calistirin)"
}
$uploadDocx = Join-Path $repoRoot "docs/odak/document_intelligence/sample/ODK-LINE-ACTIVITY-template-upload.docx"
$footerScript = Join-Path $scriptDir "inject-coc-footer-docx.ps1"
if (Test-Path $footerScript) {
    & $footerScript -InputDocx $docxPath -OutputDocx $uploadDocx -SeedJson $seedFile
    if (Test-Path $uploadDocx) { $docxPath = $uploadDocx }
}

$headers = @{
    Authorization = "Bearer $token"
    "Content-Type" = "application/json"
}
$categoriesBase = "$BaseUrl/documents/api/v1/template-categories"
$templatesBase = "$BaseUrl/documents/api/v1/templates"
$utf8 = [System.Text.Encoding]::UTF8

function Invoke-Json {
    param(
        [string]$Method,
        [string]$Uri,
        [hashtable]$Body
    )
    if ($Body) {
        $json = $Body | ConvertTo-Json -Depth 12 -Compress
        $bytes = $utf8.GetBytes($json)
        return Invoke-RestMethod -Uri $Uri -Headers $headers -Method $Method -Body $bytes -ContentType "application/json; charset=utf-8"
    }
    if ($Method -eq "DELETE") {
        Invoke-RestMethod -Uri $Uri -Headers $headers -Method DELETE | Out-Null
        return $null
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
            if ($n.name -eq $segment) {
                $found = $n
                break
            }
        }
        if (-not $found) { throw "Kategori bulunamadi: $segment (once seed-designer-template-categories.ps1 calistirin)" }
        $nodes = if ($found.children) { @($found.children) } else { @() }
    }
    return [string]$found.id
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

Write-Host "Line Activity seed -> $BaseUrl" -ForegroundColor Cyan
$categoryId = Find-CategoryByPath -Path @($seed.categoryPath)
Write-Host "Kategori id: $categoryId" -ForegroundColor DarkGray

$existing = Find-TemplateByCode -CategoryId $categoryId -Code $tpl.code
if ($existing) {
    if (-not $Replace) {
        Write-Host "SKIP: '$($tpl.code)' zaten var (id=$($existing.id)). Yeniden seed icin -Replace kullanin." -ForegroundColor Yellow
        exit 0
    }
    if ($WhatIf) {
        Write-Host "WhatIf DELETE + recreate '$($tpl.code)' (id=$($existing.id))" -ForegroundColor Yellow
        exit 0
    }
    Write-Host "Replace: mevcut '$($tpl.code)' siliniyor (id=$($existing.id))..." -ForegroundColor Yellow
    Invoke-Json -Method DELETE -Uri "$templatesBase/$($existing.id)" | Out-Null
    Write-Host "OK delete" -ForegroundColor Green
}

$bytes = [IO.File]::ReadAllBytes($docxPath)
$contentB64 = [Convert]::ToBase64String($bytes)

$createBody = @{
    categoryId  = $categoryId
    name        = [string]$tpl.name
    description = [string]$tpl.description
    content     = $contentB64
    fileName    = [string]$tpl.docxFile
    size        = $bytes.Length
}

if ($WhatIf) {
    Write-Host "WhatIf POST from-reference '$($tpl.name)' ($($tpl.code))" -ForegroundColor Yellow
    exit 0
}

$created = Invoke-Json -Method POST -Uri "$templatesBase/from-reference" -Body $createBody
$templateId = [string]$created.id
Write-Host "OK create id=$templateId" -ForegroundColor Green

Invoke-Json -Method PUT -Uri "$templatesBase/$templateId/metadata" -Body @{
    name = [string]$tpl.name
    code = [string]$tpl.code
} | Out-Null

$params = ConvertTo-DiTemplateParameterEntries -Parameters @($tpl.parameters)
$paramBody = @{ parameters = $params }
if ($tpl.primaryContextType) { $paramBody.primaryContextType = [string]$tpl.primaryContextType }
if ($tpl.generationProfile) { $paramBody.generationProfile = [string]$tpl.generationProfile }
Invoke-Json -Method PUT -Uri "$templatesBase/$templateId/parameters" -Body $paramBody | Out-Null
Write-Host "OK parameters ($($params.Count))" -ForegroundColor Green

Invoke-Json -Method PUT -Uri "$templatesBase/$templateId/letterhead" -Body @{
    letterhead = $tpl.letterhead
} | Out-Null
Write-Host "OK letterhead" -ForegroundColor Green

Invoke-Json -Method PUT -Uri "$templatesBase/$templateId/footer" -Body @{
    footer = $tpl.footer
} | Out-Null
Write-Host "OK footer" -ForegroundColor Green

$pageStructureBody = @{
    letterhead = $tpl.letterhead
    footer = $tpl.footer
}
if ($tpl.pageLayout) { $pageStructureBody.pageLayout = $tpl.pageLayout }
Invoke-Json -Method PUT -Uri "$templatesBase/$templateId/page-structure" -Body $pageStructureBody | Out-Null
Write-Host "OK page-structure" -ForegroundColor Green

Write-Host "Seed tamam. Template id: $templateId - publish icin patch-line-activity-standard-test.ps1 calistirin." -ForegroundColor Cyan
