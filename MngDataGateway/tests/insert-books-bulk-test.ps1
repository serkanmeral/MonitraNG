# Books Dataset Bulk Insert Test Script
# Tests bulk insert functionality for tst_books dataset

$baseUrl = "https://localhost:5010"

# Token dosyasının yolunu belirle
$tokenFile = "$env:TEMP\serkan_token.txt"

# Token'ı kontrol et
if (-not (Test-Path $tokenFile)) {
    Write-Host "❌ Token bulunamadı! Önce token almak için:" -ForegroundColor Red
    Write-Host "   pwsh -ExecutionPolicy Bypass -File get-serkan-token.ps1" -ForegroundColor Yellow
    Write-Host ""
    exit 1
}

# Token'ı oku
$token = Get-Content $tokenFile -Raw
$token = $token.Trim()

$headers = @{
    "Authorization" = "Bearer $token"
    "Content-Type" = "application/json"
}

# Ignore SSL certificate errors
[System.Net.ServicePointManager]::ServerCertificateValidationCallback = {$true}

Write-Host "`n📚 Testing Bulk Insert for Books Dataset...`n" -ForegroundColor Cyan

# ============================================
# Step 1: Get existing publishers and genres
# ============================================
Write-Host "📖 Step 1: Fetching existing publishers and genres..." -ForegroundColor Yellow

$publisherIds = @{}
$genreIds = @{}

try {
    # Get publishers
    $publishersResponse = Invoke-RestMethod -Uri "$baseUrl/api/data/tst_publishers?pageSize=100" -Method GET -Headers $headers -SkipCertificateCheck
    if ($publishersResponse.items) {
        foreach ($pub in $publishersResponse.items) {
            $dataId = $pub.__dataId
            if (-not $dataId) { $dataId = $pub.dataId }
            if (-not $dataId) { $dataId = $pub.DataId }
            if ($dataId -and $pub.name) {
                $publisherIds[$pub.name] = $dataId
            }
        }
        Write-Host "   ✅ Found $($publisherIds.Count) publishers" -ForegroundColor Green
    }
    
    # Get genres
    $genresResponse = Invoke-RestMethod -Uri "$baseUrl/api/data/tst_genres?pageSize=100" -Method GET -Headers $headers -SkipCertificateCheck
    if ($genresResponse.items) {
        foreach ($genre in $genresResponse.items) {
            $dataId = $genre.__dataId
            if (-not $dataId) { $dataId = $genre.dataId }
            if (-not $dataId) { $dataId = $genre.DataId }
            if ($dataId -and $genre.name) {
                $genreIds[$genre.name] = $dataId
            }
        }
        Write-Host "   ✅ Found $($genreIds.Count) genres" -ForegroundColor Green
    }
} catch {
    Write-Host "   ❌ Failed to fetch publishers/genres: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "   ⚠️  Please run insert-books-test-data.ps1 first to create publishers and genres" -ForegroundColor Yellow
    exit 1
}

if ($publisherIds.Count -eq 0 -or $genreIds.Count -eq 0) {
    Write-Host "   ❌ No publishers or genres found. Please run insert-books-test-data.ps1 first." -ForegroundColor Red
    exit 1
}

# Get first publisher and some genres for test
$firstPublisherId = ($publisherIds.Values | Select-Object -First 1)
$testGenreIds = ($genreIds.Values | Select-Object -First 3)

Write-Host "   Using Publisher ID: $firstPublisherId" -ForegroundColor Gray
Write-Host "   Using Genre IDs: $($testGenreIds -join ', ')" -ForegroundColor Gray
Write-Host ""

# ============================================
# Step 2: Prepare bulk insert data
# ============================================
Write-Host "📗 Step 2: Preparing bulk insert data..." -ForegroundColor Yellow

$currentYear = (Get-Date).Year

# Create 20 test books for bulk insert
$bulkBooks = @()

for ($i = 1; $i -le 20; $i++) {
    $bookNumber = $i.ToString("00")
    $yearOffset = Get-Random -Minimum 0 -Maximum 10
    
    # Vary the data to test different scenarios
    $book = @{
        title = "Bulk Test Book $bookNumber"
        subtitle = "Test Subtitle for Book $bookNumber"
        publisherCode = "BULK$bookNumber"
        name = "BulkBook$bookNumber"
        publisher = $firstPublisherId
        genres = $testGenreIds
        author = @{
            uid = "bulk-user-$bookNumber"
            userName = "bulk.author.$bookNumber"
            domain = "meral"
            displayName = "Bulk Author $bookNumber"
        }
        pageCount = (Get-Random -Minimum 200 -Maximum 600)
        publicationDate = "$($currentYear - $yearOffset)-$(Get-Random -Minimum 1 -Maximum 13)-$(Get-Random -Minimum 1 -Maximum 29)T00:00:00Z"
        language = "English"
        price = [Math]::Round((Get-Random -Minimum 10.00 -Maximum 50.00), 2)
    }
    
    # Add optional fields randomly
    if ($i % 3 -eq 0) {
        $book["coAuthors"] = @(
            @{
                uid = "bulk-coauthor-$bookNumber"
                userName = "bulk.coauthor.$bookNumber"
                domain = "meral"
                displayName = "Bulk Co-Author $bookNumber"
            }
        )
    }
    
    if ($i % 4 -eq 0) {
        $book["reviewerGroups"] = @(
            @{
                groupName = "bulk-reviewers-$bookNumber"
                domain = "meral"
            }
        )
    }
    
    if ($i % 5 -eq 0) {
        $book["editorialTeam"] = @{
            groupName = "bulk-editors-$bookNumber"
            domain = "meral"
        }
    }
    
    if ($i % 6 -eq 0) {
        $book["coverImage"] = @{
            url = "https://example.com/covers/bulk-$bookNumber.jpg"
            alt = "Bulk Test Book $bookNumber Cover"
            width = 400
            height = 600
        }
    }
    
    $bulkBooks += $book
}

Write-Host "   ✅ Prepared $($bulkBooks.Count) books for bulk insert" -ForegroundColor Green
Write-Host ""

# ============================================
# Step 3: Execute bulk insert
# ============================================
Write-Host "🚀 Step 3: Executing bulk insert..." -ForegroundColor Yellow

$bulkRequest = @{
    items = $bulkBooks
} | ConvertTo-Json -Depth 10

try {
    $startTime = Get-Date
    $response = Invoke-RestMethod -Uri "$baseUrl/api/data/tst_books/bulk" -Method POST -Headers $headers -Body $bulkRequest -SkipCertificateCheck
    $endTime = Get-Date
    $duration = ($endTime - $startTime).TotalSeconds
    
    Write-Host "   ✅ Bulk insert completed in $([Math]::Round($duration, 2)) seconds" -ForegroundColor Green
    Write-Host ""
    
    # Display results
    # Response format: { success: true, data: { total: ..., successful: ..., failed: ..., items: [...], errors: [...] }, meta: {...} }
    $result = $response.data
    if (-not $result) { $result = $response.Data }
    
    if ($result) {
        Write-Host "📊 Bulk Insert Results:" -ForegroundColor Cyan
        Write-Host "   Total Items: $($result.total)" -ForegroundColor White
        Write-Host "   Successful: $($result.successful)" -ForegroundColor Green
        Write-Host "   Failed: $($result.failed)" -ForegroundColor $(if ($result.failed -gt 0) { "Red" } else { "Green" })
        Write-Host ""
        
        if ($result.items -and $result.items.Count -gt 0) {
            Write-Host "   ✅ Successfully inserted items (showing first 5):" -ForegroundColor Green
            $result.items | Select-Object -First 5 | ForEach-Object {
                $itemId = $_.__dataId
                if (-not $itemId) { $itemId = $_.dataId }
                if (-not $itemId) { $itemId = $_.DataId }
                $title = $_.title
                if (-not $title) { $title = "N/A" }
                Write-Host "      - $title (ID: $itemId)" -ForegroundColor Gray
            }
            if ($result.items.Count -gt 5) {
                Write-Host "      ... and $($result.items.Count - 5) more" -ForegroundColor Gray
            }
            Write-Host ""
        }
        
        if ($result.errors -and $result.errors.Count -gt 0) {
            Write-Host "   ❌ Failed items (showing first 5):" -ForegroundColor Red
            $result.errors | Select-Object -First 5 | ForEach-Object {
                $errorMsg = $_.error
                if (-not $errorMsg) { $errorMsg = $_.Error }
                if (-not $errorMsg) { $errorMsg = "Unknown error" }
                $index = $_.index
                if (-not $index) { $index = $_.Index }
                Write-Host "      - Item #$index : $errorMsg" -ForegroundColor Yellow
            }
            if ($result.errors.Count -gt 5) {
                Write-Host "      ... and $($result.errors.Count - 5) more errors" -ForegroundColor Yellow
            }
            Write-Host ""
        }
    } else {
        Write-Host "   ⚠️  Response format unexpected. Full response:" -ForegroundColor Yellow
        Write-Host ($response | ConvertTo-Json -Depth 5) -ForegroundColor Gray
    }
    
} catch {
    Write-Host "   ❌ Bulk insert failed: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.ErrorDetails.Message) {
        Write-Host "   Details: $($_.ErrorDetails.Message)" -ForegroundColor Gray
    }
    if ($_.Exception.Response) {
        try {
            $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
            $responseBody = $reader.ReadToEnd()
            Write-Host "   Response: $responseBody" -ForegroundColor Gray
        } catch {
            # Ignore stream read errors
        }
    }
    exit 1
}

Write-Host ""
Write-Host "✅ Bulk insert test completed!" -ForegroundColor Green
Write-Host ""
Write-Host "💡 Tip: You can verify the inserted books by querying:" -ForegroundColor Cyan
Write-Host "   GET $baseUrl/api/data/tst_books?search=Bulk%20Test&pageSize=50" -ForegroundColor Gray

