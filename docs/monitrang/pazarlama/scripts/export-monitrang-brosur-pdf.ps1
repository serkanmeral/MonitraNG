# MonitraNG — Platform broşürü: MD → DOCX (MNG-STD) → PDF
#
# Kaynak: docs/monitrang/pazarlama/brosur/monitrang-platform-brosuru.md
# Cikti (repo):
#   docs/monitrang/pazarlama/Files/MonitraNG-Platform-Brosuru.docx
#   docs/monitrang/pazarlama/Files/MonitraNG-Platform-Brosuru.pdf
# DI (Broşür klasoru):
#   MonitraNG Platform Broşürü.docx  →  export/pdf
#
# Onkosul:
#   pandoc (PATH)
#   .\docs\monitrang\pazarlama\scripts\seed-letterheads-monitrang.ps1
#   .\docs\monitrang\pazarlama\scripts\ensure-brosur-reference-docx.ps1
#   Gotenberg (DI export/pdf icin)
#
# Usage (repo kokunden):
#   .\docs\monitrang\pazarlama\scripts\export-monitrang-brosur-pdf.ps1
#   .\docs\monitrang\pazarlama\scripts\export-monitrang-brosur-pdf.ps1 -LocalOnly
#   .\docs\monitrang\pazarlama\scripts\export-monitrang-brosur-pdf.ps1 -WhatIf

param(
    [string]$BaseUrl = "http://localhost:5040",
    [string]$SourceMd = "monitrang-platform-brosuru.md",
    [switch]$LocalOnly = $false,
    [switch]$WhatIf = $false
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
$repoRoot = (Resolve-Path (Join-Path $scriptDir "../../../..")).Path
$brosurDir = Join-Path $repoRoot "docs/monitrang/pazarlama/brosur"
$filesDir = Join-Path $repoRoot "docs/monitrang/pazarlama/Files"
$templatesDir = Join-Path $repoRoot "docs/monitrang/pazarlama/templates"
$refDocx = Join-Path $templatesDir "reference-brosur-mng-std.docx"
$mdPath = Join-Path $brosurDir $SourceMd
$outDocxName = "MonitraNG-Platform-Brosuru.docx"
$outPdfName = "MonitraNG-Platform-Brosuru.pdf"
$outDocxPath = Join-Path $filesDir $outDocxName
$outPdfPath = Join-Path $filesDir $outPdfName
$diFileName = "MonitraNG Platform Broşürü.docx"

function Assert-Pandoc {
    $cmd = Get-Command pandoc -ErrorAction SilentlyContinue
    if (-not $cmd) {
        throw "pandoc bulunamadi. https://pandoc.org/installing.html"
    }
    return $cmd.Source
}

function Prepare-BrosurMarkdown {
    param([string]$InputPath, [string]$OutputPath, [string]$PazarlamaFilesDir)
    $content = [System.IO.File]::ReadAllText($InputPath, [System.Text.Encoding]::UTF8)
    # www.monitrang.com artik antet/footer'da; hero satirini govdeden cikar
    $content = [regex]::Replace($content, '(?m)^\s*\*\*www\.monitrang\.com\*\*\s*\r?\n', "")
    $content = [regex]::Replace($content, '\]\(\.\./Files/([^)]+)\.svg\)', {
            param($m)
            $base = $m.Groups[1].Value
            $png = Join-Path $PazarlamaFilesDir "$base.png"
            if (-not (Test-Path $png)) {
                throw "PNG bulunamadi (SVG icin once resvg ile uretin): $png"
            }
            $uri = [uri]::new($png).AbsoluteUri
            return "]($uri)"
        })
    $content = [regex]::Replace($content, '\]\(\.\./Files/([^)]+)\)', {
            param($m)
            $rel = $m.Groups[1].Value
            $abs = Join-Path $PazarlamaFilesDir $rel
            if (-not (Test-Path $abs)) { throw "Gorsel bulunamadi: $abs" }
            $uri = [uri]::new($abs).AbsoluteUri
            return "]($uri)"
        })
    [System.IO.File]::WriteAllText($OutputPath, $content, [System.Text.UTF8Encoding]::new($false))
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
if (-not (Test-Path $refDocx)) {
    Write-Host "Referans DOCX yok — ensure-brosur-reference-docx.ps1 calistiriliyor..." -ForegroundColor Yellow
    & (Join-Path $scriptDir "ensure-brosur-reference-docx.ps1") -BaseUrl $BaseUrl
}

Assert-Pandoc | Out-Null

$tmpMd = Join-Path $env:TEMP "monitrang-brosur-export.md"
Prepare-BrosurMarkdown -InputPath $mdPath -OutputPath $tmpMd -PazarlamaFilesDir $filesDir

Write-Host "`nPandoc DOCX..." -ForegroundColor Cyan
if ($WhatIf) {
    Write-Host "WhatIf: pandoc $tmpMd -> $outDocxPath" -ForegroundColor Yellow
}
else {
    Invoke-PandocDocx -InputMd $tmpMd -OutputDocx $outDocxPath -ReferenceDocx $refDocx
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

function Upsert-BrosurDocxInDi {
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

Write-Host "`nDI yukleme + PDF..." -ForegroundColor Cyan
$brosurFolderId = Ensure-FolderPath -Segments @("MonitraNG", "Pazarlama", "Broşür")
$docxBytes = if ($WhatIf) { [byte[]]::new(0) } else { [System.IO.File]::ReadAllBytes($outDocxPath) }
$resourceId = Upsert-BrosurDocxInDi -ParentId $brosurFolderId -FileName $diFileName -Bytes $docxBytes

if ($WhatIf) {
    Write-Host "WhatIf GET export/pdf" -ForegroundColor Yellow
    exit 0
}

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
Write-Host "DI:   Pazarlama > Broşür > $diFileName (id=$resourceId)" -ForegroundColor Green
Write-Host "DI UI: Kaynak uzerinden 'PDF disa aktar' ile guncel cikti alinabilir." -ForegroundColor Gray
