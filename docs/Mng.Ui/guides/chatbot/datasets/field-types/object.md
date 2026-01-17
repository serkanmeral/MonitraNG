---
title: "Object Field Type"
category: "datasets"
tags: ["dataset", "field-type", "object", "nested", "json"]
service: "MngDataGateway"
difficulty: "intermediate"
estimated_time: "5 dakika"
language: "tr"
priority: 1
---

# Object Field Type

## Özet
Object field type, nested JSON object'leri saklamak için kullanılır. Free-form yapıda, herhangi bir JSON object saklanabilir.

## Özellikler
- ✅ Free-form JSON object
- ✅ Nested structure (iç içe object'ler)
- ✅ Array desteği (isArray: true)
- ✅ Schema validation yok (flexible)

## Field Tanımı

### Minimal Tanım
```json
{
  "fieldType": "object",
  "name": "metadata",
  "title": "Metadata"
}
```

### Tam Tanım
```json
{
  "fieldType": "object",
  "name": "customData",
  "title": "Özel Veriler",
  "description": "Ek metadata bilgileri",
  "mandatory": false,
  "unique": false,
  "isArray": false
}
```

## MongoDB Storage

**Format:** BSON Document (nested object)

**Örnek:**
```json
{
  "title": "The Great Gatsby",
  "metadata": {
    "language": "English",
    "originalLanguage": "English",
    "awards": ["Pulitzer Prize"],
    "translations": {
      "tr": "Muhteşem Gatsby",
      "fr": "Gatsby le Magnifique"
    }
  }
}
```

**MongoDB:**
```json
{
  "title": "The Great Gatsby",
  "metadata": {
    "language": "English",
    "originalLanguage": "English",
    "awards": ["Pulitzer Prize"],
    "translations": {
      "tr": "Muhteşem Gatsby",
      "fr": "Gatsby le Magnifique"
    }
  }
}
```

## Validation Rules

Object field için özel validation kuralları yok. Ancak:
- ✅ İçerik herhangi bir JSON object olabilir
- ✅ Nested structure desteklenir
- ✅ Array, object, primitive değerler içerebilir

## Kullanım Senaryoları

### Senaryo 1: Metadata
```json
{
  "fieldType": "object",
  "name": "metadata",
  "title": "Metadata"
}
```

**Data:**
```json
{
  "metadata": {
    "language": "English",
    "publisher": "Penguin",
    "edition": "First Edition"
  }
}
```

### Senaryo 2: Custom Data
```json
{
  "fieldType": "object",
  "name": "customData",
  "title": "Özel Veriler"
}
```

**Data:**
```json
{
  "customData": {
    "internalNotes": "Special handling required",
    "tags": ["important", "review"],
    "settings": {
      "notifications": true,
      "autoSave": false
    }
  }
}
```

### Senaryo 3: Nested Structure
```json
{
  "fieldType": "object",
  "name": "address",
  "title": "Adres"
}
```

**Data:**
```json
{
  "address": {
    "street": "123 Main St",
    "city": "Istanbul",
    "country": "Turkey",
    "coordinates": {
      "latitude": 41.0082,
      "longitude": 28.9784
    }
  }
}
```

### Senaryo 4: Array Object Field
```json
{
  "fieldType": "object",
  "name": "reviews",
  "title": "Yorumlar",
  "isArray": true
}
```

**Data:**
```json
{
  "reviews": [
    {
      "user": "user-001",
      "rating": 5,
      "comment": "Great book!"
    },
    {
      "user": "user-002",
      "rating": 4,
      "comment": "Good read"
    }
  ]
}
```

## Query Örnekleri

### Nested Field Sorgulama
```http
GET /api/v1/data/@books?filter={"metadata.language":"English"}
```

### Nested Object Sorgulama
```http
GET /api/v1/data/@books?filter={"address.city":"Istanbul"}
```

## Sık Sorulan Sorular

**S: Object field'da schema tanımlayabilir miyim?**  
C: Şu anda desteklenmiyor. Object field free-form'dur. Gelecekte schema validation desteği eklenebilir.

**S: Object field'ı unique yapabilir miyim?**  
C: Teknik olarak mümkün, ancak object'lerin tam eşitliği kontrol edilir (deep equality).

**S: Object field'da validation yapabilir miyim?**  
C: Şu anda field-level validation yok. Expression-based validation ile nested field'lar kontrol edilebilir.

**S: Object field'da arama yapabilir miyim?**  
C: Evet, dot notation ile nested field'larda arama yapabilirsiniz: `metadata.language`

**S: Object field'ı array yapabilir miyim?**  
C: Evet, `isArray: true` yaparak object array'i saklayabilirsiniz.

## İlgili Linkler
- [Dataset Oluşturma](../creating-dataset.md)
- [Field Types Genel Bakış](../index.md)

---

**Son Güncelleme:** 16 Ocak 2026
