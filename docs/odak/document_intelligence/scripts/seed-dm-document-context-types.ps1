# G3 — dm_document_context_types seed (idempotent upsert by type)
#
#   .\docs\odak\document_intelligence\scripts\seed-dm-document-context-types.ps1
#   .\docs\odak\document_intelligence\scripts\seed-dm-document-context-types.ps1 -BaseUrl "http://192.168.20.20:5040"

param(
    [string]$BaseUrl = "http://192.168.20.20:5040",
    [string]$Token = $env:DI_TOKEN,
    [switch]$WhatIf = $false
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
$repoRoot = (Resolve-Path (Join-Path $scriptDir "../../../..")).Path
$seedFile = Join-Path $repoRoot "docs/odak/document_intelligence/datasets/seed-dm-document-context-types.json"
$dataset = "dm_document_context_types"

$token = $Token
if ([string]::IsNullOrEmpty($token)) {
    $loadTokenScript = Join-Path $scriptDir "..\..\operationcore\scripts\load-operationcore-token.ps1"
    if (Test-Path $loadTokenScript) { $token = & $loadTokenScript }
}
if ([string]::IsNullOrEmpty($token)) { throw "Token yok." }
$token = $token.Trim()

$headers = @{
    Authorization = "Bearer $token"
    "Content-Type" = "application/json"
}
$dataBase = "$BaseUrl/data/api/v1/data/$dataset"
$utf8 = [System.Text.Encoding]::UTF8

function Invoke-DgData {
    param(
        [string]$Method,
        [string]$Uri,
        [hashtable]$Body = $null
    )
    if ($Body) {
        $json = $Body | ConvertTo-Json -Depth 12 -Compress
        $bytes = $utf8.GetBytes($json)
        return Invoke-RestMethod -Uri $Uri -Headers $headers -Method $Method `
            -Body $bytes -ContentType "application/json; charset=utf-8"
    }
    return Invoke-RestMethod -Uri $Uri -Headers $headers -Method $Method
}

function Find-ByType {
    param([string]$Type)
    $filter = [Uri]::EscapeDataString("type:eq:$Type")
    $uri = "${dataBase}?filter=$filter&limit=1"
    $res = Invoke-DgData -Method GET -Uri $uri
    $items = @($res.items)
    if ($items.Count -eq 0 -and $res.data) { $items = @($res.data) }
    return $items | Select-Object -First 1
}

if (-not (Test-Path $seedFile)) { throw "Seed dosyasi yok: $seedFile" }
$seed = Get-Content $seedFile -Raw -Encoding UTF8 | ConvertFrom-Json

Write-Host "G3 context types seed -> $BaseUrl ($dataset)" -ForegroundColor Cyan

foreach ($rec in @($seed.records)) {
    $type = [string]$rec.type
    $body = @{
        type           = $type
        displayName    = [string]$rec.displayName
        rootDataset    = [string]$rec.rootDataset
        definitionJson = [string]$rec.definitionJson
        isActive       = if ($null -ne $rec.isActive) { [bool]$rec.isActive } else { $true }
    }

    $existing = Find-ByType -Type $type
    if ($WhatIf) {
        $action = if ($existing) { "PUT" } else { "POST" }
        Write-Host "  WhatIf $action type=$type" -ForegroundColor Yellow
        continue
    }

    if ($existing -and $existing.dataId) {
        $id = [string]$existing.dataId
        Invoke-DgData -Method PUT -Uri "$dataBase/$id" -Body $body | Out-Null
        Write-Host "  OK update type=$type id=$id" -ForegroundColor Green
    }
    else {
        $created = Invoke-DgData -Method POST -Uri $dataBase -Body $body
        $id = if ($created.dataId) { [string]$created.dataId } else { [string]$created.id }
        Write-Host "  OK create type=$type id=$id" -ForegroundColor Green
    }
}

Write-Host "Tamamlandi." -ForegroundColor Cyan
