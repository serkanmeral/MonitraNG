# Get Serkan Token for meral domain
$baseUrl = "https://localhost:5001"
$domainName = "meral"
$username = "serkan.meral"
$password = "Serkan123!"

# Skip certificate validation for localhost
[System.Net.ServicePointManager]::ServerCertificateValidationCallback = {$true}

Write-Host "Getting token for $username in domain $domainName..." -ForegroundColor Yellow

try {
    $tokenResponse = Invoke-RestMethod -Uri "$baseUrl/api/auth/token" `
        -Method POST `
        -ContentType "application/json" `
        -Body (@{
            username = $username
            password = $password
            domain = $domainName
        } | ConvertTo-Json) `
        -SkipCertificateCheck `
        -ErrorAction Stop

    $accessToken = $tokenResponse.accessToken
    
    if ([string]::IsNullOrEmpty($accessToken)) {
        Write-Host "✗ Token alınamadı" -ForegroundColor Red
        exit 1
    }

    # Save token to temp file
    $tokenFile = "$env:TEMP\serkan_token.txt"
    Set-Content -Path $tokenFile -Value $accessToken
    
    Write-Host "✓ Token başarıyla alındı ve kaydedildi: $tokenFile" -ForegroundColor Green
    Write-Host "Token: $($accessToken.Substring(0, [Math]::Min(50, $accessToken.Length)))..." -ForegroundColor Gray
} catch {
    Write-Host "✗ Token alma hatası: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.ErrorDetails.Message) {
        Write-Host "Details: $($_.ErrorDetails.Message)" -ForegroundColor Gray
    }
    exit 1
}

