# Otomatik Deployment Workflow Rehberi

**Hedef:** Lokal geliştirme → GitLab commit → Otomatik deployment  
**Workflow:** Local Development → Git Push → CI/CD Pipeline → Remote Server Deployment

---

## 🎯 Workflow Özeti

```
1. Lokal Makine (Windows)
   ├── Kod geliştirme
   ├── Docker Desktop ile test (opsiyonel)
   └── "GitLab'a commit yap" komutu
        │
        ▼
2. GitLab (Remote Server)
   ├── Repository'ye push
   ├── CI/CD Pipeline tetiklenir
   ├── Build job'ları çalışır
   ├── Test job'ları çalışır
   └── Deployment job'ı çalışır
        │
        ▼
3. Remote Server (Hosting)
   ├── SSH ile bağlanır
   ├── Yeni kodları çeker
   ├── Docker image'ları build eder
   ├── Servisleri günceller
   └── Health check yapar
```

---

## 📋 Gereksinimler

### 1. Remote Server Hazırlığı

- ✅ GitLab kurulu ve çalışıyor
- ✅ Docker ve Docker Compose kurulu
- ✅ SSH erişimi aktif
- ✅ Deployment kullanıcısı oluşturulmuş
- ✅ SSH key authentication yapılandırılmış

### 2. GitLab CI/CD

- ✅ `.gitlab-ci.yml` dosyası mevcut
- ✅ Deployment stage eklenecek
- ✅ SSH key GitLab CI/CD variables'a eklenecek

---

## 🔧 Adım 1: Remote Server Hazırlığı

### 1.1 Deployment Kullanıcısı Oluşturma

```bash
# SSH ile remote server'a bağlan
ssh user@your-server-ip

# Deployment kullanıcısı oluştur
sudo adduser deploy
# Şifre belirleyin (güçlü bir şifre)

# Sudo yetkisi ver (opsiyonel - sadece Docker için)
sudo usermod -aG docker deploy
sudo usermod -aG sudo deploy

# Deploy kullanıcısına geç
su - deploy
```

### 1.2 Deployment Klasör Yapısı

```bash
# Deploy kullanıcısı ile
cd ~
mkdir -p monitrang
cd monitrang

# Git repository clone et
git clone https://gitlab.yourdomain.com/root/MonitraNG.git .

# Docker Compose dosyasını kopyala
cp ApplicationResources/mng_apps/docker-compose.production.yml docker-compose.yml

# Environment dosyası oluştur
cp ApplicationResources/mng_apps/env.example .env
nano .env  # Gerekli değerleri doldurun
```

### 1.3 SSH Key Oluşturma (GitLab CI/CD için)

```bash
# Deploy kullanıcısı ile
ssh-keygen -t ed25519 -C "gitlab-ci-deploy" -f ~/.ssh/gitlab_deploy_key -N ""

# Public key'i authorized_keys'e ekle
cat ~/.ssh/gitlab_deploy_key.pub >> ~/.ssh/authorized_keys
chmod 600 ~/.ssh/authorized_keys

# Private key'i göster (GitLab CI/CD variables'a eklenecek)
cat ~/.ssh/gitlab_deploy_key
# Bu çıktıyı kopyalayın!
```

---

## 🔐 Adım 2: GitLab CI/CD Variables Yapılandırması

### 2.1 GitLab'da Variables Ekleme

1. GitLab'da projeye gidin: `MonitraNG`
2. **Settings > CI/CD** sekmesine gidin
3. **Variables** bölümünü genişletin
4. **"Add variable"** butonuna tıklayın

**Ekleyeceğiniz Variables:**

| Key | Value | Type | Protected | Masked |
|-----|-------|------|-----------|--------|
| `DEPLOY_SSH_PRIVATE_KEY` | (Private key içeriği) | Variable | ❌ | ✅ |
| `DEPLOY_SERVER_HOST` | `your-server-ip` veya `gitlab.yourdomain.com` | Variable | ❌ | ❌ |
| `DEPLOY_SERVER_USER` | `deploy` | Variable | ❌ | ❌ |
| `DEPLOY_SERVER_PORT` | `22` | Variable | ❌ | ❌ |
| `DEPLOY_SERVER_PATH` | `/home/deploy/monitrang` | Variable | ❌ | ❌ |

**Önemli:**
- `DEPLOY_SSH_PRIVATE_KEY` → **Masked** işaretleyin
- Private key içeriğini tam olarak kopyalayın (başında `-----BEGIN` ve sonunda `-----END` dahil)

---

## 📝 Adım 3: GitLab CI/CD Pipeline'a Deployment Stage Ekleme

`.gitlab-ci.yml` dosyasına deployment stage'lerini ekleyin:

```yaml
# .gitlab-ci.yml dosyasına eklenecek

stages:
  - test-setup
  - build
  - test
  - build-docker
  - openapi-extract
  - validate-docs
  - deploy-docs
  - deploy  # Yeni stage

# ... mevcut job'lar ...

# ============================================
# DEPLOYMENT STAGE
# ============================================

# Deployment job - Sadece main branch için
deploy-services:
  stage: deploy
  image: alpine:latest
  tags:
    - docker
  before_script:
    - apk add --no-cache openssh-client rsync
    - eval $(ssh-agent -s)
    - echo "$DEPLOY_SSH_PRIVATE_KEY" | tr -d '\r' | ssh-add -
    - mkdir -p ~/.ssh
    - chmod 700 ~/.ssh
    - ssh-keyscan -H $DEPLOY_SERVER_HOST >> ~/.ssh/known_hosts
    - chmod 644 ~/.ssh/known_hosts
  script:
    - echo "=== Deploying to Remote Server ==="
    - |
      ssh -o StrictHostKeyChecking=no -p ${DEPLOY_SERVER_PORT:-22} $DEPLOY_SERVER_USER@$DEPLOY_SERVER_HOST << 'ENDSSH'
        set -e
        cd $DEPLOY_SERVER_PATH
        
        echo "Pulling latest code..."
        git fetch origin
        git reset --hard origin/main
        
        echo "Updating Docker Compose..."
        docker compose pull
        
        echo "Stopping old services..."
        docker compose down
        
        echo "Starting services with new code..."
        docker compose up -d --build
        
        echo "Waiting for services to be healthy..."
        sleep 10
        
        echo "Checking service status..."
        docker compose ps
        
        echo "=== Deployment completed successfully ==="
      ENDSSH
    - echo "✅ Services deployed successfully!"
  after_script:
    - |
      if [ "$CI_JOB_STATUS" == "failed" ]; then
        echo "❌ Deployment failed!"
        echo "Pipeline: $CI_PIPELINE_URL"
        echo "Job: $CI_JOB_URL"
      elif [ "$CI_JOB_STATUS" == "success" ]; then
        echo "✅ Deployment succeeded!"
        echo "Services are running on: $DEPLOY_SERVER_HOST"
      fi
  only:
    - main
  when: manual  # Manuel tetikleme için (opsiyonel - otomatik için 'on_success' kullanın)
  environment:
    name: production
    url: http://$DEPLOY_SERVER_HOST
```

---

## 🚀 Adım 4: Gelişmiş Deployment Script (Opsiyonel)

Daha kontrollü deployment için remote server'da script oluşturun:

### 4.1 Remote Server'da Deployment Script

```bash
# Remote server'da (deploy kullanıcısı ile)
cd ~/monitrang
nano deploy.sh
```

Script içeriği:

```bash
#!/bin/bash
set -e

echo "=== MonitraNG Deployment Script ==="
echo "Date: $(date)"
echo ""

# Git pull
echo "1. Pulling latest code..."
git fetch origin
git reset --hard origin/main
git submodule update --init --recursive || true

# Environment check
echo "2. Checking environment..."
if [ ! -f .env ]; then
    echo "ERROR: .env file not found!"
    exit 1
fi

# Docker Compose pull
echo "3. Pulling Docker images..."
docker compose pull

# Backup (opsiyonel)
echo "4. Creating backup..."
mkdir -p backups
docker compose exec -T mongo mongodump --archive > backups/mongo-$(date +%Y%m%d-%H%M%S).archive || true

# Stop services gracefully
echo "5. Stopping services..."
docker compose down --timeout 30

# Start services
echo "6. Starting services..."
docker compose up -d --build

# Health check
echo "7. Waiting for services to be healthy..."
sleep 15

# Check service status
echo "8. Checking service status..."
docker compose ps

# Health check endpoints
echo "9. Health checks..."
curl -f http://localhost:5001/health || echo "⚠️  MngKeeper health check failed"
curl -f http://localhost:5010/health || echo "⚠️  MngDataGateway health check failed"
curl -f http://localhost:5020/health || echo "⚠️  MngHub health check failed"
curl -f http://localhost:3000/health || echo "⚠️  Mng.Ui health check failed"

echo ""
echo "=== Deployment completed successfully ==="
echo "Services are running on: $(hostname)"
```

```bash
# Script'i çalıştırılabilir yap
chmod +x deploy.sh
```

### 4.2 GitLab CI/CD'de Script Kullanımı

`.gitlab-ci.yml` içinde script'i çağırın:

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
    - |
      ssh -o StrictHostKeyChecking=no -p ${DEPLOY_SERVER_PORT:-22} $DEPLOY_SERVER_USER@$DEPLOY_SERVER_HOST \
        "cd $DEPLOY_SERVER_PATH && ./deploy.sh"
  only:
    - main
  when: manual  # veya on_success (otomatik)
  environment:
    name: production
    url: http://$DEPLOY_SERVER_HOST
```

---

## 💻 Adım 5: Lokal Makinede Helper Script

Windows PowerShell script'i oluşturun:

### 5.1 GitLab Commit Script

`scripts/gitlab-commit.ps1`:

```powershell
# GitLab Commit and Deploy Script
param(
    [string]$Message = "",
    [switch]$Deploy = $false
)

Write-Host "=== GitLab Commit and Deploy ===" -ForegroundColor Green
Write-Host ""

# Git status kontrolü
Write-Host "📋 Checking git status..." -ForegroundColor Cyan
$status = git status --porcelain
if ([string]::IsNullOrEmpty($status)) {
    Write-Host "⚠️  No changes to commit!" -ForegroundColor Yellow
    exit 0
}

# Commit mesajı
if ([string]::IsNullOrEmpty($Message)) {
    $Message = Read-Host "Enter commit message"
    if ([string]::IsNullOrEmpty($Message)) {
        Write-Host "❌ Commit message is required!" -ForegroundColor Red
        exit 1
    }
}

# Stage all changes
Write-Host "📦 Staging changes..." -ForegroundColor Cyan
git add .

# Commit
Write-Host "💾 Committing changes..." -ForegroundColor Cyan
git commit -m $Message

# Push to GitLab
Write-Host "🚀 Pushing to GitLab..." -ForegroundColor Cyan
git push gitlab main

if ($LASTEXITCODE -eq 0) {
    Write-Host "✅ Successfully pushed to GitLab!" -ForegroundColor Green
    Write-Host ""
    Write-Host "📊 Pipeline URL:" -ForegroundColor Cyan
    Write-Host "   http://your-gitlab-server/root/MonitraNG/-/pipelines" -ForegroundColor White
    
    if ($Deploy) {
        Write-Host ""
        Write-Host "🚀 Deployment will be triggered automatically..." -ForegroundColor Yellow
        Write-Host "   (or manually trigger from GitLab UI)" -ForegroundColor Gray
    }
} else {
    Write-Host "❌ Push failed!" -ForegroundColor Red
    exit 1
}
```

### 5.2 Kullanım

```powershell
# Sadece commit ve push
.\scripts\gitlab-commit.ps1 -Message "feat: yeni özellik eklendi"

# Commit, push ve deploy
.\scripts\gitlab-commit.ps1 -Message "feat: yeni özellik eklendi" -Deploy
```

---

## 🔄 Adım 6: Workflow Kullanımı

### Senaryo 1: Normal Geliştirme

```powershell
# 1. Kod geliştirme (lokal)
# 2. Test et (lokal veya Docker Desktop)
# 3. Commit ve push
.\scripts\gitlab-commit.ps1 -Message "fix: bug düzeltildi"

# 4. GitLab'da pipeline'ı izle
# 5. Deployment job'ını manuel tetikle (eğer when: manual ise)
```

### Senaryo 2: Otomatik Deployment

`.gitlab-ci.yml` içinde `when: on_success` yaparsanız:

```powershell
# 1. Kod geliştirme
# 2. Commit ve push
.\scripts\gitlab-commit.ps1 -Message "feat: yeni özellik"

# 3. Pipeline otomatik çalışır ve deploy eder!
# 4. GitLab'da pipeline'ı izle
```

---

## 🎯 Adım 7: Deployment Stratejileri

### Strateji 1: Blue-Green Deployment (Önerilen)

```yaml
deploy-services:
  script:
    - |
      ssh $DEPLOY_SERVER_USER@$DEPLOY_SERVER_HOST << 'ENDSSH'
        cd $DEPLOY_SERVER_PATH
        
        # Blue environment (mevcut)
        docker compose -f docker-compose.blue.yml ps
        
        # Green environment'a deploy
        docker compose -f docker-compose.green.yml pull
        docker compose -f docker-compose.green.yml up -d
        
        # Health check
        sleep 10
        curl -f http://localhost:5001/health || exit 1
        
        # Switch traffic (Nginx config update)
        # Blue'yu durdur, Green'i aktif et
        
        # Cleanup
        docker compose -f docker-compose.blue.yml down
      ENDSSH
```

### Strateji 2: Rolling Update (Basit)

```yaml
deploy-services:
  script:
    - |
      ssh $DEPLOY_SERVER_USER@$DEPLOY_SERVER_HOST << 'ENDSSH'
        cd $DEPLOY_SERVER_PATH
        
        # Her servisi sırayla güncelle
        docker compose up -d --no-deps --build mngkeeper
        sleep 5
        docker compose up -d --no-deps --build mngdatagateway
        sleep 5
        docker compose up -d --no-deps --build mnghub
        sleep 5
        docker compose up -d --no-deps --build mngui
      ENDSSH
```

---

## 🔍 Adım 8: Monitoring ve Rollback

### 8.1 Health Check Endpoints

Her servisin health check endpoint'i olmalı:

```yaml
# docker-compose.yml
services:
  mngkeeper:
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:5001/health"]
      interval: 30s
      timeout: 10s
      retries: 3
```

### 8.2 Rollback Script

```bash
# Remote server'da
nano rollback.sh
```

```bash
#!/bin/bash
set -e

echo "=== Rolling back to previous version ==="

cd ~/monitrang

# Önceki commit'e dön
git fetch origin
git reset --hard HEAD~1

# Servisleri yeniden başlat
docker compose down
docker compose up -d --build

echo "✅ Rollback completed"
```

---

## 📊 Adım 9: Deployment Notifications

### 9.1 Slack/Email Bildirimleri (Opsiyonel)

```yaml
deploy-services:
  after_script:
    - |
      if [ "$CI_JOB_STATUS" == "success" ]; then
        curl -X POST -H 'Content-type: application/json' \
          --data "{\"text\":\"✅ MonitraNG deployed successfully to production!\"}" \
          $SLACK_WEBHOOK_URL || true
      fi
```

---

## ✅ Checklist

### Remote Server
- [ ] Deployment kullanıcısı oluşturuldu
- [ ] SSH key oluşturuldu ve authorized_keys'e eklendi
- [ ] Deployment klasörü hazır
- [ ] Docker Compose production dosyası hazır
- [ ] Environment variables yapılandırıldı
- [ ] Deployment script oluşturuldu (opsiyonel)

### GitLab CI/CD
- [ ] CI/CD Variables eklendi (SSH key, server bilgileri)
- [ ] `.gitlab-ci.yml`'a deployment stage eklendi
- [ ] Deployment job'ı yapılandırıldı
- [ ] Pipeline test edildi

### Lokal Makine
- [ ] GitLab remote yapılandırıldı
- [ ] Helper script oluşturuldu (`gitlab-commit.ps1`)
- [ ] Workflow test edildi

---

## 🎯 Kullanım Senaryosu

### Günlük Kullanım

```powershell
# 1. Kod geliştirme
# ... kod yaz ...

# 2. Test et (lokal veya Docker Desktop)
docker compose up -d
# ... test et ...

# 3. Commit ve push
.\scripts\gitlab-commit.ps1 -Message "feat: yeni özellik eklendi"

# 4. GitLab'da pipeline'ı izle
# Browser'da: http://your-gitlab-server/root/MonitraNG/-/pipelines

# 5. Deployment job'ını tetikle (manuel ise)
# GitLab UI'da "deploy-services" job'ına tıkla > "Play" butonu

# 6. Servisler otomatik deploy edilir! 🎉
```

---

## 🆘 Sorun Giderme

### SSH Bağlantı Hatası

```bash
# GitLab CI/CD'de SSH key doğru mu kontrol et
# Variables'da DEPLOY_SSH_PRIVATE_KEY'in tam içeriği var mı?

# Test et:
ssh -i ~/.ssh/gitlab_deploy_key deploy@your-server-ip
```

### Deployment Başarısız

```bash
# Remote server'da logları kontrol et
cd ~/monitrang
docker compose logs
docker compose ps
```

### Pipeline Çalışmıyor

```bash
# GitLab'da:
# 1. Settings > CI/CD > Variables kontrol et
# 2. Runner'ın çalıştığını kontrol et
# 3. Pipeline loglarını incele
```

---

## 📝 Özet

Bu workflow ile:
- ✅ Lokal geliştirme yapabilirsiniz
- ✅ Docker Desktop ile test edebilirsiniz
- ✅ Tek komutla GitLab'a push edebilirsiniz
- ✅ Pipeline otomatik çalışır
- ✅ Servisler remote server'da otomatik deploy edilir
- ✅ Health check'ler yapılır
- ✅ Rollback mümkündür

**"GitLab'a commit yap" dediğinizde → Her şey otomatik! 🚀**

---

**Son Güncelleme:** 28 Aralık 2024

