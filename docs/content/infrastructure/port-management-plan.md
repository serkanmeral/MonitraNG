# Port Yönetimi Planlama Dokümantasyonu

**Tarih:** 4 Ocak 2026  
**Durum:** ✅ Tamamlandı  
**Amaç:** Port çakışmalarını önlemek ve standart bir port yönetim stratejisi oluşturmak

---

## 📋 Mevcut Durum Analizi

### 🔴 Tespit Edilen Sorunlar

#### 1. Port Çakışmaları (Geçmişte Yaşananlar)
- ✅ **Port 80:** Nginx ↔ GitLab (Çözüldü - GitLab → 8090)
- ✅ **Port 443:** Nginx ↔ MngGateway (Çözüldü - MngGateway → 5443)
- ✅ **Port 8080:** Keycloak ↔ GitLab (Çözüldü - GitLab → 8090)
- ✅ **Port 8081:** Mongo Express ↔ Mailu Front (Çözüldü - Mailu → 8082)
- ✅ **Port 9000:** Portainer ↔ MinIO (Çözüldü - MinIO → 9090)

#### 2. Güvenlik Sorunları
- ❌ **Internal Servisler External'a Expose Edilmiş:**
  - MongoDB (27017) - External'a expose edilmiş
  - PostgreSQL (5432) - External'a expose edilmiş
  - Redis (6379) - External'a expose edilmiş
  - RabbitMQ (5672, 15672) - External'a expose edilmiş
  - MinIO (9090, 9091) - External'a expose edilmiş
- ❌ **Application Servisleri Direct Erişilebilir:**
  - MngGateway (5000, 5443) - Direct erişilebilir
  - MngKeeper (5001) - Direct erişilebilir
  - MngDataGateway (5010) - Direct erişilebilir
  - MngHub (5020) - Direct erişilebilir
  - MngUI (3000) - Direct erişilebilir

#### 3. Port Yönetimi Sorunları
- ❌ **Tutarsız Port Mapping:**
  - Bazı servisler `localhost:port` kullanıyor
  - Bazı servisler direkt `port` kullanıyor
  - Development ve production arasında farklı port yapılandırmaları var
- ❌ **Standart Port Numaralandırma Yok:**
  - Port numaraları rastgele seçilmiş
  - Port aralıkları organize değil
  - Yeni servis eklerken port seçimi zor

---

## 🎯 Hedef Port Yönetim Stratejisi

### 1. Port Kategorileri

#### A. Public Ports (External'a Expose Edilen)
- **Port 22:** SSH
- **Port 80:** HTTP (Nginx) → HTTPS redirect
- **Port 443:** HTTPS (Nginx)

#### B. Internal Ports (Docker Network Only)
- **Database Ports:** 27017 (MongoDB), 5432 (PostgreSQL)
- **Cache/Queue Ports:** 6379 (Redis), 5672 (RabbitMQ)
- **Application Ports:** 5000-5099 (Backend servisleri)
- **UI/Admin Ports:** 8000-8999 (Admin panelleri, UI servisleri)
- **Infrastructure Ports:** 9000-9999 (Infrastructure servisleri)

#### C. Development/Testing Ports (Opsiyonel)
- **Port 3000-3999:** Development frontend servisleri
- **Port 4000-4999:** Development backend servisleri
- **Port 6000-6999:** Testing servisleri

### 2. Port Numaralandırma Standardı

#### Backend Application Servisleri (5000-5099)
| Port | Servis | Açıklama |
|------|--------|----------|
| 5000 | MngGateway | API Gateway (HTTP) |
| 5001 | MngKeeper | IAM servisi |
| 5010 | MngDataGateway | Data Gateway |
| 5020 | MngHub | Hub servisi |
| 5003 | MngReactor | Reactor servisi (gelecek) |
| 5004-5009 | Reserved | Gelecek backend servisleri |

#### Infrastructure Servisleri (8000-8999)
| Port | Servis | Açıklama |
|------|--------|----------|
| 8080 | Keycloak | Authentication |
| 8081 | Mongo Express | MongoDB UI (opsiyonel) |
| 8082 | Mailu Front | Mail sunucusu frontend |
| 8090 | GitLab | GitLab UI |
| 8000 | MkDocs | Dokümantasyon |
| 8001 | Redis Commander | Redis UI (opsiyonel) |
| 5341 | Seq | Logging servisi |

#### Infrastructure Servisleri (9000-9999)
| Port | Servis | Açıklama |
|------|--------|----------|
| 9000 | Portainer | Container management |
| 9090 | MinIO API | Object storage API |
| 9091 | MinIO Console | Object storage UI |

#### Development/Testing (3000-3999)
| Port | Servis | Açıklama |
|------|--------|----------|
| 3000 | MngUI | Frontend (development) |
| 1880 | Node-RED | Node-RED flow editor |

#### Database/Cache/Queue (Internal Only - No Host Mapping)
| Port | Servis | Açıklama |
|------|--------|----------|
| 27017 | MongoDB | Database (internal only) |
| 5432 | PostgreSQL | Database (internal only) |
| 6379 | Redis | Cache (internal only) |
| 5672 | RabbitMQ AMQP | Message queue (internal only) |
| 15672 | RabbitMQ Management | Management UI (internal only) |
| 1883 | Mosquitto MQTT | MQTT broker (internal only) |
| 9001 | Mosquitto WebSocket | MQTT WebSocket (internal only) |

---

## 🔒 Güvenlik Stratejisi

### 1. Internal Servislerin External'a Expose Edilmemesi

**Kural:** Internal servisler (database, cache, queue) ASLA host port mapping ile external'a expose edilmemeli.

**Mevcut Sorunlar:**
```yaml
# ❌ YANLIŞ - MongoDB external'a expose edilmiş
mongo:
  ports:
    - "27017:27017"  # KALDIRILMALI

# ✅ DOĞRU - Sadece Docker network içinden erişilebilir
mongo:
  # ports kısmı yok veya sadece internal network
  networks:
    - mng_network
```

### 2. Application Servislerinin Nginx Üzerinden Erişilmesi

**Kural:** Application servisleri direct erişilebilir olmamalı, sadece Nginx reverse proxy üzerinden erişilebilir olmalı.

**Mevcut Sorunlar:**
```yaml
# ❌ YANLIŞ - Direct erişilebilir
mnggateway:
  ports:
    - "5000:5000"  # KALDIRILMALI veya sadece localhost

# ✅ DOĞRU - Sadece localhost veya hiç expose edilmemeli
mnggateway:
  ports:
    - "127.0.0.1:5000:5000"  # Sadece localhost
  # veya
  # ports kısmı yok - sadece Docker network
```

### 3. Firewall Kuralları

**Kural:** Sadece gerekli portlar firewall'da açık olmalı.

**Açık Portlar:**
- `22` - SSH
- `80` - HTTP (Nginx)
- `443` - HTTPS (Nginx)

**Kapalı Portlar:**
- Tüm internal servis portları
- Tüm application servis portları (Nginx üzerinden erişilebilir)

---

## 📝 Port Yönetim Planı

### Phase 1: Nginx Containerization ve Port Mapping'leri Kaldırma

#### 1.1 Nginx'i Docker Container Olarak Çalıştırma
- [ ] Nginx Docker image'ını docker-compose.yml'e ekle
- [ ] Nginx container'ını Docker network'e bağla (`mng_network`)
- [ ] Nginx port mapping'lerini yapılandır (80, 443)
- [ ] Let's Encrypt sertifikalarını volume mount et
- [ ] Nginx yapılandırma dosyalarını volume mount et
- [ ] Nginx log dosyalarını volume mount et

#### 1.2 Internal Servisleri Korumaya Alma
- [ ] MongoDB port mapping'i kaldır (27017) - Sadece Docker network
- [ ] PostgreSQL port mapping'i kaldır (5432) - Sadece Docker network
- [ ] Redis port mapping'i kaldır (6379) - Sadece Docker network
- [ ] RabbitMQ port mapping'lerini kaldır (5672, 15672) - Sadece Docker network
- [ ] MinIO port mapping'lerini kaldır (9090, 9091) - Sadece Docker network

#### 1.3 Application Servislerini Korumaya Alma
- [ ] MngGateway port mapping'lerini kaldır (5000, 5443) - Sadece Docker network
- [ ] MngKeeper port mapping'ini kaldır (5001) - Sadece Docker network
- [ ] MngDataGateway port mapping'ini kaldır (5010) - Sadece Docker network
- [ ] MngHub port mapping'ini kaldır (5020) - Sadece Docker network
- [ ] MngUI port mapping'ini kaldır (3000) - Sadece Docker network

#### 1.4 Admin/UI Servislerini Yapılandırma
- [ ] Keycloak port mapping'ini kaldır (8080) - Sadece Docker network
- [ ] Mongo Express port mapping'ini kaldır (8081) - Sadece Docker network
- [ ] GitLab port mapping'ini kaldır (8090) - Sadece Docker network
- [ ] Portainer port mapping'ini kaldır (9000) - Sadece Docker network
- [ ] Seq port mapping'ini kaldır (5341) - Sadece Docker network
- [ ] Node-RED port mapping'ini kaldır (1880) - Sadece Docker network (opsiyonel)

#### 1.5 Nginx Yapılandırmasını Güncelleme
- [ ] Nginx yapılandırmasında `localhost:port` → `container_name:port` değişikliği
- [ ] Tüm `proxy_pass` direktiflerini container name'ler kullanacak şekilde güncelle
- [ ] Nginx yapılandırmasını test et
- [ ] Nginx container'ını başlat ve test et

### Phase 2: Port Numaralandırma Standardizasyonu

#### 2.1 Port Aralıklarını Organize Etme
- [ ] Backend servisleri: 5000-5099
- [ ] Infrastructure servisleri: 8000-8999
- [ ] Infrastructure servisleri (devam): 9000-9999
- [ ] Development servisleri: 3000-3999

#### 2.2 Port Rezervasyon Sistemi
- [ ] Port rezervasyon tablosu oluştur
- [ ] Yeni servis eklerken port rezervasyon kontrolü yap
- [ ] Port çakışması kontrol scripti oluştur

### Phase 3: Dokümantasyon ve Otomasyon

#### 3.1 Port Yönetim Dokümantasyonu
- [ ] Port yapılandırması dokümantasyonu güncelle
- [ ] Port rezervasyon tablosu dokümante et
- [ ] Port yönetim best practices dokümantasyonu oluştur

#### 3.2 Port Kontrol Scriptleri
- [ ] Port kullanım kontrol scripti
- [ ] Port çakışması tespit scripti
- [ ] Port rezervasyon kontrol scripti

---

## 🔧 Uygulama Adımları

### Adım 1: Port Mapping'leri Güncelleme

**Örnek - MongoDB:**
```yaml
# ÖNCE (Yanlış)
mongo:
  ports:
    - "27017:27017"

# SONRA (Doğru)
mongo:
  # ports kısmı kaldırıldı - sadece Docker network
  networks:
    - mng_network
```

**Örnek - MngGateway:**
```yaml
# ÖNCE (Yanlış)
mnggateway:
  ports:
    - "5000:5000"
    - "5443:443"

# SONRA (Doğru)
mnggateway:
  ports:
    - "127.0.0.1:5000:5000"  # Sadece localhost
    - "127.0.0.1:5443:443"   # Sadece localhost
```

### Adım 2: Firewall Kurallarını Yapılandırma

```bash
# UFW durumunu kontrol et
sudo ufw status

# Sadece gerekli portları aç
sudo ufw allow 22/tcp    # SSH
sudo ufw allow 80/tcp    # HTTP
sudo ufw allow 443/tcp   # HTTPS

# UFW'yi etkinleştir
sudo ufw enable

# Durumu kontrol et
sudo ufw status verbose
```

### Adım 3: Port Kontrol Scripti Oluşturma

```bash
#!/bin/bash
# Port kullanım kontrol scripti
# Kullanım: ./check-ports.sh

echo "🔍 Port Kullanım Kontrolü"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"

# Tüm dinleyen portları listele
echo "📊 Dinleyen Portlar:"
sudo netstat -tlnp | grep LISTEN

echo ""
echo "🐳 Docker Container Portları:"
docker ps --format "table {{.Names}}\t{{.Ports}}"

echo ""
echo "⚠️  Port Çakışması Kontrolü:"
# Port çakışması kontrolü için script
```

---

## 📊 Port Rezervasyon Tablosu

### Backend Application Servisleri (5000-5099)
| Port | Servis | Durum | Notlar |
|------|--------|-------|--------|
| 5000 | MngGateway | ✅ Kullanılıyor | HTTP port |
| 5001 | MngKeeper | ✅ Kullanılıyor | HTTPS port |
| 5010 | MngDataGateway | ✅ Kullanılıyor | HTTPS port |
| 5020 | MngHub | ✅ Kullanılıyor | HTTP port |
| 5003 | MngReactor | ⏳ Rezerve | Gelecek servis |
| 5004-5009 | - | 🔒 Rezerve | Gelecek servisler |
| 5011-5019 | - | 🔒 Rezerve | Gelecek servisler |
| 5021-5099 | - | 🔒 Rezerve | Gelecek servisler |

### Infrastructure Servisleri (8000-8999)
| Port | Servis | Durum | Notlar |
|------|--------|-------|--------|
| 8000 | MkDocs | ✅ Kullanılıyor | Dokümantasyon |
| 8001 | Redis Commander | ✅ Kullanılıyor | Redis UI |
| 8080 | Keycloak | ✅ Kullanılıyor | Authentication |
| 8081 | Mongo Express | ✅ Kullanılıyor | MongoDB UI |
| 8082 | Mailu Front | ✅ Kullanılıyor | Mail sunucusu |
| 8090 | GitLab | ✅ Kullanılıyor | GitLab UI |
| 5341 | Seq | ✅ Kullanılıyor | Logging |
| 1880 | Node-RED | ✅ Kullanılıyor | Flow editor |

### Infrastructure Servisleri (9000-9999)
| Port | Servis | Durum | Notlar |
|------|--------|-------|--------|
| 9000 | Portainer | ✅ Kullanılıyor | Container management |
| 9090 | MinIO API | ✅ Kullanılıyor | Object storage |
| 9091 | MinIO Console | ✅ Kullanılıyor | Object storage UI |

### Development/Testing (3000-3999)
| Port | Servis | Durum | Notlar |
|------|--------|-------|--------|
| 3000 | MngUI | ✅ Kullanılıyor | Frontend |

---

## ✅ Kontrol Listesi

### Güvenlik
- [ ] Internal servisler external'a expose edilmemiş
- [ ] Application servisleri sadece localhost veya Nginx üzerinden erişilebilir
- [ ] Firewall kuralları yapılandırılmış
- [ ] Sadece gerekli portlar açık

### Port Yönetimi
- [ ] Port numaralandırma standardı uygulanmış
- [ ] Port rezervasyon tablosu güncel
- [ ] Port çakışması kontrol scripti çalışıyor
- [ ] Port yönetim dokümantasyonu güncel

### Dokümantasyon
- [ ] Port yapılandırması dokümante edilmiş
- [ ] Port rezervasyon tablosu dokümante edilmiş
- [ ] Port yönetim best practices dokümante edilmiş

---

## 🌐 Nginx Yapılandırması ve Port Yönetimi

### Mevcut Durum

**Nginx Çalışma Modu:** Host üzerinde (container değil)  
**Erişim Yöntemi:** `localhost:port` üzerinden servislere erişiyor  
**Docker Network:** Nginx Docker network'üne bağlı değil

**Mevcut Nginx Yapılandırması:**
```nginx
# ❌ MEVCUT - localhost kullanıyor
location / {
    proxy_pass http://localhost:3000;  # MngUI
}

location /api/ {
    proxy_pass http://localhost:5000;  # MngGateway
}

location /auth/ {
    proxy_pass http://localhost:8080;  # Keycloak
}
```

### Port Yönetimi Planı ile Nginx Entegrasyonu

#### Seçenek 1: Nginx'i Host Üzerinde Tutmak (Mevcut Durum)

**Avantajlar:**
- ✅ Let's Encrypt sertifikaları kolay yönetiliyor
- ✅ Nginx yapılandırması host üzerinde
- ✅ Port mapping'ler localhost'a çekildiğinde erişim çalışıyor

**Dezavantajlar:**
- ❌ Docker network'üne bağlı değil
- ❌ Container name'ler kullanılamıyor
- ❌ Port mapping'ler gerekli (localhost:port)

**Yapılandırma:**
```yaml
# Application servisleri localhost'a map edilmeli
mngui:
  ports:
    - "127.0.0.1:3000:80"  # Sadece localhost

mnggateway:
  ports:
    - "127.0.0.1:5000:5000"  # Sadece localhost
```

```nginx
# Nginx yapılandırması (localhost kullanmaya devam)
location / {
    proxy_pass http://localhost:3000;
}
```

#### Seçenek 2: Nginx'i Docker Container Olarak Çalıştırmak (Önerilen)

**Avantajlar:**
- ✅ Docker network'üne bağlı
- ✅ Container name'ler kullanılabilir
- ✅ Port mapping'ler gerekmez (sadece Docker network)
- ✅ Daha güvenli (tamamen containerized)

**Dezavantajlar:**
- ❌ Let's Encrypt sertifikaları volume mount gerektirir
- ❌ Nginx yapılandırması container içinde

**Yapılandırma:**
```yaml
# Nginx container
nginx:
  image: nginx:alpine
  container_name: nginx
  ports:
    - "80:80"
    - "443:443"
  volumes:
    - ./nginx.conf:/etc/nginx/nginx.conf
    - /etc/letsencrypt:/etc/letsencrypt:ro
  networks:
    - mng_network
  depends_on:
    - mngui
    - mnggateway
    - keycloak
```

```yaml
# Application servisleri - port mapping YOK
mngui:
  # ports kısmı yok - sadece Docker network
  networks:
    - mng_network

mnggateway:
  # ports kısmı yok - sadece Docker network
  networks:
    - mng_network
```

```nginx
# Nginx yapılandırması (container name kullanıyor)
location / {
    proxy_pass http://mngui:80;  # Container name
}

location /api/ {
    proxy_pass http://mnggateway:5000;  # Container name
}

location /auth/ {
    proxy_pass http://keycloak:8080;  # Container name
}
```

### Önerilen Yaklaşım ✅

**Seçilen Yaklaşım: Phase 2 (Containerized Nginx)**

- ✅ Nginx'i Docker container olarak çalıştırmak
- ✅ Docker network üzerinden container name'ler kullanmak
- ✅ Port mapping'leri tamamen kaldırmak (sadece Nginx için 80/443 kalacak)
- ✅ Daha güvenli ve modern yaklaşım

### Nginx Containerization Detayları

#### 1. Docker Compose Güncellemesi

**Nginx Container Ekleme:**
```yaml
# ApplicationResources/mng_common/docker-compose.yml veya
# ApplicationResources/mng_apps/docker-compose.yml'e eklenecek

nginx:
  image: nginx:alpine
  container_name: nginx
  ports:
    - "80:80"
    - "443:443"
  volumes:
    - ./nginx/nginx.conf:/etc/nginx/nginx.conf:ro
    - ./nginx/conf.d:/etc/nginx/conf.d:ro
    - /etc/letsencrypt:/etc/letsencrypt:ro
    - nginx_logs:/var/log/nginx
  networks:
    - mng_network
  restart: unless-stopped
  depends_on:
    - mngui
    - mnggateway
    - keycloak
    - gitlab
```

**Application Servisleri Güncellemesi:**
```yaml
# ÖNCE
mngui:
  ports:
    - "3000:80"

# SONRA
mngui:
  # ports kısmı kaldırıldı - sadece Docker network
  networks:
    - mng_network
```

#### 2. Nginx Yapılandırması Güncellemesi

**Container Name'ler Kullanımı:**
```nginx
# ÖNCE (localhost)
location / {
    proxy_pass http://localhost:3000;  # MngUI
}

location /api/ {
    proxy_pass http://localhost:5000;  # MngGateway
}

location /auth/ {
    proxy_pass http://localhost:8080;  # Keycloak
}

# SONRA (container name)
location / {
    proxy_pass http://mngui:80;  # MngUI container
}

location /api/ {
    proxy_pass http://mnggateway:5000;  # MngGateway container
}

location /auth/ {
    proxy_pass http://keycloak:8080;  # Keycloak container
}
```

#### 3. Let's Encrypt Sertifikaları

**Volume Mount:**
```yaml
nginx:
  volumes:
    - /etc/letsencrypt:/etc/letsencrypt:ro  # Read-only mount
```

**Nginx Yapılandırması:**
```nginx
ssl_certificate /etc/letsencrypt/live/monitrang.com/fullchain.pem;
ssl_certificate_key /etc/letsencrypt/live/monitrang.com/privkey.pem;
```

#### 4. Nginx Yapılandırma Dosya Yapısı

```
ApplicationResources/
  mng_common/
    nginx/
      nginx.conf          # Ana Nginx yapılandırması
      conf.d/
        monitrang.conf   # MonitraNG domain yapılandırması
        mail.conf        # Mail sunucusu yapılandırması
      ssl/                # SSL yapılandırmaları (opsiyonel)
```

#### 5. Network Yapılandırması

**Tüm Servisler Aynı Network'te:**
```yaml
networks:
  mng_network:
    driver: bridge
    name: mng_network  # External network name
```

**Servislerin Network'e Bağlanması:**
```yaml
services:
  nginx:
    networks:
      - mng_network
  
  mngui:
    networks:
      - mng_network
  
  mnggateway:
    networks:
      - mng_network
```

---

## 🚀 Uygulama Planı (Phase 2 Yaklaşımı)

### Adım 1: Nginx Containerization
1. **Nginx Docker Compose Yapılandırması:**
   - Nginx container'ını docker-compose.yml'e ekle
   - Network yapılandırmasını yap
   - Volume mount'ları yapılandır (config, logs, certificates)
   - Dependencies ekle (mngui, mnggateway, keycloak, vb.)

2. **Nginx Yapılandırma Dosyalarını Hazırlama:**
   - Nginx yapılandırma dosyalarını organize et
   - Container name'ler kullanacak şekilde güncelle
   - SSL yapılandırmasını kontrol et

3. **Nginx Container'ını Test Etme:**
   - Nginx container'ını başlat
   - Yapılandırmayı test et (`nginx -t`)
   - Servislere erişimi test et

### Adım 2: Port Mapping'leri Kaldırma
1. **Internal Servisleri Güncelleme:**
   - MongoDB, PostgreSQL, Redis, RabbitMQ, MinIO port mapping'lerini kaldır
   - Sadece Docker network üzerinden erişilebilir hale getir

2. **Application Servislerini Güncelleme:**
   - MngGateway, MngKeeper, MngDataGateway, MngHub, MngUI port mapping'lerini kaldır
   - Sadece Docker network üzerinden erişilebilir hale getir

3. **Admin/UI Servislerini Güncelleme:**
   - Keycloak, GitLab, Portainer, Seq port mapping'lerini kaldır
   - Sadece Docker network üzerinden erişilebilir hale getir

### Adım 3: Test ve Doğrulama
1. **Servis Erişim Testleri:**
   - Nginx üzerinden tüm servislere erişimi test et
   - Container name'lerin çözümlendiğini doğrula
   - SSL sertifikalarının çalıştığını doğrula

2. **Port Kontrolü:**
   - Host üzerinde sadece 80, 443 portlarının açık olduğunu doğrula
   - Internal servislerin external'a expose edilmediğini doğrula

3. **Firewall Yapılandırması:**
   - Sadece 22, 80, 443 portlarını aç
   - Diğer portları kapat

### Adım 4: Dokümantasyon ve Otomasyon
1. **Dokümantasyon Güncelleme:**
   - Port yönetim dokümantasyonunu güncelle
   - Nginx containerization dokümantasyonu oluştur
   - Migration rehberi oluştur

2. **Script Oluşturma:**
   - Port kontrol scripti
   - Network connectivity test scripti
   - Nginx yapılandırma test scripti

---

---

## 📝 Implementation Checklist

### Phase 1: Nginx Containerization
- [ ] Nginx Docker Compose yapılandırması oluştur
- [ ] Nginx yapılandırma dosyalarını organize et
- [ ] Container name'ler kullanacak şekilde Nginx yapılandırmasını güncelle
- [ ] Let's Encrypt sertifikalarını volume mount et
- [ ] Nginx container'ını test et

### Phase 2: Port Mapping'leri Kaldırma
- [ ] Internal servislerin port mapping'lerini kaldır
- [ ] Application servislerin port mapping'lerini kaldır
- [ ] Admin/UI servislerin port mapping'lerini kaldır
- [ ] Tüm servislerin Docker network'e bağlı olduğunu doğrula

### Phase 3: Test ve Doğrulama
- [ ] Nginx üzerinden servis erişimini test et
- [ ] Port kontrolü yap (sadece 80, 443 açık olmalı)
- [ ] Firewall yapılandırmasını güncelle
- [ ] SSL sertifikalarının çalıştığını doğrula

### Phase 4: Dokümantasyon
- [ ] Port yönetim dokümantasyonunu güncelle
- [ ] Nginx containerization dokümantasyonu oluştur
- [ ] Migration rehberi oluştur
- [ ] Script'leri oluştur ve dokümante et

---

**Son Güncelleme:** 4 Ocak 2026  
**Durum:** 📋 Planlama Aşamasında - Phase 2 Yaklaşımı Seçildi  
**Yaklaşım:** Nginx Containerization + Container Name'ler

