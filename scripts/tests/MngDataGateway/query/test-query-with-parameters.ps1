# Test Query with Parameters (New Format)
# Tests: POST /api/data/tst_books/queries/books_by_publication_date_range with datetime parameters

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

Write-Host "`n🧪 Testing Query with Parameters (New Format)`n" -ForegroundColor Cyan
Write-Host "=" * 60 -ForegroundColor Gray
Write-Host ""

# Step 1: Update query with parameter type definitions
Write-Host "📋 Step 1: Updating query with parameter type definitions..." -ForegroundColor Yellow
Write-Host ""

try {
    # Get current dataset schema
    $currentSchema = Invoke-RestMethod -Uri "$baseUrl/api/datasets/$datasetName" -Method GET -Headers $headers -SkipCertificateCheck
    
    # Update query with new parameter format (with type definitions)
    $updateData = @{
        Queries = @(
            @{
                name = "books_by_publication_date_range"
                description = "Get books published between two dates"
                parameters = @(
                    @{
                        name = "startDate"
                        type = "datetime"
                        required = $true
                        description = "Start date (ISO 8601 format)"
                    },
                    @{
                        name = "endDate"
                        type = "datetime"
                        required = $true
                        description = "End date (ISO 8601 format)"
                    }
                )
                pipeline = @(
                    @{
                        "`$match" = @{
                            publicationDate = @{
                                "`$gte" = ":startDate"
                                "`$lte" = ":endDate"
                            }
                        }
                    },
                    @{
                        "`$sort" = @{
                            publicationDate = -1
                            title = 1
                        }
                    }
                )
            }
        )
    } | ConvertTo-Json -Depth 20

    Write-Host "Query JSON:" -ForegroundColor Gray
    Write-Host $updateData -ForegroundColor DarkGray
    Write-Host ""

    # Update dataset
    $response = Invoke-RestMethod -Uri "$baseUrl/api/datasets/$datasetName" -Method PUT -Headers $headers -Body $updateData -SkipCertificateCheck
    
    Write-Host "✅ Query updated successfully with parameter type definitions!" -ForegroundColor Green
    Write-Host "   - Dataset: $($response.Name)" -ForegroundColor Gray
    Write-Host "   - Queries: $($response.QueriesCount)" -ForegroundColor Gray
    Write-Host ""
    
} catch {
    Write-Host "❌ Failed to update query: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.ErrorDetails.Message) {
        Write-Host "Details: $($_.ErrorDetails.Message)" -ForegroundColor Gray
    }
    exit 1
}

Write-Host "=" * 60 -ForegroundColor Gray
Write-Host ""

# Step 2: Test query with parameters
Write-Host "🧪 Step 2: Testing query with datetime parameters..." -ForegroundColor Yellow
Write-Host ""

# Test 1: 2025-01-01 ile 2025-12-31 arası kitaplar
Write-Host "   Test 1: 2025-01-01 ile 2025-12-31 arası kitaplar" -ForegroundColor Cyan
Write-Host ""

try {
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
                Write-Host "      - $title" -ForegroundColor White
                Write-Host "        Publication Date: $pubDate" -ForegroundColor DarkGray
            }
            Write-Host ""
            
            # Doğrulama: Tüm sonuçlar tarih aralığında olmalı
            $startDate = [DateTime]::Parse("2025-01-01T00:00:00Z")
            $endDate = [DateTime]::Parse("2025-12-31T23:59:59Z")
            
            $invalidResults = $response | Where-Object { 
                if ($_.publicationDate) {
                    $pubDate = if ($_.publicationDate -is [DateTime]) { 
                        $_.publicationDate 
                    } else { 
                        [DateTime]::Parse($_.publicationDate.ToString()) 
                    }
                    $pubDate -lt $startDate -or $pubDate -gt $endDate
                } else {
                    $false
                }
            }
            
            if ($invalidResults.Count -gt 0) {
                Write-Host "   ⚠️  UYARI: Bazı sonuçlar tarih aralığı dışında!" -ForegroundColor Yellow
                $invalidResults | ForEach-Object {
                    Write-Host "      - $($_.title) (Date: $($_.publicationDate))" -ForegroundColor Yellow
                }
            } else {
                Write-Host "   ✅ Doğrulama: Tüm sonuçlar belirtilen tarih aralığında" -ForegroundColor Green
            }
            
            # Doğrulama: Sıralama kontrolü
            $dates = $response | Where-Object { $_.publicationDate } | ForEach-Object { 
                if ($_.publicationDate -is [DateTime]) { 
                    $_.publicationDate 
                } else { 
                    [DateTime]::Parse($_.publicationDate.ToString()) 
                }
            }
            
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
                if ($errorDetails.error.details) {
                    Write-Host "   Error Details: $($errorDetails.error.details)" -ForegroundColor Gray
                }
            }
        } catch {
            # JSON parse hatası
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

