# docs/MngKeeper Klasörü — İnceleme ve Karar Özeti

**Tarih:** 26 Ocak 2026  
**Amaç:** `docs/MngKeeper/` (content dışı, MkDocs nav’da olmayan) altındaki dokümanları sınıflandırmak: işe yarayanlar → `docs/content/MngKeeper/support/` altına taşınacak; geçersiz/duplicate olanlar arşivlenecek veya silinecek.

---

## Özet Tablo

| Dosya | Değerlendirme | Öneri |
|-------|----------------|--------|
| **api/README.md** | ✅ İşe yarar | API özeti, başlangıç, endpoint grupları. `support/guides/API_OVERVIEW.md` olarak taşı veya `main/TECHNICAL_SPECS.md` için kaynak olarak kullan. |
| **changelog/CHANGELOG.md** | ❌ Geçersiz (duplicate) | Canonical zaten `content/MngKeeper/main/CHANGELOG.md`. Bu kopyayı sil veya arşivle. |
| **changelog/GATEWAY_INTEGRATION_CHANGES.md** | ✅ İşe yarar | Gateway entegrasyonu yapılan değişikliklerin özeti. `support/guides/` veya ROADMAP “Yapılanlar”a özet alınabilir; ya da `support/guides/GATEWAY_INTEGRATION_CHANGES.md` olarak taşı. |
| **changelog/VERSION.md** | ✅ İşe yarar | SemVer kuralları, assembly/package versiyon yönetimi. `support/setup/VERSION_MANAGEMENT.md` veya `support/guides/` altına taşı. |
| **changelog/WORK_SESSION_20251216.md** | ⚠️ Tarihsel | 16 Aralık 2025 oturum notları; “Mevcut Sorunlar” (token claims, 403) güncel mi bilinmiyor. Arşivlenebilir veya ROADMAP/CHANGELOG’a özetlendiyse silinebilir. |
| **guides/CLEANUP_INSTRUCTIONS.md** | ✅ İşe yarar | Keycloak/MongoDB test verisi temizleme. `support/guides/CLEANUP_INSTRUCTIONS.md` veya `support/troubleshooting/` altına taşı. |
| **guides/CURRENT_ACCESS_STATUS.md** | ✅ İşe yarar | Port, sertifika, erişim yolları (31 Aralık 2025). Güncelliğini kontrol edip `support/guides/` veya `support/setup/` altında tut. |
| **guides/DOMAIN_CREATION_EMAIL_NOTIFICATION.md** | ✅ İşe yarar | Domain oluşturma email bildirimi detaylı özellik dokümanı. `support/guides/DOMAIN_CREATION_EMAIL_NOTIFICATION.md` olarak taşı. |
| **guides/GATEWAY_INTEGRATION.md** | ❌ Duplicate | İçerik `content/MngKeeper/support/guides/GATEWAY_INTEGRATION.md` ile aynı. Bu kopyayı kaldır. |
| **guides/GATEWAY_JWT_VALIDATION_ISSUE.md** | ✅ İşe yarar | JWT validation sorunu ve çözüm notları. `support/troubleshooting/GATEWAY_JWT_VALIDATION_ISSUE.md` olarak taşı. |
| **guides/GATEWAY_TROUBLESHOOTING.md** | ✅ İşe yarar | Gateway + Keeper sorun giderme (docker, curl, token). `support/troubleshooting/GATEWAY_TROUBLESHOOTING.md` olarak taşı. |
| **guides/PRODUCTION_MIGRATION_PLAN.md** | ✅ İşe yarar | Production’a geçiş (port kapatma, HTTP/HTTPS). `support/guides/PRODUCTION_MIGRATION_PLAN.md` veya `support/setup/` altına taşı. |
| **guides/tests-README.md** | ✅ İşe yarar | Test yapısı ve çalıştırma. `support/guides/TESTS_README.md` olarak taşı (veya test script’lerinin yanında kalabilir; MkDocs’a dahil edilecekse support’a). |
| **licensing/LICENSING_ROADMAP.md** | ✅ İşe yarar | Lisanslama modeli roadmap. `support/guides/LICENSING_ROADMAP.md` veya `support/specs/` benzeri bir klasöre taşı. |
| **licensing/TEST_PLAN.md** | ✅ İşe yarar | Lisanslama test planı. `support/guides/LICENSING_TEST_PLAN.md` veya ilgili spec klasörüne taşı. |
| **setup/ENVIRONMENT_VARIABLES.md** | ✅ İşe yarar | Env değişkenleri referansı. `support/setup/ENVIRONMENT_VARIABLES.md` olarak taşı. |
| **specs/CODE_OPTIMIZATION_PLAN.md** | ⚠️ Kısmen geçersiz | v1.1.0’daki optimizasyonlar yapıldı (Redis, index, ExceptionHelper). Plan “tamamlandı” notu eklenip `support/specs/` veya “Yapılanlar”a taşınabilir; ya da arşiv. |

---

## Önerilen Aksiyonlar

### 1. Taşınacak (işe yarar) — support altında amaca göre

| Hedef | Kaynak |
|-------|--------|
| `support/guides/API_OVERVIEW.md` | `api/README.md` (veya TECHNICAL_SPECS için kaynak) |
| `support/guides/GATEWAY_INTEGRATION_CHANGES.md` | `changelog/GATEWAY_INTEGRATION_CHANGES.md` |
| `support/setup/VERSION_MANAGEMENT.md` | `changelog/VERSION.md` |
| `support/guides/CLEANUP_INSTRUCTIONS.md` | `guides/CLEANUP_INSTRUCTIONS.md` |
| `support/guides/CURRENT_ACCESS_STATUS.md` | `guides/CURRENT_ACCESS_STATUS.md` |
| `support/guides/DOMAIN_CREATION_EMAIL_NOTIFICATION.md` | `guides/DOMAIN_CREATION_EMAIL_NOTIFICATION.md` |
| `support/troubleshooting/GATEWAY_JWT_VALIDATION_ISSUE.md` | `guides/GATEWAY_JWT_VALIDATION_ISSUE.md` |
| `support/troubleshooting/GATEWAY_TROUBLESHOOTING.md` | `guides/GATEWAY_TROUBLESHOOTING.md` |
| `support/guides/PRODUCTION_MIGRATION_PLAN.md` | `guides/PRODUCTION_MIGRATION_PLAN.md` |
| `support/guides/TESTS_README.md` | `guides/tests-README.md` |
| `support/guides/LICENSING_ROADMAP.md` | `licensing/LICENSING_ROADMAP.md` |
| `support/guides/LICENSING_TEST_PLAN.md` | `licensing/TEST_PLAN.md` |
| `support/setup/ENVIRONMENT_VARIABLES.md` | `setup/ENVIRONMENT_VARIABLES.md` |

`support/troubleshooting/` ve `support/setup/` yoksa oluşturulmalı (DOCUMENTATION_STANDARDS §3.5.2’ye uygun).

### 2. Duplicate / geçersiz — kaldırılabilir veya arşiv

- **changelog/CHANGELOG.md** — Silinebilir (canonical: `content/MngKeeper/main/CHANGELOG.md`).
- **guides/GATEWAY_INTEGRATION.md** — Silinebilir (canonical: `content/MngKeeper/support/guides/GATEWAY_INTEGRATION.md`).

### 3. Tarihsel / karar sonrası

- **changelog/WORK_SESSION_20251216.md** — Arşiv (örn. `docs/archive/MngKeeper/`) veya “Yapılanlar” özetlendiyse silinebilir.
- **specs/CODE_OPTIMIZATION_PLAN.md** — Üstüne “Tamamlandı (v1.1.0)” notu eklenip `support/specs/` veya `support/guides/` altına taşınabilir; ya da arşiv.

---

## Sonuç

- **Taşınacak (13 dosya):** Hepsi `docs/content/MngKeeper/support/` altında uygun klasörle (guides, setup, troubleshooting, specs) konumlandırılmalı.
- **Kaldırılabilecek (2 dosya):** `changelog/CHANGELOG.md`, `guides/GATEWAY_INTEGRATION.md` (canonical’lar content’te).
- **Arşiv / not ekle (2 dosya):** `WORK_SESSION_20251216.md`, `CODE_OPTIMIZATION_PLAN.md`.

Bu tabloya göre taşıma ve silme işlemleri yapıldıktan sonra `docs/MngKeeper/` klasörü boşaltılabilir veya yalnızca arşivlik kopyalar bırakılabilir.
