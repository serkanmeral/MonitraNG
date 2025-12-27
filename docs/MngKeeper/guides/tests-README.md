# MngKeeper Test Suite

MngKeeper servisi için test dosyaları ve script'leri.

## 📁 Test Yapısı

```
tests/
├── README.md              # Bu dosya
├── api-test.ps1           # Genel API testi
├── domain-crud-test.ps1   # Domain CRUD testleri
├── user-crud-test.ps1     # User CRUD testleri
└── test-domain.json       # Test data
```

## 🚀 Testleri Çalıştırma

### Tüm Testler

```powershell
cd MngKeeper/tests
.\api-test.ps1
```

### Domain CRUD Testi

```powershell
.\domain-crud-test.ps1
```

### User CRUD Testi

```powershell
.\user-crud-test.ps1
```

## 📝 Test Data

Test verileri `ApplicationResources/test_data/mng_keeper/` klasöründe:

- **Domain Tests:** `ApplicationResources/test_data/mng_keeper/create_domain/`
- **Auth Tests:** `ApplicationResources/test_data/mng_keeper/auth/`

## ✅ Test Senaryoları

### 1. Domain Management
- ✅ Create domain
- ✅ Get domains
- ✅ Get domain by ID
- ✅ Get domain by name
- ✅ Update domain
- ✅ Delete domain

### 2. User Management
- ✅ Create user
- ✅ Get users (with pagination)
- ✅ Get user by ID
- ✅ Update user
- ✅ Delete user
- ✅ Add user to group
- ✅ Remove user from group

### 3. Authentication
- ✅ Get token (login)
- ✅ Refresh token
- ✅ Logout (revoke token)
- ✅ Token validation

## 🔧 Test Konfigürasyonu

**Base URL:** `http://localhost:5001`

**Test Domain:**
- Name: `test-domain-2`
- Admin: `test-domain-2_admin`
- Password: `Admin123!`

## 📊 Test Coverage (Planlanan)

```
Unit Tests:       [ ] 0%
Integration Tests: [x] 80%
E2E Tests:        [ ] 0%
```

## 🎯 Gelecek Testler

- [ ] Unit tests (xUnit)
- [ ] Integration tests (WebApplicationFactory)
- [ ] Performance tests
- [ ] Load tests
- [ ] Security tests

