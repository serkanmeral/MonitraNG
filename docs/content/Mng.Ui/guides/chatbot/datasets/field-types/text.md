---
title: "Text Field Type"
category: "datasets"
tags: ["dataset", "field-type", "text", "string"]
service: "MngDataGateway"
difficulty: "beginner"
estimated_time: "3 dakika"
language: "tr"
priority: 1
---

# Text Field Type

## Özet
Text field type, string (metin) değerleri saklamak için kullanılır. En yaygın kullanılan field type'dır.

## Özellikler
- ✅ String değerler
- ✅ Unicode desteği (Türkçe karakterler, emoji, vb.)
- ✅ Validation rules (minLength, maxLength, pattern)
- ✅ Array desteği (isArray: true)

## Field Tanımı

### Minimal Tanım
```json
{
  "fieldType": "text",
  "name": "title",
  "title": "Başlık"
}
```

### Tam Tanım
```json
{
  "fieldType": "text",
  "name": "title",
  "title": "Başlık",
  "description": "Kitabın başlığı",
  "mandatory": true,
  "unique": false,
  "isArray": false,
  "validation": {
    "minLength": 3,
    "maxLength": 100,
    "pattern": "^[A-Z]",
    "message": "Başlık büyük harfle başlamalı ve 3-100 karakter arası olmalıdır"
  }
}
```

## MongoDB Storage

**Format:** String

**Örnek:**
```json
{
  "title": "The Great Gatsby",
  "description": "A classic American novel"
}
```

**MongoDB:**
```json
{
  "title": "The Great Gatsby",
  "description": "A classic American novel"
}
```

## Validation Rules

### Desteklenen Kurallar
- `minLength` - Minimum karakter sayısı
- `maxLength` - Maksimum karakter sayısı
- `pattern` - Regex pattern
- `message` - Özel hata mesajı

### Örnekler

**Min/Max Length:**
```json
{
  "validation": {
    "minLength": 3,
    "maxLength": 100
  }
}
```

**Regex Pattern:**
```json
{
  "validation": {
    "pattern": "^[A-Z][a-zA-Z0-9 ]*$",
    "message": "Başlık büyük harfle başlamalıdır"
  }
}
```

**E-posta Validasyonu:**
```json
{
  "validation": {
    "pattern": "^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\\.[a-zA-Z]{2,}$",
    "message": "Geçerli bir e-posta adresi giriniz"
  }
}
```

## Kullanım Senaryoları

### Senaryo 1: Basit Metin Alanı
```json
{
  "fieldType": "text",
  "name": "title",
  "title": "Başlık",
  "mandatory": true
}
```

### Senaryo 2: E-posta Alanı
```json
{
  "fieldType": "text",
  "name": "email",
  "title": "E-posta",
  "mandatory": true,
  "validation": {
    "pattern": "^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\\.[a-zA-Z]{2,}$",
    "message": "Geçerli bir e-posta adresi giriniz"
  }
}
```

### Senaryo 3: Array Text Field
```json
{
  "fieldType": "text",
  "name": "tags",
  "title": "Etiketler",
  "isArray": true,
  "validation": {
    "minItems": 1,
    "maxItems": 10
  }
}
```

**Data:**
```json
{
  "tags": ["fiction", "classic", "american"]
}
```

## Sık Sorulan Sorular

**S: Text field'da maksimum uzunluk var mı?**  
C: MongoDB'de teorik limit yok, ancak pratikte 16MB'dan küçük tutmak önerilir. Validation ile maxLength belirleyebilirsiniz.

**S: Unicode karakterler destekleniyor mu?**  
C: Evet, Türkçe karakterler, emoji, Çince karakterler, vb. desteklenir.

**S: Text field'ı unique yapabilir miyim?**  
C: Evet, `unique: true` yaparak aynı değerin tekrar kullanılmasını engelleyebilirsiniz.

**S: Text field'da arama yapabilir miyim?**  
C: Evet, MongoDB text search veya regex ile arama yapabilirsiniz. Index ekleyerek performansı artırabilirsiniz.

## İlgili Linkler
- [Field-Level Validation](../validations/field-level-validation.md)
- [Dataset Oluşturma](../creating-dataset.md)

---

**Son Güncelleme:** 16 Ocak 2026
