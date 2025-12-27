# Debug Edge Case Failures
$baseUrl = "https://localhost:5010"

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

# Test 1: PageCount minimum boundary (1)
Write-Host "`nTest: PageCount = 1 (minimum boundary)" -ForegroundColor Yellow
$body1 = @{
    title = "Test Book"
    publisher = $publisherId
    author = $authorId
    pageCount = 1
    price = 50
    publisherCode = $publisherCode
    name = "Test-PageCount-Min-$(Get-Date -Format 'yyyyMMddHHmmssfff')"
} | ConvertTo-Json

try {
    $response = Invoke-RestMethod -Uri "$baseUrl/api/data/tst_books" -Method POST -Headers $headers -Body $body1 -SkipCertificateCheck
    Write-Host "✅ SUCCESS" -ForegroundColor Green
} catch {
    $statusCode = $_.Exception.Response.StatusCode.value__
    Write-Host "❌ FAILED - Status: $statusCode" -ForegroundColor Red
    if ($_.ErrorDetails.Message) {
        try {
            $errorJson = $_.ErrorDetails.Message | ConvertFrom-Json
            Write-Host ($errorJson | ConvertTo-Json -Depth 10) -ForegroundColor DarkGray
        } catch {
            Write-Host $_.ErrorDetails.Message -ForegroundColor DarkGray
        }
    }
}

# Test 2: PublicationDate maximum boundary (2100-12-31T23:59:59Z)
Write-Host "`nTest: PublicationDate = 2100-12-31T23:59:59Z (maximum boundary)" -ForegroundColor Yellow
$body2 = @{
    title = "Test Book"
    publisher = $publisherId
    author = $authorId
    publicationDate = "2100-12-31T23:59:59Z"
    price = 50
    publisherCode = $publisherCode
    name = "Test-Date-Max-$(Get-Date -Format 'yyyyMMddHHmmssfff')"
} | ConvertTo-Json

try {
    $response = Invoke-RestMethod -Uri "$baseUrl/api/data/tst_books" -Method POST -Headers $headers -Body $body2 -SkipCertificateCheck
    Write-Host "✅ SUCCESS" -ForegroundColor Green
} catch {
    $statusCode = $_.Exception.Response.StatusCode.value__
    Write-Host "❌ FAILED - Status: $statusCode" -ForegroundColor Red
    if ($_.ErrorDetails.Message) {
        try {
            $errorJson = $_.ErrorDetails.Message | ConvertFrom-Json
            Write-Host ($errorJson | ConvertTo-Json -Depth 10) -ForegroundColor DarkGray
        } catch {
            Write-Host $_.ErrorDetails.Message -ForegroundColor DarkGray
        }
    }
}

