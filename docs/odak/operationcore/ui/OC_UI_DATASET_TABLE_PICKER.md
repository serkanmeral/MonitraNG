# Spec — Dataset tablo seçici (Table Picker)

**Durum:** TP-1 + TP-ZIM uygulandı (zengin kolonlar, sort, multiselect chip UX; zimmet `demirbasIds`)  
**Son güncelleme:** 11 Temmuz 2026  
**Karar oturumu:** Zimmet ZIM — çok demirbaş / zengin seçim UI  
**İlgili:** [OC_UI_LOOKUP_FIELDS.md](./OC_UI_LOOKUP_FIELDS.md) (L1–L5) · [OC_UI_FORM_DEFINITIONS.md](./OC_UI_FORM_DEFINITIONS.md) · Zimmet: [PLAN.md](../../zimmet/PLAN.md) · [current_status.md](../../zimmet/current_status.md)

> **Kalan:** TP-2 (kolon filter UI), TP-3 (admin sütun editörü), TP-AF, TP-OC1 (`updateDatasetRows`).

---

## 1. Amaç

Person / dizin seçiciye benzer, **herhangi bir DG dataset** için modal **tablo seçici**:

> Form alanında satır(lar) seçilir; değer FK id (veya id dizisi) olarak saklanır. Dialog’da sütunlar, sıralama, arama/filtre ve sayfalama yönetilir.

**Birincil senaryo (Zimmet):** Bir personele aynı anda laptop + klavye + mouse zimmetlemek → **çoklu seçim (multiselect)** zorunlu.

**İkincil:** Tek demirbaş, tedarikçi, lokasyon vb. — aynı bileşen, `cardinality: single`.

---

## 2. Kararlar (kilitli)

| Konu | Karar |
|------|--------|
| Yeni `fieldType`? | **Hayır (MVP).** Mevcut `relation` (+ `options.lookup`) genişletilir; sunum `presentation: picker` (ileride alias `table` kabul edilebilir) |
| Person benzeri UX | Modal grid; dışarıda chip / özet; serbest metin commit yok |
| Multiselect | **`cardinality: multi`** ile birinci sınıf; zimmet varsayılan hedef |
| Sütun modeli | `options.lookup.columns[]` — admin tanımlar |
| AF | Aynı metadata dili; AF widget parity **TP-AF** fazında (OC önce) |
| OC-1 etkisi | Çoklu demirbaş → `updateDatasetRows` / N satır güncelleme (ayrı backlog; bu spec UI+alan) |

---

## 3. Neden mevcut L4 picker yetmez?

Bugünkü `presentation: picker` (`OcLookupDatasetPickerField`):

| Özellik | L4 bugün | Bu spec |
|---------|----------|---------|
| Sütunlar | `labelField` + en fazla 2 `searchFields` | Yapılandırılabilir `columns[]` |
| Sıralama | Yok | Kolon + varsayılan sort |
| Filtre | Seed `filter` + `dependsOn` | + dialog arama/kolon filtresi |
| Multiselect | Prop var, zimmet tekil | Zimmet **multi** + chip UX |
| Format | Ham değer | `format`: text / date / enum map / bool |

Person picker (`MngDirectoryPickerField`) zengin sabit kolonlu; dataset tarafı aynı seviyeye çıkarılacak.

---

## 4. Kullanıcı deneyimi

### 4.1 Form alanı (kapalı)

| Cardinality | Görünüm |
|-------------|---------|
| `single` | Tek satır özet (`selection.displayFields` veya `labelField`) + «Seç» / «Değiştir» |
| `multi` | Chip listesi (aynı etiket kuralı) + «Ekle» / chip sil |

Readonly profil: chip veya virgüllü etiketler (`fieldDisplays` / label resolve).

### 4.2 Modal (açık)

```text
┌─ Demirbaş seç ─────────────────────────────────────┐
│ [Ara…                    ]  Filtreler ▾            │
│ ☑ | Demirbaş no | Seri no | Ürün | Durum | Depo   │
│ ☐ | DMB-00012   | GIR-…   | Klavye | depoda | …  │
│ ☑ | DMB-00013   | GIR-…   | Mouse  | depoda | …  │
│ … sayfalama …                                      │
│ Seçili: 2                    [İptal]  [Tamam]      │
└────────────────────────────────────────────────────┘
```

- **Tekil:** satır tıklama veya radyo; Tamam → tek id  
- **Çoklu:** checkbox; Tamam → id dizisi; modal içinde seçili sayacı  
- Zaten seçili satırlar modal açılışında işaretli  
- `filter` (ör. `durum:eq:depoda`) her zaman uygulanır; kullanıcı bunu aşamaz  

---

## 5. Metadata şeması

`fieldType: relation` + `options.lookup` genişlemesi (mevcut alanlar korunur).

```json
{
  "key": "demirbasIds",
  "label": "Demirbaşlar",
  "fieldType": "relation",
  "cardinality": "multi",
  "relationDatasetName": "zimmet_demirbaslar",
  "options": {
    "lookup": {
      "source": "dataset",
      "presentation": "picker",
      "valueField": "__dataId",
      "labelField": "demirbasNo",
      "searchFields": ["demirbasNo", "seriNo", "marka", "model"],
      "pageSize": 25,
      "filter": "durum:eq:depoda",
      "defaultSort": { "field": "demirbasNo", "dir": "asc" },
      "dependsOn": {
        "fieldKey": "depoId",
        "filterTemplate": "depoId:eq:{{parentValue}}",
        "optional": true
      },
      "columns": [
        { "field": "demirbasNo", "title": "Demirbaş no", "sortable": true, "width": 120 },
        { "field": "seriNo", "title": "Seri no", "sortable": true, "filterable": true },
        { "field": "katalogUrunId", "title": "Ürün", "sortable": false, "format": "relationLabel" },
        { "field": "durum", "title": "Durum", "format": "enum", "enumMap": {
            "depoda": "Depoda",
            "zimmetli": "Zimmetli"
          }},
        { "field": "depoId", "title": "Depo", "format": "relationLabel" }
      ],
      "selection": {
        "mode": "multi",
        "min": 1,
        "max": 50,
        "displayFields": ["katalogUrunId", "demirbasNo"],
        "displaySeparator": " · "
      }
    }
  }
}
```

### 5.1 Alanlar

| Alan | Zorunlu | Açıklama |
|------|---------|----------|
| `presentation` | evet | MVP: `picker` (zengin tablo). `dropdown` / `autocomplete` değişmez |
| `columns` | hayır | Yoksa L4 davranışı (label + searchFields). Varsa tablo bu liste |
| `columns[].field` | evet | Dataset alan `name` |
| `columns[].title` | hayır | Yoksa field key / i18n |
| `columns[].sortable` | hayır | Varsayılan `false` |
| `columns[].filterable` | hayır | Dialog kolon filtresi (faz TP-2) |
| `columns[].width` | hayır | px veya CSS |
| `columns[].format` | hayır | `text` (varsayılan) \| `date` \| `bool` \| `enum` \| `relationLabel` |
| `columns[].enumMap` | hayır | `format: enum` |
| `defaultSort` | hayır | `{ field, dir: asc\|desc }` |
| `selection.mode` | hayır | Yoksa `cardinality` ile uyumlu (`single` / `multi`) |
| `selection.min` / `max` | hayır | Multi doğrulama (UI + isteğe bağlı kural) |
| `filter` / `dependsOn` / `searchFields` / `pageSize` | — | L1–L3 ile aynı |

**Persist**

- `single` → skaler id (`string`)  
- `multi` → `string[]` (`extraFields`)

`selection.mode` ile `cardinality` çelişirse: **`cardinality` kazanır**; admin UI uyarı verir.

---

## 6. Zimmet etkisi

### 6.1 Alan modeli

Bugün: `demirbasId` (tekil relation, autocomplete).

| Seçenek | Açıklama | Öneri |
|---------|----------|--------|
| **A** | `demirbasId` → `cardinality: multi` (aynı key, dizi) | Breaking: eski WI tek id → diziye migrate |
| **B** | Yeni `demirbasIds` multi; `demirbasId` deprecated | Daha güvenli |
| **C** | Tek WI’da child satırlar / ayrı tip | Aşırı; MVP dışı |

**Öneri: B** — seed’de `demirbasIds` + form/transition zorunlu alan güncellemesi; demo script multi örnek.

### 6.2 İş kuralı (ürün)

- Zimmet verme: en az 1 demirbaş; üst sınır örn. 50  
- Hepsi `durum=depoda` (lookup `filter` + transition validation)  
- Aynı demirbaş iki kez seçilemez (set)  
- İade WI: MVP’de tek veya multi — ayrı netleştirme (çoğu iade tekil olabilir)

### 6.3 OC-1 / OC-2 (bağlı backlog)

| ID | Multi sonrası |
|----|----------------|
| **OC-1** | ZIM kapanınca **tüm** seçili demirbaşlarda `durum=zimmetli`, `personelId=…` |
| **OC-2** | İade tamamlanınca ilgili demirbaş(lar) `depoda` |

Bu spec yalnız seçim UI + alan; güncelleme aksiyonu `updateDatasetRows` (CDR ailesi) ile gelir.

---

## 7. Admin UI (Workspace → Alanlar)

Lookup editörüne ek:

1. Sunum: Picker (tablo)  
2. **Sütunlar** listesi: ekle / sil / sırala; field combobox (dataset şeması); title; sortable; format  
3. Varsayılan sıralama  
4. Cardinality single/multi (mevcut) + min/max (multi)  
5. Canlı önizleme (opsiyonel, TP-3)

---

## 8. Teknik dokunuşlar (beklenen)

| Katman | Dosya / alan |
|--------|----------------|
| Options parse | `ocLookupFieldOptions.ts` — `columns`, `defaultSort`, `selection` |
| Picker API | `useOcDatasetPicker.ts` — sort query, column filter |
| UI | `OcLookupDatasetPickerField.client.vue` — dinamik headers, multi checkbox |
| Form wiring | `OcDynamicFormField` / `relationSelect` / `relationSelectMulti` |
| Admin | `OcWorkspaceDefinitionsFieldsTab.vue` |
| MO readonly | `fieldDisplays` — multi label listesi (L5 genişletme) |
| Zimmet seed | `demirbasIds` + `presentation: picker` + `columns` |

DG: mevcut list/filter/sort/search yeterli; yeni endpoint şart değil (MVP).

---

## 9. Fazlar

| Faz | Kapsam | DoD |
|-----|--------|-----|
| **TP-0** | Bu spec onayı | ✅ |
| **TP-1** | `columns[]` + `defaultSort` + zengin modal (single + multi) | ✅ |
| **TP-2** | Kolon `filterable` UI + enum/date format | ✅ (text contains + enum select; relationLabel filtre sonra) |
| **TP-3** | Admin sütun editörü + önizleme | ⏳ |
| **TP-AF** | Automated Forms aynı picker | ⏳ |
| **TP-ZIM** | Seed: `demirbasIds` multi + form/transition | ✅ |
| **TP-OC1** | (ayrı) `updateDatasetRows` multi demirbaş | ⏳ |

Sıra önerisi: **TP-0 → TP-1 → TP-ZIM** (senaryo açılır) → TP-2/3 → TP-OC1 → TP-AF.

---

## 10. Kabul kriterleri (TP-1 + TP-ZIM)

1. Admin `columns` tanımlı picker modalında sütunlar görünür.  
2. `cardinality: multi` → birden fazla satır seçilir; formda chip’ler; kayıtta id dizisi.  
3. `filter: durum:eq:depoda` dışındaki satırlar listelenmez / seçilemez.  
4. Arama `searchFields` üzerinde çalışır.  
5. Zimmet verme WI’da laptop + klavye (+ mouse) seçilip kaydedilir.  
6. Single relation alanlar (tedarikçi vb.) regressyon: autocomplete/dropdown bozulmaz.

---

## 11. Açık noktalar

1. `relationLabel` formatı: satırda gömülü expand mı, yoksa id→label cache mi? (L5 ile hizala)  
2. İade WI multi mi tekil mi?  
3. `demirbasId` → `demirbasIds` migrate stratejisi (eski kapalı WI’lar)  
4. Üst sınır 50 — DG/MO payload limiti ile doğrula  
5. Board listesinde multi relation özeti (ilk N + “+2”)

---

## 12. Bilinçli sınırlar (MVP dışı)

- Excel yapıştır / barkod toplu ekle  
- Modal içinde inline demirbaş oluşturma  
- Cross-dataset union (birden fazla dataset tek picker)  
- MngWorkflow adımı olarak picker  

---

*Temel lookup: [OC_UI_LOOKUP_FIELDS.md](./OC_UI_LOOKUP_FIELDS.md) · Form: [OC_UI_FORM_DEFINITIONS.md](./OC_UI_FORM_DEFINITIONS.md)*
