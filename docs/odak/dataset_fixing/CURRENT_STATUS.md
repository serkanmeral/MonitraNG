# Dataset Fixing — Oturum Durumu

**Son güncelleme:** 10 Haziran 2026  
**Durum:** ✅ Odak hedefleri tamamlandı — oturum kapatıldı  
**Ortam:** Odak (`192.168.20.20`, domain `odak`) · Production **ertelendi**

---

## Son Çalışılan Konu

Mng.Ui + MngDataGateway dataset/kategori organizasyonu — sahaya çıkış öncesi Odak ortamında envanter, taksonomi uygulama ve legacy temizlik.

---

## Tamamlanan İşler

### Analiz ve planlama
- [x] Odak canlı envanter (74 dataset → analiz)
- [x] Hedef kategori taksonomisi ([CATEGORIES.md](./CATEGORIES.md))
- [x] Mimari kararlar ([DECISIONS.md](./DECISIONS.md)) — DG'de isAdmin guard **eklenmeyecek** (bilinçli)

### Odak uygulama
- [x] **Kategori taksonomisi** — `apply-category-taxonomy-odak.ps1`
  - 4 yeni kategori: ReferenceDatasets, SchedulerDatasets, WorkflowDatasets, LegacyDatasets (sonra silindi)
  - OC, Notifier, DI, ChatRoom → `isSystemCategory: true`
  - `chat_room_datasets` → **ChatRoomDatasets**
  - Kategorisiz dataset: **0**
- [x] **Legacy temizlik** — `cleanup-legacy-datasets-odak.ps1`
  - 3 AF form + 3 side menu (demo kitap)
  - 14 legacy schema silindi (`tm_*`, `tst_*`, `@test_files`)
  - Boş kategoriler silindi (Car Category, Book Categories, LegacyDatasets)

### Repo seed güncellemeleri
- [x] `operationcore_dataset_category.json` → `isSystemCategory: true`
- [x] `notifier_dataset_category.json` → `isSystemCategory: true`
- [x] `documentintelligence_dataset_category.json` → `isSystemCategory: true`

---

## Odak Final Metrikleri

| Metrik | Başlangıç | Final |
|--------|----------:|------:|
| Dataset | 74 | **60** |
| Kategori | 10 | **11** |
| Kategorisiz dataset | 13 | **0** |
| Sistem kategorisi | 3 | **9** |
| Manager-visible dataset | ~40+ | **3** |
| AF form (iş) | 4 | **1** (`tedarikciler-form`) |

**Manager görür:** `tedarikciler` · `ulkeler` · `sehirler`

**Kategori dağılımı (manager):**
- BusinessDatasets → `tedarikciler`
- ReferenceDatasets → `ulkeler`, `sehirler`

---

## Devam Eden / Ertelenen

| Konu | Durum |
|------|--------|
| Production (`192.168.20.8`) manifest uygulama | ⏸ Ertelendi — erken |
| Manager token ile UI smoke test | ⏸ İsteğe bağlı |
| MongoDB legacy collection veri temizliği | ⏸ Schema silindi; collection'lar DB'de kalabilir |
| UI deploy | ⏸ Gerekmedi (mevcut filtre yeterli) |

---

## Sonraki Oturum (öneri)

1. Production için aynı script'ler — ayrı onay + bakım penceresi
2. İsteğe bağlı: manager kullanıcı ile `/apps/datasets` smoke test
3. İsteğe bağlı: silinen schema'lara ait MongoDB collection drop (veri analizi sonrası)
4. Yeni modül dataset'leri eklendiğinde manifest'e ekleme standardı

---

## Script Referansı

```powershell
# Envanter
pwsh -File docs/odak/dataset_fixing/scripts/audit-datasets-odak.ps1 -UpdateInventoryMarkdown

# Kategori taksonomisi (yeni ortam)
pwsh -File docs/odak/dataset_fixing/scripts/apply-category-taxonomy-odak.ps1

# Legacy temizlik (yeni ortam — dikkatli)
pwsh -File docs/odak/dataset_fixing/scripts/cleanup-legacy-datasets-odak.ps1 -DryRun
```

---

## Nerede Kalmıştık

Odak dataset fixing **hedefe ulaştı**. Production'a taşıma yapılmadı. Dokümantasyon `docs/odak/dataset_fixing/` altında güncel.
