# File Integration Test Script
# Tests complete file upload + data create workflow using object model

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

Write-Host "`n🧪 Testing File Integration (Object Model - Direct Data Create)" -ForegroundColor Cyan
Write-Host "================================================`n" -ForegroundColor Cyan

$pdfBase64 = "JVBERi0xLjQKJeLjz9MKMSAwIG9iago8PAovVHlwZSAvQ2F0YWxvZwovUGFnZXMgMiAwIFIKPj4KZW5kb2JqCjIgMCBvYmoKPDwKL1R5cGUgL1BhZ2VzCi9LaWRzIFszIDAgUl0KL0NvdW50IDEKPD4KZW5kb2JqCjMgMCBvYmoKPDwKL1R5cGUgL1BhZ2UKL1BhcmVudCAyIDAgUgovTWVkaWFCb3ggWzAgMCA2MTIgNzkyXQovUmVzb3VyY2VzIDw8Ci9Gb250IDw8Ci9GMSA0IDAgUgo+Pgo+PgovQ29udGVudHMgNSAwIFIKPj4KZW5kb2JqCjQgMCBvYmoKPDwKL1R5cGUgL0ZvbnQKL1N1YnR5cGUgL1R5cGUxCi9CYXNlRm9udCAvSGVsdmV0aWNhCj4+CmVuZG9iago1IDAgb2JqCjw8Ci9MZW5ndGggNDQKPj4Kc3RyZWFtCkJUCi9GMSAxMiBUZgo1MCA3MDAgVGQKKEhlbGxvIFdvcmxkKSBUagpFVAplbmRzdHJlYW0KZW5kb2JqCnhyZWYKMCA2CjAwMDAwMDAwMDAgNjU1MzUgZiAKMDAwMDAwMDAwOSAwMDAwMCBuIAowMDAwMDAwMDU4IDAwMDAwIG4gCjAwMDAwMDAwOTggMDAwMDAgbiAKMDAwMDAwMDI0MCAwMDAwMCBuIAowMDAwMDAwMzI0IDAwMDAwIG4gCnRyYWlsZXIKPDwKL1NpemUgNgovUm9vdCAxIDAgUgo+PgpzdGFydHhyZWYKNDE2CiUlRU9G"

try {
    # Step 1: Create data record with file object model (direct upload)
    Write-Host "Step 1: Create data record with file object model" -ForegroundColor Yellow

    $dataBody = @{
        title = "Test Invoice with File (Object Model)"
        documentFile = @{
            content = $pdfBase64
            folder = "invoices/2025"
            useCompression = $true
            useEncryption = $true
        }
        amount = 1000.50
    } | ConvertTo-Json -Depth 10

    $dataResponse = Invoke-RestMethod -Uri "$baseUrl/api/v1/data/@test_files" `
        -Method POST `
        -Headers @{
            "Authorization" = "Bearer $token"
            "Content-Type" = "application/json"
        } `
        -Body $dataBody

    $filePath = $dataResponse.Data.documentFile
    Write-Host "✅ Data record created with file upload!" -ForegroundColor Green
    Write-Host "   Record ID: $($dataResponse.Data.__dataId)" -ForegroundColor Gray
    Write-Host "   File Path: $filePath" -ForegroundColor Gray

    # Step 2: Test with array file field (attachments)
    Write-Host "`nStep 2: Create data record with array file field (attachments)" -ForegroundColor Yellow

    $dataBody2 = @{
        title = "Test Document with Multiple Attachments"
        documentFile = @{
            content = $pdfBase64
            folder = "books/files"
            useCompression = $true
            useEncryption = $true
        }
        attachments = @(
            @{
                content = $pdfBase64
                folder = "books/file_attachs"
                useCompression = $true
                useEncryption = $true
            },
            @{
                content = $pdfBase64
                folder = "books/file_attachs"
                useCompression = $false
                useEncryption = $false
            }
        )
        amount = 5000.0
    } | ConvertTo-Json -Depth 10

    try {
        $dataResponse2 = Invoke-RestMethod -Uri "$baseUrl/api/v1/data/@test_files" `
            -Method POST `
            -Headers @{
                "Authorization" = "Bearer $token"
                "Content-Type" = "application/json"
            } `
            -Body $dataBody2

        Write-Host "✅ Data record with attachments created!" -ForegroundColor Green
        Write-Host "   Record ID: $($dataResponse2.Data.__dataId)" -ForegroundColor Gray
        Write-Host "   Document File: $($dataResponse2.Data.documentFile)" -ForegroundColor Gray
        Write-Host "   Attachments Count: $(($dataResponse2.Data.attachments).Count)" -ForegroundColor Gray
    }
    catch {
        Write-Host "❌ Failed to create record with attachments: $($_.Exception.Message)" -ForegroundColor Red
        if ($_.ErrorDetails.Message) {
            Write-Host "   Details: $($_.ErrorDetails.Message)" -ForegroundColor Red
        }
        if ($_.Exception.Response) {
            $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
            $responseBody = $reader.ReadToEnd()
            Write-Host "   Response: $responseBody" -ForegroundColor Red
        }
        throw
    }

    # Step 3: Download file
    Write-Host "`nStep 3: Download file" -ForegroundColor Yellow

    $downloadResponse = Invoke-WebRequest -Uri "$baseUrl/api/v1/files/download?filePath=$([System.Web.HttpUtility]::UrlEncode($filePath))" `
        -Method GET `
        -Headers @{
            "Authorization" = "Bearer $token"
        } `
 `
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
