# Test: Single menu item insert

$baseUrl = "https://localhost:5010"
$token = Get-Content "$env:TEMP\serkan_token.txt" -Raw
$headers = @{
    "Authorization" = "Bearer $token"
    "Content-Type" = "application/json"
}

$testItem = @{
    order = 999
    itemType = "header"
    header = "Test Header"
    level = 0
    parentId = $null
    pageType = "admin"
    pageCode = "test-header-999"
}

$body = @{
    items = @($testItem)
} | ConvertTo-Json -Depth 10

try {
    $response = Invoke-RestMethod -Uri "$baseUrl/api/v1/data/@side_menu/bulk" -Headers $headers -Method "POST" -Body $body -SkipCertificateCheck -ErrorAction Stop
    Write-Host "✅ Test item başarıyla eklendi!" -ForegroundColor Green
    $response | ConvertTo-Json -Depth 10 | Write-Host
} catch {
    Write-Host "❌ Hata: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.ErrorDetails.Message) {
        Write-Host "Details:" -ForegroundColor Yellow
        $_.ErrorDetails.Message | Write-Host
    }
}
