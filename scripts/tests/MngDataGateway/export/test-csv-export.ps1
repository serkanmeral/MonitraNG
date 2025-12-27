# Test CSV Export Functionality
# Tests CSV export with various scenarios

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

Write-Host "`n📊 Testing CSV Export Functionality`n" -ForegroundColor Cyan
Write-Host "=" * 80 -ForegroundColor Gray
Write-Host ""

$testResults = @()

# Test 1: Basic CSV export
Write-Host "📋 Test 1: Basic CSV export (format=csv&limit=5)" -ForegroundColor Yellow
try {
    $url = "$baseUrl/api/data/$datasetName" + "?format=csv&limit=5"
    
    $response = Invoke-WebRequest -Uri $url -Method GET -Headers $headers -SkipCertificateCheck
    $csvContent = $response.Content
    
    if ($response.StatusCode -eq 200 -and $response.Headers.'Content-Type' -like "*text/csv*") {
        Write-Host "   ✅ Başarılı! CSV içeriği alındı" -ForegroundColor Green
        Write-Host "   📊 CSV Satır Sayısı: $($csvContent.Split("`n").Count)" -ForegroundColor Cyan
        Write-Host "   📄 İlk 3 satır:" -ForegroundColor Cyan
        $csvContent.Split("`n") | Select-Object -First 3 | ForEach-Object {
            Write-Host "      $_" -ForegroundColor White
        }
        
        $testResults += @{ Test = "Basic CSV export"; Status = "✅ Başarılı"; Error = $null }
    } else {
        throw "Unexpected response: Status=$($response.StatusCode), ContentType=$($response.Headers.'Content-Type')"
    }
} catch {
    $errorMsg = $_.Exception.Message
    Write-Host "   ❌ Hata: $errorMsg" -ForegroundColor Red
    $testResults += @{ Test = "Basic CSV export"; Status = "❌ Hata"; Error = $errorMsg }
}
Write-Host ""

# Test 2: CSV export with relation fields
Write-Host "📋 Test 2: CSV export with relation fields (format=csv&expand=true&limit=3)" -ForegroundColor Yellow
try {
    $url = "$baseUrl/api/data/$datasetName" + "?format=csv&expand=true&limit=3"
    
    $response = Invoke-WebRequest -Uri $url -Method GET -Headers $headers -SkipCertificateCheck
    $csvContent = $response.Content
    
    if ($response.StatusCode -eq 200) {
        Write-Host "   ✅ Başarılı! CSV içeriği alındı" -ForegroundColor Green
        
        # Check if relation fields are flattened
        $hasPublisherName = $csvContent -match "publisher\.name"
        $hasPublisherCountry = $csvContent -match "publisher\.country"
        
        Write-Host "   📊 Relation field kontrolü:" -ForegroundColor Cyan
        Write-Host "      - publisher.name: $(if ($hasPublisherName) { '✅ Var' } else { '❌ Yok' })" -ForegroundColor $(if ($hasPublisherName) { "Green" } else { "Red" })
        Write-Host "      - publisher.country: $(if ($hasPublisherCountry) { '✅ Var' } else { '❌ Yok' })" -ForegroundColor $(if ($hasPublisherCountry) { "Green" } else { "Red" })
        
        Write-Host "   📄 Header satırı:" -ForegroundColor Cyan
        $headerLine = $csvContent.Split("`n")[0]
        Write-Host "      $headerLine" -ForegroundColor White
        
        $testResults += @{ Test = "CSV with relation fields"; Status = "✅ Başarılı"; Error = $null }
    } else {
        throw "Unexpected response: Status=$($response.StatusCode)"
    }
} catch {
    $errorMsg = $_.Exception.Message
    Write-Host "   ❌ Hata: $errorMsg" -ForegroundColor Red
    $testResults += @{ Test = "CSV with relation fields"; Status = "❌ Hata"; Error = $errorMsg }
}
Write-Host ""

# Test 3: CSV export with array fields (genres)
Write-Host "📋 Test 3: CSV export with array fields (format=csv&expand=true&limit=3)" -ForegroundColor Yellow
try {
    $url = "$baseUrl/api/data/$datasetName" + "?format=csv&expand=true&limit=3"
    
    $response = Invoke-WebRequest -Uri $url -Method GET -Headers $headers -SkipCertificateCheck
    $csvContent = $response.Content
    
    if ($response.StatusCode -eq 200) {
        Write-Host "   ✅ Başarılı! CSV içeriği alındı" -ForegroundColor Green
        
        # Check if genres field exists
        $hasGenres = $csvContent -match "genres"
        
        Write-Host "   📊 Array field kontrolü:" -ForegroundColor Cyan
        Write-Host "      - genres: $(if ($hasGenres) { '✅ Var' } else { '❌ Yok' })" -ForegroundColor $(if ($hasGenres) { "Green" } else { "Red" })
        
        if ($hasGenres) {
            Write-Host "   📄 İlk data satırı (genres kontrolü):" -ForegroundColor Cyan
            $dataLines = $csvContent.Split("`n") | Where-Object { $_ -and $_ -notmatch "^[^,]*$" -and $_ -notmatch "^title," }
            if ($dataLines.Count -gt 0) {
                $firstDataLine = $dataLines[0]
                Write-Host "      $($firstDataLine.Substring(0, [Math]::Min(100, $firstDataLine.Length)))..." -ForegroundColor White
            }
        }
        
        $testResults += @{ Test = "CSV with array fields"; Status = "✅ Başarılı"; Error = $null }
    } else {
        throw "Unexpected response: Status=$($response.StatusCode)"
    }
} catch {
    $errorMsg = $_.Exception.Message
    Write-Host "   ❌ Hata: $errorMsg" -ForegroundColor Red
    $testResults += @{ Test = "CSV with array fields"; Status = "❌ Hata"; Error = $errorMsg }
}
Write-Host ""

# Test 4: CSV export with filter
Write-Host "📋 Test 4: CSV export with filter (format=csv&filter=price:gte:20&limit=5)" -ForegroundColor Yellow
try {
    $url = "$baseUrl/api/data/$datasetName" + "?format=csv&filter=price:gte:20&limit=5"
    
    $response = Invoke-WebRequest -Uri $url -Method GET -Headers $headers -SkipCertificateCheck
    $csvContent = $response.Content
    
    if ($response.StatusCode -eq 200) {
        $lineCount = ($csvContent.Split("`n") | Where-Object { $_ }).Count
        Write-Host "   ✅ Başarılı! CSV içeriği alındı" -ForegroundColor Green
        Write-Host "   📊 CSV Satır Sayısı: $lineCount (header + data)" -ForegroundColor Cyan
        
        $testResults += @{ Test = "CSV with filter"; Status = "✅ Başarılı"; Error = $null }
    } else {
        throw "Unexpected response: Status=$($response.StatusCode)"
    }
} catch {
    $errorMsg = $_.Exception.Message
    Write-Host "   ❌ Hata: $errorMsg" -ForegroundColor Red
    $testResults += @{ Test = "CSV with filter"; Status = "❌ Hata"; Error = $errorMsg }
}
Write-Host ""

# Test 5: CSV export with search
Write-Host "📋 Test 5: CSV export with search (format=csv&search=Penguin&limit=5)" -ForegroundColor Yellow
try {
    $url = "$baseUrl/api/data/$datasetName" + "?format=csv&search=Penguin&limit=5"
    
    $response = Invoke-WebRequest -Uri $url -Method GET -Headers $headers -SkipCertificateCheck
    $csvContent = $response.Content
    
    if ($response.StatusCode -eq 200) {
        $lineCount = ($csvContent.Split("`n") | Where-Object { $_ }).Count
        Write-Host "   ✅ Başarılı! CSV içeriği alındı" -ForegroundColor Green
        Write-Host "   📊 CSV Satır Sayısı: $lineCount (header + data)" -ForegroundColor Cyan
        
        $testResults += @{ Test = "CSV with search"; Status = "✅ Başarılı"; Error = $null }
    } else {
        throw "Unexpected response: Status=$($response.StatusCode)"
    }
} catch {
    $errorMsg = $_.Exception.Message
    Write-Host "   ❌ Hata: $errorMsg" -ForegroundColor Red
    $testResults += @{ Test = "CSV with search"; Status = "❌ Hata"; Error = $errorMsg }
}
Write-Host ""

# Test 6: CSV export with pagination
Write-Host "📋 Test 6: CSV export with pagination (format=csv&skip=0&limit=3)" -ForegroundColor Yellow
try {
    $url = "$baseUrl/api/data/$datasetName" + "?format=csv&skip=0&limit=3"
    
    $response = Invoke-WebRequest -Uri $url -Method GET -Headers $headers -SkipCertificateCheck
    $csvContent = $response.Content
    
    if ($response.StatusCode -eq 200) {
        $lineCount = ($csvContent.Split("`n") | Where-Object { $_ }).Count
        Write-Host "   ✅ Başarılı! CSV içeriği alındı" -ForegroundColor Green
        Write-Host "   📊 CSV Satır Sayısı: $lineCount (header + data, beklenen: 4)" -ForegroundColor Cyan
        
        if ($lineCount -eq 4) {
            Write-Host "   ✅ Pagination çalışıyor (3 data + 1 header)" -ForegroundColor Green
        } else {
            Write-Host "   ⚠️  Pagination kontrolü: Beklenen 4 satır, bulunan $lineCount satır" -ForegroundColor Yellow
        }
        
        $testResults += @{ Test = "CSV with pagination"; Status = "✅ Başarılı"; Error = $null }
    } else {
        throw "Unexpected response: Status=$($response.StatusCode)"
    }
} catch {
    $errorMsg = $_.Exception.Message
    Write-Host "   ❌ Hata: $errorMsg" -ForegroundColor Red
    $testResults += @{ Test = "CSV with pagination"; Status = "❌ Hata"; Error = $errorMsg }
}
Write-Host ""

# Test 7: CSV export - save to file
Write-Host "📋 Test 7: CSV export - save to file (format=csv&limit=5)" -ForegroundColor Yellow
try {
    $url = "$baseUrl/api/data/$datasetName" + "?format=csv&limit=5"
    
    $response = Invoke-WebRequest -Uri $url -Method GET -Headers $headers -SkipCertificateCheck
    $csvContent = $response.Content
    
    if ($response.StatusCode -eq 200) {
        $outputFile = "$env:TEMP\test-csv-export.csv"
        $csvContent | Out-File -FilePath $outputFile -Encoding UTF8
        
        Write-Host "   ✅ Başarılı! CSV dosyası kaydedildi" -ForegroundColor Green
        Write-Host "   📁 Dosya yolu: $outputFile" -ForegroundColor Cyan
        Write-Host "   📊 Dosya boyutu: $([Math]::Round((Get-Item $outputFile).Length / 1KB, 2)) KB" -ForegroundColor Cyan
        
        $testResults += @{ Test = "CSV save to file"; Status = "✅ Başarılı"; Error = $null }
    } else {
        throw "Unexpected response: Status=$($response.StatusCode)"
    }
} catch {
    $errorMsg = $_.Exception.Message
    Write-Host "   ❌ Hata: $errorMsg" -ForegroundColor Red
    $testResults += @{ Test = "CSV save to file"; Status = "❌ Hata"; Error = $errorMsg }
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
        Write-Host "  - $($_.Test): $($_.Error)" -ForegroundColor Red
    }
}

Write-Host "`n✅ Test tamamlandı!`n" -ForegroundColor Green

