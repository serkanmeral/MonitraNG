# MngDataGateway - Proje Durumu

**Son Güncelleme:** 3 Kasım 2025

---

## ✅ Tamamlanan İşlemler

### 🏗️ Proje Yapısı (Clean Architecture)

```
MngDataGateway/
├── Core/
│   ├── MngDataGateway.Domain/          ✅ Oluşturuldu
│   └── MngDataGateway.Application/     ✅ Oluşturuldu
├── Infrastructure/
│   ├── MngDataGateway.Infrastructure/  ✅ Oluşturuldu
│   └── MngDataGateway.Persistence/     ✅ Oluşturuldu
├── Presentation/
│   └── MngDataGateway.Api/            ✅ Oluşturuldu
├── MngDataGateway.sln                  ✅ Oluşturuldu
├── README.md                           ✅ Oluşturuldu
└── ROADMAP_MngDataGateway.md          ✅ Oluşturuldu (1343 satır)
```

---

### 📦 Domain Layer

**Dosyalar:**
- ✅ `Exceptions/DataGatewayException.cs`
  - `DataGatewayException` (base)
  - `ValidationException`
  - `NotFoundException`
  - `UnauthorizedException`

---

### 📦 Application Layer

**Configuration (IOptions Pattern):**
- ✅ `Configuration/MongoDbOptions.cs`
  - ConnectionString
  - DatabaseName
- ✅ `Configuration/RabbitMqOptions.cs`
  - Host, Port
  - Username, Password
  - VirtualHost

**Paketler:**
- ✅ MediatR 13.0.0
- ✅ FluentValidation.AspNetCore 11.3.1

---

### 📦 Infrastructure Layer

**Paketler:**
- ✅ MongoDB.Driver 3.3.0
- ✅ RabbitMQ.Client 7.0.0

**Not:** Servis implementasyonları henüz eklenmedi (gerektiğinde eklenecek)

---

### 📦 Persistence Layer

**Paketler:**
- ✅ MongoDB.Driver 3.3.0

**Not:** Repository implementasyonları henüz eklenmedi (gerektiğinde eklenecek)

---

### 📦 API Layer (Presentation)

**Controllers:**
- ✅ `Controllers/VersionController.cs`
  - `GET /api/version` - Detaylı versiyon bilgisi
  - `GET /api/version/short` - Kısa versiyon

**Middleware:**
- ✅ `Middleware/GlobalExceptionHandlerMiddleware.cs`
  - Exception handling
  - Structured error responses
  - Logging integration

**Configuration:**
- ✅ `Program.cs`
  - Serilog yapılandırması (Console + Seq)
  - MongoDB client yapılandırması
  - IOptions<> pattern kullanımı
  - Swagger yapılandırması
  - Global exception handler
  - MediatR registration (hazır, kullanılacak)
  
- ✅ `appsettings.json`
  - Serilog (Console + Seq)
  - MongoDB connection
  - RabbitMQ connection
  - HTTP endpoint: port 5010
  
- ✅ `appsettings.Development.json`
  - Debug level logging

- ✅ `launchSettings.json`
  - HTTP profile: localhost:5010

**Paketler:**
- ✅ MongoDB.Driver 3.3.0
- ✅ Serilog.AspNetCore 8.0.0
- ✅ Serilog.Sinks.Console 5.0.1
- ✅ Serilog.Sinks.Seq 9.0.0
- ✅ Serilog.Enrichers.Environment 2.3.0
- ✅ Serilog.Enrichers.Thread 3.1.0
- ✅ Swashbuckle.AspNetCore 7.0.0

---

## 🚀 Çalışan Uygulama

### Port Bilgileri:
- **HTTP:** http://localhost:5010
- **Swagger UI:** http://localhost:5010/swagger

### Çalıştırma:
```powershell
cd C:\Serkan\iSIM\MonitraNG\MngDataGateway\Presentation\MngDataGateway.Api
dotnet run
```

### Test Edildi:
- ✅ `/api/version` endpoint çalışıyor
- ✅ `/api/version/short` endpoint çalışıyor
- ✅ Swagger UI erişilebilir
- ✅ Serilog logging çalışıyor
- ✅ Global exception handler çalışıyor

---

## 📚 Planlama Dökümanları

### ROADMAP_MngDataGateway.md (1343 satır)

**Konuşulan ve Dokümante Edilen Konular:**

#### 1. Genel Mimari
- ✅ Clean Architecture yapısı
- ✅ Multi-tenant izolasyon (JWT token + domain_name)
- ✅ Database bağlantısı: JWT token'dan `domain_name` → `mng_{domain_name}`
- ✅ Event-driven architecture (RabbitMQ)

#### 2. Datasets Kavramı
- ✅ `@datasets` collection = Meta-schema layer
- ✅ Çift katmanlı yapı (meta-schema + actual data)

#### 3. Dataset Alanları (Detaylı)
- ✅ **category** (uuid | null) - Dataset kategorilendirme
- ✅ **__dataId** (GUID) - Primary key, tüm lookup'larda kullanılacak
- ✅ **name** (string, unique) - Dataset adı = Collection adı
- ✅ **description** (string, optional) - Açıklama
- ✅ **logging** (enum: self | none | common) - History stratejisi
- ✅ **publish_mode** (enum: none | basic | full) - RabbitMQ event publishing
- ✅ **forceSchema** (boolean) - Strict/Flexible schema

#### 4. Field Types (9 Tip)
- ✅ **text** - String
- ✅ **number** - Number
- ✅ **bool** - Boolean
- ✅ **datetime** - Date
- ✅ **object** - JSON Object
- ✅ **relation** - Dataset referansı (MongoDB lookup)
- ✅ **persons** - User referansı (MngKeeper entegrasyonu)
- ✅ **personGroups** - Group referansı (MngKeeper entegrasyonu)
- ✅ **incremental** - Auto-increment (format desteği ile)

#### 5. Field Özellikleri
- ✅ **name** - Field adı = MongoDB field adı
- ✅ **title** - Display name (UI için)
- ✅ **description** - Açıklama
- ✅ **mandatory** - Zorunlu alan
- ✅ **unique** - Unique constraint
- ✅ **isArray** - Array field
- ✅ **relation** - İlişki tanımı (relatedDataset, relationField)
- ✅ **incrementalOptions** - Auto-increment ayarları

#### 6. Incremental Field (Detaylı)
- ✅ Format desteği (TASK-{0:D6}, INV-{year}{month}-{0:D4})
- ✅ Placeholders: {0}, {domain}, {year}, {month}, {day}, {yy}
- ✅ Scope: Per domain + per collection + per field
- ✅ @__counters collection yapısı
- ✅ Atomic increment (concurrent-safe)
- ✅ Immutable (update edilemez)

#### 7. Validations
- ✅ External HTTP validation
- ✅ Request/Response format
- ✅ Sequential execution strategy
- ✅ Multiple validation support

#### 8. Queries
- ✅ Predefined MongoDB aggregation pipelines
- ✅ Parameter injection (##current_workspace_id)
- ⏳ Detaylar sonra konuşulacak

#### 9. Index Management
- ✅ indexList tanımı
- ✅ Index types: unique, ascending, descending, sparse, TTL
- ✅ Compound index support
- ✅ System indexes (__dataId)

#### 10. Öneriler (Gelecek İçin)
- 🔴 **Yüksek Öncelik:**
  - Field-level permissions
  - Default values
  - Field validation rules
  - Cascade delete strategy
  
- 🟡 **Orta Öncelik:**
  - Computed fields
  - Lifecycle hooks
  - UI metadata
  - Bulk operations
  
- 🟢 **Düşük Öncelik:**
  - Conditional fields
  - Data versioning
  - Schema migration
  - Import/Export

---

## ❓ Karar Verilecek Sorular

### persons/personGroups İmplementasyonu:
- ❓ MngKeeper HTTP API call mı? Cache'den mi?
- ❓ Batch request endpoint'i var mı?
- ❓ JWT token forward edilecek mi?
- ❓ User bulunamazsa ne olur?
- ❓ Create/Update sırasında user ID doğrulanacak mı?

### Logging (common mode):
- ❓ @data_logs için index stratejisi?
- ❓ Log retention policy?
- ❓ Log cleanup mekanizması?

### Validation:
- ❓ Multiple validation execution: Sequential mi, parallel mi?
- ❓ Token'ı validation endpoint'e forward etmeli miyiz?
- ❓ Timeout sonrası ne olsun?

### Queries:
- ❓ Parameter injection nasıl çalışacak?
- ❓ Güvenlik: SQL injection benzeri saldırılara karşı korunma?
- ❓ Query caching?

### Performance:
- ❓ Redis cache entegrasyonu?
- ❓ Query result caching?
- ❓ persons/personGroups için cache stratejisi?

---

## 🎯 Sıradaki Adımlar (Yarın)

### 1. API Endpoints Tasarımı
```http
# Dataset Management
POST   /api/datasets                    # Schema oluştur
GET    /api/datasets                    # Schema listesi
GET    /api/datasets/{name}             # Schema detay
PUT    /api/datasets/{name}             # Schema güncelle
DELETE /api/datasets/{name}             # Schema sil

# Data CRUD
POST   /api/datasets/{name}/data        # Veri ekle
GET    /api/datasets/{name}/data        # Veri listesi
GET    /api/datasets/{name}/data/{id}   # Tekil veri
PUT    /api/datasets/{name}/data/{id}   # Veri güncelle
DELETE /api/datasets/{name}/data/{id}   # Veri sil

# Predefined Queries
GET    /api/datasets/{name}/query/{queryName}?param1=value1
```

### 2. Request/Response Models
- CreateDatasetRequest
- UpdateDatasetRequest
- CreateDataRequest
- UpdateDataRequest
- PaginatedResponse<T>
- StandardResponse<T>

### 3. JWT Authentication
- Token validation middleware
- Domain extraction (domain_name → database)
- User context extraction

### 4. Dataset Schema Management
- Dataset CRUD operations
- Schema validation
- Collection management

### 5. Data CRUD Operations
- Create with __dataId generation
- Create with incremental fields
- Update (with validation)
- Delete (hard delete)
- Get with lookup resolution

### 6. Query Execution
- Parameter injection mechanism
- Aggregation pipeline execution
- persons/personGroups enrichment

### 7. Event Publishing
- RabbitMQ integration
- publish_mode handling (none, basic, full)
- Message format standardization

---

## 🔧 Teknik Notlar

### MongoDB Bağlantısı
```csharp
// Program.cs'te yapılandırıldı
builder.Services.AddSingleton<IMongoClient>(...)

// Kullanım (JWT token'dan):
var domainName = ParseDomainFromToken(jwtToken);  // "test-domain"
var databaseName = $"mng_{domainName}";           // "mng_test-domain"
var database = mongoClient.GetDatabase(databaseName);
```

### IOptions<> Pattern
```csharp
// Configuration
builder.Services.Configure<MongoDbOptions>(
    builder.Configuration.GetSection(MongoDbOptions.SectionName));

// Kullanım
public class MyService
{
    private readonly MongoDbOptions _options;
    
    public MyService(IOptions<MongoDbOptions> options)
    {
        _options = options.Value;
    }
}
```

### Serilog
- ✅ Console sink (colored output)
- ✅ Seq sink (http://localhost:5341)
- ✅ Enrichers: MachineName, ThreadId, EnvironmentUserName
- ✅ Structured logging

---

## 🚫 HTTPS Yapılandırması
- ⏸️ Şu anda devre dışı (sertifika yok)
- ⏸️ Sadece HTTP (port 5010)
- 📌 Sertifika yapılandırması sonra eklenecek

---

## 🧪 Test Edildi

### ✅ Çalışan Endpoint'ler:
```
GET http://localhost:5010/api/version       ✅ Çalışıyor
GET http://localhost:5010/api/version/short ✅ Çalışıyor
GET http://localhost:5010/swagger           ✅ Çalışıyor
```

### Version Response Örneği:
```json
{
  "product": "MngDataGateway API",
  "version": "1.0.0",
  "assemblyVersion": "1.0.0.0",
  "buildDate": "2025-11-03T...",
  "company": "iSIM Platform",
  "copyright": "Copyright © 2025",
  "environment": "Development",
  "runtime": {
    "framework": "9.0.0",
    "os": "...",
    "machineName": "...",
    "processorCount": 8
  },
  "dependencies": {
    "mongoDb": "7.0",
    "rabbitMq": "3-management"
  }
}
```

---

## 🔐 JWT Token Yapısı (MngKeeper'dan)

```json
{
  "sub": "user-id",
  "email": "admin@test-domain.com",
  "preferred_username": "test-domain_admin",
  "domain_id": "69051b09da18595c1fa866ce",
  "domain_name": "test-domain",           // ← Database seçimi için kritik!
  "domain_realm": "test-domain",
  "is_admin": false,
  "realm_access": {
    "roles": ["offline_access"]
  }
}
```

**Kullanım:**
```csharp
var domainName = token.Claims.FirstOrDefault(c => c.Type == "domain_name")?.Value;
var databaseName = $"mng_{domainName}";
```

---

## 📊 Datasets Meta-Schema Yapısı

### Örnek Dataset Kaydı (@datasets collection):
```json
{
  "__dataId": "uuid",
  "category": "uuid | null",
  "name": "@tasks",
  "description": "Task management data",
  "forceSchema": false,
  "logging": "self | none | common",
  "publish_mode": "none | basic | full",
  
  "fields": [
    {
      "fieldType": "text | number | bool | datetime | object | relation | persons | personGroups | incremental",
      "name": "field_name",
      "title": "Display Name",
      "description": "Field açıklaması",
      "mandatory": true | false,
      "unique": true | false,
      "isArray": true | false,
      "relation": {
        "relatedDataset": "@other_dataset",
        "relationField": "__dataId"
      },
      "incrementalOptions": {
        "startValue": 1,
        "incrementStep": 1,
        "format": "TASK-{0:D6}"
      }
    }
  ],
  
  "validations": [
    {
      "name": "validation_name",
      "endpoint": "https://...",
      "method": "POST",
      "when": ["create", "update"],
      "order": 1,
      "enabled": true
    }
  ],
  
  "queries": [
    {
      "name": "query_name",
      "filter": {
        "customquery": [ /* MongoDB Aggregation Pipeline */ ]
      }
    }
  ],
  
  "indexList": [
    {
      "name": "idx_name",
      "fields": { "field_name": 1 },
      "unique": true,
      "sparse": false,
      "ttl": 3600
    }
  ]
}
```

---

## 🎯 Yarın Devam Edilecek Konular

### 1. API Endpoints İmplementasyonu
- DatasetsController (schema CRUD)
- DataController (data CRUD)
- QueryController (predefined queries)

### 2. JWT Authentication
- Middleware
- Token parsing
- Domain extraction
- User context

### 3. Core Services
- DatasetSchemaService
- DataCrudService
- QueryExecutionService
- IncrementalService (counter management)
- ValidationService (external HTTP)

### 4. MongoDB Integration
- Dynamic database selection
- Collection operations
- Aggregation pipeline execution
- Lookup resolution

### 5. RabbitMQ Integration
- Event publisher
- Message formats (basic, full)
- Routing strategies

---

## 📝 Önemli Notlar

- MongoDB `_id` alanı hiç kullanılmayacak → Sadece `__dataId`
- Hard delete (soft delete yok)
- Tüm lookup'larda varsayılan: `foreignField: "__dataId"`
- `@` prefix'i kullanıcı tercihi (sistem için önemli değil)
- Incremental field'lar immutable (update edilemez)
- persons/personGroups implementasyonu sonra detaylandırılacak

---

## 🛠️ Çalışma Ortamı

- **OS:** Windows 10
- **.NET:** 9.0
- **IDE:** Cursor
- **Database:** MongoDB (localhost:27017)
- **Message Broker:** RabbitMQ (localhost:5672)
- **Logging:** Seq (localhost:5341)

---

## 🎉 Başarılı Milestone

MngDataGateway projesi **başarıyla oluşturuldu** ve **çalışır durumda!**

**Sonraki oturum:** API endpoint'leri ve core business logic implementasyonu.

---

**Proje Durumu:** 🟢 Aktif Geliştirme  
**Son Test:** 3 Kasım 2025 - Başarılı ✅

