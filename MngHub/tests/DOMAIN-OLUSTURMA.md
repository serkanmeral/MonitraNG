# MngKeeper Domain Oluşturma Endpoint

## 📍 Endpoint

```http
POST https://localhost:5001/api/domain
Content-Type: application/json
```

## 📦 Request Body

### Minimum (Zorunlu) Alanlar

```json
{
  "domainName": "acme-corp",
  "displayName": "ACME Corporation",
  "adminEmail": "admin@acme.com",
  "adminPassword": "SecurePass123!"
}
```

### Tam (Opsiyonel Settings ile)

```json
{
  "domainName": "acme-corp",
  "displayName": "ACME Corporation",
  "adminEmail": "admin@acme.com",
  "adminPassword": "SecurePass123!",
  "settings": {
    "maxUsers": 100,
    "maxAssets": 1000,
    "enableMqtt": false,
    "mqttSettings": {
      "brokerHost": "localhost",
      "brokerPort": 1883,
      "username": "",
      "password": "",
      "topicPrefix": "MNG"
    },
    "customSettings": {}
  }
}
```

## 📋 Alan Açıklamaları

### Zorunlu Alanlar

- **domainName** (string, required)
  - Domain adı (unique)
  - Kurallar: lowercase, numbers, hyphens only
  - Örnek: `"acme-corp"`, `"test-domain-20251223"`

- **displayName** (string, required)
  - Domain'in görünen adı
  - Örnek: `"ACME Corporation"`

- **adminEmail** (string, required)
  - Admin kullanıcısının email adresi
  - Örnek: `"admin@acme.com"`

- **adminPassword** (string, required)
  - Admin kullanıcısının şifresi
  - Minimum güvenlik gereksinimleri: Keycloak password policy'ye uygun olmalı

### Opsiyonel Alanlar

- **settings** (object, optional)
  - Domain ayarları
  - Varsayılan değerler kullanılır if not provided

  - **maxUsers** (int, optional)
    - Maksimum kullanıcı sayısı
    - Varsayılan: `100`

  - **maxAssets** (int, optional)
    - Maksimum asset sayısı
    - Varsayılan: `1000`

  - **enableMqtt** (bool, optional)
    - MQTT desteği aktif/pasif
    - Varsayılan: `false`

  - **mqttSettings** (object, optional)
    - MQTT broker ayarları
    - Sadece `enableMqtt: true` ise kullanılır

  - **customSettings** (object, optional)
    - Özel domain ayarları
    - Key-value pairs

## ✅ Response

### Başarılı Response (201 Created)

```json
{
  "domainId": "507f1f77bcf86cd799439011",
  "domainName": "acme-corp",
  "databaseName": "mng_acme-corp",
  "realmName": "acme-corp",
  "adminUsername": "acme-corp_admin",
  "adminEmail": "admin@acme.com",
  "createdAt": "2025-11-05T10:00:00Z",
  "isSuccess": true,
  "message": "Domain 'acme-corp' created successfully with 11 steps",
  "failedStep": null
}
```

### Hata Response (400 Bad Request)

```json
{
  "isSuccess": false,
  "errorMessage": "Domain name already exists",
  "domainId": null
}
```

## 🔄 Domain Oluşturma Pipeline (11 Adım)

1. ✅ Domain validation
2. ✅ MongoDB database oluşturma (`mng_{domainName}`)
3. ✅ Database collections initialization
4. ✅ Keycloak realm oluşturma
5. ✅ Default groups oluşturma (admins, managers, users, guests)
6. ✅ Admin user oluşturma (`{domainName}_admin`)
7. ✅ RabbitMQ event publishing (`system.mngkeeper.domain.created`)
8. ✅ Redis cache initialization
9. ✅ MinIO bucket oluşturma (`mng-{domainName}`)
10. ✅ Domain activation

## 📝 PowerShell Örneği

```powershell
$baseUrl = "https://localhost:5001"
[System.Net.ServicePointManager]::ServerCertificateValidationCallback = {$true}

$domainBody = @{
    domainName = "test-domain-$(Get-Date -Format 'yyyyMMddHHmmss')"
    displayName = "Test Domain"
    adminEmail = "admin@test.com"
    adminPassword = "AdminPass123!"
    settings = @{
        maxUsers = 100
        maxAssets = 1000
        enableMqtt = $false
    }
} | ConvertTo-Json

$response = Invoke-RestMethod -Uri "$baseUrl/api/domain" `
    -Method POST `
    -Body $domainBody `
    -ContentType "application/json" `
    -SkipCertificateCheck

Write-Host "Domain ID: $($response.domainId)"
Write-Host "Admin Username: $($response.adminUsername)"
```

## 📝 cURL Örneği

```bash
curl -X POST https://localhost:5001/api/domain \
  -H "Content-Type: application/json" \
  -d '{
    "domainName": "acme-corp",
    "displayName": "ACME Corporation",
    "adminEmail": "admin@acme.com",
    "adminPassword": "SecurePass123!"
  }' \
  -k
```

## ⚠️ Önemli Notlar

1. **Domain Name Kuralları:**
   - Sadece lowercase harfler, rakamlar ve tire (-) kullanılabilir
   - Underscore (_) veya özel karakterler kullanılamaz
   - Unique olmalı (aynı isimde domain olamaz)

2. **Admin Password:**
   - Keycloak password policy'ye uygun olmalı
   - Genellikle: minimum 8 karakter, büyük/küçük harf, rakam

3. **Domain Oluşturma Süresi:**
   - 11 adımlı pipeline çalışır
   - Genellikle 10-30 saniye sürer
   - Domain oluşturulduktan sonra mapper'ları yapılandırmanız gerekir

4. **Mapper Yapılandırması:**
   - Domain oluşturulduktan sonra mutlaka çalıştırılmalı:
   ```http
   POST https://localhost:5001/api/admin/realms/{domainName}/configure-mappers
   ```
   - Bu, JWT token'a `domain_id` ve `domain_name` claim'lerini ekler

