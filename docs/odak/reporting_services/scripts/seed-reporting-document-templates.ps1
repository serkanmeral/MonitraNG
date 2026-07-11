# Raporlama DI kategori + 2 Odak Eğitim rapor belge şablonu (idempotent upsert).
#
# Kullanım:
#   .\docs\odak\reporting_services\scripts\seed-reporting-document-templates.ps1
#   .\docs\odak\reporting_services\scripts\seed-reporting-document-templates.ps1 -BaseUrl "http://192.168.20.20:5040" -Replace

param(
    [string]$BaseUrl = "http://192.168.20.20:5040",
    [string]$Token = $env:DI_TOKEN,
    [string]$SeedFile = "",
    [switch]$WhatIf = $false,
    [switch]$Replace = $false
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
$repoRoot = (Resolve-Path (Join-Path $scriptDir "../../../..")).Path
if ([string]::IsNullOrWhiteSpace($SeedFile)) {
    $seedFile = Join-Path $repoRoot "docs/odak/reporting_services/datasets/seed-reporting-document-templates.json"
}
else {
    $seedFile = if ([IO.Path]::IsPathRooted($SeedFile)) { $SeedFile } else { Join-Path $repoRoot $SeedFile }
}
$diScripts = Join-Path $repoRoot "docs/odak/document_intelligence/scripts"
. (Join-Path $diScripts "lib/Convert-DiTemplateParameters.ps1")

$token = $Token
$isProd = $BaseUrl -match "192\.168\.20\.8"
if ([string]::IsNullOrEmpty($token)) {
    $serkan = Join-Path $env:TEMP "serkan_token.txt"
    if (Test-Path $serkan) { $token = (Get-Content $serkan -Raw).Trim() }
}
if ([string]::IsNullOrEmpty($token)) {
    $loadTokenScript = if ($isProd) {
        Join-Path $repoRoot "docs/odak/operationcore/scripts/load-operationcore-token-prod.ps1"
    } else {
        Join-Path $repoRoot "docs/odak/operationcore/scripts/load-operationcore-token.ps1"
    }
    if (Test-Path $loadTokenScript) { $token = & $loadTokenScript }
}
if ([string]::IsNullOrEmpty($token)) {
    Write-Host "Token yok. -Token, `$env:DI_TOKEN, serkan_token.txt veya OC token script." -ForegroundColor Red
    exit 1
}
$token = $token.Trim()

if (-not (Test-Path $seedFile)) { throw "Seed dosyasi yok: $seedFile" }

$seed = [IO.File]::ReadAllText($seedFile, [Text.UTF8Encoding]::new($false)) | ConvertFrom-Json
$categoryName = [string]$seed.categoryName
if ([string]::IsNullOrWhiteSpace($categoryName)) { $categoryName = "Raporlama" }

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

function Ensure-ReportingCategory {
    $tree = Invoke-Json -Method GET -Uri "$categoriesBase/tree"
    $roots = if ($tree -is [System.Array]) { @($tree) } else { @($tree) }
    foreach ($n in $roots) {
        if ($n.name -eq $categoryName) {
            return [string]$n.id
        }
    }
    if ($WhatIf) {
        Write-Host "WhatIf POST category '$categoryName'" -ForegroundColor Yellow
        return "whatif-category"
    }
    $created = Invoke-Json -Method POST -Uri $categoriesBase -Body @{
        name     = $categoryName
        parentId = $null
    }
    Write-Host "OK category create id=$($created.id)" -ForegroundColor Green
    return [string]$created.id
}

function Find-TemplateByCode {
    param([string]$CategoryId, [string]$Code)
    $uri = "$templatesBase" + "?categoryId=" + [Uri]::EscapeDataString($CategoryId)
    $res = Invoke-Json -Method GET -Uri $uri
    $items = @($res.items)
    foreach ($item in $items) {
        if ($item.code -eq $Code) { return $item }
    }
    # Fallback: list all
    $all = Invoke-Json -Method GET -Uri $templatesBase
    foreach ($item in @($all.items)) {
        if ($item.code -eq $Code) { return $item }
    }
    return $null
}

function Ensure-SourceFile {
    param([object]$Tpl)
    $fileName = $null
    if ($Tpl.docxFile) { $fileName = [string]$Tpl.docxFile }
    elseif ($Tpl.xlsxFile) { $fileName = [string]$Tpl.xlsxFile }
    elseif ($Tpl.fileName) { $fileName = [string]$Tpl.fileName }
    if ([string]::IsNullOrWhiteSpace($fileName)) { throw "Sablon dosya adi yok (docxFile/xlsxFile)" }
    $path = Join-Path $repoRoot "docs/odak/document_intelligence/sample/$fileName"
    if (Test-Path $path) { return $path }
    $buildScript = Join-Path $scriptDir "build-reporting-document-templates-xlsx.ps1"
    if (-not (Test-Path $buildScript)) { throw "Kaynak yok ve build script bulunamadi: $path" }
    & $buildScript
    if (-not (Test-Path $path)) { throw "Kaynak uretilemedi: $path" }
    return $path
}

Write-Host "Reporting document templates seed -> $BaseUrl" -ForegroundColor Cyan
$categoryId = Ensure-ReportingCategory
Write-Host "Kategori id: $categoryId ($categoryName)" -ForegroundColor DarkGray

$results = @()

foreach ($tpl in @($seed.templates)) {
    $code = [string]$tpl.code
    $sourcePath = Ensure-SourceFile -Tpl $tpl
    $fileName = [IO.Path]::GetFileName($sourcePath)
    $existing = Find-TemplateByCode -CategoryId $categoryId -Code $code

    if ($existing) {
        if (-not $Replace) {
            Write-Host "SKIP: '$code' zaten var (id=$($existing.id)). Yeniden seed icin -Replace." -ForegroundColor Yellow
            $results += [pscustomobject]@{ code = $code; id = [string]$existing.id; action = "skip" }
            continue
        }
        if ($WhatIf) {
            Write-Host "WhatIf DELETE + recreate '$code' (id=$($existing.id))" -ForegroundColor Yellow
            continue
        }
        Write-Host "Replace: mevcut '$code' siliniyor (id=$($existing.id))..." -ForegroundColor Yellow
        Invoke-Json -Method DELETE -Uri "$templatesBase/$($existing.id)" | Out-Null
    }

    $bytes = [IO.File]::ReadAllBytes($sourcePath)
    $contentB64 = [Convert]::ToBase64String($bytes)
    $createBody = @{
        categoryId  = $categoryId
        name        = [string]$tpl.name
        description = [string]$tpl.description
        content     = $contentB64
        fileName    = $fileName
        size        = $bytes.Length
    }

    if ($WhatIf) {
        Write-Host "WhatIf POST from-reference '$($tpl.name)' ($code)" -ForegroundColor Yellow
        continue
    }

    $created = Invoke-Json -Method POST -Uri "$templatesBase/from-reference" -Body $createBody
    $templateId = [string]$created.id
    Write-Host "OK create $code id=$templateId" -ForegroundColor Green

    Invoke-Json -Method PUT -Uri "$templatesBase/$templateId/metadata" -Body @{
        name = [string]$tpl.name
        code = $code
    } | Out-Null

    $params = ConvertTo-DiTemplateParameterEntries -Parameters @($tpl.parameters)
    Invoke-Json -Method PUT -Uri "$templatesBase/$templateId/parameters" -Body @{
        parameters = $params
    } | Out-Null
    Write-Host "OK parameters ($($params.Count))" -ForegroundColor Green

    if ($tpl.letterhead) {
        Invoke-Json -Method PUT -Uri "$templatesBase/$templateId/letterhead" -Body @{
            letterhead = $tpl.letterhead
        } | Out-Null
    }

    try {
        Invoke-Json -Method POST -Uri "$templatesBase/$templateId/publish" | Out-Null
        Write-Host "OK publish $code" -ForegroundColor Green
    } catch {
        Write-Host "WARN publish failed for $code : $_" -ForegroundColor Yellow
    }

    $results += [pscustomobject]@{ code = $code; id = $templateId; action = "created" }
}

Write-Host "`nSeed tamam." -ForegroundColor Cyan
$results | Format-Table -AutoSize
Write-Host "Rapor baglari icin templateCode kullanin: RPT_ODAK_EGITIM_LIST / RPT_ODAK_EGITIM_PERSON" -ForegroundColor DarkGray
