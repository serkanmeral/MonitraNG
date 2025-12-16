# MngKeeper API - Development Roadmap

**Microservice:** Identity & Access Management (IAM)  
**Version:** 1.1.0  
**Last Updated:** 16 Aralık 2025

---

## 📊 Genel Durum

| Component | Status | Completion |
|-----------|--------|------------|
| Domain Creation Pipeline | ✅ Complete | 100% |
| Authentication API | ✅ Complete | 100% |
| User Management | ✅ Complete | 100% |
| Group Management | ✅ Complete | 100% |
| DataGateway Sync | ✅ Complete | 100% |
| Infrastructure Services | ✅ Complete | 100% |
| Clean Architecture | ✅ Complete | 100% |
| RabbitMQ Events | 🔄 Partial | 60% |
| Password Management | ⏸️ Planned | 0% |
| Code Optimization | ⏸️ Planned | 0% |
| WebSocket Gateway | ⏸️ Planned | 0% |
| Admin UI | ⏸️ Planned | 0% |

**Overall Progress:** **85%** of Core Features

---

## ✅ TAMAMLANAN ÖZELLİKLER

### 1. Domain Creation Pipeline (v1.0) - ✅ TAMAMLANDI

**12 Adımlı Pipeline:**
1. ✅ ValidateDomain - Domain name validation
2. ✅ CreateDomainEntity - MongoDB entity creation
3. ✅ CreateDatabase - Dedicated domain database
4. ✅ InitializeDatabaseCollections - Default collections (@datasets, @dataset_categories)
5. ✅ InitializeDataGatewayCollections - DataGateway collections (@users, @groups)
6. ✅ CreateKeycloakRealm - Keycloak realm creation
7. ✅ CreateDefaultGroups - 4 default groups (admins, managers, users, guests) + Keycloak + MongoDB + DataGateway sync
8. ✅ CreateAdminUser - Domain admin user with isAdmin attribute
9. ✅ PublishDomainCreatedEvent - RabbitMQ event publishing
10. ✅ InitializeDomainCache - Redis cache (users, groups, metadata)
11. ✅ CreateMinIOBucket - S3-compatible storage bucket + folders (system, data, backups)
12. ✅ ActivateDomain - Domain activation

**Test Edildi:**
- ✅ MongoDB: Database + Collections + Indexes (MngKeeper + DataGateway)
- ✅ Keycloak: Realm + Users + Groups
- ✅ Redis: Cache initialization
- ✅ RabbitMQ: Topic exchange (mng.topics)
- ✅ MinIO: Bucket + Folder structure

---

### 2. Authentication API - ✅ TAMAMLANDI

**Endpoints:**
- ✅ `POST /api/auth/token` - Get JWT token (username + password + domain)
- ✅ `POST /api/auth/refresh` - Refresh expired token
- ✅ `POST /api/auth/revoke` - Revoke refresh token (logout)

**Custom Token Claims:**
- ✅ `user_groups`: Array - Kullanıcının bağlı olduğu gruplar
- ✅ `isAdmin`: Boolean - admins grubunda ise true
- ✅ `domain_id`: String - Domain ID
- ✅ `domain_name`: String - Domain name

**Token Özellikleri:**
- Access Token Expiry: 300 seconds (5 minutes)
- Refresh Token Expiry: 1800 seconds (30 minutes)
- Token Type: Bearer
- Client: admin-cli (Keycloak default)

**Eksik Özellikler:**
- [ ] `POST /api/auth/forgot-password` - Şifremi unuttum (password reset request)
- [ ] `POST /api/auth/reset-password` - Şifre sıfırlama (reset token ile)
- [ ] `POST /api/auth/change-password` - Şifre değiştirme (authenticated user)

---

### 3. User Management API - ✅ TAMAMLANDI

**Endpoints:**
- ✅ `POST /api/user` - Create user (auto-assigned to "users" group)
- ✅ `GET /api/user` - Get users (pagination, search, filter)
- ✅ `GET /api/user/{userId}` - Get user by ID
- ✅ `PUT /api/user/{userId}` - Update user
- ✅ `DELETE /api/user/{userId}` - Delete user (soft delete)
- ✅ `POST /api/user/{userId}/groups/{groupId}` - Add user to group
- ✅ `DELETE /api/user/{userId}/groups/{groupId}` - Remove user from group

**Özellikler:**
- ✅ Automatic "users" group assignment on creation
- ✅ Multi-tenant isolation (domain-based)
- ✅ Keycloak integration
- ✅ DataGateway sync with custom data support
- ✅ Pagination & search
- ✅ Soft delete

---

### 4. Group Management API - ✅ TAMAMLANDI

**Endpoints:**
- ✅ `POST /api/group` - Create group
- ✅ `GET /api/group` - Get groups (pagination, search, filter)
- ✅ `GET /api/group/{groupId}` - Get group by ID
- ✅ `PUT /api/group/{groupId}` - Update group
- ✅ `DELETE /api/group/{groupId}` - Delete group (soft delete)

**Özellikler:**
- ✅ Default groups (admins, managers, users, guests)
- ✅ Multi-tenant isolation (domain-based)
- ✅ Keycloak integration
- ✅ DataGateway sync with custom data support
- ✅ Permission management (future)
- ✅ Soft delete

---

### 5. DataGateway Sync Service - ✅ TAMAMLANDI

**Interface:**
- ✅ `IDataGatewaySyncService` - Sync service interface
- ✅ `SyncUserToDataGatewayAsync` - User sync with custom data
- ✅ `SyncGroupToDataGatewayAsync` - Group sync with custom data
- ✅ `SyncAllUsersAsync` - Bulk user sync
- ✅ `SyncAllGroupsAsync` - Bulk group sync

**Implementation:**
- ✅ `DataGatewaySyncService` - Direct MongoDB sync (no RabbitMQ dependency)
- ✅ Automatic sync on user/group CRUD operations
- ✅ Custom data support
- ✅ Domain-based database routing (`mng_{domainName}`)
- ✅ Manual sync endpoints (`/api/sync/*`)

**Avantajlar:**
- ✅ Transaction guarantee (same operation)
- ✅ No event loss risk
- ✅ Faster (no network hop)
- ✅ Simpler architecture

---

### 6. Infrastructure Services - ✅ TAMAMLANDI

**Implemented:**
- ✅ KeycloakService (Realm, User, Group, Token management)
- ✅ RedisService (Cache operations)
- ✅ RabbitMqService (Message publishing)
- ✅ MinioService (S3 object storage)
- ✅ MongoDbService (Database operations)
- ✅ JwtTokenService (Token generation/validation)
- ✅ CertificateHandler (SSL/TLS certificate management)
- ✅ DataGatewaySyncService (MngDataGateway MongoDB sync)

---

### 7. Clean Architecture & Best Practices - ✅ TAMAMLANDI

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

### RabbitMQ Event Publishing - 🔄 %60 TAMAMLANDI

**Mevcut Durum:**
- ✅ Domain creation event (`domain.created`)
- ✅ User/Group CRUD events (partial)

**Eksikler:**
- [ ] User group assignment events (`user.group.added`, `user.group.removed`)
- [ ] Event retry mechanism
- [ ] Dead Letter Queue (DLQ) handling
- [ ] Event versioning

**Priority:** Medium

---

## 🎯 GELECEK PLANLAR

### 1. RabbitMQ Event System Completion - YÜKSEK ÖNCELİK

**Amaç:** Tüm CRUD işlemlerinin event olarak yayınlanması

**Event'ler:**
```
Domain Events:
- ✅ domain.created
- [ ] domain.updated
- [ ] domain.deleted

User Events:
- ✅ user.created
- ✅ user.updated
- ✅ user.deleted
- [ ] user.group.added
- [ ] user.group.removed

Group Events:
- ✅ group.created
- ✅ group.updated
- ✅ group.deleted
```

**İmplementasyon:**
- [ ] Event model'leri tamamlama
- [ ] Event retry mechanism
- [ ] DLQ handling
- [ ] Event versioning

---

### 2. Permission Management System - ORTA ÖNCELİK

**Amaç:** Group-based permission yönetimi

**Özellikler:**
- [ ] Permission CRUD operations
- [ ] Permission assignment to groups
- [ ] Permission validation in API endpoints
- [ ] Permission-based authorization policies

---

### 3. Audit Logging - ORTA ÖNCELİK

**Amaç:** Tüm işlemlerin audit log'lanması

**Özellikler:**
- [ ] Audit log entity
- [ ] Audit log repository
- [ ] Automatic audit logging for CRUD operations
- [ ] Audit log query endpoints
- [ ] Audit log retention policy

---

### 4. WebSocket Gateway - DÜŞÜK ÖNCELİK

**Yeni Microservice:**
- [ ] SignalR Hub implementation
- [ ] RabbitMQ bridge (topic subscription)
- [ ] JWT validation
- [ ] Domain-based room management
- [ ] Reconnection strategy

---

### 5. Password Management - YÜKSEK ÖNCELİK

**Amaç:** Kullanıcı şifre yönetimi (forgot password, reset, change)

**Özellikler:**
- [ ] **Forgot Password Endpoint** (`POST /api/auth/forgot-password`)
  - Email/username ile reset token oluşturma
  - Reset token'ı email ile gönderme (SMTP entegrasyonu)
  - Token expiration (örn: 1 saat)
  - Rate limiting (spam önleme)
  
- [ ] **Reset Password Endpoint** (`POST /api/auth/reset-password`)
  - Reset token ile yeni şifre belirleme
  - Token validation
  - Password strength validation
  - Token tek kullanımlık yapma
  
- [ ] **Change Password Endpoint** (`POST /api/auth/change-password`)
  - Authenticated user için mevcut şifre ile değiştirme
  - JWT token'dan user bilgisi alma
  - Eski şifre doğrulama
  - Yeni şifre validation
  - Keycloak password update

**İmplementasyon Detayları:**
- [ ] Password reset token entity (MongoDB)
- [ ] Email service (SMTP) entegrasyonu
- [ ] Token generation (secure random)
- [ ] Password policy validation
- [ ] Rate limiting middleware
- [ ] Audit logging (password change events)

**Keycloak Entegrasyonu:**
- [ ] Keycloak password reset API kullanımı
- [ ] Keycloak password update API kullanımı
- [ ] Keycloak email action token (alternatif)

---

### 6. Code Optimization - ORTA ÖNCELİK

**Amaç:** Kod kalitesi, performans ve maintainability iyileştirmeleri

**Optimizasyon Alanları:**

**Performance:**
- [ ] Database query optimization (index review, N+1 problem)
- [ ] Caching strategy review (Redis cache hit ratio)
- [ ] Async/await pattern review (deadlock prevention)
- [ ] Memory leak detection ve düzeltme
- [ ] Response time optimization

**Code Quality:**
- [ ] Code duplication reduction (DRY principle)
- [ ] Method complexity reduction (cyclomatic complexity)
- [ ] Unused code removal (dead code elimination)
- [ ] Magic number/string elimination (constants)
- [ ] Exception handling improvement (specific exceptions)

**Architecture:**
- [ ] Service layer refactoring (single responsibility)
- [ ] Repository pattern optimization
- [ ] Dependency injection optimization
- [ ] Configuration management improvement
- [ ] Logging strategy optimization

**Security:**
- [ ] Input validation review
- [ ] SQL injection prevention (MongoDB query validation)
- [ ] XSS prevention review
- [ ] Authentication/Authorization review
- [ ] Secret management review

**Testing:**
- [ ] Unit test coverage increase
- [ ] Integration test coverage
- [ ] Performance testing
- [ ] Load testing
- [ ] Security testing

**Documentation:**
- [ ] Code comments improvement
- [ ] API documentation update
- [ ] Architecture documentation
- [ ] Deployment documentation

**Tools & Metrics:**
- [ ] Code analysis tools (SonarQube, CodeQL)
- [ ] Performance profiling (dotMemory, Application Insights)
- [ ] Code metrics tracking
- [ ] Technical debt tracking

---

### 7. Admin UI - DÜŞÜK ÖNCELİK

**Amaç:** Web-based admin panel

**Özellikler:**
- [ ] Domain management UI
- [ ] User management UI
- [ ] Group management UI
- [ ] Permission management UI
- [ ] Audit log viewer

---

## 📝 NOTLAR VE İYİLEŞTİRMELER

### Keycloak Mapper Configuration

**Durum:** ✅ Çözüldü

**Çözüm:** 
- İki aşamalı süreç:
  1. Domain oluştur (`POST /api/domain`)
  2. Mapper'ları yapılandır (`POST /api/admin/realms/{realmName}/configure-mappers`)

**Not:** Protocol mapper'lar domain creation sırasında otomatik eklenemiyor (Keycloak permission issue). Workaround endpoint mevcut.

---

### Version Management

**Mevcut Versiyon:** 1.1.0

**Version Endpoints:**
- `GET /api/version` - Detaylı versiyon bilgisi
- `GET /api/version/short` - Kısa versiyon string'i

---

## 🚀 NEXT STEPS

1. **Password Management** - Forgot password, reset password, change password endpoints
2. **RabbitMQ Event System Completion** - Event retry ve DLQ
3. **Code Optimization** - Performance, quality ve security iyileştirmeleri
4. **Permission Management** - Group-based permissions
5. **Audit Logging** - Comprehensive audit trail
6. **Test Coverage** - Unit ve integration testleri
7. **Documentation** - API documentation güncellemesi

---

## 📚 İLGİLİ DOKÜMANTASYON

- [README.md](./README.md) - Genel bilgiler ve kullanım
- [ENVIRONMENT_VARIABLES.md](./ENVIRONMENT_VARIABLES.md) - Environment variables
- [CHANGELOG.md](./CHANGELOG.md) - Değişiklik geçmişi

---

**Son Güncelleme:** 16 Aralık 2025  
**Status:** Core features complete, enhancements in progress  
**Yeni Özellikler:** Password Management, Code Optimization eklendi

