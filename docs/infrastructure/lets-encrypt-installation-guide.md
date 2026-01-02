# Let's Encrypt Wildcard Sertifikası Kurulum Rehberi

**Tarih:** 2 Ocak 2026  
**Domain:** `monitrang.com`  
**Sertifika Tipi:** Wildcard (`*.monitrang.com` + `monitrang.com`)

---

## 📋 Ön Gereksinimler

- ✅ Certbot kurulu (certbot 2.1.0)
- ✅ DNS kayıtları doğru yapılandırılmış
- ✅ Nginx çalışıyor
- ✅ Port 80 açık (HTTP doğrulama için)

---

## 🚀 Kurulum Adımları

### Adım 1: Certbot Komutunu Çalıştırma

Sunucuda şu komutu çalıştırın:

```bash
certbot certonly --manual --preferred-challenges dns \
  -d "*.monitrang.com" \
  -d "monitrang.com" \
  --email admin@monitrang.com \
  --agree-tos \
  --no-eff-email \
  --manual-public-ip-logging-ok
```

**Beklenen Çıktı:**
```
Saving debug log to /var/log/letsencrypt/letsencrypt.log
Requesting a certificate for *.monitrang.com and monitrang.com

- - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
Please deploy a DNS TXT record under the name
_acme-challenge.monitrang.com with the following value:

abc123xyz789... (uzun bir string)

Before continuing, verify the record is deployed.
- - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
Press Enter to Continue
```

### Adım 2: DNS TXT Kaydı Ekleme

Certbot size bir veya birden fazla TXT kaydı verebilir (wildcard + ana domain için). Bu kayıtları Hosting Dünyam DNS panelinde eklemeniz gerekiyor:

**ÖNEMLİ:** Eğer Certbot size **iki farklı TXT kaydı** veriyorsa (wildcard ve ana domain için), her ikisini de eklemeniz gerekiyor.

#### Seçenek A: Birden Fazla TXT Kaydı (Önerilen)

Hosting Dünyam DNS paneline giriş yapın ve **iki ayrı TXT kaydı** ekleyin:

**Kayıt 1:**
1. **Yeni kayıt ekle** butonuna tıklayın
2. **Form alanlarını doldurun:**
   - **Type:** `TXT`
   - **Name:** `_acme-challenge` (sadece bu, nokta veya domain adı eklemeyin)
   - **Content:** İlk TXT değeri (Certbot'un verdiği ilk string)
   - **TTL:** `300` (veya `3600`)
3. **Kaydet** butonuna tıklayın

**Kayıt 2:**
1. **Yeni kayıt ekle** butonuna tekrar tıklayın
2. **Form alanlarını doldurun:**
   - **Type:** `TXT`
   - **Name:** `_acme-challenge` (aynı isim)
   - **Content:** İkinci TXT değeri (Certbot'un verdiği ikinci string)
   - **TTL:** `300` (veya `3600`)
3. **Kaydet** butonuna tıklayın

**Örnek DNS Kayıtları:**
```
Kayıt 1:
Type: TXT
Name: _acme-challenge
Content: bSheGIV1R7kyb_Zcv_XAKHrizs87I7BttRdbhOCKYf8
TTL: 300

Kayıt 2:
Type: TXT
Name: _acme-challenge
Content: Nf0Zsk6R99e5qn_hr2IvcEftfzoDDozMPUbYQmeAzPI
TTL: 300
```

**Not:** Bazı DNS sağlayıcıları aynı isimde birden fazla TXT kaydına izin verir. Eğer izin vermiyorsa, Seçenek B'yi kullanın.

#### Seçenek B: Tek TXT Kaydı (Çoklu Değer)

Eğer DNS sağlayıcınız aynı isimde birden fazla TXT kaydına izin vermiyorsa, tek bir TXT kaydına her iki değeri ekleyin (DNS sağlayıcınız destekliyorsa):

```
Type: TXT
Name: _acme-challenge
Content: bSheGIV1R7kyb_Zcv_XAKHrizs87I7BttRdbhOCKYf8 Nf0Zsk6R99e5qn_hr2IvcEftfzoDDozMPUbYQmeAzPI
TTL: 300
```

### Adım 3: DNS Propagation Kontrolü

DNS kayıtlarını ekledikten sonra, kayıtların propagate olduğunu kontrol edin:

**Sunucuda Kontrol (Önerilen):**

```bash
# Kontrol script'ini çalıştır (scripts/check-dns-txt.sh)
bash scripts/check-dns-txt.sh

# Veya manuel kontrol
dig +short TXT _acme-challenge.monitrang.com @8.8.8.8
dig +short TXT _acme-challenge.monitrang.com @1.1.1.1
dig +short TXT _acme-challenge.monitrang.com @9.9.9.9
```

**Windows'ta Kontrol:**

```powershell
# PowerShell script'ini çalıştır
.\scripts\check-dns-txt.ps1

# Veya manuel kontrol
Resolve-DnsName -Name _acme-challenge.monitrang.com -Type TXT -Server 8.8.8.8
Resolve-DnsName -Name _acme-challenge.monitrang.com -Type TXT -Server 1.1.1.1
```

**Beklenen Çıktı (Her iki değer de görünmeli):**
```
"bSheGIV1R7kyb_Zcv_XAKHrizs87I7BttRdbhOCKYf8"
"Nf0Zsk6R99e5qn_hr2IvcEftfzoDDozMPUbYQmeAzPI"
```

**Not:** 
- DNS propagation 1-5 dakika sürebilir
- Tüm DNS sunucularında **her iki değer de** görünene kadar bekleyin
- Eğer sadece bir değer görünüyorsa, diğer kaydı da eklediğinizden emin olun

### Adım 4: Certbot'a Devam Etme

DNS kaydı propagate olduktan sonra, certbot terminalinde **Enter** tuşuna basın.

Certbot DNS kaydını kontrol edecek ve sertifikayı oluşturacak.

**Beklenen Çıktı:**
```
Successfully received certificate.
Certificate is saved at: /etc/letsencrypt/live/monitrang.com/fullchain.pem
Key is saved at:         /etc/letsencrypt/live/monitrang.com/privkey.pem
This certificate expires on 2026-04-02.
These files will be updated when the certificate renews.
Certbot has set up a scheduled task to automatically renew this certificate in the background.
```

### Adım 5: Nginx Yapılandırmasını Güncelleme

Sertifika başarıyla oluşturulduktan sonra, Nginx yapılandırmasını güncelleyin:

**Otomatik Güncelleme (Önerilen):**

```bash
# Script'i repository'den sunucuya kopyalayın
scp scripts/update-nginx-ssl.sh root@monitrang-server:/root/

# Script'i çalıştırın
ssh root@monitrang-server "chmod +x /root/update-nginx-ssl.sh && /root/update-nginx-ssl.sh"
```

**Manuel Güncelleme:**

```bash
# Yapılandırma dosyasını düzenle
sudo nano /etc/nginx/sites-available/monitrang
```

**SSL sertifika satırlarını güncelleyin:**

```nginx
# Eski (self-signed):
# ssl_certificate /etc/nginx/ssl/monitrang.crt;
# ssl_certificate_key /etc/nginx/ssl/monitrang.key;

# Yeni (Let's Encrypt):
ssl_certificate /etc/letsencrypt/live/monitrang.com/fullchain.pem;
ssl_certificate_key /etc/letsencrypt/live/monitrang.com/privkey.pem;
```

**Tüm server block'larda (app, api, auth, docs, gitlab) güncelleyin.**

**Not:** Eğer dosyada zaten aktif self-signed sertifika satırları varsa, bunları da değiştirmeniz gerekir:

```bash
# Tüm self-signed sertifika satırlarını Let's Encrypt ile değiştir
sudo sed -i 's|ssl_certificate /etc/nginx/ssl/monitrang.crt;|ssl_certificate /etc/letsencrypt/live/monitrang.com/fullchain.pem;|g' /etc/nginx/sites-available/monitrang
sudo sed -i 's|ssl_certificate_key /etc/nginx/ssl/monitrang.key;|ssl_certificate_key /etc/letsencrypt/live/monitrang.com/privkey.pem;|g' /etc/nginx/sites-available/monitrang
```

### Adım 6: Nginx Yapılandırmasını Test Etme

```bash
# Yapılandırmayı test et
sudo nginx -t
```

**Beklenen Çıktı:**
```
nginx: the configuration file /etc/nginx/nginx.conf syntax is ok
nginx: configuration file /etc/nginx/nginx.conf test is successful
```

### Adım 7: Nginx'i Yeniden Başlatma

```bash
# Nginx'i yeniden başlat
sudo systemctl reload nginx

# Durumu kontrol et
sudo systemctl status nginx
```

### Adım 8: Sertifikayı Test Etme

```bash
# SSL sertifikasını test et
openssl s_client -connect monitrang.com:443 -servername monitrang.com < /dev/null 2>/dev/null | openssl x509 -noout -dates

# Browser'dan test
# https://monitrang.com adresine gidin
# Adres çubuğundaki kilit simgesine tıklayın
# Sertifika detaylarını kontrol edin
```

---

## 🔄 Otomatik Yenileme Yapılandırması

### Certbot Timer Kontrolü

```bash
# Timer durumunu kontrol et
sudo systemctl status certbot.timer

# Timer'ı etkinleştir (zaten etkin olmalı)
sudo systemctl enable certbot.timer

# Timer'ı başlat
sudo systemctl start certbot.timer
```

### Yenileme Testi

```bash
# Dry-run (test modu)
sudo certbot renew --dry-run
```

### Yenileme Sonrası Nginx Yeniden Yükleme

Certbot otomatik olarak Nginx'i yeniden yükler, ancak manuel kontrol için:

```bash
# Renewal hook oluştur
sudo nano /etc/letsencrypt/renewal-hooks/deploy/reload-nginx.sh
```

```bash
#!/bin/bash
systemctl reload nginx
```

```bash
# Script'i çalıştırılabilir yap
sudo chmod +x /etc/letsencrypt/renewal-hooks/deploy/reload-nginx.sh
```

---

## 🧹 DNS TXT Kaydını Temizleme

Sertifika başarıyla oluşturulduktan sonra, DNS'teki `_acme-challenge` TXT kaydını silebilirsiniz (opsiyonel):

1. Hosting Dünyam DNS paneline giriş yapın
2. `_acme-challenge` TXT kaydını bulun
3. Silin veya devre dışı bırakın

**Not:** Bu kayıt sadece sertifika oluşturma sırasında gereklidir. Sertifika oluşturulduktan sonra silinebilir.

---

## ⚠️ Sorun Giderme

### Sorun 1: DNS Propagation Bekleme

**Sorun:** DNS kaydı henüz propagate olmadı.

**Çözüm:**
```bash
# Farklı DNS sunucularından kontrol et
dig @8.8.8.8 +short TXT _acme-challenge.monitrang.com
dig @1.1.1.1 +short TXT _acme-challenge.monitrang.com

# Tüm sunucularda görünene kadar bekleyin (1-5 dakika)
```

### Sorun 2: Rate Limit

**Sorun:** Çok fazla sertifika isteği yapıldı.

**Çözüm:**
```bash
# Staging environment kullan (sınırsız)
certbot certonly --staging --manual --preferred-challenges dns \
  -d "*.monitrang.com" -d "monitrang.com"
```

### Sorun 3: Sertifika Yenileme Başarısız

**Sorun:** Otomatik yenileme çalışmıyor.

**Çözüm:**
```bash
# Timer durumunu kontrol et
sudo systemctl status certbot.timer

# Manuel yenileme dene
sudo certbot renew --dry-run

# Log'ları kontrol et
sudo tail -f /var/log/letsencrypt/letsencrypt.log
```

---

## ✅ Kontrol Listesi

- [ ] Certbot kurulu
- [ ] DNS kayıtları doğru
- [ ] Certbot komutu çalıştırıldı
- [ ] DNS TXT kaydı eklendi
- [ ] DNS propagation kontrol edildi
- [ ] Sertifika başarıyla oluşturuldu
- [ ] Nginx yapılandırması güncellendi
- [ ] Nginx test edildi ve yeniden başlatıldı
- [ ] SSL sertifikası test edildi
- [ ] Otomatik yenileme yapılandırıldı
- [ ] DNS TXT kaydı temizlendi (opsiyonel)

---

**Son Güncelleme:** 2 Ocak 2026

