# Widget dataset kurulum (planlama)

**Son güncelleme:** 7 Haziran 2026  
**Durum:** ✅ Kurulum script + seed JSON hazır — Odak provizyonu `setup-widget-templates-datasets.ps1`

---

## 1. Dosyalar

| Dosya | Açıklama |
|-------|----------|
| [DATASETS.md](./DATASETS.md) | `@widget_templates`, `@widgets` genişletme |
| [widget-templates-dataset-create.json](./widget-templates-dataset-create.json) | DG create JSON |
| [widget_categories_seed_v1.json](./widget_categories_seed_v1.json) | V1 kategori seed |
| [widget_templates_seed_v1.json](./widget_templates_seed_v1.json) | ✅ V1 şablon manifest listesi (19 kayıt; 6 P0 aktif) — [KATALOG_V1.md](../KATALOG_V1.md) |
| [widget_dataset_category.json](./widget_dataset_category.json) | `@widget_templates` dataset category |

---

## 2. Kurulum sırası (hedef script)

[../scripts/setup-widget-templates-datasets.ps1](../scripts/setup-widget-templates-datasets.ps1)

1. `@widget_categories` — mevcut dataset; seed kategorileri merge (duplicate skip)
2. `@widget_templates` — dataset create (yoksa)
3. `@widget_templates` — `widget_templates_seed_v1.json` POST
4. `@widgets` — schema patch: `templateId`, `templateVersion`, `manifestVersion` alanları *(opsiyonel object field)*

**Pattern:** [notifications/scripts/setup-notifier-datasets.ps1](../../notifications/scripts/setup-notifier-datasets.ps1)

---

## 3. Önkoşullar

- MngDataGateway erişimi (Odak token)
- `@widget_categories` dataset mevcut ([widget-categories-dataset-create.json](../../../content/Mng.Ui/support/specs/datasets/widget-categories-dataset-create.json))
- `@widgets` dataset mevcut

---

## 6. Doğrulama

```powershell
.\docs\odak\operationcore\scripts\get-operationcore-token.ps1
.\docs\odak\widgets\scripts\setup-widget-templates-datasets.ps1
.\docs\odak\widgets\scripts\smoke-widget-p0-data.ps1
```

Manuel HTTP:

```http
GET /data/api/v1/data/@widget_templates?filter=domain:eq:alarm&limit=20
GET /data/api/v1/data/@widget_categories?limit=50
```

---

## 4. Starter widget instance seed (Alarm / SIEM / MO)

Modül kategorileri + şablon katalogu hazır olduktan sonra `@widgets` örnek kayıtları:

| Dosya | Açıklama |
|-------|----------|
| [widget_instances_seed_v1.json](./widget_instances_seed_v1.json) | 15 widget (4 alarm, 6 siem, 5 MO) + 3 özet dashboard |
| [../scripts/seed-widget-instances.ps1](../scripts/seed-widget-instances.ps1) | Şablondan instance üretir |
| [../scripts/widget-instance-helpers.ps1](../scripts/widget-instance-helpers.ps1) | Ortak POST/ kategori çözümleme |

```powershell
.\docs\odak\operationcore\scripts\get-operationcore-token.ps1
.\docs\odak\widgets\scripts\reset-widget-catalog.ps1          # modul kategorileri
.\docs\odak\widgets\scripts\setup-widget-templates-datasets.ps1
.\docs\odak\operationcore\scripts\seed-operation-core-demo.ps1 # MO workspaceId icin
.\docs\odak\widgets\scripts\seed-widget-instances.ps1
# Modul bazli: -Module alarm | siem | operation-core
# Sadece widget: -SkipDashboards
```

Dashboard slug'ları: `seed-alarm-overview`, `seed-siem-overview`, `seed-oc-workspace`

### Eski dashboard temizliği

```powershell
.\docs\odak\widgets\scripts\cleanup-dashboards.ps1 -WhatIf   # onizleme
.\docs\odak\widgets\scripts\cleanup-dashboards.ps1           # sil
```

Whitelist: [dashboards_keep_v1.json](./dashboards_keep_v1.json) — starter panolar + `siem-center` (SIEM uygulama layout'u).

---

Widget template seed **tek başına yeterli değil** — veri kaynakları domain chat’lerinde:

| Domain | Önkoşul | Doküman |
|--------|---------|---------|
| alarm | MngAlarm snapshot alanları | [DOMAIN_ALARM.md](../DOMAIN_ALARM.md) |
| siem | Reactor dashboard-summary buckets | [DOMAIN_SIEM.md](../DOMAIN_SIEM.md) |
| operation-core | `op_work_items` queries provizyon | [DOMAIN_OPERATION_CORE.md](../DOMAIN_OPERATION_CORE.md) |
| document-intelligence | MngDocument list/search | [DOMAIN_DOCUMENT_INTELLIGENCE.md](../DOMAIN_DOCUMENT_INTELLIGENCE.md) |
