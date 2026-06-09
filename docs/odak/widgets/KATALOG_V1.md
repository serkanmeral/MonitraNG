# V1 Widget Template Katalogu

**Son güncelleme:** 7 Haziran 2026  
**Durum:** ✅ Seed JSON hazır — [datasets/widget_templates_seed_v1.json](./datasets/widget_templates_seed_v1.json)  
**Kapsam:** D7 — alarm · siem · operation-core · document-intelligence

---

## 1. Katalog özeti

| Domain | Şablon sayısı (V1) | Veri tipi | Domain doküman |
|--------|-------------------|-----------|----------------|
| alarm | 4 | `serviceRef` | [DOMAIN_ALARM.md](./DOMAIN_ALARM.md) |
| siem | 6 (1 composite) | `serviceRef` | [DOMAIN_SIEM.md](./DOMAIN_SIEM.md) |
| operation-core | 4 | `queryRef` | [DOMAIN_OPERATION_CORE.md](./DOMAIN_OPERATION_CORE.md) |
| document-intelligence | 5 (2 P1) | `serviceRef` | [DOMAIN_DOCUMENT_INTELLIGENCE.md](./DOMAIN_DOCUMENT_INTELLIGENCE.md) |

**Toplam:** ~19 şablon (composite hariç ~18)

---

## 2. Tam liste

### Alarm

| templateId | Başlık (TR) | kind | preset | serviceRef / not |
|------------|-------------|------|--------|------------------|
| `alarm.open-count-stat` | Açık alarm sayısı | stat | `stat-simple` | `mngalarm:alarms/dashboard-snapshot` |
| `alarm.severity-distribution-donut` | Severity dağılımı | chart | `chart-donut-breakup` | aynı snapshot |
| `alarm.recent-table` | Son alarmlar | table | `table-compact` | snapshot veya `mngalarm:alarms/list` |
| `alarm.trend-area` | Alarm trendi | chart | `chart-area-gradient` | 🔲 API genişletme |

### SIEM

| templateId | Başlık (TR) | kind | preset | serviceRef |
|------------|-------------|------|--------|------------|
| `siem.events-total-stat` | Toplam olay (24s) | stat | `stat-simple` | `mngreactor:sec-events/dashboard-summary` |
| `siem.login-failed-stat` | Başarısız giriş | stat | `stat-simple` | aynı |
| `siem.open-alarms-stat` | Açık alarmlar | stat | `stat-simple` | `mngalarm:alarms/dashboard-snapshot` |
| `siem.events-hourly-trend` | Saatlik olay trendi | chart | `chart-area-gradient` | 🔲 bucket API |
| `siem.recent-events-table` | Son olaylar | table | `table-compact` | `mngreactor:sec-events/list` |
| `siem.scenario-cards` | Senaryo kartları | composite | — | Faz 3 |

### Operation Core (MO)

| templateId | Başlık (TR) | kind | preset | queryRef |
|------------|-------------|------|--------|----------|
| `oc.work-items-by-state` | Duruma göre işler | chart | `chart-donut-breakup` | `@op_work_items/queries/wi_count_by_state` 🔲 |
| `oc.sla-breach-stat` | SLA ihlali | stat | `stat-simple` | `@op_work_items/queries/wi_sla_response_breach` |
| `oc.my-assigned-table` | Bana atanan açık işler | table | `table-compact` | `@op_work_items/queries/wi_assigned_open` |
| `oc.open-work-queue-table` | İş kuyruğu | table | `table-drilldown` | `@op_work_items/queries/wi_by_workspace_and_state` |

### Document Intelligence

| templateId | Başlık (TR) | kind | preset | serviceRef |
|------------|-------------|------|--------|------------|
| `di.folder-children-table` | Klasör içeriği | table | `table-compact` | `mngdocument:resources/children` |
| `di.recent-search-list` | Doküman arama | list | `list-activity` | `mngdocument:resources/search` |
| `di.quick-link-banner` | Dokümanlara git | banner | `banner-info` | statik |
| `di.recent-updates-list` | Son güncellenenler | list | `list-activity` | 🔲 P1 stats API |
| `di.draft-count-stat` | Taslak sayısı | stat | `stat-simple` | 🔲 P1 |

---

## 3. Öncelik (seed sırası)

1. **P0 — mevcut API/query:** `oc.my-assigned-table`, `siem.events-total-stat`, `siem.login-failed-stat`, `alarm.open-count-stat`, `di.folder-children-table`, `di.quick-link-banner`
2. **P1 — küçük API/query genişletme:** `alarm.severity-distribution-donut`, `alarm.recent-table`, `siem.open-alarms-stat`, `siem.events-hourly-trend`, `siem.recent-events-table`, `oc.sla-breach-stat`, `oc.open-work-queue-table`, `di.recent-search-list` — **aktif** (smoke: [scripts/smoke-widget-p1-data.ps1](./scripts/smoke-widget-p1-data.ps1))
3. **P2 — bekleyen API:** `alarm.trend-area`, `oc.work-items-by-state`, `di.recent-updates-list`, `di.draft-count-stat`, `siem.scenario-cards`

---

## 4. Seed dosyası

Konum: [datasets/widget_templates_seed_v1.json](./datasets/widget_templates_seed_v1.json)  
Kurulum: [scripts/setup-widget-templates-datasets.ps1](./scripts/setup-widget-templates-datasets.ps1) — bkz. [datasets/KURULUM.md](./datasets/KURULUM.md)

| Öncelik | Aktif (`isActive`) | Adet |
|---------|-------------------|------|
| P0 | `true` | 6 |
| P1 | `true` | 8 |
| P2 | `false` | 5 |

---

## 5. @widget_categories (V1)

| name | domain | icon önerisi |
|------|--------|--------------|
| `alarm-kpi` | alarm | mdi-bell-alert |
| `alarm-charts` | alarm | mdi-chart-donut |
| `siem-kpi` | siem | mdi-shield-check |
| `siem-charts` | siem | mdi-chart-timeline-variant |
| `oc-kpi` | operation-core | mdi-clipboard-list |
| `oc-work-queues` | operation-core | mdi-format-list-bulleted |
| `di-lists` | document-intelligence | mdi-file-document-multiple |
| `di-quick-access` | document-intelligence | mdi-link-variant |

Seed: [widget_categories_seed_v1.json](./datasets/widget_categories_seed_v1.json)
