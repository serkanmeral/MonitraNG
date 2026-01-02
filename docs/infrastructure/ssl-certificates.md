# SSL/TLS Sertifikası Yapılandırması

**Tarih:** 2 Ocak 2026  
**Domain:** `monitrang.com`  
**Production Sunucu IP:** `45.141.151.52`  
**Durum:** ⏳ Karar Verilecek

---

## 📋 Genel Bakış

MonitraNG sisteminde SSL/TLS sertifikası için üç seçenek bulunmaktadır. Sistemin gereksinimlerine göre uygun seçeneği seçmeniz gerekmektedir.

---

## 🔐 SSL Sertifikası Seçenekleri

### Seçenek 1: Let's Encrypt (Önerilen - Internet Bağlantılı Sistemler İçin)

**Avantajlar:**
- ✅ **Ücretsiz** - Hiçbir maliyet yok
- ✅ **Otomatik Yenileme** - Certbot ile otomatik yenilenir
- ✅ **Güvenilir** - Tüm modern browser'lar tarafından güvenilir
- ✅ **Kolay Kurulum** - Certbot ile tek komutla kurulur
- ✅ **Wildcard Sertifikası** - Tüm subdomain'ler için tek sertifika

**Dezavantajlar:**
- ❌ **Internet Bağlantısı Gerekir** - Let's Encrypt'e erişim olmalı
- ❌ **DNS Doğrulama** - DNS kayıtlarının doğru olması gerekir
- ❌ **90 Günlük Geçerlilik** - Otomatik yenileme gerekir

**Kullanım Senaryosu:**
- Production sunucu internete bağlıysa
- DNS kayıtları doğru yapılandırılmışsa
- Otomatik yenileme için cron job kurulabilirse

**Kurulum Süresi:** ~15 dakika  
**Bakım:** Otomatik (cron job ile)

---

### Seçenek 2: Self-Signed Certificate (Şu Anki Durum)

**Avantajlar:**
- ✅ **Hızlı Kurulum** - Hemen kullanılabilir
- ✅ **Internet Bağlantısı Gerektirmez** - Air-gapped sistemlerde çalışır
- ✅ **Ücretsiz** - Ek maliyet yok
- ✅ **Tam Kontrol** - Sertifika tamamen sizin kontrolünüzde

**Dezavantajlar:**
- ❌ **Browser Uyarısı** - Her kullanıcı güvenlik uyarısı görür
- ❌ **Güven Sorunu** - Kullanıcılar sertifikayı manuel olarak güvenmek zorunda
- ❌ **Manuel Yenileme** - Sertifika süresi dolduğunda manuel yenileme gerekir
- ❌ **Production İçin Uygun Değil** - Müşteri tarafında güven sorunu yaratır

**Kullanım Senaryosu:**
- Test/Development ortamları
- Internal network'ler
- Air-gapped sistemler (internet bağlantısı yoksa)
- Geçici çözüm olarak

**Kurulum Süresi:** ~5 dakika (zaten yapıldı)  
**Bakım:** Manuel (sertifika süresi dolduğunda)

---

### Seçenek 3: Kurumsal CA Sertifikası (Air-Gapped Sistemler İçin)

**Avantajlar:**
- ✅ **Güvenilir** - Tüm browser'lar tarafından güvenilir
- ✅ **Air-Gapped Uyumlu** - Internet bağlantısı gerektirmez
- ✅ **Uzun Geçerlilik** - Genellikle 1-3 yıl geçerli
- ✅ **Kurumsal Standart** - Enterprise ortamlar için uygun

**Dezavantajlar:**
- ❌ **Maliyetli** - Yıllık ücret gerekir (genellikle $50-$500)
- ❌ **Kurulum Karmaşık** - CA'dan sertifika almak ve yüklemek gerekir
- ❌ **Manuel Yenileme** - Sertifika süresi dolduğunda manuel yenileme
- ❌ **Süreç Uzun** - Sertifika almak birkaç gün sürebilir

**Kullanım Senaryosu:**
- Air-gapped production sistemler
- Kurumsal ortamlar
- Yüksek güvenlik gereksinimleri
- Müşteri güveni kritikse

**Kurulum Süresi:** ~2-3 saat (sertifika almak dahil)  
**Bakım:** Manuel (sertifika süresi dolduğunda)

---

## 🎯 Öneri ve Karar Matrisi

### Senaryo 1: Production Sunucu Internet Bağlantılı

**Öneri:** ✅ **Let's Encrypt**

**Gerekçe:**
- Ücretsiz ve otomatik yenileme
- Tüm modern browser'lar tarafından güvenilir
- Kolay kurulum ve bakım
- Wildcard sertifikası ile tüm subdomain'ler için tek sertifika

**Adımlar:**
1. Certbot kurulumu
2. DNS kayıtlarının doğru olduğunu doğrulama
3. Let's Encrypt sertifikası alma
4. Nginx yapılandırmasını güncelleme
5. Otomatik yenileme için cron job kurma

---

### Senaryo 2: Air-Gapped Sistem (Internet Bağlantısı Yok)

**Öneri:** ⚠️ **Kurumsal CA Sertifikası** veya **Self-Signed (Geçici)**

**Gerekçe:**
- Let's Encrypt kullanılamaz (internet bağlantısı gerekir)
- Self-signed geçici çözüm olarak kullanılabilir
- Production için kurumsal CA sertifikası önerilir

**Adımlar:**
1. **Self-Signed (Geçici):**
   - Şu anki durum devam eder
   - Kullanıcılar sertifikayı manuel olarak güvenmek zorunda

2. **Kurumsal CA (Kalıcı):**
   - CA'dan sertifika alınır
   - Sertifika sunucuya yüklenir
   - Nginx yapılandırması güncellenir

---

## 📝 Let's Encrypt Kurulum Rehberi

### Ön Gereksinimler

1. **DNS Kayıtları:** Tüm subdomain'ler için A kayıtları doğru yapılandırılmış olmalı
2. **Port 80 Erişimi:** Let's Encrypt doğrulama için port 80'e erişebilmeli
3. **Domain Kontrolü:** Domain'in size ait olduğunu kanıtlamalısınız

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
dig +short app.monitrang.com
dig +short api.monitrang.com
dig +short auth.monitrang.com
dig +short docs.monitrang.com
dig +short gitlab.monitrang.com

# Tüm kayıtlar 45.141.151.52'yi göstermeli
```

### Adım 3: Let's Encrypt Sertifikası Alma

**Seçenek A: Tek Domain İçin (Ana Domain)**

```bash
sudo certbot --nginx -d monitrang.com -d www.monitrang.com
```

**Seçenek B: Wildcard Sertifikası (Tüm Subdomain'ler İçin - Önerilen)**

```bash
# DNS doğrulama ile wildcard sertifikası
sudo certbot certonly --manual --preferred-challenges dns \
  -d "*.monitrang.com" -d "monitrang.com"

# Certbot size bir TXT kaydı verecek, bunu DNS'e eklemeniz gerekecek
# Örnek: _acme-challenge.monitrang.com TXT "abc123..."
```

**Seçenek C: Her Subdomain İçin Ayrı Sertifika**

```bash
sudo certbot --nginx \
  -d app.monitrang.com \
  -d api.monitrang.com \
  -d auth.monitrang.com \
  -d docs.monitrang.com \
  -d gitlab.monitrang.com \
  -d monitrang.com \
  -d www.monitrang.com
```

### Adım 4: Nginx Yapılandırmasını Güncelleme

Certbot otomatik olarak Nginx yapılandırmasını güncelleyecektir. Ancak manuel kontrol için:

```bash
# Yapılandırma dosyasını kontrol et
sudo nano /etc/nginx/sites-available/monitrang

# SSL sertifika yolları şu şekilde olmalı:
# ssl_certificate /etc/letsencrypt/live/monitrang.com/fullchain.pem;
# ssl_certificate_key /etc/letsencrypt/live/monitrang.com/privkey.pem;
```

### Adım 5: Otomatik Yenileme Kurulumu

```bash
# Certbot otomatik olarak cron job ekler, kontrol et:
sudo systemctl status certbot.timer

# Manuel test:
sudo certbot renew --dry-run

# Yenileme log'larını kontrol et:
sudo tail -f /var/log/letsencrypt/letsencrypt.log
```

---

## 📝 Self-Signed Certificate (Mevcut Durum)

### Mevcut Yapılandırma

**Sertifika Konumu:**
- `/etc/nginx/ssl/monitrang.crt`
- `/etc/nginx/ssl/monitrang.key`

**Geçerlilik Süresi:** 365 gün

**Oluşturulma Tarihi:** 2 Ocak 2026

### Sertifika Bilgilerini Kontrol Etme

```bash
# Sertifika detaylarını görüntüle
openssl x509 -in /etc/nginx/ssl/monitrang.crt -text -noout

# Geçerlilik süresini kontrol et
openssl x509 -in /etc/nginx/ssl/monitrang.crt -noout -dates
```

### Sertifikayı Yenileme

```bash
# Yeni sertifika oluştur
sudo openssl req -x509 -nodes -days 365 -newkey rsa:2048 \
  -keyout /etc/nginx/ssl/monitrang.key \
  -out /etc/nginx/ssl/monitrang.crt \
  -subj '/CN=monitrang.com/O=MonitraNG/C=TR'

# İzinleri ayarla
sudo chmod 600 /etc/nginx/ssl/monitrang.key
sudo chmod 644 /etc/nginx/ssl/monitrang.crt

# Nginx'i yeniden başlat
sudo systemctl reload nginx
```

---

## 📝 Kurumsal CA Sertifikası Kurulumu

### Adım 1: Sertifika İsteği (CSR) Oluşturma

```bash
# Private key oluştur
openssl genrsa -out /etc/nginx/ssl/monitrang.key 2048

# CSR oluştur
openssl req -new -key /etc/nginx/ssl/monitrang.key \
  -out /etc/nginx/ssl/monitrang.csr \
  -subj '/CN=monitrang.com/O=MonitraNG/C=TR'

# CSR'ı CA'ya gönderin
```

### Adım 2: CA'dan Sertifika Alma

1. CSR dosyasını CA'ya gönderin
2. CA sertifikayı size gönderir
3. Sertifikayı sunucuya yükleyin

### Adım 3: Sertifikayı Yükleme

```bash
# CA'dan gelen sertifikayı yükle
sudo cp your-certificate.crt /etc/nginx/ssl/monitrang.crt

# Intermediate certificate varsa yükle
sudo cp intermediate.crt /etc/nginx/ssl/intermediate.crt

# İzinleri ayarla
sudo chmod 600 /etc/nginx/ssl/monitrang.key
sudo chmod 644 /etc/nginx/ssl/monitrang.crt
```

### Adım 4: Nginx Yapılandırmasını Güncelleme

```nginx
ssl_certificate /etc/nginx/ssl/monitrang.crt;
ssl_certificate_key /etc/nginx/ssl/monitrang.key;

# Intermediate certificate varsa:
# ssl_certificate /etc/nginx/ssl/intermediate.crt;
```

---

## 🔍 Sertifika Test ve Doğrulama

### SSL Sertifikasını Test Etme

```bash
# SSL sertifikasını test et
openssl s_client -connect monitrang.com:443 -servername monitrang.com

# Sertifika detaylarını görüntüle
echo | openssl s_client -connect monitrang.com:443 -servername monitrang.com 2>/dev/null | openssl x509 -noout -text
```

### Online SSL Test Araçları

- **SSL Labs:** https://www.ssllabs.com/ssltest/
- **SSL Checker:** https://www.sslshopper.com/ssl-checker.html

### Browser'dan Test

1. Browser'da `https://monitrang.com` adresine gidin
2. Adres çubuğundaki kilit simgesine tıklayın
3. Sertifika detaylarını kontrol edin

---

## ⚠️ Güvenlik Notları

### SSL/TLS Yapılandırması

```nginx
# Güvenli SSL yapılandırması
ssl_protocols TLSv1.2 TLSv1.3;
ssl_ciphers HIGH:!aNULL:!MD5;
ssl_prefer_server_ciphers on;
ssl_session_cache shared:SSL:10m;
ssl_session_timeout 10m;

# HSTS (HTTP Strict Transport Security)
add_header Strict-Transport-Security "max-age=31536000; includeSubDomains" always;
```

### Sertifika Güvenliği

- Private key'i asla paylaşmayın
- Sertifika dosyalarının izinlerini kontrol edin
- Sertifika süresi dolmadan önce yenileyin
- Sertifika yedeklerini güvenli bir yerde saklayın

---

## 📊 Karşılaştırma Tablosu

| Özellik | Let's Encrypt | Self-Signed | Kurumsal CA |
|--------|---------------|-------------|-------------|
| **Maliyet** | Ücretsiz | Ücretsiz | $50-$500/yıl |
| **Kurulum Süresi** | 15 dakika | 5 dakika | 2-3 saat |
| **Browser Güveni** | ✅ Evet | ❌ Hayır | ✅ Evet |
| **Otomatik Yenileme** | ✅ Evet | ❌ Hayır | ❌ Hayır |
| **Internet Gereksinimi** | ✅ Evet | ❌ Hayır | ❌ Hayır |
| **Air-Gapped Uyumlu** | ❌ Hayır | ✅ Evet | ✅ Evet |
| **Production Uygun** | ✅ Evet | ❌ Hayır | ✅ Evet |

---

## 🎯 Öneri

**Mevcut Durum:** Production sunucu internete bağlı görünüyor.

**Öneri:** ✅ **Let's Encrypt (Wildcard Sertifikası)**

**Gerekçe:**
1. Ücretsiz ve otomatik yenileme
2. Tüm browser'lar tarafından güvenilir
3. Kolay kurulum ve bakım
4. Wildcard sertifikası ile tüm subdomain'ler için tek sertifika
5. Production için ideal

**Alternatif:** Eğer air-gapped sistem gereksinimi varsa, kurumsal CA sertifikası kullanılabilir.

---

## ✅ Karar Kontrol Listesi

- [ ] Sistemin internet bağlantısı var mı?
- [ ] DNS kayıtları doğru yapılandırılmış mı?
- [ ] Otomatik yenileme için cron job kurulabilir mi?
- [ ] Air-gapped sistem gereksinimi var mı?
- [ ] Bütçe sınırlaması var mı?
- [ ] Müşteri güveni kritik mi?

---

**Son Güncelleme:** 2 Ocak 2026  
**Durum:** ⏳ Karar Verilecek

