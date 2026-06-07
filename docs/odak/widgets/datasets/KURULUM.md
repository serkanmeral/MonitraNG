# Widget dataset kurulum (planlama)

**Son güncelleme:** 7 Haziran 2026  
**Durum:** 📋 Script henüz yok — implementasyon Faz 0

---

## 1. Dosyalar

| Dosya | Açıklama |
|-------|----------|
| [DATASETS.md](./DATASETS.md) | `@widget_templates`, `@widgets` genişletme |
| [widget-templates-dataset-create.json](./widget-templates-dataset-create.json) | DG create JSON |
| [widget_categories_seed_v1.json](./widget_categories_seed_v1.json) | V1 kategori seed |
| `widget_templates_seed_v1.json` | 🔲 V1 şablon manifest listesi — [KATALOG_V1.md](../KATALOG_V1.md) |

---

## 2. Kurulum sırası (hedef script)

`docs/odak/widgets/scripts/setup-widget-templates-datasets.ps1` *(oluşturulacak)*

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

## 4. Doğrulama (manuel)

```http
GET /data/api/v1/data/@widget_templates?filter=domain:eq:alarm&limit=20
GET /data/api/v1/data/@widget_categories?limit=50
```

---

## 5. Domain veri önkoşulları (widget seed öncesi)

Widget template seed **tek başına yeterli değil** — veri kaynakları domain chat’lerinde:

| Domain | Önkoşul | Doküman |
|--------|---------|---------|
| alarm | MngAlarm snapshot alanları | [DOMAIN_ALARM.md](../DOMAIN_ALARM.md) |
| siem | Reactor dashboard-summary buckets | [DOMAIN_SIEM.md](../DOMAIN_SIEM.md) |
| operation-core | `op_work_items` queries provizyon | [DOMAIN_OPERATION_CORE.md](../DOMAIN_OPERATION_CORE.md) |
| document-intelligence | MngDocument list/search | [DOMAIN_DOCUMENT_INTELLIGENCE.md](../DOMAIN_DOCUMENT_INTELLIGENCE.md) |
