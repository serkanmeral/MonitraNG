# Nginx Reverse Proxy Yapılandırması

**Tarih:** 2 Ocak 2026  
**Domain:** `monitrang.com`  
**Production Sunucu IP:** `45.141.151.52`  
**Durum:** ⏳ Yapılandırılacak

---

## 📋 Genel Bakış

Nginx reverse proxy, tüm subdomain'leri backend servislere yönlendirir ve SSL/TLS terminasyonu sağlar.

### Subdomain Yapısı

| Subdomain | Backend Service | Port | Amaç |
|-----------|----------------|------|------|
| `app.monitrang.com` | MngUI (Frontend) | `3000` | Frontend uygulaması |
| `api.monitrang.com` | MngGateway (API Gateway) | `5000` | API Gateway |
| `auth.monitrang.com` | Keycloak | `8080` | Authentication servisi |
| `docs.monitrang.com` | GitLab Pages | `8090` | Dokümantasyon |
| `gitlab.monitrang.com` | GitLab UI | `8090` | GitLab web arayüzü |
| `monitrang.com` | MngUI (Frontend) | `3000` | Ana domain (frontend) |
| `www.monitrang.com` | MngUI (Frontend) | `3000` | WWW subdomain (frontend) |

---

## 🔧 Port Yapılandırmaları

### Infrastructure Servisleri

| Servis | Container Port | Host Port | Protokol | Açıklama |
|--------|----------------|------------|----------|----------|
| **Keycloak** | `8080` | `8080` | HTTP | Authentication servisi |
| **MongoDB** | `27017` | `27017` | TCP | Database (internal only) |
| **Redis** | `6379` | `6379` | TCP | Cache (internal only) |
| **RabbitMQ** | `5672` | `5672` | TCP | Message queue (internal only) |
| **RabbitMQ Management** | `15672` | `15672` | HTTP | Management UI (opsiyonel) |
| **MinIO API** | `9000` | `9090` | HTTP | Object storage API |
| **MinIO Console** | `9001` | `9091` | HTTP | Object storage Console |
| **PostgreSQL** | `5432` | `5432` | TCP | Database (internal only) |
| **GitLab** | `80` | `8090` | HTTP | GitLab web arayüzü |
| **GitLab SSH** | `22` | `2222` | TCP | GitLab SSH |
| **Mongo Express** | `8081` | `8081` | HTTP | MongoDB web UI (opsiyonel) |

### Application Servisleri

| Servis | Container Port | Host Port | Protokol | Açıklama |
|--------|----------------|------------|----------|----------|
| **MngGateway** | `5000` | `5000` | HTTP | API Gateway |
| **MngGateway** | `443` | `5443` | HTTPS | API Gateway (HTTPS) |
| **MngKeeper** | `5001` | `5001` | HTTPS | Identity & Access Management |
| **MngDataGateway** | `5010` | `5010` | HTTPS | Data Gateway |
| **MngHub** | `5020` | `5020` | HTTP | Hub servisi (SignalR) |
| **MngUI** | `80` | `3000` | HTTP | Frontend uygulaması |

---

## 🌐 Nginx Yapılandırma Dosyası

### Dosya Konumu

**Production Sunucu:** `/etc/nginx/sites-available/monitrang`

### Yapılandırma Dosyası

```nginx
# ============================================
# MonitraNG - Nginx Reverse Proxy Configuration
# Domain: monitrang.com
# Production Server: 45.141.151.52
# ============================================

# HTTP → HTTPS Redirect (Ana Domain ve WWW)
server {
    listen 80;
    listen [::]:80;
    server_name monitrang.com www.monitrang.com;

    # Let's Encrypt verification için
    location /.well-known/acme-challenge/ {
        root /var/www/html;
    }

    # Diğer tüm istekleri HTTPS'ye yönlendir
    location / {
        return 301 https://$server_name$request_uri;
    }
}

# ============================================
# app.monitrang.com - Frontend (MngUI)
# ============================================
server {
    listen 80;
    listen [::]:80;
    server_name app.monitrang.com;

    # Let's Encrypt verification için
    location /.well-known/acme-challenge/ {
        root /var/www/html;
    }

    # HTTP → HTTPS redirect
    location / {
        return 301 https://$server_name$request_uri;
    }
}

server {
    listen 443 ssl http2;
    listen [::]:443 ssl http2;
    server_name app.monitrang.com;

    # SSL Certificate (Let's Encrypt - gelecekte eklenecek)
    # ssl_certificate /etc/letsencrypt/live/monitrang.com/fullchain.pem;
    # ssl_certificate_key /etc/letsencrypt/live/monitrang.com/privkey.pem;
    
    # SSL Configuration (self-signed için şimdilik)
    ssl_protocols TLSv1.2 TLSv1.3;
    ssl_ciphers HIGH:!aNULL:!MD5;
    ssl_prefer_server_ciphers on;
    ssl_session_cache shared:SSL:10m;
    ssl_session_timeout 10m;

    # Security Headers
    add_header Strict-Transport-Security "max-age=31536000; includeSubDomains" always;
    add_header X-Frame-Options "SAMEORIGIN" always;
    add_header X-Content-Type-Options "nosniff" always;
    add_header X-XSS-Protection "1; mode=block" always;
    add_header Referrer-Policy "no-referrer-when-downgrade" always;

    # Logging
    access_log /var/log/nginx/app.monitrang.com-access.log;
    error_log /var/log/nginx/app.monitrang.com-error.log;

    # Frontend (MngUI)
    location / {
        proxy_pass http://localhost:3000;
        proxy_http_version 1.1;
        
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_set_header X-Forwarded-Host $host;
        proxy_set_header X-Forwarded-Port $server_port;
        
        # WebSocket support (SignalR için)
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection "upgrade";
        
        # Timeouts
        proxy_connect_timeout 60s;
        proxy_send_timeout 60s;
        proxy_read_timeout 60s;
    }

    # Health check endpoint
    location /health {
        proxy_pass http://localhost:3000/health;
        access_log off;
    }
}

# ============================================
# api.monitrang.com - API Gateway (MngGateway)
# ============================================
server {
    listen 80;
    listen [::]:80;
    server_name api.monitrang.com;

    # Let's Encrypt verification için
    location /.well-known/acme-challenge/ {
        root /var/www/html;
    }

    # HTTP → HTTPS redirect
    location / {
        return 301 https://$server_name$request_uri;
    }
}

server {
    listen 443 ssl http2;
    listen [::]:443 ssl http2;
    server_name api.monitrang.com;

    # SSL Certificate (Let's Encrypt - gelecekte eklenecek)
    # ssl_certificate /etc/letsencrypt/live/monitrang.com/fullchain.pem;
    # ssl_certificate_key /etc/letsencrypt/live/monitrang.com/privkey.pem;
    
    # SSL Configuration
    ssl_protocols TLSv1.2 TLSv1.3;
    ssl_ciphers HIGH:!aNULL:!MD5;
    ssl_prefer_server_ciphers on;
    ssl_session_cache shared:SSL:10m;
    ssl_session_timeout 10m;

    # Security Headers
    add_header Strict-Transport-Security "max-age=31536000; includeSubDomains" always;
    add_header X-Frame-Options "SAMEORIGIN" always;
    add_header X-Content-Type-Options "nosniff" always;
    add_header X-XSS-Protection "1; mode=block" always;

    # Logging
    access_log /var/log/nginx/api.monitrang.com-access.log;
    error_log /var/log/nginx/api.monitrang.com-error.log;

    # API Gateway (MngGateway)
    location / {
        proxy_pass http://localhost:5000;
        proxy_http_version 1.1;
        
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_set_header X-Forwarded-Host $host;
        proxy_set_header X-Forwarded-Port $server_port;
        
        # CORS headers (backend'den gelen CORS header'ları geçir)
        proxy_pass_header Access-Control-Allow-Origin;
        proxy_pass_header Access-Control-Allow-Methods;
        proxy_pass_header Access-Control-Allow-Headers;
        proxy_pass_header Access-Control-Allow-Credentials;
        
        # Timeouts
        proxy_connect_timeout 60s;
        proxy_send_timeout 60s;
        proxy_read_timeout 60s;
        
        # Request size limit (file upload için)
        client_max_body_size 100M;
    }

    # Health check endpoint
    location /health {
        proxy_pass http://localhost:5000/health;
        access_log off;
    }
}

# ============================================
# auth.monitrang.com - Keycloak
# ============================================
server {
    listen 80;
    listen [::]:80;
    server_name auth.monitrang.com;

    # Let's Encrypt verification için
    location /.well-known/acme-challenge/ {
        root /var/www/html;
    }

    # HTTP → HTTPS redirect
    location / {
        return 301 https://$server_name$request_uri;
    }
}

server {
    listen 443 ssl http2;
    listen [::]:443 ssl http2;
    server_name auth.monitrang.com;

    # SSL Certificate (Let's Encrypt - gelecekte eklenecek)
    # ssl_certificate /etc/letsencrypt/live/monitrang.com/fullchain.pem;
    # ssl_certificate_key /etc/letsencrypt/live/monitrang.com/privkey.pem;
    
    # SSL Configuration
    ssl_protocols TLSv1.2 TLSv1.3;
    ssl_ciphers HIGH:!aNULL:!MD5;
    ssl_prefer_server_ciphers on;
    ssl_session_cache shared:SSL:10m;
    ssl_session_timeout 10m;

    # Security Headers
    add_header Strict-Transport-Security "max-age=31536000; includeSubDomains" always;
    add_header X-Frame-Options "SAMEORIGIN" always;
    add_header X-Content-Type-Options "nosniff" always;
    add_header X-XSS-Protection "1; mode=block" always;

    # Logging
    access_log /var/log/nginx/auth.monitrang.com-access.log;
    error_log /var/log/nginx/auth.monitrang.com-error.log;

    # Keycloak
    location / {
        proxy_pass http://localhost:8080;
        proxy_http_version 1.1;
        
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_set_header X-Forwarded-Host $host;
        proxy_set_header X-Forwarded-Port $server_port;
        
        # Keycloak proxy headers
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_set_header X-Forwarded-Host $host;
        proxy_set_header X-Forwarded-Server $host;
        
        # Timeouts
        proxy_connect_timeout 60s;
        proxy_send_timeout 60s;
        proxy_read_timeout 60s;
        
        # Request size limit
        client_max_body_size 10M;
    }
}

# ============================================
# docs.monitrang.com - GitLab Pages (Dokümantasyon)
# ============================================
server {
    listen 80;
    listen [::]:80;
    server_name docs.monitrang.com;

    # Let's Encrypt verification için
    location /.well-known/acme-challenge/ {
        root /var/www/html;
    }

    # HTTP → HTTPS redirect
    location / {
        return 301 https://$server_name$request_uri;
    }
}

server {
    listen 443 ssl http2;
    listen [::]:443 ssl http2;
    server_name docs.monitrang.com;

    # SSL Certificate (Let's Encrypt - gelecekte eklenecek)
    # ssl_certificate /etc/letsencrypt/live/monitrang.com/fullchain.pem;
    # ssl_certificate_key /etc/letsencrypt/live/monitrang.com/privkey.pem;
    
    # SSL Configuration
    ssl_protocols TLSv1.2 TLSv1.3;
    ssl_ciphers HIGH:!aNULL:!MD5;
    ssl_prefer_server_ciphers on;
    ssl_session_cache shared:SSL:10m;
    ssl_session_timeout 10m;

    # Security Headers
    add_header Strict-Transport-Security "max-age=31536000; includeSubDomains" always;
    add_header X-Frame-Options "SAMEORIGIN" always;
    add_header X-Content-Type-Options "nosniff" always;
    add_header X-XSS-Protection "1; mode=block" always;

    # Logging
    access_log /var/log/nginx/docs.monitrang.com-access.log;
    error_log /var/log/nginx/docs.monitrang.com-error.log;

    # GitLab Pages (dokümantasyon)
    # Not: GitLab Pages'in nasıl serve edildiğine bağlı olarak bu yapılandırma değişebilir
    # Şimdilik GitLab'ın kendi port'una yönlendiriyoruz
    location / {
        proxy_pass http://localhost:8090;
        proxy_http_version 1.1;
        
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_set_header X-Forwarded-Host $host;
        proxy_set_header X-Forwarded-Port $server_port;
        
        # Timeouts
        proxy_connect_timeout 60s;
        proxy_send_timeout 60s;
        proxy_read_timeout 60s;
    }
}

# ============================================
# gitlab.monitrang.com - GitLab UI
# ============================================
server {
    listen 80;
    listen [::]:80;
    server_name gitlab.monitrang.com;

    # Let's Encrypt verification için
    location /.well-known/acme-challenge/ {
        root /var/www/html;
    }

    # HTTP → HTTPS redirect
    location / {
        return 301 https://$server_name$request_uri;
    }
}

server {
    listen 443 ssl http2;
    listen [::]:443 ssl http2;
    server_name gitlab.monitrang.com;

    # SSL Certificate (Let's Encrypt - gelecekte eklenecek)
    # ssl_certificate /etc/letsencrypt/live/monitrang.com/fullchain.pem;
    # ssl_certificate_key /etc/letsencrypt/live/monitrang.com/privkey.pem;
    
    # SSL Configuration
    ssl_protocols TLSv1.2 TLSv1.3;
    ssl_ciphers HIGH:!aNULL:!MD5;
    ssl_prefer_server_ciphers on;
    ssl_session_cache shared:SSL:10m;
    ssl_session_timeout 10m;

    # Security Headers
    add_header Strict-Transport-Security "max-age=31536000; includeSubDomains" always;
    add_header X-Frame-Options "SAMEORIGIN" always;
    add_header X-Content-Type-Options "nosniff" always;
    add_header X-XSS-Protection "1; mode=block" always;

    # Logging
    access_log /var/log/nginx/gitlab.monitrang.com-access.log;
    error_log /var/log/nginx/gitlab.monitrang.com-error.log;

    # GitLab UI
    location / {
        proxy_pass http://localhost:8090;
        proxy_http_version 1.1;
        
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_set_header X-Forwarded-Host $host;
        proxy_set_header X-Forwarded-Port $server_port;
        
        # GitLab specific headers
        proxy_set_header X-Forwarded-Ssl on;
        
        # Timeouts
        proxy_connect_timeout 60s;
        proxy_send_timeout 60s;
        proxy_read_timeout 60s;
        
        # Request size limit (GitLab için büyük dosyalar)
        client_max_body_size 100M;
    }
}

# ============================================
# Opsiyonel: Management UI'lar (Internal)
# ============================================

# RabbitMQ Management UI (opsiyonel - internal only)
# server {
#     listen 443 ssl http2;
#     server_name rabbitmq.monitrang.com;
#     
#     # SSL Configuration (aynı yukarıdaki gibi)
#     
#     location / {
#         proxy_pass http://localhost:15672;
#         proxy_http_version 1.1;
#         proxy_set_header Host $host;
#         proxy_set_header X-Real-IP $remote_addr;
#         proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
#         proxy_set_header X-Forwarded-Proto $scheme;
#         
#         # Basic Auth eklenebilir
#         # auth_basic "RabbitMQ Management";
#         # auth_basic_user_file /etc/nginx/.htpasswd;
#     }
# }

# MinIO Console (opsiyonel - internal only)
# server {
#     listen 443 ssl http2;
#     server_name minio.monitrang.com;
#     
#     # SSL Configuration (aynı yukarıdaki gibi)
#     
#     location / {
#         proxy_pass http://localhost:9091;
#         proxy_http_version 1.1;
#         proxy_set_header Host $host;
#         proxy_set_header X-Real-IP $remote_addr;
#         proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
#         proxy_set_header X-Forwarded-Proto $scheme;
#     }
# }
```

---

## 📝 Nginx Yapılandırma Adımları

### 1. Nginx Kurulumu Kontrolü

Production sunucusunda Nginx'in kurulu olduğundan emin olun:

```bash
# Nginx kurulumunu kontrol et
nginx -v

# Nginx servis durumunu kontrol et
sudo systemctl status nginx
```

**Eğer Nginx kurulu değilse:**

```bash
# Debian/Ubuntu
sudo apt update
sudo apt install -y nginx

# Nginx'i başlat ve otomatik başlatmayı etkinleştir
sudo systemctl start nginx
sudo systemctl enable nginx
```

### 2. Yapılandırma Dosyasını Oluşturma

```bash
# Yapılandırma dosyasını oluştur
sudo nano /etc/nginx/sites-available/monitrang

# Yukarıdaki yapılandırmayı yapıştırın
# Ctrl+X, Y, Enter ile kaydedin
```

### 3. Yapılandırmayı Aktifleştirme

```bash
# Yapılandırmayı sites-enabled'e linkle
sudo ln -s /etc/nginx/sites-available/monitrang /etc/nginx/sites-enabled/

# Varsayılan yapılandırmayı devre dışı bırak (opsiyonel)
sudo rm /etc/nginx/sites-enabled/default
```

### 4. Yapılandırmayı Test Etme

```bash
# Nginx yapılandırmasını test et
sudo nginx -t
```

**Beklenen Çıktı:**
```
nginx: the configuration file /etc/nginx/nginx.conf syntax is ok
nginx: configuration file /etc/nginx/nginx.conf test is successful
```

### 5. Nginx'i Yeniden Başlatma

```bash
# Nginx'i yeniden başlat
sudo systemctl reload nginx

# Veya
sudo systemctl restart nginx

# Durumu kontrol et
sudo systemctl status nginx
```

---

## 🔒 SSL/TLS Sertifikası (Gelecek Adım)

Şu anda yapılandırma HTTP → HTTPS redirect içeriyor ancak SSL sertifikası henüz kurulmadı. SSL sertifikası kurulana kadar HTTP çalışacak.

**Not:** SSL sertifikası kurulduğunda, yapılandırma dosyasındaki SSL certificate satırlarının yorumlarını kaldırmanız gerekecek.

---

## 🔍 Test ve Doğrulama

### 1. HTTP Erişim Testi

```bash
# Her subdomain için HTTP erişimini test et
curl -I http://app.monitrang.com
curl -I http://api.monitrang.com
curl -I http://auth.monitrang.com
curl -I http://docs.monitrang.com
curl -I http://gitlab.monitrang.com
```

**Beklenen Sonuç:** HTTP 301 (redirect to HTTPS) veya HTTP 200

### 2. HTTPS Erişim Testi (SSL kurulduktan sonra)

```bash
# Her subdomain için HTTPS erişimini test et
curl -I -k https://app.monitrang.com
curl -I -k https://api.monitrang.com
curl -I -k https://auth.monitrang.com
```

### 3. Browser Testi

1. Her subdomain'i browser'da açın
2. Sayfanın yüklendiğini kontrol edin
3. Developer Tools (F12) → Network sekmesinden yanıt kodlarını kontrol edin

### 4. Log Kontrolü

```bash
# Nginx access log'larını kontrol et
sudo tail -f /var/log/nginx/app.monitrang.com-access.log
sudo tail -f /var/log/nginx/api.monitrang.com-access.log

# Nginx error log'larını kontrol et
sudo tail -f /var/log/nginx/error.log
```

---

## ⚠️ Bilinen Sorunlar ve Çözümler

### 1. SSL Sertifikası Hatası

**Sorun:** HTTPS erişiminde SSL sertifikası hatası.

**Çözüm:**
- Let's Encrypt sertifikası kurulana kadar `-k` flag'i ile test edin
- SSL sertifikası kurulduğunda yapılandırma dosyasındaki SSL satırlarının yorumlarını kaldırın

### 2. 502 Bad Gateway Hatası

**Sorun:** Backend servis çalışmıyor veya port yanlış.

**Çözüm:**
```bash
# Backend servislerin çalıştığını kontrol et
docker ps | grep mngui
docker ps | grep mnggateway
docker ps | grep keycloak

# Port'ların doğru olduğunu kontrol et
netstat -tlnp | grep 3000
netstat -tlnp | grep 5000
netstat -tlnp | grep 8080
```

### 3. Location Sırası Sorunu

**Sorun:** Bazı location'lar diğerlerini override ediyor.

**Çözüm:**
- Location block'larını öncelik sırasına göre düzenleyin
- Daha spesifik location'ları (örn: `/api/keeper/`) daha genel location'lardan (örn: `/api/`) önce yazın

### 4. CORS Hatası

**Sorun:** Frontend'den API'ye istek atılırken CORS hatası.

**Çözüm:**
- Nginx yapılandırmasında CORS header'larını kontrol edin
- Backend servislerde CORS yapılandırmasını kontrol edin

---

## 📚 İlgili Dokümantasyon

- **Port Yapılandırması:** `docs/infrastructure/ports.md` (oluşturulacak)
- **SSL/TLS Sertifikaları:** `docs/infrastructure/ssl-certificates.md` (oluşturulacak)
- **Domain ve DNS:** `docs/infrastructure/domain-dns.md`
- **Deployment Rehberi:** `docs/content/cicd/DEPLOYMENT_GUIDE.md`

---

## ✅ Tamamlanma Kontrol Listesi

- [ ] Nginx kurulumu kontrol edildi
- [ ] Yapılandırma dosyası oluşturuldu (`/etc/nginx/sites-available/monitrang`)
- [ ] Yapılandırma aktifleştirildi (`/etc/nginx/sites-enabled/monitrang`)
- [ ] Nginx yapılandırması test edildi (`nginx -t`)
- [ ] Nginx yeniden başlatıldı
- [ ] Her subdomain için HTTP erişim testi yapıldı
- [ ] Browser'dan erişim testi yapıldı
- [ ] Log dosyaları kontrol edildi
- [ ] SSL/TLS sertifikası kuruldu (gelecek adım)

---

**Son Güncelleme:** 2 Ocak 2026  
**Durum:** ⏳ Yapılandırılacak

