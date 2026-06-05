# Document Intelligence — System / Diagnostic Raporu (manager görünürlüğü)
#
# Ön koşul: System klasörü (seed-system-release-notes.ps1 veya ilk seed ile oluşturulmuş olmalı)
# İçerik: docs/odak/document_intelligence/system/diagnostic-raporu.md
#
# Usage (repo kökünden):
#   .\docs\odak\document_intelligence\scripts\seed-system-diagnostic-report.ps1
#   .\docs\odak\document_intelligence\scripts\seed-system-diagnostic-report.ps1 -WhatIf

param(
    [string]$BaseUrl = "http://192.168.20.8:5040",
    [string]$Server = "192.168.20.8",
    [switch]$WhatIf = $false
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
$systemContentDir = Join-Path $scriptDir "..\system"
$isProd = $BaseUrl -match "192\.168\.20\.8"

$token = $env:DI_TOKEN
if ([string]::IsNullOrEmpty($token)) {
    if ($isProd) {
        $loadTokenScript = Join-Path $scriptDir "..\..\operationcore\scripts\load-operationcore-token-prod.ps1"
    }
    else {
        $loadTokenScript = Join-Path $scriptDir "..\..\operationcore\scripts\load-operationcore-token.ps1"
    }
    if (Test-Path $loadTokenScript) {
        $token = & $loadTokenScript
    }
}
if ([string]::IsNullOrEmpty($token)) {
    Write-Host "Token alinamadi." -ForegroundColor Red
    exit 1
}

$headers = @{ Authorization = "Bearer $token" }
$apiBase = "$BaseUrl/documents/api/v1/resources"
$utf8 = [System.Text.Encoding]::UTF8

function Invoke-DocApi {
    param([string]$Method, [string]$Path, [hashtable]$Body)
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
    $path = Join-Path $systemContentDir $fileName
    if (-not (Test-Path $path)) { throw "Markdown bulunamadi: $path" }
    return [System.IO.File]::ReadAllText($path, $utf8)
}

function Find-SystemFolderId {
    $roots = Get-Items (Invoke-DocApi -Method GET -Path "/children")
    $folder = $roots | Where-Object { $_.type -eq "folder" -and $_.name -eq "System" } | Select-Object -First 1
    if (-not $folder) { throw "System klasoru bulunamadi. Once seed-system-release-notes.ps1 calistirin." }
    return $folder.id
}

function Ensure-Markdown {
    param([string]$ParentId, [string]$Title, [string]$FileName)
    $content = Read-Md $FileName
    $children = @()
    if (-not $WhatIf) {
        $children = Get-Items (Invoke-DocApi -Method GET -Path "/children?parentId=$ParentId")
    }
    $existing = $children | Where-Object {
        $_.type -eq "markdown" -and ($_.title -eq $Title -or $_.name -eq $Title)
    } | Select-Object -First 1

    if ($WhatIf) {
        Write-Host "WhatIf markdown '$Title' ($($content.Length) karakter)" -ForegroundColor Yellow
        return
    }

    if ($existing) {
        $ver = if ($null -ne $existing.currentVersionNumber) { [int]$existing.currentVersionNumber } else { 1 }
        Write-Host "PUT /markdown/$($existing.id) '$Title' (v$ver)..." -ForegroundColor Yellow
        Invoke-DocApi -Method PUT -Path "/markdown/$($existing.id)" -Body @{
            title                 = $Title
            content               = $content
            expectedVersionNumber = $ver
            isDraft               = $false
        } | Out-Null
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
Write-Host "System / Diagnostic Raporu Seed" -ForegroundColor Cyan
Write-Host "Gateway: $BaseUrl" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

$systemId = Find-SystemFolderId
Ensure-Markdown -ParentId $systemId -Title "Diagnostic Raporu" -FileName "diagnostic-raporu.md"

Write-Host "`nTamamlandi." -ForegroundColor Cyan
Write-Host "UI: Dokumanlar > System > Diagnostic Raporu" -ForegroundColor Cyan
