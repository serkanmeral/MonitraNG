# Documentation Provider Test Script
# Bu script DocumentationProvider'ı test eder

param(
    [string]$BaseUrl = "http://localhost:5030",
    [string]$Token = "",
    [switch]$SkipAuth = $false
)

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Documentation Provider Test Script" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Test 1: Health Check (if available)
Write-Host "[Test 1] Checking service availability..." -ForegroundColor Yellow
try {
    $healthResponse = Invoke-WebRequest -Uri "$BaseUrl/health" -Method GET -ErrorAction SilentlyContinue
    if ($healthResponse.StatusCode -eq 200) {
        Write-Host "✓ Service is running" -ForegroundColor Green
    }
} catch {
    Write-Host "⚠ Health endpoint not available or service not running" -ForegroundColor Yellow
    Write-Host "  Make sure MngLLM service is running on $BaseUrl" -ForegroundColor Yellow
}

Write-Host ""

# Test 2: Get All Documents
Write-Host "[Test 2] Getting all indexed documents..." -ForegroundColor Yellow
try {
    $headers = @{}
    if (-not $SkipAuth -and $Token) {
        $headers["Authorization"] = "Bearer $Token"
    }

    $response = Invoke-RestMethod -Uri "$BaseUrl/api/v1/docs" -Method GET -Headers $headers
    $docCount = ($response | Measure-Object).Count
    Write-Host "✓ Found $docCount indexed documents" -ForegroundColor Green
    
    if ($docCount -gt 0) {
        Write-Host "  Sample documents:" -ForegroundColor Gray
        $response | Select-Object -First 3 | ForEach-Object {
            Write-Host "    - $($_.Title) ($($_.Source))" -ForegroundColor Gray
        }
    } else {
        Write-Host "  ⚠ No documents found. Run reindex first!" -ForegroundColor Yellow
    }
} catch {
    Write-Host "✗ Error: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.Exception.Response.StatusCode -eq 401) {
        Write-Host "  Authentication required. Provide -Token parameter or use -SkipAuth if auth is disabled" -ForegroundColor Yellow
    }
}

Write-Host ""

# Test 3: Re-index
Write-Host "[Test 3] Re-indexing documentation..." -ForegroundColor Yellow
try {
    $headers = @{}
    if (-not $SkipAuth -and $Token) {
        $headers["Authorization"] = "Bearer $Token"
    }

    $response = Invoke-RestMethod -Uri "$BaseUrl/api/v1/docs/reindex" -Method POST -Headers $headers
    Write-Host "✓ Re-indexing completed: $($response.message)" -ForegroundColor Green
    Start-Sleep -Seconds 2  # Wait for indexing to complete
} catch {
    Write-Host "✗ Error: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""

# Test 4: Search Tests
$searchQueries = @(
    "user management",
    "dataset",
    "authentication",
    "api",
    "architecture"
)

Write-Host "[Test 4] Testing search functionality..." -ForegroundColor Yellow
foreach ($query in $searchQueries) {
    try {
        $headers = @{}
        if (-not $SkipAuth -and $Token) {
            $headers["Authorization"] = "Bearer $Token"
        }

        $encodedQuery = [System.Web.HttpUtility]::UrlEncode($query)
        $response = Invoke-RestMethod -Uri "$BaseUrl/api/v1/docs/search?query=$encodedQuery&limit=3" -Method GET -Headers $headers
        
        $resultCount = ($response | Measure-Object).Count
        Write-Host "  Query: '$query' -> Found $resultCount results" -ForegroundColor Cyan
        
        if ($resultCount -gt 0) {
            $topResult = $response[0]
            Write-Host "    Top result: $($topResult.Title) (Score: $([math]::Round($topResult.RelevanceScore, 3)))" -ForegroundColor Gray
        }
    } catch {
        Write-Host "  ✗ Query '$query' failed: $($_.Exception.Message)" -ForegroundColor Red
    }
}

Write-Host ""

# Test 5: Get Document Content
Write-Host "[Test 5] Testing document content retrieval..." -ForegroundColor Yellow
try {
    $headers = @{}
    if (-not $SkipAuth -and $Token) {
        $headers["Authorization"] = "Bearer $Token"
    }

    # First get all documents to find a valid ID
    $allDocs = Invoke-RestMethod -Uri "$BaseUrl/api/v1/docs" -Method GET -Headers $headers -SkipCertificateCheck
    
    if ($allDocs.Count -gt 0) {
        $testDocId = $allDocs[0].Id
        Write-Host "  Testing with document ID: $testDocId" -ForegroundColor Gray
        
        $contentResponse = Invoke-RestMethod -Uri "$BaseUrl/api/v1/docs/$([System.Web.HttpUtility]::UrlEncode($testDocId))" -Method GET -Headers $headers
        $contentLength = $contentResponse.content.Length
        Write-Host "✓ Retrieved document content ($contentLength characters)" -ForegroundColor Green
        Write-Host "  Preview: $($contentResponse.content.Substring(0, [Math]::Min(100, $contentLength)))..." -ForegroundColor Gray
    } else {
        Write-Host "  ⚠ No documents available to test content retrieval" -ForegroundColor Yellow
    }
} catch {
    Write-Host "✗ Error: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Test completed!" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
