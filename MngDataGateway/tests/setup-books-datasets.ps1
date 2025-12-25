# Books Dataset Setup Script
# Creates Book Categories category and 3 datasets: tst_publishers, tst_genres, tst_books

$baseUrl = "https://localhost:5010"

# Token dosyasının yolunu belirle
$tokenFile = "$env:TEMP\serkan_token.txt"

# Token'ı kontrol et
if (-not (Test-Path $tokenFile)) {
    Write-Host "❌ Token bulunamadı! Önce token almak için:" -ForegroundColor Red
    Write-Host "   cd C:\Serkan\iSIM\MonitraNG\MngKeeper\tests" -ForegroundColor Yellow
    Write-Host "   .\get-serkan-token.ps1" -ForegroundColor Yellow
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

Write-Host "`n🚀 Setting up Books datasets...`n" -ForegroundColor Cyan

# ============================================
# Step 1: Create Book Categories Category
# ============================================
Write-Host "📁 Step 1: Creating Book Categories category..." -ForegroundColor Yellow

$categoryData = @{
    CategoryName = "Book Categories"
    CategoryDescription = "Category for book-related datasets (publishers, genres, books)"
} | ConvertTo-Json -Depth 10

$categoryId = $null

try {
    $response = Invoke-RestMethod -Uri "$baseUrl/api/dataset-categories" -Method POST -Headers $headers -Body $categoryData -SkipCertificateCheck
    # Debug: Log full response
    Write-Host "Debug - Full Response: $($response | ConvertTo-Json -Depth 5)" -ForegroundColor Gray
    # Try different property names for dataId
    if ($response.DataId) {
        $categoryId = $response.DataId
    } elseif ($response.__dataId) {
        $categoryId = $response.__dataId
    } elseif ($response.dataId) {
        $categoryId = $response.dataId
    } else {
        Write-Host "⚠️  Response received but DataId not found. Trying to inspect response structure..." -ForegroundColor Yellow
        $categoryId = $null
    }
    if ($categoryId) {
        Write-Host "✅ Book Categories category created: $categoryId" -ForegroundColor Green
    }
} catch {
    $statusCode = $_.Exception.Response.StatusCode.value__
    if ($statusCode -eq 409 -or $statusCode -eq 400) {
        Write-Host "⚠️  Book Categories category already exists. Fetching existing category..." -ForegroundColor Yellow
        # Try to find existing category
        try {
            $categories = Invoke-RestMethod -Uri "$baseUrl/api/dataset-categories?pageNumber=1&pageSize=100" -Method GET -Headers $headers -SkipCertificateCheck
            Write-Host "Debug - Categories response: $($categories | ConvertTo-Json -Depth 3)" -ForegroundColor Gray
            $categoriesList = $null
            if ($categories.items) {
                $categoriesList = $categories.items
            } elseif ($categories.Data) {
                $categoriesList = $categories.Data
            } elseif ($categories.data) {
                $categoriesList = $categories.data
            } elseif ($categories -is [Array]) {
                $categoriesList = $categories
            }
            
            if ($categoriesList -and $categoriesList.Count -gt 0) {
                $existingCategory = $categoriesList | Where-Object { 
                    ($_.CategoryName -eq "Book Categories") -or ($_.categoryName -eq "Book Categories")
                }
                if ($existingCategory) {
                    $categoryId = if ($existingCategory.dataId) { $existingCategory.dataId }
                                  elseif ($existingCategory.DataId) { $existingCategory.DataId } 
                                  elseif ($existingCategory.__dataId) { $existingCategory.__dataId }
                    if ($categoryId) {
                        Write-Host "✅ Found existing category: $categoryId" -ForegroundColor Green
                    } else {
                        Write-Host "❌ Category found but DataId not found" -ForegroundColor Red
                        exit 1
                    }
                } else {
                    Write-Host "❌ Category 'Book Categories' not found in response" -ForegroundColor Red
                    exit 1
                }
            } else {
                Write-Host "❌ Failed to retrieve categories list or list is empty" -ForegroundColor Red
                exit 1
            }
        } catch {
            Write-Host "❌ Failed to fetch existing category: $($_.Exception.Message)" -ForegroundColor Red
            exit 1
        }
    } else {
        Write-Host "❌ Failed to create category: $($_.Exception.Message)" -ForegroundColor Red
        if ($_.ErrorDetails.Message) {
            Write-Host "Details: $($_.ErrorDetails.Message)" -ForegroundColor Gray
        }
        exit 1
    }
}

if (-not $categoryId) {
    Write-Host "❌ Category ID not found. Cannot continue." -ForegroundColor Red
    exit 1
}

Write-Host ""

# ============================================
# Step 2: Create tst_publishers Dataset
# ============================================
Write-Host "📚 Step 2: Creating tst_publishers dataset..." -ForegroundColor Yellow

$publishersSchema = @{
    Name = "tst_publishers"
    Description = "Book publishers dataset (test)"
    Category = $categoryId
    ForceSchema = $true
    Logging = "none"
    PublishMode = "none"
    Fields = @(
        @{
            fieldType = "text"
            name = "name"
            title = "Publisher Name"
            mandatory = $true
            unique = $true
        },
        @{
            fieldType = "text"
            name = "website"
            title = "Website"
            mandatory = $false
            unique = $false
        },
        @{
            fieldType = "text"
            name = "country"
            title = "Country"
            mandatory = $false
            unique = $false
        }
    )
    IndexList = @(
        @{
            name = "idx_name"
            fields = @{
                name = 1
            }
            unique = $true
        }
    )
} | ConvertTo-Json -Depth 10

try {
    $response = Invoke-RestMethod -Uri "$baseUrl/api/datasets" -Method POST -Headers $headers -Body $publishersSchema -SkipCertificateCheck
    Write-Host "✅ tst_publishers dataset created" -ForegroundColor Green
} catch {
    if ($_.Exception.Response.StatusCode -eq 409) {
        Write-Host "⚠️  tst_publishers dataset already exists" -ForegroundColor Yellow
    } else {
        Write-Host "❌ Failed to create tst_publishers: $($_.Exception.Message)" -ForegroundColor Red
        if ($_.ErrorDetails.Message) {
            Write-Host "Details: $($_.ErrorDetails.Message)" -ForegroundColor Gray
        }
    }
}

Write-Host ""

# ============================================
# Step 3: Create tst_genres Dataset
# ============================================
Write-Host "📚 Step 3: Creating tst_genres dataset..." -ForegroundColor Yellow

$genresSchema = @{
    Name = "tst_genres"
    Description = "Book genres dataset (test)"
    Category = $categoryId
    ForceSchema = $true
    Logging = "none"
    PublishMode = "basic"
    Fields = @(
        @{
            fieldType = "text"
            name = "name"
            title = "Genre Name"
            mandatory = $true
            unique = $true
        },
        @{
            fieldType = "text"
            name = "description"
            title = "Description"
            mandatory = $false
            unique = $false
        }
    )
    IndexList = @(
        @{
            name = "idx_name"
            fields = @{
                name = 1
            }
            unique = $true
        }
    )
} | ConvertTo-Json -Depth 10

try {
    $response = Invoke-RestMethod -Uri "$baseUrl/api/datasets" -Method POST -Headers $headers -Body $genresSchema -SkipCertificateCheck
    Write-Host "✅ tst_genres dataset created" -ForegroundColor Green
} catch {
    if ($_.Exception.Response.StatusCode -eq 409) {
        Write-Host "⚠️  tst_genres dataset already exists" -ForegroundColor Yellow
    } else {
        Write-Host "❌ Failed to create tst_genres: $($_.Exception.Message)" -ForegroundColor Red
        if ($_.ErrorDetails.Message) {
            Write-Host "Details: $($_.ErrorDetails.Message)" -ForegroundColor Gray
        }
    }
}

Write-Host ""

# ============================================
# Step 4: Create tst_books Dataset (Complex)
# ============================================
Write-Host "📚 Step 4: Creating tst_books dataset (complex with relations, queries, permissions)..." -ForegroundColor Yellow

$booksSchema = @{
    Name = "tst_books"
    Description = "Books dataset with relations and person fields (test)"
    Category = $categoryId
    ForceSchema = $true
    Logging = "self"
    PublishMode = "full"
    Fields = @(
        @{
            fieldType = "incremental"
            name = "isbn"
            title = "ISBN"
            mandatory = $true
            unique = $true
            incrementalOptions = @{
                format = "ISBN-{year}-{0:D6}"
                startValue = 1
                incrementStep = 1
            }
        },
        @{
            fieldType = "incremental"
            name = "bookCode"
            title = "Book Code"
            mandatory = $true
            unique = $true
            incrementalOptions = @{
                format = "BK-{yy}{month}-{0:D4}"
                startValue = 1
                incrementStep = 1
            }
        },
        @{
            fieldType = "text"
            name = "publisherCode"
            title = "Publisher Code"
            mandatory = $false
            unique = $false
        },
        @{
            fieldType = "incremental"
            name = "internalBookNumber"
            title = "Internal Book Number"
            mandatory = $true
            unique = $true
            incrementalOptions = @{
                format = "{publisherCode}-{year}-{0:D5}"
                startValue = 1
                incrementStep = 1
            }
        },
        @{
            fieldType = "incremental"
            name = "sequenceNumber"
            title = "Sequence Number"
            mandatory = $true
            unique = $true
            incrementalOptions = @{
                format = "{domain}-BOOK-{0:D6}"
                startValue = 1000
                incrementStep = 10
            }
        },
        @{
            fieldType = "text"
            name = "name"
            title = "Book Name"
            mandatory = $false
            unique = $false
        },
        @{
            fieldType = "text"
            name = "title"
            title = "Book Title"
            mandatory = $true
            unique = $false
        },
        @{
            fieldType = "text"
            name = "subtitle"
            title = "Subtitle"
            mandatory = $false
            unique = $false
        },
        @{
            fieldType = "relation"
            name = "publisher"
            title = "Publisher"
            mandatory = $true
            unique = $false
            isArray = $false
            relationDataset = "tst_publishers"
            relationField = "__dataId"
        },
        @{
            fieldType = "relation"
            name = "genres"
            title = "Genres"
            mandatory = $false
            unique = $false
            isArray = $true
            relationDataset = "tst_genres"
            relationField = "__dataId"
        },
        @{
            fieldType = "persons"
            name = "author"
            title = "Author"
            mandatory = $true
            unique = $false
            isArray = $false
        },
        @{
            fieldType = "persons"
            name = "coAuthors"
            title = "Co-Authors"
            mandatory = $false
            unique = $false
            isArray = $true
        },
        @{
            fieldType = "personGroups"
            name = "reviewerGroups"
            title = "Reviewer Groups"
            mandatory = $false
            unique = $false
            isArray = $true
        },
        @{
            fieldType = "personGroups"
            name = "editorialTeam"
            title = "Editorial Team"
            mandatory = $false
            unique = $false
            isArray = $false
        },
        @{
            fieldType = "number"
            name = "pageCount"
            title = "Page Count"
            mandatory = $false
            unique = $false
        },
        @{
            fieldType = "datetime"
            name = "publicationDate"
            title = "Publication Date"
            mandatory = $false
            unique = $false
        },
        @{
            fieldType = "text"
            name = "language"
            title = "Language"
            mandatory = $false
            unique = $false
        },
        @{
            fieldType = "number"
            name = "price"
            title = "Price"
            mandatory = $false
            unique = $false
        },
        @{
            fieldType = "object"
            name = "coverImage"
            title = "Cover Image"
            mandatory = $false
            unique = $false
            objectSchema = @{
                url = "text"
                alt = "text"
                width = "number"
                height = "number"
            }
        }
    )
    IndexList = @(
        @{
            name = "idx_isbn"
            fields = @{
                isbn = 1
            }
            unique = $true
        },
        @{
            name = "idx_bookCode"
            fields = @{
                bookCode = 1
            }
            unique = $true
        },
        @{
            name = "idx_internalBookNumber"
            fields = @{
                internalBookNumber = 1
            }
            unique = $true
        },
        @{
            name = "idx_sequenceNumber"
            fields = @{
                sequenceNumber = 1
            }
            unique = $true
        },
        @{
            name = "idx_name"
            fields = @{
                name = 1
            }
            unique = $true
        },
        @{
            name = "idx_title"
            fields = @{
                title = 1
            }
            unique = $false
        },
        @{
            name = "idx_title_bookCode"
            fields = @{
                title = 1
                bookCode = 1
            }
            unique = $false
        },
        @{
            name = "idx_publisher"
            fields = @{
                publisher = 1
            }
            unique = $false
        },
        @{
            name = "idx_author"
            fields = @{
                author = 1
            }
            unique = $false
        },
        @{
            name = "idx_publicationDate"
            fields = @{
                publicationDate = -1
            }
            unique = $false
        }
    )
    Queries = @(
        @{
            name = "books_by_publication_date_range"
            description = "Get books published between two dates"
            parameters = @("startDate", "endDate")
            pipeline = @(
                @{
                    "`$match" = @{
                        publicationDate = @{
                            "`$gte" = ":startDate"
                            "`$lte" = ":endDate"
                        }
                    }
                },
                @{
                    "`$sort" = @{
                        publicationDate = -1
                        title = 1
                    }
                }
            )
        }
    )
    Permissions = @{
        read = @{
            groups = @("managers")
            users = @()
        }
        write = @{
            groups = @("managers")
            users = @()
        }
        create = @{
            groups = @("managers")
            users = @()
        }
        update = @{
            groups = @("managers")
            users = @()
        }
        delete = @{
            groups = @("managers")
            users = @()
        }
    }
} | ConvertTo-Json -Depth 20

try {
    $response = Invoke-RestMethod -Uri "$baseUrl/api/datasets" -Method POST -Headers $headers -Body $booksSchema -SkipCertificateCheck
    Write-Host "✅ tst_books dataset created" -ForegroundColor Green
    if ($response.Fields) {
        Write-Host "   - Fields: $($response.Fields.Count)" -ForegroundColor Gray
    }
    if ($response.IndexList) {
        Write-Host "   - Indexes: $($response.IndexList.Count)" -ForegroundColor Gray
    }
    if ($response.Queries) {
        Write-Host "   - Queries: $($response.Queries.Count)" -ForegroundColor Gray
    }
} catch {
    if ($_.Exception.Response.StatusCode -eq 409) {
        Write-Host "⚠️  tst_books dataset already exists" -ForegroundColor Yellow
    } else {
        Write-Host "❌ Failed to create tst_books: $($_.Exception.Message)" -ForegroundColor Red
        if ($_.ErrorDetails.Message) {
            Write-Host "Details: $($_.ErrorDetails.Message)" -ForegroundColor Gray
            # Try to parse and show more details
            try {
                $errorJson = $_.ErrorDetails.Message | ConvertFrom-Json
                if ($errorJson.error) {
                    Write-Host "Error: $($errorJson.error.message)" -ForegroundColor Red
                }
            } catch {
                # Ignore JSON parse errors
            }
        }
    }
}

Write-Host "`n✅ Books datasets setup completed!`n" -ForegroundColor Green

