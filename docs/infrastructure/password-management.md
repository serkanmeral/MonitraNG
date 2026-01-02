# Password Management

**Tarih:** 2 Ocak 2026  
**Durum:** Test ortamı için standartlaştırıldı

---

## 📋 Genel Bakış

Tüm infrastructure servisleri için varsayılan şifre standartlaştırıldı. Test ortamında tüm servisler aynı şifreyi kullanır, ancak her servis için ayrı environment variable tanımları mevcuttur.

---

## 🔐 Varsayılan Şifre

**Test Ortamı Şifresi:** `!2345Qawsedrf*`

> ⚠️ **Not:** Production ortamlarında her servis için farklı güçlü şifreler kullanılmalıdır.

---

## 📁 Dosya Yapısı

### Environment Dosyaları

- **`.env`** → Gerçek şifreler (`.gitignore`'da, commit edilmez)
- **`env.example`** → Şablon dosya (şifreler `CHANGE_ME` olarak işaretlenmiş)

### Konumlar

- `ApplicationResources/mng_common/.env` → Infrastructure servisleri için
- `ApplicationResources/mng_apps/.env` → Application servisleri için

---

## 🔧 Servisler ve Şifre Değişkenleri

### Infrastructure Servisleri (mng_common)

| Servis | Environment Variable | Varsayılan Değer |
|--------|---------------------|------------------|
| MongoDB | `MONGO_ROOT_PASSWORD` | `!2345Qawsedrf*` |
| MongoDB Express | `MONGO_EXPRESS_PASSWORD` | `!2345Qawsedrf*` |
| Keycloak | `KEYCLOAK_ADMIN_PASSWORD` | `!2345Qawsedrf*` |
| PostgreSQL (Keycloak) | `POSTGRES_PASSWORD` | `!2345Qawsedrf*` |
| Redis | `REDIS_PASSWORD` | `!2345Qawsedrf*` |
| RabbitMQ | `RABBITMQ_DEFAULT_PASS` | `!2345Qawsedrf*` |
| MinIO | `MINIO_ROOT_PASSWORD` | `!2345Qawsedrf*` |
| Seq | `SEQ_ADMIN_PASSWORD` | `!2345Qawsedrf*` |
| Node-RED | `NODE_RED_PASSWORD` | `!2345Qawsedrf*` |

### GitLab Servisleri (Dışarıda Tutuldu)

GitLab servisleri bu standartlaştırmadan hariçtir ve kendi şifrelerini kullanır:
- GitLab PostgreSQL: `gitlab123`
- GitLab Redis: `gitlab123`
- GitLab Root: `MonitraNG2026!` (GitLab UI'den değiştirilebilir)

---

## 🚀 Kurulum

### 1. .env Dosyası Oluşturma

```bash
# Otomatik oluşturma (script ile)
cd scripts
chmod +x create-env-file.sh
./create-env-file.sh

# Veya manuel olarak
cd ApplicationResources/mng_common
cp env.example .env
# .env dosyasını düzenle ve CHANGE_ME değerlerini !2345Qawsedrf* ile değiştir
```

### 2. Mevcut Ortamı Güncelleme

**⚠️ UYARI:** Bu işlem mevcut veritabanlarını siler ve yeniden oluşturur!

```bash
# Migration script'i çalıştır
cd scripts
chmod +x migrate-passwords.sh
./migrate-passwords.sh
```

Script şunları yapar:
1. Tüm container'ları durdurur
2. Mevcut volume'ları siler (MongoDB, PostgreSQL, Redis, RabbitMQ, MinIO, Seq)
3. Container'ları yeni şifrelerle yeniden başlatır

> **Not:** GitLab volume'ları silinmez (gitlab-postgres, gitlab-redis, gitlab_config, gitlab_logs, gitlab_data)

---

## 🔄 Connection String'ler

### MongoDB Connection String

```bash
mongodb://admin:!2345Qawsedrf*@mongo:27017
```

URL encoding gerekirse:
```bash
# ! karakteri %21 olur
mongodb://admin:%212345Qawsedrf%2A@mongo:27017
```

### Redis Connection String

```bash
redis:6379,password=!2345Qawsedrf*
```

### RabbitMQ Connection

```
Host: rabbitmq
Port: 5672
Username: admin
Password: !2345Qawsedrf*
```

---

## 📝 Application Servisleri (.env)

Application servisleri için `ApplicationResources/mng_apps/.env` dosyasında şu değişkenler güncellenmelidir:

```bash
# MongoDB
MONGO_CONNECTION_STRING=mongodb://admin:!2345Qawsedrf*@mongo:27017

# Redis
REDIS_CONNECTION_STRING=redis:6379,password=!2345Qawsedrf*

# RabbitMQ
RABBITMQ_PASSWORD=!2345Qawsedrf*

# Keycloak
KEYCLOAK_ADMIN_PASSWORD=!2345Qawsedrf*

# MinIO
MINIO_SECRET_KEY=!2345Qawsedrf*
```

---

## 🔒 Güvenlik Notları

### Test Ortamı
- ✅ Tüm servisler aynı şifreyi kullanabilir
- ✅ Şifreler `.env` dosyasında saklanır (`.gitignore`'da)
- ✅ `env.example` dosyası commit edilir (şifreler `CHANGE_ME`)

### Production Ortamı
- ⚠️ Her servis için **farklı güçlü şifreler** kullanılmalı
- ⚠️ Şifreler güvenli bir secret manager'da saklanmalı (HashiCorp Vault, AWS Secrets Manager, vb.)
- ⚠️ Şifreler düzenli olarak rotate edilmeli
- ⚠️ `.env` dosyası asla commit edilmemeli

---

## 🛠️ Sorun Giderme

### Şifre Değiştirme Sonrası Bağlantı Sorunları

1. **Container'ları yeniden başlat:**
   ```bash
   cd ApplicationResources/mng_common
   docker compose restart
   ```

2. **Connection string'leri kontrol et:**
   - Application servislerinin `.env` dosyalarını kontrol et
   - URL encoding gerekip gerekmediğini kontrol et

3. **Volume'ları kontrol et:**
   ```bash
   docker volume ls
   docker volume inspect mng_common_mongo_data
   ```

### MongoDB Init Script Sorunu

MongoDB init script'i (`mongo-init/init.js`) hala eski şifreyi (`admin123`) kullanıyorsa:
- Bu script MongoDB'nin kendi init mekanizmasından önce çalışır
- `MONGO_INITDB_ROOT_PASSWORD` environment variable'ı kullanılmalı
- Script'teki hardcoded şifre kaldırılabilir veya environment variable kullanılabilir

---

## 📚 İlgili Dosyalar

- `ApplicationResources/mng_common/docker-compose.yml` → Infrastructure servisleri
- `ApplicationResources/mng_common/.env` → Infrastructure şifreleri (gitignore'da)
- `ApplicationResources/mng_common/env.example` → Şablon dosya
- `ApplicationResources/mng_apps/.env` → Application şifreleri (gitignore'da)
- `ApplicationResources/mng_apps/env.example` → Application şablon dosya
- `scripts/create-env-file.sh` → .env dosyası oluşturma script'i
- `scripts/migrate-passwords.sh` → Şifre migration script'i

---

**Son Güncelleme:** 2 Ocak 2026

