# Validation Test Script
# Tests field-level and expression-based validations for tst_books dataset

$baseUrl = "https://localhost:5010"

# Token'─▒ al (get-token.ps1 kullanarak)
$scriptPath = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $PSCommandPath }
if ([string]::IsNullOrEmpty($scriptPath)) {
    $scriptPath = Get-Location
}
$getTokenScript = Join-Path $scriptPath "..\auth\get-token.ps1"

if (-not (Test-Path $getTokenScript)) {
    Write-Host "ÔØî get-token.ps1 bulunamad─▒! Path: $getTokenScript" -ForegroundColor Red
    exit 1
}

$token = & $getTokenScript

if ([string]::IsNullOrEmpty($token)) {
    Write-Host "ÔØî Token al─▒namad─▒! Testler durduruluyor." -ForegroundColor Red
    exit 1
}

$headers = @{
    "Authorization" = "Bearer $token"
    "Content-Type" = "application/json"
}

# Ignore SSL certificate errors
[System.Net.ServicePointManager]::ServerCertificateValidationCallback = {$true}

Write-Host "`n­şğ¬ Validation Testleri Ba┼şl─▒yor...`n" -ForegroundColor Cyan

# ├ûnce publisher ve author ID'lerini al
Write-Host "­şôï Publisher ve Author ID'leri al─▒n─▒yor..." -ForegroundColor Yellow
$publisherId = $null
$authorId = $null

try {
    # Publisher'lar─▒ al
    $publishersUrl = "$baseUrl/api/data/tst_publishers?limit=1"
    $publishersResponse = Invoke-RestMethod -Uri $publishersUrl -Method GET -Headers $headers -SkipCertificateCheck -ErrorAction SilentlyContinue
    if ($publishersResponse -is [Array] -and $publishersResponse.Count -gt 0) {
        $publisherId = $publishersResponse[0].__dataId
        Write-Host "   Ô£à Publisher bulundu: $publisherId" -ForegroundColor Green
    } else {
        Write-Host "   ÔÜá´©Å  Publisher bulunamad─▒, test verisi olu┼şturulacak" -ForegroundColor Yellow
        # Test publisher olu┼ştur
        try {
            $newPublisher = @{
                name = "Test Publisher"
            } | ConvertTo-Json
            $publisherResponse = Invoke-RestMethod -Uri "$baseUrl/api/data/tst_publishers" -Method POST -Headers $headers -Body $newPublisher -SkipCertificateCheck
            $publisherId = $publisherResponse.__dataId
            Write-Host "   Ô£à Test publisher olu┼şturuldu: $publisherId" -ForegroundColor Green
        } catch {
            Write-Host "   ÔØî Test publisher olu┼şturulamad─▒: $($_.Exception.Message)" -ForegroundColor Red
        }
    }
} catch {
    Write-Host "   ÔÜá´©Å  Publisher al─▒namad─▒: $($_.Exception.Message)" -ForegroundColor Yellow
}

try {
    # Author'lar─▒ al (MngKeeper'dan)
    $keeperUrl = "https://localhost:5001"
    $usersUrl = "$keeperUrl/api/user?pageSize=1"
    $usersResponse = Invoke-RestMethod -Uri $usersUrl -Method GET -Headers $headers -SkipCertificateCheck -ErrorAction SilentlyContinue
    if ($usersResponse.users -and $usersResponse.users.Count -gt 0) {
        $authorId = $usersResponse.users[0].userId
        Write-Host "   Ô£à Author bulundu: $authorId" -ForegroundColor Green
    } else {
        Write-Host "   ÔÜá´©Å  Author bulunamad─▒" -ForegroundColor Yellow
    }
} catch {
    Write-Host "   ÔÜá´©Å  Author al─▒namad─▒: $($_.Exception.Message)" -ForegroundColor Yellow
}

if (-not $publisherId -or -not $authorId) {
    Write-Host "`nÔØî Publisher veya Author bulunamad─▒! Testler i├ğin gerekli." -ForegroundColor Red
    Write-Host "   Publisher ID: $publisherId" -ForegroundColor Gray
    Write-Host "   Author ID: $authorId" -ForegroundColor Gray
    exit 1
}

# publisherCode i├ğin test de─şeri (internalBookNumber format'─▒ i├ğin gerekli)
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
        # Her test i├ğin unique name ekle (idx_name unique index'i i├ğin)
        if (-not $Data.ContainsKey("name")) {
            $Data["name"] = "Test-$testCount-$(Get-Date -Format 'yyyyMMddHHmmssfff')"
        }
        
        $body = $Data | ConvertTo-Json -Depth 10
        $response = Invoke-RestMethod -Uri "$baseUrl/api/data/tst_books" -Method POST -Headers $headers -Body $body -SkipCertificateCheck -ErrorAction Stop
        
        if ($ShouldFail) {
            Write-Host "  ÔØî BA┼ŞARISIZ: Validation hatas─▒ bekleniyordu ama ba┼şar─▒l─▒ oldu!" -ForegroundColor Red
            $script:failCount++
            return $false
        } else {
            Write-Host "  Ô£à BA┼ŞARILI: Validation ge├ğti" -ForegroundColor Green
            $script:passCount++
            return $true
        }
    } catch {
        $statusCode = $_.Exception.Response.StatusCode.value__
        $errorMessage = ""
        
        # 401 hatas─▒ al─▒nd─▒ysa token'─▒ yenile ve tekrar dene
        if ($statusCode -eq 401) {
            Write-Host "  ÔÜá´©Å  401 Unauthorized - Token yenileniyor..." -ForegroundColor Yellow
            $scriptPath = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $PSCommandPath }
            if ([string]::IsNullOrEmpty($scriptPath)) {
                $scriptPath = Get-Location
            }
            $getTokenScript = Join-Path $scriptPath "..\auth\get-token.ps1"
            if (Test-Path $getTokenScript) {
                $newToken = & $getTokenScript
                if (-not [string]::IsNullOrEmpty($newToken)) {
                    $script:headers["Authorization"] = "Bearer $newToken"
                    # Tekrar dene
                    try {
                        # Her test i├ğin unique name ekle (idx_name unique index'i i├ğin)
                        if (-not $Data.ContainsKey("name")) {
                            $Data["name"] = "Test-$testCount-Retry-$(Get-Date -Format 'yyyyMMddHHmmssfff')"
                        }
                        
                        $body = $Data | ConvertTo-Json -Depth 10
                        $response = Invoke-RestMethod -Uri "$baseUrl/api/data/tst_books" -Method POST -Headers $script:headers -Body $body -SkipCertificateCheck -ErrorAction Stop
                        
                        if ($ShouldFail) {
                            Write-Host "  ÔØî BA┼ŞARISIZ: Validation hatas─▒ bekleniyordu ama ba┼şar─▒l─▒ oldu!" -ForegroundColor Red
                            $script:failCount++
                            return $false
                        } else {
                            Write-Host "  Ô£à BA┼ŞARILI: Validation ge├ğti" -ForegroundColor Green
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
        
        # 500 hatalar─▒ i├ğin detayl─▒ log
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
            Write-Host "  Ô£à BA┼ŞARILI: Validation hatas─▒ bekleniyordu ve al─▒nd─▒" -ForegroundColor Green
            Write-Host "    Status: $statusCode" -ForegroundColor Gray
            if ($errorMessage) {
                Write-Host "    Error: $errorMessage" -ForegroundColor Gray
            }
            $script:passCount++
            return $true
        } else {
            Write-Host "  ÔØî BA┼ŞARISIZ: Validation hatas─▒ beklenmiyordu!" -ForegroundColor Red
            Write-Host "    Status: $statusCode" -ForegroundColor Gray
            if ($errorMessage) {
                Write-Host "    Error: $errorMessage" -ForegroundColor Gray
            }
            # 400 hatas─▒ i├ğin detayl─▒ log
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
Write-Host "`n­şôØ Field-Level Validation: Title`n" -ForegroundColor Cyan

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
Write-Host "`n­şôÜ Field-Level Validation: Genres Array`n" -ForegroundColor Cyan

# Test 2.1: Too many genres (maxItems) - ├ûnce genre ID'leri al
$genreIds = @()
try {
    $genresUrl = "$baseUrl/api/data/tst_genres?limit=10"
    $genresResponse = Invoke-RestMethod -Uri $genresUrl -Method GET -Headers $headers -SkipCertificateCheck -ErrorAction SilentlyContinue
    if ($genresResponse -is [Array]) {
        $genreIds = $genresResponse | ForEach-Object { $_.__dataId } | Select-Object -First 6
    }
} catch {
    Write-Host "   ÔÜá´©Å  Genres al─▒namad─▒" -ForegroundColor Yellow
}

if ($genreIds.Count -lt 6) {
    Write-Host "   ÔÜá´©Å  Yeterli genre yok, test atlan─▒yor" -ForegroundColor Yellow
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
Write-Host "`n­şôä Field-Level Validation: PageCount`n" -ForegroundColor Cyan

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
Write-Host "`n­şôà Field-Level Validation: PublicationDate`n" -ForegroundColor Cyan

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
Write-Host "`n­şîÉ Field-Level Validation: Language Pattern`n" -ForegroundColor Cyan

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
Write-Host "`n­şÆ░ Field-Level Validation: Price`n" -ForegroundColor Cyan

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
Write-Host "`n­şöó Expression-Based Validation`n" -ForegroundColor Cyan

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
# Test 8: HTTP Validation
# ============================================
Write-Host "`n🌐 HTTP Validation Tests`n" -ForegroundColor Cyan

Write-Host "Note: HTTP validation flow kuralı: price > 50 ise isValid: true, price <= 50 ise isValid: false" -ForegroundColor Yellow
Write-Host "Flow URL: http://localhost:1880/dg_validasyontest`n" -ForegroundColor Yellow

# Test 8.1: HTTP validation başarısız (price = 50, isValid: false dönecek - çünkü price > 50 kontrolü var)
Test-Validation -TestName "HTTP validation başarısız (price = 50 - should fail)" -Data @{
    title = "HTTP Invalid Book - Price 50"
    publisher = $publisherId
    author = $authorId
    price = 50
    pageCount = 100
    publisherCode = $publisherCode
} -ShouldFail $true

# Test 8.2: HTTP validation başarılı (price > 50, isValid: true dönecek)
Test-Validation -TestName "HTTP validation başarılı (price > 50 - should pass)" -Data @{
    title = "HTTP Validated Book - Price High"
    publisher = $publisherId
    author = $authorId
    price = 75
    pageCount = 100
    publisherCode = $publisherCode
} -ShouldFail $false

# Test 8.3: HTTP validation başarısız (price < 50, isValid: false dönecek)
Test-Validation -TestName "HTTP validation başarısız (price < 50 - should fail)" -Data @{
    title = "HTTP Invalid Book - Price Low"
    publisher = $publisherId
    author = $authorId
    price = 49
    pageCount = 100
    publisherCode = $publisherCode
} -ShouldFail $true

# Test 8.4: HTTP validation başarısız (price = 0, isValid: false dönecek)
Test-Validation -TestName "HTTP validation başarısız (price = 0 - should fail)" -Data @{
    title = "HTTP Invalid Book - Price Zero"
    publisher = $publisherId
    author = $authorId
    price = 0
    pageCount = 100
    publisherCode = $publisherCode
} -ShouldFail $true

# Test 8.5: HTTP validation başarısız (price = 25, isValid: false dönecek)
Test-Validation -TestName "HTTP validation başarısız (price = 25 - should fail)" -Data @{
    title = "HTTP Invalid Book - Price 25"
    publisher = $publisherId
    author = $authorId
    price = 25
    pageCount = 100
    publisherCode = $publisherCode
} -ShouldFail $true

# ============================================
# Test 9: Edge Cases - Boundary Values
# ============================================
Write-Host "`n🎯 Edge Cases: Boundary Values`n" -ForegroundColor Cyan

# Test 9.1: Title minimum length (exactly 3 chars - should pass)
Test-Validation -TestName "Title minimum boundary (3 chars - should pass)" -Data @{
    title = "ABC"
    publisher = $publisherId
    author = $authorId
    price = 50
    publisherCode = $publisherCode
} -ShouldFail $false

# Test 9.2: Title maximum length (exactly 200 chars - should pass)
Test-Validation -TestName "Title maximum boundary (200 chars - should pass)" -Data @{
    title = "A" * 200
    publisher = $publisherId
    author = $authorId
    price = 50
    publisherCode = $publisherCode
} -ShouldFail $false

# Test 9.3: PageCount minimum boundary (exactly 1 - should pass)
# Note: price must be <= 10 when pageCount=1 to satisfy price_page_ratio validation
Test-Validation -TestName "PageCount minimum boundary (1 - should pass)" -Data @{
    title = "Test Book"
    publisher = $publisherId
    author = $authorId
    pageCount = 1
    price = 10
    publisherCode = $publisherCode
} -ShouldFail $false

# Test 9.4: PageCount maximum boundary (exactly 10000 - should pass)
Test-Validation -TestName "PageCount maximum boundary (10000 - should pass)" -Data @{
    title = "Test Book"
    publisher = $publisherId
    author = $authorId
    pageCount = 10000
    price = 50
    publisherCode = $publisherCode
} -ShouldFail $false

# Test 9.5: Price minimum boundary (exactly 0 - should pass)
Test-Validation -TestName "Price minimum boundary (0 - should pass)" -Data @{
    title = "Test Book"
    publisher = $publisherId
    author = $authorId
    price = 0
    publisherCode = $publisherCode
} -ShouldFail $false

# Test 9.6: Price maximum boundary (exactly 100000 - should pass)
Test-Validation -TestName "Price maximum boundary (100000 - should pass)" -Data @{
    title = "Test Book"
    publisher = $publisherId
    author = $authorId
    price = 100000
    publisherCode = $publisherCode
} -ShouldFail $false

# Test 9.7: PublicationDate minimum boundary (1900-01-01 - should pass)
Test-Validation -TestName "PublicationDate minimum boundary (1900-01-01 - should pass)" -Data @{
    title = "Test Book"
    publisher = $publisherId
    author = $authorId
    publicationDate = "1900-01-01T00:00:00Z"
    price = 50
    publisherCode = $publisherCode
} -ShouldFail $false

# Test 9.8: PublicationDate maximum boundary (2100-12-31T12:00:00Z - should pass)
# Note: Using mid-day to avoid potential precision/timezone issues at exact boundary
Test-Validation -TestName "PublicationDate maximum boundary (2100-12-31T12:00:00Z - should pass)" -Data @{
    title = "Test Book"
    publisher = $publisherId
    author = $authorId
    publicationDate = "2100-12-31T12:00:00Z"
    price = 50
    publisherCode = $publisherCode
} -ShouldFail $false

# Test 9.9: Price/Page ratio maximum boundary (exactly 10 - should pass)
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
Write-Host "ÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉ" -ForegroundColor Cyan
Write-Host "­şôè TEST ├ûZET─░" -ForegroundColor Cyan
Write-Host "ÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉ" -ForegroundColor Cyan
Write-Host "Toplam Test: $testCount" -ForegroundColor White
Write-Host "Ô£à Ba┼şar─▒l─▒: $passCount" -ForegroundColor Green
Write-Host "ÔØî Ba┼şar─▒s─▒z: $failCount" -ForegroundColor Red
Write-Host "ÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉ" -ForegroundColor Cyan

if ($failCount -eq 0) {
    Write-Host "`n­şÄë T├╝m testler ba┼şar─▒l─▒!`n" -ForegroundColor Green
    exit 0
} else {
    Write-Host "`nÔÜá´©Å  Baz─▒ testler ba┼şar─▒s─▒z oldu.`n" -ForegroundColor Yellow
    exit 1
}

