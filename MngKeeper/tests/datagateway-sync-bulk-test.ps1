# DataGateway Sync Bulk Test Script
# Tests user/group sync with multiple records and various scenarios

Write-Host "`n=== DataGateway Sync Bulk Test ===" -ForegroundColor Green

# Get token first
Write-Host "`n[1/10] Getting token..." -ForegroundColor Cyan
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

$timestamp = Get-Date -Format "yyyyMMddHHmmss"
$testResults = @{
    UsersCreated = 0
    UsersUpdated = 0
    GroupsCreated = 0
    GroupsUpdated = 0
    Errors = @()
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

# Test 1: Create Multiple Groups First
Write-Host "`n[2/10] Creating multiple groups..." -ForegroundColor Cyan
$groups = @()

$groupDefinitions = @(
    @{ name = "engineering"; description = "Engineering Team"; permissions = @("read", "write", "delete"); color = "#00FF00"; category = "Technical" },
    @{ name = "sales"; description = "Sales Team"; permissions = @("read", "write"); color = "#0000FF"; category = "Business" },
    @{ name = "marketing"; description = "Marketing Team"; permissions = @("read"); color = "#FF00FF"; category = "Business" },
    @{ name = "hr"; description = "Human Resources"; permissions = @("read", "write"); color = "#FFFF00"; category = "Administrative" },
    @{ name = "finance"; description = "Finance Team"; permissions = @("read"); color = "#00FFFF"; category = "Administrative" }
)

foreach ($groupDef in $groupDefinitions) {
    $groupName = "$($groupDef.name).$timestamp"
    $createGroupBody = @{
        name = $groupName
        description = $groupDef.description
        permissions = $groupDef.permissions
        isActive = $true
        customData = @{
            color = $groupDef.color
            category = $groupDef.category
            createdBy = "bulk-test"
        }
    }

    $createdGroup = Test-Endpoint -Method "POST" -Url "$baseUrl/group" -Body $createGroupBody -Description "Create Group: $groupName"
    
    if ($createdGroup -and $createdGroup.isSuccess) {
        $groups += @{
            id = $createdGroup.groupId
            name = $groupName
            originalName = $groupDef.name
        }
        $testResults.GroupsCreated++
        Write-Host "   ✅ Group created: $groupName (ID: $($createdGroup.groupId))" -ForegroundColor Green
    } else {
        $testResults.Errors += "Failed to create group: $groupName"
    }
    
    Start-Sleep -Milliseconds 500
}

Write-Host "`n   Created $($groups.Count) groups" -ForegroundColor Cyan

# Test 2: Create Multiple Users with Different Scenarios
Write-Host "`n[3/10] Creating multiple users with different scenarios..." -ForegroundColor Cyan
$users = @()

$userDefinitions = @(
    @{ 
        username = "john.doe"; 
        email = "john.doe@seven.com"; 
        firstName = "John"; 
        lastName = "Doe"; 
        groups = @("engineering"); 
        customData = @{ 
            phone = "+90 555 111 1111"; 
            department = "Engineering"; 
            position = "Senior Developer"; 
            employeeId = "EMP-001"; 
            salary = 75000;
            skills = @("C#", ".NET", "MongoDB");
            startDate = "2020-01-15"
        } 
    },
    @{ 
        username = "jane.smith"; 
        email = "jane.smith@seven.com"; 
        firstName = "Jane"; 
        lastName = "Smith"; 
        groups = @("sales"); 
        customData = @{ 
            phone = "+90 555 222 2222"; 
            department = "Sales"; 
            position = "Sales Manager"; 
            employeeId = "EMP-002"; 
            salary = 65000;
            region = "Istanbul";
            target = 1000000
        } 
    },
    @{ 
        username = "bob.johnson"; 
        email = "bob.johnson@seven.com"; 
        firstName = "Bob"; 
        lastName = "Johnson"; 
        groups = @("marketing", "sales"); 
        customData = @{ 
            phone = "+90 555 333 3333"; 
            department = "Marketing"; 
            position = "Marketing Specialist"; 
            employeeId = "EMP-003"; 
            salary = 55000;
            campaigns = @("Summer Campaign", "Winter Campaign")
        } 
    },
    @{ 
        username = "alice.williams"; 
        email = "alice.williams@seven.com"; 
        firstName = "Alice"; 
        lastName = "Williams"; 
        groups = @("hr"); 
        customData = @{ 
            phone = "+90 555 444 4444"; 
            department = "Human Resources"; 
            position = "HR Manager"; 
            employeeId = "EMP-004"; 
            salary = 70000;
            certifications = @("PHR", "SHRM-CP")
        } 
    },
    @{ 
        username = "charlie.brown"; 
        email = "charlie.brown@seven.com"; 
        firstName = "Charlie"; 
        lastName = "Brown"; 
        groups = @("finance"); 
        customData = @{ 
            phone = "+90 555 555 5555"; 
            department = "Finance"; 
            position = "Financial Analyst"; 
            employeeId = "EMP-005"; 
            salary = 60000;
            certifications = @("CFA Level 1")
        } 
    },
    @{ 
        username = "diana.prince"; 
        email = "diana.prince@seven.com"; 
        firstName = "Diana"; 
        lastName = "Prince"; 
        groups = @("engineering", "hr"); 
        customData = @{ 
            phone = "+90 555 666 6666"; 
            department = "Engineering"; 
            position = "Tech Lead"; 
            employeeId = "EMP-006"; 
            salary = 90000;
            skills = @("C#", ".NET", "MongoDB", "Docker", "Kubernetes");
            projects = @("Project Alpha", "Project Beta")
        } 
    },
    @{ 
        username = "eve.adams"; 
        email = "eve.adams@seven.com"; 
        firstName = "Eve"; 
        lastName = "Adams"; 
        groups = @(); 
        customData = @{ 
            phone = "+90 555 777 7777"; 
            department = "Intern"; 
            position = "Junior Developer"; 
            employeeId = "EMP-007"; 
            salary = 30000;
            mentor = "john.doe"
        } 
    },
    @{ 
        username = "frank.miller"; 
        email = "frank.miller@seven.com"; 
        firstName = "Frank"; 
        lastName = "Miller"; 
        groups = @("sales", "marketing"); 
        customData = @{ 
            phone = "+90 555 888 8888"; 
            department = "Sales"; 
            position = "Account Executive"; 
            employeeId = "EMP-008"; 
            salary = 58000;
            region = "Ankara";
            target = 800000;
            achievements = @("Top Sales Q1", "Top Sales Q2")
        } 
    },
    @{ 
        username = "grace.lee"; 
        email = "grace.lee@seven.com"; 
        firstName = "Grace"; 
        lastName = "Lee"; 
        groups = @("engineering"); 
        customData = @{ 
            phone = "+90 555 999 9999"; 
            department = "Engineering"; 
            position = "DevOps Engineer"; 
            employeeId = "EMP-009"; 
            salary = 80000;
            skills = @("Docker", "Kubernetes", "Azure", "CI/CD");
            certifications = @("AWS Certified", "Kubernetes Certified")
        } 
    },
    @{ 
        username = "henry.taylor"; 
        email = "henry.taylor@seven.com"; 
        firstName = "Henry"; 
        lastName = "Taylor"; 
        groups = @("finance", "hr"); 
        customData = @{ 
            phone = "+90 555 000 0000"; 
            department = "Finance"; 
            position = "CFO"; 
            employeeId = "EMP-010"; 
            salary = 120000;
            certifications = @("CPA", "MBA");
            boardMember = $true
        } 
    }
)

foreach ($userDef in $userDefinitions) {
    $username = "$($userDef.username).$timestamp"
    $email = "$($userDef.username).$timestamp@seven.com"
    
    # Map group names to actual group IDs
    $groupIds = @()
    foreach ($groupName in $userDef.groups) {
        $group = $groups | Where-Object { $_.originalName -eq $groupName }
        if ($group) {
            $groupIds += $group.id
        }
    }
    
    $createUserBody = @{
        username = $username
        email = $email
        password = "Test123!"
        firstName = $userDef.firstName
        lastName = $userDef.lastName
        groupIds = $groupIds
        isActive = $true
        customData = $userDef.customData
    }

    $createdUser = Test-Endpoint -Method "POST" -Url "$baseUrl/user" -Body $createUserBody -Description "Create User: $username"
    
    if ($createdUser -and $createdUser.isSuccess) {
        $users += @{
            id = $createdUser.userId
            username = $username
            originalUsername = $userDef.username
        }
        $testResults.UsersCreated++
        Write-Host "   ✅ User created: $username (ID: $($createdUser.userId), Groups: $($groupIds.Count))" -ForegroundColor Green
    } else {
        $testResults.Errors += "Failed to create user: $username"
    }
    
    Start-Sleep -Milliseconds 500
}

Write-Host "`n   Created $($users.Count) users" -ForegroundColor Cyan

# Wait for sync
Write-Host "`n[4/10] Waiting for automatic sync..." -ForegroundColor Cyan
Start-Sleep -Seconds 3

# Test 3: Update Some Users with New Custom Data
Write-Host "`n[5/10] Updating users with new custom data..." -ForegroundColor Cyan
$usersToUpdate = $users | Select-Object -First 3

foreach ($user in $usersToUpdate) {
    $updateUserBody = @{
        username = $user.username
        email = "$($user.username).updated@seven.com"
        firstName = "$($user.originalUsername.Split('.')[0]) Updated"
        lastName = "Updated"
        groupIds = @() # Keep existing groups
        isActive = $true
        customData = @{
            phone = "+90 555 UPDATED"
            department = "Updated Department"
            position = "Updated Position"
            updatedAt = (Get-Date -Format "yyyy-MM-dd HH:mm:ss")
            updateReason = "Bulk test update"
        }
    }

    $updatedUser = Test-Endpoint -Method "PUT" -Url "$baseUrl/user/$($user.id)" -Body $updateUserBody -Description "Update User: $($user.username)"
    
    if ($updatedUser -and $updatedUser.isSuccess) {
        $testResults.UsersUpdated++
        Write-Host "   ✅ User updated: $($user.username)" -ForegroundColor Green
    } else {
        $testResults.Errors += "Failed to update user: $($user.username)"
    }
    
    Start-Sleep -Milliseconds 500
}

# Test 4: Add Users to Additional Groups
Write-Host "`n[6/10] Adding users to additional groups..." -ForegroundColor Cyan
if ($users.Count -ge 2 -and $groups.Count -ge 2) {
    $userToUpdate = $users[0]
    $additionalGroup = $groups[1]
    
    # Get current user to preserve groups
    $currentUser = Test-Endpoint -Method "GET" -Url "$baseUrl/user/$($userToUpdate.id)" -Description "Get User for Group Update"
    
    if ($currentUser) {
        $newGroupIds = $currentUser.groups
        if ($newGroupIds -notcontains $additionalGroup.id) {
            $newGroupIds += $additionalGroup.id
        }
        
        $updateUserBody = @{
            username = $userToUpdate.username
            email = $currentUser.email
            firstName = $currentUser.firstName
            lastName = $currentUser.lastName
            groupIds = $newGroupIds
            isActive = $currentUser.isActive
        }

        $updatedUser = Test-Endpoint -Method "PUT" -Url "$baseUrl/user/$($userToUpdate.id)" -Body $updateUserBody -Description "Add User to Group: $($additionalGroup.name)"
        
        if ($updatedUser -and $updatedUser.isSuccess) {
            Write-Host "   ✅ User added to group: $($additionalGroup.name)" -ForegroundColor Green
        }
    }
}

# Test 5: Manual Sync - Users
Write-Host "`n[7/10] Testing manual user sync..." -ForegroundColor Cyan
$syncUsersResult = Test-Endpoint -Method "POST" -Url "$baseUrl/sync/users" -Description "Manual Sync Users" -ShowResponse

if ($syncUsersResult) {
    Write-Host "   Total: $($syncUsersResult.totalCount)" -ForegroundColor Cyan
    Write-Host "   Created: $($syncUsersResult.createdCount)" -ForegroundColor Green
    Write-Host "   Updated: $($syncUsersResult.updatedCount)" -ForegroundColor Yellow
    Write-Host "   Errors: $($syncUsersResult.errorCount)" -ForegroundColor $(if ($syncUsersResult.errorCount -gt 0) { "Red" } else { "Green" })
}

# Test 6: Manual Sync - Groups
Write-Host "`n[8/10] Testing manual group sync..." -ForegroundColor Cyan
$syncGroupsResult = Test-Endpoint -Method "POST" -Url "$baseUrl/sync/groups" -Description "Manual Sync Groups" -ShowResponse

if ($syncGroupsResult) {
    Write-Host "   Total: $($syncGroupsResult.totalCount)" -ForegroundColor Cyan
    Write-Host "   Created: $($syncGroupsResult.createdCount)" -ForegroundColor Green
    Write-Host "   Updated: $($syncGroupsResult.updatedCount)" -ForegroundColor Yellow
    Write-Host "   Errors: $($syncGroupsResult.errorCount)" -ForegroundColor $(if ($syncGroupsResult.errorCount -gt 0) { "Red" } else { "Green" })
}

# Test 7: Manual Sync - All
Write-Host "`n[9/10] Testing manual full sync..." -ForegroundColor Cyan
$syncAllResult = Test-Endpoint -Method "POST" -Url "$baseUrl/sync/all" -Description "Manual Sync All" -ShowResponse

if ($syncAllResult) {
    Write-Host "   Total: $($syncAllResult.totalCount)" -ForegroundColor Cyan
    Write-Host "   Created: $($syncAllResult.createdCount)" -ForegroundColor Green
    Write-Host "   Updated: $($syncAllResult.updatedCount)" -ForegroundColor Yellow
    Write-Host "   Errors: $($syncAllResult.errorCount)" -ForegroundColor $(if ($syncAllResult.errorCount -gt 0) { "Red" } else { "Green" })
}

# Test 8: Verify Data in MongoDB
Write-Host "`n[10/10] MongoDB Verification Queries..." -ForegroundColor Cyan
Write-Host "`n📝 MongoDB Queries to Run:" -ForegroundColor Yellow
Write-Host "`n1. Check all synced users:" -ForegroundColor Cyan
Write-Host "   use mng_seven" -ForegroundColor Gray
Write-Host "   db.getCollection(`"@users`").find({}).count()" -ForegroundColor Gray
Write-Host "   db.getCollection(`"@users`").find({}).pretty()" -ForegroundColor Gray

Write-Host "`n2. Check all synced groups:" -ForegroundColor Cyan
Write-Host "   db.getCollection(`"@groups`").find({}).count()" -ForegroundColor Gray
Write-Host "   db.getCollection(`"@groups`").find({}).pretty()" -ForegroundColor Gray

Write-Host "`n3. Check users with custom data:" -ForegroundColor Cyan
Write-Host "   db.getCollection(`"@users`").find({ phone: { `$exists: true } }).count()" -ForegroundColor Gray
Write-Host "   db.getCollection(`"@users`").find({ phone: { `$exists: true } }, { username: 1, phone: 1, department: 1, position: 1 }).pretty()" -ForegroundColor Gray

Write-Host "`n4. Check groups with custom data:" -ForegroundColor Cyan
Write-Host "   db.getCollection(`"@groups`").find({ color: { `$exists: true } }).count()" -ForegroundColor Gray
Write-Host "   db.getCollection(`"@groups`").find({ color: { `$exists: true } }, { name: 1, color: 1, category: 1 }).pretty()" -ForegroundColor Gray

Write-Host "`n5. Check sync metadata:" -ForegroundColor Cyan
Write-Host "   db.getCollection(`"@users`").find({}, { username: 1, __syncInfo: 1 }).limit(5).pretty()" -ForegroundColor Gray
Write-Host "   db.getCollection(`"@groups`").find({}, { name: 1, __syncInfo: 1 }).limit(5).pretty()" -ForegroundColor Gray

Write-Host "`n6. Check users by department:" -ForegroundColor Cyan
Write-Host "   db.getCollection(`"@users`").aggregate([ { `$group: { _id: `$department, count: { `$sum: 1 } } } ]).pretty()" -ForegroundColor Gray

Write-Host "`n7. Check users with multiple groups:" -ForegroundColor Cyan
Write-Host "   db.getCollection(`"@users`").find({ `$expr: { `$gt: [ { `$size: `$groups }, 1 ] } }).count()" -ForegroundColor Gray

# Summary
Write-Host "`n=== Test Summary ===" -ForegroundColor Green
Write-Host "✅ Groups Created: $($testResults.GroupsCreated)" -ForegroundColor Green
Write-Host "✅ Users Created: $($testResults.UsersCreated)" -ForegroundColor Green
Write-Host "✅ Users Updated: $($testResults.UsersUpdated)" -ForegroundColor Green
Write-Host "✅ Groups Created: $($testResults.GroupsCreated)" -ForegroundColor Green

if ($testResults.Errors.Count -gt 0) {
    Write-Host "`n⚠️ Errors:" -ForegroundColor Yellow
    foreach ($error in $testResults.Errors) {
        Write-Host "   - $error" -ForegroundColor Red
    }
} else {
    Write-Host "✅ No errors!" -ForegroundColor Green
}

Write-Host "`n📊 Statistics:" -ForegroundColor Cyan
Write-Host "   Total Groups: $($groups.Count)" -ForegroundColor Cyan
Write-Host "   Total Users: $($users.Count)" -ForegroundColor Cyan
Write-Host "   Users with Groups: $(($users | Where-Object { $_.groups.Count -gt 0 }).Count)" -ForegroundColor Cyan
Write-Host "   Users without Groups: $(($users | Where-Object { $_.groups.Count -eq 0 }).Count)" -ForegroundColor Cyan

Write-Host "`n=== Test Complete ===" -ForegroundColor Green

