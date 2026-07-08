# Zimmet & Demirbaş Yönetimi (Odak)

**Son güncelleme:** 8 Temmuz 2026  
**Ortam:** Odak test (`192.168.20.20:5040`) — prod henüz kurulmadı

Fabrika personeline laptop, monitör, çanta vb. malzeme zimmeti; depo girişi ve demirbaş envanteri için **Automated Forms (AF)** master veri + **Operation Core (OC)** iş akışı modeli.

---

## Dokümantasyon

| Dosya | İçerik |
|-------|--------|
| [PLAN.md](./PLAN.md) | Mimari kararlar, faz planı (F0–F4), süreç diyagramı, geliştirme backlog |
| [DEVAM.md](./DEVAM.md) | Tamamlanan işler, seed sonuçları, sıradaki adımlar, kurulum komutları |

---

## Klasör yapısı

```
docs/odak/zimmet/
├── README.md                 ← bu dosya
├── PLAN.md                   ← plan ve kararlar
├── DEVAM.md                  ← durum ve devam noktası
├── datasets/                 ← DG dataset şema JSON (5 adet)
├── automated-forms/          ← AF form tanım JSON (5 adet)
├── seed/
│   ├── zimmet_master_seed.json   ← master veri tanımı
│   ├── zimmet_master_ids.json    ← seed sonrası oluşan ID'ler
│   └── zimmet-oc-seed.json       ← OC workspace/board/flow + demo özeti
└── scripts/
    ├── lib/ZimmetDgCommon.ps1
    ├── setup-zimmet-datasets-and-forms.ps1   (F0)
    ├── seed-zimmet-master-data.ps1            (F1)
    ├── seed-operation-core-zimmet.ps1         (F2–F3 + demo)
    ├── patch-zimmet-side-menu.ps1
    └── setup-zimmet-all.ps1                  ← tam kurulum
```

---

## Hızlı erişim (UI)

| Yüzey | URL |
|-------|-----|
| Demirbaşlar (AF) | `/apps/automated-forms/view/zimmet-demirbaslar-form` |
| Ürün kataloğu | `/apps/automated-forms/view/zimmet-urunler-form` |
| Operasyon Merkezi | `/apps/operation-core/workspace` → *Zimmet Depo*, *Personel Zimmet* |
| WS tanımları (admin) | `/apps/operation-core/admin/workspace-definitions` |

**Side menü:** `Dinamik Formlar` → `Zimmet Yönetimi` → alt formlar

---

## Tek komutla kurulum (Odak test)

```powershell
# Repo kökünden
.\docs\odak\operationcore\scripts\get-operationcore-token.ps1
.\docs\odak\zimmet\scripts\setup-zimmet-all.ps1 -SeedDemo
```

Sadece side menü güncellemesi:

```powershell
.\docs\odak\operationcore\scripts\get-operationcore-token.ps1
.\docs\odak\zimmet\scripts\patch-zimmet-side-menu.ps1
```

---

## İlgili dokümanlar

- [Dinamik Formlar — Mevcut Durum](../dynamicforms/MEVCUT_DURUM.md)
- [Operation Core — Uygulama Planı](../operationcore/OPERATION_CORE_IMPLEMENTATION_PLAN.md)
- [Tedarikçiler POC](../dynamicforms/TEDARIKCILER_POC.md) — `tedarikciler` dataset yeniden kullanımı
