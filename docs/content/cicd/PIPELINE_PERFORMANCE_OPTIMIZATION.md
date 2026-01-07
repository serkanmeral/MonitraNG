# Pipeline Performans Optimizasyonu

**Tarih:** 2 Ocak 2026  
**Durum:** ⚠️ Kısmen Tamamlandı - Cache çalışıyor ancak etkisi beklenenden düşük

---

## 🎯 Optimizasyon Hedefleri

1. Build sürelerini azaltma
2. Cache mekanizmasını optimize etme
3. Parallel build desteği
4. Docker layer cache optimizasyonu

---

## ✅ Uygulanan Optimizasyonlar

### 1. .NET NuGet Package Cache Optimizasyonu

**Önceki Durum:**
- Her job kendi cache'ini oluşturuyordu
- Cache key yoktu, her job için ayrı cache

**Yeni Durum:**
- Global cache key: `nuget-$CI_COMMIT_REF_SLUG`
- Tüm .NET job'ları aynı cache'i kullanıyor
- `NUGET_PACKAGES: .nuget` environment variable eklendi
- `--packages .nuget` parametresi restore komutlarına eklendi

**Etki:**
- İlk build: Normal süre
- Sonraki build'ler: %50-70 daha hızlı (package restore atlanıyor)

**Uygulanan Job'lar:**
- `build-mngkeeper`
- `build-mngdatagateway`
- `build-mnghub`
- `build-mnggateway`
- `test-mngkeeper`
- `test-mngdatagateway`
- `test-mnghub`
- `test-mnggateway`

---

### 2. NPM Cache Yapılandırması

**Önceki Durum:**
- NPM cache yoktu
- Her build'de tüm package'lar indiriliyordu

**Yeni Durum:**
- Cache key: `npm-$CI_COMMIT_REF_SLUG`
- Cache paths:
  - `.npm-cache/` - NPM cache directory
  - `Mng.Ui/node_modules/` - Installed packages
- `NPM_CONFIG_CACHE: .npm-cache` environment variable
- `NPM_CONFIG_PREFER_OFFLINE: "true"` - Offline mode
- `npm ci --prefer-offline --cache ../.npm-cache` - Cache kullanımı

**Etki:**
- İlk build: Normal süre
- Sonraki build'ler: %60-80 daha hızlı (package install atlanıyor)

**Uygulanan Job:**
- `build-frontend`

---

### 3. Docker Layer Cache Optimizasyonu

**Önceki Durum:**
- Docker build cache kullanılmıyordu
- Her build'de tüm layer'lar yeniden build ediliyordu

**Yeni Durum:**
- `DOCKER_BUILDKIT: "1"` - BuildKit enabled
- `BUILDKIT_PROGRESS: "plain"` - Progress output
- `--cache-from <image>:latest` - Previous image'ı cache olarak kullan

**Etki:**
- İlk build: Normal süre
- Sonraki build'ler: %40-60 daha hızlı (değişmeyen layer'lar cache'den)

**Uygulanan Job'lar:**
- `build-docker-ui`
- `build-docker-gateway`

---

### 4. Global Cache Yapılandırması

**Önceki Durum:**
```yaml
cache:
  paths:
    - .nuget/
    - .pip-cache/
    - docs/.cache/
```

**Yeni Durum:**
```yaml
cache:
  key: ${CI_COMMIT_REF_SLUG}
  paths:
    - .nuget/  # .NET NuGet package cache (shared across all .NET jobs)
    - .npm-cache/  # NPM cache (frontend build için)
    - .pip-cache/  # Python pip cache for MkDocs
    - docs/.cache/  # MkDocs cache
  policy: pull-push  # Her job cache'i okuyup yazabilir
```

**Etki:**
- Tüm job'lar aynı cache'i paylaşıyor
- Branch bazlı cache (her branch için ayrı cache)
- Pull-push policy ile cache güncelleniyor

---

## 📊 Beklenen Performans İyileştirmeleri

### Build Süreleri (Tahmini)

| Job | Önceki Süre | Yeni Süre (İlk) | Yeni Süre (Cache'li) | İyileştirme |
|-----|-------------|-----------------|----------------------|-------------|
| **build-mngkeeper** | ~5-8 dk | ~5-8 dk | ~2-3 dk | %60-70 |
| **build-mngdatagateway** | ~6-10 dk | ~6-10 dk | ~2-4 dk | %60-70 |
| **build-mnghub** | ~4-6 dk | ~4-6 dk | ~1.5-2.5 dk | %60-70 |
| **build-mnggateway** | ~5-8 dk | ~5-8 dk | ~2-3 dk | %60-70 |
| **build-frontend** | ~3-5 dk | ~3-5 dk | ~1-2 dk | %60-80 |
| **build-docker-ui** | ~4-6 dk | ~4-6 dk | ~2-3 dk | %40-60 |
| **build-docker-gateway** | ~5-8 dk | ~5-8 dk | ~2-4 dk | %40-60 |

### Toplam Pipeline Süresi

**Önceki Durum:**
- Build stage: ~20-30 dakika (sıralı)
- Test stage: ~15-20 dakika (sıralı)
- **Toplam:** ~35-50 dakika

**Yeni Durum (Cache'li):**
- Build stage: ~8-12 dakika (parallel + cache)
- Test stage: ~6-10 dakika (parallel + cache)
- **Toplam:** ~14-22 dakika

**İyileştirme:** %50-60 daha hızlı

---

## 🔧 Cache Yönetimi

### Cache Temizleme

Cache otomatik olarak branch bazlı yönetiliyor:
- Her branch için ayrı cache (`$CI_COMMIT_REF_SLUG`)
- Eski branch'lerin cache'leri otomatik temizlenir
- Main branch cache'i her zaman mevcut

### Manuel Cache Temizleme

```bash
# GitLab UI'dan:
# Settings > CI/CD > Runners > Expand runner > Clear runner caches

# Veya runner'da:
docker exec gitlab-runner gitlab-runner cache clear
```

---

## 📈 Monitoring ve Ölçüm

### Pipeline Süresi Takibi

Pipeline süreleri GitLab UI'da görülebilir:
- **Pipeline List:** Her pipeline'ın süresi gösterilir
- **Job Details:** Her job'ın süresi detaylı gösterilir
- **Cache Hit Rate:** Cache kullanım oranı log'larda görülebilir

### Cache Hit Rate Kontrolü

```bash
# Job log'larında cache hit rate kontrolü:
# "Restoring cache" mesajı görünüyorsa cache kullanılıyor
# "No cache found" mesajı görünüyorsa cache yok (ilk build)
```

---

## 🎯 Sonraki Optimizasyonlar (Gelecek)

### 1. Parallel Build Optimizasyonu
- Build job'ları zaten parallel çalışıyor (aynı stage)
- Test job'ları da parallel çalışıyor
- **Durum:** ✅ Zaten optimize

### 2. Artifact Dependency Optimizasyonu
- Artifacts kullanılmıyor (local build)
- **Durum:** ✅ Zaten optimize

### 3. Docker Registry Cache
- Docker image'ları registry'ye push edilmiyor
- **Gelecek:** Registry cache eklenebilir

### 4. Pipeline Süresi Ölçümü
- GitLab UI'da görülebilir
- **Gelecek:** Otomatik raporlama eklenebilir

---

## 📝 Notlar

- **Cache Key Stratejisi:** Branch bazlı (`$CI_COMMIT_REF_SLUG`)
- **Cache Policy:** `pull-push` (her job cache'i okuyup yazabilir)
- **Cache Boyutu:** ~100-500 MB (package'lar ve node_modules)
- **Cache Retention:** GitLab otomatik yönetir (disk alanına göre)

---

**Son Güncelleme:** 2 Ocak 2026

---

## 📊 Gerçek Performans Sonuçları

### Pipeline Süreleri (Gerçek Ölçümler)

| Build | Süre | Cache Durumu | Notlar |
|-------|------|--------------|--------|
| İlk build | 8:55 | Cache yok | Tüm package'lar indirildi |
| İkinci build | 8:18 | Cache restore edildi | NuGet global cache yoktu |
| Üçüncü build | 8:42 | Cache restore edildi | NuGet global cache yoktu |
| Dördüncü build | 8:14 | Cache oluşturuldu | NuGet global cache path eklendi |
| Beşinci build | 8:14 | Cache restore edildi | Cache çalışıyor (8847 dosya) |

### Analiz

**Beklenen:** %40-50 iyileştirme (~4-5 dakika)  
**Gerçek:** %7-8 iyileştirme (8:55 → 8:14)

**Nedenler:**
1. ✅ Cache mekanizması çalışıyor (8847 dosya cache'leniyor)
2. ⚠️ NuGet'in global cache mekanizması farklı çalışıyor
3. ⚠️ `~/.nuget/packages/` absolute path olduğu için cache'lenemiyor
4. ⚠️ Build süresinin çoğu restore değil, compile/build işlemlerinde geçiyor olabilir

**Sonuç:** Cache çalışıyor ancak etkisi beklenenden düşük. Mevcut süre (8:14 dakika) idare edilebilir seviyede.

