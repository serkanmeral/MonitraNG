# Translation Test Script
# Tests the MngLLM translation endpoint

param(
    [string]$LLMBaseUrl = "http://localhost:5030",
    [string]$TokenFile = "$env:TEMP\serkan_token.txt"
)

# Comprehensive SSL/TLS fixes
[System.Net.ServicePointManager]::ServerCertificateValidationCallback = {$true}

# Enable all TLS protocols
try {
    [System.Net.ServicePointManager]::SecurityProtocol = [System.Net.ServicePointManager]::SecurityProtocol -bor [System.Net.SecurityProtocolType]::Tls12
} catch {
    try {
        [System.Net.ServicePointManager]::SecurityProtocol = [System.Net.SecurityProtocolType]::Tls12
    } catch {
        # Use default if TLS12 not available
    }
}

Write-Host ""
Write-Host "🧪 MngLLM Translation Test" -ForegroundColor Cyan
Write-Host "   LLM URL: $LLMBaseUrl" -ForegroundColor Gray
Write-Host ""

# Load token
if (-not (Test-Path $TokenFile)) {
    Write-Host "❌ Token dosyası bulunamadı: $TokenFile" -ForegroundColor Red
    Write-Host "   Lütfen önce token alın (scripts/tests/MngKeeper/auth/get-token.ps1)" -ForegroundColor Yellow
    exit 1
}

$token = Get-Content $TokenFile -Raw | ForEach-Object { $_.Trim() }
if ([string]::IsNullOrEmpty($token)) {
    Write-Host "❌ Token boş!" -ForegroundColor Red
    exit 1
}

Write-Host "✅ Token yüklendi" -ForegroundColor Green
Write-Host ""

# Test cases
$testCases = @(
    @{
        Name = "Basit Kelime: Kitaplar"
        Text = "Kitaplar"
        SourceLanguage = "tr"
        TargetLanguages = @("en", "fr", "ar", "zh")
    },
    @{
        Name = "Kelime Grubu: Dataset Yönetimi"
        Text = "Dataset Yönetimi"
        SourceLanguage = "tr"
        TargetLanguages = @("en", "fr", "ar", "zh")
    },
    @{
        Name = "Kelime Grubu: Kullanıcı Yönetimi"
        Text = "Kullanıcı Yönetimi"
        SourceLanguage = "tr"
        TargetLanguages = @("en", "fr", "ar", "zh")
    },
    @{
        Name = "Cümle: Sistem Ayarları"
        Text = "Sistem Ayarları"
        SourceLanguage = "tr"
        TargetLanguages = @("en", "fr", "ar", "zh")
    }
)

$headers = @{
    "Authorization" = "Bearer $token"
    "Content-Type" = "application/json"
}

$successCount = 0
$failCount = 0

foreach ($testCase in $testCases) {
    Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Gray
    Write-Host "Test: $($testCase.Name)" -ForegroundColor Yellow
    Write-Host "  Text: $($testCase.Text)" -ForegroundColor Gray
    Write-Host "  Source: $($testCase.SourceLanguage)" -ForegroundColor Gray
    Write-Host "  Targets: $($testCase.TargetLanguages -join ', ')" -ForegroundColor Gray
    Write-Host ""
    
    try {
        $requestBody = @{
            text = $testCase.Text
            sourceLanguage = $testCase.SourceLanguage
            targetLanguages = $testCase.TargetLanguages
        } | ConvertTo-Json
        
        $params = @{
            Uri = "$LLMBaseUrl/api/v1/llm/translate"
            Method = "POST"
            Headers = $headers
            Body = $requestBody
            ContentType = "application/json"
            ErrorAction = "Stop"
            TimeoutSec = 90
        }
        
        $startTime = Get-Date
        $response = Invoke-RestMethod @params
        $endTime = Get-Date
        $duration = ($endTime - $startTime).TotalSeconds
        
        Write-Host "✅ Başarılı! (Süre: $([math]::Round($duration, 2))s)" -ForegroundColor Green
        Write-Host ""
        Write-Host "Çeviri Sonuçları:" -ForegroundColor Cyan
        
        if ($response.translations) {
            foreach ($targetLang in $testCase.TargetLanguages) {
                $translatedText = $response.translations[$targetLang]
                if ($translatedText) {
                    Write-Host "  $targetLang : $translatedText" -ForegroundColor White
                } else {
                    Write-Host "  $targetLang : (çeviri bulunamadı)" -ForegroundColor Yellow
                }
            }
        } else {
            Write-Host "  (çeviri sonuçları bulunamadı)" -ForegroundColor Yellow
        }
        
        if ($response.model) {
            Write-Host ""
            Write-Host "Model: $($response.model)" -ForegroundColor Gray
        }
        
        $successCount++
    } catch {
        Write-Host "❌ Hata: $($_.Exception.Message)" -ForegroundColor Red
        if ($_.ErrorDetails) {
            Write-Host "   Detay: $($_.ErrorDetails.Message)" -ForegroundColor Yellow
        }
        $failCount++
    }
    
    Write-Host ""
    Start-Sleep -Milliseconds 500
}

Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Gray
Write-Host ""
Write-Host "📊 Test Özeti:" -ForegroundColor Cyan
Write-Host "  ✅ Başarılı: $successCount" -ForegroundColor Green
Write-Host "  ❌ Başarısız: $failCount" -ForegroundColor Red
Write-Host "  📝 Toplam: $($testCases.Count)" -ForegroundColor Gray
Write-Host ""

if ($failCount -eq 0) {
    Write-Host "🎉 Tüm testler başarılı!" -ForegroundColor Green
    exit 0
} else {
    Write-Host "⚠️  Bazı testler başarısız oldu." -ForegroundColor Yellow
    exit 1
}
