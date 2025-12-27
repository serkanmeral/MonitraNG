# HTTP Validation Documentation

**Date:** 26 Aralık 2025  
**Status:** ✅ COMPLETED & TESTED

---

## 📋 Overview

HTTP Validation, dataset validations array'ine eklenen `type: "http"` validasyonları ile dış bir HTTP endpoint'e validation isteği göndererek data validation yapma özelliğidir. Bu özellik, Node-RED gibi external servislerle entegre validation işlemleri yapmayı mümkün kılar.

---

## 🎯 Features

- ✅ External HTTP endpoint'e POST request gönderme
- ✅ Nesnenin tamamını body olarak gönderme
- ✅ Authorization header'ı otomatik forward etme
- ✅ `when` kontrolü (create, update, both)
- ✅ `order` sıralaması (validation execution order)
- ✅ Timeout yönetimi (configurable)
- ✅ Error handling (network errors, timeouts - safe default)
- ✅ Response format: `{ "isValid": true/false, "errorMessage": "..." }`

---

## 📝 Schema Definition

HTTP validation, dataset schema'nın `validations` array'ine aşağıdaki formatta eklenir:

```json
{
  "name": "external_validation",
  "description": "Node-RED flow ile HTTP validation testi",
  "type": "http",
  "url": "http://localhost:1880/dg_validasyontest",
  "method": "POST",
  "when": "both",
  "order": 2
}
```

### Field Definitions

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `name` | string | ✅ | Validation adı (unique identifier) |
| `description` | string | ❌ | Validation açıklaması |
| `type` | string | ✅ | `"http"` olmalı |
| `url` | string | ✅ | HTTP endpoint URL'i |
| `method` | string | ❌ | HTTP method (`POST`, `GET`). Default: `POST` |
| `when` | string | ❌ | Ne zaman çalışacak: `"create"`, `"update"`, `"both"`. Default: `"both"` |
| `order` | number | ❌ | Execution order (küçük sayı önce çalışır). Default: `0` |

---

## 🔄 Request/Response Format

### Request

**Method:** POST (default)

**URL:** Validation definition'daki `url` field'ı

**Headers:**
```
Authorization: Bearer {token}
Content-Type: application/json
```

**Body:**
```json
{
  "title": "Test Book",
  "publisher": "publisher-id",
  "author": "author-id",
  "price": 75,
  "pageCount": 100,
  ...
}
```

**Not:** Body'de nesnenin tamamı gönderilir, field seçimi yapılmaz.

### Response

**Success Response (200 OK):**
```json
{
  "isValid": true
}
```

**Validation Failed (200 OK):**
```json
{
  "isValid": false,
  "errorMessage": "Price must be greater than 50"
}
```

**Note:** 
- 200 OK dışındaki status code'lar validation geçerli sayılır (safe default)
- Network error veya timeout durumlarında validation geçerli sayılır (safe default)

---

## ⚙️ Configuration

### Timeout Setting

`appsettings.json` içerisinde timeout ayarlanabilir:

```json
{
  "MngDataGatewaySettings": {
    "Validation": {
      "HttpValidationTimeout": 30
    }
  }
}
```

**Default:** 30 saniye

---

## 🧪 Example: Node-RED Flow

### Flow Configuration

**Endpoint:** `http://localhost:1880/dg_validasyontest`

**Flow Logic:**
```javascript
// msg.payload içinde data nesnesi gelir
const price = msg.payload.price;

if (price > 50) {
    return {
        isValid: true
    };
} else {
    return {
        isValid: false,
        errorMessage: `Price must be greater than 50. Current value: ${price}`
    };
}
```

---

## 📊 Validation Execution Flow

```
1. Field-level validations (min, max, pattern, etc.)
   ↓
2. Unique constraints (MongoDB check)
   ↓
3. Expression-based validations (in-memory evaluation)
   ↓
4. HTTP validations (external endpoint calls)
   └─> Filtered by "when" (create/update/both)
   └─> Sorted by "order"
   └─> Executed sequentially
```

**Important:** HTTP validations, diğer validation'ların başarılı olması durumunda çalışır. Önceki validations başarısız olursa HTTP validation'a geçilmez.

---

## 🚀 MongoDB Dataset Update Example

### Adding HTTP Validation to Dataset

```javascript
// MongoDB'de dataset'i güncelleme
db.datasets.updateOne(
  { name: "tst_books" },
  {
    $push: {
      validations: {
        name: "external_price_validation",
        description: "Node-RED flow ile price validation",
        type: "http",
        url: "http://localhost:1880/dg_validasyontest",
        method: "POST",
        when: "both",
        order: 2
      }
    }
  }
)
```

### Removing HTTP Validation

```javascript
db.datasets.updateOne(
  { name: "tst_books" },
  {
    $pull: {
      validations: {
        type: "http",
        name: "external_price_validation"
      }
    }
  }
)
```

---

## ✅ Test Results

**Test Script:** `scripts/tests/MngDataGateway/validation/test-validations.ps1`

**Test Cases:**
1. ✅ `price = 50` → Validation failed (expected)
2. ✅ `price = 75 > 50` → Validation passed
3. ✅ `price = 49 < 50` → Validation failed (expected)
4. ✅ `price = 0 < 50` → Validation failed (expected)
5. ✅ `price = 25 < 50` → Validation failed (expected)

**Test Results:** 5/5 HTTP validation tests passed ✅

---

## 🔒 Security Considerations

1. **Authorization Header:** Mevcut request'in `Authorization` header'ı aynen forward edilir. External endpoint'in bu token'ı validate etmesi gerekir.

2. **Timeout:** Network timeout'ları safe default olarak validation geçerli sayılır. Bu nedenle critical validations için timeout değerini dikkatli ayarlayın.

3. **Error Handling:** Network errors ve timeouts safe default olarak validation geçerli sayılır. Bu nedenle critical validations için external endpoint'in her zaman available olması gerekir.

4. **URL Validation:** URL validation yapılmaz. Güvenli endpoint'ler kullanın.

---

## 📚 Related Documentation

- [Validation Service Implementation](../architecture/ARCHITECTURE_GUIDE.md#validation-service)
- [Dataset Schema Summary](./DATASET_SCHEMA_SUMMARY.md)
- [Expression-Based Validation](./DATASET_SCHEMA_SUMMARY.md#expression-based-validation)

---

**Last Updated:** 26 Aralık 2025  
**Maintainer:** MngDataGateway Team

