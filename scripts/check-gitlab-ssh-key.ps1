# GitLab CI/CD SSH Key Kontrol Script'i
# Bu script, SSH key'in GitLab CI/CD Variables'da olup olmadığını kontrol eder

Write-Host "=== GitLab CI/CD SSH Key Kontrol ===" -ForegroundColor Cyan
Write-Host ""

# 1. Lokal SSH key kontrolu
Write-Host "1. Lokal SSH Key Kontrolu:" -ForegroundColor Yellow
$sshKeyPath = "$env:USERPROFILE\.ssh\gitlab_deploy_key"
if (Test-Path $sshKeyPath) {
    Write-Host "   [OK] gitlab_deploy_key bulundu: $sshKeyPath" -ForegroundColor Green
    $keyInfo = Get-Item $sshKeyPath
    Write-Host "   Olusturulma: $($keyInfo.LastWriteTime)" -ForegroundColor Gray
    Write-Host "   Boyut: $($keyInfo.Length) bytes" -ForegroundColor Gray
    
    # Key fingerprint kontrolu
    $pubKeyPath = "$env:USERPROFILE\.ssh\gitlab_deploy_key.pub"
    if (Test-Path $pubKeyPath) {
        Write-Host "   [OK] Public key bulundu: $pubKeyPath" -ForegroundColor Green
        Write-Host "   Public key:" -ForegroundColor Gray
        Get-Content $pubKeyPath | ForEach-Object { Write-Host "      $_" -ForegroundColor DarkGray }
    }
} else {
    Write-Host "   [WARN] gitlab_deploy_key bulunamadi" -ForegroundColor Yellow
    Write-Host "   Key olusturmak icin: ssh-keygen -t ed25519 -C 'gitlab-ci-deploy' -f `"$sshKeyPath`" -N `"`"" -ForegroundColor Gray
}

Write-Host ""

# 2. GitLab Variables kontrolu (manuel)
Write-Host "2. GitLab CI/CD Variables Kontrolu:" -ForegroundColor Yellow
Write-Host "   [WARN] Bu kontrol manuel olarak yapilmalidir:" -ForegroundColor Yellow
Write-Host ""
Write-Host "   Adimlar:" -ForegroundColor Cyan
Write-Host "   1. GitLab'a gidin: http://localhost/root/MonitraNG" -ForegroundColor White
Write-Host "      (veya GitLab URL'iniz: http://45.141.151.52:8090/root/MonitraNG)" -ForegroundColor DarkGray
Write-Host "   2. Settings > CI/CD > Variables bolumune gidin" -ForegroundColor White
Write-Host "   3. Su variable'lari kontrol edin:" -ForegroundColor White
Write-Host "      - DEPLOY_SSH_PRIVATE_KEY (oncelikli)" -ForegroundColor Cyan
Write-Host "      - SSH_PRIVATE_KEY (alternatif)" -ForegroundColor Cyan
Write-Host ""

# 3. SSH baglanti testi
Write-Host "3. SSH Baglanti Testi:" -ForegroundColor Yellow
if (Test-Path $sshKeyPath) {
    Write-Host "   Sunucuya baglanti testi yapiliyor..." -ForegroundColor Gray
    $testResult = ssh -i $sshKeyPath -o ConnectTimeout=5 -o StrictHostKeyChecking=no root@monitrang-server "echo 'SSH_TEST_SUCCESS'" 2>&1
    if ($testResult -match "SSH_TEST_SUCCESS") {
        Write-Host "   [OK] SSH baglantisi basarili!" -ForegroundColor Green
        Write-Host "   [OK] Key sunucuda authorized_keys icinde" -ForegroundColor Green
    } else {
        Write-Host "   [WARN] SSH baglantisi basarisiz veya key authorized_keys'te yok" -ForegroundColor Yellow
        Write-Host "   Public key'i sunucuya eklemek icin:" -ForegroundColor Gray
        Write-Host "      Get-Content `"$env:USERPROFILE\.ssh\gitlab_deploy_key.pub`" | ssh root@monitrang-server 'cat >> ~/.ssh/authorized_keys'" -ForegroundColor DarkGray
    }
} else {
    Write-Host "   [WARN] SSH key bulunamadi, test yapilamiyor" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "=== Ozet ===" -ForegroundColor Cyan
Write-Host ""
Write-Host "[OK] Lokal key kontrolu tamamlandi" -ForegroundColor Green
Write-Host "[WARN] GitLab Variables kontrolu manuel yapilmalidir (yukaridaki adimlari takip edin)" -ForegroundColor Yellow
Write-Host ""

# 4. Key icerigini gosterme (opsiyonel)
Write-Host "4. Private Key Icerigi (GitLab'a eklemek icin):" -ForegroundColor Yellow
Write-Host "   [WARN] DIKKAT: Private key hassas bilgidir!" -ForegroundColor Red
$showKey = Read-Host "   Key icerigini gostermek istiyor musunuz? (y/N)"
if ($showKey -eq "y" -or $showKey -eq "Y") {
    if (Test-Path $sshKeyPath) {
        Write-Host ""
        Write-Host "   Private Key Icerigi:" -ForegroundColor Cyan
        Write-Host "   " + ("=" * 60) -ForegroundColor DarkGray
        Get-Content $sshKeyPath | ForEach-Object { Write-Host "   $_" -ForegroundColor DarkGray }
        Write-Host "   " + ("=" * 60) -ForegroundColor DarkGray
        Write-Host ""
        Write-Host "   Bu icerigi GitLab CI/CD Variables'a ekleyin:" -ForegroundColor Yellow
        Write-Host "      Key: DEPLOY_SSH_PRIVATE_KEY" -ForegroundColor White
        Write-Host "      Value: (yukaridaki icerigin tamami)" -ForegroundColor White
        Write-Host "      Type: Variable" -ForegroundColor White
        Write-Host "      Protected: [OK] (isaretle)" -ForegroundColor White
        Write-Host "      Masked: [OK] (isaretle)" -ForegroundColor White
    }
}

Write-Host ""
Write-Host "[OK] Kontrol tamamlandi!" -ForegroundColor Green
