# Test script for aggregate endpoint
# Tests: POST /api/data/tst_books/aggregate

$baseUrl = "https://localhost:5010"
$datasetName = "tst_books"

# Token'ı yükle (ortak script kullanarak)
$scriptPath = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $PSCommandPath }
if ([string]::IsNullOrEmpty($scriptPath)) {
    $scriptPath = Get-Location
}
$loadTokenScript = Join-Path $scriptPath "..\auth\load-token.ps1"

if (-not (Test-Path $loadTokenScript)) {
    Write-Host "❌ load-token.ps1 bulunamadı! Path: $loadTokenScript" -ForegroundColor Red
    exit 1
}

$token = & $loadTokenScript

if ([string]::IsNullOrEmpty($token)) {
    Write-Host "❌ Token alınamadı! Testler durduruluyor." -ForegroundColor Red
    exit 1
}

$tokenFile = "$env:TEMP\serkan_token.txt"

$headers = @{
    "Authorization" = "Bearer $token"
    "Content-Type" = "application/json"
}

# Ignore SSL certificate errors
[System.Net.ServicePointManager]::ServerCertificateValidationCallback = {$true}

Write-Host "🔍 Testing Aggregate Endpoint" -ForegroundColor Cyan
Write-Host "=" * 60 -ForegroundColor Gray
Write-Host ""

# Test: Aggregate pipeline
# Pipeline: price > 20 olanların 5 tanesini getir, title'a göre sırala
Write-Host "📋 Test: POST /api/data/tst_books/aggregate" -ForegroundColor Yellow
Write-Host "   Pipeline:" -ForegroundColor Gray
Write-Host "     1. `$match: { price: { `$gt: 20 } }" -ForegroundColor Gray
Write-Host "     2. `$sort: { title: 1 }" -ForegroundColor Gray
Write-Host "     3. `$limit: 5" -ForegroundColor Gray
Write-Host ""

try {
    # Aggregate pipeline oluştur
    $pipeline = @(
        @{
            '$match' = @{
                price = @{
                    '$gt' = 20
                }
            }
        },
        @{
            '$sort' = @{
                title = 1
            }
        },
        @{
            '$limit' = 5
        }
    )
    
    $requestBody = @{
        pipeline = $pipeline
    } | ConvertTo-Json -Depth 10
    
    $url = "$baseUrl/api/data/$datasetName/aggregate"
    
    Write-Host "   URL: $url" -ForegroundColor DarkGray
    Write-Host "   Body:" -ForegroundColor DarkGray
    Write-Host ($requestBody | ConvertFrom-Json | ConvertTo-Json -Depth 10) -ForegroundColor DarkGray
    Write-Host ""
    
    $response = Invoke-RestMethod -Uri $url -Method POST -Headers $headers -Body $requestBody -SkipCertificateCheck
    
    if ($response -is [System.Array]) {
        $count = $response.Count
        Write-Host "   ✅ Başarılı! $count kayıt bulundu" -ForegroundColor Green
        Write-Host ""
        
        if ($count -gt 0) {
            Write-Host "   📊 Sonuçlar (title'a göre sıralı, price > 20):" -ForegroundColor Cyan
            $response | ForEach-Object {
                $title = if ($_.title) { $_.title } else { "N/A" }
                $price = if ($_.price) { $_.price } else { "N/A" }
                $isbn = if ($_.isbn) { $_.isbn } else { "N/A" }
                Write-Host "      - $title (Price: $price, ISBN: $isbn)" -ForegroundColor White
            }
            Write-Host ""
            
            # Doğrulama: Tüm sonuçlar price > 20 olmalı
            $invalidResults = $response | Where-Object { $_.price -and $_.price -le 20 }
            if ($invalidResults.Count -gt 0) {
                Write-Host "   ⚠️  UYARI: Bazı sonuçlar price <= 20!" -ForegroundColor Yellow
                $invalidResults | ForEach-Object {
                    Write-Host "      - $($_.title) (Price: $($_.price))" -ForegroundColor Yellow
                }
            } else {
                Write-Host "   ✅ Doğrulama: Tüm sonuçlar price > 20" -ForegroundColor Green
            }
            
            # Doğrulama: Sıralama kontrolü (title'a göre ascending)
            $titles = $response | Where-Object { $_.title } | ForEach-Object { $_.title }
            if ($titles.Count -gt 1) {
                $isSorted = $true
                for ($i = 0; $i -lt $titles.Count - 1; $i++) {
                    if ($titles[$i] -gt $titles[$i + 1]) {
                        $isSorted = $false
                        break
                    }
                }
                if ($isSorted) {
                    Write-Host "   ✅ Doğrulama: Sonuçlar title'a göre sıralı (ascending)" -ForegroundColor Green
                } else {
                    Write-Host "   ⚠️  UYARI: Sonuçlar title'a göre sıralı değil!" -ForegroundColor Yellow
                }
            }
            
            # Doğrulama: Limit kontrolü (max 5 kayıt)
            if ($count -le 5) {
                Write-Host "   ✅ Doğrulama: Limit kontrolü başarılı (max 5 kayıt)" -ForegroundColor Green
            } else {
                Write-Host "   ⚠️  UYARI: Limit aşıldı! ($count kayıt, beklenen: max 5)" -ForegroundColor Yellow
            }
        } else {
            Write-Host "   ℹ️  Hiç kayıt bulunamadı (price > 20 olan kitap yok)" -ForegroundColor Gray
        }
    } else {
        Write-Host "   ⚠️  Beklenmeyen response formatı" -ForegroundColor Yellow
        Write-Host "   Response:" -ForegroundColor Gray
        $response | ConvertTo-Json -Depth 3
    }
} catch {
    Write-Host "   ❌ Hata: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.ErrorDetails.Message) {
        Write-Host "   Details: $($_.ErrorDetails.Message)" -ForegroundColor Gray
        try {
            $errorDetails = $_.ErrorDetails.Message | ConvertFrom-Json
            if ($errorDetails.error) {
                Write-Host "   Error Code: $($errorDetails.error.code)" -ForegroundColor Gray
                Write-Host "   Error Message: $($errorDetails.error.message)" -ForegroundColor Gray
            }
        } catch {
            # JSON parse hatası, sadece mesajı göster
        }
    }
}

Write-Host ""
Write-Host "=" * 60 -ForegroundColor Gray
Write-Host ""
Write-Host "✅ Test tamamlandı!" -ForegroundColor Green

