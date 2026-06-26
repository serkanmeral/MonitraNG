# ODK-COC-23-202.docx referansından placeholder gövdeli seed şablonu üretir.
# Legacy JPEG antet + footer kaldırılır; sayfa marjinleri ODK referans değerlerine ayarlanır.
# "Uygunluk Belgesi" ve docNo gövdede değil — letterhead API ile enjekte edilir.
#
# Çıktı: docs/odak/document_intelligence/sample/ODK-COC-template-seed.docx
#
# Kullanım:
#   .\docs\odak\document_intelligence\scripts\build-coc-seed-docx.ps1

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
$repoRoot = (Resolve-Path (Join-Path $scriptDir "../../../..")).Path
$sourceDocx = Join-Path $repoRoot "docs/odak/document_intelligence/sample/ODK-COC-23-202.docx"
$outputDocx = Join-Path $repoRoot "docs/odak/document_intelligence/sample/ODK-COC-template-seed.docx"

if (-not (Test-Path $sourceDocx)) {
    throw "Kaynak bulunamadi: $sourceDocx"
}

function Escape-Xml([string]$value) {
    return $value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace('"', "&quot;")
}

function Compress-OoxmlFolder {
    param(
        [string]$SourceFolder,
        [string]$DestinationFile
    )
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    if (Test-Path $DestinationFile) { Remove-Item $DestinationFile -Force }
    $zip = [System.IO.Compression.ZipFile]::Open(
        $DestinationFile,
        [System.IO.Compression.ZipArchiveMode]::Create)
    try {
        Get-ChildItem -Path $SourceFolder -Recurse -File | ForEach-Object {
            $relative = $_.FullName.Substring($SourceFolder.Length).TrimStart('\', '/').Replace('\', '/')
            [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
                $zip,
                $_.FullName,
                $relative,
                [System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
        }
    }
    finally {
        $zip.Dispose()
    }
}

function New-FieldLine([string]$label, [string]$token) {
    $l = Escape-Xml($label)
    $t = Escape-Xml($token)
    return @"
<w:p>
  <w:pPr><w:jc w:val="both"/></w:pPr>
  <w:r><w:rPr><w:sz w:val="24"/><w:szCs w:val="24"/></w:rPr><w:t xml:space="preserve">$l : $t</w:t></w:r>
</w:p>
"@
}

function New-BodyParagraph([string]$text, [string]$align = "both") {
    $t = Escape-Xml($text)
    return @"
<w:p>
  <w:pPr><w:jc w:val="$align"/></w:pPr>
  <w:r><w:rPr><w:sz w:val="24"/><w:szCs w:val="24"/></w:rPr><w:t xml:space="preserve">$t</w:t></w:r>
</w:p>
"@
}

# Belge adı (Uygunluk Belgesi) ve docNo header'da — gövde doğrudan alan satırlarıyla başlar.
$bodyInner = @"
$(New-FieldLine "İş Paketi No" "{{workPackageNo}}")
$(New-FieldLine "Düzenlenme Tarihi" "{{issueDate}}")
$(New-FieldLine "Firma Bilgileri" "{{customerName}}")
$(New-FieldLine "Sipariş No" "{{orderNo}}")
$(New-FieldLine "Parça Tanımı" "{{partDescription}}")
$(New-FieldLine "Teknik Resim No" "{{drawingNo}} ({{drawingRef}})")
$(New-FieldLine "TR Revizyon No" "{{drawingRevision}}")
$(New-FieldLine "Parça Adedi" "{{partQuantity}}")
$(New-FieldLine "Parça Seri No" "{{serialNo}}")
$(New-BodyParagraph "{{complianceStatement}}")
$(New-BodyParagraph "{{leakTestStatement}}")
$(New-BodyParagraph "Açıklamalar:" "both")
$(New-BodyParagraph "{{attachmentsNote}}")
$(New-BodyParagraph "ODAK KOMPOZİT TEKNOLOJİLERİ A.Ş." "center")
$(New-BodyParagraph "{{signatoryName}}" "center")
$(New-BodyParagraph "{{signatoryTitle}}" "center")
$(New-BodyParagraph "Tarih-İmza" "center")
"@

$odakSectPr = @"
    <w:sectPr>
      <w:pgSz w:w="11910" w:h="16840"/>
      <w:pgMar w:top="1440" w:right="1797" w:bottom="1440" w:left="1797" w:header="709" w:footer="658" w:gutter="0"/>
      <w:cols w:space="708"/>
    </w:sectPr>
"@

$tmp = Join-Path $env:TEMP "coc-seed-build-$([Guid]::NewGuid().ToString('N'))"
New-Item -ItemType Directory -Path $tmp | Out-Null
try {
    Copy-Item $sourceDocx -Destination (Join-Path $tmp "source.zip") -Force
    Expand-Archive -Path (Join-Path $tmp "source.zip") -DestinationPath (Join-Path $tmp "unpacked") -Force

    $unpacked = Join-Path $tmp "unpacked"

    foreach ($path in @(
        "word/header1.xml",
        "word/_rels/header1.xml.rels",
        "word/footer1.xml",
        "word/media/image1.jpeg",
        "word/media/image1.jpg"
    )) {
        $full = Join-Path $unpacked $path
        if (Test-Path $full) { Remove-Item $full -Force }
    }

    $docRelsPath = Join-Path $unpacked "word/_rels/document.xml.rels"
    if (Test-Path $docRelsPath) {
        $rels = [IO.File]::ReadAllText($docRelsPath, [Text.UTF8Encoding]::new($false))
        $rels = $rels -replace '<Relationship[^>]+Target="header1\.xml"[^>]*/>', ''
        $rels = $rels -replace '<Relationship[^>]+Target="footer1\.xml"[^>]*/>', ''
        [IO.File]::WriteAllText($docRelsPath, $rels, [Text.UTF8Encoding]::new($false))
    }

    $contentTypesPath = Join-Path $unpacked "[Content_Types].xml"
    if (Test-Path $contentTypesPath) {
        $ct = [IO.File]::ReadAllText($contentTypesPath, [Text.UTF8Encoding]::new($false))
        $ct = $ct -replace '<Override[^>]+PartName="/word/header1\.xml"[^>]*/>', ''
        $ct = $ct -replace '<Override[^>]+PartName="/word/footer1\.xml"[^>]*/>', ''
        [IO.File]::WriteAllText($contentTypesPath, $ct, [Text.UTF8Encoding]::new($false))
    }

    $docPath = Join-Path $unpacked "word/document.xml"
    $newDocument = @"
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main"
            xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
  <w:body>
$bodyInner
$odakSectPr
  </w:body>
</w:document>
"@
    [IO.File]::WriteAllText($docPath, $newDocument, [Text.UTF8Encoding]::new($false))

    if (Test-Path $outputDocx) { Remove-Item $outputDocx -Force }
    Compress-OoxmlFolder -SourceFolder $unpacked -DestinationFile $outputDocx
    Write-Host "OK: $outputDocx" -ForegroundColor Green
}
finally {
    if (Test-Path $tmp) { Remove-Item $tmp -Recurse -Force -ErrorAction SilentlyContinue }
}
