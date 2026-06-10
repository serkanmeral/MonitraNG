# Dataset Fixing — Çalışma Planı

**Son güncelleme:** 10 Haziran 2026  
**Durum:** ✅ Odak fazları tamamlandı · Production ertelendi

---

## 1. Hedef Kategori Modeli (uygulandı)

```text
Sistem kategorileri (isSystemCategory: true) — manager GÖRMEZ
  System Datasets · WidgetDatasets · OperationCoreDatasets · Monitoring
  NotifierDatasets · ChatRoomDatasets · DocumentIntelligenceDatasets
  SchedulerDatasets · WorkflowDatasets

İş kategorileri (isSystemCategory: false) — manager GÖRÜR
  BusinessDatasets (tedarikciler)
  ReferenceDatasets (ulkeler, sehirler)
```

Detay: [CATEGORIES.md](./CATEGORIES.md) · Karar: modül bazlı sistem kategorileri (D-04, D-09).

---

## 2. Dataset Sınıflandırma Matrisi

Her dataset envanter sonrası şu tiplerden birine atanır:

| Tip | Tanım | Kategori hedefi | Manager erişimi | Örnekler |
|-----|--------|-----------------|-----------------|----------|
| **A — Platform core** | Uygulama altyapısı; bozulursa sistem çöker | System Datasets (`isSystemCategory: true`) | Yok | `@side_menu`, `@automated_forms`, `@datasets` meta |
| **B — Modül infra** | Modülün çalışması için gerekli şema | Modül sistem kategorisi veya System Datasets | Yok | `@widgets`, `@widget_categories`, `@dashboards`, `op_*`, `@mail_*` |
| **C — AF iş verisi** | Manager'ın Dynamic Form ile yönettiği veri | İş kategorisi | Tam CRUD (veri + şema) | `tedarikciler` |
| **D — Paylaşılan lookup** | Birden fazla form/modülün referans aldığı veri | İş veya ortak referans kategorisi | Read + sınırlı edit | `ulkeler`, `sehirler` |
| **E — Legacy / demo** | POC, test, artık kullanılmayan | — | Kaldırma adayı | `@books`, `@test_*` |

---

## 3. Fazlar

### Faz 0 — Hazırlık ✅ (bu oturum)

- [x] Çalışma kuralları netleştirildi (UI deploy onaylı, backend otomatik)
- [x] DG isAdmin yokluğu bilinçli karar olarak dokümante edildi
- [x] Plan dokümanı oluşturuldu

### Faz 1 — Envanter (Odak canlı) ✅

**Amaç:** Mevcut durumu ölçülebilir hale getirmek.

**Çıktı:** [INVENTORY.md](./INVENTORY.md)  
**Script:** [scripts/audit-datasets-odak.ps1](./scripts/audit-datasets-odak.ps1)

### Faz 2 — Kategori taksonomisi ✅

- [x] [CATEGORIES.md](./CATEGORIES.md)
- [x] K1–K5 onaylandı → [DECISIONS.md D-09](./DECISIONS.md)
- [x] Manifest + `apply-category-taxonomy-odak.ps1`

### Faz 3 — Taksonomi uygulama (Odak) ✅

- [x] 4 yeni kategori, bayrak/rename, dataset atamaları
- [x] Car Category + Book Categories silindi
- [x] Kategorisiz: **0**

### Faz 4 — Legacy temizlik (Odak) ✅

- [x] `cleanup-legacy-datasets-odak.ps1`
- [x] 14 schema, 3 AF, 3 side menu silindi
- [x] LegacyDatasets kategorisi silindi

### Faz 5 — Doğrulama (Odak) ✅

- [x] Final envanter: 60 dataset, manager-visible 3, AF 1
- [ ] Manager token UI smoke — isteğe bağlı, yapılmadı

### Faz 6 — Production ⏸ Ertelendi

- [ ] `apply-category-taxonomy-odak.ps1` (prod BaseUrl)
- [ ] `cleanup-legacy-datasets-odak.ps1` (prod — dikkatli)
- [ ] Bakım penceresi + yedek

---

## 4. Bilinen Platform Dataset'leri (Başlangıç Referansı)

Repo dokümantasyonundan; canlı envanter ile doğrulanacak.

### UI altyapısı (A)

| Dataset | Beklenen kategori |
|---------|-------------------|
| `@side_menu` | System Datasets |
| `@automated_forms` | System Datasets |

### Widget / Dashboard (B)

| Dataset | Beklenen kategori |
|---------|-------------------|
| `@widgets` | WidgetDatasets veya System Datasets |
| `@widget_categories` | WidgetDatasets |
| `@widget_templates` | WidgetDatasets |
| `@dashboards` | System Datasets veya WidgetDatasets |

### OperationCore (B — tartışılacak)

Tüm `op_*` dataset'leri — şu an `OperationCoreDatasets` (`isSystemCategory: false`). Sahaya çıkış için **`isSystemCategory: true` yapılması** veya System Datasets altına taşınması önerilir.

Kaynak: [operationcore/datasets/operationcore_datasets_phase1_current_final_2026-05-25.json](../operationcore/datasets/operationcore_datasets_phase1_current_final_2026-05-25.json)

### Notifier (B)

| Dataset | Not |
|---------|-----|
| `@mail_templates` | |
| `@mail_layouts` | |
| `@notification_templates` | |

### Kullanıcı özellikleri (B veya A)

| Dataset | Not |
|---------|-----|
| `@user_preferences` | |
| `@user_notes` | |

### İş / AF verisi (C/D)

| Dataset | Not |
|---------|-----|
| `tedarikciler` | Dynamic Forms POC |
| `ulkeler`, `sehirler` | Lookup referans |

---

## 5. Silme Politikası

Dataset **schema silme** (`DELETE /api/v1/datasets/{name}`):

- Yalnızca **E tipi** ve tüm referans kontrolleri geçtikten sonra
- Collection ve veri **silinmez** — ayrı temizlik adımı gerekir
- Production'da önce yedek / staging doğrulama

**Kontrol listesi (silmeden önce):**

- [ ] Hiçbir `@automated_forms` kaydında `datasetName` olarak geçmiyor
- [ ] `@side_menu` veya widget/dashboard referansı yok
- [ ] Başka dataset'lerin `relationDataset` alanında yok
- [ ] Kod tabanında hardcoded referans yok
- [ ] Kullanıcı onayı alındı

---

## 6. Riskler

| Risk | Azaltma |
|------|---------|
| Yanlış kategoriye taşıma → Manager platform dataset'ini görür | Envanter + admin smoke test |
| Schema silme → AF form kırılır | Referans kontrolü, soft-delete önce |
| OperationCore dataset'lerini sistem kategorisine almak → OC admin UI etkilenir mi? | OC dataset yönetimi DG UI üzerinden değil script/MO üzerinden; doğrula |
| Kategori dataId değişirse mevcut dataset.category kopar | Taşıma script'inde ID map kullan |
