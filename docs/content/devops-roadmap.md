# DevOps Roadmap - MonitraNG

## 🎯 Genel Bakış

Bu roadmap, MonitraNG projesi için DevOps süreçlerini kapsar:
- 📚 **Dokümantasyon Sistemi** (MkDocs)
- 🔄 **CI/CD Pipeline** (GitHub Actions / GitLab CI)
- 🚀 **Deployment** (Test & Production Servers)
- 🔍 **Code Quality** (SonarQube)
- ☸️ **Container Orchestration** (Docker → Kubernetes - Gelecek)

---

## 📚 Phase 1: Dokümantasyon Sistemi

### 1.1 MkDocs Kurulumu

**Hedef:** Modern, arama destekli dokümantasyon sistemi

**Yapı:**
```
docs/
├── mkdocs.yml          # MkDocs yapılandırması
├── requirements.txt    # Python dependencies
└── content/            # Dokümantasyon kaynak dosyaları (docs_dir)
    ├── index.md
    ├── api/
    └── Services...
```

**Kurulum:**
```bash
cd docs
python -m venv venv
source venv/bin/activate  # Windows: venv\Scripts\activate
pip install -r requirements.txt
mkdocs serve
```

**Özellikler:**
- ✅ Material theme
- ✅ Arama (search) özelliği
- ✅ Dark mode
- ✅ OpenAPI/Swagger entegrasyonu
- ✅ GitHub Pages deploy

**Durum:** ✅ Hazır (mkdocs.yml ve requirements.txt mevcut)

---

## 🔄 Phase 2: CI/CD Pipeline

### 2.1 Platform Seçimi

**GitHub Actions (Önerilen)**
- ✅ GitHub ile entegre
- ✅ Self-hosted runner desteği
- ✅ Ücretsiz (public repo için)
- ✅ Kolay öğrenme

**GitLab CI (Alternatif)**
- ✅ Unlimited private repos (ücretsiz)
- ✅ Güçlü pipeline yapısı
- ✅ Built-in Docker registry
- ✅ Self-hosted runner desteği

**Öneri:** GitHub Actions (mevcut repo GitHub'da)

---

### 2.2 Self-Hosted Runner Kurulumu

**Windows:**
```powershell
.\scripts\setup-github-runner.ps1 -GitHubToken "ghp_xxxxx" -RunnerName "local-runner"
```

**Linux/Mac:**
```bash
bash scripts/setup-github-runner.sh <GITHUB_TOKEN> <RUNNER_NAME>
```

**Durum:** ✅ Script'ler hazır

---

### 2.3 CI/CD Pipeline'ları

**Build Pipeline:**
- ✅ .NET build (MngKeeper, MngDataGateway, MngHub)
- ✅ Frontend build (Mng.Ui)
- ✅ Test execution
- ✅ SonarQube analysis

**Deploy Pipeline:**
- ✅ Test server'a otomatik deploy
- ✅ Production server'a tag-based deploy
- ✅ Dokümantasyon otomatik deploy

**Durum:** ✅ Workflow dosyaları hazır (.github/workflows/)

---

## 🚀 Phase 3: Deployment

### 3.1 Server Gereksinimleri

**Test Server:**
- CPU: 4 cores
- RAM: 8GB
- Disk: 100GB SSD
- OS: Ubuntu 22.04 LTS

**Production Server:**
- CPU: 8 cores
- RAM: 16GB
- Disk: 200GB SSD
- OS: Ubuntu 22.04 LTS

**Önerilen Provider'lar:**
- DigitalOcean: 8GB RAM (~$48/ay)
- Hetzner: CPX31 (~€20/ay) - Avrupa, ucuz
- Linode: 8GB RAM (~$48/ay)

---

### 3.2 Deployment Adımları

**1. Server Kurulumu:**
```bash
# SSH ile bağlan
ssh root@your-server-ip

# Setup script'i çalıştır
git clone https://github.com/serkanmeral/MonitraNG.git
cd MonitraNG
sudo bash scripts/setup-server.sh
```

**2. Environment Yapılandırması:**
```bash
su - deploy
cd ~/MonitraNG/ApplicationResources/mng_apps
cp env.example .env
vim .env  # Şifreleri değiştir
```

**3. Deployment:**
```bash
cd ~/MonitraNG
./scripts/deploy.sh production latest
```

**4. Nginx + SSL:**
```bash
# Nginx yapılandır
sudo vim /etc/nginx/sites-available/monitrang

# SSL sertifikası
sudo certbot --nginx -d monitrang.com
```

**Durum:** ✅ Script'ler ve docker-compose.production.yml hazır

---

## 🔍 Phase 4: Code Quality (SonarQube)

### 4.1 SonarQube Kurulumu

**Öneri:** SonarQube Community Edition (Ücretsiz)

**Docker ile Kurulum:**
```yaml
# docker-compose.yml
services:
  sonarqube:
    image: sonarqube:community
    ports:
      - "9000:9000"
    # PostgreSQL gerekli
```

**Kurulum:**
```bash
docker-compose up -d
# http://localhost:9000
# admin/admin (ilk girişte değiştirin)
```

---

### 4.2 CI/CD Entegrasyonu

**GitHub Actions:**
```yaml
- name: SonarQube Analysis
  run: |
    dotnet sonarscanner begin /k:"MonitraNG"
    dotnet build
    dotnet test
    dotnet sonarscanner end
```

**Özellikler:**
- ✅ Code quality analizi
- ✅ Security scanning (OWASP Top 10)
- ✅ Code coverage
- ✅ Technical debt tracking

**Durum:** ⏳ Planlanmış (henüz kurulmadı)

---

## ☸️ Phase 5: Kubernetes (Gelecek)

### 5.1 Şu An: Docker Desktop

**Durum:** ✅ Docker Desktop kullanılıyor
**Neden:** Local development için ideal, basit, yeterli

---

### 5.2 Gelecek: Kubernetes

**Ne zaman?**
- Production'a geçerken
- High availability gerektiğinde
- Auto-scaling gerektiğinde
- Multiple servers gerektiğinde

**Öğrenme:**
- Minikube/Kind ile local'de deneyin
- YAML dosyalarını hazırlayın
- Production'a geçerken kullanın

**Durum:** ⏳ Gelecek planı (şu an gerekli değil)

---

## 📋 Implementation Roadmap

### Phase 1: Dokümantasyon (1 hafta)
- [x] MkDocs yapılandırması
- [x] requirements.txt
- [ ] Mevcut markdown dosyalarını organize et
- [ ] GitHub Pages deploy yapılandırması
- [ ] İlk dokümantasyon build

**Öncelik:** Orta

---

### Phase 2: CI/CD (2 hafta)
- [x] GitHub Actions workflow dosyaları
- [ ] Self-hosted runner kurulumu
- [ ] Build pipeline test
- [ ] Deploy pipeline test
- [ ] Dokümantasyon otomatik deploy

**Öncelik:** Yüksek

---

### Phase 3: Deployment (2 hafta)
- [x] Production docker-compose.yml
- [x] Deployment script'leri
- [x] Server setup script'i
- [ ] Test server kurulumu
- [ ] Production server kurulumu
- [ ] Nginx yapılandırması
- [ ] SSL sertifikaları
- [ ] DNS yapılandırması
- [ ] Backup stratejisi

**Öncelik:** Yüksek

---

### Phase 4: Code Quality (1 hafta)
- [ ] SonarQube kurulumu
- [ ] CI/CD entegrasyonu
- [ ] Quality gates yapılandırması
- [ ] İlk analiz
- [ ] Security issues düzeltme

**Öncelik:** Orta

---

### Phase 5: Kubernetes (Gelecek)
- [ ] Kubernetes öğrenme
- [ ] Minikube/Kind kurulumu
- [ ] YAML dosyaları hazırlama
- [ ] Production'a geçerken kullanım

**Öncelik:** Düşük (şu an gerekli değil)

---

## 🎯 Öncelik Sırası

### Yüksek Öncelik (Şimdi)
1. ✅ **CI/CD Pipeline** - Otomatik build ve test
2. ✅ **Deployment** - Test ve production server'ları

### Orta Öncelik (Yakın Gelecek)
3. ✅ **Dokümantasyon** - MkDocs sistemi
4. ✅ **Code Quality** - SonarQube kurulumu

### Düşük Öncelik (Gelecek)
5. ⏳ **Kubernetes** - Production'a geçerken

---

## 📊 Durum Özeti

| Phase | Durum | Öncelik | Tahmini Süre |
|-------|-------|---------|--------------|
| **Dokümantasyon** | ✅ Hazır | Orta | 1 hafta |
| **CI/CD** | ✅ Hazır | Yüksek | 2 hafta |
| **Deployment** | ✅ Hazır | Yüksek | 2 hafta |
| **Code Quality** | ⏳ Planlanmış | Orta | 1 hafta |
| **Kubernetes** | ⏳ Gelecek | Düşük | - |

---

## 🚀 Hızlı Başlangıç

### 1. CI/CD Kurulumu
```bash
# GitHub Actions runner kur
.\scripts\setup-github-runner.ps1 -GitHubToken "ghp_xxxxx"

# Test
git push origin main
# GitHub > Actions sekmesinde kontrol et
```

### 2. Deployment
```bash
# Server setup
sudo bash scripts/setup-server.sh

# Deploy
./scripts/deploy.sh production latest
```

### 3. Dokümantasyon
```bash
cd docs
pip install -r requirements.txt
mkdocs serve
```

---

## 📝 Notlar

- **Docker Desktop:** Şu an local development için yeterli
- **Kubernetes:** Production'a geçerken düşünülecek
- **SonarQube:** Code quality için önerilir (ücretsiz Community Edition)
- **GitHub Actions:** Mevcut repo GitHub'da olduğu için önerilir

---

## 🔗 Kaynaklar

- [MkDocs Documentation](https://www.mkdocs.org/)
- [GitHub Actions](https://docs.github.com/en/actions)
- [SonarQube Documentation](https://docs.sonarqube.org/)
- [Docker Documentation](https://docs.docker.com/)
- [Kubernetes Documentation](https://kubernetes.io/docs/)

---

**Son Güncelleme:** 2025-01-XX
**Durum:** Aktif geliştirme

---

## 📦 Hazır Dosyalar

### Script'ler
- ✅ `scripts/setup-github-runner.ps1` - GitHub Actions runner kurulumu (Windows)
- ✅ `scripts/setup-github-runner.sh` - GitHub Actions runner kurulumu (Linux/Mac)
- ✅ `scripts/setup-server.sh` - Server kurulum script'i
- ✅ `scripts/deploy.sh` - Deployment script'i
- ✅ `scripts/backup.sh` - Backup script'i

### Yapılandırma Dosyaları
- ✅ `docs/mkdocs.yml` - MkDocs yapılandırması
- ✅ `docs/requirements.txt` - Python dependencies
- ✅ `.github/workflows/ci.yml` - CI pipeline
- ✅ `.github/workflows/docs-deploy.yml` - Dokümantasyon deploy
- ✅ `.github/workflows/docker-build.yml` - Docker build
- ✅ `ApplicationResources/mng_apps/docker-compose.production.yml` - Production compose
- ✅ `ApplicationResources/mng_apps/env.example` - Environment variables örneği
