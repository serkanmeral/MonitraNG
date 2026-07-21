# Export Odak Kompozit customer proposal to PDF (cover page count + page numbers).
#
# Requires: pandoc, Microsoft Word
#
#   pwsh -File .\docs\odak\commercial\export-teklif-pdf.ps1
#   pwsh -File .\docs\odak\commercial\export-teklif-pdf.ps1 -OpenAfter

param(
    [string]$SourceMd = "",
    [string]$OutDir = "",
    [switch]$OpenAfter
)

$ErrorActionPreference = "Stop"

$scriptDir = $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($SourceMd)) {
    $SourceMd = Join-Path $scriptDir "Odak_Kompozit_Fiyat_Teklifi_MUSTERI.md"
}
if ([string]::IsNullOrWhiteSpace($OutDir)) {
    $OutDir = Join-Path $scriptDir "output"
}

$SourceMd = (Resolve-Path $SourceMd).Path
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

$stamp = Get-Date -Format "yyyyMMdd_HHmmss"
$workMd = Join-Path $OutDir "teklif_work_$stamp.md"
$docxPath = Join-Path $OutDir "Odak_Kompozit_Fiyat_Teklifi_$stamp.docx"
$pdfPath = Join-Path $OutDir "Odak_Kompozit_Fiyat_Teklifi_$stamp.pdf"

if (-not (Get-Command pandoc -ErrorAction SilentlyContinue)) {
    throw "pandoc bulunamadı. https://pandoc.org"
}

Copy-Item $SourceMd $workMd -Force

& pandoc $workMd -o $docxPath --from markdown -t docx `
    --metadata title="Fiyat Teklifi — Odak Kompozit"
if ($LASTEXITCODE -ne 0) { throw "pandoc docx üretimi başarısız (exit $LASTEXITCODE)" }

$word = $null
$doc = $null
try {
    $word = New-Object -ComObject Word.Application
    $word.Visible = $false
    $word.DisplayAlerts = 0

    $doc = $word.Documents.Open($docxPath, $false, $false)
    $null = $doc.Repaginate()
    $pageCount = [int]$doc.ComputeStatistics(2) # wdStatisticPages

    # Cover placeholder -> actual page count
    $find = $doc.Content.Find
    $find.ClearFormatting()
    $find.Replacement.ClearFormatting()
    $find.Text = "SAYFA_SAYISI"
    $find.Replacement.Text = [string]$pageCount
    $find.Forward = $true
    $find.Wrap = 1 # wdFindContinue
    $find.Format = $false
    $find.MatchCase = $true
    $find.MatchWholeWord = $true
    $null = $find.Execute([Type]::Missing, [Type]::Missing, [Type]::Missing, [Type]::Missing, [Type]::Missing, [Type]::Missing, [Type]::Missing, [Type]::Missing, [Type]::Missing, [Type]::Missing, 2)

    # Footer on each section: Sayfa X / Y
    foreach ($section in @($doc.Sections)) {
        $footer = $section.Footers.Item(1)
        $footer.LinkToPrevious = $false
        $rng = $footer.Range
        $rng.Delete()
        $rng = $footer.Range
        $rng.ParagraphFormat.Alignment = 1 # center
        $rng.Font.Size = 9
        $rng.Font.Name = "Calibri"
        $rng.Text = "Sayfa "
        $rng.Collapse(0) # end
        $null = $doc.Fields.Add($rng, 33) # PAGE
        $rng = $footer.Range
        $rng.Collapse(0)
        $rng.InsertAfter(" / ")
        $rng.Collapse(0)
        $null = $doc.Fields.Add($rng, 26) # NUMPAGES
    }

    $null = $doc.Fields.Update()
    $null = $doc.Repaginate()

    $mdText = [System.IO.File]::ReadAllText($workMd)
    $mdText = $mdText.Replace("SAYFA_SAYISI", [string]$pageCount)
    [System.IO.File]::WriteAllText($workMd, $mdText)

    # wdExportFormatPDF = 17
    $doc.ExportAsFixedFormat($pdfPath, 17)

    $doc.Save()
    $doc.Close($false)
    $doc = $null

    Write-Host "OK PDF : $pdfPath"
    Write-Host "OK DOCX: $docxPath"
    Write-Host "Sayfa  : $pageCount"
}
finally {
    if ($null -ne $doc) { try { $doc.Close($false) } catch {} }
    if ($null -ne $word) {
        try { $word.Quit() } catch {}
        [System.Runtime.InteropServices.Marshal]::ReleaseComObject($word) | Out-Null
    }
    [GC]::Collect()
    [GC]::WaitForPendingFinalizers()
}

if ($OpenAfter -and (Test-Path $pdfPath)) {
    Start-Process $pdfPath
}
