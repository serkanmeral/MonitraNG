# Generic package media template seed (V1 or V2 via -SeedFile)
#
# Kullanım:
#   .\seed-designer-template-package-media.ps1 -SeedFile ..\datasets\seed-designer-template-package-dashboard-standard-v2.json
#   .\seed-designer-template-package-media.ps1 -SeedFile ..\datasets\seed-designer-template-package-brief-standard-v2.json -Replace

param(
    [Parameter(Mandatory = $true)]
    [string]$SeedFile,
    [string]$BaseUrl = "http://192.168.20.8:5040",
    [string]$Token = $env:DI_TOKEN,
    [switch]$WhatIf = $false,
    [switch]$Replace = $false
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
$repoRoot = (Resolve-Path (Join-Path $scriptDir "../../../..")).Path
$seedPath = if ([IO.Path]::IsPathRooted($SeedFile)) { $SeedFile } else { Join-Path $repoRoot $SeedFile }
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

if (-not (Test-Path $seedPath)) { throw "Seed dosyasi yok: $seedPath" }

$seed = [IO.File]::ReadAllText($seedPath, [Text.UTF8Encoding]::new($false)) | ConvertFrom-Json
$tpl = $seed.template
$binaryFile = if ($tpl.xlsxFile) { [string]$tpl.xlsxFile } elseif ($tpl.pptxFile) { [string]$tpl.pptxFile } else { throw "Seed template xlsxFile veya pptxFile icermeli" }
$binaryPath = Join-Path $repoRoot "docs/odak/document_intelligence/sample/$binaryFile"
$isXlsx = $binaryFile -match '\.xlsx$'

if (-not (Test-Path $binaryPath)) {
    $buildScript = if ($isXlsx) {
        Join-Path $scriptDir "build-package-dashboard-seed-xlsx.ps1"
    } else {
        Join-Path $scriptDir "build-package-brief-seed-pptx.ps1"
    }
    if (-not (Test-Path $buildScript)) { throw "Binary yok ve build script bulunamadi: $binaryPath" }
    if ($binaryFile -match 'v2/') {
        & $buildScript -OutputPath $binaryPath
    } else {
        & $buildScript
    }
    if (-not (Test-Path $binaryPath)) { throw "Binary uretilemedi: $binaryPath" }
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

Write-Host "Package media seed ($([IO.Path]::GetFileName($seedPath))) -> $BaseUrl" -ForegroundColor Cyan
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

$bytes = [IO.File]::ReadAllBytes($binaryPath)
$contentB64 = [Convert]::ToBase64String($bytes)
$fileName = [IO.Path]::GetFileName($binaryFile)

$createBody = @{
    categoryId  = $categoryId
    name        = [string]$tpl.name
    description = [string]$tpl.description
    content     = $contentB64
    fileName    = $fileName
    size        = $bytes.Length
}

if ($WhatIf) {
    Write-Host "WhatIf POST from-reference '$($tpl.name)' ($($tpl.code))" -ForegroundColor Yellow
    exit 0
}

$created = Invoke-Json -Method POST -Uri "$templatesBase/from-reference" -Body $createBody
$templateId = [string]$created.id
Write-Host "OK create id=$templateId" -ForegroundColor Green

try {
    Invoke-Json -Method PUT -Uri "$templatesBase/$templateId/metadata" -Body @{
        name = [string]$tpl.name
        code = [string]$tpl.code
    } | Out-Null
} catch {
    Write-Host "WARN metadata PUT atlandi" -ForegroundColor Yellow
}

$params = ConvertTo-DiTemplateParameterEntries -Parameters @($tpl.parameters)
$paramBody = @{ parameters = $params }
if ($tpl.primaryContextType) { $paramBody.primaryContextType = [string]$tpl.primaryContextType }
if ($tpl.generationProfile) { $paramBody.generationProfile = [string]$tpl.generationProfile }
Invoke-Json -Method PUT -Uri "$templatesBase/$templateId/parameters" -Body $paramBody | Out-Null
Write-Host "OK parameters ($($params.Count))" -ForegroundColor Green

if ($tpl.letterhead -and $tpl.letterhead.enabled -eq $true) {
    try {
        Invoke-Json -Method PUT -Uri "$templatesBase/$templateId/letterhead" -Body @{
            letterhead = $tpl.letterhead
        } | Out-Null
        Write-Host "OK letterhead" -ForegroundColor Green
    } catch {
        Write-Host "WARN letterhead PUT atlandi" -ForegroundColor Yellow
    }
}

Write-Host "Seed tamam. Template id: $templateId code=$($tpl.code)" -ForegroundColor Cyan
