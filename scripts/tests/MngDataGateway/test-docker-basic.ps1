# Basic Docker Test Script
# Quick tests to verify Docker container is working

$baseUrl = "https://localhost:5010"
$scriptPath = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $PSCommandPath }
$getTokenScript = Join-Path $scriptPath "auth\get-token.ps1"

Write-Host "`n🔍 Docker Container Basic Tests" -ForegroundColor Cyan
Write-Host "=" * 60 -ForegroundColor Gray
Write-Host ""

# Get token
Write-Host "📝 Getting token..." -ForegroundColor Yellow
$token = & $getTokenScript
if ([string]::IsNullOrEmpty($token)) {
    Write-Host "❌ Token alınamadı!" -ForegroundColor Red
    exit 1
}
Write-Host "✅ Token alındı" -ForegroundColor Green
Write-Host ""

$headers = @{
    "Authorization" = "Bearer $token"
    "Content-Type" = "application/json"
}

[System.Net.ServicePointManager]::ServerCertificateValidationCallback = {$true}

$testResults = @()

# Test 1: Health Check
Write-Host "1️⃣  Health Check..." -ForegroundColor Yellow
try {
    $health = Invoke-RestMethod -Uri "$baseUrl/api/v1/health" -Method GET -Headers $headers -SkipCertificateCheck
    Write-Host "   ✅ Health: $($health.status)" -ForegroundColor Green
    $testResults += "PASS"
} catch {
    Write-Host "   ❌ Failed: $($_.Exception.Message)" -ForegroundColor Red
    $testResults += "FAIL"
}

# Test 2: Version
Write-Host "2️⃣  Version..." -ForegroundColor Yellow
try {
    $version = Invoke-RestMethod -Uri "$baseUrl/api/v1/version" -Method GET -Headers $headers -SkipCertificateCheck
    Write-Host "   ✅ Version: $($version.version)" -ForegroundColor Green
    $testResults += "PASS"
} catch {
    Write-Host "   ❌ Failed: $($_.Exception.Message)" -ForegroundColor Red
    $testResults += "FAIL"
}

# Test 3: List Datasets
Write-Host "3️⃣  List Datasets..." -ForegroundColor Yellow
try {
    $datasets = Invoke-RestMethod -Uri "$baseUrl/api/v1/datasets?pageSize=5" -Method GET -Headers $headers -SkipCertificateCheck
    Write-Host "   ✅ Found $($datasets.data.Count) datasets" -ForegroundColor Green
    if ($datasets.data.Count -gt 0) {
        Write-Host "   Sample: $($datasets.data[0].name)" -ForegroundColor Gray
    }
    $testResults += "PASS"
} catch {
    Write-Host "   ❌ Failed: $($_.Exception.Message)" -ForegroundColor Red
    $testResults += "FAIL"
}

# Test 4: List Data (if tst_books exists)
Write-Host "4️⃣  List Data (tst_books)..." -ForegroundColor Yellow
try {
    $books = Invoke-RestMethod -Uri "$baseUrl/api/v1/data/tst_books?limit=5" -Method GET -Headers $headers -SkipCertificateCheck
    Write-Host "   ✅ Found $($books.Count) books" -ForegroundColor Green
    if ($books.Count -gt 0) {
        Write-Host "   Sample: $($books[0].title)" -ForegroundColor Gray
    }
    $testResults += "PASS"
} catch {
    Write-Host "   ⚠️  tst_books dataset may not exist: $($_.Exception.Message)" -ForegroundColor Yellow
    $testResults += "SKIP"
}

# Summary
Write-Host ""
Write-Host "=" * 60 -ForegroundColor Gray
Write-Host "Test Summary" -ForegroundColor Cyan
Write-Host "=" * 60 -ForegroundColor Gray
$passed = ($testResults | Where-Object { $_ -eq "PASS" }).Count
$failed = ($testResults | Where-Object { $_ -eq "FAIL" }).Count
$skipped = ($testResults | Where-Object { $_ -eq "SKIP" }).Count
Write-Host "✅ Passed: $passed" -ForegroundColor Green
Write-Host "❌ Failed: $failed" -ForegroundColor $(if ($failed -eq 0) { "Gray" } else { "Red" })
Write-Host "⚠️  Skipped: $skipped" -ForegroundColor $(if ($skipped -eq 0) { "Gray" } else { "Yellow" })
Write-Host ""

if ($failed -eq 0) {
    Write-Host "🎉 Basic tests passed! Container is working." -ForegroundColor Green
    exit 0
} else {
    Write-Host "❌ Some tests failed. Check container logs." -ForegroundColor Red
    exit 1
}

