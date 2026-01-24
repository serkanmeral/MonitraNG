# File Validation Test Script
# Tests file field validation in DataController

$scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path
$authScript = Join-Path $scriptPath "..\auth\load-token.ps1"
. $authScript

$baseUrl = "https://localhost:5010"
$token = $global:Token

if ([string]::IsNullOrEmpty($token)) {
    Write-Host "❌ Token not found. Please run get-token.ps1 first." -ForegroundColor Red
    exit 1
}

Write-Host "`n🧪 Testing File Field Validation" -ForegroundColor Cyan
Write-Host "==============================`n" -ForegroundColor Cyan

# Test 1: Valid file path
Write-Host "Test 1: Valid file path" -ForegroundColor Yellow

$validData = @{
    title = "Test with valid file"
    documentFile = "/mng-meral/data/@test_files/record-id/test/file-uuid.pdf"
} | ConvertTo-Json

try {
    $response = Invoke-RestMethod -Uri "$baseUrl/api/v1/data/@test_files" `
        -Method POST `
        -Headers @{
            "Authorization" = "Bearer $token"
            "Content-Type" = "application/json"
        } `
        -Body $validData `
        -SkipCertificateCheck

    Write-Host "✅ Valid path accepted!" -ForegroundColor Green
}
catch {
    Write-Host "❌ Unexpected error: $($_.Exception.Message)" -ForegroundColor Red
}

# Test 2: Invalid domain in path
Write-Host "`nTest 2: Invalid domain in path" -ForegroundColor Yellow

$invalidDomainData = @{
    title = "Test with invalid domain"
    documentFile = "/mng-wrong-domain/data/@test_files/record-id/file.pdf"
} | ConvertTo-Json

try {
    $response = Invoke-RestMethod -Uri "$baseUrl/api/v1/data/@test_files" `
        -Method POST `
        -Headers @{
            "Authorization" = "Bearer $token"
            "Content-Type" = "application/json"
        } `
        -Body $invalidDomainData `
        -SkipCertificateCheck

    Write-Host "❌ Should have failed but didn't!" -ForegroundColor Red
}
catch {
    if ($_.ErrorDetails.Message -like "*INVALID_FILE_PATH*" -or $_.ErrorDetails.Message -like "*domain*") {
        Write-Host "✅ Correctly rejected invalid domain!" -ForegroundColor Green
    }
    else {
        Write-Host "❌ Wrong error: $($_.Exception.Message)" -ForegroundColor Red
    }
}

# Test 3: Invalid dataset in path
Write-Host "`nTest 3: Invalid dataset in path" -ForegroundColor Yellow

$invalidDatasetData = @{
    title = "Test with invalid dataset"
    documentFile = "/mng-meral/data/@wrong_dataset/record-id/file.pdf"
} | ConvertTo-Json

try {
    $response = Invoke-RestMethod -Uri "$baseUrl/api/v1/data/@test_files" `
        -Method POST `
        -Headers @{
            "Authorization" = "Bearer $token"
            "Content-Type" = "application/json"
        } `
        -Body $invalidDatasetData `
        -SkipCertificateCheck

    Write-Host "❌ Should have failed but didn't!" -ForegroundColor Red
}
catch {
    if ($_.ErrorDetails.Message -like "*INVALID_FILE_PATH*" -or $_.ErrorDetails.Message -like "*dataset*") {
        Write-Host "✅ Correctly rejected invalid dataset!" -ForegroundColor Green
    }
    else {
        Write-Host "❌ Wrong error: $($_.Exception.Message)" -ForegroundColor Red
    }
}

# Test 4: Invalid path format
Write-Host "`nTest 4: Invalid path format" -ForegroundColor Yellow

$invalidFormatData = @{
    title = "Test with invalid format"
    documentFile = "invalid/path/format"
} | ConvertTo-Json

try {
    $response = Invoke-RestMethod -Uri "$baseUrl/api/v1/data/@test_files" `
        -Method POST `
        -Headers @{
            "Authorization" = "Bearer $token"
            "Content-Type" = "application/json"
        } `
        -Body $invalidFormatData `
        -SkipCertificateCheck

    Write-Host "❌ Should have failed but didn't!" -ForegroundColor Red
}
catch {
    if ($_.ErrorDetails.Message -like "*INVALID_FILE_PATH*") {
        Write-Host "✅ Correctly rejected invalid format!" -ForegroundColor Green
    }
    else {
        Write-Host "❌ Wrong error: $($_.Exception.Message)" -ForegroundColor Red
    }
}

# Test 5: Array file field
Write-Host "`nTest 5: Array file field" -ForegroundColor Yellow

$arrayData = @{
    title = "Test with array files"
    attachments = @(
        "/mng-meral/data/@test_files/record-id/file1.pdf",
        "/mng-meral/data/@test_files/record-id/file2.pdf"
    )
} | ConvertTo-Json

try {
    $response = Invoke-RestMethod -Uri "$baseUrl/api/v1/data/@test_files" `
        -Method POST `
        -Headers @{
            "Authorization" = "Bearer $token"
            "Content-Type" = "application/json"
        } `
        -Body $arrayData `
        -SkipCertificateCheck

    Write-Host "✅ Array file paths accepted!" -ForegroundColor Green
}
catch {
    Write-Host "❌ Unexpected error: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host "`n✅ File Validation Tests Complete!" -ForegroundColor Green
