# Widget & Dashboard — Kaldığımız yer

**Son güncelleme:** 7 Haziran 2026  
**Durum:** 📋 **Planlama dokümantasyonu tamamlandı** — implementasyon bekliyor

---

## Tamamlanan (planlama dokümantasyonu)

- [x] Katman modeli → [ARCHITECTURE.md](./ARCHITECTURE.md)
- [x] Manifest şema (prose + JSON Schema) → [MANIFEST_SCHEMA.md](./MANIFEST_SCHEMA.md), [schemas/widget-manifest-v1.schema.json](./schemas/widget-manifest-v1.schema.json)
- [x] Etkileşim modeli → [INTERACTIVITY_MODEL.md](./INTERACTIVITY_MODEL.md)
- [x] Veri katalogu (queryRef + serviceRef) → [DATA_CATALOG.md](./DATA_CATALOG.md)
- [x] V1 template katalogu → [KATALOG_V1.md](./KATALOG_V1.md)
- [x] Domain dokümanları → [DOMAIN_ALARM.md](./DOMAIN_ALARM.md), [DOMAIN_SIEM.md](./DOMAIN_SIEM.md), [DOMAIN_OPERATION_CORE.md](./DOMAIN_OPERATION_CORE.md), [DOMAIN_DOCUMENT_INTELLIGENCE.md](./DOMAIN_DOCUMENT_INTELLIGENCE.md)
- [x] Preset katalogu → [PRESENTATION_PRESETS.md](./PRESENTATION_PRESETS.md)
- [x] Designer UX → [DESIGNER_UX.md](./DESIGNER_UX.md)
- [x] Dataset tasarım + seed taslakları → [datasets/](./datasets/)
- [x] Kilitli kararlar D0–D8

---

## Kilitli kararlar

| # | Konu | Karar |
|---|------|-------|
| **D0** | Backend | Ayrı widget servisi yok — DG + Mng.Ui |
| **D1** | Template depolama | `@widget_templates` DG dataset |
| **D2** | Legacy widget | **Runtime adapter** + isteğe bağlı migration script |
| **D3** | SIEM layout persist | `@dashboards` + `surfaceKind=siem-center` |
| **D4** | List widget | Faz 1: `TableWidget` + `list-activity` preset; Faz 2: ayrı `ListWidget` |
| **D5** | Batch fetch | **Faz 2** — Nuxt BFF veya DG; Faz 1’de yok |
| **D6** | Document Intelligence | `serviceRef` → MngDocument |
| **D7** | V1 katalog domain | alarm · siem · **operation-core (MO)** · document-intelligence — **monitoring plan dışı** |
| **D8** | Alarm & SIEM veri yolu | **`serviceRef`** → MngAlarm / MngReactor — DG queryRef değil (kod gerçeği) |

---

## V1 katalog (özet)

Bkz. [KATALOG_V1.md](./KATALOG_V1.md) · [DATA_CATALOG.md](./DATA_CATALOG.md)

| Domain | Veri tipi | ~Şablon |
|--------|-----------|---------|
| alarm | `serviceRef` (MngAlarm) | 4 |
| siem | `serviceRef` (MngReactor + Alarm snapshot) | 6 |
| operation-core (MO) | `queryRef` (`op_work_items`) | 4 |
| document-intelligence | `serviceRef` (MngDocument) | 5 |

---

## Mevcut kod envanteri

| Bileşen | Durum | Not |
|---------|-------|-----|
| `@dashboards` + builder | ✅ | Placement uyumlu |
| `@widgets` + `WidgetForm` | ⚠️ | Wizard ile değişecek |
| `WidgetRenderer` | ✅ | → `WidgetHost` |
| `alarmService` / `secEventService` | ✅ | → `serviceRef` adapter |
| `widgetDataService` | ✅ | → `queryRef` + `serviceRef` |
| `OcDashboardWidgetForm` | ⚠️ | Faz 4 MO birleşme |
| `AcSiemCenterDashboard` | ⚠️ | Faz 3 template migrasyon |
| `MonitoringWidgetForm` | ⚪ | V1 plan dışı |

---

## Faz planı (implementasyon — henüz başlamadı)

### Faz 0 — Sözleşme & dataset

- [ ] `setup-widget-templates-datasets.ps1`
- [ ] `widget_templates_seed_v1.json`
- [ ] UI: manifest adapter, preset registry *(kod)*

### Faz 1 — Katalog & designer

- [ ] P0 template seed · [KATALOG_V1.md](./KATALOG_V1.md) §3
- [ ] Widget Designer wizard · [DESIGNER_UX.md](./DESIGNER_UX.md)
- [ ] `WidgetHost` + `serviceRef` / `queryRef` fetch

### Faz 2 — Etkileşim

- [ ] Surface toolbar · batch fetch (D5)

### Faz 3 — SIEM panel migrasyon

- [ ] `AcSiemCenterDashboard` → WidgetHost (D3)

### Faz 4 — MO birleşme

- [ ] `OcDashboardWidgetDef` → `@widgets`

### Faz 5 — Reporting

- [ ] Snapshot / export hook

---

## Domain chat’lerinde tamamlanacak (widget önkoşul)

| Domain | Eksik | Doküman |
|--------|-------|---------|
| Alarm | Trend/bucket snapshot | [DOMAIN_ALARM.md](./DOMAIN_ALARM.md) §5 |
| SIEM | Hourly buckets API | [DOMAIN_SIEM.md](./DOMAIN_SIEM.md) §6 |
| MO | `wi_count_by_state` query | [DOMAIN_OPERATION_CORE.md](./DOMAIN_OPERATION_CORE.md) §3.1 |
| DI | Stats/list API P1 | [DOMAIN_DOCUMENT_INTELLIGENCE.md](./DOMAIN_DOCUMENT_INTELLIGENCE.md) |

---

## Implementasyon için hazır mıyız?

**Hayır — bilinçli olarak bekliyoruz.**

| Hazır | Eksik (domain chat) |
|-------|---------------------|
| Mimari, manifest, katalog, UX spec | Alarm/SIEM API genişletmeleri |
| JSON Schema, category seed taslağı | `wi_count_by_state`, DI stats uçları |
| Dataset create JSON | Kurulum script + template seed JSON |
| | Paralel chat’lerde çakışma riski — widget kodu Faz 0 sonra |

---

## Karar kaydı

| Tarih | Karar |
|-------|-------|
| 2026-06-07 | D0–D1, katman modeli, Grafana etkileşim |
| 2026-06-07 | D6 DI serviceRef; D7 MO terminolojisi + monitoring V1 dışı |
| 2026-06-07 | **D8** Alarm/SIEM serviceRef (MngAlarm/MngReactor) — DATA_CATALOG |
| 2026-06-07 | **D2–D5 kilitlendi** — öneri kabul |
| 2026-06-07 | **Planlama dokümantasyonu tamamlandı** — implementasyon bekliyor |
