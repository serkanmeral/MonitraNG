---
title: "DateTime Field Type"
category: "datasets"
tags: ["dataset", "field-type", "datetime", "date", "time"]
service: "MngDataGateway"
difficulty: "beginner"
estimated_time: "3 dakika"
language: "tr"
priority: 1
---

# DateTime Field Type

## Özet
DateTime field type, tarih ve saat değerleri saklamak için kullanılır. ISO 8601 formatında UTC zamanı saklanır.

## Özellikler
- ✅ ISO 8601 format (UTC)
- ✅ Validation rules (minDate, maxDate)
- ✅ Array desteği (isArray: true)

## Field Tanımı

### Minimal Tanım
```json
{
  "fieldType": "datetime",
  "name": "createdAt",
  "title": "Oluşturulma Tarihi"
}
```

### Tam Tanım
```json
{
  "fieldType": "datetime",
  "name": "publicationDate",
  "title": "Yayın Tarihi",
  "description": "Kitabın yayın tarihi",
  "mandatory": false,
  "unique": false,
  "isArray": false,
  "validation": {
    "minDate": "1900-01-01T00:00:00Z",
    "maxDate": "2100-12-31T23:59:59Z",
    "message": "Yayın tarihi 1900 ile 2100 arasında olmalıdır"
  }
}
```

## MongoDB Storage

**Format:** ISODate (MongoDB Date type)

**Örnek:**
```json
{
  "publicationDate": "2025-01-15T10:30:00Z",
  "createdAt": "2025-01-01T00:00:00Z"
}
```

**MongoDB:**
```json
{
  "publicationDate": ISODate("2025-01-15T10:30:00Z"),
  "createdAt": ISODate("2025-01-01T00:00:00Z")
}
```

## Date Format

### ISO 8601 Format
- **Tam Format:** `2025-01-15T10:30:00Z`
- **Sadece Tarih:** `2025-01-15` (saat 00:00:00 olarak kabul edilir)
- **UTC Zaman:** `Z` suffix UTC zamanı belirtir

### Örnekler
- `2025-01-15T10:30:00Z` ✅
- `2025-01-15` ✅ (saat 00:00:00 UTC)
- `2025-01-15T10:30:00+03:00` ✅ (timezone offset)

## Validation Rules

### Desteklenen Kurallar
- `minDate` - Minimum tarih (ISO 8601)
- `maxDate` - Maksimum tarih (ISO 8601)
- `message` - Özel hata mesajı

### Örnekler

**Tarih Aralığı:**
```json
{
  "validation": {
    "minDate": "1900-01-01T00:00:00Z",
    "maxDate": "2100-12-31T23:59:59Z"
  }
}
```

**Geçmiş Tarih Kontrolü:**
```json
{
  "validation": {
    "maxDate": "2025-01-01T00:00:00Z",
    "message": "Tarih bugünden önce olmalıdır"
  }
}
```

## Kullanım Senaryoları

### Senaryo 1: Yayın Tarihi
```json
{
  "fieldType": "datetime",
  "name": "publicationDate",
  "title": "Yayın Tarihi",
  "validation": {
    "minDate": "1900-01-01T00:00:00Z",
    "maxDate": "2100-12-31T23:59:59Z"
  }
}
```

### Senaryo 2: Doğum Tarihi
```json
{
  "fieldType": "datetime",
  "name": "birthDate",
  "title": "Doğum Tarihi",
  "validation": {
    "minDate": "1900-01-01T00:00:00Z",
    "maxDate": "2025-01-01T00:00:00Z"
  }
}
```

### Senaryo 3: Proje Başlangıç/Bitiş Tarihi
```json
{
  "fields": [
    {
      "fieldType": "datetime",
      "name": "startDate",
      "title": "Başlangıç Tarihi"
    },
    {
      "fieldType": "datetime",
      "name": "endDate",
      "title": "Bitiş Tarihi"
    }
  ],
  "validations": [
    {
      "name": "endDateAfterStartDate",
      "type": "expression",
      "expression": "endDate > startDate"
    }
  ]
}
```

## Sık Sorulan Sorular

**S: DateTime field'da timezone nasıl yönetilir?**  
C: Tüm tarihler UTC olarak saklanır. Frontend'de kullanıcının timezone'ına göre gösterilir.

**S: Sadece tarih (saat olmadan) saklayabilir miyim?**  
C: Evet, saat kısmını `00:00:00` olarak ayarlayabilirsiniz: `2025-01-15T00:00:00Z`

**S: DateTime field'ı unique yapabilir miyim?**  
C: Evet, ancak pratikte nadiren kullanılır (aynı tarih/saat olabilir).

**S: Array datetime field kullanabilir miyim?**  
C: Evet, `isArray: true` yaparak tarih listesi saklayabilirsiniz.

## İlgili Linkler
- [Field-Level Validation](../validations/field-level-validation.md)
- [Expression-Based Validation](../validations/expression-validation.md)
- [Dataset Oluşturma](../creating-dataset.md)

---

**Son Güncelleme:** 16 Ocak 2026
