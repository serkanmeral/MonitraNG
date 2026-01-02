# DNS TXT Kaydı Kontrol Script'i
# Let's Encrypt DNS doğrulama için

$domain = "_acme-challenge.monitrang.com"

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "DNS TXT Kaydı Kontrolü" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host ""

# Beklenen değerler
$expectedValues = @(
    "bSheGIV1R7kyb_Zcv_XAKHrizs87I7BttRdbhOCKYf8",
    "Nf0Zsk6R99e5qn_hr2IvcEftfzoDDozMPUbYQmeAzPI"
)

Write-Host "Kontrol edilen domain: $domain" -ForegroundColor Yellow
Write-Host "Beklenen değerler:" -ForegroundColor Yellow
foreach ($value in $expectedValues) {
    Write-Host "  - $value" -ForegroundColor Gray
}
Write-Host ""

# Farklı DNS sunucularından kontrol
$dnsServers = @(
    @{Name = "Google DNS"; IP = "8.8.8.8"},
    @{Name = "Cloudflare DNS"; IP = "1.1.1.1"},
    @{Name = "Quad9 DNS"; IP = "9.9.9.9"}
)

$allFound = $false

foreach ($dnsServer in $dnsServers) {
    Write-Host "Kontrol ediliyor: $($dnsServer.Name) ($($dnsServer.IP))..." -ForegroundColor Cyan
    
    try {
        $result = Resolve-DnsName -Name $domain -Type TXT -Server $dnsServer.IP -ErrorAction Stop
        
        $foundValues = @()
        foreach ($record in $result) {
            if ($record.Strings) {
                foreach ($string in $record.Strings) {
                    $foundValues += $string
                    Write-Host "  ✓ Bulundu: $string" -ForegroundColor Green
                }
            }
        }
        
        # Beklenen değerleri kontrol et
        $missingValues = @()
        foreach ($expected in $expectedValues) {
            if ($foundValues -notcontains $expected) {
                $missingValues += $expected
            }
        }
        
        if ($missingValues.Count -eq 0) {
            Write-Host "  ✅ Tüm değerler bulundu!" -ForegroundColor Green
            $allFound = $true
        } else {
            Write-Host "  ⚠️  Eksik değerler:" -ForegroundColor Yellow
            foreach ($missing in $missingValues) {
                Write-Host "    - $missing" -ForegroundColor Red
            }
        }
    } catch {
        Write-Host "  ❌ Kayıt bulunamadı veya hata oluştu: $($_.Exception.Message)" -ForegroundColor Red
    }
    
    Write-Host ""
}

Write-Host "==========================================" -ForegroundColor Cyan
if ($allFound) {
    Write-Host "✅ DNS kayıtları tüm sunucularda görünüyor!" -ForegroundColor Green
    Write-Host "Certbot terminalinde Enter'a basabilirsiniz." -ForegroundColor Green
} else {
    Write-Host "⚠️  DNS kayıtları henüz tüm sunucularda görünmüyor." -ForegroundColor Yellow
    Write-Host "Birkaç dakika bekleyip tekrar kontrol edin." -ForegroundColor Yellow
}
Write-Host "==========================================" -ForegroundColor Cyan

