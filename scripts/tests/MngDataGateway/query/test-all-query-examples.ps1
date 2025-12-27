# Test All Query Examples from query-examples.md
# Tests all query examples with appropriate parameters

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

Write-Host "`n🧪 Testing All Query Examples`n" -ForegroundColor Cyan
Write-Host "=" * 80 -ForegroundColor Gray
Write-Host ""

$testResults = @()

# Test 1: books_by_price_range (number parameters)
Write-Host "📋 Test 1: books_by_price_range (number parameters)" -ForegroundColor Yellow
try {
    $queryParams = @{
        minPrice = 10
        maxPrice = 100
    }
    $requestBody = $queryParams | ConvertTo-Json
    $url = "$baseUrl/api/data/$datasetName/queries/books_by_price_range"
    
    $response = Invoke-RestMethod -Uri $url -Method POST -Headers $headers -Body $requestBody -SkipCertificateCheck
    $count = if ($response -is [Array]) { $response.Count } else { 0 }
    Write-Host "   ✅ Başarılı! $count kayıt bulundu" -ForegroundColor Green
    $testResults += @{ Query = "books_by_price_range"; Status = "✅ Başarılı"; Count = $count; Error = $null }
} catch {
    $errorMsg = $_.Exception.Message
    $errorDetails = $null
    if ($_.ErrorDetails.Message) {
        try {
            $errorDetails = $_.ErrorDetails.Message | ConvertFrom-Json -ErrorAction Stop
        } catch {
            $errorDetails = $null
        }
    }
    
    if ($errorDetails -and $errorDetails.error) {
        $errorMsg = $errorDetails.error.message
        if ($errorDetails.error.details) {
            $errorMsg += " | Details: $($errorDetails.error.details)"
        }
    }
    
    Write-Host "   ❌ Hata: $errorMsg" -ForegroundColor Red
    $testResults += @{ Query = "books_by_price_range"; Status = "❌ Hata"; Count = 0; Error = $errorMsg }
}
Write-Host ""

# Test 2: books_by_min_pages (number parameter)
Write-Host "📋 Test 2: books_by_min_pages (number parameter)" -ForegroundColor Yellow
try {
    $queryParams = @{
        minPages = 200
    }
    $requestBody = $queryParams | ConvertTo-Json
    $url = "$baseUrl/api/data/$datasetName/queries/books_by_min_pages"
    
    $response = Invoke-RestMethod -Uri $url -Method POST -Headers $headers -Body $requestBody -SkipCertificateCheck
    $count = if ($response -is [Array]) { $response.Count } else { 0 }
    Write-Host "   ✅ Başarılı! $count kayıt bulundu" -ForegroundColor Green
    $testResults += @{ Query = "books_by_min_pages"; Status = "✅ Başarılı"; Count = $count; Error = $null }
} catch {
    $errorDetails = $null
    if ($_.ErrorDetails.Message) {
        try {
            $errorDetails = $_.ErrorDetails.Message | ConvertFrom-Json -ErrorAction Stop
        } catch {
            $errorDetails = $null
        }
    }
    $errorMsg = if ($errorDetails -and $errorDetails.error) { $errorDetails.error.message } else { $_.Exception.Message }
    Write-Host "   ❌ Hata: $errorMsg" -ForegroundColor Red
    $testResults += @{ Query = "books_by_min_pages"; Status = "❌ Hata"; Count = 0; Error = $errorMsg }
}
Write-Host ""

# Test 3: books_by_availability (bool parameter)
Write-Host "📋 Test 3: books_by_availability (bool parameter)" -ForegroundColor Yellow
try {
    $queryParams = @{
        isAvailable = $true
    }
    $requestBody = $queryParams | ConvertTo-Json
    $url = "$baseUrl/api/data/$datasetName/queries/books_by_availability"
    
    $response = Invoke-RestMethod -Uri $url -Method POST -Headers $headers -Body $requestBody -SkipCertificateCheck
    $count = if ($response -is [Array]) { $response.Count } else { 0 }
    Write-Host "   ✅ Başarılı! $count kayıt bulundu" -ForegroundColor Green
    $testResults += @{ Query = "books_by_availability"; Status = "✅ Başarılı"; Count = $count; Error = $null }
} catch {
    $errorDetails = $null
    if ($_.ErrorDetails.Message) {
        try {
            $errorDetails = $_.ErrorDetails.Message | ConvertFrom-Json -ErrorAction Stop
        } catch {
            $errorDetails = $null
        }
    }
    $errorMsg = if ($errorDetails -and $errorDetails.error) { $errorDetails.error.message } else { $_.Exception.Message }
    Write-Host "   ❌ Hata: $errorMsg" -ForegroundColor Red
    $testResults += @{ Query = "books_by_availability"; Status = "❌ Hata"; Count = 0; Error = $errorMsg }
}
Write-Host ""

# Test 4: books_by_published_status (bool parameter)
Write-Host "📋 Test 4: books_by_published_status (bool parameter)" -ForegroundColor Yellow
try {
    $queryParams = @{
        isPublished = $true
    }
    $requestBody = $queryParams | ConvertTo-Json
    $url = "$baseUrl/api/data/$datasetName/queries/books_by_published_status"
    
    $response = Invoke-RestMethod -Uri $url -Method POST -Headers $headers -Body $requestBody -SkipCertificateCheck
    $count = if ($response -is [Array]) { $response.Count } else { 0 }
    Write-Host "   ✅ Başarılı! $count kayıt bulundu" -ForegroundColor Green
    $testResults += @{ Query = "books_by_published_status"; Status = "✅ Başarılı"; Count = $count; Error = $null }
} catch {
    $errorDetails = $null
    if ($_.ErrorDetails.Message) {
        try {
            $errorDetails = $_.ErrorDetails.Message | ConvertFrom-Json -ErrorAction Stop
        } catch {
            $errorDetails = $null
        }
    }
    $errorMsg = if ($errorDetails -and $errorDetails.error) { $errorDetails.error.message } else { $_.Exception.Message }
    Write-Host "   ❌ Hata: $errorMsg" -ForegroundColor Red
    $testResults += @{ Query = "books_by_published_status"; Status = "❌ Hata"; Count = 0; Error = $errorMsg }
}
Write-Host ""

# Test 5: books_by_author (text parameter with regex)
Write-Host "📋 Test 5: books_by_author (text parameter with regex)" -ForegroundColor Yellow
try {
    $queryParams = @{
        authorName = "John"
    }
    $requestBody = $queryParams | ConvertTo-Json
    $url = "$baseUrl/api/data/$datasetName/queries/books_by_author"
    
    $response = Invoke-RestMethod -Uri $url -Method POST -Headers $headers -Body $requestBody -SkipCertificateCheck
    $count = if ($response -is [Array]) { $response.Count } else { 0 }
    Write-Host "   ✅ Başarılı! $count kayıt bulundu" -ForegroundColor Green
    $testResults += @{ Query = "books_by_author"; Status = "✅ Başarılı"; Count = $count; Error = $null }
} catch {
    $errorDetails = $null
    if ($_.ErrorDetails.Message) {
        try {
            $errorDetails = $_.ErrorDetails.Message | ConvertFrom-Json -ErrorAction Stop
        } catch {
            $errorDetails = $null
        }
    }
    $errorMsg = if ($errorDetails -and $errorDetails.error) { $errorDetails.error.message } else { $_.Exception.Message }
    Write-Host "   ❌ Hata: $errorMsg" -ForegroundColor Red
    $testResults += @{ Query = "books_by_author"; Status = "❌ Hata"; Count = 0; Error = $errorMsg }
}
Write-Host ""

# Test 6: books_by_category_and_title (text parameters)
Write-Host "📋 Test 6: books_by_category_and_title (text parameters)" -ForegroundColor Yellow
try {
    $queryParams = @{
        category = "Fiction"
        titleKeyword = "Python"
    }
    $requestBody = $queryParams | ConvertTo-Json
    $url = "$baseUrl/api/data/$datasetName/queries/books_by_category_and_title"
    
    $response = Invoke-RestMethod -Uri $url -Method POST -Headers $headers -Body $requestBody -SkipCertificateCheck
    $count = if ($response -is [Array]) { $response.Count } else { 0 }
    Write-Host "   ✅ Başarılı! $count kayıt bulundu" -ForegroundColor Green
    $testResults += @{ Query = "books_by_category_and_title"; Status = "✅ Başarılı"; Count = $count; Error = $null }
} catch {
    $errorDetails = $null
    if ($_.ErrorDetails.Message) {
        try {
            $errorDetails = $_.ErrorDetails.Message | ConvertFrom-Json -ErrorAction Stop
        } catch {
            $errorDetails = $null
        }
    }
    $errorMsg = if ($errorDetails -and $errorDetails.error) { $errorDetails.error.message } else { $_.Exception.Message }
    Write-Host "   ❌ Hata: $errorMsg" -ForegroundColor Red
    $testResults += @{ Query = "books_by_category_and_title"; Status = "❌ Hata"; Count = 0; Error = $errorMsg }
}
Write-Host ""

# Test 7: books_by_price_date_and_status (mixed: number, datetime, bool)
Write-Host "📋 Test 7: books_by_price_date_and_status (mixed: number, datetime, bool)" -ForegroundColor Yellow
try {
    $queryParams = @{
        maxPrice = 50
        startDate = "2020-01-01T00:00:00Z"
        endDate = "2025-12-31T23:59:59Z"
        isAvailable = $true
    }
    $requestBody = $queryParams | ConvertTo-Json
    $url = "$baseUrl/api/data/$datasetName/queries/books_by_price_date_and_status"
    
    $response = Invoke-RestMethod -Uri $url -Method POST -Headers $headers -Body $requestBody -SkipCertificateCheck
    $count = if ($response -is [Array]) { $response.Count } else { 0 }
    Write-Host "   ✅ Başarılı! $count kayıt bulundu" -ForegroundColor Green
    $testResults += @{ Query = "books_by_price_date_and_status"; Status = "✅ Başarılı"; Count = $count; Error = $null }
} catch {
    $errorDetails = $null
    if ($_.ErrorDetails.Message) {
        try {
            $errorDetails = $_.ErrorDetails.Message | ConvertFrom-Json -ErrorAction Stop
        } catch {
            $errorDetails = $null
        }
    }
    $errorMsg = if ($errorDetails -and $errorDetails.error) { $errorDetails.error.message } else { $_.Exception.Message }
    Write-Host "   ❌ Hata: $errorMsg" -ForegroundColor Red
    $testResults += @{ Query = "books_by_price_date_and_status"; Status = "❌ Hata"; Count = 0; Error = $errorMsg }
}
Write-Host ""

# Test 8: books_by_author_pages_and_published (mixed: text, number, bool)
Write-Host "📋 Test 8: books_by_author_pages_and_published (mixed: text, number, bool)" -ForegroundColor Yellow
try {
    $queryParams = @{
        authorName = "John"
        minPages = 100
        isPublished = $true
    }
    $requestBody = $queryParams | ConvertTo-Json
    $url = "$baseUrl/api/data/$datasetName/queries/books_by_author_pages_and_published"
    
    $response = Invoke-RestMethod -Uri $url -Method POST -Headers $headers -Body $requestBody -SkipCertificateCheck
    $count = if ($response -is [Array]) { $response.Count } else { 0 }
    Write-Host "   ✅ Başarılı! $count kayıt bulundu" -ForegroundColor Green
    $testResults += @{ Query = "books_by_author_pages_and_published"; Status = "✅ Başarılı"; Count = $count; Error = $null }
} catch {
    $errorDetails = $null
    if ($_.ErrorDetails.Message) {
        try {
            $errorDetails = $_.ErrorDetails.Message | ConvertFrom-Json -ErrorAction Stop
        } catch {
            $errorDetails = $null
        }
    }
    $errorMsg = if ($errorDetails -and $errorDetails.error) { $errorDetails.error.message } else { $_.Exception.Message }
    Write-Host "   ❌ Hata: $errorMsg" -ForegroundColor Red
    $testResults += @{ Query = "books_by_author_pages_and_published"; Status = "❌ Hata"; Count = 0; Error = $errorMsg }
}
Write-Host ""

# Test 9: books_with_optional_filters (optional parameter)
Write-Host "📋 Test 9: books_with_optional_filters (optional parameter)" -ForegroundColor Yellow
try {
    $queryParams = @{
        maxPrice = 100
    }
    $requestBody = $queryParams | ConvertTo-Json
    $url = "$baseUrl/api/data/$datasetName/queries/books_with_optional_filters"
    
    $response = Invoke-RestMethod -Uri $url -Method POST -Headers $headers -Body $requestBody -SkipCertificateCheck
    $count = if ($response -is [Array]) { $response.Count } else { 0 }
    Write-Host "   ✅ Başarılı! $count kayıt bulundu" -ForegroundColor Green
    $testResults += @{ Query = "books_with_optional_filters"; Status = "✅ Başarılı"; Count = $count; Error = $null }
} catch {
    $errorDetails = $null
    if ($_.ErrorDetails.Message) {
        try {
            $errorDetails = $_.ErrorDetails.Message | ConvertFrom-Json -ErrorAction Stop
        } catch {
            $errorDetails = $null
        }
    }
    $errorMsg = if ($errorDetails -and $errorDetails.error) { $errorDetails.error.message } else { $_.Exception.Message }
    Write-Host "   ❌ Hata: $errorMsg" -ForegroundColor Red
    $testResults += @{ Query = "books_with_optional_filters"; Status = "❌ Hata"; Count = 0; Error = $errorMsg }
}
Write-Host ""

# Test 10: books_by_publication_date_range (datetime parameters - already in dataset)
Write-Host "📋 Test 10: books_by_publication_date_range (datetime parameters)" -ForegroundColor Yellow
try {
    $queryParams = @{
        startDate = "2020-01-01T00:00:00Z"
        endDate = "2025-12-31T23:59:59Z"
    }
    $requestBody = $queryParams | ConvertTo-Json
    $url = "$baseUrl/api/data/$datasetName/queries/books_by_publication_date_range"
    
    $response = Invoke-RestMethod -Uri $url -Method POST -Headers $headers -Body $requestBody -SkipCertificateCheck
    $count = if ($response -is [Array]) { $response.Count } else { 0 }
    Write-Host "   ✅ Başarılı! $count kayıt bulundu" -ForegroundColor Green
    $testResults += @{ Query = "books_by_publication_date_range"; Status = "✅ Başarılı"; Count = $count; Error = $null }
} catch {
    $errorDetails = $null
    if ($_.ErrorDetails.Message) {
        try {
            $errorDetails = $_.ErrorDetails.Message | ConvertFrom-Json -ErrorAction Stop
        } catch {
            $errorDetails = $null
        }
    }
    $errorMsg = if ($errorDetails -and $errorDetails.error) { $errorDetails.error.message } else { $_.Exception.Message }
    Write-Host "   ❌ Hata: $errorMsg" -ForegroundColor Red
    $testResults += @{ Query = "books_by_publication_date_range"; Status = "❌ Hata"; Count = 0; Error = $errorMsg }
}
Write-Host ""

# Summary
Write-Host "=" * 80 -ForegroundColor Gray
Write-Host "`n📊 Test Özeti`n" -ForegroundColor Cyan

$successCount = ($testResults | Where-Object { $_.Status -eq "✅ Başarılı" }).Count
$failCount = ($testResults | Where-Object { $_.Status -eq "❌ Hata" }).Count

Write-Host "Toplam Test: $($testResults.Count)" -ForegroundColor White
Write-Host "✅ Başarılı: $successCount" -ForegroundColor Green
Write-Host "❌ Hata: $failCount" -ForegroundColor Red
Write-Host ""

if ($failCount -gt 0) {
    Write-Host "Hata Detayları:" -ForegroundColor Yellow
    $testResults | Where-Object { $_.Status -eq "❌ Hata" } | ForEach-Object {
        Write-Host "  - $($_.Query): $($_.Error)" -ForegroundColor Red
    }
}

Write-Host "`n✅ Test tamamlandı!`n" -ForegroundColor Green

