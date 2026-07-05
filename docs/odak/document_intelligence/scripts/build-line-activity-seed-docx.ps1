# Kalem Activity Raporu — profesyonel gövde DOCX (antet/altbilgi API ile enjekte edilir).
#
# Çıktı: docs/odak/document_intelligence/sample/ODK-LINE-ACTIVITY-template-seed.docx
#
# Kullanım:
#   .\docs\odak\document_intelligence\scripts\build-line-activity-seed-docx.ps1

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
$repoRoot = (Resolve-Path (Join-Path $scriptDir "../../../..")).Path
$sourceDocx = Join-Path $repoRoot "docs/odak/document_intelligence/sample/ODK-COC-23-202.docx"
$outputDocx = Join-Path $repoRoot "docs/odak/document_intelligence/sample/ODK-LINE-ACTIVITY-template-seed.docx"
$contentFile = Join-Path $repoRoot "docs/odak/document_intelligence/datasets/line-activity-docx-content.json"

if (-not (Test-Path $sourceDocx)) {
    throw "Kaynak bulunamadi: $sourceDocx"
}
if (-not (Test-Path $contentFile)) {
    throw "Icerik dosyasi yok: $contentFile"
}

$content = [IO.File]::ReadAllText($contentFile, [Text.UTF8Encoding]::new($false)) | ConvertFrom-Json

# ODAK kurumsal palet
$FontBody = "Calibri"
$ColorNavy = "1E3A5F"
$ColorLabel = "5A6370"
$ColorBody = "2F3542"
$ColorMuted = "8B95A5"
$FillSection = "E8EEF4"
$FillLabelCell = "F4F6F8"
$BorderLight = "D0D7DE"

function Escape-Xml([string]$value) {
    if ($null -eq $value) { return "" }
    return $value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace('"', "&quot;")
}

function Compress-OoxmlFolder {
    param([string]$SourceFolder, [string]$DestinationFile)
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    if (Test-Path $DestinationFile) { Remove-Item $DestinationFile -Force }
    $zip = [System.IO.Compression.ZipFile]::Open(
        $DestinationFile,
        [System.IO.Compression.ZipArchiveMode]::Create)
    try {
        Get-ChildItem -Path $SourceFolder -Recurse -File | ForEach-Object {
            $relative = $_.FullName.Substring($SourceFolder.Length).TrimStart('\', '/').Replace('\', '/')
            [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
                $zip, $_.FullName, $relative,
                [System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
        }
    }
    finally { $zip.Dispose() }
}

function New-RunProps {
    param(
        [int]$Size = 20,
        [string]$Color = $ColorBody,
        [bool]$Bold = $false,
        [bool]$Italic = $false
    )
    $parts = @("<w:rFonts w:ascii=`"$FontBody`" w:hAnsi=`"$FontBody`" w:cs=`"$FontBody`"/>")
    if ($Bold) { $parts += "<w:b/>" }
    if ($Italic) { $parts += "<w:i/>" }
    $parts += "<w:color w:val=`"$Color`"/>"
    $parts += "<w:sz w:val=`"$Size`"/><w:szCs w:val=`"$Size`"/>"
    return "<w:rPr>$($parts -join '')</w:rPr>"
}

function New-Run([string]$Text, [int]$Size = 20, [string]$Color = $ColorBody, [bool]$Bold = $false, [bool]$Italic = $false) {
    $t = Escape-Xml($Text)
    $rPr = New-RunProps -Size $Size -Color $Color -Bold $Bold -Italic $Italic
    return "<w:r>$rPr<w:t xml:space=`"preserve`">$t</w:t></w:r>"
}

function New-Paragraph {
    param(
        [string]$InnerRuns,
        [string]$Align = "left",
        [int]$Before = 0,
        [int]$After = 120,
        [int]$Line = 240
    )
    $pPr = @("<w:jc w:val=`"$Align`"/>")
    if ($Before -gt 0 -or $After -gt 0 -or $Line -gt 0) {
        $pPr += "<w:spacing w:before=`"$Before`" w:after=`"$After`" w:line=`"$Line`" w:lineRule=`"auto`"/>"
    }
    return @"
<w:p>
  <w:pPr>$($pPr -join '')</w:pPr>
  $InnerRuns
</w:p>
"@
}

function New-Spacer([int]$After = 160) {
    return New-Paragraph -InnerRuns "" -After $After -Before 0
}

function New-SectionHeading([string]$Title) {
    $t = Escape-Xml($Title)
    return @"
<w:tbl>
  <w:tblPr>
    <w:tblW w:w="5000" w:type="pct"/>
    <w:tblBorders>
      <w:top w:val="nil"/><w:left w:val="nil"/><w:bottom w:val="nil"/><w:right w:val="nil"/>
      <w:insideH w:val="nil"/><w:insideV w:val="nil"/>
    </w:tblBorders>
    <w:tblCellMar><w:top w:w="0" w:type="dxa"/><w:left w:w="0" w:type="dxa"/><w:bottom w:w="0" w:type="dxa"/><w:right w:w="0" w:type="dxa"/></w:tblCellMar>
  </w:tblPr>
  <w:tblGrid><w:gridCol w:w="113"/><w:gridCol w:w="8913"/></w:tblGrid>
  <w:tr>
    <w:tc>
      <w:tcPr><w:tcW w:w="113" w:type="dxa"/><w:shd w:val="clear" w:color="auto" w:fill="$ColorNavy"/></w:tcPr>
      <w:p><w:pPr><w:spacing w:before="40" w:after="40"/></w:pPr></w:p>
    </w:tc>
    <w:tc>
      <w:tcPr><w:tcW w:w="8913" w:type="dxa"/><w:shd w:val="clear" w:color="auto" w:fill="$FillSection"/></w:tcPr>
      <w:p>
        <w:pPr><w:spacing w:before="60" w:after="60"/></w:pPr>
        $(New-Run $Title 22 $ColorNavy $true)
      </w:p>
    </w:tc>
  </w:tr>
</w:tbl>
"@
}

function New-KvTableRow([string]$Label, [string]$ValueToken) {
    $l = Escape-Xml($Label)
    $v = Escape-Xml($ValueToken)
    return @"
  <w:tr>
    <w:tc>
      <w:tcPr>
        <w:tcW w:w="3200" w:type="dxa"/>
        <w:shd w:val="clear" w:color="auto" w:fill="$FillLabelCell"/>
        <w:tcMar><w:top w:w="80" w:type="dxa"/><w:left w:w="120" w:type="dxa"/><w:bottom w:w="80" w:type="dxa"/><w:right w:w="80" w:type="dxa"/></w:tcMar>
      </w:tcPr>
      <w:p><w:pPr><w:spacing w:before="20" w:after="20"/></w:pPr>$(New-Run $Label 20 $ColorLabel $true)</w:p>
    </w:tc>
    <w:tc>
      <w:tcPr>
        <w:tcW w:w="5826" w:type="dxa"/>
        <w:tcMar><w:top w:w="80" w:type="dxa"/><w:left w:w="120" w:type="dxa"/><w:bottom w:w="80" w:type="dxa"/><w:right w:w="120" w:type="dxa"/></w:tcMar>
      </w:tcPr>
      <w:p><w:pPr><w:spacing w:before="20" w:after="20"/></w:pPr>$(New-Run $ValueToken 20 $ColorBody $false)</w:p>
    </w:tc>
  </w:tr>
"@
}

function New-KvTable([array]$Rows) {
    $inner = ($Rows | ForEach-Object { New-KvTableRow $_.Label $_.Value }) -join "`n"
    return @"
<w:tbl>
  <w:tblPr>
    <w:tblW w:w="5000" w:type="pct"/>
    <w:tblBorders>
      <w:top w:val="single" w:sz="4" w:space="0" w:color="$BorderLight"/>
      <w:left w:val="single" w:sz="4" w:space="0" w:color="$BorderLight"/>
      <w:bottom w:val="single" w:sz="4" w:space="0" w:color="$BorderLight"/>
      <w:right w:val="single" w:sz="4" w:space="0" w:color="$BorderLight"/>
      <w:insideH w:val="single" w:sz="4" w:space="0" w:color="$BorderLight"/>
      <w:insideV w:val="single" w:sz="4" w:space="0" w:color="$BorderLight"/>
    </w:tblBorders>
  </w:tblPr>
  <w:tblGrid><w:gridCol w:w="3200"/><w:gridCol w:w="5826"/></w:tblGrid>
$inner
</w:tbl>
"@
}

function New-StatusCell([string]$Label, [string]$Token, [bool]$IsHeader = $false) {
    $fill = if ($IsHeader) { $FillSection } else { "FFFFFF" }
    $size = if ($IsHeader) { 18 } else { 20 }
    $bold = $IsHeader
    $color = if ($IsHeader) { $ColorNavy } else { $ColorBody }
    $text = if ($IsHeader) { $Label } else { $Token }
    return @"
    <w:tc>
      <w:tcPr>
        <w:tcW w:w="2256" w:type="dxa"/>
        <w:shd w:val="clear" w:color="auto" w:fill="$fill"/>
        <w:tcMar><w:top w:w="100" w:type="dxa"/><w:left w:w="100" w:type="dxa"/><w:bottom w:w="100" w:type="dxa"/><w:right w:w="100" w:type="dxa"/></w:tcMar>
      </w:tcPr>
      <w:p><w:pPr><w:jc w:val="center"/><w:spacing w:before="20" w:after="20"/></w:pPr>$(New-Run $text $size $color $bold)</w:p>
    </w:tc>
"@
}

function New-StatusStrip {
    $s = $content.statusStrip
    return @"
<w:tbl>
  <w:tblPr>
    <w:tblW w:w="5000" w:type="pct"/>
    <w:tblBorders>
      <w:top w:val="single" w:sz="6" w:space="0" w:color="$ColorNavy"/>
      <w:left w:val="single" w:sz="4" w:space="0" w:color="$BorderLight"/>
      <w:bottom w:val="single" w:sz="4" w:space="0" w:color="$BorderLight"/>
      <w:right w:val="single" w:sz="4" w:space="0" w:color="$BorderLight"/>
      <w:insideH w:val="single" w:sz="4" w:space="0" w:color="$BorderLight"/>
      <w:insideV w:val="single" w:sz="4" w:space="0" w:color="$BorderLight"/>
    </w:tblBorders>
  </w:tblPr>
  <w:tblGrid>
    <w:gridCol w:w="2256"/><w:gridCol w:w="2256"/><w:gridCol w:w="2256"/><w:gridCol w:w="2256"/>
  </w:tblGrid>
  <w:tr>
    $(New-StatusCell $s.lineDeliveryDate "" $true)
    $(New-StatusCell $s.shippedSummary "" $true)
    $(New-StatusCell $s.cocDocNo "" $true)
    $(New-StatusCell $s.issueDate "" $true)
  </w:tr>
  <w:tr>
    $(New-StatusCell "" "{{lineDeliveryDate}}" $false)
    $(New-StatusCell "" "{{shippedSummary}}" $false)
    $(New-StatusCell "" "{{cocDocNo}}" $false)
    $(New-StatusCell "" "{{issueDate}}" $false)
  </w:tr>
</w:tbl>
"@
}

function Convert-ContentRows($rows) {
    return @($rows | ForEach-Object { @{ Label = [string]$_.label; Value = [string]$_.value } })
}

$packageRows = Convert-ContentRows $content.packageRows
$lineRows = Convert-ContentRows $content.lineRows
$qualityRows = Convert-ContentRows $content.qualityRows
$shipmentRows = Convert-ContentRows $content.shipmentRows
$sections = $content.sections
$labels = $content.labels

$bodyInner = @"
$(New-Paragraph (New-Run "K{{lineNo}} - {{partDescription}}" 28 $ColorNavy $true) "left" 0 80)
$(New-Paragraph ((New-Run "{{workPackageNo}}" 22 $ColorBody $true) + (New-Run " | {{workPackageName}}" 22 $ColorMuted $false)) "left" 0 60)
$(New-Paragraph (New-Run "{{customerName}}" 20 $ColorLabel $false) "left" 0 160)
$(New-Paragraph (New-Run "$($labels.docNoPrefix) {{poDocNo}}" 18 $ColorMuted $false) "right" 0 200)
$(New-StatusStrip)
$(New-Spacer 200)
$(New-SectionHeading $sections.packageContext)
$(New-Spacer 80)
$(New-KvTable $packageRows)
$(New-Spacer 200)
$(New-SectionHeading $sections.lineCommercial)
$(New-Spacer 80)
$(New-KvTable $lineRows)
$(New-Spacer 200)
$(New-SectionHeading $sections.quality)
$(New-Spacer 80)
$(New-KvTable $qualityRows)
$(New-Spacer 200)
$(New-SectionHeading $sections.shipment)
$(New-Spacer 80)
$(New-KvTable $shipmentRows)
$(New-Spacer 240)
$(New-Paragraph (New-Run $labels.footerNote 16 $ColorMuted $false $true) "left" 120 0)
"@

$odakSectPr = @"
    <w:sectPr>
      <w:pgSz w:w="11906" w:h="16838"/>
      <w:pgMar w:top="1440" w:right="1797" w:bottom="1440" w:left="1797" w:header="709" w:footer="658" w:gutter="0"/>
      <w:cols w:space="708"/>
    </w:sectPr>
"@

$tmp = Join-Path $env:TEMP "line-activity-seed-$([Guid]::NewGuid().ToString('N'))"
New-Item -ItemType Directory -Path $tmp | Out-Null
try {
    Copy-Item $sourceDocx -Destination (Join-Path $tmp "source.zip") -Force
    Expand-Archive -Path (Join-Path $tmp "source.zip") -DestinationPath (Join-Path $tmp "unpacked") -Force
    $unpacked = Join-Path $tmp "unpacked"

    foreach ($path in @(
        "word/header1.xml", "word/_rels/header1.xml.rels",
        "word/footer1.xml", "word/media/image1.jpeg", "word/media/image1.jpg"
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
