# Add User to Multiple Groups Script
param(
    [string]$DomainName = "meral8",
    [string]$Username = "serkan.meral"
)

$baseUrl = "https://localhost:5001"

Write-Host "`n=== KULLANICIYI GRUPLARA EKLEME ===" -ForegroundColor Cyan
Write-Host "Domain: $DomainName" -ForegroundColor Yellow
Write-Host "Kullanıcı: $Username" -ForegroundColor Yellow
Write-Host ""

# Skip certificate validation for localhost
[System.Net.ServicePointManager]::ServerCertificateValidationCallback = {$true}

# Step 1: Get Admin Token
Write-Host "1. Admin token alınıyor..." -ForegroundColor Yellow
try {
    $adminTokenResponse = Invoke-RestMethod -Uri "$baseUrl/api/auth/token" `
        -Method POST `
        -ContentType "application/json" `
        -Body (@{
            username = "${DomainName}_admin"
            password = "Admin123!"
            domain = $DomainName
        } | ConvertTo-Json) `
        -SkipCertificateCheck `
        -ErrorAction Stop
    
    $adminToken = $adminTokenResponse.accessToken
    Write-Host "✓ Admin token alındı" -ForegroundColor Green
    Write-Host ""
} catch {
    Write-Host "✗ Admin token alınamadı: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

$headers = @{
    "Authorization" = "Bearer $adminToken"
    "Content-Type" = "application/json"
}

# Step 2: Get User ID
Write-Host "2. Kullanıcı bilgileri alınıyor..." -ForegroundColor Yellow
try {
    $usersResponse = Invoke-RestMethod -Uri "$baseUrl/api/user?page=1&pageSize=100" `
        -Method GET `
        -Headers $headers `
        -SkipCertificateCheck `
        -ErrorAction Stop
    
    $user = $usersResponse.users | Where-Object { $_.username -eq $Username } | Select-Object -First 1
    
    if (-not $user) {
        Write-Host "✗ Kullanıcı bulunamadı: $Username" -ForegroundColor Red
        exit 1
    }
    
    Write-Host "✓ Kullanıcı bulundu: $($user.username) (ID: $($user.userId))" -ForegroundColor Green
    Write-Host "  Mevcut gruplar: $($user.groups -join ', ')" -ForegroundColor Gray
    Write-Host ""
} catch {
    Write-Host "✗ Kullanıcı bilgileri alınamadı: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# Step 3: Get Groups
Write-Host "3. Gruplar listeleniyor..." -ForegroundColor Yellow
try {
    $groupsResponse = Invoke-RestMethod -Uri "$baseUrl/api/group?page=1&pageSize=100" `
        -Method GET `
        -Headers $headers `
        -SkipCertificateCheck `
        -ErrorAction Stop
    
    $allGroups = $groupsResponse.groups
    Write-Host "✓ $($allGroups.Count) grup bulundu" -ForegroundColor Green
    Write-Host ""
} catch {
    Write-Host "✗ Gruplar listelenemedi: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# Step 4: Add User to Multiple Groups
Write-Host "4. Kullanıcı gruplara ekleniyor..." -ForegroundColor Yellow

# Groups to add (excluding groups user is already in)
$groupsToAdd = @("testers", "viewers", "managers")
$currentGroups = $user.groups

$addedGroups = @()
$failedGroups = @()

foreach ($groupName in $groupsToAdd) {
    # Check if user is already in this group
    if ($currentGroups -contains $groupName) {
        Write-Host "  ⚠ Kullanıcı zaten '$groupName' grubunda" -ForegroundColor Yellow
        continue
    }
    
    # Find group by name
    $group = $allGroups | Where-Object { $_.name -eq $groupName } | Select-Object -First 1
    
    if (-not $group) {
        Write-Host "  ✗ Grup bulunamadı: $groupName" -ForegroundColor Red
        $failedGroups += $groupName
        continue
    }
    
    try {
        $addResponse = Invoke-RestMethod -Uri "$baseUrl/api/user/$($user.userId)/groups/$($group.groupId)" `
            -Method POST `
            -Headers $headers `
            -SkipCertificateCheck `
            -ErrorAction Stop
        
        Write-Host "  ✓ Kullanıcı '$groupName' grubuna eklendi" -ForegroundColor Green
        $addedGroups += $groupName
    } catch {
        Write-Host "  ✗ Kullanıcı '$groupName' grubuna eklenemedi: $($_.Exception.Message)" -ForegroundColor Red
        $failedGroups += $groupName
    }
}

Write-Host ""
Write-Host "=== ÖZET ===" -ForegroundColor Cyan
Write-Host "Eklenen gruplar: $($addedGroups.Count)" -ForegroundColor $(if ($addedGroups.Count -gt 0) { "Green" } else { "Gray" })
if ($addedGroups.Count -gt 0) {
    Write-Host "  - $($addedGroups -join ', ')" -ForegroundColor White
}
Write-Host "Başarısız: $($failedGroups.Count)" -ForegroundColor $(if ($failedGroups.Count -gt 0) { "Red" } else { "Gray" })
if ($failedGroups.Count -gt 0) {
    Write-Host "  - $($failedGroups -join ', ')" -ForegroundColor White
}

# Step 5: Get Updated User Info
Write-Host ""
Write-Host "5. Güncel kullanıcı bilgileri alınıyor..." -ForegroundColor Yellow
try {
    $updatedUserResponse = Invoke-RestMethod -Uri "$baseUrl/api/user/$($user.userId)" `
        -Method GET `
        -Headers $headers `
        -SkipCertificateCheck `
        -ErrorAction Stop
    
    Write-Host "✓ Güncel gruplar: $($updatedUserResponse.groups -join ', ')" -ForegroundColor Green
    Write-Host ""
} catch {
    Write-Host "✗ Güncel kullanıcı bilgileri alınamadı: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host "=== TAMAMLANDI ===" -ForegroundColor Cyan
Write-Host "Şimdi token alıp user_groups claim'ini kontrol edebilirsiniz:" -ForegroundColor Yellow
Write-Host "  pwsh -ExecutionPolicy Bypass -File 'MngKeeper/tests/get-user-token.ps1'" -ForegroundColor White
Write-Host ""

