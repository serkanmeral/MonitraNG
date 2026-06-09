# widgets-demo dashboard smoke — @dashboards layout + @widgets referanslari
#
#   .\docs\odak\widgets\scripts\smoke-widget-demo-dashboard.ps1

param(
    [string]$BaseUrl = "http://192.168.20.20:5040",
    [string]$Domain = "odak",
    [string]$Token = "",
    [string]$LoadTokenScript = ""
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../../../..")).Path
$seedFile = Join-Path $repoRoot "docs/odak/widgets/datasets/widget_demo_dashboard_seed.json"

if ([string]::IsNullOrEmpty($Token)) {
    $tokenFile = "$env:TEMP\operationcore_dg_token.txt"
    if (Test-Path $tokenFile) {
        $Token = (Get-Content $tokenFile -Raw).Trim()
    }
}
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

$headers = @{
    Authorization   = "Bearer $Token"
    "X-Domain-Name" = $Domain
}

$seed = Get-Content $seedFile -Raw -Encoding UTF8 | ConvertFrom-Json
$slug = [string]$seed.dashboard.slug

Write-Host ''
Write-Host "Widget demo dashboard smoke ($BaseUrl)" -ForegroundColor Cyan

$uri = "$BaseUrl/data/api/v1/data/@dashboards?filter=slug:eq:$slug&limit=1"
$row = Invoke-RestMethod -Uri $uri -Headers $headers -Method GET -TimeoutSec 30
$item = $null
if ($row -is [array] -and $row.Count -gt 0) { $item = $row[0] }
elseif ($row.items -and $row.items.Count -gt 0) { $item = $row.items[0] }

if (-not $item) {
    Write-Host "  FAIL slug=$slug bulunamadi — setup-widget-demo-dashboard.ps1" -ForegroundColor Red
    exit 1
}

$rows = $item.layout.rows
if (-not $rows -or $rows.Count -eq 0) {
    Write-Host "  FAIL layout.rows bos (UI: Layout tanimli degil)" -ForegroundColor Red
    exit 1
}

$expectedWidgets = @($seed.widgets | ForEach-Object { [string]$_.name })
$widgetIds = @()
foreach ($r in $rows) {
    foreach ($c in $r.cols) {
        if ($c.widgetId) { $widgetIds += [string]$c.widgetId }
    }
}

if ($widgetIds.Count -eq 0) {
    Write-Host "  FAIL layout cols widgetId yok" -ForegroundColor Red
    exit 1
}

$missing = @()
foreach ($wName in $expectedWidgets) {
    $filter = [uri]::EscapeDataString("name:eq:$wName")
    $wUri = "$BaseUrl/data/api/v1/data/@widgets?filter=$filter&limit=1"
    $wList = Invoke-RestMethod -Uri $wUri -Headers $headers -Method GET -TimeoutSec 30
    $w = $null
    if ($wList -is [array] -and $wList.Count -gt 0) { $w = $wList[0] }
    elseif ($wList.items -and $wList.items.Count -gt 0) { $w = $wList.items[0] }
    if (-not $w) {
        $missing += $wName
    }
}

if ($missing.Count -gt 0) {
    Write-Host "  FAIL eksik @widgets: $($missing -join ', ')" -ForegroundColor Red
    exit 1
}

Write-Host "  OK slug=$slug layoutRows=$($rows.Count) widgetRefs=$($widgetIds.Count)" -ForegroundColor Green
Write-Host "  UI: http://192.168.20.20:3000/dashboards/$slug" -ForegroundColor Gray
Write-Host ''
Write-Host "Demo dashboard smoke tamam." -ForegroundColor Green
