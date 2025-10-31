# CreateDomain Endpoints

## Base URL
```
http://localhost:5001
```

## Endpoints

### 1. Test Domain CRUD Controller
**URL:** `/api/domaincrudtest`  
**Method:** `POST`  
**Description:** Domain oluşturma (In-memory test controller)

### 2. Create Domain Test Controller
**URL:** `/api/createdomaintest`  
**Method:** `POST`  
**Description:** Domain oluşturma (Mock handler ile test)

### 3. Main Domain Controller
**URL:** `/api/domains`  
**Method:** `POST`  
**Description:** Ana domain controller (Gerçek implementasyon)

## Request Headers
```json
{
  "Content-Type": "application/json"
}
```

## Response Format
```json
{
  "domainId": "string",
  "domainName": "string",
  "databaseName": "string",
  "adminUsername": "string",
  "adminEmail": "string",
  "createdAt": "datetime",
  "isSuccess": boolean,
  "errorMessage": "string"
}
```

## Test Scenarios

### 1. Başarılı Domain Oluşturma
- **Endpoint:** `/api/domaincrudtest`
- **Method:** `POST`
- **Expected Status:** `200 OK`

### 2. Duplicate Domain Hatası
- **Endpoint:** `/api/domaincrudtest`
- **Method:** `POST`
- **Expected Status:** `400 Bad Request`

### 3. Validation Hatası
- **Endpoint:** `/api/domaincrudtest`
- **Method:** `POST`
- **Expected Status:** `400 Bad Request`

## Docker Test
```bash
# Container'da test
curl -X POST http://localhost:5001/api/domaincrudtest \
  -H "Content-Type: application/json" \
  -d @test-domain.json
```

## Local Test
```bash
# PowerShell ile test
$domainData = @{
    domainName = "test-domain"
    displayName = "Test Domain"
    adminEmail = "admin@test-domain.com"
    adminPassword = "Admin123!"
    settings = @{
        maxUsers = 50
        maxAssets = 500
        enableMqtt = $true
        mqttSettings = @{
            brokerHost = "localhost"
            brokerPort = 1883
            username = "testuser"
            password = "testpass"
            topicPrefix = "TEST"
        }
    }
} | ConvertTo-Json -Depth 3

Invoke-RestMethod -Uri "http://localhost:5001/api/domaincrudtest" -Method POST -Headers @{"Content-Type"="application/json"} -Body $domainData
```
