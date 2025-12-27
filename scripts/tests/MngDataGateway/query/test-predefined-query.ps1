# Test script for predefined queries endpoint
# Tests: POST /api/data/tst_books/queries/books_by_publication_date_range

$baseUrl = "https://localhost:5010"
$datasetName = "tst_books"
$queryName = "books_by_publication_date_range"

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

Write-Host "🔍 Testing Predefined Query Endpoint" -ForegroundColor Cyan
Write-Host "=" * 60 -ForegroundColor Gray
Write-Host ""

# Test: books_by_publication_date_range query
Write-Host "📋 Test: POST /api/data/$datasetName/queries/$queryName" -ForegroundColor Yellow
Write-Host "   Query: books_by_publication_date_range" -ForegroundColor Gray
Write-Host "   Parameters: startDate, endDate" -ForegroundColor Gray
Write-Host "   Description: Get books published between two dates" -ForegroundColor Gray
Write-Host ""

# Test 1: Belirli bir tarih aralığı ile test
Write-Host "   Test 1: 2025-01-01 ile 2025-12-31 arası kitaplar" -ForegroundColor Cyan
Write-Host ""

try {
    # Query parametreleri - String olarak gönder (backend DateTime'a çevirecek)
    $queryParams = @{
        startDate = "2025-01-01T00:00:00Z"
        endDate = "2025-12-31T23:59:59Z"
    }
    
    $requestBody = $queryParams | ConvertTo-Json
    
    $url = "$baseUrl/api/data/$datasetName/queries/$queryName"
    
    Write-Host "   URL: $url" -ForegroundColor DarkGray
    Write-Host "   Body: $requestBody" -ForegroundColor DarkGray
    Write-Host ""
    
    $response = Invoke-RestMethod -Uri $url -Method POST -Headers $headers -Body $requestBody -SkipCertificateCheck
    
    if ($response -is [System.Array]) {
        $count = $response.Count
        Write-Host "   ✅ Başarılı! $count kayıt bulundu" -ForegroundColor Green
        Write-Host ""
        
        if ($count -gt 0) {
            Write-Host "   📊 İlk 5 kayıt:" -ForegroundColor Cyan
            $response | Select-Object -First 5 | ForEach-Object {
                $title = if ($_.title) { $_.title } else { "N/A" }
                $pubDate = if ($_.publicationDate) { $_.publicationDate } else { "N/A" }
                $isbn = if ($_.isbn) { $_.isbn } else { "N/A" }
                Write-Host "      - $title" -ForegroundColor White
                Write-Host "        Publication Date: $pubDate, ISBN: $isbn" -ForegroundColor DarkGray
            }
            Write-Host ""
            
            # Doğrulama: Tüm sonuçlar tarih aralığında olmalı
            $invalidResults = $response | Where-Object { 
                $_.publicationDate -and (
                    $_.publicationDate -lt [DateTime]::Parse("2025-01-01T00:00:00Z") -or
                    $_.publicationDate -gt [DateTime]::Parse("2025-12-31T23:59:59Z")
                )
            }
            if ($invalidResults.Count -gt 0) {
                Write-Host "   ⚠️  UYARI: Bazı sonuçlar tarih aralığı dışında!" -ForegroundColor Yellow
                $invalidResults | ForEach-Object {
                    Write-Host "      - $($_.title) (Date: $($_.publicationDate))" -ForegroundColor Yellow
                }
            } else {
                Write-Host "   ✅ Doğrulama: Tüm sonuçlar belirtilen tarih aralığında" -ForegroundColor Green
            }
            
            # Doğrulama: Sıralama kontrolü (publicationDate descending, title ascending)
            $dates = $response | Where-Object { $_.publicationDate } | ForEach-Object { $_.publicationDate }
            if ($dates.Count -gt 1) {
                $isSorted = $true
                for ($i = 0; $i -lt $dates.Count - 1; $i++) {
                    # publicationDate descending kontrolü
                    if ($dates[$i] -lt $dates[$i + 1]) {
                        $isSorted = $false
                        break
                    }
                }
                if ($isSorted) {
                    Write-Host "   ✅ Doğrulama: Sonuçlar publicationDate'e göre sıralı (descending)" -ForegroundColor Green
                } else {
                    Write-Host "   ⚠️  UYARI: Sonuçlar publicationDate'e göre sıralı değil!" -ForegroundColor Yellow
                }
            }
        } else {
            Write-Host "   ℹ️  Hiç kayıt bulunamadı (belirtilen tarih aralığında kitap yok)" -ForegroundColor Gray
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

# Test 2: Daha dar bir tarih aralığı
Write-Host "   Test 2: 2025-06-01 ile 2025-06-30 arası kitaplar" -ForegroundColor Cyan
Write-Host ""

try {
    $queryParams = @{
        startDate = "2025-06-01T00:00:00Z"
        endDate = "2025-06-30T23:59:59Z"
    }
    
    $requestBody = $queryParams | ConvertTo-Json
    
    $url = "$baseUrl/api/data/$datasetName/queries/$queryName"
    
    $response = Invoke-RestMethod -Uri $url -Method POST -Headers $headers -Body $requestBody -SkipCertificateCheck
    
    if ($response -is [System.Array]) {
        $count = $response.Count
        Write-Host "      ✅ $count kayıt bulundu (2025-06-01 ile 2025-06-30 arası)" -ForegroundColor Green
    } else {
        Write-Host "      ⚠️  Beklenmeyen response formatı" -ForegroundColor Yellow
    }
} catch {
    Write-Host "      ❌ Hata: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""
Write-Host "=" * 60 -ForegroundColor Gray
Write-Host ""
Write-Host "✅ Test tamamlandı!" -ForegroundColor Green

