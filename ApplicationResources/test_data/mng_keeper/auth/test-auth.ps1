# Authentication Test Script
Write-Host "=== Authentication Test Script ===" -ForegroundColor Green

$baseUrl = "http://localhost:5001"

function Test-AuthEndpoint {
    param(
        [string]$Endpoint,
        [string]$JsonFile,
        [string]$Description,
        [hashtable]$Headers = @{}
    )

    Write-Host "`nTesting: $Description" -ForegroundColor Cyan
    Write-Host "Endpoint: $Endpoint" -ForegroundColor Gray
    Write-Host "JSON File: $JsonFile" -ForegroundColor Gray

    try {
        $jsonContent = Get-Content -Path $JsonFile -Raw
        $defaultHeaders = @{
            "Content-Type" = "application/json"
        }
        
        # Merge headers
        $finalHeaders = $defaultHeaders.Clone()
        foreach ($key in $Headers.Keys) {
            $finalHeaders[$key] = $Headers[$key]
        }

        $response = Invoke-RestMethod -Uri "$baseUrl$Endpoint" -Method POST -Headers $finalHeaders -Body $jsonContent -TimeoutSec 10

        Write-Host "✅ SUCCESS" -ForegroundColor Green
        Write-Host "Response: $($response | ConvertTo-Json -Depth 2)" -ForegroundColor Gray
        return $response
    }
    catch {
        Write-Host "❌ FAILED: $($_.Exception.Message)" -ForegroundColor Red
        return $null
    }
}

# Test 1: Login with username
Write-Host "`n=== Test 1: Login with Username ===" -ForegroundColor Yellow
$loginResult = Test-AuthEndpoint -Endpoint "/api/auth/login" -JsonFile "login-request.json" -Description "Login with Username"

# Test 2: Login with email
Write-Host "`n=== Test 2: Login with Email ===" -ForegroundColor Yellow
$loginEmailResult = Test-AuthEndpoint -Endpoint "/api/auth/login" -JsonFile "login-request-email.json" -Description "Login with Email"

# Test 3: Invalid Login (Expected Error)
Write-Host "`n=== Test 3: Invalid Login (Expected Error) ===" -ForegroundColor Yellow
Test-AuthEndpoint -Endpoint "/api/auth/login" -JsonFile "invalid-login.json" -Description "Invalid Login"

# Test 4: Token Validation (if we have a token)
Write-Host "`n=== Test 4: Token Validation ===" -ForegroundColor Yellow
if ($loginResult -and $loginResult.accessToken) {
    $authHeaders = @{
        "Authorization" = "Bearer $($loginResult.accessToken)"
    }
    Test-AuthEndpoint -Endpoint "/api/auth/validate" -JsonFile "validate-token.json" -Description "Token Validation" -Headers $authHeaders
} else {
    Write-Host "⚠️ SKIPPED: No access token available" -ForegroundColor Yellow
}

# Test 5: Refresh Token (if we have a refresh token)
Write-Host "`n=== Test 5: Refresh Token ===" -ForegroundColor Yellow
if ($loginResult -and $loginResult.refreshToken) {
    Test-AuthEndpoint -Endpoint "/api/auth/refresh" -JsonFile "refresh-token.json" -Description "Refresh Token"
} else {
    Write-Host "⚠️ SKIPPED: No refresh token available" -ForegroundColor Yellow
}

# Test 6: Logout (if we have a refresh token)
Write-Host "`n=== Test 6: Logout ===" -ForegroundColor Yellow
if ($loginResult -and $loginResult.refreshToken) {
    Test-AuthEndpoint -Endpoint "/api/auth/logout" -JsonFile "logout-request.json" -Description "Logout"
} else {
    Write-Host "⚠️ SKIPPED: No refresh token available" -ForegroundColor Yellow
}

# Test 7: Protected Endpoint Access (if we have a token)
Write-Host "`n=== Test 7: Protected Endpoint Access ===" -ForegroundColor Yellow
if ($loginResult -and $loginResult.accessToken) {
    try {
        $authHeaders = @{
            "Authorization" = "Bearer $($loginResult.accessToken)"
            "Content-Type" = "application/json"
        }
        
        $protectedResponse = Invoke-RestMethod -Uri "$baseUrl/api/domains" -Method GET -Headers $authHeaders -TimeoutSec 10
        
        Write-Host "✅ SUCCESS: Protected endpoint accessed" -ForegroundColor Green
        Write-Host "Response: $($protectedResponse | ConvertTo-Json -Depth 2)" -ForegroundColor Gray
    }
    catch {
        Write-Host "❌ FAILED: $($_.Exception.Message)" -ForegroundColor Red
    }
} else {
    Write-Host "⚠️ SKIPPED: No access token available" -ForegroundColor Yellow
}

Write-Host "`n=== Authentication Test Complete ===" -ForegroundColor Green
