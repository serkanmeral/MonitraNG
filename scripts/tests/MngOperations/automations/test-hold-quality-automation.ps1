# Reproduce hold_quality -> NCR workspace automation
param(
    [string]$BaseUrl = "http://192.168.20.20:5040",
    [string]$MoBaseUrl = "http://192.168.20.20:5040/operations",
    [string]$WorkspaceId = "9f9cc085-81c7-4a92-9fa2-357ad5c654cd",
    [string]$QualityStateId = "ce375adb-19ee-4100-9a2f-211e9d1679d2"
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../../../..")).Path
$token = & (Join-Path $repoRoot "docs/odak/operationcore/scripts/load-operationcore-token.ps1")
$headers = @{
    Authorization = "Bearer $token"
    "Content-Type" = "application/json"
}

$filter = [Uri]::EscapeDataString("workspaceId:eq:$WorkspaceId")
$uri = "$BaseUrl/data/api/v1/data/op_work_items?filter=$filter&limit=30"
$resp = Invoke-RestMethod -Uri $uri -Headers $headers -Method GET
$items = @($resp.items)
if (-not $items.Count -and $resp.data) { $items = @($resp.data) }

$wi = $items | Where-Object { $_.stateId -eq $QualityStateId } | Select-Object -First 1
if (-not $wi) {
    Write-Host "Kalite kontrol state'inde WI yok; SeedDemo calistiriliyor..." -ForegroundColor Yellow
    & (Join-Path $repoRoot "docs/odak/is_surecleri/scripts/seed-operation-core-odak-uretim.ps1") -SeedDemo -ReloadMetadataCache
    exit $LASTEXITCODE
}

Write-Host "hold_quality test: $($wi.key) ($($wi.__dataId))" -ForegroundColor Cyan
$body = @{
    fields = @{
        qualityResult = "uygunsuz"
        qualityNotes  = "Test uygunsuzluk — otomasyon"
    }
} | ConvertTo-Json -Depth 5

try {
    $result = Invoke-RestMethod `
        -Uri "$MoBaseUrl/api/v1/work-items/$($wi.__dataId)/transitions/hold_quality" `
        -Method POST -Body $body -Headers $headers
    Write-Host "OK" -ForegroundColor Green
    $result | ConvertTo-Json -Depth 4
}
catch {
    Write-Host "FAIL: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.ErrorDetails.Message) {
        Write-Host $_.ErrorDetails.Message
    }
    $parentFilter = [Uri]::EscapeDataString("parentItemId:eq:$($wi.__dataId)")
    $childUri = "$BaseUrl/data/api/v1/data/op_work_items?filter=$parentFilter&limit=5"
    $children = Invoke-RestMethod -Uri $childUri -Headers $headers -Method GET
    $childItems = @($children.items)
    if (-not $childItems.Count -and $children.data) { $childItems = @($children.data) }
    Write-Host "Child WIs (parent=$($wi.__dataId)): $($childItems.Count)" -ForegroundColor Yellow
    $childItems | ForEach-Object { Write-Host "  $($_.key) type=$($_.typeId)" }
    exit 1
}

$parentFilter = [Uri]::EscapeDataString("parentItemId:eq:$($wi.__dataId)")
$childUri = "$BaseUrl/data/api/v1/data/op_work_items?filter=$parentFilter&limit=5"
$children = Invoke-RestMethod -Uri $childUri -Headers $headers -Method GET
$childItems = @($children.items)
if (-not $childItems.Count -and $children.data) { $childItems = @($children.data) }
Write-Host "NCR children: $($childItems.Count)" -ForegroundColor Cyan
