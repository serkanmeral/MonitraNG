# Get User Token Script
# Gets access token for a specific user in meral4 domain

$baseUrl = "https://localhost:5001"
$domainName = "meral8"
$username = "serkan.meral"
$password = "Serkan123!"  # Password from create-meral-domain.ps1

Write-Host "`n=== KULLANICI TOKEN ALMA ===" -ForegroundColor Cyan
Write-Host ""

# Skip certificate validation for localhost
[System.Net.ServicePointManager]::ServerCertificateValidationCallback = {$true}

Write-Host "Kullanıcı: $username" -ForegroundColor Yellow
Write-Host "Domain: $domainName" -ForegroundColor Yellow
Write-Host ""

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
        Write-Host "Response: $($tokenResponse | ConvertTo-Json -Depth 5)" -ForegroundColor Gray
        exit 1
    }

    Write-Host "✓ Token başarıyla alındı!" -ForegroundColor Green
    Write-Host ""
    Write-Host "=== ACCESS TOKEN ===" -ForegroundColor Cyan
    Write-Host $accessToken -ForegroundColor White
    Write-Host ""
    Write-Host "=== JWT.IO KULLANIMI ===" -ForegroundColor Cyan
    Write-Host "1. https://jwt.io adresine gidin" -ForegroundColor White
    Write-Host "2. Token'ı yapıştırın" -ForegroundColor White
    Write-Host "3. Signature verification için:" -ForegroundColor White
    Write-Host "   - get-realm-public-key.ps1 script'ini çalıştırarak realm'in public key'ini alın" -ForegroundColor Yellow
    Write-Host "   - jwt.io'da 'Verify Signature' bölümünde public key'i yapıştırın" -ForegroundColor Yellow
    Write-Host ""

} catch {
    Write-Host "✗ Hata: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.Exception.Response) {
        try {
            $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
            $responseBody = $reader.ReadToEnd()
            Write-Host "Response: $responseBody" -ForegroundColor Gray
        } catch {
            # Ignore
        }
    }
    exit 1
}

