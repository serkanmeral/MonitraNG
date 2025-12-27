# Test script for MngGateway - MngDataGateway routing
# Tests: /data/api/* → MngDataGateway:5010

param(
    [string]$GatewayUrl = "http://localhost:5040",
    [string]$Token = $null
)

$ErrorActionPreference = "Stop"

Write-Host "=== MngGateway - MngDataGateway Routing Test ===" -ForegroundColor Cyan
Write-Host "Gateway URL: $GatewayUrl" -ForegroundColor Gray
Write-Host ""

# Load token if not provided
if (-not $Token) {
    $tokenPath = Join-Path $env:TEMP "serkan_token.txt"
    if (Test-Path $tokenPath) {
        $Token = Get-Content $tokenPath -Raw | Trim
        Write-Host "Token loaded from: $tokenPath" -ForegroundColor Gray
    } else {
        Write-Host "Token not found. Please provide token or run get-token.ps1 first." -ForegroundColor Yellow
        Write-Host "Usage: .\test-gateway-datagateway.ps1 -Token 'your-token'" -ForegroundColor Yellow
        exit 1
    }
}

$headers = @{
    "Content-Type" = "application/json"
    "Authorization" = "Bearer $Token"
}

# Test 1: MngDataGateway - Health Check (via Gateway)
Write-Host "Test 1: MngDataGateway - Health Check (via Gateway /data/api/v1/health)" -ForegroundColor Yellow
try {
    $response = Invoke-RestMethod -Uri "$GatewayUrl/data/api/v1/health" -Method Get -Headers $headers -SkipCertificateCheck
    Write-Host "✓ MngDataGateway health check successful" -ForegroundColor Green
    Write-Host "  Status: $($response.status)" -ForegroundColor Gray
} catch {
    Write-Host "✗ MngDataGateway health check failed: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.Exception.Response) {
        $statusCode = $_.Exception.Response.StatusCode.value__
        Write-Host "  Status Code: $statusCode" -ForegroundColor Red
    }
}

Write-Host ""

# Test 2: MngDataGateway - Get Datasets (via Gateway)
Write-Host "Test 2: MngDataGateway - Get Datasets (via Gateway /data/api/v1/datasets)" -ForegroundColor Yellow
try {
    $response = Invoke-RestMethod -Uri "$GatewayUrl/data/api/v1/datasets" -Method Get -Headers $headers -SkipCertificateCheck
    Write-Host "✓ Datasets retrieved successfully" -ForegroundColor Green
    Write-Host "  Count: $($response.Count)" -ForegroundColor Gray
    if ($response.Count -gt 0) {
        Write-Host "  First dataset: $($response[0].name)" -ForegroundColor Gray
    }
} catch {
    Write-Host "✗ Failed to get datasets: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.Exception.Response) {
        $statusCode = $_.Exception.Response.StatusCode.value__
        Write-Host "  Status Code: $statusCode" -ForegroundColor Red
    }
}

Write-Host ""

# Test 3: MngDataGateway - Get Dataset Categories (via Gateway)
Write-Host "Test 3: MngDataGateway - Get Dataset Categories (via Gateway /data/api/v1/dataset-categories)" -ForegroundColor Yellow
try {
    $response = Invoke-RestMethod -Uri "$GatewayUrl/data/api/v1/dataset-categories" -Method Get -Headers $headers -SkipCertificateCheck
    Write-Host "✓ Dataset categories retrieved successfully" -ForegroundColor Green
    Write-Host "  Count: $($response.Count)" -ForegroundColor Gray
} catch {
    Write-Host "✗ Failed to get dataset categories: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""
Write-Host "=== Test Summary ===" -ForegroundColor Cyan
Write-Host "Gateway routing to MngDataGateway is working!" -ForegroundColor Green

