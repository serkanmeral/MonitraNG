# Operation Core — Lookup / seçim alanları (pool)

**Son güncelleme:** 9 Haziran 2026  
**Durum:** L1+L2 uygulandı (dataset lookup + statik select) · L3 dependsOn · L4 picker · L5 MO readonly  
**Kod:** `Mng.Ui/utils/ocLookupFieldOptions.ts` · `useOcDynamicFormLookups.ts` · `OcWorkspaceDefinitionsFieldsTab.vue`

---

## 1. Amaç

Workspace **Alanlar** (`op_fields`) havuzunda:

| İhtiyaç | Çözüm |
|---------|--------|
| Harici dataset’ten dropdown (`tedarikciler`) | `fieldType: relation` + `options.lookup` |
| Statik enum | `fieldType: select` + `staticItems` |
| Bağımlı filtre (ürün grubu → ürün) | `dependsOn` (L3) |
| Büyük tablo seçimi | `presentation: picker` (L4) |

Seçim **zorunlu listeden**; arama yazılabilir, serbest metin commit edilmez.

---

## 2. Dataset havuzu (admin)

Lookup kaynağı dataset combobox:

- Tüm `@datasets` kayıtları
- Kategori `isSystemCategory === true` → **elenir**
- DG permissions boş liste → beklenen davranış

---

## 3. Metadata şeması

### 3.1 `fieldType: select` (statik)

```json
{
  "key": "oncelikSeviyesi",
  "fieldType": "select",
  "cardinality": "single",
  "options": {
    "lookup": {
      "source": "static",
      "presentation": "dropdown",
      "staticItems": [
        { "value": "low", "label": "Düşük" },
        { "value": "high", "label": "Yüksek" }
      ]
    }
  }
}
```

Persist: skaler `value` (`extraFields`).

### 3.2 `fieldType: relation` (dataset FK)

```json
{
  "key": "tedarikciId",
  "fieldType": "relation",
  "cardinality": "single",
  "relationDatasetName": "tedarikciler",
  "options": {
    "lookup": {
      "source": "dataset",
      "presentation": "autocomplete",
      "valueField": "__dataId",
      "labelField": "unvan",
      "searchFields": ["unvan", "kod"],
      "pageSize": 50,
      "filter": null,
      "dependsOn": {
        "fieldKey": "urunGrubuId",
        "filterTemplate": "grupId={{parentValue}}"
      }
    }
  }
}
```

Persist: `valueField` değeri (varsayılan `__dataId`). `cardinality: multi` → dizi.

---

## 4. Sunum (`presentation`)

| Değer | UI (L1) | Not |
|-------|---------|-----|
| `dropdown` | `v-select` | Küçük listeler |
| `autocomplete` | `v-autocomplete` | Arama; listeden seçim zorunlu |
| `picker` | L4 — şimdilik autocomplete fallback | Modal datatable |

---

## 5. Runtime

- **Yükleme:** `useOcDynamicFormLookups` → `ocListDataset` (UI→DG)
- **Readonly profil:** MO `fieldDisplays` (L5: `labelField` genişletmesi)
- **Çoklu:** `cardinality: multi` → chips
- **Automated Forms:** şema OC’de canonical; AF refactor sonra

---

## 6. Uygulama fazları

| Faz | Kapsam | Durum |
|-----|--------|--------|
| L1 | Dataset lookup: label/value, admin dataset picker, autocomplete | ✅ |
| L2 | `select` + static items editor | ✅ |
| L3 | `dependsOn` cascade | ✅ UI temel |
| L4 | `useOcDatasetPicker` modal | backlog |
| L5 | MO profil/aktivite `labelField` | backlog |

---

## 7. Test checklist

1. Alan Tanımları → relation → dataset seç (system hariç) → label/value alanı → kaydet
2. Form layout’a alan ekle → metadata cache reload
3. Yeni iş → autocomplete dolu → yalnız listeden seçim
4. `select` + static items → dropdown görünür
5. `cardinality: multi` → çoklu chip

---

*Form widget genel: [OC_UI_FORM_DEFINITIONS.md](./OC_UI_FORM_DEFINITIONS.md)*
