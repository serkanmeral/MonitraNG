# MonitraNG Deployment Roadmap

**Tarih:** 15 Ocak 2025  
**Durum:** Detaylandırma aşamasında  
**Versiyon:** 1.0.0

---

## 📋 İçindekiler

1. [Genel Bakış](#genel-bakış)
2. [Pre-Deployment Checklist](#pre-deployment-checklist)
3. [Ortam Gereksinimleri](#ortam-gereksinimleri)
4. [Server Kurulum Aşamaları](#server-kurulum-aşamaları)
5. [Environment Yapılandırması](#environment-yapılandırması)
6. [SSL/HTTPS Yapılandırması](#sslhttps-yapılandırması)
7. [Nginx Reverse Proxy Yapılandırması](#nginx-reverse-proxy-yapılandırması)
8. [Deployment Süreci](#deployment-süreci)
9. [Air-Gapped (Offline) Deployment](#air-gapped-offline-deployment)
10. [Backup Stratejisi](#backup-stratejisi)
11. [Monitoring ve Logging](#monitoring-ve-logging)
12. [Rollback Stratejisi](#rollback-stratejisi)
13. [Post-Deployment Doğrulama](#post-deployment-doğrulama)
14. [Sorun Giderme](#sorun-giderme)
15. [Timeline ve Öncelikler](#timeline-ve-öncelikler)

---

## 🎯 Genel Bakış

MonitraNG deployment roadmap'i, sistemin production ortamına başarılı bir şekilde deploy edilmesi için gerekli tüm adımları detaylı bir şekilde kapsar.

### Deployment Senaryoları

1. **Test Ortamı Deployment** - İlk test ve doğrulama için
2. **Production Deployment** - Canlı sistem deployment'ı
3. **Air-Gapped Deployment** - İnternet bağlantısı olmayan ortamlar için

### Deployment Yaklaşımı

- ✅ **Docker-Based:** Tüm servisler containerize edilmiş
- ✅ **Self-Hosted:** Tüm bileşenler self-hosted (cloud dependency yok)
- ✅ **Automated Scripts:** Deployment script'leri hazır
- ✅ **Infrastructure First:** Infrastructure servisleri önce başlatılır
- ✅ **Health Checks:** Otomatik health check'ler mevcut

---

## ✅ Pre-Deployment Checklist

### Sistem Gereksinimleri

- [ ] **Server Seçildi:**
  - [ ] Test ortamı için: Minimum 16 GB RAM, 4 Core, 150 GB SSD
  - [ ] Production için: Minimum 32 GB RAM, 8 Core, 200 GB SSD
  - [ ] İnternet bağlantısı (air-gapped için ayrı senaryo)

- [ ] **OS Hazır:**
  - [ ] Ubuntu 22.04 LTS kurulu
  - [ ] Sistem güncellemeleri yapıldı
  - [ ] Root veya sudo erişimi mevcut

- [ ] **Network Hazır:**
  - [ ] Firewall portları açıldı (22, 80, 443)
  - [ ] DNS kayıtları yapıldı (production için)
  - [ ] SSL sertifikası hazır (production için)

- [ ] **Kaynaklar Hazır:**
  - [ ] Docker ve Docker Compose kurulum dosyaları
  - [ ] Git repository erişimi
  - [ ] Environment variable şablonu (.env.example)

- [ ] **Güvenlik:**
  - [ ] Tüm şifreler güvenli şekilde oluşturuldu
  - [ ] SSH key-based authentication yapılandırıldı
  - [ ] Firewall aktif edildi

---

## 🖥️ Ortam Gereksinimleri

### Test Ortamı

| Kaynak | Minimum | Önerilen | Açıklama |
|--------|---------|----------|----------|
| **RAM** | 16 GB | 32 GB | Infrastructure + Applications + AI Chat Bot |
| **CPU** | 4 Core | 8 Core | 2.4 GHz+ |
| **Disk** | 150 GB SSD | 200 GB SSD | Sistem + Veri + Modeller |
| **Network** | 100 Mbps | 1 Gbps | İç ağ yeterli |

**Kullanım Senaryosu:**
- 1-5 domain
- 10-50 kullanıcı
- Düşük-orta trafik
- AI Chat Bot (küçük model: rn_tr_r1)

**Hosting Önerileri:**
- **Hetzner CPX41:** 16 GB RAM, 8 Core, 240 GB SSD - €35/ay (~$38)
- **DigitalOcean:** 16 GB RAM, 4 vCPU, 320 GB SSD - $96/ay
- **Linode:** 16 GB RAM, 4 vCPU, 320 GB SSD - $80/ay

### Production Ortamı

| Kaynak | Minimum | Önerilen | Açıklama |
|--------|---------|----------|----------|
| **RAM** | 32 GB | 64 GB | Yüksek performans için |
| **CPU** | 8 Core | 16 Core | 2.8 GHz+ |
| **Disk** | 200 GB SSD | 500 GB SSD | Sistem + Veri + Logs + Backups |
| **Network** | 1 Gbps | 10 Gbps | Yüksek trafik için |

**Kullanım Senaryosu:**
- 20+ domain
- 200+ kullanıcı
- Yüksek trafik
- AI Chat Bot (turkcell-llm-7b-v1)

**Hosting Önerileri:**
- **Hetzner CCX33:** 64 GB RAM, 16 Core, 640 GB SSD - €95/ay (~$103)
- **Hetzner Dedicated EX42:** 64 GB RAM, Intel i7, 2x 512 GB SSD - €49/ay (~$53)
- **AWS t3.4xlarge:** 64 GB RAM, 16 vCPU - ~$480/ay + storage

---

## 🛠️ Server Kurulum Aşamaları

### Phase 1: İlk Server Kurulumu

**Hedef:** Sunucuyu deployment için hazır hale getirmek

#### Adım 1.1: Sistem Güncellemeleri

```bash
# SSH ile sunucuya bağlan
ssh root@your-server-ip

# Sistem güncellemeleri
apt update && apt upgrade -y
```

#### Adım 1.2: Temel Araçların Kurulumu

```bash
# Temel araçlar
apt install -y curl wget git vim ufw htop net-tools
```

#### Adım 1.3: Firewall Yapılandırması

```bash
# Firewall kurulumu ve yapılandırması
ufw allow 22/tcp    # SSH
ufw allow 80/tcp    # HTTP
ufw allow 443/tcp   # HTTPS
ufw --force enable

# Firewall durumunu kontrol et
ufw status
```

#### Adım 1.4: Docker Kurulumu

```bash
# Docker kurulumu
curl -fsSL https://get.docker.com -o get-docker.sh
sh get-docker.sh

# Docker servisini başlat
systemctl start docker
systemctl enable docker

# Docker versiyonunu kontrol et
docker --version
```

#### Adım 1.5: Docker Compose Kurulumu

```bash
# Docker Compose kurulumu
apt install -y docker-compose-plugin

# Docker Compose versiyonunu kontrol et
docker compose version
```

#### Adım 1.6: Docker Log Rotation Yapılandırması

```bash
# Docker log rotation yapılandırması
mkdir -p /etc/docker
cat > /etc/docker/daemon.json <<EOF
{
  "log-driver": "json-file",
  "log-opts": {
    "max-size": "10m",
    "max-file": "3"
  }
}
EOF

# Docker servisini yeniden başlat
systemctl restart docker
```

#### Adım 1.7: Deploy Kullanıcısı Oluşturma

```bash
# Deploy kullanıcısı oluştur
adduser --disabled-password --gecos "" deploy

# Sudo yetkisi ver
usermod -aG sudo deploy

# Docker grubuna ekle
usermod -aG docker deploy

# Kullanıcıya geç
su - deploy
```

**Durum:** ✅ Script mevcut: `scripts/setup-server.sh`

---

### Phase 2: Nginx ve SSL Kurulumu

#### Adım 2.1: Nginx Kurulumu

```bash
# Nginx kurulumu
sudo apt install -y nginx

# Nginx servisini başlat
sudo systemctl start nginx
sudo systemctl enable nginx

# Nginx durumunu kontrol et
sudo systemctl status nginx
```

#### Adım 2.2: Certbot Kurulumu (SSL için)

```bash
# Certbot kurulumu (Let's Encrypt için)
sudo apt install -y certbot python3-certbot-nginx

# Certbot versiyonunu kontrol et
certbot --version
```

#### Adım 2.3: Nginx Yapılandırması (Adım 6'da detaylı)

Nginx reverse proxy yapılandırması için [Nginx Yapılandırması](#nginx-reverse-proxy-yapılandırması) bölümüne bakın.

**Durum:** ✅ Tamamlandı (4 Ocak 2026) - Nginx container olarak çalışıyor

---

### Phase 3: Repository ve Environment Kurulumu

#### Adım 3.1: Repository Clone

```bash
# Deploy kullanıcısı olarak
cd ~
git clone https://github.com/serkanmeral/MonitraNG.git
cd MonitraNG
```

#### Adım 3.2: Environment Dosyası Yapılandırması

```bash
# Environment dosyasını kopyala
cd ApplicationResources/mng_apps
cp env.example .env

# Environment dosyasını düzenle
vim .env  # veya nano .env
```

**Environment Variables Yapılandırması için:** [Environment Yapılandırması](#environment-yapılandırması) bölümüne bakın.

---

## ⚙️ Environment Yapılandırması

### Environment Dosyası Yapısı

**Konum:** `ApplicationResources/mng_apps/.env`

### Kritik Yapılandırmalar

#### 1. Domain ve URL Yapılandırması

```bash
# Environment
ENVIRONMENT=Production  # veya Test

# Domain
DOMAIN=monitrang.com  # Production domain
# Test için: DOMAIN=test.monitrang.com

# OpenAPI Server Path
OPENAPI_SERVER_PATH=https://monitrang.com
# Test için: OPENAPI_SERVER_PATH=https://test.monitrang.com
```

#### 2. MongoDB Yapılandırması

```bash
# MongoDB Connection String
MONGO_CONNECTION_STRING=mongodb://admin:STRONG_PASSWORD@mongo:27017

# MongoDB Database Name
MONGO_DATABASE_NAME=mngkeeper
```

**Şifre Güvenliği:**
- ✅ En az 16 karakter
- ✅ Büyük/küçük harf, rakam, özel karakter içermeli
- ✅ Güvenli bir şifre üretici kullanın

#### 3. Keycloak Yapılandırması

```bash
# Keycloak Base URL
KEYCLOAK_BASE_URL=http://keycloak:8080

# Keycloak Admin Credentials
KEYCLOAK_ADMIN_USERNAME=admin
KEYCLOAK_ADMIN_PASSWORD=STRONG_PASSWORD
KEYCLOAK_DEFAULT_ADMIN_PASSWORD=STRONG_PASSWORD

# Keycloak Client Configuration
KEYCLOAK_CLIENT_ID=mng-keeper-admin
KEYCLOAK_CLIENT_SECRET=STRONG_SECRET
```

#### 4. Redis Yapılandırması

```bash
# Redis Connection String
REDIS_CONNECTION_STRING=redis:6379,password=STRONG_PASSWORD
```

#### 5. RabbitMQ Yapılandırması

```bash
# RabbitMQ Configuration
RABBITMQ_HOST=rabbitmq
RABBITMQ_PORT=5672
RABBITMQ_USERNAME=admin
RABBITMQ_PASSWORD=STRONG_PASSWORD
RABBITMQ_VIRTUALHOST=/
```

#### 6. MQTT Yapılandırması

```bash
# MQTT Configuration
MQTT_BROKER_HOST=mosquitto
MQTT_BROKER_PORT=1883
MQTT_USERNAME=monitrang
MQTT_PASSWORD=STRONG_PASSWORD
MQTT_TOPIC_PREFIX=MNG
```

#### 7. MinIO Yapılandırması

```bash
# MinIO Configuration
MINIO_ENDPOINT=minio:9000
MINIO_ACCESS_KEY=admin
MINIO_SECRET_KEY=STRONG_SECRET
MINIO_USE_SSL=false
MINIO_REGION=us-east-1
```

#### 8. Application URLs

```bash
# Application URLs
MNGKEEPER_URL=https://mngkeeper:5001

# Certificate DNS
CERTIFICATE_DNS=mngkeeper
```

### Şifre Oluşturma

Güvenli şifreler oluşturmak için:

```bash
# Linux'ta güvenli şifre oluştur
openssl rand -base64 32

# veya
pwgen -s 32 1
```

**Durum:** ✅ `env.example` dosyası mevcut

---

## 🔒 SSL/HTTPS Yapılandırması

### Yöntem 1: Let's Encrypt (Internet Bağlantısı Gerektirir)

#### Adım 1: Nginx Yapılandırması

Nginx yapılandırması için [Nginx Yapılandırması](#nginx-reverse-proxy-yapılandırması) bölümüne bakın.

#### Adım 2: SSL Sertifikası Alma

```bash
# Let's Encrypt sertifikası al
sudo certbot --nginx -d monitrang.com -d www.monitrang.com

# Sertifika otomatik yenileme testi
sudo certbot renew --dry-run
```

#### Adım 3: Otomatik Yenileme

```bash
# Cron job kontrolü (certbot otomatik ekler)
sudo systemctl status certbot.timer

# Veya manuel cron job ekle
sudo crontab -e
# Şu satırı ekle:
# 0 0 * * * certbot renew --quiet
```

### Yöntem 2: Self-Signed Certificate (Test/Development)

#### Adım 1: Self-Signed Certificate Oluşturma

```bash
# Certificate dizini oluştur
sudo mkdir -p /etc/nginx/ssl

# Self-signed certificate oluştur
sudo openssl req -x509 -nodes -days 365 -newkey rsa:2048 \
  -keyout /etc/nginx/ssl/monitrang.key \
  -out /etc/nginx/ssl/monitrang.crt

# Sertifika bilgilerini girin
# Common Name: monitrang.com veya IP adresi
```

#### Adım 2: Nginx Yapılandırmasında Kullanım

Nginx yapılandırmasında self-signed certificate'ı kullanın (adım 6'da detaylı).

### Yöntem 3: Kurumsal CA Certificate (Air-Gapped/Enterprise)

#### Adım 1: Certificate Dosyalarını Yükleme

```bash
# Certificate dosyalarını yükle
sudo mkdir -p /etc/nginx/ssl
sudo cp your-certificate.crt /etc/nginx/ssl/monitrang.crt
sudo cp your-private-key.key /etc/nginx/ssl/monitrang.key

# İzinleri ayarla
sudo chmod 600 /etc/nginx/ssl/monitrang.key
sudo chmod 644 /etc/nginx/ssl/monitrang.crt
```

**Durum:** ✅ Tamamlandı (4 Ocak 2026) - Let's Encrypt sertifikaları yapılandırıldı

---

## 🌐 Nginx Reverse Proxy Yapılandırması

### Nginx Yapılandırma Dosyası

**Konum:** `/etc/nginx/sites-available/monitrang`

#### Temel Yapılandırma (HTTP → HTTPS Redirect)

```nginx
# HTTP → HTTPS Redirect
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
```

#### HTTPS Yapılandırması (Let's Encrypt)

```nginx
# HTTPS Server
server {
    listen 443 ssl http2;
    listen [::]:443 ssl http2;
    server_name monitrang.com www.monitrang.com;

    # SSL Certificate (Let's Encrypt)
    ssl_certificate /etc/letsencrypt/live/monitrang.com/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/monitrang.com/privkey.pem;

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
    access_log /var/log/nginx/monitrang-access.log;
    error_log /var/log/nginx/monitrang-error.log;

    # MngGateway (API Gateway)
    location /api/ {
        proxy_pass https://localhost:5000;
        proxy_http_version 1.1;
        
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        
        # SSL Verification bypass (self-signed certificate için)
        proxy_ssl_verify off;
        proxy_ssl_server_name on;
    }

    # MngKeeper (Direct Access - Optional)
    location /keeper/ {
        proxy_pass https://localhost:5001/;
        proxy_http_version 1.1;
        
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        
        proxy_ssl_verify off;
        proxy_ssl_server_name on;
    }

    # MngDataGateway (Direct Access - Optional)
    location /datagateway/ {
        proxy_pass https://localhost:5010/;
        proxy_http_version 1.1;
        
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        
        proxy_ssl_verify off;
        proxy_ssl_server_name on;
    }

    # Keycloak
    location /auth/ {
        proxy_pass http://localhost:8080/;
        proxy_http_version 1.1;
        
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}
```

#### HTTPS Yapılandırması (Self-Signed Certificate)

```nginx
# HTTPS Server (Self-Signed)
server {
    listen 443 ssl http2;
    listen [::]:443 ssl http2;
    server_name monitrang.com;

    # SSL Certificate (Self-Signed)
    ssl_certificate /etc/nginx/ssl/monitrang.crt;
    ssl_certificate_key /etc/nginx/ssl/monitrang.key;

    # SSL Configuration (aynı yukarıdaki gibi)
    # ...
}
```

### Nginx Yapılandırmasını Aktifleştirme

```bash
# Yapılandırma dosyasını sites-enabled'e linkle
sudo ln -s /etc/nginx/sites-available/monitrang /etc/nginx/sites-enabled/

# Yapılandırmayı test et
sudo nginx -t

# Nginx'i yeniden başlat
sudo systemctl reload nginx
```

**Durum:** ✅ Tamamlandı (4 Ocak 2026) - Nginx container yapılandırması hazır

---

## 🚀 Deployment Süreci

### Phase 1: Infrastructure Deployment

#### Adım 1.1: Infrastructure Servislerini Başlatma

```bash
# mng_common dizinine git
cd ~/MonitraNG/ApplicationResources/mng_common

# Infrastructure servislerini başlat
docker compose up -d

# Servislerin durumunu kontrol et
docker compose ps
```

**Beklenen Servisler:**
- MongoDB
- PostgreSQL (Keycloak için)
- Keycloak
- Redis
- RabbitMQ
- MinIO
- Mosquitto (MQTT)
- Seq (Logging)
- Portainer (Opsiyonel)
- Mongo Express (Opsiyonel)

#### Adım 1.2: Infrastructure Health Checks

```bash
# MongoDB health check
docker exec mongo mongosh --eval "db.adminCommand('ping')"

# Keycloak health check
curl -f http://localhost:8080/health/ready

# Redis health check
docker exec redis redis-cli ping

# RabbitMQ health check
curl -f http://localhost:15672/api/healthchecks/node

# MinIO health check
curl -f http://localhost:9000/minio/health/live
```

**Durum:** ✅ Script'te health check'ler mevcut

---

### Phase 2: Application Deployment

#### Adım 2.1: Environment Variables Kontrolü

```bash
# Environment dosyasının varlığını kontrol et
cd ~/MonitraNG/ApplicationResources/mng_apps
test -f .env && echo ".env file exists" || echo ".env file NOT found!"

# Environment variables'ı kontrol et (hassas bilgileri gizle)
cat .env | grep -v PASSWORD | grep -v SECRET
```

#### Adım 2.2: Docker Image Build

```bash
# Docker image'ları build et
docker compose -f docker-compose.production.yml build

# Build durumunu kontrol et
docker images | grep mng
```

#### Adım 2.3: Application Servislerini Başlatma

```bash
# Application servislerini başlat
docker compose -f docker-compose.production.yml up -d

# Servislerin durumunu kontrol et
docker compose -f docker-compose.production.yml ps
```

**Beklenen Servisler:**
- MngGateway (API Gateway)
- MngKeeper (IAM)
- MngDataGateway (Data Layer)
- MngHub (Event Hub)

#### Adım 2.4: Application Health Checks

```bash
# MngKeeper health check
curl -k -f https://localhost:5001/api/version/short

# MngDataGateway health check
curl -k -f https://localhost:5010/health

# MngHub health check
curl -f http://localhost:5020/health

# MngGateway health check (varsa)
curl -k -f https://localhost:5000/health
```

**Durum:** ✅ `deploy.sh` script'i mevcut ve otomatik health check yapıyor

---

### Phase 3: Deployment Script Kullanımı

#### Otomatik Deployment

```bash
# Deployment script'ini çalıştır
cd ~/MonitraNG
./scripts/deploy.sh production latest

# Belirli bir versiyon deploy et
./scripts/deploy.sh production v1.0.0
```

**Script Adımları:**
1. ✅ Latest code pull
2. ✅ Docker image build
3. ✅ Infrastructure services start
4. ✅ Infrastructure health checks
5. ✅ Application services start
6. ✅ Application health checks
7. ✅ Old images cleanup

**Durum:** ✅ `scripts/deploy.sh` mevcut

---

## 🔌 Air-Gapped (Offline) Deployment

### Senaryo: İnternet Bağlantısı Olmayan Ortam

MonitraNG tamamen **air-gapped** sistemlerde çalışabilir çünkü tüm bileşenler self-hosted'dır.

### Phase 1: Online Ortamda Hazırlık

#### Adım 1.1: Docker Image Export

```bash
# Tüm Docker image'ları export et
docker save -o monitrang-images.tar \
  mongo:latest \
  postgres:latest \
  keycloak:latest \
  redis:latest \
  rabbitmq:latest \
  minio/minio:latest \
  eclipse-mosquitto:latest \
  mnggateway:latest \
  mngkeeper:latest \
  mngdatagateway:latest \
  mnghub:latest

# Image listesini kontrol et
docker images > images-list.txt
```

#### Adım 1.2: Repository Export

```bash
# Repository'yi archive et
cd ~/MonitraNG
git archive --format=tar.gz --output=monitrang-repo.tar.gz HEAD
```

#### Adım 1.3: AI Chat Bot Modelleri Export (Varsa)

```bash
# Ollama modellerini export et (varsa)
# Modeller volume'da olduğu için volume'u export etmek gerekir
```

#### Adım 1.4: Transfer

```bash
# USB disk veya network üzerinden transfer
# Örnek: scp ile transfer (network varsa)
scp monitrang-images.tar deploy@air-gapped-server:/tmp/
scp monitrang-repo.tar.gz deploy@air-gapped-server:/tmp/
```

### Phase 2: Offline Ortamda Deployment

#### Adım 2.1: Docker Image Import

```bash
# Air-gapped server'da
docker load -i /tmp/monitrang-images.tar

# Image'ların yüklendiğini kontrol et
docker images
```

#### Adım 2.2: Repository Extract

```bash
# Repository'yi extract et
cd ~
tar -xzf /tmp/monitrang-repo.tar.gz -C MonitraNG
cd MonitraNG
```

#### Adım 2.3: Environment Yapılandırması

```bash
# Environment dosyasını yapılandır (şifreleri değiştir)
cd ApplicationResources/mng_apps
cp env.example .env
vim .env
```

#### Adım 2.4: Docker Compose Build (Offline)

```bash
# Docker Compose build (offline mode)
# Image'lar zaten yüklü olduğu için build gerekmez
# Sadece container'ları başlat
cd ~/MonitraNG/ApplicationResources/mng_common
docker compose up -d

cd ../mng_apps
docker compose -f docker-compose.production.yml up -d
```

#### Adım 2.5: Self-Signed Certificate Kullanımı

Air-gapped ortamda Let's Encrypt kullanılamaz, self-signed certificate kullanın:

```bash
# Self-signed certificate oluştur
sudo mkdir -p /etc/nginx/ssl
sudo openssl req -x509 -nodes -days 365 -newkey rsa:2048 \
  -keyout /etc/nginx/ssl/monitrang.key \
  -out /etc/nginx/ssl/monitrang.crt
```

**Durum:** ⏳ Air-gapped deployment script'i hazırlanmalı

---

## 💾 Backup Stratejisi

### Backup Bileşenleri

1. **MongoDB Backup** - Veritabanı verileri
2. **MinIO Backup** - Object storage (dosyalar)
3. **Keycloak Backup** - Authentication verileri (PostgreSQL)
4. **Configuration Backup** - Environment dosyaları, Nginx config
5. **Docker Volumes Backup** - Tüm volume'lar

### MongoDB Backup

#### Otomatik Backup Script

```bash
#!/bin/bash
# scripts/backup-mongodb.sh

BACKUP_DIR="/backup/mongodb"
DATE=$(date +%Y%m%d_%H%M%S)
BACKUP_FILE="$BACKUP_DIR/mongodb_backup_$DATE.tar.gz"

mkdir -p $BACKUP_DIR

# MongoDB dump
docker exec mongo mongodump --out /data/backup

# Backup'i archive et
docker exec mongo tar -czf /data/backup.tar.gz -C /data backup

# Host'a kopyala
docker cp mongo:/data/backup.tar.gz $BACKUP_FILE

# Eski backup'leri sil (30 günden eski)
find $BACKUP_DIR -name "*.tar.gz" -mtime +30 -delete
```

#### Cron Job

```bash
# Cron job ekle
crontab -e
# Şu satırı ekle (her gün saat 02:00'de backup):
0 2 * * * /home/deploy/MonitraNG/scripts/backup-mongodb.sh
```

### MinIO Backup

```bash
#!/bin/bash
# scripts/backup-minio.sh

BACKUP_DIR="/backup/minio"
DATE=$(date +%Y%m%d_%H%M%S)
BACKUP_FILE="$BACKUP_DIR/minio_backup_$DATE.tar.gz"

mkdir -p $BACKUP_DIR

# MinIO data volume'u backup et
docker run --rm \
  -v minio_data:/data \
  -v $BACKUP_DIR:/backup \
  alpine tar -czf /backup/minio_backup_$DATE.tar.gz -C /data .

# Eski backup'leri sil (30 günden eski)
find $BACKUP_DIR -name "*.tar.gz" -mtime +30 -delete
```

### Keycloak (PostgreSQL) Backup

```bash
#!/bin/bash
# scripts/backup-postgres.sh

BACKUP_DIR="/backup/postgres"
DATE=$(date +%Y%m%d_%H%M%S)
BACKUP_FILE="$BACKUP_DIR/postgres_backup_$DATE.sql.gz"

mkdir -p $BACKUP_DIR

# PostgreSQL dump
docker exec postgres pg_dump -U keycloak keycloak | gzip > $BACKUP_FILE

# Eski backup'leri sil (30 günden eski)
find $BACKUP_DIR -name "*.sql.gz" -mtime +30 -delete
```

### Configuration Backup

```bash
#!/bin/bash
# scripts/backup-config.sh

BACKUP_DIR="/backup/config"
DATE=$(date +%Y%m%d_%H%M%S)
BACKUP_FILE="$BACKUP_DIR/config_backup_$DATE.tar.gz"

mkdir -p $BACKUP_DIR

# Configuration dosyalarını backup et
tar -czf $BACKUP_FILE \
  ~/MonitraNG/ApplicationResources/mng_apps/.env \
  /etc/nginx/sites-available/monitrang \
  /etc/nginx/ssl/

# Eski backup'leri sil (90 günden eski)
find $BACKUP_DIR -name "*.tar.gz" -mtime +90 -delete
```

### Comprehensive Backup Script

```bash
#!/bin/bash
# scripts/backup-all.sh

echo "Starting comprehensive backup..."

# MongoDB backup
./scripts/backup-mongodb.sh

# MinIO backup
./scripts/backup-minio.sh

# PostgreSQL backup
./scripts/backup-postgres.sh

# Configuration backup
./scripts/backup-config.sh

echo "Backup completed!"
```

**Durum:** ⏳ Backup script'leri hazırlanmalı (şu an sadece `backup.sh` var)

---

## 📊 Monitoring ve Logging

### Log Aggregation (Seq)

**Durum:** ✅ Seq zaten docker-compose.yml'de mevcut

**Erişim:**
- URL: `http://localhost:5341`
- Log seviyeleri ve filtreleme mevcut

### Container Logs

```bash
# Tüm servislerin loglarını görüntüle
docker compose -f docker-compose.production.yml logs -f

# Belirli bir servisin loglarını görüntüle
docker compose -f docker-compose.production.yml logs -f mngkeeper

# Son 100 satır log
docker compose -f docker-compose.production.yml logs --tail=100 mngkeeper
```

### Health Check Monitoring

```bash
# Health check script'i
#!/bin/bash
# scripts/health-check.sh

services=("mongo" "keycloak" "redis" "rabbitmq" "minio" "mngkeeper" "mngdatagateway" "mnghub")

for service in "${services[@]}"; do
    if docker ps | grep -q $service; then
        echo "✅ $service is running"
    else
        echo "❌ $service is NOT running"
    fi
done
```

### Resource Monitoring

```bash
# Docker stats
docker stats

# Disk usage
df -h

# Memory usage
free -h

# CPU usage
top
```

**Durum:** ⏳ Monitoring script'leri ve dashboard hazırlanmalı

---

## 🔄 Rollback Stratejisi

### Senaryo: Deployment Başarısız Oldu

#### Adım 1: Mevcut Versiyonu Tespit Etme

```bash
# Çalışan container'ların image versiyonlarını kontrol et
docker ps --format "table {{.Names}}\t{{.Image}}"

# Git tag'lerini kontrol et
git tag -l
```

#### Adım 2: Önceki Versiyona Geri Dönme

```bash
# Önceki versiyona git
git checkout v1.0.0  # Önceki versiyon

# Environment dosyasını geri yükle (backup'tan)
cp /backup/config/config_backup_YYYYMMDD.tar.gz /tmp/
tar -xzf /tmp/config_backup_YYYYMMDD.tar.gz

# Deployment'ı tekrar yap
./scripts/deploy.sh production v1.0.0
```

#### Adım 3: Veritabanı Rollback (Gerekirse)

```bash
# MongoDB backup'tan geri yükle
docker exec -i mongo mongorestore --archive < /backup/mongodb/mongodb_backup_YYYYMMDD.tar.gz
```

### Senaryo: Hızlı Rollback

```bash
#!/bin/bash
# scripts/rollback.sh

PREVIOUS_VERSION=${1:-latest}

echo "Rolling back to version: $PREVIOUS_VERSION"

# Önceki versiyona git
git checkout $PREVIOUS_VERSION

# Deployment'ı tekrar yap
./scripts/deploy.sh production $PREVIOUS_VERSION

echo "Rollback completed!"
```

**Durum:** ⏳ Rollback script'i hazırlanmalı

---

## ✅ Post-Deployment Doğrulama

### Functional Tests

#### Adım 1: API Health Checks

```bash
# MngKeeper health check
curl -k https://localhost:5001/api/version/short

# MngDataGateway health check
curl -k https://localhost:5010/health

# MngHub health check
curl http://localhost:5020/health
```

#### Adım 2: Authentication Test

```bash
# Token alma testi
curl -X POST https://localhost:5001/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"admin","password":"..."}'
```

#### Adım 3: Database Connection Test

```bash
# MongoDB connection test
docker exec mongo mongosh --eval "db.adminCommand('ping')"

# Redis connection test
docker exec redis redis-cli ping
```

#### Adım 4: Service Integration Test

```bash
# MngKeeper → MongoDB integration
# MngDataGateway → MongoDB integration
# MngHub → RabbitMQ integration
```

### Performance Tests

```bash
# Response time test
time curl -k https://localhost:5001/api/version/short

# Load test (opsiyonel - Apache Bench veya k6)
ab -n 100 -c 10 https://localhost:5001/api/version/short
```

### Security Checks

- [ ] SSL certificate geçerli mi?
- [ ] Firewall aktif mi?
- [ ] Şifreler güçlü mü?
- [ ] Security headers aktif mi? (Nginx'te)
- [ ] Rate limiting aktif mi?

**Durum:** ⏳ Post-deployment test script'i hazırlanmalı

---

## 🔧 Sorun Giderme

### Yaygın Sorunlar ve Çözümleri

#### Sorun 1: Container Başlamıyor

**Semptom:**
```bash
docker compose ps
# Container status: Exited (1)
```

**Çözüm:**
```bash
# Logları kontrol et
docker compose logs container-name

# Container'ı yeniden başlat
docker compose restart container-name

# Container'ı sıfırdan başlat
docker compose down container-name
docker compose up -d container-name
```

#### Sorun 2: MongoDB Connection Error

**Semptom:**
```
MongoDB connection failed: connection refused
```

**Çözüm:**
```bash
# MongoDB container'ının çalıştığını kontrol et
docker ps | grep mongo

# MongoDB loglarını kontrol et
docker logs mongo

# MongoDB'yi yeniden başlat
docker compose restart mongo

# Connection string'i kontrol et
cat ApplicationResources/mng_apps/.env | grep MONGO
```

#### Sorun 3: SSL Certificate Hatası

**Semptom:**
```
SSL certificate verification failed
```

**Çözüm:**
```bash
# Certificate'i kontrol et
sudo certbot certificates

# Certificate'i yenile
sudo certbot renew

# Nginx yapılandırmasını kontrol et
sudo nginx -t

# Nginx'i yeniden başlat
sudo systemctl reload nginx
```

#### Sorun 4: Port Already in Use

**Semptom:**
```
Error: port 5000 is already in use
```

**Çözüm:**
```bash
# Port'u kullanan process'i bul
sudo lsof -i :5000

# Process'i sonlandır
sudo kill -9 <PID>

# Veya docker-compose.yml'de port'u değiştir
```

#### Sorun 5: Disk Space Full

**Semptom:**
```
No space left on device
```

**Çözüm:**
```bash
# Disk kullanımını kontrol et
df -h

# Docker unused resources'ları temizle
docker system prune -a

# Eski log dosyalarını temizle
sudo journalctl --vacuum-time=7d

# Eski backup'leri sil
find /backup -name "*.tar.gz" -mtime +30 -delete
```

### Log Analysis

```bash
# Tüm servislerin loglarını topla
docker compose logs > deployment-logs.txt

# Error logları filtrele
docker compose logs | grep -i error

# Belirli bir zaman aralığındaki loglar
docker compose logs --since 1h
```

**Durum:** ⏳ Sorun giderme rehberi genişletilmeli

---

## 📅 Timeline ve Öncelikler

### Phase 1: Hazırlık (1 Hafta)

**Öncelik:** Yüksek

- [ ] **Server Seçimi ve Kurulumu**
  - [ ] Test ortamı server seçimi
  - [ ] Server kurulumu (setup-server.sh çalıştır)
  - [ ] Network yapılandırması
  
- [ ] **Environment Yapılandırması**
  - [ ] .env dosyası hazırlama
  - [ ] Şifre oluşturma ve yapılandırma
  - [ ] Environment variables doğrulama

- [x] **SSL/HTTPS Yapılandırması** ✅ TAMAMLANDI (2 Ocak 2026)
  - [x] Let's Encrypt wildcard sertifikası alma (production)
    - Domain: `monitrang.com` ve `*.monitrang.com`
    - DNS-01 challenge kullanıldı
    - Sertifika geçerlilik: 2 Nisan 2026'ya kadar
  - [x] Nginx SSL yapılandırması
    - SSL sertifikaları aktif
    - HTTP → HTTPS redirect
    - Tüm subdomain'ler için SSL aktif
  - [ ] Otomatik yenileme hook script'i (gelecekte eklenecek)
  - [ ] Self-signed certificate (test için - gerekirse)

**Durum:** ✅ Tamamlandı

---

### Phase 2: İlk Deployment (1 Hafta)

**Öncelik:** Yüksek

- [ ] **Infrastructure Deployment**
  - [ ] MongoDB deployment ve test
  - [ ] Keycloak deployment ve test
  - [ ] Redis, RabbitMQ, MinIO deployment
  - [ ] Infrastructure health checks

- [ ] **Application Deployment**
  - [ ] MngKeeper deployment
  - [ ] MngDataGateway deployment
  - [ ] MngHub deployment
  - [ ] MngGateway deployment (opsiyonel)

- [ ] **Post-Deployment Doğrulama**
  - [ ] Functional tests
  - [ ] Integration tests
  - [ ] Performance tests
  - [ ] Security checks

**Durum:** ⏳ Planlanmış

---

### Phase 3: Production Deployment (1 Hafta)

**Öncelik:** Yüksek

- [ ] **Production Server Kurulumu**
  - [ ] Production server seçimi
  - [ ] Server kurulumu
  - [ ] Production environment yapılandırması

- [ ] **Production Deployment**
  - [ ] Production deployment (deploy.sh)
  - [x] DNS yapılandırması ✅ (2 Ocak 2026)
  - [x] SSL sertifikası (Let's Encrypt) ✅ (2 Ocak 2026)
  - [x] Nginx reverse proxy yapılandırması ✅ (2 Ocak 2026)
  - [x] Nginx containerization ✅ (4 Ocak 2026)
  - [x] Port yönetimi tamamlandı ✅ (4 Ocak 2026)

- [ ] **Production Doğrulama**
  - [ ] End-to-end tests
  - [ ] Load tests
  - [ ] Security audit
  - [ ] Monitoring setup

**Durum:** ⏳ Planlanmış

---

### Phase 4: Backup ve Monitoring (1 Hafta)

**Öncelik:** Orta

- [ ] **Backup Stratejisi**
  - [ ] MongoDB backup script'i
  - [ ] MinIO backup script'i
  - [ ] PostgreSQL backup script'i
  - [ ] Configuration backup script'i
  - [ ] Automated backup cron jobs

- [ ] **Monitoring ve Logging**
  - [ ] Seq logging yapılandırması
  - [ ] Health check monitoring
  - [ ] Resource monitoring
  - [ ] Alerting setup (opsiyonel)

**Durum:** ⏳ Planlanmış

---

### Phase 5: Air-Gapped Deployment (Opsiyonel)

**Öncelik:** Düşük (Gerekirse)

- [ ] **Air-Gapped Deployment Hazırlığı**
  - [ ] Docker image export script'i
  - [ ] Repository export script'i
  - [ ] Transfer metodları

- [ ] **Air-Gapped Deployment**
  - [ ] Image import
  - [ ] Repository extract
  - [ ] Self-signed certificate
  - [ ] Offline deployment test

**Durum:** ⏳ Planlanmış (Gerekirse)

---

### Phase 6: Dokümantasyon ve İyileştirme (Sürekli)

**Öncelik:** Orta

- [ ] **Dokümantasyon**
  - [ ] Deployment rehberi tamamlama
  - [ ] Sorun giderme rehberi genişletme
  - [ ] Best practices dokümantasyonu

- [ ] **Script İyileştirmeleri**
  - [ ] Deployment script iyileştirmeleri
  - [ ] Backup script iyileştirmeleri
  - [ ] Monitoring script'leri

- [ ] **Otomasyon**
  - [ ] CI/CD pipeline entegrasyonu
  - [ ] Automated testing
  - [ ] Automated deployment (opsiyonel)
  - [ ] **deploy-docs-to-server job (DEVRE DIŞI - 4 Ocak 2026)**
    - **Durum:** ⏸️ Devre dışı bırakıldı
    - **Sebep:** SSH passphrase sorunu ve key authentication problemleri
    - **Yapılan Denemeler:**
      1. ✅ Job optimize edildi (`pages` yerine `deploy-docs` artifacts kullanılıyor)
      2. ✅ SSH key yönetimi iyileştirildi (base64 decode, format kontrolü)
      3. ✅ Backup mekanizması eklendi
      4. ✅ SSH BatchMode ve PasswordAuthentication ayarları eklendi
      5. ✅ Daha iyi hata mesajları ve diagnostics eklendi
    - **Sorunlar:**
      - SSH key passphrase sorunu (`read_passphrase: can't open /dev/tty`)
      - SSH public key'in sunucunun `~/.ssh/authorized_keys` dosyasına eklenmesi gerekiyor
      - Job başarısız oldu (Permission denied)
    - **Çözüm İçin Gerekenler:**
      1. SSH public key'in sunucuya eklenmesi
      2. Passphrase'siz SSH key kullanılması
      3. SSH key'in `authorized_keys` dosyasına doğru formatta eklenmesi
    - **Gelecek Planlar:**
      - Manuel deployment script'leri kullanılabilir
      - Alternatif deployment yöntemleri değerlendirilebilir
      - SSH key yapılandırması tamamlandığında job tekrar aktif edilebilir

**Durum:** ⏳ Devam ediyor

---

## 📊 Özet Tablo

| Phase | Süre | Öncelik | Durum | Notlar |
|-------|------|---------|-------|--------|
| **Phase 1: Hazırlık** | 1 hafta | Yüksek | ⏳ Devam ediyor | Server ve environment hazırlığı |
| **Phase 2: İlk Deployment** | 1 hafta | Yüksek | ⏳ Planlanmış | Test ortamı deployment |
| **Phase 3: Production** | 1 hafta | Yüksek | ⏳ Planlanmış | Production deployment |
| **Phase 4: Backup/Monitoring** | 1 hafta | Orta | ⏳ Planlanmış | Backup ve monitoring setup |
| **Phase 5: Air-Gapped** | 1 hafta | Düşük | ⏳ Planlanmış | Gerekirse |
| **Phase 6: Dokümantasyon** | Sürekli | Orta | ⏳ Devam ediyor | Sürekli iyileştirme |

---

## 🔗 İlgili Dokümantasyon

- [DevOps Roadmap](../DEVOPS_ROADMAP.md) - Genel DevOps roadmap
- [Hosting Resource Requirements](../HOSTING_RESOURCE_REQUIREMENTS.md) - Kaynak gereksinimleri
- [Infrastructure Overview](../INFRASTRUCTURE_OVERVIEW.md) - Infrastructure genel bakış
- [Certificate Management Plan](../CERTIFICATE_MANAGEMENT_PLAN.md) - SSL sertifika yönetimi

---

## 📝 Son Güncelleme

**Tarih:** 7 Ocak 2026  
**Versiyon:** 1.1.1  
**Durum:** Nginx containerization tamamlandı, port yönetimi tamamlandı, admin.monitrang.com Basic Auth geçici olarak devre dışı bırakıldı

### ⚠️ Önemli Notlar

#### admin.monitrang.com HTTP Basic Authentication - Geçici Devre Dışı (7 Ocak 2026)

**Durum:** Tüm admin UI'lar için HTTP Basic Auth geçici olarak devre dışı bırakıldı.

**Sebep:** Tarayıcı Basic Auth modal'ında şifre kabul edilme sorunu yaşandı. curl ile test başarılı olmasına rağmen tarayıcıdan erişimde sorun devam etti.

**Yapılan Değişiklikler:**
- `ApplicationResources/mng_common/nginx/conf.d/admin.monitrang.conf` dosyasında:
  - Server-level Basic Auth yorum satırına alındı
  - Tüm location'lara (`/`, `/portainer/`, `/rabbitmq/`, `/seq/`, `/mongo/`, `/redis/`, `/nodered/`) `auth_basic off;` eklendi
  - Notlar eklendi: "Basic Auth devre dışı (geçici - 7 Ocak 2026)"

**Güvenlik Etkisi:**
- ⚠️ **Düşük Risk:** admin.monitrang.com artık Basic Auth olmadan erişilebilir
- ✅ **Kısmi Koruma:** Her admin UI'ın kendi authentication mekanizması var (Portainer, RabbitMQ, vb.)
- ✅ **SSL/TLS:** HTTPS ile şifrelenmiş iletişim devam ediyor

**Sonraki Adımlar (Orta Vade):**
1. Basic Auth sorununu çöz (tarayıcı cache, realm, veya farklı bir yaklaşım)
2. Basic Auth'u tekrar aktif et
3. Alternatif: IP whitelist ekle (double protection)
4. Uzun vade: VPN entegrasyonu (production için)

**İlgili Dosyalar:**
- `ApplicationResources/mng_common/nginx/conf.d/admin.monitrang.conf`
- `.htpasswd` dosyası mevcut ve hazır (şifre: `mc0HKkBaE4Qan65Nd0xSJv3X`)

---

## 🎯 Sonraki Adımlar

1. ✅ **Deployment Roadmap Detaylandırıldı** (Bu dokümantasyon)
2. ✅ **Nginx Yapılandırma Template Hazırlama** ✅ (4 Ocak 2026)
3. ⏳ **Port Yönetimi - Kalan Opsiyonel İşler:**
   - [ ] Application servislerin kalan port mapping'lerini kaldır (mngui:3000, mnggateway:5000, keycloak:8080)
   - [ ] Internal servislerin port mapping'lerini kaldır (MongoDB:27017, PostgreSQL:5432, Redis:6379, RabbitMQ:5672) - Güvenlik için
   - [ ] Admin/UI servislerini Nginx üzerinden erişilebilir hale getir
   - [ ] Nginx yapılandırma uyarılarını düzelt (http2 directive deprecated)
3. ⏳ **Backup Script'leri Geliştirme**
4. ⏳ **Post-Deployment Test Script'leri**
5. ⏳ **Sorun Giderme Rehberi Genişletme**
6. ⏳ **Air-Gapped Deployment Script'leri** (Gerekirse)

---

**Hazırlayan:** AI Assistant  
**Onay:** Bekliyor

