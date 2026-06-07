# SIEM — Widget katalog kapsamı

**Son güncelleme:** 7 Haziran 2026  
**Durum:** 📋 Planlama  
**Veri yolu:** **`serviceRef` → MngReactor** (+ panelde alarm snapshot için **MngAlarm**)  
**İlişkili:** [DATA_CATALOG.md](./DATA_CATALOG.md) · [monitoring/](../monitoring/README.md)

---

## 1. Modül özeti

| Bileşen | Route / API |
|---------|-------------|
| SIEM Güvenlik Paneli | `/apps/siem-center` |
| Olay arama | `/apps/siem-center/events` |
| Olay verisi | `GET /api/reactor/v1/sec-events` |
| Dashboard özeti | `GET /api/reactor/v1/sec-events/dashboard-summary` |
| Panel alarm stat | `GET /api/alarm/v1/alarms/dashboard-snapshot` (birleşik payload) |

Mevcut panel: `AcSiemCenterDashboard.vue` + `useSiemDashboardData.ts` — hardcoded; Faz 3’te widget template + layout’a taşınacak.

**Not:** SIEM plan dokümanları `docs/odak/monitoring/` altında; manifest **`domain: siem`**.

---

## 2. serviceRef kayıtları

### 2.1 Olaylar (MngReactor)

| serviceRef | UI | Parametreler |
|----------|-----|--------------|
| `mngreactor:sec-events/dashboard-summary` | `secEventDashboardSummary()` | `rangeHours`, `excludeUnknown` |
| `mngreactor:sec-events/list` | `secEventQuery()` | `from`, `to`, `eventAction`, `limit`, … |

### 2.2 Panel alarm parçası (MngAlarm)

| serviceRef | UI | Not |
|----------|-----|-----|
| `mngalarm:alarms/dashboard-snapshot` | `alarmDashboardSnapshot()` | SIEM hero stat: openAlarms, loginFailed vb. bir kısmı events summary’den |

Composite widget `siem.scenario-cards` — senaryo katalogu + olay/alarm birleşimi; Faz 3 migrasyon.

---

## 3. Öntanımlı widget şablonları (V1 seed)

| templateId | kind | serviceRef | Kaynak (bugünkü panel) |
|------------|------|------------|------------------------|
| `siem.events-total-stat` | stat | `mngreactor:sec-events/dashboard-summary` | `eventsTotal` |
| `siem.login-failed-stat` | stat | aynı | `loginFailed` |
| `siem.events-hourly-trend` | chart | 🔲 bucket uç veya summary genişletme | `hourlyBuckets` (hardcoded) |
| `siem.recent-events-table` | table | `mngreactor:sec-events/list` | events sayfası benzeri |
| `siem.open-alarms-stat` | stat | `mngalarm:alarms/dashboard-snapshot` | `openAlarms` |
| `siem.scenario-cards` | composite | çoklu ref | Faz 3 — `useSiemScenarioCatalog` |

---

## 4. Context variables

| Variable | Kullanım |
|----------|----------|
| `$timeRange.hours` | `rangeHours` (varsayılan 24) |
| `$timeRange.from` / `.to` | Olay listesi / arama |
| `$variables.scenarioId` | Senaryo filtresi (ileride) |

---

## 5. Yüzeyler

| Yüzey | Not |
|-------|-----|
| `siem-center` | Birincil — layout persist D3 |
| `dashboard` | Aynı template’ler |
| `dashboard-container` | NOC rotasyon |

---

## 6. Eksik iş paketi

| # | İş | Sahip |
|---|-----|-------|
| S-W1 | `dashboard-summary` içinde saatlik bucket dizisi (chart widget) | MngReactor |
| S-W2 | Scenario rollup API (kart grid) | MngReactor / SIEM chat |
| S-W3 | SIEM layout → `@dashboards` + `surfaceKind=siem-center` (D3) | Widget Faz 3 |

Kaynak composable: `Mng.Ui/composables/useSiemDashboardData.ts`
