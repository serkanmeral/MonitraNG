# Test: Two menu items insert

$baseUrl = "https://localhost:5010"
$token = Get-Content "$env:TEMP\serkan_token.txt" -Raw
$headers = @{
    "Authorization" = "Bearer $token"
    "Content-Type" = "application/json"
}

$testItems = @(
    @{
        order = 998
        itemType = "header"
        header = "Test Header 1"
        level = 0
        parentId = $null
        pageType = "admin"
        pageCode = "test-header-998"
    },
    @{
        order = 997
        itemType = "item"
        title = "Test Item 1"
        level = 0
        parentId = $null
        pageType = "admin"
        pageCode = "test-item-997"
        icon = "ChartPieIcon"
        iconType = "tabler"
        to = "/test/item1"
        type = "internal"
        disabled = $false
    }
)

$body = @{
    items = $testItems
} | ConvertTo-Json -Depth 10

Write-Host "Request body:" -ForegroundColor Cyan
$body | Write-Host
Write-Host ""

try {
    $response = Invoke-RestMethod -Uri "$baseUrl/api/v1/data/@side_menu/bulk" -Headers $headers -Method "POST" -Body $body -SkipCertificateCheck -ErrorAction Stop
    Write-Host "✅ Test items başarıyla eklendi!" -ForegroundColor Green
    $response | ConvertTo-Json -Depth 10 | Write-Host
} catch {
    Write-Host "❌ Hata: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.ErrorDetails.Message) {
        Write-Host "Details:" -ForegroundColor Yellow
        $_.ErrorDetails.Message | Write-Host
    }
}
