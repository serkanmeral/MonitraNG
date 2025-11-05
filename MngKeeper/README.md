# MngKeeper API

**Identity & Access Management (IAM) Microservice**

MngKeeper, MonitraNG ekosisteminin merkezi kimlik ve yetkilendirme servisidir. Multi-tenant domain yönetimi, kullanıcı/grup yönetimi ve JWT token tabanlı authentication sağlar.

---

## 🎯 Özellikler

- ✅ **Multi-Tenant Domain Management** - Her domain kendi database, realm ve storage ile izole
- ✅ **Keycloak Integration** - Enterprise-grade authentication
- ✅ **JWT Token Authentication** - Custom claims (user_groups, isAdmin)
- ✅ **Pipeline Architecture** - 11 adımlı domain creation workflow
- ✅ **Redis Cache** - Yüksek performans için user/group cache
- ✅ **RabbitMQ Events** - Real-time event publishing
- ✅ **MinIO Storage** - S3-compatible object storage
- ✅ **Clean Architecture** - Domain, Application, Infrastructure, Presentation katmanları

---

## 🚀 Başlangıç

### Gereksinimler

- .NET 9.0 SDK
- Docker & Docker Compose
- MongoDB 7.0
- Keycloak 23.0
- Redis 7
- RabbitMQ 3
- MinIO (latest)

### Infrastructure Başlatma

```bash
cd ApplicationResources/mng_common
docker-compose up -d
```

**Servisler:**
- MongoDB: `localhost:27017` (admin/admin123)
- Keycloak: `localhost:8080` (admin/admin123)
- Redis: `localhost:6379` (redis123)
- RabbitMQ: `localhost:5672` (admin/admin123)
- RabbitMQ Management: `localhost:15672` (admin/admin123)
- MinIO API: `localhost:9090` (admin/admin123)
- MinIO Console: `localhost:9091` (admin/admin123)
- Redis Commander: `localhost:8001`
- Seq Logging: `localhost:5341` (Admin123!)
- Mongo Express: `localhost:8081` (admin/admin123)

### MngKeeper Başlatma

```bash
cd MngKeeper/Presentation/MngKeeper.Api
dotnet run
```

**API:** `https://localhost:5001`

---

## 📬 API Endpoints

### 🏢 Domain Management

#### Create Domain
**Yeni bir domain (tenant) oluşturur. 11 adımlı pipeline:**
- MongoDB database oluşturma
- Keycloak realm oluşturma
- Default groups ve admin user
- Redis cache initialization
- RabbitMQ event publishing
- MinIO bucket creation

```http
POST https://localhost:5001/api/domain
Content-Type: application/json

{
  "domainName": "acme-corp",
  "displayName": "ACME Corporation",
  "adminEmail": "admin@acme.com",
  "adminPassword": "SecurePass123!"
}
```

**Response:**
```json
{
  "domainId": "507f1f77bcf86cd799439011",
  "domainName": "acme-corp",
  "databaseName": "mng_acme-corp",
  "adminUsername": "acme-corp_admin",
  "adminEmail": "admin@acme.com",
  "createdAt": "2025-11-05T10:00:00Z",
  "isSuccess": true,
  "message": "Domain 'acme-corp' created successfully with 11 steps",
  "failedStep": null
}
```

**Domain Naming Rules:**
- Lowercase letters, numbers, hyphens only
- No underscores or special characters
- Must be unique

---

#### Get All Domains
```http
GET https://localhost:5001/api/domain
```

**Response:**
```json
{
  "domains": [
    {
      "id": "507f1f77bcf86cd799439011",
      "name": "acme-corp",
      "displayName": "ACME Corporation",
      "status": "Active",
      "createdAt": "2025-11-05T10:00:00Z"
    }
  ]
}
```

---

#### Get Domain by ID
```http
GET https://localhost:5001/api/domain/{domainId}
```

---

### 🔐 Authentication

#### Get Token
**Domain kullanıcısı için JWT token alır.**

```http
POST https://localhost:5001/api/auth/token
Content-Type: application/json

{
  "username": "acme-corp_admin",
  "password": "SecurePass123!",
  "domain": "acme-corp"
}
```

**Response:**
```json
{
  "accessToken": "eyJhbGciOiJSUzI1NiIsInR5cC...",
  "refreshToken": "eyJhbGciOiJIUzI1NiIsInR5cC...",
  "tokenType": "Bearer",
  "expiresIn": 300,
  "refreshExpiresIn": 1800
}
```

**Token Claims (Decoded):**
```json
{
  "sub": "user-uuid",
  "email": "admin@acme.com",
  "preferred_username": "acme-corp_admin",
  "email_verified": true,
  "user_groups": ["admins"],     // Custom claim
  "isAdmin": true                 // Custom claim
}
```

---

#### Refresh Token
**Süresi dolmuş access token'ı yeniler.**

```http
POST https://localhost:5001/api/auth/refresh
Content-Type: application/json

{
  "refreshToken": "eyJhbGciOiJIUzI1NiIsInR5cC...",
  "domain": "acme-corp"
}
```

---

#### Revoke Token (Logout)
**Refresh token'ı iptal eder.**

```http
POST https://localhost:5001/api/auth/revoke
Content-Type: application/json

{
  "refreshToken": "eyJhbGciOiJIUzI1NiIsInR5cC...",
  "domain": "acme-corp"
}
```

---

### 🔧 Admin Operations

#### Configure Realm Mappers
**Keycloak realm için protocol mapper'ları yapılandırır (user_groups, isAdmin).**

```http
POST https://localhost:5001/api/admin/realms/acme-corp/configure-mappers
```

**Response:**
```json
{
  "realmName": "acme-corp",
  "mappersAdded": ["user_groups", "isAdmin"],
  "message": "Successfully configured 2 mapper(s) for realm acme-corp"
}
```

**Not:** Her domain için **1 kere** çalıştırılmalı (domain oluşturulduktan sonra).

---

### 👥 User Management

#### Create User
```http
POST https://localhost:5001/api/user
Authorization: Bearer {token}
Content-Type: application/json

{
  "username": "john.doe",
  "email": "john@acme.com",
  "password": "SecurePass123!",
  "firstName": "John",
  "lastName": "Doe",
  "groups": ["users"]
}
```

#### Get Users
```http
GET https://localhost:5001/api/user?page=1&pageSize=20
Authorization: Bearer {token}
```

#### Get User by ID
```http
GET https://localhost:5001/api/user/{userId}
Authorization: Bearer {token}
```

#### Update User
```http
PUT https://localhost:5001/api/user/{userId}
Authorization: Bearer {token}
Content-Type: application/json

{
  "firstName": "John Updated",
  "lastName": "Doe",
  "email": "john.updated@acme.com"
}
```

#### Delete User
```http
DELETE https://localhost:5001/api/user/{userId}
Authorization: Bearer {token}
```

---

### 👪 Group Management

#### Create Group
```http
POST https://localhost:5001/api/group
Authorization: Bearer {token}
Content-Type: application/json

{
  "name": "developers",
  "description": "Development team"
}
```

#### Get Groups
```http
GET https://localhost:5001/api/group
Authorization: Bearer {token}
```

#### Update Group
```http
PUT https://localhost:5001/api/group/{groupId}
Authorization: Bearer {token}
Content-Type: application/json

{
  "name": "developers",
  "description": "Updated description"
}
```

#### Delete Group
```http
DELETE https://localhost:5001/api/group/{groupId}
Authorization: Bearer {token}
```

---

### 📊 Health & Monitoring

#### Health Check
```http
GET https://localhost:5001/api/health
```

#### API Version
```http
GET https://localhost:5001/api/version/short
```

#### API Info
```http
GET https://localhost:5001/api/apidocs/info
```

---

## 🔄 Tam Domain Oluşturma & Token Alma Süreci

### Adım 1: Domain Oluştur

```bash
curl -X POST https://localhost:5001/api/domain \
  -H "Content-Type: application/json" \
  -d '{
    "domainName": "acme-corp",
    "displayName": "ACME Corporation",
    "adminEmail": "admin@acme.com",
    "adminPassword": "SecurePass123!"
  }'
```

**Oluşturulanlar:**
- ✅ MongoDB Database: `mng_acme-corp`
- ✅ Collections: `@datasets`, `@dataset_categories`
- ✅ Keycloak Realm: `acme-corp`
- ✅ Admin User: `acme-corp_admin`
- ✅ Groups: admins, managers, users, guests
- ✅ Redis Cache: `mngkeeper:domain:acme-corp:*`
- ✅ RabbitMQ Event: `system.mngkeeper.domain.created`
- ✅ MinIO Bucket: `mng-acme-corp` (folders: system, data, backups)

---

### Adım 2: Mapper'ları Yapılandır

```bash
curl -X POST https://localhost:5001/api/admin/realms/acme-corp/configure-mappers
```

**Eklenen Mapper'lar:**
- ✅ `user_groups` - Kullanıcının grupları
- ✅ `isAdmin` - Admin kontrolü

---

### Adım 3: Token Al

```bash
curl -X POST https://localhost:5001/api/auth/token \
  -H "Content-Type: application/json" \
  -d '{
    "username": "acme-corp_admin",
    "password": "SecurePass123!",
    "domain": "acme-corp"
  }'
```

**Response:**
```json
{
  "accessToken": "eyJhbGciOiJSUzI1NiIsInR5cC...",
  "refreshToken": "eyJhbGciOiJIUzI1NiIsInR5cC...",
  "tokenType": "Bearer",
  "expiresIn": 300,
  "refreshExpiresIn": 1800
}
```

---

### Adım 4: Token'ı Decode Et

**https://jwt.io** adresinde `accessToken` değerini yapıştırın:

```json
{
  "sub": "user-uuid",
  "email": "admin@acme.com",
  "preferred_username": "acme-corp_admin",
  "email_verified": true,
  "user_groups": ["admins"],
  "isAdmin": true,
  "exp": 1730820000,
  "iat": 1730819700
}
```

---

## 🏗️ Architecture

### Pipeline Pattern - Domain Creation

```
CreateDomainCommand
    ↓
DomainCreationPipeline
    ↓
┌────────────────────────────────────┐
│ 1. ValidateDomain                  │
│ 2. CreateDomainEntity              │
│ 3. CreateDatabase                  │
│ 4. InitializeDatabaseCollections   │
│ 5. CreateKeycloakRealm             │
│ 6. CreateDefaultGroups             │
│ 7. CreateAdminUser                 │
│ 8. PublishDomainCreatedEvent       │
│ 9. InitializeDomainCache           │
│ 10. CreateMinIOBucket              │
│ 11. ActivateDomain                 │
└────────────────────────────────────┘
    ↓
Success/Failure with Rollback
```

**Rollback:** Herhangi bir step başarısız olursa, önceki adımlar otomatik geri alınır.

---

### Redis Cache Structure

```
mngkeeper:domain:{domainName}:users:{userId}
mngkeeper:domain:{domainName}:groups:{groupId}
mngkeeper:domain:{domainName}:metadata
```

**Metadata Example:**
```json
{
  "usersLastUpdate": "2025-11-05T10:00:00Z",
  "groupsLastUpdate": "2025-11-05T10:00:00Z",
  "usersCount": 1,
  "groupsCount": 4,
  "status": "ready"
}
```

---

### RabbitMQ Events

**Exchange:** `mng.topics` (topic, durable)

**Domain Created Event:**
```json
{
  "eventId": "uuid",
  "eventType": "system.mngkeeper.domain.created",
  "timestamp": "2025-11-05T10:00:00Z",
  "source": "MngKeeper",
  "version": "1.0",
  "payload": {
    "domainId": "507f1f77bcf86cd799439011",
    "domainName": "acme-corp",
    "databaseName": "mng_acme-corp",
    "realmName": "acme-corp",
    "bucketName": "mng-acme-corp",
    "status": "Active",
    "adminEmail": "admin@acme.com",
    "createdAt": "2025-11-05T10:00:00Z"
  }
}
```

**Routing Key:** `system.mngkeeper.domain.created`

---

### MinIO Bucket Structure

Her domain için:
```
mng-{domainName}/
├── system/       # Sistem dosyaları (config, templates)
├── data/         # Domain verileri (user uploads, datasets)
└── backups/      # Yedek dosyaları
```

**Access:**
- Console: http://localhost:9091
- API: http://localhost:9090

---

## 🔧 Configuration

### appsettings.json

```json
{
  "MngKeeperSettings": {
    "OpenApiServerPath": "https://localhost:5001",
    "MongoDB": {
      "ConnectionString": "mongodb://admin:admin123@localhost:27017",
      "DatabaseName": "mngkeeper"
    },
    "RabbitMQ": {
      "Host": "localhost",
      "Port": 5672,
      "Username": "admin",
      "Password": "admin123",
      "VirtualHost": "/"
    },
    "Redis": {
      "ConnectionString": "localhost:6379,password=redis123"
    },
    "Keycloak": {
      "BaseUrl": "http://localhost:8080",
      "AdminUsername": "admin",
      "AdminPassword": "admin123",
      "ClientId": "mng-keeper-admin",
      "ClientSecret": "your-client-secret",
      "DefaultAdminPassword": "Admin123!"
    },
    "MinIO": {
      "Endpoint": "localhost:9090",
      "AccessKey": "admin",
      "SecretKey": "admin123",
      "UseSSL": false,
      "Region": "us-east-1"
    }
  }
}
```

---

## 📝 Kullanım Örnekleri

### PowerShell ile Domain Oluşturma

```powershell
# 1. Domain oluştur
$body = @{
    domainName = "acme-corp"
    displayName = "ACME Corporation"
    adminEmail = "admin@acme.com"
    adminPassword = "SecurePass123!"
} | ConvertTo-Json

$domain = Invoke-RestMethod -Uri "https://localhost:5001/api/domain" `
  -Method POST `
  -Body $body `
  -ContentType "application/json" `
  -SkipCertificateCheck

Write-Host "Domain ID: $($domain.domainId)"

# 2. Mapper'ları yapılandır
Invoke-RestMethod -Uri "https://localhost:5001/api/admin/realms/acme-corp/configure-mappers" `
  -Method POST `
  -SkipCertificateCheck

# 3. Token al
$tokenBody = @{
    username = "acme-corp_admin"
    password = "SecurePass123!"
    domain = "acme-corp"
} | ConvertTo-Json

$tokenResponse = Invoke-RestMethod -Uri "https://localhost:5001/api/auth/token" `
  -Method POST `
  -Body $tokenBody `
  -ContentType "application/json" `
  -SkipCertificateCheck

$token = $tokenResponse.accessToken
Write-Host "Access Token: $token"

# 4. Token ile API çağrısı yap
$headers = @{
    "Authorization" = "Bearer $token"
}

$users = Invoke-RestMethod -Uri "https://localhost:5001/api/user" `
  -Headers $headers `
  -SkipCertificateCheck

Write-Host "User count: $($users.Count)"
```

---

### cURL ile Token Alma

```bash
# Token al
curl -X POST "https://localhost:5001/api/auth/token" \
  -H "Content-Type: application/json" \
  -d '{
    "username": "acme-corp_admin",
    "password": "SecurePass123!",
    "domain": "acme-corp"
  }' \
  -k | jq -r '.accessToken' > token.txt

# Token'ı kullan
curl -X GET "https://localhost:5001/api/user" \
  -H "Authorization: Bearer $(cat token.txt)" \
  -k
```

---

## 🧪 Test Verileri

### Template Dosyalar

**Minimal Domain:**
```json
{
  "domainName": "test-minimal",
  "displayName": "Test Minimal",
  "adminEmail": "admin@test.com",
  "adminPassword": "Admin123!"
}
```

**Dosya:** `ApplicationResources/test_data/mng_keeper/create_domain/test-domain-minimal.json`

---

## 🐳 Docker & Development

### Self-Signed Certificate

Development ortamında self-signed certificate otomatik oluşturulur:

```json
"CertificateSettings": {
  "DNS": "localhost"
}
```

**Production:** Signed certificate kullanın:

```json
"CertificateSettings": {
  "MNG_CERT_FILE": "path/to/cert.crt",
  "MNG_KEY_FILE": "path/to/cert.key"
}
```

---

## 📊 Monitoring & Logging

### Serilog

**Console + Seq** sinks kullanılır.

**Seq Dashboard:** http://localhost:5341

**Filter Examples:**
```
@Message like '%MAPPER%'
@Message like '%REALM%'
@Level = 'Error'
Application = 'MngKeeper.Api'
```

---

### Redis Commander

**URL:** http://localhost:8001

**Kullanım:**
- Domain cache'lerini görüntüleme: `mngkeeper:domain:*`
- Hash yapılarını inceleme
- TTL kontrolü
- Memory usage

---

### RabbitMQ Management

**URL:** http://localhost:15672

**Kullanım:**
- Exchanges: `mng.topics`
- Message rates
- Connections & channels

---

## 🔍 Troubleshooting

### Keycloak Client Secret

1. http://localhost:8080/admin/ → Master realm
2. Clients → `mng-keeper-admin`
3. Credentials sekmesi → Client secret

`appsettings.json` dosyasını güncelleyin:
```json
"ClientSecret": "your-copied-secret"
```

---

### Redis Cache Göremiyorum

**Prefix kontrolü:** Redis'te tüm key'ler `mngkeeper:` prefix'i ile başlar.

```bash
# Tüm key'leri listele
docker exec redis redis-cli -a redis123 --no-auth-warning KEYS "mngkeeper:*"

# Domain spesifik
docker exec redis redis-cli -a redis123 --no-auth-warning KEYS "mngkeeper:domain:acme-corp:*"
```

---

### MinIO Bucket Görünmüyor

**Console:** http://localhost:9091 (admin/admin123)

**CLI:**
```bash
docker exec minio sh -c "mc alias set local http://localhost:9000 admin admin123 && mc ls local"
```

---

## 🎯 Roadmap

**Tamamlanan:**
- ✅ Domain Creation Pipeline (11 steps)
- ✅ Authentication API (token, refresh, revoke)
- ✅ Infrastructure Integration (MongoDB, Keycloak, Redis, RabbitMQ, MinIO)
- ✅ Clean Architecture
- ✅ Pipeline Pattern

**Devam Eden:**
- 🔄 User Management (MediatR commands/queries)
- 🔄 Group Management

**Planlanan:**
- ⏸️ WebSocket Gateway integration
- ⏸️ Dataset Management (MngDataGateway ile)
- ⏸️ File Storage API (MinIO integration)
- ⏸️ Audit logging
- ⏸️ Rate limiting

Detaylı roadmap: [ROADMAP_MngKeeper.md](../ROADMAP_MngKeeper.md)

---

## 📚 Ek Kaynaklar

- **Swagger UI:** https://localhost:5001/swagger
- **GraphQL Playground:** https://localhost:5001/graphql
- **Seq Logs:** http://localhost:5341
- **Keycloak Admin:** http://localhost:8080/admin
- **Mongo Express:** http://localhost:8081
- **RabbitMQ Management:** http://localhost:15672
- **MinIO Console:** http://localhost:9091
- **Redis Commander:** http://localhost:8001

---

## 👨‍💻 Development

### Build

```bash
dotnet build
```

### Run

```bash
dotnet run --project Presentation/MngKeeper.Api
```

### Test

```bash
dotnet test
```

---

## 📄 License

MIT License - MonitraNG Project

---

**Son Güncelleme:** 2025-11-05  
**Version:** 1.0.0  
**Maintainer:** MonitraNG Team
