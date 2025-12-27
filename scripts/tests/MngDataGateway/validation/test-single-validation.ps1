# Single Validation Test
$baseUrl = "https://localhost:5010"
$tokenFile = "$env:TEMP\serkan_token.txt"
$token = Get-Content $tokenFile -Raw
$token = $token.Trim()

$headers = @{
    "Authorization" = "Bearer $token"
    "Content-Type" = "application/json"
}

[System.Net.ServicePointManager]::ServerCertificateValidationCallback = {$true}

$publisherId = "fbc53c6b-d992-444b-9a9b-2ca70d67c5f3"
$authorId = "694b04bd6d57c8ba7b798774"

$body = @{
    title = "Valid Book Title"
    publisher = $publisherId
    author = $authorId
} | ConvertTo-Json

Write-Host "Request Body:" -ForegroundColor Yellow
Write-Host $body
Write-Host ""

try {
    $response = Invoke-RestMethod -Uri "$baseUrl/api/data/tst_books" -Method POST -Headers $headers -Body $body -SkipCertificateCheck
    Write-Host "Success!" -ForegroundColor Green
    Write-Host ($response | ConvertTo-Json -Depth 5)
} catch {
    Write-Host "Error Status: $($_.Exception.Response.StatusCode.value__)" -ForegroundColor Red
    if ($_.ErrorDetails.Message) {
        Write-Host "Error Details:" -ForegroundColor Yellow
        Write-Host $_.ErrorDetails.Message
        try {
            $errorJson = $_.ErrorDetails.Message | ConvertFrom-Json
            Write-Host "`nParsed Errors:" -ForegroundColor Cyan
            Write-Host ($errorJson | ConvertTo-Json -Depth 5)
        } catch {
            Write-Host "Could not parse as JSON" -ForegroundColor Yellow
        }
    }
}

