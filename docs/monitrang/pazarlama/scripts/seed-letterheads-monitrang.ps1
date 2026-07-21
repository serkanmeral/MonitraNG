# MonitraNG — minimal antet katalog seed (MNG-STD)
#
# Ust: 3 kolonlu tablo — ortada {{documentName}} (ornek: MonitraNG)
# Alt: ortalanmis sayfa numarasi (PAGE alani)
#
# Usage (repo kokunden):
#   .\docs\monitrang\pazarlama\scripts\seed-letterheads-monitrang.ps1
#   .\docs\monitrang\pazarlama\scripts\seed-letterheads-monitrang.ps1 -BaseUrl "http://localhost:5040"
#   .\docs\monitrang\pazarlama\scripts\seed-letterheads-monitrang.ps1 -WhatIf

param(
    [string]$BaseUrl = "http://localhost:5040",
    [string]$Token = $env:DI_TOKEN,
    [switch]$WhatIf = $false,
    [switch]$SkipDesignPatch = $false
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
$repoRoot = (Resolve-Path (Join-Path $scriptDir "../../../..")).Path
$seedFile = Join-Path $repoRoot "docs/monitrang/pazarlama/datasets/seed-letterheads-monitrang.json"

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

$token = $Token
if ([string]::IsNullOrEmpty($token)) {
    . (Join-Path $repoRoot "scripts/tests/MngDocument/auth/DiAuthCommon.ps1")
    $token = Get-DiPersonaToken -Persona Admin -Gateway $BaseUrl
}
if ([string]::IsNullOrEmpty($token)) {
    Write-Host "Token alinamadi." -ForegroundColor Red
    exit 1
}
$token = $token.Trim()
$headers = @{
    Authorization = "Bearer $token"
    "Content-Type" = "application/json"
}

$seed = Get-Content $seedFile -Raw -Encoding UTF8 | ConvertFrom-Json
$letterheadsBase = "$BaseUrl/documents/api/v1/letterheads"
$dgBase = "$BaseUrl/data/api/v1"

function Invoke-DmApi {
    param(
        [string]$Method,
        [string]$Uri,
        [object]$Body = $null
    )
    $params = @{
        Uri        = $Uri
        Method     = $Method
        Headers    = $headers
        TimeoutSec = 120
    }
    if ($null -ne $Body) {
        $params.Body = ($Body | ConvertTo-Json -Depth 12 -Compress)
    }
    return Invoke-RestMethod @params
}

function Merge-LetterheadSettings {
    param($defaults, $override)
    if (-not $override) { return $defaults }
    $result = @{}
    foreach ($prop in $defaults.PSObject.Properties) {
        $result[$prop.Name] = $prop.Value
    }
    foreach ($prop in $override.PSObject.Properties) {
        $result[$prop.Name] = $prop.Value
    }
    return [PSCustomObject]$result
}

function Get-DesignBytes {
    param([string]$StoragePath)
    if ([string]::IsNullOrWhiteSpace($StoragePath)) { return $null }
    $uri = "$dgBase/files/download?filePath=$([uri]::EscapeDataString($StoragePath))"
    $resp = Invoke-WebRequest -Uri $uri -Headers $headers -Method GET
    return [byte[]]$resp.Content
}

function Get-FooterPageNumberXml {
    @"
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<w:ftr xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">
  <w:p>
    <w:pPr><w:jc w:val="center"/></w:pPr>
    <w:r><w:fldChar w:fldCharType="begin"/></w:r>
    <w:r><w:instrText xml:space="preserve"> PAGE </w:instrText></w:r>
    <w:r><w:fldChar w:fldCharType="separate"/></w:r>
    <w:r><w:t>1</w:t></w:r>
    <w:r><w:fldChar w:fldCharType="end"/></w:r>
  </w:p>
</w:ftr>
"@
}

function Set-DocxFooterPageNumber {
    param([byte[]]$DocxBytes)
    $footerXml = Get-FooterPageNumberXml
    $footerBytes = [System.Text.Encoding]::UTF8.GetBytes($footerXml)

    $inputMs = New-Object System.IO.MemoryStream(,$DocxBytes)
    $outputMs = New-Object System.IO.MemoryStream
    $readZip = New-Object System.IO.Compression.ZipArchive($inputMs, [System.IO.Compression.ZipArchiveMode]::Read)
    $writeZip = New-Object System.IO.Compression.ZipArchive($outputMs, [System.IO.Compression.ZipArchiveMode]::Create)

    $replacedFooter = $false
    foreach ($entry in $readZip.Entries) {
        if ($entry.FullName -eq "word/footer1.xml") {
            $replacedFooter = $true
            $newEntry = $writeZip.CreateEntry("word/footer1.xml", [System.IO.Compression.CompressionLevel]::Optimal)
            $stream = $newEntry.Open()
            $stream.Write($footerBytes, 0, $footerBytes.Length)
            $stream.Close()
            continue
        }

        $newEntry = $writeZip.CreateEntry($entry.FullName, [System.IO.Compression.CompressionLevel]::Optimal)
        $src = $entry.Open()
        $dst = $newEntry.Open()
        $src.CopyTo($dst)
        $src.Close()
        $dst.Close()
    }

    if (-not $replacedFooter) {
        $newEntry = $writeZip.CreateEntry("word/footer1.xml", [System.IO.Compression.CompressionLevel]::Optimal)
        $stream = $newEntry.Open()
        $stream.Write($footerBytes, 0, $footerBytes.Length)
        $stream.Close()
    }

    $readZip.Dispose()
    $writeZip.Dispose()
    return $outputMs.ToArray()
}

function Set-LetterheadDesign {
    param(
        [string]$LetterheadId,
        [byte[]]$DesignBytes,
        [string]$FileName,
        [object]$RowMeta
    )
    $b64 = [Convert]::ToBase64String($DesignBytes)
    $upload = Invoke-DmApi -Method POST -Uri "$dgBase/files/upload" -Body @{
        Content        = $b64
        DatasetName    = "dm_letterheads"
        FieldName      = "designFile"
        RecordId       = $LetterheadId
        UseCompression = $false
        UseEncryption  = $false
    }

    $filePath = $upload.data.filePath
    if ([string]::IsNullOrWhiteSpace($filePath)) { $filePath = $upload.Data.FilePath }
    if ([string]::IsNullOrWhiteSpace($filePath)) { throw "Upload filePath bos." }

    $storedName = $upload.data.file_name
    if ([string]::IsNullOrWhiteSpace($storedName)) { $storedName = $upload.Data.file_name }
    if ([string]::IsNullOrWhiteSpace($storedName)) { $storedName = $FileName }

    Invoke-DmApi -Method PUT -Uri "$dgBase/data/dm_letterheads/$LetterheadId" -Body @{
        name                = $RowMeta.name
        code                = $RowMeta.code
        description         = $RowMeta.description
        isDefault           = [bool]$RowMeta.isDefault
        isActive            = [bool]$RowMeta.isActive
        letterheadJson      = $RowMeta.letterheadJson
        settingsJson        = $RowMeta.settingsJson
        designStoragePath   = $filePath
        designFileName      = $storedName
        updatedBy           = "seed-letterheads-monitrang"
        updatedAt           = (Get-Date).ToUniversalTime().ToString("o")
    } | Out-Null
}

function Initialize-LetterheadDesign {
    param(
        [string]$LetterheadId,
        [string]$Code
    )
    Write-Host "  design-session baslatiliyor..." -ForegroundColor Cyan
    Invoke-DmApi -Method GET -Uri "$letterheadsBase/$LetterheadId/design-session" | Out-Null

    $row = Invoke-DmApi -Method GET -Uri "$dgBase/data/dm_letterheads/$LetterheadId"
    $path = $row.designStoragePath
    if ([string]::IsNullOrWhiteSpace($path)) {
        throw "Tasarim dosyasi olusturulamadi (designStoragePath bos)."
    }

    $bytes = Get-DesignBytes -StoragePath $path
    if (-not $bytes -or $bytes.Length -eq 0) {
        throw "Tasarim dosyasi indirilemedi."
    }

    Write-Host "  footer -> sayfa numarasi (PAGE)..." -ForegroundColor Cyan
    $patched = Set-DocxFooterPageNumber -DocxBytes $bytes
    $fileName = if ($row.designFileName) { $row.designFileName } else { "$Code-design.docx" }
    Set-LetterheadDesign -LetterheadId $LetterheadId -DesignBytes $patched -FileName $fileName -RowMeta $row
    Write-Host "  OK tasarim guncellendi ($fileName)" -ForegroundColor Green
}

Write-Host "`n=== MonitraNG antet seed -> $BaseUrl ===" -ForegroundColor Cyan

$codeToId = @{}
foreach ($entry in $seed.letterheads) {
    $code = [string]$entry.code
    $body = @{
        name        = [string]$entry.name
        code        = $code
        description = [string]$entry.description
        isDefault   = [bool]$entry.isDefault
        isActive    = [bool]$entry.isActive
        letterhead  = $entry.letterhead
        settings    = Merge-LetterheadSettings $seed.defaultSettings $entry.settings
    }

    $list = Invoke-DmApi -Method GET -Uri $letterheadsBase
    $existing = $list.items | Where-Object { $_.code -eq $code } | Select-Object -First 1

    if ($WhatIf) {
        Write-Host "WHATIF $($entry.name) ($code)" -ForegroundColor DarkGray
        continue
    }

    if ($existing) {
        $updated = Invoke-DmApi -Method PUT -Uri "$letterheadsBase/$($existing.id)" -Body $body
        $codeToId[$code] = $updated.id
        Write-Host "OK update $code id=$($updated.id)" -ForegroundColor Green
    }
    else {
        $created = Invoke-DmApi -Method POST -Uri $letterheadsBase -Body $body
        $codeToId[$code] = $created.id
        Write-Host "OK create $code id=$($created.id)" -ForegroundColor Green
    }

    if (-not $SkipDesignPatch) {
        Initialize-LetterheadDesign -LetterheadId $codeToId[$code] -Code $code
    }
}

Write-Host "`nTamamlandi." -ForegroundColor Cyan
Write-Host "UI: Belge Tasarimcisi > Antetler > MonitraNG (MNG-STD)" -ForegroundColor Cyan
Write-Host "Kod: MNG-STD — ust tablo ortada belge adi, alt ortada sayfa numarasi" -ForegroundColor Gray
