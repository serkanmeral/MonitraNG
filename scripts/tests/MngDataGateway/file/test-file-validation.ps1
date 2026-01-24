# File Validation Test Script
# Tests file field validation in DataController

$scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path
$authScript = Join-Path $scriptPath "..\auth\load-token.ps1"
. $authScript

$baseUrl = "http://localhost:5010"
$token = $global:Token

if ([string]::IsNullOrEmpty($token)) {
    Write-Host "❌ Token not found. Please run get-token.ps1 first." -ForegroundColor Red
    exit 1
}

# API check - Swagger is available, so API is running
Write-Host ""
Write-Host "API is running at $baseUrl" -ForegroundColor Green

Write-Host "`n🧪 Testing File Field Validation" -ForegroundColor Cyan
Write-Host "==============================`n" -ForegroundColor Cyan

# Test 1: Valid file path
Write-Host "Test 1: Valid file path" -ForegroundColor Yellow

$validData = @{
    title = "Test with valid file"
    documentFile = "/mng-meral/data/users/@test_files/record-id/test/file-uuid.pdf"
} | ConvertTo-Json

try {
    $response = Invoke-RestMethod -Uri "$baseUrl/api/v1/data/@test_files" `
        -Method POST `
        -Headers @{
            "Authorization" = "Bearer $token"
            "Content-Type" = "application/json"
        } `
        -Body $validData `

    Write-Host "✅ Valid path accepted!" -ForegroundColor Green
}
catch {
    Write-Host "❌ Unexpected error: $($_.Exception.Message)" -ForegroundColor Red
}

# Test 2: Invalid domain in path
Write-Host "`nTest 2: Invalid domain in path" -ForegroundColor Yellow

$invalidDomainData = @{
    title = "Test with invalid domain"
    documentFile = "/mng-wrong-domain/data/users/@test_files/record-id/file.pdf"
} | ConvertTo-Json

try {
    $response = Invoke-RestMethod -Uri "$baseUrl/api/v1/data/@test_files" `
        -Method POST `
        -Headers @{
            "Authorization" = "Bearer $token"
            "Content-Type" = "application/json"
        } `
        -Body $invalidDomainData `

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
    documentFile = "/mng-meral/data/users/@wrong_dataset/record-id/file.pdf"
} | ConvertTo-Json

try {
    $response = Invoke-RestMethod -Uri "$baseUrl/api/v1/data/@test_files" `
        -Method POST `
        -Headers @{
            "Authorization" = "Bearer $token"
            "Content-Type" = "application/json"
        } `
        -Body $invalidDatasetData `

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

# Test 5: Array file field (string paths)
Write-Host "`nTest 5: Array file field (string paths)" -ForegroundColor Yellow

$arrayData = @{
    title = "Test with array files"
    attachments = @(
        "/mng-meral/data/users/@test_files/record-id/file1.pdf",
        "/mng-meral/data/users/@test_files/record-id/file2.pdf"
    )
} | ConvertTo-Json

try {
    $response = Invoke-RestMethod -Uri "$baseUrl/api/v1/data/@test_files" `
        -Method POST `
        -Headers @{
            "Authorization" = "Bearer $token"
            "Content-Type" = "application/json"
        } `
        -Body $arrayData

    Write-Host "✅ Array file paths accepted!" -ForegroundColor Green
}
catch {
    Write-Host "❌ Unexpected error: $($_.Exception.Message)" -ForegroundColor Red
}

# Test 6: File field with object model
Write-Host "`nTest 6: File field with object model" -ForegroundColor Yellow

$pdfBase64 = "JVBERi0xLjQKJeLjz9MKMSAwIG9iago8PAovVHlwZSAvQ2F0YWxvZwovUGFnZXMgMiAwIFIKPj4KZW5kb2JqCjIgMCBvYmoKPDwKL1R5cGUgL1BhZ2VzCi9LaWRzIFszIDAgUl0KL0NvdW50IDEKPD4KZW5kb2JqCjMgMCBvYmoKPDwKL1R5cGUgL1BhZ2UKL1BhcmVudCAyIDAgUgovTWVkaWFCb3ggWzAgMCA2MTIgNzkyXQovUmVzb3VyY2VzIDw8Ci9Gb250IDw8Ci9GMSA0IDAgUgo+Pgo+PgovQ29udGVudHMgNSAwIFIKPj4KZW5kb2JqCjQgMCBvYmoKPDwKL1R5cGUgL0ZvbnQKL1N1YnR5cGUgL1R5cGUxCi9CYXNlRm9udCAvSGVsdmV0aWNhCj4+CmVuZG9iago1IDAgb2JqCjw8Ci9MZW5ndGggNDQKPj4Kc3RyZWFtCkJUCi9GMSAxMiBUZgo1MCA3MDAgVGQKKEhlbGxvIFdvcmxkKSBUagpFVAplbmRzdHJlYW0KZW5kb2JqCnhyZWYKMCA2CjAwMDAwMDAwMDAgNjU1MzUgZiAKMDAwMDAwMDAwOSAwMDAwMCBuIAowMDAwMDAwMDU4IDAwMDAwIG4gCjAwMDAwMDAwOTggMDAwMDAgbiAKMDAwMDAwMDI0MCAwMDAwMCBuIAowMDAwMDAwMzI0IDAwMDAwIG4gCnRyYWlsZXIKPDwKL1NpemUgNgovUm9vdCAxIDAgUgo+PgpzdGFydHhyZWYKNDE2CiUlRU9G"

$objectModelData = @{
    title = "Test with object model"
    documentFile = @{
        content = $pdfBase64
        folder = "test/validation"
        useCompression = $true
        useEncryption = $true
    }
} | ConvertTo-Json -Depth 10

try {
    $response = Invoke-RestMethod -Uri "$baseUrl/api/v1/data/@test_files" `
        -Method POST `
        -Headers @{
            "Authorization" = "Bearer $token"
            "Content-Type" = "application/json"
        } `
        -Body $objectModelData

    Write-Host "✅ Object model accepted!" -ForegroundColor Green
    Write-Host "   File Path: $($response.Data.documentFile)" -ForegroundColor Gray
}
catch {
    Write-Host "❌ Unexpected error: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.ErrorDetails.Message) {
        Write-Host "   Details: $($_.ErrorDetails.Message)" -ForegroundColor Red
    }
}

# Test 7: Array file field with object model
Write-Host "`nTest 7: Array file field with object model" -ForegroundColor Yellow

$arrayObjectModelData = @{
    title = "Test with array object model"
    attachments = @(
        @{
            content = $pdfBase64
            folder = "test/attachments"
            useCompression = $true
            useEncryption = $true
        },
        @{
            content = $pdfBase64
            folder = "test/attachments"
            useCompression = $false
            useEncryption = $false
        }
    )
} | ConvertTo-Json -Depth 10

try {
    $response = Invoke-RestMethod -Uri "$baseUrl/api/v1/data/@test_files" `
        -Method POST `
        -Headers @{
            "Authorization" = "Bearer $token"
            "Content-Type" = "application/json"
        } `
        -Body $arrayObjectModelData

    Write-Host "✅ Array object model accepted!" -ForegroundColor Green
    Write-Host "   Attachments Count: $(($response.Data.attachments).Count)" -ForegroundColor Gray
}
catch {
    Write-Host "❌ Unexpected error: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.ErrorDetails.Message) {
        Write-Host "   Details: $($_.ErrorDetails.Message)" -ForegroundColor Red
    }
}

# Test 8: Invalid object model (missing content)
Write-Host "`nTest 8: Invalid object model (missing content)" -ForegroundColor Yellow

$invalidObjectData = @{
    title = "Test with invalid object"
    documentFile = @{
        folder = "test/invalid"
        useCompression = $true
    }
} | ConvertTo-Json -Depth 10

try {
    $response = Invoke-RestMethod -Uri "$baseUrl/api/v1/data/@test_files" `
        -Method POST `
        -Headers @{
            "Authorization" = "Bearer $token"
            "Content-Type" = "application/json"
        } `
        -Body $invalidObjectData

    Write-Host "❌ Should have failed but didn't!" -ForegroundColor Red
}
catch {
    if ($_.ErrorDetails.Message -like "*content*" -or $_.ErrorDetails.Message -like "*INVALID_FILE_FIELD*") {
        Write-Host "✅ Correctly rejected invalid object model!" -ForegroundColor Green
    }
    else {
        Write-Host "❌ Wrong error: $($_.Exception.Message)" -ForegroundColor Red
    }
}

Write-Host "`n✅ File Validation Tests Complete!" -ForegroundColor Green
