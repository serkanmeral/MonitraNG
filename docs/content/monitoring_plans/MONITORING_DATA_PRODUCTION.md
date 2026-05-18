# Monitoring Veri Üretme ve Kalıcılık Mimarisi

Bu doküman, MonitraNG Monitoring'de üretilen metrik verisinin **formatını**, **saklama yapısını** (MongoDB Time Series) ve **Engine → Reactor** akışını tanımlar. Planlama özeti için [MonitraNG Monitoring planlama](monitrang_monitoring_planlama.md) dokümanına bakınız.

---

## 1. Genel Akış

```
[MngEngine] → Collector toplar → Batch payload oluşturur → [Reactor Ingest]
                                                                    ↓
[MongoDB Time Series: mon_metrics] ← Reactor her metriği ayrı dokümana yazar
```

- **Engine:** Agent tanımına göre asset'lerden veri toplar; batch formatında Reactor'a gönderir.
- **Reactor:** Batch'i alır, her metriği ayrı Time Series dokümanına dönüştürür, MongoDB'ye yazar.
- **Storage:** MngDataGateway dışında, Reactor **doğrudan MongoDB Time Series** koleksiyonuna yazar (performans ve Time Series optimizasyonu için).

---

## 2. MongoDB Time Series Koleksiyonu

### 2.1 Koleksiyon oluşturma

```javascript
db.createCollection("mon_metrics", {
  timeseries: {
    timeField: "timestamp",
    metaField: "meta",
    granularity: "seconds"  // saniye/dakika bazlı toplama için
  },
  expireAfterSeconds: 2592000  // 30 gün — TTL env'den gelir
})
```

### 2.2 Konum

- **Database:** `mng_{domain_name}` (tenant veritabanı)
- **Collection:** `mon_metrics`
- **TTL:** `MONITORING_METRICS_TTL_DAYS` environment değişkeni ile (örn. 30). Reactor başlangıcında `expireAfterSeconds = TTL_DAYS * 86400` hesaplanır; `collMod` ile güncellenebilir.

### 2.3 TTL güncelleme (mevcut koleksiyon)

```javascript
db.runCommand({
  collMod: "mon_metrics",
  expireAfterSeconds: 604800  // 7 gün
})
```

---

## 3. Metrik Doküman Şeması

### 3.1 Tek metrik dokümanı (Time Series'te saklanan)

| Alan | Tip | Açıklama |
|------|-----|----------|
| timestamp | datetime | Toplama zamanı (ISO 8601) |
| meta | object | Boyutlar — sorgu ve bucketing için |
| value | number \| string \| object | Metrik değeri |
| unit | string | Opsiyonel; sayısal metriklerde (%, KB, MB) |

### 3.2 meta yapısı

| Alan | Açıklama |
|------|----------|
| domain | Tenant domain adı |
| assetId | `mon_assets` __dataId |
| itemId | `mon_items` __dataId — dashboard Item filtresi için |
| agentId | `mon_agents` __dataId |
| engineId | `mon_engines` __dataId. Tek Engine = tek mon_engines kaydı. |
| collectibleCode | Metrik tipi (örn. cpu_usage, memory_used, sysdescr) |

### 3.3 value tipi

- **number:** CPU %, memory KB, disk kullanımı vb.
- **string:** SNMP sysDescr vb.
- **object:** Bileşik metrikler (örn. disk_usage: { total, used, free, percent })

---

## 4. Engine → Reactor Batch Payload

Engine, topladığı metrikleri **batch array** olarak gönderir. Reactor her metriği ayrı dokümana çevirir.

### 4.1 İstek formatı (batch array)

Engine, birikmiş batch'leri `batches` dizisinde gönderir:

```json
{
  "batches": [
    {
      "domain": "acme",
      "assetId": "c3d4e5f6-a7b8-9012-cdef-555555555555",
      "itemId": "d4e5f6a7-b8c9-0123-defa-555555555555",
      "agentId": "a1b2c3d4-1111-2222-3333-444444444444",
      "engineId": "engine-instance-01",
      "collectedAt": "2025-01-30T12:00:05.000Z",
      "metrics": [
        { "collectibleCode": "cpu_usage", "value": 34.5, "unit": "%" },
        { "collectibleCode": "memory_used", "value": 2048576, "unit": "KB" }
      ]
    }
  ]
}
```

Tek batch gönderilebilir (`batches: [batch]`). Payload şifrelenir ve sıkıştırılır; Reactor decrypt + decompress sonrası bu yapıyı alır.

### 4.2 Dönüşüm kuralı

Her batch'teki her `metrics` elemanı için Reactor bir doküman üretir:

```json
{
  "timestamp": "2025-01-30T12:00:05.000Z",
  "meta": {
    "domain": "acme",
    "assetId": "c3d4e5f6-a7b8-9012-cdef-555555555555",
    "itemId": "d4e5f6a7-b8c9-0123-defa-555555555555",
    "agentId": "a1b2c3d4-1111-2222-3333-444444444444",
    "engineId": "engine-instance-01",
    "collectibleCode": "cpu_usage"
  },
  "value": 34.5,
  "unit": "%"
}
```

### 4.3 Ingest yanıt formatı

Reactor, partial success destekler. Yanıt:

```json
{
  "savedCount": 42,
  "failedCount": 3,
  "errorList": [
    {
      "batchIndex": 1,
      "metricIndex": 0,
      "code": "validation_error",
      "message": "Invalid value type"
    },
    {
      "batchIndex": 2,
      "metricIndex": 2,
      "code": "missing_assetId",
      "message": "assetId is required"
    }
  ]
}
```

| Alan | Açıklama |
|------|----------|
| savedCount | Başarıyla yazılan metrik sayısı |
| failedCount | Yazılamayan metrik sayısı |
| errorList | Hata detayları: batchIndex, metricIndex (veya metricKey), code, message |

**HTTP status:** Partial success için `200 OK`. Tümü başarısızsa implementasyonda `422` veya `400` değerlendirilebilir.

---

## 5. Örnek Dokümanlar

### 5.1 Sayısal metrik (CPU)

```json
{
  "timestamp": "2025-01-30T12:00:05.000Z",
  "meta": {
    "domain": "acme",
    "assetId": "c3d4e5f6-a7b8-9012-cdef-555555555555",
    "itemId": "d4e5f6a7-b8c9-0123-defa-555555555555",
    "agentId": "a1b2c3d4-1111-2222-3333-444444444444",
    "engineId": "engine-01",
    "collectibleCode": "cpu_usage"
  },
  "value": 34.5,
  "unit": "%"
}
```

### 5.2 String metrik (SNMP sysDescr)

```json
{
  "timestamp": "2025-01-30T12:00:00.000Z",
  "meta": {
    "domain": "acme",
    "assetId": "c3d4e5f6-a7b8-9012-cdef-666666666666",
    "itemId": "d4e5f6a7-b8c9-0123-defa-666666666666",
    "agentId": "a1b2c3d4-1111-2222-3333-444444444444",
    "engineId": "engine-01",
    "collectibleCode": "sysdescr"
  },
  "value": "Cisco IOS Software, Version 15.2(4)M6"
}
```

### 5.3 Object metrik (disk usage)

```json
{
  "timestamp": "2025-01-30T12:00:05.000Z",
  "meta": {
    "domain": "acme",
    "assetId": "c3d4e5f6-a7b8-9012-cdef-555555555555",
    "itemId": "d4e5f6a7-b8c9-0123-defa-555555555555",
    "agentId": "a1b2c3d4-1111-2222-3333-444444444444",
    "engineId": "engine-01",
    "collectibleCode": "disk_usage"
  },
  "value": {
    "total": 500000000,
    "used": 320000000,
    "free": 180000000,
    "percent": 64.0
  },
  "unit": "KB"
}
```

---

## 6. Dashboard ve Query Builder Desteği

Bu yapı dynamic widget ve query builder için uygundur:

| İhtiyaç | Karşılık |
|---------|----------|
| Zaman aralığı | `timestamp` üzerinde filtre |
| Metrik seçimi | `meta.collectibleCode` |
| Asset / Item filtresi | `meta.assetId`, `meta.itemId` |
| Tenant | `meta.domain` |
| Aggregation | `value` üzerinde `$avg`, `$max`, `$min`, `$sum` |
| Time-series grafik | Zaman bucketing + aggregation |
| Gauge / tek değer | Son değer veya aralık ortalaması |

---

## 7. Environment Değişkeni

| Değişken | Açıklama | Varsayılan |
|----------|----------|------------|
| MONITORING_METRICS_TTL_DAYS | Metrik verisi retention süresi (gün) | 30 |

Reactor, bu değeri `expireAfterSeconds = TTL_DAYS * 86400` olarak MongoDB'ye uygular.

---

## 8. Referanslar

- [MonitraNG Monitoring planlama](monitrang_monitoring_planlama.md)
- [Monitoring Agent Architecture](MONITORING_AGENT_ARCHITECTURE.md)
- [Monitoring Asset Datasets](MONITORING_ASSET_DATASETS.md)
- [MongoDB Time Series Collections](https://www.mongodb.com/docs/manual/core/timeseries-collections/)
