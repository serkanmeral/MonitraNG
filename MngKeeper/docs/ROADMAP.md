# MngKeeper API - Development Roadmap

**Microservice:** Identity & Access Management (IAM)  
**Version:** 1.1.0  
**Last Updated:** 30 Aralık 2025

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
| Code Optimization | ✅ Complete | 100% |
| RabbitMQ Events | ✅ Complete | 100% |
| Password Management | 🔄 Partial | 67% |
| User Profile Enhancement | 📋 Planned | 0% |
| Manager Role & Authorization | 📋 Planned | 0% |

**Overall Progress:** **98%** of Core Features

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
  - ✅ Domain parametresi opsiyonel (domain@username formatı desteği)
  - ✅ Tek domain varsa otomatik domain seçimi
- ✅ `POST /api/auth/refresh` - Refresh expired token
- ✅ `POST /api/auth/revoke` - Revoke refresh token (logout)
- ✅ `POST /api/auth/change-password` - Change password (authenticated user) - 23 Aralık 2025
- ✅ `POST /api/auth/reset-password` - Reset password (reset token ile) - 23 Aralık 2025
- ✅ `POST /api/auth/create-reset-token` - Create reset token (admin only, test için) - 23 Aralık 2025

**Custom Token Claims:**
- ✅ `user_groups`: Array - Kullanıcının bağlı olduğu gruplar
- ✅ `isAdmin`: Boolean - admins grubunda ise true
- ✅ `isManager`: Boolean - managers grubunda ise true (Planlanıyor - Yüksek Öncelik)
- ✅ `domain_id`: String - Domain ID
- ✅ `domain_name`: String - Domain name

**Token Özellikleri:**
- Access Token Expiry: 300 seconds (5 minutes)
- Refresh Token Expiry: 1800 seconds (30 minutes)
- Token Type: Bearer
- Client: admin-cli (Keycloak default)

**Password Management:**
- ✅ `POST /api/auth/change-password` - Şifre değiştirme (authenticated user) - 23 Aralık 2025
- ✅ `POST /api/auth/reset-password` - Şifre sıfırlama (reset token ile) - 23 Aralık 2025
- ✅ `POST /api/auth/create-reset-token` - Reset token oluşturma (test için, admin only) - 23 Aralık 2025
- [ ] `POST /api/auth/forgot-password` - Şifremi unuttum (password reset request) - Karar bekleniyor

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
- ✅ RabbitMQ event publishing (user.created, user.updated, user.deleted)

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

### 8. Code Optimization & Performance Improvements (v1.1.0) - ✅ TAMAMLANDI

**Performance Optimizations:**
- ✅ Redis cache integration for query handlers (GetUsers, GetGroups)
- ✅ MongoDB indexes for users and groups collections (DomainId, Username, Email, Name, compound indexes)
- ✅ Cache-aside pattern with configurable TTL (5 minutes default)
- ✅ Database-level filtering and pagination (reduced memory usage by ~90%)
- ✅ Compound indexes for common query patterns (DomainId + IsActive)
- ✅ Async disposal patterns improved (removed `.Wait()` anti-patterns)

**Code Quality Improvements:**
- ✅ Constants class (`SystemConstants`, `SystemGroups`) for magic string elimination
- ✅ Code duplication reduced through extension methods (`CacheExtensions`)
- ✅ Exception handling standardized with `ExceptionHelper` class
- ✅ Specific exception handling for MongoDB, HTTP, and timeout errors
- ✅ User-friendly error messages based on exception types
- ✅ Log levels determined by exception type (Warning for client errors, Error for server errors)

**Infrastructure:**
- ✅ `IndexManager` service for automatic index creation during domain setup
- ✅ `CreateIndexesStep` in domain creation pipeline
- ✅ `IAsyncDisposable` implementation for proper async disposal patterns
- ✅ Cache key building utilities for consistent cache key format
- ✅ Cache extension methods for reusable cache operations

**Performance Metrics:**
- ✅ Cache Performance: 680ms → 40ms (94.1% improvement) on cache hits
- ✅ Database Indexes: 5 indexes for users, 4 indexes for groups
- ✅ Cache TTL: 5 minutes for lists, 10 minutes for details
- ✅ Memory Usage: ~90% reduction through database-level filtering

---

## 🔄 DEVAM EDEN İŞLER

### RabbitMQ Event Publishing - ✅ TAMAMLANDI

**Mevcut Durum:**
- ✅ Domain creation event (`domain.created`)
- ✅ User CRUD events (`user.created`, `user.updated`, `user.deleted`)
- ✅ Group CRUD events (`group.created`, `group.updated`, `group.deleted`)
- ✅ User group assignment events (`user.group.added`, `user.group.removed`) - 23 Aralık 2025

**Gelecek İyileştirmeler:**
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
- ✅ user.updated (23 Aralık 2025 - eklendi)
- ✅ user.deleted (23 Aralık 2025 - eklendi)
- ✅ user.group.added (23 Aralık 2025 - eklendi)
- ✅ user.group.removed (23 Aralık 2025 - eklendi)

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

### 2. User Profile Enhancement - YÜKSEK ÖNCELİK ⭐

**Amaç:** Kullanıcı profil bilgilerini genişletme (Title, Department, Gender, Photo)

**Yeni Alanlar:**
- [ ] **Title (Unvan/İş Unvanı)**: String field (opsiyonel, max 100 karakter)
- [ ] **Department (Departman)**: String field (opsiyonel, max 100 karakter)
- [ ] **Gender (Cinsiyet)**: Enum field (NotSpecified, Male, Female)
- [ ] **PhoneNumber (Telefon Numarası)**: String field (opsiyonel, max 20 karakter)
- [ ] **PhotoUrl (Profil Fotoğrafı)**: String field (MinIO URL, opsiyonel)

**Implementasyon Detayları:**

**Backend (MngKeeper):**
- [ ] Gender enum oluşturma (`Gender.NotSpecified`, `Gender.Male`, `Gender.Female`)
- [ ] User entity'ye yeni alanlar ekleme (Title, Department, Gender, PhotoUrl)
- [ ] UserDto'ya yeni alanlar ekleme
- [ ] KeycloakService'de attributes'a yeni alanları ekleme
- [ ] CreateUser/UpdateUser command'larına yeni alanları ekleme
- [ ] UserRepository mapping'lerini güncelleme
- [ ] DataGatewaySyncService'de yeni alanları sync etme
- [ ] PhoneNumber validation (format kontrolü - opsiyonel)

**Photo Upload Sistemi:**
- [ ] Photo upload endpoint (`POST /api/user/{userId}/photo`)
- [ ] Photo get endpoint (`GET /api/user/{userId}/photo`)
- [ ] MinIO storage yapısı (`{domainName}/users/{userId}/photo.{ext}`)
- [ ] Photo validation (max 5MB, jpg/jpeg/png/webp, max 2000x2000px)
- [ ] Photo URL formatı (proxy URL: `/api/user/{userId}/photo`)

**Frontend (Mng.UI):**
- [ ] User interface'e yeni alanlar ekleme
- [ ] Avatar color computed property (Gender'a göre: Male=info, Female=pink, NotSpecified=primary)
- [ ] Avatar component'lerini güncelleme (PhotoUrl varsa fotoğraf, yoksa initials)
- [ ] User form'larına yeni alanları ekleme (Title, Department, Gender select, Photo upload)
- [ ] User list/detail sayfalarında yeni alanları gösterme

**Avatar Stratejisi:**
- PhotoUrl varsa → MinIO'dan fotoğraf göster
- PhotoUrl yoksa → Gender'a göre renkli initials avatar:
  - Erkek (Male): `info` (açık mavi)
  - Kadın (Female): `pink` (pembe)
  - Belirtilmemiş (NotSpecified): `primary` (mavi)

**Not:** Offline sistemler için Gravatar kullanılmayacak, sadece MinIO ve initials avatar kullanılacak.

---

### 3. Server-Side Pagination & Performance Optimization - ORTA ÖNCELİK ⚡

**Amaç:** Binlerce kullanıcı/grup için performans optimizasyonu ve ölçeklenebilirlik

**Mevcut Durum:**
- ✅ Backend zaten server-side pagination destekliyor (MongoDB Skip/Limit)
- ✅ Database-level filtering ve search mevcut
- ✅ Redis cache entegrasyonu var
- ❌ Frontend client-side pagination kullanıyor (tüm veriler çekiliyor)

**Sorunlar:**
- Binlerce kullanıcı olduğunda ilk yüklemede 100 kullanıcı çekiliyor (yavaş)
- Tüm veriler frontend'de tutuluyor (memory sorunu)
- Search ve filter client-side yapılıyor (tüm veriler üzerinde)
- Gereksiz network trafiği

**Çözüm: Server-Side Pagination (Frontend)**

**Frontend (Mng.UI) Güncellemeleri:**
- [ ] `v-data-table`'ı server-side pagination moduna geçirme
- [ ] `server-items-length` prop'unu kullanma
- [ ] `@update:options` event'ini handle etme
- [ ] Search input'unu backend'e gönderme (debounce ile)
- [ ] Filter'ları backend'e gönderme (status, department, vb.)
- [ ] User Store'da `fetchUsers` metodunu optimize etme (default pageSize: 10)
- [ ] Group Store'da aynı optimizasyonları uygulama

**Backend (MngKeeper) İyileştirmeleri:**
- [ ] Sorting desteği ekleme (şu anda sadece pagination var)
- [ ] Multiple column sorting desteği
- [ ] Advanced filtering (department, title, gender vb.)
- [ ] Search index optimizasyonu (full-text search için)
- [ ] Cache key stratejisi iyileştirme (sorting ve filter'ları dahil etme)

**Performans Karşılaştırması:**

| Senaryo | Client-side (Mevcut) | Server-side (Hedef) |
|---------|---------------------|---------------------|
| 100 kullanıcı | ✅ Hızlı | ✅ Hızlı |
| 1,000 kullanıcı | ⚠️ Yavaş (100 çekiliyor) | ✅ Hızlı (10 çekiliyor) |
| 10,000 kullanıcı | ❌ Çok yavaş | ✅ Hızlı (10 çekiliyor) |
| Search (10K) | ❌ Tüm veriler üzerinde | ✅ Database index ile |
| Memory | ❌ Yüksek | ✅ Düşük |

**Implementasyon Detayları:**

**1. v-data-table Server-Side Mode:**
```vue
<v-data-table
  :items="userStore.users"
  :server-items-length="userStore.totalCount"
  :items-per-page="tableOptions.itemsPerPage"
  :page="tableOptions.page"
  @update:options="handleOptionsUpdate"
/>
```

**2. Options Update Handler:**
```typescript
const handleOptionsUpdate = async (options: any) => {
  await userStore.fetchUsers({
    page: options.page,
    pageSize: options.itemsPerPage,
    search: search.value,
    isActive: statusFilter.value === 'active' ? true : undefined,
  });
};
```

**3. Debounced Search:**
```typescript
const debouncedSearch = debounce(async (searchTerm: string) => {
  await handleOptionsUpdate({ ...tableOptions.value, page: 1 });
}, 300);
```

**Öncelik:** Orta (küçük sistemlerde sorun yok, büyük sistemler için kritik)

---

### 4. Manager Role & Enhanced Authorization System - YÜKSEK ÖNCELİK ⭐

**Amaç:** Admin ve Manager rolleri ile hiyerarşik yetkilendirme sistemi

**Yetkilendirme Hiyerarşisi:**
```
Admin (isAdmin = true)
  ├─ Sistem yönetimi (dataset tanımı, domain yönetimi vb.)
  └─ Management işlemleri (kullanıcı oluşturma, listeleme vb.)

Manager (isManager = true, isAdmin = false)
  └─ Management işlemleri (kullanıcı oluşturma, listeleme vb.)

Normal User (isAdmin = false, isManager = false)
  └─ Sınırlı yetkiler
```

**Özellikler:**
- [ ] `isManager` claim'i ekleme (TokenClaims, JWT token)
- [ ] `isManager` attribute'unu Keycloak'ta set etme (managers grubundaki kullanıcılar için)
- [ ] Protocol mapper ekleme (`isManager` claim'i için)
- [ ] `ManagerAuthorizationAttribute` oluşturma (Admin veya Manager kontrolü)
- [ ] `AdminOnlyAuthorizationAttribute` oluşturma (Sadece Admin kontrolü)
- [ ] Mevcut `AdminAuthorizationAttribute`'u `ManagerAuthorizationAttribute`'a dönüştürme
- [ ] API endpoint yetkilendirmelerini güncelleme:
  - User Management: `ManagerAuthorizationAttribute` (Admin veya Manager)
  - Group Management: `ManagerAuthorizationAttribute` (Admin veya Manager)
  - Domain Management: `AdminOnlyAuthorizationAttribute` (Sadece Admin)
  - Dataset Management (MngDataGateway): `AdminOnlyAuthorizationAttribute` (Sadece Admin)

**Implementasyon Detayları:**
- [ ] `TokenClaims` class'ına `IsManager` property ekleme
- [ ] `JwtTokenParserService`'de `isManager` claim'ini parse etme
- [ ] `JwtTokenService.AddDomainClaimToToken` metoduna `isManager` parametresi ekleme
- [ ] `GetTokenCommandHandler`'da `isManager` kontrolü yapma (managers grubunda mı?)
- [ ] `KeycloakService.CreateUserAsync`'de `isManager` attribute'unu set etme
- [ ] `KeycloakService.UpdateUserAsync`'de `isManager` attribute'unu güncelleme
- [ ] `AdminController.ConfigureMappersAsync`'de `isManager` protocol mapper ekleme
- [ ] Authorization attribute'larını oluşturma ve mevcut endpoint'lere uygulama

**Not:** Admin otomatik olarak Manager yetkilerine sahiptir (isAdmin = true ise Manager kontrolüne gerek yok).

---

### 3. Permission Management System - ORTA ÖNCELİK

**Amaç:** Group-based permission yönetimi

**Özellikler:**
- [ ] Permission CRUD operations
- [ ] Permission assignment to groups
- [ ] Permission validation in API endpoints
- [ ] Permission-based authorization policies

---

### 6. Audit Logging - ORTA ÖNCELİK

**Amaç:** Tüm işlemlerin audit log'lanması

**Özellikler:**
- [ ] Audit log entity
- [ ] Audit log repository
- [ ] Automatic audit logging for CRUD operations
- [ ] Audit log query endpoints
- [ ] Audit log retention policy

---

### 7. Password Management - 🔄 %67 TAMAMLANDI

**Amaç:** Kullanıcı şifre yönetimi (forgot password, reset, change)

**Tamamlanan Özellikler:**
- ✅ **Change Password Endpoint** (`POST /api/auth/change-password`) - 23 Aralık 2025
  - ✅ Authenticated user için mevcut şifre ile değiştirme
  - ✅ JWT token'dan user bilgisi alma
  - ✅ Eski şifre doğrulama (Keycloak)
  - ✅ Yeni şifre validation (strength check)
  - ✅ Keycloak password update
  - ✅ `AuthenticatedAuthorizationAttribute` (admin gerekmez)
  
- ✅ **Reset Password Endpoint** (`POST /api/auth/reset-password`) - 23 Aralık 2025
  - ✅ Reset token ile yeni şifre belirleme
  - ✅ Token validation (expired, used kontrolü)
  - ✅ Password strength validation
  - ✅ Token tek kullanımlık yapma
  - ✅ Keycloak password update
  
- ✅ **Create Reset Token Endpoint** (`POST /api/auth/create-reset-token`) - 23 Aralık 2025
  - ✅ Admin only endpoint (test için)
  - ✅ Secure random token generation (Base64Url)
  - ✅ Token expiration (configurable, default 1 hour)
  - ✅ MongoDB'de token saklama

**Tamamlanan İmplementasyon Detayları:**
- ✅ Password reset token entity (MongoDB) - `PasswordResetToken`
- ✅ Password reset token repository - `IPasswordResetTokenRepository`
- ✅ Token generation (secure random, Base64Url format)
- ✅ Password policy validation - `PasswordValidator` helper
- ✅ Keycloak password update API - `UpdateUserPasswordAsync`
- ✅ Keycloak password validation API - `ValidateUserPasswordAsync`

**Eksik Özellikler:**
- [ ] **Forgot Password Endpoint** (`POST /api/auth/forgot-password`)
  - Email/username ile reset token oluşturma
  - Reset token'ı email ile gönderme (SMTP entegrasyonu)
  - Token expiration (örn: 1 saat)
  - Rate limiting (spam önleme)
  - **Durum:** Karar bekleniyor

**Gelecek İyileştirmeler:**
- [ ] Email service (SMTP) entegrasyonu (forgot password için)
- [ ] Rate limiting middleware (forgot password için)
- [ ] Audit logging (password change events)

---

### 8. Code Optimization - ✅ TAMAMLANDI (v1.1.0)

**Durum:** v1.1.0'da tamamlandı. Detaylar için "TAMAMLANAN ÖZELLİKLER" bölümüne bakın.

**Gelecek İyileştirmeler (Opsiyonel):**
- [ ] Unit test coverage increase
- [ ] Integration test coverage
- [ ] Performance testing (load testing)
- [ ] Code analysis tools (SonarQube, CodeQL)
- [ ] Performance profiling (dotMemory, Application Insights)
- [ ] Code metrics tracking
- [ ] Technical debt tracking

---

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

## 📋 EKSİKLİKLER VE ÖNCELİKLER

### 🔴 Yüksek Öncelik

1. ~~**User Group Assignment Events**~~ - ✅ TAMAMLANDI (23 Aralık 2025)
   
   **Tamamlanan İşler:**
   - ✅ `IEventPublisher` dependency injection eklendi (her iki handler'a)
   - ✅ `UserAddedToGroupEvent` publish ediliyor (AddUserToGroupCommandHandler)
   - ✅ `UserRemovedFromGroupEvent` publish ediliyor (RemoveUserFromGroupCommandHandler)
   - ✅ Event içeriği: UserId, Username, GroupId, GroupName, DomainId
   - ✅ Non-blocking error handling (event publish hatası işlemi durdurmuyor)
   
   **Event Routing:**
   - Routing key formatı: `{domainId}.useraddedtogroupevent` / `{domainId}.userremovedfromgroupevent`
   - Exchange: `mngkeeper.events` (topic exchange)

2. **User Profile Enhancement** - ⭐ YENİ ÖZELLİK
   - [ ] Title, Department, Gender, PhotoUrl alanları ekleme
   - [ ] Photo upload sistemi (MinIO)
   - [ ] Gender-based avatar renkleri
   - Detaylar için "GELECEK PLANLAR" bölümüne bakın

3. **User Profile Enhancement** - ⭐ YENİ ÖZELLİK
   - [ ] Title, Department, Gender, PhoneNumber, PhotoUrl alanları
   - [ ] Photo upload sistemi (MinIO)
   - [ ] Gender-based avatar renkleri
   - Detaylar için "GELECEK PLANLAR" bölümüne bakın

4. **Manager Role & Enhanced Authorization System** - ⭐ YENİ ÖZELLİK
   - [ ] `isManager` claim'i ekleme ve token generation
   - [ ] `ManagerAuthorizationAttribute` oluşturma (Admin veya Manager)
   - [ ] `AdminOnlyAuthorizationAttribute` oluşturma (Sadece Admin)
   - [ ] API endpoint yetkilendirmelerini güncelleme
   - Detaylar için "GELECEK PLANLAR" bölümüne bakın

5. **Server-Side Pagination & Performance Optimization** - ⚡ YENİ ÖZELLİK
   - [ ] Frontend'de server-side pagination implementasyonu
   - [ ] Search ve filter'ları backend'e taşıma
   - [ ] Backend'de sorting ve advanced filtering desteği
   - Detaylar için "GELECEK PLANLAR" bölümüne bakın

6. ~~**Password Management (Change & Reset)**~~ - ✅ TAMAMLANDI (23 Aralık 2025)
   - ✅ `POST /api/auth/change-password` - Şifre değiştirme
   - ✅ `POST /api/auth/reset-password` - Şifre sıfırlama
   - ✅ `POST /api/auth/create-reset-token` - Reset token oluşturma (test için)
   
   **Eksik:**
   - [ ] `POST /api/auth/forgot-password` - Şifremi unuttum (karar bekleniyor)

5. **RabbitMQ Event System Completion** - Event retry ve DLQ
   - [ ] Event retry mechanism
   - [ ] Dead Letter Queue (DLQ) handling
   - [ ] Event versioning

### 🟡 Orta Öncelik

4. **Permission Management** - Group-based permissions
   - [ ] Permission CRUD operations
   - [ ] Permission assignment to groups
   - [ ] Permission validation in API endpoints

5. **Audit Logging** - Comprehensive audit trail
   - [ ] Audit log entity
   - [ ] Audit log repository
   - [ ] Automatic audit logging for CRUD operations

6. **Test Coverage** - Unit ve integration testleri
   - [ ] Unit test coverage increase
   - [ ] Integration test coverage
   - [ ] Performance testing (load testing)

### 🟢 Düşük Öncelik

7. **Documentation** - API documentation güncellemesi
   - [ ] Swagger/OpenAPI documentation
   - [ ] API usage examples
   - [ ] Integration guides

---

## 🚀 NEXT STEPS

1. **User Profile Enhancement** - ⭐ YÜKSEK ÖNCELİK
   - Title, Department, Gender, PhoneNumber, PhotoUrl alanları
   - Photo upload sistemi (MinIO)
   - Gender-based avatar renkleri
   - User form ve list sayfalarını güncelleme
2. **Manager Role & Enhanced Authorization System** - ⭐ YÜKSEK ÖNCELİK
   - `isManager` claim'i ve token generation
   - Authorization attribute'ları (ManagerAuthorization, AdminOnlyAuthorization)
   - API endpoint yetkilendirmelerini güncelleme
3. **Server-Side Pagination & Performance Optimization** - ⚡ ORTA ÖNCELİK
   - Frontend'de server-side pagination implementasyonu
   - Search ve filter'ları backend'e taşıma
   - Backend'de sorting ve advanced filtering desteği
4. **Forgot Password Endpoint** - Email ile reset token gönderme (Karar bekleniyor)
3. **RabbitMQ Event System Completion** - Event retry ve DLQ (Orta Öncelik)
4. **Permission Management** - Group-based permissions (Orta Öncelik)
5. **Audit Logging** - Comprehensive audit trail (Orta Öncelik)
6. **Test Coverage** - Unit ve integration testleri (Orta Öncelik)

---

## 📚 İLGİLİ DOKÜMANTASYON

- [README.md](./README.md) - Genel bilgiler ve kullanım
- [ENVIRONMENT_VARIABLES.md](./ENVIRONMENT_VARIABLES.md) - Environment variables
- [CHANGELOG.md](./CHANGELOG.md) - Değişiklik geçmişi

---

**Son Güncelleme:** 30 Aralık 2025  
**Status:** Core features complete (98%), Manager Role & Authorization System planlandı  
**Son Tamamlanan:** 
- MongoDB Collection Bug Fix (@users ve @groups collection'larına yazma düzeltildi) - 23 Aralık 2025
- DataGatewaySyncService __syncInfo hatası düzeltildi - 23 Aralık 2025
- UserRepository ve GroupRepository BsonDocument uyumluluğu - 23 Aralık 2025
- Password Management (Change Password, Reset Password) - 23 Aralık 2025
- RabbitMQ User Group Assignment Events (user.group.added, user.group.removed) - 23 Aralık 2025
- RabbitMQ User Events (user.updated, user.deleted) - 23 Aralık 2025
- Authentication API iyileştirmeleri (domain@username formatı, tek domain otomatik seçimi) - 23 Aralık 2025

**Planlanan:**
- User Profile Enhancement (Title, Department, Gender, PhoneNumber, PhotoUrl, Photo Upload) - 30 Aralık 2025
- Manager Role & Enhanced Authorization System (isManager claim, ManagerAuthorizationAttribute, AdminOnlyAuthorizationAttribute) - 30 Aralık 2025
- Server-Side Pagination & Performance Optimization (Frontend server-side pagination, backend sorting/filtering) - 30 Aralık 2025

