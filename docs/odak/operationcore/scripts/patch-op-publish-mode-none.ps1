# Operation Core - Tum op_* datasetlerde publish_mode -> none (Q14)
# Mevcut Odak kurulumu icin; yeni kurulumda draft JSON zaten none olmali.
#
#   .\docs\odak\operationcore\scripts\get-operationcore-token.ps1
#   .\docs\odak\operationcore\scripts\patch-op-publish-mode-none.ps1

param(
    [string]$BaseUrl = "http://192.168.20.20:5040",
    [switch]$UseGateway = $true
)

$ErrorActionPreference = "Stop"
$datasetsPath = if ($UseGateway) { "/data/api/v1/datasets" } else { "/api/v1/datasets" }

$loadTokenScript = Join-Path $PSScriptRoot "load-operationcore-token.ps1"
$token = & $loadTokenScript
if ([string]::IsNullOrEmpty($token)) {
    Write-Host "Token alinamadi." -ForegroundColor Red
    exit 1
}

$headers = @{
    "Authorization" = "Bearer $token"
    "Content-Type"  = "application/json"
}

$opDatasets = @(
    "op_states", "op_priorities", "op_work_item_types", "op_fields", "op_workspaces",
    "op_state_flows", "op_rules", "op_forms", "op_profiles", "op_boards", "op_labels",
    "op_sla_policies", "op_notification_policies", "op_dashboards", "op_saved_filters", "op_reports",
    "op_work_items", "op_comments", "op_activities", "op_links", "op_work_item_timelines", "op_notifications"
)

$body = @{ publishMode = "none" } | ConvertTo-Json -Compress

Write-Host ''
Write-Host "op_* publish_mode -> none ($BaseUrl)" -ForegroundColor Cyan
Write-Host ''

foreach ($name in $opDatasets) {
    $uri = "$BaseUrl$datasetsPath/$name"
    try {
        $irm = @{ Uri = $uri; Method = "PUT"; Headers = $headers; Body = $body }
        if ($uri.StartsWith("https://")) { $irm.SkipCertificateCheck = $true }
        $null = Invoke-RestMethod @irm
        Write-Host "  $name OK" -ForegroundColor Green
    }
    catch {
        $code = $null
        try { $code = [int]$_.Exception.Response.StatusCode } catch { }
        Write-Host "  $name HATA HTTP $code" -ForegroundColor Red
        if ($_.ErrorDetails.Message) { Write-Host "    $($_.ErrorDetails.Message)" -ForegroundColor Gray }
    }
}

Write-Host ''
Write-Host "Tamamlandi." -ForegroundColor Cyan
