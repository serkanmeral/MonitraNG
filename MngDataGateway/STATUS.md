# MngDataGateway - Current Status

**Last Updated:** 6 Kasım 2025 (19:45 UTC)  
**Session:** Dataset Schema CRUD Completed ✅  
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

### 🔥 MongoDB Context Service (COMPLETED - 6 Nov 2025)

**✅ Status:** READY & TESTED

**Implementation:**
- Created `IMongoContextService` interface in Application layer
- Implemented `MongoContextService` in Persistence layer
- Registered service in DI container
- Added `HttpContextAccessor` for JWT claims access

**Features:**
- ✅ `GetDatabase()` - Gets database from JWT token
- ✅ `GetDatabase(domainName)` - Gets database by domain name
- ✅ `GetCurrentDomainName()` - Extracts domain from JWT
- ✅ `GetCurrentUserId()` - Extracts user ID from JWT
- ✅ `GetCurrentUsername()` - Extracts username from JWT
- ✅ `IsCurrentUserAdmin()` - Checks admin status

**Test Controller:** `MongoContextTestController.cs`
- ✅ `GET /api/mongocontexttest/health` - Service health check
- ✅ `GET /api/mongocontexttest/info` - Domain & user info
- ✅ `GET /api/mongocontexttest/database` - Database info & collections
- ✅ `GET /api/mongocontexttest/datasets-collection` - @datasets collection test
- ✅ `GET /api/mongocontexttest/database/{domainName}` - Database by domain (admin only)

**Test Results (6 Nov 2025):**
```
✅ Health Check: PASSED
✅ Context Info: PASSED (Domain: seven, DB: mng_seven)
✅ Database Info: PASSED (6 collections found)
✅ Datasets Collection: PASSED (0 datasets)
✅ Database by Domain: PASSED
```

**Files Created:**
```
Application/Services/IMongoContextService.cs
Persistence/Services/MongoContextService.cs
Persistence/ServiceRegistration.cs
Api/Controllers/MongoContextTestController.cs
tests/test-mongo-context-service.ps1
```

---

### 🔥 Dataset Categories CRUD (COMPLETED - 6 Nov 2025)

**✅ Status:** READY & TESTED (7/7 tests passed)

**Implementation:**
- Created Base Entity pattern (Full Metadata)
- Implemented DatasetCategory entity with MongoDB mapping
- Created complete CRUD service
- Built REST API controller with 6 endpoints
- Added comprehensive test suite

**Base Entity Pattern (Full Metadata):**
- ✅ `__dataId` - GUID primary key (backend auto-generated)
- ✅ `__createInfo` - Creation metadata from JWT
- ✅ `__lastUpdateInfo` - Last update metadata
- ✅ `__history` - Self-logging audit trail (MaxHistoryEntries: 50)

**UserInfo Structure (Simplified):**
```json
{
  "uid": "user-guid",
  "userName": "serkan",
  "domain": "seven"
}
```

**Features:**
- ✅ JWT-based multi-tenancy (domain → mng_{domain})
- ✅ Automatic metadata population
- ✅ History tracking (only changed fields)
- ✅ Hard delete + `__deletedDatas` backup (TTL: 7 days)
- ✅ Restore functionality
- ✅ Pagination support
- ✅ MongoDB conventions (BsonElement, BsonIgnoreExtraElements)

**Endpoints:**
```
POST   /api/dataset-categories          - Create category
GET    /api/dataset-categories          - List (pagination)
GET    /api/dataset-categories/{dataId} - Get by ID
PUT    /api/dataset-categories/{dataId} - Update
DELETE /api/dataset-categories/{dataId} - Delete (hard + backup)
POST   /api/dataset-categories/{dataId}/restore - Restore
```

**Test Results (6 Nov 2025 - 18:51 UTC):**
```
✅ CREATE: PASSED
✅ LIST (Pagination): PASSED (4 categories)
✅ GET BY ID: PASSED
✅ UPDATE: PASSED (__lastUpdateInfo added, historyCount: 2)
✅ GET UPDATED: PASSED
✅ DELETE: PASSED (backed up to __deletedDatas)
✅ RESTORE: PASSED (historyCount: 3 with restore entry)
```

**MongoDB Collections:**
- `@dataset_categories` - Main collection
- `__deletedDatas` - Deleted data backup (TTL: 7 days)

**Files Created:**
```
Domain/Entities/Base/BaseEntity.cs
Domain/Entities/DatasetCategory.cs
Application/Services/IUserInfoService.cs
Application/Services/IDatasetCategoryService.cs
Application/DTOs/DatasetCategory/CreateDatasetCategoryDto.cs
Application/DTOs/DatasetCategory/UpdateDatasetCategoryDto.cs
Application/DTOs/DatasetCategory/DatasetCategoryResponseDto.cs
Application/DTOs/Common/PagedResultDto.cs
Persistence/Services/UserInfoService.cs
Persistence/Services/DatasetCategoryService.cs
Api/Controllers/DatasetCategoriesController.cs
tests/test-dataset-categories.ps1
```

**Configuration Added:**
```json
{
  "History": {
    "MaxHistoryEntries": 50
  },
  "DeletedData": {
    "RetentionDays": 7
  }
}
```

---

### 🔥 Dataset Schema CRUD (COMPLETED - 6 Nov 2025)

**✅ Status:** READY & TESTED (8/8 tests passed)

**Collection:** `@datasets` in `mng_{domain}` database

**Implementation:**
- Updated DatasetSchema entity (BaseEntity inheritance)
- Implemented complete CRUD service with validations
- Built REST API controller with 6 endpoints
- Field type validation and incremental field checks
- Comprehensive test suite with minimal and full schemas

**Supported Field Types (9):**
1. ✅ `text` - String values
2. ✅ `number` - Integer/Decimal
3. ✅ `bool` - Boolean
4. ✅ `datetime` - ISO 8601 UTC
5. ✅ `object` - JSON object
6. ✅ `relation` - Dataset reference
7. ✅ `persons` - User reference (MngKeeper)
8. ✅ `personGroups` - Group reference (MngKeeper)
9. ✅ `incremental` - Auto-increment with prefix support

**Incremental Field Features:**
- ✅ Format templates: `{0}`, `{year}`, `{month}`, `{day}`, `{yy}`, `{domain}`, `{fieldName}`
- ✅ Prefix-based scope (per unique prefix)
- ✅ Field reference support (dynamic prefix)
- ✅ Configurable increment step
- ✅ NO reset period (continuous counter)

**Schema Properties:**
- Required: `name` (unique, collection name)
- Optional: `description`, `category`, `forceSchema`, `logging`, `publish_mode`
- Arrays: `fields[]`, `validations[]`, `queries[]`, `indexList[]`
- Defaults: `forceSchema: true`, `logging: "none"`, `publish_mode: "none"`

**Lazy Features (not executed, only stored):**
- Validations: Definition stored, execution postponed to data controller
- Queries: Definition stored, execution postponed to data controller
- Indexes: Definition stored, creation postponed to first data insert
- Collections: Not created until first data insert

**Endpoints:**
```
POST   /api/datasets              - Create schema (metadata only)
GET    /api/datasets              - List schemas (pagination)
GET    /api/datasets/{name}       - Get schema (with field details)
PUT    /api/datasets/{name}       - Update schema
DELETE /api/datasets/{name}       - Delete schema (collection preserved!)
POST   /api/datasets/{name}/restore - Restore schema
```

**Test Results (6 Nov 2025 - 19:43 UTC):**
```
✅ CREATE (Minimal): PASSED (only name)
✅ CREATE (Full): PASSED (6 fields, 2 indexes, incremental)
✅ LIST: PASSED (5 datasets)
✅ GET BY NAME: PASSED (field details included)
✅ UPDATE: PASSED (fields 6→2, __lastUpdateInfo added)
✅ GET UPDATED: PASSED (historyCount: 2)
✅ DELETE: PASSED (backed up to __deletedDatas)
✅ RESTORE: PASSED (historyCount: 3)
```

**Field Validations:**
- ✅ Duplicate field name detection
- ✅ Invalid field type rejection
- ✅ Relation field must have relationDataset
- ✅ Incremental field must be unique + mandatory + non-array
- ✅ Incremental field must have incrementalOptions

**Files Created:**
```
Application/Services/IDatasetService.cs
Application/DTOs/Dataset/CreateDatasetDto.cs
Application/DTOs/Dataset/UpdateDatasetDto.cs
Application/DTOs/Dataset/DatasetResponseDto.cs
Persistence/Services/DatasetService.cs
Api/Controllers/DatasetsController.cs
tests/test-datasets.ps1
```

**Configuration:**
- Uses existing History and DeletedData settings
- Field conversion: object → BsonValue for MongoDB compatibility

---

## 🎯 Next Steps - Implementation Priority

### 🔴 HIGH PRIORITY - Core Functionality

#### 1. Dataset Schema Controller (NEXT TASK)
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
