# GitLab Pages Artifacts Size Sorunu - Çözüm

**Tarih:** 15 Ocak 2025  
**Sorun:** Pages job'u artifacts upload sırasında "413 Request Entity Too Large" hatası veriyordu

---

## 🔍 Sorun Analizi

### Hata Mesajı
```
ERROR: Uploading artifacts as "archive" to coordinator... 413 Request Entity Too Large
FATAL: too large
```

### Sorunun Nedeni
- Artifacts boyutu: **6.9M**
- GitLab'ın artifact upload limit'i aşıldı
- Public klasöründe gereksiz dosyalar vardı (.map, .log, .cache)

---

## ✅ Uygulanan Çözümler

### 1. Artifacts Exclude Eklendi

`.gitlab-ci.yml` dosyasında `pages` job'una `exclude` eklendi:

```yaml
artifacts:
  paths:
    - public
  exclude:
    # Gereksiz dosyaları hariç tut (cache, log, vb.)
    - public/**/*.map
    - public/**/*.log
    - public/.cache/
  expire_in: 30 days
```

**Etki:** Artifacts upload sırasında bu dosyalar hariç tutulur.

---

### 2. Script İçinde Temizlik Eklendi

`pages` job'unun script'ine dosya temizleme komutları eklendi:

```bash
# Gereksiz dosyaları temizle (artifacts size'ı küçültmek için)
echo "Cleaning up unnecessary files to reduce artifacts size..."
find public -name "*.map" -type f -delete 2>/dev/null || true
find public -name "*.log" -type f -delete 2>/dev/null || true
find public -type d -name ".cache" -exec rm -rf {} + 2>/dev/null || true
echo "Cleanup completed"
```

**Etki:** Public klasöründen gereksiz dosyalar silinir, artifacts boyutu küçülür.

---

## 📊 Beklenen Sonuçlar

### Artifacts Boyutu
- **Önce:** 6.9M
- **Sonra:** ~5-6M (tahmini, .map ve .log dosyalarına bağlı)

### Dosya Sayısı
- **Önce:** 57 dosya
- **Sonra:** ~50-55 dosya (tahmini)

---

## 🔄 Alternatif Çözümler (Gerekirse)

### Seçenek 1: GitLab Config'de Artifact Size Limit Artırma

GitLab config'de artifact size limit'ini artırabilirsiniz:

```ruby
# /etc/gitlab/gitlab.rb
gitlab_rails['artifacts_max_size'] = 100.megabytes
```

**Not:** Self-hosted GitLab için geçerlidir.

---

### Seçenek 2: Artifacts'ı Compress Etme

Artifacts'ı compress ederek boyutu küçültebilirsiniz:

```yaml
artifacts:
  paths:
    - public
  reports:
    # Compress artifacts
    archive:
      paths:
        - public
```

**Not:** GitLab otomatik olarak artifacts'ı compress eder, bu seçenek genellikle gerekmez.

---

### Seçenek 3: Daha Agresif Temizlik

Daha fazla dosya türünü temizleyebilirsiniz:

```bash
# Daha agresif temizlik
find public -name "*.map" -type f -delete
find public -name "*.log" -type f -delete
find public -type d -name ".cache" -exec rm -rf {} +
find public -name "*.gz" -type f -delete  # Eğer gerekirse
find public -name "*.br" -type f -delete  # Brotli compressed files
```

---

## 🧪 Test Sonuçları

### Pipeline Test
- **Commit:** `fix: Optimize Pages artifacts to reduce size`
- **Push:** ✅ Başarılı
- **Pipeline:** Çalışıyor...

### Beklenen Sonuç
- ✅ Pages job'u artifacts upload başarılı
- ✅ Artifacts boyutu limit altında
- ✅ GitLab Pages deploy edildi

---

## 📋 Kontrol Listesi

Pipeline tamamlandıktan sonra kontrol edin:

- [ ] Pages job'u başarılı
- [ ] Artifacts upload başarılı (413 hatası yok)
- [ ] Artifacts boyutu limit altında
- [ ] GitLab Pages erişilebilir

---

## 🔗 İlgili Dosyalar

- `.gitlab-ci.yml` - Pages job yapılandırması
- `docs/content/cicd/PAGES_ARTIFACTS_FIX.md` - Bu dosya

---

**Son Güncelleme:** 15 Ocak 2025

