# MngDataGateway - Development Roadmap

## 📋 Genel Bakış

**MngDataGateway**, MongoDB'ye sofistike ve dinamik veri erişimi sağlayan, schema-based bir veri yönetim mikroservisidir.

### Temel Özellikler:
- ✅ **Dynamic Schema Management** - Runtime'da dataset tanımlama
- ✅ **Multi-tenant İzolasyon** - JWT token bazlı domain izolasyonu
- ✅ **Clean Architecture** - MngKeeper benzeri yapı
- ✅ **Event-Driven** - RabbitMQ entegrasyonu
- ✅ **External Validation** - HTTP-based validation
- ✅ **Predefined Queries** - Parametrik aggregation pipelines
- ✅ **Auto-increment Fields** - Format desteği ile
- ✅ **Flexible/Strict Schema** - forceSchema seçeneği

---

## 🏗️ Mimari Tasarım

### Katmanlı Yapı (Clean Architecture):
```
MngDataGateway/
├── MngDataGateway.Domain/          # Entities, Interfaces, Enums
├── MngDataGateway.Application/     # Commands, Queries, DTOs, Handlers
├── MngDataGateway.Infrastructure/  # MongoDB, RabbitMQ, MngKeeper Client
└── MngDataGateway.Api/            # Controllers, Middleware
```

### Veri Akışı:
```
┌──────────────┐
│   Frontend   │ → JWT Token
└──────┬───────┘
       ↓
┌──────────────────────────────────────────┐
│  MngDataGateway API                      │
│  - JWT Validation                        │
│  - domain_name → Database: mng_{domain}  │
└──────┬───────────────────────────────────┘
       ↓
┌──────────────────────────────────────────┐
│  MongoDB (Per Domain Database)           │
│  ├── @datasets (meta-schema)             │
│  ├── @tasks (actual data)                │
│  ├── @projects (actual data)             │
│  ├── @__counters (incremental tracking)  │
│  └── @data_logs (common logging)         │
└──────────────────────────────────────────┘
```

---

## 🔐 Authentication & Authorization

### JWT Token Yapısı:

Token'dan alınacak bilgiler:
```json
{
  "sub": "user-id",
  "email": "user@example.com",
  "domain_id": "69051b09da18595c1fa866ce",    // ← Database seçimi için
  "domain_name": "test-domain",                // ← mng_{domain_name}
  "is_admin": false,
  "realm_access": {
    "roles": ["user", "editor"]
  }
}
```

### Database Bağlantısı:
- **Connection String:** Environment'tan (appsettings.json)
  ```
  "MongoDB": "mongodb://admin:admin123@localhost:27017"
  ```
- **Database Name:** JWT token'dan parse edilecek
  ```
  domain_name: "test-domain" → Database: "mng_test-domain"
  ```

---

## 📊 Datasets Kavramı

**@datasets** collection'ı, diğer collection'ların meta-schema'larını tutar.

### Çift Katmanlı Yapı:
```
@datasets (Meta-schema Layer)
  └─ "tasks" dataset kaydı
       ↓ tanımlar
@tasks (Data Layer)
  └─ Gerçek task kayıtları
```

### Dataset Kaydı Yapısı:

```json
{
  "__dataId": "uuid",
  "category": "uuid | null",
  "name": "@tasks",
  "description": "Task management data",
  "forceSchema": false,
  "logging": "self",
  "publish_mode": "full",
  "fields": [...],
  "validations": [...],
  "queries": [...],
  "indexList": [...]
}
```

---

## 🔧 Dataset Alanları (Detaylı)

### 1. category (UUID | null)
**Amaç:** Dataset kategorilendirme

- **İlişki:** `@dataset_categories` collection'ı
- **Opsiyonel:** null olabilir
- **Kullanım:** Dataset'leri gruplamak için

**Örnek:**
```json
{
  "category": "df481da7-f056-4a38-9136-e988558ac2b8"  // → "Task Management" kategorisi
}
```

---

### 2. __dataId (GUID - Auto-generated)
**Amaç:** Tüm kayıtlar için benzersiz, platform-agnostic identifier

- ✅ **Otomatik oluşturuluyor** (MngDataGateway tarafından)
- ✅ **GUID formatında** (UUID v4)
- ✅ **Zorunlu alan** (her kayıtta mutlaka var)
- ✅ **Değiştirilemez** (immutable)
- ✅ **TÜM lookup'larda varsayılan olarak kullanılacak**
- ❌ MongoDB'nin `_id` alanı hiç kullanılmayacak

**MongoDB Index:**
```javascript
db.collection.createIndex({ "__dataId": 1 }, { unique: true })
```

**Response'larda:**
```json
{
  "$project": {
    "_id": 0,           // ← _id'yi HİÇBİR ZAMAN döndürmüyoruz
    "__dataId": 1,      // ← Bizim primary key'imiz
    // ...
  }
}
```

---

### 3. name (string - Unique)
**Amaç:** Dataset'in adı = MongoDB collection adı

- ✅ **Unique** (aynı database'de iki dataset aynı isimde olamaz)
- ✅ **Zorunlu alan** (mandatory)
- ✅ **`name === collection_name`** (bire bir aynı)
- ✅ **Prefix'ler (`@`, `__`, vb.) opsiyonel** (kullanıcı tercihi)

**Örnekler:**
```json
{ "name": "@tasks" }           → Collection: "@tasks"
{ "name": "__system_logs" }    → Collection: "__system_logs"
{ "name": "customers" }        → Collection: "customers"
```

**Unique Index:**
```javascript
db["@datasets"].createIndex({ "name": 1 }, { unique: true })
```

---

### 4. description (string - Optional)
**Amaç:** Dataset'in amacını açıklayan metin

- ✅ **Opsiyonel** (null veya boş olabilir)
- ✅ **Sadece dokümantasyon amaçlı**
- ✅ **UI'da gösterilebilir**

---

### 5. logging (enum: "self" | "none" | "common")
**Amaç:** Insert/Update işlemlerinde history tutma stratejisi

#### A. logging: "self"
History kaydın **kendi içinde** tutulur

```json
{
  "__dataId": "task-123",
  "title": "Fix bug",
  "__history": [
    {
      "operation": "create",
      "timeUTC": "2025-11-03T10:00:00Z",
      "userInfo": {
        "uid": "user-456",
        "userName": "john.doe",
        "domain": "test-domain",
        "dbName": "mng_test-domain"
      },
      "oldValue": null,
      "newValue": { "title": "Fix bug" }
    }
  ]
}
```

#### B. logging: "none"
History **hiç tutulmaz**

#### C. logging: "common"
History **merkezi collection'da** tutulur

```json
// @data_logs collection
{
  "__dataId": "log-xyz-123",
  "collectionName": "@tasks",
  "recordId": "task-456",
  "operation": "update",
  "timeUTC": "2025-11-03T12:00:00Z",
  "userInfo": {...},
  "oldValue": { "status": "open" },
  "newValue": { "status": "done" }
}
```

---

### 6. publish_mode (enum: "none" | "basic" | "full")
**Amaç:** CRUD işlemlerinde RabbitMQ'ya event yayınlama stratejisi

#### A. publish_mode: "none"
Event yayınlanmaz

#### B. publish_mode: "basic"
Minimal bilgiler yayınlanır

```json
{
  "collectionName": "@tasks",
  "recordId": "task-123",
  "operation": "update",
  "timestamp": "2025-11-03T12:00:00Z",
  "domain": "test-domain",
  "userId": "user-789"
}
```

#### C. publish_mode: "full"
Tüm veri yayınlanır

```json
{
  "collectionName": "@tasks",
  "recordId": "task-123",
  "operation": "update",
  "timestamp": "2025-11-03T12:00:00Z",
  "domain": "test-domain",
  "userId": "user-789",
  "data": {
    "__dataId": "task-123",
    "title": "Fix bug",
    "status": "done",
    // ... tüm field'lar
  },
  "oldValue": { "status": "in_progress" },
  "newValue": { "status": "done" }
}
```

**RabbitMQ Routing:**
```
Exchange: "mng.datagateway.events"
Routing Key: "{domain}.{collection}.{operation}"
Example: "test-domain.@tasks.update"
```

---

### 7. forceSchema (boolean)
**Amaç:** Schema zorunluluğu (strict vs flexible)

#### forceSchema: true (Strict)
Sadece schema'da tanımlı field'lar kullanılabilir

```json
// ✅ OK
{ "title": "Fix bug", "status": "open" }

// ❌ HATA!
{ "title": "Fix bug", "extra_field": "value" }  // Schema'da yok
```

#### forceSchema: false (Flexible)
Schema dışında field'lar eklenebilir

```json
// ✅ OK
{ "title": "Fix bug", "extra_field": "value", "custom": 123 }
```

---

### 8. fields[] (array) - Field Tanımları
**Amaç:** Dataset'teki alanların tanımı

Her field için:
```json
{
  "fieldType": "text | number | bool | datetime | object | relation | persons | personGroups | incremental",
  "name": "field_name",
  "title": "Display Name",
  "description": "Field açıklaması",
  "mandatory": true | false,
  "unique": true | false,
  "isArray": true | false,
  "relation": {...},
  "incrementalOptions": {...}
}
```

---

## 📝 Field Types (Detaylı)

### 1. text
String veri

```json
{
  "fieldType": "text",
  "name": "title",
  "title": "Title"
}
```
MongoDB: `{ "title": "Fix bug" }`

---

### 2. number
Sayısal veri

```json
{
  "fieldType": "number",
  "name": "story_point",
  "title": "Story Point"
}
```
MongoDB: `{ "story_point": 5 }`

---

### 3. bool
Boolean (true/false)

```json
{
  "fieldType": "bool",
  "name": "is_active",
  "title": "Active"
}
```
MongoDB: `{ "is_active": true }`

---

### 4. datetime
Tarih/saat

```json
{
  "fieldType": "datetime",
  "name": "due_date",
  "title": "Due Date"
}
```
MongoDB: `{ "due_date": ISODate("2025-11-05T00:00:00Z") }`

---

### 5. object
JSON object (free-form)

```json
{
  "fieldType": "object",
  "name": "custom_data",
  "title": "Custom Data"
}
```
MongoDB: `{ "custom_data": { "key1": "value1", "nested": {...} } }`

---

### 6. relation
Başka dataset'e referans (MongoDB Lookup)

```json
{
  "fieldType": "relation",
  "name": "task_state",
  "relation": {
    "relatedDataset": "@task_states",
    "relationField": "__dataId"      // Boş ise varsayılan __dataId
  }
}
```

**Veri:**
```json
{ "task_state": "state-abc-xyz" }  // __dataId referansı
```

**Lookup (Otomatik):**
```javascript
{
  $lookup: {
    from: "@task_states",
    localField: "task_state",
    foreignField: "__dataId",
    as: "task_state"
  }
}
```

**Sonuç:**
```json
{
  "task_state": {
    "__dataId": "state-abc-xyz",
    "name": "In Progress",
    "color": "#FFA500"
  }
}
```

**relation + isArray:**
```json
{
  "fieldType": "relation",
  "name": "tags",
  "isArray": true,
  "relation": {
    "relatedDataset": "@task_labels"
  }
}
```
MongoDB: `{ "tags": ["label-1", "label-2", "label-3"] }`

---

### 7. persons
User ID'leri tutar, GET'te MngKeeper'dan user bilgisi ile zenginleştirilir

```json
{
  "fieldType": "persons",
  "name": "assign_users",
  "isArray": true,
  "title": "Kullanıcı Atama"
}
```

**MongoDB:**
```json
{ "assign_users": ["user-id-456", "user-id-789"] }  // Sadece ID'ler
```

**GET Response (Zenginleştirilmiş):**
```json
{
  "assign_users": [
    {
      "userId": "user-id-456",
      "username": "john.doe",
      "email": "john.doe@test-domain.com",
      "firstName": "John",
      "lastName": "Doe"
    },
    {
      "userId": "user-id-789",
      "username": "jane.smith",
      "email": "jane.smith@test-domain.com",
      "firstName": "Jane",
      "lastName": "Smith"
    }
  ]
}
```

**İmplementasyon:** Sonra karar verilecek (HTTP API call veya cache)

---

### 8. personGroups
User Group ID'leri tutar, GET'te MngKeeper'dan group bilgisi ile zenginleştirilir

```json
{
  "fieldType": "personGroups",
  "name": "assign_user_groups",
  "isArray": true,
  "title": "Kullanıcı Atama Grupları"
}
```

**MongoDB:**
```json
{ "assign_user_groups": ["group-id-abc", "group-id-xyz"] }
```

**GET Response:**
```json
{
  "assign_user_groups": [
    {
      "groupId": "group-id-abc",
      "name": "Backend Team",
      "description": "Backend developers",
      "memberCount": 5
    }
  ]
}
```

---

### 9. incremental
Auto-increment field (format desteği ile)

```json
{
  "fieldType": "incremental",
  "name": "task_number",
  "title": "Task Number",
  "mandatory": true,
  "unique": true,
  "incrementalOptions": {
    "startValue": 1,
    "incrementStep": 1,
    "format": null                  // null: number, string: formatted
  }
}
```

#### Format Yok (Plain Number):
```json
{
  "incrementalOptions": {
    "startValue": 1,
    "incrementStep": 1,
    "format": null
  }
}
```
MongoDB: `{ "task_number": 156 }` (number)

#### Format Var (Formatted String):
```json
{
  "incrementalOptions": {
    "startValue": 1,
    "incrementStep": 1,
    "format": "TASK-{0:D6}"
  }
}
```
MongoDB: `{ "task_number": "TASK-000156" }` (string)

#### Format Örnekleri:

| Format | Sonuç |
|--------|-------|
| `null` | 156 |
| `"{0:D6}"` | 000156 |
| `"TASK-{0}"` | TASK-156 |
| `"INV-{0:D8}"` | INV-00000156 |
| `"ORD-{year}{month}-{0:D4}"` | ORD-202511-0156 |
| `"{domain}-TKT-{0:D5}"` | test-domain-TKT-00156 |

#### Placeholders:
- `{0}` → Counter değeri
- `{0:D4}` → Zero-padded (4 digit)
- `{domain}` → Domain name
- `{year}` → Current year (4 digit)
- `{month}` → Current month (2 digit)
- `{day}` → Current day (2 digit)
- `{yy}` → Year (2 digit)

#### Counter Collection:
```json
// @__counters collection
{
  "_id": "@tasks.task_number",       // {collection}.{field}
  "datasetName": "@tasks",
  "fieldName": "task_number",
  "currentValue": 156,
  "startValue": 1,
  "incrementStep": 1,
  "format": "TASK-{0:D6}",
  "createdAt": "2025-11-03T10:00:00Z",
  "lastUpdatedAt": "2025-11-03T12:00:00Z"
}
```

#### Scope:
- **Per domain** (her domain ayrı)
- **Per collection** (her collection ayrı)
- **Per field** (her incremental field ayrı counter)

```
Domain: test-domain
  └─ @tasks
      ├─ task_number: 1, 2, 3...
      └─ order_number: 1, 2, 3...  (ayrı counter)
```

#### Özellikler:
- ✅ Otomatik oluşturulur (create sırasında)
- ✅ Unique olmalı
- ✅ Mandatory olmalı
- ✅ Update edilemez (immutable)
- ✅ Client tarafından değer gönderilemez
- ✅ Reset yok (hiç sıfırlanmaz)
- ✅ Atomic increment (concurrent-safe)

---

## 🔍 Field Özellikleri

### name (string)
Field'in adı ve MongoDB'deki alan adı

```json
{ "name": "title" }
```
→ MongoDB: `{ "title": "..." }`

---

### title (string)
Field'in görünen adı (UI)

```json
{
  "name": "assign_users",
  "title": "Kullanıcı Atama"
}
```

---

### description (string - optional)
Field açıklaması

```json
{
  "name": "task_sprint",
  "description": "sonradan objectId yapılacak"
}
```

---

### mandatory (boolean)
Field zorunlu mu?

```json
{ "mandatory": true }
```

Validation:
```
❌ { "title": null }
❌ { "title": "" }
✅ { "title": "Fix bug" }
```

---

### unique (boolean)
Unique constraint

```json
{ "unique": true }
```

MongoDB:
```javascript
db.collection.createIndex({ "field": 1 }, { unique: true })
```

---

### isArray (boolean)
Field bir array mi?

```json
{
  "fieldType": "text",
  "name": "tags",
  "isArray": true
}
```

MongoDB:
```json
{ "tags": ["bug", "urgent", "backend"] }
```

**Tüm field type'lar array olabilir:**
- `text + isArray` → string array
- `number + isArray` → number array
- `relation + isArray` → multiple references
- `persons + isArray` → multiple users

---

## ✅ validations[] (array) - External HTTP Validation

**Amaç:** CRUD işlemi öncesinde harici HTTP endpoint'e validation sorgusu

### Validation Tanımı:
```json
{
  "validations": [
    {
      "name": "check_stock",
      "description": "Stok kontrolü yap",
      "endpoint": "https://api.monitra.local/inventory/api/validate/stock",
      "method": "POST",
      "when": ["create", "update"],
      "order": 1,
      "enabled": true,
      "timeout": 5000,
      "headers": {
        "X-Validation-Type": "stock-check"
      }
    }
  ]
}
```

### Request Payload:
```json
{
  "validationContext": {
    "operation": "create",
    "datasetName": "@orders",
    "recordId": null,
    "timestamp": "2025-11-03T12:00:00Z"
  },
  "domainContext": {
    "domainId": "69051b09da18595c1fa866ce",
    "domainName": "test-domain"
  },
  "userContext": {
    "userId": "user-789",
    "username": "john.doe",
    "email": "john.doe@test-domain.com",
    "roles": ["user", "editor"]
  },
  "data": {
    "product_id": "prod-123",
    "quantity": 10
  },
  "oldData": null,
  "changes": null
}
```

### Success Response:
```json
{
  "isValid": true,
  "message": "Validation successful"
}
```
→ İşlem devam eder

### Error Response:
```json
{
  "isValid": false,
  "errorMessage": "Stok yetersiz! Mevcut: 5, Talep: 10",
  "errorCode": "INSUFFICIENT_STOCK",
  "details": {
    "available": 5,
    "requested": 10
  }
}
```
→ MngDataGateway hata fırlatır, işlem iptal

### Execution Strategy:
**Sequential (Sıralı) - ÖNERİLEN**
```
1. check_user_permission (order: 1) → OK
2. check_business_rules (order: 2) → OK
3. check_budget (order: 3) → FAIL ❌ → DUR, hata fırlat
```

---

## 🔎 queries[] (array) - Predefined Aggregation Pipelines

**Amaç:** Dataset için önceden tanımlı, parametrik MongoDB aggregation sorguları

### Query Tanımı:
```json
{
  "queries": [
    {
      "name": "getbyworkspace",
      "filter": {
        "customquery": [
          {
            "$match": {
              "$and": [
                { "__isDeleted": false },
                { "workspace": "##current_workspace_id" }  // ← Parameter
              ]
            }
          },
          {
            "$lookup": {
              "from": "@task_states",
              "localField": "task_state",
              "foreignField": "__dataId",
              "as": "task_state"
            }
          },
          {
            "$unwind": "$task_state"
          }
        ]
      }
    }
  ]
}
```

### Parametreler:
```
##current_workspace_id   → Runtime'da değiştirilecek
##user_id                → Olabilir
##domain_id              → Olabilir
##custom_param           → Herhangi bir parametre
```

### Çalışma Mantığı:
```
1. Query tanımını al: "getbyworkspace"
2. Pipeline'ı oku
3. ##current_workspace_id → endpoint'ten gelen değerle değiştir
4. Aggregation'ı çalıştır
5. Sonuçları döndür
```

**Not:** GET metodları detayları sonra konuşulacak

---

## 📇 indexList[] (array) - MongoDB Index Tanımları

**Amaç:** Collection'da oluşturulacak index'lerin tanımı

**Not:** Index oluşturma işlemi başka bir servis tarafından yapılacak

### Index Tanımı:
```json
{
  "indexList": [
    {
      "name": "idx_task_number",
      "fields": {
        "task_number": 1              // 1: ascending, -1: descending
      },
      "unique": true,
      "sparse": false
    },
    {
      "name": "idx_workspace_state",
      "fields": {
        "workspace": 1,
        "task_state": 1
      },
      "unique": false,
      "sparse": false
    },
    {
      "name": "idx_due_date",
      "fields": {
        "due_date": -1
      },
      "unique": false,
      "sparse": true
    },
    {
      "name": "idx_temp_sessions",
      "fields": {
        "expireAt": 1
      },
      "ttl": 3600                     // 1 saat sonra otomatik sil
    }
  ]
}
```

### Index Types:

#### 1. Simple Index:
```json
{
  "name": "idx_email",
  "fields": { "email": 1 },
  "unique": true
}
```
MongoDB:
```javascript
db.collection.createIndex({ "email": 1 }, { unique: true })
```

#### 2. Compound Index:
```json
{
  "name": "idx_user_workspace",
  "fields": {
    "user_id": 1,
    "workspace": 1
  }
}
```

#### 3. Sparse Index:
```json
{
  "name": "idx_phone",
  "fields": { "phone_number": 1 },
  "unique": true,
  "sparse": true              // Null değerler index'e dahil değil
}
```

#### 4. TTL Index:
```json
{
  "name": "idx_sessions",
  "fields": { "expireAt": 1 },
  "ttl": 3600                 // 3600 saniye sonra sil
}
```

**Kullanım:** Temporary sessions, cache data, one-time tokens

### System Indexes (Otomatik):
Her collection için otomatik oluşturulur:
```json
{
  "name": "idx_system___dataId",
  "fields": { "__dataId": 1 },
  "unique": true
}
```

---

## 💡 Eksikler ve Öneriler (Gelecek İçin)

### 🔴 Yüksek Öncelik:

#### 1. Field-Level Permissions
```json
{
  "fieldType": "number",
  "name": "salary",
  "permissions": {
    "read": ["admin", "hr"],
    "write": ["admin"],
    "requiredRoles": ["hr_manager"]
  }
}
```

#### 2. Default Values
```json
{
  "fieldType": "text",
  "name": "status",
  "defaultValue": "draft",
  "defaultValueType": "static"
}

{
  "fieldType": "datetime",
  "name": "created_at",
  "defaultValue": "{{now}}",
  "defaultValueType": "dynamic"
}
```

**Placeholders:**
- `{{now}}` → Current datetime
- `{{user_id}}` → JWT user ID
- `{{domain_id}}` → JWT domain ID
- `{{uuid}}` → New GUID

#### 3. Field Validation Rules
```json
{
  "fieldType": "text",
  "name": "email",
  "validationRules": [
    {
      "type": "regex",
      "pattern": "^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\\.[a-zA-Z]{2,}$",
      "message": "Invalid email format"
    },
    {
      "type": "maxLength",
      "value": 100
    }
  ]
}

{
  "fieldType": "number",
  "name": "age",
  "validationRules": [
    { "type": "min", "value": 18 },
    { "type": "max", "value": 100 }
  ]
}

{
  "fieldType": "text",
  "name": "status",
  "validationRules": [
    {
      "type": "enum",
      "values": ["draft", "pending", "approved", "rejected"]
    }
  ]
}
```

#### 4. Cascade Delete Strategy
```json
{
  "fieldType": "relation",
  "name": "workspace",
  "relation": {
    "relatedDataset": "@workspaces",
    "onDelete": "cascade"        // cascade | restrict | set_null
  }
}
```

**Strategies:**
- `cascade` → Parent silinince child'lar da silinir
- `restrict` → Child'lar varsa parent silinmez
- `set_null` → Parent silinince field null olur

---

### 🟡 Orta Öncelik:

#### 5. Computed Fields
```json
{
  "fieldType": "computed",
  "name": "total_price",
  "computeExpression": "quantity * unit_price",
  "returnType": "number",
  "dependencies": ["quantity", "unit_price"]
}

{
  "fieldType": "computed",
  "name": "full_name",
  "computeExpression": "CONCAT(first_name, ' ', last_name)",
  "returnType": "text"
}
```

#### 6. Lifecycle Hooks
```json
{
  "hooks": {
    "beforeCreate": [
      {
        "name": "validate_quota",
        "endpoint": "https://api.monitra.local/hooks/check-quota",
        "async": false
      }
    ],
    "afterCreate": [
      {
        "name": "send_notification",
        "endpoint": "https://api.monitra.local/notifications/send",
        "async": true
      }
    ]
  }
}
```

#### 7. UI Metadata
```json
{
  "fieldType": "text",
  "name": "description",
  "ui": {
    "widget": "textarea",
    "placeholder": "Enter description...",
    "width": "full",
    "order": 2,
    "group": "basic_info"
  }
}
```

#### 8. Bulk Operations
```http
POST /api/datasets/@tasks/data/bulk
{
  "operation": "create",
  "records": [
    { "title": "Task 1" },
    { "title": "Task 2" }
  ]
}
```

---

### 🟢 Düşük Öncelik:

#### 9. Conditional Fields
```json
{
  "fieldType": "number",
  "name": "discount_amount",
  "visibleWhen": {
    "field": "has_discount",
    "operator": "equals",
    "value": true
  }
}
```

#### 10. Data Versioning
```json
{
  "versioning": {
    "enabled": true,
    "strategy": "snapshot",
    "maxVersions": 10
  }
}
```

#### 11. Schema Migration
```json
{
  "version": "2.0",
  "migrations": [
    {
      "fromVersion": "1.0",
      "toVersion": "2.0",
      "script": "https://api.monitra.local/migrations/tasks-v2.js"
    }
  ]
}
```

#### 12. Import/Export
```http
GET /api/datasets/@tasks/export?format=json
POST /api/datasets/@tasks/import (file: tasks.csv)
```

---

## 📚 Soruların Toplu Listesi (Karar Verilecek)

### persons/personGroups İmplementasyonu:
- ❓ MngKeeper HTTP API call mı?
- ❓ Cache'den mi çekme?
- ❓ Batch request endpoint'i var mı?
- ❓ JWT token forward edilecek mi?
- ❓ User bulunamazsa ne olur? (null, error, partial data?)
- ❓ Create/Update sırasında user ID doğrulanacak mı?

### Logging (common mode):
- ❓ @data_logs için index stratejisi?
- ❓ Log retention policy? (ne kadar süre tutulacak?)
- ❓ Log cleanup mekanizması?

### Validation:
- ❓ Multiple validation execution: Sequential mi, parallel mi?
- ❓ Token'ı validation endpoint'e forward etmeli miyiz?
- ❓ Timeout sonrası ne olsun? (block, warn, continue?)

### Queries:
- ❓ Parameter injection nasıl çalışacak?
- ❓ Güvenlik: SQL injection benzeri saldırılara karşı korunma?
- ❓ Query caching?

### Performance:
- ❓ Redis cache entegrasyonu?
- ❓ Query result caching?
- ❓ persons/personGroups için cache stratejisi?

---

## 🎯 Sıradaki Adımlar

### 1. API Endpoints Tasarımı
```http
# Dataset Management
POST   /api/datasets
GET    /api/datasets
GET    /api/datasets/{name}
PUT    /api/datasets/{name}
DELETE /api/datasets/{name}

# Data CRUD
POST   /api/datasets/{name}/data
GET    /api/datasets/{name}/data
GET    /api/datasets/{name}/data/{id}
PUT    /api/datasets/{name}/data/{id}
DELETE /api/datasets/{name}/data/{id}

# Predefined Queries
GET    /api/datasets/{name}/query/{queryName}
```

### 2. Request/Response Models
- CreateDatasetRequest
- UpdateDatasetRequest
- CreateDataRequest
- UpdateDataRequest
- Query parameters (pagination, filtering, sorting)

### 3. Validation Logic
- Field validation
- External HTTP validation
- Schema validation (forceSchema)

### 4. CRUD Operations
- Create with incremental fields
- Update restrictions (incremental immutable)
- Delete operations
- Lookup resolution

### 5. Query Execution
- Parameter injection
- Aggregation pipeline
- persons/personGroups enrichment

### 6. Event Publishing
- RabbitMQ integration
- Message format
- publish_mode handling

### 7. Error Handling
- Validation errors
- Database errors
- External service errors
- Standard error responses

### 8. Testing Strategy
- Unit tests
- Integration tests
- Performance tests

---

## 📊 Teknoloji Stack

### Backend:
- **Framework:** ASP.NET Core 8.0
- **Architecture:** Clean Architecture
- **Pattern:** CQRS + MediatR

### Database:
- **MongoDB:** Primary data store
- **Collections:**
  - `@datasets` - Meta-schema
  - `@__counters` - Incremental tracking
  - `@data_logs` - Common logging (optional)
  - Dynamic collections per dataset

### Messaging:
- **RabbitMQ:** Event publishing

### External Integration:
- **MngKeeper API:** User/Group data enrichment
- **External Validation:** HTTP-based validation

### Authentication:
- **JWT:** Token-based authentication
- **KeyCloak:** Identity provider (via MngKeeper)

### Logging:
- **Serilog:** Structured logging

---

## 🔒 Production Readiness Checklist

### ⚠️ Kritik - Production için Düzeltilmesi Gerekenler

#### 1. JWT Authentication & Validation
**Durum:** 🔴 Development mode (validation disabled)

**Mevcut:**
```csharp
options.TokenValidationParameters = new TokenValidationParameters
{
    ValidateAudience = false,
    ValidateIssuer = false,
    ValidateIssuerSigningKey = false,
    SignatureValidator = delegate (string token, TokenValidationParameters parameters)
    {
        var jwt = new JsonWebToken(token);
        return jwt; // Doğrulama yapılmıyor!
    }
};
```

**Yapılacak:**
- ✅ `ValidateIssuer = true` - Issuer doğrulaması aktif edilmeli
- ✅ `ValidateIssuerSigningKey = true` - Signature validation aktif edilmeli
- ✅ `ValidateAudience = true` (opsiyonel) - Audience kontrolü eklenebilir
- ✅ Certificate validation bypass edilmemeli
- ✅ Custom signature validator kaldırılmalı (standart validation kullanılmalı)

**Öncelik:** 🔴 Yüksek

---

#### 2. CORS Policy
**Durum:** 🔴 AllowAnyOrigin aktif

**Mevcut:**
```csharp
builder.Services.AddCors(l =>
{
    l.AddPolicy("CorsPolicy", b =>
        b.AllowAnyOrigin()  // ← Tüm origin'lere izin veriyor!
        .AllowAnyMethod()
        .AllowAnyHeader()
        .WithExposedHeaders("Content-Disposition"));
});
```

**Yapılacak:**
```csharp
// Production için specific origin'ler tanımlanmalı
b.WithOrigins(
    "https://app.yourdomain.com",
    "https://admin.yourdomain.com"
)
.AllowAnyMethod()
.AllowAnyHeader()
.WithExposedHeaders("Content-Disposition");
```

**Öncelik:** 🔴 Yüksek

---

#### 3. Exception Handler - Response Format
**Durum:** 🟡 HTML yerine JSON dönmeli

**Mevcut:**
```csharp
context.Response.ContentType = "text/html";  // ← API için uygun değil
var errorMessage = $"{exceptionObject.Error.Message}";
await context.Response.WriteAsync(errorMessage);
```

**Yapılacak:**
```csharp
context.Response.ContentType = "application/json";
var errorResponse = new
{
    error = true,
    message = exceptionObject.Error.Message,
    timestamp = DateTime.UtcNow,
    path = context.Request.Path
};
await context.Response.WriteAsJsonAsync(errorResponse);
```

**Öncelik:** 🟡 Orta

---

#### 4. Certificate Selection Logic
**Durum:** 🟡 Kontrol edilmeli

**Mevcut:**
```csharp
// Line 97-99 CertificateHandler.cs
return string.IsNullOrEmpty(settings.CertificateSettings.DNS)
    ? GetSignedCertificate(log, settings)      // DNS boş → signed
    : CreateSelfSignedCertificate(log, settings.CertificateSettings.DNS); // DNS var → self-signed
```

**Soru:** 
- DNS boşsa → Signed certificate kullanılıyor ✅
- DNS doluysa → Self-signed oluşturuluyor ✅

Bu mantık doğru mu? Yoksa tersine mi olmalı?

**Yapılacak:**
- Mantık kontrolü
- Environment variable bazlı seçim (örn: `USE_SELF_SIGNED_CERT=true/false`)

**Öncelik:** 🟡 Orta

---

### 💡 İyileştirme Önerileri

#### 1. Certificate Handler - Error Handling
**Durum:** ⚪ İyileştirilebilir

**Mevcut:**
```csharp
catch (Exception ex)
{
    log.Error(ex, "Signed Cert Loading Error");
}
// Exception yutulur, boş certWithKey döner!
```

**Öneri:**
```csharp
catch (Exception ex)
{
    log.Fatal(ex, "Certificate loading failed - Application cannot start");
    throw; // Application başlamasın
}
```

**Öncelik:** 🟢 Düşük

---

#### 2. Scalar API Documentation Fix
**Durum:** 🔴 Çalışmıyor

**Mevcut Durum:**
- Swagger UI çalışıyor ✅
- Scalar UI boş ekran gösteriyor ❌
- OpenAPI route: `/api-docs/v1/swagger.json` ✅
- Scalar route pattern tanımlı ✅

**Denenen Çözümler:**
1. ❌ Route pattern kaldırma (ilk deneme)
2. ❌ Theme değiştirme (Solarized → Purple)
3. ❌ Route standardizasyonu (MngKeeper ile uyumlu hale getirme)
   ```csharp
   app.UseSwagger(c =>
   {
       c.RouteTemplate = "api-docs/{documentName}/swagger.json";
   });
   
   app.MapScalarApiReference(options =>
   {
       options
           .WithTitle("MngDataGateway API")
           .WithTheme(ScalarTheme.Purple)
           .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient)
           .WithOpenApiRoutePattern("/api-docs/{documentName}/swagger.json");
       
       options.AddServer(new ScalarServer(settings.OpenApiServerPath));
   });
   ```

**Olası Sebepler:**
- MapOpenApi() ve Scalar arasında uyumsuzluk
- .NET 9.0 + Scalar.AspNetCore 2.5.1 version uyumsuzluğu
- Kestrel configuration ile çakışma
- HTTPS certificate ile ilgili JavaScript loading problemi

**Yapılacaklar:**
- [ ] Scalar paket versiyonunu güncelle (2.5.1 → latest)
- [ ] MapOpenApi() olmadan Scalar kullanmayı dene
- [ ] Browser console'da JavaScript hatalarını kontrol et
- [ ] MngKeeper'daki Scalar konfigürasyonu ile karşılaştır
- [ ] Minimal API test projesi oluştur (isolated test)
- [ ] Scalar GitHub issues'larını kontrol et
- [ ] Alternative: Redoc veya SwaggerUI'ya geç

**Workaround:**
Swagger UI kullan (şu an çalışıyor): `https://localhost:5010/swagger`

**Öncelik:** 🟡 Orta (Swagger çalıştığı için critical değil)

**Not:** MngKeeper'da Scalar çalışıyor, oradaki konfigürasyonu referans al.

---

#### 3. Health Check Endpoint
**Durum:** ⚪ Yok

**Öneri:**
```csharp
builder.Services.AddHealthChecks()
    .AddMongoDb(mongoConnectionString, name: "mongodb")
    .AddRabbitMQ(rabbitConnectionString, name: "rabbitmq");

// Endpoint
app.MapHealthChecks("/health");
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});
```

**Faydalar:**
- Kubernetes liveness/readiness probes
- Monitoring sistemleri için
- Dependency kontrolü (MongoDB, RabbitMQ)

**Öncelik:** 🟢 Düşük

---

#### 4. Rate Limiting
**Durum:** ⚪ Yok

**Öneri:**
```csharp
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("fixed", opt =>
    {
        opt.PermitLimit = 100;
        opt.Window = TimeSpan.FromMinutes(1);
    });
});

app.UseRateLimiter();
```

**Faydalar:**
- API abuse önleme
- DDoS koruması
- Resource management

**Öncelik:** 🟢 Düşük

---

#### 5. Metrics & Monitoring
**Durum:** ⚪ Yok

**Öneri:**
```csharp
// Application Insights
builder.Services.AddApplicationInsightsTelemetry();

// Prometheus metrics
builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics =>
    {
        metrics.AddPrometheusExporter();
        metrics.AddAspNetCoreInstrumentation();
        metrics.AddHttpClientInstrumentation();
    });
```

**Faydalar:**
- Performance monitoring
- Error tracking
- Business metrics

**Öncelik:** 🟢 Düşük

---

#### 6. Environment Variable Validation
**Durum:** ⚪ Yok

**Öneri:**
```csharp
// Startup'ta required environment variables kontrolü
if (string.IsNullOrEmpty(datagatewaySettings?.MongoDB?.ConnectionString))
{
    throw new InvalidOperationException("MongoDB connection string is required!");
}

if (string.IsNullOrEmpty(datagatewaySettings?.Actors?.MngKeeper))
{
    throw new InvalidOperationException("MngKeeper URL is required!");
}
```

**Öncelik:** 🟡 Orta

---

#### 7. Certificate Loading Test
**Durum:** ⚪ Test edilmedi

**Yapılacak:**
- File-based certificate loading test
- Environment variable-based loading test
- PEM parsing test
- RSA key import test

**Öncelik:** 🟡 Orta

---

## 📨 RabbitMQ Messaging Architecture

### 🎯 Overview - 3 Katmanlı Topic Yapısı

MonitraNG ekosisteminde 3 farklı messaging topic'i kullanılacak:

1. **System Topic** - Backend-to-Backend (Mikroservis entegrasyonu)
2. **Global Topic** - System-to-All-Users (Platform geneli duyurular)
3. **Domain Topic** - Domain-Specific (İş olayları, real-time updates)

**Önemli:** Sadece **TOPIC** kullanılacak (ephemeral, broadcast). **QUEUE** kullanılmayacak (şu an için).

---

### 1️⃣ System Topic - Backend-to-Backend Communication

**Amaç:** Mikroservisler arası sistem olayları

**Exchange:**
```
Exchange: mng.topics
Type: Topic Exchange
Durable: false (ephemeral)
Auto-delete: false
```

**Routing Key Pattern:**
```
system.{service}.{entity}.{action}

Examples:
- system.mngkeeper.domain.created
- system.mngkeeper.domain.statusChanged
- system.mngdatagateway.dataset.created
- system.mngengine.alert.triggered
- system.mngreactor.workflow.executed
```

**Message Format:**
```json
{
  "eventId": "uuid",
  "eventType": "system.mngkeeper.domain.created",
  "timestamp": "2025-11-05T14:30:00Z",
  "source": "MngKeeper",
  "version": "1.0",
  "payload": {
    "domainId": "69051b09da18595c1fa866ce",
    "domainName": "test-domain",
    "databaseName": "mng_test-domain",
    "realmName": "test-domain",
    "status": "Active",
    "adminEmail": "admin@test-domain.com",
    "settings": {
      "maxUsers": 50,
      "maxAssets": 500,
      "enableMqtt": true
    },
    "createdAt": "2025-11-05T14:30:00Z"
  }
}
```

**Publishers:**
- MngKeeper → Domain lifecycle events
- MngDataGateway → Dataset operations
- MngEngine → Monitoring events
- MngReactor → Automation events

**Subscribers:**
```
MngDataGateway:
  - system.mngkeeper.domain.created → Initialize @datasets collection
  
MngEngine:
  - system.mngkeeper.domain.created → Setup monitoring
  
MngReactor:
  - system.mngkeeper.domain.created → Setup automation rules
```

**Characteristics:**
- ✅ Fire-and-forget
- ✅ No persistence
- ✅ Active listeners only
- ✅ Lightweight

---

### 2️⃣ Global Topic - System-wide Announcements

**Amaç:** Tüm kullanıcılara (tüm domain'ler) sistem duyuruları

**Exchange:**
```
Exchange: mng.topics (same)
Type: Topic Exchange
```

**Routing Key Pattern:**
```
global.{type}

Examples:
- global.maintenance
- global.announcement
- global.security
- global.feature
- global.emergency
```

**Message Format:**
```json
{
  "messageId": "uuid",
  "messageType": "global.maintenance",
  "timestamp": "2025-11-05T14:30:00Z",
  "severity": "critical | warning | info",
  "payload": {
    "title": {
      "en": "Scheduled Maintenance",
      "tr": "Planlı Bakım"
    },
    "message": {
      "en": "System will be under maintenance on Nov 6, 02:00-04:00 UTC",
      "tr": "Sistem 6 Kasım 02:00-04:00 arası bakımda olacaktır"
    },
    "scheduledAt": "2025-11-06T02:00:00Z",
    "duration": 7200,
    "affectedServices": ["MngKeeper", "MngDataGateway"],
    "displayOptions": {
      "showPopup": true,
      "popupDuration": 10000,
      "showBanner": true,
      "blockAccess": false
    }
  }
}
```

**Use Cases:**
- 🔧 Planlı bakım bildirisi
- 📢 Sistem güncellemesi duyurusu
- 🔒 Güvenlik uyarıları
- ✨ Yeni özellik duyuruları
- 🚨 Acil durum bildirimleri

**Publisher:**
- Admin Panel (Future) → Manual announcements
- MngKeeper → System-triggered announcements

**Subscribers:**
- Mng.UI (Frontend) → All connected users
- MngWebSocketGateway → Broadcast to all WebSocket connections

**Characteristics:**
- ✅ Broadcast to all users (all domains)
- ✅ Real-time delivery
- ✅ Multi-language support
- ✅ Display options

---

### 3️⃣ Domain Topic - Domain-Specific Events

**Amaç:** Belirli bir domain'e özel iş olayları (business events)

**Exchange:**
```
Exchange: mng.topics (same)
Type: Topic Exchange
```

**Routing Key Pattern:**
```
domain.{domain-name}.{dataset}.{action}

Examples:
- domain.test-domain.tasks.created
- domain.test-domain.tasks.updated
- domain.test-domain.tasks.deleted
- domain.test-domain.tasks.assigned
- domain.test-domain.assets.statusChanged
- domain.test-domain.workflows.completed
- domain.acme-corp.invoices.generated
```

**Message Format:**
```json
{
  "eventId": "uuid",
  "eventType": "domain.test-domain.tasks.created",
  "timestamp": "2025-11-05T14:30:00Z",
  "source": "MngDataGateway",
  "domain": {
    "domainId": "69051b09da18595c1fa866ce",
    "domainName": "test-domain"
  },
  "actor": {
    "userId": "user-id",
    "email": "john@test-domain.com",
    "username": "john.doe"
  },
  "payload": {
    "datasetName": "@tasks",
    "recordId": "TASK-000001",
    "action": "created",
    "data": {
      "title": "New Task",
      "assignedTo": "user-id-2",
      "priority": "high",
      "dueDate": "2025-11-10"
    }
  }
}
```

**Publisher:**
- MngDataGateway → Data CRUD operations (based on publish_mode)

**publish_mode Integration:**
```
Dataset definition:
{
  "name": "@tasks",
  "publish_mode": "none | basic | full"
}

none  → No publishing
basic → Minimal info (id, action, timestamp only)
full  → Complete data payload
```

**Subscribers:**
- Mng.UI (Frontend) → Domain-specific users only
- MngEngine → Monitoring & alerting
- MngReactor → Automation triggers

**Subscription Examples:**
```
Frontend (test-domain user):
  - domain.test-domain.#  (all domain events)

MngEngine:
  - domain.*.assets.*  (asset events from all domains)

MngReactor:
  - domain.#  (all domain events for automation)
```

**Characteristics:**
- ✅ Domain isolation enforced
- ✅ Only domain users receive messages
- ✅ Real-time business events
- ✅ publish_mode controls verbosity

---

## 🌐 MngWebSocketGateway - New Microservice

### Purpose:
Separate microservice acting as RabbitMQ ↔ WebSocket bridge for real-time frontend communication.

### Service Details:
```
Name: MngWebSocketGateway
Port: 5020 (HTTPS)
Technology: ASP.NET Core + SignalR
Architecture: Clean Architecture
Authentication: JWT (validated via MngKeeper)
```

### Responsibilities:

**✅ Does:**
- Accept WebSocket connections (SignalR)
- Validate JWT tokens (via MngKeeper API)
- Subscribe to RabbitMQ topics per connection
- Bridge RabbitMQ messages to WebSocket clients
- Connection lifecycle management
- Domain-based message filtering

**❌ Does NOT:**
- Business logic
- Database operations
- Domain management
- User management
- ✅ Pure messaging bridge only

---

### Architecture:

```
┌─────────────────────────────────────────────────┐
│  Mng.UI (Browser)                               │
│  - SignalR client                               │
│  - JWT token                                    │
└───────────────┬─────────────────────────────────┘
                │ WebSocket connection
                ↓
┌─────────────────────────────────────────────────┐
│  MngWebSocketGateway (Port 5020)                │
│                                                 │
│  1. Validate JWT (MngKeeper API + cache)        │
│  2. Extract domain_name from token              │
│  3. Subscribe to RabbitMQ:                      │
│     - global.*                                  │
│     - domain.{domain-name}.*                    │
│  4. Forward messages to WebSocket               │
└───────────────┬─────────────────────────────────┘
                │ RabbitMQ subscription
                ↓
┌─────────────────────────────────────────────────┐
│  RabbitMQ                                       │
│  Exchange: mng.topics (Topic)                   │
│                                                 │
│  Routing keys:                                  │
│  - global.*                                     │
│  - domain.{domain-name}.*                       │
└─────────────────────────────────────────────────┘
```

---

### SignalR Hub Implementation:

```csharp
public class NotificationHub : Hub
{
    private readonly IConnectionManager _connectionManager;
    private readonly IRabbitMqConsumer _rabbitMq;
    private readonly IJwtValidator _jwtValidator;
    
    public override async Task OnConnectedAsync()
    {
        // 1. Get JWT token
        var token = Context.GetHttpContext()?.Request.Query["access_token"];
        
        // 2. Validate via MngKeeper (cached)
        var claims = await _jwtValidator.ValidateAsync(token);
        
        var domainName = claims["domain_name"];
        var userId = claims["sub"];
        
        // 3. Register connection
        var connection = new WebSocketConnection
        {
            ConnectionId = Context.ConnectionId,
            UserId = userId,
            DomainName = domainName,
            ConnectedAt = DateTime.UtcNow,
            Subscriptions = new List<string>
            {
                "global.*",
                $"domain.{domainName}.#"
            }
        };
        
        await _connectionManager.AddAsync(connection);
        
        // 4. Setup RabbitMQ subscriptions
        foreach (var topic in connection.Subscriptions)
        {
            await _rabbitMq.SubscribeAsync(topic, 
                message => ForwardToClient(Context.ConnectionId, message));
        }
        
        await base.OnConnectedAsync();
    }
    
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await _rabbitMq.UnsubscribeAllAsync(Context.ConnectionId);
        await _connectionManager.RemoveAsync(Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}
```

---

### JWT Validation Strategy:

**Hybrid Approach (Performance):**
```csharp
public class JwtValidator : IJwtValidator
{
    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    
    public async Task<TokenClaims> ValidateAsync(string token)
    {
        var cacheKey = $"jwt:{ComputeHash(token)}";
        
        // 1. Cache check
        if (_cache.TryGetValue<TokenClaims>(cacheKey, out var cached))
        {
            return cached;  // ✅ ~1ms
        }
        
        // 2. MngKeeper API call
        var response = await _httpClient.PostAsync(
            "https://localhost:5001/api/auth/validate",
            new StringContent(JsonSerializer.Serialize(new { token }))
        );
        
        if (!response.IsSuccessStatusCode)
        {
            throw new UnauthorizedException("Invalid token");
        }
        
        var claims = await response.Content.ReadAsAsync<TokenClaims>();
        
        // 3. Cache (5 minutes)
        _cache.Set(cacheKey, claims, TimeSpan.FromMinutes(5));
        
        return claims;  // ✅ ~50ms (first time only)
    }
}
```

**Performance:**
- First validation: ~50ms (MngKeeper API call)
- Cached validation: ~1ms (memory cache)
- Cache TTL: 5 minutes

---

### Reconnection Strategy:

**Automatic Reconnection (SignalR):**
```typescript
// Frontend - Mng.UI
const connection = new signalR.HubConnectionBuilder()
  .withUrl("https://localhost:5020/ws", {
    accessTokenFactory: () => getJwtToken()
  })
  .withAutomaticReconnect({
    nextRetryDelayInMilliseconds: (retryContext) => {
      // Exponential backoff
      if (retryContext.previousRetryCount === 0) return 0;      // Immediate
      if (retryContext.previousRetryCount === 1) return 2000;   // 2s
      if (retryContext.previousRetryCount === 2) return 5000;   // 5s
      return 10000;  // 10s max
    }
  })
  .build();

connection.onreconnecting(() => {
  showReconnectingBanner();
});

connection.onreconnected(() => {
  hideReconnectingBanner();
  // Optional: Refresh data
});
```

**Missed Messages:**

**Phase 1 (Current):**
- Messages lost during disconnect
- No server-side buffer
- Frontend refreshes on reconnect
- Acceptable for real-time (non-critical) updates

**Phase 2 (Future Enhancement):**
- Server-side message buffer (per user)
- Max 100 messages buffered
- TTL: 5 minutes
- Flush on reconnection

---

### Performance Guidelines:

**Per Instance Limits:**
```
Max concurrent connections: 5,000
Connection timeout: 30 minutes idle
Heartbeat interval: 30 seconds
Max message size: 32KB
Rate limit: 100 messages/minute per connection
```

**Kestrel Configuration:**
```csharp
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxConcurrentConnections = 5000;
    options.Limits.MaxConcurrentUpgradedConnections = 5000;
    options.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(2);
});
```

**SignalR Configuration:**
```csharp
builder.Services.AddSignalR(options =>
{
    options.KeepAliveInterval = TimeSpan.FromSeconds(30);
    options.ClientTimeoutInterval = TimeSpan.FromMinutes(1);
    options.HandshakeTimeout = TimeSpan.FromSeconds(15);
    options.MaximumReceiveMessageSize = 32 * 1024;  // 32KB
});
```

**Scalability:**
- Redis backplane for multi-instance deployment
- Target: 50,000+ concurrent users
- Horizontal scaling: 10+ instances

---

### RabbitMQ Consumer Configuration:

**Per-Connection Subscription:**
```csharp
public async Task SubscribeAsync(string connectionId, string[] topics)
{
    foreach (var topic in topics)
    {
        // Create temporary, exclusive queue
        var queueName = $"ws.{connectionId}.{Guid.NewGuid()}";
        
        await _channel.QueueDeclareAsync(
            queue: queueName,
            durable: false,      // ← Ephemeral
            exclusive: true,     // ← Connection-specific
            autoDelete: true     // ← Auto-delete on disconnect
        );
        
        await _channel.QueueBindAsync(
            queue: queueName,
            exchange: "mng.topics",
            routingKey: topic
        );
        
        await _channel.BasicConsumeAsync(
            queue: queueName,
            autoAck: true,       // ← No persistence needed
            consumer: CreateConsumer(connectionId)
        );
    }
}
```

---

### Complete Message Flow Example:

**Scenario:** Task created in test-domain

```
Step 1: User creates task
  ↓
  POST https://localhost:5010/api/datasets/@tasks/data
  
Step 2: MngDataGateway
  ↓
  - Save to MongoDB
  - Check publish_mode: "full"
  - Publish to RabbitMQ:
      Exchange: mng.topics
      Routing Key: domain.test-domain.tasks.created
      
Step 3: RabbitMQ broadcasts
  ↓
  - Find bindings: domain.test-domain.#
  - Forward to temporary queues
  
Step 4: MngWebSocketGateway
  ↓
  - Consumer receives message
  - Find connections for "test-domain"
  - Filter: Only test-domain users
  
Step 5: SignalR push
  ↓
  await Clients.Users(testDomainUserIds)
      .SendAsync("ReceiveMessage", message);
      
Step 6: Mng.UI handles
  ↓
  connection.on("ReceiveMessage", (msg) => {
    refreshTaskList();
    showNotification("New task created!");
  });
```

---

### Project Structure:

```
MngWebSocketGateway/
├── Core/
│   ├── MngWebSocketGateway.Domain/
│   │   ├── Entities/
│   │   │   └── WebSocketConnection.cs
│   │   └── Enums/
│   │       └── MessageType.cs
│   └── MngWebSocketGateway.Application/
│       ├── Configuration/
│       │   └── MngWebSocketGatewaySettings.cs
│       ├── Interfaces/
│       │   ├── IConnectionManager.cs
│       │   ├── IRabbitMqConsumer.cs
│       │   └── IJwtValidator.cs
│       └── ServiceRegistration.cs
├── Infrastructure/
│   └── MngWebSocketGateway.Infrastructure/
│       ├── Services/
│       │   ├── ConnectionManager.cs
│       │   ├── RabbitMqConsumer.cs
│       │   └── JwtValidator.cs
│       └── Certificate/
│           └── CertificateHandler.cs
└── Presentation/
    └── MngWebSocketGateway.Api/
        ├── Config/
        │   └── Extensions.cs
        ├── Hubs/
        │   └── NotificationHub.cs
        ├── Middleware/
        │   └── JwtAuthenticationMiddleware.cs
        ├── Program.cs
        └── appsettings.json
```

---

### Security:

**JWT Validation:**
- ✅ MngKeeper API call for validation
- ✅ Memory cache (5 minutes TTL)
- ✅ Cache invalidation on token refresh

**Domain Isolation:**
- ✅ Users only receive messages from their domain
- ✅ Subscription validation (can't subscribe to other domains)
- ✅ Connection-level filtering

**Rate Limiting:**
- ✅ 100 messages/minute per connection
- ✅ Connection throttling
- ✅ DDoS protection

---

### Monitoring:

**Metrics:**
- Active connections count
- Messages per second
- Connection duration average
- Reconnection rate
- Message delivery latency
- RabbitMQ consumer lag

**Health Checks:**
```
GET /health
{
  "status": "healthy",
  "checks": {
    "rabbitmq": "healthy",
    "redis": "healthy",
    "mngkeeper": "healthy"
  },
  "metrics": {
    "activeConnections": 1500,
    "messagesPerSecond": 45,
    "avgLatency": "12ms"
  }
}
```

---

### Future Enhancements:

**Message Filtering (User-level):**
```
Future: Users can filter messages based on preferences
- Only assigned tasks
- Only high priority items
- Custom filter rules
```

**Message Persistence:**
```
Future: Optional message history
- Store recent messages in MongoDB
- Replay on reconnect
- Notification center
```

**Analytics:**
```
Future: Message analytics
- Delivery statistics
- User engagement metrics
- Event correlation
```

---

## 💾 Redis Cache Strategy - Domain Users & Groups

### 🎯 Problem Statement

**Performance Issue:**
```
MngDataGateway - persons field enrichment:

Scenario: 100 tasks with assignedTo field (type: persons)
  ↓
Current: 100 HTTP calls to MngKeeper
  GET /api/users/{id} × 100
  Total latency: 100 × 50ms = 5,000ms (5 seconds) 😱

Solution: Redis cache (domain-based)
  1 Redis batch read
  Total latency: ~10ms 🚀
  Performance improvement: 500x faster!
```

---

### 📋 Cache Structure Design

#### Cache Keys Pattern:
```
domain:{domain-name}:users          → Hash<user-id, UserJson>
domain:{domain-name}:groups         → Hash<group-id, GroupJson>
domain:{domain-name}:metadata       → String (last update, count)
```

#### User Hash Entry:
```
Key: domain:test-domain:users
Field: user-id-123
Value: {
  "id": "user-id-123",
  "email": "john@test-domain.com",
  "firstName": "John",
  "lastName": "Doe",
  "username": "john.doe",
  "groups": [
    { "id": "group-1", "name": "admins" },
    { "id": "group-2", "name": "managers" }
  ]
}
```

#### Group Hash Entry:
```
Key: domain:test-domain:groups
Field: group-id-1
Value: {
  "id": "group-id-1",
  "name": "admins",
  "description": "Administrators",
  "users": [
    { 
      "id": "user-id-123", 
      "email": "john@test-domain.com", 
      "firstName": "John", 
      "lastName": "Doe" 
    }
  ]
}
```

**Why Hash?**
- ✅ Partial updates (single user update)
- ✅ Efficient lookups (O(1) per user)
- ✅ Memory efficient
- ✅ Atomic operations

---

### 🔄 MngKeeper - Cache Update Strategy

**Pattern:** Immediate Update (Write-Through)

**CRUD Operations:**

#### CREATE User:
```csharp
public async Task<IActionResult> CreateUser(CreateUserRequest request)
{
    // 1. Create in Keycloak
    var userInfo = await _keycloakService.CreateUserAsync(realmName, request);
    
    // 2. Save to MongoDB
    var user = await _userRepository.AddAsync(user);
    
    // 3. Update Redis cache ← NEW
    await _domainCacheService.SetUserCacheAsync(domainName, user);
    
    // 4. Publish event (RabbitMQ) - optional
    await _eventPublisher.PublishAsync(new UserCreatedEvent { ... });
    
    return Ok(user);
}
```

#### UPDATE User:
```csharp
public async Task<IActionResult> UpdateUser(string id, UpdateUserRequest request)
{
    // 1. Update in Keycloak
    await _keycloakService.UpdateUserAsync(realmName, id, request);
    
    // 2. Update in MongoDB
    var user = await _userRepository.UpdateAsync(user);
    
    // 3. Update Redis cache ← NEW
    await _domainCacheService.SetUserCacheAsync(domainName, user);
    
    return Ok(user);
}
```

#### DELETE User:
```csharp
public async Task<IActionResult> DeleteUser(string id)
{
    var domainName = GetDomainFromToken();
    
    // Get user first (to update related caches)
    var user = await _userRepository.GetByIdAsync(id);
    
    // 1. Delete from Keycloak
    await _keycloakService.DeleteUserAsync(realmName, id);
    
    // 2. Delete from MongoDB
    await _userRepository.DeleteAsync(id);
    
    // 3. Remove from Redis cache ← NEW
    await _domainCacheService.RemoveUserCacheAsync(domainName, id);
    
    // 4. Update group caches (remove user from groups)
    foreach (var group in user.Groups)
    {
        await _domainCacheService.RemoveUserFromGroupCacheAsync(domainName, group.Id, id);
    }
    
    return Ok();
}
```

#### Add User to Group:
```csharp
public async Task<IActionResult> AddUserToGroup(string userId, string groupId)
{
    var domainName = GetDomainFromToken();
    
    // 1. Add in Keycloak
    await _keycloakService.AddUserToGroupAsync(realmName, userId, groupId);
    
    // 2. Update in MongoDB
    await _userRepository.AddToGroupAsync(userId, groupId);
    await _groupRepository.AddUserAsync(groupId, userId);
    
    // 3. Update Redis cache (bidirectional) ← NEW
    await _domainCacheService.UpdateUserGroupMembershipAsync(domainName, userId, groupId);
    
    return Ok();
}
```

---

### Bidirectional Cache Update:

```csharp
// DomainCacheService.cs
public async Task UpdateUserGroupMembershipAsync(string domainName, string userId, string groupId)
{
    // 1. Add group to user's groups list
    var userKey = $"domain:{domainName}:users";
    var userData = await _redis.HashGetAsync(userKey, userId);
    
    if (!userData.IsNullOrEmpty)
    {
        var user = JsonSerializer.Deserialize<UserCacheData>(userData);
        
        // Get group info
        var groupKey = $"domain:{domainName}:groups";
        var groupData = await _redis.HashGetAsync(groupKey, groupId);
        var group = JsonSerializer.Deserialize<GroupCacheData>(groupData);
        
        // Add group to user
        if (!user.Groups.Any(g => g.Id == groupId))
        {
            user.Groups.Add(new { Id = group.Id, Name = group.Name });
            await _redis.HashSetAsync(userKey, userId, JsonSerializer.Serialize(user));
        }
        
        // Add user to group
        if (!group.Users.Any(u => u.Id == userId))
        {
            group.Users.Add(new {
                Id = user.Id,
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName
            });
            await _redis.HashSetAsync(groupKey, groupId, JsonSerializer.Serialize(group));
        }
    }
}
```

---

### 📖 MngDataGateway - Cache Read Implementation

#### Batch User Lookup:

```csharp
// MngDataGateway - PersonsFieldEnricher.cs
public async Task<List<UserData>> EnrichPersonsFieldAsync(
    string domainName, 
    List<string> userIds)
{
    var cacheKey = $"domain:{domainName}:users";
    var users = new List<UserData>();
    
    // Batch read from Redis hash
    foreach (var userId in userIds)
    {
        var userData = await _redis.HashGetAsync(cacheKey, userId);
        
        if (!userData.IsNullOrEmpty)
        {
            users.Add(JsonSerializer.Deserialize<UserData>(userData));
        }
        else
        {
            // Cache miss - fallback to API
            var user = await GetUserFromApiAsync(domainName, userId);
            if (user != null)
            {
                users.Add(user);
            }
        }
    }
    
    return users;
}
```

#### Query Optimization Example:

```csharp
// Before cache:
var tasks = await GetTasksAsync();  // 100 tasks
foreach (var task in tasks)
{
    // 100 HTTP calls
    task.AssignedToUser = await _httpClient.GetAsync($"/api/users/{task.AssignedTo}");
}
// Total: ~5 seconds

// With cache:
var tasks = await GetTasksAsync();  // 100 tasks
var userIds = tasks.Select(t => t.AssignedTo).Distinct().ToList();  // 50 unique users
var users = await _redisService.GetUsersAsync(domainName, userIds);  // 1 call
var userDict = users.ToDictionary(u => u.Id);

foreach (var task in tasks)
{
    task.AssignedToUser = userDict.GetValueOrDefault(task.AssignedTo);
}
// Total: ~10ms 🚀
```

---

### 🔌 MngKeeper Batch API Endpoint:

```csharp
// UserController.cs
/// <summary>
/// Get multiple users by IDs (optimized for cache population)
/// </summary>
[HttpPost("batch")]
public async Task<IActionResult> GetUsersBatch([FromBody] GetUsersBatchRequest request)
{
    var domainName = GetDomainFromToken();
    
    // Try cache first
    var users = await _domainCacheService.GetUsersAsync(domainName, request.UserIds);
    
    return Ok(new
    {
        users = users,
        source = users.Count == request.UserIds.Count ? "cache" : "mixed",
        requestedCount = request.UserIds.Count,
        foundCount = users.Count
    });
}
```

**Usage from MngDataGateway:**
```csharp
// Single API call for multiple users
var response = await _httpClient.PostAsync(
    "https://localhost:5001/api/users/batch",
    new { userIds = new[] { "user-1", "user-2", "user-3" } }
);
```

---

### ⚡ Performance Benefits

**Comparison Table:**

| Scenario | Without Cache | With Cache | Improvement |
|----------|--------------|------------|-------------|
| 100 users lookup | 5,000ms (100 calls) | 10ms (1 call) | **500x** |
| 50 users lookup | 2,500ms (50 calls) | 8ms (1 call) | **312x** |
| 10 users lookup | 500ms (10 calls) | 5ms (1 call) | **100x** |
| Single user lookup | 50ms (1 call) | 2ms (1 call) | **25x** |

**Memory Usage:**
```
Average user object: ~500 bytes
100 users: ~50KB
1000 users (large domain): ~500KB
10 domains × 1000 users: ~5MB

Negligible for modern Redis instances
```

---

### 🔄 Cache Lifecycle

#### Domain Creation:
```
Step 9: Initialize Cache
  ↓
Load admin user + default groups
  ↓
Populate Redis cache
  ↓
Cache ready for use
```

#### User CRUD:
```
CREATE → Add to cache
UPDATE → Update in cache
DELETE → Remove from cache + update related groups
```

#### Group CRUD:
```
CREATE → Add to cache
UPDATE → Update in cache
DELETE → Remove from cache + update related users
ADD_USER → Update both user and group cache
REMOVE_USER → Update both user and group cache
```

---

### 🛡️ Cache Consistency Strategy

**Consistency Model:** Eventual Consistency (acceptable)

**Write Pattern:** Write-Through
```
1. Update primary source (Keycloak + MongoDB)
2. Update cache immediately
3. If cache update fails → Log error, continue
   (Cache will be refreshed on next read or periodic refresh)
```

**Read Pattern:** Cache-Aside
```
1. Check cache
2. If hit → Return from cache
3. If miss → Load from source + Update cache
```

**Conflict Resolution:**
```
Cache out of sync?
  ↓
Periodic refresh (safety net, every 1 hour)
  ↓
Or manual refresh endpoint:
  POST /api/cache/refresh?domain={domain-name}
```

---

### 🔧 Cache Service Implementation

```csharp
// MngKeeper.Infrastructure - DomainCacheService.cs
public interface IDomainCacheService
{
    // Users
    Task SetUserCacheAsync(string domainName, User user);
    Task RemoveUserCacheAsync(string domainName, string userId);
    Task<List<UserCacheData>> GetUsersAsync(string domainName, List<string> userIds);
    Task<UserCacheData?> GetUserAsync(string domainName, string userId);
    Task RefreshUsersCacheAsync(string domainName);
    
    // Groups
    Task SetGroupCacheAsync(string domainName, Group group);
    Task RemoveGroupCacheAsync(string domainName, string groupId);
    Task<List<GroupCacheData>> GetGroupsAsync(string domainName, List<string> groupIds);
    Task<GroupCacheData?> GetGroupAsync(string domainName, string groupId);
    Task RefreshGroupsCacheAsync(string domainName);
    
    // Membership
    Task UpdateUserGroupMembershipAsync(string domainName, string userId, string groupId);
    Task RemoveUserFromGroupCacheAsync(string domainName, string groupId, string userId);
    
    // Bulk operations
    Task InitializeDomainCacheAsync(string domainName);
    Task ClearDomainCacheAsync(string domainName);
    
    // Metadata
    Task<CacheMetadata> GetMetadataAsync(string domainName);
}
```

---

### 📊 Cache Metadata

```csharp
// Track cache status per domain
public class CacheMetadata
{
    public DateTime UsersLastUpdate { get; set; }
    public DateTime GroupsLastUpdate { get; set; }
    public int UsersCount { get; set; }
    public int GroupsCount { get; set; }
    public string Status { get; set; }  // "ready", "refreshing", "error"
}

// Redis key: domain:{domain-name}:metadata
{
  "usersLastUpdate": "2025-11-05T14:30:00Z",
  "groupsLastUpdate": "2025-11-05T14:30:00Z",
  "usersCount": 45,
  "groupsCount": 8,
  "status": "ready"
}
```

---

### 🎯 MngDataGateway Integration

#### Cache Read Service:

```csharp
// MngDataGateway.Infrastructure - MngKeeperCacheService.cs
public class MngKeeperCacheService : IMngKeeperCacheService
{
    private readonly IRedisService _redis;
    private readonly HttpClient _httpClient;
    
    public async Task<List<UserData>> GetUsersAsync(string domainName, List<string> userIds)
    {
        var cacheKey = $"domain:{domainName}:users";
        var users = new List<UserData>();
        var missedIds = new List<string>();
        
        // Batch read from cache
        foreach (var userId in userIds.Distinct())
        {
            var userData = await _redis.HashGetAsync(cacheKey, userId);
            
            if (!userData.IsNullOrEmpty)
            {
                users.Add(JsonSerializer.Deserialize<UserData>(userData));
            }
            else
            {
                missedIds.Add(userId);  // Cache miss
            }
        }
        
        // Fallback: API call for missed users
        if (missedIds.Any())
        {
            var fallbackUsers = await GetUsersFromApiAsync(domainName, missedIds);
            users.AddRange(fallbackUsers);
        }
        
        return users;
    }
    
    private async Task<List<UserData>> GetUsersFromApiAsync(
        string domainName, 
        List<string> userIds)
    {
        // Call MngKeeper batch endpoint
        var response = await _httpClient.PostAsJsonAsync(
            "https://localhost:5001/api/users/batch",
            new { userIds }
        );
        
        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadAsAsync<GetUsersBatchResponse>();
            return result.Users;
        }
        
        return new List<UserData>();
    }
}
```

#### persons Field Enrichment:

```csharp
// MngDataGateway - DataEnrichmentService.cs
public async Task<List<object>> EnrichDataAsync(
    string domainName,
    List<object> data,
    Dataset schema)
{
    foreach (var record in data)
    {
        foreach (var field in schema.Fields.Where(f => f.FieldType == FieldType.Persons))
        {
            var userIds = GetFieldValue(record, field.Name) as List<string>;
            
            if (userIds?.Any() == true)
            {
                // ✅ Single cache call for all users
                var users = await _mngKeeperCache.GetUsersAsync(domainName, userIds);
                SetFieldValue(record, $"{field.Name}_data", users);
            }
        }
        
        foreach (var field in schema.Fields.Where(f => f.FieldType == FieldType.PersonGroups))
        {
            var groupIds = GetFieldValue(record, field.Name) as List<string>;
            
            if (groupIds?.Any() == true)
            {
                // ✅ Single cache call for all groups
                var groups = await _mngKeeperCache.GetGroupsAsync(domainName, groupIds);
                SetFieldValue(record, $"{field.Name}_data", groups);
            }
        }
    }
    
    return data;
}
```

---

### 🔄 Cache Initialization (Domain Creation)

**MngKeeper - CreateDomainPipeline:**

```
Step 9: Initialize Domain Cache (NEW)
  ↓
After admin user and groups created:
  ↓
Load all users and groups
  ↓
Populate Redis cache:
  - domain:{domain-name}:users
  - domain:{domain-name}:groups
  - domain:{domain-name}:metadata
```

**Implementation:**
```csharp
// Step 9 in CreateDomainPipeline
public class InitializeDomainCacheStep : IDomainCreationStep
{
    public async Task<StepResult> ExecuteAsync(DomainCreationContext context)
    {
        var domainName = context.DomainName;
        
        // Load users
        var users = await _userRepository.GetByDomainAsync(domainName);
        foreach (var user in users)
        {
            await _cacheService.SetUserCacheAsync(domainName, user);
        }
        
        // Load groups
        var groups = await _groupRepository.GetByDomainAsync(domainName);
        foreach (var group in groups)
        {
            await _cacheService.SetGroupCacheAsync(domainName, group);
        }
        
        // Set metadata
        await _cacheService.UpdateMetadataAsync(domainName, new CacheMetadata
        {
            UsersLastUpdate = DateTime.UtcNow,
            GroupsLastUpdate = DateTime.UtcNow,
            UsersCount = users.Count,
            GroupsCount = groups.Count,
            Status = "ready"
        });
        
        _logger.LogInformation("Domain cache initialized: {DomainName}", domainName);
        
        return StepResult.Success();
    }
    
    public async Task RollbackAsync(DomainCreationContext context)
    {
        // Clear cache if initialization fails
        await _cacheService.ClearDomainCacheAsync(context.DomainName);
    }
}
```

---

### 🔄 Cache Refresh Mechanisms

#### Option 1: On-Demand Refresh (Primary)
```
Every CRUD operation updates cache immediately
No periodic refresh needed
```

#### Option 2: Periodic Refresh (Safety Net)
```csharp
// Background service
public class DomainCacheRefreshService : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
            
            // Get all active domains
            var domains = await _domainRepository.GetActiveDomainsAsync();
            
            foreach (var domain in domains)
            {
                try
                {
                    await _cacheService.RefreshUsersCacheAsync(domain.Name);
                    await _cacheService.RefreshGroupsCacheAsync(domain.Name);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Cache refresh failed for {Domain}", domain.Name);
                }
            }
        }
    }
}
```

#### Option 3: Manual Refresh Endpoint
```csharp
// Admin endpoint for manual cache refresh
[HttpPost("cache/refresh")]
[Authorize(Roles = "admin")]
public async Task<IActionResult> RefreshCache([FromQuery] string? domainName = null)
{
    if (string.IsNullOrEmpty(domainName))
    {
        // Refresh all domains
        var domains = await _domainRepository.GetAllAsync();
        foreach (var domain in domains)
        {
            await _cacheService.RefreshDomainCacheAsync(domain.Name);
        }
    }
    else
    {
        // Refresh specific domain
        await _cacheService.RefreshDomainCacheAsync(domainName);
    }
    
    return Ok(new { message = "Cache refreshed successfully" });
}
```

---

### 🏷️ Cache TTL Strategy

**Recommendation:** No Expiration (Manual Invalidation Only)

**Why:**
```
✅ User/Group data doesn't change frequently
✅ CRUD operations always update cache
✅ No stale data risk
✅ Memory efficient (only active domains)
✅ Predictable behavior
```

**Alternative (Hybrid):**
```
TTL: 24 hours (safety net)
On CRUD: Update cache + Reset TTL
On cache miss: Load from source + Cache
```

**Configuration:**
```csharp
// appsettings.json
"Redis": {
  "ConnectionString": "localhost:6379,password=redis123",
  "CacheSettings": {
    "DomainUsers": {
      "TTL": null,  // No expiration
      "RefreshInterval": 3600  // 1 hour (background refresh)
    },
    "DomainGroups": {
      "TTL": null,
      "RefreshInterval": 3600
    }
  }
}
```

---

### 🎯 Cache Invalidation Rules

#### User Changes:
```
CREATE User       → Add to cache
UPDATE User       → Update in cache
DELETE User       → Remove from cache + update related groups
ADD_TO_GROUP      → Update user cache + group cache
REMOVE_FROM_GROUP → Update user cache + group cache
```

#### Group Changes:
```
CREATE Group      → Add to cache
UPDATE Group      → Update in cache (name, description only)
DELETE Group      → Remove from cache + update related users
ADD_USER          → Update group cache + user cache
REMOVE_USER       → Update group cache + user cache
```

#### Domain Changes:
```
CREATE Domain     → Initialize cache
Status Change     → No cache impact (domain metadata separate)
```

**Important:** Domain immutable (no update/delete), so no complex invalidation needed!

---

### 🔍 Cache Monitoring

#### Metrics to Track:
```
- Cache hit rate (per domain)
- Cache miss rate
- Average lookup latency
- Cache size (per domain)
- Refresh frequency
- Stale data incidents
```

#### Health Check:
```
GET /api/health/cache

{
  "status": "healthy",
  "domains": [
    {
      "domainName": "test-domain",
      "usersCount": 45,
      "groupsCount": 8,
      "lastUpdate": "2025-11-05T14:30:00Z",
      "status": "ready",
      "hitRate": 98.5,
      "avgLatency": "2ms"
    }
  ],
  "totalMemory": "5.2MB"
}
```

---

### 🎯 Implementation Priority

#### Phase 1: Basic Cache ✅
- User hash cache (domain:{domain}:users)
- Group hash cache (domain:{domain}:groups)
- CRUD integration
- Cache initialization on domain creation

#### Phase 2: Optimization 🟡
- Batch operations
- Cache metadata
- Monitoring metrics

#### Phase 3: Advanced 🟢
- Periodic refresh (background service)
- Manual refresh endpoint
- Cache analytics

---

### 🚀 Benefits Summary

**Performance:**
- ✅ 500x faster user lookups
- ✅ Sub-10ms latency
- ✅ Reduced MngKeeper load
- ✅ Better user experience

**Scalability:**
- ✅ Handles thousands of users per domain
- ✅ Minimal memory footprint
- ✅ Horizontally scalable (Redis cluster)

**Reliability:**
- ✅ Fallback to API on cache miss
- ✅ Automatic refresh on CRUD
- ✅ Consistent data model

**Maintainability:**
- ✅ Simple cache schema
- ✅ Clear invalidation rules
- ✅ Easy to debug

---

## 🎯 Implementation Priority

### Phase 1: System Topic ✅
- CreateDomain publishes to system.mngkeeper.domain.created
- MngDataGateway consumes for @datasets initialization

### Phase 2: Redis Cache (Users & Groups) 🔴
- MngKeeper cache service implementation
- CRUD integration
- Cache initialization on domain creation
- MngDataGateway cache read service

### Phase 3: MngWebSocketGateway 🟡
- Create new microservice
- SignalR hub implementation
- JWT validation
- RabbitMQ integration

### Phase 4: Domain Topic 🟢
- MngDataGateway publish_mode implementation
- Real-time data updates
- Frontend integration

### Phase 5: Global Topic 🟢
- Admin panel (announcement system)
- Multi-language support
- Display options

---

### 📋 Production Deployment Checklist

#### Security
- [ ] JWT validation aktif
- [ ] CORS policy kısıtlı
- [ ] HTTPS zorunlu (HTTP kapalı)
- [ ] API keys/secrets environment variable'dan
- [ ] Certificate doğru yükleniyor

#### Performance
- [ ] Rate limiting aktif
- [ ] Connection pooling yapılandırıldı
- [ ] Response caching (opsiyonel)
- [ ] MongoDB index'ler oluşturuldu

#### Monitoring
- [ ] Health checks çalışıyor
- [ ] Structured logging aktif
- [ ] Metrics collection
- [ ] Error tracking
- [ ] Performance monitoring

#### Reliability
- [ ] Circuit breaker pattern (external API'ler için)
- [ ] Retry policies
- [ ] Timeout configuration
- [ ] Graceful shutdown

#### Documentation
- [ ] API documentation güncel
- [ ] Deployment guide hazır
- [ ] Environment variables dokümante edildi
- [ ] Troubleshooting guide

---

## 🎉 Özet

MngDataGateway, **sofistike ve esnek** bir veri yönetim sistemi sunacak:

✅ **Dynamic Schema** - Runtime'da dataset tanımlama  
✅ **Multi-tenant** - Domain bazlı izolasyon  
✅ **Flexible/Strict** - forceSchema seçeneği  
✅ **Rich Field Types** - 9 farklı tip  
✅ **Auto-increment** - Format desteği ile  
✅ **Predefined Queries** - Parametrik aggregation  
✅ **External Validation** - HTTP-based  
✅ **Event-Driven** - RabbitMQ publishing  
✅ **Clean Architecture** - Maintainable, testable  

**Bu roadmap, projenin temelini oluşturuyor. Endpoint tasarımı ve implementasyon detayları sırada!** 🚀

