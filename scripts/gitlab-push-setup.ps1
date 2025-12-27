# GitLab Push Setup Script
# Bu script, mevcut repository'yi GitLab'a push etmek için gerekli komutları çalıştırır

Write-Host "=== GitLab Push Setup ===" -ForegroundColor Cyan
Write-Host ""

# GitLab proje URL'i
$gitlabUrl = Read-Host "GitLab proje URL'inizi girin (örn: http://localhost/root/monitrang.git)"

if ([string]::IsNullOrWhiteSpace($gitlabUrl)) {
    Write-Host "Hata: GitLab URL'i boş olamaz!" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "GitLab remote'unu ekleniyor..." -ForegroundColor Yellow

# GitLab remote'unu ekle
git remote add gitlab $gitlabUrl

if ($LASTEXITCODE -eq 0) {
    Write-Host "✓ GitLab remote eklendi" -ForegroundColor Green
} else {
    Write-Host "Remote zaten mevcut veya hata oluştu. Mevcut remote'ları kontrol ediliyor..." -ForegroundColor Yellow
    git remote -v
    exit 1
}

Write-Host ""
Write-Host "Remote'lar:" -ForegroundColor Cyan
git remote -v

Write-Host ""
Write-Host "=== Sonraki Adımlar ===" -ForegroundColor Cyan
Write-Host ""
Write-Host "1. Değişiklikleri commit edin (isteğe bağlı):" -ForegroundColor Yellow
Write-Host "   git add ." -ForegroundColor White
Write-Host "   git commit -m 'GitLab entegrasyonu'"
Write-Host ""
Write-Host "2. GitLab'a push edin:" -ForegroundColor Yellow
Write-Host "   git push -u gitlab main" -ForegroundColor White
Write-Host ""
Write-Host "3. Veya tüm branch'leri push edin:" -ForegroundColor Yellow
Write-Host "   git push -u gitlab --all" -ForegroundColor White
Write-Host "   git push -u gitlab --tags" -ForegroundColor White
Write-Host ""

