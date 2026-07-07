# Smoke: O-0 / O-1 / S2 — native XLSX + editor + export/pdf (test gateway)
param(
    [string]$Gateway = "http://192.168.20.20:5040",
    [string]$TokenFile = "$env:TEMP\operationcore_dg_token.txt",
    [switch]$KeepArtifacts
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../../..")).Path
$loadToken = Join-Path $repoRoot "docs/odak/operationcore/scripts/load-operationcore-token.ps1"

$stamp = Get-Date -Format "yyyyMMddHHmmss"
$sheetName = "Smoke Sheet $stamp"
$sheetCode = "SMK-SHT-$stamp"

function Get-Token {
    if (Test-Path $TokenFile) {
        $t = (Get-Content $TokenFile -Raw).Trim()
        if ($t) { return $t }
    }
    $fresh = & $loadToken -AutoRefresh
    if ($fresh) { return $fresh.Trim() }
    throw "Token alinamadi."
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

function Get-NodeId([object]$node) {
    if ($null -eq $node) { return $null }
    if ($node -is [System.Array]) { $node = $node[0] }
    $id = $node.id
    if ($id -is [System.Array]) { return [string]$id[0] }
    return [string]$id
}

function Find-AnyFolder {
    $roots = @(Invoke-Docs -Path "/resources/tree/roots")
    $docsRoot = $roots | Where-Object { $_.name -match 'D.k.man' } | Select-Object -First 1
    if ($docsRoot) {
        $children = @(Invoke-Docs -Path "/resources/tree/children?parentId=$([uri]::EscapeDataString($(Get-NodeId $docsRoot)))")
        $folder = @($children | Select-Object -First 1)[0]
        if ($folder) { return $folder }
    }
    foreach ($root in $roots) {
        $children = @(Invoke-Docs -Path "/resources/tree/children?parentId=$([uri]::EscapeDataString($(Get-NodeId $root)))")
        $folder = @($children | Select-Object -First 1)[0]
        if ($folder) { return $folder }
    }
    return @($roots | Select-Object -First 1)[0]
}

$token = Get-Token
$script:Headers = @{ Authorization = "Bearer $token" }

Write-Host "== Smoke: native sheet (O-0/S1/S2) ==" -ForegroundColor Cyan

$folder = Find-AnyFolder
if (-not $folder) { throw "No folder found for sheet create." }
$parentId = Get-NodeId $folder
Write-Host "Folder: $($folder.name) ($parentId)"

Write-Host "1) sheets/native" -ForegroundColor Yellow
$created = Invoke-Docs -Method POST -Path "/resources/sheets/native" -Body @{
    parentId   = $parentId
    name       = $sheetName
    documentNo = $sheetCode
}
if (-not $created.id) { throw "Sheet create returned no id." }
Write-Host "  OK sheet $($created.id)" -ForegroundColor Green

$detail = Invoke-Docs -Path "/resources/$($created.id)"
$ext = ($detail.extension ?? '').ToString().TrimStart('.').ToLowerInvariant()
$mime = ($detail.mimeType ?? '').ToString()
if ($ext -ne 'xlsx' -and $mime -notmatch 'spreadsheetml') {
    throw "Expected xlsx resource, got ext=$ext mime=$mime"
}
Write-Host "2) MIME/extension" -ForegroundColor Yellow
Write-Host "  OK xlsx" -ForegroundColor Green

Write-Host "3) editor-session" -ForegroundColor Yellow
$session = Invoke-Docs -Path "/resources/$($created.id)/editor-session"
if (-not $session.editorUrl) { throw "Editor session missing editorUrl." }
Write-Host "  OK editor-session" -ForegroundColor Green

Write-Host "4) export/pdf (sheet)" -ForegroundColor Yellow
$pdfResp = Invoke-Docs -Path "/resources/$([uri]::EscapeDataString($created.id))/export/pdf" -RawBytes
if ($pdfResp.StatusCode -ne 200) { throw "export/pdf HTTP $($pdfResp.StatusCode)" }
$pdfBytes = $pdfResp.Content
if ($pdfBytes.Length -lt 100 -or $pdfBytes[0] -ne 0x25 -or $pdfBytes[1] -ne 0x50) {
    throw "export/pdf gecerli PDF donmedi (len=$($pdfBytes.Length))"
}
Write-Host "  OK export/pdf bytes=$($pdfBytes.Length)" -ForegroundColor Green

Write-Host "5) versions + preview-session (sheet)" -ForegroundColor Yellow
$versions = @(Invoke-Docs -Path "/resources/$($created.id)/versions")
if ($versions.Count -lt 1) { throw "Sheet versions missing." }
$preview = Invoke-Docs -Path "/resources/$($created.id)/versions/1/preview-session"
if (-not $preview.editorUrl) { throw "Sheet preview-session missing editorUrl." }
Write-Host "  OK versions/preview" -ForegroundColor Green

Write-Host "6) presentations/native (API)" -ForegroundColor Yellow
$pptName = "Smoke Presentation $stamp"
$ppt = Invoke-Docs -Method POST -Path "/resources/presentations/native" -Body @{
    parentId   = $parentId
    name       = $pptName
    documentNo = "SMK-PPT-$stamp"
}
$pptExt = ($ppt.extension ?? '').ToString().TrimStart('.').ToLowerInvariant()
if ($pptExt -ne 'pptx') { throw "Expected pptx, got $pptExt" }
Write-Host "  OK ppt $($ppt.id)" -ForegroundColor Green

if (-not $KeepArtifacts) {
    Write-Host "7) cleanup" -ForegroundColor Yellow
    foreach ($rid in @($created.id, $ppt.id)) {
        try {
            Invoke-Docs -Path "/resources/$([uri]::EscapeDataString($rid))" -Method DELETE | Out-Null
            Write-Host "  OK silindi $rid" -ForegroundColor Green
        }
        catch {
            Write-Host "  Uyari: $rid silinemedi — $($_.Exception.Message)" -ForegroundColor DarkYellow
        }
    }
}

Write-Host "PASS (6/6)" -ForegroundColor Green
