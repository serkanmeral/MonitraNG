# Check Token Domain Script
# Decodes JWT token to see domain information

$tokenFile = "$env:TEMP\serkan_token.txt"

if (-not (Test-Path $tokenFile)) {
    Write-Host "❌ Token bulunamadı!" -ForegroundColor Red
    exit 1
}

$token = Get-Content $tokenFile -Raw | ForEach-Object { $_.Trim() }

Write-Host "`n🔍 Token Domain Bilgisi`n" -ForegroundColor Cyan

# JWT token'ı decode et (basit - sadece payload kısmı)
$parts = $token.Split('.')
if ($parts.Length -ge 2) {
    $payload = $parts[1]
    
    # Base64 decode (padding ekle gerekirse)
    $mod = $payload.Length % 4
    if ($mod -gt 0) {
        $payload += "=" * (4 - $mod)
    }
    
    try {
        $bytes = [System.Convert]::FromBase64String($payload)
        $json = [System.Text.Encoding]::UTF8.GetString($bytes)
        $claims = $json | ConvertFrom-Json
        
        Write-Host "Token Claims:" -ForegroundColor Yellow
        Write-Host "  domain_name: $($claims.domain_name)" -ForegroundColor White
        Write-Host "  domain_id: $($claims.domain_id)" -ForegroundColor White
        Write-Host "  preferred_username: $($claims.preferred_username)" -ForegroundColor White
        Write-Host "  email: $($claims.email)" -ForegroundColor White
        Write-Host ""
        
        Write-Host "✅ Domain: $($claims.domain_name)" -ForegroundColor Green
        Write-Host "   Bu domain'de dataset'ler aranacak: mng_$($claims.domain_name)" -ForegroundColor Gray
        
    } catch {
        Write-Host "❌ Token decode edilemedi: $($_.Exception.Message)" -ForegroundColor Red
    }
} else {
    Write-Host "❌ Geçersiz token formatı" -ForegroundColor Red
}

Write-Host ""

