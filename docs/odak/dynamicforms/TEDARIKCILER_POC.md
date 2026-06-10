# Tedarikçiler — Dinamik Form POC

**Son güncelleme:** 9 Haziran 2026 (gün sonu)  
**Amaç:** Zenginleştirilmiş `tedarikciler` dataset + Automated Form ile AF geliştirme boşluklarını somut senaryoda test etmek.

**Devam noktası:** [DEVAM.md](./DEVAM.md)

---

## Kurulum

```powershell
.\docs\odak\operationcore\scripts\get-operationcore-token.ps1
.\docs\odak\dynamicforms\scripts\setup-tedarikciler-automated-form.ps1
.\docs\odak\dynamicforms\scripts\patch-tedarikciler-side-menu.ps1
```

**Bilinen:** Tam şema sync (`options` alanı) Odak API’de PUT hatası veriyor; geçici olarak `-SkipSchema` kullanılabilir. Ayrıntı: [DEVAM.md §3](./DEVAM.md).

OC lookup demo (aynı dataset, `tedarikciId` alanı):

```powershell
.\docs\odak\operationcore\scripts\setup-oc-demo-tedarikci-lookup.ps1 -ReloadMetadataCache
```

---

## URL'ler (Odak UI)

| Sayfa | Route |
|-------|--------|
| Form runtime (liste + CRUD) | `/apps/automated-forms/view/tedarikciler-form` |
| Form builder | `/apps/automated-forms/edit/tedarikciler-form` |
| Form listesi | `/apps/automated-forms` |

---

## Dataset (`tedarikciler`)

Kaynak: [operationcore/datasets/tedarikciler_dataset.json](../operationcore/datasets/tedarikciler_dataset.json)

| Alan | Tip | Not |
|------|-----|-----|
| `kod` | text | Zorunlu, unique, `TED-XXX` pattern |
| `unvan` | text | Zorunlu |
| `tedarikciTipi` | select | Malzeme / Hizmet / Karma (staticItems) |
| `anaTedarikciId` | relation → `tedarikciler` | Self-FK — AF relation testi |
| `vergiNo`, `odemeVadesiGun` | text / number | Finans grubu |
| `ilgiliKisi`, `email`, `telefon`, `webSitesi` | text | İletişim |
| `ulke`, `sehir`, `ilce`, `adres` | text | Adres |
| `notlar` | text | Uzun metin — formda `textWidget: textarea` |
| `isActive` | bool | Liste filtresi |

Seed: [datasets/tedarikciler_seed.json](./datasets/tedarikciler_seed.json) — `TED-005` → `TED-001` ana tedarikçi.

---

## Automated Form

Kaynak: [datasets/tedarikciler_automated_form.json](./datasets/tedarikciler_automated_form.json)

- **formCode:** `tedarikciler-form`
- **Gruplar:** Genel, İletişim, Adres, Finans, Notlar
- **Liste:** kod, unvan, tip, şehir, telefon, vade, aktif + global search
- **Relation config:** `anaTedarikciId` → display `unvan`
- **Düzenleme:** `kod` alanı yalnızca edit modunda salt okunur (`readonlyOnEditFields`)
- **Yan menü:** `patch-tedarikciler-side-menu.ps1` ile **Dinamik Formlar → Tedarikçiler**

---

## CRUD kullanımı

Runtime sayfası (`/apps/automated-forms/view/tedarikciler-form`) tam CRUD sunar:

| İşlem | UI |
|-------|-----|
| **Listele** | Sayfa açılışında tablo; sıralama, filtre, arama |
| **Oluştur** | **Yeni** → gruplu dialog (Genel / İletişim / …) → **Kaydet** |
| **Güncelle** | Satır **Düzenle** → `kod` kilitli, diğer alanlar düzenlenebilir |
| **Sil** | Satır **Sil** → onay dialogu |

**Yeni kayıt örneği:** `kod`: `TED-009`, `unvan`: zorunlu, `tedarikciTipi`: select, `isActive`: varsayılan `true`.

Yan menü kurulumu:

```powershell
.\docs\odak\dynamicforms\scripts\patch-tedarikciler-side-menu.ps1
```

---

## Form designer — alan sunumu (9 Haz 2026)

**Form Ayarları** sekmesi → alan tablosunda **Sunum** sütunu + **tune** butonu → modal:

| Sekme | Seçenekler |
|-------|------------|
| **Form sunumu** | Metin: textbox / textarea / richtext · Seçim: select / autocomplete |
| **Liste** | visible, sortable, filterable, order |

`formConfig.fieldLayout[field].textWidget` · `choiceWidget`

**Liste Ayarları** sekmesinde de visible/sortable/filterable zaten vardı; alan modalı aynı ayarlara Form sekmesinden erişim sağlar.

---

## Bu POC ile test edilecek AF boşlukları

Öncelik sırasıyla geliştirme odakları:

| # | Konu | Durum |
|---|------|--------|
| 1 | **`select` + `options` sync** | ⚠️ Kod + UI var; Odak şema PUT engelli ([DEVAM.md §3](./DEVAM.md)) |
| 2 | **Textarea / richtext** | ✅ Designer + runtime (`notlar`, `adres`) |
| 3 | **Relation lookup sınırlı** | 🔲 `anaTedarikciId` — arama/filtre/dependsOn yok |
| 4 | **Edit'te birincil anahtar readonly** | ✅ `readonlyOnEditFields: ["kod"]` |
| 5 | **Varsayılan değer** | 🔲 `isActive=true` yalnızca dataset default |
| 6 | **Permission entegrasyonu** | 🔲 CRUD butonları yetkisiz |
| 7 | **Form önizleme** | 🔲 Builder'da kaydetmeden önizleme yok |
| 8 | **Koşullu alan** | 🔲 Pasif tedarikçide `notlar` zorunlu gibi kurallar yok |

OC karşılaştırması: [MEVCUT_DURUM.md §4](./MEVCUT_DURUM.md)

---

## Deploy durumu (9 Haz 2026)

| Bileşen | Odak |
|---------|------|
| MngDataGateway (`select`, `options`) | ✅ |
| `@automated_forms` / `tedarikciler-form` | ✅ |
| `@side_menu` (Dinamik Formlar → Tedarikçiler) | ✅ |
| Mng.Ui | ❌ — lokal `npm run dev` |

---

## Dosyalar

```
docs/odak/operationcore/datasets/tedarikciler_dataset.json   ← şema (kaynak)
docs/odak/dynamicforms/datasets/tedarikciler_seed.json       ← seed
docs/odak/dynamicforms/datasets/tedarikciler_automated_form.json
docs/odak/dynamicforms/scripts/setup-tedarikciler-automated-form.ps1
docs/odak/dynamicforms/scripts/patch-tedarikciler-side-menu.ps1
```
