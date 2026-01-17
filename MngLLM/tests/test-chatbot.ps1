# Chatbot Test Script
# Bu script ChatbotController'ı test eder

param(
    [string]$BaseUrl = "http://localhost:5030",
    [string]$SessionId = ""
)

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Chatbot (Moni) Test Script" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Test 1: Basic greeting
Write-Host "[Test 1] Basic greeting..." -ForegroundColor Yellow
try {
    $body = @{
        message = "Merhaba, sen kimsin?"
        language = "tr"
    } | ConvertTo-Json
    
    if ($SessionId) {
        $body = @{
            message = "Merhaba, sen kimsin?"
            language = "tr"
            sessionId = $SessionId
        } | ConvertTo-Json
    }
    
    $response = Invoke-RestMethod -Uri "$BaseUrl/api/v1/chatbot/chat" -Method POST -Body $body -ContentType "application/json"
    $sessionId = $response.SessionId
    
    Write-Host "✓ Response received" -ForegroundColor Green
    Write-Host "  Session ID: $sessionId" -ForegroundColor Gray
    Write-Host "  Intent: $($response.Intent) (Confidence: $([math]::Round($response.IntentConfidence, 2)))" -ForegroundColor Gray
    Write-Host "  Answer: $($response.Answer.Substring(0, [Math]::Min(100, $response.Answer.Length)))..." -ForegroundColor Gray
} catch {
    Write-Host "✗ Error: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

Write-Host ""

# Test 2: Documentation query
Write-Host "[Test 2] Documentation query..." -ForegroundColor Yellow
try {
    $body = @{
        message = "Kullanıcı nasıl oluşturulur?"
        language = "tr"
        sessionId = $sessionId
    } | ConvertTo-Json
    
    $response = Invoke-RestMethod -Uri "$BaseUrl/api/v1/chatbot/chat" -Method POST -Body $body -ContentType "application/json"
    
    Write-Host "✓ Response received" -ForegroundColor Green
    Write-Host "  Intent: $($response.Intent) (Confidence: $([math]::Round($response.IntentConfidence, 2)))" -ForegroundColor Gray
    Write-Host "  Documentation Sources: $($response.DocumentationSources.Count)" -ForegroundColor Gray
    
    if ($response.DocumentationSources.Count -gt 0) {
        Write-Host "  Sources:" -ForegroundColor Gray
        $response.DocumentationSources | ForEach-Object {
            Write-Host "    - $($_.Title) ($($_.Service))" -ForegroundColor DarkGray
        }
    }
    
    Write-Host "  Answer: $($response.Answer.Substring(0, [Math]::Min(150, $response.Answer.Length)))..." -ForegroundColor Gray
} catch {
    Write-Host "✗ Error: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""

# Test 3: API documentation query
Write-Host "[Test 3] API documentation query..." -ForegroundColor Yellow
try {
    $body = @{
        message = "API dokümantasyonu hakkında bilgi ver"
        language = "tr"
        sessionId = $sessionId
    } | ConvertTo-Json
    
    $response = Invoke-RestMethod -Uri "$BaseUrl/api/v1/chatbot/chat" -Method POST -Body $body -ContentType "application/json"
    
    Write-Host "✓ Response received" -ForegroundColor Green
    Write-Host "  Intent: $($response.Intent) (Confidence: $([math]::Round($response.IntentConfidence, 2)))" -ForegroundColor Gray
    Write-Host "  Documentation Sources: $($response.DocumentationSources.Count)" -ForegroundColor Gray
} catch {
    Write-Host "✗ Error: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""

# Test 4: English language test
Write-Host "[Test 4] English language test..." -ForegroundColor Yellow
try {
    $body = @{
        message = "How do I create a dataset?"
        language = "en"
        sessionId = $sessionId
    } | ConvertTo-Json
    
    $response = Invoke-RestMethod -Uri "$BaseUrl/api/v1/chatbot/chat" -Method POST -Body $body -ContentType "application/json"
    
    Write-Host "✓ Response received" -ForegroundColor Green
    Write-Host "  Intent: $($response.Intent) (Confidence: $([math]::Round($response.IntentConfidence, 2)))" -ForegroundColor Gray
    Write-Host "  Answer: $($response.Answer.Substring(0, [Math]::Min(150, $response.Answer.Length)))..." -ForegroundColor Gray
} catch {
    Write-Host "✗ Error: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""

# Test 5: Context persistence (conversation history)
Write-Host "[Test 5] Context persistence test..." -ForegroundColor Yellow
try {
    # First message
    $body1 = @{
        message = "Benim adım Serkan"
        language = "tr"
        sessionId = $sessionId
    } | ConvertTo-Json
    
    $response1 = Invoke-RestMethod -Uri "$BaseUrl/api/v1/chatbot/chat" -Method POST -Body $body1 -ContentType "application/json"
    
    # Second message (should remember context)
    $body2 = @{
        message = "Benim adım ne?"
        language = "tr"
        sessionId = $sessionId
    } | ConvertTo-Json
    
    $response2 = Invoke-RestMethod -Uri "$BaseUrl/api/v1/chatbot/chat" -Method POST -Body $body2 -ContentType "application/json"
    
    Write-Host "✓ Context test completed" -ForegroundColor Green
    Write-Host "  First message response: $($response1.Answer.Substring(0, [Math]::Min(80, $response1.Answer.Length)))..." -ForegroundColor Gray
    Write-Host "  Second message response: $($response2.Answer.Substring(0, [Math]::Min(80, $response2.Answer.Length)))..." -ForegroundColor Gray
} catch {
    Write-Host "✗ Error: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""

# Test 6: Clear session
Write-Host "[Test 6] Clear session test..." -ForegroundColor Yellow
try {
    $response = Invoke-RestMethod -Uri "$BaseUrl/api/v1/chatbot/session/$sessionId" -Method DELETE
    Write-Host "✓ Session cleared: $($response.message)" -ForegroundColor Green
} catch {
    Write-Host "✗ Error: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Test completed!" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
