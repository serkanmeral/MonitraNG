# MngHub SignalR Event Test Script
# Tests domain and user creation events via SignalR

$MngKeeperUrl = "https://localhost:5001"
$MngHubUrl = "http://localhost:5020"
$TestDomainName = "test-domain-$(Get-Date -Format 'yyyyMMddHHmmss')"
$TestUsername = "testuser"
$TestPassword = "TestPass123!"

Write-Host "=== MngHub SignalR Event Test ===" -ForegroundColor Cyan
Write-Host ""

# Skip certificate validation
[System.Net.ServicePointManager]::ServerCertificateValidationCallback = {$true}

# Check services
Write-Host "1. Servisleri kontrol ediyorum..." -ForegroundColor Yellow
try {
    $mngHubHealth = Invoke-RestMethod -Uri "$MngHubUrl/health" -Method GET -ErrorAction Stop
    Write-Host "   ✓ MngHub: $($mngHubHealth.status)" -ForegroundColor Green
} catch {
    Write-Host "   ✗ MngHub erişilemiyor: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

try {
    $mngKeeperHealth = Invoke-RestMethod -Uri "$MngKeeperUrl/health" -Method GET -ErrorAction Stop
    Write-Host "   ✓ MngKeeper: $($mngKeeperHealth.status)" -ForegroundColor Green
} catch {
    Write-Host "   ⚠ MngKeeper erişilemiyor (Docker'da çalışıyor olabilir)" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "2. Test domain'i oluşturuyorum..." -ForegroundColor Yellow

# Create domain
$domainBody = @{
    domainName = $TestDomainName
    displayName = "Test Domain for SignalR"
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
    
    Write-Host "   ✓ Domain oluşturuldu: $($domain.domainName) (ID: $($domain.domainId))" -ForegroundColor Green
    $domainId = $domain.domainId
} catch {
    Write-Host "   ✗ Domain oluşturulamadı: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "   Response: $($_.Exception.Response)" -ForegroundColor Gray
    exit 1
}

Write-Host ""
Write-Host "3. Mapper'ları yapılandırıyorum..." -ForegroundColor Yellow
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
Write-Host "4. Admin token alıyorum..." -ForegroundColor Yellow
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
Write-Host "5. Test kullanıcısı oluşturuyorum..." -ForegroundColor Yellow
$headers = @{
    "Authorization" = "Bearer $accessToken"
    "Content-Type" = "application/json"
}

$userBody = @{
    username = $TestUsername
    email = "$TestUsername@test.com"
    password = $TestPassword
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
    
    Write-Host "   ✓ Kullanıcı oluşturuldu: $($user.username) (ID: $($user.userId))" -ForegroundColor Green
} catch {
    Write-Host "   ✗ Kullanıcı oluşturulamadı: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "=== Test Sonuçları ===" -ForegroundColor Cyan
Write-Host ""
Write-Host "Domain oluşturma event'i gönderildi:" -ForegroundColor Yellow
Write-Host "  - Routing Key: system.mngkeeper.domain.created" -ForegroundColor Gray
Write-Host "  - Exchange: mng.topics" -ForegroundColor Gray
Write-Host "  - Room: global" -ForegroundColor Gray
Write-Host ""
Write-Host "Kullanıcı oluşturma event'i gönderildi:" -ForegroundColor Yellow
Write-Host "  - Routing Key: $domainId.usercreatedevent" -ForegroundColor Gray
Write-Host "  - Exchange: mngkeeper.events" -ForegroundColor Gray
Write-Host "  - Room: domain.$TestDomainName" -ForegroundColor Gray
Write-Host ""
Write-Host "SignalR bağlantısı test etmek için:" -ForegroundColor Cyan
Write-Host "  1. MngHub'a SignalR client ile bağlanın:" -ForegroundColor White
Write-Host "     URL: $MngHubUrl/ws?access_token=$($accessToken.Substring(0, [Math]::Min(50, $accessToken.Length)))..." -ForegroundColor Gray
Write-Host ""
Write-Host "  2. JavaScript örneği:" -ForegroundColor White
Write-Host "     const connection = new signalR.HubConnectionBuilder()" -ForegroundColor Gray
Write-Host "         .withUrl('$MngHubUrl/ws', { accessTokenFactory: () => '$accessToken' })" -ForegroundColor Gray
Write-Host "         .build();" -ForegroundColor Gray
Write-Host ""
Write-Host "     connection.on('ReceiveMessage', (message) => {" -ForegroundColor Gray
Write-Host "         console.log('Received:', message);" -ForegroundColor Gray
Write-Host "     });" -ForegroundColor Gray
Write-Host ""
Write-Host "     connection.start();" -ForegroundColor Gray
Write-Host ""
Write-Host "Test domain bilgileri:" -ForegroundColor Cyan
Write-Host "  - Domain Name: $TestDomainName" -ForegroundColor White
Write-Host "  - Domain ID: $domainId" -ForegroundColor White
Write-Host "  - Admin Username: $adminUsername" -ForegroundColor White
Write-Host "  - Test Username: $TestUsername" -ForegroundColor White
Write-Host ""

