# Test: COC-STANDARD docNo -> poDocNo ayrimi (govde vs antet)
#
# 1) Seed parametrelerini gunceller (docNo -> poDocNo)
# 2) Guncel seed DOCX yukler (govde {{poDocNo}}, antet {{docNo}} ayri)
# 3) Yayinlar (opsiyonel)
#
# Kullanim:
#   .\patch-coc-standard-po-docno-test.ps1
#   .\patch-coc-standard-po-docno-test.ps1 -WhatIf
#   .\patch-coc-standard-po-docno-test.ps1 -SkipPublish

param(
    [string]$BaseUrl = "http://192.168.20.20:5040",
    [string]$WopiHost = "http://192.168.20.20:5095",
    [string]$TemplateCode = "COC-STANDARD",
    [string]$Token = $env:DI_TOKEN,
    [switch]$WhatIf = $false,
    [switch]$SkipPublish = $false,
    [switch]$SkipBuild = $false
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
$repoRoot = (Resolve-Path (Join-Path $scriptDir "../../../..")).Path
$seedFile = Join-Path $repoRoot "docs/odak/document_intelligence/datasets/seed-designer-template-coc-standard.json"
$buildScript = Join-Path $scriptDir "build-coc-seed-docx.ps1"
$seedDocx = Join-Path $repoRoot "docs/odak/document_intelligence/sample/ODK-COC-template-seed.docx"

$token = $Token
if ([string]::IsNullOrEmpty($token)) {
    $loadTokenScript = Join-Path $scriptDir "..\..\operationcore\scripts\load-operationcore-token.ps1"
    if (Test-Path $loadTokenScript) { $token = & $loadTokenScript }
}
if ([string]::IsNullOrEmpty($token)) { throw "Token yok." }
$token = $token.Trim()

if (-not $SkipBuild) {
    Write-Host "Seed DOCX uretiliyor..." -ForegroundColor Cyan
    & $buildScript
}
if (-not (Test-Path $seedDocx)) { throw "Seed DOCX yok: $seedDocx" }

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
    param([string]$Method, [string]$RequestUri, [hashtable]$Body = $null)
    if ($Body) {
        $json = $Body | ConvertTo-Json -Depth 12 -Compress
        $bytes = $utf8.GetBytes($json)
        return Invoke-RestMethod -Uri $RequestUri -Headers $headers -Method $Method -Body $bytes -ContentType "application/json; charset=utf-8" -SkipCertificateCheck
    }
    return Invoke-RestMethod -Uri $RequestUri -Headers $headers -Method $Method -SkipCertificateCheck
}

function Get-AllCategories {
    param([object[]]$Nodes, [string]$Prefix = '')
    $all = @()
    foreach ($n in $Nodes) {
        $path = if ($Prefix) { "$Prefix / $($n.name)" } else { [string]$n.name }
        $all += [pscustomobject]@{ id = $n.id; path = $path; name = [string]$n.name }
        if ($n.children) { $all += Get-AllCategories -Nodes @($n.children) -Prefix $path }
    }
    return $all
}

Write-Host "CoC poDocNo patch -> $BaseUrl ($TemplateCode)" -ForegroundColor Cyan
$tree = Invoke-Json -Method GET -RequestUri "$categoriesBase/tree"
$roots = if ($tree -is [System.Array]) { @($tree) } else { @($tree) }
$cat = Get-AllCategories -Nodes $roots | Where-Object { $_.path -like '*CoC*' -or $_.path -like '*Uygunluk*' } | Select-Object -First 1
if (-not $cat) { throw "CoC kategorisi bulunamadi" }
$listUri = "${templatesBase}?categoryId=$([Uri]::EscapeDataString([string]$cat.id))"
$list = Invoke-Json -Method GET -RequestUri $listUri
$tpl = @($list.items) | Where-Object { $_.code -eq $TemplateCode } | Select-Object -First 1
if (-not $tpl) { throw "Sablon bulunamadi: $TemplateCode" }

$detail = Invoke-Json -Method GET -RequestUri "$templatesBase/$($tpl.id)"
Write-Host "Sablon: $($detail.name) id=$($detail.id) status=$($detail.status)" -ForegroundColor DarkGray

if ($detail.status -eq "published") {
    Write-Host "Published -> draft (unpublish)..." -ForegroundColor Yellow
    if (-not $WhatIf) {
        Invoke-Json -Method POST -RequestUri "$templatesBase/$($tpl.id)/unpublish" | Out-Null
    }
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
    Write-Host "WhatIf: parameters + WOPI upload + page-structure + publish" -ForegroundColor Yellow
    exit 0
}

Invoke-Json -Method PUT -RequestUri "$templatesBase/$($tpl.id)/parameters" -Body $paramBody | Out-Null
Write-Host "OK parameters (poDocNo incremental)" -ForegroundColor Green

$bytes = [IO.File]::ReadAllBytes($seedDocx)
$session = Invoke-Json -Method GET -RequestUri "$templatesBase/$($tpl.id)/editor-session"
$putUrl = "$WopiHost/wopi/files/$($tpl.id)/contents?access_token=$([Uri]::EscapeDataString($session.accessToken))"
Invoke-WebRequest -Uri $putUrl -Method POST -Body $bytes -ContentType "application/vnd.openxmlformats-officedocument.wordprocessingml.document" -SkipCertificateCheck -UseBasicParsing | Out-Null
Write-Host "OK WOPI upload (govde {{poDocNo}})" -ForegroundColor Green

$pageStructureBody = @{
    footer = $tplSeed.footer
    pageLayout = $tplSeed.pageLayout
}
if ($detail.defaultLetterheadId) {
    $pageStructureBody.defaultLetterheadId = [string]$detail.defaultLetterheadId
}
Invoke-Json -Method PUT -RequestUri "$templatesBase/$($tpl.id)/page-structure" -Body $pageStructureBody | Out-Null
Write-Host "OK page-structure (antet refresh, {{docNo}} antet)" -ForegroundColor Green

if (-not $SkipPublish) {
    Invoke-Json -Method POST -RequestUri "$templatesBase/$($tpl.id)/publish" | Out-Null
    Write-Host "OK publish" -ForegroundColor Green
}

$verify = Invoke-Json -Method GET -RequestUri "$templatesBase/$($tpl.id)"
$poParam = @($verify.parameters) | Where-Object { $_.key -eq "poDocNo" } | Select-Object -First 1
Write-Host "Dogrulama: status=$($verify.status) poDocNo=$($poParam.key) docNoParam=$(@($verify.parameters | Where-Object { $_.key -eq 'docNo' -and $_.valueSourceMode -ne 'incremental' } | Select-Object -First 1).key)" -ForegroundColor Cyan
