# User CRUD Test Script
Write-Host "=== User CRUD Test Script ===" -ForegroundColor Green

$baseUrl = "http://localhost:5001/api/usercrudtest"

# Test data
$user1 = @{
    username = "john.doe"
    email = "john.doe@test-domain.com"
    firstName = "John"
    lastName = "Doe"
    domainId = "test-domain-1"
    roles = @("user", "admin")
} | ConvertTo-Json -Depth 2

$user2 = @{
    username = "jane.smith"
    email = "jane.smith@test-domain.com"
    firstName = "Jane"
    lastName = "Smith"
    domainId = "test-domain-1"
    roles = @("user")
} | ConvertTo-Json -Depth 2

$updateUser = @{
    username = "john.doe.updated"
    email = "john.doe.updated@test-domain.com"
    firstName = "John Updated"
    lastName = "Doe Updated"
    isActive = $true
    roles = @("user", "admin", "manager")
} | ConvertTo-Json -Depth 2

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

# Test 2: Clear all users
Write-Host "`n=== Test 2: Clear All Users ===" -ForegroundColor Yellow
Test-Endpoint -Method "DELETE" -Url "$baseUrl/clear" -Description "Clear All Users"

# Test 3: Get empty users list
Write-Host "`n=== Test 3: Get Empty Users List ===" -ForegroundColor Yellow
Test-Endpoint -Method "GET" -Url "$baseUrl" -Description "Get Empty Users List"

# Test 4: Create first user
Write-Host "`n=== Test 4: Create First User ===" -ForegroundColor Yellow
$user1Result = Test-Endpoint -Method "POST" -Url "$baseUrl" -Body $user1 -Description "Create First User"

# Test 5: Create second user
Write-Host "`n=== Test 5: Create Second User ===" -ForegroundColor Yellow
$user2Result = Test-Endpoint -Method "POST" -Url "$baseUrl" -Body $user2 -Description "Create Second User"

# Test 6: Try to create duplicate user
Write-Host "`n=== Test 6: Try to Create Duplicate User ===" -ForegroundColor Yellow
Test-Endpoint -Method "POST" -Url "$baseUrl" -Body $user1 -Description "Try to Create Duplicate User"

# Test 7: Get all users
Write-Host "`n=== Test 7: Get All Users ===" -ForegroundColor Yellow
$allUsers = Test-Endpoint -Method "GET" -Url "$baseUrl" -Description "Get All Users"

# Test 8: Get user by ID
Write-Host "`n=== Test 8: Get User by ID ===" -ForegroundColor Yellow
if ($user1Result -and $user1Result.userId) {
    Test-Endpoint -Method "GET" -Url "$baseUrl/$($user1Result.userId)" -Description "Get User by ID"
}

# Test 9: Get user by username
Write-Host "`n=== Test 9: Get User by Username ===" -ForegroundColor Yellow
Test-Endpoint -Method "GET" -Url "$baseUrl/username/john.doe" -Description "Get User by Username"

# Test 10: Get non-existent user
Write-Host "`n=== Test 10: Get Non-existent User ===" -ForegroundColor Yellow
Test-Endpoint -Method "GET" -Url "$baseUrl/non-existent-id" -Description "Get Non-existent User"

# Test 11: Update user
Write-Host "`n=== Test 11: Update User ===" -ForegroundColor Yellow
if ($user1Result -and $user1Result.userId) {
    $updateResult = Test-Endpoint -Method "PUT" -Url "$baseUrl/$($user1Result.userId)" -Body $updateUser -Description "Update User"
}

# Test 12: Get updated user
Write-Host "`n=== Test 12: Get Updated User ===" -ForegroundColor Yellow
if ($user1Result -and $user1Result.userId) {
    Test-Endpoint -Method "GET" -Url "$baseUrl/$($user1Result.userId)" -Description "Get Updated User"
}

# Test 13: Delete user
Write-Host "`n=== Test 13: Delete User ===" -ForegroundColor Yellow
if ($user2Result -and $user2Result.userId) {
    Test-Endpoint -Method "DELETE" -Url "$baseUrl/$($user2Result.userId)" -Description "Delete User"
}

# Test 14: Verify deletion
Write-Host "`n=== Test 14: Verify Deletion ===" -ForegroundColor Yellow
if ($user2Result -and $user2Result.userId) {
    Test-Endpoint -Method "GET" -Url "$baseUrl/$($user2Result.userId)" -Description "Verify Deletion"
}

# Test 15: Get final users list
Write-Host "`n=== Test 15: Get Final Users List ===" -ForegroundColor Yellow
Test-Endpoint -Method "GET" -Url "$baseUrl" -Description "Get Final Users List"

Write-Host "`n=== User CRUD Test Complete ===" -ForegroundColor Green
