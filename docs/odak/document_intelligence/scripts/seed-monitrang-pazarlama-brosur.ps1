# Document Intelligence — MonitraNG / Pazarlama / Broşür seed
#
# Klasör ağacı:
#   Sayfalar/
#     MonitraNG/
#       Pazarlama/
#         Docs/          (seed-monitrang-pazarlama-folders.ps1)
#         Files/
#         Broşür/
#           MonitraNG Platform Broşürü.md
#           Modüller/
#             Platform Omurgası.md
#             …
#
# Repo kaynak: docs/monitrang/pazarlama/brosur/
#
# Önkoşul (isteğe bağlı — PDF/antet için):
#   .\docs\monitrang\pazarlama\scripts\seed-letterheads-monitrang.ps1
#
# Usage (repo kökünden):
#   .\docs\odak\document_intelligence\scripts\seed-monitrang-pazarlama-brosur.ps1
#   .\docs\odak\document_intelligence\scripts\seed-monitrang-pazarlama-brosur.ps1 -BaseUrl "http://192.168.20.20:5040"
#   .\docs\odak\document_intelligence\scripts\seed-monitrang-pazarlama-brosur.ps1 -WhatIf
#
# Tam ağaç (klasörler + broşür):
#   .\docs\odak\document_intelligence\scripts\seed-monitrang-pazarlama-folders.ps1
#   .\docs\odak\document_intelligence\scripts\seed-monitrang-pazarlama-brosur.ps1

param(
    [string]$BaseUrl = "http://localhost:5040",
    [string]$Server = "192.168.20.20",
    [switch]$WhatIf = $false
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
$repoRoot = (Resolve-Path (Join-Path $scriptDir "../../../..")).Path
$brosurDir = Join-Path $repoRoot "docs/monitrang/pazarlama/brosur"
$pazarlamaFilesDir = Join-Path $repoRoot "docs/monitrang/pazarlama/Files"
$isProd = $BaseUrl -match "192\.168\.20\.8"

$token = $env:DI_TOKEN
if ([string]::IsNullOrEmpty($token)) {
    $diAuth = Join-Path $repoRoot "scripts/tests/MngDocument/auth/DiAuthCommon.ps1"
    if (Test-Path $diAuth) {
        . $diAuth
        try {
            $token = Get-DiPersonaToken -Persona Admin -Gateway $BaseUrl
        }
        catch {
            Write-Host "DiAuthCommon token alinamadi: $($_.Exception.Message)" -ForegroundColor Yellow
        }
    }
}
if ([string]::IsNullOrEmpty($token)) {
    $loadTokenScript = if ($isProd) {
        Join-Path $scriptDir "..\..\operationcore\scripts\load-operationcore-token-prod.ps1"
    } else {
        Join-Path $scriptDir "..\..\operationcore\scripts\load-operationcore-token.ps1"
    }
    if (Test-Path $loadTokenScript) { $token = & $loadTokenScript }
}
if ([string]::IsNullOrEmpty($token)) {
    Write-Host "Token alinamadi. `$env:DI_TOKEN, DiAuthCommon veya OC token script kullanin." -ForegroundColor Red
    exit 1
}
$token = $token.Trim()

$headers = @{ Authorization = "Bearer $token" }
$apiBase = "$BaseUrl/documents/api/v1/resources"
$utf8 = [System.Text.Encoding]::UTF8

function Invoke-DocApi {
    param(
        [string]$Method,
        [string]$Path,
        [hashtable]$Body
    )
    $uri = "$apiBase$Path"
    if ($Body) {
        $json = $Body | ConvertTo-Json -Depth 12 -Compress
        $bytes = $utf8.GetBytes($json)
        return Invoke-RestMethod -Uri $uri -Headers $headers -Method $Method -Body $bytes -ContentType "application/json; charset=utf-8"
    }
    return Invoke-RestMethod -Uri $uri -Headers $headers -Method $Method
}

function Get-Items($response) {
    if ($null -eq $response) { return @() }
    if ($null -ne $response.items) { return , @($response.items) }
    if ($response -is [System.Array]) { return , $response }
    return , @($response)
}

function Read-Md {
    param([string]$RelativePath)
    $path = Join-Path $brosurDir $RelativePath
    if (-not (Test-Path $path)) { throw "Markdown bulunamadi: $path" }
    return [System.IO.File]::ReadAllText($path, $utf8)
}

function Get-DiFilePathMarkdownHref {
    param([string]$FilePath)
    return "di-fp:$([uri]::EscapeDataString($FilePath.Trim()))"
}

function Get-MimeTypeForExtension {
    param([string]$Extension)
    switch ($Extension.ToLowerInvariant()) {
        ".svg" { return "image/svg+xml" }
        ".png" { return "image/png" }
        ".jpg" { return "image/jpeg" }
        ".jpeg" { return "image/jpeg" }
        ".gif" { return "image/gif" }
        ".webp" { return "image/webp" }
        default { return "application/octet-stream" }
    }
}

function Ensure-FileAsset {
    param(
        [string]$ParentId,
        [string]$FileName,
        [string]$SourcePath
    )
    if (-not (Test-Path $SourcePath)) {
        throw "Dosya bulunamadi: $SourcePath"
    }

    $bytes = [System.IO.File]::ReadAllBytes($SourcePath)
    $ext = [System.IO.Path]::GetExtension($FileName)
    $mimeType = Get-MimeTypeForExtension $ext

    $children = @()
    if (-not $WhatIf) {
        $children = Get-Items (Invoke-DocApi -Method GET -Path "/children?parentId=$ParentId")
    }
    $existing = $children | Where-Object {
        $_.type -eq "file" -and ($_.name -eq $FileName -or $_.fileName -eq $FileName)
    } | Select-Object -First 1

    if ($WhatIf) {
        Write-Host "  WhatIf file '$FileName' ($($bytes.Length) byte) <- $SourcePath" -ForegroundColor Yellow
        return "<whatif-filePath/$FileName>"
    }

    if ($existing) {
        Write-Host "DELETE /resources/$($existing.id) '$FileName' (yeniden yukleme)..." -ForegroundColor Yellow
        Invoke-DocApi -Method DELETE -Path "/$($existing.id)" | Out-Null
    }

    Write-Host "POST /file '$FileName'..." -ForegroundColor Yellow
    $created = Invoke-DocApi -Method POST -Path "/file" -Body @{
        parentId         = $ParentId
        name             = $FileName
        originalFileName = $FileName
        mimeType         = $mimeType
        extension        = $ext
        size             = $bytes.Length
        content          = [Convert]::ToBase64String($bytes)
    }
    $filePath = $created.filePath
    if ([string]::IsNullOrWhiteSpace($filePath)) { $filePath = $created.FilePath }
    if ([string]::IsNullOrWhiteSpace($filePath)) {
        throw "Dosya yuklendi ancak filePath donmedi: $FileName"
    }
    Write-Host "  OK filePath=$filePath" -ForegroundColor Green
    return $filePath
}

function Resolve-DiAssetUpload {
    param([string]$FileName)
    $sourcePath = Join-Path $pazarlamaFilesDir $FileName
    if ($FileName.EndsWith(".svg", [StringComparison]::OrdinalIgnoreCase)) {
        $pngPath = [System.IO.Path]::ChangeExtension($sourcePath, ".png")
        if (Test-Path $pngPath) {
            return @{
                SourcePath = $pngPath
                UploadName = [System.IO.Path]::ChangeExtension($FileName, ".png")
            }
        }
    }
    return @{
        SourcePath = $sourcePath
        UploadName = $FileName
    }
}

function Resolve-DiMarkdownContent {
    param(
        [string]$RelativePath,
        [string]$FilesFolderId
    )
    $content = Read-Md $RelativePath
    $matches = [regex]::Matches($content, '\]\(\.\./Files/([^)]+)\)')
    if ($matches.Count -eq 0) { return $content }

    foreach ($match in $matches) {
        $fileName = $match.Groups[1].Value.Trim()
        $asset = Resolve-DiAssetUpload $fileName
        $repoRef = "../Files/$fileName"
        $filePath = Ensure-FileAsset -ParentId $FilesFolderId -FileName $asset.UploadName -SourcePath $asset.SourcePath
        $diHref = Get-DiFilePathMarkdownHref $filePath
        $content = $content.Replace("($repoRef)", "($diHref)")
    }
    return $content
}

function Get-SayfalarFolderId {
    $roots = Get-Items (Invoke-DocApi -Method GET -Path "/children")
    $folder = $roots | Where-Object { $_.type -eq "folder" -and $_.name -eq "Sayfalar" } | Select-Object -First 1
    if ($folder) { return $folder.id }
    return $null
}

function Ensure-Folder {
    param(
        [string]$Name,
        [string]$ParentId = $null
    )
    $parentLabel = if ($ParentId) { "parent=$ParentId" } else { "kok" }
    Write-Host "Klasor araniyor: '$Name' ($parentLabel)..." -ForegroundColor Cyan

    if ($ParentId) {
        $siblings = Get-Items (Invoke-DocApi -Method GET -Path "/children?parentId=$ParentId")
    }
    else {
        $siblings = Get-Items (Invoke-DocApi -Method GET -Path "/children")
    }

    $existing = $siblings | Where-Object { $_.type -eq "folder" -and $_.name -eq $Name } | Select-Object -First 1
    if ($existing) {
        Write-Host "  SKIP: '$Name' (id=$($existing.id))" -ForegroundColor Green
        return $existing.id
    }

    if ($WhatIf) {
        Write-Host "  WhatIf POST /folder '$Name'" -ForegroundColor Yellow
        return "<whatif-$Name>"
    }

    $body = @{ name = $Name }
    if ($ParentId) { $body.parentId = $ParentId }
    $created = Invoke-DocApi -Method POST -Path "/folder" -Body $body
    Write-Host "  OK olusturuldu (id=$($created.id))" -ForegroundColor Green
    return $created.id
}

function Ensure-Markdown {
    param(
        [string]$ParentId,
        [string]$Title,
        [string]$RelativePath,
        [string]$FilesFolderId = $null,
        [switch]$Publish
    )
    $content = if ($FilesFolderId) {
        Resolve-DiMarkdownContent -RelativePath $RelativePath -FilesFolderId $FilesFolderId
    }
    else {
        Read-Md $RelativePath
    }
    $children = @()
    if (-not $WhatIf) {
        $children = Get-Items (Invoke-DocApi -Method GET -Path "/children?parentId=$ParentId")
    }
    $existing = $children | Where-Object {
        $_.type -eq "markdown" -and ($_.title -eq $Title -or $_.name -eq $Title)
    } | Select-Object -First 1

    if ($WhatIf) {
        Write-Host "  WhatIf markdown '$Title' ($($content.Length) karakter) <- $RelativePath" -ForegroundColor Yellow
        return
    }

    if ($existing) {
        $ver = if ($null -ne $existing.currentVersionNumber) { [int]$existing.currentVersionNumber } else { 1 }
        Write-Host "PUT /markdown/$($existing.id) '$Title' (v$ver)..." -ForegroundColor Yellow
        $putBody = @{
            title                 = $Title
            content               = $content
            expectedVersionNumber = $ver
        }
        if ($Publish) { $putBody.isDraft = $false }
        Invoke-DocApi -Method PUT -Path "/markdown/$($existing.id)" -Body $putBody | Out-Null
        Write-Host "  OK guncellendi" -ForegroundColor Green
    }
    else {
        Write-Host "POST /markdown '$Title'..." -ForegroundColor Yellow
        Invoke-DocApi -Method POST -Path "/markdown" -Body @{
            parentId = $ParentId
            title    = $Title
            content  = $content
            isDraft  = $false
        } | Out-Null
        Write-Host "  OK olusturuldu" -ForegroundColor Green
    }
}

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "MonitraNG Pazarlama / Broşür Seed" -ForegroundColor Cyan
Write-Host "Gateway: $BaseUrl" -ForegroundColor Cyan
Write-Host "Kaynak: docs/monitrang/pazarlama/brosur/" -ForegroundColor Gray
Write-Host "========================================`n" -ForegroundColor Cyan

$sayfalarId = Get-SayfalarFolderId
if ($sayfalarId) {
    Write-Host "Sayfalar klasoru bulundu (id=$sayfalarId)" -ForegroundColor Green
    $monitraNgId = Ensure-Folder -Name "MonitraNG" -ParentId $sayfalarId
}
else {
    Write-Host "Sayfalar klasoru yok — legacy kok" -ForegroundColor Yellow
    $monitraNgId = Ensure-Folder -Name "MonitraNG"
}

$pazarlamaId = Ensure-Folder -Name "Pazarlama" -ParentId $monitraNgId
Ensure-Folder -Name "Docs" -ParentId $pazarlamaId | Out-Null
$filesFolderId = Ensure-Folder -Name "Files" -ParentId $pazarlamaId
$brosurId = Ensure-Folder -Name "Broşür" -ParentId $pazarlamaId
$modullerId = Ensure-Folder -Name "Modüller" -ParentId $brosurId

Write-Host "`nMarkdown dokumanlari..." -ForegroundColor Cyan

Ensure-Markdown -ParentId $brosurId `
    -Title "MonitraNG Platform Broşürü" `
    -RelativePath "monitrang-platform-brosuru.md" `
    -FilesFolderId $filesFolderId `
    -Publish

$modulePages = @(
    @{ Title = "Platform Omurgası";      File = "moduller/00-platform-omurgasi.md" },
    @{ Title = "Döküman Zekası";         File = "moduller/01-dokuman-zekasi.md" },
    @{ Title = "Operasyon Merkezi (OC)"; File = "moduller/02-operasyon-merkezi.md" },
    @{ Title = "Raporlama";              File = "moduller/03-raporlama.md" },
    @{ Title = "Monitoring";             File = "moduller/04-monitoring.md" },
    @{ Title = "Güvenlik Merkezi (SIEM)"; File = "moduller/05-guvenlik-merkezi.md" },
    @{ Title = "Workflow";               File = "moduller/06-workflow.md" },
    @{ Title = "Veri Yüzeyleri";         File = "moduller/07-veri-yuzeyleri.md" }
)

foreach ($page in $modulePages) {
    Ensure-Markdown -ParentId $modullerId `
        -Title $page.Title `
        -RelativePath $page.File `
        -Publish
}

Write-Host "`nTamamlandi." -ForegroundColor Cyan
Write-Host "UI: Dokumanlar > Sayfalar > MonitraNG > Pazarlama > Broşür" -ForegroundColor Cyan
Write-Host "Antet (PDF): Belge Tasarimcisi > Antetler > MNG-STD" -ForegroundColor Gray
Write-Host "  -> docs/monitrang/pazarlama/scripts/seed-letterheads-monitrang.ps1" -ForegroundColor Gray
Write-Host "PDF export: docs/monitrang/pazarlama/scripts/export-monitrang-brosur-pdf.ps1" -ForegroundColor Gray
