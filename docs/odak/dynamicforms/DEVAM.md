# DEVAM — Dinamik Formlar (Kaldığımız Yer)

**Son güncelleme:** 10 Haziran 2026  
**Durum:** ✅ Tedarikçiler AF POC — CRUD + filtre + formatlama örnekleri tamam; ⚠️ liste yatay scroll açık

> **İndeks:** [README.md](./README.md) · **Kod envanteri:** [MEVCUT_DURUM.md](./MEVCUT_DURUM.md) · **POC detay:** [TEDARIKCILER_POC.md](./TEDARIKCILER_POC.md)

---

## Buradan devam et

1. **Bu dosyayı oku** (§2–§4 ve §8 checklist).
2. Lokal UI: `cd Mng.Ui` → `npm run dev` → `/apps/automated-forms/view/tedarikciler-form`
3. **İlk öncelik:** Liste yatay scroll sorunu — `Mng.Ui/pages/apps/automated-forms/view/[formCode].vue` (`.af-automated-form-list-scroll` CSS). Kullanıcı: scroll çubuğu görünmüyor, sağ sütunlara erişilemiyor. Gerekirse sticky **İşlemler** sütununu geçici kaldırıp test et.
4. **Sonra:** POC kalan maddeler (#5 varsayılan değer, #7 önizleme, #8 koşullu alan) veya UI Odak deploy (kullanıcı onayı).

---

## 1. Tek cümlede durum

**Tedarikçiler** Automated Form POC’su Odak’ta çalışır durumda: CRUD, gelişmiş filtre, relation lookup, permission, liste formatlama örnekleri ve `anaTedarikciId` unvan gösterimi hazır. UI değişiklikleri yalnızca lokal `npm run dev` ile test edilir; **liste yatay scroll** henüz kullanıcı tarafında doğrulanmadı / sorun devam ediyor.

---

## 2. Bu oturumda tamamlananlar (9–10 Haz 2026)

### Backend — `options` BsonDocument PUT engeli (✅)

- **Sorun:** `FieldDefinition.options` doğrudan `BsonDocument?` → API model binding hatası
- **Çözüm:** `defaultValue` deseni — `options` (object, `[BsonIgnore]`) + `optionsBson` (`[BsonElement("options")]`)
- **Dosyalar:**
  - `MngDataGateway/Core/MngDataGateway.Domain/Entities/DatasetSchema.cs`
  - `MngDataGateway/Infrastructure/MngDataGateway.Persistence/Services/DatasetService.cs`
- Odak’a deploy edildi; `setup-tedarikciler-automated-form.ps1` artık `-SkipSchema` olmadan da çalışır

### Relation lookup (✅ POC #3)

- `Mng.Ui/composables/useAfRelationPicker.ts` — debounced arama, sayfalama, label cache
- `DynamicFormField.vue` — relation autocomplete picker
- `view/[formCode].vue` — edit modunda `exclude-relation-id` (self-FK engeli)
- Liste: `relationListLabelCache`, `getEffectiveListDisplayField()`, `loadRelationListLabels()` — `anaTedarikciId` unvan gösterimi

### Permission entegrasyonu (✅ POC #6)

- `patch-tedarikciler-side-menu.ps1` — `permissions.groups` + `pageType: user`
- `ConvertTo-Json -Depth 12` düzeltmesi (nested permissions)
- Odak’a uygulandı; `usePagePermissions` ile view/create/update/delete/export

### Gelişmiş filtreleme (✅)

- `Mng.Ui/utils/afListFilters.ts`
- `Mng.Ui/components/apps/automated-forms/AfListFilters.vue`
- OC board benzeri gelişmiş arama (alan + operatör + değer, AND)
- Yalnızca `listConfig.columns[].filterable: true` sütunlar
- Hızlı filtre satırı kaldırıldı; varsayılan kapalı açılır panel

### Liste görünümü iyileştirmeleri

- [x] Action sütunu sağa sabit (`position: sticky; right: 0`) — OC board ile aynı
- [x] `anaTedarikciId` listeye eklendi (`displayField: unvan`)
- [x] Liste formatlama örnekleri — `tedarikciler_automated_form.json` + Odak sync:
  - `kod` → `color` (yalnızca yazı rengi: primary)
  - `unvan` → `text-transform: capitalize`
  - `tedarikciTipi` → `conditional-color` (Malzeme/Hizmet/Karma — yalnızca yazı)
  - `sehir` → `text-transform: uppercase`
  - `odemeVadesiGun` → `conditional-color` (≤30 yeşil, ≥45 turuncu, ≥60 kırmızı)
  - `isActive` → `conditional-color` (Evet/Hayır — yalnızca yazı)
- [x] Arka plan renklendirmesi kaldırıldı (kullanıcı geri bildirimi: tablo arka planını bozuyordu)

### API smoke test (✅)

- CRUD Odak API üzerinden doğrulandı (`TED-XXX` kod formatı)

### Önceki oturumdan (9 Haz) — hâlâ geçerli

- [x] `afFormFieldPresentation.ts`, `AutomatedFormForm.vue`, `DynamicFormField.vue`
- [x] `readonlyOnEditFields` (`kod` edit’te kilitli)
- [x] `select` alan tipi + DG deploy
- [x] Yan menü: **Dinamik Formlar → Tedarikçiler**

---

## 3. Açık sorun / devam eden iş

### Liste yatay scroll (⚠️ öncelikli)

**Belirti:** Tablo altında yatay scroll çubuğu görünmüyor; dar ekranda sağ sütunlara erişilemiyor.

**Denenen düzeltmeler** (`view/[formCode].vue`):

- `.af-automated-form-list-scroll` dış sarmalayıcı
- `overflow-x: auto` (önce `.v-table__wrapper`, sonra dış div)
- `width: fit-content`, sütun `min-width: 160px`
- Scrollbar thumb/track stilleri (koyu tema)
- `v-card-item` → `min-width: 0`

**Kullanıcı geri bildirimi (10 Haz):** Durum hâlâ aynı.

**Sonraki adımlar:**

1. Tarayıcı DevTools → tablo genişliği vs container; hangi parent `overflow: hidden` kesiyor?
2. Sticky **İşlemler** sütununu geçici devre dışı bırakıp scroll’u doğrula
3. OC `OcBoardPanel.client.vue` ile yan yana karşılaştır; gerekirse `v-data-table` yerine scroll wrapper + `fixed-header` kombinasyonu
4. Geniş monitörde tüm sütunlar sığıyorsa scroll beklenmez — dar pencerede test

### UI Odak deploy (❌ bekliyor)

Tüm UI değişiklikleri lokal. Deploy kullanıcı onayına bağlı.

---

## 4. POC — kalan AF geliştirme odakları

| # | Konu | Durum |
|---|------|--------|
| 1 | `select` dataset tipi + `options` sync | ✅ Backend fix + Odak şema sync |
| 2 | Textarea / richtext | ✅ Designer + runtime |
| 3 | Relation lookup (arama, filtre, dependsOn) | ✅ `anaTedarikciId` autocomplete + liste unvan |
| 4 | Edit’te birincil anahtar readonly | ✅ `readonlyOnEditFields` (`kod`) |
| 5 | Form builder varsayılan değer | 🔲 `isActive=true` yalnızca dataset default |
| 6 | Permission entegrasyonu | ✅ Side menu + `usePagePermissions` |
| 7 | Form önizleme (builder) | 🔲 |
| 8 | Koşullu alan / alan politikası | 🔲 OC `fieldBehaviors` benzeri |
| 9 | Liste yatay scroll (çok sütun) | ⚠️ Devam ediyor |
| 10 | Liste formatlama örnekleri | ✅ Tedarikçiler’de demo (yalnızca yazı rengi) |

---

## 5. Önemli dosyalar

```
docs/odak/dynamicforms/
  DEVAM.md                          ← bu dosya (devam noktası)
  MEVCUT_DURUM.md
  TEDARIKCILER_POC.md
  datasets/tedarikciler_automated_form.json   ← liste formatları + sütunlar
  datasets/tedarikciler_seed.json
  scripts/setup-tedarikciler-automated-form.ps1
  scripts/patch-tedarikciler-side-menu.ps1

Mng.Ui/
  pages/apps/automated-forms/view/[formCode].vue   ← liste, scroll, format, filtre
  components/apps/automated-forms/AfListFilters.vue
  components/apps/automated-forms/DynamicFormField.vue
  composables/useAfRelationPicker.ts
  utils/afListFilters.ts
  utils/afFormFieldPresentation.ts

MngDataGateway/
  Core/.../DatasetSchema.cs
  Infrastructure/.../DatasetService.cs
```

---

## 6. Hızlı komutlar

```powershell
# Token
.\docs\odak\operationcore\scripts\get-operationcore-token.ps1

# Form + seed (şema dahil — options fix sonrası)
.\docs\odak\dynamicforms\scripts\setup-tedarikciler-automated-form.ps1

# Yalnızca form sync
.\docs\odak\dynamicforms\scripts\setup-tedarikciler-automated-form.ps1 -SkipSchema -SkipSeed

# Yan menü
.\docs\odak\dynamicforms\scripts\patch-tedarikciler-side-menu.ps1

# Lokal UI
cd Mng.Ui
npm run dev
```

**Test URL’leri**

| Sayfa | Route |
|-------|--------|
| CRUD runtime | `/apps/automated-forms/view/tedarikciler-form` |
| Form builder | `/apps/automated-forms/edit/tedarikciler-form` |
| Odak API (gateway) | `http://192.168.20.20:5040` |

---

## 7. Karar kaydı

| Tarih | Karar | Gerekçe |
|-------|-------|---------|
| 9 Haz 2026 | `docs/odak/dynamicforms/` planlama klasörü | Odak altında merkezi planlama |
| 9 Haz 2026 | Tedarikçiler = AF POC senaryosu | Somut CRUD + relation + select testi |
| 9 Haz 2026 | UI Odak deploy ertelendi | Lokal dev ile test; deploy kullanıcı onayına bağlı |
| 10 Haz 2026 | Liste renklendirme yalnızca yazı rengi | Arka plan renkleri tablo görünümünü bozuyordu |
| 10 Haz 2026 | Gelişmiş filtre: yalnızca açılır panel | Hızlı filtre satırı kaldırıldı |
| 10 Haz 2026 | `options` BsonDocument binding fix | `-SkipSchema` workaround kaldırıldı |

---

## 8. Sonraki adımlar (checklist)

- [ ] **Liste yatay scroll** — doğrula veya alternatif çözüm (sticky işlemler etkisi?)
- [ ] Lokal CRUD smoke test (kullanıcı): create / edit / delete / gelişmiş filtre
- [ ] POC #5: form builder varsayılan değer (`isActive=true`)
- [ ] POC #7: form önizleme (builder)
- [ ] POC #8: koşullu alan politikası
- [ ] (İsteğe bağlı) UI Odak deploy — kullanıcı onayı sonrası
- [ ] (İsteğe bağlı) `currency`, `date`, `number` format örnekleri listeye ekle
- [ ] Kapsam tanımı (1 sayfa): in-scope / out-of-scope
- [ ] AF ↔ OC birleştirme kararı (Q1–Q2)

---

## 9. Bilinen zararsız uyarılar

- `npm run dev` → `manifest-route-rule middleware already exists` — Nuxt HMR uyarısı, işlevi etkilemez
