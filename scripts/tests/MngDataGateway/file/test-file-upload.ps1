# File Upload Test Script
# Tests file upload functionality via FilesController

$scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path
$authScript = Join-Path $scriptPath "..\auth\load-token.ps1"
. $authScript

$baseUrl = "https://localhost:5010"
$token = $global:Token

if ([string]::IsNullOrEmpty($token)) {
    Write-Host "❌ Token not found. Please run get-token.ps1 first." -ForegroundColor Red
    exit 1
}

Write-Host "`n🧪 Testing File Upload Endpoint" -ForegroundColor Cyan
Write-Host "================================`n" -ForegroundColor Cyan

# Test 1: Upload PDF file
Write-Host "Test 1: Upload PDF file" -ForegroundColor Yellow

# Create a simple PDF base64 (minimal valid PDF)
$pdfBase64 = "JVBERi0xLjQKJeLjz9MKMSAwIG9iago8PAovVHlwZSAvQ2F0YWxvZwovUGFnZXMgMiAwIFIKPj4KZW5kb2JqCjIgMCBvYmoKPDwKL1R5cGUgL1BhZ2VzCi9LaWRzIFszIDAgUl0KL0NvdW50IDEKPD4KZW5kb2JqCjMgMCBvYmoKPDwKL1R5cGUgL1BhZ2UKL1BhcmVudCAyIDAgUgovTWVkaWFCb3ggWzAgMCA2MTIgNzkyXQovUmVzb3VyY2VzIDw8Ci9Gb250IDw8Ci9GMSA0IDAgUgo+Pgo+PgovQ29udGVudHMgNSAwIFIKPj4KZW5kb2JqCjQgMCBvYmoKPDwKL1R5cGUgL0ZvbnQKL1N1YnR5cGUgL1R5cGUxCi9CYXNlRm9udCAvSGVsdmV0aWNhCj4+CmVuZG9iago1IDAgb2JqCjw8Ci9MZW5ndGggNDQKPj4Kc3RyZWFtCkJUCi9GMSAxMiBUZgo1MCA3MDAgVGQKKEhlbGxvIFdvcmxkKSBUagpFVAplbmRzdHJlYW0KZW5kb2JqCnhyZWYKMCA2CjAwMDAwMDAwMDAgNjU1MzUgZiAKMDAwMDAwMDAwOSAwMDAwMCBuIAowMDAwMDAwMDU4IDAwMDAwIG4gCjAwMDAwMDAwOTggMDAwMDAgbiAKMDAwMDAwMDI0MCAwMDAwMCBuIAowMDAwMDAwMzI0IDAwMDAwIG4gCnRyYWlsZXIKPDwKL1NpemUgNgovUm9vdCAxIDAgUgo+PgpzdGFydHhyZWYKNDE2CiUlRU9G"

$uploadBody = @{
    Content = $pdfBase64
    DatasetName = "@test_files"
    FieldName = "documentFile"
    Folder = "test/2025"
    UseCompression = $true
    UseEncryption = $true
} | ConvertTo-Json

try {
    $response = Invoke-RestMethod -Uri "$baseUrl/api/v1/files/upload" `
        -Method POST `
        -Headers @{
            "Authorization" = "Bearer $token"
            "Content-Type" = "application/json"
        } `
        -Body $uploadBody `
        -SkipCertificateCheck

    Write-Host "✅ Upload successful!" -ForegroundColor Green
    Write-Host "   File Path: $($response.Data.FilePath)" -ForegroundColor Gray
    Write-Host "   File Size: $($response.Data.FileSize) bytes" -ForegroundColor Gray
    Write-Host "   MIME Type: $($response.Data.MimeType)" -ForegroundColor Gray
    Write-Host "   Compressed: $($response.Data.IsCompressed)" -ForegroundColor Gray
    Write-Host "   Encrypted: $($response.Data.IsEncrypted)" -ForegroundColor Gray
    
    $global:UploadedFilePath = $response.Data.FilePath
}
catch {
    Write-Host "❌ Upload failed: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.ErrorDetails.Message) {
        Write-Host "   Details: $($_.ErrorDetails.Message)" -ForegroundColor Red
    }
}

Write-Host "`nTest 2: Upload without compression/encryption" -ForegroundColor Yellow

$uploadBody2 = @{
    Content = $pdfBase64
    DatasetName = "@test_files"
    FieldName = "documentFile"
    Folder = "test/2025"
    UseCompression = $false
    UseEncryption = $false
} | ConvertTo-Json

try {
    $response2 = Invoke-RestMethod -Uri "$baseUrl/api/v1/files/upload" `
        -Method POST `
        -Headers @{
            "Authorization" = "Bearer $token"
            "Content-Type" = "application/json"
        } `
        -Body $uploadBody2 `
        -SkipCertificateCheck

    Write-Host "✅ Upload successful (no compression/encryption)!" -ForegroundColor Green
    Write-Host "   File Path: $($response2.Data.FilePath)" -ForegroundColor Gray
    Write-Host "   Compressed: $($response2.Data.IsCompressed)" -ForegroundColor Gray
    Write-Host "   Encrypted: $($response2.Data.IsEncrypted)" -ForegroundColor Gray
}
catch {
    Write-Host "❌ Upload failed: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host "`n✅ File Upload Tests Complete!" -ForegroundColor Green
