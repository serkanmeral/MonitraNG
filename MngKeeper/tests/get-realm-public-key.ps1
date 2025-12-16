# Get Keycloak Realm Public Key for JWT Signature Verification
param(
    [string]$RealmName = "meral8",
    [string]$KeycloakBaseUrl = "http://localhost:8080"
)

Write-Host "`n=== KEYCLOAK REALM PUBLIC KEY ALMA ===" -ForegroundColor Cyan
Write-Host "Realm: $RealmName" -ForegroundColor Yellow
Write-Host "Keycloak URL: $KeycloakBaseUrl" -ForegroundColor Yellow
Write-Host ""

try {
    # Get JWKS (JSON Web Key Set) from Keycloak
    $jwksUrl = "$KeycloakBaseUrl/realms/$RealmName/protocol/openid-connect/certs"
    Write-Host "JWKS URL: $jwksUrl" -ForegroundColor Gray
    Write-Host ""
    
    $jwksResponse = Invoke-RestMethod -Uri $jwksUrl -Method GET -ErrorAction Stop
    
    Write-Host "✓ JWKS başarıyla alındı!" -ForegroundColor Green
    Write-Host ""
    
    # Display JWKS
    Write-Host "=== JWKS (JSON Web Key Set) ===" -ForegroundColor Cyan
    $jwksResponse | ConvertTo-Json -Depth 10 | Write-Host
    
    Write-Host ""
    Write-Host "=== JWT.IO KULLANIMI ===" -ForegroundColor Cyan
    Write-Host "1. https://jwt.io adresine gidin" -ForegroundColor White
    Write-Host "2. Token'ı yapıştırın" -ForegroundColor White
    Write-Host "3. Sağ tarafta 'Verify Signature' bölümünde:" -ForegroundColor White
    Write-Host "   - 'JWKS URL' seçeneğini seçin" -ForegroundColor Yellow
    Write-Host "   - URL olarak şunu girin: $jwksUrl" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "VEYA" -ForegroundColor Gray
    Write-Host ""
    
    # Extract signing key (use: "sig") and convert to PEM format
    $signingKey = $jwksResponse.keys | Where-Object { $_.use -eq "sig" -or ($_.use -eq $null -and $_.alg -like "RS*") } | Select-Object -First 1
    
    if ($signingKey -and $signingKey.x5c -and $signingKey.x5c.Count -gt 0) {
        $certBase64 = $signingKey.x5c[0]
        
        Write-Host "=== PUBLIC KEY (PEM FORMAT) ===" -ForegroundColor Cyan
        Write-Host "Key ID (kid): $($signingKey.kid)" -ForegroundColor White
        Write-Host "Algorithm: $($signingKey.alg)" -ForegroundColor White
        Write-Host ""
        
        # Convert base64 certificate to PEM format (64 characters per line)
        $certPem = "-----BEGIN CERTIFICATE-----`n"
        for ($i = 0; $i -lt $certBase64.Length; $i += 64) {
            $line = $certBase64.Substring($i, [Math]::Min(64, $certBase64.Length - $i))
            $certPem += $line + "`n"
        }
        $certPem += "-----END CERTIFICATE-----"
        
        Write-Host $certPem -ForegroundColor White
        Write-Host ""
        
        # Also try to extract public key from certificate (for jwt.io compatibility)
        try {
            $certBytes = [Convert]::FromBase64String($certBase64)
            $cert = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2(,$certBytes)
            $rsa = $cert.GetRSAPublicKey()
            
            if ($rsa) {
                # Export public key in PEM format (available in .NET 5+)
                $publicKeyBytes = $rsa.ExportSubjectPublicKeyInfo()
                $publicKeyBase64 = [Convert]::ToBase64String($publicKeyBytes)
                
                # Format as PEM
                $publicKeyPem = "-----BEGIN PUBLIC KEY-----`n"
                for ($i = 0; $i -lt $publicKeyBase64.Length; $i += 64) {
                    $line = $publicKeyBase64.Substring($i, [Math]::Min(64, $publicKeyBase64.Length - $i))
                    $publicKeyPem += $line + "`n"
                }
                $publicKeyPem += "-----END PUBLIC KEY-----"
                
                Write-Host "=== PUBLIC KEY (RSA PEM) ===" -ForegroundColor Cyan
                Write-Host "Not: jwt.io'da certificate yerine bu public key'i de kullanabilirsiniz" -ForegroundColor Gray
                Write-Host $publicKeyPem -ForegroundColor White
                Write-Host ""
            }
        } catch {
            Write-Host "⚠ Public key extraction hatası: $($_.Exception.Message)" -ForegroundColor Yellow
            Write-Host "Certificate formatı jwt.io'da da çalışır (yukarıdaki certificate'i kullanın)" -ForegroundColor Gray
            Write-Host ""
        }
        
        Write-Host "=== JWT.IO KULLANIMI (CERTIFICATE) ===" -ForegroundColor Cyan
        Write-Host "1. https://jwt.io adresine gidin" -ForegroundColor White
        Write-Host "2. Token'ı yapıştırın" -ForegroundColor White
        Write-Host "3. Sağ tarafta 'Verify Signature' bölümünde:" -ForegroundColor White
        Write-Host "   - 'Public Key' seçeneğini seçin" -ForegroundColor Yellow
        Write-Host "   - Yukarıdaki PEM formatındaki CERTIFICATE'i yapıştırın" -ForegroundColor Yellow
        Write-Host "   (jwt.io certificate formatını da kabul eder)" -ForegroundColor Gray
        Write-Host ""
    }
    else {
        Write-Host "⚠ Signing key bulunamadı veya x5c değeri yok" -ForegroundColor Yellow
    }
    
    Write-Host ""
    Write-Host "=== JWKS URL (Kopyala-Yapıştır) ===" -ForegroundColor Cyan
    Write-Host $jwksUrl -ForegroundColor White
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

