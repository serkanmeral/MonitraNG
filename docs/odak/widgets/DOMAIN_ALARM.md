# Alarm — Widget katalog kapsamı

**Son güncelleme:** 7 Haziran 2026  
**Durum:** 📋 Planlama  
**Veri yolu:** **`serviceRef` → MngAlarm** (DG `queryRef` değil)  
**İlişkili:** [DATA_CATALOG.md](./DATA_CATALOG.md) · [alarm/](../alarm/README.md)

---

## 1. Modül özeti

| Özellik | Durum |
|---------|--------|
| Alarm Merkezi UI | ✅ `/apps/alarm-center/alarms` |
| Kural yönetimi | ✅ `/apps/alarm-center/rules` |
| MngAlarm API | ✅ `/api/alarm/v1/` |
| Veri store | Domain Mongo — `@mon_alarms` (motor); UI **API** üzerinden |

Alarm verisi düşük gecikme ve motor iş kuralları nedeniyle **DG predefined query ile widget’a açılmaz**; `alarmService.ts` pattern’i `serviceRef` ile manifest’e taşınır.

---

## 2. Mevcut API uçları (widget kaynağı)

| UI fonksiyon | HTTP | Widget kullanımı |
|--------------|------|------------------|
| `alarmListOpen` | `GET /alarms?openOnly=&minSeverity=&…` | Tablo widget |
| `alarmDashboardSnapshot` | `GET /alarms/dashboard-snapshot?rangeHours=&minSeverity=&openLimit=` | Stat + trend + son alarmlar |
| `alarmGet` | `GET /alarms/{id}` | Drill-down detay |

Kaynak: `Mng.Ui/services/alarmService.ts`

### 2.1 Dashboard snapshot yanıt alanları (özet)

`AlarmDashboardSnapshot` — stat kart ve tablo field map için referans (implementasyonda tip dosyası: `@/types/apps/alarm`).

Tipik alanlar: açık alarm sayıları, severity dağılımı, son N alarm listesi, zaman aralığı metadata. *(Tam şema implementasyon chat’inde snapshot DTO’dan export edilebilir.)*

---

## 3. Öntanımlı widget şablonları (V1 seed)

| templateId | kind | serviceRef | Parametreler (kullanıcı) |
|------------|------|------------|--------------------------|
| `alarm.open-count-stat` | stat | `mngalarm:alarms/dashboard-snapshot` | `minSeverity`, `$timeRange.hours` → field `openCount` |
| `alarm.severity-distribution-donut` | chart | aynı snapshot | severity buckets → donut |
| `alarm.recent-table` | table | aynı snapshot veya `mngalarm:alarms/list` | `openLimit`, `minSeverity` |
| `alarm.trend-area` | chart | 🔲 snapshot genişletme veya aggregate uç | `$timeRange` |

---

## 4. Yüzeyler

| Yüzey | Uygun widget |
|-------|--------------|
| Dashboard | ✅ Tüm V1 şablonlar |
| Alarm Merkezi | ✅ Özet panel (Faz 3+ layout) |
| SIEM panel | ✅ `openAlarms` stat (mevcut hardcoded → template) |
| Workspace panel | ⚠️ Workspace-scoped alarm yoksa genel filtre |
| Report | ✅ Snapshot `rangeHours` sabit |

**Drill-down:** `/apps/alarm-center/alarms` + `status`, `severity`, `ruleId` query params.

---

## 5. Eksik API / iş paketi (Alarm chat)

| # | İş | Sahip |
|---|-----|-------|
| A-W1 | Snapshot DTO’ya saatlik trend bucket (veya ayrı uç) | MngAlarm |
| A-W2 | Severity aggregate tek sayı / dizi garantisi (widget fieldMap) | MngAlarm |
| A-W3 | Manifest `serviceRef` → `alarmService` adapter spec | Widget Faz 1 |

---

## 6. Kategori seed

`@widget_categories`: `alarm-kpi`, `alarm-charts`, `alarm-tables`
