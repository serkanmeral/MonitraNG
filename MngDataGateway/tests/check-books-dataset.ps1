# Check tst_books dataset for queries and indexes
# This script verifies that queries and index definitions are correctly saved

$tokenFile = "$env:TEMP\serkan_token.txt"
if (-not (Test-Path $tokenFile)) {
    Write-Host "❌ Token file not found. Please run get-serkan-token.ps1 first." -ForegroundColor Red
    exit 1
}

$token = Get-Content $tokenFile -Raw | ForEach-Object { $_.Trim() }
$baseUrl = "https://localhost:5010/api/datasets/tst_books"

# Skip certificate validation for localhost
[System.Net.ServicePointManager]::ServerCertificateValidationCallback = {$true}

Write-Host "🔍 Checking tst_books dataset for queries and indexes..." -ForegroundColor Yellow
Write-Host ""

try {
    $headers = @{
        "Authorization" = "Bearer $token"
        "Content-Type" = "application/json"
    }

    $response = Invoke-RestMethod -Uri $baseUrl -Method Get -Headers $headers -SkipCertificateCheck
    
    Write-Host "✅ Dataset retrieved successfully!" -ForegroundColor Green
    Write-Host ""
    
    # Check Queries
    Write-Host "📋 Queries:" -ForegroundColor Cyan
    if ($response.queries -and $response.queries.Count -gt 0) {
        Write-Host "   Found $($response.queries.Count) query(ies):" -ForegroundColor Green
        foreach ($query in $response.queries) {
            Write-Host "   - Name: $($query.name)" -ForegroundColor White
            Write-Host "     Description: $($query.description)" -ForegroundColor Gray
            Write-Host "     Parameters: $($query.parameters -join ', ')" -ForegroundColor Gray
            if ($query.pipeline) {
                Write-Host "     Pipeline stages: $($query.pipeline.Count)" -ForegroundColor Gray
                # Show first stage as example
                if ($query.pipeline.Count -gt 0) {
                    $firstStage = $query.pipeline[0] | ConvertTo-Json -Depth 3 -Compress
                    Write-Host "     First stage: $firstStage" -ForegroundColor DarkGray
                }
            } else {
                Write-Host "     Pipeline: null or empty" -ForegroundColor Yellow
            }
            Write-Host ""
        }
    } else {
        Write-Host "   ⚠️  No queries found!" -ForegroundColor Yellow
    }
    
    Write-Host ""
    
    # Check Indexes
    Write-Host "📇 Indexes:" -ForegroundColor Cyan
    if ($response.indexList -and $response.indexList.Count -gt 0) {
        Write-Host "   Found $($response.indexList.Count) index(es):" -ForegroundColor Green
        foreach ($index in $response.indexList) {
            Write-Host "   - Name: $($index.name)" -ForegroundColor White
            Write-Host "     Unique: $($index.unique)" -ForegroundColor Gray
            $fieldsStr = ($index.fields.PSObject.Properties | ForEach-Object { "$($_.Name): $($_.Value)" }) -join ", "
            Write-Host "     Fields: $fieldsStr" -ForegroundColor Gray
            Write-Host ""
        }
    } else {
        Write-Host "   ⚠️  No indexes found!" -ForegroundColor Yellow
    }
    
    Write-Host ""
    
    # Check Fields
    Write-Host "📝 Fields:" -ForegroundColor Cyan
    if ($response.fields -and $response.fields.Count -gt 0) {
        Write-Host "   Found $($response.fields.Count) field(s)" -ForegroundColor Green
        # Check if "name" field exists
        $nameField = $response.fields | Where-Object { $_.name -eq "name" }
        if ($nameField) {
            Write-Host "   ✅ 'name' field found" -ForegroundColor Green
        } else {
            Write-Host "   ⚠️  'name' field NOT found!" -ForegroundColor Yellow
        }
    }
    
    Write-Host ""
    Write-Host "✅ Verification complete!" -ForegroundColor Green
    
} catch {
    Write-Host "❌ Error: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.ErrorDetails.Message) {
        Write-Host "Details: $($_.ErrorDetails.Message)" -ForegroundColor Gray
    }
}

