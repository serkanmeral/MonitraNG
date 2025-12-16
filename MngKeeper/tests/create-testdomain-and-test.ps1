# Create new domain and test user default group assignment
Write-Host "`n=== Create New Domain and Test User Default Group ===" -ForegroundColor Green

$baseUrl = "https://localhost:5001/api"
$timestamp = Get-Date -Format "yyyyMMddHHmmss"
$domainName = "testdomain$timestamp"

# Step 1: Create new domain (no authentication required)
Write-Host "`n[1/6] Creating new domain: $domainName..." -ForegroundColor Cyan
$domainBody = @{
    domainName = $domainName
    displayName = "Test Domain"
    adminEmail = "$domainName.admin@test.com"
    adminPassword = "Admin123!"
    settings = @{
        maxUsers = 1000
        maxStorageGB = 100
        features = @("users", "groups", "datasets")
    }
} | ConvertTo-Json -Depth 10

try {
    $domainResponse = Invoke-RestMethod -Uri "$baseUrl/domain" -Method POST -Body $domainBody -ContentType "application/json" -SkipCertificateCheck -ErrorAction Stop
    
    if ($domainResponse.isSuccess) {
        Write-Host "✅ Domain created successfully!" -ForegroundColor Green
        Write-Host "   Domain ID: $($domainResponse.domainId)" -ForegroundColor Cyan
        Write-Host "   Domain Name: $($domainResponse.domainName)" -ForegroundColor Cyan
    } else {
        Write-Host "❌ Domain creation failed: $($domainResponse.errorMessage)" -ForegroundColor Red
        exit 1
    }
} catch {
    Write-Host "❌ Error creating domain: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.ErrorDetails) {
        Write-Host "   Error Details: $($_.ErrorDetails.Message)" -ForegroundColor Yellow
    }
    exit 1
}

# Wait a bit for domain setup to complete
Write-Host "`n   Waiting 5 seconds for domain setup to complete..." -ForegroundColor Yellow
Start-Sleep -Seconds 5

# Step 2: Get admin token for new domain
Write-Host "`n[2/6] Getting admin token for $domainName domain..." -ForegroundColor Cyan
$adminTokenBody = @{
    username = "$domainName`_admin"
    password = "Admin123!"
    domain = $domainName
} | ConvertTo-Json

try {
    $adminToken = (Invoke-RestMethod -Uri "$baseUrl/auth/token" -Method POST -Body $adminTokenBody -ContentType "application/json" -SkipCertificateCheck).accessToken
    Write-Host "✅ Admin token retrieved!" -ForegroundColor Green
} catch {
    Write-Host "❌ Error getting admin token: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

$adminHeaders = @{
    "Authorization" = "Bearer $adminToken"
    "Content-Type" = "application/json"
}

# Step 2.5: Configure Keycloak protocol mappers for the new domain
Write-Host "`n[2.5/6] Configuring Keycloak protocol mappers for $domainName..." -ForegroundColor Cyan
try {
    $mapperResponse = Invoke-RestMethod -Uri "$baseUrl/admin/realms/$domainName/configure-mappers" -Method POST -Headers $adminHeaders -ContentType "application/json" -SkipCertificateCheck -ErrorAction SilentlyContinue
    Write-Host "✅ Protocol mappers configured (or already configured)" -ForegroundColor Green
    
    # Get a fresh token after mapper configuration
    Write-Host "   Getting fresh admin token after mapper configuration..." -ForegroundColor Yellow
    $adminToken = (Invoke-RestMethod -Uri "$baseUrl/auth/token" -Method POST -Body $adminTokenBody -ContentType "application/json" -SkipCertificateCheck).accessToken
    $adminHeaders = @{
        "Authorization" = "Bearer $adminToken"
        "Content-Type" = "application/json"
    }
} catch {
    Write-Host "⚠️  Warning: Could not configure protocol mappers: $($_.Exception.Message)" -ForegroundColor Yellow
    Write-Host "   Continuing anyway..." -ForegroundColor Yellow
}

# Step 3: Verify "users" group exists
Write-Host "`n[3/6] Verifying 'users' group exists..." -ForegroundColor Cyan
try {
    $groupsResponse = Invoke-RestMethod -Uri "$baseUrl/group" -Method GET -Headers $adminHeaders -SkipCertificateCheck
    $usersGroup = $groupsResponse.groups | Where-Object { $_.name -eq "users" } | Select-Object -First 1
    
    if ($usersGroup) {
        Write-Host "✅ 'users' group found: $($usersGroup.groupId)" -ForegroundColor Green
        Write-Host "   Name: $($usersGroup.name)" -ForegroundColor Cyan
        Write-Host "   Description: $($usersGroup.description)" -ForegroundColor Cyan
    } else {
        Write-Host "❌ 'users' group NOT found!" -ForegroundColor Red
        Write-Host "   Available groups: $($groupsResponse.groups.name -join ', ')" -ForegroundColor Yellow
        exit 1
    }
} catch {
    Write-Host "❌ Error checking groups: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# Step 4: Create a new user WITHOUT specifying groups
Write-Host "`n[4/6] Creating new user WITHOUT specifying groups..." -ForegroundColor Cyan
Write-Host "   Expected: User should be automatically added to 'users' group" -ForegroundColor Yellow

$userBody = @{
    username = "test.user.$timestamp"
    email = "test.user.$timestamp@$domainName.com"
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
    $userResponse = Invoke-RestMethod -Uri "$baseUrl/user" -Method POST -Headers $adminHeaders -Body $userBody -ContentType "application/json" -SkipCertificateCheck -ErrorAction Stop

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

# Step 5: Get token for new user and check user_groups claim
Write-Host "`n[5/6] Getting token for new user and checking user_groups claim..." -ForegroundColor Cyan

$tokenBody = @{
    username = $username
    password = "Test123!"
    domain = $domainName
} | ConvertTo-Json

try {
    $tokenResponse = Invoke-RestMethod -Uri "$baseUrl/auth/token" -Method POST -Body $tokenBody -ContentType "application/json" -SkipCertificateCheck -ErrorAction Stop

    $token = $tokenResponse.accessToken
    Write-Host "✅ Token retrieved successfully!" -ForegroundColor Green
    
    # Parse token
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
                Write-Host "   ✅ SUCCESS: 'users' group is present in token!" -ForegroundColor Green
            } else {
                Write-Host "   ⚠️  'users' group is NOT present" -ForegroundColor Yellow
            }
        } else {
            Write-Host "   Groups: $($claims.user_groups)" -ForegroundColor Cyan
            if ($claims.user_groups -like "*users*") {
                Write-Host "   ✅ SUCCESS: 'users' group appears to be present!" -ForegroundColor Green
            }
        }
    } else {
        Write-Host "❌ User Groups: MISSING" -ForegroundColor Red
        Write-Host "   The user_groups claim is not in the token" -ForegroundColor Yellow
    }
} catch {
    Write-Host "❌ Error getting token: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# Step 6: Verify user is in "users" group in MngKeeper DB
Write-Host "`n[6/6] Verifying user is in 'users' group in MngKeeper DB..." -ForegroundColor Cyan
try {
    $userResponse = Invoke-RestMethod -Uri "$baseUrl/user" -Method GET -Headers $adminHeaders -SkipCertificateCheck
    $createdUser = $userResponse.users | Where-Object { $_.username -eq $username } | Select-Object -First 1
    
    if ($createdUser) {
        Write-Host "✅ User found in MngKeeper DB" -ForegroundColor Green
        Write-Host "   User ID: $($createdUser.userId)" -ForegroundColor Cyan
        Write-Host "   Groups: $($createdUser.groups -join ', ')" -ForegroundColor Cyan
        Write-Host "   Groups Count: $($createdUser.groups.Count)" -ForegroundColor Cyan
        
        if ($createdUser.groups.Count -gt 0) {
            Write-Host "   ✅ User has groups in MngKeeper database" -ForegroundColor Green
            if ($createdUser.groups -contains $usersGroup.groupId) {
                Write-Host "   ✅ User IS in 'users' group (MngKeeper DB)" -ForegroundColor Green
            } else {
                Write-Host "   ⚠️  User is NOT in 'users' group (MngKeeper DB)" -ForegroundColor Yellow
            }
        } else {
            Write-Host "   ❌ User has NO groups in MngKeeper database" -ForegroundColor Red
        }
    } else {
        Write-Host "⚠️  User not found in user list" -ForegroundColor Yellow
    }
} catch {
    Write-Host "⚠️  Error checking user in MngKeeper DB: $($_.Exception.Message)" -ForegroundColor Yellow
}

# Summary
Write-Host "`n=== Test Summary ===" -ForegroundColor Green
Write-Host "✅ Domain created: $domainName" -ForegroundColor Green
Write-Host "✅ 'users' group verified in domain" -ForegroundColor Green
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

