# Mailu Mail Sunucusu Implementation Planı

**Tarih:** 2 Ocak 2026  
**Durum:** Planlama aşamasında  
**Hedef:** Test ve production ortamı için gerçek mail gönderimi

---

## 🎯 Genel Bakış

Mailu, Docker Compose ile çalışan tam özellikli bir mail sunucusu çözümüdür. MonitraNG için notification'lar, Keycloak email doğrulama, GitLab bildirimleri ve diğer email ihtiyaçları için kullanılacak.

---

## 📋 Implementation Planı

### Phase 1: Hazırlık ve Planlama (1-2 saat)

#### 1.1 DNS Kayıtları Planlama

**Gerekli DNS Kayıtları:**

1. **A Record:**
   ```
   mail.monitrang.com → 45.141.151.52
   ```

2. **MX Record:**
   ```
   monitrang.com → mail.monitrang.com (priority: 10)
   ```

3. **SPF Record:**
   ```
   monitrang.com TXT "v=spf1 mx a:mail.monitrang.com ~all"
   ```
   - `~all`: Soft fail (test için uygun)
   - Production'da `-all` (hard fail) kullanılabilir

4. **DKIM Record:**
   - Mailu kurulumundan sonra oluşturulacak
   - Format: `default._domainkey.monitrang.com TXT "v=DKIM1; k=rsa; p=..."`
   - Mailu admin panelinden public key alınacak

5. **DMARC Record:**
   ```
   _dmarc.monitrang.com TXT "v=DMARC1; p=quarantine; rua=mailto:admin@monitrang.com; ruf=mailto:admin@monitrang.com"
   ```
   - `p=quarantine`: Test için uygun
   - Production'da `p=reject` kullanılabilir

6. **Reverse DNS (PTR) Kaydı:**
   - Hosting sağlayıcısından talep edilmeli
   - `45.141.151.52` → `mail.monitrang.com`
   - Spam önleme için kritik

#### 1.2 Port Yapılandırması

**Gerekli Portlar:**

| Port | Protokol | Açıklama | Firewall |
|------|----------|----------|----------|
| 25 | SMTP | Mail gönderimi (MTA) | Açık olmalı |
| 587 | SMTP Submission | TLS ile mail gönderimi | Açık olmalı |
| 465 | SMTPS | SSL ile mail gönderimi | Açık olmalı |
| 143 | IMAP | Mail okuma | Açık olmalı |
| 993 | IMAPS | SSL ile mail okuma | Açık olmalı |
| 110 | POP3 | Mail okuma (eski) | Opsiyonel |
| 995 | POP3S | SSL ile POP3 | Opsiyonel |
| 80 | HTTP | Web UI (Nginx üzerinden) | Açık |
| 443 | HTTPS | Web UI (Nginx üzerinden) | Açık |

**Not:** Bazı hosting sağlayıcıları port 25'i bloklar. Alternatif olarak port 587 kullanılabilir.

#### 1.3 Network Yapılandırması

**Seçenekler:**

1. **Ayrı Network (Önerilen):**
   - Mailu kendi network'ünde çalışır
   - Application servisleri `mng_network`'te kalır
   - SMTP bağlantısı için hostname veya IP kullanılır

2. **Ortak Network:**
   - Mailu `mng_network`'e bağlanır
   - Container name ile erişim: `mailu-smtp`
   - Daha kolay entegrasyon

**Öneri:** Ayrı network kullanmak, güvenlik ve izolasyon açısından daha iyi.

---

### Phase 2: Mailu Kurulumu (2-3 saat)

#### 2.1 Repository ve Dosya Yapısı

```bash
ApplicationResources/
  mng_common/
    mailu/
      docker-compose.yml    # Mailu compose dosyası
      .env                   # Mailu environment variables
      mailu.yml             # Mailu ana yapılandırması (opsiyonel)
      README.md             # Kurulum notları
```

#### 2.2 Yapılandırma Dosyaları

**`.env` dosyası önemli ayarlar:**

```bash
# Domain Configuration
DOMAIN=monitrang.com
HOSTNAME=mail.monitrang.com

# Secret Keys (otomatik oluşturulacak)
SECRET_KEY=<generate-random>
DB_PW=<generate-random>
JWT_SECRET_KEY=<generate-random>

# Admin Account
INITIAL_ADMIN_USERNAME=admin
INITIAL_ADMIN_PASSWORD=!2345Qawsedrf*

# Database
DBNAME=mailu
DBUSER=mailu

# TLS Configuration
TLS_FLAVOR=letsencrypt
# veya: cert (kendi sertifika), notls (test için)

# Features
ENABLE_WEBDAV=true
ENABLE_FETCHMAIL=true
ENABLE_CLAMAV=true
ENABLE_RSPAMD=true
```

#### 2.3 Docker Compose Yapılandırması

Mailu'nun resmi docker-compose template'ini kullan:
- GitHub: https://github.com/Mailu/Mailu
- Template: `docs/compose/docker-compose.yml`

**Özelleştirmeler:**
- Network: Ayrı network veya `mng_network`
- Volume'lar: Persistent storage
- Port mapping: Host port'ları
- Environment: `.env` dosyasından okuma

---

### Phase 3: DNS Yapılandırması (1 saat)

#### 3.1 DNS Kayıtlarını Ekleme

1. **A Record:**
   - DNS panelinden `mail.monitrang.com` → `45.141.151.52` ekle

2. **MX Record:**
   - `monitrang.com` için MX record ekle
   - Priority: 10
   - Value: `mail.monitrang.com`

3. **SPF Record:**
   - `monitrang.com` için TXT record
   - Value: `v=spf1 mx a:mail.monitrang.com ~all`

4. **DMARC Record:**
   - `_dmarc.monitrang.com` için TXT record
   - Value: `v=DMARC1; p=quarantine; rua=mailto:admin@monitrang.com`

#### 3.2 DKIM Key Oluşturma

1. Mailu kurulumundan sonra admin panelden DKIM key al
2. DNS'e ekle: `default._domainkey.monitrang.com TXT "v=DKIM1; k=rsa; p=..."`

#### 3.3 DNS Propagation Kontrolü

```bash
# MX record kontrolü
dig MX monitrang.com

# SPF record kontrolü
dig TXT monitrang.com

# DMARC record kontrolü
dig TXT _dmarc.monitrang.com

# A record kontrolü
dig A mail.monitrang.com
```

---

### Phase 4: SSL/TLS Yapılandırması (1 saat)

#### 4.1 Let's Encrypt Entegrasyonu

Mailu, Let's Encrypt ile otomatik sertifika alabilir:

```bash
TLS_FLAVOR=letsencrypt
```

**Gereksinimler:**
- Port 80 açık olmalı (HTTP-01 challenge için)
- Domain DNS'de doğru yapılandırılmış olmalı
- Mailu container'ı internet'e erişebilmeli

#### 4.2 Nginx Reverse Proxy (Opsiyonel)

Mailu web UI'sine Nginx üzerinden erişim:

```nginx
# /etc/nginx/sites-available/mail
server {
    listen 80;
    server_name mail.monitrang.com;
    
    location / {
        proxy_pass http://localhost:8080;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
    }
}
```

---

### Phase 5: Entegrasyon (2-3 saat)

#### 5.1 Keycloak SMTP Yapılandırması

**Environment Variables:**

```yaml
KC_SMTP_HOST: mailu-smtp  # veya mail.monitrang.com
KC_SMTP_PORT: 587
KC_SMTP_FROM: noreply@monitrang.com
KC_SMTP_USER: noreply@monitrang.com
KC_SMTP_PASSWORD: <mailu_password>
KC_SMTP_STARTTLS: true
```

**Keycloak Admin Console:**
- Realm Settings → Email
- SMTP ayarlarını yapılandır
- Test email gönder

#### 5.2 GitLab SMTP Yapılandırması

**docker-compose.yml içinde:**

```yaml
gitlab:
  environment:
    GITLAB_OMNIBUS_CONFIG: |
      gitlab_rails['smtp_enable'] = true
      gitlab_rails['smtp_address'] = "mailu-smtp"
      gitlab_rails['smtp_port'] = 587
      gitlab_rails['smtp_user_name'] = "noreply@monitrang.com"
      gitlab_rails['smtp_password'] = "<mailu_password>"
      gitlab_rails['smtp_domain'] = "monitrang.com"
      gitlab_rails['smtp_authentication'] = "login"
      gitlab_rails['smtp_enable_starttls_auto'] = true
      gitlab_rails['gitlab_email_from'] = "noreply@monitrang.com"
```

#### 5.3 Application Servisleri Entegrasyonu

**MngKeeper için SMTP ayarları:**

```json
{
  "MngKeeperSettings": {
    "Smtp": {
      "Host": "mailu-smtp",
      "Port": 587,
      "Username": "noreply@monitrang.com",
      "Password": "<mailu_password>",
      "FromAddress": "noreply@monitrang.com",
      "FromName": "MonitraNG",
      "EnableSsl": true
    }
  }
}
```

**MngDataGateway için notification email'leri:**
- Dataset event'leri için email bildirimleri
- SMTP servisi entegrasyonu

---

### Phase 6: Test ve Doğrulama (1-2 saat)

#### 6.1 SMTP Bağlantı Testi

```bash
# Telnet ile test
telnet mail.monitrang.com 587

# swaks ile test
swaks --to test@example.com \
  --from noreply@monitrang.com \
  --server mail.monitrang.com \
  --port 587 \
  --auth LOGIN \
  --auth-user noreply@monitrang.com \
  --auth-password <password> \
  --tls
```

#### 6.2 Email Gönderim Testi

1. **Keycloak'tan test email:**
   - Admin Console → Realm Settings → Email → Send test email

2. **GitLab'tan test email:**
   - User Settings → Email → Resend confirmation email

3. **Application servislerinden:**
   - MngKeeper password reset testi
   - MngDataGateway notification testi

#### 6.3 Spam Testi

- Mail-tester.com ile spam score kontrolü
- SPF, DKIM, DMARC doğrulama
- Reverse DNS kontrolü

---

## ⚠️ Dikkat Edilmesi Gerekenler

### 1. Port 25 Blokajı

Bazı hosting sağlayıcıları port 25'i bloklar. Çözümler:
- Port 587 kullan (SMTP Submission)
- Hosting sağlayıcısından port 25 açılmasını talep et
- Alternatif: Relay server kullan

### 2. Reverse DNS (PTR)

- Hosting sağlayıcısından PTR kaydı talep et
- `45.141.151.52` → `mail.monitrang.com`
- Spam önleme için kritik

### 3. IP Reputation

- Yeni IP'ler spam olarak işaretlenebilir
- İlk birkaç hafta dikkatli olun
- Warm-up süreci gerekebilir

### 4. Rate Limiting

- Mailu'da rate limiting yapılandırılabilir
- Spam önleme için önemli
- Günlük gönderim limitleri ayarlanabilir

### 5. Backup

- Mailu volume'larını yedekle
- Database backup (PostgreSQL)
- Mail storage backup

---

## 📊 Tahmini Süre

| Phase | Süre | Açıklama |
|-------|------|----------|
| Phase 1: Hazırlık | 1-2 saat | DNS planlama, port yapılandırması |
| Phase 2: Kurulum | 2-3 saat | Mailu kurulumu ve yapılandırması |
| Phase 3: DNS | 1 saat | DNS kayıtlarını ekleme |
| Phase 4: SSL/TLS | 1 saat | Let's Encrypt yapılandırması |
| Phase 5: Entegrasyon | 2-3 saat | Keycloak, GitLab, Application entegrasyonu |
| Phase 6: Test | 1-2 saat | Test ve doğrulama |
| **TOPLAM** | **8-12 saat** | |

---

## 🎯 Öncelik Sırası

### Yüksek Öncelik (İlk Yapılacaklar)

1. ✅ DNS kayıtları (A, MX, SPF)
2. ✅ Mailu kurulumu
3. ✅ Basic SMTP testi
4. ✅ Keycloak entegrasyonu

### Orta Öncelik

5. DKIM/DMARC yapılandırması
6. GitLab entegrasyonu
7. Application servisleri entegrasyonu
8. Nginx reverse proxy

### Düşük Öncelik (İyileştirmeler)

9. Spam koruması optimizasyonu
10. Backup stratejisi
11. Monitoring ve alerting
12. Rate limiting yapılandırması

---

## 📝 Sonraki Adımlar

1. **DNS kayıtlarını hazırla:**
   - A record: `mail.monitrang.com`
   - MX record: `monitrang.com`
   - SPF record: `monitrang.com`

2. **Mailu repository'sini clone et:**
   ```bash
   cd /root/MonitraNG/ApplicationResources/mng_common
   git clone https://github.com/Mailu/Mailu.git mailu
   ```

3. **Yapılandırma dosyalarını oluştur:**
   - `.env` dosyası
   - `docker-compose.yml` özelleştirmeleri

4. **Kurulumu başlat:**
   - Docker Compose ile başlat
   - Admin panelden ilk kullanıcı oluştur
   - Email adresleri oluştur (noreply@monitrang.com, vb.)

---

## 🔗 İlgili Dokümantasyon

- [Mailu Official Documentation](https://mailu.io/)
- [Mailu GitHub Repository](https://github.com/Mailu/Mailu)
- [DNS Yapılandırması](domain-dns.md)
- [Mail Server Setup Guide](mail-server-setup.md)

---

**Son Güncelleme:** 2 Ocak 2026

