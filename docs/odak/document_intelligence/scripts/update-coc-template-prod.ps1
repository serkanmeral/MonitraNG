# Prod COC-STANDARD: parametreli DOCX yukler + antet/altbilgi yeniden enjekte eder.
#
# Adimlar:
#   1) parameterize-coc-docx.ps1 ile guncel seed DOCX uret (veya -SkipParameterize)
#   2) WOPI PutFile ile prod sablon DOCX guncelle
#   3) page-structure PUT (antet + altbilgi tablo + sayfa kenarlari)
#
# Kullanım:
#   .\update-coc-template-prod.ps1
#   .\update-coc-template-prod.ps1 -TemplateCode COC-STANDARD -WhatIf

param(
    [string]$BaseUrl = "http://192.168.20.8:5040",
    [string]$WopiHost = "http://192.168.20.8:5095",
    [string]$TemplateCode = "COC-STANDARD",
    [string]$Token = "",
    [switch]$SkipParameterize = $false,
    [switch]$WhatIf = $false
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
$repoRoot = (Resolve-Path (Join-Path $scriptDir "../../../..")).Path
$paramScript = Join-Path $scriptDir "parameterize-coc-docx.ps1"
$seedFile = Join-Path $repoRoot "docs/odak/document_intelligence/datasets/seed-designer-template-coc-standard.json"
$prodExport = Join-Path $repoRoot "docs/odak/document_intelligence/sample/ODK-COC-prod-current.docx"
$seedDocx = Join-Path $repoRoot "docs/odak/document_intelligence/sample/ODK-COC-template-seed.docx"

$tokenFile = Join-Path $env:TEMP 'operationcore_dg_token_prod.txt'
$token = $Token
if ([string]::IsNullOrEmpty($token) -and (Test-Path $tokenFile)) {
    $token = (Get-Content $tokenFile -Raw).Trim()
}
if ([string]::IsNullOrEmpty($token)) {
    $loadTokenScript = Join-Path $repoRoot "docs/odak/operationcore/scripts/load-operationcore-token-prod.ps1"
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
    param([string]$Method, [string]$Uri, [hashtable]$Body)
    if ($Body) {
        $json = $Body | ConvertTo-Json -Depth 12 -Compress
        $bytes = $utf8.GetBytes($json)
        return Invoke-RestMethod -Uri $Uri -Headers $headers -Method $Method -Body $bytes -ContentType "application/json; charset=utf-8"
    }
    return Invoke-RestMethod -Uri $Uri -Headers $headers -Method $Method
}

function Get-AllCategories {
    param([object[]]$Nodes, [string]$Prefix = '')
    $all = @()
    foreach ($n in $Nodes) {
        $path = if ($Prefix) { "$Prefix / $($n.name)" } else { [string]$n.name }
        $all += [pscustomobject]@{ id = $n.id; path = $path }
        if ($n.children) { $all += Get-AllCategories -Nodes @($n.children) -Prefix $path }
    }
    return $all
}

if (-not $SkipParameterize) {
    if (-not (Test-Path $prodExport)) {
        throw "Prod export yok: $prodExport (once export script calistirin veya prod'dan indirin)"
    }
    Write-Host "Parametreleştirme: $prodExport -> $seedDocx" -ForegroundColor Cyan
    if (-not $WhatIf) {
        & $paramScript -InputDocx $prodExport -OutputDocx $seedDocx
    }
}

if (-not (Test-Path $seedDocx)) { throw "Seed DOCX yok: $seedDocx" }

$tree = Invoke-RestMethod "$BaseUrl/documents/api/v1/template-categories/tree" -Headers $headers
$cat = (Get-AllCategories -Nodes @($tree) | Where-Object { $_.path -like '*CoC*' -or $_.path -like '*Uygunluk*' } | Select-Object -First 1)
if (-not $cat) { throw "CoC kategorisi bulunamadi" }

$list = Invoke-RestMethod "$BaseUrl/documents/api/v1/templates?categoryId=$($cat.id)" -Headers $headers
$tpl = $list.items | Where-Object { $_.code -eq $TemplateCode } | Select-Object -First 1
if (-not $tpl) { throw "Sablon bulunamadi: $TemplateCode" }

Write-Host "Hedef sablon: $($tpl.name) ($($tpl.id))" -ForegroundColor Cyan
$bytes = [IO.File]::ReadAllBytes($seedDocx)
Write-Host "DOCX boyutu: $($bytes.Length) bytes" -ForegroundColor DarkGray

if ($WhatIf) {
    Write-Host "WhatIf: WOPI upload + page-structure" -ForegroundColor Yellow
    exit 0
}

$session = Invoke-RestMethod "$BaseUrl/documents/api/v1/templates/$($tpl.id)/editor-session" -Headers $headers
$putUrl = "$WopiHost/wopi/files/$($tpl.id)/contents?access_token=$([Uri]::EscapeDataString($session.accessToken))"
Invoke-WebRequest -Uri $putUrl -Method POST -Body $bytes -ContentType "application/vnd.openxmlformats-officedocument.wordprocessingml.document" -UseBasicParsing | Out-Null
Write-Host "OK WOPI upload" -ForegroundColor Green

$seed = [IO.File]::ReadAllText($seedFile, [Text.UTF8Encoding]::new($false)) | ConvertFrom-Json
$pageStructureBody = @{
    letterhead = $seed.template.letterhead
    footer = $seed.template.footer
}
if ($seed.template.pageLayout) {
    $pageStructureBody.pageLayout = $seed.template.pageLayout
}
Invoke-Json -Method PUT -Uri "$BaseUrl/documents/api/v1/templates/$($tpl.id)/page-structure" -Body $pageStructureBody | Out-Null
Write-Host "OK page-structure (antet + altbilgi tablo + kenar bosluklari)" -ForegroundColor Green

$struct = Invoke-RestMethod "$BaseUrl/documents/api/v1/templates/$($tpl.id)/source/structure" -Headers $headers
Write-Host "Placeholder sayisi: $($struct.placeholders.Count)" -ForegroundColor Cyan
if ($struct.placeholderWarnings) {
    foreach ($w in $struct.placeholderWarnings) { Write-Host "  WARN: $w" -ForegroundColor Yellow }
}
$struct.placeholders | Select-Object -First 20 key, occurrenceCount | Format-Table

Write-Host "Guncelleme tamam." -ForegroundColor Cyan
