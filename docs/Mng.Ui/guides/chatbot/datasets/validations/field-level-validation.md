---
title: "Field-Level Validation"
category: "datasets"
tags: ["dataset", "validation", "field-level", "min", "max", "regex", "pattern"]
service: "MngDataGateway"
difficulty: "intermediate"
estimated_time: "7 dakika"
language: "tr"
priority: 1
steps:
  - order: 1
    title: "Field Tanımına Validation Ekle"
    action: "Field definition'da validation object'ini ekleyin"
    expected_result: "Validation rules tanımlanır"
  - order: 2
    title: "Field Type'a Göre Kurallar Seç"
    action: "Field type'a uygun validation rules ekleyin"
    expected_result: "Validation rules yapılandırılır"
  - order: 3
    title: "Test Et"
    action: "Veri eklerken validation'ları test edin"
    expected_result: "Validation'lar çalışır"
---

# Field-Level Validation

## Özet
Field-level validation, her field için ayrı ayrı validation kuralları tanımlamanıza olanak sağlar. Field type'a göre farklı validation kuralları desteklenir.

## Özellikler
- ✅ Field type'a göre özel kurallar
- ✅ min/max (number)
- ✅ minLength/maxLength (text)
- ✅ pattern (regex) (text)
- ✅ minDate/maxDate (datetime)
- ✅ minItems/maxItems (array)
- ✅ Custom error messages

## Validation Rules Yapısı

```json
{
  "fieldType": "text",
  "name": "email",
  "validation": {
    "minLength": 5,
    "maxLength": 100,
    "pattern": "^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\\.[a-zA-Z]{2,}$",
    "message": "Geçerli bir e-posta adresi giriniz"
  }
}
```

## Field Type'a Göre Validation Rules

### Text Fields

**Desteklenen Kurallar:**
- `minLength` - Minimum karakter sayısı
- `maxLength` - Maksimum karakter sayısı
- `pattern` - Regex pattern
- `message` - Özel hata mesajı

**Örnek:**
```json
{
  "fieldType": "text",
  "name": "title",
  "title": "Başlık",
  "validation": {
    "minLength": 3,
    "maxLength": 100,
    "pattern": "^[A-Z]",
    "message": "Başlık büyük harfle başlamalı ve 3-100 karakter arası olmalıdır"
  }
}
```

**Kullanım Senaryoları:**
- E-posta formatı kontrolü
- Telefon numarası formatı
- URL formatı
- Özel kod formatı (örn: ISBN)

### Number Fields

**Desteklenen Kurallar:**
- `min` - Minimum değer
- `max` - Maksimum değer
- `message` - Özel hata mesajı

**Örnek:**
```json
{
  "fieldType": "number",
  "name": "pageCount",
  "title": "Sayfa Sayısı",
  "validation": {
    "min": 1,
    "max": 10000,
    "message": "Sayfa sayısı 1 ile 10000 arasında olmalıdır"
  }
}
```

**Kullanım Senaryoları:**
- Yaş kontrolü (min: 0, max: 150)
- Fiyat kontrolü (min: 0)
- Yüzde kontrolü (min: 0, max: 100)
- Stok miktarı (min: 0)

### DateTime Fields

**Desteklenen Kurallar:**
- `minDate` - Minimum tarih (ISO 8601)
- `maxDate` - Maksimum tarih (ISO 8601)
- `message` - Özel hata mesajı

**Örnek:**
```json
{
  "fieldType": "datetime",
  "name": "publicationDate",
  "title": "Yayın Tarihi",
  "validation": {
    "minDate": "1900-01-01T00:00:00Z",
    "maxDate": "2100-12-31T23:59:59Z",
    "message": "Yayın tarihi 1900 ile 2100 arasında olmalıdır"
  }
}
```

**Kullanım Senaryoları:**
- Doğum tarihi kontrolü (maxDate: bugün)
- Proje başlangıç/bitiş tarihi
- Geçmiş tarih kontrolü

### Array Fields

**Desteklenen Kurallar:**
- `minItems` - Minimum item sayısı
- `maxItems` - Maksimum item sayısı
- `message` - Özel hata mesajı

**Örnek:**
```json
{
  "fieldType": "relation",
  "name": "genres",
  "title": "Türler",
  "isArray": true,
  "validation": {
    "minItems": 1,
    "maxItems": 5,
    "message": "En az 1, en fazla 5 tür seçilmelidir"
  }
}
```

**Kullanım Senaryoları:**
- En az bir kategori seçimi
- Maksimum tag sayısı
- Çoklu seçim limitleri

## Pratik Örnekler

### Örnek 1: E-posta Validasyonu
```json
{
  "fieldType": "text",
  "name": "email",
  "title": "E-posta",
  "mandatory": true,
  "validation": {
    "minLength": 5,
    "maxLength": 255,
    "pattern": "^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\\.[a-zA-Z]{2,}$",
    "message": "Geçerli bir e-posta adresi giriniz"
  }
}
```

**Geçerli Değerler:**
- `user@example.com` ✅
- `test.email@domain.co.uk` ✅

**Geçersiz Değerler:**
- `invalid-email` ❌ (pattern uymuyor)
- `@example.com` ❌ (pattern uymuyor)
- `user@` ❌ (pattern uymuyor)

### Örnek 2: Telefon Numarası Validasyonu
```json
{
  "fieldType": "text",
  "name": "phoneNumber",
  "title": "Telefon Numarası",
  "validation": {
    "pattern": "^\\+?[1-9]\\d{1,14}$",
    "message": "Geçerli bir telefon numarası giriniz (örn: +905551234567)"
  }
}
```

**Geçerli Değerler:**
- `+905551234567` ✅
- `905551234567` ✅

**Geçersiz Değerler:**
- `05551234567` ❌ (başında + veya 9 yok)
- `123` ❌ (çok kısa)

### Örnek 3: ISBN Validasyonu
```json
{
  "fieldType": "text",
  "name": "isbn",
  "title": "ISBN",
  "validation": {
    "pattern": "^(?:ISBN(?:-1[03])?:? )?(?=[0-9X]{10}$|(?=(?:[0-9]+[- ]){3})[- 0-9X]{13}$|97[89][0-9]{10}$|(?=(?:[0-9]+[- ]){4})[- 0-9]{17}$)(?:97[89][- ]?)?[0-9]{1,5}[- ]?[0-9]+[- ]?[0-9]+[- ]?[0-9X]$",
    "message": "Geçerli bir ISBN formatı giriniz"
  }
}
```

### Örnek 4: Yaş Validasyonu
```json
{
  "fieldType": "number",
  "name": "age",
  "title": "Yaş",
  "validation": {
    "min": 0,
    "max": 150,
    "message": "Yaş 0 ile 150 arasında olmalıdır"
  }
}
```

### Örnek 5: Fiyat Validasyonu
```json
{
  "fieldType": "number",
  "name": "price",
  "title": "Fiyat",
  "validation": {
    "min": 0,
    "message": "Fiyat 0'dan küçük olamaz"
  }
}
```

### Örnek 6: Tarih Aralığı Validasyonu
```json
{
  "fieldType": "datetime",
  "name": "birthDate",
  "title": "Doğum Tarihi",
  "validation": {
    "minDate": "1900-01-01T00:00:00Z",
    "maxDate": "2025-01-01T00:00:00Z",
    "message": "Doğum tarihi 1900 ile 2025 arasında olmalıdır"
  }
}
```

### Örnek 7: Çoklu Kategori Seçimi
```json
{
  "fieldType": "relation",
  "name": "categories",
  "title": "Kategoriler",
  "isArray": true,
  "validation": {
    "minItems": 1,
    "maxItems": 3,
    "message": "En az 1, en fazla 3 kategori seçilmelidir"
  }
}
```

## Regex Pattern Örnekleri

### E-posta
```regex
^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$
```

### Telefon (Uluslararası)
```regex
^\+?[1-9]\d{1,14}$
```

### URL
```regex
^https?://(www\.)?[-a-zA-Z0-9@:%._\+~#=]{1,256}\.[a-zA-Z0-9()]{1,6}\b([-a-zA-Z0-9()@:%_\+.~#?&//=]*)$
```

### Türkçe Karakterler
```regex
^[a-zA-ZçğıöşüÇĞIİÖŞÜ\s]+$
```

### Alphanumeric + Underscore
```regex
^[a-zA-Z0-9_]+$
```

### Posta Kodu (5 haneli)
```regex
^\d{5}$
```

## Custom Error Messages

**Varsayılan Mesajlar:**
- min/max: "Değer {min} ile {max} arasında olmalıdır"
- minLength/maxLength: "Uzunluk {minLength} ile {maxLength} arasında olmalıdır"
- pattern: "Format geçersiz"

**Özel Mesaj:**
```json
{
  "validation": {
    "min": 0,
    "max": 100,
    "message": "Yüzde değeri 0 ile 100 arasında olmalıdır"
  }
}
```

## Validation Execution

**Ne Zaman Çalışır:**
- ✅ Data create (POST)
- ✅ Data update (PUT)
- ✅ Field-level validation önce çalışır (hızlı)
- ✅ Expression-based validation sonra çalışır (karmaşık)

**Hata Formatı:**
```json
{
  "isValid": false,
  "errors": [
    {
      "field": "email",
      "code": "VALIDATION_PATTERN",
      "message": "Geçerli bir e-posta adresi giriniz"
    }
  ]
}
```

## Sık Sorulan Sorular

**S: Regex pattern'de escape karakterleri nasıl kullanılır?**  
C: JSON'da backslash escape edilmelidir: `\\d` → `\d`, `\\+` → `\+`

**S: minDate ve maxDate formatı nedir?**  
C: ISO 8601 formatı: `"2025-01-01T00:00:00Z"` veya `"2025-01-01"`

**S: Validation mesajları çoklu dil destekliyor mu?**  
C: Şu anda tek dil. Gelecekte i18n desteği eklenebilir.

**S: Field-level validation ile expression-based validation arasındaki fark nedir?**  
C: Field-level: Tek field için basit kurallar (min/max, regex). Expression-based: Birden fazla field arası karmaşık kurallar (örn: `endDate > startDate`).

**S: Validation'ları devre dışı bırakabilir miyim?**  
C: Hayır, validation'lar her zaman çalışır. Ancak `mandatory: false` yaparak zorunluluğu kaldırabilirsiniz.

## İlgili Linkler
- [Expression-Based Validation](../validations/expression-validation.md)
- [Dataset Oluşturma](../creating-dataset.md)
- [Field Types](../field-types/)

---

**Son Güncelleme:** 16 Ocak 2026
