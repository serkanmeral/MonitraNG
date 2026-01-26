---
title: "MkDocs Docker Deployment Rehberi"
category: "deployment"
tags: ["mkdocs", "docker", "deployment", "documentation"]
service: "MkDocs"
difficulty: "intermediate"
estimated_time: "10 dakika"
language: "tr"
priority: 1
---

# MkDocs Docker Deployment Rehberi

**Durum:** ✅ Dockerfile ve Docker Compose entegrasyonu tamamlandı  
**Tarih:** 16 Ocak 2026

---

## 📋 Genel Bakış

MkDocs dokümantasyonu hem **lokal (Docker Desktop)** hem de **remote sunucu** üzerinde Docker container olarak çalıştırılabilir.

### Özellikler

- ✅ Multi-stage Docker build (Python build + Nginx production)
- ✅ Docker Compose entegrasyonu
- ✅ Lokal development desteği
- ✅ Production deployment desteği
- ✅ Health check endpoint
- ✅ Static site serving (Nginx)

---

## 🐳 Dockerfile Yapısı

### Stage 1: Build
- **Image:** `python:3.11-slim`
- **İşlemler:**
  - Python dependencies install (`pip install -r requirements.txt`)
  - MkDocs site build (`mkdocs build`)
  - Output: `site/` klasörü

### Stage 2: Production
- **Image:** `nginx:alpine`
- **İşlemler:**
  - Built site'i nginx'e kopyala
  - Port 80'de serve et
  - Health check endpoint

---

## 📁 Dosya Yapısı

```
docs/
├── Dockerfile          # Multi-stage build
├── .dockerignore       # Docker ignore patterns
├── mkdocs.yml          # MkDocs configuration
├── requirements.txt    # Python dependencies
└── content/           # Documentation source
```

---

## 🚀 Lokal Çalıştırma (Docker Desktop)

### Yöntem 1: Docker Compose (Önerilen)

**Adımlar:**

1. **Docker Compose ile çalıştır:**
   ```bash
   cd ApplicationResources/mng_apps
   docker compose up -d mkdocs
   ```

2. **Dokümantasyona eriş:**
   - URL: `http://localhost:6010`
   - Tarayıcıda açın

3. **Logları kontrol et:**
   ```bash
   docker compose logs -f mkdocs
   ```

4. **Durdur:**
   ```bash
   docker compose stop mkdocs
   ```

### Yöntem 2: Docker Run (Standalone)

**Adımlar:**

1. **Image build et:**
   ```bash
   cd docs
   docker build -t mkdocs:latest .
   ```

2. **Container çalıştır:**
   ```bash
   docker run -d \
     --name mkdocs \
     -p 6010:80 \
     --network mng_common_mng_network \
     mkdocs:latest
   ```

3. **Dokümantasyona eriş:**
   - URL: `http://localhost:6010`

4. **Durdur:**
   ```bash
   docker stop mkdocs
   docker rm mkdocs
   ```

---

## 🌐 Remote Sunucu Deployment

### Yöntem 1: Docker Compose (Production)

**Adımlar:**

1. **Kodu sunucuya çek:**
   ```bash
   git pull origin main
   ```

2. **Docker Compose ile deploy:**
   ```bash
   cd ApplicationResources/mng_apps
   docker compose -f docker-compose.production.yml up -d mkdocs
   ```

3. **Nginx reverse proxy yapılandır (opsiyonel):**
   ```nginx
   server {
       listen 80;
       server_name docs.monitra.local;
       
       location / {
           proxy_pass http://localhost:6010;
           proxy_set_header Host $host;
           proxy_set_header X-Real-IP $remote_addr;
       }
   }
   ```

### Yöntem 2: Standalone Docker

**Adımlar:**

1. **Image build et:**
   ```bash
   cd docs
   docker build -t mkdocs:latest .
   ```

2. **Container çalıştır:**
   ```bash
   docker run -d \
     --name mkdocs \
     -p 6010:80 \
     --restart unless-stopped \
     mkdocs:latest
   ```

3. **Firewall ayarları:**
   ```bash
   # Port 6010'u aç (gerekirse)
   sudo ufw allow 6010/tcp
   ```

---

## 🔄 Güncelleme Süreci

### Dokümantasyon Güncellemesi

1. **Dokümantasyonu güncelle:**
   ```bash
   # Markdown dosyalarını düzenle
   # docs/content/ klasöründe değişiklikler yap
   ```

2. **Image'i yeniden build et:**
   ```bash
   cd ApplicationResources/mng_apps
   docker compose build mkdocs
   ```

3. **Container'ı yeniden başlat:**
   ```bash
   docker compose up -d mkdocs
   ```

### Otomatik Güncelleme (CI/CD)

**GitLab CI/CD Pipeline:**

MkDocs Docker deployment için `.gitlab-ci.yml` dosyasına job eklenebilir:

```yaml
# MkDocs Docker Build and Deploy
build-mkdocs-docker:
  stage: build-docker
  image: docker:latest
  services:
    - docker:dind
  variables:
    DOCKER_DRIVER: overlay2
  before_script:
    - docker login -u $CI_REGISTRY_USER -p $CI_REGISTRY_PASSWORD $CI_REGISTRY
  script:
    - cd docs
    - docker build -t $CI_REGISTRY_IMAGE/mkdocs:$CI_COMMIT_SHA .
    - docker tag $CI_REGISTRY_IMAGE/mkdocs:$CI_COMMIT_SHA $CI_REGISTRY_IMAGE/mkdocs:latest
    - docker push $CI_REGISTRY_IMAGE/mkdocs:$CI_COMMIT_SHA
    - docker push $CI_REGISTRY_IMAGE/mkdocs:latest
  only:
    changes:
      - docs/**/*
      - .gitlab-ci.yml
  tags:
    - docker

deploy-mkdocs-docker:
  stage: deploy
  image: docker:latest
  services:
    - docker:dind
  variables:
    DOCKER_DRIVER: overlay2
  before_script:
    - docker login -u $CI_REGISTRY_USER -p $CI_REGISTRY_PASSWORD $CI_REGISTRY
  script:
    - |
      ssh $DEPLOY_USER@$DEPLOY_HOST << 'EOF'
        cd /root/MonitraNG
        docker pull $CI_REGISTRY_IMAGE/mkdocs:$CI_COMMIT_SHA
        docker stop mkdocs || true
        docker rm mkdocs || true
        docker run -d \
          --name mkdocs \
          -p 6010:80 \
          --restart unless-stopped \
          --network mng_common_mng_network \
          $CI_REGISTRY_IMAGE/mkdocs:$CI_COMMIT_SHA
      EOF
  only:
    - main
  when: manual
  tags:
    - docker
```

**Not:** Bu job'lar `.gitlab-ci.yml` dosyasına eklenebilir. Şu anda manuel deployment yapılabilir.

---

## 🔍 Troubleshooting

### Problem: Container başlamıyor

**Çözüm:**
```bash
# Logları kontrol et
docker compose logs mkdocs

# Container'ı yeniden build et
docker compose build --no-cache mkdocs
docker compose up -d mkdocs
```

### Problem: Port 6010 kullanımda

**Çözüm:**
```bash
# Port'u değiştir (docker-compose.yml'de)
ports:
  - "6011:80"  # Farklı port kullan

# Veya mevcut container'ı durdur
docker compose stop mkdocs
```

### Problem: Site görünmüyor

**Çözüm:**
```bash
# Health check yap
curl http://localhost:6010

# Container durumunu kontrol et
docker compose ps mkdocs

# Container'a gir ve kontrol et
docker compose exec mkdocs sh
ls -la /usr/share/nginx/html
```

---

## 📊 Performans

### Resource Kullanımı

- **Memory:** ~50-100 MB
- **CPU:** Minimal (static site)
- **Disk:** ~100-200 MB (image)

### Ölçeklendirme

MkDocs static site olduğu için:
- **Horizontal scaling:** Nginx load balancer ile
- **CDN:** Static asset'ler için CDN kullanılabilir
- **Caching:** Browser ve proxy cache kullanılabilir

---

## 🔐 Güvenlik

### Best Practices

1. **Read-only container:**
   ```yaml
   read_only: true
   tmpfs:
     - /tmp
   ```

2. **Non-root user:**
   - Nginx alpine image zaten non-root kullanıyor

3. **Security headers:**
   - Nginx config'de security header'lar eklenebilir

---

## 📝 İlgili Linkler

- [MkDocs Setup Guide](./MKDOCS_SETUP.md)
- [MkDocs Kullanım](./MKdocs_KULLANIM.md)
- [Docker Compose Usage](../../ApplicationResources/mng_apps/README.md)

---

**Son Güncelleme:** 16 Ocak 2026
