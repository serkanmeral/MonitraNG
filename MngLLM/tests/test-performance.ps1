# Performance Test Script
# Bu script optimizasyonların performans etkisini ölçer

param(
    [string]$BaseUrl = "http://localhost:5030"
)

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Chatbot Performance Test" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Test 1: First request (no cache)
Write-Host "[Test 1] First request (cold start, no cache)..." -ForegroundColor Yellow
$startTime = Get-Date
try {
    $body = @{
        message = "Kullanıcı nasıl oluşturulur?"
        language = "tr"
    } | ConvertTo-Json
    
    $response1 = Invoke-RestMethod -Uri "$BaseUrl/api/v1/chatbot/chat" -Method POST -Body $body -ContentType "application/json"
    $endTime = Get-Date
    $duration = ($endTime - $startTime).TotalSeconds
    
    Write-Host "✓ Response received" -ForegroundColor Green
    Write-Host "  Duration: $([math]::Round($duration, 2)) seconds" -ForegroundColor Gray
    Write-Host "  Intent: $($response1.Intent) (Confidence: $([math]::Round($response1.IntentConfidence, 2)))" -ForegroundColor Gray
    Write-Host "  Cached: $($response1.Metadata.cached)" -ForegroundColor Gray
    Write-Host "  Documentation Sources: $($response1.DocumentationSources.Count)" -ForegroundColor Gray
} catch {
    Write-Host "✗ Error: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

Write-Host ""

# Test 2: Cached request (same message)
Write-Host "[Test 2] Cached request (same message, should be fast)..." -ForegroundColor Yellow
$startTime = Get-Date
try {
    $body = @{
        message = "Kullanıcı nasıl oluşturulur?"
        language = "tr"
    } | ConvertTo-Json
    
    $response2 = Invoke-RestMethod -Uri "$BaseUrl/api/v1/chatbot/chat" -Method POST -Body $body -ContentType "application/json"
    $endTime = Get-Date
    $duration = ($endTime - $startTime).TotalSeconds
    
    Write-Host "✓ Response received" -ForegroundColor Green
    Write-Host "  Duration: $([math]::Round($duration, 2)) seconds" -ForegroundColor Gray
    Write-Host "  Cached: $($response2.Metadata.cached)" -ForegroundColor Gray
    
    if ($response2.Metadata.cached -eq $true) {
        Write-Host "  ✓ Cache hit! Performance improvement: $([math]::Round(($duration / ($response1.Metadata.timestamp - $startTime).TotalSeconds) * 100, 1))%" -ForegroundColor Green
    } else {
        Write-Host "  ⚠ Cache miss" -ForegroundColor Yellow
    }
} catch {
    Write-Host "✗ Error: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""

# Test 3: Different message (no cache)
Write-Host "[Test 3] Different message (no cache)..." -ForegroundColor Yellow
$startTime = Get-Date
try {
    $body = @{
        message = "Dataset nasıl oluşturulur?"
        language = "tr"
    } | ConvertTo-Json
    
    $response3 = Invoke-RestMethod -Uri "$BaseUrl/api/v1/chatbot/chat" -Method POST -Body $body -ContentType "application/json"
    $endTime = Get-Date
    $duration = ($endTime - $startTime).TotalSeconds
    
    Write-Host "✓ Response received" -ForegroundColor Green
    Write-Host "  Duration: $([math]::Round($duration, 2)) seconds" -ForegroundColor Gray
    Write-Host "  Intent: $($response3.Intent) (Confidence: $([math]::Round($response3.IntentConfidence, 2)))" -ForegroundColor Gray
    Write-Host "  Cached: $($response3.Metadata.cached)" -ForegroundColor Gray
} catch {
    Write-Host "✗ Error: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""

# Test 4: Simple greeting (should be fast with keyword detection)
Write-Host "[Test 4] Simple greeting (keyword-based intent, should be fast)..." -ForegroundColor Yellow
$startTime = Get-Date
try {
    $body = @{
        message = "Merhaba"
        language = "tr"
    } | ConvertTo-Json
    
    $response4 = Invoke-RestMethod -Uri "$BaseUrl/api/v1/chatbot/chat" -Method POST -Body $body -ContentType "application/json"
    $endTime = Get-Date
    $duration = ($endTime - $startTime).TotalSeconds
    
    Write-Host "✓ Response received" -ForegroundColor Green
    Write-Host "  Duration: $([math]::Round($duration, 2)) seconds" -ForegroundColor Gray
    Write-Host "  Intent: $($response4.Intent) (Confidence: $([math]::Round($response4.IntentConfidence, 2)))" -ForegroundColor Gray
    Write-Host "  Note: Intent detection should be ~0.1s (keyword-based)" -ForegroundColor DarkGray
} catch {
    Write-Host "✗ Error: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""

# Test 5: API docs query
Write-Host "[Test 5] API docs query..." -ForegroundColor Yellow
$startTime = Get-Date
try {
    $body = @{
        message = "API dokümantasyonu"
        language = "tr"
    } | ConvertTo-Json
    
    $response5 = Invoke-RestMethod -Uri "$BaseUrl/api/v1/chatbot/chat" -Method POST -Body $body -ContentType "application/json"
    $endTime = Get-Date
    $duration = ($endTime - $startTime).TotalSeconds
    
    Write-Host "✓ Response received" -ForegroundColor Green
    Write-Host "  Duration: $([math]::Round($duration, 2)) seconds" -ForegroundColor Gray
    Write-Host "  Intent: $($response5.Intent) (Confidence: $([math]::Round($response5.IntentConfidence, 2)))" -ForegroundColor Gray
    Write-Host "  Documentation Sources: $($response5.DocumentationSources.Count)" -ForegroundColor Gray
} catch {
    Write-Host "✗ Error: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""

# Summary
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Performance Summary" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Optimizations applied:" -ForegroundColor Yellow
Write-Host "  ✓ Keyword-based intent detection" -ForegroundColor Green
Write-Host "  ✓ Prompt optimization (shorter prompts)" -ForegroundColor Green
Write-Host "  ✓ Response caching (1 hour TTL)" -ForegroundColor Green
Write-Host "  ✓ Retry mechanism (2 retries with exponential backoff)" -ForegroundColor Green
Write-Host ""
Write-Host "Expected improvements:" -ForegroundColor Yellow
Write-Host "  - Intent detection: 5-10s → 0.1s (keyword-based)" -ForegroundColor Gray
Write-Host "  - Main response: 20-60s → 10-30s (prompt optimization)" -ForegroundColor Gray
Write-Host "  - Cache hits: <1s (95%+ faster)" -ForegroundColor Gray
Write-Host ""
