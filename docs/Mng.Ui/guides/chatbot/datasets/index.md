---
title: "Dataset Field Types ve Özellikleri"
category: "datasets"
tags: ["dataset", "field-types", "overview", "guide"]
service: "MngDataGateway"
difficulty: "beginner"
estimated_time: "5 dakika"
language: "tr"
priority: 1
---

# Dataset Field Types ve Özellikleri

## Özet
Bu rehber, MonitraNG platformunda desteklenen tüm dataset field types ve özelliklerini özetler.

## Desteklenen Field Types (9)

| Field Type | Açıklama | Kullanım | Detaylı Rehber |
|------------|----------|----------|----------------|
| `text` | Metin alanı | String değerler | [Text Field](../field-types/text.md) |
| `number` | Sayısal alan | Integer/Decimal | [Number Field](../field-types/number.md) |
| `bool` | Boolean | true/false | [Bool Field](../field-types/bool.md) |
| `datetime` | Tarih/Saat | ISO 8601 tarih | [DateTime Field](../field-types/datetime.md) |
| `object` | JSON object | Nested object | [Object Field](../field-types/object.md) |
| `relation` | Dataset referansı | MongoDB Lookup | [Relation Field](../field-types/relation.md) |
| `persons` | Kullanıcı referansı | MngKeeper User ID | [Persons Field](../field-types/persons.md) |
| `personGroups` | Grup referansı | MngKeeper Group ID | [PersonGroups Field](../field-types/personGroups.md) |
| `incremental` | Otomatik artan | Format template | [Incremental Field](../field-types/incremental.md) |

## Validation Types

### 1. Field-Level Validation
- **Amaç:** Tek field için basit kurallar
- **Kurallar:** min/max, minLength/maxLength, pattern, minDate/maxDate
- **Detaylı Rehber:** [Field-Level Validation](../validations/field-level-validation.md)

### 2. Expression-Based Validation
- **Amaç:** Birden fazla field arası karmaşık kurallar
- **Syntax:** JavaScript benzeri expression'lar
- **Detaylı Rehber:** [Expression-Based Validation](../validations/expression-validation.md)

### 3. HTTP-Based Validation
- **Amaç:** External API ile validation
- **Kullanım:** Custom validation logic
- **Detaylı Rehber:** [HTTP-Based Validation](../validations/http-validation.md)

## Index Types

### 1. Unique Index
- **Amaç:** Benzersiz değerler
- **Detaylı Rehber:** [Unique Index](../indexes/unique-index.md)

### 2. Non-Unique Index
- **Amaç:** Sorgu performansı
- **Detaylı Rehber:** [Index Types](../indexes/index-types.md)

### 3. Composite Index
- **Amaç:** Çoklu field sorguları
- **Detaylı Rehber:** [Composite Index](../indexes/composite-index.md)

## Örnek Senaryolar

### 1. Books Dataset
- **İçerik:** Tüm field types, validations, indexes
- **Detaylı Rehber:** [Books Dataset Örneği](../examples/books-dataset.md)

### 2. Tasks Dataset
- **İçerik:** Incremental field kullanımı
- **Detaylı Rehber:** [Tasks Dataset Örneği](../examples/tasks-dataset.md)

### 3. Users Dataset
- **İçerik:** Persons field kullanımı
- **Detaylı Rehber:** [Users Dataset Örneği](../examples/users-dataset.md)

## Hızlı Başlangıç

### 1. Dataset Oluşturma
[Dataset Oluşturma Rehberi](../creating-dataset.md)

### 2. Field Ekleme
Her field type için detaylı rehberler yukarıdaki tabloda listelenmiştir.

### 3. Validation Ekleme
[Field-Level Validation](../validations/field-level-validation.md) veya [Expression-Based Validation](../validations/expression-validation.md)

### 4. Index Ekleme
[Index Types](../indexes/index-types.md)

## İlgili Linkler
- [Dataset Oluşturma](../creating-dataset.md)
- [Field Types](../field-types/)
- [Validations](../validations/)
- [Indexes](../indexes/)
- [Örnekler](../examples/)

---

**Son Güncelleme:** 16 Ocak 2026
