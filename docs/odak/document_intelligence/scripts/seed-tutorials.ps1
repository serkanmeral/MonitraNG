# Document Intelligence — "Öğreticiler" klasörü + vitrin markdown dökümanları (Odak)
#
# "Öğreticiler" adında bir klasör oluşturur (varsa kullanır) ve içine iki markdown
# döküman ekler/günceller (idempotent):
#   - Operasyon Merkezi Kullanıcı Rehberi
#   - Operasyon Merkezi Yönetici (Admin) Rehberi
#
# İçerik kaynağı: bu script ile aynı repo'daki tutorials/*.md dosyaları.
#
# Token: $env:DI_TOKEN set edilmemişse OC token loader (odak_admin) kullanılır.
# Usage (repo kökünden):
#   .\docs\odak\document_intelligence\scripts\seed-tutorials.ps1
#   .\docs\odak\document_intelligence\scripts\seed-tutorials.ps1 -WhatIf

param(
    [string]$BaseUrl = "http://192.168.20.20:5040",
    [switch]$WhatIf = $false
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
$tutorialsDir = Join-Path $scriptDir "..\tutorials"

# --- Token ---
$token = $env:DI_TOKEN
if ([string]::IsNullOrEmpty($token)) {
    $loadTokenScript = Join-Path $scriptDir "..\..\operationcore\scripts\load-operationcore-token.ps1"
    if (Test-Path $loadTokenScript) {
        $token = & $loadTokenScript
    }
}
if ([string]::IsNullOrEmpty($token)) {
    Write-Host "Token alinamadi. Once get-operationcore-token.ps1 calistirin ya da \$env:DI_TOKEN set edin." -ForegroundColor Red
    exit 1
}

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
        $json = $Body | ConvertTo-Json -Depth 10 -Compress
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

function Read-Md($fileName) {
    $path = Join-Path $tutorialsDir $fileName
    if (-not (Test-Path $path)) { throw "Markdown bulunamadi: $path" }
    return [System.IO.File]::ReadAllText($path, $utf8)
}

# --- 1) "Öğreticiler" klasörü (kök) ---
$folderName = "Öğreticiler"
Write-Host "Kok icerigi listeleniyor..." -ForegroundColor Cyan
$rootChildren = Get-Items (Invoke-DocApi -Method GET -Path "/children")
$folder = $rootChildren | Where-Object { $_.type -eq "folder" -and $_.name -eq $folderName } | Select-Object -First 1

if ($folder) {
    $folderId = $folder.id
    Write-Host "SKIP: '$folderName' klasoru mevcut (id=$folderId)" -ForegroundColor Green
}
elseif ($WhatIf) {
    Write-Host "WhatIf POST /folder -> { name='$folderName' }" -ForegroundColor Yellow
    $folderId = "<whatif-folder-id>"
}
else {
    Write-Host "POST /folder '$folderName'..." -ForegroundColor Yellow
    $created = Invoke-DocApi -Method POST -Path "/folder" -Body @{ name = $folderName; parentId = $null }
    $folderId = $created.id
    Write-Host "  OK klasor olusturuldu (id=$folderId)" -ForegroundColor Green
}

# --- 2) Markdown dokumanlari ---
$docs = @(
    @{ Title = "Operasyon Merkezi Kullanıcı Rehberi"; File = "operasyon-merkezi-kullanici-rehberi.md" },
    @{ Title = "Operasyon Merkezi Yönetici (Admin) Rehberi"; File = "operasyon-merkezi-yonetici-rehberi.md" }
)

# Klasor icerigini (WhatIf disinda) bir kez cek
$folderChildren = @()
if (-not $WhatIf -and $folder) {
    $folderChildren = Get-Items (Invoke-DocApi -Method GET -Path "/children?parentId=$folderId")
}

foreach ($doc in $docs) {
    $content = Read-Md $doc.File
    $existing = $folderChildren | Where-Object { $_.type -eq "markdown" -and ($_.title -eq $doc.Title -or $_.name -eq $doc.Title) } | Select-Object -First 1

    if ($WhatIf) {
        Write-Host "WhatIf markdown '$($doc.Title)' (icerik $($content.Length) karakter)" -ForegroundColor Yellow
        continue
    }

    if ($existing) {
        $ver = if ($null -ne $existing.currentVersionNumber) { [int]$existing.currentVersionNumber } else { 1 }
        Write-Host "PUT /markdown/$($existing.id) '$($doc.Title)' (v$ver)..." -ForegroundColor Yellow
        Invoke-DocApi -Method PUT -Path "/markdown/$($existing.id)" -Body @{
            title                 = $doc.Title
            content               = $content
            expectedVersionNumber = $ver
        } | Out-Null
        Write-Host "  OK guncellendi" -ForegroundColor Green
    }
    else {
        Write-Host "POST /markdown '$($doc.Title)'..." -ForegroundColor Yellow
        Invoke-DocApi -Method POST -Path "/markdown" -Body @{
            parentId = $folderId
            title    = $doc.Title
            content  = $content
        } | Out-Null
        Write-Host "  OK olusturuldu" -ForegroundColor Green
    }
}

Write-Host "`nTamamlandi. UI'da Dokumanlar > Ogreticiler altinda gorebilirsiniz." -ForegroundColor Cyan
