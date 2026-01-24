# File Integration Test Script
# Tests complete file upload + data create workflow

$scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path
$authScript = Join-Path $scriptPath "..\auth\load-token.ps1"
. $authScript

$baseUrl = "https://localhost:5010"
$token = $global:Token

if ([string]::IsNullOrEmpty($token)) {
    Write-Host "❌ Token not found. Please run get-token.ps1 first." -ForegroundColor Red
    exit 1
}

Write-Host "`n🧪 Testing File Integration (Upload + Data Create)" -ForegroundColor Cyan
Write-Host "================================================`n" -ForegroundColor Cyan

# Step 1: Upload file
Write-Host "Step 1: Upload file" -ForegroundColor Yellow

$pdfBase64 = "JVBERi0xLjQKJeLjz9MKMSAwIG9iago8PAovVHlwZSAvQ2F0YWxvZwovUGFnZXMgMiAwIFIKPj4KZW5kb2JqCjIgMCBvYmoKPDwKL1R5cGUgL1BhZ2VzCi9LaWRzIFszIDAgUl0KL0NvdW50IDEKPD4KZW5kb2JqCjMgMCBvYmoKPDwKL1R5cGUgL1BhZ2UKL1BhcmVudCAyIDAgUgovTWVkaWFCb3ggWzAgMCA2MTIgNzkyXQovUmVzb3VyY2VzIDw8Ci9Gb250IDw8Ci9GMSA0IDAgUgo+Pgo+PgovQ29udGVudHMgNSAwIFIKPj4KZW5kb2JqCjQgMCBvYmoKPDwKL1R5cGUgL0ZvbnQKL1N1YnR5cGUgL1R5cGUxCi9CYXNlRm9udCAvSGVsdmV0aWNhCj4+CmVuZG9iago1IDAgb2JqCjw8Ci9MZW5ndGggNDQKPj4Kc3RyZWFtCkJUCi9GMSAxMiBUZgo1MCA3MDAgVGQKKEhlbGxvIFdvcmxkKSBUagpFVAplbmRzdHJlYW0KZW5kb2JqCnhyZWYKMCA2CjAwMDAwMDAwMDAgNjU1MzUgZiAKMDAwMDAwMDAwOSAwMDAwMCBuIAowMDAwMDAwMDU4IDAwMDAwIG4gCjAwMDAwMDAwOTggMDAwMDAgbiAKMDAwMDAwMDI0MCAwMDAwMCBuIAowMDAwMDAwMzI0IDAwMDAwIG4gCnRyYWlsZXIKPDwKL1NpemUgNgovUm9vdCAxIDAgUgo+PgpzdGFydHhyZWYKNDE2CiUlRU9G"

$uploadBody = @{
    Content = $pdfBase64
    DatasetName = "@test_files"
    FieldName = "documentFile"
    Folder = "invoices/2025"
    UseCompression = $true
    UseEncryption = $true
} | ConvertTo-Json

try {
    $uploadResponse = Invoke-RestMethod -Uri "$baseUrl/api/v1/files/upload" `
        -Method POST `
        -Headers @{
            "Authorization" = "Bearer $token"
            "Content-Type" = "application/json"
        } `
        -Body $uploadBody `
        -SkipCertificateCheck

    $filePath = $uploadResponse.Data.FilePath
    Write-Host "✅ File uploaded!" -ForegroundColor Green
    Write-Host "   File Path: $filePath" -ForegroundColor Gray

    # Step 2: Create data record with file path
    Write-Host "`nStep 2: Create data record with file path" -ForegroundColor Yellow

    $dataBody = @{
        title = "Test Invoice with File"
        documentFile = $filePath
        amount = 1000.50
    } | ConvertTo-Json

    $dataResponse = Invoke-RestMethod -Uri "$baseUrl/api/v1/data/@test_files" `
        -Method POST `
        -Headers @{
            "Authorization" = "Bearer $token"
            "Content-Type" = "application/json"
        } `
        -Body $dataBody `
        -SkipCertificateCheck

    Write-Host "✅ Data record created!" -ForegroundColor Green
    Write-Host "   Record ID: $($dataResponse.Data.__dataId)" -ForegroundColor Gray
    Write-Host "   File Path: $($dataResponse.Data.documentFile)" -ForegroundColor Gray

    # Step 3: Download file
    Write-Host "`nStep 3: Download file" -ForegroundColor Yellow

    $downloadResponse = Invoke-WebRequest -Uri "$baseUrl/api/v1/files/download?filePath=$([System.Web.HttpUtility]::UrlEncode($filePath))" `
        -Method GET `
        -Headers @{
            "Authorization" = "Bearer $token"
        } `
        -SkipCertificateCheck `
        -OutFile "test_downloaded_file.pdf"

    Write-Host "✅ File downloaded!" -ForegroundColor Green
    Write-Host "   Saved to: test_downloaded_file.pdf" -ForegroundColor Gray
    Write-Host "   Size: $((Get-Item 'test_downloaded_file.pdf').Length) bytes" -ForegroundColor Gray

    # Step 4: Get metadata
    Write-Host "`nStep 4: Get file metadata" -ForegroundColor Yellow

    $metadataResponse = Invoke-RestMethod -Uri "$baseUrl/api/v1/files/metadata?filePath=$([System.Web.HttpUtility]::UrlEncode($filePath))" `
        -Method GET `
        -Headers @{
            "Authorization" = "Bearer $token"
        } `
        -SkipCertificateCheck

    Write-Host "✅ Metadata retrieved!" -ForegroundColor Green
    Write-Host "   Original Filename: $($metadataResponse.Data.'x-amz-meta-original-filename')" -ForegroundColor Gray
    Write-Host "   File Size: $($metadataResponse.Data.'x-amz-meta-file-size') bytes" -ForegroundColor Gray
    Write-Host "   MIME Type: $($metadataResponse.Data.'x-amz-meta-mime-type')" -ForegroundColor Gray
    Write-Host "   Uploaded By: $($metadataResponse.Data.'x-amz-meta-uploaded-by')" -ForegroundColor Gray
    Write-Host "   Is Compressed: $($metadataResponse.Data.'x-amz-meta-is-zipped')" -ForegroundColor Gray
    Write-Host "   Is Encrypted: $($metadataResponse.Data.'x-amz-meta-is-encrypted')" -ForegroundColor Gray

    Write-Host "`n✅ Integration Test Complete!" -ForegroundColor Green
}
catch {
    Write-Host "❌ Test failed: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.ErrorDetails.Message) {
        Write-Host "   Details: $($_.ErrorDetails.Message)" -ForegroundColor Red
    }
    exit 1
}
