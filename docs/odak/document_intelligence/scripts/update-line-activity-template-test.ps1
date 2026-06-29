# Test: LINE-ACTIVITY-STD DOCX guncelle (profesyonel govde) + antet/altbilgi.
#
# Kullanim:
#   .\docs\odak\document_intelligence\scripts\build-line-activity-seed-docx.ps1
#   .\docs\odak\document_intelligence\scripts\update-line-activity-template-test.ps1

param(
    [string]$BaseUrl = "http://192.168.20.20:5040",
    [string]$WopiHost = "http://192.168.20.20:5095",
    [string]$TemplateCode = "LINE-ACTIVITY-STD",
    [string]$Token = $env:DI_TOKEN,
    [switch]$SkipBuild = $false,
    [switch]$WhatIf = $false
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
$repoRoot = (Resolve-Path (Join-Path $scriptDir "../../../..")).Path
$buildScript = Join-Path $scriptDir "build-line-activity-seed-docx.ps1"
$footerScript = Join-Path $scriptDir "inject-coc-footer-docx.ps1"
$seedFile = Join-Path $repoRoot "docs/odak/document_intelligence/datasets/seed-designer-template-line-activity-standard.json"
$seedDocx = Join-Path $repoRoot "docs/odak/document_intelligence/sample/ODK-LINE-ACTIVITY-template-seed.docx"
$uploadDocx = Join-Path $repoRoot "docs/odak/document_intelligence/sample/ODK-LINE-ACTIVITY-template-upload.docx"

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
    $tree = Invoke-Json -Method GET -Uri "$BaseUrl/documents/api/v1/template-categories/tree"
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

if (-not $SkipBuild) {
    Write-Host "Govde DOCX uretiliyor..." -ForegroundColor Cyan
    & $buildScript
}

if (-not (Test-Path $seedDocx)) { throw "Seed DOCX yok: $seedDocx" }

Write-Host "Footer tablo enjekte ediliyor..." -ForegroundColor Cyan
if (-not $WhatIf) {
    & $footerScript -InputDocx $seedDocx -OutputDocx $uploadDocx -SeedJson $seedFile
}
$docxForUpload = if ((Test-Path $uploadDocx) -and -not $WhatIf) { $uploadDocx } else { $seedDocx }

$seed = [IO.File]::ReadAllText($seedFile, [Text.UTF8Encoding]::new($false)) | ConvertFrom-Json
$categoryId = Find-CategoryByPath -Path @($seed.categoryPath | ForEach-Object { [string]$_ })
$listUri = "$BaseUrl/documents/api/v1/templates?categoryId=" + [Uri]::EscapeDataString($categoryId)
$list = Invoke-Json -Method GET -Uri $listUri
$tpl = @($list.items) | Where-Object { $_.code -eq $TemplateCode } | Select-Object -First 1
if (-not $tpl) { throw "Sablon bulunamadi: $TemplateCode (once seed -Replace calistirin)" }

Write-Host "Hedef: $($tpl.name) id=$($tpl.id) status=$($tpl.status)" -ForegroundColor Cyan
if ($tpl.status -eq "published") {
    throw "Sablon published; WOPI yazilamaz. deploy-line-activity-design-test.ps1 veya seed -Replace kullanin."
}

$bytes = [IO.File]::ReadAllBytes($docxForUpload)
Write-Host "DOCX: $($bytes.Length) bytes" -ForegroundColor DarkGray

if ($WhatIf) {
    Write-Host "WhatIf: WOPI upload + page-structure + parameters" -ForegroundColor Yellow
    exit 0
}

$session = Invoke-Json -Method GET -Uri "$BaseUrl/documents/api/v1/templates/$($tpl.id)/editor-session"
if ($session.readOnly) {
    throw "Editor oturumu salt okunur; once draft sablon olusturun (seed -Replace)."
}
$putUrl = "$WopiHost/wopi/files/$($tpl.id)/contents?access_token=$([Uri]::EscapeDataString($session.accessToken))"
Invoke-WebRequest -Uri $putUrl -Method POST -Body $bytes -ContentType "application/vnd.openxmlformats-officedocument.wordprocessingml.document" -UseBasicParsing | Out-Null
Write-Host "OK WOPI upload" -ForegroundColor Green

$pageStructureBody = @{
    letterhead = $seed.template.letterhead
    footer = $seed.template.footer
}
if ($seed.template.pageLayout) { $pageStructureBody.pageLayout = $seed.template.pageLayout }
Invoke-Json -Method PUT -Uri "$BaseUrl/documents/api/v1/templates/$($tpl.id)/page-structure" -Body $pageStructureBody | Out-Null
Write-Host "OK page-structure" -ForegroundColor Green

# Footer tablo yeniden (page-structure sonrasi)
$session2 = Invoke-Json -Method GET -Uri "$BaseUrl/documents/api/v1/templates/$($tpl.id)/editor-session"
$getUrl = "$WopiHost/wopi/files/$($tpl.id)/contents?access_token=$([Uri]::EscapeDataString($session2.accessToken))"
$currentBytes = (Invoke-WebRequest -Uri $getUrl -UseBasicParsing).Content
$tmpBranded = Join-Path $env:TEMP "act-branded-$([Guid]::NewGuid().ToString('N')).docx"
$tmpFooter = Join-Path $env:TEMP "act-footer-$([Guid]::NewGuid().ToString('N')).docx"
[IO.File]::WriteAllBytes($tmpBranded, $currentBytes)
& $footerScript -InputDocx $tmpBranded -OutputDocx $tmpFooter -SeedJson $seedFile
$footerBytes = [IO.File]::ReadAllBytes($tmpFooter)
$putUrl2 = "$WopiHost/wopi/files/$($tpl.id)/contents?access_token=$([Uri]::EscapeDataString($session2.accessToken))"
Invoke-WebRequest -Uri $putUrl2 -Method POST -Body $footerBytes -ContentType "application/vnd.openxmlformats-officedocument.wordprocessingml.document" -UseBasicParsing | Out-Null
Write-Host "OK footer tablo" -ForegroundColor Green
Remove-Item $tmpBranded, $tmpFooter -Force -ErrorAction SilentlyContinue

$params = @()
foreach ($p in @($seed.template.parameters)) {
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
Invoke-Json -Method PUT -Uri "$BaseUrl/documents/api/v1/templates/$($tpl.id)/parameters" -Body @{
    parameters = $params
    primaryContextType = [string]$seed.template.primaryContextType
    generationProfile = [string]$seed.template.generationProfile
} | Out-Null
Write-Host "OK parameters ($($params.Count))" -ForegroundColor Green

$struct = Invoke-Json -Method GET -Uri "$BaseUrl/documents/api/v1/templates/$($tpl.id)/source/structure"
Write-Host "Placeholder: $($struct.placeholders.Count)" -ForegroundColor Cyan
if ($struct.placeholderWarnings) {
    foreach ($w in $struct.placeholderWarnings) { Write-Host "  WARN: $w" -ForegroundColor Yellow }
}
Write-Host "Guncelleme tamam." -ForegroundColor Green
