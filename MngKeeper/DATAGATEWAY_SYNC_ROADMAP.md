# MngKeeper - DataGateway Sync Implementation Roadmap

**Date:** 10 Aralık 2025  
**Status:** Planning  
**Goal:** MngKeeper'dan MngDataGateway MongoDB'ye direkt sync (event-driven yerine)

---

## 📋 Genel Bakış

### Mevcut Durum (Yanlış Yaklaşım)
```
MngKeeper:
  1. Keycloak'a user oluştur
  2. MongoDB'ye user yaz (mngkeeper DB)
  3. RabbitMQ event publish

MngDataGateway:
  4. RabbitMQ event dinle
  5. MongoDB'ye user yaz (mng_{domain} DB)
```

**Sorunlar:**
- ❌ RabbitMQ bağımlılığı (ekstra altyapı)
- ❌ Event kaybı riski
- ❌ Eventual consistency (gecikme)
- ❌ Karmaşıklık (event consumer, retry, DLQ)
- ❌ Custom data desteği zor

---

### Yeni Yaklaşım (Doğru)
```
MngKeeper:
  1. Keycloak'a user oluştur (custom data YOK)
  2. MongoDB'ye user yaz (mngkeeper DB)
  3. MngDataGateway MongoDB'ye user yaz (mng_{domain} DB) + custom data
  4. RabbitMQ event publish (opsiyonel, notification için)
```

**Avantajlar:**
- ✅ Daha basit (event consumer yok)
- ✅ Transaction garantisi (aynı işlem)
- ✅ Daha hızlı (network hop yok)
- ✅ Daha güvenilir (event kaybı riski yok)
- ✅ Custom data desteği kolay
- ✅ RabbitMQ opsiyonel (sadece notification)

---

## 🎯 Implementation Plan

### Phase 1: DataGateway User/Group Service

**Amaç:** MngKeeper'dan MngDataGateway MongoDB'ye direkt yazma servisi

**Interface:**
```csharp
// Core/MngKeeper.Application/Interfaces/IDataGatewaySyncService.cs
public interface IDataGatewaySyncService
{
    Task SyncUserToDataGatewayAsync(
        User user, 
        Dictionary<string, object>? customData = null);
    
    Task SyncGroupToDataGatewayAsync(
        Group group, 
        Dictionary<string, object>? customData = null);
    
    Task SyncAllUsersAsync(string domainId);
    Task SyncAllGroupsAsync(string domainId);
}
```

**Implementation:**
```csharp
// Infrastructure/MngKeeper.Infrastructure/Services/DataGatewaySyncService.cs
public class DataGatewaySyncService : IDataGatewaySyncService
{
    private readonly IMongoClient _mongoClient;
    private readonly IDomainRepository _domainRepository;
    
    // MngDataGateway MongoDB connection (aynı MongoDB instance)
    // Configuration'dan alınacak
}
```

**Özellikler:**
- Domain database'ini hesaplar: `mng_{domainName}`
- `@users` collection'ına yazar
- `@groups` collection'ına yazar
- Custom data desteği
- Upsert (create/update)

---

### Phase 2: User/Group CRUD Integration

**CreateUserCommandHandler Güncellemesi:**
```csharp
// CreateUserCommandHandler.cs
public async Task<CreateUserResponse> Handle(...)
{
    // 1. Keycloak'a user oluştur (custom data YOK)
    var keycloakUser = await _keycloakService.CreateUserAsync(...);
    
    // 2. MongoDB'ye user yaz (mngkeeper DB)
    var savedUser = await _userRepository.AddAsync(user);
    
    // 3. MngDataGateway MongoDB'ye user yaz (mng_{domain} DB) + custom data
    await _dataGatewaySyncService.SyncUserToDataGatewayAsync(
        savedUser, 
        request.CustomData); // ← Yeni field
    
    // 4. RabbitMQ event publish (opsiyonel, notification için)
    await _eventPublisher.PublishAsync(userCreatedEvent, domainId);
    
    return response;
}
```

**UpdateUserCommandHandler Güncellemesi:**
- Aynı şekilde `SyncUserToDataGatewayAsync` çağrılacak

**DeleteUserCommandHandler Güncellemesi:**
- Soft delete için `SyncUserToDataGatewayAsync` çağrılacak (IsDeleted = true)

**Group CRUD için aynı pattern:**
- `CreateGroupCommandHandler`
- `UpdateGroupCommandHandler`
- `DeleteGroupCommandHandler`

---

### Phase 3: Domain Creation Pipeline Güncelleme

**Yeni Step: InitializeDataGatewayCollectionsStep**

Domain oluşturulurken:
```csharp
// Step 12: Initialize DataGateway Collections
public class InitializeDataGatewayCollectionsStep : IPipelineStep<DomainCreationContext>
{
    public async Task<StepResult> ExecuteAsync(...)
    {
        // mng_{domain} database'inde @users ve @groups collection'larını oluştur
        // Index'leri oluştur
        // BaseEntity pattern için hazırla
    }
}
```

**Pipeline'a ekle:**
```csharp
_pipeline
    .AddStep(validateDomainStep)
    .AddStep(createDomainEntityStep)
    .AddStep(createDatabaseStep)                    // mngkeeper DB
    .AddStep(createDataGatewayDatabaseStep)         // mng_{domain} DB ← YENİ
    .AddStep(initializeDatabaseCollectionsStep)     // mngkeeper collections
    .AddStep(initializeDataGatewayCollectionsStep)  // mng_{domain} collections ← YENİ
    .AddStep(createKeycloakRealmStep)
    // ...
```

---

### Phase 4: Configuration

**MngKeeperSettings'e ekle:**
```json
{
  "MngKeeperSettings": {
    "MongoDB": {
      "ConnectionString": "mongodb://admin:admin123@localhost:27017",
      "DataGatewayDatabasePrefix": "mng_"  // Default: "mng_"
    }
  }
}
```

**Not:** MngKeeper ve MngDataGateway aynı MongoDB instance'ını kullanıyor, sadece database farklı.

---

### Phase 5: Custom Data Support

**CreateUserCommand'a ekle:**
```csharp
public class CreateUserCommand
{
    // ... mevcut field'lar
    public Dictionary<string, object>? CustomData { get; set; }
}
```

**UserSync Entity (MngDataGateway format):**
```csharp
// MngKeeper'da da aynı entity kullanılabilir veya DTO
public class DataGatewayUserSync
{
    public string __dataId { get; set; }  // MngKeeper User._id
    public string KeycloakUserId { get; set; }
    public string Username { get; set; }
    public string Email { get; set; }
    // ... sync data
    
    // Custom data (flexible)
    public Dictionary<string, object>? CustomData { get; set; }
}
```

---

## 📊 Veri Akışı

### User Creation Flow

```
1. POST /api/user (CreateUserCommand)
   ↓
2. Keycloak'a user oluştur (custom data YOK)
   ↓
3. MongoDB'ye user yaz (mngkeeper.users)
   ↓
4. MngDataGateway MongoDB'ye user yaz (mng_{domain}.@users) + custom data
   ↓
5. RabbitMQ event publish (notification için, opsiyonel)
```

### User Update Flow

```
1. PUT /api/user/{id} (UpdateUserCommand)
   ↓
2. Keycloak'ı güncelle
   ↓
3. MongoDB'yi güncelle (mngkeeper.users)
   ↓
4. MngDataGateway MongoDB'yi güncelle (mng_{domain}.@users) + custom data
   ↓
5. RabbitMQ event publish (notification için)
```

---

## 🔧 Technical Details

### MongoDB Connection

**MngKeeper zaten MongoDB'ye bağlı:**
- Connection string: `MngKeeperSettings.MongoDB.ConnectionString`
- Aynı connection string kullanılacak
- Sadece database name değişecek: `mng_{domainName}`

### Database Structure

```
MongoDB Instance:
├── mngkeeper (MngKeeper'ın database'i)
│   ├── domains
│   ├── users
│   └── groups
│
└── mng_seven (Domain-specific, MngDataGateway için)
    ├── @users (sync edilen user'lar)
    ├── @groups (sync edilen group'lar)
    └── @datasets, @tasks, ... (MngDataGateway data)
```

### Collection Schema

**@users Collection (mng_{domain}):**
```json
{
  "__dataId": "mngkeeper-user-objectid",
  "keycloakUserId": "keycloak-uuid",
  "username": "serkan",
  "email": "serkan@seven.com",
  "firstName": "Serkan",
  "lastName": "MERAL",
  "isActive": true,
  "domainId": "domain-objectid",
  "groups": ["group-id-1"],
  
  // Custom data (optional)
  "customData": {
    "phone": "+90 555 123 4567",
    "department": "IT",
    "position": "Senior Developer"
  },
  
  // Sync metadata
  "__syncInfo": {
    "lastSyncedAt": "2025-12-10T15:30:00Z",
    "syncSource": "mngkeeper",
    "syncVersion": 1
  },
  
  // BaseEntity metadata
  "__createInfo": {...},
  "__lastUpdateInfo": {...},
  "__isDeleted": false
}
```

**@groups Collection (mng_{domain}):**
```json
{
  "__dataId": "mngkeeper-group-objectid",
  "name": "admins",
  "description": "Administrators",
  "permissions": ["read", "write"],
  "domainId": "domain-objectid",
  
  // Custom data (optional)
  "customData": {
    "color": "#FF0000",
    "icon": "admin-icon"
  },
  
  // Sync metadata
  "__syncInfo": {...},
  
  // BaseEntity metadata
  "__createInfo": {...},
  "__lastUpdateInfo": {...},
  "__isDeleted": false
}
```

---

## 📝 Implementation Steps

### Step 1: Create IDataGatewaySyncService Interface

**File:** `Core/MngKeeper.Application/Interfaces/IDataGatewaySyncService.cs`

**Methods:**
- `SyncUserToDataGatewayAsync(User user, Dictionary<string, object>? customData = null)`
- `SyncGroupToDataGatewayAsync(Group group, Dictionary<string, object>? customData = null)`
- `SyncAllUsersAsync(string domainId)` - Manual sync endpoint için
- `SyncAllGroupsAsync(string domainId)` - Manual sync endpoint için

---

### Step 2: Implement DataGatewaySyncService

**File:** `Infrastructure/MngKeeper.Infrastructure/Services/DataGatewaySyncService.cs`

**Dependencies:**
- `IMongoClient` (MngKeeper'ın mevcut MongoDB client'ı)
- `IDomainRepository` (domain bilgisi için)

**Logic:**
1. Domain bilgisini al (domainId'den)
2. Database name hesapla: `mng_{domainName}`
3. MongoDB'ye yaz (`@users` veya `@groups` collection)
4. Upsert pattern (create/update)

---

### Step 3: Update CreateUserCommand

**File:** `Core/MngKeeper.Application/Features/User/Commands/CreateUser/CreateUserCommand.cs`

**Add:**
```csharp
public Dictionary<string, object>? CustomData { get; set; }
```

---

### Step 4: Update CreateUserCommandHandler

**File:** `Core/MngKeeper.Application/Features/User/Commands/CreateUser/CreateUserCommandHandler.cs`

**Add after line 113:**
```csharp
// Sync to DataGateway MongoDB
await _dataGatewaySyncService.SyncUserToDataGatewayAsync(
    savedUser, 
    request.CustomData);
```

---

### Step 5: Update UpdateUserCommandHandler

**File:** `Core/MngKeeper.Application/Features/User/Commands/UpdateUser/UpdateUserCommandHandler.cs`

**Add after user update:**
```csharp
// Sync to DataGateway MongoDB
await _dataGatewaySyncService.SyncUserToDataGatewayAsync(
    updatedUser, 
    request.CustomData);
```

---

### Step 6: Update DeleteUserCommandHandler

**File:** `Core/MngKeeper.Application/Features/User/Commands/DeleteUser/DeleteUserCommandHandler.cs`

**Add after user delete:**
```csharp
// Sync to DataGateway MongoDB (soft delete)
await _dataGatewaySyncService.SyncUserToDataGatewayAsync(
    deletedUser, 
    null);
```

---

### Step 7: Update Group Command Handlers

**Same pattern for:**
- `CreateGroupCommandHandler`
- `UpdateGroupCommandHandler`
- `DeleteGroupCommandHandler`

---

### Step 8: Add Manual Sync Endpoints

**File:** `Presentation/MngKeeper.Api/Controllers/SyncController.cs`

**Endpoints:**
```csharp
[HttpPost("users/sync")]
public async Task<IActionResult> SyncUsers()

[HttpPost("groups/sync")]
public async Task<IActionResult> SyncGroups()

[HttpPost("all/sync")]
public async Task<IActionResult> SyncAll()
```

---

### Step 9: Domain Creation Pipeline Update

**Add new step:**
```csharp
// Step 12: Initialize DataGateway Collections
public class InitializeDataGatewayCollectionsStep : IPipelineStep<DomainCreationContext>
{
    public async Task<StepResult> ExecuteAsync(...)
    {
        // mng_{domain} database'inde:
        // 1. @users collection oluştur
        // 2. @groups collection oluştur
        // 3. Index'leri oluştur
    }
}
```

---

## 🔄 Migration Strategy

### MngDataGateway'den Kaldırılacaklar

1. **Event Consumer:**
   - `MngKeeperEventConsumer.cs` → Kaldır veya sadece notification için bırak
   - `IMngKeeperEventConsumer.cs` → Kaldır
   - `MngKeeperEventHandler.cs` → Kaldır
   - `IMngKeeperEventHandler.cs` → Kaldır

2. **Sync Service:**
   - `MngKeeperSyncService.cs` → Kaldır (MngKeeper'a taşındı)
   - `IMngKeeperSyncService.cs` → Kaldır
   - `SyncController.cs` → Kaldır (MngKeeper'a taşındı)

3. **Event DTOs:**
   - `MngKeeperEventDto.cs` → Kaldır (artık gerek yok)

4. **Service Registration:**
   - Event consumer registration → Kaldır
   - Sync service registration → Kaldır

### MngKeeper'a Eklenecekler

1. **DataGateway Sync Service:**
   - `IDataGatewaySyncService.cs` → Yeni
   - `DataGatewaySyncService.cs` → Yeni

2. **Command Updates:**
   - `CreateUserCommand` → CustomData field ekle
   - `UpdateUserCommand` → CustomData field ekle
   - `CreateGroupCommand` → CustomData field ekle
   - `UpdateGroupCommand` → CustomData field ekle

3. **Command Handler Updates:**
   - Tüm user/group command handler'larına sync çağrısı ekle

4. **Domain Creation Pipeline:**
   - `InitializeDataGatewayCollectionsStep` → Yeni step

5. **Sync Controller:**
   - `SyncController.cs` → Yeni (manual sync için)

---

## ✅ Avantajlar

1. **Basitlik:** Event consumer yok, direkt yazma
2. **Güvenilirlik:** Transaction garantisi, event kaybı yok
3. **Performans:** Network hop yok, daha hızlı
4. **Custom Data:** Kolay custom data desteği
5. **Maintainability:** Tek yerden yönetim (MngKeeper)

---

## ⚠️ Dikkat Edilmesi Gerekenler

1. **MongoDB Connection:** MngKeeper ve MngDataGateway aynı MongoDB instance'ını kullanmalı
2. **Database Naming:** `mng_{domainName}` format'ı tutarlı olmalı
3. **Error Handling:** DataGateway sync başarısız olursa ne olacak? (Rollback veya retry)
4. **Custom Data Validation:** Custom data format'ı nasıl validate edilecek?

---

## 🚀 Implementation Priority

### 🔴 Yüksek Öncelik
1. `IDataGatewaySyncService` interface ve implementation
2. `CreateUserCommandHandler` güncelleme
3. `CreateGroupCommandHandler` güncelleme

### 🟡 Orta Öncelik
4. Update/Delete handler'ları güncelleme
5. Domain creation pipeline güncelleme
6. Manual sync endpoints

### 🟢 Düşük Öncelik
7. Custom data validation
8. Error handling ve retry mechanism
9. Performance optimizasyonları

---

**Son Güncelleme:** 10 Aralık 2025  
**Status:** Planning - Implementation'a hazır

