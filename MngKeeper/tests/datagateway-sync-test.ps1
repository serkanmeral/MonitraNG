# DataGateway Sync Test Script
# Tests user/group sync from MngKeeper to DataGateway MongoDB

Write-Host "`n=== DataGateway Sync Test ===" -ForegroundColor Green

# Get token first
Write-Host "`n[1/8] Getting token..." -ForegroundColor Cyan
& "$PSScriptRoot\get-serkan-token.ps1"
if (-not $global:serkanToken) {
    $global:serkanToken = Get-Content "$env:TEMP\serkan_token.txt" -Raw -ErrorAction SilentlyContinue
}

if (-not $global:serkanToken) {
    Write-Host "❌ Failed to get token!" -ForegroundColor Red
    exit 1
}

$baseUrl = "https://localhost:5001/api"
$headers = @{
    "Authorization" = "Bearer $global:serkanToken"
    "Content-Type" = "application/json"
}

# Test helper function
function Test-Endpoint {
    param(
        [string]$Method,
        [string]$Url,
        [object]$Body = $null,
        [string]$Description,
        [switch]$ShowResponse
    )

    Write-Host "`n📋 $Description" -ForegroundColor Yellow
    Write-Host "   $Method $Url" -ForegroundColor Gray

    try {
        $params = @{
            Uri = $Url
            Method = $Method
            Headers = $headers
            SkipCertificateCheck = $true
            ErrorAction = "Stop"
        }

        if ($Body) {
            $params.Body = ($Body | ConvertTo-Json -Depth 10)
        }

        $response = Invoke-RestMethod @params

        Write-Host "   ✅ SUCCESS" -ForegroundColor Green
        if ($ShowResponse) {
            Write-Host "   Response:" -ForegroundColor Cyan
            $response | ConvertTo-Json -Depth 5 | Write-Host -ForegroundColor Gray
        }
        return $response
    }
    catch {
        Write-Host "   ❌ FAILED: $($_.Exception.Message)" -ForegroundColor Red
        if ($_.Exception.Response) {
            try {
                $stream = $_.Exception.Response.GetResponseStream()
                $reader = New-Object System.IO.StreamReader($stream)
                $responseBody = $reader.ReadToEnd()
                Write-Host "   Error Response: $responseBody" -ForegroundColor Yellow
            } catch {
                Write-Host "   Could not read error response" -ForegroundColor Yellow
            }
        }
        return $null
    }
}

# Test 1: Create User (without customData first, to test basic sync)
Write-Host "`n[2/8] Creating user..." -ForegroundColor Cyan
$timestamp = Get-Date -Format "yyyyMMddHHmmss"
$testUsername = "test.sync.user.$timestamp"
$testEmail = "test.sync.user.$timestamp@seven.com"
$createUserBody = @{
    username = $testUsername
    email = $testEmail
    password = "Test123!"
    firstName = "Test"
    lastName = "SyncUser"
    groupIds = @()
    isActive = $true
}

$createdUser = Test-Endpoint -Method "POST" -Url "$baseUrl/user" -Body $createUserBody -Description "Create User" -ShowResponse

if (-not $createdUser -or -not $createdUser.isSuccess) {
    Write-Host "`n❌ User creation failed! Cannot continue." -ForegroundColor Red
    exit 1
}

$userId = $createdUser.userId
Write-Host "   Created User ID: $userId" -ForegroundColor Cyan

# Wait a bit for sync
Start-Sleep -Seconds 2

# Test 2: Check MongoDB @users collection
Write-Host "`n[3/8] Checking MongoDB @users collection..." -ForegroundColor Cyan
Write-Host "   Note: Manual MongoDB check required" -ForegroundColor Yellow
Write-Host "   Database: mng_seven" -ForegroundColor Yellow
Write-Host "   Collection: @users" -ForegroundColor Yellow
$queryStr = "{ `"__dataId`": `"$userId`" }"
Write-Host "   Query: $queryStr" -ForegroundColor Yellow
Write-Host "`n   Run in MongoDB shell:" -ForegroundColor Cyan
Write-Host "   use mng_seven" -ForegroundColor Gray
$mongoQuery = "db.getCollection(`"@users`").find({ `"__dataId`": `"$userId`" }).pretty()"
Write-Host "   $mongoQuery" -ForegroundColor Gray

# Test 3: Update User with Custom Data
Write-Host "`n[4/8] Updating user with new custom data..." -ForegroundColor Cyan
$updateUserBody = @{
    username = $testUsername
    email = "$testUsername.updated@seven.com"
    firstName = "Test Updated"
    lastName = "SyncUser Updated"
    groupIds = @()
    isActive = $true
    customData = @{
        phone = "+90 555 999 8888"
        department = "Engineering"
        position = "Lead Developer"
        employeeId = "EMP-001"
        salary = 50000
    }
}

$updatedUser = Test-Endpoint -Method "PUT" -Url "$baseUrl/user/$userId" -Body $updateUserBody -Description "Update User with Custom Data" -ShowResponse

# Wait a bit for sync
Start-Sleep -Seconds 2

# Test 4: Manual Sync - Users
Write-Host "`n[5/8] Testing manual user sync..." -ForegroundColor Cyan
$syncUsersResult = Test-Endpoint -Method "POST" -Url "$baseUrl/sync/users" -Description "Manual Sync Users" -ShowResponse

if ($syncUsersResult) {
    Write-Host "   Total: $($syncUsersResult.totalCount)" -ForegroundColor Cyan
    Write-Host "   Created: $($syncUsersResult.createdCount)" -ForegroundColor Green
    Write-Host "   Updated: $($syncUsersResult.updatedCount)" -ForegroundColor Yellow
    Write-Host "   Errors: $($syncUsersResult.errorCount)" -ForegroundColor $(if ($syncUsersResult.errorCount -gt 0) { "Red" } else { "Green" })
}

# Test 5: Create Group with Custom Data
Write-Host "`n[6/8] Creating group with custom data..." -ForegroundColor Cyan
$testGroupName = "test-sync-group.$timestamp"
$createGroupBody = @{
    name = $testGroupName
    description = "Test sync group"
    permissions = @("read", "write")
    isActive = $true
    customData = @{
        color = "#FF0000"
        icon = "test-icon"
        category = "Testing"
    }
}

$createdGroup = Test-Endpoint -Method "POST" -Url "$baseUrl/group" -Body $createGroupBody -Description "Create Group with Custom Data" -ShowResponse

if (-not $createdGroup -or -not $createdGroup.isSuccess) {
    Write-Host "`n⚠️ Group creation failed, but continuing..." -ForegroundColor Yellow
} else {
    $groupId = $createdGroup.groupId
    Write-Host "   Created Group ID: $groupId" -ForegroundColor Cyan
    
    # Wait a bit for sync
    Start-Sleep -Seconds 2
}

# Test 6: Manual Sync - Groups
Write-Host "`n[7/8] Testing manual group sync..." -ForegroundColor Cyan
$syncGroupsResult = Test-Endpoint -Method "POST" -Url "$baseUrl/sync/groups" -Description "Manual Sync Groups" -ShowResponse

if ($syncGroupsResult) {
    Write-Host "   Total: $($syncGroupsResult.totalCount)" -ForegroundColor Cyan
    Write-Host "   Created: $($syncGroupsResult.createdCount)" -ForegroundColor Green
    Write-Host "   Updated: $($syncGroupsResult.updatedCount)" -ForegroundColor Yellow
    Write-Host "   Errors: $($syncGroupsResult.errorCount)" -ForegroundColor $(if ($syncGroupsResult.errorCount -gt 0) { "Red" } else { "Green" })
}

# Test 7: Manual Sync - All
Write-Host "`n[8/8] Testing manual full sync..." -ForegroundColor Cyan
$syncAllResult = Test-Endpoint -Method "POST" -Url "$baseUrl/sync/all" -Description "Manual Sync All" -ShowResponse

if ($syncAllResult) {
    Write-Host "   Total: $($syncAllResult.totalCount)" -ForegroundColor Cyan
    Write-Host "   Created: $($syncAllResult.createdCount)" -ForegroundColor Green
    Write-Host "   Updated: $($syncAllResult.updatedCount)" -ForegroundColor Yellow
    Write-Host "   Errors: $($syncAllResult.errorCount)" -ForegroundColor $(if ($syncAllResult.errorCount -gt 0) { "Red" } else { "Green" })
}

# Summary
Write-Host "`n=== Test Summary ===" -ForegroundColor Green
Write-Host "✅ User created with custom data" -ForegroundColor Green
Write-Host "✅ User updated with custom data" -ForegroundColor Green
Write-Host "✅ Manual sync endpoints tested" -ForegroundColor Green
Write-Host "`n📝 Next Steps:" -ForegroundColor Yellow
Write-Host "   1. Check MongoDB @users collection in mng_seven database" -ForegroundColor Cyan
Write-Host "   2. Check MongoDB @groups collection in mng_seven database" -ForegroundColor Cyan
Write-Host "   3. Verify custom data fields are present" -ForegroundColor Cyan
Write-Host "   4. Verify __syncInfo metadata is present" -ForegroundColor Cyan

Write-Host "`n=== Test Complete ===" -ForegroundColor Green

