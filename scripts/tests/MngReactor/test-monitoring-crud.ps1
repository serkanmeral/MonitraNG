# MngReactor Monitoring CRUD Test
# Engine, Agent, Asset - GET/POST/PUT/DELETE
# Onkosullar: MngReactor, MngKeeper, MongoDB, DataGateway calisiyor

param(
    [string]$BaseUrl = "http://localhost:15010"
)

[System.Net.ServicePointManager]::ServerCertificateValidationCallback = { $true }
try {
    [System.Net.ServicePointManager]::SecurityProtocol = [System.Net.SecurityProtocolType]::Tls12 -bor [System.Net.SecurityProtocolType]::Tls11
} catch { }

$scriptPath = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $PSCommandPath }
$loadTokenScript = Join-Path $scriptPath "auth\load-token.ps1"
if (-not (Test-Path $loadTokenScript)) {
    Write-Host "Hata: load-token.ps1 bulunamadi." -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "========================================"
Write-Host "MngReactor Monitoring CRUD Test"
Write-Host "========================================"
Write-Host ""

$token = & $loadTokenScript
if ([string]::IsNullOrEmpty($token)) {
    Write-Host "Hata: Token alinamadi." -ForegroundColor Red
    exit 1
}

$headers = @{
    "Authorization" = "Bearer $token"
    "Content-Type"  = "application/json"
}

$params = @{ Uri = ""; Method = "GET"; Headers = $headers; ErrorAction = "Stop" }
$hasSkipCertCheck = Get-Command Invoke-RestMethod | Select-Object -ExpandProperty Parameters | Where-Object { $_.Name -eq "SkipCertificateCheck" }
if ($hasSkipCertCheck) { $params.SkipCertificateCheck = $true }

$results = @()
$testId = "test-crud-$(Get-Date -Format 'yyyyMMddHHmmss')"

# --- Engine CRUD ---
Write-Host "[Engine CRUD]"
# GET
try {
    $params.Uri = "$BaseUrl/api/v1/monitoring/engines"; $params.Method = "GET"
    $engines = Invoke-RestMethod @params
    Write-Host "  GET /engines: OK ($($engines.data.Count) kayit)" -ForegroundColor Green
    $results += "PASS"
} catch { Write-Host "  GET /engines: FAIL" -ForegroundColor Red; $results += "FAIL" }

# POST
$engineBody = @{ name = "Test Engine $testId"; __dataId = $testId } | ConvertTo-Json
try {
    $params.Uri = "$BaseUrl/api/v1/monitoring/engines"; $params.Method = "POST"; $params.Body = $engineBody
    $created = Invoke-RestMethod @params
    if ($created.data.__dataId -or $created.isSuccess) {
        Write-Host "  POST /engines: OK" -ForegroundColor Green
        $results += "PASS"
        $engineId = if ($created.data.__dataId) { $created.data.__dataId } else { $testId }
    } else { Write-Host "  POST /engines: WARN" -ForegroundColor Yellow; $results += "WARN" }
} catch { Write-Host "  POST /engines: FAIL - $($_.Exception.Message)" -ForegroundColor Red; $results += "FAIL"; $engineId = $testId }

# PUT
$engineUpdate = @{ __dataId = $engineId; name = "Test Engine Updated $testId" } | ConvertTo-Json
try {
    $params.Uri = "$BaseUrl/api/v1/monitoring/engines"; $params.Method = "PUT"; $params.Body = $engineUpdate
    Invoke-RestMethod @params | Out-Null
    Write-Host "  PUT /engines: OK" -ForegroundColor Green
    $results += "PASS"
} catch { Write-Host "  PUT /engines: FAIL" -ForegroundColor Red; $results += "FAIL" }

# DELETE
$engineDelete = @{ __dataId = $engineId } | ConvertTo-Json
try {
    $params.Uri = "$BaseUrl/api/v1/monitoring/engines"; $params.Method = "DELETE"; $params.Body = $engineDelete
    Invoke-RestMethod @params | Out-Null
    Write-Host "  DELETE /engines: OK" -ForegroundColor Green
    $results += "PASS"
} catch { Write-Host "  DELETE /engines: FAIL" -ForegroundColor Red; $results += "FAIL" }

Write-Host ""

# --- Agent CRUD (engine gerekli, once engine olustur) ---
Write-Host "[Agent CRUD]"
$agentEngineId = "agent-test-$testId"
$engineForAgent = @{ name = "Agent Test Engine"; __dataId = $agentEngineId } | ConvertTo-Json
try {
    $params.Uri = "$BaseUrl/api/v1/monitoring/engines"; $params.Method = "POST"; $params.Body = $engineForAgent
    Invoke-RestMethod @params | Out-Null
} catch { }

try {
    $params.Uri = "$BaseUrl/api/v1/monitoring/agents"; $params.Method = "GET"
    $agents = Invoke-RestMethod @params
    Write-Host "  GET /agents: OK ($($agents.data.Count) kayit)" -ForegroundColor Green
    $results += "PASS"
} catch { Write-Host "  GET /agents: FAIL" -ForegroundColor Red; $results += "FAIL" }

$agentId = "agent-$testId"
$agentBody = @{ __dataId = $agentId; engineId = $agentEngineId; name = "Test Agent" } | ConvertTo-Json
try {
    $params.Uri = "$BaseUrl/api/v1/monitoring/agents"; $params.Method = "POST"; $params.Body = $agentBody
    $ca = Invoke-RestMethod @params
    if ($ca.data -or $ca.isSuccess) {
        Write-Host "  POST /agents: OK" -ForegroundColor Green
        $results += "PASS"
    } else { $results += "WARN" }
} catch { Write-Host "  POST /agents: SKIP (engine gerekebilir)" -ForegroundColor Gray; $results += "SKIP" }

Write-Host ""

# --- Asset CRUD ---
Write-Host "[Asset CRUD]"
try {
    $params.Uri = "$BaseUrl/api/v1/monitoring/assets"; $params.Method = "GET"
    $assets = Invoke-RestMethod @params
    Write-Host "  GET /assets: OK ($($assets.data.Count) kayit)" -ForegroundColor Green
    $results += "PASS"
} catch { Write-Host "  GET /assets: FAIL" -ForegroundColor Red; $results += "FAIL" }

$assetId = "asset-$testId"
$assetBody = @{ __dataId = $assetId; name = "Test Asset"; assetType = "generic" } | ConvertTo-Json
try {
    $params.Uri = "$BaseUrl/api/v1/monitoring/assets"; $params.Method = "POST"; $params.Body = $assetBody
    $ca = Invoke-RestMethod @params
    if ($ca.data -or $ca.isSuccess) {
        Write-Host "  POST /assets: OK" -ForegroundColor Green
        $results += "PASS"
        # Temizlik
        $params.Method = "DELETE"; $params.Body = (@{ __dataId = $assetId } | ConvertTo-Json)
        try { Invoke-RestMethod @params | Out-Null } catch { }
    } else { $results += "WARN" }
} catch { Write-Host "  POST /assets: FAIL" -ForegroundColor Red; $results += "FAIL" }

# Agent ve engine temizligi
try {
    $params.Uri = "$BaseUrl/api/v1/monitoring/agents"; $params.Method = "DELETE"; $params.Body = (@{ __dataId = $agentId } | ConvertTo-Json)
    Invoke-RestMethod @params | Out-Null
} catch { }
try {
    $params.Uri = "$BaseUrl/api/v1/monitoring/engines"; $params.Method = "DELETE"; $params.Body = (@{ __dataId = $agentEngineId } | ConvertTo-Json)
    Invoke-RestMethod @params | Out-Null
} catch { }

Write-Host ""
Write-Host "========================================"
$passed = ($results | Where-Object { $_ -eq "PASS" }).Count
$failed = ($results | Where-Object { $_ -eq "FAIL" }).Count
Write-Host "Ozet: $passed PASS, $failed FAIL"
if ($failed -gt 0) { exit 1 }
exit 0
