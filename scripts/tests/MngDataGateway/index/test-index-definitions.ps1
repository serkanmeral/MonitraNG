# Test Index Definitions Storage
# Tests that index definitions can be stored and retrieved from dataset schema

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

Write-Host "`n📇 Testing Index Definitions Storage`n" -ForegroundColor Cyan
Write-Host "=" * 80 -ForegroundColor Gray
Write-Host ""

$testResults = @()

# Test 1: Check if index definitions exist in schema
Write-Host "📋 Test 1: Check index definitions in schema" -ForegroundColor Yellow
try {
    $url = "$baseUrl/api/datasets/$datasetName"
    
    $response = Invoke-RestMethod -Uri $url -Method GET -Headers $headers -SkipCertificateCheck
    
    if ($response.indexList -and $response.indexList.Count -gt 0) {
        Write-Host "   ✅ Başarılı! Schema'da $($response.indexList.Count) index tanımı bulundu" -ForegroundColor Green
        Write-Host "   📊 Index tanımları:" -ForegroundColor Cyan
        foreach ($index in $response.indexList) {
            $fieldsStr = ($index.fields.PSObject.Properties | ForEach-Object { "$($_.Name):$($_.Value)" }) -join ", "
            $uniqueStr = if ($index.unique) { " (unique)" } else { "" }
            Write-Host "      - $($index.name): $fieldsStr$uniqueStr" -ForegroundColor White
        }
        
        $testResults += @{ Test = "Index definitions in schema"; Status = "✅ Başarılı"; Count = $response.indexList.Count; Error = $null }
    } else {
        Write-Host "   ⚠️  Schema'da index tanımı bulunamadı" -ForegroundColor Yellow
        $testResults += @{ Test = "Index definitions in schema"; Status = "⚠️  Uyarı"; Count = 0; Error = "No index definitions found" }
    }
} catch {
    $errorMsg = $_.Exception.Message
    Write-Host "   ❌ Hata: $errorMsg" -ForegroundColor Red
    $testResults += @{ Test = "Index definitions in schema"; Status = "❌ Hata"; Count = 0; Error = $errorMsg }
}
Write-Host ""

# Test 2: Verify index definition structure
Write-Host "📋 Test 2: Verify index definition structure" -ForegroundColor Yellow
try {
    $url = "$baseUrl/api/datasets/$datasetName"
    
    $response = Invoke-RestMethod -Uri $url -Method GET -Headers $headers -SkipCertificateCheck
    
    if ($response.indexList -and $response.indexList.Count -gt 0) {
        $allValid = $true
        foreach ($index in $response.indexList) {
            $hasName = $index.name -and $index.name -ne ""
            $hasFields = $index.fields -and $index.fields.Count -gt 0
            $hasUnique = $index.PSObject.Properties.Name -contains "unique"
            
            if (-not $hasName -or -not $hasFields) {
                $allValid = $false
                Write-Host "   ❌ Geçersiz index: $($index.name)" -ForegroundColor Red
                Write-Host "      - Name: $(if ($hasName) { '✅' } else { '❌' })" -ForegroundColor $(if ($hasName) { "Green" } else { "Red" })
                Write-Host "      - Fields: $(if ($hasFields) { '✅' } else { '❌' })" -ForegroundColor $(if ($hasFields) { "Green" } else { "Red" })
            }
        }
        
        if ($allValid) {
            Write-Host "   ✅ Tüm index tanımları geçerli yapıda" -ForegroundColor Green
            Write-Host "   📊 Yapı kontrolü:" -ForegroundColor Cyan
            Write-Host "      - name: ✅ Zorunlu" -ForegroundColor Green
            Write-Host "      - fields: ✅ Zorunlu (Dictionary<string, int>)" -ForegroundColor Green
            Write-Host "      - unique: ✅ Opsiyonel (bool)" -ForegroundColor Green
            
            $testResults += @{ Test = "Index structure validation"; Status = "✅ Başarılı"; Error = $null }
        } else {
            $testResults += @{ Test = "Index structure validation"; Status = "❌ Hata"; Error = "Invalid index structure" }
        }
    } else {
        Write-Host "   ⚠️  Index tanımı yok, yapı kontrolü yapılamadı" -ForegroundColor Yellow
        $testResults += @{ Test = "Index structure validation"; Status = "⚠️  Skip"; Error = "No index definitions" }
    }
} catch {
    $errorMsg = $_.Exception.Message
    Write-Host "   ❌ Hata: $errorMsg" -ForegroundColor Red
    $testResults += @{ Test = "Index structure validation"; Status = "❌ Hata"; Error = $errorMsg }
}
Write-Host ""

# Test 3: Test index definitions can be updated
Write-Host "📋 Test 3: Test index definitions can be updated (read-only check)" -ForegroundColor Yellow
try {
    $url = "$baseUrl/api/datasets/$datasetName"
    
    $response = Invoke-RestMethod -Uri $url -Method GET -Headers $headers -SkipCertificateCheck
    
    if ($response.indexList -and $response.indexList.Count -gt 0) {
        Write-Host "   ✅ Index tanımları okunabiliyor" -ForegroundColor Green
        Write-Host "   📝 Not: Index tanımları dataset schema ile birlikte kaydedilir" -ForegroundColor Cyan
        Write-Host "   📝 Not: Fiziksel index oluşturma başka bir servis tarafından yapılacak" -ForegroundColor Cyan
        
        $testResults += @{ Test = "Index definitions readable"; Status = "✅ Başarılı"; Error = $null }
    } else {
        Write-Host "   ⚠️  Index tanımı yok" -ForegroundColor Yellow
        $testResults += @{ Test = "Index definitions readable"; Status = "⚠️  Skip"; Error = "No index definitions" }
    }
} catch {
    $errorMsg = $_.Exception.Message
    Write-Host "   ❌ Hata: $errorMsg" -ForegroundColor Red
    $testResults += @{ Test = "Index definitions readable"; Status = "❌ Hata"; Error = $errorMsg }
}
Write-Host ""

# Summary
Write-Host "=" * 80 -ForegroundColor Gray
Write-Host "`n📊 Test Özeti`n" -ForegroundColor Cyan

$successCount = ($testResults | Where-Object { $_.Status -eq "✅ Başarılı" }).Count
$failCount = ($testResults | Where-Object { $_.Status -eq "❌ Hata" }).Count
$skipCount = ($testResults | Where-Object { $_.Status -eq "⚠️  Skip" -or $_.Status -eq "⚠️  Uyarı" }).Count

Write-Host "Toplam Test: $($testResults.Count)" -ForegroundColor White
Write-Host "✅ Başarılı: $successCount" -ForegroundColor Green
Write-Host "❌ Hata: $failCount" -ForegroundColor Red
Write-Host "⚠️  Skip/Uyarı: $skipCount" -ForegroundColor Yellow
Write-Host ""

if ($failCount -gt 0) {
    Write-Host "Hata Detayları:" -ForegroundColor Yellow
    $testResults | Where-Object { $_.Status -eq "❌ Hata" } | ForEach-Object {
        Write-Host "  - $($_.Test): $($_.Error)" -ForegroundColor Red
    }
}

Write-Host "`n📝 Önemli Notlar:`n" -ForegroundColor Cyan
Write-Host "  1. Index tanımları dataset schema içerisinde saklanır" -ForegroundColor White
Write-Host "  2. Index tanımları dataset oluşturma/güncelleme sırasında kaydedilir" -ForegroundColor White
Write-Host "  3. Fiziksel index oluşturma DataGateway'in sorumluluğunda DEĞİLDİR" -ForegroundColor White
Write-Host "  4. Fiziksel index oluşturma ayrı bir servis tarafından yapılacak" -ForegroundColor White
Write-Host "  5. DataGateway sadece index tanımlarını metadata olarak saklar" -ForegroundColor White

Write-Host "`n✅ Test tamamlandı!`n" -ForegroundColor Green

