# MngKeeper Environment Configuration Check

**Tarih:** 7 Ocak 2026  
**Durum:** ⚠️ **ŞİFRELER GÜNCELLENMELİ**

---

## 📋 Genel Bakış

MngKeeper servisinin environment configuration'ı kontrol edildi. Bazı bileşenlerin şifreleri eski değerler içeriyor ve `Admin2026MonitraNG` ile güncellenmelidir.

---

## 🔍 Mevcut Durum Analizi

### 1. MongoDB Configuration

**Docker Compose (.env):**
```bash
MONGO_CONNECTION_STRING=mongodb://admin:admin123@mongo:27017
```

**MongoDB Container (gerçek değer):**
```bash
MONGO_INITDB_ROOT_USERNAME=admin
MONGO_INITDB_ROOT_PASSWORD=admin123  # ❌ ESKİ DEĞER
```

**Durum:** ⚠️ MongoDB şifresi `.env` dosyasında ve container'da eski (`admin123`). Yeni şifre: `Admin2026MonitraNG`

**Güncellenmesi Gerekenler:**
- ✅ `.env` dosyası: `MONGO_CONNECTION_STRING`
- ✅ MongoDB container'ında admin kullanıcısının şifresi

---

### 2. Keycloak Configuration

**Docker Compose (.env):**
```bash
KEYCLOAK_BASE_URL=http://keycloak:8080
KEYCLOAK_ADMIN_USERNAME=admin
KEYCLOAK_ADMIN_PASSWORD=admin123  # ❌ ESKİ DEĞER
KEYCLOAK_CLIENT_ID=mng-keeper-admin
KEYCLOAK_CLIENT_SECRET=2NnraWfHb3SYfbXnhUM8pXJt9E1IOnjV
```

**Keycloak Container (gerçek değer):**
```bash
KEYCLOAK_ADMIN=admin
KEYCLOAK_ADMIN_PASSWORD=!2345Qawsedrf*  # ⚠️ FARKLI DEĞER (container'da farklı)
```

**Durum:** ⚠️ Keycloak şifresi `.env` dosyasında `admin123`, ancak container'da `!2345Qawsedrf*`. İkisi de eski değerler. Yeni şifre: `Admin2026MonitraNG`

**Güncellenmesi Gerekenler:**
- ✅ `.env` dosyası: `KEYCLOAK_ADMIN_PASSWORD`
- ✅ Keycloak container'ında admin şifresi (zaten kullanıcı UI'dan güncellemiş)

---

### 3. Redis Configuration

**Docker Compose (.env):**
```bash
REDIS_CONNECTION_STRING=redis:6379,password=redis123  # ❌ ESKİ DEĞER
```

**Redis Container:**
- Container'da `--requirepass` ile şifre korumalı
- Gerçek şifre kontrol edilemedi (komut hatası)

**Durum:** ⚠️ Redis şifresi `.env` dosyasında eski (`redis123`). Yeni şifre: `Admin2026MonitraNG`

**Güncellenmesi Gerekenler:**
- ✅ `.env` dosyası: `REDIS_CONNECTION_STRING`
- ✅ Redis container'ında şifre (docker-compose.yml'de `REDIS_PASSWORD` environment variable'ı ile)

---

### 4. RabbitMQ Configuration

**Docker Compose (.env):**
```bash
RABBITMQ_HOST=rabbitmq
RABBITMQ_PORT=5672
RABBITMQ_USERNAME=admin
RABBITMQ_PASSWORD=admin123  # ❌ ESKİ DEĞER
RABBITMQ_VIRTUALHOST=/
```

**RabbitMQ Container:**
- `admin` kullanıcısı mevcut
- Gerçek şifre kontrol edilemedi

**Durum:** ⚠️ RabbitMQ şifresi `.env` dosyasında eski (`admin123`). Yeni şifre: `Admin2026MonitraNG`

**Güncellenmesi Gerekenler:**
- ✅ `.env` dosyası: `RABBITMQ_PASSWORD`
- ✅ RabbitMQ container'ında admin kullanıcısının şifresi

---

### 5. MinIO Configuration

**Docker Compose (.env):**
```bash
MINIO_ENDPOINT=minio:9000
MINIO_ACCESS_KEY=admin
MINIO_SECRET_KEY=S/XxNysk65SJB4EErrp61Vx++etG15hPJk+g7/k6TMc=  # ⚠️ ŞİFRELENMİŞ DEĞER
MINIO_USE_SSL=false
MINIO_REGION=us-east-1
```

**Durum:** ⚠️ MinIO secret key şifrelenmiş görünüyor. Gerçek değer kontrol edilmeli.

**Güncellenmesi Gerekenler:**
- ✅ `.env` dosyası: `MINIO_SECRET_KEY` (gerçek değer kontrol edilmeli)
- ✅ MinIO container'ında root şifresi

---

### 6. MQTT Configuration

**Docker Compose (.env):**
```bash
MQTT_BROKER_HOST=mosquitto
MQTT_BROKER_PORT=1883
MQTT_USERNAME=monitrang
MQTT_PASSWORD=EtGMtRD+yMTaYb6fTb7/MvaNrqxz/V7fO32Q6Bs+Sms=  # ⚠️ ŞİFRELENMİŞ DEĞER
MQTT_TOPIC_PREFIX=MNG
```

**Durum:** ⚠️ MQTT şifresi şifrelenmiş görünüyor. Bu normal olabilir (base64 encoded).

---

## 📊 Özet Tablo

| Bileşen | .env Değeri | Container Değeri | Durum | Güncellenecek |
|---------|-------------|------------------|-------|---------------|
| **MongoDB** | `admin123` | `admin123` | ⚠️ Eski | ✅ |
| **Keycloak** | `admin123` | `!2345Qawsedrf*` | ⚠️ Eski/Farklı | ✅ (UI'dan güncellendi) |
| **Redis** | `redis123` | ? | ⚠️ Eski | ✅ |
| **RabbitMQ** | `admin123` | ? | ⚠️ Eski | ✅ |
| **MinIO** | Şifrelenmiş | ? | ⚠️ Kontrol edilmeli | ⚠️ |
| **MQTT** | Şifrelenmiş | ? | ✅ Normal | ❌ |

---

## 🔧 Güncellenmesi Gerekenler

### 1. .env Dosyası Güncellemesi

`ApplicationResources/mng_apps/.env` dosyasında şu değerler güncellenmelidir:

```bash
# MongoDB
MONGO_CONNECTION_STRING=mongodb://admin:Admin2026MonitraNG@mongo:27017

# Keycloak
KEYCLOAK_ADMIN_PASSWORD=Admin2026MonitraNG

# Redis
REDIS_CONNECTION_STRING=redis:6379,password=Admin2026MonitraNG

# RabbitMQ
RABBITMQ_PASSWORD=Admin2026MonitraNG

# MinIO (gerçek değer kontrol edilmeli)
# MINIO_SECRET_KEY=Admin2026MonitraNG
```

### 2. Container Şifreleri Güncelleme

**Not:** `.env` dosyasını güncellemek yeterli değil. Container'ların kendi şifreleri de güncellenmelidir:

1. **MongoDB**: `mongosh` ile admin şifresi güncellenmeli
2. **Keycloak**: ✅ Zaten kullanıcı UI'dan güncellemiş (`Admin2026MonitraNG`)
3. **Redis**: `docker-compose.yml` içinde `REDIS_PASSWORD` environment variable'ı ile ayarlanmış (container restart gerekebilir)
4. **RabbitMQ**: `rabbitmqctl change_password` ile admin şifresi güncellenmeli
5. **MinIO**: MinIO UI'dan veya CLI ile root şifresi güncellenmeli

---

## ✅ Yapılacaklar Listesi

- [ ] `.env` dosyasındaki MongoDB connection string'i güncelle
- [ ] MongoDB container'ında admin şifresini `Admin2026MonitraNG` ile güncelle
- [ ] `.env` dosyasındaki Keycloak admin şifresini güncelle
- [ ] `.env` dosyasındaki Redis connection string'i güncelle
- [ ] Redis container'ının şifresini kontrol et ve güncelle (gerekirse restart)
- [ ] `.env` dosyasındaki RabbitMQ şifresini güncelle
- [ ] RabbitMQ container'ında admin şifresini `Admin2026MonitraNG` ile güncelle
- [ ] MinIO secret key'in gerçek değerini kontrol et
- [ ] MinIO container'ının root şifresini kontrol et ve güncelle
- [ ] MngKeeper container'ını restart et (yeni environment variable'ları yüklemek için)
- [ ] Tüm servislerin bağlantılarını test et

---

## 🔄 Güncelleme Sonrası Test

Environment variable'ları güncelledikten sonra:

1. **MngKeeper Health Check**: Servis başarıyla başlıyor mu?
2. **MongoDB Connection**: MngKeeper MongoDB'ye bağlanabiliyor mu?
3. **Keycloak Connection**: MngKeeper Keycloak'a bağlanabiliyor mu?
4. **Redis Connection**: MngKeeper Redis'e bağlanabiliyor mu?
5. **RabbitMQ Connection**: MngKeeper RabbitMQ'ya bağlanabiliyor mu?
6. **MinIO Connection**: MngKeeper MinIO'ya bağlanabiliyor mu?

---

## 📝 Notlar

1. **Şifrelenmiş Değerler**: MinIO ve MQTT şifreleri base64 veya benzer bir şekilde şifrelenmiş görünüyor. Bunların gerçek değerlerini kontrol etmek gerekebilir.

2. **Container Restart**: `.env` dosyasını güncelledikten sonra ilgili container'ları restart etmek gerekebilir:
   ```bash
   docker compose -f ApplicationResources/mng_apps/docker-compose.production.yml restart mngkeeper
   ```

3. **Şifre Güncelleme Sırası**: Önce container'ların şifrelerini güncelle, sonra `.env` dosyasını güncelle, sonra MngKeeper'ı restart et.

4. **Yedekleme**: Şifre güncelleme işleminden önce yedek alınması önerilir.

---

**Son Güncelleme:** 7 Ocak 2026
