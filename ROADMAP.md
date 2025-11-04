# MonitraNG Development Roadmap

## 🔒 Deployment Gereksinimleri

**⚠️ KRİTİK KISIT:** MonitraNG, **internete kapalı (air-gapped) sistemlerde** çalışacak.

### Sonuçlar:
- ✅ Tüm bileşenler **self-hosted** olmalı (cloud servisleri kullanılamaz)
- ✅ Docker-based offline deployment
- ✅ Docker image'lar offline ortama taşınabilir olmalı
- ✅ NuGet package'lar local/private feed'den
- ⚠️ External API çağrıları yapılamaz
- ⚠️ Online license validation yapılamaz

### Stack Kontrolü:
```yaml
✅ MongoDB          - Self-hosted
✅ RabbitMQ         - Self-hosted
✅ MinIO            - Self-hosted
✅ KeyCloak         - Self-hosted
✅ MngKeeper API    - Self-hosted
✅ Diğer servisler  - Self-hosted
```

**Tüm bileşenler offline çalışabilir! 🎉**

---

## 🎯 Öncelikli Görevler (Sıralı)

### 1. User CRUD İşlemleri Test ✅ Hazır, Test Edilecek
**Endpoint'ler:**
- POST `/api/user` - Create user
- GET `/api/user` - Get users (pagination)
- GET `/api/user/{userId}` - Get user by ID
- PUT `/api/user/{userId}` - Update user
- DELETE `/api/user/{userId}` - Delete user
- POST `/api/user/{userId}/groups/{groupId}` - Add to group
- DELETE `/api/user/{userId}/groups/{groupId}` - Remove from group

**Test Senaryoları:**
- [ ] User oluşturma (domain içinde)
- [ ] User listesi (pagination, search, filter)
- [ ] User detay
- [ ] User güncelleme
- [ ] User silme
- [ ] User'ı gruba ekleme
- [ ] User'ı gruptan çıkarma
- [ ] Multi-tenant izolasyonu (farklı domain'lerde)

**Test Script:** `MngKeeper/tests/user-crud-test.ps1`

---

### 2. Group CRUD İşlemleri Test ✅ Hazır, Test Edilecek
**Endpoint'ler:**
- POST `/api/group` - Create group
- GET `/api/group` - Get groups (pagination)
- PUT `/api/group/{groupId}` - Update group
- DELETE `/api/group/{groupId}` - Delete group

**Test Senaryoları:**
- [ ] Group oluşturma
- [ ] Group listesi (pagination, search, filter)
- [ ] Group güncelleme (name, description, permissions)
- [ ] Group silme
- [ ] Multi-tenant izolasyonu

**Test Script:** `MngKeeper/tests/group-crud-test.ps1` (oluşturulacak)

---

### 3. RabbitMQ Event Publishing 🔄 Tasarım + İmplementasyon

**Amaç:** Tüm CRUD işlemlerinin event olarak yayınlanması

**Event'ler:**
```
Domain Events:
- domain.created
- domain.updated
- domain.deleted

User Events:
- user.created
- user.updated
- user.deleted
- user.group.added
- user.group.removed

Group Events:
- group.created
- group.updated
- group.deleted
```

**İmplementasyon:**
- [ ] Event model'leri oluşturma
- [ ] Domain event handler'lar
- [ ] RabbitMQ publisher entegrasyonu
- [ ] Event consumer'lar (MngReactor için)
- [ ] Event logging
- [ ] Dead letter queue
- [ ] Retry mechanism

**Teknoloji:**
- RabbitMQ (Topic Exchange)
- MediatR Notifications
- EventPublisher service

---

### 4. MngStorage Servisi 📦 Yeni Mikroservis

**KARAR: Dosyalama için ayrı mikroservis geliştirilecek ✅**

**Amaç:** Dosyalama işlemlerini yöneten merkezi, bağımsız servis. MinIO'yu sadece bu servis bilir, diğer tüm servisler ve client'lar MngStorage API'sini kullanır.

#### Mimari:
```
Frontend/Services → MngStorage API → MinIO
                          ↓
                      MongoDB (metadata)
                          ↓
                      RabbitMQ (events)
```

#### Temel Özellikler:

**1. Upload/Download:**
- ✅ Streaming upload (memory efficient)
- ✅ Chunked upload (büyük dosyalar, resume capability)
- ✅ Streaming download (Range support)
- ✅ Multipart upload (MinIO native)
- ⚠️ **Dosya boyut sınırı YOK** (streaming ile)

**2. Metadata Yönetimi:**
- MongoDB'de file metadata storage
- File properties: id, name, size, type, owner, uploadedAt
- Custom metadata support
- Tags and categories
- Search and filtering

**3. Business Logic:**
- File type validation (whitelist)
- Virus scanning (opsiyonel, ClamAV)
- Thumbnail generation (images)
- File hash calculation (SHA256)
- Duplicate detection

**4. Domain İzolasyonu:**
- Domain-based access control (JWT validation)
- Bucket-per-domain (MinIO)
- Quota management per domain
- User-level permissions

**5. Event Publishing:**
```
Events:
- file.uploaded
- file.downloaded
- file.deleted
- file.scan.completed
- file.quota.exceeded
```

#### REST API Endpoints:

```http
# Upload
POST   /api/v1/files/upload
       Query: domain, category, fileName
       Body: multipart/form-data (streaming)
       Response: { fileId, url, size }

# Chunked Upload (Büyük dosyalar)
POST   /api/v1/files/upload/start
       Response: { uploadId, chunkSize }
       
PUT    /api/v1/files/upload/{uploadId}/chunk/{chunkNumber}
       Body: binary chunk
       
POST   /api/v1/files/upload/{uploadId}/complete
       Response: { fileId, url }

# Download
GET    /api/v1/files/{fileId}/download
       Headers: Range support
       Response: File stream

# File Operations
GET    /api/v1/files/{fileId}
DELETE /api/v1/files/{fileId}
GET    /api/v1/files?domain={id}&category={cat}&page=1

# Temporary URL (External access)
POST   /api/v1/files/{fileId}/temp-url
       Body: { expiresIn: 3600 }
       Response: { url, expiresAt }
```

#### gRPC API (Service-to-Service):

```protobuf
service StorageService {
  rpc UploadFile(stream UploadFileRequest) returns (UploadFileResponse);
  rpc DownloadFile(DownloadFileRequest) returns (stream DownloadFileResponse);
  rpc DeleteFile(DeleteFileRequest) returns (DeleteFileResponse);
  rpc GetFileMetadata(GetFileMetadataRequest) returns (FileMetadata);
  rpc ListFiles(ListFilesRequest) returns (ListFilesResponse);
}
```

#### MongoDB Schema:

**FileMetadata Collection:**
```json
{
  "_id": "file_123",
  "domainId": "domain_456",
  "userId": "user_789",
  "category": "reports",
  "fileName": "report.pdf",
  "originalFileName": "Monthly Report.pdf",
  "contentType": "application/pdf",
  "size": 15728640,
  "bucketName": "domain-456",
  "objectName": "reports/pdf/file_123.pdf",
  "hash": "sha256:abc123...",
  "metadata": { "custom": "fields" },
  "uploadedAt": "2024-10-31T10:00:00Z",
  "uploadedBy": "user_789",
  "isDeleted": false,
  "tags": ["report", "pdf"]
}
```

#### Teknoloji Stack:

- **Framework:** ASP.NET Core 8.0 Web API
- **Communication:** REST API + gRPC
- **Storage:** MinIO (via Minio.AspNetCore SDK)
- **Database:** MongoDB (metadata)
- **Messaging:** RabbitMQ (events)
- **Authentication:** JWT (KeyCloak tokens)
- **Validation:** FluentValidation
- **Logging:** Serilog

#### Güvenlik:

- [ ] JWT token validation (KeyCloak)
- [ ] Domain-based authorization
- [ ] File type whitelist
- [ ] File size limits per category
- [ ] Rate limiting (upload/download)
- [ ] Virus scanning integration
- [ ] Audit logging

#### İmplementasyon Adımları:

- [ ] 1. **Project Setup:**
  - MngStorage.Api project
  - MngStorage.Application layer
  - MngStorage.Domain layer
  - MngStorage.Infrastructure layer
  
- [ ] 2. **Core Infrastructure:**
  - MinIO client configuration
  - MongoDB repository setup
  - RabbitMQ publisher
  - JWT authentication middleware
  
- [ ] 3. **Upload Implementation:**
  - Streaming upload endpoint
  - Chunked upload endpoints
  - Metadata storage
  - Event publishing
  
- [ ] 4. **Download Implementation:**
  - Streaming download endpoint
  - Range request support
  - Authorization checks
  
- [ ] 5. **gRPC Services:**
  - Proto definitions
  - Service implementations
  - Server configuration
  
- [ ] 6. **Business Logic:**
  - File validation
  - Thumbnail generation
  - Hash calculation
  - Duplicate detection
  
- [ ] 7. **Domain Integration:**
  - Domain event handlers
  - Bucket initialization (on domain created)
  - Bucket cleanup (on domain deleted)
  
- [ ] 8. **Testing:**
  - Unit tests
  - Integration tests (MinIO, MongoDB)
  - Performance tests (large files)
  - Concurrent upload tests
  
- [ ] 9. **Docker & Deployment:**
  - Dockerfile
  - docker-compose integration
  - Health checks
  
- [ ] 10. **Documentation:**
  - API documentation (Swagger)
  - Integration guide
  - Performance benchmarks

#### Kullanım Örnekleri:

**Frontend - Dosya Yükleme:**
```javascript
// Streaming upload
const formData = new FormData();
formData.append('file', file);

const response = await fetch(
  '/api/v1/files/upload?domain=123&category=reports&fileName=report.pdf',
  {
    method: 'POST',
    headers: { 'Authorization': `Bearer ${jwtToken}` },
    body: formData
  }
);
```

**Mikroservis - Rapor Kaydetme:**
```csharp
// gRPC call
var request = new UploadFileRequest
{
    DomainId = "domain_123",
    Category = "reports",
    FileName = "monthly-report.pdf",
    ContentType = "application/pdf"
};

using var call = _storageClient.UploadFile();
await call.RequestStream.WriteAsync(request);

// Stream chunks
var buffer = new byte[4096];
int bytesRead;
while ((bytesRead = await pdfStream.ReadAsync(buffer)) > 0)
{
    await call.RequestStream.WriteAsync(new UploadFileRequest
    {
        ChunkData = Google.Protobuf.ByteString.CopyFrom(buffer, 0, bytesRead)
    });
}

await call.RequestStream.CompleteAsync();
var response = await call.ResponseAsync;
```

---

### 5. API Gateway 🚪 Merkezi Giriş Noktası

**KARAR: Ocelot API Gateway kullanılacak ✅**

**Amaç:** Tüm mikroservislerin tek giriş noktasından yönetilmesi. Client'lar sadece gateway'i bilir, backend servisleri bilmez.

#### Mimari:
```
┌──────────┐
│ Frontend │ → https://api.monitra.local
└────┬─────┘
     │
     ↓
┌─────────────────────┐
│   API Gateway       │ ← Ocelot (ASP.NET Core)
│   - Authentication  │
│   - Rate Limiting   │
│   - Routing         │
│   - Logging         │
└─────────┬───────────┘
          │
          ├──→ MngKeeper:5001   (/keeper/*)
          ├──→ MngStorage:5002  (/storage/*)
          ├──→ MngReactor:5003  (/reactor/*)
          ├──→ MngMonitor:5004  (/monitor/*)
          └──→ KeyCloak:8080    (/auth/*)
```

#### Sağladığı Faydalar:

**1. Tek Giriş Noktası (Unified Entry Point):**
- Client tek endpoint kullanır: `https://api.monitra.local`
- Backend servislerin port/host bilgisine ihtiyaç yok
- Service discovery otomatik

**2. Merkezi Authentication:**
- JWT validation tek yerden (KeyCloak)
- Her serviste auth kodu yazmaya gerek yok
- Token refresh merkezi yönetim

**3. Cross-Cutting Concerns:**
- Rate limiting (API throttling)
- Request/Response logging
- CORS policy
- SSL/TLS termination
- Request transformation
- Response caching

**4. Backend İzolasyonu:**
- Servisler external network'e expose edilmez
- Internal network üzerinde çalışır
- Security katmanı

**5. Load Balancing:**
- Multiple instance varsa otomatik dağıtım
- Health check integration
- Failover support

**6. API Composition (Gelecekte):**
- Birden fazla servisi çağırıp tek response
- Dashboard aggregation
- Reduced client requests

#### Routing Yapısı:

```http
# Client requests → Gateway routes

# MngKeeper (User/Domain/Group yönetimi)
https://api.monitra.local/keeper/api/users
https://api.monitra.local/keeper/api/domains
https://api.monitra.local/keeper/api/groups
  → http://mngkeeper:5001/api/*

# MngStorage (Dosyalama)
https://api.monitra.local/storage/api/files
https://api.monitra.local/storage/api/files/upload
  → http://mngstorage:5002/api/*

# MngMonitor (Monitoring)
https://api.monitra.local/monitor/api/metrics
https://api.monitra.local/monitor/api/health
  → http://mngmonitor:5004/api/*

# KeyCloak (Authentication)
https://api.monitra.local/auth/realms/monitra/protocol/openid-connect/token
  → http://keycloak:8080/realms/monitra/protocol/openid-connect/token
```

#### Ocelot Configuration:

**ocelot.json:**
```json
{
  "Routes": [
    {
      "DownstreamPathTemplate": "/api/{everything}",
      "DownstreamScheme": "http",
      "DownstreamHostAndPorts": [
        { "Host": "mngkeeper", "Port": 5001 }
      ],
      "UpstreamPathTemplate": "/keeper/api/{everything}",
      "UpstreamHttpMethod": [ "GET", "POST", "PUT", "DELETE", "PATCH" ],
      "AuthenticationOptions": {
        "AuthenticationProviderKey": "Bearer",
        "AllowedScopes": []
      },
      "RateLimitOptions": {
        "ClientWhitelist": [],
        "EnableRateLimiting": true,
        "Period": "1m",
        "PeriodTimespan": 60,
        "Limit": 100
      }
    },
    {
      "DownstreamPathTemplate": "/api/{everything}",
      "DownstreamScheme": "http",
      "DownstreamHostAndPorts": [
        { "Host": "mngstorage", "Port": 5002 }
      ],
      "UpstreamPathTemplate": "/storage/api/{everything}",
      "UpstreamHttpMethod": [ "GET", "POST", "PUT", "DELETE" ],
      "AuthenticationOptions": {
        "AuthenticationProviderKey": "Bearer"
      },
      "RateLimitOptions": {
        "EnableRateLimiting": true,
        "Period": "1m",
        "Limit": 50
      }
    }
  ],
  "GlobalConfiguration": {
    "BaseUrl": "https://api.monitra.local",
    "RateLimitOptions": {
      "DisableRateLimitHeaders": false,
      "QuotaExceededMessage": "API rate limit exceeded. Please try again later.",
      "HttpStatusCode": 429,
      "ClientIdHeader": "X-ClientId"
    }
  }
}
```

**Program.cs:**
```csharp
var builder = WebApplication.CreateBuilder(args);

// Ocelot configuration
builder.Configuration
    .AddJsonFile("ocelot.json", optional: false, reloadOnChange: true);

// Add Ocelot
builder.Services.AddOcelot(builder.Configuration);

// JWT Authentication (KeyCloak)
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer("Bearer", options =>
    {
        options.Authority = "http://keycloak:8080/realms/monitra";
        options.Audience = "account";
        options.RequireHttpsMetadata = false; // Dev için
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true
        };
    });

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins("https://app.monitra.local")
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

// Logging & Monitoring
builder.Services.AddSerilog();

var app = builder.Build();

app.UseCors("AllowFrontend");
app.UseAuthentication();
await app.UseOcelot();

app.Run();
```

#### Güvenlik Özellikleri:

- [ ] **JWT Validation:** KeyCloak public key ile token doğrulama
- [ ] **Rate Limiting:** Client/IP bazlı throttling
  - Anonymous: 30 req/min
  - Authenticated: 100 req/min
  - Admin: 500 req/min
- [ ] **CORS Policy:** Sadece frontend origin'i izin
- [ ] **IP Whitelisting:** Gerekirse IP bazlı kısıtlama
- [ ] **Request Size Limits:** Max 100MB (file upload için)
- [ ] **SSL/TLS:** HTTPS enforcement (production)
- [ ] **API Key Support:** External integrations için (gelecekte)

#### Monitoring & Logging:

**Metrics:**
- Request count per service
- Response time per route
- Error rate
- Rate limit hits
- Active connections

**Logging:**
```csharp
// Request logging middleware
app.Use(async (context, next) =>
{
    var stopwatch = Stopwatch.StartNew();
    
    await next();
    
    stopwatch.Stop();
    
    _logger.LogInformation(
        "Gateway: {Method} {Path} → {StatusCode} ({ElapsedMs}ms)",
        context.Request.Method,
        context.Request.Path,
        context.Response.StatusCode,
        stopwatch.ElapsedMilliseconds
    );
});
```

#### Docker Configuration:

**docker-compose.yml:**
```yaml
services:
  api-gateway:
    build:
      context: ./Gateway
      dockerfile: Dockerfile
    container_name: monitra-gateway
    ports:
      - "80:80"
      - "443:443"
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ASPNETCORE_URLS=http://+:80;https://+:443
    volumes:
      - ./Gateway/ocelot.json:/app/ocelot.json
      - ./certs:/app/certs
    depends_on:
      - mngkeeper
      - mngstorage
      - mngmonitor
      - keycloak
    networks:
      - monitra-network
    restart: unless-stopped

  mngkeeper:
    # Port'ları expose etme (sadece internal network)
    expose:
      - "5001"
    networks:
      - monitra-network

  mngstorage:
    expose:
      - "5002"
    networks:
      - monitra-network

  mngmonitor:
    expose:
      - "5004"
    networks:
      - monitra-network

networks:
  monitra-network:
    driver: bridge
```

#### High Availability (Gelecekte):

Multiple gateway instances için:
```yaml
api-gateway-1:
  ...
  ports:
    - "80:80"

api-gateway-2:
  ...
  ports:
    - "8080:80"

nginx-load-balancer:
  image: nginx:alpine
  volumes:
    - ./nginx.conf:/etc/nginx/nginx.conf
  ports:
    - "80:80"
  depends_on:
    - api-gateway-1
    - api-gateway-2
```

#### İmplementasyon Adımları:

- [ ] 1. **Project Setup:**
  - MonitraNG.Gateway project oluştur
  - Ocelot NuGet package yükle
  - Project structure
  
- [ ] 2. **Basic Routing:**
  - ocelot.json configuration
  - Route definitions (keeper, storage, monitor)
  - Service discovery
  
- [ ] 3. **Authentication Integration:**
  - JWT Bearer authentication
  - KeyCloak integration
  - Token validation
  
- [ ] 4. **Rate Limiting:**
  - Global rate limits
  - Per-route limits
  - Client-based throttling
  
- [ ] 5. **CORS Configuration:**
  - Frontend origin whitelist
  - Preflight requests
  
- [ ] 6. **Logging & Monitoring:**
  - Serilog integration
  - Request/Response logging
  - Performance metrics
  
- [ ] 7. **Error Handling:**
  - Global exception handler
  - Service unavailable fallback
  - Circuit breaker (gelecekte)
  
- [ ] 8. **Docker Integration:**
  - Dockerfile
  - docker-compose update
  - Network configuration
  
- [ ] 9. **Testing:**
  - Integration tests
  - Load testing (stress test)
  - Failover testing
  
- [ ] 10. **SSL/TLS:**
  - Certificate configuration
  - HTTPS enforcement
  - HTTP → HTTPS redirect

#### NuGet Packages:

```xml
<PackageReference Include="Ocelot" Version="20.0.0" />
<PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="8.0.0" />
<PackageReference Include="Serilog.AspNetCore" Version="8.0.0" />
```

#### Kullanım Örnekleri:

**Frontend - API Calls:**
```javascript
// Önceden (direkt servisler):
await fetch('http://localhost:5001/api/users', { ... });
await fetch('http://localhost:5002/api/files', { ... });

// Gateway ile (tek endpoint):
await fetch('https://api.monitra.local/keeper/api/users', {
  headers: {
    'Authorization': `Bearer ${token}`
  }
});

await fetch('https://api.monitra.local/storage/api/files', {
  headers: {
    'Authorization': `Bearer ${token}`
  }
});
```

**Servisler artık basitleşir:**
```csharp
// MngKeeper - Auth middleware kaldırılabilir
// Gateway zaten validate etti

[ApiController]
[Route("api/users")]
public class UserController : ControllerBase
{
    // Gateway'den gelen request zaten authorized
    [HttpGet]
    public async Task<IActionResult> GetUsers()
    {
        // JWT claims gateway tarafından ekleniyor
        var userId = User.FindFirst("sub")?.Value;
        // ...
    }
}
```

---

### 6. MngScheduler Servisi 🕐 Zamanlanmış Görevler

**KARAR: Hangfire ile database-driven dinamik job scheduler ✅**

**Amaç:** Zamanlanmış görevleri yöneten, cron-based job execution sağlayan, MongoDB'den dinamik olarak job tanımlarını yükleyen servis.

#### Mimari:
```
┌──────────────┐
│   MongoDB    │ ← Job tanımları (cron, endpoint, body, headers)
│ (Job Config) │
└──────┬───────┘
       │ Dynamic loading (on startup + every 5 min)
       ↓
┌────────────────────┐
│  MngScheduler      │
│  - Hangfire        │
│  - Job Loader      │
│  - REST API        │
└─────────┬──────────┘
          │
          ├→ HTTP Calls (scheduled)
          ├→ RabbitMQ (events)
          └→ Dashboard (/hangfire)
```

#### Ana Özellikler:

**1. Database-Driven Jobs:**
- ✅ Job tanımları MongoDB'de saklanır
- ✅ Runtime'da job ekle/sil/güncelle (no deployment needed)
- ✅ Automatic sync MongoDB ↔ Hangfire
- ✅ Dynamic job loading (startup + periodic refresh)

**2. Scheduled HTTP Calls:**
- ✅ Herhangi bir endpoint'e cron-based çağrı
- ✅ POST, GET, PUT, DELETE support
- ✅ Custom headers (Authorization, API keys, etc.)
- ✅ JSON body payload
- ✅ Configurable timeout & retry

**3. Hangfire Features:**
- ✅ Web dashboard (job monitoring)
- ✅ Persistent storage (MongoDB)
- ✅ Automatic retry mechanism
- ✅ Cron expressions (flexible scheduling)
- ✅ Distributed execution (multiple instances)
- ✅ Fire-and-forget jobs
- ✅ Job history & statistics

**4. Management API:**
- ✅ REST API (CRUD operations)
- ✅ Trigger job immediately
- ✅ Enable/Disable jobs
- ✅ Job status & statistics
- ✅ JWT authentication

#### Kullanım Senaryoları:

**1. Database Backups:**
```json
{
  "jobName": "database-backup",
  "url": "https://api.monitra.local/storage/api/backup",
  "method": "POST",
  "body": "{\"type\":\"full\"}",
  "cronExpression": "0 2 * * *",
  "description": "Daily backup at 2 AM"
}
```

**2. Report Generation:**
```json
{
  "jobName": "weekly-report",
  "url": "https://api.monitra.local/keeper/api/reports/generate",
  "method": "POST",
  "body": "{\"type\":\"weekly\",\"email\":true}",
  "cronExpression": "0 9 * * 1",
  "description": "Weekly report on Monday 9 AM"
}
```

**3. Storage Cleanup:**
```json
{
  "jobName": "cleanup-temp",
  "url": "https://api.monitra.local/storage/api/cleanup",
  "method": "POST",
  "body": "{\"category\":\"temp\",\"olderThan\":24}",
  "cronExpression": "0 * * * *",
  "description": "Hourly temp file cleanup"
}
```

**4. Health Checks:**
```json
{
  "jobName": "health-check",
  "url": "https://api.monitra.local/monitor/api/health/check",
  "method": "POST",
  "cronExpression": "*/5 * * * *",
  "description": "Health check every 5 minutes"
}
```

**5. External API Integration:**
```json
{
  "jobName": "external-sync",
  "url": "https://external-system.com/api/webhook",
  "method": "POST",
  "body": "{\"source\":\"monitra\",\"timestamp\":\"{{now}}\"}",
  "headers": {
    "X-API-Key": "secret-key-123"
  },
  "cronExpression": "0 */3 * * *",
  "description": "Sync with external system every 3 hours"
}
```

**6. Domain-Specific Tasks:**
```json
{
  "jobName": "domain-123-processing",
  "url": "https://api.monitra.local/reactor/api/process",
  "method": "POST",
  "body": "{\"domainId\":\"123\",\"action\":\"daily\"}",
  "headers": {
    "Authorization": "Bearer {{admin-token}}",
    "X-Domain-Id": "123"
  },
  "cronExpression": "0 0 * * *",
  "description": "Daily processing for domain 123"
}
```

#### MongoDB Schema:

**ScheduledHttpJob Collection:**
```javascript
{
  "_id": "job_123",
  "jobName": "daily-sync",
  "description": "Daily data synchronization",
  
  // HTTP Configuration
  "url": "https://api.monitra.local/keeper/api/sync",
  "method": "POST",
  "body": "{\"action\":\"sync\",\"full\":true}",
  "contentType": "application/json",
  "headers": {
    "Authorization": "Bearer xyz123",
    "X-Custom-Header": "value"
  },
  
  // Scheduling
  "cronExpression": "0 2 * * *",
  "timeZone": "Europe/Istanbul",
  
  // Configuration
  "enabled": true,
  "timeoutSeconds": 60,
  "maxRetries": 3,
  
  // Multi-tenant
  "domainId": "domain_456",
  
  // Metadata
  "createdAt": "2024-10-31T10:00:00Z",
  "createdBy": "user_789",
  "updatedAt": "2024-10-31T12:00:00Z",
  "updatedBy": "user_789",
  
  // Statistics
  "statistics": {
    "totalExecutions": 30,
    "successfulExecutions": 29,
    "failedExecutions": 1,
    "lastExecutionAt": "2024-10-31T02:00:00Z",
    "lastExecutionStatus": "success",
    "lastExecutionDurationMs": 1250
  }
}
```

#### REST API Endpoints:

```http
# List all jobs
GET    /api/scheduler/jobs
       Response: [{ jobName, cron, url, enabled, ... }]

# Get job details
GET    /api/scheduler/jobs/{jobName}
       Response: { jobName, cron, url, body, headers, statistics, ... }

# Create new job
POST   /api/scheduler/jobs
       Body: { jobName, url, method, body, headers, cronExpression, ... }
       Response: { jobName, nextExecution, ... }

# Update job
PUT    /api/scheduler/jobs/{jobName}
       Body: { url, cronExpression, enabled, ... }
       Response: { jobName, updated fields }

# Delete job
DELETE /api/scheduler/jobs/{jobName}
       Response: 204 No Content

# Trigger job immediately
POST   /api/scheduler/jobs/{jobName}/trigger
       Response: 202 Accepted

# Enable job
POST   /api/scheduler/jobs/{jobName}/enable
       Response: { jobName, enabled: true }

# Disable job
POST   /api/scheduler/jobs/{jobName}/disable
       Response: { jobName, enabled: false }

# Get job execution history
GET    /api/scheduler/jobs/{jobName}/history
       Response: [{ executedAt, status, duration, error }]
```

#### Core Components:

**1. DynamicJobLoaderService (Background Service):**
```csharp
// Startup'ta ve her 5 dakikada bir MongoDB'den job'ları okur
// Hangfire'a kaydeder veya günceller
// Enabled = false olanları Hangfire'dan kaldırır

public class DynamicJobLoaderService : IHostedService
{
    // On startup: Load all jobs from MongoDB
    // Every 5 min: Refresh jobs (check for changes)
    // Sync: MongoDB → Hangfire
}
```

**2. HttpCallJob (Hangfire Job):**
```csharp
// Generic HTTP caller
// Retry mechanism
// Statistics tracking
// Event publishing

public class HttpCallJob
{
    [AutomaticRetry(Attempts = 3)]
    public async Task ExecuteAsync(
        string url,
        string method,
        string body,
        Dictionary<string, string> headers,
        string contentType,
        int timeoutSeconds,
        PerformContext context)
    {
        // Make HTTP call
        // Track statistics
        // Publish events
        // Handle errors
    }
}
```

**3. JobManagementService:**
```csharp
// CRUD operations for jobs
// Sync MongoDB ↔ Hangfire
// Enable/Disable jobs
// Trigger jobs manually

public class JobManagementService
{
    Task<ScheduledHttpJob> CreateJobAsync(...);
    Task<ScheduledHttpJob> UpdateJobAsync(...);
    Task DeleteJobAsync(string jobName);
    Task EnableJobAsync(string jobName);
    Task DisableJobAsync(string jobName);
    Task TriggerJobNowAsync(string jobName);
}
```

**4. ScheduledJobRepository:**
```csharp
// MongoDB operations
// CRUD for job configurations

public interface IScheduledJobRepository
{
    Task<List<ScheduledHttpJob>> GetAllAsync();
    Task<ScheduledHttpJob?> GetByNameAsync(string jobName);
    Task<ScheduledHttpJob> CreateAsync(ScheduledHttpJob job);
    Task<ScheduledHttpJob> UpdateAsync(ScheduledHttpJob job);
    Task DeleteAsync(string jobName);
}
```

#### Teknoloji Stack:

- **Framework:** ASP.NET Core 8.0 Web API
- **Scheduler:** Hangfire
- **Storage:** MongoDB (job configs + Hangfire storage)
- **Messaging:** RabbitMQ (event publishing)
- **Authentication:** JWT (KeyCloak)
- **Logging:** Serilog
- **HTTP Client:** IHttpClientFactory

#### NuGet Packages:

```xml
<PackageReference Include="Hangfire.Core" Version="1.8.6" />
<PackageReference Include="Hangfire.AspNetCore" Version="1.8.6" />
<PackageReference Include="Hangfire.Mongo" Version="1.10.0" />
<PackageReference Include="MongoDB.Driver" Version="2.23.0" />
<PackageReference Include="Microsoft.Extensions.Http" Version="8.0.0" />
<PackageReference Include="Serilog.AspNetCore" Version="8.0.0" />
```

#### Hangfire Dashboard:

```
Access: https://api.monitra.local/scheduler/hangfire

Features:
- ✅ Recurring jobs list
- ✅ Enqueued jobs
- ✅ Processing jobs
- ✅ Succeeded jobs (with duration)
- ✅ Failed jobs (with error details)
- ✅ Scheduled jobs (queue)
- ✅ Servers (active workers)
- ✅ Manual job trigger
- ✅ Retry failed jobs
```

#### Configuration:

**appsettings.json:**
```json
{
  "ConnectionStrings": {
    "MongoDB": "mongodb://localhost:27017/monitra_scheduler"
  },
  "Hangfire": {
    "ServerName": "MngScheduler",
    "WorkerCount": 5,
    "Queues": ["default", "critical", "background"],
    "JobRefreshIntervalMinutes": 5,
    "DashboardPath": "/hangfire",
    "DashboardTitle": "MonitraNG - Scheduler"
  }
}
```

**Program.cs:**
```csharp
// MongoDB
services.AddSingleton<IMongoClient>(sp => 
    new MongoClient(configuration.GetConnectionString("MongoDB")));

// Hangfire with MongoDB storage
services.AddHangfire(config =>
{
    config.UseMongoStorage(
        configuration.GetConnectionString("MongoDB"),
        new MongoStorageOptions
        {
            Prefix = "hangfire_",
            CheckConnection = true
        }
    );
});

// Hangfire Server
services.AddHangfireServer(options =>
{
    options.ServerName = "MngScheduler";
    options.WorkerCount = 5;
    options.Queues = new[] { "default", "critical", "background" };
});

// Dynamic Job Loader
services.AddHostedService<DynamicJobLoaderService>();

// Repositories & Services
services.AddScoped<IScheduledJobRepository, ScheduledJobRepository>();
services.AddScoped<JobManagementService>();
services.AddScoped<HttpCallJob>();

// HTTP Client
services.AddHttpClient();
```

#### Docker Configuration:

```yaml
# docker-compose.yml
mngsscheduler:
  build:
    context: ./MngScheduler
    dockerfile: Dockerfile
  container_name: monitra-scheduler
  expose:
    - "5005"
  environment:
    - ASPNETCORE_ENVIRONMENT=Production
    - ConnectionStrings__MongoDB=mongodb://mongodb:27017/monitra_scheduler
  depends_on:
    - mongodb
    - rabbitmq
  networks:
    - monitra-network
  restart: unless-stopped
```

#### Güvenlik Özellikleri:

- [ ] **JWT Authentication:** API endpoints protected
- [ ] **Dashboard Authentication:** Admin-only access
- [ ] **URL Whitelist:** Allowed domains for HTTP calls
- [ ] **Header Encryption:** Sensitive headers encrypted in DB
- [ ] **Rate Limiting:** Job creation limits per user
- [ ] **Audit Logging:** All job changes logged
- [ ] **Domain Isolation:** Jobs filtered by domainId

#### Event Publishing:

Job olayları RabbitMQ'ya publish edilir:
```
Events:
- job.created
- job.updated
- job.deleted
- job.executed.success
- job.executed.failed
- job.enabled
- job.disabled
```

#### İmplementasyon Adımları:

- [ ] 1. **Project Setup:**
  - MngScheduler.Api project
  - MngScheduler.Application layer
  - MngScheduler.Domain layer
  - MngScheduler.Infrastructure layer

- [ ] 2. **MongoDB Integration:**
  - ScheduledHttpJob model
  - Repository implementation
  - Index creation
  
- [ ] 3. **Hangfire Setup:**
  - Hangfire.Mongo configuration
  - Server options
  - Dashboard setup
  
- [ ] 4. **Dynamic Job Loader:**
  - Background service
  - MongoDB → Hangfire sync
  - Periodic refresh (5 min)
  
- [ ] 5. **HTTP Call Job:**
  - Generic HTTP caller
  - Retry mechanism
  - Statistics tracking
  - Error handling
  
- [ ] 6. **Management Service:**
  - CRUD operations
  - Enable/Disable
  - Trigger manually
  - Hangfire sync
  
- [ ] 7. **REST API:**
  - Controller implementation
  - Request validation
  - JWT authentication
  - Swagger documentation
  
- [ ] 8. **Dashboard Security:**
  - Authorization filter
  - Admin role check
  - JWT validation
  
- [ ] 9. **Event Publishing:**
  - RabbitMQ integration
  - Job lifecycle events
  - Execution events
  
- [ ] 10. **Testing:**
  - Unit tests
  - Integration tests
  - End-to-end tests
  - Load testing

#### Monitoring & Statistics:

**Per-Job Metrics:**
- Total executions
- Success rate
- Failure rate
- Average duration
- Last execution time
- Next execution time

**System Metrics:**
- Active jobs count
- Disabled jobs count
- Total HTTP calls today
- Failed jobs (last 24h)
- Average response time

**Alerts:**
- Job failed (after max retries)
- Job timeout
- High failure rate
- Scheduler service down

#### Kullanım Örnekleri:

**Frontend - Job Oluşturma:**
```javascript
// Create scheduled job via API
const response = await fetch('/api/scheduler/jobs', {
  method: 'POST',
  headers: {
    'Content-Type': 'application/json',
    'Authorization': `Bearer ${token}`
  },
  body: JSON.stringify({
    jobName: 'my-daily-task',
    url: 'https://api.monitra.local/keeper/api/process',
    method: 'POST',
    body: JSON.stringify({ action: 'process' }),
    headers: { 'Authorization': `Bearer ${adminToken}` },
    cronExpression: '0 9 * * *', // Daily at 9 AM
    enabled: true
  })
});

const job = await response.json();
console.log('Job created:', job.jobName);
console.log('Next execution:', job.nextExecution);
```

**Admin - Job Yönetimi:**
```javascript
// List all jobs
const jobs = await fetch('/api/scheduler/jobs').then(r => r.json());

// Trigger immediately
await fetch(`/api/scheduler/jobs/${jobName}/trigger`, { method: 'POST' });

// Disable job
await fetch(`/api/scheduler/jobs/${jobName}/disable`, { method: 'POST' });

// View in dashboard
window.open('https://api.monitra.local/scheduler/hangfire');
```

---

### 7. MngDataGateway Servisi 🗄️ MongoDB CRUD Gateway

**⚠️ PLANLAMA AŞAMASINDA - Detaylar belirlenecek**

**Amaç:** MongoDB'ye genel amaçlı CRUD ve Get işlemleri sağlayan gateway servisi.

#### Temel Özellikler (Planlanıyor):

**1. CRUD Operations:**
- ✅ Create (Insert documents)
- ✅ Read (Query documents)
- ✅ Update (Modify documents)
- ✅ Delete (Remove documents)
- ✅ Get (Retrieve by ID)

**2. Generic MongoDB Gateway:**
- REST API interface
- Dynamic collection operations
- Query building
- Filtering & sorting
- Pagination support

**3. Multi-tenant Support:**
- Domain-based isolation
- Collection-level access control
- JWT authentication

#### Teknoloji Stack (Taslak):

- **Framework:** ASP.NET Core 8.0 Web API
- **Database:** MongoDB
- **Authentication:** JWT (KeyCloak)
- **Logging:** Serilog

#### REST API Endpoints (Taslak):

```http
# Generic CRUD operations
POST   /api/data/{collection}                    # Create
GET    /api/data/{collection}                    # List with query
GET    /api/data/{collection}/{id}               # Get by ID
PUT    /api/data/{collection}/{id}               # Update
DELETE /api/data/{collection}/{id}               # Delete

# Query operations
POST   /api/data/{collection}/query              # Advanced query
GET    /api/data/{collection}/count              # Count documents
```

#### İmplementasyon Adımları:

- [ ] Gereksinim analizi ve detaylı tasarım
- [ ] REST API endpoint'leri tanımlanacak
- [ ] Security model belirlenecek
- [ ] Generic repository pattern
- [ ] Integration tests
- [ ] Documentation

#### Notlar:

- ⚠️ **Detaylar sonra belirlenecek**
- Bu servisin kapsamı, güvenlik modeli ve kullanım senaryoları netleştirilecek
- Diğer servislerle entegrasyonu planlanacak
- API Gateway routing eklenecek

---

### 8. MinIO Infrastructure Setup 📁 Altyapı Kurulumu

**Not:** Bu bölüm MngStorage servisinin kullanacağı MinIO altyapısını kurar.

#### A. Mimari Kararlar

**Storage Backend:** MinIO
- ✅ S3 compatible API
- ✅ Self-hosted, tam kontrol
- ✅ Docker container (kolay deploy)
- ✅ Bucket-per-domain izolasyonu
- ✅ Native access policies
- ✅ Admin web UI

**Multi-Tenant İzolasyon:**
- Her domain için ayrı bucket: `domain-{domainId}`
- Bucket-level access policies
- CreateDomain → MinIO bucket otomatik oluşturma
- DeleteDomain → Bucket temizleme/arşivleme

#### B. Bucket Yapısı (Her Domain İçin)
```
domain-{domainId}/
├── users/
│   ├── profiles/{userId}/avatar.jpg        # Profil fotoları
│   └── uploads/{userId}/{fileId}.ext       # User yüklemeleri
├── reports/
│   ├── pdf/{reportId}.pdf                  # Üretilen PDF raporlar
│   ├── excel/{reportId}.xlsx               # Excel raporlar
│   └── images/{imageId}.png                # Rapor görselleri
├── assets/
│   ├── images/{assetId}/photo.jpg          # Asset görselleri
│   └── documents/{assetId}/manual.pdf      # Asset dokümanları
├── backups/
│   ├── database/{yyyy-MM-dd}/backup.bak    # DB yedekleri
│   └── files/{yyyy-MM-dd}/archive.zip      # Dosya yedekleri
└── temp/
    └── {processId}/temp-file.tmp           # Geçici dosyalar (TTL)
```

#### C. Dosya Kategorileri
1. **User Files**
   - Profile photos (max 5MB, jpg/png)
   - Document uploads (max 50MB, pdf/docx/xlsx)

2. **System Generated**
   - PDF reports (auto-generated)
   - Excel exports (auto-generated)
   - Asset images (from services)

3. **Backups**
   - Database dumps (manual/scheduled)
   - Configuration backups
   - File archives

4. **Temporary**
   - Processing temp files (auto-delete 24h)
   - Upload staging area

#### D. MinIO Container Setup
```yaml
# docker-compose.yml eklenecek
minio:
  image: minio/minio:latest
  container_name: monitra-minio
  ports:
    - "9000:9000"    # API
    - "9001:9001"    # Console UI
  environment:
    MINIO_ROOT_USER: admin
    MINIO_ROOT_PASSWORD: ${MINIO_ROOT_PASSWORD}
  volumes:
    - minio-data:/data
  command: server /data --console-address ":9001"
  networks:
    - monitra-network
```

#### E. Storage Service İmplementasyonu

**1. IStorageService Interface:**
```csharp
Task<string> UploadFileAsync(string domainId, string category, 
    string fileName, Stream content, string contentType);
Task<Stream> DownloadFileAsync(string domainId, string filePath);
Task DeleteFileAsync(string domainId, string filePath);
Task<bool> FileExistsAsync(string domainId, string filePath);
Task InitializeDomainStorageAsync(string domainId);
Task DeleteDomainStorageAsync(string domainId, bool archive = true);
```

**2. MinioStorageService İmplementasyonu:**
- Minio.AspNetCore NuGet package
- Bucket management (create/delete)
- File operations (upload/download/delete)
- Access policy configuration
- Presigned URL generation (temporary access)

**3. Domain Event Handlers:**
```
DomainCreated → InitializeDomainStorageAsync()
  - Create bucket: domain-{domainId}
  - Set bucket policy (private)
  - Create standard folders
  - Set quota limits

DomainDeleted → DeleteDomainStorageAsync()
  - Option 1: Archive to backup bucket
  - Option 2: Permanent delete
```

#### F. API Endpoints
```http
# Upload
POST   /api/storage/upload
  Query: domain, category (users/reports/assets/backups)
  Body: multipart/form-data
  
# Download
GET    /api/storage/download/{domainId}/{filePath}
  Response: File stream
  
# Delete
DELETE /api/storage/{domainId}/{filePath}

# List
GET    /api/storage/{domainId}/files?category=reports&prefix=pdf/

# Temporary URL (for external services)
GET    /api/storage/{domainId}/temp-url?filePath=...&expiresIn=3600
```

#### G. Güvenlik & Limitler
- [ ] Domain-based authorization (her domain sadece kendi bucket'ına erişir)
- [ ] File type validation (whitelist: jpg,png,pdf,xlsx,docx,bak,zip)
- [ ] File size limits per category:
  - Profiles: 5MB
  - Documents: 50MB
  - Reports: 100MB
  - Backups: 5GB
- [ ] Bucket quota per domain: 10GB default (configurable)
- [ ] Virus scanning (ClamAV integration - opsiyonel)
- [ ] Rate limiting (upload: 10 req/min per domain)

#### H. İmplementasyon Adımları
- [ ] 1. Docker compose'a MinIO ekleme
- [ ] 2. Minio.AspNetCore NuGet package yükleme
- [ ] 3. IStorageService interface tanımlama
- [ ] 4. MinioStorageService implementation
- [ ] 5. Domain event handlers (bucket init/delete)
- [ ] 6. Storage API controller
- [ ] 7. File validation middleware
- [ ] 8. Integration tests
- [ ] 9. Admin UI'dan bucket yönetimi
- [ ] 10. Backup/restore stratejisi

#### I. Kullanım Senaryoları
```csharp
// Senaryo 1: User profil fotoğrafı yükleme
await _storageService.UploadFileAsync(
    domainId: user.DomainId,
    category: "users/profiles/" + user.Id,
    fileName: "avatar.jpg",
    content: fileStream,
    contentType: "image/jpeg"
);

// Senaryo 2: PDF rapor üretme ve kaydetme
var pdfStream = _reportGenerator.GeneratePdf(reportData);
var filePath = await _storageService.UploadFileAsync(
    domainId: report.DomainId,
    category: "reports/pdf",
    fileName: $"{report.Id}.pdf",
    content: pdfStream,
    contentType: "application/pdf"
);

// Senaryo 3: Database backup
var backupStream = await _backupService.CreateBackupAsync(domainId);
await _storageService.UploadFileAsync(
    domainId: domainId,
    category: "backups/database",
    fileName: $"{DateTime.UtcNow:yyyy-MM-dd-HHmmss}.bak",
    content: backupStream,
    contentType: "application/octet-stream"
);
```

---

## 📅 Tahmini Süre

| Görev | Süre | Öncelik |
|-------|------|---------|
| User CRUD Test | 2-3 saat | 🔴 Yüksek |
| Group CRUD Test | 1-2 saat | 🔴 Yüksek |
| RabbitMQ Events | 1 gün | 🟡 Orta |
| MngStorage Servis (Project Setup) | 1 gün | 🟡 Orta |
| MngStorage Servis (Core Features) | 2-3 gün | 🟡 Orta |
| MngStorage Servis (gRPC & Tests) | 1-2 gün | 🟡 Orta |
| API Gateway (Ocelot Setup) | 1 gün | 🟡 Orta |
| API Gateway (Advanced Features) | 1-2 gün | 🟢 Düşük |
| MngScheduler Servis (Hangfire Setup) | 1 gün | 🟡 Orta |
| MngScheduler Servis (Dynamic Jobs) | 1-2 gün | 🟡 Orta |
| MngScheduler Servis (API & Dashboard) | 1 gün | 🟢 Düşük |
| MngDataGateway Servis | TBD | ⏳ Planlanıyor |
| MinIO Infrastructure Setup | 3-4 saat | 🟢 Düşük |

---

## 🎯 Kararlar

### ✅ API Gateway - KARARLAŞTIRILDI:
1. **Teknoloji:** ✅ Ocelot (ASP.NET Core)
2. **Endpoint:** ✅ https://api.monitra.local (tek giriş noktası)
3. **Routing:**
   - `/keeper/*` → MngKeeper
   - `/storage/*` → MngStorage
   - `/scheduler/*` → MngScheduler
   - `/data/*` → MngDataGateway (planlanıyor)
   - `/monitor/*` → MngMonitor
   - `/auth/*` → KeyCloak
4. **Authentication:** ✅ JWT validation (merkezi, KeyCloak)
5. **Rate Limiting:** ✅ Client/IP bazlı (30-500 req/min)
6. **CORS:** ✅ Frontend origin whitelist
7. **SSL/TLS:** ✅ HTTPS enforcement (production)
8. **Logging:** ✅ Request/Response logging (Serilog)
9. **Backend izolasyonu:** ✅ Servisler external network'e expose edilmez

### ✅ Zamanlanmış Görevler - KARARLAŞTIRILDI:
1. **Teknoloji:** ✅ Hangfire (ASP.NET Core)
2. **Storage:** ✅ MongoDB (job configs + Hangfire storage)
3. **Job Tanımlama:** ✅ Database-driven (MongoDB'den dinamik yükleme)
4. **Job Types:** ✅ Scheduled HTTP calls (cron-based)
5. **Methods:** ✅ POST, GET, PUT, DELETE support
6. **Dynamic Loading:** ✅ Startup + periodic refresh (5 min)
7. **Management:** ✅ REST API (CRUD operations)
8. **Dashboard:** ✅ Hangfire web UI (admin-only)
9. **Retry:** ✅ Automatic retry mechanism (max 3)
10. **Events:** ✅ RabbitMQ publishing (job lifecycle)
11. **Authentication:** ✅ JWT (KeyCloak)
12. **Statistics:** ✅ Per-job execution tracking

### ✅ Dosyalama Sistemi - KARARLAŞTIRILDI:
1. **Mimari:** ✅ Ayrı mikroservis (MngStorage)
2. **Storage backend:** ✅ MinIO (Self-hosted S3)
3. **API:** ✅ REST + gRPC (service-to-service)
4. **Authentication:** ✅ JWT (KeyCloak tokens)
5. **Dosya kategorileri:** ✅ users, reports, assets, backups, temp
6. **Klasör yapısı:** ✅ Bucket-per-domain (domain-{domainId})
7. **File size limits:** ✅ SINIR YOK (streaming upload)
8. **Metadata storage:** ✅ MongoDB
9. **Event publishing:** ✅ RabbitMQ
10. **Quota:** ✅ 10GB per domain (configurable)
11. **Virus scan:** ⏳ Opsiyonel (ClamAV - gelecekte)
12. **Versioning:** ⏳ MinIO versioning support (gelecekte)
13. **Bucket oluşturma:** ✅ CreateDomain event handler ile otomatik
14. **Encapsulation:** ✅ MinIO'yu sadece MngStorage bilir

---

## 📝 Notlar

- User ve Group CRUD'lar zaten hazır, sadece test edilecek
- RabbitMQ publisher servisi var, sadece event handler'lar eklenecek
- **API Gateway:** Ocelot ile tek giriş noktası, merkezi authentication
- **MngStorage servisi:** Dosyalama için yeni mikroservis geliştirilecek
- **MngScheduler servisi:** Zamanlanmış görevler için yeni mikroservis geliştirilecek
- **MngDataGateway servisi:** MongoDB CRUD gateway (planlama aşamasında, detaylar sonra)
- **MinIO encapsulation:** Sadece MngStorage servisi MinIO'yu bilir
- **Streaming upload:** Dosya boyut sınırı yok, memory efficient
- **Database-driven jobs:** Job tanımları MongoDB'de, dinamik yükleme
- **Hangfire dashboard:** Web UI ile job monitoring, manual trigger
- **JWT authentication:** KeyCloak token'ları API Gateway'de validate ediliyor
- **Backend izolasyonu:** Servisler sadece internal network'te, external'a kapalı
- **Air-gapped deployment:** Tüm bileşenler internetsiz ortamda çalışabilir
- **Rate limiting:** Gateway seviyesinde API throttling
- CreateDomain event handler storage yapısını otomatik kuracak
- Her domain'in dosyaları tamamen izole ve bağımsız (bucket-per-domain)
- Scheduled jobs runtime'da eklenip düzenlenebilir (no deployment needed)

**Yeni session'da bu roadmap'e göre ilerleriz!**

