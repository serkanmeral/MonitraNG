# Refresh Token Test Script
Write-Host "=== REFRESH TOKEN TEST ===" -ForegroundColor Cyan

$baseUrl = "http://localhost:5001"
$domain = "test-domain-2"

# 1. Get Initial Token
Write-Host "`n1. Getting initial token..." -ForegroundColor Yellow
$loginBody = @{
    username = "test-domain-2_admin"
    password = "Admin123!"
    domainName = $domain
} | ConvertTo-Json

try {
    $tokenResponse = Invoke-RestMethod -Uri "$baseUrl/api/auth/token" -Method POST -ContentType "application/json" -Body $loginBody
    
    Write-Host "✅ Token obtained successfully!" -ForegroundColor Green
    Write-Host "  Access Token (first 50 chars): $($tokenResponse.accessToken.Substring(0, 50))..." -ForegroundColor Gray
    Write-Host "  Refresh Token (first 50 chars): $($tokenResponse.refreshToken.Substring(0, [Math]::Min(50, $tokenResponse.refreshToken.Length)))..." -ForegroundColor Gray
    Write-Host "  Access Token expires in: $($tokenResponse.expiresIn) seconds" -ForegroundColor Gray
    Write-Host "  Refresh Token expires in: $($tokenResponse.refreshExpiresIn) seconds" -ForegroundColor Gray
    
    # 2. Refresh Token
    Write-Host "`n2. Refreshing token..." -ForegroundColor Yellow
    Start-Sleep -Seconds 2  # Wait a bit to simulate token usage
    
    $refreshBody = @{
        refreshToken = $tokenResponse.refreshToken
        domainName = $domain
    } | ConvertTo-Json
    
    $refreshResponse = Invoke-RestMethod -Uri "$baseUrl/api/auth/refresh" -Method POST -ContentType "application/json" -Body $refreshBody
    
    Write-Host "✅ Token refreshed successfully!" -ForegroundColor Green
    Write-Host "  New Access Token (first 50 chars): $($refreshResponse.accessToken.Substring(0, 50))..." -ForegroundColor Gray
    Write-Host "  New Refresh Token (first 50 chars): $($refreshResponse.refreshToken.Substring(0, [Math]::Min(50, $refreshResponse.refreshToken.Length)))..." -ForegroundColor Gray
    Write-Host "  New Access Token expires in: $($refreshResponse.expiresIn) seconds" -ForegroundColor Gray
    Write-Host "  New Refresh Token expires in: $($refreshResponse.refreshExpiresIn) seconds" -ForegroundColor Gray
    
    # 3. Use New Access Token (Make API Call)
    Write-Host "`n3. Using new access token to call API..." -ForegroundColor Yellow
    
    $headers = @{
        "Authorization" = "Bearer $($refreshResponse.accessToken)"
    }
    
    $domains = Invoke-RestMethod -Uri "$baseUrl/api/domain" -Method GET -Headers $headers
    Write-Host "✅ API call successful with refreshed token!" -ForegroundColor Green
    Write-Host "  Found $($domains.Count) domains" -ForegroundColor Gray
    
    # 4. Logout (Revoke Refresh Token)
    Write-Host "`n4. Logging out (revoking refresh token)..." -ForegroundColor Yellow
    
    $logoutBody = @{
        refreshToken = $refreshResponse.refreshToken
        domainName = $domain
    } | ConvertTo-Json
    
    $logoutResponse = Invoke-RestMethod -Uri "$baseUrl/api/auth/logout" -Method POST -ContentType "application/json" -Body $logoutBody
    
    Write-Host "✅ Logout successful!" -ForegroundColor Green
    Write-Host "  Message: $($logoutResponse.message)" -ForegroundColor Gray
    
    # 5. Try to use revoked refresh token (should fail)
    Write-Host "`n5. Attempting to use revoked refresh token (should fail)..." -ForegroundColor Yellow
    
    try {
        $revokedRefreshResponse = Invoke-RestMethod -Uri "$baseUrl/api/auth/refresh" -Method POST -ContentType "application/json" -Body $logoutBody
        Write-Host "❌ UNEXPECTED: Revoked token still works!" -ForegroundColor Red
    }
    catch {
        Write-Host "✅ Expected failure: Revoked token rejected" -ForegroundColor Green
    }
    
    Write-Host "`n🎉 ALL TESTS PASSED!" -ForegroundColor Green
}
catch {
    Write-Host "❌ TEST FAILED!" -ForegroundColor Red
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.ErrorDetails) {
        Write-Host "Details: $($_.ErrorDetails.Message)" -ForegroundColor Red
    }
}

