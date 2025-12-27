# Test script for price filter query
# Tests: GET /api/data/tst_books?filter=price:gt:200

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

[System.Net.ServicePointManager]::ServerCertificateValidationCallback = {$true}

Write-Host "🔍 Testing Price Filter Query" -ForegroundColor Cyan
Write-Host "=" * 60 -ForegroundColor Gray
Write-Host ""

# Test 1: GET with filter parameter (price > 200)
Write-Host "📋 Test 1: GET /api/data/tst_books?filter=price:gt:200" -ForegroundColor Yellow
Write-Host "   Beklenen: price değeri 200'den büyük olan kitaplar" -ForegroundColor Gray
Write-Host ""

try {
    $filter = "price:gt:200"
    $url = "$baseUrl/api/data/$datasetName" + "?filter=" + [System.Web.HttpUtility]::UrlEncode($filter) + "&expand=true&limit=10"
    
    Write-Host "   URL: $url" -ForegroundColor DarkGray
    Write-Host ""
    
    $response = Invoke-RestMethod -Uri $url -Method GET -Headers $headers -SkipCertificateCheck
    
    if ($response -is [System.Array]) {
        $count = $response.Count
        Write-Host "   ✅ Başarılı! $count kayıt bulundu" -ForegroundColor Green
        Write-Host ""
        
        if ($count -gt 0) {
            Write-Host "   📊 İlk 3 kayıt:" -ForegroundColor Cyan
            $response | Select-Object -First 3 | ForEach-Object {
                $price = if ($_.price) { $_.price } else { "N/A" }
                $title = if ($_.title) { $_.title } else { "N/A" }
                Write-Host "      - $title (Price: $price)" -ForegroundColor White
            }
            Write-Host ""
            
            # Verify all results have price > 200
            $invalidResults = $response | Where-Object { $_.price -and $_.price -le 200 }
            if ($invalidResults.Count -gt 0) {
                Write-Host "   ⚠️  UYARI: Bazı sonuçlar price <= 200!" -ForegroundColor Yellow
                $invalidResults | ForEach-Object {
                    Write-Host "      - $($_.title) (Price: $($_.price))" -ForegroundColor Yellow
                }
            } else {
                Write-Host "   ✅ Doğrulama: Tüm sonuçlar price > 200" -ForegroundColor Green
            }
        } else {
            Write-Host "   ℹ️  Hiç kayıt bulunamadı (price > 200 olan kitap yok)" -ForegroundColor Gray
        }
    } elseif ($response.Data) {
        # QueryResultDto format
        $items = $response.Data
        $count = if ($items) { $items.Count } else { 0 }
        Write-Host "   ✅ Başarılı! $count kayıt bulundu" -ForegroundColor Green
        Write-Host ""
        
        if ($count -gt 0) {
            Write-Host "   📊 İlk 3 kayıt:" -ForegroundColor Cyan
            $items | Select-Object -First 3 | ForEach-Object {
                $price = if ($_.price) { $_.price } else { "N/A" }
                $title = if ($_.title) { $_.title } else { "N/A" }
                Write-Host "      - $title (Price: $price)" -ForegroundColor White
            }
            Write-Host ""
            
            # Verify all results have price > 200
            $invalidResults = $items | Where-Object { $_.price -and $_.price -le 200 }
            if ($invalidResults.Count -gt 0) {
                Write-Host "   ⚠️  UYARI: Bazı sonuçlar price <= 200!" -ForegroundColor Yellow
                $invalidResults | ForEach-Object {
                    Write-Host "      - $($_.title) (Price: $($_.price))" -ForegroundColor Yellow
                }
            } else {
                Write-Host "   ✅ Doğrulama: Tüm sonuçlar price > 200" -ForegroundColor Green
            }
        } else {
            Write-Host "   ℹ️  Hiç kayıt bulunamadı (price > 200 olan kitap yok)" -ForegroundColor Gray
        }
    } else {
        Write-Host "   ⚠️  Beklenmeyen response formatı" -ForegroundColor Yellow
        $response | ConvertTo-Json -Depth 3
    }
} catch {
    Write-Host "   ❌ Hata: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.ErrorDetails.Message) {
        Write-Host "   Details: $($_.ErrorDetails.Message)" -ForegroundColor Gray
    }
}

Write-Host ""
Write-Host "=" * 60 -ForegroundColor Gray
Write-Host ""

# Test 2: POST /query endpoint with MongoDB native format
Write-Host "📋 Test 2: POST /api/data/tst_books/query (MongoDB native format)" -ForegroundColor Yellow
Write-Host "   Beklenen: price değeri 200'den büyük olan kitaplar" -ForegroundColor Gray
Write-Host ""

try {
    # MongoDB native format - $gt operatörü için özel escape
    $matchObj = @{
        price = @{
            '$gt' = 200
        }
    }
    $queryBody = @{
        match = $matchObj
    } | ConvertTo-Json -Depth 10
    
    $url = "$baseUrl/api/data/$datasetName/query?expand=true&limit=10"
    
    Write-Host "   URL: $url" -ForegroundColor DarkGray
    Write-Host "   Body: $queryBody" -ForegroundColor DarkGray
    Write-Host ""
    
    $response = Invoke-RestMethod -Uri $url -Method POST -Headers $headers -Body $queryBody -SkipCertificateCheck
    
    if ($response -is [System.Array]) {
        $count = $response.Count
        Write-Host "   ✅ Başarılı! $count kayıt bulundu" -ForegroundColor Green
        Write-Host ""
        
        if ($count -gt 0) {
            Write-Host "   📊 İlk 3 kayıt:" -ForegroundColor Cyan
            $response | Select-Object -First 3 | ForEach-Object {
                $price = if ($_.price) { $_.price } else { "N/A" }
                $title = if ($_.title) { $_.title } else { "N/A" }
                Write-Host "      - $title (Price: $price)" -ForegroundColor White
            }
            Write-Host ""
            
            # Verify all results have price > 200
            $invalidResults = $response | Where-Object { $_.price -and $_.price -le 200 }
            if ($invalidResults.Count -gt 0) {
                Write-Host "   ⚠️  UYARI: Bazı sonuçlar price <= 200!" -ForegroundColor Yellow
                $invalidResults | ForEach-Object {
                    Write-Host "      - $($_.title) (Price: $($_.price))" -ForegroundColor Yellow
                }
            } else {
                Write-Host "   ✅ Doğrulama: Tüm sonuçlar price > 200" -ForegroundColor Green
            }
        } else {
            Write-Host "   ℹ️  Hiç kayıt bulunamadı (price > 200 olan kitap yok)" -ForegroundColor Gray
        }
    } else {
        Write-Host "   ⚠️  Beklenmeyen response formatı" -ForegroundColor Yellow
        $response | ConvertTo-Json -Depth 3
    }
} catch {
    Write-Host "   ❌ Hata: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.ErrorDetails.Message) {
        Write-Host "   Details: $($_.ErrorDetails.Message)" -ForegroundColor Gray
    }
}

Write-Host ""
Write-Host "=" * 60 -ForegroundColor Gray
Write-Host ""

# Test 3: Additional filter operators
Write-Host "📋 Test 3: Diğer filter operatörleri" -ForegroundColor Yellow
Write-Host ""

# Test 3a: price >= 200 (gte)
Write-Host "   Test 3a: price >= 200 (gte)" -ForegroundColor Cyan
try {
    $filter = "price:gte:200"
    $url = "$baseUrl/api/data/$datasetName" + "?filter=" + [System.Web.HttpUtility]::UrlEncode($filter) + "&limit=5"
    $response = Invoke-RestMethod -Uri $url -Method GET -Headers $headers -SkipCertificateCheck
    
    $count = if ($response -is [System.Array]) { $response.Count } elseif ($response.Data) { $response.Data.Count } else { 0 }
    Write-Host "      ✅ $count kayıt bulundu (price >= 200)" -ForegroundColor Green
} catch {
    Write-Host "      ❌ Hata: $($_.Exception.Message)" -ForegroundColor Red
}

# Test 3b: price < 200 (lt)
Write-Host "   Test 3b: price < 200 (lt)" -ForegroundColor Cyan
try {
    $filter = "price:lt:200"
    $url = "$baseUrl/api/data/$datasetName" + "?filter=" + [System.Web.HttpUtility]::UrlEncode($filter) + "&limit=5"
    $response = Invoke-RestMethod -Uri $url -Method GET -Headers $headers -SkipCertificateCheck
    
    $count = if ($response -is [System.Array]) { $response.Count } elseif ($response.Data) { $response.Data.Count } else { 0 }
    Write-Host "      ✅ $count kayıt bulundu (price < 200)" -ForegroundColor Green
} catch {
    Write-Host "      ❌ Hata: $($_.Exception.Message)" -ForegroundColor Red
}

# Test 3c: Multiple filters (price > 200 AND pageCount > 300)
Write-Host "   Test 3c: Multiple filters (price > 200 AND pageCount > 300)" -ForegroundColor Cyan
try {
    $filter = "price:gt:200,pageCount:gt:300"
    $url = "$baseUrl/api/data/$datasetName" + "?filter=" + [System.Web.HttpUtility]::UrlEncode($filter) + "&limit=5"
    $response = Invoke-RestMethod -Uri $url -Method GET -Headers $headers -SkipCertificateCheck
    
    $count = if ($response -is [System.Array]) { $response.Count } elseif ($response.Data) { $response.Data.Count } else { 0 }
    Write-Host "      ✅ $count kayıt bulundu (price > 200 AND pageCount > 300)" -ForegroundColor Green
} catch {
    Write-Host "      ❌ Hata: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""
Write-Host "✅ Tüm testler tamamlandı!" -ForegroundColor Green

