# Books Dataset Test Data Insertion Script
# Inserts test data for tst_publishers, tst_genres, and tst_books datasets

$baseUrl = "https://localhost:5010"
$keeperUrl = "https://localhost:5001"

# Token'ı yükle (ortak script kullanarak)
$scriptPath = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $PSCommandPath }
if ([string]::IsNullOrEmpty($scriptPath)) {
    $scriptPath = Get-Location
}
$loadTokenScript = Join-Path $scriptPath "..\auth\load-token.ps1"

if (-not (Test-Path $loadTokenScript)) {
    Write-Host "❌ load-token.ps1 bulunamadı! Path: $loadTokenScript" -ForegroundColor Red
    exit 1
}

$token = & $loadTokenScript

if ([string]::IsNullOrEmpty($token)) {
    Write-Host "❌ Token alınamadı! Testler durduruluyor." -ForegroundColor Red
    exit 1
}

$tokenFile = "$env:TEMP\serkan_token.txt"

$headers = @{
    "Authorization" = "Bearer $token"
    "Content-Type" = "application/json"
}

# Ignore SSL certificate errors
[System.Net.ServicePointManager]::ServerCertificateValidationCallback = {$true}

Write-Host "`n📚 Inserting Books Test Data...`n" -ForegroundColor Cyan

# ============================================
# Step 0: Fetch Users and Groups from MngKeeper
# ============================================
Write-Host "👥 Step 0: Fetching users and groups from MngKeeper..." -ForegroundColor Yellow

$userIds = @()
$groupIds = @()

try {
    # Get users from MngKeeper
    $usersResponse = Invoke-RestMethod -Uri "$keeperUrl/api/user?pageSize=100" -Method GET -Headers $headers -SkipCertificateCheck
    if ($usersResponse.users -and $usersResponse.users.Count -gt 0) {
        $userIds = $usersResponse.users | ForEach-Object { $_.userId }
        Write-Host "   ✅ Found $($userIds.Count) users" -ForegroundColor Green
    } else {
        Write-Host "   ⚠️  No users found in MngKeeper" -ForegroundColor Yellow
    }
    
    # Get groups from MngKeeper
    $groupsResponse = Invoke-RestMethod -Uri "$keeperUrl/api/group?pageSize=100" -Method GET -Headers $headers -SkipCertificateCheck
    if ($groupsResponse.groups -and $groupsResponse.groups.Count -gt 0) {
        $groupIds = $groupsResponse.groups | ForEach-Object { $_.groupId }
        Write-Host "   ✅ Found $($groupIds.Count) groups" -ForegroundColor Green
    } else {
        Write-Host "   ⚠️  No groups found in MngKeeper" -ForegroundColor Yellow
    }
    
    if ($userIds.Count -eq 0) {
        Write-Host "   ❌ No users available. Cannot proceed with book insertion." -ForegroundColor Red
        exit 1
    }
    
    if ($groupIds.Count -eq 0) {
        Write-Host "   ⚠️  No groups available. reviewerGroups will be empty." -ForegroundColor Yellow
    }
    
} catch {
    Write-Host "   ❌ Failed to fetch users/groups from MngKeeper: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.ErrorDetails.Message) {
        Write-Host "      Details: $($_.ErrorDetails.Message)" -ForegroundColor Gray
    }
    exit 1
}

Write-Host ""

# ============================================
# Step 1: Get or Insert Publishers
# ============================================
Write-Host "📖 Step 1: Getting or inserting publishers..." -ForegroundColor Yellow

$publisherIds = @{}

# First, try to get all existing publishers
try {
    $existingPublishersResponse = Invoke-RestMethod -Uri "$baseUrl/api/data/tst_publishers?pageSize=100" -Method GET -Headers $headers -SkipCertificateCheck
    # Response might be { items: [...] } or direct array
    $publisherList = $null
    if ($existingPublishersResponse.items) {
        $publisherList = $existingPublishersResponse.items
    } elseif ($existingPublishersResponse -is [array]) {
        $publisherList = $existingPublishersResponse
    } elseif ($existingPublishersResponse.data -and $existingPublishersResponse.data.items) {
        $publisherList = $existingPublishersResponse.data.items
    }
    
    if ($publisherList -and $publisherList.Count -gt 0) {
        foreach ($pub in $publisherList) {
            $dataId = $pub.__dataId
            if (-not $dataId) { $dataId = $pub.dataId }
            if (-not $dataId) { $dataId = $pub.DataId }
            if ($dataId -and $pub.name) {
                $publisherIds[$pub.name] = $dataId
            }
        }
        Write-Host "   ✅ Found $($publisherIds.Count) existing publishers" -ForegroundColor Green
    } else {
        Write-Host "   ℹ️  No existing publishers found" -ForegroundColor Gray
    }
} catch {
    Write-Host "   ⚠️  Could not fetch existing publishers: $($_.Exception.Message)" -ForegroundColor Yellow
}

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

# Only insert publishers that don't exist yet
foreach ($publisher in $publishers) {
    if ($publisherIds.ContainsKey($publisher.name)) {
        Write-Host "   ⏭️  Skipping (already exists): $($publisher.name)" -ForegroundColor Gray
        continue
    }
    try {
        $body = $publisher | ConvertTo-Json -Depth 10
        $response = Invoke-RestMethod -Uri "$baseUrl/api/data/tst_publishers" -Method POST -Headers $headers -Body $body -SkipCertificateCheck
        
        # Response format: { success: true, data: { __dataId: "...", ... }, meta: {...} }
        $dataId = $response.data.__dataId
        if (-not $dataId) { $dataId = $response.Data.__dataId }
        if (-not $dataId) { $dataId = $response.__dataId }
        $publisherIds[$publisher.name] = $dataId
        Write-Host "   ✅ Created: $($publisher.name) (ID: $dataId)" -ForegroundColor Green
    } catch {
        $statusCode = $_.Exception.Response.StatusCode.value__
        if ($statusCode -eq 409 -or $statusCode -eq 400) {
            Write-Host "   ⚠️  Already exists or validation error: $($publisher.name)" -ForegroundColor Yellow
            # Try to get existing publisher
            try {
                $getResponse = Invoke-RestMethod -Uri "$baseUrl/api/data/tst_publishers?search=$($publisher.name)" -Method GET -Headers $headers -SkipCertificateCheck
                if ($getResponse.items -and $getResponse.items.Count -gt 0) {
                    $item = $getResponse.items[0]
                    $dataId = $item.__dataId
                    if (-not $dataId) { $dataId = $item.dataId }
                    if (-not $dataId) { $dataId = $item.DataId }
                    if ($dataId) {
                        $publisherIds[$publisher.name] = $dataId
                        Write-Host "   ✅ Found existing: $($publisher.name) (ID: $dataId)" -ForegroundColor Green
                    }
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
# Step 2: Get or Insert Genres
# ============================================
Write-Host "📚 Step 2: Getting or inserting genres..." -ForegroundColor Yellow

$genreIds = @{}

# First, try to get all existing genres
try {
    $existingGenresResponse = Invoke-RestMethod -Uri "$baseUrl/api/data/tst_genres?pageSize=100" -Method GET -Headers $headers -SkipCertificateCheck
    # Response might be { items: [...] } or direct array
    $genreList = $null
    if ($existingGenresResponse.items) {
        $genreList = $existingGenresResponse.items
    } elseif ($existingGenresResponse -is [array]) {
        $genreList = $existingGenresResponse
    } elseif ($existingGenresResponse.data -and $existingGenresResponse.data.items) {
        $genreList = $existingGenresResponse.data.items
    }
    
    if ($genreList -and $genreList.Count -gt 0) {
        foreach ($genre in $genreList) {
            $dataId = $genre.__dataId
            if (-not $dataId) { $dataId = $genre.dataId }
            if (-not $dataId) { $dataId = $genre.DataId }
            if ($dataId -and $genre.name) {
                $genreIds[$genre.name] = $dataId
            }
        }
        Write-Host "   ✅ Found $($genreIds.Count) existing genres" -ForegroundColor Green
    } else {
        Write-Host "   ℹ️  No existing genres found" -ForegroundColor Gray
    }
} catch {
    Write-Host "   ⚠️  Could not fetch existing genres: $($_.Exception.Message)" -ForegroundColor Yellow
}

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

# Only insert genres that don't exist yet
foreach ($genre in $genres) {
    if ($genreIds.ContainsKey($genre.name)) {
        Write-Host "   ⏭️  Skipping (already exists): $($genre.name)" -ForegroundColor Gray
        continue
    }
    try {
        $body = $genre | ConvertTo-Json -Depth 10
        $response = Invoke-RestMethod -Uri "$baseUrl/api/data/tst_genres" -Method POST -Headers $headers -Body $body -SkipCertificateCheck
        
        # Response format: { success: true, data: { __dataId: "...", ... }, meta: {...} }
        $dataId = $response.data.__dataId
        if (-not $dataId) { $dataId = $response.Data.__dataId }
        if (-not $dataId) { $dataId = $response.__dataId }
        $genreIds[$genre.name] = $dataId
        Write-Host "   ✅ Created: $($genre.name) (ID: $dataId)" -ForegroundColor Green
    } catch {
        $statusCode = $_.Exception.Response.StatusCode.value__
        if ($statusCode -eq 409 -or $statusCode -eq 400) {
            Write-Host "   ⚠️  Already exists or validation error: $($genre.name)" -ForegroundColor Yellow
            # Try to get existing genre
            try {
                $getResponse = Invoke-RestMethod -Uri "$baseUrl/api/data/tst_genres?search=$($genre.name)" -Method GET -Headers $headers -SkipCertificateCheck
                if ($getResponse.items -and $getResponse.items.Count -gt 0) {
                    $item = $getResponse.items[0]
                    $dataId = $item.__dataId
                    if (-not $dataId) { $dataId = $item.dataId }
                    if (-not $dataId) { $dataId = $item.DataId }
                    if ($dataId) {
                        $genreIds[$genre.name] = $dataId
                        Write-Host "   ✅ Found existing: $($genre.name) (ID: $dataId)" -ForegroundColor Green
                    }
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

# Helper function to get random user ID
function Get-RandomUserId {
    if ($userIds.Count -eq 0) { return $null }
    return $userIds | Get-Random
}

# Helper function to get random group IDs (multiple)
function Get-RandomGroupIds {
    if ($groupIds.Count -eq 0) { return @() }
    $count = Get-Random -Minimum 1 -Maximum ([Math]::Min(4, $groupIds.Count + 1))
    # PowerShell Get-Random -Count returns array, but single item might not be
    # Convert to array explicitly
    $selected = @($groupIds | Get-Random -Count $count)
    return $selected
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
        author = Get-RandomUserId
        coAuthors = @()
        reviewerGroups = Get-RandomGroupIds
        editorialTeam = $null
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
        author = Get-RandomUserId
        coAuthors = @()
        reviewerGroups = Get-RandomGroupIds
        editorialTeam = $null
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
        author = Get-RandomUserId
        coAuthors = @()
        reviewerGroups = Get-RandomGroupIds
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
        author = Get-RandomUserId
        coAuthors = @()
        reviewerGroups = Get-RandomGroupIds
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
        author = Get-RandomUserId
        coAuthors = @()
        reviewerGroups = Get-RandomGroupIds
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
        author = Get-RandomUserId
        coAuthors = @()
        reviewerGroups = Get-RandomGroupIds
        editorialTeam = $null
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
        author = Get-RandomUserId
        coAuthors = @()
        reviewerGroups = Get-RandomGroupIds
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
        author = Get-RandomUserId
        coAuthors = @()
        reviewerGroups = Get-RandomGroupIds
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
        # Ensure author is set (required field)
        if (-not $book.author) {
            $book.author = Get-RandomUserId
        }
        
        # Ensure reviewerGroups is always an array (even if empty or single item)
        if ($null -ne $book.reviewerGroups) {
            if ($book.reviewerGroups -isnot [array]) {
                $book.reviewerGroups = @($book.reviewerGroups)
            }
        }
        
        # Remove null values for cleaner JSON
        $cleanBook = @{}
        foreach ($key in $book.Keys) {
            if ($null -ne $book[$key]) {
                # Skip empty arrays for reviewerGroups if no groups available
                if ($key -eq "reviewerGroups" -and $book[$key].Count -eq 0) {
                    continue
                }
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

