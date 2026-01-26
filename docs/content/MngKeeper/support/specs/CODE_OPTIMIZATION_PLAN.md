# Code Optimization Plan - MngKeeper

**Date:** 16 Aralık 2025  
**Status:** ✅ Tamamlandı (v1.1.0 — Redis cache, MongoDB index, ExceptionHelper, CacheExtensions, IndexManager vb. uygulandı)  
**Priority:** Medium

---

## 📊 Tespit Edilen Optimizasyon Alanları

### 1. ⚠️ CRITICAL: Database Query Optimization

#### Problem: In-Memory Filtering & Pagination

**Dosyalar:**
- `GetUsersQueryHandler.cs`
- `GetGroupsQueryHandler.cs`

**Sorun:**
```csharp
// ❌ YANLIŞ: Tüm veriler memory'ye yükleniyor
var users = await _userRepository.GetByDomainIdAsync(claims.DomainId);
var usersList = users.ToList(); // Tüm kullanıcılar memory'de
var filteredUsers = new List<User>();

// In-memory filtering
foreach (var user in usersList) {
    if (user.Username.ToLower().Contains(searchTerm)) {
        filteredUsers.Add(user);
    }
}

// In-memory pagination
var pagedUsers = filteredUsers
    .Skip((request.Page - 1) * request.PageSize)
    .Take(request.PageSize)
    .ToList();
```

**Etki:**
- Büyük domain'lerde (1000+ users/groups) tüm veriler memory'ye yükleniyor
- Network trafiği artıyor
- Memory kullanımı yüksek
- Response time yavaşlıyor

**Çözüm:**
- MongoDB'de filtering yapılmalı
- MongoDB'de pagination yapılmalı
- Repository metodları filter ve pagination parametreleri almalı

---

### 2. 🔄 Code Duplication

#### Problem: Similar Query Handlers

**Dosyalar:**
- `GetUsersQueryHandler.cs` ve `GetGroupsQueryHandler.cs` %80 benzer kod

**Sorun:**
- Aynı filtering logic
- Aynı pagination logic
- Aynı error handling pattern

**Çözüm:**
- Base query handler oluşturulabilir
- Generic repository pattern ile ortak logic extract edilebilir
- Extension methods kullanılabilir

---

### 3. 🔤 Magic Strings & Numbers

#### Problem: Hardcoded Values

**Tespit Edilenler:**
```csharp
// Group names
"users", "admins", "managers", "guests"

// System values
"system" // CreatedBy, UpdatedBy

// Database names
"MngKeeper" // Default database name
```

**Çözüm:**
- Constants class oluşturulmalı
- Configuration'dan okunmalı
- Enum kullanılabilir (group names için)

---

### 4. ⚠️ Exception Handling

#### Problem: Generic Exception Catching

**Tespit Edilenler:**
```csharp
catch (Exception ex) // ❌ Çok genel
{
    _logger.LogError(ex, "Error getting users");
    return new GetUsersResponse { IsSuccess = false };
}
```

**Sorun:**
- Tüm exception'lar aynı şekilde handle ediliyor
- Spesifik exception'lar (MongoDB, Keycloak, etc.) yakalanmıyor
- Retry logic yok
- Circuit breaker pattern yok

**Çözüm:**
- Spesifik exception handling
- Retry policies (Polly)
- Circuit breaker pattern

---

### 5. 💾 Redis Cache Kullanımı

#### Problem: Cache Kullanılmıyor

**Tespit:**
- Redis service mevcut ama query handler'larda kullanılmıyor
- Her request'te database'e gidiliyor
- Cache hit ratio: 0%

**Çözüm:**
- Query handler'larda cache check eklenmeli
- Cache-aside pattern
- TTL ayarları
- Cache invalidation strategy

---

### 6. 🔍 Missing Indexes

#### Problem: Index Kontrolü Yok

**Tespit:**
- MongoDB collection'larda index tanımları görünmüyor
- DomainId, Username, Email gibi sık sorgulanan field'lar için index yok

**Çözüm:**
- Index tanımları eklenmeli
- Index creation script'i
- Index usage monitoring

---

## 🎯 Optimizasyon Öncelikleri

### Phase 1: Critical Performance (Hemen)
1. ✅ Database query optimization (in-memory → MongoDB)
2. ✅ Missing indexes ekleme
3. ✅ Redis cache integration

### Phase 2: Code Quality (Kısa Vadede)
4. ✅ Code duplication reduction
5. ✅ Magic strings elimination
6. ✅ Exception handling improvement

### Phase 3: Advanced (Orta Vadede)
7. ✅ Retry policies
8. ✅ Circuit breaker
9. ✅ Performance monitoring

---

## 📝 Implementation Plan

### Step 1: Database Query Optimization

**Repository Interface Güncellemesi:**
```csharp
// IUserRepository.cs
Task<(IEnumerable<User> Users, int TotalCount)> GetByDomainIdAsync(
    string domainId, 
    int page, 
    int pageSize, 
    string? searchTerm = null, 
    bool? isActive = null);
```

**MongoDB Query:**
```csharp
// Filter builder
var filterBuilder = Builders<User>.Filter;
var filter = filterBuilder.Eq(u => u.DomainId, domainId);

if (!string.IsNullOrEmpty(searchTerm)) {
    var searchFilter = filterBuilder.Or(
        filterBuilder.Regex(u => u.Username, new BsonRegularExpression(searchTerm, "i")),
        filterBuilder.Regex(u => u.Email, new BsonRegularExpression(searchTerm, "i"))
    );
    filter &= searchFilter;
}

if (isActive.HasValue) {
    filter &= filterBuilder.Eq(u => u.IsActive, isActive.Value);
}

// Count
var totalCount = await _collection.CountDocumentsAsync(filter);

// Pagination
var users = await _collection
    .Find(filter)
    .Skip((page - 1) * pageSize)
    .Limit(pageSize)
    .ToListAsync();
```

---

### Step 2: Constants Class

**Constants.cs:**
```csharp
public static class SystemGroups
{
    public const string Admins = "admins";
    public const string Managers = "managers";
    public const string Users = "users";
    public const string Guests = "guests";
    
    public static readonly string[] All = { Admins, Managers, Users, Guests };
}

public static class SystemUsers
{
    public const string System = "system";
}

public static class DatabaseNames
{
    public const string Default = "MngKeeper";
}
```

---

### Step 3: Redis Cache Integration

**Cache Pattern:**
```csharp
// GetUsersQueryHandler.cs
public async Task<GetUsersResponse> Handle(...)
{
    // 1. Check cache
    var cacheKey = $"users:domain:{domainId}:page:{page}:size:{pageSize}";
    var cached = await _cacheService.GetAsync<GetUsersResponse>(cacheKey);
    if (cached != null) return cached;
    
    // 2. Query database
    var result = await _userRepository.GetByDomainIdAsync(...);
    
    // 3. Cache result
    await _cacheService.SetAsync(cacheKey, result, TimeSpan.FromMinutes(5));
    
    return result;
}
```

---

### Step 4: Index Creation

**Index Script:**
```csharp
// User collection indexes
await _collection.Indexes.CreateOneAsync(
    new CreateIndexModel<User>(
        Builders<User>.IndexKeys.Ascending(u => u.DomainId),
        new CreateIndexOptions { Name = "idx_domainId" }));

await _collection.Indexes.CreateOneAsync(
    new CreateIndexModel<User>(
        Builders<User>.IndexKeys.Ascending(u => u.Username),
        new CreateIndexOptions { Name = "idx_username" }));

await _collection.Indexes.CreateOneAsync(
    new CreateIndexModel<User>(
        Builders<User>.IndexKeys.Compound(
            Builders<User>.IndexKeys.Ascending(u => u.DomainId),
            Builders<User>.IndexKeys.Ascending(u => u.IsActive)),
        new CreateIndexOptions { Name = "idx_domainId_isActive" }));
```

---

## 📊 Expected Improvements

| Metric | Before | After | Improvement |
|--------|--------|-------|-------------|
| Memory Usage (1000 users) | ~50MB | ~5MB | 90% ↓ |
| Response Time (pagination) | ~500ms | ~50ms | 90% ↓ |
| Database Queries | 1 (all data) | 1 (filtered) | Optimized |
| Cache Hit Ratio | 0% | 70%+ | +70% |
| Code Duplication | High | Low | 60% ↓ |

---

## ✅ Checklist

- [ ] Step 1: Database query optimization
- [ ] Step 2: Constants class creation
- [ ] Step 3: Redis cache integration
- [ ] Step 4: Index creation
- [ ] Step 5: Code duplication reduction
- [ ] Step 6: Exception handling improvement
- [ ] Step 7: Performance testing
- [ ] Step 8: Documentation update

---

**Son Güncelleme:** 16 Aralık 2025

