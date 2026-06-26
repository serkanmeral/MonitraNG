# Prod test / taslak dm_document_templates kayitlarini siler (Belge Tasarimcisi smoke/probe)
param(
    [string]$Gateway = "http://192.168.20.8:5040",
    [string]$TokenFile = "$env:TEMP\operationcore_dg_token_prod.txt",
    [switch]$WhatIf
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../../..")).Path
$loadToken = Join-Path $repoRoot "docs/odak/operationcore/scripts/load-operationcore-token-prod.ps1"

$token = if (Test-Path $TokenFile) {
    (Get-Content $TokenFile -Raw).Trim()
} else {
    & $loadToken
}

if ([string]::IsNullOrWhiteSpace($token)) {
    Write-Host "Prod token bulunamadi." -ForegroundColor Red
    exit 1
}

$headers = @{ Authorization = "Bearer $token" }
$templatesPath = "$Gateway/documents/api/v1/templates"
$dataPath = "$Gateway/data/api/v1/data/dm_document_templates"

$list = Invoke-RestMethod -Uri $templatesPath -Headers $headers -TimeoutSec 60
$items = @($list.items)

if ($items.Count -eq 0) {
    Write-Host "Silinecek sablon yok." -ForegroundColor Gray
    exit 0
}

Write-Host "Bulunan sablon: $($items.Count)" -ForegroundColor Cyan
foreach ($row in $items) {
    $id = $row.id
    $name = $row.name
    if (-not $id) { continue }
    Write-Host "  - $name ($id)"
    if ($WhatIf) { continue }
    $uri = "$dataPath/$id"
    Invoke-RestMethod -Uri $uri -Headers $headers -Method DELETE -TimeoutSec 60 | Out-Null
    Write-Host "    silindi" -ForegroundColor Green
}

if ($WhatIf) {
    Write-Host "WhatIf: silme yapilmadi." -ForegroundColor Yellow
} else {
    Write-Host "Tamamlandi." -ForegroundColor Cyan
}
