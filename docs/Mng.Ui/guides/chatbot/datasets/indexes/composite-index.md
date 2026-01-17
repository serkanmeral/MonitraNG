---
title: "Composite Index"
category: "datasets"
tags: ["dataset", "index", "composite", "multi-field", "performance"]
service: "MngDataGateway"
difficulty: "intermediate"
estimated_time: "5 dakika"
language: "tr"
priority: 1
---

# Composite Index

## Özet
Composite index, birden fazla field içeren index'tir. Çoklu field sorguları ve sıralamalar için optimize edilmiştir.

## Özellikler
- ✅ Birden fazla field içerir
- ✅ Field sırası önemlidir (MongoDB index prefix kuralı)
- ✅ Query optimization
- ✅ Unique veya non-unique olabilir

## Index Tanımı

### Basit Composite Index
```json
{
  "name": "idx_title_publicationDate",
  "fields": {
    "title": 1,
    "publicationDate": -1
  },
  "unique": false
}
```

### Unique Composite Index
```json
{
  "name": "idx_email_domain",
  "fields": {
    "email": 1,
    "domain": 1
  },
  "unique": true
}
```

## MongoDB Index Prefix Kuralı

**Kural:** Composite index'te field sırası önemlidir. İlk field'lar prefix oluşturur.

**Örnek:**
```json
{
  "name": "idx_publisher_publicationDate",
  "fields": {
    "publisher": 1,        // Prefix 1
    "publicationDate": -1  // Prefix 2
  }
}
```

**Bu Index Şu Query'leri Destekler:**
- ✅ `{ publisher: "..." }` (prefix 1 kullanılır)
- ✅ `{ publisher: "...", publicationDate: ... }` (tam index kullanılır)
- ❌ `{ publicationDate: ... }` (prefix 2 yok, index kullanılmaz)

## Pratik Örnekler

### Örnek 1: Publisher + Publication Date
**Amaç:** Publisher'a göre gruplama + tarih sıralama

**Index:**
```json
{
  "name": "idx_publisher_publicationDate",
  "fields": {
    "publisher": 1,
    "publicationDate": -1
  },
  "unique": false
}
```

**Query Örnekleri:**
```javascript
// ✅ Index kullanılır (prefix 1)
{ publisher: "publisher-001" }

// ✅ Index kullanılır (tam index)
{ publisher: "publisher-001", publicationDate: { $gte: "2020-01-01" } }

// ❌ Index kullanılmaz (prefix değil)
{ publicationDate: { $gte: "2020-01-01" } }
```

### Örnek 2: Title + Book Code (Unique)
**Amaç:** Title + BookCode kombinasyonu benzersiz olmalı

**Index:**
```json
{
  "name": "idx_title_bookCode",
  "fields": {
    "title": 1,
    "bookCode": 1
  },
  "unique": true
}
```

**Geçerli:**
```json
{ "title": "Book 1", "bookCode": "B001" }
{ "title": "Book 1", "bookCode": "B002" }  // ✅ Farklı bookCode
{ "title": "Book 2", "bookCode": "B001" }  // ✅ Farklı title
```

**Geçersiz:**
```json
{ "title": "Book 1", "bookCode": "B001" }
{ "title": "Book 1", "bookCode": "B001" }  // ❌ Duplicate
```

### Örnek 3: Status + Priority + Created Date
**Amaç:** Karmaşık sorgular için

**Index:**
```json
{
  "name": "idx_status_priority_createdDate",
  "fields": {
    "status": 1,
    "priority": -1,
    "createdDate": -1
  },
  "unique": false
}
```

**Query Örnekleri:**
```javascript
// ✅ Index kullanılır (prefix 1)
{ status: "active" }

// ✅ Index kullanılır (prefix 1-2)
{ status: "active", priority: { $gte: 5 } }

// ✅ Index kullanılır (tam index)
{ status: "active", priority: 10, createdDate: { $gte: "2025-01-01" } }
```

## Best Practices

### 1. Field Sırası
**Kural:** En çok kullanılan field'ı önce koyun.

**İyi:**
```json
{
  "fields": {
    "publisher": 1,        // Daha sık kullanılıyor
    "publicationDate": -1
  }
}
```

**Kötü:**
```json
{
  "fields": {
    "publicationDate": -1,  // Daha az kullanılıyor
    "publisher": 1
  }
}
```

### 2. Index Sayısı
**Öneri:** Composite index sayısını sınırlayın (5-10 arası). Çok fazla index write performance'ı düşürür.

### 3. Query Pattern
**Kural:** Sık kullanılan query pattern'lerine göre index oluşturun.

**Örnek:**
- Sık sorgu: `{ publisher: "...", publicationDate: ... }`
- Index: `idx_publisher_publicationDate` ✅

## Sık Sorulan Sorular

**S: Composite index'te kaç field olabilir?**  
C: MongoDB'de teorik limit yok, ancak pratikte 3-5 field yeterlidir. Çok fazla field index boyutunu artırır.

**S: Field sırası değiştirilirse ne olur?**  
C: Yeni bir index oluşturulur. Eski index manuel olarak silinmelidir.

**S: Composite index'te tüm field'lar ascending olmalı mı?**  
C: Hayır, her field için ayrı ayrı ascending (1) veya descending (-1) seçebilirsiniz.

**S: Partial index (koşullu index) destekleniyor mu?**  
C: Şu anda desteklenmiyor. Gelecekte partial index desteği eklenebilir.

## İlgili Linkler
- [Index Types](../indexes/index-types.md)
- [Unique Index](../indexes/unique-index.md)
- [Index Best Practices](../indexes/index-best-practices.md)
- [Dataset Oluşturma](../creating-dataset.md)

---

**Son Güncelleme:** 16 Ocak 2026
