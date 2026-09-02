# F1-9 — PMO + kalite iş paketi katalog tohumu (idempotent).
# Şema yazmaz. Türleri (core seed) + DI klasör/şablon/diyagram basar.
#
#   .\docs\odak\project_management\scripts\seed-job-packs.ps1
#   .\docs\odak\project_management\scripts\seed-job-packs.ps1 -WhatIf
param(
    [string]$BaseUrl = "http://192.168.20.20:5040",
    [string]$Token = $env:DI_TOKEN,
    [switch]$WhatIf = $false
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
$repoRoot = (Resolve-Path (Join-Path $scriptDir "../../../..")).Path
. (Join-Path $repoRoot "docs/odak/document_intelligence/scripts/lib/Seed-DmCatalogByCode.ps1")

$kindsFile = Join-Path $repoRoot "docs/odak/document_intelligence/datasets/seed-dm-resource-kinds.json"
$packsDir = Join-Path $repoRoot "MngOperations/Core/MngOperations.Application/Packs"
$drawioPath = Join-Path $repoRoot "docs/odak/project_management/packs/teslimat-omurgasi.drawio"

if ([string]::IsNullOrWhiteSpace($Token) -and -not [string]::IsNullOrWhiteSpace($env:DI_TOKEN)) {
    $Token = $env:DI_TOKEN
}
if ([string]::IsNullOrWhiteSpace($Token)) {
    $loader = Join-Path $repoRoot "docs/odak/operationcore/scripts/load-operationcore-token.ps1"
    if (Test-Path $loader) { $Token = (& $loader) }
}
if ([string]::IsNullOrWhiteSpace($Token)) { throw "Token yok." }
$Token = $Token.Trim()
$headers = @{ Authorization = "Bearer $Token" }
$apiBase = "$BaseUrl/documents/api/v1/resources"
$utf8 = [System.Text.Encoding]::UTF8

function Invoke-DocApi {
    param([string]$Method, [string]$Path, [hashtable]$Body = $null)
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

function Find-Folder {
    param([string]$Name, [string]$ParentId = $null)
    if ($ParentId) {
        $siblings = Get-Items (Invoke-DocApi -Method GET -Path "/children?parentId=$ParentId")
    }
    else {
        $siblings = Get-Items (Invoke-DocApi -Method GET -Path "/children")
    }
    return $siblings | Where-Object { $_.type -eq "folder" -and $_.name -eq $Name } | Select-Object -First 1
}

function Ensure-Folder {
    param([string]$Name, [string]$ParentId = $null)
    $existing = Find-Folder -Name $Name -ParentId $ParentId
    if ($existing) {
        Write-Host "  SKIP folder '$Name'" -ForegroundColor Green
        return [string]$existing.id
    }
    if ($WhatIf) {
        Write-Host "  WhatIf folder '$Name'" -ForegroundColor Yellow
        return "whatif-$Name"
    }
    $body = @{ name = $Name }
    if ($ParentId) { $body.parentId = $ParentId }
    $created = Invoke-DocApi -Method POST -Path "/folder" -Body $body
    Write-Host "  OK folder '$Name' id=$($created.id)" -ForegroundColor Green
    return [string]$created.id
}

function Ensure-Markdown {
    param(
        [string]$ParentId,
        [string]$Title,
        [string]$Content,
        [string]$Kind
    )
    if ($WhatIf) {
        Write-Host "  WhatIf markdown '$Title' kind=$Kind" -ForegroundColor Yellow
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
            content               = $Content
            expectedVersionNumber = $ver
            isDraft               = $false
        } | Out-Null
        if ($Kind) {
            Invoke-DocApi -Method PATCH -Path "/$($existing.id)/metadata" -Body @{ kind = $Kind } | Out-Null
        }
        Write-Host "  OK update markdown '$Title'" -ForegroundColor Green
        return
    }
    $created = Invoke-DocApi -Method POST -Path "/markdown" -Body @{
        parentId = $ParentId
        title    = $Title
        content  = $Content
        isDraft  = $false
    }
    if ($Kind -and $created.id) {
        Invoke-DocApi -Method PATCH -Path "/$($created.id)/metadata" -Body @{ kind = $Kind } | Out-Null
    }
    Write-Host "  OK markdown '$Title'" -ForegroundColor Green
}

function Ensure-Drawio {
    param(
        [string]$ParentId,
        [string]$Title,
        [string]$Kind
    )
    if (-not (Test-Path $drawioPath)) { throw "draw.io yok: $drawioPath" }
    if ($WhatIf) {
        Write-Host "  WhatIf drawio '$Title'" -ForegroundColor Yellow
        return
    }
    $children = Get-Items (Invoke-DocApi -Method GET -Path "/children?parentId=$ParentId")
    $existing = $children | Where-Object {
        $_.type -eq "file" -and ($_.name -eq $Title -or $_.title -eq $Title)
    } | Select-Object -First 1
    if ($existing) {
        Write-Host "  SKIP drawio '$Title'" -ForegroundColor Green
        return
    }
    $bytes = [System.IO.File]::ReadAllBytes($drawioPath)
    $b64 = [Convert]::ToBase64String($bytes)
    $created = Invoke-DocApi -Method POST -Path "/file" -Body @{
        parentId         = $ParentId
        name             = $Title
        mimeType         = "application/vnd.jgraph.mxfile"
        extension        = "drawio"
        size             = $bytes.Length
        content          = $b64
        originalFileName = $Title
        kind             = $Kind
    }
    Write-Host "  OK drawio '$Title' id=$($created.id)" -ForegroundColor Green
}

Write-Host "F1-9 job packs seed -> $BaseUrl" -ForegroundColor Cyan

if (-not $WhatIf) {
    Invoke-DmCatalogSeed -BaseUrl $BaseUrl -Token $Token -Dataset "dm_resource_kinds" -SeedFile $kindsFile -Label "F1-9 resource kinds"
}

$dokumanlar = Find-Folder -Name "Dökümanlar"
if (-not $dokumanlar) { $dokumanlar = Find-Folder -Name "Dokumanlar" }
if (-not $dokumanlar) { throw "Kokte 'Dökümanlar' klasoru yok. Once resource-roots seed calistirin." }
$docsId = [string]$dokumanlar.id
$packsRoot = Ensure-Folder -Name "İş paketleri" -ParentId $docsId

$packFiles = Get-ChildItem -Path $packsDir -Filter "*.json" | Sort-Object Name
if ($packFiles.Count -eq 0) { throw "Pack JSON yok: $packsDir" }

foreach ($file in $packFiles) {
    $pack = Get-Content $file.FullName -Raw -Encoding UTF8 | ConvertFrom-Json
    Write-Host ""
    Write-Host "=== pack $($pack.code) ===" -ForegroundColor Cyan
    $packFolderId = Ensure-Folder -Name ([string]$pack.name) -ParentId $packsRoot
    $folderIds = @{}
    foreach ($folder in @($pack.folders)) {
        $folderIds[[string]$folder.name] = Ensure-Folder -Name ([string]$folder.name) -ParentId $packFolderId
    }
    foreach ($starter in @($pack.starters)) {
        $parent = $folderIds[[string]$starter.folder]
        if (-not $parent) { throw "Pack $($pack.code): folder '$($starter.folder)' yok" }
        Ensure-Markdown -ParentId $parent -Title ([string]$starter.title) -Content ([string]$starter.body) -Kind ([string]$starter.kind)
    }
    if ($pack.diagram) {
        $diagParent = $folderIds[[string]$pack.diagram.folder]
        if (-not $diagParent) { throw "Pack $($pack.code): diagram folder yok" }
        Ensure-Drawio -ParentId $diagParent -Title ([string]$pack.diagram.title) -Kind ([string]$pack.diagram.kind)
    }
}

Write-Host ""
Write-Host "F1-9 job packs seed OK" -ForegroundColor Green
