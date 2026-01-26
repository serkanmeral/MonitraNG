# Next Session Roadmap - MngDataGateway

**Tarih:** 25 Aralık 2025  
**Durum:** Planlama Aşaması

---

## 🎯 Yapılacaklar Listesi

### 1. ✅ Predefined Queries Sorununun Çözülmesi

**Sorun:**
- `InvalidCastException: Unable to cast object of type 'MongoDB.Bson.BsonDocument' to type 'MongoDB.Bson.BsonBoolean'`
- Predefined query execution sırasında pipeline parse hatası
- Özellikle `$sort` stage'indeki sayısal değerler (1, -1) boolean olarak algılanıyor

**Çözüm Adımları:**
- [ ] Pipeline stage'lerinin BsonDocument olarak doğru parse edilmesi
- [ ] `$sort` stage'indeki sayısal değerlerin korunması
- [ ] Parameter replacement sırasında BsonValue tiplerinin korunması
- [ ] Test: `books_by_publication_date_range` query'sinin çalışması

**Durum:** Devam ediyor - Backend'de InvalidCastException hatası var

---

### 2. 📋 Query Parameter Type Definitions

**Amaç:** Predefined query'lerde parametrelerin tip tanımlamalarını yapabilmek

**Desteklenecek Parametre Tipleri:**
- `number` - Sayısal değerler (int, long, double)
- `text` - String değerler
- `boolean` - Boolean değerler (true/false)
- `datetime` - Tarih/saat değerleri

**Tartışılacak Konular:**
- [ ] Schema'da parametre tip tanımlaması nasıl yapılacak?
- [ ] Request body'den gelen parametrelerin tip kontrolü
- [ ] Tip dönüşümü (string → number, string → datetime, vb.)
- [ ] Hata mesajları (yanlış tip gönderildiğinde)
- [ ] Örnek: `{ "startDate": "2025-01-01T00:00:00Z", "endDate": "2025-12-31T23:59:59Z" }` → datetime tipinde

**Örnek Schema Tanımı:**
```json
{
  "name": "books_by_publication_date_range",
  "parameters": [
    { "name": "startDate", "type": "datetime" },
    { "name": "endDate", "type": "datetime" }
  ],
  "pipeline": [...]
}
```

---

### 3. 🔍 Search Functionality

**Amaç:** Dataset'lerde full-text search veya gelişmiş arama özelliği

**Tartışılacak Konular:**
- [ ] MongoDB text index kullanımı
- [ ] Full-text search endpoint'i (`POST /api/data/{datasetName}/search`)
- [ ] Arama algoritması (exact match, contains, starts with, vb.)
- [ ] Multi-field search (birden fazla field'da arama)
- [ ] Search result ranking/scoring
- [ ] Search history/cache (opsiyonel)
- [ ] Performance optimizasyonu (index kullanımı)

**Örnek Kullanım:**
```
POST /api/data/tst_books/search
Body: {
  "query": "harry potter",
  "fields": ["title", "name", "subtitle"],
  "limit": 50
}
```

---

### 4. 📊 CSV Export Functionality

**Amaç:** Dataset verilerini CSV formatında export etme

**Tartışılacak Konular:**
- [ ] CSV export endpoint'i (`GET /api/data/{datasetName}/export?format=csv`)
- [ ] Field selection (hangi field'lar export edilecek)
- [ ] Pagination (büyük dataset'ler için)
- [ ] Filtering (export edilecek verilerin filtrelenmesi)
- [ ] Relation expansion (export sırasında relation'lar expand edilecek mi?)
- [ ] CSV encoding (UTF-8, UTF-8 BOM, vb.)
- [ ] Streaming (büyük dataset'ler için chunk'lar halinde)
- [ ] Excel export (opsiyonel - gelecekte)

**Örnek Kullanım:**
```
GET /api/data/tst_books/export?format=csv&fields=title,price,author&filter=price:gt:200
```

---

### 5. ✅ Validation İşlemleri

**Amaç:** Data validation mekanizmasını geliştirmek ve genişletmek

**Tartışılacak Konular:**
- [ ] Field-level validation rules
  - Min/Max değerler (number, text length)
  - Regex patterns (text fields)
  - Custom validation functions
  - Conditional validation (field A varsa field B zorunlu)
- [ ] Dataset-level validation
  - Cross-field validation
  - Business rule validation
- [ ] Validation error messages
  - Kullanıcı dostu hata mesajları
  - Field bazlı hata mesajları
  - Çoklu hata mesajları
- [ ] Validation timing
  - Create sırasında validation
  - Update sırasında validation
  - Bulk operations sırasında validation
- [ ] Custom validators
  - Plugin-based validation system
  - External validation services

**Örnek Validation Rules:**
```json
{
  "name": "price",
  "fieldType": "number",
  "validation": {
    "min": 0,
    "max": 10000,
    "required": true
  }
}
```

---

### 6. 🌐 API Gateway Servisi

**Amaç:** Farklı bir servis olarak API Gateway geliştirmek

**Tartışılacak Konular:**
- [ ] API Gateway'in amacı ve faydaları
- [ ] Mimari yaklaşım
  - Centralized API Gateway
  - Microservices routing
  - Request/Response transformation
- [ ] Özellikler
  - Request routing (MngKeeper, MngDataGateway, MngHub, vb.)
  - Authentication/Authorization (JWT validation)
  - Rate limiting
  - Request/Response logging
  - API versioning
  - CORS handling
  - Load balancing
- [ ] Teknoloji seçimi
  - Ocelot (ASP.NET Core)
  - YARP (Yet Another Reverse Proxy)
  - Custom implementation
- [ ] Service discovery
  - Static configuration
  - Dynamic service discovery (consul, etcd, vb.)
- [ ] Monitoring ve observability
  - Request metrics
  - Error tracking
  - Performance monitoring

**Örnek Mimari:**
```
Client → API Gateway → MngKeeper
                      → MngDataGateway
                      → MngHub
                      → (Future services)
```

---

## 📝 Notlar

### Öncelik Sırası
1. **Yüksek Öncelik:**
   - Predefined queries sorununun çözülmesi
   - Query parameter type definitions

2. **Orta Öncelik:**
   - Search functionality
   - CSV export

3. **Düşük Öncelik:**
   - Validation işlemleri (mevcut validation genişletilecek)
   - API Gateway servisi (yeni servis, ayrı proje)

### İlgili Dosyalar
- `MngDataGateway/docs/GET_OPERATIONS_ROADMAP.md` - GET operations roadmap
- `MngDataGateway/docs/ROADMAP.md` - Genel roadmap
- `MngDataGateway/Infrastructure/MngDataGateway.Persistence/Services/DataService.cs` - Predefined query execution

---

**Hazırlayan:** AI Assistant  
**Tarih:** 25 Aralık 2025  
**Durum:** Planlama Aşaması

