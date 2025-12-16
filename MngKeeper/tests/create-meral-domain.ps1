# Meral Domain Test Data Creation Script
# Creates domain, groups, and users for bug testing

$baseUrl = "https://localhost:5001"
$domainName = "meral"
$displayName = "Meral Domain"
$adminEmail = "admin@meral.com"
$adminPassword = "Admin123!"

Write-Host "`n=== MERAL DOMAIN TEST DATA OLUŞTURMA ===" -ForegroundColor Cyan
Write-Host ""

# Skip certificate validation for localhost
[System.Net.ServicePointManager]::ServerCertificateValidationCallback = {$true}

# Check if API is running (try version endpoint as health check)
Write-Host "API bağlantısı kontrol ediliyor: $baseUrl" -ForegroundColor Yellow
try {
    $versionCheck = Invoke-RestMethod -Uri "$baseUrl/api/version/short" `
        -Method GET `
        -SkipCertificateCheck `
        -TimeoutSec 5 `
        -ErrorAction Stop
    Write-Host "✓ API çalışıyor (Version: $versionCheck)" -ForegroundColor Green
    Write-Host ""
} catch {
    Write-Host "⚠️  API health check başarısız, devam ediliyor..." -ForegroundColor Yellow
    Write-Host "  (API çalışıyor olabilir, domain oluşturma deneniyor)" -ForegroundColor Gray
    Write-Host ""
}

try {
    # Step 1: Create Domain
    Write-Host "1. Domain oluşturuluyor: $domainName" -ForegroundColor Yellow
    
    $domainBody = @{
        domainName = $domainName
        displayName = $displayName
        adminEmail = $adminEmail
        adminPassword = $adminPassword
    } | ConvertTo-Json
    
    $domainResponse = Invoke-RestMethod -Uri "$baseUrl/api/domain" `
        -Method POST `
        -Body $domainBody `
        -ContentType "application/json" `
        -SkipCertificateCheck `
        -ErrorAction Stop
    
    if (-not $domainResponse.isSuccess) {
        Write-Host "✗ Domain oluşturulamadı: $($domainResponse.message)" -ForegroundColor Red
        exit 1
    }
    
    Write-Host "✓ Domain oluşturuldu: $($domainResponse.domainName)" -ForegroundColor Green
    Write-Host "  Domain ID: $($domainResponse.domainId)" -ForegroundColor Gray
    Write-Host "  Admin Username: $($domainResponse.adminUsername)" -ForegroundColor Gray
    Write-Host ""
    
    # Step 2: Configure Realm Mappers
    Write-Host "2. Realm mapper'ları yapılandırılıyor..." -ForegroundColor Yellow
    
    $mapperResponse = Invoke-RestMethod -Uri "$baseUrl/api/admin/realms/$domainName/configure-mappers" `
        -Method POST `
        -SkipCertificateCheck `
        -ErrorAction Stop
    
    Write-Host "✓ Mapper'lar yapılandırıldı: $($mapperResponse.message)" -ForegroundColor Green
    Write-Host ""
    
    # Step 3: Get Admin Token
    Write-Host "3. Admin token alınıyor..." -ForegroundColor Yellow
    
    $tokenBody = @{
        username = $domainResponse.adminUsername
        password = $adminPassword
        domain = $domainName
    } | ConvertTo-Json
    
    $tokenResponse = Invoke-RestMethod -Uri "$baseUrl/api/auth/token" `
        -Method POST `
        -Body $tokenBody `
        -ContentType "application/json" `
        -SkipCertificateCheck `
        -ErrorAction Stop
    
    $adminToken = $tokenResponse.accessToken
    Write-Host "✓ Admin token alındı" -ForegroundColor Green
    Write-Host ""
    
    $headers = @{
        "Authorization" = "Bearer $adminToken"
        "Content-Type" = "application/json"
    }
    
    # Step 4: Create Additional Groups
    Write-Host "4. Ek gruplar oluşturuluyor..." -ForegroundColor Yellow
    
    $groups = @(
        @{ name = "developers"; description = "Development Team" },
        @{ name = "testers"; description = "Testing Team" },
        @{ name = "viewers"; description = "View Only Access" }
    )
    
    $createdGroups = @()
    foreach ($group in $groups) {
        try {
            $groupBody = $group | ConvertTo-Json
            $groupResponse = Invoke-RestMethod -Uri "$baseUrl/api/group" `
                -Method POST `
                -Headers $headers `
                -Body $groupBody `
                -SkipCertificateCheck `
                -ErrorAction Stop
            
            Write-Host "  ✓ Grup oluşturuldu: $($group.name)" -ForegroundColor Green
            $createdGroups += $groupResponse
        }
        catch {
            Write-Host "  ✗ Grup oluşturulamadı: $($group.name) - $($_.Exception.Message)" -ForegroundColor Red
        }
    }
    Write-Host ""
    
    # Get all groups to create name-to-ID mapping
    Write-Host "Gruplar listeleniyor (ID mapping için)..." -ForegroundColor Yellow
    try {
        $allGroupsResponse = Invoke-RestMethod -Uri "$baseUrl/api/group?page=1&pageSize=100" `
            -Method GET `
            -Headers $headers `
            -SkipCertificateCheck `
            -ErrorAction Stop
        
        $groupNameToId = @{}
        foreach ($grp in $allGroupsResponse.groups) {
            $groupNameToId[$grp.name] = $grp.groupId
        }
        Write-Host "  ✓ $($groupNameToId.Count) grup bulundu" -ForegroundColor Green
    }
    catch {
        Write-Host "  ✗ Gruplar listelenemedi: $($_.Exception.Message)" -ForegroundColor Red
        $groupNameToId = @{}
    }
    Write-Host ""
    
    # Step 5: Create Users
    Write-Host "5. Kullanıcılar oluşturuluyor..." -ForegroundColor Yellow
    
    $users = @(
        @{
            username = "serkan.meral"
            email = "serkan.meral@outlook.com"
            password = "Serkan123!"
            firstName = "Serkan"
            lastName = "MERAL"
            groupNames = @("users", "developers")  # Changed to groupNames
            isActive = $true
        },
        @{
            username = "test.user1"
            email = "test.user1@meral.com"
            password = "Test123!"
            firstName = "Test"
            lastName = "User1"
            groupNames = @("users")  # Changed to groupNames
            isActive = $true
        },
        @{
            username = "test.user2"
            email = "test.user2@meral.com"
            password = "Test123!"
            firstName = "Test"
            lastName = "User2"
            groupNames = @("users", "testers")  # Changed to groupNames
            isActive = $true
        },
        @{
            username = "manager.user"
            email = "manager@meral.com"
            password = "Manager123!"
            firstName = "Manager"
            lastName = "User"
            groupNames = @("users", "managers")  # Changed to groupNames
            isActive = $true
        }
    )
    
    $createdUsers = @()
    foreach ($user in $users) {
        try {
            # Convert group names to IDs
            $groupIds = @()
            if ($user.groupNames) {
                foreach ($groupName in $user.groupNames) {
                    if ($groupNameToId.ContainsKey($groupName)) {
                        $groupIds += $groupNameToId[$groupName]
                    }
                    else {
                        Write-Host "  ⚠ Grup bulunamadı: $groupName (kullanıcı oluşturulurken atlanacak)" -ForegroundColor Yellow
                    }
                }
            }
            
            # Create user object with groupIds
            $userToCreate = @{
                username = $user.username
                email = $user.email
                password = $user.password
                firstName = $user.firstName
                lastName = $user.lastName
                groupIds = $groupIds
                isActive = $user.isActive
            }
            
            $userBody = $userToCreate | ConvertTo-Json -Depth 3
            $userResponse = Invoke-RestMethod -Uri "$baseUrl/api/user" `
                -Method POST `
                -Headers $headers `
                -Body $userBody `
                -SkipCertificateCheck `
                -ErrorAction Stop
            
            Write-Host "  ✓ Kullanıcı oluşturuldu: $($user.username) ($($user.email))" -ForegroundColor Green
            if ($groupIds.Count -gt 0) {
                Write-Host "    Gruplar: $($user.groupNames -join ', ')" -ForegroundColor Gray
            }
            $createdUsers += $userResponse
        }
        catch {
            Write-Host "  ✗ Kullanıcı oluşturulamadı: $($user.username) - $($_.Exception.Message)" -ForegroundColor Red
        }
    }
    Write-Host ""
    
    # Summary
    Write-Host "=== ÖZET ===" -ForegroundColor Green
    Write-Host "Domain: $domainName" -ForegroundColor Cyan
    Write-Host "Admin Username: $($domainResponse.adminUsername)" -ForegroundColor Cyan
    Write-Host "Admin Email: $adminEmail" -ForegroundColor Cyan
    Write-Host "Admin Password: $adminPassword" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Oluşturulan Gruplar: $($createdGroups.Count)" -ForegroundColor Cyan
    Write-Host "Oluşturulan Kullanıcılar: $($createdUsers.Count)" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Özel Kullanıcı:" -ForegroundColor Yellow
    Write-Host "  - Username: serkan.meral" -ForegroundColor Gray
    Write-Host "  - Email: serkan.meral@outlook.com" -ForegroundColor Gray
    Write-Host "  - Name: Serkan MERAL" -ForegroundColor Gray
    Write-Host "  - Groups: users, developers" -ForegroundColor Gray
    Write-Host ""
    Write-Host "✓ Test verileri başarıyla oluşturuldu!" -ForegroundColor Green
    
} catch {
    Write-Host "`n✗ Hata oluştu: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "  Detay: $($_.Exception)" -ForegroundColor Gray
    exit 1
}

