# Multi-Environment Analizi - Kaynak Gereksinimleri ve Alternatifler

**Tarih:** 1 Ocak 2026  
**Durum:** Analiz ve Öneriler

---

## 📊 Mevcut Durum (Production)

### Kaynak Kullanımı

| Kategori | RAM | CPU | Disk | Notlar |
|----------|-----|-----|------|--------|
| **Infrastructure** | ~8 GB | ~9 Core | ~97 GB | MongoDB, PostgreSQL, Keycloak, Redis, RabbitMQ, MinIO, vb. |
| **Application** | ~4 GB | ~5 Core | ~8 GB | MngGateway, MngKeeper, MngDataGateway, MngHub, MngUI |
| **CI/CD (GitLab)** | ~4 GB | ~2 Core | ~20 GB | GitLab CE, Runner, PostgreSQL, Redis |
| **Sistem Overhead** | ~1 GB | ~1 Core | ~5 GB | OS, Docker, vb. |
| **TOPLAM** | **~17 GB** | **~17 Core** | **~130 GB** | |

**Not:** AI Chat Bot dahil değil (opsiyonel)

---

## 🔄 Multi-Environment Senaryoları

### Senaryo 1: Tam Ayrı Staging Ortamı (Ayrı Sunucu)

**Kaynak Gereksinimleri:**

| Ortam | RAM | CPU | Disk | Toplam Maliyet |
|-------|-----|-----|------|----------------|
| **Production** | 17 GB | 17 Core | 130 GB | Mevcut |
| **Staging** | 17 GB | 17 Core | 130 GB | +100% |
| **TOPLAM** | **34 GB** | **34 Core** | **260 GB** | **2x Artış** |

**Avantajlar:**
- ✅ Tam izolasyon (production etkilenmez)
- ✅ Farklı konfigürasyonlar test edilebilir
- ✅ Staging'de yıkıcı testler yapılabilir
- ✅ Production ve staging aynı anda çalışabilir

**Dezavantajlar:**
- ❌ **2x kaynak gereksinimi** (RAM, CPU, Disk)
- ❌ **2x maliyet** (sunucu, hosting)
- ❌ **2x yönetim** (backup, monitoring, vb.)
- ❌ Infrastructure servisleri de duplicate (MongoDB, PostgreSQL, vb.)

**Maliyet Örneği:**
- **Hetzner CPX41:** €35/ay → **€70/ay** (2 sunucu)
- **DigitalOcean:** $96/ay → **$192/ay** (2 sunucu)

---

### Senaryo 2: Aynı Sunucuda Farklı Portlarda (Kaynak Paylaşımı)

**Kaynak Gereksinimleri:**

| Kategori | RAM | CPU | Disk | Notlar |
|----------|-----|-----|------|--------|
| **Infrastructure (Paylaşılan)** | ~8 GB | ~9 Core | ~97 GB | MongoDB, PostgreSQL, Keycloak, Redis, RabbitMQ, MinIO (tek instance) |
| **Production Apps** | ~4 GB | ~5 Core | ~8 GB | Production servisleri |
| **Staging Apps** | ~4 GB | ~5 Core | ~8 GB | Staging servisleri (farklı portlarda) |
| **CI/CD (Paylaşılan)** | ~4 GB | ~2 Core | ~20 GB | GitLab (tek instance) |
| **Sistem Overhead** | ~1 GB | ~1 Core | ~5 GB | OS, Docker |
| **TOPLAM** | **~21 GB** | **~22 Core** | **~138 GB** | **+23% Artış** |

**Avantajlar:**
- ✅ **Daha az kaynak gereksinimi** (sadece %23 artış)
- ✅ **Tek sunucu** (maliyet tasarrufu)
- ✅ Infrastructure paylaşımı (MongoDB, PostgreSQL, vb.)
- ✅ CI/CD paylaşımı (GitLab tek instance)

**Dezavantajlar:**
- ⚠️ **Kısmi izolasyon** (aynı infrastructure)
- ⚠️ **Port yönetimi** (production ve staging farklı portlarda)
- ⚠️ **Veri karışma riski** (aynı MongoDB'de farklı database'ler)
- ⚠️ **Kaynak çakışması** (yüksek trafik durumunda)

**Maliyet Örneği:**
- **Hetzner CPX41:** €35/ay → **€50/ay** (daha güçlü sunucu)
- **DigitalOcean:** $96/ay → **$144/ay** (32 GB RAM sunucu)

**Port Yapılandırması Örneği:**
```
Production:
- MngGateway: 5000, 5443
- MngKeeper: 5001
- MngDataGateway: 5010
- MngHub: 5020
- MngUI: 3000

Staging:
- MngGateway: 6000, 6443
- MngKeeper: 6001
- MngDataGateway: 6010
- MngHub: 6020
- MngUI: 4000
```

---

### Senaryo 3: Feature Flags ile Production'da Test (En Az Kaynak)

**Kaynak Gereksinimleri:**

| Kategori | RAM | CPU | Disk | Notlar |
|----------|-----|-----|------|--------|
| **Production** | 17 GB | 17 Core | 130 GB | Mevcut (değişmez) |
| **Ek Overhead** | ~0.5 GB | ~0.5 Core | ~1 GB | Feature flag sistemi |
| **TOPLAM** | **~17.5 GB** | **~17.5 Core** | **~131 GB** | **+3% Artış** |

**Avantajlar:**
- ✅ **Minimal kaynak gereksinimi** (sadece %3 artış)
- ✅ **Gerçek production ortamında test** (daha gerçekçi)
- ✅ **A/B testing** yapılabilir
- ✅ **Canary deployment** ile entegre edilebilir

**Dezavantajlar:**
- ⚠️ **Production riski** (test sırasında production etkilenebilir)
- ⚠️ **Karmaşık yönetim** (feature flag yönetimi)
- ⚠️ **Gerçek staging ortamı yok** (sadece feature toggle)

**Kullanım Senaryosu:**
- Yeni özellikler feature flag ile açılır/kapatılır
- Küçük kullanıcı grubuna açılır (canary)
- Başarılı olursa tüm kullanıcılara açılır

---

### Senaryo 4: Docker Compose Override ile Environment Switching

**Kaynak Gereksinimleri:**

| Durum | RAM | CPU | Disk | Notlar |
|-------|-----|-----|------|--------|
| **Production Çalışırken** | 17 GB | 17 Core | 130 GB | Normal |
| **Staging'e Geçiş** | 17 GB | 17 Core | 130 GB | Production durdurulur, staging başlatılır |
| **TOPLAM** | **17 GB** | **17 Core** | **~150 GB** | **+15% Disk** (2 set config) |

**Avantajlar:**
- ✅ **Minimal kaynak gereksinimi** (aynı kaynaklar)
- ✅ **Tam izolasyon** (aynı anda sadece biri çalışır)
- ✅ **Aynı sunucu** (maliyet tasarrufu)
- ✅ **Kolay geçiş** (`docker-compose -f docker-compose.production.yml` vs `docker-compose -f docker-compose.staging.yml`)

**Dezavantajlar:**
- ❌ **Aynı anda çalışamaz** (production durdurulmalı)
- ❌ **Deployment sırasında downtime** (staging'e geçiş)
- ❌ **Manuel geçiş** (otomatik değil)

**Kullanım Senaryosu:**
- Production çalışırken staging test edilemez
- Staging test edilirken production durur
- Manuel olarak environment değiştirilir

---

## 📊 Karşılaştırma Tablosu

| Senaryo | RAM Artışı | CPU Artışı | Disk Artışı | Maliyet Artışı | İzolasyon | Aynı Anda Çalışma |
|---------|------------|------------|-------------|----------------|-----------|-------------------|
| **1. Ayrı Sunucu** | +100% | +100% | +100% | +100% | ✅ Tam | ✅ Evet |
| **2. Aynı Sunucu (Farklı Port)** | +23% | +29% | +6% | +43% | ⚠️ Kısmi | ✅ Evet |
| **3. Feature Flags** | +3% | +3% | +1% | +3% | ❌ Yok | ✅ Evet (production içinde) |
| **4. Override Switching** | 0% | 0% | +15% | 0% | ✅ Tam | ❌ Hayır |

---

## 💡 Öneriler

### Senaryonuz İçin En Uygun: **Senaryo 2 (Aynı Sunucuda Farklı Portlarda)**

**Neden?**
1. **Air-Gapped Sistem:** Tek sunucuda tüm ortamlar (offline deployment kolay)
2. **Kaynak Tasarrufu:** Sadece %23 artış (vs %100)
3. **Maliyet:** Tek sunucu (vs 2 sunucu)
4. **Pratiklik:** Aynı anda production ve staging çalışabilir
5. **Yönetim:** Tek sunucu yönetimi (backup, monitoring, vb.)

**Gereksinimler:**
- Mevcut sunucu: **32 GB RAM, 8 Core** → **40 GB RAM, 10 Core** (yeterli)
- Veya: **16 GB RAM, 4 Core** → **24 GB RAM, 6 Core** (minimum)

**Yapılandırma:**
```yaml
# docker-compose.production.yml
services:
  mnggateway:
    ports:
      - "5000:5000"  # Production
      - "5443:443"

# docker-compose.staging.yml
services:
  mnggateway:
    ports:
      - "6000:5000"  # Staging
      - "6443:443"
```

**Database İzolasyonu:**
- MongoDB: Farklı database'ler (`monitra_prod`, `monitra_staging`)
- PostgreSQL: Farklı database'ler (`keycloak_prod`, `keycloak_staging`)
- Redis: Farklı database index'leri (0: production, 1: staging)

---

### Alternatif: **Senaryo 3 (Feature Flags)** - Gelecek İçin

**Ne Zaman?**
- Production'da canary deployment yapmak istediğinizde
- A/B testing yapmak istediğinizde
- Yeni özellikleri kademeli olarak açmak istediğinizde

**Gereksinimler:**
- Feature flag sistemi (LaunchDarkly, Unleash, veya custom)
- Minimal kaynak artışı (%3)

---

## 🎯 Sonuç ve Öneri

### Mevcut Durumunuz İçin:

**Multi-Environment'e gerek yok** çünkü:
1. ✅ **Automated Rollback** var (hata durumunda geri dönüş)
2. ✅ **Health Check** mekanizması var (deployment öncesi kontrol)
3. ✅ **Pre-Deployment Backup** var (güvenli geri dönüş)
4. ✅ **Air-Gapped sistem** (offline deployment, test ortamı zor)

**Ancak, gelecekte ihtiyaç olursa:**
- **Senaryo 2** (Aynı sunucuda farklı portlarda) en uygun
- Sadece %23 kaynak artışı
- Tek sunucu yönetimi
- Aynı anda production ve staging çalışabilir

### Öncelik Önerisi:

1. **Şimdi:** Pipeline optimizasyonu (cache, parallel build)
2. **Gelecek:** Multi-Environment (ihtiyaç olursa Senaryo 2)
3. **İleri Seviye:** Feature Flags + Canary Deployment

---

## 📝 Notlar

- **Air-Gapped Sistem:** Offline deployment için tek sunucu daha pratik
- **Kaynak Tasarrufu:** Senaryo 2 ile %77 kaynak tasarrufu (vs Senaryo 1)
- **Yönetim Kolaylığı:** Tek sunucu yönetimi daha kolay
- **Maliyet:** Senaryo 2 ile %57 maliyet tasarrufu (vs Senaryo 1)

---

**Son Güncelleme:** 1 Ocak 2026

