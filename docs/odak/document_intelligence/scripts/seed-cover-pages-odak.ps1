# Odak test — kapak sayfasi katalog seed (D-BR2)
#
#   .\docs\odak\document_intelligence\scripts\seed-cover-pages-odak.ps1
#   .\docs\odak\document_intelligence\scripts\seed-cover-pages-odak.ps1 -BaseUrl "http://192.168.20.20:5040"

param(
    [string]$BaseUrl = "http://192.168.20.20:5040",
    [string]$Token = $env:DI_TOKEN,
    [switch]$WhatIf = $false
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
$repoRoot = (Resolve-Path (Join-Path $scriptDir "../../../..")).Path
$seedFile = Join-Path $repoRoot "docs/odak/document_intelligence/datasets/seed-cover-pages-odak.json"

$token = $Token
if ([string]::IsNullOrEmpty($token)) {
    $loadToken = Join-Path $scriptDir "..\..\operationcore\scripts\load-operationcore-token.ps1"
    if (Test-Path $loadToken) { $token = & $loadToken }
}
if ([string]::IsNullOrEmpty($token)) {
    Write-Host "Token yok." -ForegroundColor Red
    exit 1
}
$token = $token.Trim()
$headers = @{
    Authorization = "Bearer $token"
    "Content-Type" = "application/json"
}

$seed = Get-Content $seedFile -Raw -Encoding UTF8 | ConvertFrom-Json
$coverPagesBase = "$BaseUrl/documents/api/v1/cover-pages"

function Invoke-DmApi {
    param(
        [string]$Method,
        [string]$Uri,
        [object]$Body = $null
    )
    $params = @{
        Uri = $Uri
        Method = $Method
        Headers = $headers
        TimeoutSec = 120
    }
    if ($null -ne $Body) {
        $params.Body = ($Body | ConvertTo-Json -Depth 10 -Compress)
    }
    return Invoke-RestMethod @params
}

Write-Host "=== Kapak sayfasi katalog seed -> $BaseUrl ===" -ForegroundColor Cyan

$codeToId = @{}
foreach ($entry in $seed.coverPages) {
    $code = [string]$entry.code
    $body = @{
        name = [string]$entry.name
        code = $code
        description = [string]$entry.description
        isDefault = [bool]$entry.isDefault
        isActive = [bool]$entry.isActive
        definition = $entry.definition
        settings = $seed.defaultSettings
    }

    $existing = $null
    try {
        $list = Invoke-DmApi -Method GET -Uri $coverPagesBase
        $existing = @($list.items) | Where-Object { $_.code -eq $code } | Select-Object -First 1
    } catch { }

    if ($WhatIf) {
        Write-Host "WhatIf $($entry.name) ($code)" -ForegroundColor Yellow
        continue
    }

    if ($existing) {
        $updated = Invoke-DmApi -Method PUT -Uri "$coverPagesBase/$($existing.id)" -Body $body
        $codeToId[$code] = [string]$updated.id
        Write-Host "OK update $code id=$($updated.id)" -ForegroundColor Green
    } else {
        $created = Invoke-DmApi -Method POST -Uri $coverPagesBase -Body $body
        $codeToId[$code] = [string]$created.id
        Write-Host "OK create $code id=$($created.id)" -ForegroundColor Green
    }

    try {
        Invoke-DmApi -Method GET -Uri "$coverPagesBase/$($codeToId[$code])/design-session" | Out-Null
        Write-Host "  OK design-session init $code" -ForegroundColor DarkGray
    } catch {
        Write-Host "  WARN design-session $code : $_" -ForegroundColor Yellow
    }
}

Write-Host "Seed tamam." -ForegroundColor Cyan
