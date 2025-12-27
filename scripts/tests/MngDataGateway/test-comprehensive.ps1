# Comprehensive Test Suite for MngDataGateway
# Tests all major functionality of the DataGateway API

param(
    [string]$BaseUrl = "https://localhost:5010",
    [switch]$SkipDatasetSetup = $false,
    [switch]$SkipDataTests = $false,
    [switch]$SkipValidationTests = $false
)

$ErrorActionPreference = "Stop"

# Script path
$scriptPath = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $PSCommandPath }
if ([string]::IsNullOrEmpty($scriptPath)) {
    $scriptPath = Get-Location
}

# Test results tracking
$global:TestResults = @{
    Total = 0
    Passed = 0
    Failed = 0
    Skipped = 0
    Categories = @{}
}

function Write-TestHeader {
    param([string]$Category, [string]$Description)
    Write-Host "`n" -NoNewline
    Write-Host "╔═══════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
    Write-Host "║ " -NoNewline -ForegroundColor Cyan
    Write-Host "$Category".PadRight(57) -ForegroundColor White
    Write-Host "║" -ForegroundColor Cyan
    if ($Description) {
        Write-Host "║ " -NoNewline -ForegroundColor Cyan
        Write-Host "$Description".PadRight(57) -ForegroundColor Gray
        Write-Host "║" -ForegroundColor Cyan
    }
    Write-Host "╚═══════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
}

function Test-Category {
    param(
        [string]$CategoryName,
        [string]$TestScript,
        [string[]]$Arguments = @()
    )
    
    Write-TestHeader -Category "$CategoryName Tests" -Description "Running: $TestScript"
    
    if (-not (Test-Path $TestScript)) {
        Write-Host "⚠️  Test script not found: $TestScript" -ForegroundColor Yellow
        $global:TestResults.Skipped++
        $global:TestResults.Categories[$CategoryName] = "SKIPPED (Script not found)"
        return
    }
    
    try {
        $startTime = Get-Date
        $output = & $TestScript @Arguments 2>&1
        $duration = (Get-Date) - $startTime
        
        # Check exit code
        if ($LASTEXITCODE -eq 0 -or $LASTEXITCODE -eq $null) {
            Write-Host "`n✅ Category completed: $CategoryName (Duration: $($duration.TotalSeconds.ToString('F2'))s)" -ForegroundColor Green
            $global:TestResults.Passed++
            $global:TestResults.Categories[$CategoryName] = "PASSED"
        } else {
            Write-Host "`n❌ Category failed: $CategoryName (Exit Code: $LASTEXITCODE)" -ForegroundColor Red
            $global:TestResults.Failed++
            $global:TestResults.Categories[$CategoryName] = "FAILED (Exit: $LASTEXITCODE)"
        }
        $global:TestResults.Total++
        
        # Show output
        Write-Host $output
        
    } catch {
        Write-Host "`n❌ Category error: $CategoryName" -ForegroundColor Red
        Write-Host "   Error: $($_.Exception.Message)" -ForegroundColor Red
        $global:TestResults.Failed++
        $global:TestResults.Total++
        $global:TestResults.Categories[$CategoryName] = "ERROR: $($_.Exception.Message)"
    }
    
    Write-Host ""
}

# Token setup
Write-Host "`n🔐 Authentication Setup" -ForegroundColor Cyan
Write-Host "=" * 60 -ForegroundColor Gray

$getTokenScript = Join-Path $scriptPath "auth\get-token.ps1"
if (-not (Test-Path $getTokenScript)) {
    Write-Host "❌ get-token.ps1 not found!" -ForegroundColor Red
    exit 1
}

$token = & $getTokenScript
if ([string]::IsNullOrEmpty($token)) {
    Write-Host "❌ Failed to get token!" -ForegroundColor Red
    exit 1
}

Write-Host "✅ Token retrieved successfully" -ForegroundColor Green
Write-Host ""

# Health Check First
Write-TestHeader -Category "Health Check & Version" -Description "Basic connectivity tests"
try {
    [System.Net.ServicePointManager]::ServerCertificateValidationCallback = {$true}
    
    $healthUrl = "$BaseUrl/api/v1/health"
    Write-Host "Testing: GET $healthUrl" -ForegroundColor Yellow
    $health = Invoke-RestMethod -Uri $healthUrl -Method GET -SkipCertificateCheck
    Write-Host "✅ Health Check: $($health.status)" -ForegroundColor Green
    
    $versionUrl = "$BaseUrl/api/v1/version"
    Write-Host "Testing: GET $versionUrl" -ForegroundColor Yellow
    $version = Invoke-RestMethod -Uri $versionUrl -Method GET -SkipCertificateCheck
    Write-Host "✅ Version: $($version.version)" -ForegroundColor Green
    
    $global:TestResults.Passed++
    $global:TestResults.Total++
    $global:TestResults.Categories["Health Check"] = "PASSED"
} catch {
    Write-Host "❌ Health Check Failed: $($_.Exception.Message)" -ForegroundColor Red
    $global:TestResults.Failed++
    $global:TestResults.Total++
    $global:TestResults.Categories["Health Check"] = "FAILED"
    exit 1
}

# Test Categories
if (-not $SkipDatasetSetup) {
    # Dataset Tests
    Test-Category -CategoryName "Dataset CRUD" `
        -TestScript (Join-Path $scriptPath "dataset\test-datasets.ps1")
    
    # Dataset Categories Tests
    Test-Category -CategoryName "Dataset Categories" `
        -TestScript (Join-Path $scriptPath "dataset-categories\test-dataset-categories.ps1")
}

if (-not $SkipDataTests) {
    # Data CRUD Tests
    Test-Category -CategoryName "Data CRUD" `
        -TestScript (Join-Path $scriptPath "data\test-data-crud.ps1")
    
    # Bulk Insert Tests
    Test-Category -CategoryName "Bulk Insert" `
        -TestScript (Join-Path $scriptPath "data\test-bulk-insert.ps1")
}

if (-not $SkipValidationTests) {
    # Validation Tests
    Test-Category -CategoryName "Validations" `
        -TestScript (Join-Path $scriptPath "validation\test-validations.ps1")
}

# Query Tests
Test-Category -CategoryName "Predefined Queries" `
    -TestScript (Join-Path $scriptPath "query\test-all-query-examples.ps1")

# Search Tests
Test-Category -CategoryName "Search (Basic)" `
    -TestScript (Join-Path $scriptPath "search\test-search-basic.ps1")

Test-Category -CategoryName "Search (Relations)" `
    -TestScript (Join-Path $scriptPath "search\test-search-relations.ps1")

# Filter Tests
Test-Category -CategoryName "Filter" `
    -TestScript (Join-Path $scriptPath "filter\test-price-filter.ps1")

# Aggregate Tests
Test-Category -CategoryName "Aggregate" `
    -TestScript (Join-Path $scriptPath "aggregate\test-aggregate.ps1")

# Export Tests
Test-Category -CategoryName "CSV Export" `
    -TestScript (Join-Path $scriptPath "export\test-csv-export.ps1")

# Index Tests
Test-Category -CategoryName "Index Definitions" `
    -TestScript (Join-Path $scriptPath "index\test-index-definitions.ps1")

# Final Summary
Write-Host "`n"
Write-Host "╔═══════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║ " -NoNewline -ForegroundColor Cyan
Write-Host "COMPREHENSIVE TEST SUITE SUMMARY".PadRight(57) -ForegroundColor White
Write-Host "║" -ForegroundColor Cyan
Write-Host "╠═══════════════════════════════════════════════════════════╣" -ForegroundColor Cyan
Write-Host "║ " -NoNewline -ForegroundColor Cyan
Write-Host "Total Tests: ".PadRight(20) -NoNewline -ForegroundColor Gray
Write-Host "$($global:TestResults.Total)".PadRight(37) -ForegroundColor White
Write-Host "║" -ForegroundColor Cyan
Write-Host "║ " -NoNewline -ForegroundColor Cyan
Write-Host "Passed: ".PadRight(20) -NoNewline -ForegroundColor Gray
Write-Host "$($global:TestResults.Passed)".PadRight(37) -ForegroundColor Green
Write-Host "║" -ForegroundColor Cyan
Write-Host "║ " -NoNewline -ForegroundColor Cyan
Write-Host "Failed: ".PadRight(20) -NoNewline -ForegroundColor Gray
Write-Host "$($global:TestResults.Failed)".PadRight(37) -ForegroundColor Red
Write-Host "║ " -NoNewline -ForegroundColor Cyan
Write-Host "Skipped: ".PadRight(20) -NoNewline -ForegroundColor Gray
Write-Host "$($global:TestResults.Skipped)".PadRight(37) -ForegroundColor Yellow
Write-Host "║" -ForegroundColor Cyan
Write-Host "╠═══════════════════════════════════════════════════════════╣" -ForegroundColor Cyan
Write-Host "║ Category Results:" -ForegroundColor Cyan
Write-Host "║" -ForegroundColor Cyan

foreach ($category in $global:TestResults.Categories.GetEnumerator() | Sort-Object Name) {
    $status = $category.Value
    $color = switch -Wildcard ($status) {
        "PASSED*" { "Green" }
        "FAILED*" { "Red" }
        "ERROR*" { "Red" }
        "SKIPPED*" { "Yellow" }
        default { "Gray" }
    }
    
    Write-Host "║   " -NoNewline -ForegroundColor Cyan
    Write-Host "$($category.Key):".PadRight(25) -NoNewline -ForegroundColor Gray
    Write-Host $status.PadRight(32) -ForegroundColor $color
}

Write-Host "╚═══════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
Write-Host ""

# Final status
$successRate = if ($global:TestResults.Total -gt 0) {
    [math]::Round(($global:TestResults.Passed / $global:TestResults.Total) * 100, 2)
} else {
    0
}

Write-Host "Success Rate: $successRate%" -ForegroundColor $(if ($successRate -eq 100) { "Green" } elseif ($successRate -ge 80) { "Yellow" } else { "Red" })

if ($global:TestResults.Failed -eq 0 -and $global:TestResults.Skipped -eq 0) {
    Write-Host "`n🎉 All tests passed!" -ForegroundColor Green
    exit 0
} elseif ($global:TestResults.Failed -eq 0) {
    $skipped = $global:TestResults.Skipped
    $message = "`n✅ All executed tests passed! $skipped tests skipped"
    Write-Host $message -ForegroundColor Green
    exit 0
} else {
    Write-Host "`n⚠️ Some tests failed. Please review the output above." -ForegroundColor Yellow
    exit 1
}

