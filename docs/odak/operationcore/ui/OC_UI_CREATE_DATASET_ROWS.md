# Mng.Ui — `createDatasetRows` aksiyon tanımlama ekranı

**Durum:** Implementasyon (CDR-4) — `OcCreateDatasetRowsActionEditor` + Kurallar dialog  
**Son güncelleme:** 11 Temmuz 2026  
**MO spec:** [CREATE_DATASET_ROWS_ACTION_SPEC.md](../mngoperations/CREATE_DATASET_ROWS_ACTION_SPEC.md)  
**Kurallar zemini:** [OC_UI_RULES_FAZ1.md](./OC_UI_RULES_FAZ1.md) (R-Plus / automation)  
**Mapping referansı:** [OC_UI_WORKSPACE_AUTOMATIONS.md](./OC_UI_WORKSPACE_AUTOMATIONS.md) (fieldMappings dili)

---

## 1. Amaç

Workspace **Tanımları → Kurallar** akışında, automation kuralına `createDatasetRows` aksiyonunu **JSON elle yazmadan** tanımlayabilmek.

Bu ekran teslimatı **zorunludur** (CDR-4): yalnızca seed JSON ile bırakılmaz; yönetici süreç tanımını UI’dan yapar.

> **Not:** Bu, **MngWorkflow** (workflow designer) ekranı değildir. Yer: Operation Core workspace kural editörü. Çok adımlı Workflow ayrı ürün; bu aksiyon `op_rules` inline automation’dadır.

---

## 2. Yerleşim

```text
Workspace Tanımları
  └─ Kurallar (OcWorkspaceRulesExplorer / OcWorkspaceRuleDialog)
       └─ Kural türü: Automation
            └─ Etki / Aksiyonlar (OcRuleEffectPanel veya eşdeğeri)
                 └─ Aksiyon tipi: «Dataset satırları oluştur» (createDatasetRows)
                      └─ CreateDatasetRowsActionEditor  ← YENİ
```

Mevcut R-Plus backlog (**R6**): automation action picker. Bu özellik R6’yı genişletir:

| ID | İş |
|----|-----|
| **R6** | Automation action picker (mevcut plan: mail, bildirim, activity, …) |
| **R6-CDR** | `createDatasetRows` tipi + **CreateDatasetRowsActionEditor** |

---

## 3. Ekran gereksinimleri

### 3.1 Aksiyon seçimi

- Action type dropdown / kart: **Dataset satırları oluştur**
- Kısa yardım metni: “Kaynak işten DG dataset’ine 1…N satır üretir; geçiş başarısız olursa satır yazılmaz (failTransition).”

### 3.2 Hedef dataset

- Dataset adı: text veya DG dataset listesinden select (tercih: select + serbest yazım fallback)
- Seçilince hedef alan listesi (schema) yüklenir → mapping `target` autocomplete

### 3.3 Cardinality paneli

| UI | Bağlanan alan |
|----|----------------|
| Mod: Tek satır / Sayıdan / Listeden genişlet | `cardinality.mode`: `single` \| `count` \| `expand` |
| Sayı alanı path | `countFrom` (WI alan picker) |
| Liste alanı path | `itemsFrom` |
| Satır değişken adı | `itemAs` (varsayılan `item` / `serial`) |

Mod’a göre gereksiz alanlar gizlenir.

### 3.4 Idempotency paneli

- Mod: Yok / Kaynak başına bir kez (`none` \| `one_per_source`)
- Hedef lookup alanı (dataset field select)
- Kaynak değer: WI `key` / `id` / alan path

### 3.5 Hata politikası

- Select: **Geçişi reddet** (`failTransition`) — varsayılan, önerilen
- **Devam et + kayıt** (`continue`) — uyarı metni ile (stok senaryolarında önerme)

### 3.6 Alan eşleme editörü

Otomatik işler mapping UI’si ile **aynı UX dili** (mümkünse paylaşılan bileşen):

| Kolon | İçerik |
|-------|--------|
| Hedef alan | Dataset field select |
| Kaynak türü | field / static / token / item / **sequence** |
| Değer | Path picker, sabit değer, token şablonu, item path, **sequence şablonu** (`SERI-{000}`, `startFrom`) |

- Satır ekle / sil / sırala
- Incremental / sistem alanları (ör. `demirbasNo`) listede disabled veya uyarı
- Canlı özet: “N satır şablonu: alan1 ← …, alan2 ← …”
- `sequence` seçiliyken: şablon + başlangıç (`startFrom` / `startFromPath`); `{00}` pad ipucu

### 3.7 Önizleme / doğrulama (dialog içi)

- Zorunlu: `dataset`, en az bir mapping, cardinality tutarlılığı
- `expand` iken `itemsFrom` + `item` mapping uyarısı
- Kaydetmeden önce client-side schema check (hedef alan adları)

Dry-run (MO endpoint) → R-Future; MVP’de şart değil.

---

## 4. Liste / özet

Kurallar tablosu ve dialog sağ önizlemede aksiyon özeti:

```text
Dataset satırları oluştur → zimmet_demirbaslar (expand: miktar / seriNoListesi)
```

Helper: `formatRuleThenSummary` / `ocWorkspaceRules.ts` genişletmesi.

---

## 5. i18n

`operationCore.workspaceDefinitions.rules.actions.createDatasetRows.*` (TR/EN):

- Başlık, yardım, cardinality etiketleri, idempotency, onError, mapping kolonları, validation mesajları

---

## 6. DoD (CDR-4)

1. Admin, GIR tipi + Kapalı geçişi için automation kuralı oluşturur.
2. Aksiyonu UI’dan `createDatasetRows` olarak doldurur (dataset, cardinality, mapping, idempotency).
3. Kayıt `op_rules.actions` JSON’una spec’e uygun yazılır.
4. Düzenle → aynı form dolu gelir (round-trip).
5. (CDR-5 ile) Odak’ta GIR kapatınca demirbaş oluşur; UI’sız seed’e bağımlı kalınmaz.

---

## 7. Kod indeksi (hedef)

| Ne | Dosya (öneri) |
|----|----------------|
| Action editor | `OcCreateDatasetRowsActionEditor.vue` |
| Effect panel entegrasyonu | `OcRuleEffectPanel.vue` |
| Mapping satırları | Paylaşım: otomasyon mapping bileşeni veya `OcFieldMappingEditor.vue` |
| Özet | `utils/ocWorkspaceRules.ts` |
| Tipler | `types` / `ocWorkspaceRules` action union |

---

## 8. Bilinçli kapsam dışı (UI)

- MngWorkflow canvas’ına bu aksiyonu eklemek
- Kural simülasyon sandbox (R11)
- `updateDatasetRows` editörü (ayrı faz; aynı kabuk yeniden kullanılır)

---

*Üst spec: [CREATE_DATASET_ROWS_ACTION_SPEC.md](../mngoperations/CREATE_DATASET_ROWS_ACTION_SPEC.md).*
