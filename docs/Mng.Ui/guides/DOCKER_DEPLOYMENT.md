# Mng.Ui Docker Deployment Rehberi

**Durum:** ✅ Dockerfile ve deployment yapılandırması tamamlandı  
**Tarih:** 30 Aralık 2024  
**Son Güncelleme:** API proxy yapılandırması, port numarası sorunu çözümü

---

## 📋 Genel Bakış

Mng.Ui (Nuxt 3 SPA) için Docker containerization ve deployment yapılandırması.

### Özellikler

- ✅ Multi-stage Docker build (Node.js build + Nginx production)
- ✅ Docker Compose entegrasyonu (development ve production)
- ✅ GitLab CI/CD pipeline entegrasyonu
- ✅ Runtime environment variables (Nuxt runtime config)
- ✅ Health check endpoint
- ✅ Static asset caching
- ✅ SPA routing support

---

## 🐳 Dockerfile Yapısı

### Stage 1: Build
- **Image:** `node:18-alpine`
- **İşlemler:**
  - Dependencies install (`npm ci`)
  - Application build (`npm run build`)
  - Output: `.output/public`

### Stage 2: Production
- **Image:** `nginx:alpine`
- **İşlemler:**
  - Built files'ı nginx'e kopyala
  - Nginx configuration uygula
  - Port 80'de serve et

---

## 📁 Dosya Yapısı

```
Mng.Ui/
├── Dockerfile          # Multi-stage build
├── nginx.conf          # Nginx configuration
└── .dockerignore       # Docker ignore patterns
```

---

## ⚙️ Nginx Konfigürasyonu

### Özellikler

1. **SPA Routing:**
   - Tüm route'lar `index.html`'e yönlendirilir
   - `try_files $uri $uri/ /index.html;`
   - Port numarası korunur (`port_in_redirect off`, `absolute_redirect off`)

2. **API Proxy:**
   - `/api/auth/` → `https://mngkeeper:5001/api/auth/` (HTTPS proxy)
   - `/api/keeper/` → `https://mngkeeper:5001/api/` (HTTPS proxy)
   - SSL sertifika doğrulaması bypass edilir (`proxy_ssl_verify off`)
   - CORS header'ları backend'den geçirilir
   - Authorization header'ı korunur

3. **Static Asset Caching:**
   - CSS, JS, images: 1 yıl cache
   - `Cache-Control: public, immutable`

4. **Gzip Compression:**
   - Text, CSS, JS, JSON için aktif
   - Performans optimizasyonu

5. **Security Headers:**
   - X-Frame-Options
   - X-Content-Type-Options
   - X-XSS-Protection
   - Referrer-Policy

6. **Health Check:**
   - Endpoint: `/health`
   - Response: `200 OK "healthy"`

---

## 🔧 Environment Variables

### Runtime Configuration

Nuxt'un `runtimeConfig` yapısı kullanılıyor. Environment variables build-time'da değil, runtime'da okunur.

### Desteklenen Variables

```bash
# Gateway URL (opsiyonel, şimdilik boş)
GATEWAY_URL=

# Individual Service URLs (şimdilik kullanılıyor)
KEEPER_URL=https://mngkeeper:5001
DATAGATEWAY_URL=https://mngdatagateway:5010
HUB_URL=http://mnghub:5020
REACTOR_URL=https://mngreactor:5003  # Alternatif
SERVER_URL=https://mngdatagateway:5010  # Alternatif
```

### Nuxt Config Mapping

```typescript
runtimeConfig: {
  public: {
    gatewayUrl: process.env.GATEWAY_URL || '',
    keeperUrl: process.env.KEEPER_URL || 'https://localhost:5001',
    reactorUrl: process.env.SERVER_URL || process.env.DATAGATEWAY_URL || process.env.REACTOR_URL || 'https://localhost:5010',
    hubUrl: process.env.HUB_URL || 'http://localhost:5020'
  }
}
```

---

## 🚀 Docker Compose Kullanımı

### Development

```bash
cd ApplicationResources/mng_apps
docker-compose up -d mngui
```

**Port:** `3000:80` (host:container)

**Environment Variables:**
- `KEEPER_URL=https://localhost:5001` (Browser'dan erişilebilir URL)
- `DATAGATEWAY_URL=https://localhost:5010` (Browser'dan erişilebilir URL)
- `HUB_URL=http://localhost:5020` (Browser'dan erişilebilir URL)
- `GATEWAY_URL=` (boş)

**Not:** Frontend browser'da çalıştığı için, environment variable'lar browser'dan erişilebilir URL'ler olmalıdır (`localhost:5001` gibi). Nginx proxy container içinde çalıştığı için Docker network isimlerini (`mngkeeper:5001`) kullanabilir.

### Production

```bash
cd ApplicationResources/mng_apps
docker-compose -f docker-compose.production.yml up -d mngui
```

**Environment Variables:** `.env` dosyasından okunur (veya `env.example`'dan kopyalanır)

---

## 🔄 GitLab CI/CD Pipeline

### Build Job

**Stage:** `build-docker`

**Job:** `build-docker-ui`

**Özellikler:**
- Docker-in-Docker (dind) kullanıyor
- Image tag: `mngui:$CI_COMMIT_SHORT_SHA` ve `mngui:latest`
- Artifacts: 1 gün saklanır

**Not:** Runner'ın `privileged: true` olması gerekiyor (config.toml'da)

### Pipeline Yapısı

```
test-setup → build → test → build-docker → deploy-docs
                                    ↓
                            build-docker-ui
```

---

## 🧪 Test ve Doğrulama

### Local Build Test

```bash
cd Mng.Ui
docker build -t mngui:test -f Dockerfile .
docker run -p 3000:80 -e KEEPER_URL=https://localhost:5001 mngui:test
```

### Health Check

```bash
curl http://localhost:3000/health
# Expected: "healthy"
```

### Docker Compose Test

```bash
cd ApplicationResources/mng_apps
docker-compose up -d mngui
docker-compose logs -f mngui
```

---

## 📝 Notlar

### Gateway vs Direct Services

**Şu anki durum:** Direkt servisler kullanılıyor
- `KEEPER_URL`, `DATAGATEWAY_URL`, `HUB_URL` set ediliyor
- `GATEWAY_URL` boş bırakılıyor

**Gelecekte:** Gateway kullanılabilir
- `GATEWAY_URL` set edilir
- Individual service URL'leri boş bırakılır
- Nuxt config otomatik olarak gateway'i kullanır

### Port Yapılandırması

- **Container:** Port 80 (nginx default)
- **Host:** Port 3000 (development)
- **Production:** Environment variable ile değiştirilebilir (`UI_PORT`)
- **Port Korunması:** Nginx redirect ayarları (`port_in_redirect off`, `absolute_redirect off`) sayesinde sayfa yenilendiğinde port numarası korunur

### Network

- **Network:** `mng_common_mng_network`
- **Dependencies:** `mngkeeper`, `mngdatagateway`, `mnghub`
- Container'lar arası iletişim için aynı network'te olmalı

---

## 🆘 Sorun Giderme

### Build Hatası

**Sorun:** `npm ci` başarısız oluyor

**Çözüm:**
- `package-lock.json` dosyasının güncel olduğundan emin olun
- `.dockerignore` dosyasında `node_modules` ignore ediliyor mu kontrol edin

### Container Başlamıyor

**Sorun:** Container sürekli restart ediyor

**Çözüm:**
- Health check loglarını kontrol edin: `docker-compose logs mngui`
- Nginx configuration'ı test edin: `docker exec mngui nginx -t`

### Environment Variables Çalışmıyor

**Sorun:** API URL'leri yanlış

**Çözüm:**
- Environment variables'ın doğru set edildiğini kontrol edin
- Nuxt runtime config'in doğru çalıştığını doğrulayın
- Browser console'da `$config` objesini kontrol edin
- Browser'dan erişilebilir URL'ler kullanıldığından emin olun (`localhost:5001` gibi)

### API Proxy Sorunları

**Sorun:** 405 Not Allowed veya Failed to fetch hatası

**Çözüm:**
- Nginx proxy yapılandırmasının doğru olduğunu kontrol edin
- Container network'te `mngkeeper` servisinin erişilebilir olduğunu doğrulayın
- SSL sertifika doğrulamasının bypass edildiğini kontrol edin (`proxy_ssl_verify off`)
- Frontend'in `/api/` route'larını kullandığından emin olun (direkt backend URL'leri değil)

### Port Numarası Kayboluyor

**Sorun:** Sayfa yenilendiğinde port numarası kayboluyor (`http://localhost:3000` → `http://localhost`)

**Çözüm:**
- Nginx'te `port_in_redirect off` ve `absolute_redirect off` ayarlarının olduğunu kontrol edin
- Nuxt'ta `app.baseURL` yapılandırmasının doğru olduğunu doğrulayın

---

## 📚 İlgili Dosyalar

- `Mng.Ui/Dockerfile` - Docker build configuration
- `Mng.Ui/nginx.conf` - Nginx server configuration
- `Mng.Ui/.dockerignore` - Docker ignore patterns
- `ApplicationResources/mng_apps/docker-compose.yml` - Development compose
- `ApplicationResources/mng_apps/docker-compose.production.yml` - Production compose
- `.gitlab-ci.yml` - CI/CD pipeline configuration

---

**Son Güncelleme:** 30 Aralık 2024

## 🔄 Son Değişiklikler (30 Aralık 2024)

1. ✅ **API Proxy Yapılandırması Eklendi**
   - `/api/auth/` ve `/api/keeper/` route'ları backend servislere proxy ediliyor
   - SSL sertifika doğrulaması bypass ediliyor
   - CORS header'ları backend'den geçiriliyor

2. ✅ **Port Numarası Sorunu Çözüldü**
   - Nginx redirect ayarları eklendi (`port_in_redirect off`, `absolute_redirect off`)
   - Nuxt baseURL yapılandırması eklendi

3. ✅ **Frontend API Route'ları Güncellendi**
   - Frontend artık direkt backend URL'leri yerine nginx proxy üzerinden istek yapıyor
   - Browser'dan erişilebilir URL'ler kullanılıyor (`localhost:5001` gibi)

4. ✅ **CI/CD Pipeline Güncellendi**
   - `build-frontend` job'u `npm run generate` kullanacak şekilde güncellendi
   - Docker build job'u (`build-docker-ui`) zaten mevcut ve çalışıyor

