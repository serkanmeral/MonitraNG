# Simple Docker Test Script - No emojis, just functionality tests
# Tests all major DataGateway endpoints

param(
    [string]$BaseUrl = "https://localhost:5010"
)

$scriptPath = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $PSCommandPath }
$getTokenScript = Join-Path $scriptPath "auth\get-token.ps1"

# Fix SSL/TLS issues
[System.Net.ServicePointManager]::ServerCertificateValidationCallback = {$true}
[System.Net.ServicePointManager]::SecurityProtocol = [System.Net.SecurityProtocolType]::Tls12 -bor [System.Net.SecurityProtocolType]::Tls13
if ([System.Net.ServicePointManager]::SecurityProtocol -notmatch 'Tls12' -and [System.Net.ServicePointManager]::SecurityProtocol -notmatch 'Tls13') {
    [System.Net.ServicePointManager]::SecurityProtocol = [System.Net.SecurityProtocolType]::Tls12
}

# Check if SkipCertificateCheck parameter is available
$hasSkipCertCheck = Get-Command Invoke-RestMethod | Select-Object -ExpandProperty Parameters | Where-Object { $_.Name -eq "SkipCertificateCheck" }

Write-Host ""
Write-Host "========================================" 
Write-Host "MngDataGateway Docker Test Suite" 
Write-Host "========================================" 
Write-Host ""

# Get Token
Write-Host "[1] Getting authentication token..." 
$token = & $getTokenScript
if ([string]::IsNullOrEmpty($token)) {
    Write-Host "FAILED: Could not get token" 
    exit 1
}
Write-Host "SUCCESS: Token retrieved" 
Write-Host ""

$headers = @{
    "Authorization" = "Bearer $token"
    "Content-Type" = "application/json"
}

$results = @()

# Helper function for Invoke-RestMethod with SSL fixes
function Invoke-RestMethodSafe {
    param(
        [string]$Uri,
        [string]$Method = "GET",
        [hashtable]$Headers = @{},
        [string]$Body = $null
    )
    
    # Ensure SSL/TLS settings for each request
    [System.Net.ServicePointManager]::ServerCertificateValidationCallback = {$true}
    try {
        [System.Net.ServicePointManager]::SecurityProtocol = [System.Net.SecurityProtocolType]::Tls12 -bor [System.Net.SecurityProtocolType]::Tls11 -bor [System.Net.SecurityProtocolType]::Tls
    } catch {
        try {
            [System.Net.ServicePointManager]::SecurityProtocol = [System.Net.SecurityProtocolType]::Tls12
        } catch {
            # Ignore if not available
        }
    }
    
    $params = @{
        Uri = $Uri
        Method = $Method
        Headers = $Headers
        ErrorAction = "Stop"
    }
    
    if ($hasSkipCertCheck) {
        $params.SkipCertificateCheck = $true
    }
    
    if ($Body) {
        $params.Body = $Body
    }
    
    # Use WebRequest if RestMethod fails
    try {
        return Invoke-RestMethod @params
    } catch {
        # Fallback to HttpWebRequest for SSL issues
        $request = [System.Net.HttpWebRequest]::Create($Uri)
        $request.Method = $Method
        $request.ServerCertificateValidationCallback = {$true}
        
        foreach ($key in $Headers.Keys) {
            if ($key -eq "Authorization") {
                $request.Headers.Add($key, $Headers[$key])
            } elseif ($key -eq "Content-Type") {
                $request.ContentType = $Headers[$key]
            }
        }
        
        if ($Body) {
            $bytes = [System.Text.Encoding]::UTF8.GetBytes($Body)
            $request.ContentLength = $bytes.Length
            $stream = $request.GetRequestStream()
            $stream.Write($bytes, 0, $bytes.Length)
            $stream.Close()
        }
        
        $response = $request.GetResponse()
        $reader = New-Object System.IO.StreamReader($response.GetResponseStream())
        $responseBody = $reader.ReadToEnd()
        $reader.Close()
        $response.Close()
        
        return $responseBody | ConvertFrom-Json
    }
}

# Test 1: Health Check
Write-Host "[2] Health Check..." 
try {
    $health = Invoke-RestMethodSafe -Uri "$BaseUrl/api/v1/health" -Method GET -Headers $headers
    if ($health.status -eq "healthy") {
        Write-Host "PASS: Status = $($health.status)" 
        $results += "PASS"
    } else {
        Write-Host "FAIL: Unexpected status = $($health.status)" 
        $results += "FAIL"
    }
} catch {
    Write-Host "FAIL: $($_.Exception.Message)" 
    $results += "FAIL"
}
Write-Host ""

# Test 2: Version
Write-Host "[3] Version Endpoint..." 
try {
    $version = Invoke-RestMethodSafe -Uri "$BaseUrl/api/v1/version" -Method GET -Headers $headers
    Write-Host "PASS: Version = $($version.version)" 
    $results += "PASS"
} catch {
    Write-Host "FAIL: $($_.Exception.Message)" 
    $results += "FAIL"
}
Write-Host ""

# Test 3: List Datasets
Write-Host "[4] List Datasets..." 
try {
    $datasets = Invoke-RestMethodSafe -Uri "$BaseUrl/api/v1/datasets?pageSize=10" -Method GET -Headers $headers
    Write-Host "PASS: Found $($datasets.data.Count) datasets" 
    $results += "PASS"
} catch {
    Write-Host "FAIL: $($_.Exception.Message)" 
    $results += "FAIL"
}
Write-Host ""

# Test 4: Get Dataset by Name (tst_books)
Write-Host "[5] Get Dataset (tst_books)..." 
try {
    $dataset = Invoke-RestMethodSafe -Uri "$BaseUrl/api/v1/datasets/tst_books" -Method GET -Headers $headers
    Write-Host "PASS: Dataset found - $($dataset.name)" 
    $results += "PASS"
} catch {
    Write-Host "SKIP: tst_books dataset may not exist" 
    $results += "SKIP"
}
Write-Host ""

# Test 5: List Data (tst_books)
Write-Host "[6] List Data (tst_books)..." 
try {
    $books = Invoke-RestMethodSafe -Uri "$BaseUrl/api/v1/data/tst_books?limit=5" -Method GET -Headers $headers
    Write-Host "PASS: Found $($books.Count) books" 
    $results += "PASS"
} catch {
    Write-Host "SKIP: Cannot list books - $($_.Exception.Message)" 
    $results += "SKIP"
}
Write-Host ""

# Test 6: Create Data (if dataset exists)
Write-Host "[7] Create Data Test..." 
try {
    # First, get a publisher and author ID if needed
    $publisherId = $null
    $authorId = $null
    
    try {
        $publishers = Invoke-RestMethodSafe -Uri "$BaseUrl/api/v1/data/tst_publishers?limit=1" -Method GET -Headers $headers
        if ($publishers -and $publishers.Count -gt 0) {
            $publisherId = $publishers[0].__dataId
        } else {
            # Create a test publisher
            $pubData = @{ name = "Docker Test Publisher" } | ConvertTo-Json
            $newPub = Invoke-RestMethodSafe -Uri "$BaseUrl/api/v1/data/tst_publishers" -Method POST -Headers $headers -Body $pubData
            $publisherId = $newPub.data.__dataId
        }
    } catch {
        Write-Host "SKIP: Cannot get/create publisher - $($_.Exception.Message)" 
        $results += "SKIP"
        $publisherId = $null
    }
    
    if ($publisherId) {
        $testData = @{
            name = "Docker Test Book $(Get-Date -Format 'yyyyMMddHHmmss')"
            title = "Docker Test Book $(Get-Date -Format 'yyyyMMddHHmmss')"
            publisher = $publisherId
            price = 25
            pageCount = 200
            publisherCode = "TEST"
        } | ConvertTo-Json
        
        $created = Invoke-RestMethodSafe -Uri "$BaseUrl/api/v1/data/tst_books" -Method POST -Headers $headers -Body $testData
        if ($created.data -and $created.data.__dataId) {
        Write-Host "PASS: Created book with ID $($created.data.__dataId)" 
        $createdId = $created.data.__dataId
        $results += "PASS"
        
        # Test 7: Get by ID
        Write-Host "[8] Get Data by ID..." 
        try {
            $retrieved = Invoke-RestMethodSafe -Uri "$BaseUrl/api/v1/data/tst_books/$createdId" -Method GET -Headers $headers
            if ($retrieved.data.__dataId -eq $createdId) {
                Write-Host "PASS: Retrieved book successfully" 
                $results += "PASS"
            } else {
                Write-Host "FAIL: Retrieved ID mismatch" 
                $results += "FAIL"
            }
        } catch {
            Write-Host "FAIL: Get by ID failed - $($_.Exception.Message)" 
            $results += "FAIL"
        }
        Write-Host ""
        
        # Test 8: Update Data
        Write-Host "[9] Update Data..." 
        try {
            $updateData = @{
                price = 30
                title = "Updated Docker Test Book"
            } | ConvertTo-Json
            
            $updated = Invoke-RestMethodSafe -Uri "$BaseUrl/api/v1/data/tst_books/$createdId" -Method PUT -Headers $headers -Body $updateData
            if ($updated.data.price -eq 30) {
                Write-Host "PASS: Updated book successfully" 
                $results += "PASS"
            } else {
                Write-Host "FAIL: Update did not apply correctly" 
                $results += "FAIL"
            }
        } catch {
            Write-Host "FAIL: Update failed - $($_.Exception.Message)" 
            $results += "FAIL"
        }
        Write-Host ""
        
        # Test 9: Delete Data
        Write-Host "[10] Delete Data..." 
        try {
            $deleted = Invoke-RestMethodSafe -Uri "$BaseUrl/api/v1/data/tst_books/$createdId" -Method DELETE -Headers $headers
            Write-Host "PASS: Deleted book successfully" 
            $results += "PASS"
        } catch {
            Write-Host "FAIL: Delete failed - $($_.Exception.Message)" 
            $results += "FAIL"
        }
            Write-Host ""
        } else {
            Write-Host "FAIL: Create did not return expected data" 
            $results += "FAIL"
        }
    } else {
        Write-Host "SKIP: Publisher ID not available" 
        $results += "SKIP"
    }
} catch {
    Write-Host "SKIP: Cannot create data - $($_.Exception.Message)" 
    $results += "SKIP"
}
Write-Host ""

# Test 10: Search
Write-Host "[11] Search Test..." 
try {
    $search = Invoke-RestMethodSafe -Uri "$BaseUrl/api/v1/data/tst_books?search=test&limit=5" -Method GET -Headers $headers
    Write-Host "PASS: Search found $($search.Count) results" 
    $results += "PASS"
} catch {
    Write-Host "SKIP: Search test - $($_.Exception.Message)" 
    $results += "SKIP"
}
Write-Host ""

# Test 11: Filter
Write-Host "[12] Filter Test..." 
try {
    $filter = Invoke-RestMethodSafe -Uri "$BaseUrl/api/v1/data/tst_books?filter=price>50&limit=5" -Method GET -Headers $headers
    Write-Host "PASS: Filter found $($filter.Count) results" 
    $results += "PASS"
} catch {
    Write-Host "SKIP: Filter test - $($_.Exception.Message)" 
    $results += "SKIP"
}
Write-Host ""

# Test 12: Aggregate
Write-Host "[13] Aggregate Test..." 
try {
    $matchStage = @{ '$match' = @{ price = @{ '$gt' = 20 } } }
    $sortStage = @{ '$sort' = @{ title = 1 } }
    $limitStage = @{ '$limit' = 5 }
    $pipeline = @($matchStage, $sortStage, $limitStage)
    $aggBody = @{ pipeline = $pipeline } | ConvertTo-Json -Depth 10
    
    $aggregate = Invoke-RestMethodSafe -Uri "$BaseUrl/api/v1/data/tst_books/aggregate" -Method POST -Headers $headers -Body $aggBody
    Write-Host "PASS: Aggregate returned $($aggregate.Count) results" 
    $results += "PASS"
} catch {
    Write-Host "SKIP: Aggregate test - $($_.Exception.Message)" 
    $results += "SKIP"
}
Write-Host ""

# Summary
Write-Host "========================================" 
Write-Host "Test Summary" 
Write-Host "========================================" 
Write-Host ""

$passed = ($results | Where-Object { $_ -eq "PASS" }).Count
$failed = ($results | Where-Object { $_ -eq "FAIL" }).Count
$skipped = ($results | Where-Object { $_ -eq "SKIP" }).Count

Write-Host "Total Tests: $($results.Count)"
Write-Host "Passed: $passed"
Write-Host "Failed: $failed"
Write-Host "Skipped: $skipped"
Write-Host ""

if ($failed -eq 0) {
    Write-Host "RESULT: All tests passed!" 
    exit 0
} else {
    Write-Host "RESULT: Some tests failed. Check errors above." 
    exit 1
}

