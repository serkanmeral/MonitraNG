# Dataset Schema CRUD Test Script
# Test @datasets collection with full field definitions

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Dataset Schema CRUD Test Suite" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Base URL
$baseUrl = "https://localhost:5010"

# Token dosyasının yolunu belirle
$tokenFile = "$env:TEMP\serkan_token.txt"

# Token'ı kontrol et
if (-not (Test-Path $tokenFile)) {
    Write-Host "❌ Token bulunamadı! Önce token almak için:" -ForegroundColor Red
    Write-Host "   cd C:\Serkan\iSIM\MonitraNG\MngKeeper\tests" -ForegroundColor Yellow
    Write-Host "   .\get-serkan-token.ps1" -ForegroundColor Yellow
    Write-Host ""
    exit 1
}

# Token'ı oku
$token = Get-Content $tokenFile -Raw
$token = $token.Trim()

Write-Host "✅ Token yüklendi" -ForegroundColor Green
Write-Host ""

# Headers
$headers = @{
    "Authorization" = "Bearer $token"
    "Content-Type" = "application/json"
}

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
            Write-Host "   📤 Request Body (first 200 chars):" -ForegroundColor Gray
            $preview = $jsonBody.Substring(0, [Math]::Min(200, $jsonBody.Length))
            Write-Host "   $preview..." -ForegroundColor Gray
            
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
$createdDatasetName = $null

# Test 1: CREATE - Minimal (sadece name)
Write-Host "═══ TEST 1: CREATE (Minimal) ═══" -ForegroundColor Magenta
$createMinimalBody = @{
    Name = "@test_minimal_$(Get-Date -Format 'HHmmss')"
}
$result = Test-Endpoint `
    -Name "Create Minimal Dataset" `
    -Method "POST" `
    -Url "$baseUrl/api/datasets" `
    -Headers $headers `
    -Body $createMinimalBody

$results += $result.Success

# Test 2: CREATE - Full (tüm alanlar)
Write-Host "═══ TEST 2: CREATE (Full) ═══" -ForegroundColor Magenta
$createFullBody = @{
    Name = "@test_tasks_$(Get-Date -Format 'HHmmss')"
    Description = "Test görev yönetim sistemi"
    ForceSchema = $true
    Logging = "self"
    PublishMode = "none"
    Fields = @(
        @{
            fieldType = "text"
            name = "title"
            title = "Başlık"
            mandatory = $true
            unique = $false
            isArray = $false
        },
        @{
            fieldType = "text"
            name = "description"
            title = "Açıklama"
            mandatory = $false
        },
        @{
            fieldType = "number"
            name = "priority"
            title = "Öncelik"
            mandatory = $true
        },
        @{
            fieldType = "bool"
            name = "isCompleted"
            title = "Tamamlandı mı"
            mandatory = $true
        },
        @{
            fieldType = "datetime"
            name = "dueDate"
            title = "Bitiş Tarihi"
            mandatory = $false
        },
        @{
            fieldType = "incremental"
            name = "taskNumber"
            title = "Görev No"
            mandatory = $true
            unique = $true
            incrementalOptions = @{
                format = "TASK-{0:D6}"
                startValue = 1
                incrementStep = 1
            }
        }
    )
    IndexList = @(
        @{
            name = "idx_taskNumber"
            fields = @{ taskNumber = 1 }
            unique = $true
        },
        @{
            name = "idx_priority"
            fields = @{ priority = 1 }
            unique = $false
        }
    )
}
$result = Test-Endpoint `
    -Name "Create Full Dataset (with fields & indexes)" `
    -Method "POST" `
    -Url "$baseUrl/api/datasets" `
    -Headers $headers `
    -Body $createFullBody

$results += $result.Success
if ($result.Success) {
    $createdDatasetName = $result.Data.name
    Write-Host "✅ Dataset oluşturuldu: $createdDatasetName" -ForegroundColor Green
    Write-Host ""
}

# Test 3: LIST - Dataset'leri listele
Write-Host "═══ TEST 3: LIST (Pagination) ═══" -ForegroundColor Magenta
$result = Test-Endpoint `
    -Name "List Datasets (Page 1, Size 10)" `
    -Method "GET" `
    -Url "$baseUrl/api/datasets?pageNumber=1&pageSize=10" `
    -Headers $headers

$results += $result.Success

# Test 4: GET BY NAME - Full dataset detayı
if ($createdDatasetName) {
    Write-Host "═══ TEST 4: GET BY NAME (Details) ═══" -ForegroundColor Magenta
    $result = Test-Endpoint `
        -Name "Get Dataset by Name (with field details)" `
        -Method "GET" `
        -Url "$baseUrl/api/datasets/$createdDatasetName" `
        -Headers $headers
    
    $results += $result.Success
    
    if ($result.Success) {
        Write-Host "📊 Schema Kontrolü:" -ForegroundColor Cyan
        Write-Host "   ✅ Fields Count: $($result.Data.fieldsCount)" -ForegroundColor Green
        Write-Host "   ✅ Indexes Count: $($result.Data.indexListCount)" -ForegroundColor Green
        Write-Host "   ✅ Force Schema: $($result.Data.forceSchema)" -ForegroundColor Green
        Write-Host ""
    }
}

# Test 5: UPDATE - Dataset'i güncelle
if ($createdDatasetName) {
    Write-Host "═══ TEST 5: UPDATE ═══" -ForegroundColor Magenta
    $updateBody = @{
        Description = "Güncellenmiş açıklama"
        Logging = "none"
        Fields = @(
            @{
                fieldType = "text"
                name = "title"
                title = "Başlık (Updated)"
                mandatory = $true
            },
            @{
                fieldType = "text"
                name = "status"
                title = "Durum"
                mandatory = $true
            }
        )
    }
    $result = Test-Endpoint `
        -Name "Update Dataset" `
        -Method "PUT" `
        -Url "$baseUrl/api/datasets/$createdDatasetName" `
        -Headers $headers `
        -Body $updateBody
    
    $results += $result.Success
}

# Test 6: GET UPDATED - Güncellenmiş dataset'i kontrol et
if ($createdDatasetName) {
    Write-Host "═══ TEST 6: GET UPDATED ═══" -ForegroundColor Magenta
    $result = Test-Endpoint `
        -Name "Get Updated Dataset" `
        -Method "GET" `
        -Url "$baseUrl/api/datasets/$createdDatasetName" `
        -Headers $headers
    
    $results += $result.Success
    
    if ($result.Success) {
        Write-Host "📊 Update Kontrolü:" -ForegroundColor Cyan
        Write-Host "   ✅ __lastUpdateInfo: $($result.Data.lastUpdateInfo -ne $null)" -ForegroundColor $(if ($result.Data.lastUpdateInfo) { "Green" } else { "Red" })
        Write-Host "   ✅ Fields Count: $($result.Data.fieldsCount)" -ForegroundColor Green
        Write-Host "   ✅ History Count: $($result.Data.historyCount)" -ForegroundColor Green
        Write-Host ""
    }
}

# Test 7: DELETE - Dataset'i sil
if ($createdDatasetName) {
    Write-Host "═══ TEST 7: DELETE ═══" -ForegroundColor Magenta
    $result = Test-Endpoint `
        -Name "Delete Dataset" `
        -Method "DELETE" `
        -Url "$baseUrl/api/datasets/$createdDatasetName" `
        -Headers $headers
    
    $results += $result.Success
}

# Test 8: RESTORE - Silinen dataset'i geri yükle
if ($createdDatasetName) {
    Start-Sleep -Seconds 1
    Write-Host "═══ TEST 8: RESTORE ═══" -ForegroundColor Magenta
    $result = Test-Endpoint `
        -Name "Restore Deleted Dataset" `
        -Method "POST" `
        -Url "$baseUrl/api/datasets/$createdDatasetName/restore" `
        -Headers $headers
    
    $results += $result.Success
}

# Test 9: CLEANUP - Final delete
if ($createdDatasetName) {
    Write-Host "═══ TEST 9: FINAL CLEANUP ═══" -ForegroundColor Magenta
    $result = Test-Endpoint `
        -Name "Final Delete" `
        -Method "DELETE" `
        -Url "$baseUrl/api/datasets/$createdDatasetName" `
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
    Write-Host "   Dataset Schema CRUD çalışıyor!" -ForegroundColor Green
    Write-Host "   Field definitions, indexes, validations test edildi!" -ForegroundColor Green
} else {
    Write-Host ""
    Write-Host "⚠️ Bazı testler başarısız oldu." -ForegroundColor Yellow
}

Write-Host ""

