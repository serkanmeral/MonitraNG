# Infrastructure - Mevcut Durum

**Son Güncelleme:** 3 Ocak 2026  
**Çalışılan Konu:** Mailu Mail Sunucusu Kurulumu ve Nginx Reverse Proxy Yapılandırması

---

## Son Çalışılan Konu

Mailu mail sunucusu kurulumu ve Nginx reverse proxy yapılandırması tamamlandı. Mailu container'ları çalışıyor ve `mail.monitrang.com` üzerinden erişilebilir durumda.

---

## Tamamlanan İşler

### 1. Mailu Kurulumu ✅
- **Repository Clone:** Mailu repository'si `/root/MonitraNG/ApplicationResources/mng_common/mailu/` klasörüne clone edildi
- **Yapılandırma Dosyaları:**
  - `.env` dosyası oluşturuldu (domain: `monitrang.com`, hostname: `mail.monitrang.com`)
  - Secret key'ler oluşturuldu (SECRET_KEY, DB_PW, JWT_SECRET_KEY)
  - TLS_FLAVOR: `letsencrypt`
  - ADMIN: `true`
  - WEBMAIL: `roundcube`
  - ANTIVIRUS: `clamav`
- **Docker Compose:**
  - Image isimleri `ghcr.io/mailu/` formatına güncellendi
  - Container'lar başarıyla başlatıldı:
    - `mailu-front-1` (Nginx)
    - `mailu-imap-1` (Dovecot)
    - `mailu-smtp-1` (Postfix)
    - `mailu-antispam-1` (Rspamd)
    - `mailu-antivirus-1` (ClamAV)
    - `mailu-webmail-1` (Roundcube)
    - `mailu-redis-1`
    - `mailu-fetchmail-1`

### 2. Nginx Reverse Proxy Yapılandırması ✅
- **Yapılandırma Dosyası:** `scripts/mailu-nginx-config.conf` oluşturuldu
- **Port Mapping:** Mailu front container'ı `localhost:8081` portunda çalışıyor
- **Nginx Yapılandırması:**
  - `mail.monitrang.com` için HTTP → HTTPS redirect eklendi
  - HTTPS server block eklendi (Let's Encrypt sertifikaları kullanılıyor)
  - Reverse proxy: `https://mail.monitrang.com` → `http://127.0.0.1:8081`
  - Security headers eklendi
  - Log dosyaları: `/var/log/nginx/mail.monitrang.com-*.log`
- **Durum:** Nginx başarıyla reload edildi, yapılandırma test edildi ✅

### 3. Erişim Kontrolü ✅
- **URL:** `https://mail.monitrang.com`
- **Durum:** HTTP 401 (Unauthorized) - Bu normal, admin sayfası için login gerekiyor
- **Admin Panel:** `https://mail.monitrang.com/admin` (login gerekiyor)
- **Webmail:** `https://mail.monitrang.com/webmail` (Roundcube)

---

## Devam Eden İşler

### 1. Mailu Container Durumu
- **mailu-front-1:** Unhealthy durumda (Let's Encrypt hatası var, ancak Nginx üzerinden erişim çalışıyor)
- **mailu-antispam-1:** Unhealthy durumda
- **Diğer container'lar:** Healthy ✅

**Not:** Let's Encrypt hatası Mailu'nun kendi Let's Encrypt yapılandırması ile ilgili. Nginx reverse proxy üzerinden erişim çalışıyor, bu yüzden kritik değil.

---

## Sonraki Adımlar

### 1. DKIM Key Oluşturma ve DNS Kaydı ⏳
- Mailu admin panelinden DKIM public key'i alınacak
- DNS'e DKIM TXT kaydı eklenecek: `default._domainkey.monitrang.com`
- Format: `v=DKIM1; k=rsa; p=[public key]`

### 2. Mailu Admin Panel Yapılandırması ⏳
- İlk admin kullanıcısı oluşturulacak
- Domain yapılandırması kontrol edilecek
- Mail hesapları oluşturulacak

### 3. Mail Port Yapılandırması ⏳
- SMTP (25, 587, 465) portları kontrol edilecek
- IMAP (143, 993) portları kontrol edilecek
- Firewall kuralları kontrol edilecek

### 4. Test Mail Gönderimi ⏳
- Test mail hesapları oluşturulacak
- Dış servislere (Gmail, Outlook) test mail gönderilecek
- Mail alımı test edilecek

---

## Önemli Notlar

### Docker Compose Yapılandırması
- **Konum:** `/root/MonitraNG/ApplicationResources/mng_common/mailu/`
- **Network:** `mailu_default` (172.19.0.0/16)
- **Port Mapping:** Front container `127.0.0.1:8081:80` olarak map edildi
- **Data Klasörü:** `/root/MonitraNG/ApplicationResources/mng_common/mailu/data/`

### Nginx Yapılandırması
- **Dosya:** `/etc/nginx/sites-available/monitrang`
- **Proxy Pass:** `http://127.0.0.1:8081`
- **SSL:** Let's Encrypt sertifikaları (`/etc/letsencrypt/live/monitrang.com/`)

### DNS Kayıtları
- ✅ A kaydı: `mail.monitrang.com` → `45.141.151.52`
- ✅ MX kaydı: `monitrang.com` → `mail.monitrang.com` (priority: 10)
- ✅ SPF kaydı: `v=spf1 mx a:mail.monitrang.com ~all`
- ✅ DMARC kaydı: `_dmarc.monitrang.com` → `v=DMARC1; p=quarantine; rua=mailto:admin@monitrang.com`
- ⏳ DKIM kaydı: Mailu kurulumundan sonra eklenecek

### Mailu Yapılandırma Değişkenleri
- **DOMAIN:** `monitrang.com`
- **HOSTNAMES:** `mail.monitrang.com,monitrang.com`
- **POSTMASTER:** `admin`
- **TLS_FLAVOR:** `letsencrypt`
- **ADMIN:** `true`
- **WEBMAIL:** `roundcube`
- **ANTIVIRUS:** `clamav`

---

## Karşılaşılan Sorunlar ve Çözümler

### 1. Port 80 Çakışması
- **Sorun:** Mailu front container'ı port 80'i kullanmaya çalıştı, ancak Nginx zaten kullanıyordu
- **Çözüm:** Mailu front container'ı `127.0.0.1:8081:80` olarak map edildi, Nginx reverse proxy üzerinden erişim sağlandı

### 2. Container IP Erişimi
- **Sorun:** Container IP'si ile erişim sağlanamadı (Connection refused)
- **Çözüm:** Container name yerine localhost port mapping kullanıldı

### 3. PowerShell Heredoc Sorunları
- **Sorun:** PowerShell'de heredoc ve escape karakterleri sorun çıkardı
- **Çözüm:** Dosya local'de oluşturulup SCP ile sunucuya kopyalandı

### 4. Let's Encrypt Hatası (Mailu İçinde)
- **Sorun:** Mailu'nun kendi Let's Encrypt yapılandırması port 80'e erişemiyor
- **Durum:** Kritik değil, Nginx reverse proxy üzerinden erişim çalışıyor
- **Not:** Mailu'nun Let's Encrypt yapılandırması devre dışı bırakılabilir veya daha sonra düzeltilebilir

---

## Dosya Konumları

### Mailu
- **Docker Compose:** `/root/MonitraNG/ApplicationResources/mng_common/mailu/docker-compose.yml`
- **Environment:** `/root/MonitraNG/ApplicationResources/mng_common/mailu/.env`
- **Data:** `/root/MonitraNG/ApplicationResources/mng_common/mailu/data/`

### Nginx
- **Yapılandırma:** `/etc/nginx/sites-available/monitrang`
- **Template:** `scripts/mailu-nginx-config.conf`
- **Logs:** `/var/log/nginx/mail.monitrang.com-*.log`

### DNS Dokümantasyonu
- **DNS Kayıtları:** `docs/infrastructure/domain-dns.md`
- **Mail DNS Setup:** `docs/infrastructure/mail-dns-setup.md`

---

## Sonraki Oturum İçin Hazırlık

1. **Mailu Admin Panel:** `https://mail.monitrang.com/admin` adresinden giriş yapılacak
2. **DKIM Key:** Admin panelden DKIM public key alınacak
3. **DNS Güncelleme:** DKIM TXT kaydı DNS'e eklenecek
4. **Test:** Mail gönderimi/alımı test edilecek

---

## Komutlar (Referans)

```bash
# Mailu container durumu
cd /root/MonitraNG/ApplicationResources/mng_common/mailu
docker compose ps

# Mailu logları
docker compose logs front

# Nginx test
nginx -t

# Nginx reload
systemctl reload nginx

# Mailu erişim testi
curl -I https://mail.monitrang.com
curl -L https://mail.monitrang.com/admin
```

---

**Not:** Bu dosya her oturum sonunda güncellenmelidir.

