# Test: İlk birkaç item'ı tek tek test et

$baseUrl = "https://localhost:5010"
$token = Get-Content "$env:TEMP\serkan_token.txt" -Raw
$headers = @{
    "Authorization" = "Bearer $token"
    "Content-Type" = "application/json"
}

$json = Get-Content "scripts/tests/MngDataGateway/side-menu/menu-items-export.json" -Raw | ConvertFrom-Json

Write-Host "İlk 5 item'ı test ediliyor..." -ForegroundColor Cyan
Write-Host ""

for ($i = 0; $i -lt [Math]::Min(5, $json.Count); $i++) {
    $item = $json[$i]
    
    Write-Host "[$($i+1)] Testing: $($item.title ?? $item.header) (Order: $($item.order))" -ForegroundColor Yellow
    
    # Item'ı hazırla
    $testItem = @{
        order = $item.order
        itemType = $item.itemType
        level = $item.level
        parentId = $null
        pageType = $item.pageType
        pageCode = $item.pageCode
    }
    
    if ($item.header) {
        $testItem.header = $item.header
    }
    
    if ($item.title) {
        $testItem.title = $item.title
    }
    
    if ($item.icon) {
        $testItem.icon = $item.icon
    }
    
    if ($item.iconType) {
        $testItem.iconType = $item.iconType
    }
    
    if ($item.to) {
        $testItem.to = $item.to
    }
    
    if ($item.type) {
        $testItem.type = $item.type
    }
    
    if ($item.disabled -ne $null) {
        $testItem.disabled = $item.disabled
    }
    
    $body = @{
        items = @($testItem)
    } | ConvertTo-Json -Depth 10
    
    try {
        $response = Invoke-RestMethod -Uri "$baseUrl/api/v1/data/@side_menu/bulk" -Headers $headers -Method "POST" -Body $body -SkipCertificateCheck -ErrorAction Stop
        
        if ($response.data.successful -gt 0) {
            Write-Host "   ✅ Başarılı!" -ForegroundColor Green
        } else {
            Write-Host "   ⚠️  Başarısız ama hata yok" -ForegroundColor Yellow
        }
    } catch {
        Write-Host "   ❌ Hata: $($_.Exception.Message)" -ForegroundColor Red
        if ($_.ErrorDetails.Message) {
            try {
                $errorJson = $_.ErrorDetails.Message | ConvertFrom-Json
                if ($errorJson.error) {
                    Write-Host "      Code: $($errorJson.error.code)" -ForegroundColor Red
                    Write-Host "      Message: $($errorJson.error.message)" -ForegroundColor Red
                }
                if ($errorJson.data -and $errorJson.data.errors) {
                    foreach ($err in $errorJson.data.errors) {
                        Write-Host "      - Index $($err.index): $($err.error)" -ForegroundColor Red
                        if ($err.details) {
                            foreach ($detail in $err.details) {
                                Write-Host "        Field '$($detail.field)': $($detail.message)" -ForegroundColor DarkRed
                            }
                        }
                    }
                }
            } catch {
                Write-Host "      Raw: $($_.ErrorDetails.Message)" -ForegroundColor Red
            }
        }
    }
    
    Write-Host ""
}
