# Check if user is actually in groups
Write-Host "`n=== Check User Groups ===" -ForegroundColor Green

$baseUrl = "https://localhost:5001/api"
$username = "test.user.20251213155731"

# Get admin token
$adminTokenBody = @{
    username = "ebebek_admin"
    password = "Admin123!"
    domain = "ebebek"
} | ConvertTo-Json

$adminToken = (Invoke-RestMethod -Uri "$baseUrl/auth/token" -Method POST -Body $adminTokenBody -ContentType "application/json" -SkipCertificateCheck).accessToken

$headers = @{
    "Authorization" = "Bearer $adminToken"
    "Content-Type" = "application/json"
}

# Get user by username (we need to find the user ID first)
Write-Host "`n[1/3] Getting users list to find user..." -ForegroundColor Cyan
try {
    $usersResponse = Invoke-RestMethod -Uri "$baseUrl/user" -Method GET -Headers $headers -SkipCertificateCheck
    $user = $usersResponse.users | Where-Object { $_.username -eq $username } | Select-Object -First 1
    
    if ($user) {
        $userId = $user.userId
        Write-Host "✅ User found: $userId" -ForegroundColor Green
        Write-Host "   Username: $($user.username)" -ForegroundColor Cyan
        Write-Host "   Groups in MngKeeper DB: $($user.groups -join ', ')" -ForegroundColor Cyan
        Write-Host "   Groups Count: $($user.groups.Count)" -ForegroundColor Cyan
        
        if ($user.groups.Count -gt 0) {
            Write-Host "   ✅ User has groups in MngKeeper database" -ForegroundColor Green
        } else {
            Write-Host "   ❌ User has NO groups in MngKeeper database" -ForegroundColor Red
        }
    } else {
        Write-Host "❌ User not found" -ForegroundColor Red
        exit 1
    }
} catch {
    Write-Host "❌ Error: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# Check "users" group
Write-Host "`n[2/3] Checking 'users' group..." -ForegroundColor Cyan
try {
    $groupsResponse = Invoke-RestMethod -Uri "$baseUrl/group" -Method GET -Headers $headers -SkipCertificateCheck
    $usersGroup = $groupsResponse.groups | Where-Object { $_.name -eq "users" } | Select-Object -First 1
    
    if ($usersGroup) {
        Write-Host "✅ 'users' group found: $($usersGroup.groupId)" -ForegroundColor Green
        Write-Host "   Name: $($usersGroup.name)" -ForegroundColor Cyan
        
        if ($user.groups -contains $usersGroup.groupId) {
            Write-Host "   ✅ User IS in 'users' group (MngKeeper DB)" -ForegroundColor Green
        } else {
            Write-Host "   ❌ User is NOT in 'users' group (MngKeeper DB)" -ForegroundColor Red
        }
    } else {
        Write-Host "❌ 'users' group not found" -ForegroundColor Red
    }
} catch {
    Write-Host "❌ Error: $($_.Exception.Message)" -ForegroundColor Red
}

# Try to get a fresh token and check again
Write-Host "`n[3/3] Getting fresh token and parsing..." -ForegroundColor Cyan
$tokenBody = @{
    username = $username
    password = "Test123!"
    domain = "ebebek"
} | ConvertTo-Json

try {
    $tokenResponse = Invoke-RestMethod -Uri "$baseUrl/auth/token" -Method POST -Body $tokenBody -ContentType "application/json" -SkipCertificateCheck
    $token = $tokenResponse.accessToken
    
    $tokenParts = $token.Split('.')
    $payload = $tokenParts[1]
    while ($payload.Length % 4 -ne 0) { $payload += "=" }
    $json = [System.Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($payload.Replace('-', '+').Replace('_', '/')))
    $claims = $json | ConvertFrom-Json
    
    Write-Host "`n--- TOKEN CLAIMS ---" -ForegroundColor Yellow
    if ($claims.user_groups) {
        Write-Host "✅ user_groups: $($claims.user_groups)" -ForegroundColor Green
        if ($claims.user_groups -is [System.Array]) {
            Write-Host "   Groups: $($claims.user_groups -join ', ')" -ForegroundColor Cyan
        }
    } else {
        Write-Host "❌ user_groups: MISSING" -ForegroundColor Red
        Write-Host "   All claims in token:" -ForegroundColor Yellow
        $claims.PSObject.Properties | ForEach-Object {
            Write-Host "     $($_.Name): $($_.Value)" -ForegroundColor Gray
        }
    }
} catch {
    Write-Host "❌ Error getting token: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host "`n=== Check Complete ===" -ForegroundColor Green

