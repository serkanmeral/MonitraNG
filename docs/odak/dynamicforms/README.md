# Dinamik Formlar (Dynamic Forms) — Odak planlama

**Son güncelleme:** 9 Haziran 2026 (gün sonu)  
**Durum:** 🧪 Tedarikçiler AF CRUD POC — [DEVAM.md](./DEVAM.md) · [TEDARIKCILER_POC.md](./TEDARIKCILER_POC.md)

---

## Amaç

MonitraNG'de **dinamik form oluşturma, tanımlama ve çalışma zamanı (runtime) render** konuları için Odak planlama ve karar kaydı alanı.

Bu klasör; yeni form motoru / form builder tasarımı, mevcut OC ve Automated Forms yeteneklerinin birleştirilmesi veya genişletilmesi gibi çalışmaların **merkezi planlama yeri**dir.

**Kapsam dışı (ayrı klasörler):** Operation Core iş akışı / geçiş politikaları → [operationcore/](../operationcore/); Otomasyon Merkezi workflow form editörü → [workflow/](../workflow/).

---

## Bu klasördeki belgeler

| Belge | İçerik |
|-------|--------|
| **[DEVAM.md](./DEVAM.md)** | **Yarın buradan devam** — kaldığımız yer, deploy durumu, checklist |
| **[TEDARIKCILER_POC.md](./TEDARIKCILER_POC.md)** | **Aktif POC** — `tedarikciler-form` CRUD, kurulum, AF boşlukları |
| **[MEVCUT_DURUM.md](./MEVCUT_DURUM.md)** | Kod envanteri — AF + OC hatları, route'lar, bileşenler |

---

## Aktif POC özeti

| Öğe | Değer |
|-----|--------|
| Form | `tedarikciler-form` |
| Dataset | `tedarikciler` (16 alan) |
| Runtime | `/apps/automated-forms/view/tedarikciler-form` |
| Yan menü | Dinamik Formlar → Tedarikçiler |
| Odak | Form + menü sync ✅ · UI deploy ❌ (lokal dev) |

---

## Klasör yapısı

```
docs/odak/dynamicforms/
├── README.md              ← bu dosya (indeks)
├── DEVAM.md               ← kaldığımız yer (önce bunu oku)
├── MEVCUT_DURUM.md        ← kod envanteri (AF + OC)
├── TEDARIKCILER_POC.md      ← aktif POC rehberi
├── datasets/
│   ├── tedarikciler_seed.json
│   └── tedarikciler_automated_form.json
└── scripts/
    ├── setup-tedarikciler-automated-form.ps1
    └── patch-tedarikciler-side-menu.ps1
```

Dataset şema kaynağı (paylaşılan): `docs/odak/operationcore/datasets/tedarikciler_dataset.json`

---

## İlgili mevcut dokümanlar

### Operation Core (OcDynamicForm)

| Doküman | Konum |
|---------|-------|
| Form tanımları & runtime | [operationcore/ui/OC_UI_FORM_DEFINITIONS.md](../operationcore/ui/OC_UI_FORM_DEFINITIONS.md) |
| Alan politikası | [operationcore/ui/OC_UI_FIELD_POLICY.md](../operationcore/ui/OC_UI_FIELD_POLICY.md) |
| Lookup alanları | [operationcore/ui/OC_UI_LOOKUP_FIELDS.md](../operationcore/ui/OC_UI_LOOKUP_FIELDS.md) |

### Automated Forms (Mng.Ui)

| Doküman | Konum |
|---------|-------|
| Planlama spec | [AUTOMATED_FORMS_PLANNING.md](../../content/Mng.Ui/support/specs/AUTOMATED_FORMS_PLANNING.md) |
| Güncel durum | [current_status.md](../../content/Mng.Ui/support/guides/current_status.md) |

### Deploy

| Doküman | Konum |
|---------|-------|
| Odak deploy | [deploy/README.md](../deploy/README.md) |
