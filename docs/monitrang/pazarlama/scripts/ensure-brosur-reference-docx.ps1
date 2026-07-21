# MonitraNG — Broşür Pandoc referans DOCX (kendi kendine yeten)
#
# Pandoc'un varsayilan reference.docx'ini temel alir (duzgun tablo/paragraf stilleri),
# uzerine ozel margin + antet (header) + footer enjekte eder. DI bagimliligi yoktur.
#
# Antet (header): sol = dokuman adi, sag = site adresi
# Footer:         sol = "Sayfa N", sag = site adresi
#
# Cikti: docs/monitrang/pazarlama/templates/reference-brosur-mng-std.docx
#
# Usage (repo kokunden):
#   .\docs\monitrang\pazarlama\scripts\ensure-brosur-reference-docx.ps1
#   .\docs\monitrang\pazarlama\scripts\ensure-brosur-reference-docx.ps1 -Force

param(
    [string]$BaseUrl = "http://localhost:5040", # geriye donuk uyumluluk icin; kullanilmiyor
    [string]$DocumentTitle = "MonitraNG — Kurumsal Operasyon Platformu",
    [string]$SiteUrl = "www.monitrang.com",
    [switch]$Force = $false
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
$repoRoot = (Resolve-Path (Join-Path $scriptDir "../../../..")).Path
$templatesDir = Join-Path $repoRoot "docs/monitrang/pazarlama/templates"
$outPath = Join-Path $templatesDir "reference-brosur-mng-std.docx"

if ((Test-Path $outPath) -and -not $Force) {
    Write-Host "SKIP: Referans DOCX mevcut ($outPath)" -ForegroundColor Green
    Write-Host "Yenilemek icin -Force kullanin." -ForegroundColor Gray
    exit 0
}

# A4 sayfa + margin (twip). Icerik genisligi = 11906 - 1008 - 1008 = 9890 -> sag tab.
# Kompakt broşür: ~4 sayfa hedefi (varsayılan Pandoc 12pt/geniş boşluk ~7 sayfa üretiyordu).
$pageW = 11906
$pageH = 16838
$marL = 1008
$marR = 1008
$marT = 1134
$marB = 1134
$contentW = $pageW - $marL - $marR   # 9890

function Assert-Pandoc {
    $cmd = Get-Command pandoc -ErrorAction SilentlyContinue
    if (-not $cmd) { throw "pandoc bulunamadi. https://pandoc.org/installing.html" }
}

function Write-Utf8NoBom {
    param([string]$Path, [string]$Content)
    [System.IO.File]::WriteAllText($Path, $Content, [System.Text.UTF8Encoding]::new($false))
}

function Xml-Escape {
    param([string]$s)
    return $s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;")
}

Assert-Pandoc

if (-not (Test-Path $templatesDir)) {
    New-Item -ItemType Directory -Path $templatesDir -Force | Out-Null
}

Add-Type -AssemblyName System.IO.Compression.FileSystem

# 1) Pandoc varsayilan reference.docx
$defaultRef = Join-Path $env:TEMP "monitrang-pandoc-default-ref.docx"
if (Test-Path $defaultRef) { Remove-Item $defaultRef -Force }
& pandoc -o $defaultRef --print-default-data-file reference.docx
if ($LASTEXITCODE -ne 0 -or -not (Test-Path $defaultRef)) {
    throw "Pandoc varsayilan reference.docx uretilemedi (exit=$LASTEXITCODE)"
}

# 2) Calisma dizinine ac
$work = Join-Path $env:TEMP ("monitrang-ref-" + [Guid]::NewGuid().ToString("N"))
if (Test-Path $work) { Remove-Item $work -Recurse -Force }
[System.IO.Compression.ZipFile]::ExtractToDirectory($defaultRef, $work)

# 3) header1.xml / footer1.xml
$titleEsc = Xml-Escape $DocumentTitle
$siteEsc = Xml-Escape $SiteUrl

$headerXml = @"
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<w:hdr xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
  <w:p>
    <w:pPr>
      <w:tabs>
        <w:tab w:val="right" w:pos="$contentW" />
      </w:tabs>
      <w:pBdr>
        <w:bottom w:val="single" w:sz="4" w:space="6" w:color="D1D5DB" />
      </w:pBdr>
    </w:pPr>
    <w:r><w:rPr><w:b /><w:color w:val="111827" /><w:sz w:val="18" /></w:rPr><w:t xml:space="preserve">$titleEsc</w:t></w:r>
    <w:r><w:tab /></w:r>
    <w:r><w:rPr><w:color w:val="6B7280" /><w:sz w:val="18" /></w:rPr><w:t xml:space="preserve">$siteEsc</w:t></w:r>
  </w:p>
</w:hdr>
"@

$footerXml = @"
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<w:ftr xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
  <w:p>
    <w:pPr>
      <w:tabs>
        <w:tab w:val="right" w:pos="$contentW" />
      </w:tabs>
      <w:pBdr>
        <w:top w:val="single" w:sz="4" w:space="6" w:color="D1D5DB" />
      </w:pBdr>
    </w:pPr>
    <w:r><w:rPr><w:color w:val="6B7280" /><w:sz w:val="18" /></w:rPr><w:t xml:space="preserve">Sayfa </w:t></w:r>
    <w:r><w:rPr><w:color w:val="6B7280" /><w:sz w:val="18" /></w:rPr><w:fldChar w:fldCharType="begin" /></w:r>
    <w:r><w:rPr><w:color w:val="6B7280" /><w:sz w:val="18" /></w:rPr><w:instrText xml:space="preserve"> PAGE </w:instrText></w:r>
    <w:r><w:rPr><w:color w:val="6B7280" /><w:sz w:val="18" /></w:rPr><w:fldChar w:fldCharType="end" /></w:r>
    <w:r><w:tab /></w:r>
    <w:r><w:rPr><w:color w:val="6B7280" /><w:sz w:val="18" /></w:rPr><w:t xml:space="preserve">$siteEsc</w:t></w:r>
  </w:p>
</w:ftr>
"@

Write-Utf8NoBom (Join-Path $work "word/header1.xml") $headerXml
Write-Utf8NoBom (Join-Path $work "word/footer1.xml") $footerXml

# 4) [Content_Types].xml
$ctPath = Join-Path $work "[Content_Types].xml"
$ct = [System.IO.File]::ReadAllText($ctPath)
$ctAdd = '<Override PartName="/word/header1.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.header+xml" /><Override PartName="/word/footer1.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.footer+xml" />'
$ct = $ct.Replace("</Types>", $ctAdd + "</Types>")
Write-Utf8NoBom $ctPath $ct

# 5) word/_rels/document.xml.rels
$relsPath = Join-Path $work "word/_rels/document.xml.rels"
$rels = [System.IO.File]::ReadAllText($relsPath)
$relsAdd = '<Relationship Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/header" Id="rId900" Target="header1.xml" /><Relationship Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/footer" Id="rId901" Target="footer1.xml" />'
$rels = $rels.Replace("</Relationships>", $relsAdd + "</Relationships>")
Write-Utf8NoBom $relsPath $rels

# 6) word/document.xml — sectPr'i margin + header/footer ile degistir
$docPath = Join-Path $work "word/document.xml"
$doc = [System.IO.File]::ReadAllText($docPath)
$newSectPr = @"
<w:sectPr>
      <w:footnotePr>
        <w:numRestart w:val="eachSect" />
      </w:footnotePr>
      <w:headerReference w:type="default" r:id="rId900" />
      <w:footerReference w:type="default" r:id="rId901" />
      <w:pgSz w:w="$pageW" w:h="$pageH" />
      <w:pgMar w:top="$marT" w:right="$marR" w:bottom="$marB" w:left="$marL" w:header="708" w:footer="708" w:gutter="0" />
    </w:sectPr>
"@
$doc = [regex]::Replace($doc, '<w:sectPr>[\s\S]*?</w:sectPr>', { param($m) $newSectPr }, 1)
Write-Utf8NoBom $docPath $doc

# 7) styles.xml — kompakt tipografi (govde 10pt, basliklar kucuk, bosluk sikistirilmis)
$stylesPath = Join-Path $work "word/styles.xml"
$styles = [System.IO.File]::ReadAllText($stylesPath)

function Set-StyleSz {
    param([string]$Xml, [string]$StyleId, [string]$HalfPoints)
    return [regex]::Replace(
        $Xml,
        "(<w:style[^>]*w:styleId=`"$StyleId`"[^>]*>[\s\S]*?</w:style>)",
        {
            param($m)
            $b = $m.Groups[1].Value
            $b = [regex]::Replace($b, '(<w:sz w:val=")(\d+)(")', "`${1}$HalfPoints`${3}")
            $b = [regex]::Replace($b, '(<w:szCs w:val=")(\d+)(")', "`${1}$HalfPoints`${3}")
            return $b
        },
        1
    )
}

function Set-StyleSpacing {
    param([string]$Xml, [string]$StyleId, [string]$Before, [string]$After)
    return [regex]::Replace(
        $Xml,
        "(<w:style[^>]*w:styleId=`"$StyleId`"[^>]*>[\s\S]*?</w:style>)",
        {
            param($m)
            $b = $m.Groups[1].Value
            if ($b -match '<w:spacing ') {
                $b = [regex]::Replace($b, '(<w:spacing\b)([^/>]*)(/?>)', {
                        param($sm)
                        $attrs = $sm.Groups[2].Value
                        if ($attrs -match 'w:before=') {
                            $attrs = [regex]::Replace($attrs, 'w:before="\d+"', "w:before=`"$Before`"")
                        }
                        else {
                            $attrs = " w:before=`"$Before`"" + $attrs
                        }
                        if ($attrs -match 'w:after=') {
                            $attrs = [regex]::Replace($attrs, 'w:after="\d+"', "w:after=`"$After`"")
                        }
                        else {
                            $attrs = $attrs + " w:after=`"$After`""
                        }
                        return $sm.Groups[1].Value + $attrs + $sm.Groups[3].Value
                    }, 1)
            }
            return $b
        },
        1
    )
}

# Gövde varsayılanı: 12pt → 10pt; paragraf sonrası boşluk: 200 → 80
$styles = [regex]::Replace($styles, '(<w:rPrDefault>[\s\S]*?<w:sz w:val=")24(")', '${1}20${2}')
$styles = [regex]::Replace($styles, '(<w:rPrDefault>[\s\S]*?<w:szCs w:val=")24(")', '${1}20${2}')
$styles = [regex]::Replace($styles, '(<w:pPrDefault>[\s\S]*?<w:spacing w:after=")200(")', '${1}80${2}')

# Başlık / title boyutları (half-points: 2 = 1pt)
$styles = Set-StyleSz $styles "Title" "36"       # 18pt (önce 28)
$styles = Set-StyleSz $styles "TitleChar" "36"
$styles = Set-StyleSz $styles "Subtitle" "22"    # 11pt (önce 14)
$styles = Set-StyleSz $styles "SubtitleChar" "22"
$styles = Set-StyleSz $styles "Heading1" "26"    # 13pt (önce 20)
$styles = Set-StyleSz $styles "Heading1Char" "26"
$styles = Set-StyleSz $styles "Heading2" "22"    # 11pt (önce 16)
$styles = Set-StyleSz $styles "Heading2Char" "22"
$styles = Set-StyleSz $styles "Heading3" "20"    # 10pt (önce 14)
$styles = Set-StyleSz $styles "Heading3Char" "20"

# Boşluk sıkıştırma
$styles = Set-StyleSpacing $styles "BodyText" "40" "60"
$styles = Set-StyleSpacing $styles "Compact" "12" "12"
$styles = Set-StyleSpacing $styles "Heading1" "200" "40"
$styles = Set-StyleSpacing $styles "Heading2" "120" "40"
$styles = Set-StyleSpacing $styles "Heading3" "80" "30"
$styles = Set-StyleSpacing $styles "BlockText" "40" "40"
$styles = Set-StyleSpacing $styles "Caption" "0" "60"
$styles = Set-StyleSpacing $styles "Title" "0" "40"

# Compact (tablo hücreleri): 9pt
$styles = [regex]::Replace(
    $styles,
    '(<w:style[^>]*w:styleId="Compact"[^>]*>[\s\S]*?<w:pPr>[\s\S]*?</w:pPr>)',
    {
        param($m)
        if ($m.Value -match '<w:rPr>') { return $m.Value }
        return $m.Groups[1].Value + "`n    <w:rPr>`n      <w:sz w:val=`"18`" />`n      <w:szCs w:val=`"18`" />`n    </w:rPr>"
    },
    1
)

# Tablo hücre kenar boşluğu biraz sıkı
$styles = [regex]::Replace(
    $styles,
    '(<w:style[^>]*w:styleId="Table"[^>]*>[\s\S]*?<w:tblCellMar>[\s\S]*?</w:tblCellMar>)',
    {
        param($m)
        $b = $m.Groups[1].Value
        $b = [regex]::Replace($b, '(<w:left w:w=")108(")', '${1}60${2}')
        $b = [regex]::Replace($b, '(<w:right w:w=")108(")', '${1}60${2}')
        $b = [regex]::Replace($b, '(<w:top w:w=")0(")', '${1}20${2}')
        $b = [regex]::Replace($b, '(<w:bottom w:w=")0(")', '${1}20${2}')
        return $b
    },
    1
)

Write-Utf8NoBom $stylesPath $styles

# 8) Yeniden paketle (forward-slash entry adlariyla)
if (Test-Path $outPath) { Remove-Item $outPath -Force }
$fs = [System.IO.File]::Open($outPath, [System.IO.FileMode]::CreateNew)
$zip = [System.IO.Compression.ZipArchive]::new($fs, [System.IO.Compression.ZipArchiveMode]::Create)
try {
    $workFull = (Resolve-Path $work).Path
    $files = Get-ChildItem -Path $work -Recurse -File
    foreach ($f in $files) {
        $rel = $f.FullName.Substring($workFull.Length + 1).Replace('\', '/')
        $entry = $zip.CreateEntry($rel, [System.IO.Compression.CompressionLevel]::Optimal)
        $es = $entry.Open()
        $bytes = [System.IO.File]::ReadAllBytes($f.FullName)
        $es.Write($bytes, 0, $bytes.Length)
        $es.Dispose()
    }
}
finally {
    $zip.Dispose()
    $fs.Dispose()
}

Remove-Item $work -Recurse -Force -ErrorAction SilentlyContinue

Write-Host "OK referans DOCX: $outPath ($((Get-Item $outPath).Length) byte)" -ForegroundColor Green
Write-Host "  Antet: '$DocumentTitle'  |  $SiteUrl" -ForegroundColor Gray
Write-Host "  Footer: 'Sayfa N' (sol) | $SiteUrl (sag)" -ForegroundColor Gray
Write-Host "  Tipografi: govde 10pt, H1 13pt, H2 11pt, tablo 9pt (kompakt)" -ForegroundColor Gray
