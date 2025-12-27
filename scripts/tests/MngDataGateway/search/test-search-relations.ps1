# Test Search in Relation Fields
# Tests search functionality in relation fields (pre-expansion search)

$baseUrl = "https://localhost:5010"
$datasetName = "tst_books"

# Token'ı yükle (ortak script kullanarak)
$scriptPath = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $PSCommandPath }
if ([string]::IsNullOrEmpty($scriptPath)) {
    $scriptPath = Get-Location
}
$loadTokenScript = Join-Path $scriptPath "load-token.ps1"

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

Write-Host "`n🔍 Testing Search in Relation Fields`n" -ForegroundColor Cyan
Write-Host "=" * 80 -ForegroundColor Gray
Write-Host ""

$testResults = @()

# Test 1: Search in publisher relation field (search by publisher name)
Write-Host "📋 Test 1: Search in publisher relation field (search=Penguin)" -ForegroundColor Yellow
try {
    $url = "$baseUrl/api/data/$datasetName" + "?search=Penguin&limit=10"
    
    $response = Invoke-RestMethod -Uri $url -Method GET -Headers $headers -SkipCertificateCheck
    $count = if ($response -is [Array]) { $response.Count } else { 0 }
    Write-Host "   ✅ Başarılı! $count kayıt bulundu" -ForegroundColor Green
    
    if ($count -gt 0) {
        Write-Host "   📊 İlk 3 kayıt:" -ForegroundColor Cyan
        $response | Select-Object -First 3 | ForEach-Object {
            $title = if ($_.title) { $_.title } else { "N/A" }
            $publisherName = if ($_.publisher -and $_.publisher.name) { $_.publisher.name } else { "N/A" }
            Write-Host "      - $title (Publisher: $publisherName)" -ForegroundColor White
        }
    }
    
    $testResults += @{ Test = "Search in publisher field"; Status = "✅ Başarılı"; Count = $count; Error = $null }
} catch {
    $errorMsg = $_.Exception.Message
    Write-Host "   ❌ Hata: $errorMsg" -ForegroundColor Red
    $testResults += @{ Test = "Search in publisher field"; Status = "❌ Hata"; Count = 0; Error = $errorMsg }
}
Write-Host ""

# Test 2: Search in genres relation field (search by genre name)
Write-Host "📋 Test 2: Search in genres relation field (search=Fantasy)" -ForegroundColor Yellow
try {
    $url = "$baseUrl/api/data/$datasetName" + "?search=Fantasy&limit=10"
    
    $response = Invoke-RestMethod -Uri $url -Method GET -Headers $headers -SkipCertificateCheck
    $count = if ($response -is [Array]) { $response.Count } else { 0 }
    Write-Host "   ✅ Başarılı! $count kayıt bulundu" -ForegroundColor Green
    
    if ($count -gt 0) {
        Write-Host "   📊 İlk 3 kayıt:" -ForegroundColor Cyan
        $response | Select-Object -First 3 | ForEach-Object {
            $title = if ($_.title) { $_.title } else { "N/A" }
            $genres = if ($_.genres -and $_.genres.Count -gt 0) { 
                ($_.genres | ForEach-Object { $_.name }) -join ", " 
            } else { "N/A" }
            Write-Host "      - $title (Genres: $genres)" -ForegroundColor White
        }
    }
    
    $testResults += @{ Test = "Search in genres field"; Status = "✅ Başarılı"; Count = $count; Error = $null }
} catch {
    $errorMsg = $_.Exception.Message
    Write-Host "   ❌ Hata: $errorMsg" -ForegroundColor Red
    $testResults += @{ Test = "Search in genres field"; Status = "❌ Hata"; Count = 0; Error = $errorMsg }
}
Write-Host ""

# Test 3: Search in both main fields and relation fields (search=Random)
Write-Host "📋 Test 3: Search in both main and relation fields (search=Random)" -ForegroundColor Yellow
try {
    $url = "$baseUrl/api/data/$datasetName" + "?search=Random&limit=10"
    
    $response = Invoke-RestMethod -Uri $url -Method GET -Headers $headers -SkipCertificateCheck
    $count = if ($response -is [Array]) { $response.Count } else { 0 }
    Write-Host "   ✅ Başarılı! $count kayıt bulundu" -ForegroundColor Green
    
    if ($count -gt 0) {
        Write-Host "   📊 İlk 3 kayıt:" -ForegroundColor Cyan
        $response | Select-Object -First 3 | ForEach-Object {
            $title = if ($_.title) { $_.title } else { "N/A" }
            $publisherName = if ($_.publisher -and $_.publisher.name) { $_.publisher.name } else { "N/A" }
            Write-Host "      - $title (Publisher: $publisherName)" -ForegroundColor White
        }
    }
    
    $testResults += @{ Test = "Search in both fields"; Status = "✅ Başarılı"; Count = $count; Error = $null }
} catch {
    $errorMsg = $_.Exception.Message
    Write-Host "   ❌ Hata: $errorMsg" -ForegroundColor Red
    $testResults += @{ Test = "Search in both fields"; Status = "❌ Hata"; Count = 0; Error = $errorMsg }
}
Write-Host ""

# Test 4: Search in relation field with no results
Write-Host "📋 Test 4: Search in relation field with no results (search=NonExistentPublisher12345)" -ForegroundColor Yellow
try {
    $url = "$baseUrl/api/data/$datasetName" + "?search=NonExistentPublisher12345&limit=10"
    
    $response = Invoke-RestMethod -Uri $url -Method GET -Headers $headers -SkipCertificateCheck
    $count = if ($response -is [Array]) { $response.Count } else { 0 }
    Write-Host "   ✅ Başarılı! $count kayıt bulundu (beklenen: 0)" -ForegroundColor Green
    
    $testResults += @{ Test = "Search with no results"; Status = "✅ Başarılı"; Count = $count; Error = $null }
} catch {
    $errorMsg = $_.Exception.Message
    Write-Host "   ❌ Hata: $errorMsg" -ForegroundColor Red
    $testResults += @{ Test = "Search with no results"; Status = "❌ Hata"; Count = 0; Error = $errorMsg }
}
Write-Host ""

# Test 5: Search in relation field combined with filter
Write-Host "📋 Test 5: Search in relation field + filter (search=Penguin&filter=price:gte:20)" -ForegroundColor Yellow
try {
    $url = "$baseUrl/api/data/$datasetName" + "?search=Penguin&filter=price:gte:20&limit=10"
    
    $response = Invoke-RestMethod -Uri $url -Method GET -Headers $headers -SkipCertificateCheck
    $count = if ($response -is [Array]) { $response.Count } else { 0 }
    Write-Host "   ✅ Başarılı! $count kayıt bulundu" -ForegroundColor Green
    
    if ($count -gt 0) {
        Write-Host "   📊 İlk 3 kayıt:" -ForegroundColor Cyan
        $response | Select-Object -First 3 | ForEach-Object {
            $title = if ($_.title) { $_.title } else { "N/A" }
            $price = if ($_.price) { $_.price } else { "N/A" }
            $publisherName = if ($_.publisher -and $_.publisher.name) { $_.publisher.name } else { "N/A" }
            Write-Host "      - $title (Price: $price, Publisher: $publisherName)" -ForegroundColor White
        }
    }
    
    $testResults += @{ Test = "Search + filter"; Status = "✅ Başarılı"; Count = $count; Error = $null }
} catch {
    $errorMsg = $_.Exception.Message
    Write-Host "   ❌ Hata: $errorMsg" -ForegroundColor Red
    $testResults += @{ Test = "Search + filter"; Status = "❌ Hata"; Count = 0; Error = $errorMsg }
}
Write-Host ""

# Test 6: Search in relation field with pagination
Write-Host "📋 Test 6: Search in relation field with pagination (search=Penguin&skip=0&limit=3)" -ForegroundColor Yellow
try {
    $url = "$baseUrl/api/data/$datasetName" + "?search=Penguin&skip=0&limit=3"
    
    $response = Invoke-RestMethod -Uri $url -Method GET -Headers $headers -SkipCertificateCheck
    $count = if ($response -is [Array]) { $response.Count } else { 0 }
    Write-Host "   ✅ Başarılı! $count kayıt bulundu (limit: 3)" -ForegroundColor Green
    
    if ($count -gt 0) {
        Write-Host "   📊 Tüm kayıtlar:" -ForegroundColor Cyan
        $response | ForEach-Object {
            $title = if ($_.title) { $_.title } else { "N/A" }
            $publisherName = if ($_.publisher -and $_.publisher.name) { $_.publisher.name } else { "N/A" }
            Write-Host "      - $title (Publisher: $publisherName)" -ForegroundColor White
        }
    }
    
    $testResults += @{ Test = "Search with pagination"; Status = "✅ Başarılı"; Count = $count; Error = $null }
} catch {
    $errorMsg = $_.Exception.Message
    Write-Host "   ❌ Hata: $errorMsg" -ForegroundColor Red
    $testResults += @{ Test = "Search with pagination"; Status = "❌ Hata"; Count = 0; Error = $errorMsg }
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

