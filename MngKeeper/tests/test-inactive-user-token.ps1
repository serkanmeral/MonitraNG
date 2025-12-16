# Test: Can inactive user (isActive: false) get a token?
Write-Host "`n=== Test Inactive User Token Access ===" -ForegroundColor Green

$baseUrl = "https://localhost:5001/api"

# Step 1: Get admin token for ebebek domain
Write-Host "`n[1/4] Getting admin token for ebebek domain..." -ForegroundColor Cyan

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

# Step 2: Create a test user as INACTIVE
Write-Host "`n[2/4] Creating test user as INACTIVE (isActive=false)..." -ForegroundColor Cyan
$userBody = @{
    username = "inactive.user.$timestamp"
    email = "inactive.user.$timestamp@ebebek.com"
    password = "Test123!"
    firstName = "Inactive"
    lastName = "User"
    groupIds = @()
    isActive = $false  # Create as inactive
    customData = @{
        test = "inactive_user_test"
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

# Step 3: Test if inactive user can get token
Write-Host "`n[3/4] Testing token access for inactive user..." -ForegroundColor Cyan
Write-Host "   User was created with isActive=false" -ForegroundColor Yellow
Write-Host "   Testing if AuthController checks IsActive before issuing token..." -ForegroundColor Yellow
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

    Write-Host "⚠️ WARNING: Inactive user was able to get a token!" -ForegroundColor Yellow
    Write-Host "   Token Length: $($tokenResponse.accessToken.Length) characters" -ForegroundColor Yellow
    Write-Host "   This is a security issue - inactive users should not be able to authenticate." -ForegroundColor Red
    $tokenObtained = $true
} catch {
    Write-Host "✅ GOOD: Inactive user cannot get a token (as expected)" -ForegroundColor Green
    if ($_.ErrorDetails) {
        Write-Host "   Error Details: $($_.ErrorDetails.Message)" -ForegroundColor Cyan
    }
    $tokenObtained = $false
}

# Summary
Write-Host "`n=== Test Summary ===" -ForegroundColor Green
Write-Host "✅ User created: $userId" -ForegroundColor Green
Write-Host "✅ User created with isActive = false" -ForegroundColor Green
if ($tokenObtained) {
    Write-Host "❌ SECURITY ISSUE: Inactive user can get token" -ForegroundColor Red
    Write-Host "   Recommendation: Add IsActive check in AuthController.GetToken()" -ForegroundColor Yellow
} else {
    Write-Host "✅ SECURITY OK: Inactive user cannot get token" -ForegroundColor Green
}

Write-Host "`n=== Test Complete ===" -ForegroundColor Green

