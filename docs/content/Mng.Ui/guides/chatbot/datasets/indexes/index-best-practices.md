---
title: "Index Best Practices"
category: "datasets"
tags: ["dataset", "index", "best-practices", "performance", "optimization"]
service: "MngDataGateway"
difficulty: "intermediate"
estimated_time: "10 dakika"
language: "tr"
priority: 1
---

# Index Best Practices

## Özet
Bu rehber, dataset index'leri için en iyi uygulamaları ve performans optimizasyon önerilerini içerir.

## Genel Prensipler

### 1. Index Ne Zaman Gerekli?

**Index'lenmeli:**
- ✅ Sık sorgulanan field'lar
- ✅ Sık sıralanan field'lar
- ✅ Filtreleme yapılan field'lar
- ✅ Unique constraint gereken field'lar
- ✅ Foreign key'ler (relation field'lar)

**Index'lenmemeli:**
- ❌ Çok sık değişen field'lar (update overhead)
- ❌ Çok büyük değerler (text field'lar - sınırlı)
- ❌ Nadiren sorgulanan field'lar
- ❌ Çok küçük collection'lar (< 1000 kayıt)

### 2. Index Sayısı

**Öneri:** Dataset başına 5-10 index yeterlidir.

**Neden Sınırlı?**
- Write performance'ı düşürür (her insert/update index'leri günceller)
- Disk alanı kullanır
- Memory kullanır

**Kural:** Sadece gerçekten ihtiyaç duyulan field'ları index'leyin.

### 3. Index vs Query Performance

**Index Olmadan:**
- Collection scan (tüm kayıtlar taranır)
- Yavaş (büyük collection'larda)

**Index ile:**
- Index scan (sadece index taranır)
- Hızlı (logarithmic time complexity)

## Field Type'a Göre Index Önerileri

### Text Fields
**Index'lenebilir:**
- ✅ Kısa text field'lar (title, name, code)
- ✅ Sık aranan field'lar
- ✅ Sıralama yapılan field'lar

**Dikkat:**
- ⚠️ Çok uzun text field'lar (description, content) index'lenmemeli
- ⚠️ Text index MongoDB'de özel yapılandırma gerektirir (şu anda desteklenmiyor)

### Number Fields
**Index'lenebilir:**
- ✅ Sık sorgulanan sayısal field'lar
- ✅ Sıralama yapılan field'lar
- ✅ Range query'ler (min/max)

**Örnek:**
```json
{
  "name": "idx_price",
  "fields": { "price": 1 },
  "unique": false
}
```

### DateTime Fields
**Index'lenebilir:**
- ✅ Tarih bazlı sorgular
- ✅ Tarih bazlı sıralama
- ✅ Range query'ler (tarih aralığı)

**Örnek:**
```json
{
  "name": "idx_publicationDate",
  "fields": { "publicationDate": -1 },  // Yeni önce
  "unique": false
}
```

### Relation Fields
**Index'lenmeli:**
- ✅ Foreign key benzeri kullanım
- ✅ Sık join yapılan field'lar
- ✅ Expansion ile sık kullanılan field'lar

**Örnek:**
```json
{
  "name": "idx_publisher",
  "fields": { "publisher": 1 },
  "unique": false
}
```

## Composite Index Best Practices

### 1. Field Sırası

**Kural:** En çok kullanılan field'ı önce koyun.

**İyi Örnek:**
```json
{
  "name": "idx_publisher_publicationDate",
  "fields": {
    "publisher": 1,        // Daha sık kullanılıyor
    "publicationDate": -1
  }
}
```

**Kötü Örnek:**
```json
{
  "name": "idx_publicationDate_publisher",
  "fields": {
    "publicationDate": -1,  // Daha az kullanılıyor
    "publisher": 1
  }
}
```

### 2. Prefix Kuralı

**Kural:** Composite index'te ilk field'lar prefix oluşturur.

**Örnek:**
```json
{
  "fields": {
    "field1": 1,  // Prefix 1
    "field2": 1,  // Prefix 2
    "field3": 1   // Prefix 3
  }
}
```

**Desteklenen Query'ler:**
- ✅ `{ field1: ... }` (prefix 1)
- ✅ `{ field1: ..., field2: ... }` (prefix 2)
- ✅ `{ field1: ..., field2: ..., field3: ... }` (tam index)
- ❌ `{ field2: ... }` (prefix değil)
- ❌ `{ field3: ... }` (prefix değil)

### 3. Index Sayısı

**Öneri:** Composite index sayısını sınırlayın (3-5 arası).

**Neden?**
- Her composite index tüm field'ları içerir
- Write overhead artar
- Disk/memory kullanımı artar

## Unique Index Best Practices

### 1. Ne Zaman Unique?

**Unique Olmalı:**
- ✅ Benzersiz olması gereken değerler (ISBN, e-posta, kullanıcı adı)
- ✅ Incremental field'lar
- ✅ Primary key benzeri field'lar

**Unique Olmamalı:**
- ❌ Sık değişen değerler
- ❌ Null değerlerin çok olduğu field'lar (MongoDB'de birden fazla null kabul edilir)

### 2. Composite Unique Index

**Kullanım:**
- ✅ Birden fazla field kombinasyonu benzersiz olmalı
- ✅ Örnek: Email + Domain

**Dikkat:**
- Field sırası önemlidir
- Tüm field kombinasyonu unique olmalı

## Performance Considerations

### 1. Write Performance

**Etki:**
- Her index write işleminde güncellenir
- Çok fazla index → yavaş write

**Öneri:**
- Sadece gerçekten ihtiyaç duyulan index'leri oluşturun
- Read-heavy workload'larda daha fazla index
- Write-heavy workload'larda daha az index

### 2. Query Performance

**Etki:**
- Index'li field'larda sorgular çok hızlı
- Index'siz field'larda collection scan (yavaş)

**Öneri:**
- Sık sorgulanan field'ları index'leyin
- Query pattern'lerinize göre composite index oluşturun

### 3. Memory Usage

**Etki:**
- Index'ler memory'de tutulur (hot index)
- Çok fazla index → memory kullanımı artar

**Öneri:**
- Index sayısını sınırlayın
- Büyük collection'larda index'leri dikkatli seçin

## Index Naming Conventions

### Önerilen Format

**Single Field:**
```
idx_{fieldName}
```

**Composite:**
```
idx_{field1}_{field2}
```

**Unique:**
```
idx_{fieldName}_unique
```

**Örnekler:**
- `idx_title`
- `idx_isbn`
- `idx_publisher_publicationDate`
- `idx_email_unique`

## Sık Sorulan Sorular

**S: Kaç index oluşturmalıyım?**  
C: Dataset başına 5-10 index yeterlidir. Sadece gerçekten ihtiyaç duyulan field'ları index'leyin.

**S: Index'ler ne zaman oluşturulur?**  
C: İlk veri ekleme sırasında otomatik oluşturulur. Schema'da tanımlı index'ler MongoDB'de oluşturulur.

**S: Index'leri silebilir miyim?**  
C: Schema'dan index tanımını silerseniz, MongoDB'deki index manuel olarak silinmelidir.

**S: Index'leri güncelleyebilir miyim?**  
C: Index tanımını değiştirirseniz, yeni index oluşturulur. Eski index manuel olarak silinmelidir.

**S: Index'ler query'leri otomatik optimize eder mi?**  
C: Evet, MongoDB query optimizer otomatik olarak en uygun index'i seçer.

## İlgili Linkler
- [Index Types](../indexes/index-types.md)
- [Unique Index](../indexes/unique-index.md)
- [Composite Index](../indexes/composite-index.md)
- [Dataset Oluşturma](../creating-dataset.md)

---

**Son Güncelleme:** 16 Ocak 2026
