# @mail_templates DG listesi + (opsiyonel) Notifier preview-template
param(
    [string]$Gateway = "http://192.168.20.20:5040",
    [string]$TemplateKey = "work-item-transitioned",
    [switch]$SkipPreview
)

$ErrorActionPreference = "Stop"
$tokenFile = "$env:TEMP\operationcore_dg_token.txt"
if (-not (Test-Path $tokenFile)) {
    throw "Token yok. Once: .\docs\odak\operationcore\scripts\get-operationcore-token.ps1"
}
$token = (Get-Content $tokenFile -Raw).Trim()
$headers = @{ Authorization = "Bearer $token"; "Content-Type" = "application/json" }

Write-Host "=== Mail templates API smoke ===" -ForegroundColor Cyan

$ds = [Uri]::EscapeDataString("@mail_templates")
$uri = "$Gateway/data/api/v1/data/$ds`?limit=20&filter=isActive:eq:true"
$rows = Invoke-RestMethod -Method GET -Uri $uri -Headers $headers
$items = @()
if ($rows -is [System.Array]) { $items = $rows }
elseif ($rows.data) { $items = @($rows.data) }
elseif ($rows) { $items = @($rows) }
Write-Host "[1] Active templates: $($items.Count)" -ForegroundColor Green
$items | ForEach-Object { Write-Host "    - $($_.templateKey) ($($_.name))" -ForegroundColor Gray }

if ($SkipPreview) { exit 0 }

$sample = $items | Where-Object { $_.templateKey -eq $TemplateKey } | Select-Object -First 1
if (-not $sample) {
    Write-Host "[2] UYARI: $TemplateKey bulunamadi, preview atlandi" -ForegroundColor Yellow
    exit 0
}
$ctx = $sample.sampleContext
if (-not $ctx) { $ctx = @{ workItem = @{ key = "SMOKE-001"; title = "Test" } } }

$previewUri = "$Gateway/notifier/api/v1/notifications/preview-template"
try {
    $prev = Invoke-RestMethod -Method POST -Uri $previewUri -Headers $headers -Body (@{
        templateKey = $TemplateKey
        context     = $ctx
    } | ConvertTo-Json -Depth 20 -Compress)
    $subj = $prev.subject
    $len = if ($prev.htmlBody) { $prev.htmlBody.Length } else { 0 }
    Write-Host "[2] Preview OK subject=$subj htmlLen=$len" -ForegroundColor Green
}
catch {
    Write-Host "[2] Preview gateway yolu basarisiz (UI nginx deploy sonrasi /api/notifier da dene): $($_.Exception.Message)" -ForegroundColor Yellow
}

Write-Host "Smoke tamamlandi." -ForegroundColor Cyan
