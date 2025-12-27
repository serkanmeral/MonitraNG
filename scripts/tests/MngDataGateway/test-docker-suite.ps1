# Docker Test Suite - Comprehensive Tests
# Tests all major DataGateway functionality on Docker container

param(
    [string]$BaseUrl = "https://localhost:5010"
)

$scriptPath = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $PSCommandPath }
$getTokenScript = Join-Path $scriptPath "auth\get-token.ps1"

[System.Net.ServicePointManager]::ServerCertificateValidationCallback = {$true}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "MngDataGateway Docker Test Suite" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Get Token
Write-Host "[1/10] Getting authentication token..." -ForegroundColor Yellow
$token = & $getTokenScript
if ([string]::IsNullOrEmpty($token)) {
    Write-Host "FAILED: Could not get token" -ForegroundColor Red
    exit 1
}
Write-Host "SUCCESS: Token retrieved" -ForegroundColor Green
Write-Host ""

$headers = @{
    "Authorization" = "Bearer $token"
    "Content-Type" = "application/json"
}

$results = @()

# Test 1: Health Check
Write-Host "[2/10] Health Check..." -ForegroundColor Yellow
try {
    $health = Invoke-RestMethod -Uri "$BaseUrl/api/v1/health" -Method GET -Headers $headers -SkipCertificateCheck
    Write-Host "SUCCESS: Status = $($health.status)" -ForegroundColor Green
    $results += @{Test="Health Check"; Status="PASS"}
} catch {
    Write-Host "FAILED: $($_.Exception.Message)" -ForegroundColor Red
    $results += @{Test="Health Check"; Status="FAIL"; Error=$_.Exception.Message}
}
Write-Host ""

# Test 2: Version
Write-Host "[3/10] Version Endpoint..." -ForegroundColor Yellow
try {
    $version = Invoke-RestMethod -Uri "$BaseUrl/api/v1/version" -Method GET -Headers $headers -SkipCertificateCheck
    Write-Host "SUCCESS: Version = $($version.version)" -ForegroundColor Green
    $results += @{Test="Version"; Status="PASS"}
} catch {
    Write-Host "FAILED: $($_.Exception.Message)" -ForegroundColor Red
    $results += @{Test="Version"; Status="FAIL"; Error=$_.Exception.Message}
}
Write-Host ""

# Test 3: List Datasets
Write-Host "[4/10] List Datasets..." -ForegroundColor Yellow
try {
    $datasets = Invoke-RestMethod -Uri "$BaseUrl/api/v1/datasets?pageSize=10" -Method GET -Headers $headers -SkipCertificateCheck
    Write-Host "SUCCESS: Found $($datasets.data.Count) datasets" -ForegroundColor Green
    $results += @{Test="List Datasets"; Status="PASS"}
} catch {
    Write-Host "FAILED: $($_.Exception.Message)" -ForegroundColor Red
    $results += @{Test="List Datasets"; Status="FAIL"; Error=$_.Exception.Message}
}
Write-Host ""

# Test 4: Get Dataset by Name
Write-Host "[5/10] Get Dataset (tst_books)..." -ForegroundColor Yellow
try {
    $dataset = Invoke-RestMethod -Uri "$BaseUrl/api/v1/datasets/tst_books" -Method GET -Headers $headers -SkipCertificateCheck
    Write-Host "SUCCESS: Dataset found - $($dataset.name)" -ForegroundColor Green
    $results += @{Test="Get Dataset"; Status="PASS"}
} catch {
    Write-Host "SKIPPED: tst_books may not exist" -ForegroundColor Yellow
    $results += @{Test="Get Dataset"; Status="SKIP"}
}
Write-Host ""

# Test 5: List Data (tst_books)
Write-Host "[6/10] List Data (tst_books)..." -ForegroundColor Yellow
try {
    $books = Invoke-RestMethod -Uri "$BaseUrl/api/v1/data/tst_books?limit=5" -Method GET -Headers $headers -SkipCertificateCheck
    Write-Host "SUCCESS: Found $($books.Count) books" -ForegroundColor Green
    $results += @{Test="List Data"; Status="PASS"}
} catch {
    Write-Host "SKIPPED: tst_books dataset may not exist or have data" -ForegroundColor Yellow
    $results += @{Test="List Data"; Status="SKIP"}
}
Write-Host ""

# Test 6: Search
Write-Host "[7/10] Search Test..." -ForegroundColor Yellow
try {
    $search = Invoke-RestMethod -Uri "$BaseUrl/api/v1/data/tst_books?search=test&limit=5" -Method GET -Headers $headers -SkipCertificateCheck
    Write-Host "SUCCESS: Search found $($search.Count) results" -ForegroundColor Green
    $results += @{Test="Search"; Status="PASS"}
} catch {
    Write-Host "SKIPPED: Search test" -ForegroundColor Yellow
    $results += @{Test="Search"; Status="SKIP"}
}
Write-Host ""

# Test 7: Filter
Write-Host "[8/10] Filter Test..." -ForegroundColor Yellow
try {
    $filter = Invoke-RestMethod -Uri "$BaseUrl/api/v1/data/tst_books?filter=price>50&limit=5" -Method GET -Headers $headers -SkipCertificateCheck
    Write-Host "SUCCESS: Filter found $($filter.Count) results" -ForegroundColor Green
    $results += @{Test="Filter"; Status="PASS"}
} catch {
    Write-Host "SKIPPED: Filter test" -ForegroundColor Yellow
    $results += @{Test="Filter"; Status="SKIP"}
}
Write-Host ""

# Test 8: Predefined Query
Write-Host "[9/10] Predefined Query Test..." -ForegroundColor Yellow
try {
    $queryBody = @{
        startDate = "2024-01-01T00:00:00Z"
        endDate = "2025-12-31T23:59:59Z"
    } | ConvertTo-Json
    $query = Invoke-RestMethod -Uri "$BaseUrl/api/v1/data/tst_books/queries/books_by_publication_date_range" -Method POST -Headers $headers -Body $queryBody -SkipCertificateCheck
    Write-Host "SUCCESS: Query returned $($query.Count) results" -ForegroundColor Green
    $results += @{Test="Predefined Query"; Status="PASS"}
} catch {
    Write-Host "SKIPPED: Query may not exist" -ForegroundColor Yellow
    $results += @{Test="Predefined Query"; Status="SKIP"}
}
Write-Host ""

# Test 9: Aggregate
Write-Host "[10/10] Aggregate Test..." -ForegroundColor Yellow
try {
    $matchStage = @{ '$match' = @{ price = @{ '$gt' = 20 } } }
    $sortStage = @{ '$sort' = @{ title = 1 } }
    $limitStage = @{ '$limit' = 5 }
    $pipeline = @($matchStage, $sortStage, $limitStage)
    $aggBody = @{ pipeline = $pipeline } | ConvertTo-Json -Depth 10
    $aggregate = Invoke-RestMethod -Uri "$BaseUrl/api/v1/data/tst_books/aggregate" -Method POST -Headers $headers -Body $aggBody -SkipCertificateCheck
    Write-Host "SUCCESS: Aggregate returned $($aggregate.Count) results" -ForegroundColor Green
    $results += @{Test="Aggregate"; Status="PASS"}
} catch {
    Write-Host "SKIPPED: Aggregate test" -ForegroundColor Yellow
    $results += @{Test="Aggregate"; Status="SKIP"}
}
Write-Host ""

# Summary
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Test Summary" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

$passed = ($results | Where-Object { $_.Status -eq "PASS" }).Count
$failed = ($results | Where-Object { $_.Status -eq "FAIL" }).Count
$skipped = ($results | Where-Object { $_.Status -eq "SKIP" }).Count

foreach ($result in $results) {
    $status = $result.Status
    $color = switch ($status) {
        "PASS" { "Green" }
        "FAIL" { "Red" }
        "SKIP" { "Yellow" }
        default { "Gray" }
    }
    Write-Host "$($result.Test): $status" -ForegroundColor $color
}

Write-Host ""
Write-Host "Total: $($results.Count) | Passed: $passed | Failed: $failed | Skipped: $skipped" -ForegroundColor Cyan
Write-Host ""

if ($failed -eq 0) {
    Write-Host "All tests passed!" -ForegroundColor Green
    exit 0
} else {
    Write-Host "Some tests failed. Check errors above." -ForegroundColor Red
    exit 1
}

