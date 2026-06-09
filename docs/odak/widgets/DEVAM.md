# Widget & Dashboard — Kaldığımız yer

**Son güncelleme:** 7 Haziran 2026 (akşam — SIEM Özet Paneli oturumu)  
**Durum:** 🔄 SIEM Özet Paneli (`seed-siem-overview`) manuel test · senaryo verisi düzeltildi (kod) · layout seed güncellendi · **Odak DB + hard refresh doğrulanacak**

---

## Son oturum — SIEM Özet Paneli (`/dashboards/seed-siem-overview`)

**Bağlam:** Kullanıcı SIEM Özet Paneli dashboard'unda widget'ları test ediyordu. SIEM Güvenlik Merkezi (`/apps/siem-center`) aynı senaryo verisini doğru gösterirken özet panelde sorunlar vardı.

### Tamamlanan (kod — commit edilmedi)

| Konu | Çözüm | Ana dosyalar |
|------|--------|--------------|
| Tabloda ham `id` ve tüm alanlar | `SIEM_RECENT_TABLE_COLUMNS` + inbox preset (`siem.recent-events-table`) | `widgetFieldMappingBridge.ts`, `TableWidget.vue`, `widgetTableFormats.ts` |
| `Veri çekilemedi` / desteklenmeyen `serviceRef` | `mngreactor:sec-events/scenario-rollup` → `mngalarm:alarms/dashboard-snapshot` alias | `widgetManifestServiceRefs.ts`, fetch katmanı |
| `__manifest_service__` 404 | Legacy dataset fetch engeli + binding fallback | `widgetDataService.ts` |
| Senaryo kartı layout (boşluk, grafik/tablo altta) | Seed layout: KPI satırı · senaryo tam genişlik · grafik+tablo | `widget_instances_seed_v1.json`, `patch-siem-overview-layout.ps1` |
| Senaryo widget UX | Kompakt mod, U1–U10 etiketleri, renk açıklaması, özet chip'ler | `SiemScenarioCardsWidget.vue`, locale `tr`/`en` |
| **Senaryo verisi panelde boş/Temiz, merkezde dolu** | Eski DB binding `fieldMap.rows: 'items'` rollup'ı eziyordu; stat şekli dönüyordu | `alarmScenarioRollupNormalize.ts`, `bindingWantsScenarioRollup`, `normalizeManifestBinding` fieldMap sırası |
| BFF batch PascalCase | `ScenarioRollup` / `MatchKey` normalize | `alarmScenarioRollupNormalize.ts`, `widgetManifestFetchCore.ts` |
| Batch widget id eşleşmesi | `resolveWidgetBatchDataId` | `widgetBatchDataService.ts`, `WidgetRenderer.vue` |

### Bilinen kök neden (senaryo verisi)

- `@widgets` kaydı **`seed-siem-scenario-cards`** eski seed taşıyor olabilir:
  - `serviceRef`: `mngreactor:sec-events/scenario-rollup` (API yok)
  - `fieldMap.rows`: `items` (liste şablonu kalıntısı)
- Runtime köprüsü bunu düzeltiyor; yarın **hard refresh + Yenile** ile doğrulanmalı.
- Kalıcı DB düzeltmesi isteğe bağlı: `seed-widget-instances.ps1 -Module siem` veya widget Designer'dan yeniden kayıt.

### Yarın ilk adımlar (checklist)

1. **UI yenile:** `/dashboards/seed-siem-overview` — Ctrl+F5, surface toolbar **Yenile**
2. **Layout DB:** Henüz yapılmadıysa:
   ```powershell
   .\docs\odak\widgets\scripts\patch-siem-overview-layout.ps1
   ```
   Beklenen düzen: satır1 = 3 KPI (4+4+4) · satır2 = senaryo (12) · satır3 = trend (5) + olay tablosu (7)
3. **Senaryo verisi:** U1–U10 şeridi SIEM Güvenlik Merkezi ile aynı renk/durum mu? (turuncu Son 24s, kırmızı Açık)
4. **Konsol:** `Desteklenmeyen serviceRef` veya batch fallback hatası kalmamalı
5. **Tablo:** Son olaylar — Zaman, Olay, Sonuç, Kullanıcı, Kaynak IP, Kaynak (ham `id` yok)
6. **Opsiyonel commit:** Tüm `Mng.Ui` + `docs/odak/widgets` değişiklikleri birlikte

### Yarın devam edilebilecek iyileştirmeler

- [ ] Senaryo widget **tam mod** (`compact: false`) dashboard editöründen test
- [ ] `seed-siem-scenario-cards` widget kaydını seed ile güncelle (runtime köprüye güvenmeyi azalt)
- [ ] SIEM özet tablosu: satır tıklama → `/apps/siem-center/events?eventId=…` drill-down doğrula
- [ ] `@widget_templates` Odak'ta `siem.recent-events-table` / `siem.scenario-cards` manifest güncellemesi (`setup-widget-templates-datasets.ps1`)
- [ ] Smoke: `smoke-widget-p1-data.ps1` + dashboard manuel checklist

### Oturumda dokunulan / yeni dosyalar

```
Mng.Ui/services/widgetDataService.ts
Mng.Ui/services/widgetManifestDataService.ts
Mng.Ui/services/widgetManifestFetchCore.ts
Mng.Ui/services/widgetBatchDataService.ts
Mng.Ui/utils/widgets/widgetFieldMappingBridge.ts
Mng.Ui/utils/widgets/widgetManifestServiceRefs.ts          (yeni)
Mng.Ui/utils/alarm/alarmScenarioRollupNormalize.ts         (yeni)
Mng.Ui/utils/widgets/widgetTableFormats.ts
Mng.Ui/utils/widgets/widgetManifestAdapter.ts
Mng.Ui/components/widgets/siem/SiemScenarioCardsWidget.vue
Mng.Ui/components/widgets/table/TableWidget.vue
Mng.Ui/components/widgets/WidgetRenderer.vue
Mng.Ui/components/dashboards/DashboardLayoutRenderer.vue
Mng.Ui/utils/locales/tr.json, en.json
docs/odak/widgets/datasets/widget_instances_seed_v1.json
docs/odak/widgets/datasets/widget_templates_seed_v1.json
docs/odak/widgets/scripts/patch-siem-overview-layout.ps1   (yeni)
```

**Test URL'leri:**

| Yüzey | URL |
|--------|-----|
| SIEM Özet Paneli | `/dashboards/seed-siem-overview` |
| SIEM Güvenlik Merkezi (referans) | `/apps/siem-center` |

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
| **D5** | Batch fetch | **Faz 2** — Nuxt BFF veya DG; Faz 1'de yok |
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
| `@widgets` + `WidgetForm` | ✅ | Wizard varsayılan; `?mode=advanced` legacy form |
| `WidgetRenderer` | ✅ | → `WidgetHost` |
| `alarmService` / `secEventService` | ✅ | → `serviceRef` adapter |
| `widgetDataService` | ✅ | → `queryRef` + `serviceRef` |
| `OcDashboardWidgetForm` | ✅ | Şablondan + özel düzenleme (operation-core) |
| `AcSiemCenterDashboard` | ✅ | Faz 3 hybrid + D3 layout persist |
| `MonitoringWidgetForm` | ⚪ | V1 plan dışı |

---

## Faz planı (implementasyon)

### Faz 0 — Sözleşme & dataset

- [x] `setup-widget-templates-datasets.ps1`
- [x] `widget_templates_seed_v1.json`
- [x] UI: manifest adapter, preset registry *(kod)*

### Faz 1 — Katalog & designer

- [x] P0 template seed · [KATALOG_V1.md](./KATALOG_V1.md) §3
- [x] P0 veri smoke · [scripts/smoke-widget-p0-data.ps1](./scripts/smoke-widget-p0-data.ps1)
- [x] `WidgetHost` + `serviceRef` fetch (temel — Alarm/SIEM/DI P0)
- [x] Widget Designer wizard · [DESIGNER_UX.md](./DESIGNER_UX.md) — `WidgetDesignerWizard`, `/apps/widgets/new`
- [x] Dashboard builder: şablondan widget oluştur · `WidgetPickerModal` template sekmesi
- [x] P1 şablon aktivasyonu (8 kayıt) · [smoke-widget-p1-data.ps1](./scripts/smoke-widget-p1-data.ps1)
- [x] `queryRef` runtime · `resolveManifestBindingForFetch` + MO/DG predefined normalize
- [x] P2 şablonlar — Odak 4/4 smoke OK, `isActive=true` ([smoke-widget-p2-data.ps1](./scripts/smoke-widget-p2-data.ps1))

### Faz 2 — Etkileşim

- [x] Surface toolbar (time range, severity, workspaceId, refresh) · `/dashboards/:slug`
- [x] Client batch fetch + 5s dedup · `widgetBatchDataService`, `useDashboardWidgetBatch`
- [x] Nuxt BFF batch endpoint · `POST /api/widgets/batch`, `widgetManifestFetchCore.ts`
- [x] Chart zoom → time range (A5) · `ChartWidget` + `useDashboardSurfaceContext.setTimeRangeFromZoom`
- [x] Cross-filter (A6) · `surfaceInteractions.ts`, tablo/chart tıklama + filtre chip'leri
- [x] Drill-down route (A7) · `useWidgetDrillDown`, stat/table/chart tıklama
- [x] Widget actions (O2) · `WidgetActionBar`, `widgetActionExecutor` (alarm ack/view)
- [x] List activity preset (D4) · `ListActivityWidget.vue`
- [x] SIEM scenario composite · `SiemScenarioCardsWidget` + `siem.scenario-cards` template

### Faz 3 — SIEM panel migrasyon

- [x] `siemCenterWidgets.ts` + `useSiemCenterTemplateBatch` — template yükleme + batch fetch
- [x] `AcSiemCenterDashboard` → WidgetHost hybrid:
  - Surface toolbar (time range, severity, workspaceId, auto-refresh)
  - Şablon widget'lar: `eventsTotal`, `openAlarms`, `loginFailed`, `hourlyTrend`, `recentAlarms`
  - Legacy: `deniedFlow`/`newFlow` stat, breakdown donut, U1–U10 senaryo composite
- [x] D3: `@dashboards` slug `siem-center` + `layout.meta.surfaceKind` — [setup-siem-center-dashboard.ps1](./scripts/setup-siem-center-dashboard.ps1), [useSiemCenterDashboardPersist.ts](../../../Mng.Ui/composables/useSiemCenterDashboardPersist.ts)
- [x] `siem.scenario-cards` composite şablon aktivasyonu · [activate-siem-scenario-cards.ps1](./scripts/activate-siem-scenario-cards.ps1)
- [x] SIEM panel P1 şablon aktivasyonu · [activate-siem-center-templates.ps1](./scripts/activate-siem-center-templates.ps1) (`open-alarms`, `hourly-trend`, `recent-table`)

### Faz 4 — MO birleşme

- [x] `ocDashboardWidgetAdapter.ts` — `OcDashboardWidgetDef` → legacy `@widgets` runtime
- [x] `OcDashboardWidgetHost.vue` — summaryCard/list/chart → WidgetHost + MO execution batch
- [x] `OcDashboardWidgetForm` → Widget Designer / template picker · `ocWidgetTemplateAdapter.ts`

### Faz 5 — Reporting

- [x] Snapshot / export hook · `widgetSnapshotExport.ts`, `useDashboardSnapshotExport`, `DashboardSnapshotExportMenu`, `POST /api/widgets/snapshot`
- [ ] Reporting Servis PDF/PNG render (ayrı proje — **ertelendi**)

---

## Odak manuel test — generic dashboard yüzeyi (B)

`/dashboards/:slug` yalnızca `layout.rows.length > 0` ise renderer gösterir; boş layout → **“Layout tanımlı değil”**.

| Kayıt | slug | Not |
|-------|------|-----|
| SIEM panel meta | `siem-center` | `rows: []` — UI `/apps/siem-center` (D3 hybrid) |
| **Demo surface** | **`widgets-demo`** | 6 widget + 3 satır layout — B checklist için |

**Kurulum (Odak):**

```powershell
.\docs\odak\operationcore\scripts\get-operationcore-token.ps1
.\docs\odak\widgets\scripts\setup-widget-demo-dashboard.ps1
.\docs\odak\widgets\scripts\smoke-widget-demo-dashboard.ps1
```

**UI:** `http://192.168.20.20:3000/dashboards/widgets-demo` — surface toolbar, batch fetch, cross-filter (donut), drill-down (stat), export menüsü.

---

## Domain chat'lerinde tamamlanacak (widget önkoşul)

| Domain | Eksik | Doküman |
|--------|-------|---------|
| Alarm | Trend/bucket snapshot | [DOMAIN_ALARM.md](./DOMAIN_ALARM.md) §5 |
| SIEM | Hourly buckets API | [DOMAIN_SIEM.md](./DOMAIN_SIEM.md) §6 |
| MO | `wi_count_by_state` query | [DOMAIN_OPERATION_CORE.md](./DOMAIN_OPERATION_CORE.md) §3.1 |
| DI | Stats/list API P1 | [DOMAIN_DOCUMENT_INTELLIGENCE.md](./DOMAIN_DOCUMENT_INTELLIGENCE.md) |

---

## Implementasyon için hazır mıyiz?

**Kısmen — Faz 0 dataset provizyonu tamam; UI adapter sırada.**

| Hazır | Sırada |
|-------|--------|
| Mimari, manifest, katalog, UX spec | Faz 0 UI: manifest adapter + preset registry |
| JSON Schema, category + template seed JSON | WidgetHost + fetch (Faz 1) |
| `@widget_templates` Odak kurulum script | P1 şablonları `isActive` açma (domain API hazır olunca) |

---

## Karar kaydı

| Tarih | Karar |
|-------|-------|
| 2026-06-07 | D0–D1, katman modeli, Grafana etkileşim |
| 2026-06-07 | D6 DI serviceRef; D7 MO terminolojisi + monitoring V1 dışı |
| 2026-06-07 | **D8** Alarm/SIEM serviceRef (MngAlarm/MngReactor) — DATA_CATALOG |
| 2026-06-07 | **D2–D5 kilitlendi** — öneri kabul |
| 2026-06-07 | **Planlama dokümantasyonu tamamlandı** — implementasyon bekliyor |
| 2026-06-07 | **Faz 0 seed:** `widget_templates_seed_v1.json` (19 kayıt, 6 P0 aktif) + `setup-widget-templates-datasets.ps1` — Odak provizyon |
| 2026-06-07 | **P0 smoke OK** (Odak): Alarm snapshot, SIEM summary, MO `wi_assigned_open`, DI children |
| 2026-06-07 | **Faz 1 UI:** Widget Designer wizard + dashboard template picker |
| 2026-06-07 | **P1:** 8 şablon aktif + queryRef fetch + P1 smoke script |
| 2026-06-07 | **Faz 3 + D3:** SIEM hybrid WidgetHost + `@dashboards` siem-center seed + server layout persist |
| 2026-06-07 | **Faz 5:** Dashboard snapshot JSON + CSV export hook + BFF `/api/widgets/snapshot` |
| 2026-06-07 | **A7/O2/D4/SIEM:** drill-down, widget actions, list-activity, scenario composite WidgetHost |
| 2026-06-07 | **SIEM Özet Paneli oturumu:** tablo sütun köprüsü, scenario serviceRef alias, rollup normalize, kompakt senaryo widget, layout seed + `patch-siem-overview-layout.ps1` — **yarın Odak DB + UI doğrulama** |
