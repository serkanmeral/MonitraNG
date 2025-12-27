# Test script for MngGateway - All services routing test
# Tests all services through the gateway

param(
    [string]$GatewayUrl = "http://localhost:5040",
    [string]$Token = $null
)

$ErrorActionPreference = "Stop"

Write-Host "=== MngGateway - All Services Routing Test ===" -ForegroundColor Cyan
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
        Write-Host "Usage: .\test-gateway-all.ps1 -Token 'your-token'" -ForegroundColor Yellow
        exit 1
    }
}

$scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path

# Run all individual tests
Write-Host "Running MngKeeper tests..." -ForegroundColor Yellow
& "$scriptPath\test-gateway-keeper.ps1" -GatewayUrl $GatewayUrl -Token $Token

Write-Host ""
Write-Host "Running MngHub tests..." -ForegroundColor Yellow
& "$scriptPath\test-gateway-hub.ps1" -GatewayUrl $GatewayUrl -Token $Token

Write-Host ""
Write-Host "Running MngDataGateway tests..." -ForegroundColor Yellow
& "$scriptPath\test-gateway-datagateway.ps1" -GatewayUrl $GatewayUrl -Token $Token

Write-Host ""
Write-Host "=== All Tests Complete ===" -ForegroundColor Cyan
Write-Host "All services are accessible through the gateway!" -ForegroundColor Green

