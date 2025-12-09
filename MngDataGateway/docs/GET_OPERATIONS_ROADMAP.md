# GET Operations Roadmap - MngDataGateway

**Date:** 9 Aralık 2025  
**Status:** Planning Phase  
**Phase:** 2 - GET Operations Enhancement

---

## 📋 Genel Mimari

### Core Decision: Aggregate Pipeline with Builder Pattern

**Tüm GET işlemleri:**
- MongoDB `find()` yerine `aggregate()` kullanacak
- Builder design pattern ile dinamik aggregate pipeline oluşturulacak
- Query parametrelerine göre pipeline adımları dinamik olarak eklenecek

**Avantajlar:**
- Tek pipeline'da filtreleme, sıralama, projection, lookup (relation expansion), grouping vb. tüm işlemler
- Daha esnek ve güçlü sorgulama
- Builder pattern ile temiz ve genişletilebilir kod

---

## 🎯 GET/POST Endpoints (5 Adet)

### 1. `GET /api/data/{datasetName}`
**Amaç:** Dataset'ten veri listesi getirme

**Query Parametreleri:**
- `skip` - Pagination (default: 0)
- `limit` - Page size (default: 50, max: 1000)
- `expand` - Relation expansion (default: true, false ile kapatılabilir)
- `deep` - Nested relation depth limit (appsettings'ten veya query param ile override)
- `showHistory` - History alanını göster (default: false)
- `showQuery` - Aggregate pipeline'ı döndür (default: false, debugging için)
- `showDataset` - Dataset schema'sını döndür (default: false, debugging için)
- `sort` - Sıralama (MongoDB tarzı: `?sort=field1,-field2`)
- `filter` - Filtreleme (RESTful tarzı: `?filter=field:operator:value`)
- `fields` - Field selection (basit include: `?fields=field1,field2,field3`)

### 2. `GET /api/data/{datasetName}/{dataId}`
**Amaç:** Tek kayıt getirme (__dataId'ye göre)

**Query Parametreleri:**
- `expand` - Relation expansion (default: true)
- `deep` - Nested relation depth limit
- `showHistory` - History alanını göster (default: false)
- `showQuery` - Aggregate pipeline'ı döndür (default: false, debugging için)
- `showDataset` - Dataset schema'sını döndür (default: false, debugging için)
- `sort` - Sıralama (MongoDB tarzı: `?sort=field1,-field2`)
- `fields` - Field selection (basit include: `?fields=field1,field2,field3`)

### 3. `POST /api/data/{datasetName}/query`
**Amaç:** Gelişmiş sorgulama endpoint'i (alternatif filter yaklaşımı)
- Daha karmaşık sorgular için (OR logic, nested queries, vb.)
- MongoDB native format ile match nesnesi gönderilir

**Request:**
- **Method:** POST
- **Body:** `{ "match": { ... } }` (MongoDB native format)
- **Query String:** `expand`, `deep`, `showHistory`, `showQuery`, `showDataset`, `sort`, `fields`, `skip`, `limit`

**Response:**
- **Normal:** JSON array (tek kayıt olsa bile array olarak döner)
- **showQuery=true:** `{ "query": [aggregate pipeline] }`

**Özellikler:**
- Match nesnesi doğrudan kullanılır (validation yok, schema kontrolü yok)
- MongoDB hata verirse, o hata döner
- Soft-delete filter yok (silinen kayıtlar `__deletedDatas` collection'ında)
- Otomatik eklenen filtre yok
- Tüm query params GET endpoint ile aynı (expand, deep, sort, fields, vb.)

**Örnek Request:**
```
POST /api/data/@tasks/query?expand=true&deep=2&sort=priority,-createdAt&fields=title,priority&skip=0&limit=50
Body: {
  "match": {
    "$or": [
      { "priority": { "$gte": 3 } },
      { "isCompleted": false }
    ],
    "title": { "$regex": "urgent", "$options": "i" }
  }
}
```

**Örnek Response (Normal):**
```json
[
  {
    "__dataId": "...",
    "title": "Urgent Task 1",
    "priority": 3
  },
  {
    "__dataId": "...",
    "title": "Urgent Task 2",
    "priority": 5
  }
]
```

**Örnek Response (showQuery=true):**
```json
{
  "query": [
    { "$match": { "$or": [...] } },
    { "$sort": { "priority": 1, "createdAt": -1 } },
    { "$skip": 0 },
    { "$limit": 50 },
    { "$lookup": {...} },
    { "$project": {...} }
  ]
}
```

### 4. `POST /api/data/{datasetName}/aggregate`
**Amaç:** Raw aggregate pipeline çalıştırma
- Kullanıcı body'den komple aggregate pipeline gönderir
- Pipeline olduğu gibi çalıştırılır (hiçbir değişiklik yapılmaz)
- Skip, match gibi hiçbir değer üretilmez

**Request:**
- **Method:** POST
- **Body:** `{ "pipeline": [...] }` (MongoDB aggregate pipeline array)
- **Query String:** Yok (hiçbir query param yok)

**Response:**
- **Normal:** JSON array (aggregate sonucu)
- **Hata:** MongoDB hata mesajı

**Özellikler:**
- Hiçbir validation yok
- Hiçbir query param yok
- Pipeline olduğu gibi çalıştırılır
- MongoDB hata verirse, o hata döner
- Response her zaman array (boş sonuç: `[]`)

**Örnek Request:**
```
POST /api/data/@tasks/aggregate
Body: {
  "pipeline": [
    { "$match": { "priority": { "$gte": 3 } } },
    { "$group": { "_id": "$priority", "count": { "$sum": 1 } } },
    { "$sort": { "_id": 1 } }
  ]
}
```

**Örnek Response (Normal):**
```json
[
  { "_id": 3, "count": 5 },
  { "_id": 4, "count": 3 },
  { "_id": 5, "count": 2 }
]
```

**Örnek Response (Hata):**
```json
{
  "error": "MongoDB error message",
  "code": "MongoDB error code"
}
```

**Not:** Güvenlik ve yetkilendirme konuları ileride planlanacak

---

## 📋 Predefined Queries Endpoint

### 5. `POST /api/data/{datasetName}/queries/{queryName}`
**Amaç:** Schema'da tanımlı predefined query'leri çalıştırma
- Dataset schema içinde tanımlı query'leri parametrelerle çalıştırır
- Parameters placeholder replacement yapılır
- Pipeline direkt çalıştırılır

**Request:**
- **Method:** POST
- **Body:** `{ "param1": value1, "param2": value2 }` (basit key-value, parametresiz query'lerde boş olabilir)
- **Query String:** Yok (hiçbir query param yok)

**Response:**
- **Normal:** JSON array (aggregate sonucu)
- **Hata:** MongoDB hata mesajı

**Özellikler:**
- Parameters: `:parameterName` formatında placeholder
- Type handling: Number için tırnak yok, string için tırnak var
- Validation: Schema'da tanımlı tüm parametreler zorunlu (eğer parameters array dolu ise)
- Parametresiz query: `parameters` array boş veya yoksa body boş olabilir
- Pipeline: Parameters replace edilip direkt çalıştırılır
- Response her zaman array (boş sonuç: `[]`)

**Örnek Request (Parametreli):**
```
POST /api/data/@tasks/queries/high_priority_tasks
Body: {
  "minPriority": 3,
  "maxPriority": 5
}
```

**Örnek Request (Parametresiz):**
```
POST /api/data/@tasks/queries/all_tasks
Body: {}  // veya hiç gönderilmeyebilir
```

**Örnek Response:**
```json
[
  {
    "__dataId": "...",
    "title": "Task 1",
    "priority": 5
  },
  {
    "__dataId": "...",
    "title": "Task 2",
    "priority": 4
  }
]
```

**Schema'da Query Tanımı:**
```json
{
  "name": "@tasks",
  "queries": [
    {
      "name": "high_priority_tasks",
      "description": "Get tasks by priority range",
      "pipeline": [
        { "$match": { "priority": { "$gte": ":minPriority", "$lte": ":maxPriority" } } },
        { "$sort": { "priority": -1 } }
      ],
      "parameters": ["minPriority", "maxPriority"]
    },
    {
      "name": "all_tasks",
      "description": "Get all tasks sorted by priority",
      "pipeline": [
        { "$match": {} },
        { "$sort": { "priority": -1 } }
      ],
      "parameters": []  // veya hiç olmayabilir
    }
  ]
}
```

**Parameter Replacement:**
- `:parameterName` → Body'den değer ile değiştirilir
- Number type: Tırnak olmadan (örn: `3`)
- String type: Tırnak ile (örn: `"urgent"`)
- Boolean type: Tırnak olmadan (örn: `true`)

**Validation:**
- Schema'da `parameters` array dolu ise → Tüm parametreler body'de olmalı
- Schema'da `parameters` array boş veya yoksa → Body boş olabilir
- Ek parametreler ignore edilir (veya uyarı log'u)

---

## 🔍 Debugging Query Parameters

### `showHistory`
**Strateji:**
- Default: `false` → `__history` alanı döndürülmez
- `?showHistory=true` → `__history` alanı döndürülür

**Avantajlar:**
- `__history` genellikle büyük olabilir
- Gereksiz network trafiğini azaltır
- İhtiyaç olduğunda açılabilir

**Builder pattern'da:**
- `$project` adımında handle edilir
- `showHistory=false` (default) → `__history: 0` (exclude)
- `showHistory=true` → `__history` include edilir

### `showQuery`
**Strateji:**
- Default: `false` → Normal response (sorgu sonucu veriler)
- `?showQuery=true` → Aggregate pipeline objesini döner (debugging/development için)

**Kullanım senaryoları:**
- Debugging: Pipeline'ı kontrol etmek
- Development: Pipeline yapısını görmek
- API dokümantasyonu: Örnek pipeline'lar
- Test: Pipeline doğrulama

**Response format:**
```json
{
  "success": true,
  "data": {
    "pipeline": [
      { "$match": { "__isDeleted": { "$ne": true } } },
      { "$lookup": { ... } },
      { "$project": { ... } }
    ]
  }
}
```

### `showDataset`
**Strateji:**
- Default: `false` → Normal response (aggregate pipeline sonucu)
- `?showDataset=true` → Dataset schema'sını döner (MongoDB'deki `@datasets` collection'ından)

**Kullanım senaryoları:**
- Debugging: Dataset schema'sını kontrol etmek
- Development: Schema yapısını görmek
- API dokümantasyonu: Dataset tanımlarını göstermek
- Test: Schema doğrulama

**Response format:**
```json
{
  "success": true,
  "data": {
    "__dataId": "...",
    "name": "@tasks",
    "description": "...",
    "fields": [ ... ],
    "indexList": [ ... ],
    ...
  }
}
```

### Kombinasyonlar
**Kombinasyonlar mümkün:**
- `?showQuery=true` → Sadece pipeline döner
- `?showDataset=true` → Sadece schema döner
- `?showQuery=true&showDataset=true` → Her ikisi de döner

**Response format (kombinasyon):**
```json
{
  "success": true,
  "data": {
    "pipeline": [ ... ],
    "dataset": {
      "__dataId": "...",
      "name": "@tasks",
      "fields": [ ... ],
      ...
    }
  }
}
```

---

## 📊 Sort (Sıralama)

### Format: MongoDB Tarzı

**Format:**
```
?sort=field1,-field2,field3
```

**Kurallar:**
- `-` ile başlayan → descending (-1)
- Diğerleri → ascending (1)
- Virgülle ayrılmış multiple fields

**Örnekler:**
```
GET /api/data/@tasks?sort=priority
→ Sadece priority ascending

GET /api/data/@tasks?sort=-createdAt,title
→ createdAt descending, title ascending

GET /api/data/@tasks?sort=priority,-createdAt,title&limit=10
→ Sort + pagination
```

### Pipeline Sıralaması

**Pipeline adımı:**
```javascript
{
  $sort: {
    "priority": 1,
    "createdAt": -1,
    "title": 1
  }
}
```

**Pipeline sırası:**
```
1. $match (soft-delete + filters)
2. $sort ← Buraya
3. $skip/$limit (pagination)
4. $lookup (relation expansion)
5. $project (final projection)
```

### Özellikler

**1. Field Validation**
- Schema'da tanımlı field'lar mı kontrol et
- Geçersiz field için hata döndür

**2. Index Kontrolü (Opsiyonel)**
- Sort için index var mı kontrol et
- Yoksa uyarı log'u (performance)

**3. Limit Kontrolü**
- Çok fazla field sort edilirse uyarı
- Max sort field limit (appsettings'ten)

**4. Default Sort**
- Schema'da default sort tanımlanabilir mi? (İleride)
- Yoksa `__dataId` veya `createdAt`? (İleride)

---

## 🔍 Filter (Filtreleme)

### Format: RESTful Tarzı

**Format:**
```
?filter=field:operator:value
```

**Multiple filters (AND logic):**
```
?filter=field1:operator1:value1&filter=field2:operator2:value2
```

**Örnekler:**
```
GET /api/data/@tasks?filter=priority:gte:3
→ priority >= 3

GET /api/data/@tasks?filter=priority:gte:3&filter=isCompleted:eq:false
→ priority >= 3 AND isCompleted == false

GET /api/data/@tasks?filter=title:like:urgent
→ title contains "urgent" (case-insensitive)

GET /api/data/@tasks?filter=priority:in:1,2,3
→ priority IN [1, 2, 3]
```

### Desteklenen Operatörler

| Operatör | MongoDB | Açıklama | Örnek |
|----------|---------|----------|-------|
| `eq` | `$eq` | Eşit | `?filter=priority:eq:3` |
| `ne` | `$ne` | Eşit değil | `?filter=priority:ne:3` |
| `gt` | `$gt` | Büyük | `?filter=priority:gt:3` |
| `gte` | `$gte` | Büyük eşit | `?filter=priority:gte:3` |
| `lt` | `$lt` | Küçük | `?filter=priority:lt:3` |
| `lte` | `$lte` | Küçük eşit | `?filter=priority:lte:3` |
| `in` | `$in` | İçinde | `?filter=priority:in:1,2,3` |
| `nin` | `$nin` | İçinde değil | `?filter=priority:nin:1,2,3` |
| `like` | `$regex` | İçerir (regex) | `?filter=title:like:urgent` |
| `exists` | `$exists` | Var mı | `?filter=dueDate:exists:true` |
| `null` | `$eq: null` | Null mu | `?filter=dueDate:null:true` |

### Özellikler

**1. Type Conversion**
- Schema'dan field type alınır
- String → Number, Boolean, DateTime dönüşümü
- Örnek: `?filter=priority:eq:3` → number'a çevrilir

**2. Field Validation**
- Schema'da tanımlı field'lar mı kontrol edilir
- Geçersiz field için hata döndürülür

**3. Multiple Filters (AND Logic)**
- Tüm filter'lar AND ile birleştirilir
- OR logic için ayrı format gerekir (ileride `GET /query` endpoint'inde)

**4. Array Values**
- `in` ve `nin` için virgülle ayrılmış değerler
- Örnek: `?filter=priority:in:1,2,3`

**5. Date Handling**
- ISO 8601 format: `?filter=dueDate:gte:2025-01-01T00:00:00Z`
- String'den DateTime'e dönüşüm

### Pipeline Sıralaması

**Pipeline adımı:**
```javascript
{
  $match: {
    "__isDeleted": { "$ne": true },  // Soft-delete filter (her zaman)
    "priority": { "$gte": 3 },       // User filter 1
    "isCompleted": false              // User filter 2
  }
}
```

**Pipeline sırası:**
```
1. $match (soft-delete + user filters) ← Buraya
2. $sort
3. $skip/$limit (pagination)
4. $lookup (relation expansion)
5. $project (final projection)
```

### Güvenlik

**1. Injection Prevention**
- Operatör whitelist (sadece izin verilen operatörler)
- Field name validation (schema kontrolü)
- Value sanitization

**2. Limit Kontrolü**
- Max filter sayısı (appsettings'ten)
- Çok fazla filter için uyarı

### Notlar

- **Basit Filter:** Bu endpoint'te AND logic ile basit filter'lar
- **Gelişmiş Query:** İleride `GET /api/data/{datasetName}/query` endpoint'i eklenecek
  - OR logic
  - Nested queries
  - JSON format ile query gönderilebilir
  - Daha karmaşık sorgular için

---

## 📋 Fields (Field Selection)

### Format: Basit Include

**Format:**
```
?fields=field1,field2,field3
```

**Kurallar:**
- Sadece belirtilen field'lar döner
- System field'lar her zaman dahil: `__dataId` (zorunlu)
- `__history` → `showHistory` parametresine göre
- Relation field'lar → `expand` parametresine göre
- Belirtilmeyen field'lar exclude edilir

**Örnekler:**
```
GET /api/data/@tasks?fields=title,priority
→ Sadece title, priority, __dataId döner

GET /api/data/@tasks?fields=title,priority,task_state
→ title, priority, task_state (expand edilmiş), __dataId döner

GET /api/data/@tasks?fields=title,priority&showHistory=true
→ title, priority, __dataId, __history döner
```

### Özellikler

**1. System Fields**
- `__dataId` → Her zaman dahil (zorunlu)
- `__history` → `showHistory` parametresine göre
- `__isDeleted`, `__createInfo` vb. → Normalde dahil değil (gerekirse eklenebilir)

**2. Relation Fields**
- `expand=true` ise relation field'lar expand edilir
- `expand=false` ise sadece `__dataId` döner
- Nested relation'larda tüm nested field'lar dahil (deep parametresine göre)

**3. Field Validation**
- Schema'da tanımlı field'lar mı kontrol edilir
- Geçersiz field için ignore + warning log (hata vermez)

**4. Empty Fields / Default Behavior**
- `?fields=` parametresi yoksa → Tüm field'lar döner (default behavior)
- `__history` → Sadece `showHistory=true` ise gelir (fields parametresinden bağımsız)
- Diğer system field'lar → Her zaman gelir (`__dataId`, `__isDeleted`, `__createInfo`, vb.)
- `?fields=` boş string ise → Tüm field'lar döner (default behavior ile aynı)

**Örnekler:**
```
GET /api/data/@tasks
→ Tüm field'lar gelir (__history hariç)

GET /api/data/@tasks?showHistory=true
→ Tüm field'lar gelir (__history dahil)

GET /api/data/@tasks?fields=title,priority
→ Sadece title, priority, __dataId gelir (__history hariç)

GET /api/data/@tasks?fields=title,priority&showHistory=true
→ title, priority, __dataId, __history gelir
```

### Pipeline Sıralaması

**Pipeline adımı:**
```javascript
{
  $project: {
    "__dataId": 1,        // Her zaman dahil
    "title": 1,           // User field
    "priority": 1,        // User field
    "task_state": 1,      // Relation field (expand edilmiş)
    "__history": 1,       // showHistory=true ise
    "_id": 0              // MongoDB _id exclude
  }
}
```

**Pipeline sırası:**
```
1. $match (soft-delete + filters)
2. $sort
3. $skip/$limit (pagination)
4. $lookup (relation expansion)
5. $project (final projection) ← Buraya
```

### Özel Durumlar

**1. Nested Fields (İleride)**
- Şimdilik sadece top-level fields
- İleride: `?fields=task_state.name,task_state.color` gibi nested seçim

**2. Wildcard (İleride)**
- `?fields=*` → Tüm field'lar
- Şimdilik gerekli değil

**3. Relation İçindeki Fields**
- Şimdilik relation expand edildiğinde tüm field'lar dahil
- İleride: `?fields=task_state{name,color}` gibi nested projection

---

## 🔗 Relation Expansion

### Strateji

**Default Behavior:**
- `expand=true` (default) → Tüm relation field'lar otomatik expand edilir
- `?expand=false` → Hiçbir relation expand edilmez, sadece `__dataId` değerleri döner

**Örnek:**
```
GET /api/data/@tasks
→ task_state, task_priority, task_type hepsi expand edilir

GET /api/data/@tasks?expand=false
→ Sadece __dataId değerleri: { "task_state": "abc-123", "task_priority": "def-456" }
```

### Nested Relations (Depth Limit)

**Configuration:**
- `appsettings.json`: `"RelationExpansionMaxDepth": 2` (default değer)
- Query parametresi: `?deep=2` ile override edilebilir
- Öncelik: Query parametresi varsa onu kullan, yoksa appsettings'ten al

**Örnek:**
```
appsettings.json:
{
  "RelationExpansionMaxDepth": 2
}

GET /api/data/@tasks
→ Max depth: 2 (appsettings'ten)

GET /api/data/@tasks?deep=3
→ Max depth: 3 (query parametresinden)

GET /api/data/@tasks?deep=1
→ Max depth: 1 (query parametresinden)
```

**Circular Reference Handling:**
- Recursive expansion yaparken depth counter tut
- Her `$lookup` sonrası depth++
- Depth >= limit ise durdur
- Aynı dataset'e tekrar bakılıyorsa circular detection ile durdur

### Array Relations

**Strateji:**
- Field tanımında `isArray: true` ve `fieldType: "relation"` ise
- `$lookup` içinde pipeline kullanılacak
- Array içindeki tüm `__dataId` değerleri batch olarak expand edilecek

**Yaklaşım:**
```javascript
{
  $lookup: {
    from: "@users",
    let: { userIds: "$assignedUsers" },  // Array field'ı pipeline'a geçir
    pipeline: [
      {
        $match: {
          $expr: {
            $in: ["$__dataId", "$$userIds"]  // Array içindeki tüm ID'leri match et
          }
        }
      },
      {
        $match: { __isDeleted: { $ne: true } }  // Soft-delete filter
      },
      {
        $project: { /* sadece gerekli field'lar */ }
      }
    ],
    as: "assignedUsers"  // Sonuç array olarak gelir
  }
}
```

**Avantajlar:**
- Tek lookup ile tüm array elemanları bulunur (N+1 problemi yok)
- Pipeline içinde filtreleme yapılabilir (örn: soft-deleted olanları exclude et)
- Pipeline içinde projection yapılabilir (sadece gerekli field'lar)

### Missing Relation Handling

**Strateji:**
- Relation field `null` ise → `null` döner
- Relation field geçersiz `__dataId` içeriyorsa → `null` döner
- `$lookup` sonucu boş ise → `null` döner (veya boş array, eğer `isArray: true` ise)

**Örnek:**
```json
// MongoDB'de:
{ "task_state": "invalid-id-123" }

// $lookup sonucu: boş (geçersiz ID)

// Response:
{ "task_state": null }

// Array relation için:
{ "assignedUsers": ["valid-id", "invalid-id"] }

// $lookup sonucu: sadece valid-id bulundu

// Response:
{ "assignedUsers": [{ "__dataId": "valid-id", ... }] }  // invalid-id ignore edilir
```

---

## ⚡ Performance Optimizasyonları

### 1. Index Kullanımı
- `$lookup` yapılan collection'larda `__dataId` üzerinde index olmalı
- Schema'da relation tanımlı field'lar için index kontrolü yapılabilir
- İlk data insert'te relation field'lar için index oluşturulabilir

### 2. Pipeline Sıralaması (ÖNEMLİ)
- `$match` ve `$filter` adımlarını mümkün olduğunca erken yap
- `$lookup`'ları en sona bırak (mümkünse)
- Önce filtrele, sonra expand et

**Örnek pipeline sırası:**
```
1. $match (soft-delete filter)
2. $match (user filters)
3. $sort (eğer varsa)
4. $skip/$limit (pagination)
5. $lookup (relation expansion) ← En sona
6. $project (final projection)
```

### 3. Projection (Field Seçimi)
- `$lookup` pipeline'ında sadece gerekli field'ları seç
- `__history`, `__isDeleted` gibi büyük field'ları exclude et
- Nested expansion'larda da projection uygula

### 4. Batch Lookup (Zaten Planlanmış)
- Array relation'lar için `$in` ile batch lookup
- Tek lookup ile tüm ID'leri bul (N+1 problemi yok)

### 5. Cache Mekanizması (Opsiyonel - İleride)
- Sık kullanılan relation'lar için memory cache (örn: 5 dakika TTL)
- Özellikle lookup-heavy dataset'ler için faydalı
- İlk implementasyonda gerekli değil, sonra eklenebilir

### 6. Limit Kontrolü
- Çok sayıda relation varsa (örn: 10+) uyarı log'u
- Max relation sayısı limit'i (appsettings'ten)
- Deep expansion için depth limit (zaten var)

### 7. Pipeline Optimizasyonu
- Gereksiz `$project` adımlarını birleştir
- `$addFields` yerine `$project` kullan (daha performanslı)
- `$unwind` kullanımını minimize et (sadece gerektiğinde)

### 8. Soft-Delete Filtering
- `$lookup` pipeline'ında soft-deleted kayıtları exclude et
- `$match: { __isDeleted: { $ne: true } }` ekle

### 9. Parallel Lookup (İleri Seviye - İleride)
- Birden fazla relation varsa, mümkünse parallel lookup
- MongoDB 4.2+ `$lookup` içinde `$unionWith` kullanılabilir
- İlk implementasyonda gerekli değil

### 10. Monitoring & Logging (İleride)
- Yavaş query'leri log'la (örn: >500ms)
- Pipeline execution time'ı track et
- Hangi relation'ların en çok kullanıldığını izle

---

## 📊 Öncelik Sırası

### İlk Implementasyonda:
1. ✅ Pipeline sıralaması (match → lookup)
2. ✅ Projection (sadece gerekli field'lar)
3. ✅ Soft-delete filtering (lookup pipeline'ında)
4. ✅ Index kontrolü (relation field'lar için)

### Sonra Eklenebilir:
5. ⏳ Cache mekanizması
6. ⏳ Limit kontrolü
7. ⏳ Monitoring

---

## 🏗️ Builder Pattern Yapısı

### AggregatePipelineBuilder

**Sorumluluklar:**
- Pipeline adımlarını dinamik olarak oluştur
- Query parametrelerine göre adımları ekle/çıkar
- Relation expansion logic'ini yönet
- Depth limit ve circular reference kontrolü

**Adımlar:**
1. `AddMatchStep()` - Soft-delete ve user filters
2. `AddSortStep()` - Sıralama (ileride)
3. `AddPaginationStep()` - Skip/Limit
4. `AddRelationExpansionSteps()` - Tüm relation'lar için $lookup
5. `AddProjectStep()` - Final projection

---

## 📝 Notlar

### Önemli Kararlar
1. **Default Expansion:** Tüm relation'lar varsayılan olarak expand edilir
2. **Depth Limit:** Appsettings + query param override
3. **Array Relations:** $lookup pipeline kullanılacak
4. **Missing Relations:** null dönecek (hata vermeyecek)
5. **Pipeline Order:** Match → Sort → Pagination → Lookup → Project

### Dikkat Edilmesi Gerekenler
- **Performance:** Pipeline sıralaması kritik
- **Memory:** Deep expansion için memory limit
- **Circular Reference:** Detection ve prevention
- **Index:** Relation field'lar için index zorunlu

---

## 🔗 İlgili Dosyalar

- `PHASE_2_PLANNING.md` - Phase 2 genel planı
- `STATUS.md` - Mevcut durum
- `DATASET_SCHEMA_SUMMARY.md` - Schema yapısı

---

---

## 🔮 Phase 3 - Gelecek Özellikler (İleride Planlanacak)

**Status:** Not Alındı - Detaylı planlama yapılmadı

### Özellikler

**1. persons/personGroups Field Type Implementation**
- `persons` field type → MngKeeper API entegrasyonu
- `personGroups` field type → MngKeeper API entegrasyonu
- Validation: User/Group exists check
- Caching: User/Group data cache (TTL: 5 minutes)
- Expansion: Relation expansion gibi çalışabilir

**2. Dataset Yetkilendirme ve Grup Kontrolü**
- Dataset'ler için yetkilendirilmiş gruplar tanımlama
- Yetki kontrolü (read, write, delete, vb.)
- Grup bazlı erişim kontrolü
- JWT token'dan grup bilgisi alınması

**3. Güvenlik Güncellemeleri**
- Query injection prevention
- Rate limiting
- Field-level permissions
- Dataset-level permissions
- Operation-level permissions (create, read, update, delete)
- Audit logging (kim ne zaman ne yaptı)

**4. Diğer Potansiyel Özellikler**
- Full-text search
- Export functionality (CSV, Excel)
- Batch operations
- Webhook notifications
- Real-time updates (WebSocket/SignalR)
- Advanced analytics ve reporting

---

**Hazırlayan:** AI Assistant  
**Date:** 9 Aralık 2025  
**Status:** Planning Phase (Phase 2 GET Operations)

