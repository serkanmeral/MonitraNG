# Books Dataset Test Data Insertion Script
# Inserts test data for tst_publishers, tst_genres, and tst_books datasets

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

Write-Host "`n📚 Inserting Books Test Data...`n" -ForegroundColor Cyan

# ============================================
# Step 1: Insert Publishers
# ============================================
Write-Host "📖 Step 1: Inserting publishers..." -ForegroundColor Yellow

$publishers = @(
    @{
        name = "Penguin Random House"
        website = "https://www.penguinrandomhouse.com"
        country = "USA"
    },
    @{
        name = "HarperCollins"
        website = "https://www.harpercollins.com"
        country = "USA"
    },
    @{
        name = "Simon & Schuster"
        website = "https://www.simonandschuster.com"
        country = "USA"
    },
    @{
        name = "Macmillan Publishers"
        website = "https://www.macmillan.com"
        country = "UK"
    },
    @{
        name = "Hachette Livre"
        website = "https://www.hachette.com"
        country = "France"
    }
)

$publisherIds = @{}

foreach ($publisher in $publishers) {
    try {
        $body = $publisher | ConvertTo-Json -Depth 10
        $response = Invoke-RestMethod -Uri "$baseUrl/api/data/tst_publishers" -Method POST -Headers $headers -Body $body -SkipCertificateCheck
        
        # Response format: { success: true, data: { __dataId: "...", ... }, meta: {...} }
        $dataId = $response.data.__dataId
        if (-not $dataId) { $dataId = $response.Data.__dataId }
        if (-not $dataId) { $dataId = $response.__dataId }
        $publisherIds[$publisher.name] = $dataId
        Write-Host "   ✅ Created: $($publisher.name) (ID: $($response.dataId))" -ForegroundColor Green
    } catch {
        if ($_.Exception.Response.StatusCode -eq 409) {
            Write-Host "   ⚠️  Already exists: $($publisher.name)" -ForegroundColor Yellow
            # Try to get existing publisher
            try {
                $getResponse = Invoke-RestMethod -Uri "$baseUrl/api/data/tst_publishers?search=$($publisher.name)" -Method GET -Headers $headers -SkipCertificateCheck
                if ($getResponse.items -and $getResponse.items.Count -gt 0) {
                    $item = $getResponse.items[0]
                    $dataId = $item.__dataId
                    if (-not $dataId) { $dataId = $item.dataId }
                    if (-not $dataId) { $dataId = $item.DataId }
                    $publisherIds[$publisher.name] = $dataId
                }
            } catch {
                Write-Host "   ❌ Failed to get existing publisher: $($_.Exception.Message)" -ForegroundColor Red
            }
        } else {
            Write-Host "   ❌ Failed to create $($publisher.name): $($_.Exception.Message)" -ForegroundColor Red
            if ($_.ErrorDetails.Message) {
                Write-Host "      Details: $($_.ErrorDetails.Message)" -ForegroundColor Gray
            }
        }
    }
}

Write-Host ""

# ============================================
# Step 2: Insert Genres
# ============================================
Write-Host "📚 Step 2: Inserting genres..." -ForegroundColor Yellow

$genres = @(
    @{
        name = "Science Fiction"
        description = "Futuristic and science-based fiction"
    },
    @{
        name = "Fantasy"
        description = "Imaginative fiction with magical elements"
    },
    @{
        name = "Mystery"
        description = "Detective and crime fiction"
    },
    @{
        name = "Romance"
        description = "Love stories and romantic relationships"
    },
    @{
        name = "Thriller"
        description = "Suspenseful and exciting stories"
    },
    @{
        name = "Historical Fiction"
        description = "Fiction set in the past"
    },
    @{
        name = "Biography"
        description = "True stories of people's lives"
    },
    @{
        name = "Self-Help"
        description = "Personal development and improvement"
    }
)

$genreIds = @{}

foreach ($genre in $genres) {
    try {
        $body = $genre | ConvertTo-Json -Depth 10
        $response = Invoke-RestMethod -Uri "$baseUrl/api/data/tst_genres" -Method POST -Headers $headers -Body $body -SkipCertificateCheck
        
        # Response format: { success: true, data: { __dataId: "...", ... }, meta: {...} }
        $dataId = $response.data.__dataId
        if (-not $dataId) { $dataId = $response.Data.__dataId }
        if (-not $dataId) { $dataId = $response.__dataId }
        $genreIds[$genre.name] = $dataId
        Write-Host "   ✅ Created: $($genre.name) (ID: $($response.dataId))" -ForegroundColor Green
    } catch {
        if ($_.Exception.Response.StatusCode -eq 409) {
            Write-Host "   ⚠️  Already exists: $($genre.name)" -ForegroundColor Yellow
            # Try to get existing genre
            try {
                $getResponse = Invoke-RestMethod -Uri "$baseUrl/api/data/tst_genres?search=$($genre.name)" -Method GET -Headers $headers -SkipCertificateCheck
                if ($getResponse.items -and $getResponse.items.Count -gt 0) {
                    $item = $getResponse.items[0]
                    $dataId = $item.__dataId
                    if (-not $dataId) { $dataId = $item.dataId }
                    if (-not $dataId) { $dataId = $item.DataId }
                    $genreIds[$genre.name] = $dataId
                }
            } catch {
                Write-Host "   ❌ Failed to get existing genre: $($_.Exception.Message)" -ForegroundColor Red
            }
        } else {
            Write-Host "   ❌ Failed to create $($genre.name): $($_.Exception.Message)" -ForegroundColor Red
            if ($_.ErrorDetails.Message) {
                Write-Host "      Details: $($_.ErrorDetails.Message)" -ForegroundColor Gray
            }
        }
    }
}

Write-Host ""

# ============================================
# Step 3: Insert Books
# ============================================
Write-Host "📗 Step 3: Inserting books..." -ForegroundColor Yellow

# Helper function to get random publisher ID
function Get-RandomPublisherId {
    $keys = $publisherIds.Keys | Get-Random
    return $publisherIds[$keys]
}

# Helper function to get random genre IDs (multiple)
function Get-RandomGenreIds {
    $count = Get-Random -Minimum 1 -Maximum 4
    $selectedGenres = $genreIds.Keys | Get-Random -Count $count
    return $selectedGenres | ForEach-Object { $genreIds[$_] }
}

# Get current year for date calculations
$currentYear = (Get-Date).Year

$books = @(
    @{
        title = "The Foundation"
        subtitle = "A Science Fiction Masterpiece"
        publisherCode = "PRH"
        name = "Foundation Classic"
        publisher = $publisherIds["Penguin Random House"]
        genres = @($genreIds["Science Fiction"], $genreIds["Fantasy"])
        author = @{
            uid = "user-001"
            userName = "isaac.asimov"
            domain = "meral"
            displayName = "Isaac Asimov"
        }
        coAuthors = @()
        reviewerGroups = @(
            @{
                groupName = "literary-critics"
                domain = "meral"
            }
        )
        editorialTeam = @{
            groupName = "editors"
            domain = "meral"
        }
        pageCount = 320
        publicationDate = "$($currentYear - 5)-01-15T00:00:00Z"
        language = "English"
        price = 29.99
        coverImage = @{
            url = "https://example.com/covers/foundation.jpg"
            alt = "Foundation Book Cover"
            width = 400
            height = 600
        }
    },
    @{
        title = "The Hobbit"
        subtitle = "There and Back Again"
        publisherCode = "HC"
        name = "Hobbit Classic"
        publisher = $publisherIds["HarperCollins"]
        genres = @($genreIds["Fantasy"])
        author = @{
            uid = "user-002"
            userName = "j.r.r.tolkien"
            domain = "meral"
            displayName = "J.R.R. Tolkien"
        }
        coAuthors = @()
        reviewerGroups = @(
            @{
                groupName = "fantasy-reviewers"
                domain = "meral"
            },
            @{
                groupName = "literary-critics"
                domain = "meral"
            }
        )
        editorialTeam = @{
            groupName = "editors"
            domain = "meral"
        }
        pageCount = 310
        publicationDate = "$($currentYear - 3)-06-20T00:00:00Z"
        language = "English"
        price = 24.99
        coverImage = @{
            url = "https://example.com/covers/hobbit.jpg"
            alt = "The Hobbit Book Cover"
            width = 400
            height = 600
        }
    },
    @{
        title = "The Da Vinci Code"
        subtitle = "A Thrilling Mystery"
        publisherCode = "SS"
        name = "Da Vinci Code"
        publisher = $publisherIds["Simon & Schuster"]
        genres = @($genreIds["Mystery"], $genreIds["Thriller"])
        author = @{
            uid = "user-003"
            userName = "dan.brown"
            domain = "meral"
            displayName = "Dan Brown"
        }
        coAuthors = @()
        reviewerGroups = @(
            @{
                groupName = "mystery-reviewers"
                domain = "meral"
            }
        )
        editorialTeam = $null
        pageCount = 454
        publicationDate = "$($currentYear - 2)-03-10T00:00:00Z"
        language = "English"
        price = 19.99
        coverImage = $null
    },
    @{
        title = "Pride and Prejudice"
        subtitle = "A Timeless Romance"
        publisherCode = "MP"
        name = "Pride and Prejudice"
        publisher = $publisherIds["Macmillan Publishers"]
        genres = @($genreIds["Romance"], $genreIds["Historical Fiction"])
        author = @{
            uid = "user-004"
            userName = "jane.austen"
            domain = "meral"
            displayName = "Jane Austen"
        }
        coAuthors = @()
        reviewerGroups = @()
        editorialTeam = @{
            groupName = "classic-editors"
            domain = "meral"
        }
        pageCount = 432
        publicationDate = "$($currentYear - 1)-09-05T00:00:00Z"
        language = "English"
        price = 15.99
    },
    @{
        title = "The Art of War"
        subtitle = "Ancient Strategy for Modern Times"
        publisherCode = "HL"
        name = "Art of War"
        publisher = $publisherIds["Hachette Livre"]
        genres = @($genreIds["Biography"], $genreIds["Self-Help"])
        author = @{
            uid = "user-005"
            userName = "sun.tzu"
            domain = "meral"
            displayName = "Sun Tzu"
        }
        coAuthors = @(
            @{
                uid = "user-006"
                userName = "commentator.one"
                domain = "meral"
                displayName = "Commentator One"
            }
        )
        reviewerGroups = @(
            @{
                groupName = "strategy-reviewers"
                domain = "meral"
            }
        )
        editorialTeam = @{
            groupName = "nonfiction-editors"
            domain = "meral"
        }
        pageCount = 128
        publicationDate = "$($currentYear - 4)-11-12T00:00:00Z"
        language = "English"
        price = 12.99
        coverImage = @{
            url = "https://example.com/covers/art-of-war.jpg"
            alt = "The Art of War Book Cover"
            width = 350
            height = 525
        }
    },
    @{
        title = "1984"
        subtitle = "A Dystopian Classic"
        publisherCode = "PRH"
        name = "1984 Classic"
        publisher = $publisherIds["Penguin Random House"]
        genres = @($genreIds["Science Fiction"], $genreIds["Thriller"])
        author = @{
            uid = "user-007"
            userName = "george.orwell"
            domain = "meral"
            displayName = "George Orwell"
        }
        coAuthors = @()
        reviewerGroups = @(
            @{
                groupName = "literary-critics"
                domain = "meral"
            },
            @{
                groupName = "dystopian-reviewers"
                domain = "meral"
            }
        )
        editorialTeam = @{
            groupName = "editors"
            domain = "meral"
        }
        pageCount = 328
        publicationDate = "$($currentYear - 6)-08-15T00:00:00Z"
        language = "English"
        price = 18.99
    },
    @{
        title = "To Kill a Mockingbird"
        subtitle = "A Coming-of-Age Story"
        publisherCode = "HC"
        name = "Mockingbird"
        publisher = $publisherIds["HarperCollins"]
        genres = @($genreIds["Historical Fiction"], $genreIds["Mystery"])
        author = @{
            uid = "user-008"
            userName = "harper.lee"
            domain = "meral"
            displayName = "Harper Lee"
        }
        coAuthors = @()
        reviewerGroups = @()
        editorialTeam = @{
            groupName = "classic-editors"
            domain = "meral"
        }
        pageCount = 376
        publicationDate = "$($currentYear - 7)-07-11T00:00:00Z"
        language = "English"
        price = 16.99
        coverImage = @{
            url = "https://example.com/covers/mockingbird.jpg"
            alt = "To Kill a Mockingbird Cover"
            width = 400
            height = 600
        }
    },
    @{
        title = "The Great Gatsby"
        subtitle = "The American Dream"
        publisherCode = "SS"
        name = "Great Gatsby"
        publisher = $publisherIds["Simon & Schuster"]
        genres = @($genreIds["Romance"], $genreIds["Historical Fiction"])
        author = @{
            uid = "user-009"
            userName = "f.scott.fitzgerald"
            domain = "meral"
            displayName = "F. Scott Fitzgerald"
        }
        coAuthors = @()
        reviewerGroups = @(
            @{
                groupName = "literary-critics"
                domain = "meral"
            }
        )
        editorialTeam = $null
        pageCount = 180
        publicationDate = "$($currentYear - 8)-04-10T00:00:00Z"
        language = "English"
        price = 14.99
    }
)

$bookCount = 0
$bookErrors = 0

foreach ($book in $books) {
    try {
        # Remove null values for cleaner JSON
        $cleanBook = @{}
        foreach ($key in $book.Keys) {
            if ($null -ne $book[$key]) {
                $cleanBook[$key] = $book[$key]
            }
        }
        
        $body = $cleanBook | ConvertTo-Json -Depth 10
        $response = Invoke-RestMethod -Uri "$baseUrl/api/data/tst_books" -Method POST -Headers $headers -Body $body -SkipCertificateCheck
        
        $bookCount++
        $dataId = $response.data.__dataId
        if (-not $dataId) { $dataId = $response.Data.__dataId }
        if (-not $dataId) { $dataId = $response.__dataId }
        Write-Host "   ✅ Created: $($book.title) (ID: $dataId)" -ForegroundColor Green
    } catch {
        $bookErrors++
        Write-Host "   ❌ Failed to create '$($book.title)': $($_.Exception.Message)" -ForegroundColor Red
        if ($_.ErrorDetails.Message) {
            Write-Host "      Details: $($_.ErrorDetails.Message)" -ForegroundColor Gray
        }
    }
}

Write-Host ""
Write-Host "📊 Summary:" -ForegroundColor Cyan
Write-Host "   Publishers: $($publisherIds.Count) available" -ForegroundColor White
Write-Host "   Genres: $($genreIds.Count) available" -ForegroundColor White
Write-Host "   Books: $bookCount created, $bookErrors errors" -ForegroundColor $(if ($bookErrors -eq 0) { "Green" } else { "Yellow" })
Write-Host ""
Write-Host "✅ Test data insertion completed!" -ForegroundColor Green

