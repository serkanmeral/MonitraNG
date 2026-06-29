# DOCX {{paramKey}} merge — yalnizca verilen anahtarlar doldurulur; digerleri oldugu gibi kalir.
#
# Kullanim:
#   .\merge-coc-docx.ps1 -InputDocx in.docx -OutputDocx out.docx -Values @{ docNo='ODK-COC-26-1'; orderNo='23Y...' }

param(
    [Parameter(Mandatory = $true)]
    [string]$InputDocx,
    [Parameter(Mandatory = $true)]
    [string]$OutputDocx,
    [hashtable]$Values = @{}
)

$ErrorActionPreference = "Stop"
if (-not (Test-Path $InputDocx)) { throw "Girdi bulunamadi: $InputDocx" }

function Test-ScannablePart([string]$Path) {
    $n = $Path.Replace('\', '/')
    if ($n -ieq 'word/document.xml') { return $true }
    if ($n -like 'word/header*.xml') { return $true }
    if ($n -like 'word/footer*.xml') { return $true }
    return $false
}

function Replace-Placeholders([string]$Text, [hashtable]$Map) {
    if ([string]::IsNullOrEmpty($Text) -or -not $Text.Contains('{{')) { return $Text }
    return [regex]::Replace($Text, '\{\{([a-zA-Z][a-zA-Z0-9_]*)\}\}', {
        param($m)
        $key = $m.Groups[1].Value
        foreach ($entry in $Map.GetEnumerator()) {
            if ($entry.Key.Equals($key, [StringComparison]::OrdinalIgnoreCase)) {
                return [string]$entry.Value
            }
        }
        return $m.Value
    })
}

function Merge-XmlPart {
    param([string]$XmlPath, [hashtable]$Map)
    [xml]$doc = Get-Content -LiteralPath $XmlPath -Raw -Encoding UTF8
    $ns = New-Object System.Xml.XmlNamespaceManager($doc.NameTable)
    $ns.AddNamespace('w', 'http://schemas.openxmlformats.org/wordprocessingml/2006/main')

    foreach ($p in @($doc.SelectNodes('//w:p', $ns))) {
        $textNodes = @($p.SelectNodes('.//w:t', $ns))
        if ($textNodes.Count -eq 0) { continue }
        $combined = ($textNodes | ForEach-Object { $_.InnerText }) -join ''
        $replaced = Replace-Placeholders -Text $combined -Map $Map
        if ($combined -eq $replaced) { continue }
        $textNodes[0].InnerText = $replaced
        for ($i = 1; $i -lt $textNodes.Count; $i++) {
            $textNodes[$i].InnerText = ''
        }
    }

    $settings = New-Object System.Xml.XmlWriterSettings
    $settings.Encoding = New-Object System.Text.UTF8Encoding($false)
    $settings.Indent = $false
    $writer = [System.Xml.XmlWriter]::Create($XmlPath, $settings)
    $doc.Save($writer)
    $writer.Close()
}

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

$tmp = Join-Path $env:TEMP "coc-merge-$([Guid]::NewGuid().ToString('N'))"
New-Item -ItemType Directory -Path $tmp | Out-Null
try {
    Copy-Item $InputDocx (Join-Path $tmp "source.zip") -Force
    $unpacked = Join-Path $tmp "unpacked"
    Expand-Archive (Join-Path $tmp "source.zip") -DestinationPath $unpacked -Force

    Get-ChildItem -Path $unpacked -Recurse -File | Where-Object {
        Test-ScannablePart $_.FullName.Substring($unpacked.Length).TrimStart('\', '/')
    } | ForEach-Object {
        Merge-XmlPart -XmlPath $_.FullName -Map $Values
    }

    $outDir = Split-Path $OutputDocx -Parent
    if ($outDir -and -not (Test-Path $outDir)) { New-Item -ItemType Directory -Path $outDir | Out-Null }
    if (Test-Path $OutputDocx) { Remove-Item $OutputDocx -Force }
    Compress-OoxmlFolder -SourceFolder $unpacked -DestinationFile $OutputDocx
    Write-Host "OK merge: $OutputDocx ($($Values.Count) deger)" -ForegroundColor Green
}
finally {
    if (Test-Path $tmp) { Remove-Item $tmp -Recurse -Force -ErrorAction SilentlyContinue }
}
