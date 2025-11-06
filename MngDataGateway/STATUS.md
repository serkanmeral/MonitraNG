# MngDataGateway - Current Status

**Last Updated:** 6 Kasım 2025  
**Session:** Ready for Dataset Implementation  
**Test Domain:** `seven`  
**Test User:** `serkan` (admin)

---

## ✅ Completed Tasks

### 🏗️ Project Structure (Clean Architecture)

```
MngDataGateway/
├── Core/
│   ├── MngDataGateway.Domain/          ✅ Created
│   └── MngDataGateway.Application/     ✅ Created
├── Infrastructure/
│   ├── MngDataGateway.Infrastructure/  ✅ Created
│   └── MngDataGateway.Persistence/     ✅ Created
├── Presentation/
│   └── MngDataGateway.Api/            ✅ Created
├── MngDataGateway.sln                  ✅ Created
├── README.md                           ✅ Created
├── ROADMAP_MngDataGateway.md          ✅ Created (1343+ lines)
└── STATUS.md                           ✅ This file
```

---

### 📦 Configuration & Infrastructure

**✅ Completed:**
- Clean Architecture setup
- IOptions<> pattern implementation
- Serilog logging (Console + Seq)
- Global Exception Handler
- MongoDB.Driver 3.3.0
- RabbitMQ.Client 7.0.0
- MediatR 13.0.0
- FluentValidation 11.3.1

**✅ Parametric Configuration (NEW - 6 Nov 2025):**
- Added `ServerSettings` class (Host, Port, Scheme)
- Environment variable support
- Dynamic Kestrel configuration (like MngKeeper)
- Supports: 0.0.0.0, localhost, specific IP

**Configuration:**
```json
{
  "MngDataGatewaySettings": {
    "Server": {
      "Host": "0.0.0.0",
      "Port": 5010,
      "Scheme": "https"
    },
    "OpenApiServerPath": "https://localhost:5010",
    "MongoDB": {
      "ConnectionString": "mongodb://admin:admin123@localhost:27017"
    },
    "Actors": {
      "MngKeeper": "https://localhost:5001"
    }
  }
}
```

---

### 🔐 JWT Authentication

**✅ Status:** READY & TESTED

**Implementation:**
- JWT Bearer authentication configured
- Token validation from MngKeeper
- No signature validation (trust MngKeeper)
- Claims extraction working

**Test Controller:** `AuthTestController.cs`
- ✅ `GET /api/authtest/public` - Public endpoint
- ✅ `GET /api/authtest/decode` - Decode JWT claims
- ✅ `GET /api/authtest/domain` - Extract domain info
- ✅ `GET /api/authtest/roles` - Check roles
- ✅ `GET /api/authtest/health` - Auth system health

**Working Endpoints:**
- ✅ `GET https://localhost:5010/api/version`
- ✅ `GET https://localhost:5010/swagger`
- ✅ `GET https://localhost:5010/api/authtest/*` (with valid token)

---

### 🏢 Test Environment Ready

**Seven Domain (Created 6 Nov 2025):**
- Domain ID: `690cda3aae502df7d3330bba`
- Domain Name: `seven`
- Database: `mng_seven`
- Realm: `seven`
- Status: Active ✅

**Serkan MERAL User:**
- Username: `serkan`
- Password: `Serkan123!`
- Email: `serkan@seven.com`
- User ID: `690cdb7fae502df7d3330bbb`
- Groups: `admins`
- Is Admin: `true` ✅

**Token Helper:**
```powershell
# Get fresh token
cd C:\Serkan\iSIM\MonitraNG\MngKeeper\tests
.\get-serkan-token.ps1

# Token saved to: %TEMP%\serkan_token.txt
# Global variable: $global:serkanToken
```

**Token Claims (Important):**
```json
{
  "preferred_username": "serkan",
  "email": "serkan@seven.com",
  "domain_name": "seven",          ← Database: mng_seven
  "isAdmin": true,
  "user_groups": ["admins"]
}
```

---

## 🎯 Next Steps - Implementation Priority

### 🔴 HIGH PRIORITY - Core Functionality

#### 1. MongoDB Context Service
**Purpose:** Extract domain from JWT and select correct database

**Interface:**
```csharp
public interface IMongoContextService
{
    // Get database for current request (from JWT)
    IMongoDatabase GetDatabase();
    
    // Get database by domain name
    IMongoDatabase GetDatabase(string domainName);
    
    // Get domain name from current user's token
    string GetCurrentDomainName();
    
    // Get user ID from current user's token
    string GetCurrentUserId();
}
```

**Implementation:**
```csharp
// From JWT token:
var domainName = HttpContext.User.FindFirst("domain_name")?.Value;
var databaseName = $"mng_{domainName}";  // "mng_seven"
return _mongoClient.GetDatabase(databaseName);
```

---

#### 2. Dataset Schema Controller
**Endpoints:**
```
POST   /api/datasets                    # Create schema
GET    /api/datasets                    # List schemas (pagination)
GET    /api/datasets/{name}             # Get schema detail
PUT    /api/datasets/{name}             # Update schema
DELETE /api/datasets/{name}             # Delete schema
```

**Features:**
- CRUD operations on `@datasets` collection
- Schema validation before save
- Index management
- Collection creation/deletion

---

#### 3. Data CRUD Controller
**Endpoints:**
```
POST   /api/datasets/{name}/data        # Create data
GET    /api/datasets/{name}/data        # List data (pagination)
GET    /api/datasets/{name}/data/{id}   # Get single data
PUT    /api/datasets/{name}/data/{id}   # Update data
DELETE /api/datasets/{name}/data/{id}   # Delete data
```

**Features:**
- Dynamic schema validation
- __dataId generation (GUID)
- Incremental field support
- Relation lookups
- persons/personGroups resolution

---

### 🟡 MEDIUM PRIORITY

#### 4. Incremental Service
**Purpose:** Handle auto-increment fields

**Interface:**
```csharp
public interface IIncrementalService
{
    Task<string> GetNextValueAsync(string domainName, string datasetName, string fieldName, IncrementalOptions options);
}
```

**Implementation:**
- Uses `@__counters` collection
- Atomic increment (FindAndModify)
- Format support: `TASK-{0:D6}`, `INV-{year}{month}-{0:D4}`
- Concurrent-safe

---

#### 5. Query Execution Service
**Endpoints:**
```
GET /api/datasets/{name}/query/{queryName}?param1=value1
```

**Features:**
- Predefined MongoDB aggregation pipelines
- Parameter injection
- persons/personGroups enrichment

---

#### 6. Validation Service
**Purpose:** External HTTP validation

**Interface:**
```csharp
public interface IValidationService
{
    Task<ValidationResult> ValidateAsync(object data, List<ValidationDefinition> validations);
}
```

---

### 🟢 LOW PRIORITY

#### 7. Event Publishing
- RabbitMQ integration
- publish_mode handling (none, basic, full)

#### 8. Category Management
- `@dataset_categories` CRUD

---

## 📋 Dataset Schema Structure (Reference)

```json
{
  "__dataId": "uuid",
  "category": "uuid | null",
  "name": "@tasks",
  "description": "Task management",
  "forceSchema": false,
  "logging": "self | none | common",
  "publish_mode": "none | basic | full",
  
  "fields": [
    {
      "fieldType": "text | number | bool | datetime | object | relation | persons | personGroups | incremental",
      "name": "field_name",
      "title": "Display Name",
      "mandatory": true,
      "unique": false,
      "isArray": false
    }
  ],
  
  "validations": [],
  "queries": [],
  "indexList": []
}
```

---

## 🔧 Technical Notes

### MongoDB Database Selection Logic
```csharp
// 1. Get domain from JWT
var domainName = User.FindFirst("domain_name")?.Value;  // "seven"

// 2. Build database name
var databaseName = $"mng_{domainName}";  // "mng_seven"

// 3. Get database
var db = _mongoClient.GetDatabase(databaseName);

// 4. Access collections
var datasetsCollection = db.GetCollection<BsonDocument>("@datasets");
var dataCollection = db.GetCollection<BsonDocument>("@tasks");
```

### Collections in `mng_seven` Database
- `@datasets` - Schema definitions (meta-data)
- `@dataset_categories` - Categories
- `@__counters` - Incremental counters
- `@tasks`, `@users`, etc. - Dynamic data collections

---

## 🧪 Testing Strategy

### Phase 1: Dataset Schema (Start Here)
1. Create a simple dataset schema
2. Verify it's saved in `@datasets` collection
3. List schemas
4. Update schema
5. Delete schema

### Phase 2: Data CRUD
1. Create data with __dataId
2. Create data with incremental field
3. Read data with pagination
4. Update data
5. Delete data

### Phase 3: Advanced Features
1. Relation lookups
2. persons/personGroups resolution
3. Validation execution
4. Query execution

---

## 💻 Running the Application

### Development (Local)
```powershell
cd C:\Serkan\iSIM\MonitraNG\MngDataGateway\Presentation\MngDataGateway.Api
dotnet run
```

**Access:**
- HTTPS: https://localhost:5010
- Swagger: https://localhost:5010/swagger
- Scalar: https://localhost:5010/scalar/v1

### With Custom Port
```powershell
$env:MngDataGatewaySettings__Server__Port = "8080"
dotnet run
```

---

## 📊 Project Statistics

| Component | Status | Files |
|-----------|--------|-------|
| Domain Layer | ✅ Ready | 2 |
| Application Layer | 🔄 In Progress | 3 |
| Infrastructure Layer | 🔄 In Progress | 2 |
| Persistence Layer | 🔄 In Progress | 1 |
| API Layer | 🔄 In Progress | 3 |

**Total Lines:** ~500 (scaffold)  
**Next Target:** 2000+ (with Dataset & Data CRUD)

---

## 🚀 Recommended Next Action

**Start with MongoDB Context Service:**

1. Create `IMongoContextService` interface
2. Implement `MongoContextService` class
3. Extract `domain_name` from JWT
4. Return correct database instance
5. Add to DI container

**Then:**

6. Create `DatasetsController`
7. Implement Create Dataset endpoint
8. Test with Serkan's token

---

## 🔗 Related Documentation

- [ROADMAP_MngDataGateway.md](../ROADMAP_MngDataGateway.md) - Full implementation plan (1343 lines)
- [README.md](README.md) - Project overview
- [MngKeeper/tests/SEVEN_DOMAIN_INFO.md](../../MngKeeper/tests/SEVEN_DOMAIN_INFO.md) - Seven domain credentials
- [MngKeeper/tests/get-serkan-token.ps1](../../MngKeeper/tests/get-serkan-token.ps1) - Token helper script

---

## 📝 Important Decisions Made

1. **Primary Key:** Always `__dataId` (GUID), never MongoDB `_id`
2. **Database Naming:** `mng_{domain_name}` format
3. **Delete Strategy:** Hard delete (no soft delete)
4. **Lookup Field:** Always `__dataId` for relations
5. **System Collections:** Use `@` prefix (optional, user preference)

---

## ⚠️ Known Issues

1. **None currently** - Project is in clean state
2. All dependencies installed
3. Authentication tested and working
4. MongoDB connection verified

---

## 🎯 Session Goal

**Implement Dataset Schema Management:**
- MongoContextService (JWT → Database)
- DatasetsController (5 endpoints)
- Schema validation
- Basic CRUD operations

**Success Criteria:**
- Create a dataset schema via API
- Verify in MongoDB `@datasets` collection
- List, update, delete schemas
- All operations use correct database (mng_seven)

---

**Status:** 🟢 Ready to Code  
**Blocked:** ❌ None  
**Dependencies:** ✅ All Met  
**Test User:** ✅ serkan@seven.com (admin)
