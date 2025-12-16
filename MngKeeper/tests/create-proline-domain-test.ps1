# Create Proline Domain and Test User/Group Creation
Write-Host "`n=== Create Proline Domain Test ===" -ForegroundColor Green

$baseUrl = "https://localhost:5001/api"

# Step 1: Create Proline Domain
Write-Host "`n[1/3] Creating Proline domain..." -ForegroundColor Cyan

$domainBody = @{
    domainName = "proline"
    displayName = "Proline Domain"
    adminEmail = "admin@proline.com"
    adminPassword = "Admin123!"
} | ConvertTo-Json

try {
    $domainResponse = Invoke-RestMethod -Uri "$baseUrl/domain" `
        -Method POST `
        -Body $domainBody `
        -ContentType "application/json" `
        -SkipCertificateCheck

    if ($domainResponse.isSuccess) {
        Write-Host "✅ Domain created successfully!" -ForegroundColor Green
        Write-Host "   Domain ID: $($domainResponse.domainId)" -ForegroundColor Cyan
        Write-Host "   Domain Name: $($domainResponse.domainName)" -ForegroundColor Cyan
        Write-Host "   Database: $($domainResponse.databaseName)" -ForegroundColor Cyan
        Write-Host "   Admin Username: $($domainResponse.adminUsername)" -ForegroundColor Cyan
        
        $domainId = $domainResponse.domainId
        $domainName = $domainResponse.domainName
        $adminUsername = $domainResponse.adminUsername
    } else {
        Write-Host "❌ Domain creation failed: $($domainResponse.message)" -ForegroundColor Red
        exit 1
    }
} catch {
    Write-Host "❌ Error creating domain: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.Exception.Response) {
        try {
            $responseBody = $_.Exception.Response.Content.ReadAsStringAsync().Result
            Write-Host "   Error Response: $responseBody" -ForegroundColor Yellow
        } catch {
            Write-Host "   Could not read error response" -ForegroundColor Yellow
        }
    }
    # Check if domain already exists
    if ($_.Exception.Message -like "*already exists*" -or $_.Exception.Message -like "*duplicate*") {
        Write-Host "`n⚠️ Domain may already exist. Continuing with existing domain..." -ForegroundColor Yellow
        # Try to get existing domain info
        $domainId = "693d50f18a3c00cfd54ce8b9"  # From previous run
        $domainName = "proline"
        $adminUsername = "proline_admin"
    } else {
        exit 1
    }
}

# Wait a bit for domain setup
Start-Sleep -Seconds 3

# Step 2: Get Token for Proline Domain
Write-Host "`n[2/3] Getting token for Proline domain..." -ForegroundColor Cyan

$tokenBody = @{
    username = $adminUsername
    password = "Admin123!"
    domain = $domainName
} | ConvertTo-Json

try {
    $tokenResponse = Invoke-RestMethod -Uri "$baseUrl/auth/token" `
        -Method POST `
        -Body $tokenBody `
        -ContentType "application/json" `
        -SkipCertificateCheck

    $token = $tokenResponse.accessToken
    $global:prolineToken = $token
    
    # Save token to file
    $token | Out-File -FilePath "$env:TEMP\proline_token.txt" -NoNewline -Encoding ASCII
    
    Write-Host "✅ Token retrieved successfully!" -ForegroundColor Green
    Write-Host "   Token Length: $($token.Length) characters" -ForegroundColor Cyan
} catch {
    Write-Host "❌ Error getting token: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# Step 3: Test User and Group Creation
Write-Host "`n[3/3] Testing user and group creation..." -ForegroundColor Cyan

$headers = @{
    "Authorization" = "Bearer $token"
    "Content-Type" = "application/json"
}

$timestamp = Get-Date -Format "yyyyMMddHHmmss"

# Create a group first
Write-Host "`n   Creating test group..." -ForegroundColor Yellow
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
    $groupResponse = Invoke-RestMethod -Uri "$baseUrl/group" `
        -Method POST `
        -Headers $headers `
        -Body $groupBody `
        -SkipCertificateCheck

    if ($groupResponse.isSuccess) {
        Write-Host "   ✅ Group created: $($groupResponse.groupId)" -ForegroundColor Green
        $groupId = $groupResponse.groupId
    } else {
        Write-Host "   ❌ Group creation failed" -ForegroundColor Red
        $groupId = $null
    }
} catch {
    Write-Host "   ❌ Error creating group: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.Exception.Response) {
        try {
            $stream = $_.Exception.Response.GetResponseStream()
            $reader = New-Object System.IO.StreamReader($stream)
            $responseBody = $reader.ReadToEnd()
            Write-Host "   Error Response: $responseBody" -ForegroundColor Yellow
        } catch {
            try {
                $responseBody = $_.Exception.Response.Content.ReadAsStringAsync().Result
                Write-Host "   Error Response: $responseBody" -ForegroundColor Yellow
            } catch {
                Write-Host "   Could not read error response" -ForegroundColor Yellow
            }
        }
    }
    $groupId = $null
}

Start-Sleep -Seconds 1

# Create a user
Write-Host "`n   Creating test user..." -ForegroundColor Yellow
$userBody = @{
    username = "test.user.$timestamp"
    email = "test.user.$timestamp@proline.com"
    password = "Test123!"
    firstName = "Test"
    lastName = "User"
    groupIds = if ($groupId) { @($groupId) } else { @() }
    isActive = $true
    customData = @{
        phone = "+90 555 111 2222"
        department = "Engineering"
        position = "Developer"
        employeeId = "EMP-001"
    }
} | ConvertTo-Json -Depth 10

try {
    $userResponse = Invoke-RestMethod -Uri "$baseUrl/user" `
        -Method POST `
        -Headers $headers `
        -Body $userBody `
        -SkipCertificateCheck

    if ($userResponse.isSuccess) {
        Write-Host "   ✅ User created: $($userResponse.userId)" -ForegroundColor Green
        $userId = $userResponse.userId
    } else {
        Write-Host "   ❌ User creation failed" -ForegroundColor Red
        $userId = $null
    }
} catch {
    Write-Host "   ❌ Error creating user: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.Exception.Response) {
        try {
            $responseBody = $_.Exception.Response.Content.ReadAsStringAsync().Result
            Write-Host "   Error Response: $responseBody" -ForegroundColor Yellow
        } catch {
            Write-Host "   Could not read error response" -ForegroundColor Yellow
        }
    }
    $userId = $null
}

# Wait for sync
Start-Sleep -Seconds 2

# Check MongoDB collections
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

# Summary
Write-Host "`n=== Test Summary ===" -ForegroundColor Green
Write-Host "✅ Domain: proline (ID: $domainId)" -ForegroundColor Green
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
Write-Host "`n💾 Token saved to: $env:TEMP\proline_token.txt" -ForegroundColor Cyan
Write-Host "`n=== Test Complete ===" -ForegroundColor Green

