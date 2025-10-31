# MngKeeper API Test Script
Write-Host "=== MngKeeper API Test Script ===" -ForegroundColor Green

$baseUrl = "http://localhost:5001"
$endpoints = @(
    "/health",
    "/health/ready", 
    "/health/live",
    "/graphql",
    "/api/domains",
    "/api/users",
    "/api/groups"
)

Write-Host "`nTesting endpoints on $baseUrl" -ForegroundColor Yellow

foreach ($endpoint in $endpoints) {
    $url = "$baseUrl$endpoint"
    Write-Host "`nTesting: $url" -ForegroundColor Cyan
    
    try {
        $response = Invoke-RestMethod -Uri $url -Method Get -TimeoutSec 5
        Write-Host "✅ SUCCESS" -ForegroundColor Green
        if ($response -and $response.GetType().Name -ne "String") {
            Write-Host "Response: $($response | ConvertTo-Json -Depth 2)" -ForegroundColor Gray
        } else {
            Write-Host "Response: $response" -ForegroundColor Gray
        }
    }
    catch {
        Write-Host "❌ FAILED: $($_.Exception.Message)" -ForegroundColor Red
    }
}

Write-Host "`n=== Testing POST endpoints ===" -ForegroundColor Yellow

# Test POST endpoints
$testEndpoints = @(
    @{Url="/api/domains"; Method="POST"; Body='{"name":"test-domain","displayName":"Test Domain","description":"Test domain for API testing"}'}
)

foreach ($test in $testEndpoints) {
    $url = "$baseUrl$($test.Url)"
    Write-Host "`nTesting: $($test.Method) $url" -ForegroundColor Cyan
    
    try {
        $headers = @{
            "Content-Type" = "application/json"
        }
        
        $response = Invoke-RestMethod -Uri $url -Method $test.Method -Headers $headers -Body $test.Body -TimeoutSec 10
        Write-Host "✅ SUCCESS" -ForegroundColor Green
        Write-Host "Response: $($response | ConvertTo-Json -Depth 2)" -ForegroundColor Gray
    }
    catch {
        Write-Host "❌ FAILED: $($_.Exception.Message)" -ForegroundColor Red
    }
}

Write-Host "`n=== Test Complete ===" -ForegroundColor Green
