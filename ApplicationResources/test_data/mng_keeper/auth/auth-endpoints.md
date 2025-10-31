# Authentication Endpoints

## Base URL
```
http://localhost:5001
```

## Endpoints

### 1. Login (Token Alma)
**URL:** `/api/auth/login`  
**Method:** `POST`  
**Description:** Kullanıcı girişi ve token alma

### 2. Refresh Token
**URL:** `/api/auth/refresh`  
**Method:** `POST`  
**Description:** Token yenileme

### 3. Logout
**URL:** `/api/auth/logout`  
**Method:** `POST`  
**Description:** Kullanıcı çıkışı ve token geçersizleştirme

### 4. Validate Token
**URL:** `/api/auth/validate`  
**Method:** `POST`  
**Description:** Token doğrulama

## Request Headers
```json
{
  "Content-Type": "application/json"
}
```

## Response Format

### Login Response
```json
{
  "accessToken": "string",
  "refreshToken": "string",
  "expiresIn": "number",
  "tokenType": "Bearer",
  "user": {
    "userId": "string",
    "username": "string",
    "email": "string",
    "domainId": "string",
    "roles": ["string"]
  },
  "isSuccess": boolean,
  "errorMessage": "string"
}
```

### Refresh Token Response
```json
{
  "accessToken": "string",
  "refreshToken": "string",
  "expiresIn": "number",
  "tokenType": "Bearer",
  "isSuccess": boolean,
  "errorMessage": "string"
}
```

### Validate Token Response
```json
{
  "isValid": boolean,
  "user": {
    "userId": "string",
    "username": "string",
    "email": "string",
    "domainId": "string",
    "roles": ["string"]
  },
  "isSuccess": boolean,
  "errorMessage": "string"
}
```

## Test Scenarios

### 1. Başarılı Login
- **Endpoint:** `/api/auth/login`
- **Method:** `POST`
- **Expected Status:** `200 OK`

### 2. Geçersiz Kullanıcı Bilgileri
- **Endpoint:** `/api/auth/login`
- **Method:** `POST`
- **Expected Status:** `401 Unauthorized`

### 3. Token Yenileme
- **Endpoint:** `/api/auth/refresh`
- **Method:** `POST`
- **Expected Status:** `200 OK`

### 4. Geçersiz Token
- **Endpoint:** `/api/auth/validate`
- **Method:** `POST`
- **Expected Status:** `401 Unauthorized`

## Docker Test
```bash
# Login test
curl -X POST http://localhost:5001/api/auth/login \
  -H "Content-Type: application/json" \
  -d @login-request.json

# Token validation test
curl -X POST http://localhost:5001/api/auth/validate \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -d @validate-token.json
```

## Local Test
```bash
# PowerShell ile login test
$loginData = @{
    username = "admin@test-domain.com"
    password = "Admin123!"
    domainId = "test-domain"
} | ConvertTo-Json

Invoke-RestMethod -Uri "http://localhost:5001/api/auth/login" -Method POST -Headers @{"Content-Type"="application/json"} -Body $loginData
```

## Token Kullanımı
```bash
# Protected endpoint'e erişim
curl -X GET http://localhost:5001/api/domains \
  -H "Authorization: Bearer YOUR_ACCESS_TOKEN" \
  -H "Content-Type: application/json"
```
