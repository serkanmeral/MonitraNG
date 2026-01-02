# GitLab CI/CD Deployment Rehberi

**Son Güncelleme:** 1 Ocak 2026  
**Durum:** ✅ Production'da Başarıyla Çalışıyor

---

## 📋 İçindekiler

1. [Genel Bakış](#genel-bakış)
2. [GitLab CI/CD Variables Yapılandırması](#gitlab-cicd-variables-yapılandırması)
3. [Deployment Job Yapılandırması](#deployment-job-yapılandırması)
4. [Deployment Süreci](#deployment-süreci)
5. [Sorun Giderme](#sorun-giderme)
6. [Başarılı Deployment Örnekleri](#başarılı-deployment-örnekleri)

---

## 🎯 Genel Bakış

MonitraNG projesi için GitLab CI/CD üzerinden otomatik deployment sistemi kurulmuştur. Deployment işlemi manuel olarak tetiklenir ve production sunucusuna SSH üzerinden bağlanarak Docker container'larını build edip başlatır.

### Özellikler

- ✅ **SSH Tabanlı Deployment:** Production sunucusuna SSH ile güvenli bağlantı
- ✅ **Otomatik Git Sync:** Repository'den en son kodu çeker
- ✅ **Docker Build & Deploy:** Image'ları build edip container'ları başlatır
- ✅ **Port Çakışması Önleme:** Otomatik port kontrolü ve düzeltme
- ✅ **Hata Kontrolü:** Detaylı hata mesajları ve kontroller
- ✅ **Zero-Downtime Hazırlığı:** Mevcut container'ları güvenli şekilde durdurur

---

## 🔐 GitLab CI/CD Variables Yapılandırması

Deployment için aşağıdaki GitLab CI/CD Variables'ların tanımlı olması gerekiyor:

### Gerekli Variables

**Settings > CI/CD > Variables** bölümüne gidin ve şu değişkenleri ekleyin:

| Key | Value | Type | Protected | Masked | Açıklama |
|-----|-------|------|-----------|--------|----------|
| `DEPLOY_SSH_PRIVATE_KEY` | (SSH private key içeriği) | Variable | ❌ | ✅ | Production sunucusuna SSH bağlantısı için private key |
| `DEPLOY_SERVER_HOST` | `45.141.151.52` | Variable | ❌ | ❌ | Production sunucu IP adresi veya hostname |
| `DEPLOY_SERVER_USER` | `root` | Variable | ❌ | ❌ | SSH kullanıcı adı (root veya deploy) |
| `DEPLOY_SERVER_PORT` | `22` | Variable | ❌ | ❌ | SSH port numarası (default: 22) |
| `DEPLOY_SERVER_PATH` | `/root/MonitraNG` | Variable | ❌ | ❌ | Production sunucusunda repository'nin bulunduğu tam path |

### SSH Key Oluşturma

Production sunucusunda SSH key oluşturma:

```bash
# Production sunucusunda
ssh-keygen -t ed25519 -C "gitlab-ci-deploy" -f ~/.ssh/gitlab_deploy_key -N ""

# Public key'i authorized_keys'e ekle
cat ~/.ssh/gitlab_deploy_key.pub >> ~/.ssh/authorized_keys
chmod 600 ~/.ssh/authorized_keys

# Private key'i göster (GitLab CI/CD Variables'a eklenecek)
cat ~/.ssh/gitlab_deploy_key
```

**Önemli:**
- `DEPLOY_SSH_PRIVATE_KEY` → **Masked** işaretleyin
- Private key içeriğini tam olarak kopyalayın (başında `-----BEGIN` ve sonunda `-----END` dahil)
- Private key'i güvenli bir yerde saklayın

---

## ⚙️ Deployment Job Yapılandırması

### Job Tanımı

`.gitlab-ci.yml` dosyasında `deploy-services` job'u şu şekilde yapılandırılmıştır:

```yaml
deploy-services:
  stage: deploy
  image: alpine:latest
  tags:
    - docker
  before_script:
    - apk add --no-cache openssh-client
    - eval $(ssh-agent -s)
    - echo "$DEPLOY_SSH_PRIVATE_KEY" | tr -d '\r' | ssh-add -
    - mkdir -p ~/.ssh
    - chmod 700 ~/.ssh
    - ssh-keyscan -H $DEPLOY_SERVER_HOST >> ~/.ssh/known_hosts
  script:
    # Environment variables kontrolü
    # SSH ile production sunucusuna bağlanma
    # Git sync, Docker build ve deploy
  environment:
    name: production
    url: http://$DEPLOY_SERVER_HOST
  rules:
    - if: $CI_COMMIT_MESSAGE =~ /\[skip ci\]|\[ci skip\]/i
      when: never
    - if: $CI_COMMIT_BRANCH == "main"
      when: manual
  allow_failure: true
  timeout: 30m
```

### Özellikler

- **Manuel Tetikleme:** Sadece `main` branch'inde ve manuel olarak çalışır
- **Pipeline Skip:** `[skip ci]` veya `[ci skip]` ile atlanabilir
- **Allow Failure:** Pipeline'ı bloklamaz (deploy manuel olduğu için)
- **Timeout:** 30 dakika (Docker build süresi için yeterli)

---

## 🚀 Deployment Süreci

### 1. Pipeline Tetikleme

Deployment için:

1. `main` branch'ine push yapın
2. Pipeline tamamlandığında GitLab UI'dan `deploy-services` job'unu manuel olarak çalıştırın

### 2. Deployment Adımları

Job çalıştığında şu adımlar otomatik olarak gerçekleşir:

#### Adım 1: Environment Variables Kontrolü
- `DEPLOY_SERVER_HOST`, `DEPLOY_SERVER_USER`, `DEPLOY_SERVER_PATH` kontrol edilir
- Eksik değişken varsa hata verilir

#### Adım 2: SSH Bağlantısı
- SSH agent başlatılır
- Private key eklenir
- Production sunucusuna bağlanılır

#### Adım 3: Git Sync
- Production sunucusunda repository'ye gidilir
- `git fetch origin` ile son değişiklikler çekilir
- `git reset --hard origin/main` ile main branch'e resetlenir

#### Adım 4: Port Mapping Kontrolü
- `docker-compose.production.yml` dosyasında `443:443` mapping'i kontrol edilir
- Varsa otomatik olarak `5443:443`'e düzeltilir (port çakışmasını önlemek için)

#### Adım 5: Mevcut Container'ları Durdurma
- `docker compose down` ile mevcut container'lar durdurulur
- Port 443/5443 kullanan container'lar kontrol edilir ve durdurulur

#### Adım 6: Docker Build & Deploy
- `docker compose up -d --build` ile image'lar build edilir ve container'lar başlatılır
- Tüm servisler başlatılır

#### Adım 7: Durum Kontrolü
- `docker compose ps` ile container durumları kontrol edilir

---

## 🔧 Sorun Giderme

### Sorun 1: Environment Variable Eksik

**Hata:**
```
ERROR: DEPLOY_SERVER_PATH is not set!
```

**Çözüm:**
- GitLab CI/CD Variables'a `DEPLOY_SERVER_PATH` ekleyin
- Değer: Production sunucusunda repository'nin tam path'i (örn: `/root/MonitraNG`)

### Sorun 2: SSH Bağlantı Hatası

**Hata:**
```
Permission denied (publickey)
```

**Çözüm:**
1. `DEPLOY_SSH_PRIVATE_KEY` değişkeninin doğru olduğundan emin olun
2. Production sunucusunda public key'in `authorized_keys` dosyasında olduğunu kontrol edin
3. SSH key permissions'ı kontrol edin: `chmod 600 ~/.ssh/authorized_keys`

### Sorun 3: Port 443 Çakışması

**Hata:**
```
Bind for 0.0.0.0:443 failed: port is already allocated
```

**Çözüm:**
- Script otomatik olarak `443:443` mapping'ini `5443:443`'e düzeltir
- Eğer hala sorun varsa, production sunucusunda port 443'ü kullanan servisi durdurun (ör: Nginx)

### Sorun 4: Git Repository Bulunamadı

**Hata:**
```
ERROR: /root/MonitraNG is not a git repository!
```

**Çözüm:**
- Production sunucusunda repository'yi clone edin:
  ```bash
  cd /root
  git clone <repo-url> MonitraNG
  ```

### Sorun 5: Docker Build Hatası

**Hata:**
```
Error response from daemon: pull access denied
```

**Çözüm:**
- Image'lar local build edilmesi gereken image'lar (Docker Hub'da yok)
- `docker compose up -d --build` komutu otomatik olarak build eder
- Eğer hala sorun varsa, production sunucusunda Docker'ın çalıştığını kontrol edin

---

## ✅ Başarılı Deployment Örnekleri

### Örnek 1: İlk Deployment

```
=== Starting Deployment ===
Current directory: /builds/root/monitrang
Target path: /root/MonitraNG
Changing to /root/MonitraNG...
Fetching latest code...
Resetting to origin/main...
HEAD is now at 0f4e9f7 fix(ci): Fix Docker build jobs
Changing to ApplicationResources/mng_apps...
Checking port mappings in docker-compose.production.yml...
WARNING: Found 443:443 mapping, fixing to 5443:443...
Stopping existing containers (if any)...
Building and starting Docker services...
Image mnggateway:latest Built
Image mngkeeper:latest Built
Image mngdatagateway:latest Built
Image mnghub:latest Built
Image mngui:latest Built
Container mnggateway Started
Container mngkeeper Started
Container mngdatagateway Started
Container mnghub Started
Container mngui Started
=== Deployment completed successfully ===
```

### Başarılı Container Durumları

```
NAME             IMAGE                   STATUS
mngdatagateway   mngdatagateway:latest   Up (health: starting)
mnggateway       mnggateway:latest       Up (health: starting)
mnghub           mnghub:latest           Up (health: starting)
mngkeeper        mngkeeper:latest        Up (health: starting)
mngui            mngui:latest            Up (health: starting)
```

---

## 📝 Notlar

### Port Mapping

- **MngGateway:** `5000:5000` (HTTP), `5443:443` (HTTPS)
- **MngKeeper:** `5001:5001`
- **MngDataGateway:** `5010:5010`
- **MngHub:** `5020:5020`
- **MngUI:** `3000:80`

### Deployment Süresi

- **İlk Build:** ~5-10 dakika (tüm image'lar build edilir)
- **Sonraki Build'ler:** ~1-2 dakika (cache kullanılır)

### Zero-Downtime Deployment

Şu anki deployment stratejisi:
1. Mevcut container'lar durdurulur (`docker compose down`)
2. Yeni container'lar build edilir ve başlatılır
3. Kısa bir downtime olur (~10-30 saniye)

**Gelecek İyileştirmeler:**
- Blue-Green Deployment
- Rolling Update
- Health Check'ler ile otomatik rollback

---

## 🔗 İlgili Dosyalar

- `.gitlab-ci.yml` - CI/CD pipeline yapılandırması
- `ApplicationResources/mng_apps/docker-compose.production.yml` - Production Docker Compose dosyası
- `docs/content/cicd/current_status.md` - CI/CD durum dokümantasyonu

---

**Son Güncelleme:** 1 Ocak 2026  
**Durum:** ✅ Production'da Başarıyla Çalışıyor

