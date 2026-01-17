# Quick Test Script - Hızlı test için
# Bu script sadece temel kontrolleri yapar

param(
    [string]$BaseUrl = "http://localhost:5030"
)

Write-Host "Quick Documentation Provider Test" -ForegroundColor Cyan
Write-Host "================================" -ForegroundColor Cyan
Write-Host ""

# Test 1: Re-index
Write-Host "[1/3] Re-indexing..." -ForegroundColor Yellow
try {
    $response = Invoke-RestMethod -Uri "$BaseUrl/api/v1/docs/reindex" -Method POST
    Write-Host "✓ $($response.message)" -ForegroundColor Green
    Start-Sleep -Seconds 3
} catch {
    Write-Host "✗ Error: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# Test 2: Get all documents
Write-Host "[2/3] Getting all documents..." -ForegroundColor Yellow
try {
    $docs = Invoke-RestMethod -Uri "$BaseUrl/api/v1/docs" -Method GET
    $count = ($docs | Measure-Object).Count
    Write-Host "✓ Found $count documents" -ForegroundColor Green
    
    if ($count -gt 0) {
        Write-Host "  Sample:" -ForegroundColor Gray
        $docs | Select-Object -First 5 | ForEach-Object {
            Write-Host "    - $($_.Title) [$($_.Source)]" -ForegroundColor Gray
        }
    }
} catch {
    Write-Host "✗ Error: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# Test 3: Search
Write-Host "[3/3] Testing search..." -ForegroundColor Yellow
try {
    $query = "user"
    $encoded = [System.Web.HttpUtility]::UrlEncode($query)
    $results = Invoke-RestMethod -Uri "$BaseUrl/api/v1/docs/search?query=$encoded&limit=3" -Method GET
    $resultCount = ($results | Measure-Object).Count
    Write-Host "✓ Search for '$query' returned $resultCount results" -ForegroundColor Green
    
    if ($resultCount -gt 0) {
        $top = $results[0]
        Write-Host "  Top result: $($top.Title) (Score: $([math]::Round($top.RelevanceScore, 2)))" -ForegroundColor Gray
    }
} catch {
    Write-Host "✗ Error: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "✓ All tests passed!" -ForegroundColor Green
