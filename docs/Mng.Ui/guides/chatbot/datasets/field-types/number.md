---
title: "Number Field Type"
category: "datasets"
tags: ["dataset", "field-type", "number", "integer", "decimal"]
service: "MngDataGateway"
difficulty: "beginner"
estimated_time: "3 dakika"
language: "tr"
priority: 1
---

# Number Field Type

## Özet
Number field type, sayısal değerler (integer veya decimal) saklamak için kullanılır.

## Özellikler
- ✅ Integer ve decimal değerler
- ✅ Validation rules (min, max)
- ✅ Array desteği (isArray: true)

## Field Tanımı

### Minimal Tanım
```json
{
  "fieldType": "number",
  "name": "pageCount",
  "title": "Sayfa Sayısı"
}
```

### Tam Tanım
```json
{
  "fieldType": "number",
  "name": "pageCount",
  "title": "Sayfa Sayısı",
  "description": "Kitabın toplam sayfa sayısı",
  "mandatory": false,
  "unique": false,
  "isArray": false,
  "validation": {
    "min": 1,
    "max": 10000,
    "message": "Sayfa sayısı 1 ile 10000 arasında olmalıdır"
  }
}
```

## MongoDB Storage

**Format:** Number (integer veya double)

**Örnek:**
```json
{
  "pageCount": 250,
  "price": 29.99,
  "rating": 4.5
}
```

**MongoDB:**
```json
{
  "pageCount": 250,
  "price": 29.99,
  "rating": 4.5
}
```

## Validation Rules

### Desteklenen Kurallar
- `min` - Minimum değer
- `max` - Maksimum değer
- `message` - Özel hata mesajı

### Örnekler

**Min/Max:**
```json
{
  "validation": {
    "min": 0,
    "max": 100
  }
}
```

**Sadece Min:**
```json
{
  "validation": {
    "min": 0,
    "message": "Değer 0'dan küçük olamaz"
  }
}
```

## Kullanım Senaryoları

### Senaryo 1: Sayfa Sayısı
```json
{
  "fieldType": "number",
  "name": "pageCount",
  "title": "Sayfa Sayısı",
  "validation": {
    "min": 1,
    "max": 10000
  }
}
```

### Senaryo 2: Fiyat
```json
{
  "fieldType": "number",
  "name": "price",
  "title": "Fiyat",
  "validation": {
    "min": 0
  }
}
```

### Senaryo 3: Yüzde
```json
{
  "fieldType": "number",
  "name": "discountPercentage",
  "title": "İndirim Yüzdesi",
  "validation": {
    "min": 0,
    "max": 100
  }
}
```

### Senaryo 4: Array Number Field
```json
{
  "fieldType": "number",
  "name": "scores",
  "title": "Puanlar",
  "isArray": true
}
```

**Data:**
```json
{
  "scores": [85, 90, 95, 88]
}
```

## Sık Sorulan Sorular

**S: Integer ve decimal ayrımı var mı?**  
C: Hayır, MongoDB'de number type tek bir tiptir. Integer veya decimal olabilir.

**S: Negatif sayılar destekleniyor mu?**  
C: Evet, ancak validation ile `min: 0` yaparak negatif değerleri engelleyebilirsiniz.

**S: Number field'ı unique yapabilir miyim?**  
C: Evet, `unique: true` yaparak aynı değerin tekrar kullanılmasını engelleyebilirsiniz.

**S: Çok büyük sayılar destekleniyor mu?**  
C: Evet, MongoDB double precision (64-bit) destekler. Çok büyük sayılar için string kullanmak daha güvenli olabilir.

## İlgili Linkler
- [Field-Level Validation](../validations/field-level-validation.md)
- [Expression-Based Validation](../validations/expression-validation.md)
- [Dataset Oluşturma](../creating-dataset.md)

---

**Son Güncelleme:** 16 Ocak 2026
