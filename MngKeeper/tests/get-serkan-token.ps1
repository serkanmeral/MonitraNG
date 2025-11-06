# Get Serkan's JWT Token for Seven Domain
# Usage: .\get-serkan-token.ps1

Write-Host "`n=== Getting Serkan's Token (Seven Domain) ===" -ForegroundColor Green

$tokenBody = @{
    username = "serkan"
    password = "Serkan123!"
    domain = "seven"
} | ConvertTo-Json

try {
    $tokenResponse = Invoke-RestMethod -Uri "https://localhost:5001/api/auth/token" `
      -Method POST `
      -Body $tokenBody `
      -ContentType "application/json" `
      -SkipCertificateCheck

    $token = $tokenResponse.accessToken
    $global:serkanToken = $token

    # Token'ı dosyaya kaydet
    $token | Out-File -FilePath "$env:TEMP\serkan_token.txt" -NoNewline -Encoding ASCII

    Write-Host "`n✅ Token Retrieved Successfully!" -ForegroundColor Green
    Write-Host "Token Length: $($token.Length) characters" -ForegroundColor Cyan
    Write-Host "Expires In: $($tokenResponse.expiresIn) seconds" -ForegroundColor Yellow
    Write-Host "Refresh Expires In: $($tokenResponse.refreshExpiresIn) seconds" -ForegroundColor Yellow

    # Decode token claims
    $tokenParts = $token.Split('.')
    $payload = $tokenParts[1]
    while ($payload.Length % 4 -ne 0) {
        $payload += "="
    }
    $payloadJson = [System.Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($payload))
    $claims = $payloadJson | ConvertFrom-Json

    Write-Host "`n📋 Token Claims:" -ForegroundColor Magenta
    Write-Host "  Username: $($claims.preferred_username)" -ForegroundColor Cyan
    Write-Host "  Email: $($claims.email)" -ForegroundColor Cyan
    Write-Host "  Full Name: $($claims.given_name) $($claims.family_name)" -ForegroundColor Cyan
    Write-Host "  Domain: $($claims.domain_name)" -ForegroundColor Yellow
    Write-Host "  Is Admin: $($claims.isAdmin)" -ForegroundColor Green
    Write-Host "  Groups: $($claims.user_groups -join ', ')" -ForegroundColor Green
    Write-Host "  Database: mng_$($claims.domain_name)" -ForegroundColor Yellow

    Write-Host "`n💾 Files:" -ForegroundColor Gray
    Write-Host "  Token: $env:TEMP\serkan_token.txt" -ForegroundColor Gray
    Write-Host "`n💡 Usage:" -ForegroundColor Gray
    Write-Host '  $token = Get-Content "$env:TEMP\serkan_token.txt" -Raw' -ForegroundColor Gray
    Write-Host '  OR: Use $global:serkanToken variable' -ForegroundColor Gray

    return $token
}
catch {
    Write-Host "`n❌ Failed to get token!" -ForegroundColor Red
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Yellow
    return $null
}

