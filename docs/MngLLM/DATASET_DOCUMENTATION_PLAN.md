# Dataset Detaylı Dokümantasyon Planı

**Tarih:** 16 Ocak 2026  
**Servis:** MngLLM  
**Amaç:** Dataset'ler için kapsamlı, detaylı dokümantasyon hazırlama planı

---

## 📋 İÇİNDEKİLER

1. [Genel Bakış](#genel-bakış)
2. [Dokümantasyon Yapısı](#dokümantasyon-yapısı)
3. [Field Types Dokümantasyonu](#field-types-dokümantasyonu)
4. [Validation Dokümantasyonu](#validation-dokümantasyonu)
5. [Index Dokümantasyonu](#index-dokümantasyonu)
6. [Örnek Senaryolar](#örnek-senaryolar)
7. [Template ve Standartlar](#template-ve-standartlar)

---

## 🎯 Genel Bakış

### Amaç

Dataset'ler için kapsamlı dokümantasyon hazırlamak:
- ✅ **Field Types:** Her field type için detaylı açıklama ve örnekler
- ✅ **Validations:** Field-level ve expression-based validation kuralları
- ✅ **Indexes:** Index türleri, kullanım senaryoları, best practices
- ✅ **Örnekler:** Pratik, gerçek hayat senaryoları
- ✅ **Chatbot Uyumlu:** Front matter ile chatbot parse edilebilir

### Dokümantasyon Kapsamı

1. **Field Types (9 tip)**
   - text, number, bool, datetime, object
   - relation, persons, personGroups, incremental

2. **Validation Types**
   - Field-level validation (min/max, regex, range, vb.)
   - Expression-based validation (karmaşık kurallar)
   - HTTP-based validation (external validation)

3. **Index Types**
   - Unique index
   - Non-unique index
   - Ascending/Descending index
   - Composite index

4. **Pratik Senaryolar**
   - Books dataset (tam örnek)
   - Tasks dataset (incremental field örneği)
   - Users dataset (persons field örneği)

---

## 📚 Dokümantasyon Yapısı

### Klasör Yapısı

```
docs/Mng.Ui/guides/chatbot/datasets/
├── index.md                          # Dataset genel bakış
├── field-types/
│   ├── text.md                       # Text field type
│   ├── number.md                     # Number field type
│   ├── bool.md                       # Boolean field type
│   ├── datetime.md                   # DateTime field type
│   ├── object.md                     # Object field type
│   ├── relation.md                   # Relation field type
│   ├── persons.md                    # Persons field type
│   ├── personGroups.md               # PersonGroups field type
│   └── incremental.md                # Incremental field type
├── validations/
│   ├── field-level-validation.md     # Field-level validation
│   ├── expression-validation.md      # Expression-based validation
│   └── http-validation.md            # HTTP-based validation
├── indexes/
│   ├── index-types.md                # Index türleri
│   ├── unique-index.md               # Unique index
│   ├── composite-index.md            # Composite index
│   └── index-best-practices.md       # Best practices
└── examples/
    ├── books-dataset.md              # Books dataset tam örneği
    ├── tasks-dataset.md              # Tasks dataset (incremental)
    └── users-dataset.md              # Users dataset (persons)
```

---

## 📝 Field Types Dokümantasyonu

### Template: Field Type Rehberi

Her field type için aynı template kullanılacak:

```markdown
---
title: "[Field Type] Field Type"
category: "datasets"
tags: ["dataset", "field-type", "[field-type]"]
service: "MngDataGateway"
difficulty: "beginner"
estimated_time: "3 dakika"
language: "tr"
priority: 1
---

# [Field Type] Field Type

## Özet
[Field type'ın kısa açıklaması]

## Özellikler
- [Özellik 1]
- [Özellik 2]

## Field Tanımı

### Minimal Tanım
```json
{
  "fieldType": "[field-type]",
  "name": "fieldName",
  "title": "Field Title"
}
```

### Tam Tanım
```json
{
  "fieldType": "[field-type]",
  "name": "fieldName",
  "title": "Field Title",
  "description": "Field açıklaması",
  "mandatory": true,
  "unique": false,
  "isArray": false,
  "validationRules": {
    // Field-specific validation rules
  }
}
```

## MongoDB Storage

**Format:** [MongoDB'de nasıl saklanır]

**Örnek:**
```json
{
  "fieldName": [örnek değer]
}
```

## Validation Rules

### Desteklenen Kurallar
- [Kural 1]
- [Kural 2]

### Örnekler
```json
{
  "validationRules": {
    "[kural]": [değer],
    "[kural]": [değer]
  }
}
```

## Kullanım Senaryoları

### Senaryo 1: [Basit Kullanım]
**Amaç:** [Ne için kullanılır]

**Örnek:**
```json
{
  "fieldType": "[field-type]",
  "name": "example",
  "title": "Example Field"
}
```

### Senaryo 2: [Gelişmiş Kullanım]
**Amaç:** [Ne için kullanılır]

**Örnek:**
```json
{
  "fieldType": "[field-type]",
  "name": "advanced",
  "title": "Advanced Field",
  "validationRules": {
    // ...
  }
}
```

## Sık Sorulan Sorular

**S: [Soru 1]**  
C: [Cevap 1]

**S: [Soru 2]**  
C: [Cevap 2]

## İlgili Linkler
- [İlgili Rehber 1]
- [İlgili Rehber 2]
```

### Field Type Listesi

1. **text.md** - Metin alanları
2. **number.md** - Sayısal alanlar
3. **bool.md** - Boolean (true/false)
4. **datetime.md** - Tarih/Saat
5. **object.md** - JSON object (nested)
6. **relation.md** - Dataset referansı (MongoDB Lookup)
7. **persons.md** - Kullanıcı referansı (MngKeeper)
8. **personGroups.md** - Kullanıcı grubu referansı (MngKeeper)
9. **incremental.md** - Otomatik artan sayı (format template)

---

## ✅ Validation Dokümantasyonu

### 1. Field-Level Validation

**Dosya:** `validations/field-level-validation.md`

**Kapsam:**
- min/max (number)
- minLength/maxLength (text)
- pattern (regex)
- minDate/maxDate (datetime)
- minItems/maxItems (array)

**Örnekler:**
```json
{
  "validationRules": {
    "min": 0,
    "max": 100,
    "minLength": 3,
    "maxLength": 100,
    "pattern": "^[A-Z]",
    "minDate": "2020-01-01",
    "maxDate": "2030-12-31"
  }
}
```

### 2. Expression-Based Validation

**Dosya:** `validations/expression-validation.md`

**Kapsam:**
- Aritmetik işlemler
- Karşılaştırma operatörleri
- Field referansları
- Örnekler: `endDate > startDate`, `price / pageCount <= 10`

**Örnekler:**
```json
{
  "validations": [
    {
      "name": "endDateAfterStartDate",
      "type": "expression",
      "expression": "endDate > startDate",
      "when": "both"
    }
  ]
}
```

### 3. HTTP-Based Validation

**Dosya:** `validations/http-validation.md`

**Kapsam:**
- External API validation
- Request/Response format
- Error handling

**Örnekler:**
```json
{
  "validations": [
    {
      "name": "validateEmail",
      "type": "http",
      "url": "https://api.example.com/validate/email",
      "method": "POST",
      "fields": ["email"],
      "when": "both"
    }
  ]
}
```

---

## 🔍 Index Dokümantasyonu

### 1. Index Types

**Dosya:** `indexes/index-types.md`

**Kapsam:**
- Unique vs Non-Unique
- Ascending vs Descending
- Single vs Composite
- Index naming conventions

### 2. Unique Index

**Dosya:** `indexes/unique-index.md`

**Kapsam:**
- Ne zaman kullanılır
- Duplicate değer kontrolü
- Örnekler

### 3. Composite Index

**Dosya:** `indexes/composite-index.md`

**Kapsam:**
- Field sırası önemi
- MongoDB index prefix kuralı
- Query optimization
- Örnekler

### 4. Index Best Practices

**Dosya:** `indexes/index-best-practices.md`

**Kapsam:**
- Hangi field'ları index'lemeli
- Index sayısı limitleri
- Performance considerations

---

## 📖 Örnek Senaryolar

### 1. Books Dataset (Tam Örnek)

**Dosya:** `examples/books-dataset.md`

**İçerik:**
- Tüm field types örnekleri
- Relation field kullanımı
- Validation kuralları
- Index tanımları
- Tam dataset schema

### 2. Tasks Dataset (Incremental)

**Dosya:** `examples/tasks-dataset.md`

**İçerik:**
- Incremental field kullanımı
- Format template örnekleri
- Counter scope açıklaması
- Dynamic prefix örnekleri

### 3. Users Dataset (Persons)

**Dosya:** `examples/users-dataset.md`

**İçerik:**
- Persons field kullanımı
- PersonGroups field kullanımı
- MngKeeper entegrasyonu

---

## 🛠️ Template ve Standartlar

### Field Type Template

Her field type için standart template kullanılacak (yukarıda belirtildi).

### Validation Template

```markdown
---
title: "[Validation Type] Validation"
category: "datasets"
tags: ["dataset", "validation", "[validation-type]"]
service: "MngDataGateway"
difficulty: "intermediate"
estimated_time: "5 dakika"
language: "tr"
priority: 1
---

# [Validation Type] Validation

## Özet
[Validation type'ın açıklaması]

## Kullanım Senaryoları
[Ne zaman kullanılır]

## Syntax
[Syntax açıklaması]

## Örnekler
[Pratik örnekler]

## Hata Mesajları
[Olası hata mesajları ve çözümleri]
```

### Index Template

```markdown
---
title: "[Index Type] Index"
category: "datasets"
tags: ["dataset", "index", "[index-type]"]
service: "MngDataGateway"
difficulty: "intermediate"
estimated_time: "5 dakika"
language: "tr"
priority: 1
---

# [Index Type] Index

## Özet
[Index type'ın açıklaması]

## Ne Zaman Kullanılır
[Kullanım senaryoları]

## Tanım
[Index tanım formatı]

## Örnekler
[Pratik örnekler]

## Best Practices
[En iyi uygulamalar]
```

---

## 📅 Implementasyon Planı

### Faz 1: Field Types (1 hafta)

**Görevler:**
1. ✅ Template oluştur
2. 📋 text.md hazırla
3. 📋 number.md hazırla
4. 📋 bool.md hazırla
5. 📋 datetime.md hazırla
6. 📋 object.md hazırla
7. 📋 relation.md hazırla
8. 📋 persons.md hazırla
9. 📋 personGroups.md hazırla
10. 📋 incremental.md hazırla (en detaylı)

**Öncelik:** Incremental field en önemli (en karmaşık)

### Faz 2: Validations (3-4 gün)

**Görevler:**
1. 📋 field-level-validation.md
2. 📋 expression-validation.md
3. 📋 http-validation.md

### Faz 3: Indexes (2-3 gün)

**Görevler:**
1. 📋 index-types.md
2. 📋 unique-index.md
3. 📋 composite-index.md
4. 📋 index-best-practices.md

### Faz 4: Örnek Senaryolar (2-3 gün)

**Görevler:**
1. 📋 books-dataset.md (tam örnek)
2. 📋 tasks-dataset.md (incremental)
3. 📋 users-dataset.md (persons)

---

## ✅ Sonuç

### Dokümantasyon Hedefleri

1. ✅ **Kapsamlı:** Tüm field types, validations, indexes
2. ✅ **Detaylı:** Her özellik için açıklama ve örnekler
3. ✅ **Pratik:** Gerçek hayat senaryoları
4. ✅ **Chatbot Uyumlu:** Front matter ile parse edilebilir
5. ✅ **İnsan Okunabilir:** MkDocs ile render edilebilir

### Sonraki Adımlar

1. ✅ Dataset dokümantasyon planı hazırlandı
2. 📋 Field types dokümantasyonlarına başla
3. 📋 Validations dokümantasyonlarına başla
4. 📋 Indexes dokümantasyonlarına başla
5. 📋 Örnek senaryoları hazırla

---

**Son Güncelleme:** 16 Ocak 2026
