# Document Intelligence — MonitraNG / Pazarlama klasor yapisi
#
# Klasor agaci:
#   Sayfalar/
#     MonitraNG/
#       Pazarlama/
#         Docs/
#         Files/
#
# Repo kaynak icerik: docs/monitrang/pazarlama/Docs | Files
#
# Usage (repo kokunden):
#   .\docs\odak\document_intelligence\scripts\seed-monitrang-pazarlama-folders.ps1
#   .\docs\odak\document_intelligence\scripts\seed-monitrang-pazarlama-folders.ps1 -BaseUrl "http://192.168.20.20:5040"
#   .\docs\odak\document_intelligence\scripts\seed-monitrang-pazarlama-folders.ps1 -WhatIf

param(
    [string]$BaseUrl = "http://192.168.20.8:5040",
    [switch]$WhatIf = $false
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
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

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "MonitraNG Pazarlama Klasor Seed" -ForegroundColor Cyan
Write-Host "Gateway: $BaseUrl" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

$sayfalarId = Get-SayfalarFolderId
if ($sayfalarId) {
    Write-Host "Sayfalar klasoru bulundu (id=$sayfalarId)" -ForegroundColor Green
    $monitraNgId = Ensure-Folder -Name "MonitraNG" -ParentId $sayfalarId
}
else {
    Write-Host "Sayfalar klasoru yok — legacy kok (once seed-resource-root-folders.ps1 onerilir)" -ForegroundColor Yellow
    $monitraNgId = Ensure-Folder -Name "MonitraNG"
}

$pazarlamaId = Ensure-Folder -Name "Pazarlama" -ParentId $monitraNgId
Ensure-Folder -Name "Docs" -ParentId $pazarlamaId | Out-Null
Ensure-Folder -Name "Files" -ParentId $pazarlamaId | Out-Null
Ensure-Folder -Name "Broşür" -ParentId $pazarlamaId | Out-Null

Write-Host "`nTamamlandi." -ForegroundColor Cyan
if ($sayfalarId) {
    Write-Host "UI: Dokumanlar > Sayfalar > MonitraNG > Pazarlama > Docs | Files | Broşür" -ForegroundColor Cyan
}
else {
    Write-Host "UI: Dokumanlar > MonitraNG > Pazarlama > Docs | Files | Broşür" -ForegroundColor Cyan
}
Write-Host "Repo kaynak: docs/monitrang/pazarlama/Docs | Files | brosur/" -ForegroundColor Gray
Write-Host "Broşür seed: docs/odak/document_intelligence/scripts/seed-monitrang-pazarlama-brosur.ps1" -ForegroundColor Gray
