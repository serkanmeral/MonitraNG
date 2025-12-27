# Single Validation Test - Detailed Error Output
$baseUrl = "https://localhost:5010"

# Token'ı al
$scriptPath = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $PSCommandPath }
if ([string]::IsNullOrEmpty($scriptPath)) {
    $scriptPath = Get-Location
}
$getTokenScript = Join-Path $scriptPath "..\auth\get-token.ps1"

if (-not (Test-Path $getTokenScript)) {
    Write-Host "❌ get-token.ps1 bulunamadı!" -ForegroundColor Red
    exit 1
}

$token = & $getTokenScript

if ([string]::IsNullOrEmpty($token)) {
    Write-Host "❌ Token alınamadı!" -ForegroundColor Red
    exit 1
}

$headers = @{
    "Authorization" = "Bearer $token"
    "Content-Type" = "application/json"
}

[System.Net.ServicePointManager]::ServerCertificateValidationCallback = {$true}

$publisherId = "fbc53c6b-d992-444b-9a9b-2ca70d67c5f3"
$authorId = "694b04bd6d57c8ba7b798774"
$publisherCode = "TEST"

# Test: PageCount valid - bu test başarısız olmuştu
$body = @{
    title = "Test Book"
    publisher = $publisherId
    author = $authorId
    pageCount = 500
    publisherCode = $publisherCode
} | ConvertTo-Json

Write-Host "Request Body:" -ForegroundColor Yellow
Write-Host $body
Write-Host ""

try {
    $response = Invoke-RestMethod -Uri "$baseUrl/api/data/tst_books" -Method POST -Headers $headers -Body $body -SkipCertificateCheck
    Write-Host "✅ Success!" -ForegroundColor Green
    Write-Host ($response | ConvertTo-Json -Depth 5)
} catch {
    $statusCode = $_.Exception.Response.StatusCode.value__
    Write-Host "❌ Error Status: $statusCode" -ForegroundColor Red
    
    if ($_.ErrorDetails.Message) {
        Write-Host "`nError Details:" -ForegroundColor Yellow
        Write-Host $_.ErrorDetails.Message
        
        try {
            $errorJson = $_.ErrorDetails.Message | ConvertFrom-Json
            Write-Host "`nParsed Error JSON:" -ForegroundColor Cyan
            Write-Host ($errorJson | ConvertTo-Json -Depth 10)
        } catch {
            Write-Host "Could not parse as JSON" -ForegroundColor Yellow
        }
    } else {
        Write-Host "Exception Message: $($_.Exception.Message)" -ForegroundColor Red
        if ($_.Exception.InnerException) {
            Write-Host "Inner Exception: $($_.Exception.InnerException.Message)" -ForegroundColor Red
        }
    }
}

