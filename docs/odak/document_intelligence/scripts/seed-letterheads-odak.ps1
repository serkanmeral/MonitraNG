# Odak test — antet katalog seed + sablon varsayilan antet baglantisi (D-BR1)
#
#   .\docs\odak\document_intelligence\scripts\seed-letterheads-odak.ps1
#   .\docs\odak\document_intelligence\scripts\seed-letterheads-odak.ps1 -BaseUrl "http://192.168.20.20:5040"

param(
    [string]$BaseUrl = "http://192.168.20.20:5040",
    [string]$Token = $env:DI_TOKEN,
    [switch]$WhatIf = $false
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
$repoRoot = (Resolve-Path (Join-Path $scriptDir "../../../..")).Path
$seedFile = Join-Path $repoRoot "docs/odak/document_intelligence/datasets/seed-letterheads-odak.json"

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
$letterheadsBase = "$BaseUrl/documents/api/v1/letterheads"
$templatesBase = "$BaseUrl/documents/api/v1/templates"

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

function Merge-LetterheadSettings {
    param($defaults, $override)
    if (-not $override) { return $defaults }
    $result = @{}
    foreach ($prop in $defaults.PSObject.Properties) {
        $result[$prop.Name] = $prop.Value
    }
    foreach ($prop in $override.PSObject.Properties) {
        $result[$prop.Name] = $prop.Value
    }
    return [PSCustomObject]$result
}

Write-Host "=== Antet katalog seed -> $BaseUrl ===" -ForegroundColor Cyan

$codeToId = @{}
foreach ($entry in $seed.letterheads) {
    $code = [string]$entry.code
    $body = @{
        name = [string]$entry.name
        code = $code
        description = [string]$entry.description
        isDefault = [bool]$entry.isDefault
        isActive = [bool]$entry.isActive
        letterhead = $entry.letterhead
        settings = Merge-LetterheadSettings $seed.defaultSettings $entry.settings
    }

    $existing = $null
    try {
        $list = Invoke-DmApi -Method GET -Uri $letterheadsBase
        $existing = $list.items | Where-Object { $_.code -eq $code } | Select-Object -First 1
    } catch {
        Write-Host "Letterheads list failed (dataset kurulu mu?): $_" -ForegroundColor Red
        throw
    }

    if ($WhatIf) {
        Write-Host "WHATIF $($entry.name) ($code)" -ForegroundColor DarkGray
        continue
    }

    if ($existing) {
        $updated = Invoke-DmApi -Method PUT -Uri "$letterheadsBase/$($existing.id)" -Body $body
        $codeToId[$code] = $updated.id
        Write-Host "OK update $code id=$($updated.id)" -ForegroundColor Green
    } else {
        $created = Invoke-DmApi -Method POST -Uri $letterheadsBase -Body $body
        $codeToId[$code] = $created.id
        Write-Host "OK create $code id=$($created.id)" -ForegroundColor Green
    }
}

$defaultId = $codeToId["ODK-STD"]
if (-not $defaultId) {
    $list = Invoke-DmApi -Method GET -Uri $letterheadsBase
    $defaultId = ($list.items | Where-Object { $_.code -eq "ODK-STD" } | Select-Object -First 1).id
}

if ($WhatIf) { exit 0 }

if (-not $defaultId) {
    Write-Host "ODK-STD id bulunamadi; sablon patch atlandi." -ForegroundColor Yellow
    exit 0
}

Write-Host "=== Sablon varsayilan antet -> $defaultId ===" -ForegroundColor Cyan
$templates = Invoke-DmApi -Method GET -Uri $templatesBase
foreach ($tplCode in $seed.templateCodes) {
    $tpl = $templates.items | Where-Object { $_.code -eq $tplCode } | Select-Object -First 1
    if (-not $tpl) {
        Write-Host "SKIP sablon yok: $tplCode" -ForegroundColor Yellow
        continue
    }

    $patchBody = @{
        defaultLetterheadId = $defaultId
    }

    try {
        Invoke-DmApi -Method PUT -Uri "$templatesBase/$($tpl.id)/page-structure" -Body $patchBody | Out-Null
        Write-Host "OK template $tplCode -> defaultLetterheadId" -ForegroundColor Green
    } catch {
        if ($detail.status -eq "published") {
            Write-Host "WARN $tplCode published — once unpublish veya draft clone gerekir: $_" -ForegroundColor Yellow
        } else {
            throw
        }
    }
}

Write-Host "=== Antet seed tamam ===" -ForegroundColor Green
