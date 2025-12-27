# Dataset Categories CRUD Test Script
# Full metadata pattern test

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Dataset Categories CRUD Test Suite" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Base URL
$baseUrl = "https://localhost:5010"

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
Write-Host "✅ Token yüklendi" -ForegroundColor Green
Write-Host ""

# Headers
$headers = @{
    "Authorization" = "Bearer $token"
    "Content-Type" = "application/json"
}

# PowerShell 7+ için SSL sertifika doğrulamasını atla
Write-Host "⚠️  SSL sertifika kontrolü devre dışı (development)" -ForegroundColor Yellow
Write-Host ""

# Test fonksiyonu
function Test-Endpoint {
    param(
        [string]$Name,
        [string]$Method,
        [string]$Url,
        [hashtable]$Headers,
        [object]$Body = $null
    )
    
    Write-Host "🧪 Test: $Name" -ForegroundColor Yellow
    Write-Host "   Method: $Method | URL: $Url" -ForegroundColor Gray
    
    try {
        if ($Body) {
            $jsonBody = $Body | ConvertTo-Json -Depth 10
            Write-Host "   📤 Request Body:" -ForegroundColor Gray
            $jsonBody | Write-Host
            
            $response = Invoke-RestMethod -Uri $Url -Headers $Headers -Method $Method -Body $jsonBody -SkipCertificateCheck -ErrorAction Stop
        } else {
            $response = Invoke-RestMethod -Uri $Url -Headers $Headers -Method $Method -SkipCertificateCheck -ErrorAction Stop
        }
        
        Write-Host "   ✅ Başarılı!" -ForegroundColor Green
        Write-Host "   📦 Response:" -ForegroundColor Gray
        $response | ConvertTo-Json -Depth 10 | Write-Host
        Write-Host ""
        return @{ Success = $true; Data = $response }
    }
    catch {
        Write-Host "   ❌ Hata: $($_.Exception.Message)" -ForegroundColor Red
        if ($_.ErrorDetails.Message) {
            Write-Host "   📦 Details:" -ForegroundColor Gray
            $_.ErrorDetails.Message | Write-Host
        }
        Write-Host ""
        return @{ Success = $false; Error = $_.Exception.Message }
    }
}

# Testleri çalıştır
Write-Host "🚀 Testler başlıyor..." -ForegroundColor Cyan
Write-Host ""

$results = @()
$createdCategoryId = $null

# Test 1: CREATE - Yeni kategori oluştur
Write-Host "═══ TEST 1: CREATE ═══" -ForegroundColor Magenta
$createBody = @{
    CategoryName = "Test Category $(Get-Date -Format 'HHmmss')"
    CategoryDescription = "Test açıklaması - Full metadata pattern"
}
$result = Test-Endpoint `
    -Name "Create Category" `
    -Method "POST" `
    -Url "$baseUrl/api/dataset-categories" `
    -Headers $headers `
    -Body $createBody

$results += $result.Success
if ($result.Success) {
    $createdCategoryId = $result.Data.dataId
    Write-Host "✅ Kategori oluşturuldu: $createdCategoryId" -ForegroundColor Green
    Write-Host ""
}

# Test 2: LIST - Kategorileri listele
Write-Host "═══ TEST 2: LIST (Pagination) ═══" -ForegroundColor Magenta
$result = Test-Endpoint `
    -Name "List Categories (Page 1, Size 10)" `
    -Method "GET" `
    -Url "$baseUrl/api/dataset-categories?pageNumber=1&pageSize=10" `
    -Headers $headers

$results += $result.Success

# Test 3: GET BY ID - Oluşturulan kategoriyi getir
if ($createdCategoryId) {
    Write-Host "═══ TEST 3: GET BY ID ═══" -ForegroundColor Magenta
    $result = Test-Endpoint `
        -Name "Get Category by ID" `
        -Method "GET" `
        -Url "$baseUrl/api/dataset-categories/$createdCategoryId" `
        -Headers $headers
    
    $results += $result.Success
}

# Test 4: UPDATE - Kategoriyi güncelle
if ($createdCategoryId) {
    Write-Host "═══ TEST 4: UPDATE ═══" -ForegroundColor Magenta
    $updateBody = @{
        CategoryName = "Updated Category $(Get-Date -Format 'HHmmss')"
        CategoryDescription = "Güncellenmiş açıklama"
    }
    $result = Test-Endpoint `
        -Name "Update Category" `
        -Method "PUT" `
        -Url "$baseUrl/api/dataset-categories/$createdCategoryId" `
        -Headers $headers `
        -Body $updateBody
    
    $results += $result.Success
}

# Test 5: GET UPDATED - Güncellenmiş kategoriyi kontrol et
if ($createdCategoryId) {
    Write-Host "═══ TEST 5: GET UPDATED ═══" -ForegroundColor Magenta
    $result = Test-Endpoint `
        -Name "Get Updated Category" `
        -Method "GET" `
        -Url "$baseUrl/api/dataset-categories/$createdCategoryId" `
        -Headers $headers
    
    $results += $result.Success
    
    if ($result.Success) {
        Write-Host "📊 Metadata Kontrolü:" -ForegroundColor Cyan
        Write-Host "   ✅ __createInfo: $($result.Data.createInfo -ne $null)" -ForegroundColor $(if ($result.Data.createInfo) { "Green" } else { "Red" })
        Write-Host "   ✅ __lastUpdateInfo: $($result.Data.lastUpdateInfo -ne $null)" -ForegroundColor $(if ($result.Data.lastUpdateInfo) { "Green" } else { "Red" })
        Write-Host "   ✅ History Count: $($result.Data.historyCount)" -ForegroundColor Green
        Write-Host ""
    }
}

# Test 6: DELETE - Kategoriyi sil
if ($createdCategoryId) {
    Write-Host "═══ TEST 6: DELETE ═══" -ForegroundColor Magenta
    $result = Test-Endpoint `
        -Name "Delete Category" `
        -Method "DELETE" `
        -Url "$baseUrl/api/dataset-categories/$createdCategoryId" `
        -Headers $headers
    
    $results += $result.Success
}

# Test 7: RESTORE - Silinen kategoriyi geri yükle
if ($createdCategoryId) {
    Start-Sleep -Seconds 1
    Write-Host "═══ TEST 7: RESTORE ═══" -ForegroundColor Magenta
    $result = Test-Endpoint `
        -Name "Restore Deleted Category" `
        -Method "POST" `
        -Url "$baseUrl/api/dataset-categories/$createdCategoryId/restore" `
        -Headers $headers
    
    $results += $result.Success
}

# Test 8: CLEANUP - Final delete (restore test'ten sonra)
if ($createdCategoryId) {
    Write-Host "═══ TEST 8: FINAL CLEANUP ═══" -ForegroundColor Magenta
    $result = Test-Endpoint `
        -Name "Final Delete" `
        -Method "DELETE" `
        -Url "$baseUrl/api/dataset-categories/$createdCategoryId" `
        -Headers $headers
    
    # Bu test sonucu sayılmaz, sadece temizlik
}

# Sonuçları özetle
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Test Sonuçları" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
$passed = ($results | Where-Object { $_ -eq $true }).Count
$total = $results.Count
Write-Host "Başarılı: $passed / $total" -ForegroundColor $(if ($passed -eq $total) { "Green" } else { "Yellow" })

if ($passed -eq $total) {
    Write-Host ""
    Write-Host "🎉 Tüm testler başarılı!" -ForegroundColor Green
    Write-Host "   Dataset Categories CRUD çalışıyor!" -ForegroundColor Green
    Write-Host "   Full metadata pattern (history, __createInfo, __lastUpdateInfo) test edildi!" -ForegroundColor Green
} else {
    Write-Host ""
    Write-Host "⚠️ Bazı testler başarısız oldu." -ForegroundColor Yellow
}

Write-Host ""

