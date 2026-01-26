# persons/personGroups Field Type Implementation Plan

**Date:** 10 Aralık 2025  
**Status:** Planning - Senaryo 3 (MongoDB + $lookup)  
**Approach:** MongoDB'ye kopyalama + Aggregate Pipeline $lookup

---

## 📋 Genel Bakış

### Senaryo Seçimi: **Senaryo 3 - MongoDB + $lookup**

**Neden:**
- ✅ Relation expansion ile tutarlı mimari
- ✅ En iyi performans (tek sorgu)
- ✅ MngKeeper bağımlılığı yok
- ✅ Batch işlem desteği
- ✅ MongoDB native özellikleri

---

## 🗂️ Veri Kaynakları ve Sorumlulukları

### 1. Keycloak (Authentication & Authorization)

**Tutulan Veriler (JWT Claims):**
```json
{
  "sub": "keycloak-user-uuid",           // Keycloak user ID
  "preferred_username": "serkan",         // Username
  "email": "serkan@seven.com",           // Email
  "domain_name": "seven",                 // Domain name
  "domain_id": "690cda3aae502df7d3330bba", // Domain ID
  "isAdmin": true,                        // Admin flag
  "user_groups": ["admins"]               // Group names
}
```

**Sorumluluk:**
- Authentication (login, token generation)
- Authorization (roles, permissions)
- JWT token claims
- Password management
- Session management

**Değişiklik Sıklığı:** Düşük (login, password change, role update)

---

### 2. MngKeeper MongoDB (Master User Data)

**Tutulan Veriler:**
```json
{
  "_id": "ObjectId(...)",                // MongoDB ObjectId
  "domainId": "690cda3aae502df7d3330bba",
  "keycloakUserId": "keycloak-uuid",     // Keycloak'taki user ID
  "username": "serkan",
  "email": "serkan@seven.com",
  "firstName": "Serkan",
  "lastName": "MERAL",
  "isActive": true,
  "groups": ["group-id-1", "group-id-2"], // Group ObjectId'leri
  "roles": ["admin", "user"],
  "createdAt": "2025-12-10T10:00:00Z",
  "lastLoginAt": "2025-12-10T15:30:00Z",
  "createdBy": "system",
  "updatedAt": "2025-12-10T14:00:00Z",
  "updatedBy": "admin-user-id"
}
```

**Sorumluluk:**
- User CRUD operations
- User-Group relationships
- Domain-specific user data
- Business logic metadata

**Değişiklik Sıklığı:** Orta (user update, group assignment)

---

### 3. MngDataGateway MongoDB (Sync Copy)

**Tutulan Veriler (MngKeeper'dan Sync):**
```json
{
  "__dataId": "mngkeeper-objectid",       // MngKeeper'daki _id (unique identifier)
  "keycloakUserId": "keycloak-uuid",     // Keycloak user ID
  "username": "serkan",
  "email": "serkan@seven.com",
  "firstName": "Serkan",
  "lastName": "MERAL",
  "isActive": true,
  "domainId": "690cda3aae502df7d3330bba",
  "groups": ["group-id-1", "group-id-2"], // Group ObjectId'leri (MngKeeper'dan)
  
  // Sync metadata
  "__syncInfo": {
    "lastSyncedAt": "2025-12-10T15:30:00Z",
    "syncSource": "mngkeeper",
    "syncVersion": 1
  },
  
  // BaseEntity metadata
  "__createInfo": {
    "createdAt": "2025-12-10T10:00:00Z",
    "userInfo": {
      "uid": "system",
      "userName": "system",
      "domain": "seven"
    }
  },
  "__lastUpdateInfo": {
    "updatedAt": "2025-12-10T15:30:00Z",
    "userInfo": {
      "uid": "system",
      "userName": "system",
      "domain": "seven"
    }
  }
}
```

**Not:** Extended data şu an planlanmamış. Gelecekte gerekirse mevcut data CRUD API'leri kullanılarak eklenebilir.

**Sorumluluk:**
- MngKeeper'dan sync (event-driven)
- persons/personGroups field expansion için lookup source
- Read-only (sadece sync ile güncellenir)

**Değişiklik Sıklığı:**
- Sync: Event-driven (MngKeeper değişikliklerinde)

---

## 🔄 Sync Mekanizması

### Event-Driven Sync (RabbitMQ)

**MngKeeper → RabbitMQ Events:**
```
Exchange: monitra.user.events.{domain}
Routing Keys:
  - user.created
  - user.updated
  - user.deleted
  - user.activated
  - user.deactivated
```

**Event Payload Örneği:**
```json
{
  "id": "event-uuid",
  "type": "user.updated",
  "domainId": "690cda3aae502df7d3330bba",
  "timestamp": "2025-12-10T15:30:00Z",
  "data": {
    "userId": "mngkeeper-objectid",
    "keycloakUserId": "keycloak-uuid",
    "username": "serkan",
    "email": "serkan@seven.com",
    "firstName": "Serkan",
    "lastName": "MERAL",
    "isActive": true,
    "groups": ["group-id-1", "group-id-2"]
  }
}
```

**MngDataGateway Event Consumer:**
```csharp
// RabbitMQ consumer → MongoDB'ye yaz
public class UserEventConsumer
{
    public async Task HandleUserCreated(UserCreatedEvent @event)
    {
        // 1. Event'ten user data al
        // 2. MongoDB'ye yaz (@users collection)
        // 3. __dataId = MngKeeper'daki _id (event.UserId)
        // 4. Retry mechanism ile hata durumunda tekrar dene
    }
    
    public async Task HandleUserUpdated(UserUpdatedEvent @event)
    {
        // 1. MongoDB'deki kaydı güncelle
        // 2. __syncInfo.lastSyncedAt güncelle
        // 3. Retry mechanism ile hata durumunda tekrar dene
    }
    
    public async Task HandleUserDeleted(UserDeletedEvent @event)
    {
        // 1. Hard delete: MongoDB'den kaydı sil
        // 2. Arşiv: __deletedDatas collection'ına taşı (TTL ile)
        // 3. Retry mechanism ile hata durumunda tekrar dene
    }
}
```

**Sync Failure Handling:**
- Retry mechanism: Exponential backoff (3 retry, max 5 dakika)
- Dead letter queue: Başarısız event'ler için
- Error logging: Detaylı hata logları
- Manual sync endpoint: Gerekirse manuel sync için

---

## 📊 MongoDB Collection Yapısı

### Collection: `@users`

**Location:** `mng_{domain}` database

**Schema:**
```json
{
  "__dataId": "string",              // MngKeeper _id (unique, index)
  "keycloakUserId": "string",       // Keycloak user ID (index)
  "username": "string",              // Username (index, unique)
  "email": "string",                // Email (index, unique)
  "firstName": "string",
  "lastName": "string",
  "isActive": "boolean",
  "domainId": "string",
  
  // Sync metadata
  "__syncInfo": {
    "lastSyncedAt": "datetime",
    "syncSource": "string",
    "syncVersion": "number"
  },
  
  // BaseEntity metadata
  "__createInfo": {...},
  "__lastUpdateInfo": {...},
  "__history": [...]
}
```

**Indexes:**
```javascript
db.getCollection("@users").createIndex({ "__dataId": 1 }, { unique: true });
db.getCollection("@users").createIndex({ "keycloakUserId": 1 });
db.getCollection("@users").createIndex({ "username": 1 }, { unique: true });
db.getCollection("@users").createIndex({ "email": 1 }, { unique: true });
db.getCollection("@users").createIndex({ "domainId": 1 });
```

---

### Collection: `@groups`

**Location:** `mng_{domain}` database

**Schema:**
```json
{
  "__dataId": "string",              // MngKeeper _id (unique, index)
  "name": "string",                  // Group name (index, unique)
  "description": "string",
  "permissions": ["string"],
  "domainId": "string",
  
  // Sync metadata
  "__syncInfo": {...},
  
  // BaseEntity metadata
  "__createInfo": {...},
  "__lastUpdateInfo": {...},
  "__history": [...]
}
```

**Indexes:**
```javascript
db.getCollection("@groups").createIndex({ "__dataId": 1 }, { unique: true });
db.getCollection("@groups").createIndex({ "name": 1 }, { unique: true });
db.getCollection("@groups").createIndex({ "domainId": 1 });
```

---

## 🔧 Aggregate Pipeline Implementation

### persons Field Expansion

**Mevcut AggregatePipelineBuilder'a Ekleme:**
```csharp
public AggregatePipelineBuilder AddPersonExpansion(
    bool expand = true,
    string domainName)
{
    if (!expand)
        return this;
    
    var personFields = _schema.fields
        .Where(f => f.fieldType == "persons")
        .ToList();
    
    foreach (var field in personFields)
    {
        if (field.isArray)
        {
            // Array persons field
            var lookup = new BsonDocument
            {
                ["from"] = "@users",
                ["let"] = new BsonDocument(field.name, $"${field.name}"),
                ["pipeline"] = new BsonArray
                {
                    new BsonDocument
                    {
                        ["$match"] = new BsonDocument
                        {
                            ["$expr"] = new BsonDocument
                            {
                                ["$in"] = new BsonArray
                                {
                                    "$__dataId",
                                    $"$${field.name}"
                                }
                            }
                        }
                    },
                    new BsonDocument
                    {
                        ["$project"] = new BsonDocument
                        {
                            ["_id"] = 0,
                            ["__history"] = 0
                        }
                    }
                },
                ["as"] = field.name
            };
            
            _pipeline.Add(new BsonDocument("$lookup", lookup));
        }
        else
        {
            // Single person field
            var lookup = new BsonDocument
            {
                ["from"] = "@users",
                ["localField"] = field.name,
                ["foreignField"] = "__dataId",
                ["as"] = field.name,
                ["pipeline"] = new BsonArray
                {
                    new BsonDocument
                    {
                        ["$project"] = new BsonDocument
                        {
                            ["_id"] = 0,
                            ["__history"] = 0
                        }
                    }
                }
            };
            
            _pipeline.Add(new BsonDocument("$lookup", lookup));
        }
    }
    
    return this;
}
```

### personGroups Field Expansion

**Benzer implementasyon:**
```csharp
public AggregatePipelineBuilder AddPersonGroupExpansion(
    bool expand = true,
    string domainName)
{
    // Similar to AddPersonExpansion, but lookup from "@groups"
    // ...
}
```

---

## 📝 Implementation Steps

### Phase 1: MongoDB Schema & Collections

1. ✅ `@users` collection schema tanımla
2. ✅ `@groups` collection schema tanımla
3. ✅ Index'leri oluştur
4. ✅ BaseEntity pattern uygula

### Phase 2: Event Consumer

1. ✅ RabbitMQ consumer service oluştur
2. ✅ User events handle et (created, updated, deleted)
3. ✅ Group events handle et (created, updated, deleted)
4. ✅ MongoDB'ye sync yap

### Phase 3: Aggregate Pipeline

1. ✅ `AddPersonExpansion()` method ekle
2. ✅ `AddPersonGroupExpansion()` method ekle
3. ✅ Array ve single field desteği
4. ✅ Soft delete filter ekle

### Phase 4: Testing

1. ✅ Event sync test
2. ✅ $lookup expansion test
3. ✅ Extended data CRUD test
4. ✅ Performance test

---

## 🎯 Extended Data Yönetimi (Gelecek)

**Not:** Extended data şu an planlanmamış. Gelecekte gerekirse:
- Mevcut data CRUD API'leri kullanılabilir (`/api/data/@users/{__dataId}`)
- Dataset schema'da `@users` ve `@groups` için field'lar tanımlanabilir
- Normal dataset gibi yönetilebilir

---

## 🔐 Güvenlik ve Yetkilendirme

### Sync Data Protection
- Sync data (MngKeeper'dan gelen) sadece event consumer tarafından yazılabilir
- API endpoint'leri ile değiştirilemez (read-only)
- Sync data değişiklikleri için MngKeeper API kullanılmalı
- Event consumer retry mechanism ile güvenilir sync sağlanır

---

## 📊 Veri Akışı Diyagramı

```
┌─────────────┐
│  Keycloak   │  ← Authentication & JWT Claims
└──────┬──────┘
       │
       ↓
┌─────────────┐
│ MngKeeper   │  ← Master User Data
│  MongoDB    │
└──────┬──────┘
       │
       │ RabbitMQ Event
       ↓
┌──────────────────┐
│ MngDataGateway   │  ← Sync Copy + Extended Data
│     MongoDB      │
│   (@users)       │
└────────┬─────────┘
         │
         │ $lookup
         ↓
┌──────────────────┐
│ Aggregate Query  │  ← persons/personGroups expansion
│     Result       │
└──────────────────┘
```

---

## 🚀 Avantajlar

1. **Performans:** Tek MongoDB sorgusu, network overhead yok
2. **Güvenilirlik:** MngKeeper down olsa bile çalışır
3. **Tutarlılık:** Relation expansion ile aynı pattern
4. **Esneklik:** Extended data ile business logic genişletilebilir
5. **Ölçeklenebilirlik:** MongoDB native özellikleri

---

## ⚠️ Dikkat Edilmesi Gerekenler

1. **Eventual Consistency:** Sync gecikmesi olabilir (normal)
2. **Storage:** Veri duplikasyonu (kabul edilebilir trade-off)
3. **Sync Failure:** Retry mechanism gerekli
4. **Data Integrity:** __dataId unique olmalı (MngKeeper _id)

---

**Son Güncelleme:** 10 Aralık 2025  
**Status:** Planning Complete - Implementation'a hazır

