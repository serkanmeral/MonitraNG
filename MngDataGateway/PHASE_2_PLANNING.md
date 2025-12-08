# Phase 2 Planning - MngDataGateway

**Date:** 2025  
**Status:** Planning  
**Phase 1:** ✅ Complete (100% test success)

---

## 📊 Phase 1 Özeti

### Tamamlanan Özellikler
- ✅ Dataset Categories CRUD (7/7 tests)
- ✅ Dataset Schema CRUD (8/8 tests)
- ✅ Data CRUD (9/9 tests)
- ✅ RabbitMQ Event Publishing
- ✅ Incremental Field Generation
- ✅ History Tracking
- ✅ Multi-tenant Database Isolation

### Test Sonuçları
- **Total:** 34/34 tests passed (100%)
- **Incremental Field:** TASK-000008 → TASK-000011
- **RabbitMQ Events:** Publishing successfully

---

## 🎯 Phase 2 Hedefleri

### Öncelik Sırası

#### 🔴 YÜKSEK ÖNCELİK

##### 1. Bulk Insert
**Endpoint:** `POST /api/data/{datasetName}/bulk`

**Özellikler:**
- Array of data support
- Transaction içinde hepsi birden
- Partial success handling (hangi kayıtlar başarılı/hangi başarısız)
- Validation her kayıt için ayrı
- Incremental field'lar doğru sırada generate edilmeli

**Request Body:**
```json
{
  "items": [
    { "title": "Task 1", "priority": 1 },
    { "title": "Task 2", "priority": 2 }
  ]
}
```

**Response:**
```json
{
  "success": true,
  "data": {
    "total": 2,
    "successful": 2,
    "failed": 0,
    "items": [
      { "__dataId": "...", "taskNumber": "TASK-000012", ... },
      { "__dataId": "...", "taskNumber": "TASK-000013", ... }
    ],
    "errors": []
  }
}
```

**Tahmini Süre:** 1-2 gün

---

##### 2. Relation Expansion
**Query Parameter:** `?expand=field1,field2`

**Özellikler:**
- GET endpoint'lerinde kullanılabilir
- Max depth: 2-3 level (circular reference önleme)
- Performance optimization (batch lookup)
- Missing relation handling (null döner)

**Örnek:**
```
GET /api/data/@tasks/{id}?expand=project,assignedTo
```

**Response:**
```json
{
  "__dataId": "...",
  "title": "Task",
  "project": {
    "__dataId": "...",
    "name": "Project Name",
    "description": "..."
  },
  "assignedTo": {
    "__dataId": "...",
    "name": "User Name",
    "email": "..."
  }
}
```

**Tahmini Süre:** 2-3 gün

---

##### 3. Advanced Filtering
**Query Parameters:** `?filter=field:operator:value`

**Operators:**
- `eq` - Equal
- `ne` - Not equal
- `gt` - Greater than
- `gte` - Greater than or equal
- `lt` - Less than
- `lte` - Less than or equal
- `in` - In array
- `nin` - Not in array
- `like` - Contains (regex)
- `exists` - Field exists

**Örnekler:**
```
GET /api/data/@tasks?filter=priority:gte:3
GET /api/data/@tasks?filter=isCompleted:eq:false
GET /api/data/@tasks?filter=title:like:urgent
GET /api/data/@tasks?filter=priority:in:1,2,3
GET /api/data/@tasks?filter=dueDate:exists:true
```

**Multi-field Filtering:**
```
GET /api/data/@tasks?filter=priority:gte:3&filter=isCompleted:eq:false
```

**Tahmini Süre:** 2-3 gün

---

#### 🟡 ORTA ÖNCELİK

##### 4. Dynamic Defaults
**Format:** `{placeholder}` in default values

**Supported Placeholders:**
- `{now}` - Current UTC datetime
- `{now:yyyy-MM-dd}` - Formatted datetime
- `{currentUser.id}` - Current user ID
- `{currentUser.email}` - Current user email
- `{currentUser.username}` - Current username
- `{uuid}` - New GUID
- `{timestamp}` - Unix timestamp

**Örnek:**
```json
{
  "fields": [
    {
      "name": "createdAt",
      "fieldType": "datetime",
      "default": "{now}"
    },
    {
      "name": "createdBy",
      "fieldType": "text",
      "default": "{currentUser.email}"
    },
    {
      "name": "referenceId",
      "fieldType": "text",
      "default": "{uuid}"
    }
  ]
}
```

**Tahmini Süre:** 1-2 gün

---

##### 5. logging: "common" Mode
**Collection:** `@data_logs` in domain database

**Özellikler:**
- Centralized history tracking
- All data changes logged here
- Separate from data records
- Indexed by datasetName, dataId, timestamp
- TTL: Configurable (default: 90 days)

**Log Entry Structure:**
```json
{
  "__dataId": "log-guid",
  "datasetName": "@tasks",
  "dataId": "task-guid",
  "operation": "create|update|delete|restore",
  "timestamp": "2025-11-06T21:24:55Z",
  "userInfo": { "uid": "...", "userName": "...", "domain": "..." },
  "changes": { "field": { "old": "...", "new": "..." } },
  "ipAddress": "::1"
}
```

**Tahmini Süre:** 2-3 gün

---

##### 6. Detailed Event Config
**Schema'da yeni alanlar:**

```json
{
  "publish_mode": "none|basic|full",
  "eventConfig": {
    "excludeFields": ["__history", "internalField"],
    "publishOnCreate": true,
    "publishOnUpdate": true,
    "publishOnDelete": true,
    "publishOnRestore": false
  }
}
```

**Tahmini Süre:** 1 gün

---

#### 🟢 DÜŞÜK ÖNCELİK

##### 7. persons/personGroups Integration
**MngKeeper API Integration:**

- `persons` field type → MngKeeper `/api/users/{id}` call
- `personGroups` field type → MngKeeper `/api/groups/{id}` call
- Validation: User/Group exists check
- Caching: User/Group data cache (TTL: 5 minutes)

**Tahmini Süre:** 2-3 gün

---

##### 8. Custom Validation Webhooks
**Schema'da validation definition:**

```json
{
  "validations": [
    {
      "type": "webhook",
      "url": "https://api.example.com/validate",
      "method": "POST",
      "timeout": 5000,
      "onFailure": "reject|warn"
    }
  ]
}
```

**Tahmini Süre:** 2-3 gün

---

## 📋 Implementation Roadmap

### Sprint 1 (Yüksek Öncelik - 1-2 hafta)
1. ✅ Bulk Insert (1-2 gün)
2. ✅ Relation Expansion (2-3 gün)
3. ✅ Advanced Filtering (2-3 gün)

**Toplam:** ~5-8 gün

### Sprint 2 (Orta Öncelik - 1 hafta)
4. ✅ Dynamic Defaults (1-2 gün)
5. ✅ logging: "common" Mode (2-3 gün)
6. ✅ Detailed Event Config (1 gün)

**Toplam:** ~4-6 gün

### Sprint 3 (Düşük Öncelik - 1 hafta)
7. ✅ persons/personGroups Integration (2-3 gün)
8. ✅ Custom Validation Webhooks (2-3 gün)

**Toplam:** ~4-6 gün

---

## 🔧 Teknik Detaylar

### Bulk Insert Implementation

**Service:**
```csharp
public interface IDataService
{
    Task<BulkInsertResult> BulkCreateAsync(
        string datasetName,
        List<Dictionary<string, object>> items,
        string domainName,
        string databaseName,
        string userId,
        string userEmail,
        string? ipAddress = null);
}
```

**Transaction Strategy:**
- Tüm kayıtlar tek transaction içinde
- Bir kayıt fail olursa rollback (opsiyonel: continue on error mode)
- Incremental field'lar sırayla generate edilmeli

**Error Handling:**
- Her kayıt için ayrı validation
- Başarısız kayıtlar errors array'inde
- Başarılı kayıtlar items array'inde

---

### Relation Expansion Implementation

**Service:**
```csharp
public interface IRelationExpansionService
{
    Task<Dictionary<string, object>> ExpandRelationsAsync(
        Dictionary<string, object> data,
        DatasetSchema schema,
        List<string> expandFields,
        string databaseName,
        int maxDepth = 2);
}
```

**Strategy:**
1. Schema'dan relation field'ları bul
2. Her relation için `__dataId` değerlerini topla
3. Batch lookup (FindAsync with $in)
4. Recursive expansion (max depth kontrolü)
5. Circular reference detection

**Performance:**
- Batch lookup kullan (N+1 problem önleme)
- Cache mechanism (memory cache, 5 min TTL)
- Lazy loading (sadece istenen field'lar expand edilir)

---

### Advanced Filtering Implementation

**Service:**
```csharp
public interface IFilterService
{
    FilterDefinition<BsonDocument> BuildFilter(
        List<string> filterStrings,
        DatasetSchema schema);
}
```

**Filter Parser:**
```
filter=field:operator:value
```

**MongoDB Query Building:**
- `eq` → `{ field: value }`
- `ne` → `{ field: { $ne: value } }`
- `gt` → `{ field: { $gt: value } }`
- `in` → `{ field: { $in: [value1, value2] } }`
- `like` → `{ field: { $regex: value, $options: "i" } }`

**Type Conversion:**
- Schema'dan field type'ı al
- String → Number, Boolean, DateTime conversion
- Validation: Invalid operator/type combination

---

## 🧪 Test Stratejisi

### Bulk Insert Tests
- ✅ Single item (should work like normal create)
- ✅ Multiple items (all successful)
- ✅ Partial success (some fail validation)
- ✅ All fail (validation errors)
- ✅ Transaction rollback on error
- ✅ Incremental field ordering

### Relation Expansion Tests
- ✅ Single level expansion
- ✅ Multi-level expansion (max depth)
- ✅ Missing relation (null handling)
- ✅ Circular reference prevention
- ✅ Performance test (100+ relations)

### Advanced Filtering Tests
- ✅ Single filter
- ✅ Multi-filter (AND logic)
- ✅ Type conversion
- ✅ Invalid operator handling
- ✅ Performance test (large dataset)

---

## 📊 Success Metrics

### Phase 2 Completion Criteria
- [ ] Bulk insert endpoint working (100+ items)
- [ ] Relation expansion working (2-3 levels)
- [ ] Advanced filtering working (all operators)
- [ ] Dynamic defaults working (all placeholders)
- [ ] Common logging mode working
- [ ] Detailed event config working
- [ ] Test coverage: 90%+

### Performance Targets
- Bulk insert: 100 items in < 2 seconds
- Relation expansion: 10 relations in < 500ms
- Filtering: 10,000 records filtered in < 1 second

---

## 🚀 Başlangıç Adımları

### 1. Bulk Insert (İlk Öncelik)
```bash
# 1. Interface tanımla (Application layer)
# 2. Service implementasyonu (Persistence layer)
# 3. Controller endpoint ekle
# 4. Test scripti yaz
# 5. Test et
```

### 2. Relation Expansion
```bash
# 1. IRelationExpansionService interface
# 2. RelationExpansionService implementation
# 3. DataService'e entegre et
# 4. Controller'da query parameter handling
# 5. Test et
```

### 3. Advanced Filtering
```bash
# 1. IFilterService interface
# 2. FilterService implementation
# 3. Filter parser
# 4. MongoDB query builder
# 5. Controller'da query parameter handling
# 6. Test et
```

---

## 📝 Notlar

### Önemli Kararlar
1. **Bulk Insert:** Transaction içinde, rollback on error (opsiyonel: continue mode)
2. **Relation Expansion:** Max depth: 2, batch lookup, cache support
3. **Filtering:** AND logic (multi-filter), type-aware conversion
4. **Dynamic Defaults:** Placeholder resolution at create time only
5. **Common Logging:** Separate collection, TTL: 90 days (configurable)

### Dikkat Edilmesi Gerekenler
- **Performance:** Bulk insert için batch size limit (1000 items)
- **Security:** Filter injection prevention (sanitize input)
- **Memory:** Relation expansion için memory limit (prevent DoS)
- **Consistency:** Transaction boundaries (when to use transaction)

---

## 🔗 İlgili Dosyalar

- `DATA_CRUD_PLANNING.md` - Phase 1 planning
- `STATUS.md` - Current status
- `SESSION_6NOV2025_FINAL.md` - Phase 1 completion summary
- `ARCHITECTURE_GUIDE.md` - Architecture reference

---

**Hazırlayan:** AI Assistant  
**Date:** 2025  
**Status:** Planning Phase

