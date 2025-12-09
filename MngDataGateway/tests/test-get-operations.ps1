# GET Operations Test Script
# Tests all GET endpoints with various query parameters

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "GET Operations Test Suite" -ForegroundColor Cyan
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

# Test counter
$testCount = 0
$passCount = 0
$failCount = 0

# Test function
function Test-GetEndpoint {
    param(
        [string]$Name,
        [string]$Url,
        [hashtable]$Headers,
        [string]$ExpectedPattern = ""
    )
    
    $script:testCount++
    Write-Host "🧪 TEST $testCount : $Name" -ForegroundColor Yellow
    Write-Host "   URL: $Url" -ForegroundColor Gray
    
    try {
        $response = Invoke-RestMethod -Uri $Url -Headers $Headers -SkipCertificateCheck -ErrorAction Stop
        
        if ($response -is [Array]) {
            Write-Host "   ✅ Başarılı! (Array, Count: $($response.Count))" -ForegroundColor Green
            if ($response.Count -gt 0) {
                Write-Host "   📦 First item keys: $($response[0].Keys -join ', ')" -ForegroundColor Gray
            }
        } elseif ($response.query) {
            Write-Host "   ✅ Başarılı! (Query pipeline, Stages: $($response.query.Count))" -ForegroundColor Green
        } else {
            Write-Host "   ✅ Başarılı!" -ForegroundColor Green
            Write-Host "   📦 Response type: $($response.GetType().Name)" -ForegroundColor Gray
        }
        
        $script:passCount++
        Write-Host ""
        return @{ Success = $true; Data = $response }
    }
    catch {
        Write-Host "   ❌ Hata: $($_.Exception.Message)" -ForegroundColor Red
        if ($_.ErrorDetails.Message) {
            Write-Host "   📦 Details: $($_.ErrorDetails.Message)" -ForegroundColor Gray
        }
        $script:failCount++
        Write-Host ""
        return @{ Success = $false; Error = $_.Exception.Message }
    }
}

Write-Host "🚀 Testler başlıyor...`n" -ForegroundColor Cyan

# ============================================
# TEST 1: GET /api/data/@tasks (Basic List)
# ============================================
Write-Host "═══ TEST GROUP 1: Basic List Operations ═══" -ForegroundColor Magenta

Test-GetEndpoint `
    -Name "GET /api/data/@tasks (default)" `
    -Url "$baseUrl/api/data/@tasks" `
    -Headers $headers

Test-GetEndpoint `
    -Name "GET /api/data/@tasks?skip=0&limit=3" `
    -Url "$baseUrl/api/data/@tasks?skip=0&limit=3" `
    -Headers $headers

Test-GetEndpoint `
    -Name "GET /api/data/@tasks?expand=false" `
    -Url "$baseUrl/api/data/@tasks?expand=false" `
    -Headers $headers

# ============================================
# TEST 2: GET /api/data/@tasks (With Relations)
# ============================================
Write-Host "═══ TEST GROUP 2: Relation Expansion ═══" -ForegroundColor Magenta

Test-GetEndpoint `
    -Name "GET /api/data/@tasks?expand=true" `
    -Url "$baseUrl/api/data/@tasks?expand=true" `
    -Headers $headers

Test-GetEndpoint `
    -Name "GET /api/data/@tasks?expand=true&deep=1" `
    -Url "$baseUrl/api/data/@tasks?expand=true&deep=1" `
    -Headers $headers

# ============================================
# TEST 3: GET /api/data/@tasks (Filtering)
# ============================================
Write-Host "═══ TEST GROUP 3: Filtering ═══" -ForegroundColor Magenta

Test-GetEndpoint `
    -Name "GET /api/data/@tasks?filter=priority_value:gte:5" `
    -Url "$baseUrl/api/data/@tasks?filter=priority_value:gte:5" `
    -Headers $headers

Test-GetEndpoint `
    -Name "GET /api/data/@tasks?filter=isCompleted:eq:false" `
    -Url "$baseUrl/api/data/@tasks?filter=isCompleted:eq:false" `
    -Headers $headers

# ============================================
# TEST 4: GET /api/data/@tasks (Sorting)
# ============================================
Write-Host "═══ TEST GROUP 4: Sorting ═══" -ForegroundColor Magenta

Test-GetEndpoint `
    -Name "GET /api/data/@tasks?sort=priority_value,-task_number" `
    -Url "$baseUrl/api/data/@tasks?sort=priority_value,-task_number" `
    -Headers $headers

# ============================================
# TEST 5: GET /api/data/@tasks (Field Selection)
# ============================================
Write-Host "═══ TEST GROUP 5: Field Selection ═══" -ForegroundColor Magenta

Test-GetEndpoint `
    -Name "GET /api/data/@tasks?fields=title,task_number,priority_value" `
    -Url "$baseUrl/api/data/@tasks?fields=title,task_number,priority_value" `
    -Headers $headers

# ============================================
# TEST 6: GET /api/data/@tasks (Show Options)
# ============================================
Write-Host "═══ TEST GROUP 6: Show Options ═══" -ForegroundColor Magenta

Test-GetEndpoint `
    -Name "GET /api/data/@tasks?showQuery=true" `
    -Url "$baseUrl/api/data/@tasks?showQuery=true" `
    -Headers $headers

Test-GetEndpoint `
    -Name "GET /api/data/@tasks?showDataset=true" `
    -Url "$baseUrl/api/data/@tasks?showDataset=true" `
    -Headers $headers

Test-GetEndpoint `
    -Name "GET /api/data/@tasks?showHistory=true" `
    -Url "$baseUrl/api/data/@tasks?showHistory=true" `
    -Headers $headers

# ============================================
# TEST 7: GET /api/data/@tasks/{dataId}
# ============================================
Write-Host "═══ TEST GROUP 7: Get By ID ═══" -ForegroundColor Magenta

# First, get a task ID
try {
    $tasksResponse = Invoke-RestMethod -Uri "$baseUrl/api/data/@tasks?limit=1" -Headers $headers -SkipCertificateCheck
    if ($tasksResponse -is [Array] -and $tasksResponse.Count -gt 0) {
        $taskId = $tasksResponse[0].__dataId
        Write-Host "   📋 Using task ID: $taskId" -ForegroundColor Gray
        
        Test-GetEndpoint `
            -Name "GET /api/data/@tasks/$taskId" `
            -Url "$baseUrl/api/data/@tasks/$taskId" `
            -Headers $headers
        
        Test-GetEndpoint `
            -Name "GET /api/data/@tasks/$taskId?expand=true" `
            -Url "$baseUrl/api/data/@tasks/$taskId?expand=true" `
            -Headers $headers
        
        # TEST 14: Get By ID with invalid ID (should return 404)
        Test-GetEndpoint `
            -Name "GET /api/data/@tasks/invalid-id-12345" `
            -Url "$baseUrl/api/data/@tasks/invalid-id-12345" `
            -Headers $headers `
            -ExpectedStatus 404
    } else {
        Write-Host "   ⚠️  No tasks found to test GetById" -ForegroundColor Yellow
    }
} catch {
    Write-Host "   ⚠️  Could not get task ID for testing" -ForegroundColor Yellow
}

# ============================================
# TEST 8: POST /api/data/@tasks/query
# ============================================
Write-Host "═══ TEST GROUP 8: Advanced Query ═══" -ForegroundColor Magenta

$queryBody = @{
    match = @{
        priority_value = @{
            "$gte" = 5
        }
        isCompleted = $false
    }
} | ConvertTo-Json -Depth 5

$script:testCount++
Write-Host "🧪 TEST $testCount : POST /api/data/@tasks/query" -ForegroundColor Yellow
Write-Host "   URL: $baseUrl/api/data/@tasks/query" -ForegroundColor Gray
try {
    $response = Invoke-RestMethod -Uri "$baseUrl/api/data/@tasks/query?expand=true" -Method POST -Headers $headers -Body $queryBody -SkipCertificateCheck -ErrorAction Stop
    Write-Host "   ✅ Başarılı! (Count: $($response.Count))" -ForegroundColor Green
    $script:passCount++
} catch {
    Write-Host "   ❌ Hata: $($_.Exception.Message)" -ForegroundColor Red
    $script:failCount++
}
Write-Host ""

# ============================================
# TEST 9: POST /api/data/@tasks/aggregate
# ============================================
Write-Host "═══ TEST GROUP 9: Raw Aggregate ═══" -ForegroundColor Magenta

# Use simple JSON string for aggregate to avoid PowerShell serialization issues
$aggregateBody = '{"pipeline":[{"$match":{"priority_value":{"$gte":5}}},{"$project":{"title":1,"priority_value":1,"task_state":1}},{"$sort":{"priority_value":-1}},{"$limit":5}]}'

$script:testCount++
Write-Host "🧪 TEST $testCount : POST /api/data/@tasks/aggregate" -ForegroundColor Yellow
Write-Host "   URL: $baseUrl/api/data/@tasks/aggregate" -ForegroundColor Gray
try {
    $response = Invoke-RestMethod -Uri "$baseUrl/api/data/@tasks/aggregate" -Method POST -Headers $headers -Body $aggregateBody -SkipCertificateCheck -ErrorAction Stop
    Write-Host "   ✅ Başarılı! (Count: $($response.Count))" -ForegroundColor Green
    if ($response.Count -gt 0) {
        Write-Host "   📦 Result: $($response | ConvertTo-Json -Depth 3)" -ForegroundColor Gray
    }
    $script:passCount++
} catch {
    Write-Host "   ❌ Hata: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.ErrorDetails.Message) {
        Write-Host "   📦 Details: $($_.ErrorDetails.Message)" -ForegroundColor Gray
    }
    $script:failCount++
}
Write-Host ""

# ============================================
# TEST 10: POST /api/data/@tasks/queries/high_priority_tasks
# ============================================
Write-Host "═══ TEST GROUP 10: Predefined Query ═══" -ForegroundColor Magenta

$predefinedQueryBody = @{
    minPriority = 5
} | ConvertTo-Json

$script:testCount++
Write-Host "🧪 TEST $testCount : POST /api/data/@tasks/queries/high_priority_tasks" -ForegroundColor Yellow
Write-Host "   URL: $baseUrl/api/data/@tasks/queries/high_priority_tasks" -ForegroundColor Gray
try {
    $response = Invoke-RestMethod -Uri "$baseUrl/api/data/@tasks/queries/high_priority_tasks" -Method POST -Headers $headers -Body $predefinedQueryBody -SkipCertificateCheck -ErrorAction Stop
    Write-Host "   ✅ Başarılı! (Count: $($response.Count))" -ForegroundColor Green
    if ($response.Count -gt 0) {
        Write-Host "   📦 First item: $($response[0] | ConvertTo-Json -Depth 2)" -ForegroundColor Gray
    }
    $script:passCount++
} catch {
    Write-Host "   ❌ Hata: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.ErrorDetails.Message) {
        Write-Host "   📦 Details: $($_.ErrorDetails.Message)" -ForegroundColor Gray
    }
    $script:failCount++
}
Write-Host ""

# ============================================
# TEST SUMMARY
# ============================================
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "TEST SUMMARY" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Total Tests: $testCount" -ForegroundColor White
Write-Host "✅ Passed: $passCount" -ForegroundColor Green
Write-Host "❌ Failed: $failCount" -ForegroundColor Red
Write-Host ""

if ($failCount -eq 0) {
    Write-Host "🎉 All tests passed!" -ForegroundColor Green
} else {
    Write-Host "⚠️  Some tests failed. Please review the errors above." -ForegroundColor Yellow
}

Write-Host ""

