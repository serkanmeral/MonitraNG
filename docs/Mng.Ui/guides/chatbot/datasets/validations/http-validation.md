---
title: "HTTP-Based Validation"
category: "datasets"
tags: ["dataset", "validation", "http", "external", "api"]
service: "MngDataGateway"
difficulty: "advanced"
estimated_time: "10 dakika"
language: "tr"
priority: 1
---

# HTTP-Based Validation

## Özet
HTTP-based validation, external API endpoint'leri ile custom validation logic çalıştırmanıza olanak sağlar. Karmaşık business rule'ları için kullanılır.

## Özellikler
- ✅ External API validation
- ✅ Custom validation logic
- ✅ Request/Response format
- ✅ Error handling
- ✅ Authorization header (otomatik)

## Validation Definition Yapısı

```json
{
  "validations": [
    {
      "name": "validateEmail",
      "description": "E-posta adresini external API ile doğrula",
      "type": "http",
      "url": "https://api.example.com/validate/email",
      "method": "POST",
      "fields": ["email"],
      "when": "both",
      "order": 0
    }
  ]
}
```

## Request Format

### POST Method (Varsayılan)

**Request Body:**
```json
{
  "email": "user@example.com",
  "domain": "seven"
}
```

**Headers:**
```
Authorization: Bearer {token}
Content-Type: application/json
```

### GET Method

**Query Parameters:**
```
GET https://api.example.com/validate/email?email=user@example.com&domain=seven
```

**Headers:**
```
Authorization: Bearer {token}
```

## Response Format

### Başarılı Validation

**Response:**
```json
{
  "isValid": true
}
```

**Status Code:** `200 OK`

### Başarısız Validation

**Response:**
```json
{
  "isValid": false,
  "errorMessage": "E-posta adresi geçersiz"
}
```

**Status Code:** `200 OK` (isValid: false) veya `400 Bad Request`

## Pratik Örnekler

### Örnek 1: E-posta Validasyonu
**Amaç:** E-posta adresini external API ile doğrula

**Validation:**
```json
{
  "name": "validateEmail",
  "description": "E-posta adresini external API ile doğrula",
  "type": "http",
  "url": "https://api.emailvalidator.com/validate",
  "method": "POST",
  "fields": ["email"],
  "when": "both",
  "order": 0
}
```

**Request:**
```json
POST https://api.emailvalidator.com/validate
{
  "email": "user@example.com"
}
```

**Response (Geçerli):**
```json
{
  "isValid": true
}
```

**Response (Geçersiz):**
```json
{
  "isValid": false,
  "errorMessage": "E-posta adresi geçersiz"
}
```

### Örnek 2: Telefon Numarası Validasyonu
**Amaç:** Telefon numarasını external API ile doğrula

**Validation:**
```json
{
  "name": "validatePhone",
  "type": "http",
  "url": "https://api.phonevalidator.com/validate",
  "method": "POST",
  "fields": ["phoneNumber", "countryCode"],
  "when": "both",
  "order": 0
}
```

**Request:**
```json
POST https://api.phonevalidator.com/validate
{
  "phoneNumber": "+905551234567",
  "countryCode": "TR"
}
```

### Örnek 3: Custom Business Rule
**Amaç:** Karmaşık business rule validation

**Validation:**
```json
{
  "name": "validateBusinessRule",
  "type": "http",
  "url": "https://api.example.com/validate/business-rule",
  "method": "POST",
  "fields": ["field1", "field2", "field3"],
  "when": "create",
  "order": 1
}
```

## When Parametresi

**Değerler:**
- `"create"` - Sadece create işleminde çalışır
- `"update"` - Sadece update işleminde çalışır
- `"both"` - Her iki işlemde de çalışır (varsayılan)

**Örnek:**
```json
{
  "name": "validateOnCreate",
  "type": "http",
  "url": "https://api.example.com/validate",
  "when": "create"  // Sadece yeni kayıt oluşturulurken
}
```

## Order Parametresi

**Amaç:** Validation'ların çalışma sırası

**Kural:** Düşük sayı önce çalışır (0, 1, 2, ...)

**Örnek:**
```json
{
  "validations": [
    {
      "name": "validateEmail",
      "type": "http",
      "url": "https://api.example.com/validate/email",
      "order": 0  // İlk çalışır
    },
    {
      "name": "validateBusinessRule",
      "type": "http",
      "url": "https://api.example.com/validate/business-rule",
      "order": 1  // Sonra çalışır
    }
  ]
}
```

## Error Handling

### HTTP Error (4xx, 5xx)
**Davranış:** Validation başarısız sayılır

**Response:**
```json
{
  "isValid": false,
  "errors": [
    {
      "field": null,
      "code": "VALIDATION_HTTP_ERROR",
      "message": "HTTP validation failed: 500 Internal Server Error"
    }
  ]
}
```

### Timeout
**Davranış:** Validation başarısız sayılır

**Response:**
```json
{
  "isValid": false,
  "errors": [
    {
      "field": null,
      "code": "VALIDATION_HTTP_TIMEOUT",
      "message": "HTTP validation timeout: Request exceeded 30 seconds"
    }
  ]
}
```

## Authorization

**Otomatik:** JWT token otomatik olarak Authorization header'ına eklenir

**Format:**
```
Authorization: Bearer {access_token}
```

**Not:** Validation endpoint'i token'ı validate etmeli ve kullanıcı bilgilerini kullanabilir.

## Best Practices

### 1. Response Format
**Öneri:** Standart response format kullanın:
```json
{
  "isValid": true/false,
  "errorMessage": "..." // Opsiyonel
}
```

### 2. Error Handling
**Öneri:** Validation endpoint'leri hata durumlarında da `200 OK` döndürmeli, `isValid: false` ile sonucu belirtmeli.

### 3. Performance
**Öneri:** Validation endpoint'leri hızlı olmalı (< 1 saniye). Timeout: 30 saniye.

### 4. Security
**Öneri:** Validation endpoint'leri token'ı validate etmeli ve yetkilendirme kontrolü yapmalı.

## Sık Sorulan Sorular

**S: HTTP validation endpoint'i hangi format'ta response döndürmeli?**  
C: `{ "isValid": true/false, "errorMessage": "..." }` formatında döndürmelidir.

**S: Validation endpoint'i hata döndürürse ne olur?**  
C: Validation başarısız sayılır ve hata mesajı kullanıcıya gösterilir.

**S: Timeout süresi ne kadar?**  
C: Varsayılan 30 saniye. Gelecekte yapılandırılabilir hale getirilebilir.

**S: Birden fazla field gönderebilir miyim?**  
C: Evet, `fields` array'inde birden fazla field belirtebilirsiniz.

**S: Validation endpoint'i hangi HTTP method'u kullanmalı?**  
C: Genellikle POST kullanılır (request body ile). GET de desteklenir (query parameters).

## İlgili Linkler
- [Field-Level Validation](../validations/field-level-validation.md)
- [Expression-Based Validation](../validations/expression-validation.md)
- [Dataset Oluşturma](../creating-dataset.md)

---

**Son Güncelleme:** 16 Ocak 2026
