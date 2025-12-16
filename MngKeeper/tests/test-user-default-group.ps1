# Test: New user should be automatically added to "users" group
Write-Host "`n=== Test User Default Group Assignment ===" -ForegroundColor Green

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

# Step 2: Create a new user WITHOUT specifying any groups
Write-Host "`n[2/4] Creating new user WITHOUT specifying groups..." -ForegroundColor Cyan
Write-Host "   Expected: User should be automatically added to 'users' group" -ForegroundColor Yellow

$userBody = @{
    username = "test.user.$timestamp"
    email = "test.user.$timestamp@ebebek.com"
    password = "Test123!"
    firstName = "Test"
    lastName = "User"
    groupIds = @()  # Empty - should default to "users" group
    isActive = $true
    customData = @{
        test = "default_group_test"
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

# Step 3: Get token for the new user
Write-Host "`n[3/4] Getting token for new user..." -ForegroundColor Cyan

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

    $token = $tokenResponse.accessToken
    Write-Host "✅ Token retrieved successfully!" -ForegroundColor Green
} catch {
    Write-Host "❌ Error getting token: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# Step 4: Parse token and check for user_groups claim
Write-Host "`n[4/4] Parsing token to check user_groups claim..." -ForegroundColor Cyan

$tokenParts = $token.Split('.')
$payload = $tokenParts[1]
while ($payload.Length % 4 -ne 0) { $payload += "=" }
$json = [System.Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($payload.Replace('-', '+').Replace('_', '/')))
$claims = $json | ConvertFrom-Json

Write-Host "`n--- TOKEN PAYLOAD (Relevant Claims) ---" -ForegroundColor Yellow
Write-Host "Username: $($claims.preferred_username)" -ForegroundColor Cyan
Write-Host "Email: $($claims.email)" -ForegroundColor Cyan
Write-Host "Domain Name: $($claims.domain_name)" -ForegroundColor Cyan
Write-Host "Domain ID: $($claims.domain_id)" -ForegroundColor Cyan
Write-Host "Is Admin: $($claims.isAdmin)" -ForegroundColor Cyan

if ($claims.user_groups) {
    Write-Host "✅ User Groups: $($claims.user_groups)" -ForegroundColor Green
    if ($claims.user_groups -is [System.Array]) {
        $groupsArray = $claims.user_groups
        Write-Host "   Groups found: $($groupsArray.Count)" -ForegroundColor Cyan
        foreach ($group in $groupsArray) {
            Write-Host "     - $group" -ForegroundColor Gray
        }
        if ($groupsArray -contains "users") {
            Write-Host "   ✅ 'users' group is present!" -ForegroundColor Green
        } else {
            Write-Host "   ⚠️  'users' group is NOT present" -ForegroundColor Yellow
        }
    } else {
        Write-Host "   Groups: $($claims.user_groups)" -ForegroundColor Cyan
        if ($claims.user_groups -like "*users*") {
            Write-Host "   ✅ 'users' group appears to be present!" -ForegroundColor Green
        }
    }
} else {
    Write-Host "❌ User Groups: MISSING" -ForegroundColor Red
    Write-Host "   The user_groups claim is not in the token" -ForegroundColor Yellow
}

# Display full access token
Write-Host "`n========================================" -ForegroundColor Green
Write-Host "ACCESS TOKEN (for jwt.io)" -ForegroundColor Yellow
Write-Host "========================================" -ForegroundColor Green
Write-Host $token -ForegroundColor Cyan
Write-Host "`n💡 Copy the token above and paste it into https://jwt.io to decode it" -ForegroundColor Green

# Summary
Write-Host "`n=== Test Summary ===" -ForegroundColor Green
Write-Host "✅ User created: $userId" -ForegroundColor Green
Write-Host "✅ User created without specifying groups" -ForegroundColor Green
if ($claims.user_groups) {
    Write-Host "✅ SUCCESS: user_groups claim is present in token" -ForegroundColor Green
    if ($claims.user_groups -is [System.Array] -and ($claims.user_groups -contains "users" -or $claims.user_groups -like "*users*")) {
        Write-Host "✅ SUCCESS: 'users' group is in the token" -ForegroundColor Green
    } else {
        Write-Host "⚠️  WARNING: 'users' group may not be in token (check jwt.io)" -ForegroundColor Yellow
    }
} else {
    Write-Host "❌ ISSUE: user_groups claim is missing from token" -ForegroundColor Red
    Write-Host "   Possible causes:" -ForegroundColor Yellow
    Write-Host "   - Keycloak protocol mapper not configured correctly" -ForegroundColor Yellow
    Write-Host "   - User not actually added to any groups" -ForegroundColor Yellow
    Write-Host "   - Token needs to be refreshed" -ForegroundColor Yellow
}

Write-Host "`n=== Test Complete ===" -ForegroundColor Green

