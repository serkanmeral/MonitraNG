# Create new domain, test groups, users and get access token
Write-Host "`n=== Create Test Domain with Groups and Users ===" -ForegroundColor Green

$baseUrl = "https://localhost:5001/api"
$timestamp = Get-Date -Format "yyyyMMddHHmmss"
$domainName = "testdomain$timestamp"

# Step 1: Create new domain
Write-Host "`n[1/7] Creating new domain: $domainName..." -ForegroundColor Cyan
$domainBody = @{
    domainName = $domainName
    displayName = "Test Domain for JWT Testing"
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

# Wait for domain setup to complete
Write-Host "`n   Waiting 5 seconds for domain setup to complete..." -ForegroundColor Yellow
Start-Sleep -Seconds 5

# Step 2: Get admin token
Write-Host "`n[2/7] Getting admin token for $domainName domain..." -ForegroundColor Cyan
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

# Step 3: Configure Keycloak protocol mappers
Write-Host "`n[3/7] Configuring Keycloak protocol mappers for $domainName..." -ForegroundColor Cyan
try {
    $mapperResponse = Invoke-RestMethod -Uri "$baseUrl/admin/realms/$domainName/configure-mappers" -Method POST -Headers $adminHeaders -ContentType "application/json" -SkipCertificateCheck -ErrorAction SilentlyContinue
    Write-Host "✅ Protocol mappers configured" -ForegroundColor Green
    
    # Get fresh token after mapper configuration
    Write-Host "   Getting fresh admin token after mapper configuration..." -ForegroundColor Yellow
    $adminToken = (Invoke-RestMethod -Uri "$baseUrl/auth/token" -Method POST -Body $adminTokenBody -ContentType "application/json" -SkipCertificateCheck).accessToken
    $adminHeaders = @{
        "Authorization" = "Bearer $adminToken"
        "Content-Type" = "application/json"
    }
} catch {
    Write-Host "⚠️  Warning: Could not configure protocol mappers: $($_.Exception.Message)" -ForegroundColor Yellow
}

# Step 4: Verify default groups exist
Write-Host "`n[4/7] Verifying default groups exist..." -ForegroundColor Cyan
try {
    $groupsResponse = Invoke-RestMethod -Uri "$baseUrl/group" -Method GET -Headers $adminHeaders -SkipCertificateCheck
    $defaultGroups = $groupsResponse.groups | Where-Object { $_.name -in @("users", "admins", "managers", "guests") }
    
    Write-Host "✅ Default groups found:" -ForegroundColor Green
    foreach ($group in $defaultGroups) {
        Write-Host "   - $($group.name) (ID: $($group.groupId))" -ForegroundColor Cyan
    }
    
    $usersGroup = $defaultGroups | Where-Object { $_.name -eq "users" } | Select-Object -First 1
    if (-not $usersGroup) {
        Write-Host "❌ 'users' group NOT found!" -ForegroundColor Red
        exit 1
    }
} catch {
    Write-Host "❌ Error checking groups: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# Step 5: Create additional test groups
Write-Host "`n[5/7] Creating additional test groups..." -ForegroundColor Cyan
$testGroups = @(
    @{ name = "developers"; description = "Development Team" },
    @{ name = "testers"; description = "QA Team" },
    @{ name = "viewers"; description = "Read-only Users" }
)

$createdGroups = @{}
foreach ($groupData in $testGroups) {
    try {
        $groupBody = @{
            name = $groupData.name
            description = $groupData.description
            permissions = @()
            isActive = $true
        } | ConvertTo-Json
        
        $createResponse = Invoke-RestMethod -Uri "$baseUrl/group" -Method POST -Headers $adminHeaders -Body $groupBody -ContentType "application/json" -SkipCertificateCheck -ErrorAction Stop
        
        if ($createResponse.isSuccess) {
            $createdGroups[$groupData.name] = $createResponse.groupId
            Write-Host "✅ Created group: $($groupData.name) (ID: $($createResponse.groupId))" -ForegroundColor Green
        }
    } catch {
        Write-Host "⚠️  Could not create group $($groupData.name): $($_.Exception.Message)" -ForegroundColor Yellow
    }
}

# Step 6: Create test users
Write-Host "`n[6/7] Creating test users..." -ForegroundColor Cyan
$testUsers = @(
    @{
        username = "test.user1.$timestamp"
        email = "test.user1.$timestamp@$domainName.com"
        firstName = "Test"
        lastName = "User1"
        password = "Test123!"
        groupIds = @()  # Will be auto-added to "users" group
    },
    @{
        username = "test.developer.$timestamp"
        email = "test.developer.$timestamp@$domainName.com"
        firstName = "Test"
        lastName = "Developer"
        password = "Test123!"
        groupIds = @($usersGroup.groupId)
        additionalGroups = @("developers")
    },
    @{
        username = "test.manager.$timestamp"
        email = "test.manager.$timestamp@$domainName.com"
        firstName = "Test"
        lastName = "Manager"
        password = "Test123!"
        groupIds = @($usersGroup.groupId)
        additionalGroups = @("managers", "viewers")
    }
)

$createdUsers = @{}
foreach ($userData in $testUsers) {
    try {
        # Get group IDs for additional groups
        $finalGroupIds = @($userData.groupIds)
        if ($userData.additionalGroups) {
            foreach ($groupName in $userData.additionalGroups) {
                $group = $groupsResponse.groups | Where-Object { $_.name -eq $groupName } | Select-Object -First 1
                if ($group -and $group.groupId -notin $finalGroupIds) {
                    $finalGroupIds += $group.groupId
                }
            }
        }
        
        $userBody = @{
            username = $userData.username
            email = $userData.email
            password = $userData.password
            firstName = $userData.firstName
            lastName = $userData.lastName
            groupIds = $finalGroupIds
            isActive = $true
        } | ConvertTo-Json -Depth 10
        
        $userResponse = Invoke-RestMethod -Uri "$baseUrl/user" -Method POST -Headers $adminHeaders -Body $userBody -ContentType "application/json" -SkipCertificateCheck -ErrorAction Stop
        
        if ($userResponse.isSuccess) {
            $createdUsers[$userData.username] = @{
                userId = $userResponse.userId
                username = $userResponse.username
                email = $userData.email
                password = $userData.password
            }
            Write-Host "✅ Created user: $($userData.username) (ID: $($userResponse.userId))" -ForegroundColor Green
        }
    } catch {
        Write-Host "⚠️  Could not create user $($userData.username): $($_.Exception.Message)" -ForegroundColor Yellow
    }
}

Start-Sleep -Seconds 2

# Step 7: Get access tokens for test users
Write-Host "`n[7/7] Getting access tokens for test users..." -ForegroundColor Cyan
Write-Host "`n" + "="*80 -ForegroundColor Yellow
Write-Host "ACCESS TOKENS FOR JWT.IO TESTING" -ForegroundColor Green
Write-Host "="*80 -ForegroundColor Yellow

$tokens = @{}
foreach ($username in $createdUsers.Keys) {
    $user = $createdUsers[$username]
    try {
        $tokenBody = @{
            username = $user.username
            password = $user.password
            domain = $domainName
        } | ConvertTo-Json
        
        $tokenResponse = Invoke-RestMethod -Uri "$baseUrl/auth/token" -Method POST -Body $tokenBody -ContentType "application/json" -SkipCertificateCheck -ErrorAction Stop
        
        $token = $tokenResponse.accessToken
        $tokens[$username] = $token
        
        # Parse token to show claims
        $tokenParts = $token.Split('.')
        $payload = $tokenParts[1]
        while ($payload.Length % 4 -ne 0) { $payload += "=" }
        $json = [System.Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($payload.Replace('-', '+').Replace('_', '/')))
        $claims = $json | ConvertFrom-Json
        
        Write-Host "`n--- User: $username ---" -ForegroundColor Cyan
        Write-Host "Username: $($claims.preferred_username)" -ForegroundColor White
        Write-Host "Email: $($claims.email)" -ForegroundColor White
        Write-Host "Domain: $($claims.domain_name)" -ForegroundColor White
        if ($claims.user_groups) {
            $groups = if ($claims.user_groups -is [System.Array]) { $claims.user_groups -join ', ' } else { $claims.user_groups }
            Write-Host "Groups: $groups" -ForegroundColor White
        }
        Write-Host "`nAccess Token:" -ForegroundColor Yellow
        Write-Host $token -ForegroundColor Gray
        Write-Host "`n" + "-"*80 -ForegroundColor DarkGray
        
    } catch {
        Write-Host "❌ Error getting token for $username : $($_.Exception.Message)" -ForegroundColor Red
    }
}

# Summary
Write-Host "`n" + "="*80 -ForegroundColor Yellow
Write-Host "SUMMARY" -ForegroundColor Green
Write-Host "="*80 -ForegroundColor Yellow
Write-Host "Domain Name: $domainName" -ForegroundColor Cyan
Write-Host "Domain ID: $($domainResponse.domainId)" -ForegroundColor Cyan
Write-Host "Admin Username: ${domainName}_admin" -ForegroundColor Cyan
Write-Host "Admin Password: Admin123!" -ForegroundColor Cyan
Write-Host "`nCreated Groups:" -ForegroundColor Cyan
Write-Host "  - users (default)" -ForegroundColor White
Write-Host "  - admins (default)" -ForegroundColor White
Write-Host "  - managers (default)" -ForegroundColor White
Write-Host "  - guests (default)" -ForegroundColor White
foreach ($groupName in $createdGroups.Keys) {
    Write-Host "  - $groupName" -ForegroundColor White
}
Write-Host "`nCreated Users:" -ForegroundColor Cyan
foreach ($username in $createdUsers.Keys) {
    Write-Host "  - $username (Password: Test123!)" -ForegroundColor White
}
Write-Host "`n✅ All tokens are ready for testing on jwt.io!" -ForegroundColor Green
Write-Host "="*80 -ForegroundColor Yellow

