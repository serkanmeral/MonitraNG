# Person & PersonGroups Expansion Test Script
# Tests GET operations with person/personGroups field expansion

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Person & PersonGroups Expansion Test" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Base URL
$baseUrl = "https://localhost:5010"

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

Write-Host ""

# Headers
$headers = @{
    "Authorization" = "Bearer $token"
    "Content-Type" = "application/json"
}

# Skip certificate validation
[System.Net.ServicePointManager]::ServerCertificateValidationCallback = {$true}

# Test counter
$testCount = 0
$passCount = 0
$failCount = 0

# Test function
function Test-PersonExpansion {
    param(
        [string]$Name,
        [string]$Url,
        [hashtable]$Headers,
        [bool]$ShouldExpand = $false
    )
    
    $script:testCount++
    Write-Host "🧪 TEST $testCount : $Name" -ForegroundColor Yellow
    Write-Host "   URL: $Url" -ForegroundColor Gray
    Write-Host "   Expand: $ShouldExpand" -ForegroundColor Gray
    
    try {
        $response = Invoke-RestMethod -Uri $Url -Headers $Headers -SkipCertificateCheck -ErrorAction Stop
        
        # Check response structure (can be array or object with data property)
        $dataArray = $null
        if ($response -is [Array]) {
            $dataArray = $response
        } elseif ($response.data) {
            $dataArray = $response.data
        } elseif ($response -is [PSCustomObject] -or $response -is [hashtable]) {
            # Single item response
            $dataArray = @($response)
        }
        
        if ($dataArray -and $dataArray.Count -gt 0) {
            $firstItem = $dataArray[0]
            Write-Host "   ✅ Başarılı! (Count: $($response.data.Count))" -ForegroundColor Green
            
            # Check person fields
            Write-Host "   📋 Checking person/personGroups fields..." -ForegroundColor Cyan
            
            # Check author field (single person)
            if ($firstItem.author) {
                if ($ShouldExpand) {
                    if ($firstItem.author -is [PSCustomObject] -or $firstItem.author -is [hashtable]) {
                        Write-Host "   ✅ author: Expanded (object)" -ForegroundColor Green
                        Write-Host "      - __dataId: $($firstItem.author.__dataId)" -ForegroundColor Gray
                        Write-Host "      - username: $($firstItem.author.username)" -ForegroundColor Gray
                        Write-Host "      - email: $($firstItem.author.email)" -ForegroundColor Gray
                    } else {
                        Write-Host "   ❌ author: Should be object but is: $($firstItem.author.GetType().Name)" -ForegroundColor Red
                        $script:failCount++
                    }
                } else {
                    if ($firstItem.author -is [string]) {
                        Write-Host "   ✅ author: Not expanded (ID: $($firstItem.author))" -ForegroundColor Green
                    } else {
                        Write-Host "   ❌ author: Should be string but is: $($firstItem.author.GetType().Name)" -ForegroundColor Red
                        $script:failCount++
                    }
                }
            } else {
                Write-Host "   ⚠️  author: Not found or null" -ForegroundColor Yellow
            }
            
            # Check coAuthors field (array persons)
            if ($firstItem.coAuthors) {
                if ($ShouldExpand) {
                    if ($firstItem.coAuthors -is [Array]) {
                        if ($firstItem.coAuthors.Count -gt 0) {
                            if ($firstItem.coAuthors[0] -is [PSCustomObject] -or $firstItem.coAuthors[0] -is [hashtable]) {
                                Write-Host "   ✅ coAuthors: Expanded (array of objects, Count: $($firstItem.coAuthors.Count))" -ForegroundColor Green
                                Write-Host "      - First item __dataId: $($firstItem.coAuthors[0].__dataId)" -ForegroundColor Gray
                            } else {
                                Write-Host "   ❌ coAuthors: Should be array of objects but first item is: $($firstItem.coAuthors[0].GetType().Name)" -ForegroundColor Red
                                $script:failCount++
                            }
                        } else {
                            Write-Host "   ✅ coAuthors: Empty array" -ForegroundColor Green
                        }
                    } else {
                        Write-Host "   ❌ coAuthors: Should be array but is: $($firstItem.coAuthors.GetType().Name)" -ForegroundColor Red
                        $script:failCount++
                    }
                } else {
                    if ($firstItem.coAuthors -is [Array]) {
                        if ($firstItem.coAuthors.Count -gt 0) {
                            if ($firstItem.coAuthors[0] -is [string]) {
                                Write-Host "   ✅ coAuthors: Not expanded (array of IDs, Count: $($firstItem.coAuthors.Count))" -ForegroundColor Green
                            } else {
                                Write-Host "   ❌ coAuthors: Should be array of strings but first item is: $($firstItem.coAuthors[0].GetType().Name)" -ForegroundColor Red
                                $script:failCount++
                            }
                        } else {
                            Write-Host "   ✅ coAuthors: Empty array" -ForegroundColor Green
                        }
                    } else {
                        Write-Host "   ❌ coAuthors: Should be array but is: $($firstItem.coAuthors.GetType().Name)" -ForegroundColor Red
                        $script:failCount++
                    }
                }
            } else {
                Write-Host "   ⚠️  coAuthors: Not found or null" -ForegroundColor Yellow
            }
            
            # Check reviewerGroups field (array personGroups)
            if ($firstItem.reviewerGroups) {
                if ($ShouldExpand) {
                    if ($firstItem.reviewerGroups -is [Array]) {
                        if ($firstItem.reviewerGroups.Count -gt 0) {
                            if ($firstItem.reviewerGroups[0] -is [PSCustomObject] -or $firstItem.reviewerGroups[0] -is [hashtable]) {
                                Write-Host "   ✅ reviewerGroups: Expanded (array of objects, Count: $($firstItem.reviewerGroups.Count))" -ForegroundColor Green
                                Write-Host "      - First item __dataId: $($firstItem.reviewerGroups[0].__dataId)" -ForegroundColor Gray
                                Write-Host "      - First item name: $($firstItem.reviewerGroups[0].name)" -ForegroundColor Gray
                            } else {
                                Write-Host "   ❌ reviewerGroups: Should be array of objects but first item is: $($firstItem.reviewerGroups[0].GetType().Name)" -ForegroundColor Red
                                $script:failCount++
                            }
                        } else {
                            Write-Host "   ✅ reviewerGroups: Empty array" -ForegroundColor Green
                        }
                    } else {
                        Write-Host "   ❌ reviewerGroups: Should be array but is: $($firstItem.reviewerGroups.GetType().Name)" -ForegroundColor Red
                        $script:failCount++
                    }
                } else {
                    if ($firstItem.reviewerGroups -is [Array]) {
                        if ($firstItem.reviewerGroups.Count -gt 0) {
                            if ($firstItem.reviewerGroups[0] -is [string]) {
                                Write-Host "   ✅ reviewerGroups: Not expanded (array of IDs, Count: $($firstItem.reviewerGroups.Count))" -ForegroundColor Green
                            } else {
                                Write-Host "   ❌ reviewerGroups: Should be array of strings but first item is: $($firstItem.reviewerGroups[0].GetType().Name)" -ForegroundColor Red
                                $script:failCount++
                            }
                        } else {
                            Write-Host "   ✅ reviewerGroups: Empty array" -ForegroundColor Green
                        }
                    } else {
                        Write-Host "   ❌ reviewerGroups: Should be array but is: $($firstItem.reviewerGroups.GetType().Name)" -ForegroundColor Red
                        $script:failCount++
                    }
                }
            } else {
                Write-Host "   ⚠️  reviewerGroups: Not found or null" -ForegroundColor Yellow
            }
            
            # Check editorialTeam field (single personGroup)
            if ($firstItem.editorialTeam) {
                if ($ShouldExpand) {
                    if ($firstItem.editorialTeam -is [PSCustomObject] -or $firstItem.editorialTeam -is [hashtable]) {
                        Write-Host "   ✅ editorialTeam: Expanded (object)" -ForegroundColor Green
                        Write-Host "      - __dataId: $($firstItem.editorialTeam.__dataId)" -ForegroundColor Gray
                        Write-Host "      - name: $($firstItem.editorialTeam.name)" -ForegroundColor Gray
                    } else {
                        Write-Host "   ❌ editorialTeam: Should be object but is: $($firstItem.editorialTeam.GetType().Name)" -ForegroundColor Red
                        $script:failCount++
                    }
                } else {
                    if ($firstItem.editorialTeam -is [string]) {
                        Write-Host "   ✅ editorialTeam: Not expanded (ID: $($firstItem.editorialTeam))" -ForegroundColor Green
                    } else {
                        Write-Host "   ❌ editorialTeam: Should be string but is: $($firstItem.editorialTeam.GetType().Name)" -ForegroundColor Red
                        $script:failCount++
                    }
                }
            } else {
                Write-Host "   ⚠️  editorialTeam: Not found or null" -ForegroundColor Yellow
            }
            
            $script:passCount++
        } else {
            Write-Host "   ⚠️  No data found in response" -ForegroundColor Yellow
            $script:failCount++
        }
        
        Write-Host ""
        return @{ Success = $true; Data = $response }
    }
    catch {
        Write-Host "   ❌ Hata: $($_.Exception.Message)" -ForegroundColor Red
        if ($_.ErrorDetails.Message) {
            Write-Host "   📦 Details: $($_.ErrorDetails.Message)" -ForegroundColor Gray
        }
        $script:failCount++
        Write-Host ""
        return @{ Success = $false; Error = $_.Exception.Message }
    }
}

Write-Host "🚀 Testler başlıyor...`n" -ForegroundColor Cyan

# ============================================
# TEST 1: GET without expansion (expand=false)
# ============================================
Write-Host "═══ TEST GROUP 1: Without Expansion (expand=false) ═══" -ForegroundColor Magenta
Write-Host ""

Test-PersonExpansion `
    -Name "GET /api/data/tst_books?expand=false&limit=1" `
    -Url "$baseUrl/api/data/tst_books?expand=false&limit=1" `
    -Headers $headers `
    -ShouldExpand $false

Test-PersonExpansion `
    -Name "GET /api/data/tst_books?limit=1 (default expand=true)" `
    -Url "$baseUrl/api/data/tst_books?limit=1" `
    -Headers $headers `
    -ShouldExpand $true

# ============================================
# TEST 2: GET with expansion (expand=true)
# ============================================
Write-Host "═══ TEST GROUP 2: With Expansion (expand=true) ═══" -ForegroundColor Magenta
Write-Host ""

Test-PersonExpansion `
    -Name "GET /api/data/tst_books?expand=true&limit=1" `
    -Url "$baseUrl/api/data/tst_books?expand=true&limit=1" `
    -Headers $headers `
    -ShouldExpand $true

# ============================================
# TEST 3: GET by ID without expansion
# ============================================
Write-Host "═══ TEST GROUP 3: GET by ID (without expansion) ═══" -ForegroundColor Magenta
Write-Host ""

# First, get a book ID
try {
    $listResponse = Invoke-RestMethod -Uri "$baseUrl/api/data/tst_books?limit=1" -Headers $headers -SkipCertificateCheck
    if ($listResponse.data -and $listResponse.data.Count -gt 0) {
        $bookId = $listResponse.data[0].__dataId
        Write-Host "📖 Using book ID: $bookId" -ForegroundColor Cyan
        Write-Host ""
        
        Test-PersonExpansion `
            -Name "GET /api/data/tst_books/$bookId?expand=false" `
            -Url "$baseUrl/api/data/tst_books/$bookId?expand=false" `
            -Headers $headers `
            -ShouldExpand $false
    } else {
        Write-Host "⚠️  No books found, skipping ID test" -ForegroundColor Yellow
        Write-Host ""
    }
} catch {
    Write-Host "⚠️  Could not get book ID, skipping ID test: $($_.Exception.Message)" -ForegroundColor Yellow
    Write-Host ""
}

# ============================================
# TEST 4: GET by ID with expansion
# ============================================
Write-Host "═══ TEST GROUP 4: GET by ID (with expansion) ═══" -ForegroundColor Magenta
Write-Host ""

# Use the same book ID
if ($bookId) {
    Test-PersonExpansion `
        -Name "GET /api/data/tst_books/$bookId?expand=true" `
        -Url "$baseUrl/api/data/tst_books/$bookId?expand=true" `
        -Headers $headers `
        -ShouldExpand $true
} else {
    Write-Host "⚠️  No book ID available, skipping ID expansion test" -ForegroundColor Yellow
    Write-Host ""
}

# ============================================
# Summary
# ============================================
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Test Summary" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Total Tests: $testCount" -ForegroundColor White
Write-Host "Passed: $passCount" -ForegroundColor Green
Write-Host "Failed: $failCount" -ForegroundColor $(if ($failCount -gt 0) { "Red" } else { "Green" })
Write-Host ""

if ($failCount -eq 0) {
    Write-Host "✅ All tests passed!" -ForegroundColor Green
} else {
    Write-Host "❌ Some tests failed. Please review the output above." -ForegroundColor Red
}

Write-Host ""

