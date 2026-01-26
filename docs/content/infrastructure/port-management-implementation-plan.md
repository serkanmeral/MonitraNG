# Port Yönetimi Implementation Planı

**Tarih:** 4 Ocak 2026  
**Yaklaşım:** Phase 2 - Nginx Containerization + Container Name'ler  
**Durum:** ✅ Tamamlandı

---

## 📋 Genel Bakış

Bu dokümantasyon, port yönetimi planının Phase 2 yaklaşımına göre uygulanması için detaylı adımları içerir.

### Hedefler

1. ✅ Nginx'i Docker container olarak çalıştırmak
2. ✅ Tüm servislerin port mapping'lerini kaldırmak
3. ✅ Container name'ler üzerinden servis erişimi sağlamak
4. ✅ Güvenliği artırmak (sadece 80, 443 portları açık)
5. ✅ Modern ve maintainable bir yapı oluşturmak

---

## 🎯 Implementation Phases

### Phase 1: Hazırlık ve Planlama ✅
- [x] Mevcut durum analizi
- [x] Backup stratejisi
- [x] Rollback planı
- [x] Test ortamı hazırlığı

### Phase 2: Nginx Containerization ✅
- [x] Nginx Docker Compose yapılandırması
- [x] Nginx yapılandırma dosyalarını organize etme
- [x] Container name'ler kullanacak şekilde güncelleme
- [x] Let's Encrypt sertifikalarını yapılandırma
- [x] Nginx container'ını test etme

### Phase 3: Port Mapping'leri Kaldırma ✅
- [x] GitLab port mapping'leri kaldırıldı
- [x] Application servislerin port mapping'lerini kaldırma (docker-compose.production.yml)
- [x] Keycloak port mapping'i kaldırıldı
- [x] Network yapılandırmasını doğrulama

### Phase 4: Test ve Doğrulama ✅
- [x] Servis erişim testleri
- [x] Port kontrolü
- [x] Container name erişimi test edildi
- [x] Nginx yapılandırması test edildi

### Phase 5: Dokümantasyon ve Temizlik ✅
- [ ] Dokümantasyon güncelleme
- [ ] Script'ler oluşturma
- [ ] Eski yapılandırmaları temizleme

---

## 📝 Detaylı Implementation Adımları

### Phase 1: Hazırlık ve Planlama

#### 1.1 Mevcut Durum Analizi

**Yapılacaklar:**
```bash
# Mevcut port kullanımını kontrol et
sudo netstat -tlnp | grep LISTEN

# Docker container port mapping'lerini listele
docker ps --format "table {{.Names}}\t{{.Ports}}"

# Nginx yapılandırmasını yedekle
sudo cp /etc/nginx/sites-available/monitrang /etc/nginx/sites-available/monitrang.backup

# Docker Compose dosyalarını yedekle
cp ApplicationResources/mng_common/docker-compose.yml ApplicationResources/mng_common/docker-compose.yml.backup
cp ApplicationResources/mng_apps/docker-compose.yml ApplicationResources/mng_apps/docker-compose.yml.backup
cp ApplicationResources/mng_apps/docker-compose.production.yml ApplicationResources/mng_apps/docker-compose.production.yml.backup
```

**Kontrol Listesi:**
- [ ] Mevcut port kullanımı dokümante edildi
- [ ] Nginx yapılandırması yedeklendi
- [ ] Docker Compose dosyaları yedeklendi
- [ ] Mevcut servis durumu kontrol edildi

#### 1.2 Backup Stratejisi

**Yedeklenecek Dosyalar:**
- `/etc/nginx/sites-available/monitrang`
- `/etc/nginx/sites-enabled/monitrang`
- `/etc/letsencrypt/` (sertifikalar)
- `ApplicationResources/mng_common/docker-compose.yml`
- `ApplicationResources/mng_apps/docker-compose.yml`
- `ApplicationResources/mng_apps/docker-compose.production.yml`

**Backup Komutları:**
```bash
# Backup dizini oluştur
mkdir -p ~/backups/port-management-$(date +%Y%m%d)

# Nginx yapılandırmasını yedekle
sudo cp -r /etc/nginx/sites-available/* ~/backups/port-management-$(date +%Y%m%d)/nginx/

# Let's Encrypt sertifikalarını yedekle (opsiyonel - büyük olabilir)
# sudo cp -r /etc/letsencrypt ~/backups/port-management-$(date +%Y%m%d)/letsencrypt/

# Docker Compose dosyalarını yedekle
cp ApplicationResources/mng_common/docker-compose.yml ~/backups/port-management-$(date +%Y%m%d)/
cp ApplicationResources/mng_apps/docker-compose.yml ~/backups/port-management-$(date +%Y%m%d)/
cp ApplicationResources/mng_apps/docker-compose.production.yml ~/backups/port-management-$(date +%Y%m%d)/
```

#### 1.3 Rollback Planı

**Rollback Senaryosu:**
1. Nginx container'ını durdur
2. Host üzerindeki Nginx'i yeniden başlat
3. Yedeklenen yapılandırmayı geri yükle
4. Port mapping'leri geri ekle
5. Servisleri yeniden başlat

**Rollback Komutları:**
```bash
# Nginx container'ını durdur
docker stop nginx
docker rm nginx

# Host Nginx'i başlat
sudo systemctl start nginx

# Yedeklenen yapılandırmayı geri yükle
sudo cp ~/backups/port-management-YYYYMMDD/nginx/monitrang /etc/nginx/sites-available/monitrang
sudo nginx -t
sudo systemctl reload nginx

# Port mapping'leri geri ekle (docker-compose.yml dosyalarını geri yükle)
cp ~/backups/port-management-YYYYMMDD/docker-compose.yml ApplicationResources/mng_common/
docker compose up -d
```

---

### Phase 2: Nginx Containerization

#### 2.1 Nginx Yapılandırma Dosya Yapısı Oluşturma

**Dizin Yapısı:**
```
ApplicationResources/
  mng_common/
    nginx/
      nginx.conf              # Ana Nginx yapılandırması
      conf.d/
        monitrang.conf        # MonitraNG domain yapılandırması
        mail.conf             # Mail sunucusu yapılandırması
      ssl/
        ssl-params.conf       # SSL parametreleri
      templates/              # Template dosyalar (opsiyonel)
```

**Oluşturulacak Dosyalar:**
```bash
# Dizin yapısını oluştur
mkdir -p ApplicationResources/mng_common/nginx/conf.d
mkdir -p ApplicationResources/mng_common/nginx/ssl
```

#### 2.2 Nginx Ana Yapılandırma Dosyası

**Dosya:** `ApplicationResources/mng_common/nginx/nginx.conf`

```nginx
user nginx;
worker_processes auto;
error_log /var/log/nginx/error.log warn;
pid /var/run/nginx.pid;

events {
    worker_connections 1024;
    use epoll;
}

http {
    include /etc/nginx/mime.types;
    default_type application/octet-stream;

    log_format main '$remote_addr - $remote_user [$time_local] "$request" '
                    '$status $body_bytes_sent "$http_referer" '
                    '"$http_user_agent" "$http_x_forwarded_for"';

    access_log /var/log/nginx/access.log main;

    sendfile on;
    tcp_nopush on;
    tcp_nodelay on;
    keepalive_timeout 65;
    types_hash_max_size 2048;
    client_max_body_size 100M;

    gzip on;
    gzip_vary on;
    gzip_proxied any;
    gzip_comp_level 6;
    gzip_types text/plain text/css text/xml text/javascript application/json application/javascript application/xml+rss application/rss+xml font/truetype font/opentype application/vnd.ms-fontobject image/svg+xml;

    # SSL Configuration
    include /etc/nginx/ssl/ssl-params.conf;

    # Include server configurations
    include /etc/nginx/conf.d/*.conf;
}
```

#### 2.3 SSL Parametreleri

**Dosya:** `ApplicationResources/mng_common/nginx/ssl/ssl-params.conf`

```nginx
# SSL Configuration
ssl_protocols TLSv1.2 TLSv1.3;
ssl_ciphers 'ECDHE-ECDSA-AES128-GCM-SHA256:ECDHE-RSA-AES128-GCM-SHA256:ECDHE-ECDSA-AES256-GCM-SHA384:ECDHE-RSA-AES256-GCM-SHA384:ECDHE-ECDSA-CHACHA20-POLY1305:ECDHE-RSA-CHACHA20-POLY1305:DHE-RSA-AES128-GCM-SHA256:DHE-RSA-AES256-GCM-SHA384';
ssl_prefer_server_ciphers off;
ssl_session_cache shared:SSL:10m;
ssl_session_timeout 10m;
ssl_session_tickets off;

# OCSP Stapling
ssl_stapling on;
ssl_stapling_verify on;
resolver 8.8.8.8 8.8.4.4 valid=300s;
resolver_timeout 5s;

# Security Headers
add_header Strict-Transport-Security "max-age=31536000; includeSubDomains; preload" always;
add_header X-Frame-Options "SAMEORIGIN" always;
add_header X-Content-Type-Options "nosniff" always;
add_header X-XSS-Protection "1; mode=block" always;
add_header Referrer-Policy "no-referrer-when-downgrade" always;
```

#### 2.4 MonitraNG Domain Yapılandırması

**Dosya:** `ApplicationResources/mng_common/nginx/conf.d/monitrang.conf`

```nginx
# ============================================
# MonitraNG - Nginx Reverse Proxy Configuration
# Domain: monitrang.com
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

    location /.well-known/acme-challenge/ {
        root /var/www/html;
    }

    location / {
        return 301 https://$server_name$request_uri;
    }
}

server {
    listen 443 ssl http2;
    listen [::]:443 ssl http2;
    server_name app.monitrang.com;

    # SSL Certificate
    ssl_certificate /etc/letsencrypt/live/monitrang.com/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/monitrang.com/privkey.pem;
    include /etc/nginx/ssl/ssl-params.conf;

    # Logging
    access_log /var/log/nginx/app.monitrang.com-access.log main;
    error_log /var/log/nginx/app.monitrang.com-error.log warn;

    # Frontend (MngUI) - Container name kullanıyor
    location / {
        proxy_pass http://mngui:80;
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
        proxy_pass http://mngui:80/health;
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

    location /.well-known/acme-challenge/ {
        root /var/www/html;
    }

    location / {
        return 301 https://$server_name$request_uri;
    }
}

server {
    listen 443 ssl http2;
    listen [::]:443 ssl http2;
    server_name api.monitrang.com;

    # SSL Certificate
    ssl_certificate /etc/letsencrypt/live/monitrang.com/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/monitrang.com/privkey.pem;
    include /etc/nginx/ssl/ssl-params.conf;

    # Logging
    access_log /var/log/nginx/api.monitrang.com-access.log main;
    error_log /var/log/nginx/api.monitrang.com-error.log warn;

    # API Gateway (MngGateway) - Container name kullanıyor
    location / {
        proxy_pass http://mnggateway:5000;
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

    # Health check endpoint
    location /health {
        proxy_pass http://mnggateway:5000/health;
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

    location /.well-known/acme-challenge/ {
        root /var/www/html;
    }

    location / {
        return 301 https://$server_name$request_uri;
    }
}

server {
    listen 443 ssl http2;
    listen [::]:443 ssl http2;
    server_name auth.monitrang.com;

    # SSL Certificate
    ssl_certificate /etc/letsencrypt/live/monitrang.com/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/monitrang.com/privkey.pem;
    include /etc/nginx/ssl/ssl-params.conf;

    # Logging
    access_log /var/log/nginx/auth.monitrang.com-access.log main;
    error_log /var/log/nginx/auth.monitrang.com-error.log warn;

    # Keycloak - Container name kullanıyor
    location / {
        proxy_pass http://keycloak:8080;
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
# gitlab.monitrang.com - GitLab
# ============================================
server {
    listen 80;
    listen [::]:80;
    server_name gitlab.monitrang.com;

    location /.well-known/acme-challenge/ {
        root /var/www/html;
    }

    location / {
        return 301 https://$server_name$request_uri;
    }
}

server {
    listen 443 ssl http2;
    listen [::]:443 ssl http2;
    server_name gitlab.monitrang.com;

    # SSL Certificate
    ssl_certificate /etc/letsencrypt/live/monitrang.com/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/monitrang.com/privkey.pem;
    include /etc/nginx/ssl/ssl-params.conf;

    # Logging
    access_log /var/log/nginx/gitlab.monitrang.com-access.log main;
    error_log /var/log/nginx/gitlab.monitrang.com-error.log warn;

    # GitLab - Container name kullanıyor
    location / {
        proxy_pass http://gitlab:80;
        proxy_http_version 1.1;
        
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_set_header X-Forwarded-Host $host;
        proxy_set_header X-Forwarded-Port $server_port;
        
        # GitLab için özel header'lar
        proxy_set_header X-Forwarded-Ssl on;
        
        # Timeouts
        proxy_connect_timeout 60s;
        proxy_send_timeout 60s;
        proxy_read_timeout 60s;
    }
}
```

#### 2.5 Docker Compose Yapılandırması

**Dosya:** `ApplicationResources/mng_common/docker-compose.yml` (Nginx servisi eklenecek)

```yaml
services:
  # ... mevcut servisler ...

  # Nginx Reverse Proxy
  nginx:
    image: nginx:alpine
    container_name: nginx
    ports:
      - "80:80"
      - "443:443"
    volumes:
      - ./nginx/nginx.conf:/etc/nginx/nginx.conf:ro
      - ./nginx/conf.d:/etc/nginx/conf.d:ro
      - ./nginx/ssl:/etc/nginx/ssl:ro
      - /etc/letsencrypt:/etc/letsencrypt:ro
      - nginx_logs:/var/log/nginx
      - /var/www/html:/var/www/html:ro
    networks:
      - mng_network
    restart: unless-stopped
    depends_on:
      - mngui
      - mnggateway
      - keycloak
      - gitlab
    healthcheck:
      test: ["CMD", "nginx", "-t"]
      interval: 30s
      timeout: 10s
      retries: 3

volumes:
  # ... mevcut volumes ...
  nginx_logs:
```

#### 2.6 Host Nginx'i Durdurma

**Adımlar:**
```bash
# Host Nginx'i durdur
sudo systemctl stop nginx

# Nginx'in otomatik başlamasını engelle (opsiyonel)
sudo systemctl disable nginx

# Port 80 ve 443'ün boş olduğunu kontrol et
sudo netstat -tlnp | grep -E ':(80|443)'
```

---

### Phase 3: Port Mapping'leri Kaldırma

#### 3.1 Internal Servisler

**Dosya:** `ApplicationResources/mng_common/docker-compose.yml`

**MongoDB:**
```yaml
# ÖNCE
mongo:
  ports:
    - "27017:27017"

# SONRA
mongo:
  # ports kısmı kaldırıldı
  networks:
    - mng_network
```

**PostgreSQL:**
```yaml
# ÖNCE
postgres:
  ports:
    - "5432:5432"

# SONRA
postgres:
  # ports kısmı kaldırıldı
  networks:
    - mng_network
```

**Redis:**
```yaml
# ÖNCE
redis:
  ports:
    - "6379:6379"

# SONRA
redis:
  # ports kısmı kaldırıldı
  networks:
    - mng_network
```

**RabbitMQ:**
```yaml
# ÖNCE
rabbitmq:
  ports:
    - "5672:5672"
    - "15672:15672"

# SONRA
rabbitmq:
  # ports kısmı kaldırıldı
  networks:
    - mng_network
```

**MinIO:**
```yaml
# ÖNCE
minio:
  ports:
    - "9090:9000"
    - "9091:9091"

# SONRA
minio:
  # ports kısmı kaldırıldı
  networks:
    - mng_network
```

#### 3.2 Application Servisler

**Dosya:** `ApplicationResources/mng_apps/docker-compose.yml` ve `docker-compose.production.yml`

**MngGateway:**
```yaml
# ÖNCE
mnggateway:
  ports:
    - "5000:5000"
    - "5443:443"

# SONRA
mnggateway:
  # ports kısmı kaldırıldı
  networks:
    - mng_common_mng_network
```

**MngKeeper:**
```yaml
# ÖNCE
mngkeeper:
  ports:
    - "5001:5001"

# SONRA
mngkeeper:
  # ports kısmı kaldırıldı
  networks:
    - mng_common_mng_network
```

**MngDataGateway:**
```yaml
# ÖNCE
mngdatagateway:
  ports:
    - "5010:5010"

# SONRA
mngdatagateway:
  # ports kısmı kaldırıldı
  networks:
    - mng_common_mng_network
```

**MngHub:**
```yaml
# ÖNCE
mnghub:
  ports:
    - "5020:5020"

# SONRA
mnghub:
  # ports kısmı kaldırıldı
  networks:
    - mng_common_mng_network
```

**MngUI:**
```yaml
# ÖNCE
mngui:
  ports:
    - "3000:80"

# SONRA
mngui:
  # ports kısmı kaldırıldı
  networks:
    - mng_common_mng_network
```

#### 3.3 Admin/UI Servisler

**Dosya:** `ApplicationResources/mng_common/docker-compose.yml`

**Keycloak:**
```yaml
# ÖNCE
keycloak:
  ports:
    - "8080:8080"

# SONRA
keycloak:
  # ports kısmı kaldırıldı
  networks:
    - mng_network
```

**GitLab:**
```yaml
# ÖNCE
gitlab:
  ports:
    - "8090:80"
    - "2222:22"

# SONRA
gitlab:
  # ports kısmı kaldırıldı (SSH port'u kalabilir - opsiyonel)
  # - "2222:22"  # SSH için gerekirse kalabilir
  networks:
    - mng_network
```

**Portainer:**
```yaml
# ÖNCE
portainer:
  ports:
    - "9000:9000"

# SONRA
portainer:
  # ports kısmı kaldırıldı
  networks:
    - mng_network
```

**Seq:**
```yaml
# ÖNCE
seq:
  ports:
    - "5341:80"

# SONRA
seq:
  # ports kısmı kaldırıldı
  networks:
    - mng_network
```

**Node-RED:**
```yaml
# ÖNCE
nodered:
  ports:
    - "1880:1880"

# SONRA
nodered:
  # ports kısmı kaldırıldı (opsiyonel - internal kullanım için)
  networks:
    - mng_network
```

---

### Phase 4: Test ve Doğrulama

#### 4.1 Nginx Container Testi

```bash
# Nginx container'ını başlat
cd ApplicationResources/mng_common
docker compose up -d nginx

# Nginx yapılandırmasını test et
docker exec nginx nginx -t

# Nginx loglarını kontrol et
docker logs nginx

# Container durumunu kontrol et
docker ps | grep nginx
```

#### 4.2 Servis Erişim Testleri

```bash
# MngUI erişim testi
curl -I https://app.monitrang.com

# MngGateway erişim testi
curl -I https://api.monitrang.com

# Keycloak erişim testi
curl -I https://auth.monitrang.com

# GitLab erişim testi
curl -I https://gitlab.monitrang.com
```

#### 4.3 Container Name Resolution Testi

```bash
# Nginx container'ından diğer container'lara erişim testi
docker exec nginx ping -c 2 mngui
docker exec nginx ping -c 2 mnggateway
docker exec nginx ping -c 2 keycloak
docker exec nginx ping -c 2 gitlab

# HTTP erişim testi (Nginx container'ından)
docker exec nginx wget -O- http://mngui:80/health
docker exec nginx wget -O- http://mnggateway:5000/health
docker exec nginx wget -O- http://keycloak:8080
```

#### 4.4 Port Kontrolü

```bash
# Host üzerinde sadece 80, 443 portlarının açık olduğunu kontrol et
sudo netstat -tlnp | grep LISTEN

# Beklenen portlar:
# - 22 (SSH)
# - 80 (Nginx)
# - 443 (Nginx)

# Diğer portların kapalı olduğunu doğrula
sudo netstat -tlnp | grep -E ':(27017|5432|6379|5672|5000|5001|5010|5020|3000|8080|8090|9000)'
# Hiçbir sonuç çıkmamalı
```

#### 4.5 Firewall Yapılandırması

```bash
# UFW durumunu kontrol et
sudo ufw status

# Sadece gerekli portları aç
sudo ufw allow 22/tcp    # SSH
sudo ufw allow 80/tcp    # HTTP
sudo ufw allow 443/tcp   # HTTPS

# UFW'yi etkinleştir (eğer değilse)
sudo ufw enable

# Durumu kontrol et
sudo ufw status verbose
```

#### 4.6 SSL Sertifikaları Doğrulama

```bash
# SSL sertifikalarının mount edildiğini kontrol et
docker exec nginx ls -la /etc/letsencrypt/live/monitrang.com/

# SSL sertifikası doğrulama
openssl s_client -connect app.monitrang.com:443 -servername app.monitrang.com
```

---

### Phase 5: Dokümantasyon ve Temizlik

#### 5.1 Dokümantasyon Güncelleme

- [ ] `docs/infrastructure/ports.md` güncelle
- [ ] `docs/infrastructure/nginx.md` güncelle
- [ ] `docs/infrastructure/port-management-plan.md` güncelle
- [ ] Migration rehberi oluştur

#### 5.2 Script'ler Oluşturma

**Port Kontrol Scripti:**
```bash
#!/bin/bash
# scripts/check-ports.sh
# Port kullanım kontrol scripti
```

**Network Connectivity Test Scripti:**
```bash
#!/bin/bash
# scripts/test-network-connectivity.sh
# Container name resolution test scripti
```

#### 5.3 Eski Yapılandırmaları Temizleme

```bash
# Host Nginx yapılandırmasını kaldır (opsiyonel)
sudo rm /etc/nginx/sites-enabled/monitrang
sudo rm /etc/nginx/sites-available/monitrang

# Host Nginx'i tamamen kaldır (opsiyonel)
sudo systemctl stop nginx
sudo systemctl disable nginx
```

---

## ⚠️ Önemli Notlar

### Dikkat Edilmesi Gerekenler

1. **Let's Encrypt Sertifikaları:**
   - Sertifikalar `/etc/letsencrypt/` dizininde
   - Volume mount ile container'a bağlanmalı
   - Read-only mount kullanılmalı

2. **Network Yapılandırması:**
   - Tüm servisler aynı Docker network'te olmalı
   - Network name: `mng_network` veya `mng_common_mng_network`
   - External network kullanılıyorsa önce oluşturulmalı

3. **Container Name'ler:**
   - Container name'ler Nginx yapılandırmasında kullanılacak
   - Container name'ler docker-compose.yml'de tanımlı olmalı
   - Container name'ler unique olmalı

4. **Port Mapping'ler:**
   - Sadece Nginx için port mapping kalacak (80, 443)
   - Diğer tüm servislerin port mapping'leri kaldırılacak
   - Internal erişim sadece Docker network üzerinden

5. **Rollback:**
   - Her adımda backup alınmalı
   - Rollback planı hazır olmalı
   - Test ortamında önce denenmeli

---

## 📊 Implementation Checklist

### Phase 1: Hazırlık
- [ ] Mevcut durum analizi yapıldı
- [ ] Backup alındı
- [ ] Rollback planı hazırlandı
- [ ] Test ortamı hazırlandı

### Phase 2: Nginx Containerization
- [ ] Nginx yapılandırma dosya yapısı oluşturuldu
- [ ] Nginx ana yapılandırma dosyası oluşturuldu
- [ ] SSL parametreleri yapılandırıldı
- [ ] MonitraNG domain yapılandırması oluşturuldu
- [ ] Docker Compose'a Nginx servisi eklendi
- [ ] Host Nginx durduruldu
- [ ] Nginx container test edildi

### Phase 3: Port Mapping'leri Kaldırma
- [ ] Internal servislerin port mapping'leri kaldırıldı
- [ ] Application servislerin port mapping'leri kaldırıldı
- [ ] Admin/UI servislerin port mapping'leri kaldırıldı
- [ ] Network yapılandırması doğrulandı

### Phase 4: Test ve Doğrulama
- [ ] Nginx container test edildi
- [ ] Servis erişim testleri yapıldı
- [ ] Container name resolution test edildi
- [ ] Port kontrolü yapıldı
- [ ] Firewall yapılandırıldı
- [ ] SSL sertifikaları doğrulandı

### Phase 5: Dokümantasyon
- [ ] Dokümantasyon güncellendi
- [ ] Script'ler oluşturuldu
- [ ] Eski yapılandırmalar temizlendi

---

**Son Güncelleme:** 4 Ocak 2026  
**Durum:** 📋 Implementation Planı Hazır  
**Yaklaşım:** Phase 2 - Nginx Containerization

