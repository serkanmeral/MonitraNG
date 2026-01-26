# Widget Kütüphanesi Spesifikasyonu

**Tarih:** Ocak 2026  
**Durum:** 📋 Planlama  
**Hedef:** Widget kategorileri ve widget tanımları için dataset yapısı; Data Source olarak DG GET işlemleri (default, query, aggregate, predefined query) kullanımı.

**İlişkili doküman:** [DYNAMIC_DASHBOARD_SPEC.md](./DYNAMIC_DASHBOARD_SPEC.md) — Dashboard tanımları (`@dashboards`) ve layout. Widget'lar bu dashboard'ların layout'unda `widgetId` ile konumlandırılır.

---

## 1. Widget Type Enum

Widget türü sabit enum ile tanımlanır:

| Değer    | Açıklama                                      |
|----------|-----------------------------------------------|
| `card`   | Card widget'lar (stat-card, quick-access, vb.) |
| `chart`  | Grafik widget'lar (line, bar, pie, donut, vb.) |
| `table`  | Tablo widget'lar (v-data-table)               |
| `banner` | Banner widget'lar                             |

**`@widgets` dataset'inde `type` field'ı:** Bu değerlerden biri olmalı; validation ile sınırlanabilir.

---

## 2. DG Data GET İşlemleri (Özet)

Widget **dataSource** yapılandırmasında, **data** tipi için MngDataGateway (DG) Data API’sinin aşağıdaki GET benzeri işlemleri kullanılır.

### 2.1 Default GET — Liste (Tablo için uygun)

**Endpoint:** `GET /api/v1/data/{datasetName}`

Tüm veriyi sayfalı, sıralı ve filtrelenmiş şekilde döner. Tablo widget’larında kullanılır.

| Parametre     | Tip    | Varsayılan | Açıklama |
|---------------|--------|------------|----------|
| `skip`        | number | 0          | Atlanacak kayıt sayısı |
| `limit`       | number | 50, max 1000 | Dönecek maksimum kayıt |
| `sort`        | string | -          | MongoDB tarzı: `"field1,-field2"` |
| `filter`      | string | -          | RESTful: `field:operator:value` (virgülle çoklu) |
| `fields`      | string | -          | Seçilecek alanlar: `"field1,field2,field3"` |
| `search`      | string | -          | Metin araması (searchable alanlarda) |
| `format`      | string | `"json"`   | `"json"` veya `"csv"` |
| `expand`      | bool   | true       | İlişki genişletme |
| `deep`        | number | -          | İlişki derinliği |
| `showHistory` | bool   | false      | `__history` dahil |

**Filter formatı:** `field:operator:value`  
**Operatörler:** `eq`, `ne`, `gt`, `gte`, `lt`, `lte`, `in`, `nin`, `contains`, `startsWith`, `endsWith`  
**Örnek:** `status:eq:active,createdAt:gte:2025-01-01`

**Yanıt:** Array + `X-Total-Count` header (pagination için).

---

### 2.2 Query — MongoDB Match

**Endpoint:** `POST /api/v1/data/{datasetName}/query`

**Body:** `{ "match": { ... } }` — MongoDB native match objesi.

| Query Parametre | Tip    | Varsayılan | Açıklama |
|-----------------|--------|------------|----------|
| `skip`          | number | 0          | Atlanacak kayıt |
| `limit`         | number | 50, max 1000 | Maksimum kayıt |
| `sort`          | string | -          | `"field1,-field2"` |
| `fields`        | string | -          | `"field1,field2,field3"` |
| `expand`        | bool   | true       | İlişki genişletme |
| `deep`          | number | -          | İlişki derinliği |
| `showHistory`   | bool   | false      | `__history` dahil |

**Örnek body:**
```json
{
  "match": {
    "$or": [
      { "status": "active" },
      { "priority": { "$gte": 3 } }
    ]
  }
}
```

**Yanıt:** Array.

---

### 2.3 Aggregate — Ham MongoDB Pipeline

**Endpoint:** `POST /api/v1/data/{datasetName}/aggregate`

**Body:** `{ "pipeline": [ ... ] }` — MongoDB aggregation stage'leri.

**Örnek:**
```json
{
  "pipeline": [
    { "$match": { "status": "active" } },
    { "$group": { "_id": "$category", "count": { "$sum": 1 } } },
    { "$sort": { "count": -1 } },
    { "$limit": 10 }
  ]
}
```

**Yanıt:** Array (aggregation sonucu).

---

### 2.4 Predefined Query — Dataset’teki Öntanımlı Sorgular

**Endpoint:** `POST /api/v1/data/{datasetName}/queries/{queryName}`

Dataset schema içinde tanımlı **predefined query** çalıştırılır.

**Body:** Parametreler (key-value). Schema’daki query parametre tanımlarına uygun olmalı.

**Örnek:** Dataset’te `activeTasks` adlı query, `limit` parametresi bekliyorsa:
```json
{
  "limit": 20
}
```

**Yanıt:** Array.

---

## 3. Data Source Yapılandırması (dataSource)

**dataSource** tipi: `user` | `license` | `dataset` | `data`

Sadece **`data`** tipinde DG GET işlemleri kullanılır. `getMethod` ile hangi işlemin kullanılacağı seçilir.

### 3.1 Data Source Get Method Enum

| Değer         | DG İşlemi       | Endpoint / Açıklama |
|---------------|-----------------|----------------------|
| `default`     | Default GET     | `GET /api/v1/data/{dataset}` — Liste, tablo için |
| `query`       | Query with match| `POST /api/v1/data/{dataset}/query` |
| `aggregate`   | Raw aggregate   | `POST /api/v1/data/{dataset}/aggregate` |
| `predefined`  | Predefined query| `POST /api/v1/data/{dataset}/queries/{queryName}` |

### 3.2 dataSource Obje Yapısı (type: `data`)

```typescript
interface DataSourceConfigData {
  type: 'data';
  dataset: string;                    // Dataset adı (örn. "@tasks")
  getMethod: 'default' | 'query' | 'aggregate' | 'predefined';

  // getMethod = 'default'
  default?: {
    skip?: number;
    limit?: number;
    sort?: string;
    filter?: string;
    fields?: string;
    search?: string;
    format?: 'json' | 'csv';
    expand?: boolean;
    deep?: number;
    showHistory?: boolean;
  };

  // getMethod = 'query'
  query?: {
    match: object;                    // MongoDB match
    skip?: number;
    limit?: number;
    sort?: string;
    fields?: string;
    expand?: boolean;
    deep?: number;
    showHistory?: boolean;
  };

  // getMethod = 'aggregate'
  aggregate?: {
    pipeline: object[];               // MongoDB aggregation stages
  };

  // getMethod = 'predefined'
  predefined?: {
    queryName: string;                // Schema’daki query adı
    parameters?: Record<string, any>; // Parametreler
  };

  mapping?: {                         // API yanıtı → widget verisi (opsiyonel)
    items?: string;                   // Default GET: body array; query/aggregate/predefined: body
    total?: string;                   // Default GET: X-Total-Count header (client'ta ayrı okunur)
    [key: string]: any;
  };
}
```

### 3.3 Örnek dataSource Tanımları

**Default GET (tablo):**
```json
{
  "type": "data",
  "dataset": "@tasks",
  "getMethod": "default",
  "default": {
    "limit": 20,
    "sort": "-createdAt",
    "filter": "status:eq:active",
    "fields": "title,status,priority,createdAt"
  },
  "mapping": {
    "items": "data",
    "total": "X-Total-Count"
  }
}
```
*Not:* `total` — Default GET yanıtında sayı `X-Total-Count` header'ında gelir; client bu header'ı okuyarak kullanır.

**Query (match):**
```json
{
  "type": "data",
  "dataset": "@tasks",
  "getMethod": "query",
  "query": {
    "match": { "status": "active", "priority": { "$gte": 2 } },
    "limit": 10,
    "sort": "-createdAt",
    "fields": "title,status,priority"
  }
}
```

**Aggregate:**
```json
{
  "type": "data",
  "dataset": "@tasks",
  "getMethod": "aggregate",
  "aggregate": {
    "pipeline": [
      { "$match": { "status": "active" } },
      { "$group": { "_id": "$priority", "count": { "$sum": 1 } } },
      { "$sort": { "_id": 1 } }
    ]
  }
}
```

**Predefined query:**
```json
{
  "type": "data",
  "dataset": "@tasks",
  "getMethod": "predefined",
  "predefined": {
    "queryName": "activeTasks",
    "parameters": { "limit": 15 }
  }
}
```

---

## 4. @widget_categories Dataset Schema

**Amaç:** Widget kategorilerini tanımlamak (Card, Chart, Table, Banner vb.).

```json
{
  "name": "@widget_categories",
  "description": "Widget kategorileri dataset'i",
  "forceSchema": true,
  "logging": "none",
  "publish_mode": "none",
  "fields": [
    {
      "fieldType": "text",
      "name": "name",
      "title": "Kategori Adı",
      "mandatory": true,
      "unique": true
    },
    {
      "fieldType": "text",
      "name": "description",
      "title": "Açıklama",
      "mandatory": false
    },
    {
      "fieldType": "text",
      "name": "icon",
      "title": "Icon",
      "mandatory": false
    },
    {
      "fieldType": "text",
      "name": "color",
      "title": "Renk",
      "mandatory": false
    },
    {
      "fieldType": "number",
      "name": "order",
      "title": "Sıralama",
      "mandatory": false,
      "defaultValue": 0
    },
    {
      "fieldType": "bool",
      "name": "isActive",
      "title": "Aktif",
      "mandatory": true,
      "defaultValue": true
    }
  ],
  "indexList": [
    { "name": "idx_name", "fields": { "name": 1 }, "unique": true },
    { "name": "idx_order", "fields": { "order": 1 } }
  ]
}
```

---

## 5. @widgets Dataset Schema

**Amaç:** Widget tanımlarını saklamak. Kategori referansı `@widget_categories` üzerinden.

### 5.1 Type Enum Validasyonu

`type` field’ı için validation / enum: `card` | `chart` | `table` | `banner`.  
(Expression validation veya field-level `pattern` / allowed values ile uygulanabilir.)

### 5.2 Schema

```json
{
  "name": "@widgets",
  "description": "Widget tanımları dataset'i",
  "forceSchema": true,
  "logging": "none",
  "publish_mode": "none",
  "fields": [
    {
      "fieldType": "text",
      "name": "name",
      "title": "Widget Adı",
      "mandatory": true,
      "unique": true
    },
    {
      "fieldType": "text",
      "name": "title",
      "title": "Widget Başlığı",
      "mandatory": true
    },
    {
      "fieldType": "text",
      "name": "description",
      "title": "Açıklama",
      "mandatory": false
    },
    {
      "fieldType": "relation",
      "name": "category",
      "title": "Kategori",
      "mandatory": true,
      "relationDataset": "@widget_categories"
    },
    {
      "fieldType": "text",
      "name": "type",
      "title": "Widget Türü",
      "mandatory": true
    },
    {
      "fieldType": "object",
      "name": "dataSource",
      "title": "Data Source Yapılandırması",
      "mandatory": true
    },
    {
      "fieldType": "object",
      "name": "layout",
      "title": "Layout Ayarları",
      "mandatory": false
    },
    {
      "fieldType": "object",
      "name": "style",
      "title": "Stil Ayarları",
      "mandatory": false
    },
    {
      "fieldType": "object",
      "name": "config",
      "title": "Widget Özel Yapılandırması",
      "mandatory": false
    },
    {
      "fieldType": "bool",
      "name": "isActive",
      "title": "Aktif",
      "mandatory": true,
      "defaultValue": true
    },
    {
      "fieldType": "number",
      "name": "order",
      "title": "Sıralama",
      "mandatory": false,
      "defaultValue": 0
    }
  ],
  "indexList": [
    { "name": "idx_name", "fields": { "name": 1 }, "unique": true },
    { "name": "idx_category", "fields": { "category": 1 } },
    { "name": "idx_type", "fields": { "type": 1 } },
    { "name": "idx_order", "fields": { "order": 1 } }
  ]
}
```

- **type:** `card` | `chart` | `table` | `banner` (enum).
- **dataSource:** Bölüm 3’teki yapı. `data` tipinde `getMethod` + ilgili blok (`default` | `query` | `aggregate` | `predefined`) kullanılır.
- **layout / style / config:** Mevcut spec’teki gibi opsiyonel; widget türüne göre genişletilebilir.

---

## 6. Oluşturma Sırası

1. `@widget_categories` dataset’ini oluştur.
2. Kategori kayıtlarını ekle (örn. Card, Chart, Table, Banner).
3. `@widgets` dataset’ini oluştur.
4. Widget’ları oluştururken `category` ile `@widget_categories` referansını ve `dataSource.getMethod` ile uygun DG GET işlemini kullan.

---

## 7. Referanslar

- DG Data API: `MngDataGateway.Api` — `DataController`
- Filter: `FilterParser` — `field:operator:value`, operatörler `eq`, `ne`, `gt`, `gte`, `lt`, `lte`, `in`, `nin`, `contains`, `startsWith`, `endsWith`
- Predefined queries: Dataset schema `queries` array, `ExecutePredefinedQueryAsync` / `POST .../queries/{queryName}`
