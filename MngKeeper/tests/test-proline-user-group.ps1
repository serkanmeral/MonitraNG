# Test User and Group Creation for Proline Domain
Write-Host "`n=== Test Proline User/Group Creation ===" -ForegroundColor Green

$baseUrl = "https://localhost:5001/api"

# Step 1: Get Token for Proline Domain
Write-Host "`n[1/3] Getting token for Proline domain..." -ForegroundColor Cyan

$tokenBody = @{
    username = "proline_admin"
    password = "Admin123!"
    domain = "proline"
} | ConvertTo-Json

try {
    $tokenResponse = Invoke-RestMethod -Uri "$baseUrl/auth/token" `
        -Method POST `
        -Body $tokenBody `
        -ContentType "application/json" `
        -SkipCertificateCheck

    $token = $tokenResponse.accessToken
    $global:prolineToken = $token
    
    Write-Host "✅ Token retrieved successfully!" -ForegroundColor Green
    Write-Host "   Token Length: $($token.Length) characters" -ForegroundColor Cyan
} catch {
    Write-Host "❌ Error getting token: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.Exception.Response) {
        try {
            $responseBody = $_.Exception.Response.Content.ReadAsStringAsync().Result
            Write-Host "   Error Response: $responseBody" -ForegroundColor Yellow
        } catch {
            Write-Host "   Could not read error response" -ForegroundColor Yellow
        }
    }
    exit 1
}

$headers = @{
    "Authorization" = "Bearer $token"
    "Content-Type" = "application/json"
}

$timestamp = Get-Date -Format "yyyyMMddHHmmss"

# Step 2: Create a Group
Write-Host "`n[2/3] Creating test group..." -ForegroundColor Cyan
$groupBody = @{
    name = "test-group.$timestamp"
    description = "Test group for Proline domain"
    permissions = @("read", "write")
    isActive = $true
    customData = @{
        color = "#00FF00"
        category = "Testing"
    }
} | ConvertTo-Json -Depth 10

try {
    $responseObj = Invoke-RestMethod -Uri "$baseUrl/group" `
        -Method POST `
        -Headers $headers `
        -Body $groupBody `
        -ContentType "application/json" `
        -SkipCertificateCheck `
        -ErrorAction Stop

    if ($responseObj.isSuccess) {
        Write-Host "✅ Group created successfully!" -ForegroundColor Green
        Write-Host "   Group ID: $($responseObj.groupId)" -ForegroundColor Cyan
        Write-Host "   Name: $($responseObj.name)" -ForegroundColor Cyan
        $groupId = $responseObj.groupId
    } else {
        Write-Host "❌ Group creation failed: $($responseObj.errorMessage)" -ForegroundColor Red
        $groupId = $null
    }
} catch {
    Write-Host "❌ Error creating group: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.ErrorDetails) {
        Write-Host "   Error Details: $($_.ErrorDetails.Message)" -ForegroundColor Yellow
    }
    if ($_.Exception.Response) {
        try {
            $statusCode = $_.Exception.Response.StatusCode.value__
            Write-Host "   Status Code: $statusCode" -ForegroundColor Yellow
        } catch {}
    }
    $groupId = $null
}

Start-Sleep -Seconds 1

# Step 3: Create a User
Write-Host "`n[3/3] Creating test user..." -ForegroundColor Cyan
$groupIdsArray = @()
if ($groupId) {
    $groupIdsArray = @($groupId)
}
$userBody = @{
    username = "test.user.$timestamp"
    email = "test.user.$timestamp@proline.com"
    password = "Test123!"
    firstName = "Test"
    lastName = "User"
    groupIds = $groupIdsArray
    isActive = $true
    customData = @{
        phone = "+90 555 111 2222"
        department = "Engineering"
        position = "Developer"
        employeeId = "EMP-001"
    }
} | ConvertTo-Json -Depth 10

try {
    $responseObj = Invoke-RestMethod -Uri "$baseUrl/user" `
        -Method POST `
        -Headers $headers `
        -Body $userBody `
        -ContentType "application/json" `
        -SkipCertificateCheck `
        -ErrorAction Stop

    if ($responseObj.isSuccess) {
        Write-Host "✅ User created successfully!" -ForegroundColor Green
        Write-Host "   User ID: $($responseObj.userId)" -ForegroundColor Cyan
        Write-Host "   Username: $($responseObj.username)" -ForegroundColor Cyan
        $userId = $responseObj.userId
    } else {
        Write-Host "❌ User creation failed: $($responseObj.errorMessage)" -ForegroundColor Red
        $userId = $null
    }
} catch {
    Write-Host "❌ Error creating user: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.ErrorDetails) {
        Write-Host "   Error Details: $($_.ErrorDetails.Message)" -ForegroundColor Yellow
    }
    if ($_.Exception.Response) {
        try {
            $statusCode = $_.Exception.Response.StatusCode.value__
            Write-Host "   Status Code: $statusCode" -ForegroundColor Yellow
        } catch {}
    }
    $userId = $null
}

# Wait for sync
Start-Sleep -Seconds 2

# Summary
Write-Host "`n=== Test Summary ===" -ForegroundColor Green
Write-Host "✅ Domain: proline" -ForegroundColor Green
Write-Host "✅ Database: mng_proline" -ForegroundColor Green
if ($groupId) {
    Write-Host "✅ Group created: $groupId" -ForegroundColor Green
} else {
    Write-Host "⚠️ Group creation: Failed" -ForegroundColor Yellow
}
if ($userId) {
    Write-Host "✅ User created: $userId" -ForegroundColor Green
} else {
    Write-Host "⚠️ User creation: Failed" -ForegroundColor Yellow
}

# MongoDB Verification
Write-Host "`n📝 MongoDB Verification:" -ForegroundColor Cyan
Write-Host "   Database: mng_proline" -ForegroundColor Yellow
Write-Host "   Collections to check:" -ForegroundColor Yellow
Write-Host "     - @users" -ForegroundColor Gray
Write-Host "     - @groups" -ForegroundColor Gray
Write-Host "`n   Run in MongoDB shell:" -ForegroundColor Cyan
Write-Host "   use mng_proline" -ForegroundColor Gray
if ($userId) {
    Write-Host "   db.getCollection(`"@users`").find({ `"__dataId`": `"$userId`" }).pretty()" -ForegroundColor Gray
}
if ($groupId) {
    Write-Host "   db.getCollection(`"@groups`").find({ `"__dataId`": `"$groupId`" }).pretty()" -ForegroundColor Gray
}
Write-Host "   db.getCollection(`"@users`").find({}).count()" -ForegroundColor Gray
Write-Host "   db.getCollection(`"@groups`").find({}).count()" -ForegroundColor Gray
Write-Host "   db.getCollection(`"@users`").find({}).pretty()" -ForegroundColor Gray
Write-Host "   db.getCollection(`"@groups`").find({}).pretty()" -ForegroundColor Gray

Write-Host "`n=== Test Complete ===" -ForegroundColor Green

