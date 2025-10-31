# CreateDomain Test Script
Write-Host "=== CreateDomain Test Script ===" -ForegroundColor Green

$baseUrl = "http://localhost:5001"

function Test-CreateDomain {
    param(
        [string]$Endpoint,
        [string]$JsonFile,
        [string]$Description
    )

    Write-Host "`nTesting: $Description" -ForegroundColor Cyan
    Write-Host "Endpoint: $Endpoint" -ForegroundColor Gray
    Write-Host "JSON File: $JsonFile" -ForegroundColor Gray

    try {
        $jsonContent = Get-Content -Path $JsonFile -Raw
        $headers = @{
            "Content-Type" = "application/json"
        }

        $response = Invoke-RestMethod -Uri "$baseUrl$Endpoint" -Method POST -Headers $headers -Body $jsonContent -TimeoutSec 10

        Write-Host "✅ SUCCESS" -ForegroundColor Green
        Write-Host "Response: $($response | ConvertTo-Json -Depth 2)" -ForegroundColor Gray
        return $response
    }
    catch {
        Write-Host "❌ FAILED: $($_.Exception.Message)" -ForegroundColor Red
        return $null
    }
}

# Test 1: Basic Domain Creation
Write-Host "`n=== Test 1: Basic Domain Creation ===" -ForegroundColor Yellow
$basicResult = Test-CreateDomain -Endpoint "/api/domain" -JsonFile "test-domain-basic.json" -Description "Basic Domain Creation"

# Test 2: Production Domain Creation
Write-Host "`n=== Test 2: Production Domain Creation ===" -ForegroundColor Yellow
$productionResult = Test-CreateDomain -Endpoint "/api/domain" -JsonFile "test-domain-production.json" -Description "Production Domain Creation"

# Test 3: Minimal Domain Creation
Write-Host "`n=== Test 3: Minimal Domain Creation ===" -ForegroundColor Yellow
$minimalResult = Test-CreateDomain -Endpoint "/api/domain" -JsonFile "test-domain-minimal.json" -Description "Minimal Domain Creation"

# Test 4: Duplicate Domain (Expected Error)
Write-Host "`n=== Test 4: Duplicate Domain (Expected Error) ===" -ForegroundColor Yellow
Test-CreateDomain -Endpoint "/api/domain" -JsonFile "test-domain-basic.json" -Description "Duplicate Domain Creation"

# Test 5: Get All Domains
Write-Host "`n=== Test 5: Get All Domains ===" -ForegroundColor Yellow
try {
    $allDomains = Invoke-RestMethod -Uri "$baseUrl/api/domain" -Method GET -TimeoutSec 10
    Write-Host "✅ SUCCESS" -ForegroundColor Green
    Write-Host "Total Domains: $($allDomains.Count)" -ForegroundColor Gray
    Write-Host "Domains: $($allDomains | ConvertTo-Json -Depth 2)" -ForegroundColor Gray
}
catch {
    Write-Host "❌ FAILED: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host "`n=== CreateDomain Test Complete ===" -ForegroundColor Green
