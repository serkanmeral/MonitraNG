# Hızlı Test Scripti - Domain ve Kullanıcı Oluşturma
# HTML sayfasında bağlantı kurulduktan sonra çalıştırın

$MngKeeperUrl = "https://localhost:5001"
$TestDomainName = "test-signalr-$(Get-Date -Format 'yyyyMMddHHmmss')"

Write-Host "=== MngHub SignalR Hızlı Test ===" -ForegroundColor Cyan
Write-Host ""
Write-Host "NOT: HTML sayfasında bağlantı kurulmuş olmalı!" -ForegroundColor Yellow
Write-Host ""

# Skip certificate validation
[System.Net.ServicePointManager]::ServerCertificateValidationCallback = {$true}

Write-Host "1. Test domain'i oluşturuyorum..." -ForegroundColor Yellow

$domainBody = @{
    domainName = $TestDomainName
    displayName = "SignalR Test Domain"
    adminEmail = "admin@test.com"
    adminPassword = "AdminPass123!"
    settings = @{
        maxUsers = 100
        maxAssets = 1000
        enableMqtt = $false
    }
} | ConvertTo-Json

try {
    $domain = Invoke-RestMethod -Uri "$MngKeeperUrl/api/domain" `
        -Method POST `
        -Body $domainBody `
        -ContentType "application/json" `
        -SkipCertificateCheck `
        -ErrorAction Stop
    
    Write-Host "   ✓ Domain oluşturuldu: $($domain.domainName)" -ForegroundColor Green
    Write-Host "   → HTML sayfasında 'System Events' mesajı görmelisiniz!" -ForegroundColor Cyan
    Write-Host "   → Routing Key: system.mngkeeper.domain.created" -ForegroundColor Gray
    Write-Host ""
    Write-Host "   5 saniye bekliyorum..." -ForegroundColor Yellow
    Start-Sleep -Seconds 5
    
    $domainId = $domain.domainId
} catch {
    Write-Host "   ✗ Domain oluşturulamadı: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "2. Mapper'ları yapılandırıyorum..." -ForegroundColor Yellow
try {
    Invoke-RestMethod -Uri "$MngKeeperUrl/api/admin/realms/$TestDomainName/configure-mappers" `
        -Method POST `
        -SkipCertificateCheck `
        -ErrorAction Stop | Out-Null
    Write-Host "   ✓ Mapper'lar yapılandırıldı" -ForegroundColor Green
} catch {
    Write-Host "   ⚠ Mapper yapılandırılamadı (zaten yapılandırılmış olabilir)" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "3. Admin token alıyorum..." -ForegroundColor Yellow
$adminUsername = "$TestDomainName`_admin"
$tokenBody = @{
    username = $adminUsername
    password = "AdminPass123!"
    domain = $TestDomainName
} | ConvertTo-Json

try {
    $tokenResponse = Invoke-RestMethod -Uri "$MngKeeperUrl/api/auth/token" `
        -Method POST `
        -Body $tokenBody `
        -ContentType "application/json" `
        -SkipCertificateCheck `
        -ErrorAction Stop
    
    $accessToken = $tokenResponse.accessToken
    Write-Host "   ✓ Token alındı" -ForegroundColor Green
} catch {
    Write-Host "   ✗ Token alınamadı: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "4. Test kullanıcısı oluşturuyorum..." -ForegroundColor Yellow
$headers = @{
    "Authorization" = "Bearer $accessToken"
    "Content-Type" = "application/json"
}

$userBody = @{
    username = "testuser"
    email = "testuser@test.com"
    password = "TestPass123!"
    firstName = "Test"
    lastName = "User"
    groupIds = @()
    isActive = $true
} | ConvertTo-Json

try {
    $user = Invoke-RestMethod -Uri "$MngKeeperUrl/api/user" `
        -Method POST `
        -Headers $headers `
        -Body $userBody `
        -SkipCertificateCheck `
        -ErrorAction Stop
    
    Write-Host "   ✓ Kullanıcı oluşturuldu: $($user.username)" -ForegroundColor Green
    Write-Host "   → HTML sayfasında 'Domain Events' mesajı görmelisiniz!" -ForegroundColor Cyan
    Write-Host "   → Routing Key: $domainId.usercreatedevent" -ForegroundColor Gray
    Write-Host ""
} catch {
    Write-Host "   ✗ Kullanıcı oluşturulamadı: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "=== Test Tamamlandı ===" -ForegroundColor Green
Write-Host ""
Write-Host "HTML sayfasında kontrol edin:" -ForegroundColor Yellow
Write-Host "  ✓ Domain Created → System Events" -ForegroundColor White
Write-Host "  ✓ User Created → Domain Events" -ForegroundColor White
Write-Host ""
Write-Host "Test Domain Bilgileri:" -ForegroundColor Cyan
Write-Host "  - Domain: $TestDomainName" -ForegroundColor White
Write-Host "  - Domain ID: $domainId" -ForegroundColor White
Write-Host "  - Admin: $adminUsername" -ForegroundColor White
Write-Host "  - Test User: testuser" -ForegroundColor White
Write-Host ""

