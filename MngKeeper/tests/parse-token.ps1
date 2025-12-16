# Parse and display token contents
Write-Host "`n=== Token Content Parser ===" -ForegroundColor Green

$baseUrl = "https://localhost:5001/api"

# Get token for ebebek domain (active user)
Write-Host "`n[1/2] Getting token for active user..." -ForegroundColor Cyan

$tokenBody = @{
    username = "active.user.20251213152130"
    password = "Test123!"
    domain = "ebebek"
} | ConvertTo-Json

try {
    $tokenResponse = Invoke-RestMethod -Uri "$baseUrl/auth/token" `
        -Method POST `
        -Body $tokenBody `
        -ContentType "application/json" `
        -SkipCertificateCheck

    $token = $tokenResponse.accessToken
    Write-Host "✅ Token retrieved successfully!" -ForegroundColor Green
} catch {
    Write-Host "❌ Error getting token: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "   Trying with admin user instead..." -ForegroundColor Yellow
    
    # Try with admin user
    $adminTokenBody = @{
        username = "ebebek_admin"
        password = "Admin123!"
        domain = "ebebek"
    } | ConvertTo-Json
    
    $tokenResponse = Invoke-RestMethod -Uri "$baseUrl/auth/token" `
        -Method POST `
        -Body $adminTokenBody `
        -ContentType "application/json" `
        -SkipCertificateCheck
    
    $token = $tokenResponse.accessToken
    Write-Host "✅ Admin token retrieved!" -ForegroundColor Green
}

# Parse token
Write-Host "`n[2/2] Parsing token content..." -ForegroundColor Cyan

$tokenParts = $token.Split('.')
if ($tokenParts.Length -ne 3) {
    Write-Host "❌ Invalid token format" -ForegroundColor Red
    exit 1
}

# Decode header
Write-Host "`n--- TOKEN HEADER ---" -ForegroundColor Yellow
$header = $tokenParts[0]
$headerPadded = $header
while ($headerPadded.Length % 4 -ne 0) { $headerPadded += "=" }
$headerBytes = [Convert]::FromBase64String($headerPadded.Replace('-', '+').Replace('_', '/'))
$headerJson = [System.Text.Encoding]::UTF8.GetString($headerBytes)
$headerObj = $headerJson | ConvertFrom-Json
$headerObj | ConvertTo-Json -Depth 10 | Write-Host

# Decode payload
Write-Host "`n--- TOKEN PAYLOAD ---" -ForegroundColor Yellow
$payload = $tokenParts[1]
$payloadPadded = $payload
while ($payloadPadded.Length % 4 -ne 0) { $payloadPadded += "=" }
$payloadBytes = [Convert]::FromBase64String($payloadPadded.Replace('-', '+').Replace('_', '/'))
$payloadJson = [System.Text.Encoding]::UTF8.GetString($payloadBytes)
$payloadObj = $payloadJson | ConvertFrom-Json

# Display payload in readable format
$payloadObj | ConvertTo-Json -Depth 10 | Write-Host

# Check for required claims
Write-Host "`n--- REQUIRED CLAIMS CHECK ---" -ForegroundColor Yellow

$requiredClaims = @{
    "preferred_username" = "Username"
    "email" = "Email"
    "domain_name" = "Domain Name"
    "domain_id" = "Domain ID (optional)"
    "isAdmin" = "Is Admin"
    "user_groups" = "User Groups"
}

$missingClaims = @()
$foundClaims = @{}

foreach ($claim in $requiredClaims.Keys) {
    $claimValue = $payloadObj.$claim
    if ($null -ne $claimValue) {
        $foundClaims[$claim] = $claimValue
        Write-Host "✅ $($requiredClaims[$claim]): $claimValue" -ForegroundColor Green
    } else {
        $missingClaims += $claim
        Write-Host "⚠️  $($requiredClaims[$claim]): MISSING" -ForegroundColor Yellow
    }
}

# Check for additional important claims
Write-Host "`n--- ADDITIONAL CLAIMS ---" -ForegroundColor Yellow
$additionalClaims = @("sub", "iss", "aud", "exp", "iat", "jti", "realm_access", "scope", "given_name", "family_name", "name")
foreach ($claim in $additionalClaims) {
    if ($payloadObj.$claim) {
        $value = $payloadObj.$claim
        if ($value -is [System.Array]) {
            $value = $value -join ", "
        }
        Write-Host "   $claim : $value" -ForegroundColor Cyan
    }
}

# Summary
Write-Host "`n--- SUMMARY ---" -ForegroundColor Yellow
Write-Host "Total claims found: $($payloadObj.PSObject.Properties.Count)" -ForegroundColor Cyan
Write-Host "Required claims present: $($foundClaims.Count)/$($requiredClaims.Count)" -ForegroundColor Cyan

if ($missingClaims.Count -gt 0) {
    Write-Host "`n⚠️  Missing claims:" -ForegroundColor Yellow
    foreach ($missing in $missingClaims) {
        Write-Host "   - $missing ($($requiredClaims[$missing]))" -ForegroundColor Yellow
    }
    Write-Host "`n💡 Recommendation: Check Keycloak protocol mappers for these claims" -ForegroundColor Cyan
} else {
    Write-Host "`n✅ All required claims are present!" -ForegroundColor Green
}

# Check token expiration
if ($payloadObj.exp) {
    $expDate = [DateTimeOffset]::FromUnixTimeSeconds($payloadObj.exp).DateTime
    $now = Get-Date
    $timeLeft = $expDate - $now
    Write-Host "`nToken expires: $expDate" -ForegroundColor Cyan
    Write-Host "Time remaining: $($timeLeft.TotalMinutes) minutes" -ForegroundColor Cyan
}

Write-Host "`n=== Analysis Complete ===" -ForegroundColor Green

