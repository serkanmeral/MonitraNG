[System.Net.ServicePointManager]::ServerCertificateValidationCallback = {$true}

$token = Get-Content "$env:TEMP\serkan_token.txt" -Raw | ForEach-Object { $_.Trim() }
$baseUrl = "http://localhost:5010"
$headers = @{
    "Authorization" = "Bearer $token"
    "Content-Type" = "application/json"
}

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
            mandatory = $false
            isArray = $false
        },
        @{
            fieldType = "file"
            name = "attachments"
            title = "Attachments"
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
        create = @{
            groups = @("admins", "users")
        }
        read = @{
            groups = @("admins", "users")
        }
        update = @{
            groups = @("admins", "users")
        }
        delete = @{
            groups = @("admins")
        }
    }
} | ConvertTo-Json -Depth 10

Write-Host "Creating @test_files dataset..." -ForegroundColor Yellow

try {
    $response = Invoke-RestMethod -Uri "$baseUrl/api/v1/datasets" -Method POST -Headers $headers -Body $testFilesSchema
    Write-Host "✅ Dataset created!" -ForegroundColor Green
    
    # Response format kontrolü
    if ($response.Data) {
        Write-Host "   Name: $($response.Data.Name)" -ForegroundColor Gray
        Write-Host "   Data ID: $($response.Data.DataId)" -ForegroundColor Gray
        Write-Host "   Fields: $($response.Data.Fields.Count)" -ForegroundColor Gray
        $fileFields = $response.Data.Fields | Where-Object { $_.fieldType -eq "file" }
        Write-Host "   File Fields: $($fileFields.Count)" -ForegroundColor Cyan
        foreach ($field in $fileFields) {
            $arrayInfo = if ($field.isArray) { "Array" } else { "Single" }
            Write-Host "     - $($field.name) [$arrayInfo]" -ForegroundColor Gray
        }
    } else {
        Write-Host "   Response: $($response | ConvertTo-Json -Depth 5)" -ForegroundColor Gray
    }
}
catch {
    Write-Host "❌ Error: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.ErrorDetails.Message) {
        Write-Host "   Details: $($_.ErrorDetails.Message)" -ForegroundColor Gray
        try {
            $errorObj = $_.ErrorDetails.Message | ConvertFrom-Json
            if ($errorObj.Error) {
                Write-Host "   Error Code: $($errorObj.Error.Code)" -ForegroundColor Yellow
                Write-Host "   Error Message: $($errorObj.Error.Message)" -ForegroundColor Yellow
            }
        } catch {
            Write-Host "   Raw Error: $($_.ErrorDetails.Message)" -ForegroundColor Gray
        }
    }
    if ($_.Exception.Response) {
        $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
        $responseBody = $reader.ReadToEnd()
        Write-Host "   Response Body: $responseBody" -ForegroundColor Gray
    }
}
