---
title: "Unique Index"
category: "datasets"
tags: ["dataset", "index", "unique", "constraint", "duplicate"]
service: "MngDataGateway"
difficulty: "beginner"
estimated_time: "3 dakika"
language: "tr"
priority: 1
---

# Unique Index

## Özet
Unique index, aynı değere sahip birden fazla kayıt olamayacağını garanti eder. Duplicate değer kontrolü yapar.

## Özellikler
- ✅ Duplicate değer kontrolü
- ✅ Data integrity (veri bütünlüğü)
- ✅ Hızlı arama (index avantajı)

## Index Tanımı

### Basit Unique Index
```json
{
  "name": "idx_isbn",
  "fields": {
    "isbn": 1
  },
  "unique": true
}
```

### Composite Unique Index
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

## Kullanım Senaryoları

### Senaryo 1: ISBN Unique Index
**Amaç:** Her kitabın benzersiz ISBN numarası

**Index:**
```json
{
  "name": "idx_isbn",
  "fields": {
    "isbn": 1
  },
  "unique": true
}
```

**Geçerli:**
```json
{ "isbn": "978-0-123456-78-9" }
{ "isbn": "978-0-123456-78-0" }
```

**Geçersiz:**
```json
{ "isbn": "978-0-123456-78-9" }
{ "isbn": "978-0-123456-78-9" }  // ❌ Duplicate key error
```

### Senaryo 2: E-posta Unique Index
**Amaç:** Her kullanıcının benzersiz e-posta adresi

**Index:**
```json
{
  "name": "idx_email",
  "fields": {
    "email": 1
  },
  "unique": true
}
```

### Senaryo 3: Composite Unique Index
**Amaç:** Email + Domain kombinasyonu benzersiz olmalı

**Index:**
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

**Geçerli:**
```json
{ "email": "user@example.com", "domain": "domain1" }
{ "email": "user@example.com", "domain": "domain2" }  // ✅ Farklı domain
```

**Geçersiz:**
```json
{ "email": "user@example.com", "domain": "domain1" }
{ "email": "user@example.com", "domain": "domain1" }  // ❌ Duplicate
```

## Hata Mesajları

**Duplicate Key Error:**
```json
{
  "isValid": false,
  "errors": [
    {
      "field": "isbn",
      "code": "DUPLICATE_KEY",
      "message": "Duplicate key error: isbn value '978-0-123456-78-9' already exists"
    }
  ]
}
```

## Best Practices

### 1. Ne Zaman Unique Index Kullanılmalı?
- ✅ Benzersiz olması gereken değerler (ISBN, e-posta, kullanıcı adı)
- ✅ Incremental field'lar (otomatik unique)
- ✅ Primary key benzeri field'lar

### 2. Composite Unique Index
- ✅ Birden fazla field kombinasyonu benzersiz olmalı
- ✅ Örnek: Email + Domain, Username + Domain

### 3. Null Değerler
- ✅ Unique index'te birden fazla null değer olabilir (MongoDB default)
- ✅ Eğer null'lar da unique olmalıysa, field'ı `mandatory: true` yapın

## Sık Sorulan Sorular

**S: Unique index'te null değerler nasıl işlenir?**  
C: MongoDB'de birden fazla null değer unique index'te kabul edilir. Eğer null'lar da unique olmalıysa, field'ı `mandatory: true` yapın.

**S: Unique index ile field-level unique arasındaki fark nedir?**  
C: Unique index MongoDB seviyesinde constraint sağlar (daha güvenilir). Field-level unique sadece validation seviyesindedir.

**S: Composite unique index'te tüm field'lar mı unique olmalı?**  
C: Hayır, sadece kombinasyon unique olmalı. Tek tek field'lar duplicate olabilir.

**S: Unique index'i kaldırabilir miyim?**  
C: Evet, schema'dan index tanımını silerseniz, MongoDB'deki index manuel olarak silinmelidir.

## İlgili Linkler
- [Index Types](../indexes/index-types.md)
- [Composite Index](../indexes/composite-index.md)
- [Dataset Oluşturma](../creating-dataset.md)

---

**Son Güncelleme:** 16 Ocak 2026
