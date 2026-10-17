# BV toplantı tutanağı gövdesi. Başlıklar okuma kuralıdır; değerler {{parametre}} olarak durur.
# Çıktı: docs/odak/document_intelligence/sample/BV-MEETING-MINUTES-template-seed.docx

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
$repoRoot = (Resolve-Path (Join-Path $scriptDir "../../../..")).Path
$outputDocx = Join-Path $repoRoot "docs/odak/document_intelligence/sample/BV-MEETING-MINUTES-template-seed.docx"

function Escape-Xml([string]$value) {
    return $value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;")
}

function New-BoldMark([bool]$isBold) {
    if (-not $isBold) { return "" }
    return ([char]60) + "w:b/>"
}

function New-Cell([string]$text, [bool]$isBold) {
    $t = Escape-Xml $text
    $bold = New-BoldMark $isBold
    return "<w:tc><w:tcPr><w:tcW w:w=`"2400`" w:type=`"dxa`"/></w:tcPr><w:p><w:r><w:rPr>${bold}<w:sz w:val=`"21`"/><w:szCs w:val=`"21`"/></w:rPr><w:t xml:space=`"preserve`">$t</w:t></w:r></w:p></w:tc>"
}

function New-Row([string[]]$cells, [bool]$Header) {
    $parts = foreach ($cell in $cells) { New-Cell $cell $Header }
    return "<w:tr>$($parts -join '')</w:tr>"
}

function New-Table([string]$rowsXml) {
    return @"
<w:tbl>
  <w:tblPr>
    <w:tblW w:w="5000" w:type="pct"/>
    <w:tblBorders>
      <w:top w:val="single" w:sz="4" w:space="0" w:color="666666"/>
      <w:left w:val="single" w:sz="4" w:space="0" w:color="666666"/>
      <w:bottom w:val="single" w:sz="4" w:space="0" w:color="666666"/>
      <w:right w:val="single" w:sz="4" w:space="0" w:color="666666"/>
      <w:insideH w:val="single" w:sz="4" w:space="0" w:color="666666"/>
      <w:insideV w:val="single" w:sz="4" w:space="0" w:color="666666"/>
    </w:tblBorders>
  </w:tblPr>
  $rowsXml
</w:tbl>
"@
}

function New-Paragraph([string]$text, [bool]$isBold) {
    $t = Escape-Xml $text
    $bold = New-BoldMark $isBold
    return "<w:p><w:r><w:rPr>${bold}<w:sz w:val=`"24`"/><w:szCs w:val=`"24`"/></w:rPr><w:t xml:space=`"preserve`">$t</w:t></w:r></w:p>"
}

# Turkish letters are code points so the script encoding cannot corrupt them.
$labelDate = "TAR" + [char]0x0130 + "H"
$labelParticipants = "Kat" + [char]0x0131 + "l" + [char]0x0131 + "mc" + [char]0x0131 + "lar"
$labelName = "Ad" + [char]0x0131 + " Soyad" + [char]0x0131
$labelTitle = [char]0x00DC + "nvan"
$labelAction = [char]0x0130 + [char]0x015F + "lem"
$labelTopics = "G" + [char]0x00D6 + "R" + [char]0x00DC + [char]0x015E + [char]0x00DC + "LEN KONULAR"
$labelActions = [char]0x0130 + [char]0x015E + "LEM MADDELER" + [char]0x0130
$labelMinutes = "TOPLANTI TUTANA" + [char]0x011E + "I"

$info = New-Table (@(
    (New-Row @("TOPLANTI ADI / KONUSU", "{{meetingName}}") $true)
    (New-Row @($labelDate, "{{meetingDate}}"))
    (New-Row @("SAAT", "{{meetingTime}}"))
    (New-Row @("YER", "{{meetingPlace}}"))
) -join "")

$people = New-Table (@(
    (New-Row @("No", $labelName, "Kurum", $labelTitle) $true)
    (New-Row @("", "", "", ""))
    (New-Row @("", "", "", ""))
    (New-Row @("", "", "", ""))
) -join "")

$actions = New-Table (@(
    (New-Row @("No", $labelAction, "Kurum", "Son tarih") $true)
    (New-Row @("", "", "", ""))
    (New-Row @("", "", "", ""))
    (New-Row @("", "", "", ""))
) -join "")

$narrative = @"
<w:tbl>
  <w:tblPr>
    <w:tblW w:w="5000" w:type="pct"/>
    <w:tblBorders>
      <w:top w:val="single" w:sz="4" w:space="0" w:color="666666"/>
      <w:left w:val="single" w:sz="4" w:space="0" w:color="666666"/>
      <w:bottom w:val="single" w:sz="4" w:space="0" w:color="666666"/>
      <w:right w:val="single" w:sz="4" w:space="0" w:color="666666"/>
    </w:tblBorders>
  </w:tblPr>
  <w:tr>
    <w:tc>
      <w:tcPr><w:tcW w:w="9360" w:type="dxa"/></w:tcPr>
      $(New-Paragraph $labelTopics $true)
      $(New-Paragraph "{{topics}}" $false)
      $(New-Paragraph $labelActions $true)
      $actions
      $(New-Paragraph "HAZIRLAYAN" $true)
      $(New-Paragraph "{{preparedBy}}" $false)
    </w:tc>
  </w:tr>
</w:tbl>
"@

$document = @"
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">
  <w:body>
    $(New-Paragraph $labelMinutes $true)
    $(New-Paragraph "" $false)
    $info
    $(New-Paragraph "" $false)
    $(New-Paragraph $labelParticipants $true)
    $people
    $(New-Paragraph "" $false)
    $narrative
    <w:sectPr>
      <w:pgSz w:w="11906" w:h="16838"/>
      <w:pgMar w:top="1134" w:right="1134" w:bottom="1134" w:left="1134" w:header="567" w:footer="567" w:gutter="0"/>
    </w:sectPr>
  </w:body>
</w:document>
"@

$contentTypes = @"
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
  <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
  <Default Extension="xml" ContentType="application/xml"/>
  <Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/>
  <Override PartName="/word/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.styles+xml"/>
  <Override PartName="/word/settings.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.settings+xml"/>
</Types>
"@

$docRels = @"
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/>
  <Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/settings" Target="settings.xml"/>
</Relationships>
"@

$styles = @"
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<w:styles xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">
  <w:docDefaults>
    <w:rPrDefault><w:rPr/></w:rPrDefault>
    <w:pPrDefault><w:pPr/></w:pPrDefault>
  </w:docDefaults>
</w:styles>
"@

$settings = @"
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<w:settings xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">
  <w:zoom w:percent="100"/>
</w:settings>
"@

$rels = @"
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="word/document.xml"/>
</Relationships>
"@

$tmp = Join-Path $env:TEMP "bv-minutes-seed-$([Guid]::NewGuid().ToString('N'))"
New-Item -ItemType Directory -Path (Join-Path $tmp "_rels") | Out-Null
New-Item -ItemType Directory -Path (Join-Path $tmp "word") | Out-Null
New-Item -ItemType Directory -Path (Join-Path $tmp "word/_rels") | Out-Null
$utf8 = New-Object System.Text.UTF8Encoding $false
[IO.File]::WriteAllText((Join-Path $tmp "[Content_Types].xml"), $contentTypes.Trim(), $utf8)
[IO.File]::WriteAllText((Join-Path $tmp "_rels/.rels"), $rels.Trim(), $utf8)
[IO.File]::WriteAllText((Join-Path $tmp "word/document.xml"), $document.Trim(), $utf8)
[IO.File]::WriteAllText((Join-Path $tmp "word/_rels/document.xml.rels"), $docRels.Trim(), $utf8)
[IO.File]::WriteAllText((Join-Path $tmp "word/styles.xml"), $styles.Trim(), $utf8)
[IO.File]::WriteAllText((Join-Path $tmp "word/settings.xml"), $settings.Trim(), $utf8)

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
if (Test-Path $outputDocx) { Remove-Item $outputDocx -Force }
$zip = [System.IO.Compression.ZipFile]::Open($outputDocx, [System.IO.Compression.ZipArchiveMode]::Create)
try {
    Get-ChildItem -Path $tmp -Recurse -File | ForEach-Object {
        $relative = $_.FullName.Substring($tmp.Length).TrimStart('\', '/').Replace('\', '/')
        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
            $zip, $_.FullName, $relative, [System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
}
finally {
    $zip.Dispose()
}
Remove-Item $tmp -Recurse -Force
Write-Host "OK $outputDocx"
