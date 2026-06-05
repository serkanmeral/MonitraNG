# Document Intelligence — kök System klasörü + Sürüm Notları (manager görünürlüğü)
#
# Klasör ağacı:
#   System/                         (kök; yalnızca ManagerGroupName)
#     Sürüm Notları.md
#
# İçerik: docs/odak/document_intelligence/system/surum-notlari.md
#
# Usage (repo kökünden):
#   .\docs\odak\document_intelligence\scripts\seed-system-release-notes.ps1
#   .\docs\odak\document_intelligence\scripts\seed-system-release-notes.ps1 -BaseUrl "http://192.168.20.20:5040" -Server "192.168.20.20"
#   .\docs\odak\document_intelligence\scripts\seed-system-release-notes.ps1 -WhatIf

param(
    [string]$BaseUrl = "http://192.168.20.8:5040",
    [string]$Server = "192.168.20.8",
    [string]$ManagerGroupName = "MonitraNG Users",
    [switch]$WhatIf = $false
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
$systemDir = Join-Path $scriptDir "..\system"
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
    Write-Host "Token alinamadi. `$env:DI_TOKEN set edin veya OC token script calistirin." -ForegroundColor Red
    exit 1
}

$managerGroupId = $null
$resolveScript = Join-Path $scriptDir "..\..\operationcore\scripts\resolve-odak-group-ids-prod.ps1"
if (Test-Path $resolveScript) {
    try {
        $groupMap = & $resolveScript -Names @($ManagerGroupName) -Server $Server
        $managerGroupId = $groupMap.$ManagerGroupName
        if ($managerGroupId) {
            Write-Host "Grup: $ManagerGroupName -> $managerGroupId" -ForegroundColor Green
        }
    }
    catch {
        Write-Host "Grup id cozulemedi (yalnizca groupName kullanilacak): $($_.Exception.Message)" -ForegroundColor Yellow
    }
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
    $path = Join-Path $systemDir $fileName
    if (-not (Test-Path $path)) { throw "Markdown bulunamadi: $path" }
    return [System.IO.File]::ReadAllText($path, $utf8)
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

function Set-RestrictedFolderPermissions {
    param(
        [string]$FolderId,
        [string]$GroupName,
        [string]$GroupId
    )
    if ($WhatIf) {
        Write-Host "WhatIf: System klasoru izinleri -> $GroupName (view, download)" -ForegroundColor Yellow
        return
    }

    Write-Host "System klasoru izinleri ($GroupName)..." -ForegroundColor Cyan
    $perms = Invoke-DocApi -Method GET -Path "/$FolderId/permissions"

    if (-not $perms.inheritanceBroken) {
        Write-Host "  POST break-inheritance..." -ForegroundColor Gray
        $perms = Invoke-DocApi -Method POST -Path "/$FolderId/permissions/break-inheritance"
    }

    $groupInput = @{
        groupName   = $GroupName
        permissions = @("view", "download")
    }
    if ($GroupId) { $groupInput.groupId = $GroupId }

    Invoke-DocApi -Method PUT -Path "/$FolderId/permissions" -Body @{
        groups = @($groupInput)
    } | Out-Null

    Write-Host "  OK: yalnizca '$GroupName' goruntuleyebilir (view, download)" -ForegroundColor Green
}

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "System / Surum Notlari Seed" -ForegroundColor Cyan
Write-Host "Gateway: $BaseUrl" -ForegroundColor Cyan
Write-Host "System klasoru: $ManagerGroupName" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

$systemId = Ensure-Folder -Name "System"
Set-RestrictedFolderPermissions -FolderId $systemId -GroupName $ManagerGroupName -GroupId $managerGroupId

Write-Host "`nMarkdown dokumanlari..." -ForegroundColor Cyan
Ensure-Markdown -ParentId $systemId `
    -Title "Sürüm Notları" `
    -FileName "surum-notlari.md" `
    -Publish

Write-Host "`nTamamlandi." -ForegroundColor Cyan
Write-Host "UI: Dokumanlar > System > Surum Notlari (grup: $ManagerGroupName)" -ForegroundColor Cyan
