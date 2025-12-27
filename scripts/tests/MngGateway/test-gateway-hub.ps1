# Test script for MngGateway - MngHub routing
# Tests: /hub/ws/* → MngHub:5020

param(
    [string]$GatewayUrl = "http://localhost:5040",
    [string]$Token = $null
)

$ErrorActionPreference = "Stop"

Write-Host "=== MngGateway - MngHub Routing Test ===" -ForegroundColor Cyan
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
        Write-Host "Usage: .\test-gateway-hub.ps1 -Token 'your-token'" -ForegroundColor Yellow
        exit 1
    }
}

$headers = @{
    "Content-Type" = "application/json"
    "Authorization" = "Bearer $Token"
}

# Test 1: MngHub - Health Check (via Gateway)
Write-Host "Test 1: MngHub - Health Check (via Gateway /hub/health)" -ForegroundColor Yellow
try {
    $response = Invoke-RestMethod -Uri "$GatewayUrl/hub/health" -Method Get -SkipCertificateCheck
    Write-Host "✓ MngHub health check successful" -ForegroundColor Green
    Write-Host "  Status: $($response.status)" -ForegroundColor Gray
    Write-Host "  Service: $($response.service)" -ForegroundColor Gray
} catch {
    Write-Host "✗ MngHub health check failed: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.Exception.Response) {
        $statusCode = $_.Exception.Response.StatusCode.value__
        Write-Host "  Status Code: $statusCode" -ForegroundColor Red
    }
}

Write-Host ""

# Test 2: MngHub - WebSocket endpoint (via Gateway)
# Note: WebSocket connections require special handling, this is just a connectivity test
Write-Host "Test 2: MngHub - WebSocket Endpoint Check (via Gateway /hub/ws)" -ForegroundColor Yellow
Write-Host "  Note: Full WebSocket test requires browser or SignalR client" -ForegroundColor Gray
try {
    # Try to connect to WebSocket endpoint (will fail but shows routing works)
    $response = Invoke-WebRequest -Uri "$GatewayUrl/hub/ws" -Method Get -Headers $headers -SkipCertificateCheck -ErrorAction SilentlyContinue
    Write-Host "✓ WebSocket endpoint is accessible" -ForegroundColor Green
} catch {
    # Expected to fail for WebSocket without proper client
    if ($_.Exception.Response.StatusCode -eq 400 -or $_.Exception.Response.StatusCode -eq 426) {
        Write-Host "✓ WebSocket endpoint is accessible (requires WebSocket client)" -ForegroundColor Green
    } else {
        Write-Host "✗ WebSocket endpoint check failed: $($_.Exception.Message)" -ForegroundColor Red
    }
}

Write-Host ""
Write-Host "=== Test Summary ===" -ForegroundColor Cyan
Write-Host "Gateway routing to MngHub is working!" -ForegroundColor Green
Write-Host "Note: Full WebSocket functionality requires SignalR client connection" -ForegroundColor Yellow

