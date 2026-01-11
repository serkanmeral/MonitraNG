# Refresh Token Test Script
# Tests the refresh token endpoint and decodes the new access token

param(
    [string]$KeeperBaseUrl = "https://localhost:5001",
    [string]$DomainName = "meral",
    [string]$RefreshToken = $null,
    [string]$TokenFile = "$env:TEMP\serkan_token.txt"
)

# Comprehensive SSL/TLS fixes
[System.Net.ServicePointManager]::ServerCertificateValidationCallback = {$true}

# Enable all TLS protocols
try {
    [System.Net.ServicePointManager]::SecurityProtocol = [System.Net.SecurityProtocolType]::Tls12 -bor [System.Net.SecurityProtocolType]::Tls11 -bor [System.Net.SecurityProtocolType]::Tls
} catch {
    try {
        [System.Net.ServicePointManager]::SecurityProtocol = [System.Net.SecurityProtocolType]::Tls12
    } catch {
        # Use default if TLS12 not available
    }
}

Write-Host ""
Write-Host "🔄 Refresh Token Test İşlemi" -ForegroundColor Cyan
Write-Host "   Keeper URL: $KeeperBaseUrl" -ForegroundColor Gray
Write-Host "   Domain: $DomainName" -ForegroundColor Gray
Write-Host ""

# Refresh token'ı al
if ([string]::IsNullOrEmpty($RefreshToken)) {
    # Önce access token al ve refresh token'ı çıkar
    Write-Host "📥 İlk token alınıyor..." -ForegroundColor Yellow
    
    try {
        $requestBody = @{
            username = "serkan.meral"
            password = "Serkan123!"
            domain = $DomainName
        } | ConvertTo-Json

        $params = @{
            Uri = "$KeeperBaseUrl/api/auth/token"
            Method = "POST"
            ContentType = "application/json"
            Body = $requestBody
            ErrorAction = "Stop"
        }
        
        if (Get-Command Invoke-RestMethod | Select-Object -ExpandProperty Parameters | Where-Object { $_.Name -eq "SkipCertificateCheck" }) {
            $params.SkipCertificateCheck = $true
        }
        
        $tokenResponse = Invoke-RestMethod @params
        
        if ([string]::IsNullOrEmpty($tokenResponse.refreshToken)) {
            Write-Host "❌ Refresh token alınamadı!" -ForegroundColor Red
            exit 1
        }
        
        $RefreshToken = $tokenResponse.refreshToken
        Write-Host "✅ Refresh token alındı" -ForegroundColor Green
        Write-Host "   Refresh Token (ilk 50 karakter): $($RefreshToken.Substring(0, [Math]::Min(50, $RefreshToken.Length)))..." -ForegroundColor Gray
        
    } catch {
        Write-Host "❌ Token alma hatası: $($_.Exception.Message)" -ForegroundColor Red
        exit 1
    }
}

Write-Host ""
Write-Host "🔄 Refresh token ile yeni access token alınıyor..." -ForegroundColor Yellow

try {
    $refreshRequestBody = @{
        refreshToken = $RefreshToken
        domain = $DomainName
    } | ConvertTo-Json

    $params = @{
        Uri = "$KeeperBaseUrl/api/auth/refresh"
        Method = "POST"
        ContentType = "application/json"
        Body = $refreshRequestBody
        ErrorAction = "Stop"
    }
    
    if (Get-Command Invoke-RestMethod | Select-Object -ExpandProperty Parameters | Where-Object { $_.Name -eq "SkipCertificateCheck" }) {
        $params.SkipCertificateCheck = $true
    }
    
    $refreshResponse = Invoke-RestMethod @params

    if ([string]::IsNullOrEmpty($refreshResponse.accessToken)) {
        Write-Host "❌ Yeni access token alınamadı!" -ForegroundColor Red
        Write-Host "Response: $($refreshResponse | ConvertTo-Json -Depth 5)" -ForegroundColor Gray
        exit 1
    }

    $newAccessToken = $refreshResponse.accessToken
    Write-Host "✅ Yeni access token alındı!" -ForegroundColor Green
    Write-Host ""
    
    # Token'ı dosyaya kaydet
    $newAccessToken | Out-File -FilePath $TokenFile -Encoding utf8 -NoNewline
    Write-Host "💾 Token kaydedildi: $TokenFile" -ForegroundColor Green
    Write-Host ""
    
    # Token'ı decode et
    Write-Host "🔍 Yeni token decode ediliyor..." -ForegroundColor Yellow
    Write-Host ""
    
    $parts = $newAccessToken.Split('.')
    if ($parts.Length -ge 2) {
        $payload = $parts[1]
        $mod = $payload.Length % 4
        if ($mod -gt 0) {
            $payload += "=" * (4 - $mod)
        }
        
        try {
            $payloadBytes = [System.Convert]::FromBase64String($payload.Replace('-', '+').Replace('_', '/'))
            $payloadJson = [System.Text.Encoding]::UTF8.GetString($payloadBytes)
            $claims = $payloadJson | ConvertFrom-Json
            
            Write-Host "📋 TOKEN CLAIMS:" -ForegroundColor Cyan
            Write-Host ""
            
            # domain_name kontrolü
            if ($claims.domain_name) {
                Write-Host "  ✅ domain_name: $($claims.domain_name)" -ForegroundColor Green
            } else {
                Write-Host "  ❌ domain_name: BULUNAMADI!" -ForegroundColor Red
                Write-Host "     ⚠️  Bu bir sorun! Refresh token endpoint'i domain_name claim'ini eklememiş." -ForegroundColor Red
            }
            
            if ($claims.domain_id) {
                Write-Host "  ✅ domain_id: $($claims.domain_id)" -ForegroundColor Green
            }
            
            if ($claims.domain_realm) {
                Write-Host "  ✅ domain_realm: $($claims.domain_realm)" -ForegroundColor Green
            }
            
            if ($claims.preferred_username) {
                Write-Host "  ✅ preferred_username: $($claims.preferred_username)" -ForegroundColor Green
            }
            
            if ($claims.is_admin) {
                Write-Host "  ✅ is_admin: $($claims.is_admin)" -ForegroundColor Green
            }
            
            if ($claims.is_manager) {
                Write-Host "  ✅ is_manager: $($claims.is_manager)" -ForegroundColor Green
            }
            
            if ($claims.user_groups) {
                Write-Host "  ✅ user_groups: $($claims.user_groups -join ', ')" -ForegroundColor Green
            }
            
            Write-Host ""
            Write-Host "📄 Tüm Claims (JSON):" -ForegroundColor Cyan
            Write-Host ($claims | ConvertTo-Json -Depth 10) -ForegroundColor White
            
        } catch {
            Write-Host "❌ Token decode edilemedi: $($_.Exception.Message)" -ForegroundColor Red
        }
    }
    
    Write-Host ""
    Write-Host "✅ Test tamamlandı!" -ForegroundColor Green
    Write-Host ""

} catch {
    Write-Host "❌ Refresh token hatası!" -ForegroundColor Red
    Write-Host "   Hata: $($_.Exception.Message)" -ForegroundColor Red
    
    if ($_.Exception.Response) {
        $statusCode = $_.Exception.Response.StatusCode.value__
        Write-Host "   Status Code: $statusCode" -ForegroundColor Red
        
        try {
            $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
            $responseBody = $reader.ReadToEnd()
            Write-Host "   Response Body: $responseBody" -ForegroundColor Gray
        } catch {
            # Ignore stream read errors
        }
    }
    
    Write-Host ""
    exit 1
}
