# MngKeeper API - Development Roadmap

**Microservice:** Identity & Access Management (IAM)  
**Version:** 1.0.0  
**Last Updated:** 2025-11-05

---

## ✅ TAMAMLANAN ÖZELLİKLER

### 1. Domain Creation Pipeline (v1.0) - ✅ TAMAMLANDI

**11 Adımlı Pipeline:**
1. ✅ ValidateDomain - Domain name validation
2. ✅ CreateDomainEntity - MongoDB entity creation
3. ✅ CreateDatabase - Dedicated domain database
4. ✅ InitializeDatabaseCollections - Default collections (@datasets, @dataset_categories)
5. ✅ CreateKeycloakRealm - Keycloak realm creation
6. ✅ CreateDefaultGroups - 4 default groups (admins, managers, users, guests)
7. ✅ CreateAdminUser - Domain admin user with isAdmin attribute
8. ✅ PublishDomainCreatedEvent - RabbitMQ event publishing
9. ✅ InitializeDomainCache - Redis cache (users, groups, metadata)
10. ✅ CreateMinIOBucket - S3-compatible storage bucket + folders (system, data, backups)
11. ✅ ActivateDomain - Domain activation

**Test Edildi:**
- ✅ MongoDB: Database + Collections + Indexes
- ✅ Keycloak: Realm + Users + Groups
- ✅ Redis: Cache initialization
- ✅ RabbitMQ: Topic exchange (mng.topics)
- ✅ MinIO: Bucket + Folder structure

**Known Issues:**
- ⚠️ Protocol mappers otomatik eklenemiyor (Keycloak permission issue)
- ✅ Workaround: `/api/admin/realms/{realmName}/configure-mappers` endpoint

---

### 2. Authentication API - ✅ TAMAMLANDI

**Endpoints:**
- ✅ `POST /api/auth/token` - Get JWT token (username + password + domain)
- ✅ `POST /api/auth/refresh` - Refresh expired token
- ✅ `POST /api/auth/revoke` - Revoke refresh token (logout)

**Custom Token Claims:**
- ✅ `user_groups`: Array - Kullanıcının bağlı olduğu gruplar
- ✅ `isAdmin`: Boolean - admins grubunda ise true

**Token Özellikleri:**
- Access Token Expiry: 300 seconds (5 minutes)
- Refresh Token Expiry: 1800 seconds (30 minutes)
- Token Type: Bearer
- Client: admin-cli (Keycloak default)

---

### 3. Admin Helper Endpoints - ✅ TAMAMLANDI

**Endpoints:**
- ✅ `POST /api/admin/realms/{realmName}/configure-mappers` - Keycloak protocol mapper configuration

**Kullanım:**
```
1. Domain oluştur
2. Mapper'ları yapılandır (1 kere)
3. Token al → Custom claims ile birlikte
```

---

### 4. Infrastructure Services - ✅ TAMAMLANDI

**Implemented:**
- ✅ KeycloakService (Realm, User, Group, Token management)
- ✅ RedisService (Cache operations)
- ✅ RabbitMqService (Message publishing)
- ✅ MinioService (S3 object storage)
- ✅ MongoDbService (Database operations)
- ✅ JwtTokenService (Token generation/validation)
- ✅ CertificateHandler (SSL/TLS certificate management)

---

### 5. Clean Architecture & Best Practices - ✅ TAMAMLANDI

**Architecture:**
- ✅ Domain Layer (Entities)
- ✅ Application Layer (Use Cases, Interfaces)
- ✅ Infrastructure Layer (External Services)
- ✅ Presentation Layer (API Controllers)

**Patterns:**
- ✅ Pipeline Pattern (Chain of Responsibility) - Domain Creation
- ✅ CQRS (MediatR)
- ✅ Repository Pattern
- ✅ Dependency Injection
- ✅ IOptions Pattern (Centralized configuration)

**Configuration:**
- ✅ MngKeeperSettings (Hierarchical configuration)
- ✅ Extension Methods (Clean Program.cs)
- ✅ Serilog (Structured logging)
- ✅ Global Exception Handler

---

## 🔄 DEVAM EDEN İŞLER

### User Management (ROADMAP'te var)
- [ ] User CRUD operations (MediatR commands/queries)
- [ ] User-Group management
- [ ] User search & filtering
- [ ] User pagination

### Group Management (ROADMAP'te var)
- [ ] Group CRUD operations
- [ ] Group membership management
- [ ] Permission management

---

## 🎯 ÖNCELİKLİ GÖREVLER

### 1. MngDataGateway Integration - YÜ KSEK ÖNCELİK

**Amaç:** MngKeeper'dan alınan token ile MngDataGateway'e erişim

**Gereksinimler:**
- [ ] MngDataGateway JWT validation (MngKeeper authority)
- [ ] Domain-based database routing
- [ ] Custom claims validation (user_groups, isAdmin)
- [ ] Authorization policies (IsAdmin, IsManager, IsUser)

---

### 2. Dataset Management API - YÜKSEK ÖNCELİK

**MngDataGateway'de implement edilecek:**
- [ ] Dataset CRUD operations
- [ ] Dynamic schema definition
- [ ] Schema validation
- [ ] Data CRUD (dynamic collections)
- [ ] Query builder

---

### 3. WebSocket Gateway - ORTA ÖNCELİK

**Yeni Microservice:**
- [ ] SignalR Hub implementation
- [ ] RabbitMQ bridge (topic subscription)
- [ ] JWT validation
- [ ] Domain-based room management
- [ ] Reconnection strategy

---

## 📝 NOTLAR VE İYİLEŞTİRMELER

### Keycloak Mapper Configuration

**Sorun:** Domain creation sırasında protocol mapper'lar otomatik eklenemiyor (Forbidden error).

**Root Cause:** Master realm admin token ile diğer realm'lerdeki client'lara doğrudan erişim yetkisi yok.

**Çözüm:** 
- İki aşamalı süreç:
  1. Domain oluştur (`POST /api/domain`)
  2. Mapper'ları yapılandır (`POST /api/admin/realms/{realmName}/configure-mappers`)

**Alternatif Çözümler (Gelecek):**
- [ ] Client Scope yaklaşımı (realm-wide mapper'lar)
- [ ] Keycloak admin role configuration
- [ ] Service account kullanımı

---

### Scalar UI Issue

**Sorun:** Scalar API documentation UI blank screen.

**Status:** ROADMAP'te (MngDataGateway)

**Priority:** Low (Swagger çalışıyor)

---

### MinIO Folder Structure

**Current:** `system/`, `data/`, `backups/`

**Future Consideration:**
- [ ] User-specific folders
- [ ] Temporary upload folder
- [ ] Archive folder
- [ ] Quota management

---

## 🚀 NEXT STEPS

1. **README güncellemesi** - API kullanım kılavuzu
2. **MngDataGateway JWT integration** - Token validation
3. **Dataset Management API** - Dynamic CRUD
4. **User/Group Management UI** - Admin panel

---

## 📊 TAMAMLANMA DURUMU

| Component | Status | Completion |
|-----------|--------|------------|
| Domain Creation Pipeline | ✅ Complete | 100% |
| Authentication API | ✅ Complete | 100% |
| Infrastructure Services | ✅ Complete | 100% |
| Clean Architecture | ✅ Complete | 100% |
| User Management | 🔄 Partial | 40% |
| Group Management | 🔄 Partial | 40% |
| WebSocket Gateway | ⏸️ Planned | 0% |
| Dataset Management | ⏸️ Planned | 0% |

**Overall Progress:** **60%** of Phase 1

