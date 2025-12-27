# Person & PersonGroups Lookup - Tartışma Dokümantasyonu

## 📋 Mevcut Durum

### ✅ Zaten Implement Edilmiş

**AggregatePipelineBuilder.cs:**
- ✅ `AddPersonExpansion(bool expand = true)` metodu var
- ✅ `BuildPersonLookupStage(FieldDefinition field)` metodu var
- ✅ `BuildPersonGroupLookupStage(FieldDefinition field)` metodu var

**DataService.cs:**
- ✅ `QueryAsync` metodunda `AddPersonExpansion(options.Expand)` çağrılıyor
- ✅ `QueryByIdAsync` metodunda `AddPersonExpansion(options.Expand)` çağrılıyor

**Field Type Kontrolü:**
- ✅ `fieldType == "persons"` → `@users` collection'ından lookup
- ✅ `fieldType == "personGroups"` → `@groups` collection'ından lookup

---

## 🔄 Relation FieldType ile Karşılaştırma

### Benzerlikler

| Özellik | Relation | Person/PersonGroups |
|---------|----------|-------------------|
| **Lookup Mekanizması** | ✅ `$lookup` | ✅ `$lookup` |
| **Eşleştirme** | ✅ `__dataId` | ✅ `__dataId` |
| **Array Desteği** | ✅ `$in` pipeline | ✅ `$in` pipeline |
| **Single Desteği** | ✅ `localField/foreignField` | ✅ `localField/foreignField` |
| **Field Exclusion** | ✅ `_id` exclude | ✅ `_id`, `__history` exclude |

### Farklar

| Özellik | Relation | Person/PersonGroups |
|---------|----------|-------------------|
| **Collection** | 🔄 `field.relationDataset` (dynamic) | 🔒 `@users` / `@groups` (fixed) |
| **Circular Reference** | ✅ `maxDepth`, `visitedDatasets` | ❌ Gerekli değil (sabit collection) |
| **Recursive Expansion** | ✅ Nested expansion (maxDepth) | ❌ Tek seviye (sabit collection) |
| **History Exclusion** | ❌ `__history` exclude edilmiyor | ✅ `__history` exclude ediliyor |

---

## 🔍 Mevcut Implementasyon Detayları

### Person Lookup (Single Field)

```csharp
// Single person field
lookup = new BsonDocument
{
    ["from"] = "@users",
    ["localField"] = field.name,           // Örn: "author"
    ["foreignField"] = "__dataId",          // @users.__dataId
    ["as"] = field.name,                    // Sonuç: "author" field'ına yazılır
    ["pipeline"] = new BsonArray
    {
        new BsonDocument
        {
            ["$project"] = new BsonDocument
            {
                ["_id"] = 0,
                ["__history"] = 0
            }
        }
    }
};
```

**Örnek:**
```json
// Input (tst_books collection)
{
  "__dataId": "book-001",
  "title": "The Great Gatsby",
  "author": "690cda3aae502df7d3330bba"  // User ID
}

// Output (after lookup)
{
  "__dataId": "book-001",
  "title": "The Great Gatsby",
  "author": {
    "__dataId": "690cda3aae502df7d3330bba",
    "username": "serkan",
    "email": "serkan@seven.com",
    "firstName": "Serkan",
    "lastName": "MERAL",
    "isActive": true
    // __history exclude edildi
  }
}
```

### Person Lookup (Array Field)

```csharp
// Array persons field
lookup = new BsonDocument
{
    ["from"] = "@users",
    ["let"] = new BsonDocument(field.name, $"${field.name}"),  // Örn: "coAuthors"
    ["pipeline"] = new BsonArray
    {
        new BsonDocument
        {
            ["$match"] = new BsonDocument
            {
                ["$expr"] = new BsonDocument
                {
                    ["$in"] = new BsonArray
                    {
                        "$__dataId",                    // @users.__dataId
                        $"$${field.name}"               // $coAuthors (array)
                    }
                }
            }
        },
        new BsonDocument
        {
            ["$project"] = new BsonDocument
            {
                ["_id"] = 0,
                ["__history"] = 0
            }
        }
    },
    ["as"] = field.name
};
```

**Örnek:**
```json
// Input
{
  "__dataId": "book-001",
  "title": "The Great Gatsby",
  "coAuthors": ["user-id-1", "user-id-2"]  // Array of user IDs
}

// Output
{
  "__dataId": "book-001",
  "title": "The Great Gatsby",
  "coAuthors": [
    {
      "__dataId": "user-id-1",
      "username": "john",
      "email": "john@example.com",
      ...
    },
    {
      "__dataId": "user-id-2",
      "username": "jane",
      "email": "jane@example.com",
      ...
    }
  ]
}
```

### PersonGroups Lookup

**Aynı mantık, sadece collection farklı:**
- `from`: `@groups` (personGroups için)
- `foreignField`: `__dataId` (aynı)
- `localField`: field name (aynı)

---

## 🤔 Tartışma Noktaları

### 1. Relation ile Tutarlılık

**Soru:** Relation expansion'da `__history` exclude edilmiyor, person expansion'da ediliyor. Tutarlılık için relation'da da exclude edilmeli mi?

**Mevcut Durum:**
- **Relation:** `__history` exclude edilmiyor
- **Person/PersonGroups:** `__history` exclude ediliyor

**Öneri:**
- ✅ Person/PersonGroups'da `__history` exclude edilmesi mantıklı (sync edilen veriler, history gereksiz)
- ⚠️ Relation'da da exclude edilebilir (tutarlılık için)

**Karar:** Kullanıcı ile tartışılacak

---

### 2. Lookup Sırası

**Soru:** Person expansion, relation expansion'dan önce mi sonra mı yapılmalı?

**Mevcut Durum:**
```csharp
builder
    .AddMatch(matchFilter)
    .AddRelationExpansion(...)      // 1. Önce relation
    .AddPersonExpansion(...)         // 2. Sonra person
    .AddProject(...)
    .AddSort(...)
    .AddPagination(...);
```

**Pipeline Sırası:**
1. `$match` - Filtreleme
2. `$lookup` (relation) - Relation expansion
3. `$lookup` (person) - Person expansion
4. `$lookup` (personGroups) - PersonGroups expansion
5. `$project` - Field selection
6. `$sort` - Sıralama
7. `$skip` / `$limit` - Pagination

**Öneri:**
- ✅ Mevcut sıra mantıklı (match → expansion → project → sort → pagination)
- ✅ Person expansion, relation expansion'dan sonra yapılabilir (performans açısından fark yok)

**Karar:** Mevcut sıra uygun

---

### 3. Collection İsmi Tutarlılığı

**Soru:** `@users` ve `@groups` collection isimleri sabit. Bu doğru mu?

**Mevcut Durum:**
- ✅ `@users` - Sabit collection ismi
- ✅ `@groups` - Sabit collection ismi
- ✅ Her domain için ayrı database (`mng_{domain}`)

**Öneri:**
- ✅ Sabit collection isimleri doğru (MngKeeper sync mekanizması ile uyumlu)
- ✅ Domain izolasyonu database seviyesinde (`mng_{domain}` database)

**Karar:** Mevcut yaklaşım doğru

---

### 4. Field Type İsimlendirmesi

**Soru:** `fieldType == "persons"` (çoğul) mu yoksa `"person"` (tekil) mu?

**Mevcut Durum:**
- ✅ `"persons"` - Çoğul kullanılıyor
- ✅ `"personGroups"` - Çoğul kullanılıyor

**Öneri:**
- ✅ Çoğul kullanım mantıklı (hem single hem array destekliyor)
- ✅ `"persons"` → single veya array olabilir
- ✅ `"personGroups"` → single veya array olabilir

**Karar:** Mevcut isimlendirme uygun

---

### 5. Lookup Performansı

**Soru:** `@users` ve `@groups` collection'larında index var mı?

**Gereksinimler:**
- ✅ `@users.__dataId` → Unique index (zaten var - BaseEntity)
- ✅ `@groups.__dataId` → Unique index (zaten var - BaseEntity)

**Öneri:**
- ✅ Index'ler mevcut, lookup performansı iyi olmalı
- ⚠️ Büyük array'ler için `$in` pipeline kullanımı optimize edilmiş

**Karar:** Performans sorunu yok

---

## 🎯 Önerilen İyileştirmeler

### 1. Relation Expansion'da `__history` Exclusion

**Öneri:** Relation expansion'da da `__history` exclude edilsin (tutarlılık için)

```csharp
// BuildLookupStage metodunda
new BsonDocument
{
    ["$project"] = new BsonDocument
    {
        ["_id"] = 0,
        ["__history"] = 0  // Eklenecek
    }
}
```

**Avantajlar:**
- ✅ Tutarlılık (person ve relation aynı davranış)
- ✅ Response boyutu küçülür
- ✅ History bilgisi genelde gerekli değil

**Dezavantajlar:**
- ❌ Eğer history gerekirse, `showHistory=true` ile alınamaz (expansion'da)

**Karar:** Kullanıcı ile tartışılacak

---

### 2. Error Handling

**Mevcut Durum:**
```csharp
try
{
    var lookupStage = BuildPersonLookupStage(field);
    if (lookupStage != null)
    {
        _pipeline.Add(lookupStage);
    }
}
catch (Exception ex)
{
    _logger.LogWarning(ex, "Failed to build $lookup stage for persons field {FieldName}", field.name);
}
```

**Öneri:**
- ✅ Mevcut error handling yeterli
- ✅ Lookup başarısız olsa bile, ana sorgu devam eder (field null/empty kalır)

**Karar:** Mevcut yaklaşım uygun

---

### 3. Empty Array Handling

**Soru:** Eğer person/personGroups field'ı null veya empty array ise, lookup ne yapar?

**Mevcut Durum:**
- ✅ `null` → Lookup sonucu `null` (single field)
- ✅ `[]` → Lookup sonucu `[]` (array field)
- ✅ MongoDB `$lookup` bu durumları doğru handle ediyor

**Karar:** Mevcut davranış doğru

---

## 📊 Test Senaryoları

### Test 1: Single Person Field

```http
GET /api/data/tst_books?expand=true&fields=title,author
```

**Beklenen:**
```json
{
  "data": [
    {
      "__dataId": "book-001",
      "title": "The Great Gatsby",
      "author": {
        "__dataId": "user-id-1",
        "username": "serkan",
        "email": "serkan@seven.com",
        "firstName": "Serkan",
        "lastName": "MERAL"
      }
    }
  ]
}
```

### Test 2: Array PersonGroups Field

```http
GET /api/data/tst_books?expand=true&fields=title,reviewerGroups
```

**Beklenen:**
```json
{
  "data": [
    {
      "__dataId": "book-001",
      "title": "The Great Gatsby",
      "reviewerGroups": [
        {
          "__dataId": "group-id-1",
          "name": "editors",
          "description": "Editorial team"
        },
        {
          "__dataId": "group-id-2",
          "name": "reviewers",
          "description": "Review team"
        }
      ]
    }
  ]
}
```

### Test 3: Expand=false

```http
GET /api/data/tst_books?expand=false&fields=title,author,reviewerGroups
```

**Beklenen:**
```json
{
  "data": [
    {
      "__dataId": "book-001",
      "title": "The Great Gatsby",
      "author": "user-id-1",              // ID olarak kalır
      "reviewerGroups": ["group-id-1", "group-id-2"]  // Array olarak kalır
    }
  ]
}
```

---

## ✅ Sonuç

### Mevcut Implementasyon Durumu

**✅ Tamamlanmış:**
- Person expansion (`@users` lookup)
- PersonGroups expansion (`@groups` lookup)
- Single ve array field desteği
- Error handling
- `__history` exclusion

**🤔 Tartışılacak:**
- Relation expansion'da `__history` exclusion (tutarlılık için)
- Lookup sırası (mevcut sıra uygun mu?)

**⏳ Test Edilecek:**
- Single person field expansion
- Array personGroups field expansion
- `expand=false` durumu
- Null/empty array handling

---

## 🎯 Önerilen Adımlar

1. **Mevcut implementasyonu test et**
   - Single person field
   - Array personGroups field
   - `expand=true/false` durumları

2. **Relation expansion'da `__history` exclusion ekle** (tutarlılık için)

3. **Dokümantasyonu güncelle**
   - GET operations roadmap
   - API documentation

4. **Performance test**
   - Büyük array'lerde lookup performansı
   - Index kullanımı kontrolü

---

**Sonuç:** Mevcut implementasyon doğru görünüyor. Sadece relation expansion'da `__history` exclusion eklenebilir (tutarlılık için).

