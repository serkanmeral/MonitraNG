# Document Intelligence — Güvenlik Merkezi / IT kurulum rehberleri (wiki seed)
#
# Klasor: MonitraNG / Öğreticiler / IT ve Güvenlik /
#
# Usage:
#   .\docs\odak\document_intelligence\scripts\seed-siem-it-guides.ps1
#   .\docs\odak\document_intelligence\scripts\seed-siem-it-guides.ps1 -BaseUrl "http://192.168.20.20:5040"
#   .\docs\odak\document_intelligence\scripts\seed-siem-it-guides.ps1 -WhatIf

param(
    [string]$BaseUrl = "http://192.168.20.8:5040",
    [switch]$WhatIf = $false
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
$tutorialsDir = Join-Path $scriptDir "..\tutorials"

$token = $env:DI_TOKEN
$isProd = $BaseUrl -match "192\.168\.20\.8"
if ([string]::IsNullOrEmpty($token)) {
    $loadTokenScript = if ($isProd) {
        Join-Path $scriptDir "..\..\operationcore\scripts\load-operationcore-token-prod.ps1"
    } else {
        Join-Path $scriptDir "..\..\operationcore\scripts\load-operationcore-token.ps1"
    }
    if (Test-Path $loadTokenScript) { $token = & $loadTokenScript }
}
if ([string]::IsNullOrEmpty($token)) {
    Write-Host "Token yok. `$env:DI_TOKEN veya OC token script." -ForegroundColor Red
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

function Ensure-Folder {
    param([string]$Name, [string]$ParentId = $null)
    $siblings = if ($ParentId) {
        Get-Items (Invoke-DocApi -Method GET -Path "/children?parentId=$ParentId")
    } else {
        Get-Items (Invoke-DocApi -Method GET -Path "/children")
    }
    $existing = $siblings | Where-Object { $_.type -eq "folder" -and $_.name -eq $Name } | Select-Object -First 1
    if ($existing) {
        Write-Host "  SKIP '$Name' (id=$($existing.id))" -ForegroundColor Green
        return $existing.id
    }
    if ($WhatIf) {
        Write-Host "  WhatIf klasor '$Name'" -ForegroundColor Yellow
        return "<whatif-$Name>"
    }
    $body = @{ name = $Name }
    if ($ParentId) { $body.parentId = $ParentId }
    $created = Invoke-DocApi -Method POST -Path "/folder" -Body $body
    Write-Host "  OK '$Name' (id=$($created.id))" -ForegroundColor Green
    return $created.id
}

function Ensure-Markdown {
    param([string]$ParentId, [string]$Title, [string]$FileName)
    $path = Join-Path $tutorialsDir $FileName
    if (-not (Test-Path $path)) { throw "Markdown yok: $path" }
    $content = [IO.File]::ReadAllText($path, $utf8)
    if ($WhatIf) {
        Write-Host "  WhatIf '$Title'" -ForegroundColor Yellow
        return
    }
    $children = Get-Items (Invoke-DocApi -Method GET -Path "/children?parentId=$ParentId")
    $existing = $children | Where-Object {
        $_.type -eq "markdown" -and ($_.title -eq $Title -or $_.name -eq $Title)
    } | Select-Object -First 1
    if ($existing) {
        $ver = if ($null -ne $existing.currentVersionNumber) { [int]$existing.currentVersionNumber } else { 1 }
        Invoke-DocApi -Method PUT -Path "/markdown/$($existing.id)" -Body @{
            title                 = $Title
            content               = $content
            expectedVersionNumber = $ver
            isDraft               = $false
        } | Out-Null
        Write-Host "  OK guncellendi '$Title'" -ForegroundColor Green
    } else {
        Invoke-DocApi -Method POST -Path "/markdown" -Body @{
            parentId = $ParentId
            title    = $Title
            content  = $content
            isDraft  = $false
        } | Out-Null
        Write-Host "  OK olusturuldu '$Title'" -ForegroundColor Green
    }
}

Write-Host "`n=== SIEM IT rehberleri seed ===" -ForegroundColor Cyan
Write-Host "Gateway: $BaseUrl`n" -ForegroundColor DarkGray

$monitraNgId = Ensure-Folder -Name "MonitraNG"
$ogreticilerId = Ensure-Folder -Name "Öğreticiler" -ParentId $monitraNgId
$itId = Ensure-Folder -Name "IT ve Güvenlik" -ParentId $ogreticilerId

Ensure-Markdown -ParentId $itId `
    -Title "Güvenlik Merkezi — Linux rsyslog kurulumu" `
    -FileName "guvenlik-merkezi-linux-rsyslog-kurulumu.md"

Write-Host "`nTamamlandi." -ForegroundColor Green
Write-Host "UI: Dokumanlar > MonitraNG > Ogreticiler > IT ve Guvenlik" -ForegroundColor Cyan
