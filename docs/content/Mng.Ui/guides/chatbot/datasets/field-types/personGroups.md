---
title: "PersonGroups Field Type"
category: "datasets"
tags: ["dataset", "field-type", "personGroups", "groups", "mngkeeper"]
service: "MngDataGateway"
difficulty: "intermediate"
estimated_time: "5 dakika"
language: "tr"
priority: 1
---

# PersonGroups Field Type

## Özet
PersonGroups field type, MngKeeper'daki kullanıcı gruplarına referans oluşturmanıza olanak sağlar. Group ID'leri saklanır ve expansion ile grup bilgileri getirilebilir.

## Özellikler
- ✅ MngKeeper Group ID referansı
- ✅ Single veya Array (multiple group reference)
- ✅ Expansion desteği (grup bilgileri getir)
- ✅ Validation (group exists check)
- ✅ Cache mekanizması (TTL: 5 dakika)

## Field Tanımı

### Minimal Tanım (Single Reference)
```json
{
  "fieldType": "personGroups",
  "name": "reviewerGroup",
  "title": "İnceleme Grubu",
  "mandatory": false
}
```

### Tam Tanım (Array Reference)
```json
{
  "fieldType": "personGroups",
  "name": "reviewerGroups",
  "title": "İnceleme Grupları",
  "description": "Kitabı inceleyen gruplar",
  "mandatory": false,
  "isArray": true,
  "unique": false
}
```

**Önemli Kurallar:**
- ✅ `isArray: false` → Single reference (tek grup)
- ✅ `isArray: true` → Array reference (çoklu grup)
- ✅ Değer: MngKeeper Group ID (string)

## MongoDB Storage

### Single Reference
**Field Tanımı:**
```json
{
  "fieldType": "personGroups",
  "name": "editorialTeam",
  "isArray": false
}
```

**Data:**
```json
{
  "title": "The Great Gatsby",
  "editorialTeam": "group-id-003"  // MngKeeper Group ID
}
```

**MongoDB:**
```json
{
  "title": "The Great Gatsby",
  "editorialTeam": "group-id-003"
}
```

### Array Reference
**Field Tanımı:**
```json
{
  "fieldType": "personGroups",
  "name": "reviewerGroups",
  "isArray": true
}
```

**Data:**
```json
{
  "title": "The Great Gatsby",
  "reviewerGroups": ["group-id-001", "group-id-002"]
}
```

**MongoDB:**
```json
{
  "title": "The Great Gatsby",
  "reviewerGroups": ["group-id-001", "group-id-002"]
}
```

## Expansion (Grup Bilgilerini Getir)

### Single Reference Expansion
**Query:**
```http
GET /api/v1/data/@books?expand=editorialTeam
```

**Response (expanded):**
```json
{
  "__dataId": "book-001",
  "title": "The Great Gatsby",
  "editorialTeam": {
    "groupId": "group-id-003",
    "name": "Editorial Team",
    "description": "Editorial team members",
    "memberCount": 5
  }
}
```

### Array Reference Expansion
**Query:**
```http
GET /api/v1/data/@books?expand=reviewerGroups
```

**Response (expanded):**
```json
{
  "__dataId": "book-001",
  "title": "The Great Gatsby",
  "reviewerGroups": [
    {
      "groupId": "group-id-001",
      "name": "Reviewers",
      "description": "Book reviewers",
      "memberCount": 10
    },
    {
      "groupId": "group-id-002",
      "name": "Quality Assurance",
      "description": "QA team",
      "memberCount": 8
    }
  ]
}
```

## Validation

**Otomatik Kontroller:**
- ✅ Group ID MngKeeper'da var mı?
- ✅ Group aktif mi?
- ✅ Array field'da duplicate group ID'ler olabilir (unique: false ise)

**Hata Durumu:**
```json
{
  "isValid": false,
  "errors": [
    {
      "field": "reviewerGroups",
      "code": "VALIDATION_GROUP_NOT_FOUND",
      "message": "Group ID 'invalid-group-id' not found in MngKeeper"
    }
  ]
}
```

## Cache Mekanizması

**TTL:** 5 dakika

**Amaç:** MngKeeper API çağrılarını azaltmak

**Cache Key:**
```
group_{groupId}
```

## Kullanım Senaryoları

### Senaryo 1: Books → Reviewer Groups (Array)
**Amaç:** Kitabı inceleyen gruplar

**Field Tanımı:**
```json
{
  "fieldType": "personGroups",
  "name": "reviewerGroups",
  "title": "İnceleme Grupları",
  "mandatory": false,
  "isArray": true
}
```

**Data:**
```json
{
  "title": "The Great Gatsby",
  "reviewerGroups": ["group-id-001", "group-id-002"]
}
```

### Senaryo 2: Tasks → Assignee Groups
**Amaç:** Görevin atandığı gruplar

**Field Tanımı:**
```json
{
  "fieldType": "personGroups",
  "name": "assigneeGroups",
  "title": "Atanan Gruplar",
  "mandatory": false,
  "isArray": true
}
```

## MngKeeper Entegrasyonu

**API Endpoint:**
```
GET /api/v1/groups/{groupId}
```

**Response:**
```json
{
  "groupId": "group-id-001",
  "name": "Reviewers",
  "description": "Book reviewers",
  "memberCount": 10
}
```

## Sık Sorulan Sorular

**S: PersonGroups field'da hangi değer saklanır?**  
C: MngKeeper'daki grubun Group ID'si (string) saklanır.

**S: Grup silinirse ne olur?**  
C: Referans bozulur. Expansion yapıldığında null döner veya hata alırsınız.

**S: Array personGroups field'da kaç grup olabilir?**  
C: Sınırsız. Ancak performans için makul bir sayıda tutmak önerilir.

**S: PersonGroups field'ı unique yapabilir miyim?**  
C: Evet, `unique: true` yaparak aynı grubun tekrar kullanılmasını engelleyebilirsiniz.

**S: Persons field ile PersonGroups field arasındaki fark nedir?**  
C: Persons field kullanıcılara referans verir, PersonGroups field kullanıcı gruplarına referans verir.

## İlgili Linkler
- [Persons Field](../field-types/persons.md)
- [Relation Field](../field-types/relation.md)
- [Dataset Oluşturma](../creating-dataset.md)

---

**Son Güncelleme:** 16 Ocak 2026
