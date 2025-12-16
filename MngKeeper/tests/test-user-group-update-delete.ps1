# User and Group Update/Delete Test Script
# Tests update and delete operations for meral4 domain

$baseUrl = "https://localhost:5001"
$domainName = "meral5"
$adminUsername = "meral5_admin"
$adminPassword = "Admin123!"

Write-Host "`n=== USER VE GROUP UPDATE/DELETE TESTLERI ===" -ForegroundColor Cyan
Write-Host ""

# Skip certificate validation for localhost
[System.Net.ServicePointManager]::ServerCertificateValidationCallback = {$true}

# 1. Admin token al
Write-Host "1. Admin token alınıyor..." -ForegroundColor Yellow
$tokenResponse = Invoke-RestMethod -Uri "$baseUrl/api/auth/token" `
    -Method POST `
    -ContentType "application/json" `
    -Body (@{
        username = $adminUsername
        password = $adminPassword
        domain = $domainName
    } | ConvertTo-Json) `
    -SkipCertificateCheck `
    -ErrorAction Stop

$adminToken = $tokenResponse.accessToken
if ([string]::IsNullOrEmpty($adminToken)) {
    Write-Host "✗ Token alınamadı. Response: $($tokenResponse | ConvertTo-Json)" -ForegroundColor Red
    exit 1
}

Write-Host "✓ Admin token alındı" -ForegroundColor Green
Write-Host "  Token (ilk 50 karakter): $($adminToken.Substring(0, [Math]::Min(50, $adminToken.Length)))..." -ForegroundColor Gray

$headers = @{
    "Authorization" = "Bearer $adminToken"
    "Content-Type" = "application/json"
}
Write-Host ""

# 2. Kullanıcıları listele
Write-Host "2. Kullanıcılar listeleniyor..." -ForegroundColor Yellow
$usersResponse = Invoke-RestMethod -Uri "$baseUrl/api/user?page=1&pageSize=10" `
    -Method GET `
    -Headers $headers `
    -SkipCertificateCheck `
    -ErrorAction Stop

Write-Host "✓ Toplam kullanıcı sayısı: $($usersResponse.totalCount)" -ForegroundColor Green
foreach ($user in $usersResponse.users) {
    Write-Host "  - $($user.username) ($($user.email))" -ForegroundColor Gray
}
Write-Host ""

# 3. Grupları listele
Write-Host "3. Gruplar listeleniyor..." -ForegroundColor Yellow
$groupsResponse = Invoke-RestMethod -Uri "$baseUrl/api/group?page=1&pageSize=10" `
    -Method GET `
    -Headers $headers `
    -SkipCertificateCheck `
    -ErrorAction Stop

Write-Host "✓ Toplam grup sayısı: $($groupsResponse.totalCount)" -ForegroundColor Green
foreach ($group in $groupsResponse.groups) {
    Write-Host "  - $($group.name)" -ForegroundColor Gray
}
Write-Host ""

# 4. Bir kullanıcıyı güncelle (test.user1)
Write-Host "4. Kullanıcı güncelleniyor (test.user1)..." -ForegroundColor Yellow
$testUser = $usersResponse.users | Where-Object { $_.username -eq "test.user1" }
if ($testUser) {
    $updateUserBody = @{
        userId = $testUser.userId
        username = "test.user1"
        email = "test.user1.updated@meral.com"
        firstName = "Test"
        lastName = "User1 Updated"
        isActive = $true
        groupIds = @()
    } | ConvertTo-Json

    try {
        $updateResponse = Invoke-RestMethod -Uri "$baseUrl/api/user/$($testUser.userId)" `
            -Method PUT `
            -Headers $headers `
            -Body $updateUserBody `
            -SkipCertificateCheck `
            -ErrorAction Stop
        
        Write-Host "✓ Kullanıcı güncellendi: $($updateResponse.username) - $($updateResponse.email)" -ForegroundColor Green
    } catch {
        Write-Host "✗ Kullanıcı güncellenemedi: $($_.Exception.Message)" -ForegroundColor Red
    }
} else {
    Write-Host "✗ test.user1 kullanıcısı bulunamadı" -ForegroundColor Red
}
Write-Host ""

# 5. Bir grubu güncelle (developers)
Write-Host "5. Grup güncelleniyor (developers)..." -ForegroundColor Yellow
$developersGroup = $groupsResponse.groups | Where-Object { $_.name -eq "developers" }
if ($developersGroup) {
    $updateGroupBody = @{
        groupId = $developersGroup.groupId
        name = "developers"
        description = "Developers group - Updated Description"
        permissions = @("read", "write", "develop")
        isActive = $true
    } | ConvertTo-Json

    try {
        $updateResponse = Invoke-RestMethod -Uri "$baseUrl/api/group/$($developersGroup.groupId)" `
            -Method PUT `
            -Headers $headers `
            -Body $updateGroupBody `
            -SkipCertificateCheck `
            -ErrorAction Stop
        
        Write-Host "✓ Grup güncellendi: $($updateResponse.name) - $($updateResponse.description)" -ForegroundColor Green
    } catch {
        Write-Host "✗ Grup güncellenemedi: $($_.Exception.Message)" -ForegroundColor Red
    }
} else {
    Write-Host "✗ developers grubu bulunamadı" -ForegroundColor Red
}
Write-Host ""

# 6. Bir kullanıcıyı sil (test.user2 - sistem kullanıcısı değil)
Write-Host "6. Kullanıcı siliniyor (test.user2)..." -ForegroundColor Yellow
$testUser2 = $usersResponse.users | Where-Object { $_.username -eq "test.user2" }
if ($testUser2) {
    try {
        $deleteResponse = Invoke-RestMethod -Uri "$baseUrl/api/user/$($testUser2.userId)" `
            -Method DELETE `
            -Headers $headers `
            -SkipCertificateCheck `
            -ErrorAction Stop
        
        Write-Host "✓ Kullanıcı silindi: $($testUser2.username)" -ForegroundColor Green
    } catch {
        Write-Host "✗ Kullanıcı silinemedi: $($_.Exception.Message)" -ForegroundColor Red
    }
} else {
    Write-Host "✗ test.user2 kullanıcısı bulunamadı" -ForegroundColor Red
}
Write-Host ""

# 7. Bir grubu sil (viewers - sistem grubu değil)
Write-Host "7. Grup siliniyor (viewers)..." -ForegroundColor Yellow
$viewersGroup = $groupsResponse.groups | Where-Object { $_.name -eq "viewers" }
if ($viewersGroup) {
    try {
        $deleteResponse = Invoke-RestMethod -Uri "$baseUrl/api/group/$($viewersGroup.groupId)" `
            -Method DELETE `
            -Headers $headers `
            -SkipCertificateCheck `
            -ErrorAction Stop
        
        Write-Host "✓ Grup silindi: $($viewersGroup.name)" -ForegroundColor Green
    } catch {
        Write-Host "✗ Grup silinemedi: $($_.Exception.Message)" -ForegroundColor Red
        Write-Host "  Detay: $($_.Exception)" -ForegroundColor Gray
    }
} else {
    Write-Host "✗ viewers grubu bulunamadı" -ForegroundColor Red
}
Write-Host ""

# 8. Güncel durumu kontrol et
Write-Host "8. Güncel durum kontrol ediliyor..." -ForegroundColor Yellow
$usersResponseAfter = Invoke-RestMethod -Uri "$baseUrl/api/user?page=1&pageSize=10" `
    -Method GET `
    -Headers $headers `
    -SkipCertificateCheck `
    -ErrorAction Stop

$groupsResponseAfter = Invoke-RestMethod -Uri "$baseUrl/api/group?page=1&pageSize=10" `
    -Method GET `
    -Headers $headers `
    -SkipCertificateCheck `
    -ErrorAction Stop

Write-Host "✓ Güncel kullanıcı sayısı: $($usersResponseAfter.totalCount)" -ForegroundColor Green
Write-Host "✓ Güncel grup sayısı: $($groupsResponseAfter.totalCount)" -ForegroundColor Green
Write-Host ""

Write-Host "=== TEST TAMAMLANDI ===" -ForegroundColor Cyan

