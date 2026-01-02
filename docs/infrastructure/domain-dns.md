# Domain ve DNS Yapılandırması

**Tarih:** 2 Ocak 2026  
**Domain:** `monitrang.com`  
**Hosting:** Hosting Dünyam  
**Nameserver'lar:** `dns1.hostingdunyam.net`, `dns2.hostingdunyam.net`  
**Production Sunucu IP:** `45.141.151.52`

---

## 📋 Genel Bilgiler

### Domain Bilgileri
- **Domain Adı:** `monitrang.com`
- **Registrar:** Hosting Dünyam
- **Nameserver'lar:**
  - `dns1.hostingdunyam.net`
  - `dns2.hostingdunyam.net`
- **DNS Yönetimi:** Hosting Dünyam DNS paneli üzerinden yapılacak

### Production Sunucu Bilgileri
- **IP Adresi:** `45.141.151.52`
- **Lokasyon:** Production sunucu
- **Nginx:** Reverse proxy olarak çalışıyor

---

## 🌐 DNS Kayıtları

### A Kayıtları (IPv4)

Aşağıdaki A kayıtlarını Hosting Dünyam DNS panelinde oluşturun:

| Host | Type | Value | TTL | Açıklama |
|------|------|-------|-----|----------|
| `@` | A | `45.141.151.52` | 3600 | Ana domain (monitrang.com) |
| `www` | A | `45.141.151.52` | 3600 | WWW subdomain |
| `app` | A | `45.141.151.52` | 3600 | Frontend (MngUI) |
| `api` | A | `45.141.151.52` | 3600 | API Gateway |
| `auth` | A | `45.141.151.52` | 3600 | Keycloak |
| `docs` | A | `45.141.151.52` | 3600 | GitLab Pages (Dokümantasyon) |
| `gitlab` | A | `45.141.151.52` | 3600 | GitLab UI |

### AAAA Kayıtları (IPv6) - Opsiyonel

Eğer sunucunuzda IPv6 desteği varsa:

| Host | Type | Value | TTL | Açıklama |
|------|------|-------|-----|----------|
| `@` | AAAA | `[IPv6 Adresi]` | 3600 | Ana domain (IPv6) |
| `www` | AAAA | `[IPv6 Adresi]` | 3600 | WWW subdomain (IPv6) |
| `app` | AAAA | `[IPv6 Adresi]` | 3600 | Frontend (IPv6) |
| `api` | AAAA | `[IPv6 Adresi]` | 3600 | API Gateway (IPv6) |
| `auth` | AAAA | `[IPv6 Adresi]` | 3600 | Keycloak (IPv6) |
| `docs` | AAAA | `[IPv6 Adresi]` | 3600 | GitLab Pages (IPv6) |
| `gitlab` | AAAA | `[IPv6 Adresi]` | 3600 | GitLab UI (IPv6) |

**Not:** IPv6 adresini öğrenmek için sunucuda `ip -6 addr show` komutunu çalıştırın.

### CNAME Kayıtları - Opsiyonel

Eğer bazı servisler için CNAME kullanmak isterseniz:

| Host | Type | Value | TTL | Açıklama |
|------|------|-------|-----|----------|
| `mail` | CNAME | `mail.hostingdunyam.net` | 3600 | Mail sunucusu (eğer hosting dünyam mail servisi kullanılıyorsa) |

---

## 🔧 DNS Yapılandırma Adımları

### 1. Hosting Dünyam DNS Paneline Giriş

1. Hosting Dünyam web sitesine giriş yapın
2. DNS yönetimi panelini açın
3. `monitrang.com` domain'ini seçin
4. DNS kayıtlarını yönetme sayfasına gidin

### 2. Mevcut A Kaydını Güncelleme

**⚠️ ÖNEMLİ:** Ekran görüntüsünde `monitrang.com.` için mevcut A kaydı `91.151.95.152` olarak görünüyor. Bu kaydı production sunucu IP'sine güncellemeniz gerekiyor.

**Ana Domain A Kaydını Güncelleme:**

1. Tabloda `monitrang.com.` için mevcut A kaydını bulun (Type: A, Name: `monitrang.com.`, Content: `91.151.95.152`)
2. Bu kaydın sağındaki **"Düzenle"** (mavi link) butonuna tıklayın
3. **Content/IP** alanını `91.151.95.152` → `45.141.151.52` olarak değiştirin
4. **TTL** değerini kontrol edin (3600 olmalı)
5. **Kaydet** veya **Güncelle** butonuna tıklayın

**Güncellenecek Kayıt:**
```
Type: A
Name: monitrang.com.
Content: 91.151.95.152 → 45.141.151.52 (GÜNCELLENECEK)
TTL: 3600
```

### 3. Yeni A Kayıtları Ekleme

Her bir subdomain için yeni A kaydı eklemeniz gerekiyor. Tabloda **"Yeni Kayıt Ekle"** veya **"Add Record"** butonunu bulun ve tıklayın.

**A Kaydı Ekleme Adımları:**

1. **Type** alanında: `A` seçin (dropdown menüden)
2. **Name** alanına: Subdomain adını girin (örn: `app`, `api`, `auth`, `docs`, `gitlab`)
   - **Not:** Sonuna nokta (`.`) eklemeyin, sadece subdomain adını yazın
   - Örnek: `app` (doğru), `app.` (yanlış), `app.monitrang.com` (yanlış)
3. **Content** alanına: `45.141.151.52` girin (production sunucu IP'si)
4. **TTL** alanına: `3600` girin (veya varsayılan değeri kullanın)
5. **Kaydet** veya **Add** butonuna tıklayın

**Eklenecek A Kayıtları:**

| Type | Name | Content | TTL |
|------|------|---------|-----|
| A | `app` | `45.141.151.52` | 3600 |
| A | `api` | `45.141.151.52` | 3600 |
| A | `auth` | `45.141.151.52` | 3600 |
| A | `docs` | `45.141.151.52` | 3600 |
| A | `gitlab` | `45.141.151.52` | 3600 |

**Örnek A Kaydı (app subdomain için):**
```
Type: A
Name: app
Content: 45.141.151.52
TTL: 3600
```

### 4. WWW CNAME Kaydı Kontrolü

Ekran görüntüsünde `www.monitrang.com.` için zaten bir CNAME kaydı var:
- Type: CNAME
- Name: `www.monitrang.com.`
- Content: `monitrang.com.`

Bu kayıt doğru görünüyor ve değiştirilmesine gerek yok. Ana domain A kaydını güncellediğinizde, www otomatik olarak aynı IP'ye yönlenecek.

### 4. WWW CNAME veya A Kaydı

WWW için iki seçenek var:

**Seçenek 1: CNAME (Önerilen)**
```
Host: www
Type: CNAME
Value: monitrang.com
TTL: 3600
```

**Seçenek 2: A Kaydı**
```
Host: www
Type: A
Value: 45.141.151.52
TTL: 3600
```

---

## ✅ DNS Kayıtları Kontrol Listesi

Aşağıdaki DNS kayıtlarının tümünü eklediğinizden emin olun:

- [ ] `@` (ana domain) → `45.141.151.52`
- [ ] `www` → `45.141.151.52` (veya CNAME: `monitrang.com`)
- [ ] `app` → `45.141.151.52`
- [ ] `api` → `45.141.151.52`
- [ ] `auth` → `45.141.151.52`
- [ ] `docs` → `45.141.151.52`
- [ ] `gitlab` → `45.141.151.52`

---

## 🔍 DNS Propagation Kontrolü

DNS kayıtlarını ekledikten sonra, değişikliklerin yayılması 5 dakika ile 48 saat arasında sürebilir. Kontrol etmek için:

### 1. Online DNS Kontrol Araçları

- **DNS Checker:** https://dnschecker.org/
- **What's My DNS:** https://www.whatsmydns.net/
- **MXToolbox:** https://mxtoolbox.com/DNSLookup.aspx

**Kontrol Adımları:**
1. Yukarıdaki araçlardan birini açın
2. Domain adını girin (örn: `app.monitrang.com`)
3. Kayıt tipini seçin (`A`)
4. Farklı DNS sunucularından kontrol edin
5. Tüm sunucularda `45.141.151.52` görünene kadar bekleyin

### 2. Komut Satırı ile Kontrol

**Windows (PowerShell):**
```powershell
# A kaydı kontrolü
Resolve-DnsName -Name app.monitrang.com -Type A

# Tüm DNS kayıtlarını listele
Resolve-DnsName -Name monitrang.com -Type ANY
```

**Linux/Mac:**
```bash
# A kaydı kontrolü
dig app.monitrang.com A

# Tüm DNS kayıtlarını listele
dig monitrang.com ANY

# Alternatif: nslookup
nslookup app.monitrang.com
```

### 3. Browser'dan Kontrol

1. Browser'ı açın
2. `app.monitrang.com` adresine gidin
3. Eğer sayfa yükleniyorsa DNS çalışıyor demektir
4. Developer Tools (F12) → Network sekmesinden IP adresini kontrol edin

---

## 🌍 Subdomain Yapılandırması

### Subdomain'ler ve Kullanım Amaçları

| Subdomain | Amaç | Nginx Location | Backend Service |
|-----------|------|----------------|-----------------|
| `app.monitrang.com` | Frontend (MngUI) | `/` | `http://localhost:3000` |
| `api.monitrang.com` | API Gateway | `/` | `http://localhost:5000` |
| `auth.monitrang.com` | Keycloak | `/` | `http://localhost:8080` |
| `docs.monitrang.com` | GitLab Pages | `/` | GitLab Pages |
| `gitlab.monitrang.com` | GitLab UI | `/` | `http://localhost:8090` |

### Nginx Yapılandırması (Gelecek)

Nginx yapılandırması için `docs/infrastructure/nginx.md` dosyasına bakın.

**Örnek Nginx Server Block:**
```nginx
# app.monitrang.com - Frontend
server {
    listen 80;
    listen [::]:80;
    server_name app.monitrang.com;

    location / {
        proxy_pass http://localhost:3000;
        proxy_http_version 1.1;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}
```

---

## 🔐 SSL/TLS Sertifikası (Let's Encrypt)

DNS kayıtları yayıldıktan sonra SSL sertifikası kurulabilir. Detaylar için `docs/infrastructure/ssl-certificates.md` dosyasına bakın.

**Özet:**
1. Certbot kurulumu
2. Let's Encrypt sertifikası alma
3. Nginx yapılandırması
4. Otomatik yenileme yapılandırması

---

## 📝 Test Senaryoları

### 1. DNS Kayıt Kontrolü

```bash
# Her subdomain için A kaydını kontrol et
dig app.monitrang.com A
dig api.monitrang.com A
dig auth.monitrang.com A
dig docs.monitrang.com A
dig gitlab.monitrang.com A
```

**Beklenen Sonuç:** Tüm kayıtlar `45.141.151.52` IP adresini göstermeli.

### 2. HTTP Erişim Testi

```bash
# Her subdomain için HTTP erişimini test et
curl -I http://app.monitrang.com
curl -I http://api.monitrang.com
curl -I http://auth.monitrang.com
curl -I http://docs.monitrang.com
curl -I http://gitlab.monitrang.com
```

**Beklenen Sonuç:** HTTP 200 veya 301/302 (redirect) yanıtı alınmalı.

### 3. Browser Testi

1. Her subdomain'i browser'da açın
2. Sayfanın yüklendiğini kontrol edin
3. Developer Tools (F12) → Network sekmesinden IP adresini kontrol edin

---

## ⚠️ Bilinen Sorunlar ve Çözümler

### 1. DNS Propagation Gecikmesi

**Sorun:** DNS kayıtları hemen görünmüyor.

**Çözüm:**
- TTL değerini düşürün (örn: 300 saniye)
- DNS cache'i temizleyin (`ipconfig /flushdns` Windows, `sudo systemd-resolve --flush-caches` Linux)
- Farklı DNS sunucularından kontrol edin (Google DNS: 8.8.8.8, Cloudflare: 1.1.1.1)

### 2. Nameserver Değişikliği Gecikmesi

**Sorun:** Nameserver değişikliği hemen etkili olmuyor.

**Çözüm:**
- Nameserver değişikliği 24-48 saat sürebilir
- Registrar'da nameserver'ların doğru yapılandırıldığını kontrol edin
- `whois monitrang.com` komutu ile nameserver'ları kontrol edin

### 3. Subdomain Erişim Sorunu

**Sorun:** Subdomain'ler çalışmıyor.

**Çözüm:**
- DNS kayıtlarının doğru eklendiğini kontrol edin
- Nginx yapılandırmasını kontrol edin
- Firewall kurallarını kontrol edin
- Sunucu loglarını kontrol edin (`/var/log/nginx/error.log`)

---

## 📚 İlgili Dokümantasyon

- **Nginx Yapılandırması:** `docs/infrastructure/nginx.md`
- **SSL/TLS Sertifikaları:** `docs/infrastructure/ssl-certificates.md`
- **Port Yapılandırması:** `docs/infrastructure/ports.md`
- **Deployment Rehberi:** `docs/content/cicd/DEPLOYMENT_GUIDE.md`

---

## ✅ Tamamlanma Kontrol Listesi

- [x] Hosting Dünyam DNS paneline giriş yapıldı ✅
- [x] Tüm A kayıtları eklendi (`@`, `www`, `app`, `api`, `auth`, `docs`, `gitlab`) ✅
- [x] DNS propagation kontrol edildi (tüm DNS sunucularında `45.141.151.52` görünüyor) ✅
- [x] Her subdomain için HTTP erişim testi yapıldı ✅
- [x] Browser'dan erişim testi yapıldı ✅
- [ ] Nginx yapılandırması hazırlandı (sonraki adım)
- [ ] SSL/TLS sertifikası kuruldu (sonraki adım)

---

**Son Güncelleme:** 2 Ocak 2026  
**Durum:** ✅ DNS kayıtları başarıyla eklendi ve çalışıyor

