---
title: "Persons Field Type"
category: "datasets"
tags: ["dataset", "field-type", "persons", "users", "mngkeeper"]
service: "MngDataGateway"
difficulty: "intermediate"
estimated_time: "5 dakika"
language: "tr"
priority: 1
---

# Persons Field Type

## Özet
Persons field type, MngKeeper'daki kullanıcılara referans oluşturmanıza olanak sağlar. User ID'leri saklanır ve expansion ile kullanıcı bilgileri getirilebilir.

## Özellikler
- ✅ MngKeeper User ID referansı
- ✅ Single veya Array (multiple user reference)
- ✅ Expansion desteği (kullanıcı bilgileri getir)
- ✅ Validation (user exists check)
- ✅ Cache mekanizması (TTL: 5 dakika)

## Field Tanımı

### Minimal Tanım (Single Reference)
```json
{
  "fieldType": "persons",
  "name": "author",
  "title": "Yazar",
  "mandatory": false
}
```

### Tam Tanım (Array Reference)
```json
{
  "fieldType": "persons",
  "name": "coAuthors",
  "title": "Ortak Yazarlar",
  "description": "Kitabın ortak yazarları",
  "mandatory": false,
  "isArray": true,
  "unique": false
}
```

**Önemli Kurallar:**
- ✅ `isArray: false` → Single reference (tek kullanıcı)
- ✅ `isArray: true` → Array reference (çoklu kullanıcı)
- ✅ Değer: MngKeeper User ID (string)

## MongoDB Storage

### Single Reference
**Field Tanımı:**
```json
{
  "fieldType": "persons",
  "name": "author",
  "isArray": false
}
```

**Data:**
```json
{
  "title": "The Great Gatsby",
  "author": "690cdb7fae502df7d3330bbb"  // MngKeeper User ID
}
```

**MongoDB:**
```json
{
  "title": "The Great Gatsby",
  "author": "690cdb7fae502df7d3330bbb"
}
```

### Array Reference
**Field Tanımı:**
```json
{
  "fieldType": "persons",
  "name": "coAuthors",
  "isArray": true
}
```

**Data:**
```json
{
  "title": "The Great Gatsby",
  "coAuthors": [
    "user-id-002",
    "user-id-003"
  ]
}
```

**MongoDB:**
```json
{
  "title": "The Great Gatsby",
  "coAuthors": ["user-id-002", "user-id-003"]
}
```

## Expansion (Kullanıcı Bilgilerini Getir)

### Single Reference Expansion
**Query:**
```http
GET /api/v1/data/@books?expand=author
```

**Response (expanded):**
```json
{
  "__dataId": "book-001",
  "title": "The Great Gatsby",
  "author": {
    "id": "690cdb7fae502df7d3330bbb",
    "username": "serkan",
    "email": "serkan@seven.com",
    "firstName": "Serkan",
    "lastName": "MERAL",
    "isActive": true
  }
}
```

### Array Reference Expansion
**Query:**
```http
GET /api/v1/data/@books?expand=coAuthors
```

**Response (expanded):**
```json
{
  "__dataId": "book-001",
  "title": "The Great Gatsby",
  "coAuthors": [
    {
      "id": "user-id-002",
      "username": "john.doe",
      "email": "john@seven.com",
      "firstName": "John",
      "lastName": "Doe"
    },
    {
      "id": "user-id-003",
      "username": "jane.smith",
      "email": "jane@seven.com",
      "firstName": "Jane",
      "lastName": "Smith"
    }
  ]
}
```

## Validation

**Otomatik Kontroller:**
- ✅ User ID MngKeeper'da var mı?
- ✅ User aktif mi?
- ✅ Array field'da duplicate user ID'ler olabilir (unique: false ise)

**Hata Durumu:**
```json
{
  "isValid": false,
  "errors": [
    {
      "field": "author",
      "code": "VALIDATION_USER_NOT_FOUND",
      "message": "User ID 'invalid-user-id' not found in MngKeeper"
    }
  ]
}
```

## Cache Mekanizması

**TTL:** 5 dakika

**Amaç:** MngKeeper API çağrılarını azaltmak

**Cache Key:**
```
user_{userId}
```

**Cache Invalidation:**
- TTL sona erdiğinde otomatik
- Manuel invalidation (gelecekte)

## Kullanım Senaryoları

### Senaryo 1: Books → Author (Single)
**Amaç:** Kitabın yazarı

**Field Tanımı:**
```json
{
  "fieldType": "persons",
  "name": "author",
  "title": "Yazar",
  "mandatory": true,
  "isArray": false
}
```

**Data:**
```json
{
  "title": "The Great Gatsby",
  "author": "690cdb7fae502df7d3330bbb"
}
```

### Senaryo 2: Books → Co-Authors (Array)
**Amaç:** Kitabın ortak yazarları

**Field Tanımı:**
```json
{
  "fieldType": "persons",
  "name": "coAuthors",
  "title": "Ortak Yazarlar",
  "mandatory": false,
  "isArray": true
}
```

**Data:**
```json
{
  "title": "The Great Gatsby",
  "coAuthors": ["user-id-002", "user-id-003"]
}
```

### Senaryo 3: Tasks → Assignee
**Amaç:** Görevin atandığı kullanıcı

**Field Tanımı:**
```json
{
  "fieldType": "persons",
  "name": "assignee",
  "title": "Atanan Kişi",
  "mandatory": false,
  "isArray": false
}
```

## MngKeeper Entegrasyonu

**API Endpoint:**
```
GET /api/v1/users/{userId}
```

**Response:**
```json
{
  "id": "690cdb7fae502df7d3330bbb",
  "username": "serkan",
  "email": "serkan@seven.com",
  "firstName": "Serkan",
  "lastName": "MERAL",
  "isActive": true
}
```

## Sık Sorulan Sorular

**S: Persons field'da hangi değer saklanır?**  
C: MngKeeper'daki kullanıcının User ID'si (string) saklanır.

**S: Kullanıcı silinirse ne olur?**  
C: Referans bozulur. Expansion yapıldığında null döner veya hata alırsınız. Validation ile kontrol edilebilir.

**S: Array persons field'da kaç kullanıcı olabilir?**  
C: Sınırsız. Ancak performans için makul bir sayıda tutmak önerilir.

**S: Persons field'ı unique yapabilir miyim?**  
C: Evet, `unique: true` yaparak aynı kullanıcının tekrar kullanılmasını engelleyebilirsiniz.

**S: Cache nasıl çalışır?**  
C: Kullanıcı bilgileri 5 dakika cache'lenir. TTL sona erdiğinde MngKeeper'dan yeniden alınır.

**S: Persons field ile relation field arasındaki fark nedir?**  
C: Persons field MngKeeper kullanıcılarına referans verir, relation field başka dataset'lere referans verir.

## İlgili Linkler
- [PersonGroups Field](../field-types/personGroups.md)
- [Relation Field](../field-types/relation.md)
- [Dataset Oluşturma](../creating-dataset.md)

---

**Son Güncelleme:** 16 Ocak 2026
