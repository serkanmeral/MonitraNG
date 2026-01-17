---
title: "Relation Field Type"
category: "datasets"
tags: ["dataset", "field-type", "relation", "reference", "lookup"]
service: "MngDataGateway"
difficulty: "intermediate"
estimated_time: "5 dakika"
language: "tr"
priority: 1
steps:
  - order: 1
    title: "Hedef Dataset'i Belirle"
    action: "relationDataset field'ını doldurun"
    expected_result: "Hedef dataset belirlenir"
  - order: 2
    title: "Relation Field Tanımla"
    action: "fieldType: 'relation' seçin ve relationDataset belirtin"
    expected_result: "Relation field tanımı oluşturulur"
  - order: 3
    title: "Array Field (Opsiyonel)"
    action: "isArray: true yaparak multiple reference destekleyin"
    expected_result: "Array relation field oluşturulur"
---

# Relation Field Type

## Özet
Relation field type, başka bir dataset'e referans oluşturmanıza olanak sağlar. MongoDB Lookup kullanarak ilişkili verileri expand edebilirsiniz.

## Özellikler
- ✅ Dataset referansı (MongoDB Lookup)
- ✅ Single veya Array (multiple reference)
- ✅ Expansion desteği (ilişkili verileri getir)
- ✅ Validation (referans edilen kayıt var mı?)

## Field Tanımı

### Minimal Tanım (Single Reference)
```json
{
  "fieldType": "relation",
  "name": "publisher",
  "title": "Yayıncı",
  "relationDataset": "@publishers",
  "mandatory": false
}
```

### Tam Tanım (Array Reference)
```json
{
  "fieldType": "relation",
  "name": "genres",
  "title": "Türler",
  "description": "Kitabın türleri",
  "relationDataset": "@genres",
  "mandatory": false,
  "isArray": true,
  "unique": false
}
```

**Önemli Kurallar:**
- ✅ `relationDataset` zorunlu (hedef dataset adı)
- ✅ `isArray: false` → Single reference (tek değer)
- ✅ `isArray: true` → Array reference (çoklu değer)

## MongoDB Storage

### Single Reference
**Field Tanımı:**
```json
{
  "fieldType": "relation",
  "name": "publisher",
  "relationDataset": "@publishers"
}
```

**Data:**
```json
{
  "title": "The Great Gatsby",
  "publisher": "publisher-001"  // @publishers dataset'indeki __dataId
}
```

**MongoDB:**
```json
{
  "title": "The Great Gatsby",
  "publisher": "publisher-001"
}
```

### Array Reference
**Field Tanımı:**
```json
{
  "fieldType": "relation",
  "name": "genres",
  "relationDataset": "@genres",
  "isArray": true
}
```

**Data:**
```json
{
  "title": "The Great Gatsby",
  "genres": ["genre-001", "genre-002"]  // Array of __dataId
}
```

**MongoDB:**
```json
{
  "title": "The Great Gatsby",
  "genres": ["genre-001", "genre-002"]
}
```

## Expansion (İlişkili Verileri Getir)

### Single Reference Expansion
**Query:**
```http
GET /api/v1/data/@books?expand=publisher
```

**Response (expanded):**
```json
{
  "__dataId": "book-001",
  "title": "The Great Gatsby",
  "publisher": {
    "__dataId": "publisher-001",
    "name": "Penguin Random House",
    "website": "https://www.penguinrandomhouse.com"
  }
}
```

### Array Reference Expansion
**Query:**
```http
GET /api/v1/data/@books?expand=genres
```

**Response (expanded):**
```json
{
  "__dataId": "book-001",
  "title": "The Great Gatsby",
  "genres": [
    {
      "__dataId": "genre-001",
      "name": "Fiction"
    },
    {
      "__dataId": "genre-002",
      "name": "Classic"
    }
  ]
}
```

### Multiple Expansion
**Query:**
```http
GET /api/v1/data/@books?expand=publisher,genres
```

**Response:**
```json
{
  "__dataId": "book-001",
  "title": "The Great Gatsby",
  "publisher": {
    "__dataId": "publisher-001",
    "name": "Penguin Random House"
  },
  "genres": [
    { "__dataId": "genre-001", "name": "Fiction" },
    { "__dataId": "genre-002", "name": "Classic" }
  ]
}
```

## Validation Rules

Relation field için özel validation kuralları:
- ✅ `relationDataset` zorunlu
- ✅ Referans edilen `__dataId` hedef dataset'te var olmalı
- ✅ Array field'da duplicate değerler olabilir (unique: false ise)

## Kullanım Senaryoları

### Senaryo 1: Books → Publishers (1-to-Many)
**Amaç:** Bir kitap tek bir yayıncıya ait

**Field Tanımı:**
```json
{
  "fieldType": "relation",
  "name": "publisher",
  "title": "Yayıncı",
  "relationDataset": "@publishers",
  "mandatory": true,
  "isArray": false
}
```

**Data:**
```json
{
  "title": "The Great Gatsby",
  "publisher": "publisher-001"
}
```

**Expanded:**
```json
{
  "title": "The Great Gatsby",
  "publisher": {
    "__dataId": "publisher-001",
    "name": "Penguin Random House"
  }
}
```

### Senaryo 2: Books → Genres (Many-to-Many)
**Amaç:** Bir kitap birden fazla türe ait olabilir

**Field Tanımı:**
```json
{
  "fieldType": "relation",
  "name": "genres",
  "title": "Türler",
  "relationDataset": "@genres",
  "mandatory": false,
  "isArray": true
}
```

**Data:**
```json
{
  "title": "The Great Gatsby",
  "genres": ["genre-001", "genre-002", "genre-003"]
}
```

**Expanded:**
```json
{
  "title": "The Great Gatsby",
  "genres": [
    { "__dataId": "genre-001", "name": "Fiction" },
    { "__dataId": "genre-002", "name": "Classic" },
    { "__dataId": "genre-003", "name": "American Literature" }
  ]
}
```

### Senaryo 3: Tasks → Parent Task (Self-Reference)
**Amaç:** Görevler arası hiyerarşi

**Field Tanımı:**
```json
{
  "fieldType": "relation",
  "name": "parentTask",
  "title": "Üst Görev",
  "relationDataset": "@tasks",  // Kendi dataset'ine referans
  "mandatory": false,
  "isArray": false
}
```

**Data:**
```json
{
  "title": "Alt Görev",
  "parentTask": "task-001"  // Üst görev
}
```

## Sık Sorulan Sorular

**S: Relation field'da hangi değer saklanır?**  
C: Hedef dataset'teki kaydın `__dataId` değeri saklanır.

**S: Referans edilen kayıt silinirse ne olur?**  
C: Referans bozulur. Expansion yapıldığında null döner veya hata alırsınız. Validation ile kontrol edilebilir.

**S: Array relation field'da kaç değer olabilir?**  
C: Sınırsız. Ancak performans için makul bir sayıda tutmak önerilir.

**S: Relation field'ı unique yapabilir miyim?**  
C: Evet, `unique: true` yaparak aynı referansın tekrar kullanılmasını engelleyebilirsiniz.

**S: Nested expansion yapabilir miyim?**  
C: Evet, `deep` parametresi ile nested expansion yapabilirsiniz. Örn: `?expand=publisher&deep=2`

**S: Circular reference (döngüsel referans) olabilir mi?**  
C: Evet, self-reference yapılabilir. Ancak expansion'da döngü oluşmaması için `deep` limiti kullanılır.

## İlgili Linkler
- [Dataset Oluşturma](../creating-dataset.md)
- [Field Types Genel Bakış](../index.md)
- [Expansion Dokümantasyonu](../../../MngDataGateway/api/EXPANSION.md)

---

**Son Güncelleme:** 16 Ocak 2026
