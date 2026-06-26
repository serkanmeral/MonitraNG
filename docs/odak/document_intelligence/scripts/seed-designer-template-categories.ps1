# Document Designer — dm_template_categories başlangıç katalog ağacı (D1-beta)
#
# Kullanım:
#   .\docs\odak\document_intelligence\scripts\seed-designer-template-categories.ps1
#   .\docs\odak\document_intelligence\scripts\seed-designer-template-categories.ps1 -BaseUrl "http://192.168.20.8:5040"
#   .\docs\odak\document_intelligence\scripts\seed-designer-template-categories.ps1 -WhatIf
#   .\docs\odak\document_intelligence\scripts\seed-designer-template-categories.ps1 -Reset

param(
    [string]$BaseUrl = "http://192.168.20.8:5040",
    [string]$Token = $env:DI_TOKEN,
    [switch]$WhatIf = $false,
    [switch]$Reset = $false
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
$repoRoot = (Resolve-Path (Join-Path $scriptDir "../../../..")).Path
$seedFile = Join-Path $repoRoot "docs/odak/document_intelligence/datasets/seed-designer-template-categories.json"

$token = $Token
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
    Write-Host "Token yok. -Token, `$env:DI_TOKEN veya OC token script." -ForegroundColor Red
    exit 1
}
$token = $token.Trim()

if (-not (Test-Path $seedFile)) { throw "Seed dosyasi yok: $seedFile" }

$headers = @{
    Authorization = "Bearer $token"
    "Content-Type" = "application/json"
}
$categoriesBase = "$BaseUrl/documents/api/v1/template-categories"
$utf8 = [System.Text.Encoding]::UTF8

function ConvertTo-SeedDef {
    param($Node)
    $def = @{
        Name = [string]$Node.name
    }
    if ($Node.description) { $def.Description = [string]$Node.description }
    $childDefs = @()
    if ($Node.children) {
        foreach ($c in $Node.children) {
            $childDefs += (ConvertTo-SeedDef -Node $c)
        }
    }
    $def.Children = $childDefs
    return $def
}

$seedJson = [IO.File]::ReadAllText($seedFile, $utf8) | ConvertFrom-Json
$seedTree = @()
foreach ($root in $seedJson) {
    $seedTree += (ConvertTo-SeedDef -Node $root)
}

function Invoke-CategoryApi {
    param(
        [string]$Method,
        [string]$RelativePath = "",
        [hashtable]$Body
    )
    $uri = if ($RelativePath) { "$categoriesBase/$RelativePath" } else { $categoriesBase }
    if ($Body) {
        $json = $Body | ConvertTo-Json -Depth 8 -Compress
        $bytes = $utf8.GetBytes($json)
        return Invoke-RestMethod -Uri $uri -Headers $headers -Method $Method -Body $bytes -ContentType "application/json; charset=utf-8"
    }
    if ($Method -eq "DELETE") {
        Invoke-RestMethod -Uri $uri -Headers $headers -Method DELETE | Out-Null
        return $null
    }
    return Invoke-RestMethod -Uri $uri -Headers $headers -Method $Method
}

function Get-CategoryTree {
    $raw = Invoke-CategoryApi -Method GET -RelativePath "tree"
    if ($null -eq $raw) { return @() }
    if ($raw -is [System.Array]) { return , @($raw) }
    return , @($raw)
}

function Find-CategoryInTree {
    param(
        [array]$Roots,
        [string]$Name,
        [string]$ParentId
    )
    if ([string]::IsNullOrEmpty($ParentId)) {
        foreach ($root in $Roots) {
            if ($root.name -eq $Name) { return $root }
        }
        return $null
    }

    function Search([array]$nodes) {
        foreach ($n in $nodes) {
            if ($n.id -eq $ParentId) {
                if ($n.children) {
                    foreach ($c in $n.children) {
                        if ($c.name -eq $Name) { return $c }
                    }
                }
                return $null
            }
            if ($n.children -and $n.children.Count -gt 0) {
                $hit = Search -nodes $n.children
                if ($hit) { return $hit }
            }
        }
        return $null
    }

    return Search -nodes $Roots
}

function Remove-CategoryNode {
    param($Node)
    if ($Node.children) {
        foreach ($child in @($Node.children)) {
            Remove-CategoryNode -Node $child
        }
    }
    if ($WhatIf) {
        Write-Host "  WhatIf DELETE '$($Node.name)' ($($Node.id))" -ForegroundColor Yellow
        return
    }
    Invoke-CategoryApi -Method DELETE -RelativePath $Node.id | Out-Null
    Write-Host "  DEL '$($Node.name)'" -ForegroundColor DarkYellow
}

function Ensure-Category {
    param(
        [hashtable]$Def,
        [string]$ParentId = $null,
        [array]$TreeRoots
    )

    $name = [string]$Def.Name
    $existing = Find-CategoryInTree -Roots $TreeRoots -Name $name -ParentId $ParentId
    if ($existing -and $existing.id) {
        Write-Host "  SKIP '$name' (id=$($existing.id))" -ForegroundColor Green
        $id = [string]$existing.id
    }
    elseif ($WhatIf) {
        Write-Host "  WhatIf '$name'" -ForegroundColor Yellow
        $id = "<whatif-$name>"
    }
    else {
        $body = @{ name = $name }
        if ($Def.Description) { $body.description = [string]$Def.Description }
        if ($ParentId) { $body.parentId = $ParentId }
        $created = Invoke-CategoryApi -Method POST -Body $body
        $id = [string]$created.id
        Write-Host "  OK '$name' (id=$id)" -ForegroundColor Green
    }

    $children = $Def.Children
    if ($children -and $children.Count -gt 0) {
        if (-not $WhatIf) { $TreeRoots = Get-CategoryTree }
        foreach ($child in $children) {
            Ensure-Category -Def $child -ParentId $id -TreeRoots $TreeRoots | Out-Null
        }
    }

    return $id
}

Write-Host ""
Write-Host "Document Designer — template category seed ($BaseUrl)" -ForegroundColor Cyan
Write-Host "Seed: $seedFile" -ForegroundColor Gray
if ($WhatIf) { Write-Host "(WhatIf — yazma yok)" -ForegroundColor Yellow }
if ($Reset) { Write-Host "(-Reset: mevcut agac silinip yeniden olusturulacak)" -ForegroundColor Yellow }
Write-Host ""

$tree = Get-CategoryTree

if ($Reset -and $tree.Count -gt 0) {
    Write-Host "Mevcut kategoriler siliniyor..." -ForegroundColor Yellow
    foreach ($root in @($tree)) {
        Remove-CategoryNode -Node $root
    }
    if (-not $WhatIf) { $tree = Get-CategoryTree }
}

foreach ($rootDef in $seedTree) {
    Write-Host ">> $($rootDef.Name)" -ForegroundColor Yellow
    if (-not $WhatIf) { $tree = Get-CategoryTree }
    Ensure-Category -Def $rootDef -TreeRoots $tree | Out-Null
}

Write-Host ""
Write-Host "Tamamlandi." -ForegroundColor Cyan
if (-not $WhatIf) {
    $final = Get-CategoryTree
    $count = 0
    function Count-Nodes([array]$nodes) {
        foreach ($n in $nodes) {
            $script:count++
            if ($n.children) { Count-Nodes -nodes $n.children }
        }
    }
    Count-Nodes -nodes $final
    Write-Host "Kategori sayisi (agac): $count" -ForegroundColor Green
    $final | ConvertTo-Json -Depth 6
}
