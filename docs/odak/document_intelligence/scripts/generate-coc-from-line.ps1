# Siparis kalemi -> CoC DOCX uret + Document Intelligence klasorune kaydet (pilot, deploy gerektirmez).
#
# Kullanim:
#   .\generate-coc-from-line.ps1 -LineId <odak_siparis_kalemleri dataId>
#   .\generate-coc-from-line.ps1 -LineId <id> -WhatIf
#   .\generate-coc-from-line.ps1 -LineId <id> -SkipUpload

param(
    [Parameter(Mandatory = $true)]
    [string]$LineId,
    [string]$BaseUrl = "http://192.168.20.8:5040",
    [string]$WopiHost = "http://192.168.20.8:5095",
    [string]$Token = "",
    [string]$ConfigFile = "",
    [switch]$WhatIf = $false,
    [switch]$SkipUpload = $false
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
$repoRoot = (Resolve-Path (Join-Path $scriptDir "../../../..")).Path
. (Join-Path $scriptDir "lib/CocDocNoCounter.ps1")
. (Join-Path $scriptDir "lib/CocLineMergeMap.ps1")

if ([string]::IsNullOrWhiteSpace($ConfigFile)) {
    $ConfigFile = Join-Path $repoRoot "docs/odak/document_intelligence/datasets/coc-generate-config.json"
}
$config = [IO.File]::ReadAllText($ConfigFile, [Text.UTF8Encoding]::new($false)) | ConvertFrom-Json

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
$dataBase = "$BaseUrl/data/api/v1/data"
$docsBase = "$BaseUrl/documents/api/v1"
$utf8 = [System.Text.Encoding]::UTF8

function Invoke-DgGet {
    param([string]$Dataset, [string]$Id)
    try {
        return Invoke-RestMethod -Uri "$dataBase/$Dataset/$Id" -Headers $headers -Method GET
    }
    catch {
        throw "DG kayit bulunamadi: $Dataset/$Id"
    }
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

function Get-TemplateByCode {
    param([string]$Code)
    $tree = Invoke-RestMethod "$docsBase/template-categories/tree" -Headers $headers
    $cat = (Get-AllCategories -Nodes @($tree) | Where-Object { $_.path -like '*CoC*' -or $_.path -like '*Uygunluk*' } | Select-Object -First 1)
    if (-not $cat) { throw "CoC kategorisi bulunamadi" }
    $list = Invoke-RestMethod "$docsBase/templates?categoryId=$($cat.id)" -Headers $headers
    $tpl = $list.items | Where-Object { $_.code -eq $Code } | Select-Object -First 1
    if (-not $tpl) { throw "Sablon bulunamadi: $Code" }
    return $tpl
}

function Get-ResourceChildren {
    param([string]$ParentId)
    $q = if ($ParentId) { "?parentId=$([Uri]::EscapeDataString($ParentId))" } else { "" }
    return (Invoke-RestMethod "$docsBase/resources/children$q" -Headers $headers).items
}

function Ensure-ResourceFolder {
    param([string]$ParentId, [string]$Name)
    $items = Get-ResourceChildren -ParentId $ParentId
    $existing = $items | Where-Object { $_.name -eq $Name } | Select-Object -First 1
    if ($existing) { return [string]$existing.id }
    if ($WhatIf) {
        Write-Host "WhatIf: klasor olustur '$Name' (parent=$ParentId)" -ForegroundColor Yellow
        return [Guid]::Empty.ToString()
    }
    $body = @{ name = $Name }
    if ($ParentId) { $body.parentId = $ParentId }
    $json = $body | ConvertTo-Json -Compress
    $bytes = $utf8.GetBytes($json)
    $created = Invoke-RestMethod -Uri "$docsBase/resources/folder" -Headers $headers -Method POST `
        -Body $bytes -ContentType "application/json; charset=utf-8"
    Write-Host "OK klasor: $Name ($($created.id))" -ForegroundColor DarkGray
    return [string]$created.id
}

function Resolve-DiFolderPath {
    param([string[]]$Segments)
    $parentId = $null
    foreach ($segment in $Segments) {
        $parentId = Ensure-ResourceFolder -ParentId $parentId -Name $segment
    }
    return $parentId
}

function Get-TemplateDocxBytes {
    param([string]$TemplateId)
    $session = Invoke-RestMethod "$docsBase/templates/$TemplateId/editor-session" -Headers $headers
    $getUrl = "$WopiHost/wopi/files/$TemplateId/contents?access_token=$([Uri]::EscapeDataString($session.accessToken))"
    return (Invoke-WebRequest -Uri $getUrl -UseBasicParsing).Content
}

function Save-FileResource {
    param(
        [string]$ParentId,
        [string]$FileName,
        [byte[]]$Bytes
    )
    $body = @{
        parentId = $ParentId
        name = $FileName
        originalFileName = $FileName
        mimeType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
        extension = ".docx"
        size = $Bytes.Length
        content = [Convert]::ToBase64String($Bytes)
    }
    $json = $body | ConvertTo-Json -Compress
    $payload = $utf8.GetBytes($json)
    return Invoke-RestMethod -Uri "$docsBase/resources/file" -Headers $headers -Method POST `
        -Body $payload -ContentType "application/json; charset=utf-8"
}

Write-Host "CoC uretim: lineId=$LineId" -ForegroundColor Cyan

$line = Invoke-DgGet -Dataset $config.datasets.lines -Id $LineId.Trim()

$package = $null
$packageId = $null
if ($line.parentPackageId -is [pscustomobject] -and $line.parentPackageId.PSObject.Properties['__dataId']) {
    $packageId = [string]$line.parentPackageId.__dataId
    $package = $line.parentPackageId
}
else {
    $packageId = Get-DgRelationId $line.parentPackageId
}
if (-not $packageId) { throw "Kalemde parentPackageId yok." }
if (-not $package) {
    $package = Invoke-DgGet -Dataset $config.datasets.packages -Id $packageId
}
$customer = $null
$customerId = Get-DgRelationId $package.customerId
if ($customerId) {
    $customer = Invoke-DgGet -Dataset $config.datasets.customers -Id $customerId
}

$product = $null
$productId = Get-DgRelationId $line.productId
if ($productId) {
    try { $product = Invoke-DgGet -Dataset $config.datasets.products -Id $productId } catch { }
}

$docNo = Get-NextCocDocNo
Write-Host "Belge no: $docNo" -ForegroundColor Cyan

$defaults = @{
    documentName = [string]$config.documentName
    signatoryName = [string]$config.signatoryName
    signatoryTitle = [string]$config.signatoryTitle
}
$mergeValues = Build-CocMergeValuesFromLine -Line $line -Package $package -Customer $customer -Product $product -DocNo $docNo -Defaults $defaults

Write-Host "Doldurulan parametreler:" -ForegroundColor DarkGray
$mergeValues.GetEnumerator() | Sort-Object Name | ForEach-Object { Write-Host "  $($_.Key) = $($_.Value)" }

$packageNo = if ($package.packageNo) { [string]$package.packageNo.Trim() } else { $packageId.Substring(0, [Math]::Min(8, $packageId.Length)) }
$lineNo = if ($null -ne $line.lineNo) { [string]$line.lineNo } else { "x" }
$fileName = "$docNo-$packageNo-K$lineNo.docx"

$folderSegments = @($config.diFolderPath) + @($packageNo)
$targetFolderId = $null
if (-not $SkipUpload) {
    $targetFolderId = Resolve-DiFolderPath -Segments $folderSegments
}

if ($WhatIf) {
    Write-Host "WhatIf: $fileName -> DI/$($folderSegments -join '/')" -ForegroundColor Yellow
    exit 0
}

$tpl = Get-TemplateByCode -Code ([string]$config.templateCode)
Write-Host "Sablon: $($tpl.name) ($($tpl.id))" -ForegroundColor DarkGray

$tmp = Join-Path $env:TEMP "coc-gen-$([Guid]::NewGuid().ToString('N'))"
New-Item -ItemType Directory -Path $tmp | Out-Null
try {
    $templatePath = Join-Path $tmp "template.docx"
    $mergedPath = Join-Path $tmp "merged.docx"
    [IO.File]::WriteAllBytes($templatePath, (Get-TemplateDocxBytes -TemplateId $tpl.id))

    & (Join-Path $scriptDir "merge-coc-docx.ps1") -InputDocx $templatePath -OutputDocx $mergedPath -Values $mergeValues
    $bytes = [IO.File]::ReadAllBytes($mergedPath)

    if (-not $SkipUpload) {
        $saved = Save-FileResource -ParentId $targetFolderId -FileName $fileName -Bytes $bytes
        Write-Host "OK DI kayit: $($saved.id) / $fileName" -ForegroundColor Green
        Write-Host "Klasor: $($folderSegments -join ' / ')" -ForegroundColor Cyan
    }
    else {
        $localOut = Join-Path $repoRoot "docs/odak/document_intelligence/sample/$fileName"
        [IO.File]::WriteAllBytes($localOut, $bytes)
        Write-Host "OK yerel: $localOut" -ForegroundColor Green
    }
}
finally {
    if (Test-Path $tmp) { Remove-Item $tmp -Recurse -Force -ErrorAction SilentlyContinue }
}

Write-Host "Tamam." -ForegroundColor Cyan
