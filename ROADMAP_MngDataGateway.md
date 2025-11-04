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

#### 2. Health Check Endpoint
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

#### 3. Rate Limiting
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

#### 4. Metrics & Monitoring
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

#### 5. Environment Variable Validation
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

#### 6. Certificate Loading Test
**Durum:** ⚪ Test edilmedi

**Yapılacak:**
- File-based certificate loading test
- Environment variable-based loading test
- PEM parsing test
- RSA key import test

**Öncelik:** 🟡 Orta

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

