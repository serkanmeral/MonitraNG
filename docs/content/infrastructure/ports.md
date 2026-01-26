# Port Yapılandırması

**Tarih:** 2 Ocak 2026  
**Production Sunucu IP:** `45.141.151.52`  
**Durum:** ✅ Mevcut Port Yapılandırması Dokümante Edildi

---

## 📋 Genel Bakış

Bu dokümantasyon, MonitraNG sistemindeki tüm servislerin port yapılandırmalarını içerir.

---

## 🔧 Infrastructure Servisleri Port Yapılandırması

### MongoDB

| Özellik | Değer |
|---------|-------|
| **Container Port** | `27017` |
| **Host Port** | `27017` |
| **Protokol** | TCP |
| **Erişim** | Internal only (Docker network) |
| **Authentication** | `admin/admin123` |
| **Connection String** | `mongodb://admin:admin123@mongo:27017` |

**Not:** MongoDB production'da external'a expose edilmemeli. Sadece Docker network içinden erişilebilir olmalı.

### PostgreSQL (Keycloak için)

| Özellik | Değer |
|---------|-------|
| **Container Port** | `5432` |
| **Host Port** | `5432` |
| **Protokol** | TCP |
| **Erişim** | Internal only (Docker network) |
| **Database** | `keycloak` |
| **Username** | `keycloak` |
| **Password** | `keycloak123` |
| **Connection String** | `jdbc:postgresql://postgres:5432/keycloak` |

**Not:** PostgreSQL production'da external'a expose edilmemeli. Sadece Keycloak container'ından erişilebilir olmalı.

### Keycloak

| Özellik | Değer |
|---------|-------|
| **Container Port** | `8080` |
| **Host Port** | `8080` |
| **Protokol** | HTTP |
| **Erişim** | Nginx reverse proxy üzerinden (`auth.monitrang.com`) |
| **Admin Username** | `admin` |
| **Admin Password** | `admin123` |
| **Internal URL** | `http://keycloak:8080` |
| **External URL** | `https://auth.monitrang.com` (Nginx üzerinden) |

**Yapılandırma:**
- `KC_PROXY: edge` (Nginx reverse proxy için)
- `KC_HOSTNAME_STRICT: false`
- `KC_HOSTNAME_STRICT_HTTPS: false`

### Redis

| Özellik | Değer |
|---------|-------|
| **Container Port** | `6379` |
| **Host Port** | `6379` |
| **Protokol** | TCP |
| **Erişim** | Internal only (Docker network) |
| **Password** | `redis123` |
| **Connection String** | `redis:6379,password=redis123` |

**Not:** Redis production'da external'a expose edilmemeli. Sadece application servislerinden erişilebilir olmalı.

### RabbitMQ

| Özellik | Değer |
|---------|-------|
| **AMQP Port** | `5672` (Container) → `5672` (Host) |
| **Management UI Port** | `15672` (Container) → `15672` (Host) |
| **Protokol** | TCP (AMQP), HTTP (Management) |
| **Erişim** | Internal only (AMQP), Opsiyonel external (Management UI) |
| **Username** | `admin` |
| **Password** | `admin123` |
| **Management URL** | `http://localhost:15672` (opsiyonel) |

**Not:** RabbitMQ AMQP port'u production'da external'a expose edilmemeli. Management UI opsiyonel olarak Nginx üzerinden erişilebilir.

### MinIO

| Özellik | Değer |
|---------|-------|
| **API Port** | `9000` (Container) → `9090` (Host) |
| **Console Port** | `9001` (Container) → `9091` (Host) |
| **Protokol** | HTTP |
| **Erişim** | Internal only (API), Opsiyonel external (Console) |
| **Access Key** | `minioadmin` |
| **Secret Key** | `minioadmin` |
| **API URL** | `http://minio:9000` (internal) |
| **Console URL** | `http://localhost:9091` (opsiyonel) |

**Not:** MinIO API port'u production'da external'a expose edilmemeli. Console opsiyonel olarak Nginx üzerinden erişilebilir.

### GitLab

| Özellik | Değer |
|---------|-------|
| **HTTP Port** | `80` (Container) → `8090` (Host) |
| **HTTPS Port** | `443` (Container) → `443` (Host) |
| **SSH Port** | `22` (Container) → `2222` (Host) |
| **Protokol** | HTTP, HTTPS, TCP (SSH) |
| **Erişim** | Nginx reverse proxy üzerinden (`gitlab.monitrang.com`) |
| **External URL** | `http://45.141.151.52:8090` (direct) veya `https://gitlab.monitrang.com` (Nginx) |
| **SSH URL** | `ssh://git@45.141.151.52:2222` |

**Not:** GitLab port'u `8090` olarak ayarlandı çünkü Nginx port `80`'i kullanıyor.

### Mongo Express (Opsiyonel)

| Özellik | Değer |
|---------|-------|
| **Container Port** | `8081` |
| **Host Port** | `8081` |
| **Protokol** | HTTP |
| **Erişim** | Opsiyonel (internal only önerilir) |
| **Username** | `admin` |
| **Password** | `admin123` |
| **URL** | `http://localhost:8081` |

**Not:** Mongo Express production'da external'a expose edilmemeli. Sadece internal network'ten erişilebilir olmalı.

---

## 🚀 Application Servisleri Port Yapılandırması

### MngGateway (API Gateway)

| Özellik | Değer |
|---------|-------|
| **HTTP Port** | `5000` (Container) → `5000` (Host) |
| **HTTPS Port** | `443` (Container) → `5443` (Host) |
| **Protokol** | HTTP, HTTPS |
| **Erişim** | Nginx reverse proxy üzerinden (`api.monitrang.com`) |
| **Internal URL** | `http://mnggateway:5000` |
| **External URL** | `https://api.monitrang.com` (Nginx üzerinden) |
| **Direct URL** | `http://45.141.151.52:5000` (opsiyonel) |

**Not:** Port `5443` kullanılıyor çünkü port `443` Nginx tarafından kullanılıyor.

### MngKeeper

| Özellik | Değer |
|---------|-------|
| **HTTPS Port** | `5001` (Container) → `5001` (Host) |
| **Protokol** | HTTPS |
| **Erişim** | Nginx reverse proxy üzerinden (`api.monitrang.com/api/keeper/`) |
| **Internal URL** | `https://mngkeeper:5001` |
| **External URL** | `https://api.monitrang.com/api/keeper/` (Nginx üzerinden) |
| **Direct URL** | `https://45.141.151.52:5001` (localhost only) |

**Not:** MngKeeper localhost only olarak yapılandırılmış. Nginx üzerinden erişim önerilir.

### MngDataGateway

| Özellik | Değer |
|---------|-------|
| **HTTPS Port** | `5010` (Container) → `5010` (Host) |
| **Protokol** | HTTPS |
| **Erişim** | Nginx reverse proxy üzerinden (`api.monitrang.com/api/datagateway/`) |
| **Internal URL** | `https://mngdatagateway:5010` |
| **External URL** | `https://api.monitrang.com/api/datagateway/` (Nginx üzerinden) |
| **Direct URL** | `https://45.141.151.52:5010` (opsiyonel) |

### MngHub

| Özellik | Değer |
|---------|-------|
| **HTTP Port** | `5020` (Container) → `5020` (Host) |
| **Protokol** | HTTP |
| **Erişim** | Nginx reverse proxy üzerinden (`api.monitrang.com/api/hub/`) |
| **Internal URL** | `http://mnghub:5020` |
| **External URL** | `https://api.monitrang.com/api/hub/` (Nginx üzerinden) |
| **Direct URL** | `http://45.141.151.52:5020` (internal network only) |

**Not:** MngHub SignalR için kullanılıyor, WebSocket desteği gerekiyor.

### MngUI (Frontend)

| Özellik | Değer |
|---------|-------|
| **HTTP Port** | `80` (Container) → `3000` (Host) |
| **Protokol** | HTTP |
| **Erişim** | Nginx reverse proxy üzerinden (`app.monitrang.com`) |
| **Internal URL** | `http://mngui:80` |
| **External URL** | `https://app.monitrang.com` (Nginx üzerinden) |
| **Direct URL** | `http://45.141.151.52:3000` (opsiyonel) |

---

## 🔒 Port Güvenliği

### Firewall Kuralları

Production sunucusunda sadece gerekli portlar açık olmalı:

**Açık Portlar:**
- `22` - SSH
- `80` - HTTP (Nginx)
- `443` - HTTPS (Nginx)
- `8090` - GitLab (opsiyonel - Nginx üzerinden erişilebilir)

**Kapalı Portlar (Internal Only):**
- `27017` - MongoDB
- `5432` - PostgreSQL
- `6379` - Redis
- `5672` - RabbitMQ AMQP
- `5000`, `5001`, `5010`, `5020`, `3000` - Application servisleri (Nginx üzerinden erişilebilir)

### Firewall Yapılandırması (UFW)

```bash
# UFW durumunu kontrol et
sudo ufw status

# Gerekli portları aç
sudo ufw allow 22/tcp    # SSH
sudo ufw allow 80/tcp    # HTTP
sudo ufw allow 443/tcp   # HTTPS

# UFW'yi etkinleştir
sudo ufw enable

# Durumu kontrol et
sudo ufw status verbose
```

---

## 📊 Port Kullanım Özeti

### External'a Expose Edilen Portlar

| Port | Servis | Açıklama |
|------|--------|----------|
| `22` | SSH | Sunucu yönetimi |
| `80` | Nginx | HTTP (HTTPS'e redirect) |
| `443` | Nginx | HTTPS |
| `8090` | GitLab | GitLab UI (opsiyonel - Nginx üzerinden erişilebilir) |

### Internal Portlar (Docker Network)

| Port | Servis | Açıklama |
|------|--------|----------|
| `27017` | MongoDB | Database |
| `5432` | PostgreSQL | Database |
| `6379` | Redis | Cache |
| `5672` | RabbitMQ | Message queue |
| `15672` | RabbitMQ Management | Management UI |
| `9000` | MinIO API | Object storage |
| `9001` | MinIO Console | Object storage UI |
| `8080` | Keycloak | Authentication |
| `5000` | MngGateway | API Gateway |
| `5001` | MngKeeper | IAM servisi |
| `5010` | MngDataGateway | Data Gateway |
| `5020` | MngHub | Hub servisi |
| `80` (container) | MngUI | Frontend |

---

## 🔍 Port Kontrol Komutları

### Port Kullanımını Kontrol Etme

```bash
# Tüm dinleyen portları listele
sudo netstat -tlnp

# Belirli bir port'u kontrol et
sudo netstat -tlnp | grep 5000
sudo netstat -tlnp | grep 8080

# Docker container port'larını kontrol et
docker ps --format "table {{.Names}}\t{{.Ports}}"
```

### Port Çakışması Kontrolü

```bash
# Port'un kullanılıp kullanılmadığını kontrol et
sudo lsof -i :5000
sudo lsof -i :8080

# Port tarama (güvenlik testi)
sudo nmap -p 1-65535 localhost
```

---

## ⚠️ Bilinen Port Çakışmaları ve Çözümler

### 1. Port 80 Çakışması

**Sorun:** Nginx ve GitLab port 80'i kullanmaya çalışıyor.

**Çözüm:** ✅ GitLab port'u `8090` olarak değiştirildi.

### 2. Port 443 Çakışması

**Sorun:** Nginx ve MngGateway port 443'i kullanmaya çalışıyor.

**Çözüm:** ✅ MngGateway HTTPS port'u `5443` olarak değiştirildi.

### 3. Port 8080 Çakışması

**Sorun:** Keycloak ve GitLab port 8080'i kullanmaya çalışıyor.

**Çözüm:** ✅ GitLab port'u `8090` olarak değiştirildi, Keycloak `8080`'de kaldı.

---

## 📚 İlgili Dokümantasyon

- **Nginx Yapılandırması:** `docs/infrastructure/nginx.md`
- **Domain ve DNS:** `docs/infrastructure/domain-dns.md`
- **Docker Compose:** `ApplicationResources/mng_common/docker-compose.yml`
- **Production Docker Compose:** `ApplicationResources/mng_apps/docker-compose.production.yml`

---

## ✅ Port Yapılandırması Kontrol Listesi

- [x] Infrastructure servisleri port yapılandırması dokümante edildi
- [x] Application servisleri port yapılandırması dokümante edildi
- [x] Port çakışmaları çözüldü
- [ ] Firewall kuralları yapılandırıldı
- [ ] Port güvenliği test edildi
- [ ] Internal servislerin external'a expose edilmediği doğrulandı

---

**Son Güncelleme:** 2 Ocak 2026  
**Durum:** ✅ Port Yapılandırması Dokümante Edildi

