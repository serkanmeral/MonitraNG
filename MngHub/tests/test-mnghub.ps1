# MngHub Test Script
# Tests basic endpoints and SignalR connection

# Try both HTTP and HTTPS ports
$baseUrl = "https://localhost:5020"
$httpUrl = "http://localhost:5020"
$httpAltUrl = "http://localhost:5234"  # launchSettings.json default

Write-Host "=== MngHub Test Script ===" -ForegroundColor Cyan
Write-Host ""

# Test 1: Health Check
Write-Host "1. Testing Health Check..." -ForegroundColor Yellow
$healthChecked = $false
foreach ($url in @($httpUrl, $httpAltUrl)) {
    try {
        $response = Invoke-RestMethod -Uri "$url/health" -Method Get -SkipCertificateCheck -ErrorAction Stop
        Write-Host "   ✓ Health Check: OK (on $url)" -ForegroundColor Green
        Write-Host "   Status: $($response.status)" -ForegroundColor Gray
        Write-Host "   Service: $($response.service)" -ForegroundColor Gray
        $healthChecked = $true
        $httpUrl = $url  # Update working URL
        break
    } catch {
        continue
    }
}
if (-not $healthChecked) {
    Write-Host "   ✗ Health Check: FAILED (tried both ports)" -ForegroundColor Red
    Write-Host "   Note: Make sure MngHub is running (dotnet run in MngHub.Api)" -ForegroundColor Yellow
}

Write-Host ""

# Test 2: Status Endpoint
Write-Host "2. Testing Status Endpoint..." -ForegroundColor Yellow
try {
    $response = Invoke-RestMethod -Uri "$httpUrl/api/test/status" -Method Get -SkipCertificateCheck
    Write-Host "   ✓ Status Endpoint: OK" -ForegroundColor Green
    Write-Host "   Service: $($response.service)" -ForegroundColor Gray
    Write-Host "   Status: $($response.status)" -ForegroundColor Gray
    Write-Host "   Endpoints:" -ForegroundColor Gray
    Write-Host "     - SignalR: $($response.endpoints.signalR)" -ForegroundColor Gray
    Write-Host "     - Health: $($response.endpoints.health)" -ForegroundColor Gray
    Write-Host "     - Connections: $($response.endpoints.connections)" -ForegroundColor Gray
} catch {
    Write-Host "   ✗ Status Endpoint: FAILED" -ForegroundColor Red
    Write-Host "   Error: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""

# Test 3: Get All Connections (should be empty initially)
Write-Host "3. Testing Get All Connections..." -ForegroundColor Yellow
try {
    $response = Invoke-RestMethod -Uri "$httpUrl/api/test/connections" -Method Get -SkipCertificateCheck
    Write-Host "   ✓ Get All Connections: OK" -ForegroundColor Green
    Write-Host "   Active Connections: $($response.Count)" -ForegroundColor Gray
} catch {
    Write-Host "   ✗ Get All Connections: FAILED" -ForegroundColor Red
    Write-Host "   Error: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""

# Test 4: OpenAPI
Write-Host "4. Testing OpenAPI..." -ForegroundColor Yellow
try {
    $response = Invoke-RestMethod -Uri "$httpUrl/openapi/v1.json" -Method Get -SkipCertificateCheck
    Write-Host "   ✓ OpenAPI: OK" -ForegroundColor Green
    Write-Host "   URL: $httpUrl/openapi/v1.json" -ForegroundColor Gray
    Write-Host "   Title: $($response.info.title)" -ForegroundColor Gray
} catch {
    Write-Host "   ✗ OpenAPI: FAILED" -ForegroundColor Red
    Write-Host "   Error: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""
Write-Host "=== Test Summary ===" -ForegroundColor Cyan
Write-Host ""
Write-Host "Note: SignalR connection test requires a valid JWT token." -ForegroundColor Yellow
Write-Host "To test SignalR:" -ForegroundColor Yellow
Write-Host "  1. Get a JWT token from MngKeeper (login endpoint)" -ForegroundColor Gray
Write-Host "  2. Connect to: $baseUrl/ws?access_token=<token>" -ForegroundColor Gray
Write-Host "  3. Use SignalR client library (JavaScript/C#)" -ForegroundColor Gray
Write-Host ""

