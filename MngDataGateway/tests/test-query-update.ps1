# Test Query Update Script
# Updates @books dataset with queries to test serialization fix

$baseUrl = "https://localhost:5010"

# Token dosyasının yolunu belirle
$tokenFile = "$env:TEMP\serkan_token.txt"

# Token'ı kontrol et
if (-not (Test-Path $tokenFile)) {
    Write-Host "❌ Token bulunamadı! Önce token almak için:" -ForegroundColor Red
    Write-Host "   .\get-serkan-token.ps1" -ForegroundColor Yellow
    exit 1
}

# Token'ı oku
$token = Get-Content $tokenFile -Raw
$token = $token.Trim()

$headers = @{
    "Authorization" = "Bearer $token"
    "Content-Type" = "application/json"
}

# Ignore SSL certificate errors
[System.Net.ServicePointManager]::ServerCertificateValidationCallback = {$true}

Write-Host "`n🧪 Testing Query Update for @books dataset...`n" -ForegroundColor Cyan

# Create query definition with proper PowerShell escaping
$queryData = @{
    Queries = @(
        @{
            name = "books_by_publication_date_range"
            description = "Get books published between two dates"
            parameters = @("startDate", "endDate")
            pipeline = @(
                @{
                    "`$match" = @{
                        publicationDate = @{
                            "`$gte" = ":startDate"
                            "`$lte" = ":endDate"
                        }
                    }
                },
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
Write-Host $queryData -ForegroundColor Gray
Write-Host ""

try {
    $response = Invoke-RestMethod -Uri "$baseUrl/api/datasets/@books" -Method PUT -Headers $headers -Body $queryData -SkipCertificateCheck
    Write-Host "✅ @books dataset updated successfully with queries!" -ForegroundColor Green
    Write-Host "   - Queries: $($response.QueriesCount)" -ForegroundColor Gray
} catch {
    Write-Host "❌ Failed to update @books: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.ErrorDetails.Message) {
        Write-Host "Details: $($_.ErrorDetails.Message)" -ForegroundColor Gray
    }
}

