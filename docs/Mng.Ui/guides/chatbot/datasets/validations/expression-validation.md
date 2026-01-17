---
title: "Expression-Based Validation"
category: "datasets"
tags: ["dataset", "validation", "expression", "cross-field", "complex"]
service: "MngDataGateway"
difficulty: "advanced"
estimated_time: "10 dakika"
language: "tr"
priority: 1
steps:
  - order: 1
    title: "Validation Definition Oluştur"
    action: "Dataset schema'da validations array'ine yeni validation ekleyin"
    expected_result: "Validation definition oluşturulur"
  - order: 2
    title: "Expression Yaz"
    action: "type: 'expression' seçin ve expression yazın"
    expected_result: "Expression tanımlanır"
  - order: 3
    title: "When ve Order Ayarla"
    action: "when (create/update/both) ve order değerlerini ayarlayın"
    expected_result: "Validation yapılandırılır"
---

# Expression-Based Validation

## Özet
Expression-based validation, birden fazla field arası karmaşık validation kuralları tanımlamanıza olanak sağlar. JavaScript benzeri expression syntax kullanarak field'lar arası ilişkileri kontrol edebilirsiniz.

## Özellikler
- ✅ Cross-field validation (birden fazla field kontrolü)
- ✅ Aritmetik işlemler (+, -, *, /, %)
- ✅ Karşılaştırma operatörleri (>, <, >=, <=, ==, !=)
- ✅ Mantıksal operatörler (&&, ||)
- ✅ Field referansları (field adları doğrudan kullanılır)

## Validation Definition Yapısı

```json
{
  "validations": [
    {
      "name": "endDateAfterStartDate",
      "description": "Bitiş tarihi başlangıç tarihinden sonra olmalıdır",
      "type": "expression",
      "expression": "endDate > startDate",
      "when": "both",
      "order": 0
    }
  ]
}
```

## Expression Syntax

### Field Referansları
Field adları doğrudan kullanılır:
```javascript
endDate > startDate
price * quantity == total
age >= 18
```

### Aritmetik İşlemler
```javascript
price / pageCount <= 10
total == (price * quantity)
discount == (originalPrice - finalPrice)
```

### Karşılaştırma Operatörleri
- `>` - Büyüktür
- `<` - Küçüktür
- `>=` - Büyük eşittir
- `<=` - Küçük eşittir
- `==` - Eşittir
- `!=` - Eşit değildir

### Mantıksal Operatörler
```javascript
(age >= 18) && (age <= 65)
(status == "active") || (status == "pending")
```

## Pratik Örnekler

### Örnek 1: Tarih Karşılaştırması
**Amaç:** Bitiş tarihi başlangıç tarihinden sonra olmalı

**Validation:**
```json
{
  "name": "endDateAfterStartDate",
  "description": "Bitiş tarihi başlangıç tarihinden sonra olmalıdır",
  "type": "expression",
  "expression": "endDate > startDate",
  "when": "both",
  "order": 0
}
```

**Geçerli:**
```json
{
  "startDate": "2025-01-01T00:00:00Z",
  "endDate": "2025-12-31T23:59:59Z"
}
```

**Geçersiz:**
```json
{
  "startDate": "2025-12-31T23:59:59Z",
  "endDate": "2025-01-01T00:00:00Z"  // ❌ Bitiş tarihi başlangıçtan önce
}
```

### Örnek 2: Fiyat Hesaplama
**Amaç:** Toplam fiyat = Birim fiyat × Miktar

**Validation:**
```json
{
  "name": "totalPriceCalculation",
  "description": "Toplam fiyat birim fiyat ile miktarın çarpımına eşit olmalıdır",
  "type": "expression",
  "expression": "totalPrice == (unitPrice * quantity)",
  "when": "both",
  "order": 0
}
```

**Geçerli:**
```json
{
  "unitPrice": 100,
  "quantity": 5,
  "totalPrice": 500
}
```

**Geçersiz:**
```json
{
  "unitPrice": 100,
  "quantity": 5,
  "totalPrice": 400  // ❌ 100 * 5 = 500 olmalı
}
```

### Örnek 3: Sayfa Başına Fiyat
**Amaç:** Sayfa başına fiyat 10'dan küçük veya eşit olmalı

**Validation:**
```json
{
  "name": "pricePerPageLimit",
  "description": "Sayfa başına fiyat 10'dan fazla olamaz",
  "type": "expression",
  "expression": "price / pageCount <= 10",
  "when": "both",
  "order": 0
}
```

**Geçerli:**
```json
{
  "price": 500,
  "pageCount": 100  // 500 / 100 = 5 <= 10 ✅
}
```

**Geçersiz:**
```json
{
  "price": 1500,
  "pageCount": 100  // 1500 / 100 = 15 > 10 ❌
}
```

### Örnek 4: Yaş Kontrolü
**Amaç:** Yaş 18 ile 65 arasında olmalı

**Validation:**
```json
{
  "name": "ageRange",
  "description": "Yaş 18 ile 65 arasında olmalıdır",
  "type": "expression",
  "expression": "(age >= 18) && (age <= 65)",
  "when": "both",
  "order": 0
}
```

### Örnek 5: İndirim Kontrolü
**Amaç:** İndirim oranı 0 ile 100 arasında olmalı

**Validation:**
```json
{
  "name": "discountRange",
  "description": "İndirim oranı 0 ile 100 arasında olmalıdır",
  "type": "expression",
  "expression": "(discountPercentage >= 0) && (discountPercentage <= 100)",
  "when": "both",
  "order": 0
}
```

### Örnek 6: Stok Kontrolü
**Amaç:** Satış miktarı stok miktarından fazla olamaz

**Validation:**
```json
{
  "name": "stockCheck",
  "description": "Satış miktarı stok miktarından fazla olamaz",
  "type": "expression",
  "expression": "saleQuantity <= stockQuantity",
  "when": "both",
  "order": 0
}
```

### Örnek 7: Durum Kontrolü
**Amaç:** Sadece aktif veya beklemede olan kayıtlar geçerli

**Validation:**
```json
{
  "name": "statusCheck",
  "description": "Durum aktif veya beklemede olmalıdır",
  "type": "expression",
  "expression": "(status == \"active\") || (status == \"pending\")",
  "when": "both",
  "order": 0
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
  "name": "initialStatus",
  "type": "expression",
  "expression": "status == \"draft\"",
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
      "name": "dateRange",
      "expression": "endDate > startDate",
      "order": 0  // İlk çalışır
    },
    {
      "name": "priceCalculation",
      "expression": "total == (price * quantity)",
      "order": 1  // Sonra çalışır
    }
  ]
}
```

## Hata Mesajları

**Varsayılan Format:**
```json
{
  "isValid": false,
  "errors": [
    {
      "field": null,
      "code": "VALIDATION_EXPRESSION",
      "message": "Validation 'endDateAfterStartDate' failed: endDate > startDate"
    }
  ]
}
```

**Custom Message (Gelecekte):**
```json
{
  "name": "endDateAfterStartDate",
  "expression": "endDate > startDate",
  "errorMessage": "Bitiş tarihi başlangıç tarihinden sonra olmalıdır"
}
```

## Expression Best Practices

### 1. Field Adları
- Field adlarını doğrudan kullanın (tırnak içine almayın)
- Case-sensitive (büyük/küçük harf duyarlı)

### 2. String Karşılaştırmaları
```javascript
status == "active"  // ✅ Doğru
status == 'active'  // ✅ Doğru (tek tırnak da çalışır)
status == active    // ❌ Yanlış (field adı olarak algılanır)
```

### 3. Number Karşılaştırmaları
```javascript
age >= 18           // ✅ Doğru
price > 0           // ✅ Doğru
```

### 4. Tarih Karşılaştırmaları
```javascript
endDate > startDate  // ✅ ISO 8601 tarihleri karşılaştırılır
```

### 5. Null Kontrolü
```javascript
fieldName != null    // Null kontrolü
fieldName == null    // Null eşitliği
```

## Sık Sorulan Sorular

**S: Expression'da hangi operatörler destekleniyor?**  
C: Aritmetik (+, -, *, /, %), karşılaştırma (>, <, >=, <=, ==, !=), mantıksal (&&, ||)

**S: Expression'da fonksiyon kullanabilir miyim?**  
C: Şu anda sadece operatörler destekleniyor. Fonksiyon desteği gelecekte eklenebilir.

**S: Nested field'lara erişebilir miyim?**  
C: Şu anda sadece top-level field'lar destekleniyor. Nested object field'lar için gelecekte destek eklenebilir.

**S: Array field'ları expression'da kullanabilir miyim?**  
C: Şu anda desteklenmiyor. Gelecekte array operasyonları eklenebilir.

**S: Expression'da hata alırsam ne yapmalıyım?**  
C: Expression syntax'ını kontrol edin. Field adlarının doğru olduğundan, operatörlerin doğru kullanıldığından emin olun.

**S: Birden fazla validation'ın sırası önemli mi?**  
C: Evet, `order` parametresi ile sırayı belirleyebilirsiniz. Önce çalışması gereken validation'lar düşük order değeri almalı.

## İlgili Linkler
- [Field-Level Validation](../validations/field-level-validation.md)
- [HTTP-Based Validation](../validations/http-validation.md)
- [Dataset Oluşturma](../creating-dataset.md)

---

**Son Güncelleme:** 16 Ocak 2026
