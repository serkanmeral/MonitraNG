# Dataset Fixing — Hedef Kategori Taksonomisi

**Durum:** ✅ Odak'ta uygulandı · Production ertelendi  
**Oturum kapanış:** [CURRENT_STATUS.md](./CURRENT_STATUS.md)  
**Kaynak envanter:** [INVENTORY.md](./INVENTORY.md) (Odak, 74 dataset, 10 kategori)

---

## 1. İlke

Kategori ataması **yalnızca** `isSystemCategory` bayrağından ibaret değildir. Her dataset:

1. **Amacına uygun** bir kategori adı altında gruplanmalı (modül / domain / veri tipi)
2. **Manager erişim modeline** uygun bayrak taşımalı (`isSystemCategory`)
3. Kategorisiz **kalmamalı** (istisna: silme sürecindeki geçici durum)

```text
Kategori kararı = (Anlamlı grup adı) + (isSystemCategory: manager görünürlüğü)
```

| Katman | isSystemCategory | Kim görür / düzenler | Örnek |
|--------|------------------|----------------------|--------|
| Platform / modül altyapısı | `true` | Yalnızca admin | `op_*`, `@side_menu`, `mon_*` |
| İş verisi (AF, operasyonel kayıt) | `false` | Manager (+ admin) | `tedarikciler` |
| Paylaşılan referans / lookup | `false` | Manager (çoğu domain) | `ulkeler`, `sehirler` |
| Legacy / silme adayı | `true` (geçici) | Admin temizliği | `tm_*`, `tst_*` |

---

## 2. Mevcut Durum — Sorunlar

| Sorun | Etki | Adet |
|-------|------|-----:|
| Kategorisiz dataset | UI'da dağınık, manager yanlışlıkla dokunabilir | 13 |
| Modül kategorisi var ama `isSystemCategory: false` | Manager platform şemasını görür | ~35 |
| Boş / anlamsız kategori | Gürültü | 1 (`Car Category`) |
| Demo kategori + AF bağlı | Silme kararı net değil | `Book Categories` (4) |
| İş + lookup aynı kategoride | `BusinessDatasets` hem `tedarikciler` hem `ulkeler` | 1 kategori |
| Eski Task Manager seti kategorisiz | Legacy görünürlük | 9 `tm_*` |
| Widget dağılımı | `@widgets` System'de, `@widget_templates` WidgetDatasets'te | 2 kategori |

---

## 3. Hedef Kategori Listesi

### 3.1 Sistem kategorileri (`isSystemCategory: true`)

Manager **görmez**. Mevcut kategoriler güncellenir veya yeni oluşturulur.

| # | categoryName | Durum | Açıklama | Dataset'ler |
|---|--------------|-------|----------|-------------|
| S1 | **System Datasets** | Mevcut — genişlet | UI ve uygulama çekirdeği | `@side_menu`, `@automated_forms`, `@user_preferences`, `@user_notes`, `@widgets`, `@widget_categories`, `@dashboards` |
| S2 | **WidgetDatasets** | Mevcut — birleştir veya kaldır | Widget şablon kataloğu | `@widget_templates` *(S1 ile birleştirme seçeneği: tek Widget kategorisi)* |
| S3 | **OperationCoreDatasets** | Mevcut — **`isSystemCategory: true` yap** | MngOperations `op_*` | 24 dataset |
| S4 | **Monitoring** | Mevcut ✅ | İzleme altyapısı `mon_*` | 11 dataset |
| S5 | **NotifierDatasets** | Mevcut — **`isSystemCategory: true` yap** | E-posta / bildirim şablonları | `@mail_*`, `@notification_templates` |
| S6 | **ChatRoomDatasets** | Mevcut `chat_room_datasets` — **rename + sistem bayrağı** | Sohbet modülü `cht_*` | 5 dataset |
| S7 | **DocumentIntelligenceDatasets** | Mevcut — **`isSystemCategory: true` yap** | Doküman modülü `dm_*` | 3 dataset |
| S8 | **SchedulerDatasets** | **YENİ** | MngScheduler job tanımları | `@scheduled_jobs`, `@job_executions` |
| S9 | **WorkflowDatasets** | **YENİ** | MngWorkflow pipeline tanımları | `@wf_validation_pipelines` |
| S10 | **LegacyDatasets** | **YENİ** (geçici) | Kaldırılacak / arşiv; manager erişmesin | `tm_*` (9), silme öncesi `tst_*`, `@test_files` |

**Widget birleştirme notu:** `@widgets` + `@widget_categories` + `@dashboards` + `@widget_templates` tek **Widget & Dashboard** kategorisinde toplanabilir. Alternatif: S1 (UI core) + S2 (widget template) ayrımı korunur — admin için de okunabilir.

### 3.2 İş kategorileri (`isSystemCategory: false`)

Manager **görür ve düzenleyebilir** (AF, iş verisi, referans).

| # | categoryName | Durum | Açıklama | Dataset'ler |
|---|--------------|-------|----------|-------------|
| B1 | **BusinessDatasets** | Mevcut — daralt | Operasyonel / master iş kayıtları | `tedarikciler` |
| B2 | **ReferenceDatasets** | **YENİ** | Paylaşılan lookup; birden fazla form/modül referans alır | `ulkeler`, `sehirler` |

**Gerekçe (B1/B2 ayrımı):** `tedarikciler` manager'ın AF ile yönettiği asıl iş verisi; `ulkeler`/`sehirler` destekleyici referans. Aynı kategoride kalması kafa karıştırıcı — manager "iş dataset'i" ile "sözlük"ü ayırt edemez.

### 3.3 Kaldırılacak kategoriler

| categoryName | Gerekçe |
|--------------|---------|
| **Car Category** | Boş, anlam taşımıyor |
| **Book Categories** | Demo/test; dataset'ler silinince veya Legacy'ye taşınınca kategori de silinir |

---

## 4. Dataset → Hedef Kategori Haritası

### Kategorisiz → hedef (13 dataset)

| Dataset | Hedef kategori | Not |
|---------|----------------|-----|
| `@scheduled_jobs` | SchedulerDatasets (S8) | Yeni kategori |
| `@job_executions` | SchedulerDatasets (S8) | Yeni kategori |
| `@wf_validation_pipelines` | WorkflowDatasets (S9) | Yeni kategori |
| `tm_projects` … `tm_statuses` (9 adet) | LegacyDatasets (S10) | Silme kararı bekleniyor |
| *(kontrol)* | — | Envanterde 13 kategorisiz; yukarıdaki 12 + bir tane daha olabilir — script ile doğrula |

### Mevcut kategori → düzeltme

| Dataset grubu | Şu an | Hedef | Aksiyon |
|---------------|-------|-------|---------|
| `op_*` (24) | OperationCoreDatasets, `false` | OperationCoreDatasets, **`true`** | Kategori bayrağı güncelle |
| `@mail_*`, `@notification_*` | NotifierDatasets, `false` | NotifierDatasets, **`true`** | Kategori bayrağı güncelle |
| `cht_*` (5) | chat_room_datasets, `false` | ChatRoomDatasets, **`true`** | Rename + bayrak |
| `dm_*` (3) | DocumentIntelligenceDatasets, `false` | DocumentIntelligenceDatasets, **`true`** | Kategori bayrağı güncelle |
| `ulkeler`, `sehirler` | BusinessDatasets | **ReferenceDatasets** (B2) | Yeni kategori + taşı |
| `tedarikciler` | BusinessDatasets | BusinessDatasets (B1) | Kalır |
| `tst_*`, `@test_files` | Book Categories | LegacyDatasets (S10) veya sil | AF formları önce kaldır |
| `@widgets`, `@widget_categories`, `@dashboards` | System Datasets | System Datasets veya Widget birleşik | Tutarlılık kararı |

---

## 5. Uygulama Sırası (önerilen)

```text
✅ Legacy temizlik (Odak 10 Haz 2026) — cleanup-legacy-datasets-odak.ps1
Production ortamina ayni manifest — ayri oturum
```

**Script (planlanan):** `scripts/apply-category-taxonomy-odak.ps1` — JSON manifest ile idempotent uygulama.

---

## 6. Manager Deneyimi (hedef sonrası)

Manager dataset listesinde **yalnızca** şunları görür:

| Kategori | Dataset |
|----------|---------|
| BusinessDatasets | `tedarikciler` |
| ReferenceDatasets | `ulkeler`, `sehirler` |
| *(ileride manager oluşturdukları)* | … |

AF form oluştururken dataset seçimi aynı filtreyle yalnızca iş/referans dataset'lerini listeler.

---

## 7. Açık Kararlar (onay bekliyor)

| # | Konu | Seçenekler |
|---|------|------------|
| K1 | Widget kategorileri | A) S1+S2 ayrı kalır · B) Tek **Widget & Dashboard** kategorisi |
| K2 | Document Intelligence | Manager'ın `dm_*` şemasını görmesi gerekir mi? → varsayılan: **hayır** (S7 sistem) |
| K3 | Legacy `tm_*` | A) LegacyDatasets'e taşı · B) Doğrudan sil (veri yoksa) |
| K4 | Demo `tst_*` | A) Legacy'ye taşı · B) AF form + dataset sil |
| K5 | ReferenceDatasets | Manager referans veriyi (ülke/şehir) düzenleyebilsin mi? → varsayılan: **evet** |

Kararlar [DECISIONS.md](./DECISIONS.md) dosyasına işlenecek.

---

## 8. İlgili dosyalar

| Dosya | Rol |
|-------|-----|
| [INVENTORY.md](./INVENTORY.md) | Canlı envanter |
| [PLAN.md](./PLAN.md) | Faz planı |
| [scripts/audit-datasets-odak.ps1](./scripts/audit-datasets-odak.ps1) | Envanter script |
| [scripts/category-taxonomy-manifest.json](./scripts/category-taxonomy-manifest.json) | Uygulama manifesti (oluşturulacak) |
