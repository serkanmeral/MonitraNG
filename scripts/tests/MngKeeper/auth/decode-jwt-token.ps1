# JWT Token Decode Script
# Decodes JWT token to see all claims including domain_name

param(
    [string]$Token = $null,
    [string]$TokenFile = "$env:TEMP\serkan_token.txt"
)

# Comprehensive SSL/TLS fixes
[System.Net.ServicePointManager]::ServerCertificateValidationCallback = {$true}

Write-Host ""
Write-Host "🔍 JWT Token Decode İşlemi" -ForegroundColor Cyan
Write-Host ""

# Token'ı al
if ([string]::IsNullOrEmpty($Token)) {
    if (Test-Path $TokenFile) {
        $Token = Get-Content $TokenFile -Raw | ForEach-Object { $_.Trim() }
        Write-Host "✅ Token dosyadan yüklendi: $TokenFile" -ForegroundColor Green
    } else {
        Write-Host "❌ Token bulunamadı!" -ForegroundColor Red
        Write-Host "   Kullanım: .\decode-jwt-token.ps1 -Token 'your-token-here'" -ForegroundColor Yellow
        Write-Host "   Veya token dosyası: $TokenFile" -ForegroundColor Yellow
        exit 1
    }
}

if ([string]::IsNullOrEmpty($Token)) {
    Write-Host "❌ Token boş!" -ForegroundColor Red
    exit 1
}

Write-Host "Token (ilk 50 karakter): $($Token.Substring(0, [Math]::Min(50, $Token.Length)))..." -ForegroundColor Gray
Write-Host ""

# JWT token'ı decode et
$parts = $Token.Split('.')
if ($parts.Length -lt 3) {
    Write-Host "❌ Geçersiz JWT token formatı (3 parça olmalı: header.payload.signature)" -ForegroundColor Red
    exit 1
}

# Header decode
Write-Host "📋 HEADER:" -ForegroundColor Yellow
try {
    $header = $parts[0]
    $mod = $header.Length % 4
    if ($mod -gt 0) {
        $header += "=" * (4 - $mod)
    }
    $headerBytes = [System.Convert]::FromBase64String($header.Replace('-', '+').Replace('_', '/'))
    $headerJson = [System.Text.Encoding]::UTF8.GetString($headerBytes)
    $headerObj = $headerJson | ConvertFrom-Json
    Write-Host ($headerObj | ConvertTo-Json -Depth 10) -ForegroundColor White
} catch {
    Write-Host "❌ Header decode edilemedi: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""

# Payload decode
Write-Host "📋 PAYLOAD (Claims):" -ForegroundColor Yellow
try {
    $payload = $parts[1]
    $mod = $payload.Length % 4
    if ($mod -gt 0) {
        $payload += "=" * (4 - $mod)
    }
    $payloadBytes = [System.Convert]::FromBase64String($payload.Replace('-', '+').Replace('_', '/'))
    $payloadJson = [System.Text.Encoding]::UTF8.GetString($payloadBytes)
    $claims = $payloadJson | ConvertFrom-Json
    
    # Tüm claims'i göster
    Write-Host ($claims | ConvertTo-Json -Depth 10) -ForegroundColor White
    Write-Host ""
    
    # Önemli claim'leri vurgula
    Write-Host "🔑 ÖNEMLİ CLAIMS:" -ForegroundColor Cyan
    if ($claims.domain_name) {
        Write-Host "  ✅ domain_name: $($claims.domain_name)" -ForegroundColor Green
    } else {
        Write-Host "  ❌ domain_name: BULUNAMADI!" -ForegroundColor Red
    }
    
    if ($claims.domain_id) {
        Write-Host "  ✅ domain_id: $($claims.domain_id)" -ForegroundColor Green
    } else {
        Write-Host "  ⚠️  domain_id: Bulunamadı" -ForegroundColor Yellow
    }
    
    if ($claims.domain_realm) {
        Write-Host "  ✅ domain_realm: $($claims.domain_realm)" -ForegroundColor Green
    }
    
    if ($claims.preferred_username) {
        Write-Host "  ✅ preferred_username: $($claims.preferred_username)" -ForegroundColor Green
    }
    
    if ($claims.email) {
        Write-Host "  ✅ email: $($claims.email)" -ForegroundColor Green
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
    
    # Expiration bilgisi
    if ($claims.exp) {
        $expDate = [DateTimeOffset]::FromUnixTimeSeconds($claims.exp).DateTime
        $now = [DateTime]::UtcNow
        $remaining = $expDate - $now
        Write-Host ""
        Write-Host "⏰ Token Expiration:" -ForegroundColor Cyan
        Write-Host "  Expires At: $($expDate.ToString('yyyy-MM-dd HH:mm:ss UTC'))" -ForegroundColor White
        Write-Host "  Remaining: $($remaining.TotalMinutes.ToString('F2')) dakika" -ForegroundColor White
    }
    
} catch {
    Write-Host "❌ Payload decode edilemedi: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "   Payload: $payload" -ForegroundColor Gray
}

Write-Host ""
Write-Host "✅ Decode işlemi tamamlandı" -ForegroundColor Green
Write-Host ""
