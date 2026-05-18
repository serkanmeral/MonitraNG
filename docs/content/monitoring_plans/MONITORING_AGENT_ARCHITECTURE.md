# Monitoring Agent Mimarisi

Bu doküman, MonitraNG Monitoring’de **Agent** kavramının tanımını, yapısını ve veri modelini açıklar. Planlama özeti için [MonitraNG Monitoring planlama](monitrang_monitoring_planlama.md) dokümanına bakınız.

---

## 1. Tanımlar

### 1.1 Agent

**Agent**, sunucu tarafında (Reactor üzerinden) oluşturulan bir **kayıt**dır. Veri toplama işleminin tanımını tutar.

| Özellik | Açıklama |
|---------|----------|
| **Konum** | Sunucu tarafı (MngDataGateway üzerinden saklanır) |
| **Rol** | Hangi asset'lerden, hangi periyotta ve ne zaman veri okunacağının tanımı |
| **Oluşturma** | Reactor CRUD endpoint'leri üzerinden (DG data API kullanarak) |

### 1.2 Engine

**Engine**, kendisine verilen **agent tanımına** göre veri okuma işini yapan ve sunucuya (Reactor'a) gönderen bir **servistir**.

| Özellik | Açıklama |
|---------|----------|
| **Rol** | Agent tanımını alır, asset'lerden veri okur, Reactor'a gönderir |
| **Kararlar** | Engine ile ilgili detaylı kararlar ileride alınacak |

---

## 2. Yan Tanım Kaynakları (Reusable Definitions)

Periyot ve izleme aralığı tanımları **ayrı dataset'lerde** tutulur; Agent bu tanımlara referans verir.

### 2.1 mon_collection_periods (Toplama periyotları)

**Amaç:** Yeniden kullanılabilir toplama periyodu tanımları. Kullanıcı periyot tanımı yapar, asset yapılandırmasında seçer.

| Alan | Tip | Açıklama |
|------|-----|----------|
| name | text | Görünen ad (örn. "Her 5 dakika") |
| description | text | Opsiyonel açıklama |
| expression | text | Cron ifadesi (örn. `*/5 * * * *`) |

**Cron formatı:** Standart 5 alan `dakika saat gün ay haftanın_günü`. Saniye gerekiyorsa Quartz 6 alanlı format kullanılabilir.

**Örnek kayıtlar:**
- Her 15 sn: `*/15 * * * * *` (6 alan)
- Her 5 dk: `*/5 * * * *`
- Her saat: `0 * * * *`

### 2.2 mon_schedules (İzleme aralıkları)

**Amaç:** Yeniden kullanılabilir izleme aralığı (window) tanımları. Kullanıcı günler ve saatler seçerek kaydeder, asset yapılandırmasında seçer.

| Alan | Tip | Açıklama |
|------|-----|----------|
| name | text | Görünen ad (örn. "Hafta içi mesai") |
| description | text | Opsiyonel açıklama |
| type | text | `always` \| `scheduled` |
| config | object | `type: scheduled` iken dolu; `type: always` iken boş/null |

**type = "always" (sürekli izleme):**
- 7/24 izleme. `config` boş veya null.
- Engine, config'e bakmadan her zaman toplama yapar.

**type = "scheduled" (zamanlanmış izleme):**
- `config` yapısı örnek:
```json
{
  "weekdays": [1, 2, 3, 4, 5],
  "startTime": "08:00",
  "endTime": "19:00"
}
```
- `weekdays`: 0=Pazar, 1=Pazartesi, … 6=Cumartesi
- `startTime`, `endTime`: `"HH:mm"` formatı
- Örnek: Hafta sonu → `weekdays: [0, 6]`, start/end boş veya 00:00–23:59

### 2.3 Varsayılan değerler (öncelik sırası)

Çözüm sırası: **asset_config** → **agent default** → **Engine global**

| Alan | 1. asset_config | 2. Agent default | 3. Engine global |
|------|-----------------|------------------|------------------|
| periodId | Varsa kullan | defaultPeriodId | "1 dakika" |
| scheduleId | Varsa kullan | defaultScheduleId | "her zaman" (sürekli) |

**Karar:** Tenant oluşturulduğunda **otomatik** olarak "Sürekli" (mon_schedules) ve "1 dakika" (mon_collection_periods) kayıtları oluşturulur. Agent ve asset_config'larda periodId/scheduleId yoksa bu varsayılanlara referans verilir.

---

## 3. mon_agents Dataset

**Database:** `mng_{domain_name}` (tenant veritabanı).

### 3.1 Şema

| Alan | Tip | Açıklama |
|------|-----|----------|
| name | text | Agent adı (domain içinde unique) |
| description | text | Opsiyonel açıklama |
| status | text | `active` \| `inactive` \| `maintenance` |
| engineId | relation | `mon_engines` __dataId. Bu agent'ı çalıştıracak Engine. |
| defaultPeriodId | relation | Opsiyonel. asset_config'ta yoksa bu kullanılır. |
| defaultScheduleId | relation | Opsiyonel. asset_config'ta yoksa bu kullanılır. |
| tags | object[] | Opsiyonel. Raporlama/filtreleme: `[{ "key", "value" }]` |
| asset_configs | object[] | Asset bazlı yapılandırma |

**Validasyon:** `name` domain içinde benzersiz olmalı. Reactor ve DG index ile kontrol edilir.

### 3.2 asset_configs yapısı

Her eleman:

| Alan | Tip | Zorunlu | Açıklama |
|------|-----|---------|----------|
| assetId | relation | Evet | `mon_assets` __dataId. **Unique:** Aynı asset bir agent'ta yalnızca bir kez bulunabilir. |
| periodId | relation | Hayır | Yoksa agent defaultPeriodId, o da yoksa Engine varsayılanı |
| scheduleId | relation | Hayır | Yoksa agent defaultScheduleId, o da yoksa Engine varsayılanı |
| active | bool | Evet | `false` ise bu asset izlenmez |
| description | text | Hayır | Opsiyonel not (örn. neden bu periyotta izlendiği) |

**Validasyon:** `asset_configs` içinde `assetId` değerleri unique olmalı. Reactor validasyonunda kontrol edilir.

### 3.3 status anlamları

| status | Engine davranışı |
|--------|------------------|
| active | Normal izleme. Tüm `active: true` asset'ler toplanır. |
| inactive | Agent işlenmez; hiçbir asset'ten veri okunmaz. |
| maintenance | İzleme yapılır; ileride bakım/uyarı bayrakları kullanılabilir. |

### 3.4 Örnek
```json
{
  "name": "Ana Veri Toplama",
  "description": "Production asset'leri",
  "status": "active",
  "engineId": "e1-1111-2222-3333-444444444444",
  "defaultPeriodId": "p1-1111-2222-3333-444444444444",
  "defaultScheduleId": "s1-1111-2222-3333-444444444444",
  "tags": [{ "key": "env", "value": "prod" }],
  "asset_configs": [
    {
      "assetId": "c3d4e5f6-a7b8-9012-cdef-555555555555",
      "periodId": "p1-1111-2222-3333-444444444444",
      "scheduleId": "s1-1111-2222-3333-444444444444",
      "active": true,
      "description": "Kritik web sunucusu"
    },
    {
      "assetId": "c3d4e5f6-a7b8-9012-cdef-666666666666",
      "active": true
    }
  ]
}
```
İkinci asset'te periodId/scheduleId yok; agent default kullanılır.

---

## 4. Agent – Engine İlişkisi

```
[mon_collection_periods] ──┐
[mon_schedules] ──────────┼──→ [mon_agents] (asset_configs referans verir)
[mon_assets] ─────────────┘           ↓
                              Engine tanımı okur
                                    ↓
                              Veri toplar → Reactor'a gönderir
```

- Engine, `status: active` veya `maintenance` olan agent'ları işler; `inactive` atlanır.
- `asset_configs` içinde `active: false` olan asset'ler atlanır.
- Her asset için `periodId` ve `scheduleId` belirlenir: önce asset_config, yoksa agent default, yoksa Engine varsayılanı.
- `mon_assets` üzerinden `connection_info` ve `collectibles` okunur.

**Agent–Engine ataması:** `mon_agents`'e **engineId** (relation to `mon_engines`) alanı eklenir. Bir agent tek bir Engine'e atanır; bir Engine birden fazla agent çalıştırır. Detay: [Monitoring Engine Architecture](MONITORING_ENGINE_ARCHITECTURE.md).

---

## 5. Açık Sorular

1. ~~**Sistem varsayılan kayıtları:** Tenant oluşturulduğunda `mon_schedules` ve `mon_collection_periods` için "Sürekli" ve "1 dakika" otomatik oluşturulsun mu?~~ **Karar:** Evet. Tenant oluşturulduğunda otomatik oluşturulacak.

---

## 6. DG Şemaları (CreateDataset uyumlu)

Aşağıdaki JSON blokları MngDataGateway `CreateDataset` API ile uyumludur. Tüm dataset'ler `mng_{domain_name}` veritabanında.

### 6.1 mon_collection_periods

```json
{
  "Name": "mon_collection_periods",
  "Description": "Monitoring – Toplama periyodu tanımları (cron ifadeleri).",
  "ForceSchema": true,
  "Logging": "none",
  "PublishMode": "none",
  "Fields": [
    {
      "fieldType": "text",
      "name": "name",
      "title": "Periyot adı",
      "mandatory": true,
      "unique": false,
      "isArray": false
    },
    {
      "fieldType": "text",
      "name": "description",
      "title": "Açıklama",
      "mandatory": false,
      "isArray": false
    },
    {
      "fieldType": "text",
      "name": "expression",
      "title": "Cron ifadesi",
      "mandatory": true,
      "isArray": false
    }
  ],
  "IndexList": []
}
```

### 6.2 mon_schedules

```json
{
  "Name": "mon_schedules",
  "Description": "Monitoring – İzleme aralığı (window) tanımları.",
  "ForceSchema": true,
  "Logging": "none",
  "PublishMode": "none",
  "Fields": [
    {
      "fieldType": "text",
      "name": "name",
      "title": "Schedule adı",
      "mandatory": true,
      "unique": false,
      "isArray": false
    },
    {
      "fieldType": "text",
      "name": "description",
      "title": "Açıklama",
      "mandatory": false,
      "isArray": false
    },
    {
      "fieldType": "text",
      "name": "type",
      "title": "Tip (always | scheduled)",
      "mandatory": true,
      "isArray": false
    },
    {
      "fieldType": "object",
      "name": "config",
      "title": "Zamanlama config (type=scheduled iken)",
      "mandatory": false,
      "isArray": false
    }
  ],
  "IndexList": []
}
```

### 6.3 mon_agents

```json
{
  "Name": "mon_agents",
  "Description": "Monitoring – Agent tanımları (veri toplama yapılandırması).",
  "ForceSchema": true,
  "Logging": "none",
  "PublishMode": "none",
  "Fields": [
    {
      "fieldType": "text",
      "name": "name",
      "title": "Agent adı",
      "mandatory": true,
      "unique": true,
      "isArray": false
    },
    {
      "fieldType": "text",
      "name": "description",
      "title": "Açıklama",
      "mandatory": false,
      "isArray": false
    },
    {
      "fieldType": "text",
      "name": "status",
      "title": "Durum",
      "mandatory": true,
      "isArray": false
    },
    {
      "fieldType": "relation",
      "name": "engineId",
      "title": "Engine (bu agent'ı çalıştıracak)",
      "mandatory": true,
      "isArray": false,
      "relationDataset": "mon_engines"
    },
    {
      "fieldType": "relation",
      "name": "defaultPeriodId",
      "title": "Varsayılan periyot",
      "mandatory": false,
      "isArray": false,
      "relationDataset": "mon_collection_periods"
    },
    {
      "fieldType": "relation",
      "name": "defaultScheduleId",
      "title": "Varsayılan izleme aralığı",
      "mandatory": false,
      "isArray": false,
      "relationDataset": "mon_schedules"
    },
    {
      "fieldType": "object",
      "name": "tags",
      "title": "Etiketler (key-value)",
      "mandatory": false,
      "isArray": true
    },
    {
      "fieldType": "object",
      "name": "asset_configs",
      "title": "Asset yapılandırmaları",
      "mandatory": true,
      "isArray": true
    }
  ],
  "IndexList": [
    { "name": "idx_name", "fields": { "name": 1 }, "unique": true },
    { "name": "idx_engineId", "fields": { "engineId": 1 }, "unique": false },
    { "name": "idx_status", "fields": { "status": 1 }, "unique": false }
  ]
}
```

`asset_configs` her elemanı uygulama tarafında `{ assetId, periodId?, scheduleId?, active, description? }` yapısında doğrulanır. `assetId` unique olmalı. DG object alanı serbest kabul eder. `idx_engineId`: Engine'in kendisine atanmış agent'ları sorgulaması için.

---

## 7. Referanslar

- [MonitraNG Monitoring planlama](monitrang_monitoring_planlama.md)
- [Monitoring Asset Datasets](MONITORING_ASSET_DATASETS.md)
