# Test Persons & PersonGroups Field Types
# Tests persons and personGroups field type expansion

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

Write-Host "`n👥 Testing Persons & PersonGroups Field Types`n" -ForegroundColor Cyan
Write-Host "=" * 80 -ForegroundColor Gray
Write-Host ""

$testResults = @()

# Test 1: Check if persons/personGroups fields exist in schema
Write-Host "📋 Test 1: Check schema for persons/personGroups fields" -ForegroundColor Yellow
try {
    $url = "$baseUrl/api/datasets/$datasetName"
    
    $response = Invoke-RestMethod -Uri $url -Method GET -Headers $headers -SkipCertificateCheck
    $personsFields = $response.fields | Where-Object { $_.fieldType -eq "persons" }
    $personGroupsFields = $response.fields | Where-Object { $_.fieldType -eq "personGroups" }
    
    Write-Host "   ✅ Schema başarıyla alındı" -ForegroundColor Green
    Write-Host "   📊 Persons field'ları: $($personsFields.Count)" -ForegroundColor Cyan
    $personsFields | ForEach-Object {
        Write-Host "      - $($_.name) (isArray: $($_.isArray))" -ForegroundColor White
    }
    Write-Host "   📊 PersonGroups field'ları: $($personGroupsFields.Count)" -ForegroundColor Cyan
    $personGroupsFields | ForEach-Object {
        Write-Host "      - $($_.name) (isArray: $($_.isArray))" -ForegroundColor White
    }
    
    if ($personsFields.Count -eq 0 -and $personGroupsFields.Count -eq 0) {
        Write-Host "   ⚠️  Uyarı: Schema'da persons/personGroups field'ı bulunamadı!" -ForegroundColor Yellow
        Write-Host "      Test devam edecek, ancak expansion test edilemeyecek." -ForegroundColor Yellow
    }
    
    $testResults += @{ Test = "Schema check"; Status = "✅ Başarılı"; Error = $null }
} catch {
    $errorMsg = $_.Exception.Message
    Write-Host "   ❌ Hata: $errorMsg" -ForegroundColor Red
    $testResults += @{ Test = "Schema check"; Status = "❌ Hata"; Error = $errorMsg }
}
Write-Host ""

# Test 2: Query with expand=true (persons/personGroups should be expanded)
Write-Host "📋 Test 2: Query with expand=true (persons/personGroups expansion)" -ForegroundColor Yellow
try {
    $url = "$baseUrl/api/data/$datasetName" + "?expand=true&limit=3"
    
    $response = Invoke-RestMethod -Uri $url -Method GET -Headers $headers -SkipCertificateCheck
    $count = if ($response -is [Array]) { $response.Count } else { 0 }
    
    Write-Host "   ✅ Başarılı! $count kayıt bulundu" -ForegroundColor Green
    
    if ($count -gt 0) {
        $firstItem = $response[0]
        
        # Check for persons fields
        $personsFields = @("author", "coAuthors")
        Write-Host "   📊 Persons field kontrolü:" -ForegroundColor Cyan
        foreach ($fieldName in $personsFields) {
            if ($firstItem.PSObject.Properties.Name -contains $fieldName) {
                $fieldValue = $firstItem.$fieldName
                if ($fieldValue -ne $null) {
                    if ($fieldValue -is [Array]) {
                        Write-Host "      - ${fieldName}: ✅ Array (Count: $($fieldValue.Count))" -ForegroundColor Green
                        if ($fieldValue.Count -gt 0) {
                            $firstPerson = $fieldValue[0]
                            if ($firstPerson.username -or $firstPerson.email) {
                                Write-Host "         → Expanded: ✅ (username/email var)" -ForegroundColor Green
                            } else {
                                Write-Host "         → Expanded: ❌ (username/email yok)" -ForegroundColor Red
                            }
                        }
                    } else {
                        Write-Host "      - ${fieldName}: ✅ Single object" -ForegroundColor Green
                        if ($fieldValue.username -or $fieldValue.email) {
                            Write-Host "         → Expanded: ✅ (username/email var)" -ForegroundColor Green
                        } else {
                            Write-Host "         → Expanded: ❌ (username/email yok)" -ForegroundColor Red
                        }
                    }
                } else {
                    Write-Host "      - ${fieldName}: ⚠️  Null" -ForegroundColor Yellow
                }
            } else {
                Write-Host "      - ${fieldName}: ❌ Field yok" -ForegroundColor Red
            }
        }
        
        # Check for personGroups fields
        $personGroupsFields = @("reviewerGroups", "editorialTeam")
        Write-Host "   📊 PersonGroups field kontrolü:" -ForegroundColor Cyan
        foreach ($fieldName in $personGroupsFields) {
            if ($firstItem.PSObject.Properties.Name -contains $fieldName) {
                $fieldValue = $firstItem.$fieldName
                if ($fieldValue -ne $null) {
                    if ($fieldValue -is [Array]) {
                        Write-Host "      - ${fieldName}: ✅ Array (Count: $($fieldValue.Count))" -ForegroundColor Green
                        if ($fieldValue.Count -gt 0) {
                            $firstGroup = $fieldValue[0]
                            if ($firstGroup.name -or $firstGroup.description) {
                                Write-Host "         → Expanded: ✅ (name/description var)" -ForegroundColor Green
                            } else {
                                Write-Host "         → Expanded: ❌ (name/description yok)" -ForegroundColor Red
                            }
                        }
                    } else {
                        Write-Host "      - ${fieldName}: ✅ Single object" -ForegroundColor Green
                        if ($fieldValue.name -or $fieldValue.description) {
                            Write-Host "         → Expanded: ✅ (name/description var)" -ForegroundColor Green
                        } else {
                            Write-Host "         → Expanded: ❌ (name/description yok)" -ForegroundColor Red
                        }
                    }
                } else {
                    Write-Host "      - ${fieldName}: ⚠️  Null" -ForegroundColor Yellow
                }
            } else {
                Write-Host "      - ${fieldName}: ❌ Field yok" -ForegroundColor Red
            }
        }
        
        # Show sample data
        Write-Host "   📄 Örnek kayıt (ilk kayıt):" -ForegroundColor Cyan
        Write-Host "      - Title: $($firstItem.title)" -ForegroundColor White
        if ($firstItem.author) {
            if ($firstItem.author -is [Array]) {
                Write-Host "      - Author: $($firstItem.author.Count) kişi" -ForegroundColor White
            } else {
                Write-Host "      - Author: $($firstItem.author.username) ($($firstItem.author.email))" -ForegroundColor White
            }
        }
        if ($firstItem.reviewerGroups) {
            if ($firstItem.reviewerGroups -is [Array]) {
                Write-Host "      - Reviewer Groups: $($firstItem.reviewerGroups.Count) grup" -ForegroundColor White
            } else {
                Write-Host "      - Reviewer Groups: $($firstItem.reviewerGroups.name)" -ForegroundColor White
            }
        }
    }
    
    $testResults += @{ Test = "Expansion with expand=true"; Status = "✅ Başarılı"; Error = $null }
} catch {
    $errorMsg = $_.Exception.Message
    Write-Host "   ❌ Hata: $errorMsg" -ForegroundColor Red
    $testResults += @{ Test = "Expansion with expand=true"; Status = "❌ Hata"; Count = 0; Error = $errorMsg }
}
Write-Host ""

# Test 3: Query with expand=false (persons/personGroups should NOT be expanded)
Write-Host "📋 Test 3: Query with expand=false (persons/personGroups should NOT be expanded)" -ForegroundColor Yellow
try {
    $url = "$baseUrl/api/data/$datasetName" + "?expand=false&limit=3"
    
    $response = Invoke-RestMethod -Uri $url -Method GET -Headers $headers -SkipCertificateCheck
    $count = if ($response -is [Array]) { $response.Count } else { 0 }
    
    Write-Host "   ✅ Başarılı! $count kayıt bulundu" -ForegroundColor Green
    
    if ($count -gt 0) {
        $firstItem = $response[0]
        
        # Check if persons fields are NOT expanded (should be just IDs)
        $personsFields = @("author", "coAuthors")
        Write-Host "   📊 Persons field kontrolü (expand=false):" -ForegroundColor Cyan
        foreach ($fieldName in $personsFields) {
            if ($firstItem.PSObject.Properties.Name -contains $fieldName) {
                $fieldValue = $firstItem.$fieldName
                if ($fieldValue -ne $null) {
                    if ($fieldValue -is [Array]) {
                        $firstValue = $fieldValue[0]
                        if ($firstValue -is [String] -or $firstValue -is [PSCustomObject] -and $firstValue.__dataId) {
                            Write-Host "      - ${fieldName}: ✅ ID formatında (expanded değil)" -ForegroundColor Green
                        } else {
                            Write-Host "      - ${fieldName}: ⚠️  Expanded görünüyor (beklenen: ID)" -ForegroundColor Yellow
                        }
                    } else {
                        if ($fieldValue -is [String] -or ($fieldValue -is [PSCustomObject] -and $fieldValue.__dataId)) {
                            Write-Host "      - ${fieldName}: ✅ ID formatında (expanded değil)" -ForegroundColor Green
                        } else {
                            Write-Host "      - ${fieldName}: ⚠️  Expanded görünüyor (beklenen: ID)" -ForegroundColor Yellow
                        }
                    }
                } else {
                    Write-Host "      - ${fieldName}: ⚠️  Null" -ForegroundColor Yellow
                }
            } else {
                Write-Host "      - ${fieldName}: ❌ Field yok" -ForegroundColor Red
            }
        }
    }
    
    $testResults += @{ Test = "No expansion with expand=false"; Status = "✅ Başarılı"; Error = $null }
} catch {
    $errorMsg = $_.Exception.Message
    Write-Host "   ❌ Hata: $errorMsg" -ForegroundColor Red
    $testResults += @{ Test = "No expansion with expand=false"; Status = "❌ Hata"; Error = $errorMsg }
}
Write-Host ""

# Test 4: Query single item by ID with expansion
Write-Host "📋 Test 4: Query single item by ID with expansion" -ForegroundColor Yellow
try {
    # First get an ID
    $url = "$baseUrl/api/data/$datasetName" + "?limit=1"
    $listResponse = Invoke-RestMethod -Uri $url -Method GET -Headers $headers -SkipCertificateCheck
    
    if ($listResponse -is [Array] -and $listResponse.Count -gt 0) {
        $dataId = $listResponse[0].__dataId
        
        $url = "$baseUrl/api/data/$datasetName/$dataId" + "?expand=true"
        $response = Invoke-RestMethod -Uri $url -Method GET -Headers $headers -SkipCertificateCheck
        
        Write-Host "   ✅ Başarılı! Kayıt bulundu (ID: $dataId)" -ForegroundColor Green
        
        if ($response.author) {
            if ($response.author -is [Array]) {
                Write-Host "   📊 Author: $($response.author.Count) kişi (expanded)" -ForegroundColor Cyan
            } else {
                Write-Host "   📊 Author: $($response.author.username) (expanded)" -ForegroundColor Cyan
            }
        }
        
        $testResults += @{ Test = "Query by ID with expansion"; Status = "✅ Başarılı"; Error = $null }
    } else {
        Write-Host "   ⚠️  Test verisi bulunamadı" -ForegroundColor Yellow
        $testResults += @{ Test = "Query by ID with expansion"; Status = "⚠️  Skip"; Error = "No test data" }
    }
} catch {
    $errorMsg = $_.Exception.Message
    Write-Host "   ❌ Hata: $errorMsg" -ForegroundColor Red
    $testResults += @{ Test = "Query by ID with expansion"; Status = "❌ Hata"; Error = $errorMsg }
}
Write-Host ""

# Summary
Write-Host "=" * 80 -ForegroundColor Gray
Write-Host "`n📊 Test Özeti`n" -ForegroundColor Cyan

$successCount = ($testResults | Where-Object { $_.Status -eq "✅ Başarılı" }).Count
$failCount = ($testResults | Where-Object { $_.Status -eq "❌ Hata" }).Count
$skipCount = ($testResults | Where-Object { $_.Status -eq "⚠️  Skip" }).Count

Write-Host "Toplam Test: $($testResults.Count)" -ForegroundColor White
Write-Host "✅ Başarılı: $successCount" -ForegroundColor Green
Write-Host "❌ Hata: $failCount" -ForegroundColor Red
Write-Host "⚠️  Skip: $skipCount" -ForegroundColor Yellow
Write-Host ""

if ($failCount -gt 0) {
    Write-Host "Hata Detayları:" -ForegroundColor Yellow
    $testResults | Where-Object { $_.Status -eq "❌ Hata" } | ForEach-Object {
        Write-Host "  - $($_.Test): $($_.Error)" -ForegroundColor Red
    }
}

Write-Host "`n✅ Test tamamlandı!`n" -ForegroundColor Green

