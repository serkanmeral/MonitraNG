# MngGateway - Docker Compose Kullanımı

## 📋 Docker Compose Dosyaları

MngGateway için iki farklı docker-compose dosyası kullanılıyor:

### 1. Development: `docker-compose.yml`

**Konum:** `ApplicationResources/mng_apps/docker-compose.yml`

**Kullanım:**
```bash
cd ApplicationResources/mng_apps
docker-compose up -d mnggateway
```

**Özellikler:**
- Development ortamı için
- Hard-coded değerler
- Localhost port mapping
- Debug logging enabled

**Port Mapping:**
- `5000:5000` (HTTP)
- `443:443` (HTTPS)

### 2. Production: `docker-compose.production.yml`

**Konum:** `ApplicationResources/mng_apps/docker-compose.production.yml`

**Kullanım:**
```bash
cd ApplicationResources/mng_apps
docker-compose -f docker-compose.production.yml up -d mnggateway
```

**Özellikler:**
- Production ortamı için
- Environment variable'lar kullanılır
- Resource limits tanımlı
- Production logging

**Environment Variables:**
- `GATEWAY_PORT` - Gateway port (default: 5000)
- `OPENAPI_SERVER_PATH` - OpenAPI server path
- `KEYCLOAK_BASE_URL` - KeyCloak base URL
- `CORS_ALLOWED_ORIGIN_1`, `CORS_ALLOWED_ORIGIN_2` - CORS origins
- `RATE_LIMIT_ENABLED` - Rate limiting enabled (default: true)
- `RATE_LIMIT_ANONYMOUS` - Anonymous limit (default: 30)
- `RATE_LIMIT_AUTHENTICATED` - Authenticated limit (default: 100)
- `RATE_LIMIT_ADMIN` - Admin limit (default: 500)
- `MNGKEEPER_URL` - MngKeeper URL
- `MNGDATAGATEWAY_URL` - MngDataGateway URL
- `MNGHUB_URL` - MngHub URL
- `MNGREACTOR_URL` - MngReactor URL
- `CERTIFICATE_DNS` - Certificate DNS name (default: mnggateway)

## 🚀 Hızlı Başlangıç

### Development

```bash
# Tüm servislerle birlikte
cd ApplicationResources/mng_apps
docker-compose up -d

# Sadece gateway
docker-compose up -d mnggateway
```

### Production

```bash
# Environment variables ile
export GATEWAY_PORT=5000
export KEYCLOAK_BASE_URL=http://keycloak:8080
export CORS_ALLOWED_ORIGIN_1=https://app.monitra.local

# Tüm servislerle birlikte
cd ApplicationResources/mng_apps
docker-compose -f docker-compose.production.yml up -d

# Sadece gateway
docker-compose -f docker-compose.production.yml up -d mnggateway
```

## 🔍 Test

```bash
# Health check
curl http://localhost:5000/health

# Gateway üzerinden MngKeeper test
curl http://localhost:5000/keeper/api/domain \
  -H "Authorization: Bearer YOUR_TOKEN"
```

## 📝 Notlar

- Development dosyası hard-coded değerler kullanır
- Production dosyası environment variable'lar kullanır
- Her iki dosyada da aynı network kullanılır: `mng_common_mng_network`
- Gateway, backend servislere bağımlıdır (`depends_on`)

