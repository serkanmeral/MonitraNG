# Document Intelligence — MonitraNG / Pazarlama / Odak seed
#
# Klasör ağacı:
#   Sayfalar/
#     MonitraNG/
#       Pazarlama/
#         Odak/
#           Dijital Dönüşüm Planlaması.md
#
# Repo kaynak: docs/monitrang/pazarlama/odak/
#
# Usage (repo kökünden):
#   .\docs\odak\document_intelligence\scripts\seed-monitrang-pazarlama-odak.ps1
#   .\docs\odak\document_intelligence\scripts\seed-monitrang-pazarlama-odak.ps1 -BaseUrl "http://192.168.20.20:5040"
#   .\docs\odak\document_intelligence\scripts\seed-monitrang-pazarlama-odak.ps1 -WhatIf
#
# DOCX export:
#   .\docs\monitrang\pazarlama\scripts\export-odak-dijital-donusum-docx.ps1

param(
    [string]$BaseUrl = "http://localhost:5040",
    [switch]$WhatIf = $false
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
$repoRoot = (Resolve-Path (Join-Path $scriptDir "../../../..")).Path
$odakDir = Join-Path $repoRoot "docs/monitrang/pazarlama/odak"
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

function Read-OdakMd {
    param([string]$FileName)
    $path = Join-Path $odakDir $FileName
    if (-not (Test-Path $path)) { throw "Markdown bulunamadi: $path" }
    return [System.IO.File]::ReadAllText($path, $utf8)
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
        [string]$FileName,
        [switch]$Publish
    )
    $content = Read-OdakMd $FileName
    $children = @()
    if (-not $WhatIf) {
        $children = Get-Items (Invoke-DocApi -Method GET -Path "/children?parentId=$ParentId")
    }
    $existing = $children | Where-Object {
        $_.type -eq "markdown" -and ($_.title -eq $Title -or $_.name -eq $Title)
    } | Select-Object -First 1

    if ($WhatIf) {
        Write-Host "  WhatIf markdown '$Title' ($($content.Length) karakter) <- $FileName" -ForegroundColor Yellow
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
Write-Host "MonitraNG Pazarlama / Odak Seed" -ForegroundColor Cyan
Write-Host "Gateway: $BaseUrl" -ForegroundColor Cyan
Write-Host "Kaynak: docs/monitrang/pazarlama/odak/" -ForegroundColor Gray
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
$odakId = Ensure-Folder -Name "Odak" -ParentId $pazarlamaId

Write-Host "`nMarkdown dokumanlari..." -ForegroundColor Cyan

Ensure-Markdown -ParentId $odakId `
    -Title "Dijital Dönüşüm Planlaması" `
    -FileName "dijital-donusum-planlamasi.md" `
    -Publish

Write-Host "`nTamamlandi." -ForegroundColor Cyan
Write-Host "UI: Dokumanlar > Sayfalar > MonitraNG > Pazarlama > Odak" -ForegroundColor Cyan
Write-Host "DOCX export: docs/monitrang/pazarlama/scripts/export-odak-dijital-donusum-docx.ps1" -ForegroundColor Gray
