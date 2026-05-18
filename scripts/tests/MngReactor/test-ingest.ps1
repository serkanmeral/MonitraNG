# MngReactor Ingest Test
# POST /api/v1/ingest/metrics
# Onkosullar: MngReactor, MngKeeper, DataGateway calisiyor; seed-monitoring-test-data ile Engine, Agent, Asset olusturulmus

param(
    [string]$BaseUrl = "http://localhost:15010",
    [string]$EngineId = "",
    [string]$AgentId = "",
    [string]$AssetId = "",
    [string]$ItemId = ""
)

[System.Net.ServicePointManager]::ServerCertificateValidationCallback = { $true }
try {
    [System.Net.ServicePointManager]::SecurityProtocol = [System.Net.SecurityProtocolType]::Tls12 -bor [System.Net.SecurityProtocolType]::Tls11
} catch { }

$scriptPath = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $PSCommandPath }
$loadTokenScript = Join-Path $scriptPath "auth\load-token.ps1"
if (-not (Test-Path $loadTokenScript)) {
    Write-Host "Hata: load-token.ps1 bulunamadi: $loadTokenScript" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "========================================"
Write-Host "MngReactor Ingest Test"
Write-Host "========================================"
Write-Host ""

# Token al
Write-Host "[1] Token aliniyor..."
$token = & $loadTokenScript
if ([string]::IsNullOrEmpty($token)) {
    Write-Host "Hata: Token alinamadi." -ForegroundColor Red
    exit 1
}
Write-Host "  OK: Token alindi" -ForegroundColor Green
Write-Host ""

$headers = @{
    "Authorization" = "Bearer $token"
    "Content-Type"  = "application/json"
}
$params = @{ Uri = ""; Method = "POST"; Headers = $headers; Body = ""; ErrorAction = "Stop" }
$hasSkipCertCheck = Get-Command Invoke-RestMethod | Select-Object -ExpandProperty Parameters | Where-Object { $_.Name -eq "SkipCertificateCheck" }
if ($hasSkipCertCheck) { $params.SkipCertificateCheck = $true }

# Eksik ID'leri mon_engines/mon_agents/mon_assets'tan al
if ([string]::IsNullOrEmpty($EngineId) -or [string]::IsNullOrEmpty($AgentId) -or [string]::IsNullOrEmpty($AssetId)) {
    Write-Host "[2] Engine/Agent/Asset ID'leri aliniyor..."
    try {
        $params.Uri = "$BaseUrl/api/v1/monitoring/engines"; $params.Method = "GET"
        $engines = Invoke-RestMethod @params
        if ($engines.data -and $engines.data.Count -gt 0) {
            $EngineId = $engines.data[0].__dataId
            Write-Host "  engineId: $EngineId" -ForegroundColor Gray
        }
    } catch { Write-Host "  UYARI: Engine listesi alinamadi" -ForegroundColor Yellow }
    try {
        $params.Uri = "$BaseUrl/api/v1/monitoring/agents"; $params.Method = "GET"
        $agents = Invoke-RestMethod @params
        if ($agents.data -and $agents.data.Count -gt 0) {
            $AgentId = $agents.data[0].__dataId
            if (-not [string]::IsNullOrEmpty($agents.data[0].asset_configs) -and $agents.data[0].asset_configs.Count -gt 0) {
                $AssetId = $agents.data[0].asset_configs[0].assetId
            }
            Write-Host "  agentId: $AgentId, assetId: $AssetId" -ForegroundColor Gray
        }
    } catch { Write-Host "  UYARI: Agent listesi alinamadi" -ForegroundColor Yellow }
    try {
        if ([string]::IsNullOrEmpty($AssetId)) {
            $params.Uri = "$BaseUrl/api/v1/monitoring/assets"; $params.Method = "GET"
            $assets = Invoke-RestMethod @params
            if ($assets.data -and $assets.data.Count -gt 0) {
                $AssetId = $assets.data[0].__dataId
                $ItemId = $assets.data[0].itemId
                Write-Host "  assetId: $AssetId, itemId: $ItemId" -ForegroundColor Gray
            }
        }
    } catch { }
    Write-Host ""
}

if ([string]::IsNullOrEmpty($EngineId) -or [string]::IsNullOrEmpty($AgentId) -or [string]::IsNullOrEmpty($AssetId)) {
    Write-Host "Hata: engineId, agentId, assetId gerekli. seed-monitoring-test-data.ps1 calistirin." -ForegroundColor Red
    exit 1
}

$collectedAt = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
$body = @{
    batches = @(
        @{
            assetId    = $AssetId
            itemId     = $ItemId
            agentId    = $AgentId
            engineId   = $EngineId
            collectedAt = $collectedAt
            metrics    = @(
                @{ collectibleCode = "cpu_usage"; value = 45.2; unit = "%" },
                @{ collectibleCode = "memory_used"; value = 1048576; unit = "KB" }
            )
        }
    )
} | ConvertTo-Json -Depth 5

# POST ingest
Write-Host "[3] POST /api/v1/ingest/metrics ..."
try {
    $params.Uri = "$BaseUrl/api/v1/ingest/metrics"; $params.Method = "POST"; $params.Body = $body
    $response = Invoke-RestMethod @params
    if ($response.savedCount -gt 0) {
        Write-Host "  PASS: savedCount = $($response.savedCount)" -ForegroundColor Green
        if ($response.failedCount -gt 0) {
            Write-Host "  failedCount = $($response.failedCount)" -ForegroundColor Yellow
        }
    } else {
        Write-Host "  WARN: savedCount = 0" -ForegroundColor Yellow
        if ($response.errorList) {
            $response.errorList | ForEach-Object { Write-Host "    $($_.code): $($_.message)" -ForegroundColor Gray }
        }
    }
} catch {
    Write-Host "  FAIL: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.ErrorDetails.Message) { Write-Host "  $($_.ErrorDetails.Message)" -ForegroundColor Gray }
    exit 1
}
Write-Host ""
Write-Host "Ingest test tamamlandi."
exit 0
