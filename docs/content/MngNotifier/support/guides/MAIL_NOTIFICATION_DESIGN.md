# Mail Notification Tasarım Dokümantasyonu

**Tarih:** 11 Ocak 2026  
**Versiyon:** 1.0.0  
**Durum:** 📋 Tasarım Aşaması

---

## 📋 İÇİNDEKİLER

1. [Genel Bakış](#genel-bakış)
2. [Mail Notification Yöntemleri](#mail-notification-yöntemleri)
3. [Template Yönetimi](#template-yönetimi)
4. [API Endpoint'leri](#api-endpointleri)
5. [RabbitMQ Event Yapısı](#rabbitmq-event-yapısı)
6. [Placeholder Replacement](#placeholder-replacement)
7. [MongoDB Yapısı](#mongodb-yapısı)
8. [Örnek Senaryolar](#örnek-senaryolar)

---

## 🎯 GENEL BAKIŞ

MngNotifier servisi, mail notification'ları **üç farklı yöntemle** gönderebilir:

1. **Direct API Endpoint (Unauthenticated):** HTTP POST ile direkt mail gönderme (token gerektirmez)
2. **Template-Based API Endpoint (Authenticated):** Template kullanarak dinamik mail gönderme (token gerekli)
3. **RabbitMQ Event Consumer:** Event-driven mail gönderme

**Authentication Durumları:**
- Direct endpoint: ❌ **No authentication** (bootstrap senaryoları için)
- Template-based endpoint: ✅ **Authentication required** (MngDataGateway erişimi için)

---

## 📧 MAIL NOTIFICATION YÖNTEMLERİ

### 1. Direct API Endpoint (Unauthenticated)

**Endpoint:** `POST /api/v1/notifications/send`

**Amaç:** HTTP API üzerinden direkt mail gönderme (token gerektirmez)

**Authentication:** ❌ **No authentication required** (AllowAnonymous)

**Kullanım Senaryoları:**
- Domain oluşturulduğunda admin bilgilerini gönderme (henüz token yok)
- Sistem bootstrapping sırasında ilk kullanıcı oluşturma
- External sistemlerden mail gönderme (token yok)
- Public API'lerden mail gönderme

**Request Body:**
```json
{
  "to": ["user@example.com", "admin@example.com"],
  "cc": ["manager@example.com"],  // Optional
  "from": {  // Optional - appsettings'ten default alınır
    "email": "custom@example.com",
    "name": "Custom Name"  // Optional
  },
  "subject": "Domain Oluşturuldu",
  "body": "<h1>Domain Bilgileri</h1><p>Domain: example.com</p><p>Admin Kullanıcı: admin</p><p>Şifre: TempPass123!</p>",
  "isHtml": true  // Optional, default: true
}
```

**From Bilgisi:**
- **Default:** `appsettings.json` içindeki `MngNotifierSettings.Mail.DefaultFrom` değerinden alınır
- **Override:** Request body'de `from` parametresi varsa, bu değer kullanılır
- **Format:** `{ "email": "string", "name": "string (optional)" }`

**Response:**
```json
{
  "notificationId": "507f1f77bcf86cd799439011",
  "status": "queued",
  "queuedAt": "2026-01-11T10:00:00Z"
}
```

**İş Akışı:**
1. Request validation (FluentValidation)
2. Notification entity oluştur (status: `queued`)
3. MongoDB'ye kaydet
4. RabbitMQ queue'ya ekle (`mngnotifier.mail.send`)
5. Response döndür (notificationId)
6. Background worker mail'i gönderir

**Avantajlar:**
- ✅ Token gerektirmez (bootstrap senaryoları için ideal)
- ✅ Senkron API response (notificationId döner)
- ✅ Hızlı ve basit kullanım
- ✅ HTTP-based, her yerden erişilebilir

**Dezavantajlar:**
- ⚠️ Mail gönderimi async (queue'da bekler)
- ⚠️ Delivery status için ayrı endpoint gerekir
- ⚠️ Güvenlik: Rate limiting ve IP whitelist önerilir (gelecekte)

**Güvenlik Notları:**
- Rate limiting uygulanmalı (spam önleme)
- IP whitelist desteği (opsiyonel - gelecekte)
- Request size limit (body uzunluğu kontrolü)

---

### 2. RabbitMQ Event Consumer

**Amaç:** Event-driven mail gönderme (diğer servislerden event dinleme)

**Queue:** `mngnotifier.mail.send` (configurable)

**Event Model:**
```json
{
  "eventType": "MailNotificationEvent",
  "to": ["user@example.com"],
  "cc": ["manager@example.com"],  // Optional
  "from": {  // Optional - appsettings'ten default alınır
    "email": "custom@example.com",
    "name": "Custom Name"  // Optional
  },
  "subject": "Welcome Email",
  "body": "<h1>Welcome!</h1>",
  "isHtml": true,
  "metadata": {
    "source": "MngKeeper",
    "userId": "user123",
    "domainId": "meral"
  }
}
```

**From Bilgisi:**
- **Default:** `appsettings.json` içindeki `MngNotifierSettings.Mail.DefaultFrom` değerinden alınır
- **Override:** Event'te `from` parametresi varsa, bu değer kullanılır

**İş Akışı:**
1. RabbitMQ consumer event'i yakalar
2. Event validation
3. Notification entity oluştur (status: `queued`)
4. MongoDB'ye kaydet
5. Background worker mail'i gönderir
6. Acknowledgment (başarılı ise)

**Avantajlar:**
- ✅ Loose coupling (servisler arası bağımlılık yok)
- ✅ Scalable (multiple consumer instances)
- ✅ Retry mekanizması (RabbitMQ built-in)
- ✅ Event-driven architecture

**Dezavantajlar:**
- ⚠️ Event format standardizasyonu gerekir
- ⚠️ Error handling daha karmaşık

**Kullanım Senaryoları:**
- User registration → Welcome email
- Password reset → Reset email
- Order confirmation → Confirmation email
- System alerts → Alert email

---

## 📝 TEMPLATE YÖNETİMİ

### Template'ler MngDataGateway Üzerinden Yönetilir

**Yaklaşım:** Template'ler, MngDataGateway'in dataset ve data endpoint'leri kullanılarak yönetilir. Bu sayede:
- ✅ Kod tekrarı yok (CRUD operasyonları zaten mevcut)
- ✅ Query, search, filter özellikleri otomatik gelir
- ✅ Mevcut altyapı kullanılır
- ✅ Template'ler de bir dataset olarak yönetilir

### Template Dataset Schema

**Dataset Name:** `@mail_templates`

**Dataset Schema (MngDataGateway'de oluşturulacak):**
```json
{
  "name": "@mail_templates",
  "description": "Mail notification templates",
  "forceSchema": true,
  "logging": "none",
  "publish_mode": "none",
  "fields": [
    {
      "fieldType": "text",
      "name": "templateId",
      "title": "Template ID",
      "mandatory": true,
      "unique": true
    },
    {
      "fieldType": "text",
      "name": "name",
      "title": "Template Name",
      "mandatory": true
    },
    {
      "fieldType": "text",
      "name": "subject",
      "title": "Email Subject",
      "mandatory": true
    },
    {
      "fieldType": "text",
      "name": "body",
      "title": "Email Body (HTML)",
      "mandatory": true
    },
    {
      "fieldType": "text",
      "name": "variables",
      "title": "Required Variables",
      "isArray": true,
      "mandatory": false
    },
    {
      "fieldType": "bool",
      "name": "isActive",
      "title": "Is Active",
      "mandatory": true,
      "defaultValue": true
    }
  ],
  "indexList": [
    {
      "name": "templateId_unique",
      "fields": { "templateId": 1 },
      "unique": true
    },
    {
      "name": "isActive_index",
      "fields": { "isActive": 1 },
      "unique": false
    }
  ]
}
```

### Template Data Document

**MongoDB Document (MngDataGateway'de `@mail_templates` collection'ında):**
```json
{
  "__dataId": "507f1f77bcf86cd799439011",
  "templateId": "welcome-email",
  "name": "Welcome Email Template",
  "subject": "Hoş geldiniz {{userName}}!",
  "body": "<h1>Merhaba {{userName}}</h1><p>Email adresiniz: {{userEmail}}</p><p>Kayıt tarihi: {{registrationDate}}</p>",
  "variables": ["userName", "userEmail", "registrationDate"],
  "isActive": true
}
```

### Template CRUD Operations (MngDataGateway API)

**Template'ler MngDataGateway üzerinden yönetilir:**

**Dataset Oluşturma:**
```http
POST /api/v1/datasets
Content-Type: application/json

{
  "name": "@mail_templates",
  "description": "Mail notification templates",
  "fields": [...]
}
```

**Template Oluşturma:**
```http
POST /api/v1/data/@mail_templates
Content-Type: application/json

{
  "templateId": "welcome-email",
  "name": "Welcome Email Template",
  "subject": "Hoş geldiniz {{userName}}!",
  "body": "<h1>Merhaba {{userName}}</h1><p>Email: {{userEmail}}</p>",
  "variables": ["userName", "userEmail"],
  "isActive": true
}
```

**Template Listeleme:**
```http
GET /api/v1/data/@mail_templates?filter=isActive:eq:true
```

**Template Getirme:**
```http
GET /api/v1/data/@mail_templates?filter=templateId:eq:welcome-email
```

**Template Güncelleme:**
```http
PUT /api/v1/data/@mail_templates/{__dataId}
Content-Type: application/json

{
  "templateId": "welcome-email",
  "name": "Welcome Email Template (Updated)",
  "subject": "Hoş geldiniz {{userName}}!",
  "body": "<h1>Merhaba {{userName}}</h1><p>Email: {{userEmail}}</p>",
  "variables": ["userName", "userEmail"],
  "isActive": true
}
```

**Template Silme:**
```http
DELETE /api/v1/data/@mail_templates/{__dataId}
```

### MngNotifier'da Template Okuma

**MngNotifier, template'leri okumak için MngDataGateway API'sini kullanır:**

```csharp
// MngNotifier içinde
public class TemplateService : ITemplateService
{
    private readonly HttpClient _httpClient;
    private readonly string _dataGatewayUrl;
    
    public async Task<MailTemplate?> GetTemplateAsync(string templateId)
    {
        // MngDataGateway API'den template'i getir
        var response = await _httpClient.GetAsync(
            $"{_dataGatewayUrl}/api/v1/data/@mail_templates?filter=templateId:eq:{templateId}&filter=isActive:eq:true");
        
        // Response'u parse et ve MailTemplate'e map et
        // ...
    }
}
```

**Avantajlar:**
- ✅ Template CRUD işlemleri MngDataGateway'de (kod tekrarı yok)
- ✅ MngNotifier sadece template okuma ve placeholder replacement yapar
- ✅ Query, search, filter özellikleri otomatik gelir
- ✅ Template'ler için özel bir servis yazmaya gerek yok

**Create Template Request:**
```json
{
  "templateId": "welcome-email",
  "name": "Welcome Email Template",
  "subject": "Hoş geldiniz {{userName}}!",
  "body": "<h1>Merhaba {{userName}}</h1><p>Email: {{userEmail}}</p>",
  "variables": ["userName", "userEmail"]
}
```

**Response:**
```json
{
  "templateId": "welcome-email",
  "name": "Welcome Email Template",
  "subject": "Hoş geldiniz {{userName}}!",
  "body": "<h1>Merhaba {{userName}}</h1><p>Email: {{userEmail}}</p>",
  "variables": ["userName", "userEmail"],
  "isActive": true,
  "createdAt": "2026-01-11T10:00:00Z",
  "createdBy": "admin@example.com"
}
```

---

## 🔄 TEMPLATE-BASED MAIL GÖNDERME

### Endpoint

**Endpoint:** `POST /api/v1/notifications/send-template`

**Amaç:** Template kullanarak dinamik mail gönderme

**Authentication:** ✅ **Authentication required** (JWT token gerekli)

**Kullanım Senaryoları:**
- Authenticated kullanıcılar için template-based mail gönderme
- MngDataGateway'e erişim gerektiren durumlar
- Template yönetimi yapılan durumlar

**Request Body:**
```json
{
  "to": ["user@example.com"],
  "cc": ["manager@example.com"],  // Optional
  "from": {  // Optional - appsettings'ten default alınır
    "email": "custom@example.com",
    "name": "Custom Name"  // Optional
  },
  "templateId": "welcome-email",
  "messageObject": {
    "userName": "Ahmet Yılmaz",
    "userEmail": "ahmet@example.com",
    "registrationDate": "2026-01-11"
  }
}
```

**From Bilgisi:**
- **Default:** `appsettings.json` içindeki `MngNotifierSettings.Mail.DefaultFrom` değerinden alınır
- **Override:** Request body'de `from` parametresi varsa, bu değer kullanılır
- **Format:** `{ "email": "string", "name": "string (optional)" }`

**İş Akışı:**
1. Authentication kontrolü (JWT token validation)
2. Template'i MngDataGateway API'den getir (`templateId` ile, token ile)
3. Template validation (isActive kontrolü)
4. Placeholder replacement:
   - `{{userName}}` → `"Ahmet Yılmaz"`
   - `{{userEmail}}` → `"ahmet@example.com"`
   - `{{registrationDate}}` → `"2026-01-11"`
5. Final subject ve body oluştur
6. From bilgisini belirle (request'te varsa kullan, yoksa appsettings'ten al)
7. Direct API endpoint ile aynı akış (queue'ya ekle)

**Response:**
```json
{
  "notificationId": "507f1f77bcf86cd799439011",
  "status": "queued",
  "queuedAt": "2026-01-11T10:00:00Z",
  "templateId": "welcome-email"
}
```

**Hata Senaryoları:**
- Template bulunamadı → `404 Not Found`
- Template inactive → `400 Bad Request` ("Template is not active")
- Missing required variable → `400 Bad Request` ("Missing required variable: userName")
- Invalid variable value → `400 Bad Request` ("Invalid variable value")

---

## 🔤 PLACEHOLDER REPLACEMENT

### Placeholder Format

**Basit Placeholder:**
```
{{variableName}}
```

**Nested Object Placeholder (Gelecekte):**
```
{{user.name}}
{{order.total}}
{{product.price}}
```

**Array Iteration (Gelecekte - Advanced):**
```
{{#items}}
  - {{name}}: {{price}}
{{/items}}
```

### Replacement Engine

**Algoritma:**
1. Template'teki tüm `{{variableName}}` pattern'lerini bul
2. `messageObject` içinde ilgili değeri ara
3. Replace et (case-sensitive)
4. Eksik variable varsa hata fırlat

**Örnek:**
```csharp
Template: "Hello {{userName}}, your email is {{userEmail}}"
MessageObject: {
  "userName": "Ahmet",
  "userEmail": "ahmet@example.com"
}
Result: "Hello Ahmet, your email is ahmet@example.com"
```

**Eksik Variable:**
```csharp
Template: "Hello {{userName}}, your email is {{userEmail}}"
MessageObject: {
  "userName": "Ahmet"
  // userEmail eksik!
}
Error: "Missing required variable: userEmail"
```

### Validation

**Template Validation (Create/Update):**
- Placeholder format kontrolü: `{{variableName}}` (regex: `\{\{(\w+)\}\}`)
- Variables listesi ile placeholder'ların eşleşmesi
- Duplicate variable kontrolü

**MessageObject Validation (Send):**
- Required variables kontrolü
- Variable type validation (opsiyonel - gelecekte)
- Null/empty value kontrolü (opsiyonel)

---

## 📊 MONGODB YAPISI

### Collections

**1. `@mail_templates` Collection (MngDataGateway'de):**
```json
{
  "__dataId": "507f1f77bcf86cd799439011",
  "templateId": "welcome-email",
  "name": "Welcome Email Template",
  "subject": "Hoş geldiniz {{userName}}!",
  "body": "<h1>Merhaba {{userName}}</h1>...",
  "variables": ["userName", "userEmail"],
  "isActive": true
}
```

**Not:** Template'ler MngDataGateway'de `@mail_templates` dataset'i içinde saklanır. MngNotifier sadece okur.

**Indexes (MngDataGateway'de tanımlı):**
- `templateId` (unique)
- `isActive` (filtering için)

**2. `@notifications` Collection (MngNotifier'da):**
```json
{
  "_id": ObjectId("..."),
  "notificationId": "507f1f77bcf86cd799439011",
  "type": "email",
  "status": "sent",  // queued, sending, sent, failed
  "to": ["user@example.com"],
  "cc": ["manager@example.com"],
  "subject": "Hoş geldiniz Ahmet Yılmaz!",
  "body": "<h1>Merhaba Ahmet Yılmaz</h1>...",
  "templateId": "welcome-email",  // Optional (template-based ise)
  "messageObject": {  // Optional (template-based ise)
    "userName": "Ahmet Yılmaz",
    "userEmail": "ahmet@example.com"
  },
  "queuedAt": ISODate("2026-01-11T10:00:00Z"),
  "sentAt": ISODate("2026-01-11T10:00:01Z"),
  "failedAt": null,
  "errorMessage": null,
  "retryCount": 0,
  "metadata": {
    "source": "api",  // "api" or "rabbitmq"
    "userId": "user123",
    "domainId": "meral"
  }
}
```

**Indexes:**
- `notificationId` (unique)
- `status` (filtering için)
- `queuedAt` (sorting için)
- `templateId` (template-based query için)

**3. `@notification_status` Collection (MngNotifier'da - Gelecekte - Delivery Tracking):**
```json
{
  "_id": ObjectId("..."),
  "notificationId": "507f1f77bcf86cd799439011",
  "status": "delivered",  // sent, delivered, bounced, failed
  "deliveredAt": ISODate("2026-01-11T10:00:05Z"),
  "bouncedAt": null,
  "bounceReason": null
}
```

---

## 🎯 ÖRNEK SENARYOLAR

### Senaryo 1: Direct API - Domain Oluşturulduğunda Admin Bilgileri

**Senaryo:** Yeni bir domain oluşturulduğunda, henüz token olmadığı için template kullanılamaz. Direkt body gönderilir.

**Request (No Authentication):**
```http
POST /api/v1/notifications/send
Content-Type: application/json

{
  "to": ["admin@example.com"],
  "from": {  // Optional - appsettings'ten default alınır
    "email": "noreply@monitra.local",
    "name": "MonitraNG"
  },
  "subject": "Domain Oluşturuldu - example.com",
  "body": "<h1>Domain Bilgileri</h1><p>Domain: example.com</p><p>Admin Kullanıcı Adı: admin</p><p>Geçici Şifre: TempPass123!</p><p>Lütfen ilk girişte şifrenizi değiştirin.</p>",
  "isHtml": true
}
```

**Response:**
```json
{
  "notificationId": "507f1f77bcf86cd799439011",
  "status": "queued",
  "queuedAt": "2026-01-11T10:00:00Z"
}
```

---

### Senaryo 2: Template-Based Mail (Authenticated)

**Senaryo:** Authenticated kullanıcılar için template kullanarak mail gönderme.

**1. Template Dataset Oluştur (MngDataGateway'de - İlk kurulumda bir kez):**
```http
POST /api/v1/datasets
Content-Type: application/json

{
  "name": "@mail_templates",
  "description": "Mail notification templates",
  "fields": [
    {
      "fieldType": "text",
      "name": "templateId",
      "title": "Template ID",
      "mandatory": true,
      "unique": true
    },
    {
      "fieldType": "text",
      "name": "name",
      "title": "Template Name",
      "mandatory": true
    },
    {
      "fieldType": "text",
      "name": "subject",
      "title": "Email Subject",
      "mandatory": true
    },
    {
      "fieldType": "text",
      "name": "body",
      "title": "Email Body (HTML)",
      "mandatory": true
    },
    {
      "fieldType": "text",
      "name": "variables",
      "title": "Required Variables",
      "isArray": true
    },
    {
      "fieldType": "bool",
      "name": "isActive",
      "title": "Is Active",
      "mandatory": true,
      "defaultValue": true
    }
  ]
}
```

**2. Template Oluştur (MngDataGateway'de):**
```http
POST /api/v1/data/@mail_templates
Content-Type: application/json

{
  "templateId": "order-confirmation",
  "name": "Order Confirmation Email",
  "subject": "Siparişiniz Onaylandı - #{{orderNumber}}",
  "body": "<h1>Sipariş Onayı</h1><p>Sipariş No: {{orderNumber}}</p><p>Toplam: {{total}} TL</p>",
  "variables": ["orderNumber", "total"],
  "isActive": true
}
```

**3. Template ile Mail Gönder (MngNotifier'da - Authentication Required):**
```http
POST /api/v1/notifications/send-template
Authorization: Bearer {jwt_token}
Content-Type: application/json

{
  "to": ["customer@example.com"],
  "from": {  // Optional - appsettings'ten default alınır
    "email": "orders@example.com",
    "name": "Order System"
  },
  "templateId": "order-confirmation",
  "messageObject": {
    "orderNumber": "ORD-12345",
    "total": "150.00"
  }
}
```

**Sonuç:**
- Subject: "Siparişiniz Onaylandı - #ORD-12345"
- Body: "<h1>Sipariş Onayı</h1><p>Sipariş No: ORD-12345</p><p>Toplam: 150.00 TL</p>"

**Not:** Bu endpoint authentication gerektirir çünkü MngDataGateway'den template okumak için token gerekir.

---

### Senaryo 3: RabbitMQ Event

**Event (MngKeeper'dan gönderilir):**
```json
{
  "eventType": "MailNotificationEvent",
  "to": ["newuser@example.com"],
  "subject": "Hesabınız Oluşturuldu",
  "body": "<h1>Hoş geldiniz!</h1><p>Hesabınız başarıyla oluşturuldu.</p>",
  "metadata": {
    "source": "MngKeeper",
    "userId": "user123",
    "domainId": "meral"
  }
}
```

**MngNotifier Consumer:**
1. Event'i yakalar
2. Notification entity oluşturur
3. Queue'ya ekler
4. Background worker mail'i gönderir

---

## 💡 ÖNERİLER VE İYİLEŞTİRMELER

### 1. Template Versioning (Gelecekte)

**Öneri:** Template'lerin versiyonlarını sakla
```json
{
  "templateId": "welcome-email",
  "version": 2,
  "previousVersion": 1,
  "isActive": true
}
```

**Fayda:**
- Template değişikliklerini takip et
- Rollback yapabilme
- A/B testing

---

### 2. Multi-Language Template Support (Gelecekte)

**Öneri:** Template'lerde dil desteği
```json
{
  "templateId": "welcome-email",
  "name": "Welcome Email",
  "languages": {
    "tr": {
      "subject": "Hoş geldiniz {{userName}}!",
      "body": "<h1>Merhaba {{userName}}</h1>..."
    },
    "en": {
      "subject": "Welcome {{userName}}!",
      "body": "<h1>Hello {{userName}}</h1>..."
    }
  }
}
```

**Kullanım:**
```json
{
  "templateId": "welcome-email",
  "language": "tr",  // Optional, default: "tr"
  "messageObject": {...}
}
```

---

### 3. Template Preview (Gelecekte)

**Öneri:** Template'i test etmek için preview endpoint
```http
POST /api/v1/templates/{templateId}/preview
Content-Type: application/json

{
  "messageObject": {
    "userName": "Test User",
    "userEmail": "test@example.com"
  }
}
```

**Response:**
```json
{
  "subject": "Hoş geldiniz Test User!",
  "body": "<h1>Merhaba Test User</h1><p>Email: test@example.com</p>"
}
```

---

### 4. Template Variables Validation (Gelecekte)

**Öneri:** Variable type validation
```json
{
  "templateId": "order-confirmation",
  "variables": [
    {
      "name": "orderNumber",
      "type": "string",
      "required": true
    },
    {
      "name": "total",
      "type": "number",
      "required": true,
      "format": "currency"
    }
  ]
}
```

---

### 5. Batch Mail Sending (Gelecekte)

**Öneri:** Toplu mail gönderme
```http
POST /api/v1/notifications/send-batch
Content-Type: application/json

{
  "recipients": [
    {"to": "user1@example.com", "messageObject": {...}},
    {"to": "user2@example.com", "messageObject": {...}}
  ],
  "templateId": "welcome-email"
}
```

---

## 📝 SONUÇ

Bu tasarım dokümantasyonu, MngNotifier servisinin mail notification özelliklerini detaylı olarak açıklar. İki farklı yöntem (Direct API ve RabbitMQ Event) ve template-based mail gönderme desteği ile esnek ve ölçeklenebilir bir notification sistemi sağlanır.

**Sonraki Adımlar:**
1. Domain entity'lerini oluştur
2. Service interface'lerini tanımla
3. Implementation'ları yap
4. Controller'ları oluştur
5. Test scriptleri yaz

---

**Son Güncelleme:** 11 Ocak 2026
