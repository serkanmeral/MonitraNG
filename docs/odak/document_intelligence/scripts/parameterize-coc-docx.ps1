# CoC DOCX: ornek degerleri {{paramKey}} placeholder'a cevirir.
# Font/run yapisini korur — yalnizca ilgili w:t metin dugumlerinin icerigi degisir.
# Uygunluk beyani / sizdirimlazlik gibi paragraflarin run bolunmesi ayni kalir.
#
# Kullanim:
#   .\parameterize-coc-docx.ps1 -InputDocx ..\sample\ODK-COC-prod-current.docx -OutputDocx ..\sample\ODK-COC-template-seed.docx

param(
    [Parameter(Mandatory = $true)]
    [string]$InputDocx,
    [Parameter(Mandatory = $true)]
    [string]$OutputDocx,
    [switch]$KeepLegacyBranding = $false
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $InputDocx)) { throw "Girdi bulunamadi: $InputDocx" }

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

function Remove-LegacyBrandingParts {
    param([string]$UnpackedRoot)
    foreach ($path in @(
        "word/header1.xml",
        "word/_rels/header1.xml.rels",
        "word/footer1.xml",
        "word/media/image1.jpeg",
        "word/media/image1.jpg"
    )) {
        $full = Join-Path $UnpackedRoot $path
        if (Test-Path $full) { Remove-Item $full -Force }
    }

    $docRelsPath = Join-Path $UnpackedRoot "word/_rels/document.xml.rels"
    if (Test-Path $docRelsPath) {
        $rels = [IO.File]::ReadAllText($docRelsPath, [Text.UTF8Encoding]::new($false))
        $rels = $rels -replace '<Relationship[^>]+Target="header1\.xml"[^>]*/>', ''
        $rels = $rels -replace '<Relationship[^>]+Target="footer1\.xml"[^>]*/>', ''
        [IO.File]::WriteAllText($docRelsPath, $rels, [Text.UTF8Encoding]::new($false))
    }

    $contentTypesPath = Join-Path $UnpackedRoot "[Content_Types].xml"
    if (Test-Path $contentTypesPath) {
        $ct = [IO.File]::ReadAllText($contentTypesPath, [Text.UTF8Encoding]::new($false))
        $ct = $ct -replace '<Override[^>]+PartName="/word/header1\.xml"[^>]*/>', ''
        $ct = $ct -replace '<Override[^>]+PartName="/word/footer1\.xml"[^>]*/>', ''
        [IO.File]::WriteAllText($contentTypesPath, $ct, [Text.UTF8Encoding]::new($false))
    }
}

function Update-TextNode {
    param([System.Xml.XmlElement]$Node, [string]$NewText)
    while ($Node.ChildNodes.Count -gt 0) { [void]$Node.RemoveChild($Node.FirstChild) }
    $Node.SetAttribute('space', 'http://www.w3.org/XML/1998/namespace', 'preserve') | Out-Null
    [void]$Node.AppendChild($Node.OwnerDocument.CreateTextNode($NewText))
}

function Remove-ParagraphContaining {
    param([System.Xml.XmlDocument]$Doc, [System.Xml.XmlNamespaceManager]$Ns, [scriptblock]$Predicate)
    $paras = $Doc.SelectNodes('//w:body/w:p', $Ns)
    foreach ($p in $paras) {
        $text = ($p.SelectNodes('.//w:t', $Ns) | ForEach-Object { $_.InnerText }) -join ''
        if (& $Predicate $text) {
            [void]$p.ParentNode.RemoveChild($p)
            return $true
        }
    }
    return $false
}

function Apply-TextNodeRules {
    param(
        [System.Xml.XmlDocument]$Doc,
        [System.Xml.XmlNamespaceManager]$Ns,
        [array]$Rules
    )
    foreach ($node in @($Doc.SelectNodes('//w:body//w:t', $Ns))) {
        $text = [string]$node.InnerText
        if ([string]::IsNullOrWhiteSpace($text)) { continue }

        foreach ($rule in $Rules) {
            if ($text -match $rule.Match) {
                $newText = if ($rule.Value -is [scriptblock]) { & $rule.Value $text } else { [string]$rule.Value }
                if ($newText -ne $text) {
                    Update-TextNode -Node $node -NewText $newText
                }
                break
            }
        }
    }
}

function Test-BodyHasDocNoField {
    param([System.Xml.XmlDocument]$Doc, [System.Xml.XmlNamespaceManager]$Ns)
    foreach ($p in @($Doc.SelectNodes('//w:body/w:p', $Ns))) {
        $t = ($p.SelectNodes('.//w:t', $Ns) | ForEach-Object { $_.InnerText }) -join ''
        if ($t -match '(?i)belge\s*numara') { return $true }
    }
    return $false
}

function Add-DocNoFieldParagraph {
    param([System.Xml.XmlDocument]$Doc, [System.Xml.XmlNamespaceManager]$Ns)
    $body = $Doc.SelectSingleNode('//w:body', $Ns)
    if (-not $body) { throw 'document.xml body yok' }
    $frag = @'
<w:p xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">
  <w:pPr><w:jc w:val="both"/></w:pPr>
  <w:r><w:rPr><w:sz w:val="24"/><w:szCs w:val="24"/></w:rPr><w:t xml:space="preserve">Belge Numarası         : {{poDocNo}}</w:t></w:r>
</w:p>
'@
    $fragDoc = New-Object System.Xml.XmlDocument
    $fragDoc.LoadXml($frag)
    $imported = $Doc.ImportNode($fragDoc.DocumentElement, $true)
    [void]$body.InsertBefore($imported, $body.FirstChild)
}

$fieldRules = @(
    @{ Match = '(?i)belge\s*numara\s*:\s*.+$'; Value = { param($m) ($m -replace '(?i)(:\s*).+$', ': {{poDocNo}}') } }
    @{ Match = '(?i)paketi\s*no\s*:\s*.+$'; Value = { param($m) ($m -replace '(?i)(:\s*).+$', ': {{workPackageNo}}') } }
    @{ Match = '(?i)zenlenme\s*tarihi\s*:\s*.+$'; Value = { param($m) ($m -replace '(?i)(:\s*).+$', ': {{issueDate}}') } }
    @{ Match = '(?i)firma\s*bilgileri\s*:\s*.+$'; Value = { param($m) ($m -replace '(?i)(:\s*).+$', ': {{customerName}}') } }
    @{ Match = '(?i)sipari\S*\s*no\s*:\s*.+$'; Value = { param($m) ($m -replace '(?i)(:\s*).+$', ': {{orderNo}}') } }
    @{ Match = '(?i)par\S*a\s+t\S*\s*:\s*.+$'; Value = { param($m) ($m -replace '(?i)(:\s*).+$', ': {{partDescription}}') } }
    @{ Match = '(?i)teknik\s*resim\s*no\s*:\s*.+$'; Value = { param($m) ($m -replace '(?i)(:\s*).+$', ': {{drawingNo}} ({{drawingRef}})') } }
    @{ Match = '(?i)revizyon\s*no\s*:\s*.+$'; Value = { param($m) ($m -replace '(?i)(:\s*).+$', ': {{drawingRevision}}') } }
    @{ Match = '(?i)adedi\s*:\s*.+$'; Value = { param($m) ($m -replace '(?i)(:\s*).+$', ': {{partQuantity}}') } }
    @{ Match = '(?i)seri\s*no\s*:\s*.+$'; Value = { param($m) ($m -replace '(?i)(:\s*).+$', ': {{serialNo}}') } }
)

$narrativeRules = @(
    @{
        Match = '(?i)sipari\S*inizde|belirtilen\s+\S*r\S*nlerin'
        Value = {
            param($m)
            $m = $m -replace '(?i)\bODK-COC-\d{2}-\d+\b', '{{docNo}}'
            $m = $m -replace '(?i)\b23Y\d+\w*\b', '{{orderNo}}'
            $m = $m -replace '(?i)GN-\d{4}-\d{4}', '{{drawingRef}}'
            $m
        }
    }
    @{
        Match = '(?i)teknik\s*resimde|\S*retildi\S*\s*beyan'
        Value = {
            param($m)
            $m -replace '(?i)\bREV\s*AA\b', '{{drawingRevision}}'
        }
    }
    @{
        Match = '(?i)^GN-\d{4}-\d{4}(\s*)$'
        Value = { param($m) ($m -replace '(?i)^GN-\d{4}-\d{4}(\s*)$', '{{drawingRef}}$1') }
    }
    @{
        Match = '(?i)s\S*zd\S*rmazl\S*k\s*test|mbar\s*test'
        Value = {
            param($m)
            $m = $m -replace '(?i)\bGN-\d{4}-\d{4}\b', '{{drawingRef}}'
            $m = $m -replace '(?i)\bRev\s*AA\b', 'Rev {{drawingRevision}}'
            $m = $m -replace '(?i)Not\s*\d+', 'Not {{leakTestNoteNo}}'
            $m = $m -replace '(?i)\b50\s*mbar\b', '{{leakTestPressureMbar}} mbar'
            $m
        }
    }
    @{
        Match = '(?i)^g[iı]zem\s+canda'
        Value = '{{signatoryName}}'
    }
    @{
        Match = '(?i)kalite\s*kontrol'
        Value = '{{signatoryTitle}}'
    }
)

$tmp = Join-Path $env:TEMP "coc-param-$([Guid]::NewGuid().ToString('N'))"
New-Item -ItemType Directory -Path $tmp | Out-Null
try {
    Copy-Item $InputDocx (Join-Path $tmp "source.zip") -Force
    $unpacked = Join-Path $tmp "unpacked"
    Expand-Archive (Join-Path $tmp "source.zip") -DestinationPath $unpacked -Force

    if (-not $KeepLegacyBranding) {
        Remove-LegacyBrandingParts -UnpackedRoot $unpacked
    }

    $docPath = Join-Path $unpacked "word/document.xml"
    [xml]$doc = Get-Content $docPath -Raw -Encoding UTF8
    $ns = New-Object System.Xml.XmlNamespaceManager($doc.NameTable)
    $ns.AddNamespace('w', 'http://schemas.openxmlformats.org/wordprocessingml/2006/main')

    [void](Remove-ParagraphContaining -Doc $doc -Ns $ns -Predicate {
            param($t)
            $t -match '(?i)^\s*uygunluk\s*belges'
        })

    if (-not (Test-BodyHasDocNoField -Doc $doc -Ns $ns)) {
        Add-DocNoFieldParagraph -Doc $doc -Ns $ns
    }

    Apply-TextNodeRules -Doc $doc -Ns $ns -Rules $fieldRules
    Apply-TextNodeRules -Doc $doc -Ns $ns -Rules $narrativeRules

    $settings = New-Object System.Xml.XmlWriterSettings
    $settings.Encoding = New-Object System.Text.UTF8Encoding($false)
    $settings.Indent = $false
    $settings.OmitXmlDeclaration = $false
    $writer = [System.Xml.XmlWriter]::Create($docPath, $settings)
    $doc.Save($writer)
    $writer.Close()

    $outDir = Split-Path $OutputDocx -Parent
    if ($outDir -and -not (Test-Path $outDir)) { New-Item -ItemType Directory -Path $outDir | Out-Null }
    if (Test-Path $OutputDocx) { Remove-Item $OutputDocx -Force }
    Compress-OoxmlFolder -SourceFolder $unpacked -DestinationFile $OutputDocx
    Write-Host "OK: $OutputDocx" -ForegroundColor Green
}
finally {
    if (Test-Path $tmp) { Remove-Item $tmp -Recurse -Force -ErrorAction SilentlyContinue }
}
