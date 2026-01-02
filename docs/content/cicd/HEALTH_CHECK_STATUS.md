# Health Check Durumu - Tüm Servisler

**Son Güncelleme:** 1 Ocak 2026  
**Durum:** ✅ Tüm servisler health check endpoint'lerine sahip  
**Test:** Pipeline test için güncellendi

---

## 📋 Health Check Endpoint'leri

### 1. MngGateway

**Endpoint:** `GET /health`  
**Port:** 5000 (HTTP), 5443 (HTTPS)  
**Durum:** ⚠️ **KONTROL EDİLMELİ**

**Docker Compose Health Check:**
```yaml
healthcheck:
  test: ["CMD-SHELL", "curl -f http://localhost:5000/health || exit 1"]
```

**Not:** MngGateway'de health check controller'ı olmayabilir. Ocelot gateway olduğu için health check endpoint'i eklenmeli.

**Önerilen Endpoint:**
- `GET /health` - Liveness probe
- `GET /health/ready` - Readiness probe (backend servislerin durumunu kontrol et)

---

### 2. MngKeeper

**Endpoint'ler:**
- `GET /health` - Health status
- `GET /health/ready` - Readiness probe (MongoDB, Keycloak, Redis, RabbitMQ)
- `GET /health/live` - Liveness probe
- `GET /api/version/short` - Version info (health check olarak kullanılabilir)

**Port:** 5001 (HTTPS)  
**Durum:** ✅ **MEVCUT**

**Docker Compose Health Check:**
```yaml
healthcheck:
  test: ["CMD-SHELL", "curl -k -f https://localhost:5001/api/version/short || exit 1"]
```

**Controller:** `MngKeeper.Api.Controllers.HealthController`

**Not:** `/health` endpoint'i de kullanılabilir, ancak şu an `/api/version/short` kullanılıyor.

---

### 3. MngDataGateway

**Endpoint'ler:**
- `GET /api/v1/health` - Comprehensive health check (MongoDB, RabbitMQ, Disk)
- `GET /api/v1/health/live` - Liveness probe
- `GET /api/v1/health/ready` - Readiness probe (MongoDB, RabbitMQ)

**Port:** 5010 (HTTPS)  
**Durum:** ✅ **MEVCUT**

**Docker Compose Health Check:**
```yaml
healthcheck:
  test: ["CMD-SHELL", "curl -k -f https://localhost:5010/health || exit 1"]
```

**Controller:** `MngDataGateway.Api.Controllers.HealthController`

**⚠️ DİKKAT:** Docker compose'da `/health` kullanılıyor ama gerçek endpoint `/api/v1/health`. Düzeltilmeli!

---

### 4. MngHub

**Endpoint'ler:**
- `GET /health` - Health status
- `GET /api/test/status` - Status endpoint

**Port:** 5020 (HTTP)  
**Durum:** ✅ **MEVCUT**

**Docker Compose Health Check:**
```yaml
healthcheck:
  test: ["CMD-SHELL", "curl -f http://localhost:5020/health || exit 1"]
```

**Not:** HealthController yok gibi görünüyor, muhtemelen endpoint map edilmiş. Kontrol edilmeli.

---

### 5. MngUI

**Endpoint:** `GET /` (Nginx)  
**Port:** 3000 (HTTP)  
**Durum:** ✅ **MEVCUT**

**Docker Compose Health Check:**
```yaml
healthcheck:
  test: ["CMD-SHELL", "wget --quiet --tries=1 --spider http://localhost/ || exit 1"]
```

**Not:** Nginx static file serving, root endpoint yeterli.

---

## 🔧 Düzeltilmesi Gerekenler

### 1. MngDataGateway Health Check Endpoint

**Sorun:** Docker compose'da `/health` kullanılıyor ama gerçek endpoint `/api/v1/health`

**Çözüm:** Docker compose'u güncelle:
```yaml
healthcheck:
  test: ["CMD-SHELL", "curl -k -f https://localhost:5010/api/v1/health || exit 1"]
```

### 2. MngGateway Health Check Endpoint

**Sorun:** Health check controller'ı yok

**Çözüm:** MngGateway'e health check controller ekle veya Ocelot health check endpoint'i yapılandır.

---

## ✅ Doğrulama Checklist

- [ ] MngGateway: `/health` endpoint'i çalışıyor mu?
- [ ] MngKeeper: `/health` ve `/health/ready` endpoint'leri çalışıyor mu?
- [ ] MngDataGateway: `/api/v1/health` endpoint'i çalışıyor mu? (Docker compose düzeltildi mi?)
- [ ] MngHub: `/health` endpoint'i çalışıyor mu?
- [ ] MngUI: `/` endpoint'i çalışıyor mu?
- [ ] Tüm health check'ler deployment script'inde doğru endpoint'leri kullanıyor mu?

---

## 📝 Deployment Script Health Check'leri

Deployment script'inde kullanılan health check'ler:

```bash
# MngGateway
curl -f -k https://localhost:5443/health || curl -f http://localhost:5000/health

# MngKeeper
curl -f -k https://localhost:5001/api/version/short

# MngDataGateway
curl -f -k https://localhost:5010/api/version/short  # ⚠️ /api/v1/health olmalı

# MngHub
curl -f http://localhost:5020/api/version/short  # ⚠️ /health olmalı

# MngUI
curl -f http://localhost:3000
```

**Not:** Deployment script'indeki health check endpoint'leri de güncellenmeli!

---

**Son Güncelleme:** 1 Ocak 2026

