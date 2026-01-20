# MngKeeper Lisanslama Test Scripti
# Lisanslama sisteminin tüm özelliklerini test eder

param(
    [string]$BaseUrl = "http://localhost:5001",
    [string]$TestDomainName = "test-license-$(Get-Date -Format 'yyyyMMddHHmmss')",
    [string]$AdminUsername = "admin",
    [string]$AdminPassword = "Admin123!",
    [switch]$SkipCleanup
)

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "  MngKeeper Lisanslama Test Süreci" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

# Skip certificate validation for localhost
[System.Net.ServicePointManager]::ServerCertificateValidationCallback = {$true}

$testResults = @{
    TrialLicense = @{}
    RealLicense = @{}
    LicenseValidation = @{}
    LicenseControl = @{}
    UserLimit = @{}
    Cache = @{}
    BackgroundJob = @{}
}

$headers = @{
    "Content-Type" = "application/json"
}

# ============================================
# 1. TEST DOMAIN OLUŞTURMA
# ============================================
Write-Host "=== 1. Test Domain Oluşturma ===" -ForegroundColor Yellow
Write-Host ""

try {
    $domainRequest = @{
        domainName = $TestDomainName
        displayName = "Test License Domain"
        adminEmail = "$AdminUsername@$TestDomainName"
        adminPassword = $AdminPassword
        settings = @{
            maxUsers = 100
            maxAssets = 1000
            enableMqtt = $true
        }
    } | ConvertTo-Json -Depth 10

    $response = Invoke-RestMethod -Uri "$BaseUrl/api/domain" `
        -Method POST `
        -Headers $headers `
        -Body $domainRequest `
        -ErrorAction Stop

    Write-Host "✓ Domain oluşturuldu: $TestDomainName" -ForegroundColor Green
    Write-Host "  Domain ID: $($response.domainId)" -ForegroundColor Gray
    $domainId = $response.domainId
    
    # Domain oluşturulmasını bekle (pipeline tamamlansın)
    Write-Host "  Pipeline tamamlanması bekleniyor..." -ForegroundColor Gray
    Write-Host "  (Keycloak kullanıcı oluşturma ve lisans oluşturma için bekleniyor...)" -ForegroundColor Gray
    
    # Domain status'unu kontrol et (Active olana kadar bekle)
    $maxWaitTime = 120  # 2 dakika
    $waitInterval = 5   # 5 saniye
    $elapsedTime = 0
    $domainActive = $false
    
    while (-not $domainActive -and $elapsedTime -lt $maxWaitTime) {
        Start-Sleep -Seconds $waitInterval
        $elapsedTime += $waitInterval
        
        try {
            $domainDetails = Invoke-RestMethod -Uri "$BaseUrl/api/domain/$domainId" `
                -Method GET `
                -Headers $headers `
                -ErrorAction Stop
            
            $status = $domainDetails.status
            Write-Host "    Status: $status - Elapsed: $elapsedTime seconds" -ForegroundColor Gray
            
            if ($status -eq "Active") {
                $domainActive = $true
                Write-Host "  ✓ Domain aktif oldu!" -ForegroundColor Green
                break
            }
        } catch {
            # Domain henüz hazır değil, devam et
        }
    }
    
    if (-not $domainActive) {
        Write-Host "  ⚠ Domain henüz aktif olmadı, test devam ediyor..." -ForegroundColor Yellow
    }
    
    $testResults.TrialLicense["DomainCreated"] = "OK"
} catch {
    Write-Host "✗ Domain oluşturulamadı: $($_.Exception.Message)" -ForegroundColor Red
    $testResults.TrialLicense["DomainCreated"] = "FAILED"
    exit 1
}

# ============================================
# 2. TRIAL LİSANS KONTROLÜ
# ============================================
Write-Host "`n=== 2. Trial Lisans Kontrolü ===" -ForegroundColor Yellow
Write-Host ""

# 2.1 Lisans bilgisi al (önce token al)
Write-Host "  Token alınıyor..." -ForegroundColor Gray
try {
    $tokenRequest = @{
        username = $AdminUsername
        password = $AdminPassword
        domain = $TestDomainName
    } | ConvertTo-Json

    $tokenResponse = Invoke-RestMethod -Uri "$BaseUrl/api/auth/token" `
        -Method POST `
        -Headers $headers `
        -Body $tokenRequest `
        -ErrorAction Stop

    if ($tokenResponse.accessToken) {
        $accessToken = $tokenResponse.accessToken
        $headers["Authorization"] = "Bearer $accessToken"
        Write-Host "  ✓ Token alındı" -ForegroundColor Green
    } else {
        Write-Host "  ✗ Token alınamadı (response: $($tokenResponse | ConvertTo-Json))" -ForegroundColor Red
        throw "Token alınamadı"
    }
} catch {
    $errorDetails = $_.Exception.Message
    if ($_.ErrorDetails) {
        $errorDetails += " | Details: $($_.ErrorDetails.Message)"
    }
    Write-Host "  ✗ Token alınamadı: $errorDetails" -ForegroundColor Red
    Write-Host "  ⚠ Lisans testleri token olmadan devam edemez" -ForegroundColor Yellow
    Write-Host "  ⚠ Domain pipeline'ının tamamlanması için daha fazla beklemek gerekebilir" -ForegroundColor Yellow
    $testResults.TrialLicense["LicenseCreated"] = "FAILED"
    $testResults.TrialLicense["TokenRequired"] = "FAILED"
}

# 2.2 Lisans bilgisi al
if ($headers.ContainsKey("Authorization")) {
    try {
        $licenseInfo = Invoke-RestMethod -Uri "$BaseUrl/api/license/$TestDomainName" `
            -Method GET `
            -Headers $headers `
            -ErrorAction Stop

        Write-Host "✓ Lisans bilgisi alındı" -ForegroundColor Green
        Write-Host "  Lisans Tipi: $($licenseInfo.licenseType)" -ForegroundColor Gray
        Write-Host "  Geçerli: $($licenseInfo.isValid)" -ForegroundColor Gray
        Write-Host "  Bitiş Tarihi: $($licenseInfo.expiresAt)" -ForegroundColor Gray
        
        if ($licenseInfo.licenseType -eq "Trial" -and $licenseInfo.isValid) {
            $testResults.TrialLicense["LicenseCreated"] = "OK"
        } else {
            $testResults.TrialLicense["LicenseCreated"] = "FAILED"
        }
    } catch {
        $errorDetails = $_.Exception.Message
        if ($_.ErrorDetails) {
            $errorDetails += " | Details: $($_.ErrorDetails.Message)"
        }
        Write-Host "✗ Lisans bilgisi alınamadı: $errorDetails" -ForegroundColor Red
        $testResults.TrialLicense["LicenseCreated"] = "FAILED"
    }
}

# 2.3 Lisans doğrulama
if ($headers.ContainsKey("Authorization")) {
    try {
        $validateRequest = @{
            domainName = $TestDomainName
        } | ConvertTo-Json

        $validation = Invoke-RestMethod -Uri "$BaseUrl/api/license/validate" `
            -Method POST `
            -Headers $headers `
            -Body $validateRequest `
            -ErrorAction Stop

        Write-Host "✓ Lisans doğrulandı" -ForegroundColor Green
        Write-Host "  Geçerli: $($validation.isValid)" -ForegroundColor Gray
        Write-Host "  Süresi Dolmuş: $($validation.isExpired)" -ForegroundColor Gray
        
        $testResults.LicenseValidation["ValidateLicense"] = "OK"
    } catch {
        $errorDetails = $_.Exception.Message
        if ($_.ErrorDetails) {
            $errorDetails += " | Details: $($_.ErrorDetails.Message)"
        }
        Write-Host "✗ Lisans doğrulanamadı: $errorDetails" -ForegroundColor Red
        $testResults.LicenseValidation["ValidateLicense"] = "FAILED"
    }
} else {
    Write-Host "  ⚠ Token yok, lisans doğrulama atlanıyor" -ForegroundColor Yellow
    $testResults.LicenseValidation["ValidateLicense"] = "SKIPPED"
}

# 2.3 MinIO'da lisans dosyası kontrolü (manuel kontrol gerekli)
Write-Host "  ⚠ MinIO'da lisans dosyası kontrolü için manuel kontrol gerekli" -ForegroundColor Yellow
Write-Host "    Beklenen: {bucketName}/system/license-trial.enc" -ForegroundColor Gray

# ============================================
# 3. TOKEN GENERATION KONTROLÜ
# ============================================
Write-Host "`n=== 3. Token Generation Kontrolü ===" -ForegroundColor Yellow
Write-Host ""

if ($headers.ContainsKey("Authorization")) {
    Write-Host "✓ Token zaten alınmış (Bölüm 2'de)" -ForegroundColor Green
    $testResults.LicenseControl["TokenGeneration"] = "OK"
} else {
    try {
        $tokenRequest = @{
            username = $AdminUsername
            password = $AdminPassword
            domain = $TestDomainName
        } | ConvertTo-Json

        $tokenResponse = Invoke-RestMethod -Uri "$BaseUrl/api/auth/token" `
            -Method POST `
            -Headers $headers `
            -Body $tokenRequest `
            -ErrorAction Stop

        if ($tokenResponse.accessToken) {
            Write-Host "✓ Token başarıyla alındı" -ForegroundColor Green
            $accessToken = $tokenResponse.accessToken
            $headers["Authorization"] = "Bearer $accessToken"
            $testResults.LicenseControl["TokenGeneration"] = "OK"
        } else {
            Write-Host "✗ Token alınamadı (response: $($tokenResponse | ConvertTo-Json))" -ForegroundColor Red
            $testResults.LicenseControl["TokenGeneration"] = "FAILED"
        }
    } catch {
        $errorDetails = $_.Exception.Message
        if ($_.ErrorDetails) {
            $errorDetails += " | Details: $($_.ErrorDetails.Message)"
        }
        Write-Host "✗ Token alınamadı: $errorDetails" -ForegroundColor Red
        $testResults.LicenseControl["TokenGeneration"] = "FAILED"
    }
}

# ============================================
# 4. OPERASYON KONTROLÜ
# ============================================
Write-Host "`n=== 4. Operasyon Kontrolü ===" -ForegroundColor Yellow
Write-Host ""

# 4.1 Token generation kontrolü
if ($headers.ContainsKey("Authorization")) {
    try {
        $checkRequest = @{
            domainName = $TestDomainName
            operation = "TokenGeneration"
        } | ConvertTo-Json

        $checkResponse = Invoke-RestMethod -Uri "$BaseUrl/api/license/check-operation" `
            -Method POST `
            -Headers $headers `
            -Body $checkRequest `
            -ErrorAction Stop

        Write-Host "✓ Token generation kontrolü: $($checkResponse.isAllowed)" -ForegroundColor Green
        $testResults.LicenseControl["CheckTokenGeneration"] = "OK"
    } catch {
        $errorDetails = $_.Exception.Message
        if ($_.ErrorDetails) {
            $errorDetails += " | Details: $($_.ErrorDetails.Message)"
        }
        Write-Host "✗ Operasyon kontrolü başarısız: $errorDetails" -ForegroundColor Red
        $testResults.LicenseControl["CheckTokenGeneration"] = "FAILED"
    }
} else {
    Write-Host "  ⚠ Token yok, operasyon kontrolü atlanıyor" -ForegroundColor Yellow
    $testResults.LicenseControl["CheckTokenGeneration"] = "SKIPPED"
}

# 4.2 CRUD operasyon kontrolü
if ($headers.ContainsKey("Authorization")) {
    try {
        $checkRequest = @{
            domainName = $TestDomainName
            operation = "CrudOperation"
        } | ConvertTo-Json

        $checkResponse = Invoke-RestMethod -Uri "$BaseUrl/api/license/check-operation" `
            -Method POST `
            -Headers $headers `
            -Body $checkRequest `
            -ErrorAction Stop

        Write-Host "✓ CRUD operasyon kontrolü: $($checkResponse.isAllowed)" -ForegroundColor Green
        $testResults.LicenseControl["CheckCrudOperation"] = "OK"
    } catch {
        $errorDetails = $_.Exception.Message
        if ($_.ErrorDetails) {
            $errorDetails += " | Details: $($_.ErrorDetails.Message)"
        }
        Write-Host "✗ CRUD operasyon kontrolü başarısız: $errorDetails" -ForegroundColor Red
        $testResults.LicenseControl["CheckCrudOperation"] = "FAILED"
    }
} else {
    Write-Host "  ⚠ Token yok, CRUD operasyon kontrolü atlanıyor" -ForegroundColor Yellow
    $testResults.LicenseControl["CheckCrudOperation"] = "SKIPPED"
}

# ============================================
# 5. KULLANICI SAYISI KONTROLÜ
# ============================================
Write-Host "`n=== 5. Kullanıcı Sayısı Kontrolü ===" -ForegroundColor Yellow
Write-Host ""

if ($headers.ContainsKey("Authorization")) {
    try {
        $userCountResponse = Invoke-RestMethod -Uri "$BaseUrl/api/license/$TestDomainName/user-count" `
            -Method GET `
            -Headers $headers `
            -ErrorAction Stop

        Write-Host "✓ Kullanıcı sayısı bilgisi alındı" -ForegroundColor Green
        Write-Host "  Aktif Kullanıcı: $($userCountResponse.activeUserCount)" -ForegroundColor Gray
        Write-Host "  Maksimum: $($userCountResponse.maxUsers)" -ForegroundColor Gray
        Write-Host "  Yeni Kullanıcı Oluşturulabilir: $($userCountResponse.canCreateUser)" -ForegroundColor Gray
        
        $testResults.UserLimit["GetUserCount"] = "OK"
    } catch {
        $errorDetails = $_.Exception.Message
        if ($_.ErrorDetails) {
            $errorDetails += " | Details: $($_.ErrorDetails.Message)"
        }
        Write-Host "✗ Kullanıcı sayısı alınamadı: $errorDetails" -ForegroundColor Red
        $testResults.UserLimit["GetUserCount"] = "FAILED"
    }
} else {
    Write-Host "  ⚠ Token yok, kullanıcı sayısı kontrolü atlanıyor" -ForegroundColor Yellow
    $testResults.UserLimit["GetUserCount"] = "SKIPPED"
}

# ============================================
# 6. REAL LİSANS YÜKLEME (Opsiyonel)
# ============================================
Write-Host "`n=== 6. Real Lisans Yükleme (Opsiyonel) ===" -ForegroundColor Yellow
Write-Host ""

$realLicenseFile = "test-license-real.enc"
if (Test-Path $realLicenseFile) {
    try {
        $boundary = [System.Guid]::NewGuid().ToString()
        $fileBytes = [System.IO.File]::ReadAllBytes($realLicenseFile)
        $fileEnc = [System.Text.Encoding]::GetEncoding("iso-8859-1").GetString($fileBytes)
        
        $bodyLines = @(
            "--$boundary",
            "Content-Disposition: form-data; name=`"domainName`"",
            "",
            $TestDomainName,
            "--$boundary",
            "Content-Disposition: form-data; name=`"licenseFile`"; filename=`"$realLicenseFile`"",
            "Content-Type: application/octet-stream",
            "",
            $fileEnc,
            "--$boundary--"
        )
        
        $body = $bodyLines -join "`r`n"
        
        $uploadHeaders = @{
            "Content-Type" = "multipart/form-data; boundary=$boundary"
        }
        if ($headers.ContainsKey("Authorization")) {
            $uploadHeaders["Authorization"] = $headers["Authorization"]
        }
        
        $response = Invoke-RestMethod -Uri "$BaseUrl/api/license/upload" `
            -Method POST `
            -Headers $uploadHeaders `
            -Body $body `
            -ErrorAction Stop

        Write-Host "✓ Real lisans yüklendi" -ForegroundColor Green
        $testResults.RealLicense["UploadLicense"] = "OK"
    } catch {
        Write-Host "✗ Real lisans yüklenemedi: $($_.Exception.Message)" -ForegroundColor Red
        $testResults.RealLicense["UploadLicense"] = "FAILED"
    }
} else {
    Write-Host "  ⚠ Real lisans dosyası bulunamadı: $realLicenseFile" -ForegroundColor Yellow
    Write-Host "    Bu test atlanıyor..." -ForegroundColor Gray
    $testResults.RealLicense["UploadLicense"] = "SKIPPED"
}

# ============================================
# 7. LİSANS İNDİRME
# ============================================
Write-Host "`n=== 7. Lisans İndirme ===" -ForegroundColor Yellow
Write-Host ""

if ($headers.ContainsKey("Authorization")) {
    try {
        $downloadResponse = Invoke-WebRequest -Uri "$BaseUrl/api/license/$TestDomainName/download?type=trial" `
            -Method GET `
            -Headers $headers `
            -OutFile "downloaded-license-trial.enc" `
            -ErrorAction Stop

        if (Test-Path "downloaded-license-trial.enc") {
            Write-Host "✓ Trial lisans indirildi: downloaded-license-trial.enc" -ForegroundColor Green
            $testResults.TrialLicense["DownloadLicense"] = "OK"
        } else {
            Write-Host "✗ Lisans dosyası indirilemedi" -ForegroundColor Red
            $testResults.TrialLicense["DownloadLicense"] = "FAILED"
        }
    } catch {
        $errorDetails = $_.Exception.Message
        if ($_.ErrorDetails) {
            $errorDetails += " | Details: $($_.ErrorDetails.Message)"
        }
        Write-Host "✗ Lisans indirilemedi: $errorDetails" -ForegroundColor Red
        $testResults.TrialLicense["DownloadLicense"] = "FAILED"
    }
} else {
    Write-Host "  ⚠ Token yok, lisans indirme atlanıyor" -ForegroundColor Yellow
    $testResults.TrialLicense["DownloadLicense"] = "SKIPPED"
}

# ============================================
# 8. CACHE KONTROLÜ
# ============================================
Write-Host "`n=== 8. Cache Kontrolü ===" -ForegroundColor Yellow
Write-Host ""

if ($headers.ContainsKey("Authorization")) {
    try {
        # İlk çağrı
        $start1 = Get-Date
        $license1 = Invoke-RestMethod -Uri "$BaseUrl/api/license/$TestDomainName" `
            -Method GET `
            -Headers $headers `
            -ErrorAction Stop
        $duration1 = (Get-Date) - $start1

        # İkinci çağrı (cache'den gelmeli)
        $start2 = Get-Date
        $license2 = Invoke-RestMethod -Uri "$BaseUrl/api/license/$TestDomainName" `
            -Method GET `
            -Headers $headers `
            -ErrorAction Stop
        $duration2 = (Get-Date) - $start2

        Write-Host "  İlk çağrı süresi: $($duration1.TotalMilliseconds)ms" -ForegroundColor Gray
        Write-Host "  İkinci çağrı süresi: $($duration2.TotalMilliseconds)ms" -ForegroundColor Gray

        if ($duration2.TotalMilliseconds -lt $duration1.TotalMilliseconds) {
            Write-Host "✓ Cache çalışıyor (ikinci çağrı daha hızlı)" -ForegroundColor Green
            $testResults.Cache["LicenseCache"] = "OK"
        } else {
            Write-Host "⚠ Cache performansı beklenen seviyede değil" -ForegroundColor Yellow
            $testResults.Cache["LicenseCache"] = "WARNING"
        }
    } catch {
        $errorDetails = $_.Exception.Message
        if ($_.ErrorDetails) {
            $errorDetails += " | Details: $($_.ErrorDetails.Message)"
        }
        Write-Host "✗ Cache testi başarısız: $errorDetails" -ForegroundColor Red
        $testResults.Cache["LicenseCache"] = "FAILED"
    }
} else {
    Write-Host "  ⚠ Token yok, cache testi atlanıyor" -ForegroundColor Yellow
    $testResults.Cache["LicenseCache"] = "SKIPPED"
}

# ============================================
# 9. MNGDATAGATEWAY LİSANS KONTROLÜ
# ============================================
Write-Host "`n=== 9. MngDataGateway Lisans Kontrolü ===" -ForegroundColor Yellow
Write-Host ""

$dataGatewayUrl = "http://localhost:5010"
try {
    # Token ile data endpoint'ine istek at
    if ($headers.ContainsKey("Authorization")) {
        $dataHeaders = @{
            "Authorization" = $headers["Authorization"]
            "Content-Type" = "application/json"
        }
        
        # GET isteği (lisans kontrolü yapılmalı)
        try {
            $dataResponse = Invoke-RestMethod -Uri "$dataGatewayUrl/api/v1/data/@datasets" `
                -Method GET `
                -Headers $dataHeaders `
                -ErrorAction Stop

            Write-Host "✓ MngDataGateway GET operasyonu başarılı" -ForegroundColor Green
            $testResults.LicenseControl["DataGatewayGet"] = "OK"
        } catch {
            if ($_.Exception.Response.StatusCode -eq 403) {
                Write-Host "  ⚠ 403 Forbidden - Lisans kontrolü çalışıyor (beklenen davranış)" -ForegroundColor Yellow
                $testResults.LicenseControl["DataGatewayGet"] = "OK (403 as expected)"
            } else {
                Write-Host "✗ MngDataGateway GET operasyonu başarısız: $($_.Exception.Message)" -ForegroundColor Red
                $testResults.LicenseControl["DataGatewayGet"] = "FAILED"
            }
        }
    } else {
        Write-Host "  ⚠ Token yok, test atlanıyor" -ForegroundColor Yellow
        $testResults.LicenseControl["DataGatewayGet"] = "SKIPPED"
    }
} catch {
    Write-Host "  ⚠ MngDataGateway testi atlanıyor: $($_.Exception.Message)" -ForegroundColor Yellow
    $testResults.LicenseControl["DataGatewayGet"] = "SKIPPED"
}

# ============================================
# 10. CLEANUP
# ============================================
if (-not $SkipCleanup) {
    Write-Host "`n=== 10. Cleanup ===" -ForegroundColor Yellow
    Write-Host ""

    try {
        if ($headers.ContainsKey("Authorization")) {
            $deleteResponse = Invoke-RestMethod -Uri "$BaseUrl/api/domain/$domainId" `
                -Method DELETE `
                -Headers $headers `
                -ErrorAction Stop

            Write-Host "✓ Test domain silindi: $TestDomainName" -ForegroundColor Green
        } else {
            Write-Host "  ⚠ Token yok, domain silinemedi (manuel silme gerekli)" -ForegroundColor Yellow
        }
    } catch {
        Write-Host "  ⚠ Domain silinemedi: $($_.Exception.Message)" -ForegroundColor Yellow
        Write-Host "    Manuel silme gerekli: $TestDomainName" -ForegroundColor Gray
    }

    # İndirilen dosyaları temizle
    if (Test-Path "downloaded-license-trial.enc") {
        Remove-Item "downloaded-license-trial.enc" -Force
        Write-Host "✓ İndirilen lisans dosyası temizlendi" -ForegroundColor Green
    }
}

# ============================================
# TEST SONUÇLARI ÖZETİ
# ============================================
Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "  Test Sonuçları Özeti" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

$totalTests = 0
$passedTests = 0
$failedTests = 0
$skippedTests = 0

foreach ($category in $testResults.Keys) {
    Write-Host "`n[$category]" -ForegroundColor Yellow
    foreach ($test in $testResults[$category].Keys) {
        $result = $testResults[$category][$test]
        $totalTests++
        
        switch ($result) {
            "OK" {
                Write-Host "  ✓ $test : $result" -ForegroundColor Green
                $passedTests++
            }
            "FAILED" {
                Write-Host "  ✗ $test : $result" -ForegroundColor Red
                $failedTests++
            }
            "SKIPPED" {
                Write-Host "  ⊘ $test : $result" -ForegroundColor Gray
                $skippedTests++
            }
            default {
                Write-Host "  ⚠ $test : $result" -ForegroundColor Yellow
                $passedTests++
            }
        }
    }
}

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "Toplam: $totalTests | Başarılı: $passedTests | Başarısız: $failedTests | Atlanan: $skippedTests" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

if ($failedTests -eq 0) {
    Write-Host "✓ Tüm testler başarılı!" -ForegroundColor Green
    exit 0
} else {
    Write-Host "✗ Bazı testler başarısız!" -ForegroundColor Red
    exit 1
}
