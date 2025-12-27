# Test Simple Query Update Script
# Updates tst_books dataset with a simple query (only $sort, no parameters)

$baseUrl = "https://localhost:5010"
$datasetName = "tst_books"

# Token'ı yükle (ortak script kullanarak)
$scriptPath = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $PSCommandPath }
if ([string]::IsNullOrEmpty($scriptPath)) {
    $scriptPath = Get-Location
}
$loadTokenScript = Join-Path $scriptPath "..\auth\load-token.ps1"

if (-not (Test-Path $loadTokenScript)) {
    Write-Host "❌ load-token.ps1 bulunamadı! Path: $loadTokenScript" -ForegroundColor Red
    exit 1
}

$token = & $loadTokenScript

if ([string]::IsNullOrEmpty($token)) {
    Write-Host "❌ Token alınamadı! Testler durduruluyor." -ForegroundColor Red
    exit 1
}

$tokenFile = "$env:TEMP\serkan_token.txt"

$headers = @{
    "Authorization" = "Bearer $token"
    "Content-Type" = "application/json"
}

# Ignore SSL certificate errors
[System.Net.ServicePointManager]::ServerCertificateValidationCallback = {$true}

Write-Host "`n🧪 Testing Simple Query Update for $datasetName dataset...`n" -ForegroundColor Cyan

# Get current dataset schema
Write-Host "📋 Getting current dataset schema..." -ForegroundColor Yellow
try {
    $currentSchema = Invoke-RestMethod -Uri "$baseUrl/api/datasets/$datasetName" -Method GET -Headers $headers -SkipCertificateCheck
    
    # Update only the queries part - simple $sort only, no parameters
    $updateData = @{
        Queries = @(
            @{
                name = "books_by_publication_date_range"
                description = "Get books published between two dates"
                pipeline = @(
                    @{
                        "`$sort" = @{
                            publicationDate = -1
                            title = 1
                        }
                    }
                )
            }
        )
    } | ConvertTo-Json -Depth 20

    Write-Host "Query JSON:" -ForegroundColor Yellow
    Write-Host $updateData -ForegroundColor Gray
    Write-Host ""

    # Update dataset
    Write-Host "🔄 Updating dataset..." -ForegroundColor Yellow
    $response = Invoke-RestMethod -Uri "$baseUrl/api/datasets/$datasetName" -Method PUT -Headers $headers -Body $updateData -SkipCertificateCheck
    
    Write-Host "✅ Dataset updated successfully!" -ForegroundColor Green
    Write-Host "   - Dataset: $($response.Name)" -ForegroundColor Gray
    Write-Host "   - Queries: $($response.QueriesCount)" -ForegroundColor Gray
    Write-Host ""
    
    # Test the query
    Write-Host "🧪 Testing the query (no parameters needed)..." -ForegroundColor Cyan
    Write-Host ""
    
    $queryUrl = "$baseUrl/api/data/$datasetName/queries/books_by_publication_date_range"
    Write-Host "   URL: $queryUrl" -ForegroundColor DarkGray
    Write-Host "   Method: POST" -ForegroundColor DarkGray
    Write-Host "   Body: {} (empty - no parameters)" -ForegroundColor DarkGray
    Write-Host ""
    
    $queryResponse = Invoke-RestMethod -Uri $queryUrl -Method POST -Headers $headers -Body "{}" -SkipCertificateCheck
    
    if ($queryResponse -is [System.Array]) {
        $count = $queryResponse.Count
        Write-Host "   ✅ Query executed successfully! Found $count books" -ForegroundColor Green
        Write-Host ""
        
        if ($count -gt 0) {
            Write-Host "   📊 First 5 results:" -ForegroundColor Cyan
            $queryResponse | Select-Object -First 5 | ForEach-Object {
                $title = if ($_.title) { $_.title } else { "N/A" }
                $pubDate = if ($_.publicationDate) { $_.publicationDate } else { "N/A" }
                Write-Host "      - $title (Publication Date: $pubDate)" -ForegroundColor White
            }
        }
    } else {
        Write-Host "   ⚠️  Unexpected response format" -ForegroundColor Yellow
        $queryResponse | ConvertTo-Json -Depth 3
    }
    
} catch {
    Write-Host "❌ Error: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.ErrorDetails.Message) {
        Write-Host "Details: $($_.ErrorDetails.Message)" -ForegroundColor Gray
        try {
            $errorDetails = $_.ErrorDetails.Message | ConvertFrom-Json
            if ($errorDetails.error) {
                Write-Host "Error Code: $($errorDetails.error.code)" -ForegroundColor Gray
                Write-Host "Error Message: $($errorDetails.error.message)" -ForegroundColor Gray
                if ($errorDetails.error.details) {
                    Write-Host "Error Details: $($errorDetails.error.details)" -ForegroundColor Gray
                }
            }
        } catch {
            # JSON parse hatası, sadece mesajı göster
        }
    }
}

Write-Host ""
Write-Host "✅ Test completed!" -ForegroundColor Green

