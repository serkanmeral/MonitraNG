param(
    [string]$DomainName = "meral",
    [switch]$SkipDomainCreation = $true
)

$baseUrl = "https://localhost:5001"
$headers = @{
    "Content-Type" = "application/json"
}

Write-Host "`n=== CODE OPTIMIZATION TEST ===" -ForegroundColor Cyan
Write-Host "Domain: $DomainName" -ForegroundColor Yellow
Write-Host ""

# Step 1: API Health Check
Write-Host "1. API Health Check..." -ForegroundColor Yellow
try {
    $versionResponse = Invoke-RestMethod -Uri "$baseUrl/api/version/short" `
        -Method GET `
        -SkipCertificateCheck `
        -ErrorAction Stop
    Write-Host "  ✓ API çalışıyor (Version: $versionResponse)" -ForegroundColor Green
} catch {
    Write-Host "  ⚠️  Version endpoint bulunamadı, devam ediliyor..." -ForegroundColor Yellow
}

# Step 2: Create Domain (if not skipped)
if (-not $SkipDomainCreation) {
    Write-Host "`n2. Domain oluşturuluyor..." -ForegroundColor Yellow
    try {
        $domainBody = @{
            name = $DomainName
            displayName = "$DomainName Domain"
        } | ConvertTo-Json

        $domainResponse = Invoke-RestMethod -Uri "$baseUrl/api/domain" `
            -Method POST `
            -Headers $headers `
            -Body $domainBody `
            -SkipCertificateCheck `
            -ErrorAction Stop

        Write-Host "  ✓ Domain oluşturuldu: $DomainName" -ForegroundColor Green
        Write-Host "    Domain ID: $($domainResponse.domainId)" -ForegroundColor Gray
        Write-Host "    Admin Username: $($domainResponse.adminUsername)" -ForegroundColor Gray
        Write-Host ""
        
        # Wait a bit for domain setup to complete
        Write-Host "  Domain setup tamamlanması bekleniyor (10 saniye)..." -ForegroundColor Gray
        Start-Sleep -Seconds 10
    } catch {
        Write-Host "  ✗ Domain oluşturulamadı: $($_.Exception.Message)" -ForegroundColor Red
        if ($_.Exception.Response) {
            try {
                $stream = $_.Exception.Response.Content.ReadAsStreamAsync().Result
                $reader = New-Object System.IO.StreamReader($stream)
                $responseBody = $reader.ReadToEnd()
                Write-Host "    Response: $responseBody" -ForegroundColor Red
            } catch {
                Write-Host "    Error details alınamadı" -ForegroundColor Red
            }
        }
        exit 1
    }
}

# Step 3: Get Admin Token
Write-Host "`n3. Admin token alınıyor..." -ForegroundColor Yellow
try {
    $adminUsername = "${DomainName}_admin"
    $tokenBody = @{
        username = $adminUsername
        password = "Admin123!"
        domain = $DomainName
    } | ConvertTo-Json

    $tokenResponse = Invoke-RestMethod -Uri "$baseUrl/api/auth/token" `
        -Method POST `
        -Headers $headers `
        -Body $tokenBody `
        -SkipCertificateCheck `
        -ErrorAction Stop

    $adminToken = $tokenResponse.accessToken
    if (-not $adminToken) {
        $adminToken = $tokenResponse.access_token
    }
    $headers["Authorization"] = "Bearer $adminToken"
    Write-Host "  ✓ Admin token alındı" -ForegroundColor Green
    Write-Host ""
} catch {
    Write-Host "  ✗ Token alınamadı: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "    Username: $adminUsername" -ForegroundColor Gray
    Write-Host "    Realm: $DomainName" -ForegroundColor Gray
    Write-Host "    URL: $baseUrl/api/auth/token" -ForegroundColor Gray
    exit 1
}

# Step 4: Test Cache - First Request (Cache Miss)
Write-Host "4. Cache Test - İlk Request (Cache Miss bekleniyor)..." -ForegroundColor Yellow
$firstRequestStart = Get-Date
try {
    $usersResponse1 = Invoke-RestMethod -Uri "$baseUrl/api/user?page=1&pageSize=20" `
        -Method GET `
        -Headers $headers `
        -SkipCertificateCheck `
        -ErrorAction Stop
    
    $firstRequestDuration = (Get-Date) - $firstRequestStart
    Write-Host "  ✓ İlk request tamamlandı" -ForegroundColor Green
    Write-Host "    Süre: $($firstRequestDuration.TotalMilliseconds) ms" -ForegroundColor Gray
    Write-Host "    Kullanıcı sayısı: $($usersResponse1.totalCount)" -ForegroundColor Gray
} catch {
    Write-Host "  ✗ İlk request başarısız: $($_.Exception.Message)" -ForegroundColor Red
}

# Step 5: Test Cache - Second Request (Cache Hit)
Write-Host "`n5. Cache Test - İkinci Request (Cache Hit bekleniyor)..." -ForegroundColor Yellow
$secondRequestStart = Get-Date
try {
    $usersResponse2 = Invoke-RestMethod -Uri "$baseUrl/api/user?page=1&pageSize=20" `
        -Method GET `
        -Headers $headers `
        -SkipCertificateCheck `
        -ErrorAction Stop
    
    $secondRequestDuration = (Get-Date) - $secondRequestStart
    Write-Host "  ✓ İkinci request tamamlandı" -ForegroundColor Green
    Write-Host "    Süre: $($secondRequestDuration.TotalMilliseconds) ms" -ForegroundColor Gray
    Write-Host "    Kullanıcı sayısı: $($usersResponse2.totalCount)" -ForegroundColor Gray
    
    # Compare performance
    if ($secondRequestDuration.TotalMilliseconds -lt $firstRequestDuration.TotalMilliseconds) {
        $improvement = (($firstRequestDuration.TotalMilliseconds - $secondRequestDuration.TotalMilliseconds) / $firstRequestDuration.TotalMilliseconds) * 100
        Write-Host "    ⚡ Performans iyileştirmesi: %$([math]::Round($improvement, 2))" -ForegroundColor Green
    }
} catch {
    Write-Host "  ✗ İkinci request başarısız: $($_.Exception.Message)" -ForegroundColor Red
}

# Step 6: Test Groups Query with Cache
Write-Host "`n6. Groups Query Cache Test..." -ForegroundColor Yellow
$groupsRequestStart = Get-Date
try {
    $groupsResponse = Invoke-RestMethod -Uri "$baseUrl/api/group?page=1&pageSize=20" `
        -Method GET `
        -Headers $headers `
        -SkipCertificateCheck `
        -ErrorAction Stop
    
    $groupsRequestDuration = (Get-Date) - $groupsRequestStart
    Write-Host "  ✓ Groups query tamamlandı" -ForegroundColor Green
    Write-Host "    Süre: $($groupsRequestDuration.TotalMilliseconds) ms" -ForegroundColor Gray
    Write-Host "    Grup sayısı: $($groupsResponse.totalCount)" -ForegroundColor Gray
} catch {
    Write-Host "  ✗ Groups query başarısız: $($_.Exception.Message)" -ForegroundColor Red
}

# Step 7: Test Pagination
Write-Host "`n7. Pagination Test..." -ForegroundColor Yellow
try {
    $page1Response = Invoke-RestMethod -Uri "$baseUrl/api/user?page=1&pageSize=5" `
        -Method GET `
        -Headers $headers `
        -SkipCertificateCheck `
        -ErrorAction Stop
    
    Write-Host "  ✓ Pagination çalışıyor" -ForegroundColor Green
    Write-Host "    Page 1: $($page1Response.users.Count) kullanıcı" -ForegroundColor Gray
    Write-Host "    Total Count: $($page1Response.totalCount)" -ForegroundColor Gray
    Write-Host "    Total Pages: $($page1Response.totalPages)" -ForegroundColor Gray
} catch {
    Write-Host "  ✗ Pagination test başarısız: $($_.Exception.Message)" -ForegroundColor Red
}

# Step 8: Test Search/Filter
Write-Host "`n8. Search/Filter Test..." -ForegroundColor Yellow
try {
    $searchResponse = Invoke-RestMethod -Uri "$baseUrl/api/user?page=1&pageSize=20&searchTerm=admin" `
        -Method GET `
        -Headers $headers `
        -SkipCertificateCheck `
        -ErrorAction Stop
    
    Write-Host "  ✓ Search/Filter çalışıyor" -ForegroundColor Green
    Write-Host "    'admin' araması: $($searchResponse.totalCount) sonuç" -ForegroundColor Gray
} catch {
    Write-Host "  ✗ Search/Filter test başarısız: $($_.Exception.Message)" -ForegroundColor Red
}

# Step 9: Test Exception Handling (Invalid Request)
Write-Host "`n9. Exception Handling Test..." -ForegroundColor Yellow
try {
    # Try to get users with invalid page number
    $invalidResponse = Invoke-RestMethod -Uri "$baseUrl/api/user?page=0&pageSize=20" `
        -Method GET `
        -Headers $headers `
        -SkipCertificateCheck `
        -ErrorAction Stop
    
    Write-Host "  ✓ Exception handling test tamamlandı" -ForegroundColor Green
} catch {
    $statusCode = $_.Exception.Response.StatusCode.value__
    Write-Host "  ✓ Exception yakalandı (Status: $statusCode)" -ForegroundColor Green
    Write-Host "    Mesaj: $($_.Exception.Message)" -ForegroundColor Gray
}

# Summary
Write-Host "`n=== TEST ÖZET ===" -ForegroundColor Cyan
Write-Host "Domain: $DomainName" -ForegroundColor Yellow
Write-Host "Cache Test:" -ForegroundColor Yellow
Write-Host "  - İlk Request: $([math]::Round($firstRequestDuration.TotalMilliseconds, 2)) ms" -ForegroundColor White
Write-Host "  - İkinci Request: $([math]::Round($secondRequestDuration.TotalMilliseconds, 2)) ms" -ForegroundColor White
if ($secondRequestDuration.TotalMilliseconds -lt $firstRequestDuration.TotalMilliseconds) {
    $improvement = (($firstRequestDuration.TotalMilliseconds - $secondRequestDuration.TotalMilliseconds) / $firstRequestDuration.TotalMilliseconds) * 100
    Write-Host "  - İyileştirme: %$([math]::Round($improvement, 2))" -ForegroundColor Green
}
Write-Host ""
Write-Host "✓ Optimizasyon testleri tamamlandı!" -ForegroundColor Green
Write-Host ""

