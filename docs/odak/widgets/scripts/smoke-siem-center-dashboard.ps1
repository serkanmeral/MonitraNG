# D3 smoke — @dashboards siem-center kaydi ve layout.meta
#
#   .\docs\odak\widgets\scripts\smoke-siem-center-dashboard.ps1

param(
    [string]$BaseUrl = "http://192.168.20.20:5040",
    [string]$Token = "",
    [string]$LoadTokenScript = ""
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../../../..")).Path

if ([string]::IsNullOrEmpty($Token)) { $Token = $env:WIDGET_TOKEN }
if ([string]::IsNullOrEmpty($Token)) {
    if ([string]::IsNullOrEmpty($LoadTokenScript)) {
        $LoadTokenScript = Join-Path $repoRoot "docs/odak/operationcore/scripts/load-operationcore-token.ps1"
    }
    if (Test-Path $LoadTokenScript) { $Token = & $LoadTokenScript }
}
if ([string]::IsNullOrEmpty($Token)) {
    Write-Host "Token bulunamadi." -ForegroundColor Red
    exit 1
}

$headers = @{ Authorization = "Bearer $Token" }

Write-Host ''
Write-Host "SIEM center dashboard smoke ($BaseUrl)" -ForegroundColor Cyan

$uri = "$BaseUrl/data/api/v1/data/@dashboards?filter=slug:eq:siem-center&limit=1"
$row = Invoke-RestMethod -Uri $uri -Headers $headers -Method GET -TimeoutSec 30
$item = $null
if ($row -is [array] -and $row.Count -gt 0) { $item = $row[0] }
elseif ($row.items -and $row.items.Count -gt 0) { $item = $row.items[0] }

if (-not $item) {
    Write-Host "  FAIL @dashboards slug=siem-center bulunamadi" -ForegroundColor Red
    Write-Host "  Calistirin: setup-siem-center-dashboard.ps1" -ForegroundColor Yellow
    exit 1
}

$meta = $item.layout.meta
if (-not $meta -or $meta.surfaceKind -ne 'siem-center') {
    Write-Host "  FAIL layout.meta.surfaceKind != siem-center" -ForegroundColor Red
    exit 1
}

$slots = $meta.templateSlots
$panel = $meta.siemPanel
Write-Host "  OK slug=siem-center surfaceKind=$($meta.surfaceKind)" -ForegroundColor Green
Write-Host "  templateSlots=$($slots.PSObject.Properties.Count) siemPanel.widgetOrder=$($panel.widgetOrder.Count)" -ForegroundColor Gray
Write-Host ''
Write-Host "D3 smoke tamam." -ForegroundColor Green
