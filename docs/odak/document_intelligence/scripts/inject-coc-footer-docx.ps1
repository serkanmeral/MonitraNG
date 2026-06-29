# ODK CoC kurumsal altbilgi — 2 sutunlu tablo (FooterInjector B2 ile uyumlu).
# Prod backend eski surumdeyken footer tablo duzenini WOPI oncesi yerelde uygular.
#
# Kullanim:
#   .\inject-coc-footer-docx.ps1 -InputDocx ..\sample\ODK-COC-template-seed.docx -OutputDocx ..\sample\ODK-COC-with-footer.docx

param(
    [Parameter(Mandatory = $true)]
    [string]$InputDocx,
    [Parameter(Mandatory = $true)]
    [string]$OutputDocx,
    [string]$SeedJson = "",
    [string]$AppsettingsJson = "",
    [switch]$WhatIf = $false
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $InputDocx)) { throw "Girdi bulunamadi: $InputDocx" }

$scriptDir = $PSScriptRoot
$repoRoot = (Resolve-Path (Join-Path $scriptDir "../../../..")).Path
if ([string]::IsNullOrWhiteSpace($SeedJson)) {
    $SeedJson = Join-Path $repoRoot "docs/odak/document_intelligence/datasets/seed-designer-template-coc-standard.json"
}
if ([string]::IsNullOrWhiteSpace($AppsettingsJson)) {
    $AppsettingsJson = Join-Path $repoRoot "MngDocument/Presentation/MngDocument.Api/appsettings.json"
}

$seed = [IO.File]::ReadAllText($SeedJson, [Text.UTF8Encoding]::new($false)) | ConvertFrom-Json
$footerFlags = $seed.template.footer
$pageLayout = $seed.template.pageLayout
$leftIndent = if ($pageLayout.footerLeftIndentTwips) { [int]$pageLayout.footerLeftIndentTwips } else { -567 }

$app = [IO.File]::ReadAllText($AppsettingsJson, [Text.UTF8Encoding]::new($false)) | ConvertFrom-Json
$profile = $app.MngDocumentSettings.FooterProfile
if (-not $profile) { throw "FooterProfile bulunamadi: $AppsettingsJson" }

$ContentWidthTwips = 8316
$ColumnWidthTwips = $ContentWidthTwips / 2

function Escape-Xml([string]$value) {
    if ([string]::IsNullOrEmpty($value)) { return "" }
    return $value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace('"', "&quot;")
}

function Build-ContactLine($office) {
    $parts = @()
    if ($office.Phone) { $parts += "Tel: $($office.Phone.Trim())" }
    if ($office.Fax) { $parts += "Faks: $($office.Fax.Trim())" }
    return (Escape-Xml ($parts -join "     "))
}

$offices = @($profile.Offices)
while ($offices.Count -lt 2) { $offices += [pscustomobject]@{ Label = ""; Address = ""; Phone = ""; Fax = "" } }

$BoldRunProps = '<w:rPr><w:rFonts w:ascii="Tahoma" w:hAnsi="Tahoma" w:cs="Tahoma"/><w:b/><w:color w:val="231F20"/><w:w w:val="80"/><w:sz w:val="16"/><w:szCs w:val="16"/></w:rPr>'
$NormalRunProps = '<w:rPr><w:rFonts w:ascii="Tahoma" w:hAnsi="Tahoma" w:cs="Tahoma"/><w:color w:val="231F20"/><w:w w:val="80"/><w:kern w:val="22"/><w:sz w:val="16"/><w:szCs w:val="16"/></w:rPr>'
$RevisionRunProps = '<w:rPr><w:sz w:val="14"/><w:szCs w:val="12"/></w:rPr>'

function Build-TableCell([string]$text, [string]$runProps, [int]$widthTwips) {
    @"
<w:tc>
  <w:tcPr>
    <w:tcW w:w="$widthTwips" w:type="dxa"/>
    <w:vAlign w:val="top"/>
  </w:tcPr>
  <w:p>
    <w:pPr><w:jc w:val="both"/></w:pPr>
    <w:r>$runProps<w:t xml:space="preserve">$text</w:t></w:r>
  </w:p>
</w:tc>
"@
}

function Build-TwoColumnRow([string]$left, [string]$right, [bool]$bold) {
    $runProps = if ($bold) { $BoldRunProps } else { $NormalRunProps }
    @"
<w:tr>
  $(Build-TableCell $left $runProps $ColumnWidthTwips)
  $(Build-TableCell $right $runProps $ColumnWidthTwips)
</w:tr>
"@
}

function Build-MergedRow([string]$text, [string]$runProps) {
    @"
<w:tr>
  <w:tc>
    <w:tcPr>
      <w:gridSpan w:val="2"/>
      <w:tcW w:w="$ContentWidthTwips" w:type="dxa"/>
    </w:tcPr>
    <w:p>
      <w:pPr><w:jc w:val="both"/></w:pPr>
      <w:r>$runProps<w:t xml:space="preserve">$text</w:t></w:r>
    </w:p>
  </w:tc>
</w:tr>
"@
}

function Build-DividerRow {
    @"
<w:tr>
  <w:tc>
    <w:tcPr>
      <w:gridSpan w:val="2"/>
      <w:tcW w:w="$ContentWidthTwips" w:type="dxa"/>
    </w:tcPr>
    <w:p>
      <w:pPr>
        <w:pBdr>
          <w:top w:val="single" w:sz="12" w:space="1" w:color="231F20"/>
        </w:pBdr>
      </w:pPr>
    </w:p>
  </w:tc>
</w:tr>
"@
}

$rows = @()
if ($footerFlags.showFormRevision) {
    $revision = "$(Escape-Xml $profile.FormCode) $(Escape-Xml $profile.FormRevision) $(Escape-Xml $profile.FormRevisionDate)".Trim()
    $rows += Build-MergedRow $revision $RevisionRunProps
}
if ($footerFlags.showOfficeColumns) {
    $rows += Build-TwoColumnRow (Escape-Xml $offices[0].Label) (Escape-Xml $offices[1].Label) $true
}
if ($footerFlags.showAddresses) {
    $rows += Build-TwoColumnRow (Escape-Xml $offices[0].Address) (Escape-Xml $offices[1].Address) $false
}
if ($footerFlags.showDividerLine) {
    $rows += Build-DividerRow
}
if ($footerFlags.showContacts) {
    $rows += Build-TwoColumnRow (Build-ContactLine $offices[0]) (Build-ContactLine $offices[1]) $false
}
$rows += Build-MergedRow "" '<w:rPr><w:sz w:val="16"/><w:szCs w:val="16"/></w:rPr>'

$footerXml = @"
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<w:ftr xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">
  <w:tbl>
    <w:tblPr>
      <w:tblW w:w="5000" w:type="pct"/>
      <w:tblInd w:w="$leftIndent" w:type="dxa"/>
      <w:tblLayout w:type="fixed"/>
      <w:tblCellMar>
        <w:top w:w="0" w:type="dxa"/>
        <w:left w:w="0" w:type="dxa"/>
        <w:bottom w:w="0" w:type="dxa"/>
        <w:right w:w="0" w:type="dxa"/>
      </w:tblCellMar>
      <w:tblLook w:val="04A0" w:firstRow="1" w:lastRow="0" w:firstColumn="1" w:lastColumn="0" w:noHBand="0" w:noVBand="1"/>
    </w:tblPr>
    <w:tblGrid>
      <w:gridCol w:w="$ColumnWidthTwips"/>
      <w:gridCol w:w="$ColumnWidthTwips"/>
    </w:tblGrid>
    $($rows -join "")
  </w:tbl>
</w:ftr>
"@

function Compress-OoxmlFolder {
    param([string]$SourceFolder, [string]$DestinationFile)
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    if (Test-Path $DestinationFile) { Remove-Item $DestinationFile -Force }
    $zip = [System.IO.Compression.ZipFile]::Open($DestinationFile, [System.IO.Compression.ZipArchiveMode]::Create)
    try {
        Get-ChildItem -Path $SourceFolder -Recurse -File | ForEach-Object {
            $relative = $_.FullName.Substring($SourceFolder.Length).TrimStart('\', '/').Replace('\', '/')
            [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
                $zip, $_.FullName, $relative, [System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
        }
    }
    finally { $zip.Dispose() }
}

function Update-DocumentRels([string]$RelsPath) {
    [xml]$doc = Get-Content $RelsPath -Raw -Encoding UTF8
    $ns = New-Object System.Xml.XmlNamespaceManager($doc.NameTable)
    $ns.AddNamespace('r', 'http://schemas.openxmlformats.org/package/2006/relationships')
    $root = $doc.DocumentElement
    foreach ($rel in @($root.SelectNodes("//r:Relationship", $ns))) {
        $target = [string]$rel.GetAttribute('Target')
        $id = [string]$rel.GetAttribute('Id')
        if ($target -eq 'footer1.xml' -or $id -eq 'rIdFooter1' -or $id -eq 'rId9') {
            [void]$root.RemoveChild($rel)
        }
    }
    $newRel = $doc.CreateElement('Relationship', $ns.LookupNamespace('r'))
    [void]$newRel.SetAttribute('Id', 'rIdFooter1')
    [void]$newRel.SetAttribute('Type', 'http://schemas.openxmlformats.org/officeDocument/2006/relationships/footer')
    [void]$newRel.SetAttribute('Target', 'footer1.xml')
    [void]$root.AppendChild($newRel)
    $settings = New-Object System.Xml.XmlWriterSettings
    $settings.Encoding = New-Object System.Text.UTF8Encoding($false)
    $settings.Indent = $false
    $writer = [System.Xml.XmlWriter]::Create($RelsPath, $settings)
    $doc.Save($writer)
    $writer.Close()
}

function Update-ContentTypes([string]$Path) {
    [xml]$doc = Get-Content -LiteralPath $Path -Raw -Encoding UTF8
    $nsUri = $doc.DocumentElement.NamespaceURI
    foreach ($node in @($doc.DocumentElement.ChildNodes)) {
        if ($node.LocalName -eq 'Override' -and [string]$node.GetAttribute('PartName') -eq '/word/footer1.xml') {
            [void]$doc.DocumentElement.RemoveChild($node)
        }
    }
    $override = $doc.CreateElement('Override', $nsUri)
    [void]$override.SetAttribute('PartName', '/word/footer1.xml')
    [void]$override.SetAttribute('ContentType', 'application/vnd.openxmlformats-officedocument.wordprocessingml.footer+xml')
    [void]$doc.DocumentElement.AppendChild($override)
    $settings = New-Object System.Xml.XmlWriterSettings
    $settings.Encoding = New-Object System.Text.UTF8Encoding($false)
    $settings.Indent = $false
    $writer = [System.Xml.XmlWriter]::Create($Path, $settings)
    $doc.Save($writer)
    $writer.Close()
}

function Ensure-FooterReference([string]$DocPath) {
    [xml]$doc = Get-Content $DocPath -Raw -Encoding UTF8
    $w = 'http://schemas.openxmlformats.org/wordprocessingml/2006/main'
    $r = 'http://schemas.openxmlformats.org/officeDocument/2006/relationships'
    $ns = New-Object System.Xml.XmlNamespaceManager($doc.NameTable)
    $ns.AddNamespace('w', $w)
    $ns.AddNamespace('r', $r)
    $body = $doc.SelectSingleNode('//w:body', $ns)
    if (-not $body) { throw 'document.xml body yok' }

    $sectPr = $body.SelectSingleNode('w:sectPr', $ns)
    if (-not $sectPr) {
        $sectPr = $doc.CreateElement('sectPr', $w)
        [void]$body.AppendChild($sectPr)
    }

    foreach ($fr in @($sectPr.SelectNodes('w:footerReference', $ns))) {
        [void]$sectPr.RemoveChild($fr)
    }
    $footerRef = $doc.CreateElement('footerReference', $w)
    [void]$footerRef.SetAttribute('id', $r, 'rIdFooter1')
    [void]$footerRef.SetAttribute('type', $w, 'default')
    [void]$sectPr.AppendChild($footerRef)

    $settings = New-Object System.Xml.XmlWriterSettings
    $settings.Encoding = New-Object System.Text.UTF8Encoding($false)
    $settings.Indent = $false
    $writer = [System.Xml.XmlWriter]::Create($DocPath, $settings)
    $doc.Save($writer)
    $writer.Close()
}

if ($WhatIf) {
    Write-Host "WhatIf: footer tablo enjekte edilecek (tblInd=$leftIndent, col=$ColumnWidthTwips)" -ForegroundColor Yellow
    exit 0
}

$tmp = Join-Path $env:TEMP "coc-footer-$([Guid]::NewGuid().ToString('N'))"
New-Item -ItemType Directory -Path $tmp | Out-Null
try {
    Copy-Item $InputDocx (Join-Path $tmp "source.zip") -Force
    $unpacked = Join-Path $tmp "unpacked"
    Expand-Archive (Join-Path $tmp "source.zip") -DestinationPath $unpacked -Force

    [IO.File]::WriteAllText((Join-Path $unpacked "word/footer1.xml"), $footerXml, [Text.UTF8Encoding]::new($false))
    Update-DocumentRels (Join-Path $unpacked "word/_rels/document.xml.rels")
    Update-ContentTypes (Join-Path $unpacked "[Content_Types].xml")
    Ensure-FooterReference (Join-Path $unpacked "word/document.xml")

    $outDir = Split-Path $OutputDocx -Parent
    if ($outDir -and -not (Test-Path $outDir)) { New-Item -ItemType Directory -Path $outDir | Out-Null }
    if (Test-Path $OutputDocx) { Remove-Item $OutputDocx -Force }
    Compress-OoxmlFolder -SourceFolder $unpacked -DestinationFile $OutputDocx
    Write-Host "OK footer tablo: $OutputDocx" -ForegroundColor Green
}
finally {
    if (Test-Path $tmp) { Remove-Item $tmp -Recurse -Force -ErrorAction SilentlyContinue }
}
