# Mail Gönderme Test Scripti - Local Makineden SSH ile
# Kullanım: .\test-send-mail.ps1 -ToEmail <email> [-Subject <subject>] [-Body <body>]

param(
    [Parameter(Mandatory=$false)]
    [string]$ToEmail = "serkan.meral@outlook.com",
    
    [Parameter(Mandatory=$false)]
    [string]$Subject = "Test Mail from MonitraNG Server",
    
    [Parameter(Mandatory=$false)]
    [string]$Body = "Bu bir test mailidir. Production sunucusundan SSH ile gönderilmiştir.",
    
    [Parameter(Mandatory=$false)]
    [string]$Server = "monitrang-server"
)

Write-Host "📧 Mail Gönderme Testi (SSH)" -ForegroundColor Yellow
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Gray
Write-Host "Alıcı: $ToEmail" -ForegroundColor Cyan
Write-Host "Konu: $Subject" -ForegroundColor Cyan
Write-Host "Sunucu: $Server" -ForegroundColor Cyan
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Gray
Write-Host ""

# SSH ile sunucudaki scripti çalıştır
$scriptPath = "/root/MonitraNG/scripts/tests/Mailu/test-send-mail.sh"
$command = "cd /root/MonitraNG/scripts/tests/Mailu && ./test-send-mail.sh '$ToEmail' '$Subject' '$Body'"

try {
    Write-Host "Sunucuya bağlanılıyor ve mail gönderiliyor..." -ForegroundColor Yellow
    $result = ssh root@$Server $command 2>&1
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✅ Mail başarıyla gönderildi!" -ForegroundColor Green
        Write-Host ""
        Write-Host "Çıktı:" -ForegroundColor Gray
        Write-Host $result
        exit 0
    } else {
        Write-Host "❌ Mail gönderilemedi!" -ForegroundColor Red
        Write-Host ""
        Write-Host "Hata:" -ForegroundColor Red
        Write-Host $result
        exit 1
    }
} catch {
    Write-Host "❌ SSH bağlantı hatası: $_" -ForegroundColor Red
    Write-Host ""
    Write-Host "Kontrol edin:" -ForegroundColor Yellow
    Write-Host "  - SSH bağlantısı çalışıyor mu? (ssh root@$Server)" -ForegroundColor Gray
    Write-Host "  - Sunucudaki script mevcut mu? ($scriptPath)" -ForegroundColor Gray
    exit 1
}

