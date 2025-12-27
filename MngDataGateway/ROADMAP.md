# MngDataGateway - Geliştirme Yol Haritası

**Son Güncelleme:** 26 Aralık 2025  
**Versiyon:** 1.0.0  
**Durum:** 🚀 Aktif Geliştirme

**Not:** Sonraki session için yapılacaklar için `docs/NEXT_SESSION_ROADMAP.md` dosyasına bakın.

---

## 📋 İÇİNDEKİLER

1. [Tamamlanan Özellikler](#tamamlanan-özellikler)
2. [Devam Eden İşler](#devam-eden-işler)
3. [Gelecek Planlar](#gelecek-planlar)
4. [Teknik Detaylar](#teknik-detaylar)

---

## ✅ TAMAMLANAN ÖZELLİKLER

### 1. Dataset Schema Management - ✅ TAMAMLANDI

**Components:**
- ✅ `DatasetSchema` entity
- ✅ `DatasetService` - CRUD operations
- ✅ `DatasetController` - REST API endpoints
- ✅ Field definitions (text, number, bool, datetime, object, relation, incremental)
- ✅ Index definitions (unique, non-unique, composite)
- ✅ Predefined queries (MongoDB aggregation pipeline)
- ✅ Dataset categories

**API Endpoints:**
- ✅ `GET /api/datasets` - List datasets
- ✅ `GET /api/datasets/{name}` - Get dataset schema
- ✅ `POST /api/datasets` - Create dataset
- ✅ `PUT /api/datasets/{name}` - Update dataset
- ✅ `DELETE /api/datasets/{name}` - Delete dataset

**Tarih:** 30 Aralık 2025

---

### 2. Data CRUD Operations - ✅ TAMAMLANDI

**Components:**
- ✅ `DataService` - Data operations
- ✅ `DataController` - REST API endpoints
- ✅ Field validation
- ✅ Incremental field generation
- ✅ Transaction support
- ✅ Soft delete / Restore operations
- ✅ Bulk insert operations

**API Endpoints:**
- ✅ `GET /api/data/{dataset}` - List data (pagination, sorting, filtering)
- ✅ `GET /api/data/{dataset}/{id}` - Get single data
- ✅ `POST /api/data/{dataset}` - Create data
- ✅ `PUT /api/data/{dataset}/{id}` - Update data
- ✅ `DELETE /api/data/{dataset}/{id}` - Delete data
- ✅ `POST /api/data/{dataset}/restore/{id}` - Restore deleted data
- ✅ `POST /api/data/{dataset}/bulk` - Bulk insert

**Features:**
- ✅ Server-side pagination
- ✅ Field selection
- ✅ Relation expansion
- ✅ History tracking (optional)
- ✅ Metadata display

**Tarih:** 30 Aralık 2025

---

### 3. Incremental Field Support - ✅ TAMAMLANDI

**Components:**
- ✅ `IncrementalFieldService` - Sequence generation
- ✅ Dynamic prefix resolution
- ✅ Counter management (MongoDB counters collection)
- ✅ Domain-based placeholders (`{domain}`)
- ✅ Custom startValue and incrementStep

**Features:**
- ✅ Static prefix: `ISBN-{0:D10}`
- ✅ Dynamic prefix: `{publisherCode}-{year}-{0:D6}`
- ✅ Domain-based prefix: `{domain}-BOOK-{0:D6}`
- ✅ Atomic counter increments
- ✅ Scope isolation (per prefix)

**Tarih:** 30 Aralık 2025

---

### 4. RabbitMQ Event Publishing - ✅ TAMAMLANDI

**Components:**
- ✅ `BaseDataEvent` and derived events (DataCreatedEvent, DataUpdatedEvent, DataDeletedEvent, DataRestoredEvent)
- ✅ `IEventPublisher` interface
- ✅ `EventPublisher` implementation
- ✅ Unified exchange: `mngdatagateway.events`
- ✅ Domain-based routing keys: `{domainId}.{eventType}`

**Features:**
- ✅ Domain isolation
- ✅ `publish_mode` control (none, basic, full)
- ✅ Fire & forget publishing
- ✅ Error logging

**Integration:**
- ✅ MngHub integration (SignalR broadcasting)
- ✅ UI Event Messages page support

**Tarih:** 25 Aralık 2025

---

### 5. Books Dataset Example - ✅ TAMAMLANDI

**Components:**
- ✅ Dataset categories ("Book Categories")
- ✅ Lookup datasets (`tst_publishers`, `tst_genres`)
- ✅ Main dataset (`tst_books`)
- ✅ Test scripts and documentation

**Features:**
- ✅ Multiple incremental fields
- ✅ Relation fields (1-to-many, many-to-many)
- ✅ Predefined queries
- ✅ Index definitions
- ✅ Test data insertion

**Tarih:** 30 Aralık 2025

---

### 6. Code Optimization - ✅ TAMAMLANDI

**Refactoring:**
- ✅ Query pipeline serialization (BsonDocument support)
- ✅ DTO separation (QueryDefinitionDto, QueryDefinitionResponseDto)
- ✅ Helper methods for data processing
- ✅ Error handling improvements

**Tarih:** 30 Aralık 2025

---

### 7. Query Parameter Type Definitions - ✅ TAMAMLANDI

**Components:**
- ✅ `QueryParameterDefinition` entity (name, type, description, required)
- ✅ `QueryDefinition.parameters` property güncellendi (`BsonArray?`)
- ✅ `ValidateAndConvertParameters` metodu (tip validation ve conversion)
- ✅ `ConvertParameterByType` metodu (tip dönüşümleri)
- ✅ `ConvertJsonElementByType` metodu (JSON parametre desteği)
- ✅ Backward compatibility (eski List<string> formatı destekleniyor)

**Desteklenen Parametre Tipleri:**
- ✅ `text` - String değerler
- ✅ `number` - Sayısal değerler (int, long, double)
- ✅ `bool` - Boolean değerler (true/false)
- ✅ `datetime` - Tarih/saat değerleri (ISO 8601 format)

**Features:**
- ✅ Type validation (yanlış tip gönderildiğinde hata)
- ✅ Type conversion (string'den tip dönüşümü)
- ✅ Required/Optional parameter kontrolü
- ✅ JsonElement desteği (JSON'dan gelen parametreler için)
- ✅ Datetime için number değerler reddediliyor

**Test Results:**
- ✅ 10 query örneği test edildi
- ✅ Tüm query tipleri çalışıyor (number, bool, text, datetime, karma, opsiyonel)
- ✅ Type validation çalışıyor
- ✅ Parameter conversion çalışıyor

**Test Script:** `tests/test-all-query-examples.ps1`

**Tarih:** 26 Aralık 2025

---

### 8. Search Functionality - ✅ TAMAMLANDI

**Components:**
- ✅ `QueryOptionsDto.Search` property eklendi
- ✅ `DataController.List` endpoint'ine `search` query parameter eklendi
- ✅ `AggregatePipelineBuilder.AddSearch` metodu eklendi
- ✅ `DataService.SearchInRelationCollectionsAsync` metodu eklendi (pre-expansion search)
- ✅ Pre-expansion search mekanizması (relation field'larda arama için)

**Özellikler:**
- ✅ Ana collection'daki text field'larda arama (case-insensitive regex)
- ✅ Relation field'larda arama (pre-expansion search)
- ✅ Filter ile birlikte çalışma (AND mantığı)
- ✅ Pagination ile birlikte çalışma
- ✅ Expansion ile birlikte çalışma

**Pre-Expansion Search Mekanizması:**
1. Search term verildiğinde, önce relation collection'larında arama yapılıyor
2. Eşleşen ID'ler toplanıyor
3. Ana collection'da hem text field'larda hem de relation field'larda (toplanan ID'lerle) arama yapılıyor
4. Bu sayede relation field'larda da arama çalışıyor

**Test Results:**
- ✅ Ana field'larda arama çalışıyor (6/6 test başarılı)
- ✅ Relation field'larda arama çalışıyor (6/6 test başarılı)
- ✅ Publisher field'da arama: 10 kayıt bulundu
- ✅ Genres field'da arama: 2 kayıt bulundu
- ✅ Filter + Search: Başarılı
- ✅ Pagination + Search: Başarılı

**Test Scripts:**
- `tests/test-search-basic.ps1` - Ana field'larda arama testi
- `tests/test-search-relations.ps1` - Relation field'larda arama testi

**Tarih:** 26 Aralık 2025

---

### 9. CSV Export Functionality - ✅ TAMAMLANDI

**Components:**
- ✅ `QueryOptionsDto.Format` property eklendi (default: "json")
- ✅ `DataController.List` endpoint'ine `format` query parameter eklendi
- ✅ `CsvConverter` sınıfı oluşturuldu
- ✅ CSV formatında response döndürme mantığı eklendi

**Özellikler:**
- ✅ Nested object'leri flatten etme (örn: `publisher.name`, `publisher.country`)
- ✅ Array field'ları virgülle birleştirilmiş string'e çevirme (örn: `genres`: `"Fantasy, Sci-Fi"`)
- ✅ Nested object'leri flatten etme (örn: `coverImage.url`, `coverImage.width`)
- ✅ Internal field'ları atlama (`__dataId`, `__history` vb.)
- ✅ CSV escaping (quotes, commas, newlines)
- ✅ Filter ile birlikte çalışma
- ✅ Search ile birlikte çalışma
- ✅ Pagination ile birlikte çalışma

**CSV Format Özellikleri:**
- Relation field'lar flatten edilir: `publisher.name`, `publisher.country`, `publisher.website`
- Array field'lar virgülle birleştirilir: `genres` → `"Science Fiction, Fantasy"`
- Nested object'ler flatten edilir: `author.email`, `author.firstName`, `coverImage.url`
- Internal field'lar atlanır
- CSV escaping yapılır

**Test Results:**
- ✅ Basic CSV export: Başarılı (7/7 test başarılı)
- ✅ Relation fields flatten: Başarılı (`publisher.name`, `publisher.country` var)
- ✅ Array fields: Başarılı (`genres` virgülle birleştirilmiş)
- ✅ Filter + CSV: Başarılı
- ✅ Search + CSV: Başarılı
- ✅ Pagination + CSV: Başarılı
- ✅ Save to file: Başarılı

**Test Scripts:**
- `tests/test-csv-export.ps1` - CSV export testi

**Kullanım:**
```
GET /api/data/{dataset}?format=csv
GET /api/data/{dataset}?format=csv&search=Penguin&limit=10
GET /api/data/{dataset}?format=csv&filter=price:gte:20
```

**Tarih:** 26 Aralık 2025

---

## 🔄 DEVAM EDEN İŞLER

### Books Dataset Testing

**Durum:** Test verileri eklendi, event'ler UI'da görünüyor

**Tamamlanan:**
- ✅ Dataset'ler oluşturuldu (`tst_publishers`, `tst_genres`, `tst_books`)
- ✅ Test verileri eklendi (5 publishers, 8 genres, 8 books)
- ✅ Event publishing test edildi (MngHub → SignalR → UI)
- ✅ `publish_mode` kontrolü çalışıyor

**Sonraki Adımlar:**
- [ ] Bulk insert testi
- [ ] Advanced query testleri
- [ ] Expansion testleri
- [ ] Error scenario testleri

---

## 🎯 GELECEK PLANLAR

### Phase 1: Dataset Naming Strategy - ORTA ÖNCELİK

**Amaç:** Profesyonel dataset isimlendirme stratejisi implementasyonu

**Mevcut Durum:**
- Prefix-based naming kullanılıyor (örn: `tst_books`)
- Basit ve çalışıyor, ancak sınırlı esneklik

**Gereksinimler:**
- [ ] Environment-based naming (dev, test, staging, prod)
- [ ] Optional type-based naming (lookup, master, transaction)
- [ ] Metadata-based collection name generation
- [ ] Backward compatibility support
- [ ] Migration guide

**Proposed Structure:**
```csharp
public class DatasetSchema
{
    // Clean dataset name (user-friendly)
    public string name { get; set; } = "books";
    
    // Environment (dev, test, staging, prod)
    public string environment { get; set; } = "dev";
    
    // Dataset type (lookup, master, transaction, temp) - Optional
    public string? datasetType { get; set; } = null;
    
    // Category (already exists)
    public string? category { get; set; } = null;
    
    // Computed collection name
    [BsonIgnore]
    public string CollectionName
    {
        get
        {
            var parts = new List<string> { environment };
            if (!string.IsNullOrEmpty(datasetType)) parts.Add(datasetType);
            parts.Add(name);
            return string.Join("_", parts);
        }
    }
}
```

**Examples:**
- `dev_books` (environment only)
- `test_lookup_publishers` (environment + type)
- `prod_master_books` (environment + type)
- `staging_transaction_orders` (environment + type)

**Benefits:**
- ✅ Environment separation (dev/test/prod)
- ✅ Type-based organization (lookup/master/transaction)
- ✅ Scalable naming convention
- ✅ Professional approach
- ✅ Backward compatible (migration support)

**Implementation Steps:**
1. [ ] Add `environment` field to `DatasetSchema`
2. [ ] Add optional `datasetType` field
3. [ ] Implement `CollectionName` computed property
4. [ ] Update `DataService` to use `CollectionName`
5. [ ] Update all collection access points
6. [ ] Migration script for existing datasets
7. [ ] Backward compatibility layer (support old names)
8. [ ] Documentation update

**Migration Strategy:**
- Existing datasets: Keep old names, add `environment` field
- New datasets: Use new naming convention
- Gradual migration: Update datasets as needed

**Öncelik:** Orta (mevcut prefix yaklaşımı çalışıyor, iyileştirme için)

**Tarih:** 25 Aralık 2025 (Planlandı)

---

### Phase 1: Dataset Naming Strategy - ORTA ÖNCELİK

**Amaç:** Profesyonel dataset isimlendirme stratejisi implementasyonu

**Gereksinimler:**
- [ ] Environment-based naming (dev, test, staging, prod)
- [ ] Optional type-based naming (lookup, master, transaction)
- [ ] Metadata-based collection name generation
- [ ] Backward compatibility support

**Proposed Structure:**
```csharp
public class DatasetSchema
{
    public string name { get; set; } = "books"; // Clean name
    public string environment { get; set; } = "dev"; // dev, test, staging, prod
    public string? datasetType { get; set; } = null; // Optional: lookup, master, transaction
    
    // Computed collection name
    [BsonIgnore]
    public string CollectionName
    {
        get
        {
            var parts = new List<string> { environment };
            if (!string.IsNullOrEmpty(datasetType)) parts.Add(datasetType);
            parts.Add(name);
            return string.Join("_", parts);
        }
    }
}
```

**Examples:**
- `dev_books` (environment only)
- `test_lookup_publishers` (environment + type)
- `prod_master_books` (environment + type)

**Benefits:**
- ✅ Environment separation
- ✅ Type-based organization
- ✅ Scalable naming convention
- ✅ Professional approach

**Öncelik:** Orta (mevcut prefix yaklaşımı çalışıyor, iyileştirme için)

**Tarih:** 25 Aralık 2025 (Planlandı)

---

### Phase 2: Persons & PersonGroups Field Types - YÜKSEK ÖNCELİK

**Amaç:** `persons` ve `personGroups` field type'larını implement etmek

**Gereksinimler:**
- [ ] `IPersonService` interface ve implementation
- [ ] MngKeeper API entegrasyonu (user/group lookup)
- [ ] Validation logic (persons, personGroups)
- [ ] Expansion logic (GET operations)
- [ ] Caching mechanism (TTL: 5 minutes)

**Field Types:**
- `persons` - Single or array of user references
- `personGroups` - Single or array of group references

**Test Scenarios:**
- [ ] Books dataset'te `author`, `coAuthors`, `reviewerGroups`, `editorialTeam` field'ları
- [ ] Validation tests
- [ ] Expansion tests
- [ ] Cache tests

**Öncelik:** Yüksek (Books dataset planında Phase 4)

**Referans:** `docs/BOOKS_DATASET_PLAN.md` - Phase 4

---

### Phase 3: Dataset Authorization - ORTA ÖNCELİK

**Amaç:** Dataset bazlı yetkilendirme implementasyonu

**Gereksinimler:**
- [ ] `PermissionsDefinition` entity
- [ ] Permission check helper methods
- [ ] MngKeeper integration (user groups)
- [ ] DataController'da permission checks
- [ ] Group-based and user-based permissions

**Permission Types:**
- `read` - Read access
- `write` - Write access (create, update, delete)
- `create` - Create only
- `update` - Update only
- `delete` - Delete only

**Test Scenarios:**
- [ ] Unauthorized access (403 Forbidden)
- [ ] Authorized access (success)
- [ ] Group-based permissions
- [ ] User-based permissions

**Öncelik:** Orta (Books dataset planında Phase 6)

**Referans:** `docs/BOOKS_DATASET_PLAN.md` - Phase 6

---

### Phase 4: Advanced Query Features - DÜŞÜK ÖNCELİK

**Amaç:** Predefined query'leri geliştirmek

**Gereksinimler:**
- [ ] Query parameter validation
- [ ] Query result caching
- [ ] Query performance optimization
- [ ] Query documentation

**Öncelik:** Düşük

---

### Phase 5: Index Management - ✅ TAMAMLANDI (Metadata Storage)

**Amaç:** Index tanımlarını dataset schema içerisinde saklama

**Durum:** ✅ Index tanımları başarıyla dataset schema içerisinde saklanıyor ve okunabiliyor

**Tamamlanan:**
- ✅ `IndexDefinition` entity (name, fields, unique)
- ✅ `DatasetSchema.indexList` property
- ✅ Index tanımları dataset oluşturma/güncelleme sırasında kaydediliyor
- ✅ Index tanımları dataset schema ile birlikte okunabiliyor
- ✅ Test edildi (10 index tanımı başarıyla kaydedildi ve okundu)

**Önemli Not:**
- **DataGateway'in Sorumluluğu:** Index tanımlarını metadata olarak saklamak
- **DataGateway'in Sorumluluğu DEĞİL:** Fiziksel index oluşturma
- **Fiziksel Index Oluşturma:** Ayrı bir servis tarafından yapılacak (Index Management Service)

**Index Definition Yapısı:**
```csharp
public class IndexDefinition
{
    public string name { get; set; } = string.Empty;  // Index name
    public Dictionary<string, int> fields { get; set; } = new();  // field -> 1 (asc) or -1 (desc)
    public bool unique { get; set; } = false;  // Is unique index
}
```

**Test Script:** `tests/test-index-definitions.ps1`

**Öncelik:** ✅ Tamamlandı (Metadata storage)

---

### Phase 6: Performance Optimizations - ORTA ÖNCELİK

**Amaç:** Performans iyileştirmeleri

**Gereksinimler:**
- [ ] Query result caching
- [ ] Connection pooling optimization
- [ ] Bulk operation improvements
- [ ] Index usage analysis

**Öncelik:** Orta

---

### Phase 7: Predefined Queries Enhancement - YÜKSEK ÖNCELİK

**Amaç:** Predefined queries özelliklerini geliştirmek

**Gereksinimler:**
- [ ] Predefined queries sorununun çözülmesi (InvalidCastException)
- [ ] Query parameter type definitions (number, text, boolean, datetime)
- [ ] Parameter validation ve type conversion
- [ ] Hata mesajları ve kullanıcı dostu feedback

**Öncelik:** Yüksek (Sonraki session'da çözülecek)

**Referans:** `docs/NEXT_SESSION_ROADMAP.md` - Section 1 & 2

---

### Phase 8: Search & Export Features - ORTA ÖNCELİK

**Amaç:** Search ve export özellikleri eklemek

**Gereksinimler:**
- [ ] Full-text search functionality
- [ ] CSV export functionality
- [ ] Excel export (opsiyonel - gelecekte)
- [ ] Search indexing (MongoDB text index)

**Öncelik:** Orta (Sonraki session'da tartışılacak)

**Referans:** `docs/NEXT_SESSION_ROADMAP.md` - Section 3 & 4

---

### Phase 9: Advanced Validation - ORTA ÖNCELİK

**Amaç:** Validation mekanizmasını geliştirmek

**Gereksinimler:**
- [ ] Field-level validation rules (min/max, regex, custom)
- [ ] Dataset-level validation (cross-field, business rules)
- [ ] Custom validators (plugin-based)
- [ ] Kullanıcı dostu hata mesajları

**Öncelik:** Orta (Sonraki session'da tartışılacak)

**Referans:** `docs/NEXT_SESSION_ROADMAP.md` - Section 5

---

### Phase 10: API Gateway Service - DÜŞÜK ÖNCELİK

**Amaç:** Merkezi API Gateway servisi geliştirmek

**Gereksinimler:**
- [ ] API Gateway mimarisi tasarımı
- [ ] Service routing (MngKeeper, MngDataGateway, MngHub, vb.)
- [ ] Authentication/Authorization (JWT validation)
- [ ] Rate limiting
- [ ] Request/Response transformation
- [ ] Monitoring ve observability

**Öncelik:** Düşük (Sonraki session'da tartışılacak)

**Referans:** `docs/NEXT_SESSION_ROADMAP.md` - Section 6

**Not:** Bu ayrı bir servis olarak geliştirilecek (yeni proje)

---

### Phase 11: Monitoring & Metrics - DÜŞÜK ÖNCELİK

**Amaç:** Dataset ve data operasyonları için monitoring

**Gereksinimler:**
- [ ] Dataset usage metrics
- [ ] Query performance metrics
- [ ] Error rate tracking
- [ ] Data volume tracking

**Öncelik:** Düşük

---

### Phase 12: UI Implementation - YÜKSEK ÖNCELİK (Mng.Ui)

**Amaç:** Dataset yönetimi için UI sayfaları

**Referans:** `Mng.Ui/docs/DATASET_UI_DESIGN.md`

**Gereksinimler:**
- [ ] Dataset list page
- [ ] Dataset create/edit form
- [ ] Dataset detail page
- [ ] Field definition UI
- [ ] Query definition UI
- [ ] Permission management UI

**Öncelik:** Yüksek (UI roadmap'te Phase 3.2)

---

## 📋 TEKNİK DETAYLAR

### Architecture

**Layers:**
- **Presentation:** `MngDataGateway.Api` - REST API endpoints
- **Application:** `MngDataGateway.Application` - Services, DTOs, Events
- **Domain:** `MngDataGateway.Domain` - Entities, Value Objects
- **Infrastructure:** `MngDataGateway.Infrastructure` - MongoDB, RabbitMQ
- **Persistence:** `MngDataGateway.Persistence` - Data access, Services

**Key Services:**
- `DatasetService` - Dataset schema management
- `DataService` - Data CRUD operations
- `DataProcessService` - Data validation and processing
- `IncrementalFieldService` - Sequence generation
- `EventPublisher` - RabbitMQ event publishing

### MongoDB Structure

**Database:** Domain-based (e.g., `mng_meral`)

**Collections:**
- `@datasets` - Dataset schemas
- `@dataset_categories` - Dataset categories
- `__deletedDatas` - Soft deleted data
- `__counters` - Incremental field counters
- `{dataset_name}` - Actual data collections (e.g., `tst_books`)

### Event Publishing

**Exchange:** `mngdatagateway.events` (unified)

**Routing Keys:** `{domainId}.{eventType}`
- Example: `meral.datacreatedevent`
- Example: `meral.dataupdatedevent`

**Publish Modes:**
- `none` - No events published
- `basic` - Events published (minimal data)
- `full` - Events published (full data)

### Domain Isolation

- Each domain has its own MongoDB database
- Events are isolated by domainId in routing keys
- MngHub routes events to domain-specific SignalR rooms

---

## 📝 NOTLAR

### Current Dataset Naming

**Mevcut Yaklaşım:** Prefix-based (e.g., `tst_books`)

**Durum:** Çalışıyor, ancak iyileştirme için Phase 1 planlandı.

**Alternatifler:**
- Environment-based: `dev_books`, `test_books`, `prod_books`
- Type-based: `lookup_publishers`, `master_books`
- Hybrid: `dev_master_books`, `test_lookup_publishers`

**Karar:** Phase 1'de metadata-based hybrid yaklaşım implement edilecek.

---

**Son Güncelleme:** 25 Aralık 2025

