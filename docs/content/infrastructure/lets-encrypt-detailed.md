# Let's Encrypt Detaylı Rehber

**Tarih:** 2 Ocak 2026  
**Domain:** `monitrang.com`  
**Production Sunucu IP:** `45.141.151.52`

---

## 📋 Let's Encrypt Nedir?

**Let's Encrypt**, kar amacı gütmeyen bir **Internet Security Research Group (ISRG)** projesidir. 2015 yılından beri ücretsiz SSL/TLS sertifikaları sağlamaktadır.

### Temel Bilgiler

- **Kuruluş:** 2015
- **Sertifika Tipi:** Domain Validation (DV)
- **Maliyet:** Tamamen ücretsiz
- **Geçerlilik Süresi:** 90 gün
- **Otomatik Yenileme:** Evet (Certbot ile)
- **Güvenilirlik:** Tüm modern browser'lar tarafından güvenilir
- **CA (Certificate Authority):** IdenTrust tarafından cross-signed

---

## ✅ Avantajlar

### 1. Tamamen Ücretsiz
- Hiçbir maliyet yok
- Gizli ücret yok
- Sınırsız sertifika

### 2. Otomatik Yenileme
- Certbot ile otomatik yenileme
- 90 günlük geçerlilik süresi
- Yenileme 30 gün öncesinden başlar
- Manuel müdahale gerektirmez

### 3. Güvenilir
- Tüm modern browser'lar tarafından güvenilir
- Chrome, Firefox, Safari, Edge desteği
- Browser uyarısı yok
- Production için uygun

### 4. Kolay Kurulum
- Certbot ile tek komutla kurulum
- Nginx, Apache entegrasyonu
- Otomatik yapılandırma

### 5. Wildcard Sertifikası Desteği
- `*.monitrang.com` formatında
- Tüm subdomain'ler için tek sertifika
- Sınırsız subdomain

### 6. Hızlı Aktivasyon
- Anında sertifika alımı
- DNS doğrulama ile dakikalar içinde
- HTTP doğrulama ile saniyeler içinde

---

## ❌ Dezavantajlar ve Sınırlamalar

### 1. Internet Bağlantısı Gerekir
- Let's Encrypt API'sine erişim olmalı
- Air-gapped sistemlerde kullanılamaz
- Online doğrulama gerekir

### 2. Domain Validation (DV) Sadece
- Organization Validation (OV) yok
- Extended Validation (EV) yok
- Sadece domain sahipliği doğrulanır

### 3. 90 Günlük Geçerlilik
- Kısa geçerlilik süresi
- Otomatik yenileme gerekir
- Yenileme başarısız olursa sertifika geçersiz olur

### 4. Rate Limiting
- **Sertifika başına:** 50 sertifika/hafta/domain
- **Duplicate sertifika:** 5 sertifika/hafta/domain
- **Yenileme:** Sınırsız (aynı sertifika için)
- **Failed validation:** 5 başarısız deneme/hafta/domain

### 5. Sadece Public Domain'ler
- Internal domain'ler için kullanılamaz
- `.local`, `.internal` gibi domain'ler desteklenmez
- Public DNS kayıtları gerekir

---

## 🔐 Güvenlik ve Güvenilirlik

### CA (Certificate Authority) Durumu

**Let's Encrypt**, **IdenTrust** tarafından cross-signed edilmiştir. Bu sayede:
- Eski browser'lar tarafından da güvenilir
- Root CA olarak tanınır
- Tüm işletim sistemlerinde güvenilir

### Sertifika Zinciri

```
Let's Encrypt Root CA
    └── Let's Encrypt Intermediate CA
            └── Your Domain Certificate
```

### Güvenlik Özellikleri

- **SHA-256** şifreleme
- **RSA 2048-bit** veya **ECDSA P-256** anahtarlar
- **OCSP Stapling** desteği
- **HSTS** önerilir

---

## 🚀 Kurulum Süreci

### Ön Gereksinimler

1. **Domain Sahipliği:** Domain'in size ait olması
2. **DNS Kayıtları:** Domain'in doğru IP'ye yönlendirilmesi
3. **Port 80 Erişimi:** HTTP doğrulama için (veya DNS doğrulama)
4. **Root/Sudo Erişimi:** Sertifika yükleme için

### Adım 1: Certbot Kurulumu

```bash
# Debian/Ubuntu
sudo apt update
sudo apt install -y certbot python3-certbot-nginx

# Certbot versiyonunu kontrol et
certbot --version
```

### Adım 2: DNS Kayıtlarını Doğrulama

```bash
# Her subdomain için DNS kayıtlarını kontrol et
dig +short monitrang.com
dig +short www.monitrang.com
dig +short app.monitrang.com
dig +short api.monitrang.com
dig +short auth.monitrang.com
dig +short docs.monitrang.com
dig +short gitlab.monitrang.com

# Tüm kayıtlar 45.141.151.52'yi göstermeli
```

### Adım 3: Sertifika Alma Seçenekleri

#### Seçenek A: Tek Domain İçin

```bash
# Ana domain ve www için
sudo certbot --nginx -d monitrang.com -d www.monitrang.com
```

#### Seçenek B: Wildcard Sertifikası (Önerilen)

**Wildcard sertifikası**, tüm subdomain'ler için tek sertifika sağlar:

```bash
# DNS doğrulama ile wildcard sertifikası
sudo certbot certonly --manual --preferred-challenges dns \
  -d "*.monitrang.com" -d "monitrang.com"
```

**Adımlar:**
1. Certbot size bir TXT kaydı verecek
2. DNS panelinde `_acme-challenge.monitrang.com` için TXT kaydı ekleyin
3. DNS propagation'ı bekleyin (birkaç dakika)
4. Certbot'a devam edin (Enter'a basın)
5. Sertifika oluşturulacak

**Örnek TXT Kaydı:**
```
Type: TXT
Name: _acme-challenge.monitrang.com
Value: abc123xyz789... (Certbot tarafından verilir)
TTL: 300
```

#### Seçenek C: Her Subdomain İçin Ayrı Sertifika

```bash
# Tüm subdomain'ler için ayrı sertifika
sudo certbot --nginx \
  -d monitrang.com \
  -d www.monitrang.com \
  -d app.monitrang.com \
  -d api.monitrang.com \
  -d auth.monitrang.com \
  -d docs.monitrang.com \
  -d gitlab.monitrang.com
```

**Not:** Bu yöntem her subdomain için ayrı sertifika oluşturur, yönetimi karmaşıklaştırır.

---

## 🔄 Otomatik Yenileme

### Certbot Timer (Önerilen)

Certbot, otomatik olarak systemd timer oluşturur:

```bash
# Timer durumunu kontrol et
sudo systemctl status certbot.timer

# Timer'ı etkinleştir
sudo systemctl enable certbot.timer

# Timer'ı başlat
sudo systemctl start certbot.timer

# Timer log'larını görüntüle
sudo journalctl -u certbot.timer
```

### Manuel Yenileme Testi

```bash
# Dry-run (test modu)
sudo certbot renew --dry-run

# Gerçek yenileme
sudo certbot renew
```

### Cron Job (Alternatif)

```bash
# Crontab'ı düzenle
sudo crontab -e

# Şu satırı ekle (günde 2 kez kontrol eder)
0 0,12 * * * certbot renew --quiet
```

### Yenileme Sonrası Nginx Yeniden Yükleme

```bash
# Certbot hook script oluştur
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

## 📁 Sertifika Dosyaları

### Sertifika Konumu

Let's Encrypt sertifikaları şu konumda saklanır:

```
/etc/letsencrypt/live/monitrang.com/
├── cert.pem          # Sertifika
├── chain.pem         # Intermediate CA
├── fullchain.pem     # Sertifika + Chain (Nginx için)
└── privkey.pem       # Private Key
```

### Nginx Yapılandırması

```nginx
ssl_certificate /etc/letsencrypt/live/monitrang.com/fullchain.pem;
ssl_certificate_key /etc/letsencrypt/live/monitrang.com/privkey.pem;
```

---

## 🌐 Wildcard Sertifikası Detayları

### Wildcard Sertifikası Nedir?

Wildcard sertifikası, `*.monitrang.com` formatında bir sertifikadır ve tüm subdomain'leri kapsar:

- ✅ `app.monitrang.com`
- ✅ `api.monitrang.com`
- ✅ `auth.monitrang.com`
- ✅ `docs.monitrang.com`
- ✅ `gitlab.monitrang.com`
- ✅ `www.monitrang.com`
- ✅ `test.monitrang.com`
- ✅ `staging.monitrang.com`
- ❌ `monitrang.com` (ana domain için ayrı eklenmeli)

### Wildcard + Ana Domain

Wildcard sertifikası ana domain'i kapsamaz, bu yüzden her ikisini de eklemelisiniz:

```bash
sudo certbot certonly --manual --preferred-challenges dns \
  -d "*.monitrang.com" -d "monitrang.com"
```

### DNS Doğrulama Süreci

1. **Certbot TXT kaydı oluşturur:**
   ```
   _acme-challenge.monitrang.com TXT "abc123xyz789..."
   ```

2. **DNS panelinde TXT kaydı eklenir:**
   - Hosting Dünyam DNS panelinde
   - `_acme-challenge` subdomain'i için
   - TXT tipinde kayıt

3. **DNS propagation beklenir:**
   ```bash
   # DNS kaydını kontrol et
   dig +short TXT _acme-challenge.monitrang.com
   ```

4. **Certbot doğrulamayı tamamlar:**
   - Enter'a basın
   - Sertifika oluşturulur

---

## 🔍 Doğrulama Yöntemleri

### 1. HTTP Doğrulama (HTTP-01)

**Nasıl Çalışır:**
- Let's Encrypt, `http://monitrang.com/.well-known/acme-challenge/TOKEN` adresine istek gönderir
- Certbot, bu dosyayı otomatik oluşturur
- Let's Encrypt dosyayı okur ve doğrular

**Avantajlar:**
- Hızlı (saniyeler içinde)
- Otomatik (Certbot yapar)
- Port 80 gerektirir

**Dezavantajlar:**
- Port 80 açık olmalı
- Her domain için ayrı doğrulama

### 2. DNS Doğrulama (DNS-01)

**Nasıl Çalışır:**
- Let's Encrypt, DNS'te `_acme-challenge.monitrang.com TXT "TOKEN"` kaydını arar
- Siz bu kaydı manuel olarak ekler
- Let's Encrypt DNS'i kontrol eder

**Avantajlar:**
- Wildcard sertifikası için gerekli
- Port 80 gerektirmez
- Internal domain'ler için kullanılabilir

**Dezavantajlar:**
- Manuel DNS kaydı ekleme
- DNS propagation bekleme süresi

---

## 📊 Rate Limiting Detayları

### Sertifika Limitleri

| Limit Tipi | Limit | Açıklama |
|------------|-------|----------|
| **Sertifika/Hafta** | 50 | Her domain için haftalık sertifika sayısı |
| **Duplicate Sertifika** | 5 | Aynı domain için haftalık duplicate sertifika |
| **Yenileme** | Sınırsız | Aynı sertifika için yenileme sınırsız |
| **Failed Validation** | 5 | Başarısız doğrulama denemesi/hafta |

### Rate Limit Aşımı

Rate limit aşılırsa:
- 1 hafta beklemeniz gerekir
- Veya staging environment kullanın (sınırsız)

### Staging Environment

Test için staging environment kullanın:

```bash
# Staging sertifikası al (sınırsız)
sudo certbot certonly --staging --nginx -d monitrang.com

# Production sertifikası al
sudo certbot certonly --nginx -d monitrang.com
```

---

## 🛠️ Troubleshooting

### Sorun 1: DNS Propagation Bekleme

**Sorun:** DNS kaydı henüz propagate olmadı.

**Çözüm:**
```bash
# Farklı DNS sunucularından kontrol et
dig @8.8.8.8 +short TXT _acme-challenge.monitrang.com
dig @1.1.1.1 +short TXT _acme-challenge.monitrang.com

# Tüm sunucularda görünene kadar bekleyin
```

### Sorun 2: Port 80 Kullanımda

**Sorun:** Port 80 başka bir servis tarafından kullanılıyor.

**Çözüm:**
```bash
# Port 80'i kullanan servisi bul
sudo lsof -i :80

# Nginx'in port 80'i kullandığından emin olun
# GitLab veya başka servisler port 80'i kullanmamalı
```

### Sorun 3: Rate Limit Aşımı

**Sorun:** Çok fazla sertifika isteği yapıldı.

**Çözüm:**
```bash
# Staging environment kullan
sudo certbot certonly --staging --nginx -d monitrang.com

# Veya 1 hafta bekleyin
```

### Sorun 4: Sertifika Yenileme Başarısız

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

## 📈 Let's Encrypt vs Diğer Seçenekler

### Let's Encrypt vs Self-Signed

| Özellik | Let's Encrypt | Self-Signed |
|---------|---------------|-------------|
| **Browser Güveni** | ✅ Evet | ❌ Hayır |
| **Maliyet** | Ücretsiz | Ücretsiz |
| **Kurulum** | Kolay | Çok Kolay |
| **Yenileme** | Otomatik | Manuel |
| **Production** | ✅ Uygun | ❌ Uygun Değil |

### Let's Encrypt vs Kurumsal CA

| Özellik | Let's Encrypt | Kurumsal CA |
|---------|---------------|-------------|
| **Maliyet** | Ücretsiz | $50-$500/yıl |
| **Geçerlilik** | 90 gün | 1-3 yıl |
| **Yenileme** | Otomatik | Manuel |
| **OV/EV** | ❌ Hayır | ✅ Evet |
| **Air-Gapped** | ❌ Hayır | ✅ Evet |

---

## 🎯 MonitraNG İçin Öneri

### Senaryo: Production Sunucu (Internet Bağlantılı)

**Öneri:** ✅ **Let's Encrypt Wildcard Sertifikası**

**Gerekçe:**
1. **Ücretsiz:** Hiçbir maliyet yok
2. **Wildcard:** Tüm subdomain'ler için tek sertifika
3. **Otomatik Yenileme:** Certbot timer ile
4. **Güvenilir:** Tüm browser'lar tarafından güvenilir
5. **Production Uygun:** Müşteri güveni için ideal

**Kurulum Süresi:** ~20 dakika (DNS doğrulama dahil)

**Bakım:** Otomatik (Certbot timer)

---

## ✅ Sonuç

Let's Encrypt, production ortamlar için **ideal bir seçenek**tir:

- ✅ Tamamen ücretsiz
- ✅ Wildcard sertifikası desteği
- ✅ Otomatik yenileme
- ✅ Tüm browser'lar tarafından güvenilir
- ✅ Kolay kurulum ve bakım

**Tek gereksinim:** Internet bağlantısı ve doğru DNS kayıtları.

---

**Son Güncelleme:** 2 Ocak 2026

