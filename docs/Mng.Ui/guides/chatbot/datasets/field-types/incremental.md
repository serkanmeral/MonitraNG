---
title: "Incremental Field Type"
category: "datasets"
tags: ["dataset", "field-type", "incremental", "auto-increment", "counter"]
service: "MngDataGateway"
difficulty: "advanced"
estimated_time: "10 dakika"
language: "tr"
priority: 1
steps:
  - order: 1
    title: "Incremental Field Tanımla"
    action: "Dataset schema'da incremental field type seçin"
    expected_result: "Incremental field tanımı oluşturulur"
  - order: 2
    title: "Format Template Belirle"
    action: "incrementalOptions.format alanını doldurun"
    expected_result: "Format template tanımlanır"
  - order: 3
    title: "Counter Ayarları"
    action: "startValue ve incrementStep değerlerini ayarlayın"
    expected_result: "Counter ayarları yapılır"
---

# Incremental Field Type

## Özet
Incremental field type, otomatik artan sayılar oluşturmanıza olanak sağlar. Format template desteği ile özelleştirilmiş kodlar (örn: `TASK-000001`, `INV-202511-00001`) oluşturabilirsiniz.

## Özellikler
- ✅ Otomatik artan sayı üretimi
- ✅ Format template desteği (placeholder'lar)
- ✅ Prefix-based counter scope (her prefix ayrı sayaç)
- ✅ Dynamic field reference (diğer field'lardan değer alabilir)
- ✅ Date-based placeholder'lar (year, month, day)
- ✅ Domain-based placeholder'lar

## Field Tanımı

### Minimal Tanım
```json
{
  "fieldType": "incremental",
  "name": "taskNumber",
  "title": "Görev Numarası",
  "mandatory": true,
  "unique": true,
  "incrementalOptions": {
    "format": "TASK-{0:D6}",
    "startValue": 1,
    "incrementStep": 1
  }
}
```

### Tam Tanım
```json
{
  "fieldType": "incremental",
  "name": "taskNumber",
  "title": "Görev Numarası",
  "description": "Otomatik oluşturulan görev numarası",
  "mandatory": true,
  "unique": true,
  "isArray": false,
  "incrementalOptions": {
    "format": "{projectCode}-{year}{month}-{0:D4}",
    "startValue": 1,
    "incrementStep": 1
  }
}
```

**Önemli Kurallar:**
- ✅ `mandatory: true` olmalı (zorunlu)
- ✅ `unique: true` olmalı (benzersiz)
- ✅ `isArray: false` olmalı (array olamaz)
- ✅ `incrementalOptions` zorunlu
- ✅ `incrementalOptions.format` zorunlu

## Format Template Placeholders

### Counter Placeholders
- `{0}` → Counter değeri (zorunlu, her format'ta olmalı)
- `{0:D6}` → Zero-padded counter (6 haneli, örn: 000001)
- `{0:D4}` → Zero-padded counter (4 haneli, örn: 0001)

### Date Placeholders
- `{year}` → Yıl (4 haneli, örn: 2025)
- `{yy}` → Yıl (2 haneli, örn: 25)
- `{month}` → Ay (2 haneli, örn: 01-12)
- `{day}` → Gün (2 haneli, örn: 01-31)

### System Placeholders
- `{domain}` → Domain adı (örn: seven, meral)

### Dynamic Field Reference
- `{fieldName}` → Diğer field'lardan değer alır (örn: `{projectCode}`)

**Örnek:**
```json
{
  "fields": [
    {
      "fieldType": "text",
      "name": "projectCode",
      "mandatory": true
    },
    {
      "fieldType": "incremental",
      "name": "taskNumber",
      "incrementalOptions": {
        "format": "{projectCode}-{0:D6}"  // projectCode field'ından değer alır
      }
    }
  ]
}
```

## Format Örnekleri

### Basit Format
```json
{
  "format": "TASK-{0:D6}"
}
```
**Sonuç:** `TASK-000001`, `TASK-000002`, `TASK-000003`, ...

### Date-Based Format
```json
{
  "format": "INV-{year}{month}-{0:D5}"
}
```
**Sonuç:** `INV-202511-00001`, `INV-202511-00002`, ... (ay değişince sıfırlanır)

### Dynamic Prefix Format
```json
{
  "format": "{projectCode}-{0:D6}"
}
```
**Data:**
```json
{ "projectCode": "GOREV" } → taskNumber: "GOREV-000001"
{ "projectCode": "TASK" }  → taskNumber: "TASK-000001"  // Ayrı sayaç!
```

**Önemli:** Her unique prefix için ayrı sayaç kullanılır!

### Domain-Based Format
```json
{
  "format": "{domain}-TKT-{0:D4}"
}
```
**Sonuç:** `seven-TKT-0001`, `meral-TKT-0001` (domain bazlı)

### Karmaşık Format
```json
{
  "format": "{projectCode}-{year}{month}-{0:D4}"
}
```
**Data:**
```json
{ "projectCode": "GOREV" } → "GOREV-202511-0001"
{ "projectCode": "TASK" }  → "TASK-202511-0001"  // Ayrı sayaç!
```

## Counter Scope (Sayaç Kapsamı)

### Prefix-Based Scope
Her unique resolved prefix için ayrı sayaç kullanılır.

**Örnek:**
```json
// Format: "{projectCode}-{0:D6}"

// Data 1
{ "projectCode": "GOREV" } → "GOREV-000001"
{ "projectCode": "GOREV" } → "GOREV-000002"

// Data 2
{ "projectCode": "TASK" }  → "TASK-000001"   // Ayrı sayaç!
{ "projectCode": "TASK" }  → "TASK-000002"
```

**Counter Storage:**
```json
// @__counters collection
{
  "_id": "@tasks.taskNumber|GOREV-",
  "currentValue": 2,
  "format": "{projectCode}-{0:D6}"
}

{
  "_id": "@tasks.taskNumber|TASK-",
  "currentValue": 2,
  "format": "{projectCode}-{0:D6}"
}
```

### Counter Key Yapısı
```
{datasetName}.{fieldName}|{resolvedPrefix}
```

Örnek:
- `@tasks.taskNumber|GOREV-`
- `@tasks.taskNumber|TASK-`
- `@tasks.taskNumber|INV-202511-`

## IncrementalOptions

### startValue
**Açıklama:** Sayaç başlangıç değeri  
**Varsayılan:** `1`  
**Örnek:**
```json
{
  "startValue": 100  // İlk değer 100'den başlar
}
```

### incrementStep
**Açıklama:** Her artışta kaç artacak  
**Varsayılan:** `1`  
**Örnek:**
```json
{
  "incrementStep": 5  // Her seferinde 5 artar: 1, 6, 11, 16, ...
}
```

### format
**Açıklama:** Format template (placeholder'lar ile)  
**Zorunlu:** Evet  
**Örnekler:**
- `"TASK-{0:D6}"` → `TASK-000001`
- `"{projectCode}-{0:D6}"` → `GOREV-000001` (dynamic)
- `"INV-{year}{month}-{0:D5}"` → `INV-202511-00001` (date-based)

## MongoDB Storage

**Format:** String (format varsa) veya Number (format yoksa)

**Format Yok:**
```json
{
  "incrementalOptions": {
    "format": null,
    "startValue": 1,
    "incrementStep": 1
  }
}
```
**MongoDB:** `{ "taskNumber": 156 }` (number)

**Format Var:**
```json
{
  "incrementalOptions": {
    "format": "TASK-{0:D6}",
    "startValue": 1,
    "incrementStep": 1
  }
}
```
**MongoDB:** `{ "taskNumber": "TASK-000156" }` (string)

## Validation Rules

Incremental field için özel validation kuralları yok. Ancak:
- ✅ `mandatory: true` zorunlu
- ✅ `unique: true` zorunlu
- ✅ `isArray: false` zorunlu
- ✅ Format'taki tüm placeholder'lar resolve edilebilir olmalı

## Kullanım Senaryoları

### Senaryo 1: Basit Task Numarası
**Amaç:** Görev numaraları oluşturma

**Field Tanımı:**
```json
{
  "fieldType": "incremental",
  "name": "taskNumber",
  "title": "Görev Numarası",
  "mandatory": true,
  "unique": true,
  "incrementalOptions": {
    "format": "TASK-{0:D6}",
    "startValue": 1,
    "incrementStep": 1
  }
}
```

**Sonuç:**
- `TASK-000001`
- `TASK-000002`
- `TASK-000003`
- ...

### Senaryo 2: Proje Bazlı Task Numarası
**Amaç:** Her proje için ayrı sayaç

**Field Tanımları:**
```json
{
  "fields": [
    {
      "fieldType": "text",
      "name": "projectCode",
      "title": "Proje Kodu",
      "mandatory": true
    },
    {
      "fieldType": "incremental",
      "name": "taskNumber",
      "title": "Görev Numarası",
      "mandatory": true,
      "unique": true,
      "incrementalOptions": {
        "format": "{projectCode}-{0:D6}",
        "startValue": 1,
        "incrementStep": 1
      }
    }
  ]
}
```

**Data Örnekleri:**
```json
// Proje 1
{ "projectCode": "GOREV" } → taskNumber: "GOREV-000001"
{ "projectCode": "GOREV" } → taskNumber: "GOREV-000002"

// Proje 2 (ayrı sayaç)
{ "projectCode": "TASK" }  → taskNumber: "TASK-000001"
{ "projectCode": "TASK" }  → taskNumber: "TASK-000002"
```

### Senaryo 3: Aylık Invoice Numarası
**Amaç:** Her ay için ayrı sayaç (ay değişince sıfırlanır)

**Field Tanımı:**
```json
{
  "fieldType": "incremental",
  "name": "invoiceNumber",
  "title": "Fatura Numarası",
  "mandatory": true,
  "unique": true,
  "incrementalOptions": {
    "format": "INV-{year}{month}-{0:D5}",
    "startValue": 1,
    "incrementStep": 1
  }
}
```

**Sonuç:**
- Kasım 2025: `INV-202511-00001`, `INV-202511-00002`, ...
- Aralık 2025: `INV-202512-00001`, `INV-202512-00002`, ... (sıfırlandı!)

### Senaryo 4: Domain Bazlı Ticket Numarası
**Amaç:** Her domain için ayrı sayaç

**Field Tanımı:**
```json
{
  "fieldType": "incremental",
  "name": "ticketNumber",
  "title": "Ticket Numarası",
  "mandatory": true,
  "unique": true,
  "incrementalOptions": {
    "format": "{domain}-TKT-{0:D4}",
    "startValue": 1,
    "incrementStep": 1
  }
}
```

**Sonuç:**
- Domain: `seven` → `seven-TKT-0001`
- Domain: `meral` → `meral-TKT-0001` (ayrı sayaç)

### Senaryo 5: Karmaşık Format
**Amaç:** Proje + Tarih + Sayaç kombinasyonu

**Field Tanımları:**
```json
{
  "fields": [
    {
      "fieldType": "text",
      "name": "projectCode",
      "mandatory": true
    },
    {
      "fieldType": "incremental",
      "name": "documentNumber",
      "mandatory": true,
      "unique": true,
      "incrementalOptions": {
        "format": "{projectCode}-{year}{month}-{0:D4}",
        "startValue": 1,
        "incrementStep": 1
      }
    }
  ]
}
```

**Data Örnekleri:**
```json
// Kasım 2025
{ "projectCode": "GOREV" } → "GOREV-202511-0001"
{ "projectCode": "GOREV" } → "GOREV-202511-0002"

// Aralık 2025 (ay değişti, sıfırlandı)
{ "projectCode": "GOREV" } → "GOREV-202512-0001"

// Farklı proje (ayrı sayaç)
{ "projectCode": "TASK" }  → "TASK-202512-0001"
```

## Sık Sorulan Sorular

**S: Format'ta {0} olmadan kullanabilir miyim?**  
C: Hayır, `{0}` zorunludur. Counter değeri için kullanılır.

**S: Format'taki placeholder'lar resolve edilemezse ne olur?**  
C: Hata alırsınız. Tüm placeholder'lar (field reference'lar dahil) mevcut olmalı.

**S: Counter'lar domain bazlı mı?**  
C: Evet, her domain için ayrı sayaç kullanılır. `{domain}` placeholder'ı ile domain adını format'a ekleyebilirsiniz.

**S: Ay değişince sayaç sıfırlanır mı?**  
C: Evet, format'ta `{year}` veya `{month}` varsa, bu değerler değiştiğinde yeni bir prefix oluşur ve sayaç sıfırlanır.

**S: Format değiştirirsem ne olur?**  
C: Yeni format ile yeni prefix'ler oluşur. Eski format ile oluşturulmuş değerler korunur, ancak yeni değerler yeni format ile oluşturulur.

**S: Counter'ı manuel olarak sıfırlayabilir miyim?**  
C: Şu anda manuel sıfırlama yok. Counter'lar `@__counters` collection'ında saklanır, gerekirse manuel olarak güncellenebilir.

**S: Aynı format'ta farklı prefix'ler için ayrı sayaç kullanılır mı?**  
C: Evet! Her unique resolved prefix için ayrı sayaç kullanılır. Örneğin `{projectCode}-{0:D6}` format'ında `GOREV-` ve `TASK-` prefix'leri için ayrı sayaçlar vardır.

## İlgili Linkler
- [Dataset Oluşturma](../creating-dataset.md)
- [Field Types Genel Bakış](../index.md)
- [Unique Index](../indexes/unique-index.md)

---

**Son Güncelleme:** 16 Ocak 2026
