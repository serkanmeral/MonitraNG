# MonitraNG - Multi-Tenant IoT Monitoring Platform

[![.NET](https://img.shields.io/badge/.NET-9.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![Nuxt](https://img.shields.io/badge/Nuxt-3.13-00DC82?logo=nuxt.js)](https://nuxt.com/)
[![License](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![MongoDB](https://img.shields.io/badge/MongoDB-7.0-47A248?logo=mongodb)](https://www.mongodb.com/)
[![Keycloak](https://img.shields.io/badge/Keycloak-23.0.3-0080FF)](https://www.keycloak.org/)

Modern, multi-tenant IoT monitoring ve yönetim platformu. Clean Architecture, CQRS pattern ve Keycloak tabanlı authentication ile geliştirilmiştir.

## 🏗️ Proje Yapısı

```
MonitraNG/
├── MngKeeper/              # Authorization & Multi-tenant Management Service
│   ├── Core/
│   │   ├── Application/    # CQRS Commands/Queries, MediatR handlers
│   │   └── Domain/         # Entities, Interfaces, Business rules
│   ├── Infrastructure/
│   │   ├── Infrastructure/ # External services (Keycloak, MQTT, Redis)
│   │   └── Persistence/    # Data access (MongoDB repositories)
│   └── Presentation/
│       └── Api/            # REST API Controllers, Middleware
├── MngReactor/             # Main Business Logic Service
├── MngEngine/              # Data Collection Engine
├── Mng.Ui/                 # Frontend (Nuxt 3 + Vuetify)
└── ApplicationResources/   # Docker configs, Test data
```

## 🚀 Özellikler

### 🔐 Multi-Tenant Authentication
- ✅ Keycloak tabanlı kimlik doğrulama
- ✅ JWT token + Refresh token
- ✅ Domain-based izolasyon
- ✅ Role-based access control (RBAC)

### 🏢 Domain Yönetimi
- ✅ Domain CRUD operasyonları
- ✅ Her domain için ayrı Keycloak realm
- ✅ Her domain için ayrı MongoDB database
- ✅ Otomatik admin kullanıcısı ve grup oluşturma

### 👥 Kullanıcı & Grup Yönetimi
- ✅ Kullanıcı CRUD operasyonları
- ✅ Grup CRUD operasyonları
- ✅ Kullanıcı-grup ilişkilendirme
- ✅ Permission management

### 📊 API Documentation
- ✅ Swagger UI (Classic)
- ✅ Scalar UI (Modern)
- ✅ GraphQL support
- ✅ 38+ RESTful endpoints

### 📝 Logging & Monitoring
- ✅ Serilog structured logging
- ✅ Seq integration
- ✅ Console logging
- ✅ Health check endpoints

## 🛠️ Teknoloji Stack

### Backend
| Teknoloji | Versiyon | Kullanım |
|-----------|----------|----------|
| .NET | 9.0 | Application framework |
| MongoDB | 7.0 | NoSQL database |
| Keycloak | 23.0.3 | Identity & access management |
| Redis | 7 | Cache & session store |
| RabbitMQ | 3 | Message broker |
| Mosquitto | 2.0 | MQTT broker |
| Seq | Latest | Log aggregation |

### Frontend
| Teknoloji | Versiyon | Kullanım |
|-----------|----------|----------|
| Nuxt | 3.13 | Vue.js framework |
| Vuetify | 3.7 | Material Design UI |
| Pinia | 2.2 | State management |
| TypeScript | Latest | Type safety |

### Patterns & Architecture
- Clean Architecture
- CQRS + MediatR
- Repository Pattern
- Dependency Injection
- Domain-Driven Design (DDD)

## 📋 Gereksinimler

- [.NET 9.0 SDK](https://dotnet.microsoft.com/download)
- [Node.js 18+](https://nodejs.org/)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/)
- [Git](https://git-scm.com/)

## 🚀 Kurulum

### 1. Repository'yi Klonlayın

```bash
git clone https://github.com/serkanmeral/MonitraNG.git
cd MonitraNG
```

### 2. Docker Container'ları Başlatın

```bash
cd ApplicationResources/mng_common
docker-compose up -d
```

Bu komut şunları başlatır:
- MongoDB (Port: 27017)
- Keycloak (Port: 8080)
- Redis (Port: 6379)
- RabbitMQ (Port: 5672, Management: 15672)
- Mosquitto MQTT (Port: 1883)
- Seq (Port: 5341)
- Mongo Express (Port: 8081)

### 3. Backend API'yi Başlatın (MngKeeper)

```bash
cd ../../MngKeeper/Presentation/MngKeeper.Api
dotnet restore
dotnet build
dotnet run
```

API şu adreste çalışacak: `http://localhost:5001`

### 4. Frontend'i Başlatın (Opsiyonel)

```bash
cd ../../../Mng.Ui
npm install
npm run dev
```

Frontend şu adreste çalışacak: `http://localhost:3000`

## 📖 API Dokümantasyonu

API başlatıldıktan sonra:

- **Swagger UI:** http://localhost:5001/api-docs
- **Scalar UI:** http://localhost:5001/scalar/v1
- **GraphQL:** http://localhost:5001/graphql
- **Seq Logs:** http://localhost:5341

## 🧪 Hızlı Test

### 1. Domain Oluşturma

```bash
cd ApplicationResources/test_data/mng_keeper/create_domain
.\test-createdomain.ps1
```

### 2. Authentication Test

```bash
cd ../auth
.\test-auth.ps1
```

### 3. Refresh Token Test

```bash
.\test-refresh-token.ps1
```

## 📚 API Kullanımı

### Domain Oluşturma

```http
POST http://localhost:5001/api/domain
Content-Type: application/json

{
  "domainName": "my-company",
  "displayName": "My Company",
  "adminEmail": "admin@my-company.com",
  "adminPassword": "SecurePass123!",
  "settings": {
    "maxUsers": 100,
    "maxAssets": 1000,
    "enableMqtt": true
  }
}
```

### Token Alma

```http
POST http://localhost:5001/api/auth/token
Content-Type: application/json

{
  "username": "my-company_admin",
  "password": "SecurePass123!",
  "domainName": "my-company"
}
```

### Token Yenileme

```http
POST http://localhost:5001/api/auth/refresh
Content-Type: application/json

{
  "refreshToken": "eyJhbGc...",
  "domainName": "my-company"
}
```

### Protected Endpoint Kullanımı

```http
GET http://localhost:5001/api/domain
Authorization: Bearer eyJhbGc...
```

## 🏛️ Mimari

### Clean Architecture Katmanları

```
┌─────────────────────────────────────────┐
│           Presentation Layer            │
│    (Controllers, Middleware, UI)        │
└─────────────────┬───────────────────────┘
                  │
┌─────────────────▼───────────────────────┐
│          Application Layer              │
│   (Use Cases, CQRS, Business Logic)     │
└─────────────────┬───────────────────────┘
                  │
┌─────────────────▼───────────────────────┐
│            Domain Layer                 │
│      (Entities, Interfaces, Rules)      │
└─────────────────────────────────────────┘
                  ▲
┌─────────────────┴───────────────────────┐
│        Infrastructure Layer             │
│  (External Services, Database Access)   │
└─────────────────────────────────────────┘
```

### CQRS Pattern

- **Commands:** Write operations (Create, Update, Delete)
- **Queries:** Read operations (Get, List)
- **Handlers:** MediatR request handlers
- **Events:** Domain events with RabbitMQ

## 🔒 Güvenlik

- ✅ JWT token based authentication
- ✅ Refresh token rotation
- ✅ Token revocation on logout
- ✅ Domain-based tenant isolation
- ✅ Role-based authorization
- ✅ Input validation
- ✅ Global exception handling

## 📊 Monitoring

- **Application Logs:** Seq (http://localhost:5341)
- **Database UI:** Mongo Express (http://localhost:8081)
- **Message Queue:** RabbitMQ Management (http://localhost:15672)
- **Container Management:** Portainer (http://localhost:9000)

## 🧪 Testing

### Test Script'leri

```powershell
# Domain CRUD testi
.\test-domain-crud.ps1

# User CRUD testi  
.\test-user-crud.ps1

# Authentication flow testi
cd ApplicationResources/test_data/mng_keeper/auth
.\test-auth.ps1
```

## 📦 Deployment

### Docker Production Build

```bash
# Build API image
cd MngKeeper/Presentation/MngKeeper.Api
docker build -t mngkeeper-api:latest .

# Run with docker-compose
cd ../../
docker-compose up -d
```

## 🤝 Katkıda Bulunma

1. Fork yapın
2. Feature branch oluşturun (`git checkout -b feature/amazing-feature`)
3. Commit yapın (`git commit -m 'feat: Add amazing feature'`)
4. Push yapın (`git push origin feature/amazing-feature`)
5. Pull Request oluşturun

### Commit Message Convention

```
feat: Yeni özellik
fix: Bug düzeltmesi
docs: Dokümantasyon
refactor: Code refactoring
test: Test ekleme/düzeltme
chore: Build, dependencies güncellemesi
```

## 📄 Lisans

Bu proje MIT lisansı altında lisanslanmıştır. Detaylar için [LICENSE](LICENSE) dosyasına bakın.

## 👥 Ekip

**Proje Sahibi:** Serkan MERAL  
**Email:** serkan.meral@isimplatform.io  
**GitHub:** [@serkanmeral](https://github.com/serkanmeral)

## 🔗 Bağlantılar

- [GitHub Repository](https://github.com/serkanmeral/MonitraNG)
- [Product Requirements Document](prd.md)
- [MngKeeper Documentation](MngKeeper/README.md)
- [API Documentation](http://localhost:5001/api-docs)

## 📈 Roadmap

- [x] Clean Architecture implementation
- [x] Multi-tenant authentication
- [x] Domain management
- [x] User & Group management
- [x] Refresh token support
- [x] API documentation (Swagger + Scalar)
- [x] Logging infrastructure (Seq)
- [ ] MngReactor integration
- [ ] MngEngine integration
- [ ] Frontend integration
- [ ] OPC UA client
- [ ] Production deployment
- [ ] Unit & Integration tests
- [ ] Performance optimization

## 🆘 Sorun Giderme

### Container'lar başlamıyor

```bash
# Container'ları kontrol edin
docker ps -a

# Log'ları kontrol edin
docker logs <container-name>

# Yeniden başlatın
docker-compose restart
```

### API bağlantı hatası

```bash
# Port kullanımda mı kontrol edin
netstat -ano | findstr :5001

# API log'larını kontrol edin
http://localhost:5341
```

### MongoDB bağlantı hatası

```bash
# MongoDB çalışıyor mu?
docker ps | findstr mongo

# Connection string doğru mu?
# appsettings.json: "mongodb://admin:admin123@localhost:27017"
```

## 📞 Destek

Sorularınız için:
- GitHub Issues: [Issues](https://github.com/serkanmeral/MonitraNG/issues)
- Email: serkan.meral@isimplatform.io

---

**⭐ Projeyi beğendiyseniz yıldız vermeyi unutmayın!**

