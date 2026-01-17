---
title: "Bool Field Type"
category: "datasets"
tags: ["dataset", "field-type", "bool", "boolean", "true-false"]
service: "MngDataGateway"
difficulty: "beginner"
estimated_time: "2 dakika"
language: "tr"
priority: 1
---

# Bool Field Type

## Özet
Bool field type, boolean (true/false) değerleri saklamak için kullanılır.

## Özellikler
- ✅ Boolean değerler (true/false)
- ✅ Basit ve hızlı
- ✅ Array desteği (isArray: true)

## Field Tanımı

### Minimal Tanım
```json
{
  "fieldType": "bool",
  "name": "isActive",
  "title": "Aktif mi?"
}
```

### Tam Tanım
```json
{
  "fieldType": "bool",
  "name": "isActive",
  "title": "Aktif mi?",
  "description": "Kayıt aktif durumda mı?",
  "mandatory": false,
  "unique": false,
  "isArray": false
}
```

## MongoDB Storage

**Format:** Boolean

**Örnek:**
```json
{
  "isActive": true,
  "isPublished": false,
  "isAvailable": true
}
```

**MongoDB:**
```json
{
  "isActive": true,
  "isPublished": false,
  "isAvailable": true
}
```

## Validation Rules

Bool field için özel validation kuralları yok. Ancak:
- ✅ Değer sadece `true` veya `false` olabilir
- ✅ `null` değer `mandatory: false` ise kabul edilir

## Kullanım Senaryoları

### Senaryo 1: Aktif/Pasif Durumu
```json
{
  "fieldType": "bool",
  "name": "isActive",
  "title": "Aktif mi?",
  "mandatory": false
}
```

### Senaryo 2: Yayınlanmış mı?
```json
{
  "fieldType": "bool",
  "name": "isPublished",
  "title": "Yayınlanmış mı?",
  "mandatory": false
}
```

### Senaryo 3: Array Bool Field
```json
{
  "fieldType": "bool",
  "name": "features",
  "title": "Özellikler",
  "isArray": true
}
```

**Data:**
```json
{
  "features": [true, false, true, true]
}
```

## Sık Sorulan Sorular

**S: Bool field'da default değer belirleyebilir miyim?**  
C: Evet, `defaultValue: true` veya `defaultValue: false` kullanabilirsiniz.

**S: Bool field'ı unique yapabilir miyim?**  
C: Teknik olarak mümkün, ancak pratikte anlamlı değil (sadece 2 değer var: true/false).

**S: Bool field'da null değer olabilir mi?**  
C: Evet, `mandatory: false` ise null değer kabul edilir.

## İlgili Linkler
- [Dataset Oluşturma](../creating-dataset.md)
- [Field Types Genel Bakış](../index.md)

---

**Son Güncelleme:** 16 Ocak 2026
