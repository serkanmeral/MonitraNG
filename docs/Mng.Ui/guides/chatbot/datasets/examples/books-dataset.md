---
title: "Books Dataset - Tam Örnek"
category: "datasets"
tags: ["dataset", "example", "books", "complete", "tutorial"]
service: "MngDataGateway"
difficulty: "intermediate"
estimated_time: "15 dakika"
language: "tr"
priority: 1
---

# Books Dataset - Tam Örnek

## Özet
Bu rehber, Books dataset'inin tam bir örneğini içerir. Tüm field types, validations, indexes ve relations örnekleriyle gösterilir.

## Dataset Schema

### Tam Schema Tanımı

```json
{
  "name": "@books",
  "description": "Kitap yönetim dataset'i - Tüm field types, validations ve indexes örnekleri",
  "category": "library",
  "forceSchema": true,
  "logging": "self",
  "fields": [
    {
      "fieldType": "text",
      "name": "title",
      "title": "Kitap Başlığı",
      "mandatory": true,
      "validation": {
        "minLength": 3,
        "maxLength": 200
      }
    },
    {
      "fieldType": "text",
      "name": "isbn",
      "title": "ISBN",
      "mandatory": true,
      "unique": true,
      "validation": {
        "pattern": "^(?:ISBN(?:-1[03])?:? )?(?=[0-9X]{10}$|(?=(?:[0-9]+[- ]){3})[- 0-9X]{13}$|97[89][0-9]{10}$|(?=(?:[0-9]+[- ]){4})[- 0-9]{17}$)(?:97[89][- ]?)?[0-9]{1,5}[- ]?[0-9]+[- ]?[0-9]+[- ]?[0-9X]$"
      }
    },
    {
      "fieldType": "number",
      "name": "pageCount",
      "title": "Sayfa Sayısı",
      "mandatory": false,
      "validation": {
        "min": 1,
        "max": 10000
      }
    },
    {
      "fieldType": "number",
      "name": "price",
      "title": "Fiyat",
      "mandatory": false,
      "validation": {
        "min": 0
      }
    },
    {
      "fieldType": "bool",
      "name": "isAvailable",
      "title": "Mevcut mu?",
      "mandatory": false
    },
    {
      "fieldType": "datetime",
      "name": "publicationDate",
      "title": "Yayın Tarihi",
      "mandatory": false,
      "validation": {
        "minDate": "1900-01-01T00:00:00Z",
        "maxDate": "2100-12-31T23:59:59Z"
      }
    },
    {
      "fieldType": "relation",
      "name": "publisher",
      "title": "Yayıncı",
      "relationDataset": "@publishers",
      "mandatory": true,
      "isArray": false
    },
    {
      "fieldType": "relation",
      "name": "genres",
      "title": "Türler",
      "relationDataset": "@genres",
      "mandatory": false,
      "isArray": true,
      "validation": {
        "minItems": 1,
        "maxItems": 5
      }
    },
    {
      "fieldType": "object",
      "name": "metadata",
      "title": "Metadata",
      "mandatory": false
    }
  ],
  "validations": [
    {
      "name": "pricePerPageLimit",
      "description": "Sayfa başına fiyat 10'dan fazla olamaz",
      "type": "expression",
      "expression": "price / pageCount <= 10",
      "when": "both",
      "order": 0
    }
  ],
  "indexList": [
    {
      "name": "idx_isbn",
      "fields": {
        "isbn": 1
      },
      "unique": true
    },
    {
      "name": "idx_title",
      "fields": {
        "title": 1
      },
      "unique": false
    },
    {
      "name": "idx_publicationDate",
      "fields": {
        "publicationDate": -1
      },
      "unique": false
    },
    {
      "name": "idx_publisher_publicationDate",
      "fields": {
        "publisher": 1,
        "publicationDate": -1
      },
      "unique": false
    }
  ]
}
```

## Field Açıklamaları

### 1. title (text)
- **Amaç:** Kitap başlığı
- **Validation:** 3-200 karakter arası
- **Index:** Non-unique ascending

### 2. isbn (text)
- **Amaç:** ISBN numarası
- **Validation:** ISBN format pattern
- **Index:** Unique ascending

### 3. pageCount (number)
- **Amaç:** Sayfa sayısı
- **Validation:** 1-10000 arası

### 4. price (number)
- **Amaç:** Kitap fiyatı
- **Validation:** 0'dan büyük veya eşit

### 5. isAvailable (bool)
- **Amaç:** Kitabın mevcut olup olmadığı

### 6. publicationDate (datetime)
- **Amaç:** Yayın tarihi
- **Validation:** 1900-2100 arası
- **Index:** Non-unique descending

### 7. publisher (relation)
- **Amaç:** Yayıncı referansı
- **Type:** Single reference
- **Target:** @publishers dataset

### 8. genres (relation)
- **Amaç:** Kitap türleri
- **Type:** Array reference
- **Target:** @genres dataset
- **Validation:** 1-5 arası tür

### 9. metadata (object)
- **Amaç:** Ek metadata bilgileri
- **Type:** Free-form JSON object

## Validation Örnekleri

### Expression-Based Validation
```json
{
  "name": "pricePerPageLimit",
  "type": "expression",
  "expression": "price / pageCount <= 10"
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

## Index Açıklamaları

### 1. idx_isbn (Unique)
- **Amaç:** ISBN ile hızlı arama
- **Unique:** Evet
- **Fields:** isbn (ascending)

### 2. idx_title (Non-Unique)
- **Amaç:** Başlık ile arama ve sıralama
- **Unique:** Hayır
- **Fields:** title (ascending)

### 3. idx_publicationDate (Descending)
- **Amaç:** Yeni yayınlar önce
- **Unique:** Hayır
- **Fields:** publicationDate (descending)

### 4. idx_publisher_publicationDate (Composite)
- **Amaç:** Publisher + tarih sorguları
- **Unique:** Hayır
- **Fields:** publisher (ascending), publicationDate (descending)

## Örnek Data

### Data 1: Basit Kitap
```json
{
  "title": "The Great Gatsby",
  "isbn": "978-0-7432-7356-5",
  "pageCount": 180,
  "price": 15.99,
  "isAvailable": true,
  "publicationDate": "1925-04-10T00:00:00Z",
  "publisher": "publisher-001",
  "genres": ["genre-001", "genre-002"],
  "metadata": {
    "language": "English",
    "originalLanguage": "English"
  }
}
```

### Data 2: Detaylı Kitap
```json
{
  "title": "1984",
  "isbn": "978-0-452-28423-4",
  "pageCount": 328,
  "price": 12.99,
  "isAvailable": true,
  "publicationDate": "1949-06-08T00:00:00Z",
  "publisher": "publisher-002",
  "genres": ["genre-003", "genre-004"],
  "metadata": {
    "language": "English",
    "originalLanguage": "English",
    "awards": ["Time's 100 Best Novels"]
  }
}
```

## Query Örnekleri

### 1. ISBN ile Arama
```http
GET /api/v1/data/@books?filter={"isbn":"978-0-7432-7356-5"}
```

### 2. Publisher + Tarih Sorgusu
```http
GET /api/v1/data/@books?filter={"publisher":"publisher-001","publicationDate":{"$gte":"2020-01-01"}}
```

### 3. Expansion ile İlişkili Veriler
```http
GET /api/v1/data/@books?expand=publisher,genres
```

## İlgili Linkler
- [Field Types](../field-types/)
- [Validations](../validations/)
- [Indexes](../indexes/)
- [Dataset Oluşturma](../creating-dataset.md)

---

**Son Güncelleme:** 16 Ocak 2026
