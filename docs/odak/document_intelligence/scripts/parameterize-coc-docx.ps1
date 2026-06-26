# CoC gövde metnindeki örnek/sabit değerleri {{paramKey}} placeholder'a çevirir.
# Font/run yapısına dokunmaz; yalnızca w:t metin düğümleri ve gereksiz paragraflar güncellenir.
# İsteğe bağlı: legacy gömülü header/footer parçalarını kaldırır (API antet/altbilgi için).
#
# Kullanım:
#   .\parameterize-coc-docx.ps1 -InputDocx ..\sample\ODK-COC-prod-current.docx -OutputDocx ..\sample\ODK-COC-template-seed.docx
#   .\parameterize-coc-docx.ps1 -InputDocx in.docx -OutputDocx out.docx -KeepLegacyBranding

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
            $t -match '(?i)belge\s*numara'
        })

    $textNodes = @($doc.SelectNodes('//w:body//w:t', $ns))
    $replacements = @(
        @{ Match = '(?i)paketi\s*no\s*:\s*.+$'; Value = { param($m) ($m -replace '(?i)(:\s*).+$', ': {{workPackageNo}}') } }
        @{ Match = '(?i)zenlenme\s*tarihi\s*:\s*.+$'; Value = { param($m) ($m -replace '(?i)(:\s*).+$', ': {{issueDate}}') } }
        @{ Match = '(?i)firma\s*bilgileri\s*:\s*.+$'; Value = { param($m) ($m -replace '(?i)(:\s*).+$', ': {{customerName}}') } }
        @{ Match = '(?i)sipari\S*\s*no\s*:\s*.+$'; Value = { param($m) ($m -replace '(?i)(:\s*).+$', ': {{orderNo}}') } }
        @{ Match = '(?i)tanim\S*\s*:\s*.+$'; Value = { param($m) ($m -replace '(?i)(:\s*).+$', ': {{partDescription}}') } }
        @{ Match = '(?i)teknik\s*resim\s*no\s*:\s*.+$'; Value = { param($m) ($m -replace '(?i)(:\s*).+$', ': {{drawingNo}} ({{drawingRef}})') } }
        @{ Match = '(?i)revizyon\s*no\s*:\s*.+$'; Value = { param($m) ($m -replace '(?i)(:\s*).+$', ': {{drawingRevision}}') } }
        @{ Match = '(?i)adedi\s*:\s*.+$'; Value = { param($m) ($m -replace '(?i)(:\s*).+$', ': {{partQuantity}}') } }
        @{ Match = '(?i)seri\s*no\s*:\s*.+$'; Value = { param($m) ($m -replace '(?i)(:\s*).+$', ': {{serialNo}}') } }
    )

    foreach ($node in $textNodes) {
        $text = [string]$node.InnerText
        if ([string]::IsNullOrWhiteSpace($text)) { continue }

        $handled = $false
        foreach ($rule in $replacements) {
            if ($text -match $rule.Match) {
                $newText = if ($rule.Value -is [scriptblock]) { & $rule.Value $text } else { $rule.Value }
                Update-TextNode -Node $node -NewText $newText
                $handled = $true
                break
            }
        }
        if ($handled) { continue }

        if ($text -match '(?i)POLARIZOR\s+BOYNUZ') {
            Update-TextNode -Node $node -NewText ($text -replace '(?i)(:\s*).+$', ': {{partDescription}}')
            continue
        }
        if ($text -match '(?i)sipari\S*inizde|belirtilen\s+\S*r\S*nlerin') {
            Update-TextNode -Node $node -NewText '{{complianceStatement}}'
            continue
        }
        if ($text -match '(?i)teknik\s*resimde|\S*retildi\S*\s*beyan') {
            Update-TextNode -Node $node -NewText ''
            continue
        }
        if ($text -match '(?i)s\S*zd\S*rmazl\S*k\s*test|mbar\s*test') {
            Update-TextNode -Node $node -NewText '{{leakTestStatement}}'
            continue
        }
        if ($text -match '(?i)^GN-\d{4}-\d{4}\s*$') {
            Update-TextNode -Node $node -NewText ''
            continue
        }
        if ($text -match '(?i)^a[cç][ıi]klamalar\s*:') { continue }
        if ($text -match '(?i)malzemelerin\s*uygunluk') {
            Update-TextNode -Node $node -NewText '{{attachmentsNote}}'
            continue
        }
        if ($text -match '(?i)malzemelerin\s*s\S*cak|boya\s*uygunluk') {
            Update-TextNode -Node $node -NewText ''
            continue
        }
        if ($text -match '(?i)^g[iı]zem\s+canda') {
            Update-TextNode -Node $node -NewText '{{signatoryName}}'
            continue
        }
        if ($text -match '(?i)kalite\s*kontrol') {
            Update-TextNode -Node $node -NewText '{{signatoryTitle}}'
            continue
        }
    }

    # Birden fazla attachmentsNote node varsa yalnızca birincisini tut
    $attachmentNodes = @($doc.SelectNodes('//w:body//w:t', $ns) | Where-Object { $_.InnerText -eq '{{attachmentsNote}}' })
    if ($attachmentNodes.Count -gt 1) {
        for ($i = 1; $i -lt $attachmentNodes.Count; $i++) {
            Update-TextNode -Node $attachmentNodes[$i] -NewText ''
        }
    }

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
