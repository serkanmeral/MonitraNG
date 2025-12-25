# User Profile Enhancement Test Script
# Tests new fields: Title, Department, Gender, PhoneNumber, PhotoUrl

$baseUrl = "https://localhost:5001"
$domainName = "meral5"  # Kendi domain'inizi buraya yazın
$adminUsername = "meral5_admin"  # Kendi admin kullanıcı adınızı buraya yazın
$adminPassword = "Admin123!"  # Kendi admin şifrenizi buraya yazın

Write-Host "`n=== USER PROFILE ENHANCEMENT TESTLERI ===" -ForegroundColor Cyan
Write-Host "Yeni Alanlar: Title, Department, Gender, PhoneNumber, PhotoUrl" -ForegroundColor Gray
Write-Host ""

# Skip certificate validation for localhost
[System.Net.ServicePointManager]::ServerCertificateValidationCallback = {$true}

# 1. Admin token al
Write-Host "1. Admin token alınıyor..." -ForegroundColor Yellow
try {
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
} catch {
    Write-Host "✗ Token alınamadı: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

$headers = @{
    "Authorization" = "Bearer $adminToken"
    "Content-Type" = "application/json"
}
Write-Host ""

# 2. Yeni alanlarla kullanıcı oluştur
Write-Host "2. Yeni alanlarla kullanıcı oluşturuluyor..." -ForegroundColor Yellow
$testUsername = "test.profile.$(Get-Date -Format 'yyyyMMddHHmmss')"
$createUserBody = @{
    username = $testUsername
    email = "$testUsername@test.com"
    password = "Test123!"
    firstName = "Test"
    lastName = "Profile"
    title = "Senior Developer"
    department = "IT Department"
    gender = 1  # 0: NotSpecified, 1: Male, 2: Female
    phoneNumber = "+905551234567"
    photoUrl = "https://example.com/photos/test.jpg"
    groupIds = @()
    isActive = $true
} | ConvertTo-Json -Depth 3

try {
    $createResponse = Invoke-RestMethod -Uri "$baseUrl/api/user" `
        -Method POST `
        -Headers $headers `
        -Body $createUserBody `
        -SkipCertificateCheck `
        -ErrorAction Stop
    
    if ($createResponse.isSuccess) {
        Write-Host "✓ Kullanıcı oluşturuldu: $($createResponse.username)" -ForegroundColor Green
        Write-Host "  UserId: $($createResponse.userId)" -ForegroundColor Gray
        Write-Host "  Title: $($createResponse.title)" -ForegroundColor Gray
        Write-Host "  Department: $($createResponse.department)" -ForegroundColor Gray
        Write-Host "  Gender: $($createResponse.gender)" -ForegroundColor Gray
        Write-Host "  PhoneNumber: $($createResponse.phoneNumber)" -ForegroundColor Gray
        Write-Host "  PhotoUrl: $($createResponse.photoUrl)" -ForegroundColor Gray
        
        $createdUserId = $createResponse.userId
    } else {
        Write-Host "✗ Kullanıcı oluşturulamadı: $($createResponse.errorMessage)" -ForegroundColor Red
        exit 1
    }
} catch {
    Write-Host "✗ Kullanıcı oluşturulamadı: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.ErrorDetails.Message) {
        Write-Host "  Detay: $($_.ErrorDetails.Message)" -ForegroundColor Gray
    }
    exit 1
}
Write-Host ""

# 3. Oluşturulan kullanıcıyı getir ve yeni alanları kontrol et
Write-Host "3. Oluşturulan kullanıcı getiriliyor ve yeni alanlar kontrol ediliyor..." -ForegroundColor Yellow
try {
    $getUserResponse = Invoke-RestMethod -Uri "$baseUrl/api/user/$createdUserId" `
        -Method GET `
        -Headers $headers `
        -SkipCertificateCheck `
        -ErrorAction Stop
    
    if ($getUserResponse.isSuccess) {
        $user = $getUserResponse.user
        Write-Host "✓ Kullanıcı getirildi: $($user.username)" -ForegroundColor Green
        
        # Yeni alanları kontrol et
        $allFieldsOk = $true
        if ($user.title -eq "Senior Developer") {
            Write-Host "  ✓ Title doğru: $($user.title)" -ForegroundColor Green
        } else {
            Write-Host "  ✗ Title yanlış: Beklenen 'Senior Developer', Gelen '$($user.title)'" -ForegroundColor Red
            $allFieldsOk = $false
        }
        
        if ($user.department -eq "IT Department") {
            Write-Host "  ✓ Department doğru: $($user.department)" -ForegroundColor Green
        } else {
            Write-Host "  ✗ Department yanlış: Beklenen 'IT Department', Gelen '$($user.department)'" -ForegroundColor Red
            $allFieldsOk = $false
        }
        
        if ($user.gender -eq 1) {
            Write-Host "  ✓ Gender doğru: $($user.gender) (Male)" -ForegroundColor Green
        } else {
            Write-Host "  ✗ Gender yanlış: Beklenen '1', Gelen '$($user.gender)'" -ForegroundColor Red
            $allFieldsOk = $false
        }
        
        if ($user.phoneNumber -eq "+905551234567") {
            Write-Host "  ✓ PhoneNumber doğru: $($user.phoneNumber)" -ForegroundColor Green
        } else {
            Write-Host "  ✗ PhoneNumber yanlış: Beklenen '+905551234567', Gelen '$($user.phoneNumber)'" -ForegroundColor Red
            $allFieldsOk = $false
        }
        
        if ($user.photoUrl -eq "https://example.com/photos/test.jpg") {
            Write-Host "  ✓ PhotoUrl doğru: $($user.photoUrl)" -ForegroundColor Green
        } else {
            Write-Host "  ✗ PhotoUrl yanlış: Beklenen 'https://example.com/photos/test.jpg', Gelen '$($user.photoUrl)'" -ForegroundColor Red
            $allFieldsOk = $false
        }
        
        if (-not $allFieldsOk) {
            Write-Host "  ⚠ Bazı alanlar doğru değil!" -ForegroundColor Yellow
        }
    } else {
        Write-Host "✗ Kullanıcı getirilemedi: $($getUserResponse.errorMessage)" -ForegroundColor Red
    }
} catch {
    Write-Host "✗ Kullanıcı getirilemedi: $($_.Exception.Message)" -ForegroundColor Red
}
Write-Host ""

# 4. Kullanıcı listesinde yeni alanları kontrol et
Write-Host "4. Kullanıcı listesinde yeni alanlar kontrol ediliyor..." -ForegroundColor Yellow
try {
    $usersResponse = Invoke-RestMethod -Uri "$baseUrl/api/user?page=1&pageSize=100" `
        -Method GET `
        -Headers $headers `
        -SkipCertificateCheck `
        -ErrorAction Stop
    
    $testUser = $usersResponse.users | Where-Object { $_.userId -eq $createdUserId }
    if ($testUser) {
        Write-Host "✓ Test kullanıcısı listede bulundu: $($testUser.username)" -ForegroundColor Green
        Write-Host "  Title: $($testUser.title)" -ForegroundColor Gray
        Write-Host "  Department: $($testUser.department)" -ForegroundColor Gray
        Write-Host "  Gender: $($testUser.gender)" -ForegroundColor Gray
        Write-Host "  PhoneNumber: $($testUser.phoneNumber)" -ForegroundColor Gray
        Write-Host "  PhotoUrl: $($testUser.photoUrl)" -ForegroundColor Gray
    } else {
        Write-Host "✗ Test kullanıcısı listede bulunamadı" -ForegroundColor Red
    }
} catch {
    Write-Host "✗ Kullanıcı listesi alınamadı: $($_.Exception.Message)" -ForegroundColor Red
}
Write-Host ""

# 5. Kullanıcıyı yeni alanlarla güncelle
Write-Host "5. Kullanıcı yeni alanlarla güncelleniyor..." -ForegroundColor Yellow
$updateUserBody = @{
    userId = $createdUserId
    username = $testUsername
    email = "$testUsername@test.com"
    firstName = "Test"
    lastName = "Profile Updated"
    title = "Lead Developer"
    department = "Software Development"
    gender = 2  # Female
    phoneNumber = "+905559876543"
    photoUrl = "https://example.com/photos/test-updated.jpg"
    groupIds = @()
    isActive = $true
} | ConvertTo-Json -Depth 3

try {
    $updateResponse = Invoke-RestMethod -Uri "$baseUrl/api/user/$createdUserId" `
        -Method PUT `
        -Headers $headers `
        -Body $updateUserBody `
        -SkipCertificateCheck `
        -ErrorAction Stop
    
    if ($updateResponse.isSuccess) {
        Write-Host "✓ Kullanıcı güncellendi: $($updateResponse.username)" -ForegroundColor Green
        Write-Host "  Title: $($updateResponse.title) (güncellendi)" -ForegroundColor Gray
        Write-Host "  Department: $($updateResponse.department) (güncellendi)" -ForegroundColor Gray
        Write-Host "  Gender: $($updateResponse.gender) (Female - güncellendi)" -ForegroundColor Gray
        Write-Host "  PhoneNumber: $($updateResponse.phoneNumber) (güncellendi)" -ForegroundColor Gray
        Write-Host "  PhotoUrl: $($updateResponse.photoUrl) (güncellendi)" -ForegroundColor Gray
    } else {
        Write-Host "✗ Kullanıcı güncellenemedi: $($updateResponse.errorMessage)" -ForegroundColor Red
    }
} catch {
    Write-Host "✗ Kullanıcı güncellenemedi: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.ErrorDetails.Message) {
        Write-Host "  Detay: $($_.ErrorDetails.Message)" -ForegroundColor Gray
    }
}
Write-Host ""

# 6. Güncellenmiş kullanıcıyı tekrar getir ve kontrol et
Write-Host "6. Güncellenmiş kullanıcı getiriliyor ve kontrol ediliyor..." -ForegroundColor Yellow
try {
    $getUpdatedUserResponse = Invoke-RestMethod -Uri "$baseUrl/api/user/$createdUserId" `
        -Method GET `
        -Headers $headers `
        -SkipCertificateCheck `
        -ErrorAction Stop
    
    if ($getUpdatedUserResponse.isSuccess) {
        $updatedUser = $getUpdatedUserResponse.user
        Write-Host "✓ Güncellenmiş kullanıcı getirildi" -ForegroundColor Green
        
        if ($updatedUser.title -eq "Lead Developer" -and 
            $updatedUser.department -eq "Software Development" -and 
            $updatedUser.gender -eq 2 -and 
            $updatedUser.phoneNumber -eq "+905559876543" -and
            $updatedUser.photoUrl -eq "https://example.com/photos/test-updated.jpg") {
            Write-Host "  ✓ Tüm güncellemeler doğru!" -ForegroundColor Green
        } else {
            Write-Host "  ⚠ Bazı güncellemeler doğru değil" -ForegroundColor Yellow
            Write-Host "    Title: $($updatedUser.title)" -ForegroundColor Gray
            Write-Host "    Department: $($updatedUser.department)" -ForegroundColor Gray
            Write-Host "    Gender: $($updatedUser.gender)" -ForegroundColor Gray
            Write-Host "    PhoneNumber: $($updatedUser.phoneNumber)" -ForegroundColor Gray
            Write-Host "    PhotoUrl: $($updatedUser.photoUrl)" -ForegroundColor Gray
        }
    }
} catch {
    Write-Host "✗ Güncellenmiş kullanıcı getirilemedi: $($_.Exception.Message)" -ForegroundColor Red
}
Write-Host ""

# 7. Gender enum testleri (NotSpecified, Male, Female)
Write-Host "7. Gender enum testleri yapılıyor..." -ForegroundColor Yellow
$genderTests = @(
    @{ value = 0; name = "NotSpecified" },
    @{ value = 1; name = "Male" },
    @{ value = 2; name = "Female" }
)

foreach ($genderTest in $genderTests) {
    $genderTestUsername = "test.gender.$($genderTest.value).$(Get-Date -Format 'yyyyMMddHHmmss')"
    $genderTestBody = @{
        username = $genderTestUsername
        email = "$genderTestUsername@test.com"
        password = "Test123!"
        firstName = "Gender"
        lastName = $genderTest.name
        gender = $genderTest.value
        groupIds = @()
        isActive = $true
    } | ConvertTo-Json -Depth 3
    
    try {
        $genderTestResponse = Invoke-RestMethod -Uri "$baseUrl/api/user" `
            -Method POST `
            -Headers $headers `
            -Body $genderTestBody `
            -SkipCertificateCheck `
            -ErrorAction Stop
        
        if ($genderTestResponse.isSuccess -and $genderTestResponse.gender -eq $genderTest.value) {
            Write-Host "  ✓ Gender $($genderTest.value) ($($genderTest.name)) testi başarılı" -ForegroundColor Green
        } else {
            Write-Host "  ✗ Gender $($genderTest.value) ($($genderTest.name)) testi başarısız" -ForegroundColor Red
        }
    } catch {
        Write-Host "  ✗ Gender $($genderTest.value) ($($genderTest.name)) testi hata: $($_.Exception.Message)" -ForegroundColor Red
    }
}
Write-Host ""

# 8. Opsiyonel alanlar testi (null değerler)
Write-Host "8. Opsiyonel alanlar testi (null değerler)..." -ForegroundColor Yellow
$optionalTestUsername = "test.optional.$(Get-Date -Format 'yyyyMMddHHmmss')"
$optionalTestBody = @{
    username = $optionalTestUsername
    email = "$optionalTestUsername@test.com"
    password = "Test123!"
    firstName = "Optional"
    lastName = "Test"
    # title, department, phoneNumber, photoUrl alanları gönderilmiyor (null olmalı)
    gender = 0  # NotSpecified
    groupIds = @()
    isActive = $true
} | ConvertTo-Json -Depth 3

try {
    $optionalTestResponse = Invoke-RestMethod -Uri "$baseUrl/api/user" `
        -Method POST `
        -Headers $headers `
        -Body $optionalTestBody `
        -SkipCertificateCheck `
        -ErrorAction Stop
    
    if ($optionalTestResponse.isSuccess) {
        Write-Host "✓ Opsiyonel alanlar testi başarılı" -ForegroundColor Green
        Write-Host "  Title: $($optionalTestResponse.title)" -ForegroundColor Gray
        Write-Host "  Department: $($optionalTestResponse.department)" -ForegroundColor Gray
        Write-Host "  PhoneNumber: $($optionalTestResponse.phoneNumber)" -ForegroundColor Gray
        Write-Host "  PhotoUrl: $($optionalTestResponse.photoUrl)" -ForegroundColor Gray
    }
} catch {
    Write-Host "✗ Opsiyonel alanlar testi başarısız: $($_.Exception.Message)" -ForegroundColor Red
}
Write-Host ""

Write-Host "=== TEST TAMAMLANDI ===" -ForegroundColor Cyan
Write-Host ""
Write-Host "Not: Test kullanıcıları temizlenmedi. Manuel olarak silmek isterseniz:" -ForegroundColor Yellow
Write-Host "  DELETE $baseUrl/api/user/$createdUserId" -ForegroundColor Gray
Write-Host ""

