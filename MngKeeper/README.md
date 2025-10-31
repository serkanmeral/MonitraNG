# MngKeeper - Multi-tenant Management System

MngKeeper, çok kiracılı (multi-tenant) yönetim sistemi için geliştirilmiş modern bir .NET 8.0 API projesidir. Keycloak tabanlı kimlik doğrulama, MongoDB veritabanı ve Clean Architecture pattern'i kullanır.

## 🏗️ Proje Yapısı

```
MngKeeper/
├── Core/
│   ├── MngKeeper.Application/     # Application katmanı (CQRS, MediatR)
│   └── MngKeeper.Domain/         # Domain katmanı (Entities, Interfaces)
├── Infrastructure/
│   ├── MngKeeper.Infrastructure/ # External services (Keycloak, MQTT, Redis)
│   └── MngKeeper.Persistence/    # Data access layer (MongoDB)
└── Presentation/
    └── MngKeeper.Api/            # Web API (Controllers, Middleware)
```

## 🚀 Teknoloji Stack

- **Framework:** .NET 8.0
- **Architecture:** Clean Architecture
- **Pattern:** CQRS + MediatR
- **Database:** MongoDB
- **Authentication:** Keycloak
- **Message Broker:** RabbitMQ
- **Cache:** Redis
- **IoT Communication:** MQTT
- **Documentation:** Swagger/OpenAPI
- **GraphQL:** HotChocolate

## 📋 Özellikler

### 🔐 Kimlik Doğrulama ve Yetkilendirme
- Keycloak tabanlı JWT token kimlik doğrulama
- Domain-based multitenancy
- Role-based access control (RBAC)
- Token tabanlı domain izolasyonu

### 🏢 Domain Yönetimi
- Domain oluşturma, güncelleme, silme
- Her domain için ayrı Keycloak realm
- Her domain için ayrı MongoDB veritabanı
- Varsayılan admin kullanıcısı ve grupları

### 👥 Kullanıcı ve Grup Yönetimi
- Kullanıcı CRUD operasyonları
- Grup CRUD operasyonları
- Kullanıcı-grup ilişkilendirme
- İzin yönetimi

### 🔌 Entegrasyon Servisleri
- MQTT broker entegrasyonu
- Redis cache entegrasyonu
- RabbitMQ message broker
- Event-driven architecture

## 🛠️ Kurulum ve Çalıştırma

### Gereksinimler
- .NET 8.0 SDK
- MongoDB 6.0+
- Keycloak 23.0.3+
- Docker & Docker Compose

### 1. Projeyi Klonlayın
```bash
git clone <repository-url>
cd MngKeeper
```

### 2. Bağımlılıkları Yükleyin
```bash
dotnet restore
```

### 3. Konfigürasyon
`MngKeeper/Presentation/MngKeeper.Api/appsettings.json` dosyasını düzenleyin:

```json
{
  "ConnectionStrings": {
    "MongoDB": "mongodb://admin:admin123@mongo_mngkeeper:27017",
    "RabbitMQ": "amqp://admin:admin123@rabbitmq_mngkeeper:5672",
    "Redis": "redis_mngkeeper:6379,password=redis123"
  },
  "MongoDB": {
    "DatabaseName": "mngkeeper"
  },
  "Keycloak": {
    "BaseUrl": "http://keycloak_mngkeeper:8080",
    "AdminUsername": "admin",
    "AdminPassword": "admin123"
  }
}
```

### 4. Uygulamayı Çalıştırın
```bash
cd MngKeeper
dotnet run --project Presentation/MngKeeper.Api
```

Uygulama `http://localhost:5001` adresinde çalışacaktır.

## 📖 API Dokümantasyonu

### Base URL
```
http://localhost:5001/api
```

### Endpoint'ler

#### Domain Yönetimi

**Domain Oluşturma**
```http
POST /api/domains
Content-Type: application/json

{
  "domainName": "example.com",
  "displayName": "Example Domain",
  "adminEmail": "admin@example.com",
  "adminPassword": "Admin123!",
  "settings": {
    "maxUsers": 100,
    "maxAssets": 1000,
    "enableMqtt": true
  }
}
```

**Domain Listesi**
```http
GET /api/domains
```

**Domain Detayı**
```http
GET /api/domains/{id}
```

**Domain Güncelleme**
```http
PUT /api/domains/{id}
Content-Type: application/json

{
  "displayName": "Updated Domain Name",
  "settings": {
    "maxUsers": 200
  }
}
```

**Domain Silme**
```http
DELETE /api/domains/{id}
```

#### Kullanıcı Yönetimi

**Kullanıcı Listesi**
```http
GET /api/users?page=1&pageSize=10&searchTerm=john&isActive=true
```

**Kullanıcı Detayı**
```http
GET /api/users/{id}
```

**Kullanıcı Oluşturma**
```http
POST /api/users
Content-Type: application/json

{
  "username": "john.doe",
  "email": "john.doe@example.com",
  "firstName": "John",
  "lastName": "Doe",
  "password": "Password123!",
  "groups": ["users", "editors"]
}
```

**Kullanıcı Güncelleme**
```http
PUT /api/users/{id}
Content-Type: application/json

{
  "firstName": "John Updated",
  "lastName": "Doe Updated",
  "isActive": true
}
```

**Kullanıcı Silme**
```http
DELETE /api/users/{id}
```

#### Grup Yönetimi

**Grup Listesi**
```http
GET /api/groups?page=1&pageSize=10&searchTerm=admin&isActive=true
```

**Grup Detayı**
```http
GET /api/groups/{id}
```

**Grup Oluşturma**
```http
POST /api/groups
Content-Type: application/json

{
  "name": "administrators",
  "description": "System administrators",
  "permissions": ["read", "write", "delete", "admin"]
}
```

**Grup Güncelleme**
```http
PUT /api/groups/{id}
Content-Type: application/json

{
  "name": "super-admins",
  "description": "Super administrators",
  "permissions": ["read", "write", "delete", "admin", "super-admin"]
}
```

**Grup Silme**
```http
DELETE /api/groups/{id}
```

#### Kimlik Doğrulama

**Giriş**
```http
POST /api/auth/login
Content-Type: application/json

{
  "username": "admin@example.com",
  "password": "Admin123!",
  "domain": "example.com"
}
```

**Token Doğrulama**
```http
POST /api/auth/validate
Authorization: Bearer <jwt-token>
```

### Response Formatları

#### Başarılı Response
```json
{
  "isSuccess": true,
  "data": {
    // Response data
  }
}
```

#### Hata Response
```json
{
  "isSuccess": false,
  "errorMessage": "Error description",
  "errorCode": "ERROR_CODE"
}
```

#### Sayfalama Response
```json
{
  "isSuccess": true,
  "data": {
    "items": [],
    "totalCount": 100,
    "page": 1,
    "pageSize": 10,
    "totalPages": 10
  }
}
```

## 🔧 Geliştirme

### Proje Yapısı

#### Domain Katmanı
- **Entities:** Domain modelleri
- **Interfaces:** Repository ve service interface'leri
- **Enums:** Domain enum'ları

#### Application Katmanı
- **Commands:** Write operasyonları
- **Queries:** Read operasyonları
- **DTOs:** Data transfer objects
- **Handlers:** Command/Query handlers
- **Services:** Business logic services

#### Infrastructure Katmanı
- **Services:** External service implementations
- **Repositories:** Data access implementations
- **Configuration:** Service configurations

#### Presentation Katmanı
- **Controllers:** API endpoints
- **Middleware:** Custom middleware
- **Configuration:** API configurations

### Yeni Feature Ekleme

1. **Domain Entity oluşturun**
2. **Repository interface tanımlayın**
3. **Command/Query oluşturun**
4. **Handler implementasyonu yapın**
5. **Controller endpoint'i ekleyin**
6. **Test yazın**

### Test Çalıştırma
```bash
dotnet test
```

## 🐳 Docker

### Docker Compose ile Çalıştırma
```bash
docker-compose up -d
```

### Docker Image Build
```bash
docker build -t mngkeeper-api .
```

## 📊 Monitoring ve Logging

- **Logging:** Serilog ile structured logging
- **Health Checks:** `/health` endpoint
- **Metrics:** Prometheus metrics (gelecek)
- **Tracing:** Distributed tracing (gelecek)

## 🔒 Güvenlik

- JWT token tabanlı kimlik doğrulama
- Domain-based izolasyon
- Input validation
- Rate limiting
- HTTPS zorunluluğu (production)

## 🤝 Katkıda Bulunma

1. Fork yapın
2. Feature branch oluşturun (`git checkout -b feature/amazing-feature`)
3. Commit yapın (`git commit -m 'Add amazing feature'`)
4. Push yapın (`git push origin feature/amazing-feature`)
5. Pull Request oluşturun

## 📄 Lisans

Bu proje MIT lisansı altında lisanslanmıştır. Detaylar için `LICENSE` dosyasına bakın.

## 📞 İletişim

- **Proje Sahibi:** Serkan MERAL
- **Email:** serkan.meral@isimplatform.io
- **Website:** https://mngkeeper.com

## 🚧 Bilinen Sorunlar

- Swagger dokümantasyonu geçici olarak devre dışı (schema çakışması)
- Bazı external service bağlantıları mock implementasyonu kullanıyor

## 🔄 Changelog

### v1.0.0 (2024-08-11)
- İlk sürüm
- Temel CRUD operasyonları
- Keycloak entegrasyonu
- MongoDB entegrasyonu
- MQTT, Redis, RabbitMQ entegrasyonları
