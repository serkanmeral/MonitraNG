---
title: "Index Types ve Kullanımı"
category: "datasets"
tags: ["dataset", "index", "performance", "query", "optimization"]
service: "MngDataGateway"
difficulty: "intermediate"
estimated_time: "8 dakika"
language: "tr"
priority: 1
steps:
  - order: 1
    title: "Index Tanımı Ekle"
    action: "Dataset schema'da indexList array'ine index ekleyin"
    expected_result: "Index tanımı oluşturulur"
  - order: 2
    title: "Index Türünü Seç"
    action: "Unique veya non-unique, ascending veya descending seçin"
    expected_result: "Index türü belirlenir"
  - order: 3
    title: "Field'ları Belirle"
    action: "Index'lenecek field'ları ve sıralarını belirleyin"
    expected_result: "Index yapılandırılır"
---

# Index Types ve Kullanımı

## Özet
Index'ler, MongoDB sorgularının performansını artırmak için kullanılır. Dataset schema'da index tanımları yapılır, MongoDB'de ilk veri ekleme sırasında otomatik oluşturulur.

## Özellikler
- ✅ Unique index (benzersiz değerler)
- ✅ Non-unique index (duplicate değerlere izin verir)
- ✅ Ascending index (artan sıralama)
- ✅ Descending index (azalan sıralama)
- ✅ Composite index (birden fazla field)

## Index Definition Yapısı

```json
{
  "indexList": [
    {
      "name": "idx_title",
      "fields": {
        "title": 1
      },
      "unique": false
    }
  ]
}
```

## Index Türleri

### 1. Unique Index

**Amaç:** Aynı değere sahip birden fazla kayıt olamaz

**Tanım:**
```json
{
  "name": "idx_isbn",
  "fields": {
    "isbn": 1
  },
  "unique": true
}
```

**Kullanım Senaryoları:**
- ISBN numarası (benzersiz olmalı)
- E-posta adresi (benzersiz olmalı)
- Kullanıcı adı (benzersiz olmalı)
- Incremental field'lar (otomatik unique)

**Örnek:**
```json
// ✅ Geçerli
{ "isbn": "978-0-123456-78-9" }
{ "isbn": "978-0-123456-78-0" }

// ❌ Geçersiz (duplicate)
{ "isbn": "978-0-123456-78-9" }
{ "isbn": "978-0-123456-78-9" }  // Hata: Duplicate key
```

### 2. Non-Unique Index

**Amaç:** Sorgu performansını artırmak (duplicate değerlere izin verir)

**Tanım:**
```json
{
  "name": "idx_title",
  "fields": {
    "title": 1
  },
  "unique": false
}
```

**Kullanım Senaryoları:**
- Arama yapılan field'lar
- Filtreleme yapılan field'lar
- Sıralama yapılan field'lar

**Örnek:**
```json
// ✅ Geçerli (duplicate değerlere izin verir)
{ "title": "The Great Gatsby" }
{ "title": "The Great Gatsby" }  // Aynı başlık olabilir
```

### 3. Ascending Index

**Amaç:** Artan sıralama için optimize edilmiş index

**Tanım:**
```json
{
  "name": "idx_publicationDate",
  "fields": {
    "publicationDate": 1  // 1 = ascending
  },
  "unique": false
}
```

**Kullanım Senaryoları:**
- Tarih sıralaması (eski → yeni)
- Sayısal sıralama (küçük → büyük)
- Alfabetik sıralama (A → Z)

### 4. Descending Index

**Amaç:** Azalan sıralama için optimize edilmiş index

**Tanım:**
```json
{
  "name": "idx_publicationDate",
  "fields": {
    "publicationDate": -1  // -1 = descending
  },
  "unique": false
}
```

**Kullanım Senaryoları:**
- Tarih sıralaması (yeni → eski)
- Sayısal sıralama (büyük → küçük)
- Alfabetik sıralama (Z → A)

### 5. Composite Index

**Amaç:** Birden fazla field içeren index

**Tanım:**
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

**Önemli:** Field sırası önemlidir! (MongoDB index prefix kuralı)

**Kullanım Senaryoları:**
- Çoklu field sorguları
- Çoklu field sıralama
- Karmaşık query'ler

## MongoDB Index Prefix Kuralı

**Kural:** Composite index'te field sırası önemlidir. İlk field'lar prefix oluşturur.

**Örnek:**
```json
{
  "name": "idx_title_date",
  "fields": {
    "title": 1,        // Prefix 1
    "publicationDate": -1  // Prefix 2
  }
}
```

**Bu Index Şu Query'leri Destekler:**
- ✅ `{ title: "..." }` (prefix 1 kullanılır)
- ✅ `{ title: "...", publicationDate: ... }` (tam index kullanılır)
- ❌ `{ publicationDate: ... }` (prefix 2 yok, index kullanılmaz)

**Best Practice:** En çok kullanılan field'ı önce koyun.

## Pratik Örnekler

### Örnek 1: Books Dataset - ISBN Unique Index
```json
{
  "indexList": [
    {
      "name": "idx_isbn",
      "fields": {
        "isbn": 1
      },
      "unique": true
    }
  ]
}
```

**Faydası:**
- ISBN ile hızlı arama
- Duplicate ISBN kontrolü
- Unique constraint

### Örnek 2: Books Dataset - Title Non-Unique Index
```json
{
  "indexList": [
    {
      "name": "idx_title",
      "fields": {
        "title": 1
      },
      "unique": false
    }
  ]
}
```

**Faydası:**
- Title ile hızlı arama
- Title'a göre sıralama
- Title ile filtreleme

### Örnek 3: Books Dataset - Publication Date Descending
```json
{
  "indexList": [
    {
      "name": "idx_publicationDate",
      "fields": {
        "publicationDate": -1
      },
      "unique": false
    }
  ]
}
```

**Faydası:**
- Yeni yayınlar önce (descending)
- Tarih bazlı sorgular
- Tarih bazlı sıralama

### Örnek 4: Books Dataset - Composite Index
```json
{
  "indexList": [
    {
      "name": "idx_publisher_publicationDate",
      "fields": {
        "publisher": 1,
        "publicationDate": -1
      },
      "unique": false
    }
  ]
}
```

**Faydası:**
- Publisher + Publication Date sorguları
- Publisher'a göre gruplama + tarih sıralama
- Karmaşık query'ler

**Query Örnekleri:**
```javascript
// ✅ Index kullanılır
{ publisher: "publisher-001", publicationDate: { $gte: "2020-01-01" } }

// ✅ Index kullanılır (prefix)
{ publisher: "publisher-001" }

// ❌ Index kullanılmaz (prefix değil)
{ publicationDate: { $gte: "2020-01-01" } }
```

### Örnek 5: Tasks Dataset - Task Number Unique
```json
{
  "indexList": [
    {
      "name": "idx_taskNumber",
      "fields": {
        "taskNumber": 1
      },
      "unique": true
    }
  ]
}
```

**Faydası:**
- Incremental field için unique constraint
- Task number ile hızlı arama

## Index Naming Conventions

**Önerilen Format:**
- `idx_{fieldName}` - Single field index
- `idx_{field1}_{field2}` - Composite index
- `idx_{fieldName}_unique` - Unique index (opsiyonel)

**Örnekler:**
- `idx_title`
- `idx_isbn`
- `idx_publisher_publicationDate`
- `idx_taskNumber`

## Index Oluşturma

### Lazy Creation (Otomatik)

**Ne Zaman Oluşturulur:**
- İlk veri ekleme sırasında (POST /api/v1/data/{datasetName})
- Collection yoksa oluşturulur
- Index'ler schema'dan okunur ve MongoDB'de oluşturulur

**Not:** Index tanımları sadece schema'da saklanır. MongoDB'de index oluşturma işlemi ilk veri ekleme sırasında yapılır.

### Manual Creation (Gelecekte)

Gelecekte ayrı bir index management uygulaması ile manuel index oluşturma desteklenebilir.

## Index Best Practices

### 1. Hangi Field'ları Index'lemeli?

**Index'lenmeli:**
- ✅ Sık sorgulanan field'lar
- ✅ Sık sıralanan field'lar
- ✅ Unique constraint gereken field'lar
- ✅ Foreign key'ler (relation field'lar)

**Index'lenmemeli:**
- ❌ Çok sık değişen field'lar (update overhead)
- ❌ Çok büyük değerler (text field'lar - sınırlı)
- ❌ Nadiren sorgulanan field'lar

### 2. Index Sayısı

**Öneri:** Dataset başına 5-10 index yeterlidir. Çok fazla index:
- Write performance'ı düşürür
- Disk alanı kullanır
- Memory kullanır

### 3. Composite Index Field Sırası

**Kural:** En çok kullanılan field'ı önce koyun.

**Örnek:**
```json
// ✅ İyi: publisher daha sık kullanılıyor
{
  "fields": {
    "publisher": 1,
    "publicationDate": -1
  }
}

// ❌ Kötü: publicationDate daha az kullanılıyor
{
  "fields": {
    "publicationDate": -1,
    "publisher": 1
  }
}
```

### 4. Unique Index Kullanımı

**Ne Zaman:**
- ✅ Benzersiz olması gereken değerler (ISBN, e-posta, kullanıcı adı)
- ✅ Incremental field'lar
- ✅ Primary key benzeri field'lar

**Dikkat:**
- Unique index duplicate değerlere izin vermez
- Update işlemlerinde unique constraint kontrol edilir

## Sık Sorulan Sorular

**S: Index'ler ne zaman oluşturulur?**  
C: İlk veri ekleme sırasında otomatik oluşturulur. Schema'da tanımlı index'ler MongoDB'de oluşturulur.

**S: Index'leri manuel oluşturabilir miyim?**  
C: Şu anda sadece otomatik oluşturma var. Gelecekte manuel oluşturma desteği eklenebilir.

**S: Index'leri silebilir miyim?**  
C: Schema'dan index tanımını silerseniz, MongoDB'deki index manuel olarak silinmelidir. Gelecekte otomatik silme desteği eklenebilir.

**S: Composite index'te field sırası önemli mi?**  
C: Evet! MongoDB index prefix kuralı nedeniyle field sırası önemlidir. En çok kullanılan field'ı önce koyun.

**S: Aynı field için birden fazla index oluşturabilir miyim?**  
C: Evet, farklı sıralama (ascending/descending) veya farklı unique ayarları için ayrı index'ler oluşturabilirsiniz.

**S: Index'ler query performance'ı nasıl etkiler?**  
C: Index'ler sorgu hızını önemli ölçüde artırır (özellikle büyük collection'larda). Ancak write işlemlerinde küçük bir overhead oluşturur.

## İlgili Linkler
- [Unique Index](../indexes/unique-index.md)
- [Composite Index](../indexes/composite-index.md)
- [Index Best Practices](../indexes/index-best-practices.md)
- [Dataset Oluşturma](../creating-dataset.md)

---

**Son Güncelleme:** 16 Ocak 2026
