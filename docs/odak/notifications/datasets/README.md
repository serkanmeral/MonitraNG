# MngNotifier — dataset dosyalari

| Dosya | Aciklama |
|-------|----------|
| [DATASETS.md](./DATASETS.md) | Alan tanimlari, placeholder, render birlestirme |
| [notifier_dataset_category.json](./notifier_dataset_category.json) | DG kategori **NotifierDatasets** |
| [notifier_datasets.json](./notifier_datasets.json) | `@mail_layouts` + `@mail_templates` semalari |
| [notifier_mail_layouts_seed.json](./notifier_mail_layouts_seed.json) | Ornek layout kayitlari (`default`, `minimal`) |
| [notifier_mail_templates_seed.json](./notifier_mail_templates_seed.json) | Ornek system sablonlari (6 adet) |
| [odak_test_branding.json](./odak_test_branding.json) | Odak domain test logo URL + displayName |

## Seed sablon ozeti

| templateKey | Konu |
|-------------|------|
| `domain-created` | Domain + admin bilgileri |
| `work-item-created` | Yeni is ogesi |
| `work-item-transitioned` | Durum gecisi |
| `work-item-updated` | Genel guncelleme |
| `field-changed` | Tek alan degisimi |
| `generic-notification` | Serbest baslik/govde |

## Odak test markasi

POC domain (`odak`) mail onizleme ve seed `sampleContext` icin:

| Alan | Deger |
|------|--------|
| `domainName` | `odak` |
| `displayName` | Odak Kompozit Teknolojileri A.Ş. |
| `logoUrl` | `https://img-kariyer.mncdn.com/mnresize/150/150/UploadFiles/Clients/SquareLogo/983/ai_kk_274983_6022024014542.png` |

Tam JSON: [odak_test_branding.json](./odak_test_branding.json)

Kurulum scripti: *(planlaniyor — `../scripts/setup-notifier-datasets.ps1`)*

Ust indeks: [../README.md](../README.md)
