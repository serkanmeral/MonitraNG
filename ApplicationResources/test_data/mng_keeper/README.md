# Test Data - MngKeeper API

Bu klasör MngKeeper API'si için test verilerini ve endpoint bilgilerini içerir.

## 📁 Dosya Yapısı

```
test_data/
└── mng_keeper/
    ├── README.md                           # Bu dosya
    ├── create_domain/
    │   ├── createdomain-endpoints.md           # CreateDomain endpoint bilgileri
    │   ├── createdomain-request-bodies.json    # Örnek request body'leri
    │   ├── test-createdomain.ps1              # CreateDomain test script'i
    │   ├── test-domain-basic.json             # Temel domain test verisi
    │   ├── test-domain-production.json        # Production domain test verisi
    │   └── test-domain-minimal.json           # Minimal domain test verisi
    └── auth/
        ├── auth-endpoints.md                   # Authentication endpoint bilgileri
        ├── auth-request-bodies.json            # Auth örnek request body'leri
        ├── test-auth.ps1                      # Authentication test script'i
        ├── login-request.json                 # Login test verisi
        ├── login-request-email.json           # Email ile login test verisi
        ├── refresh-token.json                 # Refresh token test verisi
        ├── validate-token.json                # Token validation test verisi
        ├── logout-request.json                # Logout test verisi
        └── invalid-login.json                 # Geçersiz login test verisi
```

## 🚀 Hızlı Başlangıç

### 1. Docker Container'ı Başlat
```bash
cd ../../mng_apps
docker-compose up -d
```

### 2. CreateDomain Test Script'ini Çalıştır
```bash
cd ../test_data/mng_keeper/create_domain
.\test-createdomain.ps1
```

### 3. Authentication Test Script'ini Çalıştır
```bash
cd ../auth
.\test-auth.ps1
```

## 📋 Endpoint'ler

### Domain Endpoints
- **Main Controller:** `/api/domain` (POST, GET, PUT, DELETE)
  - POST `/api/domain` - Create domain
  - GET `/api/domain` - Get all domains
  - GET `/api/domain/{id}` - Get domain by ID
  - GET `/api/domain/name/{name}` - Get domain by name
  - PUT `/api/domain/{id}` - Update domain
  - DELETE `/api/domain/{id}` - Delete domain

### Authentication Endpoints
- **Get Token:** `/api/auth/token` (POST)

### User Endpoints
- **Main Controller:** `/api/user` (POST, GET, PUT, DELETE)
  - POST `/api/user` - Create user
  - GET `/api/user` - Get all users (with pagination)
  - GET `/api/user/{userId}` - Get user by ID
  - PUT `/api/user/{userId}` - Update user
  - DELETE `/api/user/{userId}` - Delete user
  - POST `/api/user/{userId}/groups/{groupId}` - Add user to group
  - DELETE `/api/user/{userId}/groups/{groupId}` - Remove user from group

### Group Endpoints
- **Main Controller:** `/api/group` (POST, GET, PUT, DELETE)
  - POST `/api/group` - Create group
  - GET `/api/group` - Get all groups (with pagination)
  - PUT `/api/group/{groupId}` - Update group
  - DELETE `/api/group/{groupId}` - Delete group

## 🧪 Test Senaryoları

### CreateDomain Testleri
1. **Başarılı Domain Oluşturma** - `test-domain-basic.json`
2. **Production Domain** - `test-domain-production.json`
3. **Minimal Domain** - `test-domain-minimal.json`
4. **Duplicate Domain Hatası** - `test-domain-basic.json` (tekrar)

### Authentication Testleri
1. **Başarılı Login** - `login-request.json`
2. **Email ile Login** - `login-request-email.json`
3. **Geçersiz Login** - `invalid-login.json`
4. **Token Validation** - `validate-token.json`
5. **Refresh Token** - `refresh-token.json`
6. **Logout** - `logout-request.json`
7. **Protected Endpoint Access** - Token ile korumalı endpoint'lere erişim

## 🔧 Kullanım Örnekleri

### CreateDomain Test
```powershell
$domainData = @{
    domainName = "test-domain"
    displayName = "Test Domain"
    adminEmail = "admin@test-domain.com"
    adminPassword = "Admin123!"
} | ConvertTo-Json

Invoke-RestMethod -Uri "http://localhost:5001/api/domain" -Method POST -Headers @{"Content-Type"="application/json"} -Body $domainData
```

### Authentication Test
```powershell
$loginData = @{
    username = "admin@test-domain.com"
    password = "Admin123!"
    domainName = "test-domain"
} | ConvertTo-Json

Invoke-RestMethod -Uri "http://localhost:5001/api/auth/token" -Method POST -Headers @{"Content-Type"="application/json"} -Body $loginData
```

### Protected Endpoint Access
```powershell
$headers = @{
    "Authorization" = "Bearer YOUR_ACCESS_TOKEN"
    "Content-Type" = "application/json"
}

Invoke-RestMethod -Uri "http://localhost:5001/api/domain" -Method GET -Headers $headers
```

## 📊 Test Sonuçları

### CreateDomain Başarılı Response
```json
{
  "domainId": "ba27c28f-aec6-4a5f-ae7b-c6f704fc5c54",
  "domainName": "test-domain",
  "databaseName": "mng_test-domain",
  "adminUsername": "admin",
  "adminEmail": "admin@test-domain.com",
  "createdAt": "2025-08-12T20:43:26.8678114Z",
  "isSuccess": true,
  "errorMessage": null
}
```

### Authentication Başarılı Response
```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "refreshToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "expiresIn": 3600,
  "tokenType": "Bearer",
  "user": {
    "userId": "29f60d5f-e64d-4e73-9ec2-edf09f4d8e00",
    "username": "admin@test-domain.com",
    "email": "admin@test-domain.com",
    "domainId": "test-domain",
    "roles": ["admin", "user"]
  },
  "isSuccess": true,
  "errorMessage": null
}
```

## 🔗 İlgili Dosyalar

- **Docker Compose:** `../../mng_apps/docker-compose.yml`
- **API Controllers:** `../../../MngKeeper/Presentation/MngKeeper.Api/Controllers/`
- **Domain Controller:** `../../../MngKeeper/Presentation/MngKeeper.Api/Controllers/DomainCrudTestController.cs`
- **Auth Controller:** `../../../MngKeeper/Presentation/MngKeeper.Api/Controllers/AuthController.cs`

## 📝 Notlar

- API gerçek MongoDB ve Keycloak entegrasyonu kullanır
- Test verileri MongoDB'de saklanır
- Authentication testleri için önce domain oluşturulmalı
- Token'lar JWT formatında
- Tüm test controller'lar temizlendi, gerçek controller'lar kullanılıyor
