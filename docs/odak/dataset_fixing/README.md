# Dataset Fixing — Odak Sahaya Çıkış

**Başlangıç:** 10 Haziran 2026  
**Durum:** ✅ **Odak tamamlandı** — [CURRENT_STATUS.md](./CURRENT_STATUS.md)  
**Kapsam:** Mng.Ui dataset ekranı + MngDataGateway dataset/kategori organizasyonu  
**Ortam:** Odak (`192.168.20.20`, domain `odak`) · Production ertelendi

---

## Amaç (özet)

Sahaya çıkış öncesi dataset'leri amaca uygun kategorilere yerleştirmek; manager'ın yalnızca **iş/referans** dataset'lerini görmesini sağlamak; legacy/demo kalıntılarını temizlemek.

**Odak sonucu:** 60 dataset · 0 kategorisiz · manager'da 3 dataset · 1 AF form (`tedarikciler-form`).

---

## Çalışma Kuralları

| Konu | Kural |
|------|--------|
| **Backend deploy** | Odak'ta otomatik yapılabilir |
| **UI deploy** | Kullanıcı onayı olmadan yapılmaz |
| **Production** | Ayrı oturum + onay (bu çalışma kapsamı dışı) |
| **Dokümantasyon** | `docs/odak/dataset_fixing/` |
| **DG isAdmin guard** | Eklenmeyecek (bilinçli — UI `isSystemCategory` filtresi) |

---

## Dokümanlar

| Dosya | İçerik |
|-------|--------|
| **[CURRENT_STATUS.md](./CURRENT_STATUS.md)** | **Oturum özeti, metrikler, nerede kaldık** |
| [INVENTORY.md](./INVENTORY.md) | Son Odak envanter tablosu |
| [CATEGORIES.md](./CATEGORIES.md) | Hedef kategori taksonomisi |
| [PLAN.md](./PLAN.md) | Faz planı (tamamlandı) |
| [DECISIONS.md](./DECISIONS.md) | Alınan kararlar |
| [scripts/](./scripts/) | audit · apply · cleanup script'leri + manifest JSON |

---

## Script'ler

| Script | Amaç |
|--------|------|
| `audit-datasets-odak.ps1` | Envanter + INVENTORY.md güncelleme |
| `apply-category-taxonomy-odak.ps1` | Kategori oluştur/güncelle, dataset ata |
| `cleanup-legacy-datasets-odak.ps1` | AF form, side menu, legacy schema silme |

Manifest: `category-taxonomy-manifest.json`, `legacy-cleanup-manifest.json`

---

## Koruma modeli

Manager (admin değil) **görmez:** `isSystemCategory: true` kategorilerindeki tüm dataset'ler.  
Manager **görür/düzenler:** BusinessDatasets, ReferenceDatasets ve kendi oluşturduğu kategoriler.

---

## Sonraki adım (production — ertelendi)

Aynı manifest'ler production DG'de uygulanacak; ayrı oturum ve onay gerekir. Bkz. [CURRENT_STATUS.md](./CURRENT_STATUS.md).
