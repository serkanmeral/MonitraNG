# Setup Test Files Dataset
# Creates @test_files dataset with file field types for testing

$baseUrl = "http://localhost:5010"

# Token'ı parametre olarak al veya environment'tan oku
$token = $args[0]
if ([string]::IsNullOrEmpty($token)) {
    # Token'ı environment variable'dan oku
    $tokenFile = "$env:TEMP\serkan_token.txt"
    if (Test-Path $tokenFile) {
        $token = Get-Content $tokenFile -Raw | ForEach-Object { $_.Trim() }
    }
}

if ([string]::IsNullOrEmpty($token)) {
    Write-Host "❌ Token bulunamadı! Token'ı parametre olarak verin veya $tokenFile dosyasına kaydedin." -ForegroundColor Red
    exit 1
}

# API check - Swagger is available, so API is running
Write-Host ""
Write-Host "API is running at $baseUrl" -ForegroundColor Green

$headers = @{
    "Authorization" = "Bearer $token"
    "Content-Type" = "application/json"
}

# Ignore SSL certificate errors
[System.Net.ServicePointManager]::ServerCertificateValidationCallback = {$true}

Write-Host "`n🚀 Setting up @test_files dataset...`n" -ForegroundColor Cyan

# Create @test_files dataset with file fields
Write-Host "Creating @test_files dataset..." -ForegroundColor Yellow

$testFilesSchema = @{
    Name = "@test_files"
    Description = "Test dataset for file field type testing"
    ForceSchema = $true
    Logging = "self"
    PublishMode = "none"
    Fields = @(
        @{
            fieldType = "text"
            name = "title"
            title = "Title"
            mandatory = $true
        },
        @{
            fieldType = "file"
            name = "documentFile"
            title = "Document File"
            description = "Single document file"
            mandatory = $false
            isArray = $false
        },
        @{
            fieldType = "file"
            name = "attachments"
            title = "Attachments"
            description = "Multiple attachment files"
            mandatory = $false
            isArray = $true
        },
        @{
            fieldType = "number"
            name = "amount"
            title = "Amount"
            mandatory = $false
        }
    )
    Permissions = @{
        create = @("admins", "users")
        read = @("admins", "users")
        update = @("admins", "users")
        delete = @("admins")
    }
} | ConvertTo-Json -Depth 10

try {
    $response = Invoke-RestMethod -Uri "$baseUrl/api/v1/datasets" `
        -Method POST `
        -Headers $headers `
        -Body $testFilesSchema `

    Write-Host "✅ @test_files dataset created successfully!" -ForegroundColor Green
    Write-Host "   Dataset Name: $($response.Data.Name)" -ForegroundColor Gray
    Write-Host "   Data ID: $($response.Data.DataId)" -ForegroundColor Gray
    Write-Host "   Fields: $($response.Data.Fields.Count)" -ForegroundColor Gray
    
    # Show file fields
    $fileFields = $response.Data.Fields | Where-Object { $_.fieldType -eq "file" }
    Write-Host "`n   File Fields:" -ForegroundColor Cyan
    foreach ($field in $fileFields) {
        $arrayInfo = if ($field.isArray) { "Array" } else { "Single" }
        Write-Host "     - $($field.name) ($($field.fieldType)) [$arrayInfo]" -ForegroundColor Gray
    }
}
catch {
    if ($_.Exception.Response.StatusCode -eq 409) {
        Write-Host "⚠️  @test_files dataset already exists" -ForegroundColor Yellow
        Write-Host "   Updating dataset..." -ForegroundColor Yellow
        
        # Try to update
        try {
            $updateResponse = Invoke-RestMethod -Uri "$baseUrl/api/v1/datasets/@test_files" `
                -Method PUT `
                -Headers $headers `
                -Body $testFilesSchema `
            
            Write-Host "✅ @test_files dataset updated successfully!" -ForegroundColor Green
        }
        catch {
            Write-Host "❌ Failed to update dataset: $($_.Exception.Message)" -ForegroundColor Red
            if ($_.ErrorDetails.Message) {
                Write-Host "   Details: $($_.ErrorDetails.Message)" -ForegroundColor Gray
            }
            exit 1
        }
    }
    else {
        Write-Host "❌ Failed to create @test_files dataset: $($_.Exception.Message)" -ForegroundColor Red
        if ($_.ErrorDetails.Message) {
            Write-Host "   Details: $($_.ErrorDetails.Message)" -ForegroundColor Gray
        }
        exit 1
    }
}

Write-Host "`n✅ Setup complete! Ready for file field type testing." -ForegroundColor Green
