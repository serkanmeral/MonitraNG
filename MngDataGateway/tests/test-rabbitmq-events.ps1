# RabbitMQ Event Test Script
# Bu script, RabbitMQ'ya event'lerin publish edilip edilmediğini kontrol eder

param(
    [string]$BaseUrl = "https://localhost:5010",
    [string]$RabbitMqManagementUrl = "http://localhost:15672",
    [string]$RabbitMqUser = "admin",
    [string]$RabbitMqPass = "admin123",
    [string]$Domain = "seven"
)

# Colors
$ErrorColor = "Red"
$SuccessColor = "Green"
$InfoColor = "Cyan"
$WarningColor = "Yellow"

Write-Host "`n========================================" -ForegroundColor $InfoColor
Write-Host "RabbitMQ Event Test Script" -ForegroundColor $InfoColor
Write-Host "========================================`n" -ForegroundColor $InfoColor

# 1. Token kontrolü
Write-Host "1. Token kontrolü..." -ForegroundColor $InfoColor
$tokenFile = "$env:TEMP\serkan_token.txt"
if (-not (Test-Path $tokenFile)) {
    Write-Host "   Token bulunamadı. Token alınıyor..." -ForegroundColor $WarningColor
    $tokenScript = "C:\Serkan\iSIM\MonitraNG\MngKeeper\tests\get-serkan-token.ps1"
    if (Test-Path $tokenScript) {
        & $tokenScript
    } else {
        Write-Host "   ERROR: Token script bulunamadı: $tokenScript" -ForegroundColor $ErrorColor
        exit 1
    }
}

$token = Get-Content $tokenFile -ErrorAction SilentlyContinue
if (-not $token) {
    Write-Host "   ERROR: Token okunamadı!" -ForegroundColor $ErrorColor
    exit 1
}
Write-Host "   ✓ Token okundu" -ForegroundColor $SuccessColor

# 2. RabbitMQ Management API kontrolü
Write-Host "`n2. RabbitMQ Management API kontrolü..." -ForegroundColor $InfoColor
$exchangeName = "monitra.data.events.$Domain"

try {
    $authHeader = [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes("${RabbitMqUser}:${RabbitMqPass}"))
    $headers = @{
        "Authorization" = "Basic $authHeader"
    }
    
    # Exchange bilgilerini al
    $exchangeUrl = "$RabbitMqManagementUrl/api/exchanges/%2F/$([System.Web.HttpUtility]::UrlEncode($exchangeName))"
    $exchangeResponse = Invoke-RestMethod -Uri $exchangeUrl -Headers $headers -Method Get -ErrorAction Stop
    
    Write-Host "   ✓ Exchange bulundu: $exchangeName" -ForegroundColor $SuccessColor
    Write-Host "     Type: $($exchangeResponse.type)" -ForegroundColor Gray
    Write-Host "     Durable: $($exchangeResponse.durable)" -ForegroundColor Gray
    
    # Exchange'deki message sayısını kontrol et (topic exchange'lerde direkt mesaj sayısı yok)
    # Bunun yerine bindings ve queues kontrol edilir
} catch {
    if ($_.Exception.Response.StatusCode -eq 404) {
        Write-Host "   ⚠ Exchange henüz oluşturulmamış: $exchangeName" -ForegroundColor $WarningColor
        Write-Host "     (İlk event publish edildiğinde otomatik oluşturulacak)" -ForegroundColor Gray
    } else {
        Write-Host "   ERROR: RabbitMQ Management API'ye erişilemedi" -ForegroundColor $ErrorColor
        Write-Host "   Hata: $($_.Exception.Message)" -ForegroundColor $ErrorColor
        Write-Host "   URL: $RabbitMqManagementUrl" -ForegroundColor Gray
    }
}

# 3. Test data oluştur (event trigger etmek için)
Write-Host "`n3. Test data oluşturuluyor (event trigger için)..." -ForegroundColor $InfoColor

$datasetName = "@test_tasks_224334"
$testData = @{
    title = "RabbitMQ Test Task - $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
    description = "Bu task RabbitMQ event testi için oluşturuldu"
    priority = 1
    isCompleted = $false
} | ConvertTo-Json

try {
    $createHeaders = @{
        "Authorization" = "Bearer $token"
        "Content-Type" = "application/json"
    }
    
    $createUrl = "$BaseUrl/api/data/$datasetName"
    $createResponse = Invoke-RestMethod -Uri $createUrl -Headers $createHeaders -Method Post -Body $testData -SkipCertificateCheck -ErrorAction Stop
    
    $dataId = $createResponse.data.__dataId
    Write-Host "   ✓ Test data oluşturuldu" -ForegroundColor $SuccessColor
    Write-Host "     Data ID: $dataId" -ForegroundColor Gray
    Write-Host "     Task Number: $($createResponse.data.taskNumber)" -ForegroundColor Gray
    
    # Kısa bir bekleme (event publish için)
    Start-Sleep -Seconds 2
    
} catch {
    Write-Host "   ERROR: Test data oluşturulamadı" -ForegroundColor $ErrorColor
    Write-Host "   Hata: $($_.Exception.Message)" -ForegroundColor $ErrorColor
    if ($_.Exception.Response) {
        $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
        $responseBody = $reader.ReadToEnd()
        Write-Host "   Response: $responseBody" -ForegroundColor Gray
    }
    exit 1
}

# 4. Exchange'i tekrar kontrol et
Write-Host "`n4. Exchange kontrolü (event sonrası)..." -ForegroundColor $InfoColor
Start-Sleep -Seconds 1

try {
    $exchangeResponse = Invoke-RestMethod -Uri $exchangeUrl -Headers $headers -Method Get -ErrorAction Stop
    Write-Host "   ✓ Exchange mevcut: $exchangeName" -ForegroundColor $SuccessColor
} catch {
    Write-Host "   ⚠ Exchange hala oluşturulmamış" -ForegroundColor $WarningColor
    Write-Host "     Bu normal olabilir - event publish edilirken oluşturulur" -ForegroundColor Gray
}

# 5. RabbitMQ Management UI'dan queue kontrolü
Write-Host "`n5. RabbitMQ Queue kontrolü..." -ForegroundColor $InfoColor
Write-Host "   Management UI: $RabbitMqManagementUrl" -ForegroundColor Gray
Write-Host "   Exchange: $exchangeName" -ForegroundColor Gray
Write-Host "   Routing Key Pattern: dataset.$datasetName.*" -ForegroundColor Gray
Write-Host ""
Write-Host "   Manuel kontrol için:" -ForegroundColor $InfoColor
Write-Host "   1. Browser'da aç: $RabbitMqManagementUrl" -ForegroundColor Gray
Write-Host "   2. Login: $RabbitMqUser / $RabbitMqPass" -ForegroundColor Gray
Write-Host "   3. Exchanges sekmesine git" -ForegroundColor Gray
Write-Host "   4. '$exchangeName' exchange'ini bul" -ForegroundColor Gray
Write-Host "   5. Exchange'e tıkla → 'Bindings' sekmesine git" -ForegroundColor Gray
Write-Host "   6. Routing key'leri kontrol et: dataset.$datasetName.created" -ForegroundColor Gray

# 6. MongoDB notification_errors kontrolü
Write-Host "`n6. MongoDB Notification Errors kontrolü..." -ForegroundColor $InfoColor
Write-Host "   Collection: monitra_system.@notification_errors" -ForegroundColor Gray
Write-Host "   Bu collection'da failed event'ler loglanır" -ForegroundColor Gray
Write-Host ""
Write-Host "   MongoDB'de kontrol:" -ForegroundColor $InfoColor
Write-Host "   use monitra_system" -ForegroundColor Gray
Write-Host "   db['@notification_errors'].find().sort({timestamp: -1}).limit(5).pretty()" -ForegroundColor Gray

# 7. Application log kontrolü
Write-Host "`n7. Application Log kontrolü..." -ForegroundColor $InfoColor
Write-Host "   Log'larda şu mesajları arayın:" -ForegroundColor Gray
Write-Host "   - 'Event published successfully'" -ForegroundColor Gray
Write-Host "   - 'Exchange {ExchangeName} declared successfully'" -ForegroundColor Gray
Write-Host "   - 'Failed to publish event' (hata varsa)" -ForegroundColor Gray

# 8. Özet
Write-Host "`n========================================" -ForegroundColor $InfoColor
Write-Host "Özet" -ForegroundColor $InfoColor
Write-Host "========================================" -ForegroundColor $InfoColor
Write-Host ""
Write-Host "Event'lerin publish edildiğini kontrol etmek için:" -ForegroundColor $InfoColor
Write-Host ""
Write-Host "1. RabbitMQ Management UI:" -ForegroundColor $SuccessColor
Write-Host "   - URL: $RabbitMqManagementUrl" -ForegroundColor Gray
Write-Host "   - Exchange: $exchangeName" -ForegroundColor Gray
Write-Host "   - Routing Keys: dataset.$datasetName.created" -ForegroundColor Gray
Write-Host ""
Write-Host "2. Application Logs:" -ForegroundColor $SuccessColor
Write-Host "   - 'Event published successfully' mesajını arayın" -ForegroundColor Gray
Write-Host ""
Write-Host "3. MongoDB:" -ForegroundColor $SuccessColor
Write-Host "   - monitra_system.@notification_errors collection" -ForegroundColor Gray
Write-Host "   - Hata varsa burada loglanır" -ForegroundColor Gray
Write-Host ""
Write-Host "4. RabbitMQ CLI (opsiyonel):" -ForegroundColor $SuccessColor
Write-Host "   rabbitmqctl list_exchanges | grep monitra" -ForegroundColor Gray
Write-Host "   rabbitmqctl list_bindings | grep $exchangeName" -ForegroundColor Gray
Write-Host ""

