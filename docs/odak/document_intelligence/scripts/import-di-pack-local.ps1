# Import Odak DI pack binaries into local Docker (existing dm_* rows from Mongo dump).
# Uses DataGateway file upload + path patch — does not require mngdocument.
#
#   pwsh -File .\docs\odak\document_intelligence\scripts\import-di-pack-local.ps1
#   pwsh -File .\docs\odak\document_intelligence\scripts\import-di-pack-local.ps1 -WhatIf

param(
    [string]$PackDir = "",
    [string]$DataGatewayBaseUrl = "http://localhost:5010",
    [string]$KeeperBaseUrl = "http://localhost:5001",
    [string]$DomainName = "odak",
    [string]$Username = "odak_admin",
    [string]$Password = "Admin123!",
    [string]$Token = $env:DI_TOKEN,
    [switch]$WhatIf
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
$repoRoot = (Resolve-Path (Join-Path $scriptDir "../../../..")).Path
if ([string]::IsNullOrWhiteSpace($PackDir)) {
    $PackDir = Join-Path $repoRoot "docs/odak/exports/odak-di-pack-20260711"
}
$PackDir = (Resolve-Path $PackDir).Path
$manifestPath = Join-Path $PackDir "manifest.json"
if (-not (Test-Path $manifestPath)) { throw "manifest.json yok: $manifestPath" }

if ([string]::IsNullOrWhiteSpace($Token)) {
    $body = @{ username = $Username; password = $Password; domain = $DomainName } | ConvertTo-Json
    $Token = (Invoke-RestMethod "$KeeperBaseUrl/api/auth/token" -Method POST -Body $body -ContentType "application/json").accessToken
}
$Token = $Token.Trim()
$headers = @{
    Authorization = "Bearer $Token"
    "Content-Type" = "application/json"
}

function Invoke-Dg {
    param([string]$Method, [string]$Path, [object]$Body = $null)
    $uri = "$DataGatewayBaseUrl$Path"
    $params = @{ Uri = $uri; Method = $Method; Headers = $headers; TimeoutSec = 300 }
    if ($null -ne $Body) {
        $params.Body = ($Body | ConvertTo-Json -Depth 20 -Compress)
    }
    return Invoke-RestMethod @params
}

function Find-Binary([string]$Dir, [string[]]$Names) {
    foreach ($n in $Names) {
        $p = Join-Path $Dir $n
        if (Test-Path $p) { return (Get-Item $p) }
    }
    $hit = Get-ChildItem -Path $Dir -File | Where-Object {
        $_.Extension -match '\.(docx|xlsx|pptx|zip)$' -and $_.Name -ne "meta.json"
    } | Select-Object -First 1
    return $hit
}

function Upload-And-Patch {
    param(
        [string]$Kind,
        [string]$Code,
        [string]$RecordId,
        [string]$DatasetName,
        [string]$FileFieldName,
        [string]$PathFieldName,
        [string]$NameFieldName,
        [System.IO.FileInfo]$File,
        [hashtable]$ExtraPatch = @{}
    )

    if (-not $File -or $File.Length -le 0) {
        throw "${Kind}/${Code}: binary yok veya bos"
    }
    $bytes = [IO.File]::ReadAllBytes($File.FullName)
    $b64 = [Convert]::ToBase64String($bytes)

    Write-Host ("  {0} {1} id={2} file={3} ({4} bytes)" -f $Kind, $Code, $RecordId, $File.Name, $bytes.Length) -ForegroundColor DarkGray

    if ($WhatIf) {
        Write-Host "    WhatIf upload+patch" -ForegroundColor Yellow
        return [pscustomobject]@{ kind = $Kind; code = $Code; bytes = $bytes.Length; status = "whatIf" }
    }

    $uploadBody = @{
        Content          = $b64
        DatasetName      = $DatasetName
        FieldName        = $FileFieldName
        RecordId         = $RecordId
        OriginalFileName = $File.Name
        UseCompression   = $false
        UseEncryption    = $false
    }
    $upload = Invoke-Dg -Method POST -Path "/api/v1/files/upload" -Body $uploadBody

    $filePath = $null
    if ($upload.data) {
        $filePath = $upload.data.filePath
        if (-not $filePath) { $filePath = $upload.data.FilePath }
    }
    if (-not $filePath -and $upload.Data) {
        $filePath = $upload.Data.FilePath
        if (-not $filePath) { $filePath = $upload.Data.filePath }
    }
    if ([string]::IsNullOrWhiteSpace($filePath)) {
        throw "${Kind}/${Code}: upload filePath bos. Response: $($upload | ConvertTo-Json -Depth 6 -Compress)"
    }

    $storedName = $File.Name
    if ($upload.data -and $upload.data.originalFileName) { $storedName = $upload.data.originalFileName }
    elseif ($upload.data -and $upload.data.OriginalFileName) { $storedName = $upload.data.OriginalFileName }

    $patch = @{
        $PathFieldName = $filePath
        $NameFieldName = $storedName
        # MngDocument ResolveDesignPath prefers nested file.path over *StoragePath
        $FileFieldName = @{
            path           = $filePath
            file_name      = $storedName
            file_ext       = "zip"
            upload_person  = "import-di-pack-local"
            upload_time    = (Get-Date).ToUniversalTime().ToString("o")
            file_size      = [math]::Round($bytes.Length / 1KB)
        }
        updatedAt      = (Get-Date).ToUniversalTime().ToString("o")
        updatedBy      = "import-di-pack-local"
    }
    foreach ($k in $ExtraPatch.Keys) { $patch[$k] = $ExtraPatch[$k] }

    Invoke-Dg -Method PUT -Path "/api/v1/data/$DatasetName/$RecordId" -Body $patch | Out-Null
    Write-Host ("    OK -> {0}" -f $filePath) -ForegroundColor Green
    return [pscustomobject]@{ kind = $Kind; code = $Code; bytes = $bytes.Length; path = $filePath; status = "ok" }
}

$manifest = Get-Content $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
Write-Host "=== DI pack import (binary rehydrate) ===" -ForegroundColor Cyan
Write-Host "Pack: $PackDir"
Write-Host "DG:   $DataGatewayBaseUrl"
Write-Host ("Manifest: templates={0} letterheads={1} covers={2} failures={3}" -f `
    $manifest.counts.templates, $manifest.counts.letterheads, $manifest.counts.coverPages, @($manifest.failures).Count)

$results = [System.Collections.Generic.List[object]]::new()
$failures = [System.Collections.Generic.List[object]]::new()

# --- Letterheads ---
Write-Host "`n--- Letterheads ---" -ForegroundColor Cyan
$lhRoot = Join-Path $PackDir "letterheads"
foreach ($dir in Get-ChildItem $lhRoot -Directory -ErrorAction SilentlyContinue) {
    $metaPath = Join-Path $dir.FullName "meta.json"
    if (-not (Test-Path $metaPath)) { continue }
    $meta = Get-Content $metaPath -Raw -Encoding UTF8 | ConvertFrom-Json
    $code = [string]$meta.code
    $id = [string]$meta.id
    $file = Find-Binary $dir.FullName @("design.docx", "design.xlsx")
    try {
        $results.Add((Upload-And-Patch -Kind "letterhead" -Code $code -RecordId $id `
            -DatasetName "dm_letterheads" -FileFieldName "designFile" `
            -PathFieldName "designStoragePath" -NameFieldName "designFileName" -File $file))
    } catch {
        Write-Host "    FAIL: $_" -ForegroundColor Red
        $failures.Add([pscustomobject]@{ kind = "letterhead"; code = $code; error = "$_" })
    }
}

# --- Cover pages ---
Write-Host "`n--- Cover pages ---" -ForegroundColor Cyan
$cpRoot = Join-Path $PackDir "cover-pages"
foreach ($dir in Get-ChildItem $cpRoot -Directory -ErrorAction SilentlyContinue) {
    $metaPath = Join-Path $dir.FullName "meta.json"
    if (-not (Test-Path $metaPath)) { continue }
    $meta = Get-Content $metaPath -Raw -Encoding UTF8 | ConvertFrom-Json
    $code = [string]$meta.code
    $id = [string]$meta.id
    $file = Find-Binary $dir.FullName @("design.docx", "design.xlsx")
    try {
        $results.Add((Upload-And-Patch -Kind "cover" -Code $code -RecordId $id `
            -DatasetName "dm_cover_pages" -FileFieldName "designFile" `
            -PathFieldName "designStoragePath" -NameFieldName "designFileName" -File $file))
    } catch {
        Write-Host "    FAIL: $_" -ForegroundColor Red
        $failures.Add([pscustomobject]@{ kind = "cover"; code = $code; error = "$_" })
    }
}

# --- Templates ---
Write-Host "`n--- Templates ---" -ForegroundColor Cyan
$tplRoot = Join-Path $PackDir "templates"
foreach ($dir in Get-ChildItem $tplRoot -Directory -ErrorAction SilentlyContinue) {
    $metaPath = Join-Path $dir.FullName "meta.json"
    if (-not (Test-Path $metaPath)) { continue }
    $meta = Get-Content $metaPath -Raw -Encoding UTF8 | ConvertFrom-Json
    $code = [string]$meta.code
    $id = [string]$meta.id
    $preferred = @()
    if ($meta.sourceFileName) {
        $ext = [IO.Path]::GetExtension([string]$meta.sourceFileName)
        if ($ext) { $preferred += "source$ext" }
    }
    $preferred += @("source.docx", "source.xlsx", "source.pptx")
    $file = Find-Binary $dir.FullName $preferred
    $extra = @{}
    if ($meta.sourceFileName) { $extra["sourceFileName"] = [string]$meta.sourceFileName }
    try {
        $results.Add((Upload-And-Patch -Kind "template" -Code $code -RecordId $id `
            -DatasetName "dm_document_templates" -FileFieldName "referenceFile" `
            -PathFieldName "sourceStoragePath" -NameFieldName "sourceFileName" -File $file -ExtraPatch $extra))
    } catch {
        Write-Host "    FAIL: $_" -ForegroundColor Red
        $failures.Add([pscustomobject]@{ kind = "template"; code = $code; error = "$_" })
    }
}

Write-Host "`n=== Summary ===" -ForegroundColor Cyan
$ok = @($results | Where-Object { $_.status -eq "ok" }).Count
$wi = @($results | Where-Object { $_.status -eq "whatIf" }).Count
Write-Host "ok=$ok whatIf=$wi fail=$($failures.Count)"
if ($failures.Count -gt 0) {
    $failures | Format-Table -AutoSize | Out-String | Write-Host
    exit 1
}

# Smoke: download first template path
if (-not $WhatIf -and $ok -gt 0) {
    $sample = $results | Where-Object { $_.kind -eq "template" -and $_.path } | Select-Object -First 1
    if ($sample) {
        $dl = Invoke-WebRequest -Uri ("$DataGatewayBaseUrl/api/v1/files/download?filePath=" + [uri]::EscapeDataString($sample.path)) `
            -Headers @{ Authorization = "Bearer $Token" } -Method GET
        Write-Host ("Smoke download {0}: {1} bytes" -f $sample.code, $dl.RawContentLength) -ForegroundColor Green
        if ($dl.RawContentLength -le 0) { throw "Smoke download empty" }
    }
}

Write-Host "Done." -ForegroundColor Cyan
