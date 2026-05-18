# Monitoring Asset Datasets – DG Şemaları ve Örnekler

Bu doküman, MonitraNG Monitoring planındaki asset modeli için **MngDataGateway (DG)** dataset şemalarını ve örnek veri dokümanlarını tanımlar. Planlama özeti için [MonitraNG Monitoring planlama](monitrang_monitoring_planlama.md) dokümanı ve Asset Mimarisi bölümüne bakınız.

---

## 1. MngDataGateway Dataset Yapısı (Özet)

DG dataset’leri `@datasets` içinde **schema** olarak tutulur. Veri, ilgili **database**’te schema `name` ile aynı addaki **collection**’a yazılır.

### 1.1 Schema alanları

| Alan | Açıklama |
|------|----------|
| `name` | Dataset adı (unique); aynı zamanda collection adı. Örn. `mon_assets`. |
| `description` | Opsiyonel açıklama. |
| `category` | Opsiyonel; `@dataset_categories` referansı (__dataId). |
| `forceSchema` | `true`: sadece tanımlı alanlar kabul. `false`: ek alanlara izin. |
| `logging` | `"none"` \| `"self"` \| `"common"`. |
| `publish_mode` | `"none"` \| `"basic"` \| `"full"` (RabbitMQ). |
| `fields` | Alan tanımları (aşağıda). |
| `indexList` | Opsiyonel index tanımları. |
| `validations` | Opsiyonel validasyon kuralları. |
| `permissions` | Opsiyonel okuma/yazma izinleri. |

### 1.2 Field tipleri (fieldType)

| fieldType | Açıklama | Değer |
|-----------|----------|--------|
| `text` | Metin | string |
| `number` | Sayı | number |
| `bool` | Boolean | boolean |
| `datetime` | Tarih/saat | ISO 8601 string |
| `object` | İç içe obje / serbest yapı | object |
| `relation` | Başka dataset’e referans | **__dataId** (string) |
| `persons` | Kişi referansı | string \| array |
| `personGroups` | Grup referansı | string \| array |
| `incremental` | Otomatik artan (örn. format) | schema’da tanımlı |
| `file` | Dosya referansı | object |

- **relation:** `relationDataset` ile hedef dataset adı zorunlu. Saklanan değer hedef dokümanın `__dataId`’si (string).
- **object:** DG şema seviyesinde iç yapıyı zorlamaz; serbest JSON obje kabul edilir.
- **isArray:** `true` ise alan dizi (örn. `object` + `isArray: true` → obje dizisi).

### 1.3 Dataset konumu (multi-tenant)

Mimari **multi-tenant**’tır; her tenant’ın kendi veritabanı vardır. Veritabanı adı **`mng_{domain_name}`** formatındadır (örn. `mng_acme`, `mng_proline`). DG, JWT `domain` claim’ine göre veritabanını seçer ve bu yapıyı kendi içinde yönetir.

`mon_asset_type_family`, `mon_asset_types`, `mon_items` ve `mon_assets` dataset’lerinin **tümü** ilgili tenant’ın veritabanı **`mng_{domain_name}`** içinde bulunur. Yani family, type ve asset verileri domain bazında izole edilir. Engine ve Agent ile ilgili dataset'ler (`mon_engines`, `mon_collection_periods`, `mon_schedules`, `mon_agents`) için [Monitoring Engine Architecture](MONITORING_ENGINE_ARCHITECTURE.md) ve [Monitoring Agent Architecture](MONITORING_AGENT_ARCHITECTURE.md) dokümanlarına bakınız.

---

## 2. mon_asset_type_family

**Amaç:** Asset tipinin hangi ailede olduğu (örn. Operating Systems, Network Equipment).

**Database:** `mng_{domain_name}` (tenant veritabanı).

### 2.1 Şema (DG CreateDataset uyumlu)

```json
{
  "Name": "mon_asset_type_family",
  "Description": "Monitoring – Asset type family (ör. Operating Systems, Network).",
  "ForceSchema": true,
  "Logging": "none",
  "PublishMode": "none",
  "Fields": [
    {
      "fieldType": "text",
      "name": "name",
      "title": "Aile adı",
      "mandatory": true,
      "unique": true,
      "isArray": false
    },
    {
      "fieldType": "text",
      "name": "code",
      "title": "Kod (slug)",
      "mandatory": false,
      "unique": true,
      "isArray": false
    },
    {
      "fieldType": "text",
      "name": "description",
      "title": "Açıklama",
      "mandatory": false,
      "isArray": false
    }
  ],
  "IndexList": [
    { "name": "idx_name", "fields": { "name": 1 }, "unique": true },
    { "name": "idx_code", "fields": { "code": 1 }, "unique": true }
  ]
}
```

### 2.2 Örnek veri dokümanları

**Örnek 1: Operating Systems**

```json
{
  "__dataId": "a1b2c3d4-e5f6-7890-abcd-111111111111",
  "name": "Operating Systems",
  "code": "operating_systems",
  "description": "İşletim sistemi tabanlı host’lar (Linux, Windows, vb.)."
}
```

**Örnek 2: Network Equipment**

```json
{
  "__dataId": "a1b2c3d4-e5f6-7890-abcd-222222222222",
  "name": "Network Equipment",
  "code": "network_equipment",
  "description": "Ağ cihazları (switch, router, SNMP destekli cihazlar)."
}
```

---

## 3. mon_asset_types

**Amaç:** Asset tipi (family’ye bağlı). `collection_method` ile nasıl veri toplanacağı, `collectibles` ile ne toplanacağı tanımlanır.

**Database:** `mng_{domain_name}` (tenant veritabanı).

### 3.1 Şema (DG CreateDataset uyumlu)

```json
{
  "Name": "mon_asset_types",
  "Description": "Monitoring – Asset type (Linux, Windows, SNMP Generic, vb.).",
  "ForceSchema": true,
  "Logging": "none",
  "PublishMode": "none",
  "Fields": [
    {
      "fieldType": "text",
      "name": "name",
      "title": "Tip adı",
      "mandatory": true,
      "unique": false,
      "isArray": false
    },
    {
      "fieldType": "relation",
      "name": "family",
      "title": "Aile",
      "mandatory": true,
      "unique": false,
      "isArray": false,
      "relationDataset": "mon_asset_type_family"
    },
    {
      "fieldType": "text",
      "name": "collection_method",
      "title": "Toplama metodu",
      "mandatory": true,
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
      "fieldType": "object",
      "name": "collectibles",
      "title": "Toplanacaklar",
      "mandatory": true,
      "isArray": true
    }
  ],
  "IndexList": [
    { "name": "idx_family_name", "fields": { "family": 1, "name": 1 }, "unique": true }
  ]
}
```

`collection_method` değerleri örn.: `WMI`, `SSH`, `SNMP`, `SNMP_V3`, `REST`, `OPC_UA`.

`collectibles` her elemanı örnek yapı (DG object olduğu için zorunlu değil; uygulama tarafında anlamlı):

- `code` (string): Tekil kod.
- `name` (string): Görünen ad.
- `data_type` (string): `number` \| `string` \| `object`.
- `metric_key` (string, opsiyonel): OS metrikleri için (örn. `cpu`, `memory`).
- `oid` (string, opsiyonel): SNMP OID.
- `path` (string, opsiyonel): REST path vb.
- `overridable_params` (string[], opsiyonel): Asset’te override edilebilecek alanlar (örn. `["oid","interval"]`).

### 3.2 Örnek veri dokümanları

**Örnek 1: Linux (SSH)**

```json
{
  "__dataId": "b2c3d4e5-f6a7-8901-bcde-333333333333",
  "name": "Linux",
  "family": "a1b2c3d4-e5f6-7890-abcd-111111111111",
  "collection_method": "SSH",
  "description": "SSH ile erişilen Linux host’lar.",
  "collectibles": [
    {
      "code": "cpu_usage",
      "name": "CPU %",
      "data_type": "number",
      "metric_key": "cpu",
      "overridable_params": ["interval"]
    },
    {
      "code": "memory_used",
      "name": "Memory Used (KB)",
      "data_type": "number",
      "metric_key": "memory",
      "overridable_params": ["interval"]
    }
  ]
}
```

**Örnek 2: SNMP Generic**

```json
{
  "__dataId": "b2c3d4e5-f6a7-8901-bcde-444444444444",
  "name": "SNMP Generic",
  "family": "a1b2c3d4-e5f6-7890-abcd-222222222222",
  "collection_method": "SNMP",
  "description": "SNMP v2c ile OID tabanlı toplama.",
  "collectibles": [
    {
      "code": "sysdescr",
      "name": "sysDescr",
      "data_type": "string",
      "oid": "1.3.6.1.2.1.1.1.0",
      "overridable_params": ["oid", "interval"]
    }
  ]
}
```

---

## 4. mon_items (Organizasyon ağacı)

**Amaç:** İç içe organizasyon ağacı. Her Item alt Item'lar ve/veya Asset'ler içerebilir. Lokasyon opsiyonel; tanımlı değilse en yakın lokasyon tanımlı parent'tan miras alınır (effective location, okuma sırasında hesaplanır). `kind` ve `tags` arama/raporlama için.

**Database:** `mng_{domain_name}` (tenant veritabanı).

### 4.1 Şema (DG CreateDataset uyumlu)

```json
{
  "Name": "mon_items",
  "Description": "Monitoring – Organizasyon ağacı (Item hiyerarşisi).",
  "ForceSchema": true,
  "Logging": "none",
  "PublishMode": "none",
  "Fields": [
    {
      "fieldType": "text",
      "name": "name",
      "title": "Item adı",
      "mandatory": true,
      "unique": false,
      "isArray": false
    },
    {
      "fieldType": "relation",
      "name": "parentId",
      "title": "Üst item",
      "mandatory": false,
      "unique": false,
      "isArray": false,
      "relationDataset": "mon_items"
    },
    {
      "fieldType": "text",
      "name": "description",
      "title": "Açıklama",
      "mandatory": false,
      "isArray": false
    },
    {
      "fieldType": "object",
      "name": "location",
      "title": "Konum (lat, lon)",
      "mandatory": false,
      "isArray": false
    },
    {
      "fieldType": "text",
      "name": "kind",
      "title": "Tür (city, building, room, cabinet, server, pdu, vb.)",
      "mandatory": false,
      "isArray": false
    },
    {
      "fieldType": "object",
      "name": "tags",
      "title": "Etiketler (key-value)",
      "mandatory": false,
      "isArray": true
    }
  ],
  "IndexList": [
    { "name": "idx_parentId", "fields": { "parentId": 1 }, "unique": false },
    { "name": "idx_kind", "fields": { "kind": 1 }, "unique": false }
  ]
}
```

- **location:** Opsiyonel `{ "lat": number, "lon": number }`. Yoksa effective location = en yakın parent'tan cascade.
- **kind:** Serbest metin; UI'da önerilen değerler: `city`, `region`, `building`, `room`, `cabinet`, `server`, `pdu`, `switch`. Arama/filtre için tutarlılık sağlar.
- **tags:** `[{ "key": "...", "value": "..." }]`. Önerilen key'ler: Item için `region`, `site`; Asset için `env`, `role`, `collector`.

### 4.2 Örnek veri dokümanları

**Örnek 1: Kök (Istanbul)**

```json
{
  "__dataId": "d4e5f6a7-b8c9-0123-defa-111111111111",
  "name": "Istanbul",
  "description": "Istanbul bölge ana item.",
  "location": { "lat": 41.0082, "lon": 28.9784 },
  "kind": "city",
  "tags": [{ "key": "region", "value": "marmara" }]
}
```

**Örnek 2: Hiyerarşi (Çamlıca Bölge → Sistem odası → Kabin → Sunucu → PDU)**

```json
{
  "__dataId": "d4e5f6a7-b8c9-0123-defa-333333333333",
  "name": "Çamlıca Bölge",
  "parentId": "d4e5f6a7-b8c9-0123-defa-111111111111",
  "description": "Çamlıca bölge item.",
  "kind": "region"
}
```

```json
{
  "__dataId": "d4e5f6a7-b8c9-0123-defa-222222222222",
  "name": "1. Sistem odası",
  "parentId": "d4e5f6a7-b8c9-0123-defa-333333333333",
  "description": "Çamlıca bölge ana sistem odası.",
  "kind": "room"
}
```

```json
{
  "__dataId": "d4e5f6a7-b8c9-0123-defa-444444444444",
  "name": "2 nolu kabin",
  "parentId": "d4e5f6a7-b8c9-0123-defa-222222222222",
  "description": "2 numaralı kabin.",
  "kind": "cabinet"
}
```

```json
{
  "__dataId": "d4e5f6a7-b8c9-0123-defa-555555555555",
  "name": "sunucu1",
  "parentId": "d4e5f6a7-b8c9-0123-defa-444444444444",
  "description": "Web sunucusu.",
  "kind": "server",
  "tags": [{ "key": "role", "value": "web" }]
}
```

```json
{
  "__dataId": "d4e5f6a7-b8c9-0123-defa-666666666666",
  "name": "PDU-01",
  "parentId": "d4e5f6a7-b8c9-0123-defa-444444444444",
  "description": "2 nolu kabin PDU.",
  "kind": "pdu"
}
```

---

## 5. mon_assets

**Amaç:** İzlenen asset (OS, DB, sensör, SNMP cihazı vb.). Her Asset **bir Item içinde** olmalı (`itemId` zorunlu). Lokasyon Asset'te yok; Item'ın effective location kullanılır. Bağlantı `connection_info`; protocol type'tan gelir.

**Database:** `mng_{domain_name}` (tenant veritabanı).

### 5.1 Şema (DG CreateDataset uyumlu)

```json
{
  "Name": "mon_assets",
  "Description": "Monitoring – Asset (izlenen varlık) kayıtları. Her asset bir Item içindedir.",
  "ForceSchema": true,
  "Logging": "none",
  "PublishMode": "none",
  "Fields": [
    {
      "fieldType": "text",
      "name": "name",
      "title": "Asset adı",
      "mandatory": true,
      "unique": false,
      "isArray": false
    },
    {
      "fieldType": "relation",
      "name": "type",
      "title": "Asset tipi",
      "mandatory": true,
      "unique": false,
      "isArray": false,
      "relationDataset": "mon_asset_types"
    },
    {
      "fieldType": "relation",
      "name": "itemId",
      "title": "İçinde bulunduğu Item",
      "mandatory": true,
      "unique": false,
      "isArray": false,
      "relationDataset": "mon_items"
    },
    {
      "fieldType": "text",
      "name": "description",
      "title": "Açıklama",
      "mandatory": false,
      "isArray": false
    },
    {
      "fieldType": "object",
      "name": "tags",
      "title": "Etiketler (key-value)",
      "mandatory": false,
      "isArray": true
    },
    {
      "fieldType": "text",
      "name": "status",
      "title": "Durum",
      "mandatory": true,
      "isArray": false
    },
    {
      "fieldType": "object",
      "name": "connection_info",
      "title": "Bağlantı (endpoint + auth)",
      "mandatory": true,
      "isArray": false
    },
    {
      "fieldType": "object",
      "name": "collectible_config",
      "title": "Collectible override (seçim / params)",
      "mandatory": false,
      "isArray": true
    }
  ],
  "IndexList": [
    { "name": "idx_itemId", "fields": { "itemId": 1 }, "unique": false },
    { "name": "idx_itemId_name", "fields": { "itemId": 1, "name": 1 }, "unique": true },
    { "name": "idx_type", "fields": { "type": 1 }, "unique": false },
    { "name": "idx_status", "fields": { "status": 1 }, "unique": false }
  ]
}
```

- **itemId:** Zorunlu. Asset hangi Item içinde. `idx_itemId_name` ile aynı Item altında aynı isimde iki asset engellenir.
- **tags:** `[{ "key": "...", "value": "..." }]`.
- **status:** `active` \| `maintenance` \| `decommissioned`. Engine sadece `status=active` olan asset'leri toplamalı; `decommissioned` hariç tutulmalı.
- **connection_info:** `{ "endpoint": { "host": "...", "port": 22 }, "auth": { ... } }`. Protocol yok; type’ın `collection_method`’undan gelir. Hassas alanlar Reactor’da şifrelenip DG’ye öyle yazılır.
- **collectible_config:** Öneri B. `[{ "code": "...", "enabled": true|false, "params": { ... } }]`. Sadece override edilenler listelenir.

### 5.2 Örnek veri dokümanları

**Örnek 1: Sunucu Item'ı altında OS asset**

```json
{
  "__dataId": "c3d4e5f6-a7b8-9012-cdef-555555555555",
  "name": "OS metrikleri",
  "itemId": "d4e5f6a7-b8c9-0123-defa-555555555555",
  "type": "b2c3d4e5-f6a7-8901-bcde-333333333333",
  "description": "Sunucu1 işletim sistemi metrikleri.",
  "tags": [{ "key": "collector", "value": "ssh" }],
  "status": "active",
  "connection_info": {
    "endpoint": { "host": "192.168.1.10", "port": 22 },
    "auth": {
      "username": "monitor",
      "password": "<Reactor tarafında şifrelenmiş, DG’ye böyle yazılır>"
    }
  }
}
```

**Örnek 2: PDU Item altında SNMP asset (override ile)**

```json
{
  "__dataId": "c3d4e5f6-a7b8-9012-cdef-666666666666",
  "name": "PDU sensör",
  "itemId": "d4e5f6a7-b8c9-0123-defa-666666666666",
  "type": "b2c3d4e5-f6a7-8901-bcde-444444444444",
  "description": "PDU SNMP sensör.",
  "tags": [{ "key": "sensor", "value": "power" }],
  "status": "active",
  "connection_info": {
    "endpoint": { "host": "10.0.1.1", "port": 161 },
    "auth": { "community": "<şifrelenmiş>" }
  },
  "collectible_config": [
    {
      "code": "sysdescr",
      "enabled": true,
      "params": { "oid": "1.3.6.1.2.1.1.1.0" }
    }
  ]
}
```

---

## 5. Kısa referans

| Dataset | DB | İlişkiler |
|--------|----|-----------|
| `mon_asset_type_family` | mng_{domain_name} | — |
| `mon_asset_types` | mng_{domain_name} | `family` → mon_asset_type_family |
| `mon_items` | mng_{domain_name} | `parentId` → mon_items |
| `mon_assets` | mng_{domain_name} | `type` → mon_asset_types, `itemId` → mon_items |

Tüm referanslar **__dataId** (GUID string). DG `relation` alanında bu değer saklanır; `expand` ile ilişkili doküman açılabilir.

Bu şemalar ve örnekler, planlama dokümanındaki Asset Mimarisi ile uyumludur. Implementasyon sırasında DG API (`POST /api/datasets`, `POST /api/data/...`) ve Reactor asset CRUD akışı bu yapıya göre kullanılacaktır.
