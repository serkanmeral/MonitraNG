# Smoke: D4 — şablondan manuel üretim (test gateway)
# Kabul: preview-generation, preview-session (Collabora), generate, export/pdf
param(
    [string]$Gateway = "http://192.168.20.20:5040",
    [string]$TokenFile = "$env:TEMP\operationcore_dg_token.txt",
    [string]$TemplateCode = "COC-STANDARD",
    [string[]]$FolderPath = @("Dökümanlar", "Kalite"),
    [switch]$KeepArtifacts
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../../..")).Path
$loadToken = Join-Path $repoRoot "docs/odak/operationcore/scripts/load-operationcore-token.ps1"

$stamp = Get-Date -Format "yyyyMMddHHmmss"
$docName = "_smoke_d4_$stamp"

function Get-Token {
    if (Test-Path $TokenFile) {
        $t = (Get-Content $TokenFile -Raw).Trim()
        if ($t) { return $t }
    }
    return (& $loadToken).Trim()
}

function Invoke-Docs {
    param(
        [string]$Method = "GET",
        [string]$Path,
        [object]$Body = $null,
        [switch]$RawBytes
    )
    $uri = "$Gateway/documents/api/v1$Path"
    $params = @{
        Uri        = $uri
        Method     = $Method
        Headers    = $script:Headers
        TimeoutSec = 120
    }
    if ($Body -ne $null) {
        $params.ContentType = "application/json"
        $params.Body = ($Body | ConvertTo-Json -Depth 10 -Compress)
    }
    if ($RawBytes) {
        return Invoke-WebRequest @params -SkipCertificateCheck
    }
    return Invoke-RestMethod @params -SkipCertificateCheck
}

function Find-FolderByPath {
    param([string[]]$Segments)
    if ($Segments.Count -eq 0) { return $null }

    $nodes = @(Invoke-Docs -Path "/resources/tree/roots")
    $current = $null
    foreach ($seg in $Segments) {
        $parentId = if ($current) { $current.id } else { $null }
        $children = if ($parentId) {
            @(Invoke-Docs -Path "/resources/tree/children?parentId=$([uri]::EscapeDataString($parentId))")
        } else {
            $nodes
        }
        $current = $children | Where-Object { $_.name -eq $seg } | Select-Object -First 1
        if (-not $current) { return $null }
    }
    return $current
}

function Find-AnyFolder {
    $roots = @(Invoke-Docs -Path "/resources/tree/roots")
    $docsRoot = $roots | Where-Object { $_.name -match 'D.k.man' } | Select-Object -First 1
    if ($docsRoot) {
        $children = @(Invoke-Docs -Path "/resources/tree/children?parentId=$([uri]::EscapeDataString($docsRoot.id))")
        $folder = $children | Select-Object -First 1
        if ($folder) { return $folder }
    }
    foreach ($root in $roots) {
        $children = @(Invoke-Docs -Path "/resources/tree/children?parentId=$([uri]::EscapeDataString($root.id))")
        $folder = $children | Select-Object -First 1
        if ($folder) { return $folder }
    }
    return $null
}

function Find-PublishedTemplate {
    param([string]$Code)
    $list = Invoke-Docs -Path "/templates"
    $items = @($list.items)
    if ($Code) {
        $match = $items | Where-Object {
            ($_.code -eq $Code) -and (($_.status ?? "").ToLower() -eq "published")
        } | Select-Object -First 1
        if ($match) { return $match }
    }
    return $items | Where-Object { (($_.status ?? "").ToLower() -eq "published") } | Select-Object -First 1
}

$token = Get-Token
if ([string]::IsNullOrWhiteSpace($token)) { throw "Token alinamadi." }
$script:Headers = @{ Authorization = "Bearer $token" }

Write-Host ""
Write-Host "DI smoke D4: generate-from-template ($Gateway)" -ForegroundColor Cyan
Write-Host ""

$template = Find-PublishedTemplate -Code $TemplateCode
if (-not $template) { throw "Yayimlanmis sablon bulunamadi (code=$TemplateCode)." }
$templateId = $template.id
Write-Host "  Sablon: $($template.name) ($($template.code)) id=$templateId" -ForegroundColor Gray

Write-Host "1) preview-generation" -ForegroundColor Yellow
$preview = Invoke-Docs -Path "/templates/$([uri]::EscapeDataString($templateId))/preview-generation" -Method POST -Body @{
    documentName = $docName
    overrides    = @{ signatoryName = "Smoke Test" }
}
if (-not $preview.values) { throw "preview-generation values bos." }
Write-Host "  OK preview-generation (keys=$($preview.values.PSObject.Properties.Count))" -ForegroundColor Green

Write-Host "2) preview-session (Collabora)" -ForegroundColor Yellow
$session = Invoke-Docs -Path "/templates/$([uri]::EscapeDataString($templateId))/preview-session" -Method POST -Body @{
    documentName = $docName
    overrides    = @{ signatoryName = "Smoke Test" }
}
if ([string]::IsNullOrWhiteSpace($session.editorUrl)) { throw "preview-session editorUrl bos." }
if ([string]::IsNullOrWhiteSpace($session.accessToken)) { throw "preview-session accessToken bos." }
if (-not $session.readOnly) { throw "preview-session readOnly=false beklenmiyordu." }
Write-Host "  OK preview-session editorUrl len=$($session.editorUrl.Length)" -ForegroundColor Green

Write-Host "3) hedef klasor" -ForegroundColor Yellow
$folder = Find-FolderByPath -Segments $FolderPath
if (-not $folder) {
    Write-Host "  Uyari: $($FolderPath -join '/') bulunamadi, ilk klasor deneniyor" -ForegroundColor DarkYellow
    $folder = Find-AnyFolder
}
if (-not $folder) { throw "Hedef klasor bulunamadi." }
Write-Host "  OK folder: $($folder.name) id=$($folder.id)" -ForegroundColor Green

Write-Host "4) generate" -ForegroundColor Yellow
$generated = Invoke-Docs -Path "/templates/$([uri]::EscapeDataString($templateId))/generate" -Method POST -Body @{
    parentFolderId = $folder.id
    documentName   = $docName
    overrides      = @{ signatoryName = "Smoke Test" }
}
if ([string]::IsNullOrWhiteSpace($generated.resourceId)) { throw "generate resourceId bos." }
if ($generated.profileCode -ne "di.manual") { throw "profileCode di.manual degil: $($generated.profileCode)" }
Write-Host "  OK generate resourceId=$($generated.resourceId) file=$($generated.fileName)" -ForegroundColor Green

Write-Host "5) export/pdf" -ForegroundColor Yellow
$pdfResp = Invoke-Docs -Path "/resources/$([uri]::EscapeDataString($generated.resourceId))/export/pdf" -RawBytes
if ($pdfResp.StatusCode -ne 200) { throw "export/pdf HTTP $($pdfResp.StatusCode)" }
$pdfBytes = $pdfResp.Content
if ($pdfBytes.Length -lt 100 -or $pdfBytes[0] -ne 0x25 -or $pdfBytes[1] -ne 0x50) {
    throw "export/pdf gecerli PDF donmedi (len=$($pdfBytes.Length))"
}
Write-Host "  OK export/pdf bytes=$($pdfBytes.Length)" -ForegroundColor Green

if (-not $KeepArtifacts) {
    Write-Host "6) cleanup (smoke kaydi)" -ForegroundColor Yellow
    try {
        Invoke-Docs -Path "/resources/$([uri]::EscapeDataString($generated.resourceId))" -Method DELETE | Out-Null
        Write-Host "  OK silindi" -ForegroundColor Green
    }
    catch {
        Write-Host "  Uyari: silinemedi — $($_.Exception.Message)" -ForegroundColor DarkYellow
    }
}

Write-Host ""
Write-Host "D4 smoke PASSED" -ForegroundColor Green
