# Widget Data Catalog

**Son güncelleme:** 7 Haziran 2026  
**Durum:** 📋 Planlama v1 — implementasyon öncesi referans  
**İlişkili:** [MANIFEST_SCHEMA.md](./MANIFEST_SCHEMA.md) · [KATALOG_V1.md](./KATALOG_V1.md)

---

## 1. Amaç

Widget designer’da kullanıcıya gösterilen **semantik veri kaynakları** ile arka plandaki **gerçek API/DG çağrısı** arasındaki eşleme. Teknik detay (pipeline, endpoint path) gizli kalır.

---

## 2. Üç bağlama tipi

| Tip | Manifest alanı | Ne zaman | V1 domain’ler |
|-----|----------------|----------|---------------|
| **DG predefined** | `queryRef` | Dataset schema’da tanımlı sorgu; tenant DG izni | **operation-core** (`op_work_items`) |
| **Domain API** | `serviceRef` | Microservice read API; iş kuralları serviste | **alarm**, **siem**, **document-intelligence** |
| **Statik** | *(yok)* | Sabit link/metin; veri çekme yok | Banner / quick-link şablonları |

> **Not:** Erken planda tüm domain’ler için `queryRef` varsayılmıştı. Kod gerçeği: Alarm (`MngAlarm`) ve SIEM olayları (`MngReactor`) **DG mirror üzerinden değil**, domain API snapshot/liste uçlarından besleniyor — SIEM paneli (`useSiemDashboardData`) bunu doğruluyor.

---

## 3. serviceRef sözleşmesi

```
{service}:{resource}/{action}
```

| service | Proxy | Örnek |
|---------|-------|-------|
| `mngalarm` | `/api/alarm/v1/` | `mngalarm:alarms/dashboard-snapshot` |
| `mngreactor` | `/api/reactor/v1/` → nginx → `mngreactor:5003/api/v1/` | `mngreactor:sec-events/dashboard-summary` |

> **Production auth:** `mngreactor` uçları `[Authorize]` — statik `mngui` deploy'da istemci **`Authorization: Bearer`** göndermeli (`secEventService.authHeaders`). Dev'de Nuxt `/api/reactor/*` cookie'den ekler.
| `mngdocument` | `/api/documents/` | `mngdocument:resources/search` |
| *(MO)* | DG | `@op_work_items/queries/wi_assigned_open` |

Parametreler manifest `parameters` + `SurfaceContext` ile çözülür; HTTP query/body adapter `WidgetHost` içinde (implementasyon — Faz 1).

---

## 4. queryRef sözleşmesi

```
@{dataset}/queries/{queryName}
```

Örnek: `@op_work_items/queries/wi_assigned_open`

DG: `POST /api/v1/data/op_work_items/queries/{queryName}` + parametre body.

---

## 5. Domain özet tablosu (V1)

| Domain | Doküman | Birincil tip | Dataset / servis |
|--------|---------|--------------|------------------|
| alarm | [DOMAIN_ALARM.md](./DOMAIN_ALARM.md) | `serviceRef` | MngAlarm |
| siem | [DOMAIN_SIEM.md](./DOMAIN_SIEM.md) | `serviceRef` | MngReactor (sec-events) + MngAlarm (panel alarm snapshot) |
| operation-core (MO) | [DOMAIN_OPERATION_CORE.md](./DOMAIN_OPERATION_CORE.md) | `queryRef` | `op_work_items` (+ workspace context) |
| document-intelligence | [DOMAIN_DOCUMENT_INTELLIGENCE.md](./DOMAIN_DOCUMENT_INTELLIGENCE.md) | `serviceRef` | MngDocument |

---

## 6. Eksik / oluşturulacak (implementasyon öncesi backlog)

Bu liste **widget Faz 1’den önce** ilgili domain chat’lerinde tamamlanır; widget chat yalnızca tüketir.

### 6.1 Alarm — MngAlarm API

| serviceRef (hedef) | Mevcut UI | Durum |
|--------------------|-----------|-------|
| `mngalarm:alarms/dashboard-snapshot` | `alarmDashboardSnapshot()` | ✅ API var |
| `mngalarm:alarms/list` | `alarmListOpen()` | ✅ API var |
| `mngalarm:alarms/severity-aggregate` | — | 🔲 Widget için aggregate uç veya snapshot genişletme |

### 6.2 SIEM — MngReactor

| serviceRef (hedef) | Mevcut UI | Durum |
|--------------------|-----------|-------|
| `mngreactor:sec-events/dashboard-summary` | `secEventDashboardSummary()` | ✅ API var |
| `mngreactor:sec-events/list` | `secEventQuery()` | ✅ API var |
| `mngreactor:sec-events/hourly-buckets` | SIEM panel hardcoded | 🔲 Snapshot’a bucket alanı veya ayrı uç |

### 6.3 MO — DG `op_work_items`

| queryRef | DG schema | Odak provizyon |
|----------|-----------|----------------|
| `wi_assigned_open` | draft JSON | ✅ canlı (demo/helpdesk seed) |
| `wi_sla_response_breach` | draft JSON | ✅ |
| `wi_sla_resolve_breach` | draft JSON | ✅ |
| `wi_by_workspace_and_state` | draft JSON | ✅ draft |
| `wi_board_column` | draft JSON | ✅ draft |
| `wi_count_by_state` | — | 🔲 aggregate predefined query (widget stat/donut) |

Kaynak: [operationcore_datasets_phase1_draft_2026-05-26.json](../operationcore/datasets/operationcore_datasets_phase1_draft_2026-05-26.json)

### 6.4 DI — MngDocument

Bkz. [DOMAIN_DOCUMENT_INTELLIGENCE.md](./DOMAIN_DOCUMENT_INTELLIGENCE.md) §6.1 P1 stats uçları.

---

## 7. Context → parametre eşlemesi (ortak)

| Context | Parametre adları |
|---------|------------------|
| `$timeRange.hours` | `rangeHours`, `hours` |
| `$timeRange.from` / `.to` | `from`, `to` |
| `$variables.severity` | `minSeverity`, `severity` |
| `$variables.workspaceId` | `workspaceId` |
| `$variables.currentUserId` | `assignee` (MO — MngPersonId) |

MO özel: `{{currentUser}}` → `CurrentUserId` (MngPersonId) — bkz. [operationcore/mngoperations/DEVAM.md](../operationcore/mngoperations/DEVAM.md) DASH-CARDS.

---

## 8. İlgili kod (referans)

| Servis | UI |
|--------|-----|
| MngAlarm | `Mng.Ui/services/alarmService.ts` |
| MngReactor sec-events | `Mng.Ui/services/secEventService.ts` |
| SIEM panel birleşik | `Mng.Ui/composables/useSiemDashboardData.ts` |
| DG widget fetch | `Mng.Ui/services/widgetDataService.ts` |
| MngDocument | `Mng.Ui/services/documentIntelligenceService.ts` |
