# Document Intelligence — prod markdown sayfalarini test ortamina kopyalar (API).
#
# Prod agaci (ornek): MonitraNG/Ogreticiler/..., System/...
# Test convention (P-B): Sayfalar/MonitraNG/..., Sayfalar/System/...
#
# Yalnizca markdown icerigi + klasor yapisi; dosya (PDF/DOCX) ve izinler kopyalanmaz.
#
# Usage (repo kokunden):
#   .\docs\odak\document_intelligence\scripts\sync-markdown-prod-to-test.ps1 -WhatIf
#   .\docs\odak\document_intelligence\scripts\sync-markdown-prod-to-test.ps1
#   .\docs\odak\document_intelligence\scripts\sync-markdown-prod-to-test.ps1 -SourceSubtree "MonitraNG/Ogreticiler"
#   .\docs\odak\document_intelligence\scripts\sync-markdown-prod-to-test.ps1 -ForceUpdate

param(
    [string]$ProdUrl = "http://192.168.20.8:5040",
    [string]$TestUrl = "http://192.168.20.20:5040",
    [string]$SourceSubtree = "",
    [switch]$WhatIf = $false,
    [switch]$ForceUpdate = $false,
    [switch]$SkipExisting = $false,
    [string[]]$SayfalarPrefixRoots = @("MonitraNG", "System")
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
$utf8 = [System.Text.Encoding]::UTF8

function Get-OdakToken {
    param(
        [bool]$IsProd,
        [switch]$AutoRefresh
    )
    $loadScript = if ($IsProd) {
        Join-Path $scriptDir "..\..\operationcore\scripts\load-operationcore-token-prod.ps1"
    } else {
        Join-Path $scriptDir "..\..\operationcore\scripts\load-operationcore-token.ps1"
    }
    if (-not (Test-Path $loadScript)) {
        throw "Token script bulunamadi: $loadScript"
    }
    if ($AutoRefresh) {
        return & $loadScript -AutoRefresh
    }
    return & $loadScript
}

$prodToken = $env:DI_TOKEN_PROD
if ([string]::IsNullOrWhiteSpace($prodToken)) {
    $prodToken = Get-OdakToken -IsProd $true -AutoRefresh
}
$testToken = $env:DI_TOKEN
if ([string]::IsNullOrWhiteSpace($testToken)) {
    $testToken = Get-OdakToken -IsProd $false -AutoRefresh
}

function New-DocContext {
    param(
        [string]$BaseUrl,
        [string]$Token,
        [string]$Label
    )
    return @{
        Label   = $Label
        ApiBase = "$BaseUrl/documents/api/v1/resources"
        Headers = @{ Authorization = "Bearer $($Token.Trim())" }
    }
}

function Get-DocItems($response) {
    if ($null -eq $response) { return @() }
    if ($null -ne $response.items) {
        return @($response.items | ForEach-Object { $_ })
    }
    if ($response -is [System.Array]) {
        return @($response | ForEach-Object { $_ })
    }
    return @($response)
}

function Invoke-CtxDocApi {
    param(
        [hashtable]$Ctx,
        [string]$Method,
        [string]$Path,
        [hashtable]$Body
    )
    $uri = "$($Ctx.ApiBase)$Path"
    if ($Body) {
        $json = $Body | ConvertTo-Json -Depth 12 -Compress
        $bytes = $utf8.GetBytes($json)
        return Invoke-RestMethod -Uri $uri -Headers $Ctx.Headers -Method $Method -Body $bytes -ContentType "application/json; charset=utf-8"
    }
    return Invoke-RestMethod -Uri $uri -Headers $Ctx.Headers -Method $Method
}

function Get-CtxChildren {
    param(
        [hashtable]$Ctx,
        [string]$ParentId = $null
    )
    $path = if ($ParentId) { "/children?parentId=$ParentId" } else { "/children" }
    return Get-DocItems (Invoke-CtxDocApi -Ctx $Ctx -Method GET -Path $path)
}

function Find-CtxFolder {
    param(
        [hashtable]$Ctx,
        [string]$Name,
        [string]$ParentId = $null
    )
    foreach ($item in Get-CtxChildren -Ctx $Ctx -ParentId $ParentId) {
        if ($item.type -eq "folder" -and $item.name -eq $Name) {
            return $item
        }
    }
    return $null
}

function Ensure-CtxFolder {
    param(
        [hashtable]$Ctx,
        [string]$Name,
        [string]$ParentId = $null
    )
    $existing = Find-CtxFolder -Ctx $Ctx -Name $Name -ParentId $ParentId
    if ($existing) { return $existing.id }

    if ($WhatIf) {
        Write-Host "  WhatIf [$($Ctx.Label)] POST folder '$Name'" -ForegroundColor Yellow
        return "<whatif-$Name>"
    }

    $body = @{ name = $Name }
    if ($ParentId) { $body.parentId = $ParentId }
    $created = Invoke-CtxDocApi -Ctx $Ctx -Method POST -Path "/folder" -Body $body
    Write-Host "  OK [$($Ctx.Label)] klasor: $Name" -ForegroundColor Green
    return $created.id
}

function Resolve-CtxFolderPath {
    param(
        [hashtable]$Ctx,
        [string[]]$Segments
    )
    $parentId = $null
    foreach ($segment in $Segments) {
        if ([string]::IsNullOrWhiteSpace($segment)) { continue }
        $parentId = Ensure-CtxFolder -Ctx $Ctx -Name $segment -ParentId $parentId
    }
    return $parentId
}

function Find-CtxMarkdownInParent {
    param(
        [hashtable]$Ctx,
        [string]$ParentId,
        [string]$Title
    )
    foreach ($item in Get-CtxChildren -Ctx $Ctx -ParentId $ParentId) {
        if ($item.type -eq "markdown" -and ($item.title -eq $Title -or $item.name -eq $Title)) {
            return $item
        }
    }
    return $null
}

$prodCtx = New-DocContext -BaseUrl $ProdUrl -Token $prodToken -Label "prod"
$testCtx = New-DocContext -BaseUrl $TestUrl -Token $testToken -Label "test"

function Convert-ProdRelativePathToTest {
    param([string]$RelativePath)
    if ([string]::IsNullOrWhiteSpace($RelativePath)) { return @() }

    $segments = @($RelativePath -split '/' | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    if ($segments.Count -eq 0) { return @() }

    $first = $segments[0]
    foreach ($root in $SayfalarPrefixRoots) {
        if ($first -eq $root) {
            return @("Sayfalar") + $segments
        }
    }
    return $segments
}

function Resolve-ProdFolderByRelativePath {
    param([string]$RelativePath)

    if ([string]::IsNullOrWhiteSpace($RelativePath)) {
        return $null
    }

    $parentId = $null
    foreach ($segment in ($RelativePath -split '/' | Where-Object { $_ })) {
        $folder = Find-CtxFolder -Ctx $prodCtx -Name $segment -ParentId $parentId
        if (-not $folder) {
            throw "Prod klasor bulunamadi: $RelativePath (eksik: $segment)"
        }
        $parentId = $folder.id
    }
    return $parentId
}

function Sync-MarkdownPage {
    param(
        [object]$ProdItem,
        [string]$ProdFolderRelativePath
    )
    $title = if ($ProdItem.title) { $ProdItem.title } else { $ProdItem.name }
    $folderSegments = Convert-ProdRelativePathToTest -RelativePath $ProdFolderRelativePath
    $targetPath = ($folderSegments -join '/')

    if ($WhatIf) {
        $action = if ($SkipExisting) { "CREATE?" } else { "UPSERT" }
        Write-Host "  WhatIf $action markdown '$title' -> $targetPath" -ForegroundColor Yellow
        $script:Stats.Planned++
        return
    }

    $targetParentId = Resolve-CtxFolderPath -Ctx $testCtx -Segments $folderSegments
    $existing = Find-CtxMarkdownInParent -Ctx $testCtx -ParentId $targetParentId -Title $title

    if ($existing -and $SkipExisting) {
        Write-Host "  SKIP (var): '$title' -> $targetPath" -ForegroundColor Gray
        $script:Stats.Skipped++
        return
    }

    if ($existing -and -not $ForceUpdate) {
        $prodUpdated = if ($ProdItem.updatedAt) { [datetime]$ProdItem.updatedAt } else { [datetime]::MinValue }
        $testUpdated = if ($existing.updatedAt) { [datetime]$existing.updatedAt } else { [datetime]::MinValue }
        if ($testUpdated -ge $prodUpdated) {
            Write-Host "  SKIP (test guncel): '$title'" -ForegroundColor Gray
            $script:Stats.Skipped++
            return
        }
    }

    $contentDto = Invoke-CtxDocApi -Ctx $prodCtx -Method GET -Path "/markdown/$($ProdItem.id)/content"
    $isDraft = ($ProdItem.status -eq "draft")

    if ($existing) {
        $ver = if ($null -ne $existing.currentVersionNumber) { [int]$existing.currentVersionNumber } else { 1 }
        Write-Host "  UPDATE '$title' -> $targetPath (v$ver)..." -ForegroundColor Yellow
        Invoke-CtxDocApi -Ctx $testCtx -Method PUT -Path "/markdown/$($existing.id)" -Body @{
            title                 = $title
            content               = $contentDto.content
            expectedVersionNumber = $ver
            isDraft               = $isDraft
        } | Out-Null
        $script:Stats.Updated++
    }
    else {
        Write-Host "  CREATE '$title' -> $targetPath..." -ForegroundColor Yellow
        Invoke-CtxDocApi -Ctx $testCtx -Method POST -Path "/markdown" -Body @{
            parentId = $targetParentId
            title    = $title
            content  = $contentDto.content
            isDraft  = $isDraft
        } | Out-Null
        $script:Stats.Created++
    }
    Write-Host "    OK" -ForegroundColor Green
}

function Walk-ProdFolder {
    param(
        [string]$FolderId,
        [string]$RelativePath
    )
    $children = Get-CtxChildren -Ctx $prodCtx -ParentId $FolderId
    foreach ($child in $children) {
        if ($child.type -eq "folder") {
            $childPath = if ($RelativePath) { "$RelativePath/$($child.name)" } else { $child.name }
            Walk-ProdFolder -FolderId $child.id -RelativePath $childPath
        }
        elseif ($child.type -eq "markdown") {
            Sync-MarkdownPage -ProdItem $child -ProdFolderRelativePath $RelativePath
        }
    }
}

$script:Stats = @{
    Planned = 0
    Created = 0
    Updated = 0
    Skipped = 0
}

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "DI Markdown Sync: prod -> test" -ForegroundColor Cyan
Write-Host "Prod:  $ProdUrl" -ForegroundColor Cyan
Write-Host "Test:  $TestUrl" -ForegroundColor Cyan
if ($SourceSubtree) { Write-Host "Kaynak: $SourceSubtree" -ForegroundColor Cyan }
if ($WhatIf) { Write-Host "(WhatIf — yazma yok)" -ForegroundColor Yellow }
Write-Host "========================================`n" -ForegroundColor Cyan

$startFolderId = Resolve-ProdFolderByRelativePath -RelativePath $SourceSubtree
Walk-ProdFolder -FolderId $startFolderId -RelativePath $SourceSubtree

Write-Host "`nOzet:" -ForegroundColor Cyan
if ($WhatIf) {
    Write-Host "  Planlanan: $($script:Stats.Planned)" -ForegroundColor Gray
}
else {
    Write-Host "  Olusturulan: $($script:Stats.Created)" -ForegroundColor Green
    Write-Host "  Guncellenen: $($script:Stats.Updated)" -ForegroundColor Green
    Write-Host "  Atlanan:     $($script:Stats.Skipped)" -ForegroundColor Gray
}
Write-Host "Tamamlandi." -ForegroundColor Cyan
