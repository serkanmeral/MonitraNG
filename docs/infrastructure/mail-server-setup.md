# Mail Sunucusu Kurulum Rehberi

**Tarih:** 2 Ocak 2026  
**Durum:** Planlama aşamasında  
**Seçilen Çözüm:** Mailu (Docker Compose)

---

## 📋 Genel Bakış

MonitraNG için mail sunucusu olarak **Mailu** seçildi. Mailu, Docker Compose ile kolay kurulum, tam özellikli bir mail sunucusu çözümüdür.

### Mailu Özellikleri

- ✅ **SMTP/IMAP/POP3** desteği
- ✅ **Web UI** (admin panel ve webmail)
- ✅ **Spam koruması** (Rspamd)
- ✅ **Virus koruması** (ClamAV)
- ✅ **DKIM, SPF, DMARC** desteği
- ✅ **Çoklu domain** desteği
- ✅ **Docker Compose** ile kolay kurulum
- ✅ **Let's Encrypt** SSL entegrasyonu

---

## 🚀 Kurulum

### 1. Mailu Docker Compose Yapılandırması

Mailu, kendi Docker Compose dosyası ile çalışır. `ApplicationResources/mng_common/` altına ayrı bir klasör oluşturulabilir veya mevcut `docker-compose.yml`'e eklenebilir.

**Önerilen Yapı:**
```
ApplicationResources/
  mng_common/
    docker-compose.yml          # Mevcut servisler
    mailu/
      docker-compose.yml        # Mailu servisleri
      .env                      # Mailu yapılandırması
      mailu.yml                # Mailu ana yapılandırması
```

### 2. Gereksinimler

- **Domain:** `mail.monitrang.com` (veya `mail.monitrang.com`)
- **DNS Kayıtları:**
  - A record: `mail.monitrang.com` → `45.141.151.52`
  - MX record: `monitrang.com` → `mail.monitrang.com` (priority: 10)
  - SPF record: `v=spf1 mx a:mail.monitrang.com ~all`
  - DKIM record: (Mailu kurulumundan sonra oluşturulacak)
  - DMARC record: `v=DMARC1; p=quarantine; rua=mailto:admin@monitrang.com`
- **Portlar:**
  - 25 (SMTP)
  - 587 (SMTP Submission)
  - 465 (SMTPS)
  - 143 (IMAP)
  - 993 (IMAPS)
  - 110 (POP3)
  - 995 (POP3S)
  - 80/443 (Web UI)

### 3. Kurulum Adımları

#### Adım 1: Mailu Repository Clone

```bash
cd /root/MonitraNG/ApplicationResources/mng_common
git clone https://github.com/Mailu/Mailu.git mailu
cd mailu
```

#### Adım 2: Yapılandırma Dosyası Oluştur

```bash
# mailu.yml dosyasını düzenle
cp docs/compose/config/mailu.env .env
```

`.env` dosyasında önemli ayarlar:
```bash
# Domain
DOMAIN=monitrang.com
HOSTNAME=mail.monitrang.com

# Secret keys (otomatik oluşturulacak)
SECRET_KEY=...
DB_PW=...
JWT_SECRET_KEY=...

# Admin
INITIAL_ADMIN_USERNAME=admin
INITIAL_ADMIN_PASSWORD=!2345Qawsedrf*

# Database
DBNAME=mailu
DBUSER=mailu
```

#### Adım 3: Docker Compose Başlat

```bash
docker compose up -d
```

---

## 🔧 Yapılandırma

### SMTP Ayarları (Application Servisleri İçin)

**Host:** `mail.monitrang.com` (veya `mailu` container name)  
**Port:** `587` (TLS) veya `465` (SSL)  
**Username:** `noreply@monitrang.com`  
**Password:** `!2345Qawsedrf*`  
**From Address:** `noreply@monitrang.com`

### Keycloak SMTP Yapılandırması

Keycloak container'ına environment variable'lar ekle:

```yaml
KC_SMTP_HOST: mailu
KC_SMTP_PORT: 587
KC_SMTP_FROM: noreply@monitrang.com
KC_SMTP_USER: noreply@monitrang.com
KC_SMTP_PASSWORD: !2345Qawsedrf*
```

### GitLab SMTP Yapılandırması

GitLab `GITLAB_OMNIBUS_CONFIG` içine ekle:

```ruby
gitlab_rails['smtp_enable'] = true
gitlab_rails['smtp_address'] = "mailu"
gitlab_rails['smtp_port'] = 587
gitlab_rails['smtp_user_name'] = "noreply@monitrang.com"
gitlab_rails['smtp_password'] = "!2345Qawsedrf*"
gitlab_rails['smtp_domain'] = "monitrang.com"
gitlab_rails['smtp_authentication'] = "login"
gitlab_rails['smtp_enable_starttls_auto'] = true
gitlab_rails['gitlab_email_from'] = "noreply@monitrang.com"
```

---

## 📧 Kullanım Senaryoları

### 1. Notification Servisleri

- **MngDataGateway:** Dataset event'leri için email bildirimleri
- **MngKeeper:** Password reset, user activation email'leri
- **MngHub:** System notification'ları

### 2. Keycloak Entegrasyonu

- Password reset email'leri
- Email verification
- Account activation

### 3. GitLab Entegrasyonu

- CI/CD pipeline notification'ları
- Merge request notification'ları
- Issue notification'ları

---

## 🔒 Güvenlik

### DNS Kayıtları

1. **SPF Record:**
   ```
   v=spf1 mx a:mail.monitrang.com ~all
   ```

2. **DKIM Record:**
   Mailu kurulumundan sonra oluşturulacak public key ile:
   ```
   default._domainkey.monitrang.com TXT "v=DKIM1; k=rsa; p=..."
   ```

3. **DMARC Record:**
   ```
   _dmarc.monitrang.com TXT "v=DMARC1; p=quarantine; rua=mailto:admin@monitrang.com"
   ```

### SSL/TLS

- Let's Encrypt sertifikası kullanılacak
- Mailu otomatik olarak Let's Encrypt ile sertifika alabilir
- Nginx reverse proxy üzerinden erişim (opsiyonel)

---

## 🧪 Test

### SMTP Test

```bash
# Telnet ile test
telnet mail.monitrang.com 587

# veya swaks ile
swaks --to test@example.com \
  --from noreply@monitrang.com \
  --server mail.monitrang.com \
  --port 587 \
  --auth LOGIN \
  --auth-user noreply@monitrang.com \
  --auth-password <password> \
  --tls
```

### Web UI Erişimi

- **Admin Panel:** `https://mail.monitrang.com/admin`
- **Webmail:** `https://mail.monitrang.com`

---

## 📝 Notlar

- Mailu, kendi network'ünde çalışabilir veya `mng_network`'e bağlanabilir
- Port 25 için firewall açılması gerekebilir (bazı hosting sağlayıcıları port 25'i bloklar)
- Reverse DNS (PTR) kaydı önemli (spam önleme için)
- Mail kotaları ve limitler yapılandırılabilir

---

## 🔗 İlgili Dosyalar

- `ApplicationResources/mng_common/mailu/docker-compose.yml` → Mailu compose dosyası
- `ApplicationResources/mng_common/mailu/.env` → Mailu yapılandırması
- `docs/infrastructure/domain-dns.md` → DNS yapılandırması

---

**Son Güncelleme:** 2 Ocak 2026

