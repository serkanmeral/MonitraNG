# Data CRUD Controller - Planning Document

**Date:** 6 Kasım 2025  
**Status:** Planning Completed ✅  
**Ready for:** Phase 1 Implementation

---

## 🎯 Mission Statement

MngDataGateway'in core responsibility:
1. **Veri işlemi yapmak** (CRUD operations)
2. **RabbitMQ'ya bildirmek** (Event publishing)

---

## 📋 Final Decisions

### Pipeline Structure
```
Request → Validation → Process → Response → Notification (Async)
```

**Abort Strategy:**
- ❌ **Validation Fail** → 400 Bad Request (Pipeline STOP)
- ❌ **Process Fail** → 500 Internal Error (Pipeline STOP)
- ⚠️ **Notification Fail** → Internal Log (User etkilenmez)

### Transaction Strategy
```yaml
Type: Conditional (Auto-detect)
Required When:
  - Has incremental field: YES
  - loggingMode === "common": YES
  - Has relation fields: YES
Fallback: MongoDB Standalone → Try-catch → Direct insert
```

### RabbitMQ Architecture
```yaml
Exchange Strategy:
  Type: Topic, Domain-based
  Pattern: monitra.data.events.{domainName}
  Example: 
    - monitra.data.events.a1
    - monitra.data.events.b1
    - monitra.data.events.seven

Routing Key:
  Pattern: dataset.{datasetName}.{operation}
  Example:
    - dataset.@tasks.created
    - dataset.@tasks.updated
    - dataset.@projects.deleted

Exchange Creation:
  Strategy: Lazy (first event)
  
Event Payload:
  Content: Full data
  
Retry Strategy:
  Application Level: 3 attempts with exponential backoff
  On Final Fail: Log to @notification_errors collection
  
Isolation:
  - A1 domain → monitra.data.events.a1
  - B1 domain → monitra.data.events.b1
  - Cross-domain leak: IMPOSSIBLE
```

### Data Metadata
```yaml
Structure: Minimal
Fields:
  - __dataId: GUID (always)
  - __history: Array (only if logging: "self")
No Fields:
  - __createInfo: Not used
  - __lastUpdateInfo: Not used
Reason: Data records can be millions, prevent duplication
```

### Phase 1 Scope
```yaml
Includes:
  ✅ Single data insert (no bulk)
  ✅ Static default values only
  ✅ No relation expansion
  ✅ Simple event config (publishEvents: bool)
  ✅ Incremental gap: Allowed
  ✅ Minimal metadata
  ✅ History tracking (logging: "self")
  ✅ RabbitMQ event publish

Excludes (Phase 2):
  ⏳ Bulk insert
  ⏳ Dynamic defaults ({now}, {currentUser})
  ⏳ Relation expansion (?expand=)
  ⏳ logging: "common" mode
  ⏳ Detailed event config
```

---

## 🔄 CREATE PIPELINE FLOW

```
┌────────────────────────────────────────────────────────────┐
│ 1. REQUEST PHASE                                           │
│ ────────────────────────────────────────────────────────── │
│ POST /api/data/@tasks                                       │
│ Headers: { Authorization: Bearer JWT }                     │
│ Body: { title: "Task 1", priority: 1 }                    │
└────────────────────────────────────────────────────────────┘
                          ↓
┌────────────────────────────────────────────────────────────┐
│ 2. CONTEXT EXTRACTION                                      │
│ ────────────────────────────────────────────────────────── │
│ MongoContextService:                                       │
│   ├─ JWT → domain_name: "a1"                              │
│   ├─ JWT → userId, userEmail                              │
│   └─ Database: monitra_a1                                 │
│                                                            │
│ UserInfoService:                                           │
│   └─ Extract user details                                 │
└────────────────────────────────────────────────────────────┘
                          ↓
┌────────────────────────────────────────────────────────────┐
│ 3. SCHEMA LOADING                                          │
│ ────────────────────────────────────────────────────────── │
│ DatasetService.getSchema("@tasks"):                        │
│   ├─ Load from monitra_a1.@datasets                       │
│   ├─ Check if active                                      │
│   └─ Get field definitions                                │
│                                                            │
│ ❌ Schema not found → 404 Dataset Not Found               │
│ ❌ Schema inactive → 400 Dataset Inactive                 │
└────────────────────────────────────────────────────────────┘
                          ↓
┌────────────────────────────────────────────────────────────┐
│ 4. VALIDATION PHASE                                        │
│ ────────────────────────────────────────────────────────── │
│ ValidationService:                                         │
│   ├─ Mandatory Field Check                                │
│   │  └─ ❌ Missing → 400 "Field 'title' is required"     │
│   │                                                        │
│   ├─ Field Type Validation                                │
│   │  └─ ❌ Type mismatch → 400 "Must be number"          │
│   │                                                        │
│   ├─ forceSchema Check                                    │
│   │  ├─ Strict: Only defined fields                       │
│   │  └─ ❌ Extra field → 400 "Unknown field 'extra'"     │
│   │                                                        │
│   └─ Unique Constraint Check                              │
│      ├─ Query MongoDB for duplicates                      │
│      └─ ❌ Duplicate → 400 "Value must be unique"        │
│                                                            │
│ ✅ All validations pass → Continue                        │
└────────────────────────────────────────────────────────────┘
                          ↓
┌────────────────────────────────────────────────────────────┐
│ 5. PRE-PROCESS PHASE                                       │
│ ────────────────────────────────────────────────────────── │
│ 5.1. Apply Static Defaults                                │
│   └─ Missing fields with defaultValue → Set value         │
│                                                            │
│ 5.2. Generate Incremental Fields                          │
│   ├─ IncrementalService.generate()                        │
│   ├─ @__counters collection                               │
│   ├─ Atomic findOneAndUpdate                              │
│   ├─ Format: TASK-{0:D6} → TASK-000001                   │
│   └─ ❌ Counter fail → 500 Internal Error                │
│                                                            │
│ 5.3. Generate Metadata                                    │
│   ├─ __dataId: new GUID()                                │
│   └─ __history: [] (if logging: "self")                  │
│                                                            │
│ 5.4. Collection & Index Setup (First Insert Only)        │
│   ├─ Check if collection exists                           │
│   ├─ Create collection if needed                          │
│   ├─ Create indexes from schema.indexList                 │
│   └─ Cache collection status (don't check every time)     │
└────────────────────────────────────────────────────────────┘
                          ↓
┌────────────────────────────────────────────────────────────┐
│ 6. TRANSACTION DECISION                                    │
│ ────────────────────────────────────────────────────────── │
│ Evaluate:                                                  │
│   ├─ Has incremental field? → YES = Transaction           │
│   ├─ loggingMode === "common"? → YES = Transaction        │
│   └─ Has relation fields? → YES = Transaction             │
│                                                            │
│ MongoDB Standalone Check:                                 │
│   └─ Try transaction, catch → Fallback to direct insert   │
└────────────────────────────────────────────────────────────┘
                          ↓
┌────────────────────────────────────────────────────────────┐
│ 7. INSERT PHASE                                            │
│ ────────────────────────────────────────────────────────── │
│ MongoDB Insert:                                            │
│   ├─ db.collection("@tasks").insertOne(data)              │
│   ├─ Session if transaction needed                        │
│   └─ ❌ Insert fail → 500 Insert Failed                  │
│                                                            │
│ 7.1. History Tracking (if logging: "self")               │
│   ├─ Add to __history array:                             │
│   │  {                                                    │
│   │    operation: "create",                               │
│   │    userId: "guid-user",                               │
│   │    userEmail: "serkan@seven.com",                     │
│   │    timestamp: "2025-11-06T10:00:00Z",                 │
│   │    changes: null                                      │
│   │  }                                                    │
│   └─ Update document with history                         │
│                                                            │
│ 7.2. History Logging (if logging: "common")              │
│   └─ @data_logs collection (Phase 2)                      │
│                                                            │
│ ✅ Transaction Commit (if used)                           │
└────────────────────────────────────────────────────────────┘
                          ↓
┌────────────────────────────────────────────────────────────┐
│ 8. SUCCESS RESPONSE                                        │
│ ────────────────────────────────────────────────────────── │
│ HTTP 200 OK                                                │
│ {                                                          │
│   "success": true,                                         │
│   "data": {                                                │
│     "__dataId": "guid-123",                               │
│     "title": "Task 1",                                     │
│     "taskNumber": "TASK-000001",                          │
│     "priority": 1,                                         │
│     "__history": [...]  // if logging: "self"             │
│   },                                                       │
│   "meta": {                                                │
│     "timestamp": "2025-11-06T10:30:00.000Z"               │
│   }                                                        │
│ }                                                          │
└────────────────────────────────────────────────────────────┘
                          ↓ ASYNC
┌────────────────────────────────────────────────────────────┐
│ 9. NOTIFICATION PHASE (Async - Fire & Forget)             │
│ ────────────────────────────────────────────────────────── │
│ RabbitMqService.publishDataEvent():                        │
│                                                            │
│ 9.1. Ensure Exchange                                      │
│   ├─ Exchange: monitra.data.events.a1                     │
│   ├─ Type: topic                                           │
│   └─ Create if not exists (lazy)                          │
│                                                            │
│ 9.2. Build Event Payload                                  │
│   {                                                        │
│     "eventId": "guid-event",                              │
│     "eventType": "dataset.data.created",                  │
│     "timestamp": "2025-11-06T10:30:00.000Z",              │
│     "domain": {                                            │
│       "name": "a1",                                        │
│       "databaseName": "monitra_a1"                        │
│     },                                                     │
│     "dataset": {                                           │
│       "name": "@tasks",                                    │
│       "categoryCode": "GOREV"                             │
│     },                                                     │
│     "data": { ... },  // Full data                        │
│     "actor": {                                             │
│       "userId": "guid-user",                              │
│       "email": "serkan@seven.com"                         │
│     }                                                      │
│   }                                                        │
│                                                            │
│ 9.3. Publish with Retry                                   │
│   ├─ Routing Key: dataset.@tasks.created                  │
│   ├─ Options: { persistent: true }                        │
│   ├─ Retry: 3 attempts with exponential backoff           │
│   └─ ❌ All retries fail:                                 │
│       └─ Log to @notification_errors collection           │
│                                                            │
│ ⚠️ Notification fail does NOT affect user response        │
└────────────────────────────────────────────────────────────┘
```

---

## 🏗️ SERVICE ARCHITECTURE

```
DataController
    ↓
DataService (Main orchestrator)
    ├─→ ValidationService
    │     ├─ validateMandatoryFields()
    │     ├─ validateFieldTypes()
    │     ├─ validateForceSchema()
    │     └─ validateUniqueConstraints()
    │
    ├─→ DataProcessService
    │     ├─ applyDefaults()
    │     ├─ generateIncrementalFields()
    │     ├─ generateMetadata()
    │     └─ ensureCollectionAndIndexes()
    │
    ├─→ IncrementalFieldService
    │     └─ generate(schema, data, context)
    │         ├─ Parse format string
    │         ├─ Calculate counter key
    │         ├─ Atomic increment (@__counters)
    │         └─ Format result
    │
    ├─→ DataRepository
    │     ├─ insertOne(collection, data, session?)
    │     ├─ findDuplicates(collection, field, value)
    │     ├─ collectionExists(name)
    │     ├─ createCollection(name)
    │     └─ createIndexes(collection, indexes)
    │
    └─→ NotificationService (Async)
          └─ publishDataEvent(context, operation, data)
              ├─ Build event payload
              └─ Call RabbitMqService

RabbitMqService
    ├─ connect()
    ├─ ensureExchange(domainName)
    ├─ publish(exchange, routingKey, payload, options)
    ├─ publishWithRetry(...)
    └─ logFailedEvent(event, error)

Shared Services:
    ├─ MongoContextService (JWT → Domain → DB)
    ├─ UserInfoService (JWT → User details)
    └─ DatasetService (Schema loading & caching)
```

---

## 📦 PHASE BREAKDOWN

### ✅ PHASE 1 - Core CRUD (Current Focus)

**Endpoints:**
```
POST   /api/data/{datasetName}                   - Create data
GET    /api/data/{datasetName}                   - List data (pagination)
GET    /api/data/{datasetName}/{dataId}          - Get single data
PUT    /api/data/{datasetName}/{dataId}          - Update data
DELETE /api/data/{datasetName}/{dataId}          - Delete data (soft)
POST   /api/data/{datasetName}/{dataId}/restore  - Restore data
```

**Features:**
```
✅ Single data insert
✅ Schema validation (forceSchema, mandatory, type, unique)
✅ Static default values
✅ Incremental field generation (gap allowed)
✅ Collection & index creation (lazy)
✅ Conditional transaction
✅ Minimal metadata (__dataId + __history)
✅ History tracking (logging: "self")
✅ RabbitMQ event publish (domain-based)
✅ Simple event config (publishEvents: bool)
✅ Pagination
✅ Error handling & logging
```

**Test Dataset:**
```json
{
  "datasetName": "@test_tasks_224334",
  "fields": [
    { "name": "title", "type": "text", "isMandatory": true },
    { "name": "description", "type": "text" },
    { "name": "priority", "type": "number", "isMandatory": true },
    { "name": "isCompleted", "type": "bool", "isMandatory": true },
    { "name": "dueDate", "type": "datetime" },
    { 
      "name": "taskNumber", 
      "type": "incremental",
      "incrementalConfig": {
        "format": "TASK-{0:D6}"
      }
    }
  ]
}
```

---

### ⏳ PHASE 2 - Advanced Features

```
⏳ Bulk insert (array of data)
⏳ Dynamic default values ({now}, {currentUser.email})
⏳ Relation expansion (?expand=project,assignedTo)
⏳ logging: "common" mode (@data_logs collection)
⏳ Detailed event config (excludeFields, per-operation toggles)
⏳ Max history entries limit
⏳ Query/Filter on list (search, filter operators)
⏳ Sort options (multi-field)
⏳ Field-level permissions
⏳ Patch update (partial)
```

---

### 🔮 PHASE 3 - External Integrations

```
🔮 persons/personGroups integration (MngKeeper API)
🔮 Custom validation execution (HTTP webhook)
🔮 Query execution (dynamic data fetch)
🔮 Workflow triggers
🔮 Advanced notification (Email/SMS/Webhook)
🔮 Real-time updates (WebSocket/SignalR)
🔮 Audit dashboard
🔮 Data export/import (Excel, CSV)
🔮 Data versioning (full history snapshot)
```

---

## 📊 DATA EXAMPLES

### 1. Create Data Request

```http
POST /api/data/@tasks HTTP/1.1
Host: localhost:5000
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
Content-Type: application/json

{
  "title": "Yeni Task",
  "description": "Task açıklaması",
  "priority": 1,
  "isCompleted": false,
  "dueDate": "2025-12-31T23:59:59Z"
}
```

### 2. Create Data Response (Success)

```json
{
  "success": true,
  "data": {
    "__dataId": "550e8400-e29b-41d4-a716-446655440000",
    "title": "Yeni Task",
    "description": "Task açıklaması",
    "priority": 1,
    "isCompleted": false,
    "dueDate": "2025-12-31T23:59:59Z",
    "taskNumber": "TASK-000001",
    "__history": [
      {
        "operation": "create",
        "userId": "guid-user-123",
        "userEmail": "serkan@seven.com",
        "timestamp": "2025-11-06T10:30:00.000Z",
        "changes": null
      }
    ]
  },
  "meta": {
    "timestamp": "2025-11-06T10:30:00.123Z"
  }
}
```

### 3. Create Data Response (Validation Error)

```json
{
  "success": false,
  "error": {
    "code": "VALIDATION_ERROR",
    "message": "Validation failed",
    "details": [
      {
        "field": "title",
        "message": "Title is required",
        "value": null
      },
      {
        "field": "priority",
        "message": "Must be a number",
        "value": "high"
      }
    ]
  },
  "meta": {
    "timestamp": "2025-11-06T10:30:00.000Z",
    "path": "/api/data/@tasks"
  }
}
```

### 4. RabbitMQ Event Payload

```json
{
  "eventId": "event-guid-456",
  "eventType": "dataset.data.created",
  "eventVersion": "1.0",
  "timestamp": "2025-11-06T10:30:00.123Z",
  
  "source": {
    "service": "MngDataGateway",
    "instance": "instance-1",
    "version": "1.0.0"
  },
  
  "domain": {
    "name": "a1",
    "databaseName": "monitra_a1"
  },
  
  "dataset": {
    "name": "@tasks",
    "categoryCode": "GOREV",
    "collectionName": "@tasks"
  },
  
  "data": {
    "__dataId": "550e8400-e29b-41d4-a716-446655440000",
    "title": "Yeni Task",
    "taskNumber": "TASK-000001",
    "priority": 1,
    "isCompleted": false,
    "dueDate": "2025-12-31T23:59:59Z"
  },
  
  "actor": {
    "userId": "guid-user-123",
    "email": "serkan@seven.com",
    "fullName": "Serkan",
    "domainName": "a1",
    "ipAddress": "192.168.1.100"
  },
  
  "metadata": {
    "correlationId": "request-guid-789",
    "traceId": "trace-guid-xyz"
  }
}
```

**Published to:**
- **Exchange:** `monitra.data.events.a1`
- **Routing Key:** `dataset.@tasks.created`
- **Persistent:** `true`

---

## 🔐 SECURITY & ISOLATION

### Multi-Tenant Isolation

**Domain-based Exchange Strategy ensures:**

1. **Exchange Level Isolation**
   ```
   A1 Domain → monitra.data.events.a1
   B1 Domain → monitra.data.events.b1
   ```

2. **Consumer Binding**
   ```typescript
   // A1 consumer - only sees A1 events
   channel.bindQueue(queue, 'monitra.data.events.a1', 'dataset.#');
   
   // B1 consumer - only sees B1 events
   channel.bindQueue(queue, 'monitra.data.events.b1', 'dataset.#');
   ```

3. **Impossible Cross-Domain Leak**
   - A1 consumer CANNOT bind to B1 exchange
   - B1 events NEVER reach A1 consumers
   - Complete isolation guaranteed

### RabbitMQ Permissions (Production)

```bash
# Create domain-specific users
rabbitmqctl add_user a1_publisher strong_password_123
rabbitmqctl set_permissions -p / a1_publisher \
  "^monitra\.data\.events\.a1$" \
  "^monitra\.data\.events\.a1$" \
  ""

rabbitmqctl add_user b1_publisher strong_password_456
rabbitmqctl set_permissions -p / b1_publisher \
  "^monitra\.data\.events\.b1$" \
  "^monitra\.data\.events\.b1$" \
  ""
```

---

## 🎯 SUCCESS CRITERIA

Phase 1 tamamlandığında aşağıdaki senaryolar çalışıyor olmalı:

### ✅ Create Operation
```
✅ POST ile yeni task oluşturulabilir
✅ Validation hataları doğru dönülür (400)
✅ Mandatory field kontrolü çalışır
✅ Field type validation çalışır
✅ forceSchema (strict) çalışır
✅ Unique constraint kontrolü çalışır
✅ Static default values uygulanır
✅ taskNumber otomatik generate edilir (TASK-000001)
✅ __dataId otomatik generate edilir (GUID)
✅ __history array'i doğru doldurulur (logging: "self")
✅ Collection ve index otomatik oluşur (ilk insert'te)
✅ Transaction varsa doğru çalışır
✅ RabbitMQ'ya event publish edilir (domain-based exchange)
✅ RabbitMQ fail olursa log tutulur ama user etkilenmez
```

### ✅ List Operation
```
✅ GET /data ile liste alınabilir
✅ Pagination çalışır (skip, limit)
✅ Total count dönülür
✅ Soft-deleted kayıtlar gösterilmez
```

### ✅ Get Operation
```
✅ GET /data/{id} ile tekil kayıt alınabilir
✅ __dataId ile sorgu yapılabilir
✅ 404 döner (kayıt yoksa)
```

### ✅ Update Operation
```
✅ PUT /data/{id} ile update yapılabilir
✅ Validation çalışır
✅ __history'ye ekleme yapılır (logging: "self")
✅ Changed fields loglanır
✅ RabbitMQ event publish edilir
```

### ✅ Delete Operation
```
✅ DELETE /data/{id} ile soft delete yapılabilir
✅ __isDeleted flag set edilir
✅ __deleteInfo eklenir
✅ RabbitMQ event publish edilir
```

### ✅ Restore Operation
```
✅ POST /data/{id}/restore ile restore edilebilir
✅ __isDeleted = false
✅ __restoreInfo eklenir
✅ RabbitMQ event publish edilir
```

---

## 🚀 IMPLEMENTATION ORDER

### Step 1: Infrastructure Services (1-2 gün)
```
1. RabbitMqService
   ├─ Connection management
   ├─ Exchange creation (lazy)
   ├─ Publish with retry
   └─ Error logging

2. NotificationService
   ├─ Build event payload
   ├─ Call RabbitMqService (async)
   └─ Error handling
```

### Step 2: Data Services (2-3 gün)
```
3. ValidationService
   ├─ Mandatory fields
   ├─ Field types
   ├─ forceSchema
   └─ Unique constraints

4. IncrementalFieldService
   ├─ Counter management (@__counters)
   ├─ Format parsing
   ├─ Atomic increment
   └─ Result formatting

5. DataProcessService
   ├─ Apply defaults
   ├─ Generate incremental fields
   ├─ Generate metadata
   └─ Collection & index management
```

### Step 3: Repository & Main Service (1-2 gün)
```
6. DataRepository
   ├─ insertOne
   ├─ findDuplicates
   ├─ collectionExists
   ├─ createCollection
   └─ createIndexes

7. DataService
   ├─ create()
   ├─ Transaction decision logic
   └─ Orchestrate all services
```

### Step 4: Controller & DTOs (1 gün)
```
8. DTOs
   ├─ CreateDataDto
   ├─ DataResponseDto
   └─ ValidationErrorDto

9. DataController
   └─ POST /api/data/{datasetName}
```

### Step 5: Testing & Refinement (1 gün)
```
10. Integration Testing
    ├─ Create with test dataset
    ├─ Validation scenarios
    ├─ RabbitMQ event check
    └─ Error scenarios
```

### Step 6: CRUD Operations (2-3 gün)
```
11. List, Get, Update, Delete, Restore endpoints
12. Update history tracking
13. Soft delete implementation
```

**Total Estimate:** 8-12 gün (Phase 1)

---

## 📝 NOTES & CONSIDERATIONS

### Incremental Field - Gap Handling
- **Decision:** Gaps allowed (ignore)
- **Reason:** Counter's purpose is uniqueness, not sequentiality
- **Example:** TASK-0001, TASK-0003 (0002 missing) is acceptable
- **Scenario:** Concurrent requests, one fails after counter increment

### Transaction Fallback
- MongoDB Standalone does NOT support transactions
- Detect transaction support failure
- Fallback to direct insert
- Log warning for monitoring

### RabbitMQ Connection
- Singleton connection per application instance
- Reconnection strategy on connection loss
- Health check endpoint: `/health/rabbitmq`
- Lazy exchange creation (on first use)

### Collection & Index Management
- Check collection existence (cached)
- Create collection if not exists
- Apply indexes from schema.indexList
- One-time operation (don't repeat on every insert)

### Event Publishing
- Always async (Fire & Forget)
- Never blocks user response
- Retry 3 times with exponential backoff
- Log failures to @notification_errors
- Admin dashboard for manual retry (Phase 2)

---

## 🔗 RELATED DOCUMENTS

- `NEXT_SESSION_DATA_CRUD.md` - Initial planning notes
- `STATUS.md` - Current project status
- Dataset Schema examples in `@datasets` collection
- Test dataset: `@test_tasks_224334`

---

**Document Status:** ✅ Approved - Ready for Implementation  
**Next Action:** Phase 1 Implementation Start  
**Estimated Duration:** 8-12 days

---

**END OF PLANNING DOCUMENT**

