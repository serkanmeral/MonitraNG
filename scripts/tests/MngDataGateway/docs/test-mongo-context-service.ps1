# MongoContextService Test Script
# Bu script MongoContextService'in çalışıp çalışmadığını test eder

Write-Host "======================================" -ForegroundColor Cyan
Write-Host "MongoContextService Test Suite" -ForegroundColor Cyan
Write-Host "======================================" -ForegroundColor Cyan
Write-Host ""

# Base URL
$baseUrl = "https://localhost:5010"

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
Write-Host "✅ Token yüklendi: $($token.Substring(0, [Math]::Min(50, $token.Length)))..." -ForegroundColor Green
Write-Host ""

# Headers
$headers = @{
    "Authorization" = "Bearer $token"
    "Content-Type" = "application/json"
}

# PowerShell 7+ için SSL sertifika doğrulamasını atla
Write-Host "⚠️  SSL sertifika kontrolü devre dışı (development)" -ForegroundColor Yellow
Write-Host ""

# Test fonksiyonu
function Test-Endpoint {
    param(
        [string]$Name,
        [string]$Url,
        [hashtable]$Headers
    )
    
    Write-Host "🧪 Test: $Name" -ForegroundColor Yellow
    Write-Host "   URL: $Url" -ForegroundColor Gray
    
    try {
        $response = Invoke-RestMethod -Uri $Url -Headers $Headers -Method Get -SkipCertificateCheck -ErrorAction Stop
        Write-Host "   ✅ Başarılı!" -ForegroundColor Green
        Write-Host "   📦 Response:" -ForegroundColor Gray
        $response | ConvertTo-Json -Depth 5 | Write-Host
        Write-Host ""
        return $true
    }
    catch {
        Write-Host "   ❌ Hata: $($_.Exception.Message)" -ForegroundColor Red
        if ($_.ErrorDetails.Message) {
            Write-Host "   📦 Details:" -ForegroundColor Gray
            $_.ErrorDetails.Message | Write-Host
        }
        Write-Host ""
        return $false
    }
}

# Testleri çalıştır
Write-Host "🚀 Testler başlıyor..." -ForegroundColor Cyan
Write-Host ""

$results = @()

# Test 1: Health Check
$results += Test-Endpoint `
    -Name "Health Check" `
    -Url "$baseUrl/api/mongocontexttest/health" `
    -Headers $headers

# Test 2: Context Info
$results += Test-Endpoint `
    -Name "Context Info (Domain & User)" `
    -Url "$baseUrl/api/mongocontexttest/info" `
    -Headers $headers

# Test 3: Database Info
$results += Test-Endpoint `
    -Name "Database Info" `
    -Url "$baseUrl/api/mongocontexttest/database" `
    -Headers $headers

# Test 4: Datasets Collection
$results += Test-Endpoint `
    -Name "Datasets Collection" `
    -Url "$baseUrl/api/mongocontexttest/datasets-collection" `
    -Headers $headers

# Test 5: Database by Domain (Admin only)
$results += Test-Endpoint `
    -Name "Database by Domain (seven)" `
    -Url "$baseUrl/api/mongocontexttest/database/seven" `
    -Headers $headers

# Sonuçları özetle
Write-Host "======================================" -ForegroundColor Cyan
Write-Host "Test Sonuçları" -ForegroundColor Cyan
Write-Host "======================================" -ForegroundColor Cyan
$passed = ($results | Where-Object { $_ -eq $true }).Count
$total = $results.Count
Write-Host "Başarılı: $passed / $total" -ForegroundColor $(if ($passed -eq $total) { "Green" } else { "Yellow" })

if ($passed -eq $total) {
    Write-Host ""
    Write-Host "🎉 Tüm testler başarılı!" -ForegroundColor Green
    Write-Host "   MongoContextService düzgün çalışıyor!" -ForegroundColor Green
} else {
    Write-Host ""
    Write-Host "⚠️ Bazı testler başarısız oldu." -ForegroundColor Yellow
}

Write-Host ""

