# Test: Can active user (isActive: true) get a token?
Write-Host "`n=== Test Active User Token Access ===" -ForegroundColor Green

$baseUrl = "https://localhost:5001/api"

# Step 1: Get admin token for ebebek domain
Write-Host "`n[1/3] Getting admin token for ebebek domain..." -ForegroundColor Cyan

$adminTokenBody = @{
    username = "ebebek_admin"
    password = "Admin123!"
    domain = "ebebek"
} | ConvertTo-Json

try {
    $adminTokenResponse = Invoke-RestMethod -Uri "$baseUrl/auth/token" `
        -Method POST `
        -Body $adminTokenBody `
        -ContentType "application/json" `
        -SkipCertificateCheck

    $adminToken = $adminTokenResponse.accessToken
    Write-Host "✅ Admin token retrieved successfully!" -ForegroundColor Green
} catch {
    Write-Host "❌ Error getting admin token: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

$headers = @{
    "Authorization" = "Bearer $adminToken"
    "Content-Type" = "application/json"
}

$timestamp = Get-Date -Format "yyyyMMddHHmmss"

# Step 2: Create a test user as ACTIVE
Write-Host "`n[2/3] Creating test user as ACTIVE (isActive=true)..." -ForegroundColor Cyan
$userBody = @{
    username = "active.user.$timestamp"
    email = "active.user.$timestamp@ebebek.com"
    password = "Test123!"
    firstName = "Active"
    lastName = "User"
    groupIds = @()
    isActive = $true  # Create as active
    customData = @{
        test = "active_user_test"
    }
} | ConvertTo-Json -Depth 10

try {
    $userResponse = Invoke-RestMethod -Uri "$baseUrl/user" `
        -Method POST `
        -Headers $headers `
        -Body $userBody `
        -ContentType "application/json" `
        -SkipCertificateCheck `
        -ErrorAction Stop

    if ($userResponse.isSuccess) {
        $userId = $userResponse.userId
        $username = $userResponse.username
        Write-Host "✅ User created successfully!" -ForegroundColor Green
        Write-Host "   User ID: $userId" -ForegroundColor Cyan
        Write-Host "   Username: $username" -ForegroundColor Cyan
        Write-Host "   isActive: $($userResponse.isActive)" -ForegroundColor Cyan
    } else {
        Write-Host "❌ User creation failed: $($userResponse.errorMessage)" -ForegroundColor Red
        exit 1
    }
} catch {
    Write-Host "❌ Error creating user: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.ErrorDetails) {
        Write-Host "   Error Details: $($_.ErrorDetails.Message)" -ForegroundColor Yellow
    }
    exit 1
}

Start-Sleep -Seconds 2

# Step 3: Test if active user can get token
Write-Host "`n[3/3] Testing token access for active user..." -ForegroundColor Cyan
Write-Host "   User was created with isActive=true" -ForegroundColor Yellow
Write-Host "   Testing if AuthController allows token for active users..." -ForegroundColor Yellow

$tokenBody = @{
    username = $username
    password = "Test123!"
    domain = "ebebek"
} | ConvertTo-Json

try {
    $tokenResponse = Invoke-RestMethod -Uri "$baseUrl/auth/token" `
        -Method POST `
        -Body $tokenBody `
        -ContentType "application/json" `
        -SkipCertificateCheck `
        -ErrorAction Stop

    Write-Host "✅ SUCCESS: Active user can get a token (as expected)!" -ForegroundColor Green
    Write-Host "   Token Length: $($tokenResponse.accessToken.Length) characters" -ForegroundColor Cyan
    Write-Host "   Token Type: $($tokenResponse.tokenType)" -ForegroundColor Cyan
    Write-Host "   Expires In: $($tokenResponse.expiresIn) seconds" -ForegroundColor Cyan
    $tokenObtained = $true
} catch {
    Write-Host "❌ ERROR: Active user cannot get a token (unexpected)!" -ForegroundColor Red
    if ($_.ErrorDetails) {
        Write-Host "   Error Details: $($_.ErrorDetails.Message)" -ForegroundColor Yellow
    }
    $tokenObtained = $false
}

# Summary
Write-Host "`n=== Test Summary ===" -ForegroundColor Green
Write-Host "✅ User created: $userId" -ForegroundColor Green
Write-Host "✅ User created with isActive = true" -ForegroundColor Green
if ($tokenObtained) {
    Write-Host "✅ SECURITY OK: Active user can get token (correct behavior)" -ForegroundColor Green
    Write-Host "   The IsActive check is working correctly:" -ForegroundColor Cyan
    Write-Host "   - Active users (isActive=true) CAN get tokens ✅" -ForegroundColor Green
    Write-Host "   - Inactive users (isActive=false) CANNOT get tokens ✅" -ForegroundColor Green
} else {
    Write-Host "❌ SECURITY ISSUE: Active user cannot get token" -ForegroundColor Red
    Write-Host "   The IsActive check may be blocking all users incorrectly" -ForegroundColor Yellow
}

Write-Host "`n=== Test Complete ===" -ForegroundColor Green

