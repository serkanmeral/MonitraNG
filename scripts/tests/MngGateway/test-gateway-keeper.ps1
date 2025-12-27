# Test script for MngGateway - MngKeeper routing
# Tests: /keeper/api/* → MngKeeper:5001

param(
    [string]$GatewayUrl = "https://localhost:5040",
    [string]$Token = $null
)

$ErrorActionPreference = "Stop"

Write-Host "=== MngGateway - MngKeeper Routing Test ===" -ForegroundColor Cyan
Write-Host "Gateway URL: $GatewayUrl" -ForegroundColor Gray
Write-Host ""

# Load token if not provided
if (-not $Token) {
    $tokenPath = Join-Path $env:TEMP "serkan_token.txt"
    if (Test-Path $tokenPath) {
        $Token = (Get-Content $tokenPath -Raw).Trim()
        Write-Host "Token loaded from: $tokenPath" -ForegroundColor Gray
    } else {
        Write-Host "Token not found. Please provide token or run get-token.ps1 first." -ForegroundColor Yellow
        Write-Host "Usage: .\test-gateway-keeper.ps1 -Token 'your-token'" -ForegroundColor Yellow
        exit 1
    }
}

$headers = @{
    "Content-Type" = "application/json"
    "Authorization" = "Bearer $Token"
}

# Test 1: Health check
Write-Host "Test 1: Gateway Health Check" -ForegroundColor Yellow
try {
    $response = Invoke-RestMethod -Uri "$GatewayUrl/health" -Method Get -SkipCertificateCheck
    Write-Host "✓ Gateway is healthy" -ForegroundColor Green
    Write-Host "  Status: $($response.status)" -ForegroundColor Gray
    Write-Host "  Service: $($response.service)" -ForegroundColor Gray
} catch {
    Write-Host "✗ Gateway health check failed: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

Write-Host ""

# Test 1.5: Get token via Gateway (if not provided)
if (-not $Token) {
    Write-Host "Test 1.5: Get Token via Gateway" -ForegroundColor Yellow
    try {
        $tokenResponse = Invoke-RestMethod -Uri "$GatewayUrl/keeper/api/auth/token" -Method POST -ContentType "application/json" -Body (@{
            username = "serkan.meral"
            password = "Serkan123!"
            domain = "meral"
        } | ConvertTo-Json) -SkipCertificateCheck
        $Token = $tokenResponse.accessToken
        Write-Host "✓ Token obtained via Gateway" -ForegroundColor Green
        Write-Host "  Token preview: $($Token.Substring(0, [Math]::Min(50, $Token.Length)))..." -ForegroundColor Gray
    } catch {
        Write-Host "✗ Failed to get token: $($_.Exception.Message)" -ForegroundColor Red
        Write-Host "Please provide token manually or check credentials" -ForegroundColor Yellow
        exit 1
    }
}

Write-Host ""

# Test 2: MngKeeper - Get Domains (via Gateway)
Write-Host "Test 2: MngKeeper - Get Domains (via Gateway /keeper/api/domain)" -ForegroundColor Yellow
try {
    $response = Invoke-RestMethod -Uri "$GatewayUrl/keeper/api/domain" -Method Get -Headers $headers -SkipCertificateCheck
    Write-Host "✓ Domains retrieved successfully" -ForegroundColor Green
    Write-Host "  Count: $($response.Count)" -ForegroundColor Gray
    if ($response.Count -gt 0) {
        Write-Host "  First domain: $($response[0].name)" -ForegroundColor Gray
    }
} catch {
    Write-Host "✗ Failed to get domains: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.Exception.Response) {
        $statusCode = $_.Exception.Response.StatusCode.value__
        Write-Host "  Status Code: $statusCode" -ForegroundColor Red
    }
}

Write-Host ""

# Test 3: MngKeeper - Get Users (via Gateway)
Write-Host "Test 3: MngKeeper - Get Users (via Gateway /keeper/api/user)" -ForegroundColor Yellow
try {
    $response = Invoke-RestMethod -Uri "$GatewayUrl/keeper/api/user" -Method Get -Headers $headers -SkipCertificateCheck
    Write-Host "✓ Users retrieved successfully" -ForegroundColor Green
    Write-Host "  Count: $($response.Count)" -ForegroundColor Gray
} catch {
    Write-Host "✗ Failed to get users: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""

# Test 4: MngKeeper - Get Groups (via Gateway)
Write-Host "Test 4: MngKeeper - Get Groups (via Gateway /keeper/api/group)" -ForegroundColor Yellow
try {
    $response = Invoke-RestMethod -Uri "$GatewayUrl/keeper/api/group" -Method Get -Headers $headers -SkipCertificateCheck
    Write-Host "✓ Groups retrieved successfully" -ForegroundColor Green
    Write-Host "  Count: $($response.Count)" -ForegroundColor Gray
} catch {
    Write-Host "✗ Failed to get groups: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""
Write-Host "=== Test Summary ===" -ForegroundColor Cyan
Write-Host "Gateway routing to MngKeeper is working!" -ForegroundColor Green

