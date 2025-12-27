# Test Expression Validation - Price/Page Ratio
$baseUrl = "https://localhost:5010"

# Token'ı al
$scriptPath = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $PSCommandPath }
$getTokenScript = Join-Path $scriptPath "..\auth\get-token.ps1"
$token = & $getTokenScript

$headers = @{
    "Authorization" = "Bearer $token"
    "Content-Type" = "application/json"
}

[System.Net.ServicePointManager]::ServerCertificateValidationCallback = {$true}

$publisherId = "fbc53c6b-d992-444b-9a9b-2ca70d67c5f3"
$authorId = "694b04bd6d57c8ba7b798774"
$publisherCode = "TEST"

# Test: Price/Page ratio too high (should fail)
# price=100, pageCount=5 -> ratio=20 (>10)
$body = @{
    title = "Test Book"
    publisher = $publisherId
    author = $authorId
    price = 100
    pageCount = 5
    publisherCode = $publisherCode
    name = "Test-Ratio-$(Get-Date -Format 'yyyyMMddHHmmssfff')"
} | ConvertTo-Json

Write-Host "Request Body:" -ForegroundColor Yellow
Write-Host $body
Write-Host ""
Write-Host "Expected: Validation should FAIL (ratio 20 > 10)" -ForegroundColor Cyan
Write-Host ""

try {
    $response = Invoke-RestMethod -Uri "$baseUrl/api/data/tst_books" -Method POST -Headers $headers -Body $body -SkipCertificateCheck
    Write-Host "❌ UNEXPECTED: Request succeeded, but validation should have failed!" -ForegroundColor Red
    Write-Host ($response | ConvertTo-Json -Depth 5)
} catch {
    $statusCode = $_.Exception.Response.StatusCode.value__
    Write-Host "Status: $statusCode" -ForegroundColor $(if ($statusCode -eq 400) { "Green" } else { "Red" })
    
    if ($_.ErrorDetails.Message) {
        try {
            $errorJson = $_.ErrorDetails.Message | ConvertFrom-Json
            if ($errorJson.error.details -and $errorJson.error.details.message -like "*price_page_ratio*") {
                Write-Host "✅ CORRECT: price_page_ratio validation failed as expected" -ForegroundColor Green
            } else {
                Write-Host "⚠️  Different error occurred:" -ForegroundColor Yellow
            }
            Write-Host ($errorJson | ConvertTo-Json -Depth 10)
        } catch {
            Write-Host $_.ErrorDetails.Message
        }
    }
}

