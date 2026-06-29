# Smoke: template source/structure returns placeholders[] (D1-PLACEHOLDER)
param(
    [string]$Gateway = "http://192.168.20.8:5040",
    [string]$TokenFile = "$env:TEMP\operationcore_dg_token_prod.txt",
    [string]$TemplateId = ""
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

if ([string]::IsNullOrWhiteSpace($TemplateId)) {
    $list = Invoke-RestMethod -Uri "$Gateway/documents/api/v1/templates" -Headers $headers -TimeoutSec 30
    if (-not $list.items -or $list.items.Count -eq 0) {
        Write-Host "SKIP: Hic sablon yok; once from-reference ile sablon olusturun." -ForegroundColor Yellow
        exit 0
    }
    $TemplateId = $list.items[0].id
    Write-Host "Ilk sablon kullaniliyor: $TemplateId ($($list.items[0].name))" -ForegroundColor Gray
}

$uri = "$Gateway/documents/api/v1/templates/$([uri]::EscapeDataString($TemplateId))/source/structure"
$structure = Invoke-RestMethod -Uri $uri -Headers $headers -TimeoutSec 60

if ($null -eq $structure.placeholders) {
    Write-Host "FAIL: placeholders alani yok (backend guncel mi?)" -ForegroundColor Red
    exit 1
}

Write-Host "OK $uri" -ForegroundColor Green
Write-Host "  placeholders: $($structure.placeholders.Count)" -ForegroundColor Cyan
Write-Host "  warnings: $($structure.placeholderWarnings.Count)" -ForegroundColor Cyan
if ($structure.placeholders.Count -gt 0) {
    $structure.placeholders | Select-Object -First 5 key, token, occurrenceCount | Format-Table
}
Write-Host "D1-PLACEHOLDER smoke tamam." -ForegroundColor Cyan
