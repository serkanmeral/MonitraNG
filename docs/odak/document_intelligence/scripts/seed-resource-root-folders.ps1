# Document Intelligence — ust klasor convention (P-B)
#
# Olusturur:
#   Sayfalar/          + giris sayfasi
#   Dokumanlar/        + giris sayfasi
#     Kalite/
#     Uretilen/
#
# Opsiyonel: kokteki legacy klasorleri Sayfalar/ altina tasir (MonitraNG, System).
#
# Usage (repo kokunden):
#   .\docs\odak\document_intelligence\scripts\seed-resource-root-folders.ps1
#   .\docs\odak\document_intelligence\scripts\seed-resource-root-folders.ps1 -BaseUrl "http://192.168.20.20:5040"
#   .\docs\odak\document_intelligence\scripts\seed-resource-root-folders.ps1 -WhatIf
#   .\docs\odak\document_intelligence\scripts\seed-resource-root-folders.ps1 -SkipMigrate

param(
    [string]$BaseUrl = "http://192.168.20.8:5040",
    [switch]$WhatIf = $false,
    [switch]$SkipMigrate = $false,
    [string[]]$MigrateFolderNames = @("MonitraNG", "System")
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
$contentDir = Join-Path $scriptDir "..\system"
$isProd = $BaseUrl -match "192\.168\.20\.8"

$token = $env:DI_TOKEN
if ([string]::IsNullOrEmpty($token)) {
    $loadTokenScript = if ($isProd) {
        Join-Path $scriptDir "..\..\operationcore\scripts\load-operationcore-token-prod.ps1"
    } else {
        Join-Path $scriptDir "..\..\operationcore\scripts\load-operationcore-token.ps1"
    }
    if (Test-Path $loadTokenScript) { $token = & $loadTokenScript }
}
if ([string]::IsNullOrEmpty($token)) {
    Write-Host "Token alinamadi. `$env:DI_TOKEN set edin veya OC token script calistirin." -ForegroundColor Red
    exit 1
}

$headers = @{ Authorization = "Bearer $token" }
$apiBase = "$BaseUrl/documents/api/v1/resources"
$utf8 = [System.Text.Encoding]::UTF8

$RootAreaFolderSayfalar = "Sayfalar"
$RootAreaFolderDokumanlar = "Dökümanlar"
$ReservedRootNames = @($RootAreaFolderSayfalar, $RootAreaFolderDokumanlar)

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

function Read-Md($fileName) {
    $path = Join-Path $contentDir $fileName
    if (-not (Test-Path $path)) { throw "Markdown bulunamadi: $path" }
    return [System.IO.File]::ReadAllText($path, $utf8)
}

function Get-RootChildren {
    return Get-Items (Invoke-DocApi -Method GET -Path "/children")
}

function Find-FolderUnderParent {
    param(
        [string]$Name,
        [string]$ParentId = $null
    )
    if ($ParentId) {
        $siblings = Get-Items (Invoke-DocApi -Method GET -Path "/children?parentId=$ParentId")
    }
    else {
        $siblings = Get-RootChildren
    }
    return $siblings | Where-Object { $_.type -eq "folder" -and $_.name -eq $Name } | Select-Object -First 1
}

function Ensure-Folder {
    param(
        [string]$Name,
        [string]$ParentId = $null
    )
    $parentLabel = if ($ParentId) { "parent=$ParentId" } else { "kok" }
    Write-Host "Klasor araniyor: '$Name' ($parentLabel)..." -ForegroundColor Cyan

    $existing = Find-FolderUnderParent -Name $Name -ParentId $ParentId
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
        [string]$FileName,
        [switch]$Publish
    )
    $content = Read-Md $FileName
    $children = @()
    if (-not $WhatIf) {
        $children = Get-Items (Invoke-DocApi -Method GET -Path "/children?parentId=$ParentId")
    }
    $existing = $children | Where-Object {
        $_.type -eq "markdown" -and ($_.title -eq $Title -or $_.name -eq $Title)
    } | Select-Object -First 1

    if ($WhatIf) {
        Write-Host "  WhatIf markdown '$Title' ($($content.Length) karakter)" -ForegroundColor Yellow
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

function Move-FolderToParent {
    param(
        [string]$FolderId,
        [string]$FolderName,
        [string]$NewParentId
    )
    if ($WhatIf) {
        Write-Host "  WhatIf MOVE '$FolderName' -> parent=$NewParentId" -ForegroundColor Yellow
        return
    }

    Write-Host "  MOVE '$FolderName' (id=$FolderId) -> Sayfalar/ ..." -ForegroundColor Yellow
    Invoke-DocApi -Method PUT -Path "/$FolderId/move" -Body @{ newParentId = $NewParentId } | Out-Null
    Write-Host "  OK tasindi" -ForegroundColor Green
}

function Migrate-LegacyRootFolders {
    param([string]$SayfalarId)

    Write-Host "`nLegacy kok klasor migrasyonu..." -ForegroundColor Cyan
    $roots = Get-RootChildren | Where-Object { $_.type -eq "folder" }

    foreach ($name in $MigrateFolderNames) {
        $legacy = $roots | Where-Object { $_.name -eq $name } | Select-Object -First 1
        if (-not $legacy) {
            Write-Host "  Atlandi (kokte yok): $name" -ForegroundColor Gray
            continue
        }

        $alreadyUnder = Find-FolderUnderParent -Name $name -ParentId $SayfalarId
        if ($alreadyUnder) {
            Write-Host "  UYARI: Sayfalar/$name zaten var; kokteki kopya tasima disi (id=$($legacy.id))." -ForegroundColor Yellow
            Write-Host "         Manuel birlestirme gerekebilir." -ForegroundColor Yellow
            continue
        }

        Move-FolderToParent -FolderId $legacy.id -FolderName $name -NewParentId $SayfalarId
    }
}

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "DI Ust Klasor Seed (P-B)" -ForegroundColor Cyan
Write-Host "Gateway: $BaseUrl" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

# 1) Ust alan klasorleri
$sayfalarId = Ensure-Folder -Name $RootAreaFolderSayfalar
$dokumanlarId = Ensure-Folder -Name $RootAreaFolderDokumanlar

# 2) Giris sayfalari
Write-Host "`nGiris sayfalari..." -ForegroundColor Cyan
Ensure-Markdown -ParentId $sayfalarId -Title "Giriş" -FileName "sayfalar-giris.md" -Publish
Ensure-Markdown -ParentId $dokumanlarId -Title "Giriş" -FileName "dokumanlar-giris.md" -Publish

# 3) Dokumanlar alt iskelet
Write-Host "`nDokumanlar alt klasorleri..." -ForegroundColor Cyan
Ensure-Folder -Name "Kalite" -ParentId $dokumanlarId | Out-Null
Ensure-Folder -Name "Üretilen" -ParentId $dokumanlarId | Out-Null

# 4) Legacy tasima
if (-not $SkipMigrate) {
    Migrate-LegacyRootFolders -SayfalarId $sayfalarId
}
else {
    Write-Host "`nLegacy migrasyon atlandi (-SkipMigrate)." -ForegroundColor Yellow
}

Write-Host "`nTamamlandi." -ForegroundColor Cyan
Write-Host "UI: Dokumanlar > Sayfalar | Dokumanlar" -ForegroundColor Cyan
Write-Host "Sonraki: seed-monitrang-tutorials.ps1 (MonitraNG artik Sayfalar altinda olmali)" -ForegroundColor Gray
