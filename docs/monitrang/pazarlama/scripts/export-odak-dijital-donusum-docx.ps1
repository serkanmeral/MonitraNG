# Odak — Dijital Dönüşüm Planlaması: MD → DOCX (MNG-STD) → DI (+ isteğe bağlı PDF)
#
# Kaynak: docs/monitrang/pazarlama/odak/dijital-donusum-planlamasi.md
# Cikti (repo):
#   docs/monitrang/pazarlama/Files/Odak-Dijital-Donusum-Planlamasi.docx
#   docs/monitrang/pazarlama/Files/Odak-Dijital-Donusum-Planlamasi.pdf  (full export)
# DI (Odak klasoru):
#   Odak Dijital Dönüşüm Planlaması.docx
#
# Onkosul:
#   pandoc (PATH)
#   .\docs\monitrang\pazarlama\scripts\ensure-brosur-reference-docx.ps1
#   Full export icin: DI gateway + (PDF icin) Gotenberg
#
# Usage (repo kokunden):
#   .\docs\monitrang\pazarlama\scripts\export-odak-dijital-donusum-docx.ps1
#   .\docs\monitrang\pazarlama\scripts\export-odak-dijital-donusum-docx.ps1 -LocalOnly
#   .\docs\monitrang\pazarlama\scripts\export-odak-dijital-donusum-docx.ps1 -SkipPdf
#   .\docs\monitrang\pazarlama\scripts\export-odak-dijital-donusum-docx.ps1 -WhatIf

param(
    [string]$BaseUrl = "http://localhost:5040",
    [switch]$LocalOnly = $false,
    [switch]$SkipPdf = $false,
    [switch]$WhatIf = $false
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
$repoRoot = (Resolve-Path (Join-Path $scriptDir "../../../..")).Path
$odakDir = Join-Path $repoRoot "docs/monitrang/pazarlama/odak"
$filesDir = Join-Path $repoRoot "docs/monitrang/pazarlama/Files"
$templatesDir = Join-Path $repoRoot "docs/monitrang/pazarlama/templates"
$refDocx = Join-Path $templatesDir "reference-brosur-mng-std.docx"
$mdPath = Join-Path $odakDir "dijital-donusum-planlamasi.md"
$outDocxName = "Odak-Dijital-Donusum-Planlamasi.docx"
$outPdfName = "Odak-Dijital-Donusum-Planlamasi.pdf"
$outDocxPath = Join-Path $filesDir $outDocxName
$outPdfPath = Join-Path $filesDir $outPdfName
$diFileName = "Odak Dijital Dönüşüm Planlaması.docx"

function Assert-Pandoc {
    $cmd = Get-Command pandoc -ErrorAction SilentlyContinue
    if (-not $cmd) {
        throw "pandoc bulunamadi. https://pandoc.org/installing.html"
    }
    return $cmd.Source
}

function Invoke-PandocDocx {
    param(
        [string]$InputMd,
        [string]$OutputDocx,
        [string]$ReferenceDocx
    )
    $args = @(
        $InputMd,
        "-o", $OutputDocx,
        "--from=markdown",
        "--to=docx",
        "--standalone"
    )
    if (Test-Path $ReferenceDocx) {
        $args += @("--reference-doc=$ReferenceDocx")
    }
    else {
        Write-Host "WARN: Referans DOCX yok — antetsiz Pandoc ciktisi ($ReferenceDocx)" -ForegroundColor Yellow
        Write-Host "  -> ensure-brosur-reference-docx.ps1 calistirin." -ForegroundColor Yellow
    }
    & pandoc @args
    if ($LASTEXITCODE -ne 0) { throw "pandoc exit code $LASTEXITCODE" }
    if (-not (Test-Path $OutputDocx)) { throw "Pandoc cikti dosyasi olusmadi: $OutputDocx" }
}

if (-not (Test-Path $mdPath)) { throw "Markdown bulunamadi: $mdPath" }
if (-not (Test-Path $filesDir)) {
    New-Item -ItemType Directory -Force -Path $filesDir | Out-Null
}
if (-not (Test-Path $refDocx)) {
    Write-Host "Referans DOCX yok — ensure-brosur-reference-docx.ps1 calistiriliyor..." -ForegroundColor Yellow
    & (Join-Path $scriptDir "ensure-brosur-reference-docx.ps1") -BaseUrl $BaseUrl
}

Assert-Pandoc | Out-Null

Write-Host "`nPandoc DOCX..." -ForegroundColor Cyan
if ($WhatIf) {
    Write-Host "WhatIf: pandoc $mdPath -> $outDocxPath" -ForegroundColor Yellow
}
else {
    Invoke-PandocDocx -InputMd $mdPath -OutputDocx $outDocxPath -ReferenceDocx $refDocx
    Write-Host "OK DOCX: $outDocxPath ($((Get-Item $outDocxPath).Length) byte)" -ForegroundColor Green
}

if ($LocalOnly) {
    Write-Host "`nLocalOnly — DI adimi atlandi." -ForegroundColor Cyan
    exit 0
}

. (Join-Path $repoRoot "scripts/tests/MngDocument/auth/DiAuthCommon.ps1")
$token = Get-DiPersonaToken -Persona Admin -Gateway $BaseUrl

function Invoke-DocsJson {
    param([string]$Method, [string]$Path, [object]$Body = $null)
    $r = Invoke-DiDocs -Gateway $BaseUrl -Token $token -Method $Method -Path $Path -Body $Body -TimeoutSec 120
    if ($r.StatusCode -ge 400) {
        throw "$Method $Path -> HTTP $($r.StatusCode): $($r.Content)"
    }
    if ([string]::IsNullOrWhiteSpace($r.Content)) { return $null }
    return $r.Content | ConvertFrom-Json
}

function Get-ChildByName {
    param([string]$ParentId, [string]$Name, [string]$Type = $null)
    $q = if ($ParentId) { "?parentId=$ParentId&limit=200" } else { "?limit=200" }
    $data = Invoke-DocsJson -Method GET -Path "/resources/children$q"
    foreach ($it in @($data.items)) {
        if ($it.name -ne $Name) { continue }
        if ($Type -and $it.type -ne $Type) { continue }
        return $it
    }
    return $null
}

function Ensure-FolderPath {
    param([string[]]$Segments)
    $parentId = $null
    foreach ($seg in $Segments) {
        $existing = Get-ChildByName -ParentId $parentId -Name $seg -Type "folder"
        if ($existing) {
            $parentId = $existing.id
            continue
        }
        $body = @{ name = $seg }
        if ($parentId) { $body.parentId = $parentId }
        $created = Invoke-DocsJson -Method POST -Path "/resources/folder" -Body $body
        $parentId = $created.id
    }
    return $parentId
}

function Upsert-DocxInDi {
    param(
        [string]$ParentId,
        [string]$FileName,
        [byte[]]$Bytes
    )
    $existing = Get-ChildByName -ParentId $ParentId -Name $FileName -Type "file"
    if ($existing) {
        Write-Host "DI: mevcut '$FileName' siliniyor (id=$($existing.id))..." -ForegroundColor Yellow
        if (-not $WhatIf) {
            Invoke-DiDocs -Gateway $BaseUrl -Token $token -Method DELETE -Path "/resources/$($existing.id)" | Out-Null
        }
    }
    if ($WhatIf) {
        Write-Host "WhatIf POST /file '$FileName' ($($Bytes.Length) byte)" -ForegroundColor Yellow
        return "<whatif>"
    }
    $created = Invoke-DocsJson -Method POST -Path "/resources/file" -Body @{
        parentId         = $ParentId
        name             = $FileName
        originalFileName = $FileName
        mimeType         = "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
        extension        = ".docx"
        size             = $Bytes.Length
        content          = [Convert]::ToBase64String($Bytes)
        origin           = "manual"
    }
    return $created.id
}

Write-Host "`nDI yukleme..." -ForegroundColor Cyan
# Prefer under Sayfalar when present (children search starts at root; Ensure finds/creates path)
$sayfalar = Get-ChildByName -ParentId $null -Name "Sayfalar" -Type "folder"
$segments = if ($sayfalar) {
    @("Sayfalar", "MonitraNG", "Pazarlama", "Odak")
} else {
    @("MonitraNG", "Pazarlama", "Odak")
}
$odakFolderId = Ensure-FolderPath -Segments $segments
$docxBytes = if ($WhatIf) { [byte[]]::new(0) } else { [System.IO.File]::ReadAllBytes($outDocxPath) }
$resourceId = Upsert-DocxInDi -ParentId $odakFolderId -FileName $diFileName -Bytes $docxBytes

Write-Host "DI DOCX: Pazarlama > Odak > $diFileName (id=$resourceId)" -ForegroundColor Green

if ($SkipPdf -or $WhatIf) {
    if ($WhatIf) { Write-Host "WhatIf — PDF atlandi." -ForegroundColor Yellow }
    else { Write-Host "SkipPdf — PDF export atlandi." -ForegroundColor Cyan }
    Write-Host "`nTamamlandi." -ForegroundColor Cyan
    Write-Host "DOCX: $outDocxPath" -ForegroundColor Green
    exit 0
}

Write-Host "`nPDF export..." -ForegroundColor Cyan
$pdfResp = Invoke-WebRequest -Uri "$BaseUrl/documents/api/v1/resources/$resourceId/export/pdf" `
    -Method GET -Headers @{ Authorization = "Bearer $token" } `
    -TimeoutSec 180 -UseBasicParsing
if ($pdfResp.StatusCode -ne 200) {
    throw "export/pdf HTTP $($pdfResp.StatusCode)"
}
$pdfBytes = $pdfResp.Content
if ($pdfBytes -is [string]) {
    $pdfBytes = [System.Text.Encoding]::GetEncoding("ISO-8859-1").GetBytes($pdfBytes)
}
if ($pdfBytes.Length -lt 4 -or [System.Text.Encoding]::ASCII.GetString($pdfBytes[0..3]) -ne "%PDF") {
    throw "Gecerli PDF donmedi (len=$($pdfBytes.Length))"
}
[System.IO.File]::WriteAllBytes($outPdfPath, $pdfBytes)

Write-Host "`nTamamlandi." -ForegroundColor Cyan
Write-Host "DOCX: $outDocxPath" -ForegroundColor Green
Write-Host "PDF:  $outPdfPath ($($pdfBytes.Length) byte)" -ForegroundColor Green
Write-Host "DI:   Pazarlama > Odak > $diFileName (id=$resourceId)" -ForegroundColor Green
