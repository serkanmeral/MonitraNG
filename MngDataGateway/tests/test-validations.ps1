# Validation Test Script
# Tests field-level and expression-based validations for tst_books dataset

$baseUrl = "https://localhost:5010"

# Token'ı al (get-token.ps1 kullanarak)
$scriptPath = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $PSCommandPath }
if ([string]::IsNullOrEmpty($scriptPath)) {
    $scriptPath = Get-Location
}
$getTokenScript = Join-Path $scriptPath "get-token.ps1"

if (-not (Test-Path $getTokenScript)) {
    Write-Host "❌ get-token.ps1 bulunamadı! Path: $getTokenScript" -ForegroundColor Red
    exit 1
}

$token = & $getTokenScript

if ([string]::IsNullOrEmpty($token)) {
    Write-Host "❌ Token alınamadı! Testler durduruluyor." -ForegroundColor Red
    exit 1
}

$headers = @{
    "Authorization" = "Bearer $token"
    "Content-Type" = "application/json"
}

# Ignore SSL certificate errors
[System.Net.ServicePointManager]::ServerCertificateValidationCallback = {$true}

Write-Host "`n🧪 Validation Testleri Başlıyor...`n" -ForegroundColor Cyan

# Önce publisher ve author ID'lerini al
Write-Host "📋 Publisher ve Author ID'leri alınıyor..." -ForegroundColor Yellow
$publisherId = $null
$authorId = $null

try {
    # Publisher'ları al
    $publishersUrl = "$baseUrl/api/data/tst_publishers?limit=1"
    $publishersResponse = Invoke-RestMethod -Uri $publishersUrl -Method GET -Headers $headers -SkipCertificateCheck -ErrorAction SilentlyContinue
    if ($publishersResponse -is [Array] -and $publishersResponse.Count -gt 0) {
        $publisherId = $publishersResponse[0].__dataId
        Write-Host "   ✅ Publisher bulundu: $publisherId" -ForegroundColor Green
    } else {
        Write-Host "   ⚠️  Publisher bulunamadı, test verisi oluşturulacak" -ForegroundColor Yellow
        # Test publisher oluştur
        try {
            $newPublisher = @{
                name = "Test Publisher"
            } | ConvertTo-Json
            $publisherResponse = Invoke-RestMethod -Uri "$baseUrl/api/data/tst_publishers" -Method POST -Headers $headers -Body $newPublisher -SkipCertificateCheck
            $publisherId = $publisherResponse.__dataId
            Write-Host "   ✅ Test publisher oluşturuldu: $publisherId" -ForegroundColor Green
        } catch {
            Write-Host "   ❌ Test publisher oluşturulamadı: $($_.Exception.Message)" -ForegroundColor Red
        }
    }
} catch {
    Write-Host "   ⚠️  Publisher alınamadı: $($_.Exception.Message)" -ForegroundColor Yellow
}

try {
    # Author'ları al (MngKeeper'dan)
    $keeperUrl = "https://localhost:5001"
    $usersUrl = "$keeperUrl/api/user?pageSize=1"
    $usersResponse = Invoke-RestMethod -Uri $usersUrl -Method GET -Headers $headers -SkipCertificateCheck -ErrorAction SilentlyContinue
    if ($usersResponse.users -and $usersResponse.users.Count -gt 0) {
        $authorId = $usersResponse.users[0].userId
        Write-Host "   ✅ Author bulundu: $authorId" -ForegroundColor Green
    } else {
        Write-Host "   ⚠️  Author bulunamadı" -ForegroundColor Yellow
    }
} catch {
    Write-Host "   ⚠️  Author alınamadı: $($_.Exception.Message)" -ForegroundColor Yellow
}

if (-not $publisherId -or -not $authorId) {
    Write-Host "`n❌ Publisher veya Author bulunamadı! Testler için gerekli." -ForegroundColor Red
    Write-Host "   Publisher ID: $publisherId" -ForegroundColor Gray
    Write-Host "   Author ID: $authorId" -ForegroundColor Gray
    exit 1
}

# publisherCode için test değeri (internalBookNumber format'ı için gerekli)
$publisherCode = "TEST"

Write-Host ""

$testCount = 0
$passCount = 0
$failCount = 0

function Test-Validation {
    param(
        [string]$TestName,
        [object]$Data,
        [bool]$ShouldFail = $false,
        [string]$ExpectedError = ""
    )
    
    $script:testCount++
    Write-Host "Test $testCount : $TestName" -ForegroundColor Yellow
    
    try {
        # Her test için unique name ekle (idx_name unique index'i için)
        if (-not $Data.ContainsKey("name")) {
            $Data["name"] = "Test-$testCount-$(Get-Date -Format 'yyyyMMddHHmmssfff')"
        }
        
        $body = $Data | ConvertTo-Json -Depth 10
        $response = Invoke-RestMethod -Uri "$baseUrl/api/data/tst_books" -Method POST -Headers $headers -Body $body -SkipCertificateCheck -ErrorAction Stop
        
        if ($ShouldFail) {
            Write-Host "  ❌ BAŞARISIZ: Validation hatası bekleniyordu ama başarılı oldu!" -ForegroundColor Red
            $script:failCount++
            return $false
        } else {
            Write-Host "  ✅ BAŞARILI: Validation geçti" -ForegroundColor Green
            $script:passCount++
            return $true
        }
    } catch {
        $statusCode = $_.Exception.Response.StatusCode.value__
        $errorMessage = ""
        
        # 401 hatası alındıysa token'ı yenile ve tekrar dene
        if ($statusCode -eq 401) {
            Write-Host "  ⚠️  401 Unauthorized - Token yenileniyor..." -ForegroundColor Yellow
            $scriptPath = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $PSCommandPath }
            if ([string]::IsNullOrEmpty($scriptPath)) {
                $scriptPath = Get-Location
            }
            $getTokenScript = Join-Path $scriptPath "get-token.ps1"
            if (Test-Path $getTokenScript) {
                $newToken = & $getTokenScript
                if (-not [string]::IsNullOrEmpty($newToken)) {
                    $script:headers["Authorization"] = "Bearer $newToken"
                    # Tekrar dene
                    try {
                        # Her test için unique name ekle (idx_name unique index'i için)
                        if (-not $Data.ContainsKey("name")) {
                            $Data["name"] = "Test-$testCount-Retry-$(Get-Date -Format 'yyyyMMddHHmmssfff')"
                        }
                        
                        $body = $Data | ConvertTo-Json -Depth 10
                        $response = Invoke-RestMethod -Uri "$baseUrl/api/data/tst_books" -Method POST -Headers $script:headers -Body $body -SkipCertificateCheck -ErrorAction Stop
                        
                        if ($ShouldFail) {
                            Write-Host "  ❌ BAŞARISIZ: Validation hatası bekleniyordu ama başarılı oldu!" -ForegroundColor Red
                            $script:failCount++
                            return $false
                        } else {
                            Write-Host "  ✅ BAŞARILI: Validation geçti" -ForegroundColor Green
                            $script:passCount++
                            return $true
                        }
                    } catch {
                        $statusCode = $_.Exception.Response.StatusCode.value__
                    }
                }
            }
        }
        
        if ($_.ErrorDetails.Message) {
            try {
                $errorJson = $_.ErrorDetails.Message | ConvertFrom-Json
                if ($errorJson.error) {
                    $errorMessage = $errorJson.error.message
                    if ($errorJson.error.errors) {
                        $errorDetails = $errorJson.error.errors | ConvertTo-Json -Depth 5
                        $errorMessage += "`n    Validation Errors: $errorDetails"
                    }
                } elseif ($errorJson.message) {
                    $errorMessage = $errorJson.message
                    if ($errorJson.errors) {
                        $errorDetails = $errorJson.errors | ConvertTo-Json -Depth 5
                        $errorMessage += "`n    Validation Errors: $errorDetails"
                    }
                } elseif ($errorJson.errors) {
                    $errorMessage = ($errorJson.errors | ConvertTo-Json -Depth 5)
                } else {
                    $errorMessage = $_.ErrorDetails.Message
                }
            } catch {
                $errorMessage = $_.ErrorDetails.Message
            }
        } else {
            $errorMessage = $_.Exception.Message
        }
        
        # 500 hataları için detaylı log
        if ($statusCode -eq 500) {
            if ($_.ErrorDetails.Message) {
                try {
                    $errorJson = $_.ErrorDetails.Message | ConvertFrom-Json
                    Write-Host "    Full Error Details:" -ForegroundColor DarkYellow
                    Write-Host ($errorJson | ConvertTo-Json -Depth 10) -ForegroundColor DarkGray
                } catch {
                    Write-Host "    Raw Error: $($_.ErrorDetails.Message)" -ForegroundColor DarkGray
                }
            }
        }
        
        if ($ShouldFail) {
            Write-Host "  ✅ BAŞARILI: Validation hatası bekleniyordu ve alındı" -ForegroundColor Green
            Write-Host "    Status: $statusCode" -ForegroundColor Gray
            if ($errorMessage) {
                Write-Host "    Error: $errorMessage" -ForegroundColor Gray
            }
            $script:passCount++
            return $true
        } else {
            Write-Host "  ❌ BAŞARISIZ: Validation hatası beklenmiyordu!" -ForegroundColor Red
            Write-Host "    Status: $statusCode" -ForegroundColor Gray
            if ($errorMessage) {
                Write-Host "    Error: $errorMessage" -ForegroundColor Gray
            }
            # 400 hatası için detaylı log
            if ($statusCode -eq 400 -and -not $ShouldFail) {
                if ($_.ErrorDetails.Message) {
                    try {
                        $errorJson = $_.ErrorDetails.Message | ConvertFrom-Json
                        if ($errorJson.errors) {
                            Write-Host "    Validation Errors:" -ForegroundColor DarkGray
                            foreach ($err in $errorJson.errors) {
                                $field = if ($err.field) { $err.field } else { "N/A" }
                                $msg = if ($err.message) { $err.message } else { "N/A" }
                                Write-Host "      - Field: $field, Message: $msg" -ForegroundColor DarkGray
                            }
                        }
                    } catch {
                        Write-Host "    Full Error: $($_.ErrorDetails.Message)" -ForegroundColor DarkGray
                    }
                }
            }
            $script:failCount++
            return $false
        }
    }
}

# ============================================
# Test 1: Field-Level Validations - Title
# ============================================
Write-Host "`n📝 Field-Level Validation: Title`n" -ForegroundColor Cyan

# Test 1.1: Title too short (minLength)
Test-Validation -TestName "Title minLength (2 chars - should fail)" -Data @{
    title = "AB"
    publisher = $publisherId
    author = $authorId
    publisherCode = $publisherCode
} -ShouldFail $true

# Test 1.2: Title too long (maxLength)
Test-Validation -TestName "Title maxLength (201 chars - should fail)" -Data @{
    title = "A" * 201
    publisher = $publisherId
    author = $authorId
    publisherCode = $publisherCode
} -ShouldFail $true

# Test 1.3: Title valid length
Test-Validation -TestName "Title valid length (3-200 chars)" -Data @{
    title = "Valid Book Title"
    publisher = $publisherId
    author = $authorId
    price = 50
    publisherCode = $publisherCode
} -ShouldFail $false

# ============================================
# Test 2: Field-Level Validations - Genres (Array)
# ============================================
Write-Host "`n📚 Field-Level Validation: Genres Array`n" -ForegroundColor Cyan

# Test 2.1: Too many genres (maxItems) - Önce genre ID'leri al
$genreIds = @()
try {
    $genresUrl = "$baseUrl/api/data/tst_genres?limit=10"
    $genresResponse = Invoke-RestMethod -Uri $genresUrl -Method GET -Headers $headers -SkipCertificateCheck -ErrorAction SilentlyContinue
    if ($genresResponse -is [Array]) {
        $genreIds = $genresResponse | ForEach-Object { $_.__dataId } | Select-Object -First 6
    }
} catch {
    Write-Host "   ⚠️  Genres alınamadı" -ForegroundColor Yellow
}

if ($genreIds.Count -lt 6) {
    Write-Host "   ⚠️  Yeterli genre yok, test atlanıyor" -ForegroundColor Yellow
} else {
    Test-Validation -TestName "Genres maxItems (6 items - should fail)" -Data @{
        title = "Test Book"
        publisher = $publisherId
        author = $authorId
        genres = $genreIds
        publisherCode = $publisherCode
    } -ShouldFail $true
}

# Test 2.2: Valid number of genres
if ($genreIds.Count -ge 5) {
    Test-Validation -TestName "Genres valid count (5 items)" -Data @{
        title = "Test Book"
        publisher = $publisherId
        author = $authorId
        genres = $genreIds | Select-Object -First 5
        price = 50
        publisherCode = $publisherCode
    } -ShouldFail $false
}

# ============================================
# Test 3: Field-Level Validations - PageCount
# ============================================
Write-Host "`n📄 Field-Level Validation: PageCount`n" -ForegroundColor Cyan

# Test 3.1: PageCount too low (min)
Test-Validation -TestName "PageCount min (0 - should fail)" -Data @{
    title = "Test Book"
    publisher = $publisherId
    author = $authorId
    pageCount = 0
    publisherCode = $publisherCode
} -ShouldFail $true

# Test 3.2: PageCount too high (max)
Test-Validation -TestName "PageCount max (10001 - should fail)" -Data @{
    title = "Test Book"
    publisher = $publisherId
    author = $authorId
    pageCount = 10001
    publisherCode = $publisherCode
} -ShouldFail $true

# Test 3.3: PageCount valid
Test-Validation -TestName "PageCount valid (1-10000)" -Data @{
    title = "Test Book"
    publisher = $publisherId
    author = $authorId
    pageCount = 500
    price = 50
    publisherCode = $publisherCode
} -ShouldFail $false

# ============================================
# Test 4: Field-Level Validations - PublicationDate
# ============================================
Write-Host "`n📅 Field-Level Validation: PublicationDate`n" -ForegroundColor Cyan

# Test 4.1: PublicationDate too early (minDate)
Test-Validation -TestName "PublicationDate minDate (1899 - should fail)" -Data @{
    title = "Test Book"
    publisher = $publisherId
    author = $authorId
    publicationDate = "1899-12-31T00:00:00Z"
    publisherCode = $publisherCode
} -ShouldFail $true

# Test 4.2: PublicationDate too late (maxDate)
Test-Validation -TestName "PublicationDate maxDate (2101 - should fail)" -Data @{
    title = "Test Book"
    publisher = $publisherId
    author = $authorId
    publicationDate = "2101-01-01T00:00:00Z"
    publisherCode = $publisherCode
} -ShouldFail $true

# Test 4.3: PublicationDate valid
Test-Validation -TestName "PublicationDate valid (1900-2100)" -Data @{
    title = "Test Book"
    publisher = $publisherId
    author = $authorId
    publicationDate = "2024-01-01T00:00:00Z"
    price = 50
    publisherCode = $publisherCode
} -ShouldFail $false

# ============================================
# Test 5: Field-Level Validations - Language
# ============================================
Write-Host "`n🌐 Field-Level Validation: Language Pattern`n" -ForegroundColor Cyan

# Test 5.1: Language invalid pattern
Test-Validation -TestName "Language invalid pattern (should fail)" -Data @{
    title = "Test Book"
    publisher = $publisherId
    author = $authorId
    language = "INVALID"
    publisherCode = $publisherCode
} -ShouldFail $true

# Test 5.2: Language valid pattern (2 letters)
Test-Validation -TestName "Language valid pattern (2 letters)" -Data @{
    title = "Test Book"
    publisher = $publisherId
    author = $authorId
    language = "en"
    price = 50
    publisherCode = $publisherCode
} -ShouldFail $false

# Test 5.3: Language valid pattern (with region)
Test-Validation -TestName "Language valid pattern (with region)" -Data @{
    title = "Test Book"
    publisher = $publisherId
    author = $authorId
    language = "tr-TR"
    price = 50
    publisherCode = $publisherCode
} -ShouldFail $false

# ============================================
# Test 6: Field-Level Validations - Price
# ============================================
Write-Host "`n💰 Field-Level Validation: Price`n" -ForegroundColor Cyan

# Test 6.1: Price negative (min)
Test-Validation -TestName "Price min (-1 - should fail)" -Data @{
    title = "Test Book"
    publisher = $publisherId
    author = $authorId
    price = -1
    publisherCode = $publisherCode
} -ShouldFail $true

# Test 6.2: Price too high (max)
Test-Validation -TestName "Price max (100001 - should fail)" -Data @{
    title = "Test Book"
    publisher = $publisherId
    author = $authorId
    price = 100001
    publisherCode = $publisherCode
} -ShouldFail $true

# Test 6.3: Price valid
Test-Validation -TestName "Price valid (0-100000)" -Data @{
    title = "Test Book"
    publisher = $publisherId
    author = $authorId
    price = 50.99
    publisherCode = $publisherCode
} -ShouldFail $false

# ============================================
# Test 7: Expression-Based Validations
# ============================================
Write-Host "`n🔢 Expression-Based Validation`n" -ForegroundColor Cyan

# Test 7.1: Price per page ratio too high (should fail)
Test-Validation -TestName "Price/Page ratio too high (should fail)" -Data @{
    title = "Test Book"
    publisher = $publisherId
    author = $authorId
    price = 100
    pageCount = 5
    publisherCode = $publisherCode
} -ShouldFail $true

# Test 7.2: Price per page ratio valid
Test-Validation -TestName "Price/Page ratio valid" -Data @{
    title = "Test Book"
    publisher = $publisherId
    author = $authorId
    price = 50
    pageCount = 10
    publisherCode = $publisherCode
} -ShouldFail $false

# Test 7.3: Price positive if pages provided (should fail)
Test-Validation -TestName "Price must be positive if pages provided (should fail)" -Data @{
    title = "Test Book"
    publisher = $publisherId
    author = $authorId
    pageCount = 100
    price = 0
    publisherCode = $publisherCode
} -ShouldFail $true

# Test 7.4: Price positive if pages provided (valid)
Test-Validation -TestName "Price positive if pages provided (valid)" -Data @{
    title = "Test Book"
    publisher = $publisherId
    author = $authorId
    pageCount = 100
    price = 25
    publisherCode = $publisherCode
} -ShouldFail $false

# ============================================
# Test 8: Edge Cases - Boundary Values
# ============================================
Write-Host "`n🎯 Edge Cases: Boundary Values`n" -ForegroundColor Cyan

# Test 8.1: Title minimum length (exactly 3 chars - should pass)
Test-Validation -TestName "Title minimum boundary (3 chars - should pass)" -Data @{
    title = "ABC"
    publisher = $publisherId
    author = $authorId
    price = 50
    publisherCode = $publisherCode
} -ShouldFail $false

# Test 8.2: Title maximum length (exactly 200 chars - should pass)
Test-Validation -TestName "Title maximum boundary (200 chars - should pass)" -Data @{
    title = "A" * 200
    publisher = $publisherId
    author = $authorId
    price = 50
    publisherCode = $publisherCode
} -ShouldFail $false

# Test 8.3: PageCount minimum boundary (exactly 1 - should pass)
# Note: price must be <= 10 when pageCount=1 to satisfy price_page_ratio validation
Test-Validation -TestName "PageCount minimum boundary (1 - should pass)" -Data @{
    title = "Test Book"
    publisher = $publisherId
    author = $authorId
    pageCount = 1
    price = 10
    publisherCode = $publisherCode
} -ShouldFail $false

# Test 8.4: PageCount maximum boundary (exactly 10000 - should pass)
Test-Validation -TestName "PageCount maximum boundary (10000 - should pass)" -Data @{
    title = "Test Book"
    publisher = $publisherId
    author = $authorId
    pageCount = 10000
    price = 50
    publisherCode = $publisherCode
} -ShouldFail $false

# Test 8.5: Price minimum boundary (exactly 0 - should pass)
Test-Validation -TestName "Price minimum boundary (0 - should pass)" -Data @{
    title = "Test Book"
    publisher = $publisherId
    author = $authorId
    price = 0
    publisherCode = $publisherCode
} -ShouldFail $false

# Test 8.6: Price maximum boundary (exactly 100000 - should pass)
Test-Validation -TestName "Price maximum boundary (100000 - should pass)" -Data @{
    title = "Test Book"
    publisher = $publisherId
    author = $authorId
    price = 100000
    publisherCode = $publisherCode
} -ShouldFail $false

# Test 8.7: PublicationDate minimum boundary (1900-01-01 - should pass)
Test-Validation -TestName "PublicationDate minimum boundary (1900-01-01 - should pass)" -Data @{
    title = "Test Book"
    publisher = $publisherId
    author = $authorId
    publicationDate = "1900-01-01T00:00:00Z"
    price = 50
    publisherCode = $publisherCode
} -ShouldFail $false

# Test 8.8: PublicationDate maximum boundary (2100-12-31T12:00:00Z - should pass)
# Note: Using mid-day to avoid potential precision/timezone issues at exact boundary
Test-Validation -TestName "PublicationDate maximum boundary (2100-12-31T12:00:00Z - should pass)" -Data @{
    title = "Test Book"
    publisher = $publisherId
    author = $authorId
    publicationDate = "2100-12-31T12:00:00Z"
    price = 50
    publisherCode = $publisherCode
} -ShouldFail $false

# Test 8.9: Price/Page ratio maximum boundary (exactly 10 - should pass)
Test-Validation -TestName "Price/Page ratio maximum boundary (10 - should pass)" -Data @{
    title = "Test Book"
    publisher = $publisherId
    author = $authorId
    price = 100
    pageCount = 10
    publisherCode = $publisherCode
} -ShouldFail $false

# ============================================
# Test Summary
# ============================================
Write-Host "`n" -NoNewline
Write-Host "═══════════════════════════════════════" -ForegroundColor Cyan
Write-Host "📊 TEST ÖZETİ" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════" -ForegroundColor Cyan
Write-Host "Toplam Test: $testCount" -ForegroundColor White
Write-Host "✅ Başarılı: $passCount" -ForegroundColor Green
Write-Host "❌ Başarısız: $failCount" -ForegroundColor Red
Write-Host "═══════════════════════════════════════" -ForegroundColor Cyan

if ($failCount -eq 0) {
    Write-Host "`n🎉 Tüm testler başarılı!`n" -ForegroundColor Green
    exit 0
} else {
    Write-Host "`n⚠️  Bazı testler başarısız oldu.`n" -ForegroundColor Yellow
    exit 1
}

