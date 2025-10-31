# Domain CRUD Test Script
Write-Host "=== Domain CRUD Test Script ===" -ForegroundColor Green

$baseUrl = "http://localhost:5001/api/domaincrudtest"

# Test data
$domain1 = @{
    domainName = "test-domain-1"
    displayName = "Test Domain 1"
    adminEmail = "admin1@test-domain.com"
    adminPassword = "Admin123!"
    settings = @{
        maxUsers = 50
        maxAssets = 500
        enableMqtt = $true
        mqttSettings = @{
            brokerHost = "localhost"
            brokerPort = 1883
            username = "testuser1"
            password = "testpass1"
            topicPrefix = "TEST1"
        }
    }
} | ConvertTo-Json -Depth 3

$domain2 = @{
    domainName = "test-domain-2"
    displayName = "Test Domain 2"
    adminEmail = "admin2@test-domain.com"
    adminPassword = "Admin123!"
    settings = @{
        maxUsers = 100
        maxAssets = 1000
        enableMqtt = $true
        mqttSettings = @{
            brokerHost = "localhost"
            brokerPort = 1883
            username = "testuser2"
            password = "testpass2"
            topicPrefix = "TEST2"
        }
    }
} | ConvertTo-Json -Depth 3

$updateDomain = @{
    domainName = "test-domain-1-updated"
    displayName = "Test Domain 1 Updated"
    adminEmail = "admin1-updated@test-domain.com"
    adminPassword = "Admin123!"
    settings = @{
        maxUsers = 75
        maxAssets = 750
        enableMqtt = $true
        mqttSettings = @{
            brokerHost = "localhost"
            brokerPort = 1883
            username = "testuser1-updated"
            password = "testpass1-updated"
            topicPrefix = "TEST1-UPDATED"
        }
    }
} | ConvertTo-Json -Depth 3

function Test-Endpoint {
    param(
        [string]$Method,
        [string]$Url,
        [string]$Body = $null,
        [string]$Description
    )
    
    Write-Host "`nTesting: $Description" -ForegroundColor Cyan
    Write-Host "URL: $Method $Url" -ForegroundColor Gray
    
    try {
        $headers = @{
            "Content-Type" = "application/json"
        }
        
        if ($Body) {
            $response = Invoke-RestMethod -Uri $Url -Method $Method -Headers $headers -Body $Body -TimeoutSec 10
        } else {
            $response = Invoke-RestMethod -Uri $Url -Method $Method -Headers $headers -TimeoutSec 10
        }
        
        Write-Host "✅ SUCCESS" -ForegroundColor Green
        Write-Host "Response: $($response | ConvertTo-Json -Depth 2)" -ForegroundColor Gray
        return $response
    }
    catch {
        Write-Host "❌ FAILED: $($_.Exception.Message)" -ForegroundColor Red
        return $null
    }
}

# Test 1: Controller test
Write-Host "`n=== Test 1: Controller Test ===" -ForegroundColor Yellow
$testResult = Test-Endpoint -Method "GET" -Url "$baseUrl/test" -Description "Controller Test"

# Test 2: Clear all domains
Write-Host "`n=== Test 2: Clear All Domains ===" -ForegroundColor Yellow
Test-Endpoint -Method "DELETE" -Url "$baseUrl/clear" -Description "Clear All Domains"

# Test 3: Get empty domains list
Write-Host "`n=== Test 3: Get Empty Domains List ===" -ForegroundColor Yellow
Test-Endpoint -Method "GET" -Url "$baseUrl" -Description "Get Empty Domains List"

# Test 4: Create first domain
Write-Host "`n=== Test 4: Create First Domain ===" -ForegroundColor Yellow
$domain1Result = Test-Endpoint -Method "POST" -Url "$baseUrl" -Body $domain1 -Description "Create First Domain"

# Test 5: Create second domain
Write-Host "`n=== Test 5: Create Second Domain ===" -ForegroundColor Yellow
$domain2Result = Test-Endpoint -Method "POST" -Url "$baseUrl" -Body $domain2 -Description "Create Second Domain"

# Test 6: Try to create duplicate domain
Write-Host "`n=== Test 6: Try to Create Duplicate Domain ===" -ForegroundColor Yellow
Test-Endpoint -Method "POST" -Url "$baseUrl" -Body $domain1 -Description "Try to Create Duplicate Domain"

# Test 7: Get all domains
Write-Host "`n=== Test 7: Get All Domains ===" -ForegroundColor Yellow
$allDomains = Test-Endpoint -Method "GET" -Url "$baseUrl" -Description "Get All Domains"

# Test 8: Get domain by ID
Write-Host "`n=== Test 8: Get Domain by ID ===" -ForegroundColor Yellow
if ($domain1Result -and $domain1Result.domainId) {
    Test-Endpoint -Method "GET" -Url "$baseUrl/$($domain1Result.domainId)" -Description "Get Domain by ID"
}

# Test 9: Get domain by name
Write-Host "`n=== Test 9: Get Domain by Name ===" -ForegroundColor Yellow
Test-Endpoint -Method "GET" -Url "$baseUrl/name/test-domain-1" -Description "Get Domain by Name"

# Test 10: Get non-existent domain
Write-Host "`n=== Test 10: Get Non-existent Domain ===" -ForegroundColor Yellow
Test-Endpoint -Method "GET" -Url "$baseUrl/non-existent-id" -Description "Get Non-existent Domain"

# Test 11: Update domain
Write-Host "`n=== Test 11: Update Domain ===" -ForegroundColor Yellow
if ($domain1Result -and $domain1Result.domainId) {
    $updateResult = Test-Endpoint -Method "PUT" -Url "$baseUrl/$($domain1Result.domainId)" -Body $updateDomain -Description "Update Domain"
}

# Test 12: Get updated domain
Write-Host "`n=== Test 12: Get Updated Domain ===" -ForegroundColor Yellow
if ($domain1Result -and $domain1Result.domainId) {
    Test-Endpoint -Method "GET" -Url "$baseUrl/$($domain1Result.domainId)" -Description "Get Updated Domain"
}

# Test 13: Delete domain
Write-Host "`n=== Test 13: Delete Domain ===" -ForegroundColor Yellow
if ($domain2Result -and $domain2Result.domainId) {
    Test-Endpoint -Method "DELETE" -Url "$baseUrl/$($domain2Result.domainId)" -Description "Delete Domain"
}

# Test 14: Verify deletion
Write-Host "`n=== Test 14: Verify Deletion ===" -ForegroundColor Yellow
if ($domain2Result -and $domain2Result.domainId) {
    Test-Endpoint -Method "GET" -Url "$baseUrl/$($domain2Result.domainId)" -Description "Verify Deletion"
}

# Test 15: Get final domains list
Write-Host "`n=== Test 15: Get Final Domains List ===" -ForegroundColor Yellow
Test-Endpoint -Method "GET" -Url "$baseUrl" -Description "Get Final Domains List"

Write-Host "`n=== Domain CRUD Test Complete ===" -ForegroundColor Green
