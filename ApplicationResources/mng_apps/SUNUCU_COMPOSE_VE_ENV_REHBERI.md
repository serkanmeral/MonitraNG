# Sunucu: docker-compose.production.yml ve .env Rehberi

Sunucuda (`app.monitrang.com` / production) kullandığınız **compose dosyası** ve **.env** içinde görmeniz gerekenlerin özeti.

---

## 1. Compose dosyası (sunucuda hangi dosya?)

- **Dosya:** `docker-compose.production.yml` (veya sunucuda kullandığınız compose dosyasının adı)
- **Konum:** Genelde `ApplicationResources/mng_apps/` veya sunucuda clone ettiğiniz repo yolunda aynı yapı.

Compose içinde şunlar olmalı:

| Bölüm | Açıklama |
|--------|----------|
| **Servisler** | `mnggateway`, `mngkeeper`, `mngdatagateway`, `mnghub`, `mngllm`, `mngscheduler`, `mngadmin`, `mngnotifier`, `mngui`, `mngdomainui`, `ollama` (isteğe bağlı) |
| **Ortam değişkenleri** | Her serviste `ASPNETCORE_ENVIRONMENT=Production` ve ilgili `Mng*Settings__...` değişkenleri (compose içinde `${VAR}` ile .env’den okunur) |
| **Ağ** | `networks: mng_common_mng_network` (external: true — önce `docker network create mng_common_mng_network` gerekir) |
| **MngKeeper – şifre sıfırlama linki** | `MngKeeperSettings__UiBaseUrl=${MNG_KEEPER_UI_BASE_URL:-https://app.monitrang.com}` (maildeki linkin base URL’i) |

Compose’u çalıştırırken:

```bash
docker compose -f docker-compose.production.yml --env-file .env up -d
```

`.env` dosyası aynı dizinde olmalı.

---

## 2. .env dosyası (sunucuda görmeniz gerekenler)

Sunucuda **mutlaka doldurulması gereken** ve **production’a göre ayarlanması gereken** değişkenler:

### 2.1 Genel

| Değişken | Örnek / Açıklama |
|----------|-------------------|
| `ENVIRONMENT` | `Production` |
| `VERSION` | `latest` veya release etiketi |
| `DOMAIN` | `monitrang.com` |
| `OPENAPI_SERVER_PATH` | Dış dünyadan API base URL (örn. `https://api.monitrang.com` veya `https://monitrang.com`) |

### 2.2 Veritabanı ve altyapı

| Değişken | Açıklama |
|----------|----------|
| `MONGO_CONNECTION_STRING` | MongoDB bağlantı (örn. `mongodb://admin:SIFRE@mongo:27017`) |
| `MONGO_DATABASE_NAME` | `mngkeeper` (veya kullandığınız DB adı) |
| `KEYCLOAK_BASE_URL` | Container içinden Keycloak (örn. `http://keycloak:8080`) |
| `KEYCLOAK_PATH_PREFIX` | `/keycloak` (Keycloak path prefix) |
| `KEYCLOAK_ADMIN_USERNAME` | Keycloak admin kullanıcı |
| `KEYCLOAK_ADMIN_PASSWORD` | Keycloak admin şifre |
| `KEYCLOAK_CLIENT_ID` | `mng-keeper-admin` (veya sizin client id) |
| `KEYCLOAK_CLIENT_SECRET` | Keycloak client secret |
| `KEYCLOAK_DEFAULT_ADMIN_PASSWORD` | Varsayılan admin şifre (domain oluşturma vb.) |
| `REDIS_CONNECTION_STRING` | Redis (örn. `redis:6379,password=...`) |
| `RABBITMQ_HOST`, `RABBITMQ_PORT`, `RABBITMQ_USERNAME`, `RABBITMQ_PASSWORD`, `RABBITMQ_VIRTUALHOST` | RabbitMQ ayarları |
| `MQTT_BROKER_HOST`, `MQTT_BROKER_PORT`, `MQTT_USERNAME`, `MQTT_PASSWORD`, `MQTT_TOPIC_PREFIX` | MQTT (Mosquitto) ayarları |
| `MINIO_ENDPOINT`, `MINIO_ACCESS_KEY`, `MINIO_SECRET_KEY`, `MINIO_USE_SSL`, `MINIO_REGION` | MinIO ayarları |

### 2.3 CORS ve dış erişim

| Değişken | Açıklama |
|----------|----------|
| `CORS_ALLOWED_ORIGIN_1` | Tarayıcıdan erişen frontend (örn. `https://app.monitrang.com`) |
| `CORS_ALLOWED_ORIGIN_2` | İkinci origin (gerekirse) |

### 2.4 MngKeeper – lisans ve şifre sıfırlama

| Değişken | Açıklama |
|----------|----------|
| `MNGKEEPER_LICENSE_MASTER_KEY` | En az 32 karakter; Create Domain / trial lisans için zorunlu (`openssl rand -base64 32` ile üretebilirsiniz) |
| **`MNG_KEEPER_UI_BASE_URL`** | **Şifre sıfırlama mailindeki linkin base URL’i.** Production’da: `https://app.monitrang.com` |

### 2.5 Servis URL’leri (container içi)

| Değişken | Örnek |
|----------|--------|
| `MNGKEEPER_URL` | `https://mngkeeper:5001` veya `http://mngkeeper:5001` (compose’taki scheme’e göre) |
| `MNGDATAGATEWAY_URL` | `https://mngdatagateway:5010` |
| `MNGHUB_URL` | `http://mnghub:5020` |

### 2.6 SMTP (MngNotifier – e-posta)

| Değişken | Açıklama |
|----------|----------|
| `SMTP_HOST` | Sunucuda SMTP (Linux’ta genelde `172.17.0.1` veya `host.docker.internal`; compose’ta `extra_hosts` ile) |
| `SMTP_PORT` | `25` (veya kullandığınız port) |
| `SMTP_FROM_EMAIL` | `noreply@monitrang.com` (veya sizin adres) |
| `SMTP_FROM_NAME` | `MonitraNG` |
| `SMTP_USERNAME`, `SMTP_PASSWORD` | SMTP auth gerekirse |

### 2.7 UI / frontend

| Değişken | Açıklama |
|----------|----------|
| `GATEWAY_URL` | Tarayıcıdan erişilen gateway (örn. `https://app.monitrang.com`; MngUI build-time) |
| `KEEPER_URL`, `DATAGATEWAY_URL`, `HUB_URL` | UI container’ı için (compose’ta kullanılıyor) |
| `CERTIFICATE_DNS` | Sertifika DNS (örn. `mngkeeper`) |

### 2.8 Opsiyonel

| Değişken | Açıklama |
|----------|----------|
| `MNGLLM_JWT_AUTHORITY` | MngLLM JWT doğrulama (çoğu kurulumda boş) |
| `OLLAMA_BASE_URL` | Ollama (MngLLM) — `http://ollama:11434` |

---

## 3. Hızlı kontrol listesi (sunucuda)

- [ ] `.env` dosyası compose ile aynı dizinde (veya `--env-file` ile verildi).
- [ ] `MONGO_CONNECTION_STRING`, Keycloak, Redis, RabbitMQ, MinIO, MQTT değerleri production’a göre.
- [ ] `MNGKEEPER_LICENSE_MASTER_KEY` set (en az 32 karakter).
- [ ] **`MNG_KEEPER_UI_BASE_URL=https://app.monitrang.com`** (şifre sıfırlama mail linki için).
- [ ] `CORS_ALLOWED_ORIGIN_1=https://app.monitrang.com` (tarayıcıdan gelen istekler için).
- [ ] `OPENAPI_SERVER_PATH` ve `GATEWAY_URL` dış erişim URL’lerinize göre.
- [ ] `docker network create mng_common_mng_network` (bir kez) yapıldı.
- [ ] Compose’ta MngKeeper servisinde `MngKeeperSettings__UiBaseUrl=${MNG_KEEPER_UI_BASE_URL:-https://app.monitrang.com}` satırı var.

---

## 4. Referans

- **Compose:** `ApplicationResources/mng_apps/docker-compose.production.yml`
- **Örnek env:** `ApplicationResources/mng_apps/env.example` (sunucuda `.env` oluştururken kopyalayıp gerçek değerlerle doldurun; şifreleri repo’ya koymayın).
