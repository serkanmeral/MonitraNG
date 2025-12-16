# Create "users" group for ebebek domain
Write-Host "`n=== Create 'users' Group for ebebek Domain ===" -ForegroundColor Green

$baseUrl = "https://localhost:5001/api"

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

# Check if "users" group already exists
Write-Host "`n[1/2] Checking if 'users' group exists..." -ForegroundColor Cyan
try {
    $groupsResponse = Invoke-RestMethod -Uri "$baseUrl/group" -Method GET -Headers $headers -SkipCertificateCheck
    $usersGroup = $groupsResponse.groups | Where-Object { $_.name -eq "users" } | Select-Object -First 1
    
    if ($usersGroup) {
        Write-Host "✅ 'users' group already exists: $($usersGroup.groupId)" -ForegroundColor Green
        Write-Host "   Name: $($usersGroup.name)" -ForegroundColor Cyan
        Write-Host "   Description: $($usersGroup.description)" -ForegroundColor Cyan
        exit 0
    }
} catch {
    Write-Host "⚠️  Error checking groups: $($_.Exception.Message)" -ForegroundColor Yellow
}

# Create "users" group
Write-Host "`n[2/2] Creating 'users' group..." -ForegroundColor Cyan
$groupBody = @{
    name = "users"
    description = "Standard users group"
    permissions = @()
    isActive = $true
    customData = $null
} | ConvertTo-Json

try {
    $createResponse = Invoke-RestMethod -Uri "$baseUrl/group" -Method POST -Headers $headers -Body $groupBody -ContentType "application/json" -SkipCertificateCheck
    
    if ($createResponse.isSuccess) {
        Write-Host "✅ 'users' group created successfully!" -ForegroundColor Green
        Write-Host "   Group ID: $($createResponse.groupId)" -ForegroundColor Cyan
        Write-Host "   Name: $($createResponse.name)" -ForegroundColor Cyan
        Write-Host "   Description: $($createResponse.description)" -ForegroundColor Cyan
    } else {
        Write-Host "❌ Failed to create group: $($createResponse.errorMessage)" -ForegroundColor Red
    }
} catch {
    Write-Host "❌ Error creating group: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.ErrorDetails) {
        Write-Host "   Error Details: $($_.ErrorDetails.Message)" -ForegroundColor Yellow
    }
}

Write-Host "`n=== Complete ===" -ForegroundColor Green

