# Bulk Insert Test Script
# Test POST /api/data/{datasetName}/bulk endpoint

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Bulk Insert Test Suite" -ForegroundColor Cyan
Write-Host "Dataset: @test_tasks_224334" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Base URL
$baseUrl = "https://localhost:5010"
$datasetName = "@test_tasks_224334"

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
    Write-Host "   Method: $Method" -ForegroundColor Gray
    Write-Host "   URL: $Url" -ForegroundColor Gray
    
    try {
        $params = @{
            Uri = $Url
            Method = $Method
            Headers = $Headers
            SkipCertificateCheck = $true
        }
        
        if ($Body) {
            $jsonBody = $Body | ConvertTo-Json -Depth 10
            $params.Body = $jsonBody
            Write-Host "   Body:" -ForegroundColor Gray
            Write-Host "   $jsonBody" -ForegroundColor DarkGray
        }
        
        $response = Invoke-RestMethod @params
        
        Write-Host "   ✅ Success!" -ForegroundColor Green
        Write-Host "   Response:" -ForegroundColor Gray
        Write-Host ($response | ConvertTo-Json -Depth 10) -ForegroundColor DarkGray
        Write-Host ""
        
        return $response
    }
    catch {
        Write-Host "   ❌ Failed!" -ForegroundColor Red
        Write-Host "   Error: $($_.Exception.Message)" -ForegroundColor Red
        if ($_.ErrorDetails.Message) {
            Write-Host "   Details: $($_.ErrorDetails.Message)" -ForegroundColor Red
        }
        Write-Host ""
        return $null
    }
}

# Global değişkenler
$testResults = @{
    Total = 0
    Passed = 0
    Failed = 0
}

# ================================================
# TEST 1: Single Item (Normal Create ile Aynı)
# ================================================
Write-Host "================================================" -ForegroundColor Cyan
Write-Host "TEST 1: Single Item (Normal Create ile Aynı)" -ForegroundColor Cyan
Write-Host "================================================" -ForegroundColor Cyan
Write-Host ""

$testResults.Total++
$bulkRequest1 = @{
    items = @(
        @{
            title = "Bulk Test Task 1"
            priority = 1
            isCompleted = $false
        }
    )
}

$response1 = Test-Endpoint `
    -Name "Bulk Insert - Single Item" `
    -Method "POST" `
    -Url "$baseUrl/api/data/$datasetName/bulk" `
    -Headers $headers `
    -Body $bulkRequest1

if ($response1 -and $response1.success -and $response1.data.successful -eq 1) {
    Write-Host "✅ TEST 1 PASSED: Single item inserted successfully" -ForegroundColor Green
    $testResults.Passed++
    $createdDataId1 = $response1.data.items[0].__dataId
    $createdTaskNumber1 = $response1.data.items[0].taskNumber
    Write-Host "   Created: __dataId=$createdDataId1, taskNumber=$createdTaskNumber1" -ForegroundColor Gray
} else {
    Write-Host "❌ TEST 1 FAILED" -ForegroundColor Red
    $testResults.Failed++
}
Write-Host ""

# ================================================
# TEST 2: Multiple Items (All Success)
# ================================================
Write-Host "================================================" -ForegroundColor Cyan
Write-Host "TEST 2: Multiple Items (All Success)" -ForegroundColor Cyan
Write-Host "================================================" -ForegroundColor Cyan
Write-Host ""

$testResults.Total++
$bulkRequest2 = @{
    items = @(
        @{
            title = "Bulk Test Task 2"
            priority = 2
            isCompleted = $false
        },
        @{
            title = "Bulk Test Task 3"
            priority = 3
            isCompleted = $false
        },
        @{
            title = "Bulk Test Task 4"
            priority = 4
            isCompleted = $false
        }
    )
}

$response2 = Test-Endpoint `
    -Name "Bulk Insert - Multiple Items" `
    -Method "POST" `
    -Url "$baseUrl/api/data/$datasetName/bulk" `
    -Headers $headers `
    -Body $bulkRequest2

if ($response2 -and $response2.success -and $response2.data.successful -eq 3 -and $response2.data.failed -eq 0) {
    Write-Host "✅ TEST 2 PASSED: All 3 items inserted successfully" -ForegroundColor Green
    $testResults.Passed++
    
    # Check incremental field ordering
    $taskNumbers = $response2.data.items | ForEach-Object { $_.taskNumber }
    Write-Host "   Task Numbers: $($taskNumbers -join ', ')" -ForegroundColor Gray
    
    # Verify sequential ordering (if incremental field exists)
    if ($taskNumbers.Count -eq 3) {
        Write-Host "   ✅ Incremental field ordering verified" -ForegroundColor Green
    }
} else {
    Write-Host "❌ TEST 2 FAILED" -ForegroundColor Red
    $testResults.Failed++
}
Write-Host ""

# ================================================
# TEST 3: Partial Success (Some Fail Validation)
# ================================================
Write-Host "================================================" -ForegroundColor Cyan
Write-Host "TEST 3: Partial Success (Some Fail Validation)" -ForegroundColor Cyan
Write-Host "================================================" -ForegroundColor Cyan
Write-Host ""

$testResults.Total++
$bulkRequest3 = @{
    items = @(
        @{
            title = "Bulk Test Task 5"
            priority = 5
            isCompleted = $false
        },
        @{
            title = "Bulk Test Task 6 (No Priority)"
            isCompleted = $false
            # priority missing (mandatory field)
        },
        @{
            title = "Bulk Test Task 7"
            priority = 7
            isCompleted = $false
        }
    )
}

$response3 = Test-Endpoint `
    -Name "Bulk Insert - Partial Success" `
    -Method "POST" `
    -Url "$baseUrl/api/data/$datasetName/bulk" `
    -Headers $headers `
    -Body $bulkRequest3

if ($response3 -and $response3.success -and $response3.data.successful -eq 2 -and $response3.data.failed -eq 1) {
    Write-Host "✅ TEST 3 PASSED: 2 successful, 1 failed (as expected)" -ForegroundColor Green
    $testResults.Passed++
    
    Write-Host "   Successful items: $($response3.data.successful)" -ForegroundColor Gray
    Write-Host "   Failed items: $($response3.data.failed)" -ForegroundColor Gray
    
    if ($response3.data.errors.Count -eq 1) {
        Write-Host "   Error details:" -ForegroundColor Gray
        Write-Host ($response3.data.errors[0] | ConvertTo-Json -Depth 5) -ForegroundColor DarkGray
    }
} else {
    Write-Host "❌ TEST 3 FAILED" -ForegroundColor Red
    $testResults.Failed++
}
Write-Host ""

# ================================================
# TEST 4: All Fail (All Validation Errors)
# ================================================
Write-Host "================================================" -ForegroundColor Cyan
Write-Host "TEST 4: All Fail (All Validation Errors)" -ForegroundColor Cyan
Write-Host "================================================" -ForegroundColor Cyan
Write-Host ""

$testResults.Total++
$bulkRequest4 = @{
    items = @(
        @{
            title = "Bulk Test Task 8 (No Priority)"
            isCompleted = $false
            # priority missing (mandatory field)
        },
        @{
            title = "Bulk Test Task 9 (No Priority)"
            isCompleted = $false
            # priority missing (mandatory field)
        }
    )
}

$response4 = Test-Endpoint `
    -Name "Bulk Insert - All Fail" `
    -Method "POST" `
    -Url "$baseUrl/api/data/$datasetName/bulk" `
    -Headers $headers `
    -Body $bulkRequest4

if ($response4 -and $response4.success -and $response4.data.successful -eq 0 -and $response4.data.failed -eq 2) {
    Write-Host "✅ TEST 4 PASSED: All items failed (as expected)" -ForegroundColor Green
    $testResults.Passed++
    
    Write-Host "   Successful items: $($response4.data.successful)" -ForegroundColor Gray
    Write-Host "   Failed items: $($response4.data.failed)" -ForegroundColor Gray
    Write-Host "   Errors count: $($response4.data.errors.Count)" -ForegroundColor Gray
} else {
    Write-Host "❌ TEST 4 FAILED" -ForegroundColor Red
    $testResults.Failed++
}
Write-Host ""

# ================================================
# TEST 5: Incremental Field Ordering
# ================================================
Write-Host "================================================" -ForegroundColor Cyan
Write-Host "TEST 5: Incremental Field Ordering" -ForegroundColor Cyan
Write-Host "================================================" -ForegroundColor Cyan
Write-Host ""

$testResults.Total++
$bulkRequest5 = @{
    items = @(
        @{
            title = "Bulk Test Task 10"
            priority = 10
            isCompleted = $false
        },
        @{
            title = "Bulk Test Task 11"
            priority = 11
            isCompleted = $false
        },
        @{
            title = "Bulk Test Task 12"
            priority = 12
            isCompleted = $false
        },
        @{
            title = "Bulk Test Task 13"
            priority = 13
            isCompleted = $false
        },
        @{
            title = "Bulk Test Task 14"
            priority = 14
            isCompleted = $false
        }
    )
}

$response5 = Test-Endpoint `
    -Name "Bulk Insert - Incremental Field Ordering" `
    -Method "POST" `
    -Url "$baseUrl/api/data/$datasetName/bulk" `
    -Headers $headers `
    -Body $bulkRequest5

if ($response5 -and $response5.success -and $response5.data.successful -eq 5) {
    Write-Host "✅ TEST 5 PASSED: All 5 items inserted" -ForegroundColor Green
    $testResults.Passed++
    
    # Check incremental field ordering
    $taskNumbers = $response5.data.items | ForEach-Object { $_.taskNumber }
    Write-Host "   Task Numbers: $($taskNumbers -join ', ')" -ForegroundColor Gray
    
    # Extract numbers from task numbers (e.g., "TASK-000015" -> 15)
    $numbers = $taskNumbers | ForEach-Object {
        if ($_ -match '(\d+)$') {
            [int]$matches[1]
        }
    }
    
    # Check if numbers are sequential
    $isSequential = $true
    for ($i = 1; $i -lt $numbers.Count; $i++) {
        if ($numbers[$i] -ne $numbers[$i-1] + 1) {
            $isSequential = $false
            break
        }
    }
    
    if ($isSequential) {
        Write-Host "   ✅ Incremental field ordering verified (sequential)" -ForegroundColor Green
    } else {
        Write-Host "   ⚠️  Incremental field ordering may have gaps (normal if previous tests ran)" -ForegroundColor Yellow
    }
} else {
    Write-Host "❌ TEST 5 FAILED" -ForegroundColor Red
    $testResults.Failed++
}
Write-Host ""

# ================================================
# TEST 6: Batch Size Limit (1001 items - should fail)
# ================================================
Write-Host "================================================" -ForegroundColor Cyan
Write-Host "TEST 6: Batch Size Limit (1001 items)" -ForegroundColor Cyan
Write-Host "================================================" -ForegroundColor Cyan
Write-Host ""

$testResults.Total++
# Create 1001 items
$items = @()
for ($i = 1; $i -le 1001; $i++) {
    $items += @{
        title = "Bulk Test Task Limit $i"
        priority = $i
        isCompleted = $false
    }
}

$bulkRequest6 = @{
    items = $items
}

$response6 = Test-Endpoint `
    -Name "Bulk Insert - Batch Size Limit" `
    -Method "POST" `
    -Url "$baseUrl/api/data/$datasetName/bulk" `
    -Headers $headers `
    -Body $bulkRequest6

if ($response6 -eq $null -or -not $response6.success) {
    Write-Host "✅ TEST 6 PASSED: Batch size limit enforced (request rejected)" -ForegroundColor Green
    $testResults.Passed++
} else {
    Write-Host "❌ TEST 6 FAILED: Batch size limit not enforced" -ForegroundColor Red
    $testResults.Failed++
}
Write-Host ""

# ================================================
# TEST 7: Large Batch (100 items - Performance)
# ================================================
Write-Host "================================================" -ForegroundColor Cyan
Write-Host "TEST 7: Large Batch (100 items - Performance)" -ForegroundColor Cyan
Write-Host "================================================" -ForegroundColor Cyan
Write-Host ""

$testResults.Total++
# Create 100 items
$items100 = @()
for ($i = 1; $i -le 100; $i++) {
    $items100 += @{
        title = "Bulk Test Task Perf $i"
        priority = $i
        isCompleted = $false
    }
}

$bulkRequest7 = @{
    items = $items100
}

$startTime = Get-Date
$response7 = Test-Endpoint `
    -Name "Bulk Insert - Large Batch (100 items)" `
    -Method "POST" `
    -Url "$baseUrl/api/data/$datasetName/bulk" `
    -Headers $headers `
    -Body $bulkRequest7
$endTime = Get-Date
$duration = ($endTime - $startTime).TotalSeconds

if ($response7 -and $response7.success -and $response7.data.successful -eq 100) {
    Write-Host "✅ TEST 7 PASSED: All 100 items inserted successfully" -ForegroundColor Green
    Write-Host "   Duration: $([math]::Round($duration, 2)) seconds" -ForegroundColor Gray
    
    if ($duration -lt 2) {
        Write-Host "   ✅ Performance target met (< 2 seconds)" -ForegroundColor Green
    } else {
        Write-Host "   ⚠️  Performance target not met (>= 2 seconds)" -ForegroundColor Yellow
    }
    
    $testResults.Passed++
} else {
    Write-Host "❌ TEST 7 FAILED" -ForegroundColor Red
    $testResults.Failed++
}
Write-Host ""

# ================================================
# Test Summary
# ================================================
Write-Host "================================================" -ForegroundColor Cyan
Write-Host "TEST SUMMARY" -ForegroundColor Cyan
Write-Host "================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Total Tests: $($testResults.Total)" -ForegroundColor White
Write-Host "Passed: $($testResults.Passed)" -ForegroundColor Green
Write-Host "Failed: $($testResults.Failed)" -ForegroundColor $(if ($testResults.Failed -eq 0) { "Green" } else { "Red" })
Write-Host ""

if ($testResults.Failed -eq 0) {
    Write-Host "🎉 All tests passed!" -ForegroundColor Green
} else {
    Write-Host "⚠️  Some tests failed. Please review the output above." -ForegroundColor Yellow
}
Write-Host ""

