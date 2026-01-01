# GitLab CI/CD Pipeline Rehberi

**Durum:** ✅ `.gitlab-ci.yml` dosyası oluşturuldu  
**Tarih:** 27 Aralık 2024

---

## 📋 Pipeline Yapısı

GitLab CI/CD pipeline'ı şu stage'lerden oluşur:

1. **Build Stage** - Projeleri build eder
2. **Test Stage** - Testleri çalıştırır
3. **Build-Docker Stage** - Docker image'ları build eder (ileride)
4. **Deploy-Docs Stage** - Dokümantasyonu GitLab Pages'e deploy eder

---

## 🔧 Pipeline Detayları

### Build Stage

**Jobs:**
- `build-mngkeeper` - MngKeeper .NET projesini build eder
- `build-mngdatagateway` - MngDataGateway .NET projesini build eder
- `build-mnghub` - MngHub .NET projesini build eder
- `build-frontend` - Mng.Ui (Nuxt.js) frontend projesini build eder

**Özellikler:**
- Her job paralel çalışır
- Build artifacts saklanır (1 saat)
- Sadece `main`, `develop` branch'lerinde ve merge request'lerde çalışır

### Test Stage

**Jobs:**
- `test-mngkeeper` - MngKeeper testlerini çalıştırır
- `test-mngdatagateway` - MngDataGateway testlerini çalıştırır
- `test-mnghub` - MngHub testlerini çalıştırır

**Özellikler:**
- `allow_failure: true` - Test başarısız olsa bile pipeline devam eder
- Build stage'den artifact'leri kullanır
- Test sonuçları GitLab'da görüntülenir

### Deploy-Docs Stage

**Jobs:**
- `deploy-docs` - MkDocs ile dokümantasyon build eder
- `pages` - GitLab Pages'e otomatik deploy eder

**Özellikler:**
- Sadece `main` branch'inde çalışır
- GitLab Pages'e otomatik deploy edilir
- Dokümantasyon `http://localhost/root/MonitraNG/-/pages` adresinde erişilebilir

---

## 🚀 Pipeline'ı Çalıştırma

### Otomatik Çalışma

Pipeline otomatik olarak şu durumlarda çalışır:

1. **Push to main/develop** - Her push'ta pipeline çalışır
2. **Merge Request** - MR oluşturulduğunda pipeline çalışır
3. **Manual Trigger** - GitLab UI'dan manuel olarak başlatılabilir

### Pipeline'ı Kontrol Etme

1. GitLab proje sayfasında: **CI/CD > Pipelines**
2. Pipeline durumunu görebilirsiniz:
   - 🟢 Running
   - ✅ Passed
   - ❌ Failed
   - ⏸️ Paused

### Pipeline Logları

Her job'un loglarını görmek için:
1. Pipeline'a tıklayın
2. Job'a tıklayın
3. Log'ları görüntüleyin

---

## 🔍 Yapılandırma Detayları

### Docker Images

- **.NET Build/Test:** `mcr.microsoft.com/dotnet/sdk:9.0`
- **Frontend Build:** `node:18`
- **Docs Build:** `python:3.11`

### Cache

- `.nuget/` klasörü cache'lenir (daha hızlı restore)

### Artifacts

- Build artifacts 1 saat saklanır
- Docs artifacts 1 gün saklanır

### Tags

- Tüm job'lar `docker` tag'ine sahip
- Runner'ın bu tag'e sahip olması gerekiyor

---

## 🛠️ Pipeline'ı Geliştirme

### Docker Build Ekleme

Docker build job'larını eklemek için `.gitlab-ci.yml` dosyasındaki yorum satırlarını açın:

```yaml
build-docker-mngkeeper:
  stage: build-docker
  script:
    - docker build -t mngkeeper:latest -f MngKeeper/Presentation/MngKeeper.Api/Dockerfile .
  only:
    - main
```

### SonarQube Entegrasyonu

SonarQube analizi eklemek için:

```yaml
sonarqube-analysis:
  stage: test
  image: sonarsource/sonar-scanner-cli
  script:
    - sonar-scanner
  only:
    - main
```

### Deploy Job'ları Ekleme

Production'a deploy için:

```yaml
deploy-production:
  stage: deploy
  script:
    - ./scripts/deploy.sh production latest
  only:
    - tags
  when: manual
```

---

## 🆘 Sorun Giderme

### Pipeline Çalışmıyor

**Kontrol edin:**
1. Runner'ın online olduğundan emin olun: Settings > CI/CD > Runners
2. Runner'ın `docker` tag'ine sahip olduğundan emin olun
3. `.gitlab-ci.yml` dosyasının syntax'ının doğru olduğundan emin olun

### Job Başarısız Oluyor

**Kontrol edin:**
1. Job loglarını inceleyin
2. Docker image'ın erişilebilir olduğundan emin olun
3. GitLab Runner'ın Docker'a erişebildiğinden emin olun

### Test Job'ları Başarısız Oluyor

**Not:** Test job'ları `allow_failure: true` ile işaretlenmiş, bu yüzden başarısız olsa bile pipeline devam eder. Test sonuçlarını loglardan kontrol edin.

---

## 📚 İlgili Dokümantasyon

- [GitLab CI/CD Documentation](https://docs.gitlab.com/ee/ci/)
- [GitLab Runner Setup](GITLAB_RUNNER_SETUP.md)
- [MkDocs Pipeline Guide](MKDOCS_PIPELINE_GUIDE.md) (oluşturulacak)

---

**Son Güncelleme:** 27 Aralık 2024

