# Document Intelligence — Eğitim / Proje (Atölye kabulü)
#
# Klasor:
#   Sayfalar / Eğitim / Proje /
#     01 … 08 markdown sayfalari
#
# Kaynak: docs/odak/project_management/egitim/atolye-kabul/*.md
# Goreli ./NN-*.md linkleri yayinda /apps/document-intelligence/r/{id} olur.
#
# Usage (repo kokunden):
#   $env:DI_TOKEN = "<token>"
#   .\docs\odak\document_intelligence\scripts\seed-egitim-proje.ps1 -BaseUrl "http://192.168.1.42:5040"
#
#   .\docs\odak\document_intelligence\scripts\seed-egitim-proje.ps1 -WhatIf

param(
    [string]$BaseUrl = "http://192.168.1.42:5040",
    [switch]$WhatIf = $false
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
$pagesDir = Join-Path $scriptDir "..\..\project_management\egitim\atolye-kabul"
$utf8 = [System.Text.Encoding]::UTF8

$token = $env:DI_TOKEN
if ([string]::IsNullOrWhiteSpace($token)) {
    Write-Host "Token yok. `$env:DI_TOKEN set edin." -ForegroundColor Red
    exit 1
}
$token = $token.Trim()

$headers = @{ Authorization = "Bearer $token" }
$apiBase = "$BaseUrl/documents/api/v1/resources"

$folderSayfalar = "Sayfalar"
$folderEgitim = "E$([char]0x011F)itim"
$folderProje = "Proje"

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
        Write-Host "  SKIP klasor '$Name' (id=$($existing.id))" -ForegroundColor Green
        return $existing.id
    }
    if ($WhatIf) {
        Write-Host "  WhatIf klasor '$Name'" -ForegroundColor Yellow
        return "<whatif-$Name>"
    }
    $body = @{ name = $Name }
    if ($ParentId) { $body.parentId = $ParentId }
    $created = Invoke-DocApi -Method POST -Path "/folder" -Body $body
    Write-Host "  OK klasor '$Name' (id=$($created.id))" -ForegroundColor Green
    return $created.id
}

function Get-PageHeading {
    param([string]$Content, [string]$Fallback)
    $line = ($Content -split "`r?`n") | Where-Object { $_ -match '^#\s+\S' } | Select-Object -First 1
    if ($line -match '^#\s+(.+)$') { return $Matches[1].Trim() }
    return $Fallback
}

function Get-PageSpecs {
    $files = Get-ChildItem -Path $pagesDir -Filter "*.md" | Sort-Object Name
    if ($files.Count -eq 0) { throw "Markdown yok: $pagesDir" }
    $specs = @()
    foreach ($file in $files) {
        $content = [IO.File]::ReadAllText($file.FullName, $utf8)
        $heading = Get-PageHeading -Content $content -Fallback $file.BaseName
        $prefix = if ($file.Name -match '^(\d+)') { $Matches[1] } else { "" }
        $title = if ($prefix) { "$prefix $heading" } else { $heading }
        $specs += [pscustomobject]@{
            FileName = $file.Name
            Title    = $title
            Content  = $content
        }
    }
    return $specs
}

function Find-Markdown {
    param([string]$ParentId, [string]$Title)
    $children = Get-Items (Invoke-DocApi -Method GET -Path "/children?parentId=$ParentId")
    return $children | Where-Object {
        $_.type -eq "markdown" -and ($_.title -eq $Title -or $_.name -eq $Title)
    } | Select-Object -First 1
}

function Save-Markdown {
    param([string]$ParentId, [string]$Title, [string]$Content)
    if ($WhatIf) {
        Write-Host "  WhatIf '$Title'" -ForegroundColor Yellow
        return "whatif"
    }
    $existing = Find-Markdown -ParentId $ParentId -Title $Title
    if ($existing) {
        $ver = if ($null -ne $existing.currentVersionNumber) { [int]$existing.currentVersionNumber } else { 1 }
        Invoke-DocApi -Method PUT -Path "/markdown/$($existing.id)" -Body @{
            title                 = $Title
            content               = $Content
            expectedVersionNumber = $ver
            isDraft               = $false
        } | Out-Null
        Write-Host "  OK guncellendi '$Title'" -ForegroundColor Green
        return $existing.id
    }
    $created = Invoke-DocApi -Method POST -Path "/markdown" -Body @{
        parentId = $ParentId
        title    = $Title
        content  = $Content
        isDraft  = $false
    }
    Write-Host "  OK olusturuldu '$Title' (id=$($created.id))" -ForegroundColor Green
    return $created.id
}

function Set-DiLinks {
    param([string]$Content, [hashtable]$IdByFile)
    $result = $Content
    foreach ($file in @($IdByFile.Keys)) {
        $id = $IdByFile[$file]
        $result = $result.Replace("](./$file)", "](/apps/document-intelligence/r/$id)")
    }
    return $result
}

Write-Host "`n=== Egitim / Proje seed ===" -ForegroundColor Cyan
Write-Host "Gateway: $BaseUrl" -ForegroundColor DarkGray
Write-Host "Kaynak: $pagesDir`n" -ForegroundColor DarkGray

$specs = Get-PageSpecs
$sayfalarId = Ensure-Folder -Name $folderSayfalar
$egitimId = Ensure-Folder -Name $folderEgitim -ParentId $sayfalarId
$projeId = Ensure-Folder -Name $folderProje -ParentId $egitimId

$idByFile = @{}
Write-Host "`nSayfalar:" -ForegroundColor Cyan
foreach ($spec in $specs) {
    $id = Save-Markdown -ParentId $projeId -Title $spec.Title -Content $spec.Content
    $idByFile[$spec.FileName] = $id
}

if (-not $WhatIf) {
    Write-Host "`nIc linkler:" -ForegroundColor Cyan
    foreach ($spec in $specs) {
        $linked = Set-DiLinks -Content $spec.Content -IdByFile $idByFile
        if ($linked -eq $spec.Content) { continue }
        Save-Markdown -ParentId $projeId -Title $spec.Title -Content $linked | Out-Null
    }
}

Write-Host "`nTamamlandi." -ForegroundColor Green
Write-Host "UI: Dokumanlar > Sayfalar > Egitim > Proje" -ForegroundColor Cyan
