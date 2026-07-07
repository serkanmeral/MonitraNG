# Smoke: Pr2 — native PPTX sürüm geçmişi + PDF + upload editör engeli (test gateway)
param(
    [string]$Gateway = "http://192.168.20.20:5040",
    [string]$TokenFile = "$env:TEMP\operationcore_dg_token.txt",
    [switch]$KeepArtifacts
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../../..")).Path
$loadToken = Join-Path $repoRoot "docs/odak/operationcore/scripts/load-operationcore-token.ps1"

$stamp = Get-Date -Format "yyyyMMddHHmmss"
$pptName = "Smoke Pr2 $stamp"
$pptCode = "SMK-PR2-$stamp"
$uploadName = "Upload PPTX $stamp"

$script:ArtifactIds = @()

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
        [switch]$RawBytes,
        [switch]$AllowError
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
        if ($AllowError) {
            return Invoke-WebRequest @params -SkipCertificateCheck -SkipHttpErrorCheck
        }
        return Invoke-WebRequest @params -SkipCertificateCheck
    }
    if ($AllowError) {
        return Invoke-RestMethod @params -SkipCertificateCheck -SkipHttpErrorCheck
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

function Assert-ZipOfficeBytes([byte[]]$bytes, [string]$label) {
    if ($bytes.Length -lt 4 -or $bytes[0] -ne 0x50 -or $bytes[1] -ne 0x4B) {
        throw "$label gecerli Office zip degil (len=$($bytes.Length))"
    }
}

function Assert-PdfBytes([byte[]]$bytes) {
    if ($bytes.Length -lt 100 -or $bytes[0] -ne 0x25 -or $bytes[1] -ne 0x50) {
        throw "export/pdf gecerli PDF donmedi (len=$($bytes.Length))"
    }
}

$token = Get-Token
$script:Headers = @{ Authorization = "Bearer $token" }

function Clear-EditorSessions {
    try {
        $stats = Invoke-Docs -Path "/editor-sessions/stats"
        $sessions = @($stats.sessions)
        foreach ($s in $sessions) {
            $tok = ($s.accessToken ?? '').ToString().Trim()
            if (-not $tok) { continue }
            try {
                Invoke-Docs -Method DELETE -Path "/editor-sessions/$([uri]::EscapeDataString($tok))" | Out-Null
            }
            catch {
                # best-effort
            }
        }
        if ($sessions.Count -gt 0) {
            Write-Host "  Editor oturumlari temizlendi: $($sessions.Count)" -ForegroundColor DarkGray
        }
    }
    catch {
        Write-Host "  Editor oturum temizligi atlandi" -ForegroundColor DarkGray
    }
}

Write-Host "== Smoke: presentation Pr2 (surum + PDF) ==" -ForegroundColor Cyan
Clear-EditorSessions

$folder = Find-AnyFolder
if (-not $folder) { throw "No folder found." }
$parentId = Get-NodeId $folder
Write-Host "Folder: $($folder.name) ($parentId)"

Write-Host "1) presentations/native" -ForegroundColor Yellow
$created = Invoke-Docs -Method POST -Path "/resources/presentations/native" -Body @{
    parentId   = $parentId
    name       = $pptName
    documentNo = $pptCode
}
if (-not $created.id) { throw "Presentation create returned no id." }
$script:ArtifactIds += $created.id
Write-Host "  OK ppt $($created.id)" -ForegroundColor Green

Write-Host "2) versions list" -ForegroundColor Yellow
$versions = @(Invoke-Docs -Path "/resources/$($created.id)/versions")
if ($versions.Count -lt 1) { throw "Expected at least one version." }
$v1 = $versions | Where-Object { $_.versionNumber -eq 1 } | Select-Object -First 1
if (-not $v1) { throw "Version 1 missing." }
Write-Host "  OK versions=$($versions.Count)" -ForegroundColor Green

Write-Host "3) changeNote PATCH v1" -ForegroundColor Yellow
$note = "Pr2 smoke $stamp"
$patched = Invoke-Docs -Method PATCH -Path "/resources/$($created.id)/versions/1" -Body @{
    changeNote = $note
}
if (($patched.changeNote ?? '') -ne $note) { throw "changeNote PATCH failed." }
Write-Host "  OK changeNote" -ForegroundColor Green

Write-Host "4) versions/1/preview-session" -ForegroundColor Yellow
Clear-EditorSessions
$preview = Invoke-Docs -Path "/resources/$($created.id)/versions/1/preview-session"
if (-not $preview.editorUrl) { throw "preview-session missing editorUrl." }
Write-Host "  OK preview-session" -ForegroundColor Green

Write-Host "5) versions/1/download" -ForegroundColor Yellow
$dl = Invoke-Docs -Path "/resources/$($created.id)/versions/1/download" -RawBytes
if ($dl.StatusCode -ne 200) { throw "version download HTTP $($dl.StatusCode)" }
Assert-ZipOfficeBytes $dl.Content "version download"
Write-Host "  OK download bytes=$($dl.Content.Length)" -ForegroundColor Green
$pptxBytes = $dl.Content

Write-Host "6) editor-session (native)" -ForegroundColor Yellow
Clear-EditorSessions
$session = Invoke-Docs -Path "/resources/$($created.id)/editor-session"
if (-not $session.editorUrl) { throw "editor-session missing editorUrl." }
Write-Host "  OK editor-session" -ForegroundColor Green

Write-Host "7) export/pdf" -ForegroundColor Yellow
$pdfResp = Invoke-Docs -Path "/resources/$([uri]::EscapeDataString($created.id))/export/pdf" -RawBytes
if ($pdfResp.StatusCode -ne 200) { throw "export/pdf HTTP $($pdfResp.StatusCode)" }
Assert-PdfBytes $pdfResp.Content
Write-Host "  OK export/pdf bytes=$($pdfResp.Content.Length)" -ForegroundColor Green

Write-Host "8) upload pptx -> editor blocked" -ForegroundColor Yellow
$uploaded = Invoke-Docs -Method POST -Path "/resources/file" -Body @{
    parentId         = $parentId
    name             = $uploadName
    originalFileName = "$uploadName.pptx"
    mimeType         = "application/vnd.openxmlformats-officedocument.presentationml.presentation"
    extension        = ".pptx"
    size             = $pptxBytes.LongLength
    content          = [Convert]::ToBase64String($pptxBytes)
}
if (-not $uploaded.id) { throw "Upload create returned no id." }
$script:ArtifactIds += $uploaded.id
$uploadOrigin = ($uploaded.origin ?? '').ToString()
if ($uploadOrigin -eq 'native') { throw "Upload should not be native origin." }

$blockUri = "$Gateway/documents/api/v1/resources/$([uri]::EscapeDataString($uploaded.id))/editor-session"
$blocked = Invoke-WebRequest -Uri $blockUri -Headers $script:Headers -SkipCertificateCheck -SkipHttpErrorCheck
if ($blocked.StatusCode -eq 200) { throw "Upload pptx editor-session should be blocked." }
if ($blocked.StatusCode -lt 400 -or $blocked.StatusCode -ge 500) {
    throw "Upload pptx editor-session expected 4xx, got $($blocked.StatusCode)"
}
Write-Host "  OK editor-session blocked ($($blocked.StatusCode))" -ForegroundColor Green

if (-not $KeepArtifacts) {
    Write-Host "9) cleanup" -ForegroundColor Yellow
    foreach ($rid in $script:ArtifactIds) {
        try {
            Invoke-Docs -Path "/resources/$([uri]::EscapeDataString($rid))" -Method DELETE | Out-Null
            Write-Host "  OK silindi $rid" -ForegroundColor Green
        }
        catch {
            Write-Host "  Uyari: $rid silinemedi — $($_.Exception.Message)" -ForegroundColor DarkYellow
        }
    }
}

Write-Host "PASS (8/8)" -ForegroundColor Green
