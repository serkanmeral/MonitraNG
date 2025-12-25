# Books Dataset Örneği - Planlama Dokümanı

**Tarih:** 30 Aralık 2025  
**Durum:** 📋 Planlama Aşaması  
**Hedef:** Books dataset örneği ile persons/personGroups field type implementasyonu

---

## 📚 Dataset Yapısı

### 0. Book Categories Category (Dataset Category)

**Amaç:** Books ile ilgili tüm dataset'leri gruplamak

**Category Oluşturma:**
```json
POST /api/dataset-categories
{
  "name": "Book Categories",
  "description": "Category for book-related datasets (publishers, genres, books)",
  "isActive": true
}
```

**Örnek Response:**
```json
{
  "__dataId": "category-book-001",
  "name": "Book Categories",
  "description": "Category for book-related datasets",
  "isActive": true,
  "__createInfo": {
    "createdAt": "2025-12-30T00:00:00Z",
    "userInfo": {
      "uid": "...",
      "userName": "serkan",
      "domain": "seven"
    }
  }
}
```

**Not:** Bu category ID'si (`category-book-001`) tüm books dataset'lerinde kullanılacak.

---

### 1. Publishers Dataset (Lookup Dataset)

**Amaç:** Yayıncı bilgilerini tutmak

**Collection:** `tst_publishers`

**Field'lar:**
```json
{
  "name": "tst_publishers",
  "description": "Book publishers dataset (test)",
  "category": "category-book-001",
  "forceSchema": true,
  "logging": "none",
  "publish_mode": "none",
  "fields": [
    {
      "fieldType": "text",
      "name": "name",
      "title": "Publisher Name",
      "mandatory": true,
      "unique": true
    },
    {
      "fieldType": "text",
      "name": "website",
      "title": "Website",
      "mandatory": false,
      "unique": false
    },
    {
      "fieldType": "text",
      "name": "country",
      "title": "Country",
      "mandatory": false,
      "unique": false
    }
  ],
  "indexList": [
    {
      "name": "idx_name",
      "fields": { "name": 1 },
      "unique": true
    }
  ]
}
```

**Örnek Data:**
```json
{
  "__dataId": "publisher-001",
  "name": "Penguin Random House",
  "website": "https://www.penguinrandomhouse.com",
  "country": "USA"
}
```

---

### 2. Genres Dataset (Lookup Dataset)

**Amaç:** Kitap türlerini tutmak

**Collection:** `tst_genres`

**Field'lar:**
```json
{
  "name": "tst_genres",
  "description": "Book genres dataset (test)",
  "category": "category-book-001",
  "forceSchema": true,
  "logging": "none",
  "publish_mode": "basic",
  "fields": [
    {
      "fieldType": "text",
      "name": "name",
      "title": "Genre Name",
      "mandatory": true,
      "unique": true
    },
    {
      "fieldType": "text",
      "name": "description",
      "title": "Description",
      "mandatory": false,
      "unique": false
    }
  ],
  "indexList": [
    {
      "name": "idx_name",
      "fields": { "name": 1 },
      "unique": true
    }
  ]
}
```

**Örnek Data:**
```json
{
  "__dataId": "genre-001",
  "name": "Science Fiction",
  "description": "Futuristic and science-based fiction"
}
```

---

### 3. Books Dataset (Ana Dataset)

**Amaç:** Kitap bilgilerini tutmak

**Collection:** `tst_books`

**Field'lar:**
```json
{
  "name": "tst_books",
  "description": "Books dataset with relations and person fields (test)",
  "category": "category-book-001",
  "forceSchema": true,
  "logging": "self",
  "publish_mode": "full",
  "fields": [
    {
      "fieldType": "incremental",
      "name": "isbn",
      "title": "ISBN",
      "mandatory": true,
      "unique": true,
      "incrementalOptions": {
        "format": "ISBN-{year}-{0:D6}",
        "startValue": 1,
        "incrementStep": 1
      }
    },
    {
      "fieldType": "incremental",
      "name": "bookCode",
      "title": "Book Code",
      "mandatory": false,
      "unique": true,
      "incrementalOptions": {
        "format": "BK-{yy}{month}-{0:D4}",
        "startValue": 1,
        "incrementStep": 1
      }
    },
    {
      "fieldType": "text",
      "name": "publisherCode",
      "title": "Publisher Code",
      "mandatory": false,
      "unique": false
    },
    {
      "fieldType": "incremental",
      "name": "internalBookNumber",
      "title": "Internal Book Number",
      "mandatory": false,
      "unique": true,
      "incrementalOptions": {
        "format": "{publisherCode}-{year}-{0:D5}",
        "startValue": 1,
        "incrementStep": 1
      }
    },
    {
      "fieldType": "incremental",
      "name": "sequenceNumber",
      "title": "Sequence Number",
      "mandatory": false,
      "unique": true,
      "incrementalOptions": {
        "format": "{domain}-BOOK-{0:D6}",
        "startValue": 1000,
        "incrementStep": 10
      }
    },
    {
      "fieldType": "text",
      "name": "name",
      "title": "Book Name",
      "mandatory": false,
      "unique": false
    },
    {
      "fieldType": "text",
      "name": "title",
      "title": "Book Title",
      "mandatory": true,
      "unique": false
    },
    {
      "fieldType": "text",
      "name": "subtitle",
      "title": "Subtitle",
      "mandatory": false,
      "unique": false
    },
    {
      "fieldType": "relation",
      "name": "publisher",
      "title": "Publisher",
      "mandatory": true,
      "unique": false,
      "isArray": false,
      "relationDataset": "tst_publishers",
      "relationField": "__dataId"
    },
    {
      "fieldType": "relation",
      "name": "genres",
      "title": "Genres",
      "mandatory": false,
      "unique": false,
      "isArray": true,
      "relationDataset": "tst_genres",
      "relationField": "__dataId"
    },
    {
      "fieldType": "persons",
      "name": "author",
      "title": "Author",
      "mandatory": true,
      "unique": false,
      "isArray": false
    },
    {
      "fieldType": "persons",
      "name": "coAuthors",
      "title": "Co-Authors",
      "mandatory": false,
      "unique": false,
      "isArray": true
    },
    {
      "fieldType": "personGroups",
      "name": "reviewerGroups",
      "title": "Reviewer Groups",
      "mandatory": false,
      "unique": false,
      "isArray": true
    },
    {
      "fieldType": "personGroups",
      "name": "editorialTeam",
      "title": "Editorial Team",
      "mandatory": false,
      "unique": false,
      "isArray": false
    },
    {
      "fieldType": "number",
      "name": "pageCount",
      "title": "Page Count",
      "mandatory": false,
      "unique": false
    },
    {
      "fieldType": "datetime",
      "name": "publicationDate",
      "title": "Publication Date",
      "mandatory": false,
      "unique": false
    },
    {
      "fieldType": "text",
      "name": "language",
      "title": "Language",
      "mandatory": false,
      "unique": false
    },
    {
      "fieldType": "number",
      "name": "price",
      "title": "Price",
      "mandatory": false,
      "unique": false
    },
    {
      "fieldType": "object",
      "name": "coverImage",
      "title": "Cover Image",
      "mandatory": false,
      "unique": false,
      "objectSchema": {
        "url": "text",
        "alt": "text",
        "width": "number",
        "height": "number"
      }
    }
  ],
  "indexList": [
    {
      "name": "idx_isbn",
      "fields": { "isbn": 1 },
      "unique": true
    },
    {
      "name": "idx_bookCode",
      "fields": { "bookCode": 1 },
      "unique": true
    },
    {
      "name": "idx_internalBookNumber",
      "fields": { "internalBookNumber": 1 },
      "unique": true
    },
    {
      "name": "idx_sequenceNumber",
      "fields": { "sequenceNumber": 1 },
      "unique": true
    },
    {
      "name": "idx_name",
      "fields": { "name": 1 },
      "unique": true
    },
    {
      "name": "idx_title",
      "fields": { "title": 1 },
      "unique": false
    },
    {
      "name": "idx_title_bookCode",
      "fields": { "title": 1, "bookCode": 1 },
      "unique": false
    },
    {
      "name": "idx_publisher",
      "fields": { "publisher": 1 },
      "unique": false
    },
    {
      "name": "idx_author",
      "fields": { "author": 1 },
      "unique": false
    },
    {
      "name": "idx_publicationDate",
      "fields": { "publicationDate": -1 },
      "unique": false
    }
  ],
  "queries": [
    {
      "name": "books_by_publication_date_range",
      "description": "Get books published between two dates",
      "parameters": ["startDate", "endDate"],
      "pipeline": [
        {
          "$match": {
            "publicationDate": {
              "$gte": ":startDate",
              "$lte": ":endDate"
            }
          }
        },
        {
          "$sort": {
            "publicationDate": -1,
            "title": 1
          }
        }
      ]
    }
  ],
  "permissions": {
    "read": {
      "groups": ["managers"],
      "users": []
    },
    "write": {
      "groups": ["managers"],
      "users": []
    },
    "create": {
      "groups": ["managers"],
      "users": []
    },
    "update": {
      "groups": ["managers"],
      "users": []
    },
    "delete": {
      "groups": ["managers"],
      "users": []
    }
  }
}
```

---

## 🔗 İlişkiler (Relations)

### 1. Books → Publishers (1-to-Many)

**Tip:** `relation` field type  
**Yapı:** Bir kitap tek bir yayıncıya ait  
**Field:** `publisher` (single, mandatory)

**Örnek:**
```json
{
  "__dataId": "book-001",
  "title": "The Great Gatsby",
      "publisher": "publisher-001"  // Reference to tst_publishers.__dataId
}
```

**Query'de Expansion:**
```http
GET /api/data/tst_books?expand=publisher
```

**Response (expanded):**
```json
{
  "__dataId": "book-001",
  "title": "The Great Gatsby",
  "publisher": {
    "__dataId": "publisher-001",
    "name": "Penguin Random House",
    "website": "https://www.penguinrandomhouse.com"
  }
}
```

---

### 2. Books → Genres (Many-to-Many)

**Tip:** `relation` field type  
**Yapı:** Bir kitap birden fazla türe ait olabilir  
**Field:** `genres` (array, optional)

**Örnek:**
```json
{
  "__dataId": "book-001",
  "title": "The Great Gatsby",
      "genres": ["genre-001", "genre-002"]  // Array of tst_genres.__dataId
}
```

**Query'de Expansion:**
```http
GET /api/data/tst_books?expand=genres
```

**Response (expanded):**
```json
{
  "__dataId": "book-001",
  "title": "The Great Gatsby",
  "genres": [
    {
      "__dataId": "genre-001",
      "name": "Science Fiction"
    },
    {
      "__dataId": "genre-002",
      "name": "Adventure"
    }
  ]
}
```

---

## 👤 Persons & PersonGroups Field Types

### 3. Books → Authors (Persons Field Type)

**Tip:** `persons` field type (yeni özellik - Phase 3)  
**Yapı:** Yazar bilgileri MngKeeper'dan alınır  
**Field:** `author` (single, mandatory), `coAuthors` (array, optional)

**Örnek Data (MngKeeper User ID'leri):**
```json
{
  "__dataId": "book-001",
  "title": "The Great Gatsby",
  "author": "690cdb7fae502df7d3330bbb",  // MngKeeper User ID (serkan)
  "coAuthors": [
    "user-id-002",
    "user-id-003"
  ]
}
```

**Beklenen Davranış:**
- `persons` field type için MngKeeper API entegrasyonu gerekli
- User ID doğrulama (user exists check)
- Expansion ile user bilgileri getirilebilir
- Cache mekanizması (TTL: 5 dakika)

---

### 4. Books → Reviewer Groups & Editorial Team (PersonGroups Field Type)

**Tip:** `personGroups` field type (yeni özellik - Phase 3)  
**Yapı:** Grup bilgileri MngKeeper'dan alınır  
**Field:** `reviewerGroups` (array, optional), `editorialTeam` (single, optional)

**Örnek Data (MngKeeper Group ID'leri):**
```json
{
  "__dataId": "book-001",
  "title": "The Great Gatsby",
  "reviewerGroups": ["group-id-001", "group-id-002"],  // MngKeeper Group ID'leri
  "editorialTeam": "group-id-003"  // MngKeeper Group ID
}
```

**Beklenen Davranış:**
- `personGroups` field type için MngKeeper API entegrasyonu gerekli
- Group ID doğrulama (group exists check)
- Expansion ile group bilgileri getirilebilir
- Cache mekanizması (TTL: 5 dakika)

**Query'de Expansion (planlanan):**
```http
GET /api/data/tst_books?expand=author,coAuthors,reviewerGroups,editorialTeam
```

**Response (expanded - planlanan):**
```json
{
  "__dataId": "book-001",
  "title": "The Great Gatsby",
  "author": {
    "id": "690cdb7fae502df7d3330bbb",
    "username": "serkan",
    "email": "serkan@seven.com",
    "firstName": "Serkan",
    "lastName": "MERAL",
    "isActive": true
  },
  "coAuthors": [
    {
      "id": "user-id-002",
      "username": "john.doe",
      "email": "john@seven.com",
      "firstName": "John",
      "lastName": "Doe"
    }
  ],
  "reviewerGroups": [
    {
      "id": "group-id-001",
      "name": "Reviewers",
      "description": "Book reviewers group",
      "isActive": true
    },
    {
      "id": "group-id-002",
      "name": "Quality Control",
      "description": "QC team",
      "isActive": true
    }
  ],
  "editorialTeam": {
    "id": "group-id-003",
    "name": "Editorial Team",
    "description": "Editorial staff",
    "isActive": true
  }
}
```

---

## 📊 Örnek Veri Seti

### Publishers Data

```json
[
  {
    "__dataId": "publisher-001",
    "name": "Penguin Random House",
    "website": "https://www.penguinrandomhouse.com",
    "country": "USA"
  },
  {
    "__dataId": "publisher-002",
    "name": "HarperCollins",
    "website": "https://www.harpercollins.com",
    "country": "USA"
  },
  {
    "__dataId": "publisher-003",
    "name": "Can Yayınları",
    "website": "https://www.canyayinlari.com",
    "country": "Turkey"
  }
]
```

### Genres Data

```json
[
  {
    "__dataId": "genre-001",
    "name": "Fiction",
    "description": "Literary works of fiction"
  },
  {
    "__dataId": "genre-002",
    "name": "Science Fiction",
    "description": "Futuristic and science-based fiction"
  },
  {
    "__dataId": "genre-003",
    "name": "Adventure",
    "description": "Adventure and action stories"
  },
  {
    "__dataId": "genre-004",
    "name": "Biography",
    "description": "Biographical works"
  }
]
```

### Books Data

```json
[
  {
    "__dataId": "book-001",
    "isbn": "ISBN-2025-000001",
    "bookCode": "BK-2512-0001",
    "publisherCode": "PRH",
    "internalBookNumber": "PRH-2025-00001",
    "sequenceNumber": "seven-BOOK-001000",
    "title": "The Great Gatsby",
    "subtitle": "A Novel",
    "publisher": "publisher-001",
    "genres": ["genre-001", "genre-003"],
    "author": "690cdb7fae502df7d3330bbb",  // serkan user ID
    "coAuthors": [],
    "reviewerGroups": ["group-id-001"],
    "editorialTeam": "group-id-003",
    "pageCount": 180,
    "publicationDate": "1925-04-10T00:00:00Z",
    "language": "English",
    "price": 29.99,
    "coverImage": {
      "url": "https://example.com/covers/great-gatsby.jpg",
      "alt": "The Great Gatsby Cover",
      "width": 400,
      "height": 600
    }
  },
  {
    "__dataId": "book-002",
    "isbn": "ISBN-2025-000002",
    "bookCode": "BK-2512-0002",
    "publisherCode": "PRH",
    "internalBookNumber": "PRH-2025-00002",
    "sequenceNumber": "seven-BOOK-001010",
    "title": "1984",
    "subtitle": null,
    "publisher": "publisher-001",
    "genres": ["genre-002"],
    "author": "690cdb7fae502df7d3330bbb",
    "coAuthors": ["user-id-002"],
    "reviewerGroups": ["group-id-001", "group-id-002"],
    "editorialTeam": "group-id-003",
    "pageCount": 328,
    "publicationDate": "1949-06-08T00:00:00Z",
    "language": "English",
    "price": 24.99,
    "coverImage": null
  }
]
```

---

## 🎯 Implementasyon Planı

### Phase 1: Dataset Category ve Schema Oluşturma

**Öncelik:** Yüksek  
**Süre:** ~35 dakika

**Görevler:**
1. [ ] "Book Categories" dataset category oluştur
2. [ ] Category ID'sini al ve kaydet
3. [ ] Publishers dataset schema oluştur (category ile)
4. [ ] Genres dataset schema oluştur (category ile)
5. [ ] Books dataset schema oluştur (category + relation fields + queries + permissions ile)
6. [ ] Test: Category'yi API ile oluştur
7. [ ] Test: Schema'ları API ile oluştur
8. [ ] Test: Schema'ları MongoDB'de doğrula (category field'ı, queries ve permissions kontrol)

**Test Script:** `tests/setup-books-datasets.ps1`

---

### Phase 2: Lookup Data Oluşturma

**Öncelik:** Yüksek  
**Süre:** ~15 dakika

**Görevler:**
1. [ ] Publishers için örnek data ekle (3-5 publisher)
2. [ ] Genres için örnek data ekle (4-6 genre)
3. [ ] Test: Data'ları API ile oluştur
4. [ ] Test: Data'ları MongoDB'de doğrula

**Test Script:** `tests/load-books-lookup-data.ps1`

---

### Phase 3: Books Data Oluşturma (Relation Fields)

**Öncelik:** Yüksek  
**Süre:** ~20 dakika

**Görevler:**
1. [ ] Books için örnek data ekle (publisher relation ile)
2. [ ] Books için örnek data ekle (genres relation ile - array)
3. [ ] Test: Relation field'ları doğru şekilde kaydediliyor mu?
4. [ ] Test: GET operations ile expansion testi

**Test Script:** `tests/load-books-data.ps1`

---

### Phase 4: Persons Field Type Implementasyonu

**Öncelik:** Orta (yeni özellik)  
**Süre:** ~2-3 saat

**Görevler:**

#### 4.1 MngKeeper API Entegrasyonu
1. [ ] `IPersonService` interface oluştur
2. [ ] `PersonService` implementasyonu (MngKeeper API client)
3. [ ] User ID validation (user exists check)
4. [ ] User data caching (TTL: 5 dakika)

#### 4.2 Persons & PersonGroups Field Type Validation
1. [ ] `persons` field type validation ekle
2. [ ] Single person validation (mandatory check)
3. [ ] Array of persons validation
4. [ ] User ID format validation
5. [ ] `personGroups` field type validation ekle
6. [ ] Single group validation
7. [ ] Array of groups validation
8. [ ] Group ID format validation

#### 4.3 Persons & PersonGroups Field Type Expansion
1. [ ] Persons expansion logic'i `AggregatePipelineBuilder`'a ekle
2. [ ] MngKeeper API'den user bilgilerini çek
3. [ ] Cache'den user bilgilerini getir
4. [ ] Response'a user bilgilerini ekle
5. [ ] PersonGroups expansion logic'i `AggregatePipelineBuilder`'a ekle
6. [ ] MngKeeper API'den group bilgilerini çek
7. [ ] Cache'den group bilgilerini getir
8. [ ] Response'a group bilgilerini ekle

#### 4.4 Books Data ile Test
1. [ ] Books schema'ya `author`, `coAuthors`, `reviewerGroups`, `editorialTeam` field'ları ekle
2. [ ] Books data oluştur (persons ve personGroups field'ları ile)
3. [ ] Test: GET books with author expansion
4. [ ] Test: GET books with coAuthors expansion
5. [ ] Test: GET books with reviewerGroups expansion
6. [ ] Test: GET books with editorialTeam expansion
7. [ ] Test: GET books with all expansions (persons + personGroups)
8. [ ] Test: Farklı sequence field'ları doğru generate ediliyor mu?

**Test Script:** `tests/test-persons-field-type.ps1`

---

### Phase 6: Dataset Authorization Implementation (Planlanan - Henüz Başlanmadı)

**Öncelik:** Orta  
**Süre:** ~60 dakika

**Görevler:**
1. [ ] `PermissionsDefinition` entity class'ı oluştur
2. [ ] `DatasetSchema` entity'sine `permissions` field'ı ekle
3. [ ] MngKeeper'dan kullanıcı grup bilgilerini alan servis
4. [ ] Permission check helper method'ları
5. [ ] DataController'da her endpoint'te permission kontrolü
6. [ ] Test: Yetkisiz erişim denemeleri (403 Forbidden)
7. [ ] Test: Yetkili kullanıcı erişimleri (başarılı)
8. [ ] Test: Group-based ve user-based permission kontrolü

**Test Script:** `tests/test-dataset-authorization.ps1`

**Not:** Bu phase henüz başlanmadı, sadece planlama aşamasında.

#### 4.5 Predefined Query Test
1. [ ] Books schema'ya `books_by_publication_date_range` query tanımını ekle
2. [ ] Test: Predefined query execution (başarılı senaryo)
3. [ ] Test: Missing parameter hatası
4. [ ] Test: Query not found hatası
5. [ ] Test: Tarih aralığı filtreleme doğru çalışıyor mu?
6. [ ] Test: Pipeline'daki sort çalışıyor mu?
7. [ ] Test: Expansion parametreleri query ile uyumlu çalışıyor mu?

**Test Script:** `tests/test-predefined-query.ps1`

---

### Phase 5: Comprehensive Testing (Tüm Özellikler)

**Öncelik:** Yüksek  
**Süre:** ~2 saat

**Görevler:**
1. [ ] Object field type testleri (coverImage)
2. [ ] Update operations testleri
3. [ ] Delete ve restore operations testleri
4. [ ] Pagination, sorting, filtering, field selection testleri
5. [ ] Advanced expansion scenarios testleri
6. [ ] History ve metadata testleri (showHistory, showDataset, showQuery)
7. [ ] POST /api/data/{dataset}/query testleri (complex queries)
8. [ ] Error scenarios testleri (validation, unique constraints, etc.)
9. [ ] Unique constraint violation testleri
10. [ ] Integration testleri (tüm özellikler birlikte)

**Test Scripts:**
- `tests/test-object-fields.ps1`
- `tests/test-update-operations.ps1`
- `tests/test-delete-restore.ps1`
- `tests/test-query-parameters.ps1` (pagination, sorting, filtering, field selection)
- `tests/test-advanced-expansions.ps1`
- `tests/test-metadata-history.ps1`
- `tests/test-complex-queries.ps1`
- `tests/test-error-scenarios.ps1`
- `tests/test-integration.ps1` (tüm özellikler birlikte)

---

## 🧪 Test Senaryoları

### Test 1: Category ve Schema Oluşturma (Predefined Query ile)

```powershell
# 1. Category oluştur
POST /api/dataset-categories
{
  "name": "Book Categories",
  "description": "Category for book-related datasets",
  "isActive": true
}
# Response: { "__dataId": "category-book-001", ... }

# 2. Publishers schema (category ile)
POST /api/datasets
{
  "name": "tst_publishers",
  "description": "Book publishers dataset (test)",
  "category": "category-book-001",
  "fields": [...]
}

# 3. Genres schema (category ile)
POST /api/datasets
{
  "name": "tst_genres",
  "description": "Book genres dataset (test)",
  "category": "category-book-001",
  ...
}

# 4. Books schema (category + relations + queries + permissions ile)
POST /api/datasets
{
  "name": "tst_books",
  "description": "Books dataset with relations and person fields (test)",
  "category": "category-book-001",
  "permissions": {
    "read": {
      "groups": ["managers"],
      "users": []
    },
    "write": {
      "groups": ["managers"],
      "users": []
    }
  },
  "fields": [
    {
      "fieldType": "relation",
      "name": "publisher",
      "relationDataset": "tst_publishers",
      ...
    },
    {
      "fieldType": "relation",
      "name": "genres",
      "isArray": true,
      "relationDataset": "tst_genres",
      ...
    },
    {
      "fieldType": "persons",
      "name": "author",
      ...
    }
  ],
  "queries": [
    {
      "name": "books_by_publication_date_range",
      "description": "Get books published between two dates",
      "parameters": ["startDate", "endDate"],
      "pipeline": [
        {
          "$match": {
            "publicationDate": {
              "$gte": ":startDate",
              "$lte": ":endDate"
            }
          }
        },
        {
          "$sort": {
            "publicationDate": -1,
            "title": 1
          }
        }
      ]
    }
  ]
}
```

**Beklenen:** 
- Category başarıyla oluşturulmalı
- Tüm schema'lar category ile başarıyla oluşturulmalı
- Schema'larda category field'ı doğru set edilmeli
- Books schema'da predefined query tanımı doğru kaydedilmeli

---

### Test 2: Lookup Data Oluşturma

```powershell
# Create publisher
POST /api/data/tst_publishers
{
  "name": "Penguin Random House",
  "website": "https://www.penguinrandomhouse.com",
  "country": "USA"
}

# Create genre
POST /api/data/tst_genres
{
  "name": "Science Fiction",
  "description": "Futuristic fiction"
}
```

**Beklenen:** Lookup data'lar başarıyla oluşturulmalı

---

### Test 3: Books Data ile Relation Fields

```powershell
# Create book with publisher relation
POST /api/data/tst_books
{
  "title": "The Great Gatsby",
  "publisher": "publisher-001",  # Reference
  "genres": ["genre-001", "genre-002"],  # Array of references
  "author": "690cdb7fae502df7d3330bbb"  # MngKeeper User ID
}
```

**Beklenen:** 
- Book başarıyla oluşturulmalı
- Publisher reference doğru kaydedilmeli
- Genres array doğru kaydedilmeli
- Author (persons field) doğru kaydedilmeli

---

### Test 4: Expansion Testleri

```powershell
# Get book with publisher expansion
GET /api/data/tst_books/book-001?expand=publisher

# Get book with genres expansion
GET /api/data/tst_books/book-001?expand=genres

# Get book with all expansions
GET /api/data/tst_books/book-001?expand=publisher,genres,author

# List books with expansion
GET /api/data/tst_books?expand=publisher&limit=10
```

**Beklenen:**
- Publisher expansion çalışmalı (mevcut özellik)
- Genres expansion çalışmalı (mevcut özellik)
- Author expansion çalışmalı (yeni özellik - Phase 4)
- Deep expansion desteklenmeli

---

### Test 5: Persons & PersonGroups Field Type Validation

```powershell
# Invalid user ID
POST /api/data/tst_books
{
  "title": "Test Book",
  "author": "invalid-user-id"  # Should fail
}

# Valid user ID
POST /api/data/tst_books
{
  "title": "Test Book",
  "author": "690cdb7fae502df7d3330bbb",  # Valid MngKeeper user
  "reviewerGroups": ["invalid-group-id"]  # Should fail
}

# Valid user and group IDs
POST /api/data/tst_books
{
  "title": "Test Book",
  "author": "690cdb7fae502df7d3330bbb",  # Valid MngKeeper user
  "reviewerGroups": ["group-id-001"],  # Valid MngKeeper group
  "editorialTeam": "group-id-003"  # Valid MngKeeper group
}
```

**Beklenen:**
- Invalid user ID için validation error
- Invalid group ID için validation error
- Valid user ID için başarılı oluşturma
- Valid group ID için başarılı oluşturma

---

### Test 6: Incremental Fields (Sequence Tests)

```powershell
# Test 1: ISBN sequence (yıl bazlı)
POST /api/data/tst_books
{
  "title": "Book 1",
  "publisher": "publisher-001"
}
# Beklenen: isbn = "ISBN-2025-000001"

POST /api/data/tst_books
{
  "title": "Book 2",
  "publisher": "publisher-001"
}
# Beklenen: isbn = "ISBN-2025-000002"

# Test 2: Book Code sequence (yıl-ay bazlı)
# Beklenen: bookCode = "BK-2512-0001", "BK-2512-0002"

# Test 3: Internal Book Number (dynamic prefix)
POST /api/data/tst_books
{
  "title": "Book 3",
  "publisher": "publisher-001",
  "publisherCode": "PRH"
}
# Beklenen: internalBookNumber = "PRH-2025-00001"

POST /api/data/tst_books
{
  "title": "Book 4",
  "publisher": "publisher-002",
  "publisherCode": "HC"
}
# Beklenen: internalBookNumber = "HC-2025-00001" (farklı counter!)

# Test 4: Sequence Number (domain bazlı, custom increment)
# Beklenen: sequenceNumber = "seven-BOOK-001000", "seven-BOOK-001010"
```

**Beklenen:**
- Her sequence field doğru format'ta generate edilmeli
- Dynamic prefix'li sequence'lar ayrı counter'lara sahip olmalı
- Custom startValue ve incrementStep çalışmalı

---

### Test 7: Object Field Type (coverImage)

```powershell
# Create book with coverImage object field
POST /api/data/tst_books
{
  "title": "Test Book with Cover",
  "publisher": "publisher-001",
  "author": "690cdb7fae502df7d3330bbb",
  "coverImage": {
    "url": "https://example.com/covers/test.jpg",
    "alt": "Test Book Cover",
    "width": 400,
    "height": 600
  }
}

# Create book with null coverImage
POST /api/data/tst_books
{
  "title": "Test Book without Cover",
  "publisher": "publisher-001",
  "author": "690cdb7fae502df7d3330bbb",
  "coverImage": null
}
```

**Beklenen:**
- Object field doğru şekilde kaydedilmeli
- Object schema validation çalışmalı
- Null value kabul edilmeli (mandatory=false olduğu için)

---

### Test 8: Update Operations

```powershell
# Update book title
PUT /api/data/tst_books/book-001
{
  "title": "The Great Gatsby - Updated Edition"
}

# Update book with relation change
PUT /api/data/tst_books/book-001
{
  "publisher": "publisher-002",  # Change publisher
  "genres": ["genre-001", "genre-004"]  # Change genres
}

# Partial update (only some fields)
PUT /api/data/tst_books/book-001
{
  "pageCount": 200,
  "price": 35.99
}
```

**Beklenen:**
- Update başarılı olmalı
- __lastUpdateInfo güncellenmeli
- History kaydı oluşturulmalı (logging=self)
- Sadece gönderilen field'lar güncellenmeli

---

### Test 9: Delete and Restore Operations

```powershell
# Delete book
DELETE /api/data/tst_books/book-001

# Try to get deleted book (should return 404)
GET /api/data/tst_books/book-001

# Restore deleted book
POST /api/data/tst_books/book-001/restore

# Get restored book (should return data)
GET /api/data/tst_books/book-001
```

**Beklenen:**
- Delete başarılı olmalı
- Silinen kayıt __deletedDatas collection'ına taşınmalı
- Restore başarılı olmalı
- Restore sonrası kayıt normal collection'a dönmeli

---

### Test 10: Pagination, Sorting, Filtering, Field Selection

```powershell
# Pagination
GET /api/data/tst_books?skip=0&limit=5

# Sorting
GET /api/data/tst_books?sort=publicationDate,-title

# Filtering
GET /api/data/tst_books?filter=publicationDate:gte:2020-01-01T00:00:00Z
GET /api/data/tst_books?filter=title:regex:Great
GET /api/data/tst_books?filter=publisher:eq:publisher-001

# Field selection
GET /api/data/tst_books?fields=title,publisher,publicationDate

# Combined (pagination + sorting + filtering + field selection)
GET /api/data/tst_books?skip=0&limit=10&sort=publicationDate&filter=publisher:eq:publisher-001&fields=title,publisher
```

**Beklenen:**
- Pagination doğru çalışmalı
- Sorting doğru çalışmalı (asc/desc)
- Filtering doğru çalışmalı (gte, eq, regex, etc.)
- Field selection doğru çalışmalı (sadece seçilen field'lar dönmeli)

---

### Test 11: Advanced Expansion Scenarios

```powershell
# Multiple expansions (relations + persons + personGroups)
GET /api/data/tst_books/book-001?expand=publisher,genres,author,coAuthors,reviewerGroups,editorialTeam

# Deep expansion (nested relations)
GET /api/data/tst_books?expand=publisher&deep=2

# Expansion with pagination
GET /api/data/tst_books?expand=publisher,genres&skip=0&limit=5

# Expansion with filtering
GET /api/data/tst_books?expand=publisher&filter=publisher:eq:publisher-001
```

**Beklenen:**
- Tüm expansion'lar birlikte çalışmalı
- Deep expansion doğru çalışmalı
- Expansion + pagination birlikte çalışmalı
- Expansion + filtering birlikte çalışmalı

---

### Test 12: History and Metadata

```powershell
# Get book with history
GET /api/data/tst_books/book-001?showHistory=true

# Get book with dataset schema
GET /api/data/tst_books/book-001?showDataset=true

# Get book with query pipeline
GET /api/data/tst_books?showQuery=true&filter=title:regex:Great
```

**Beklenen:**
- History kayıtları dönmeli (__history field)
- Dataset schema dönmeli
- Query pipeline dönmeli (aggregate pipeline JSON)

---

### Test 13: POST /api/data/{dataset}/query (Advanced Query)

```powershell
# Complex query with OR logic
POST /api/data/tst_books/query
{
  "match": {
    "$or": [
      { "publicationDate": { "$gte": "2020-01-01T00:00:00Z" } },
      { "title": { "$regex": "Great", "$options": "i" } }
    ],
    "publisher": { "$in": ["publisher-001", "publisher-002"] }
  }
}
?expand=publisher,genres&sort=publicationDate&limit=10
```

**Beklenen:**
- Complex MongoDB query doğru çalışmalı
- OR logic doğru çalışmalı
- Query string parametreleri (expand, sort, limit) birlikte çalışmalı

---

### Test 14: Error Scenarios

```powershell
# Missing mandatory field (author is mandatory)
POST /api/data/tst_books
{
  "title": "Test Book",
  "publisher": "publisher-001"
  # author missing - should fail
}

# Duplicate unique field (isbn is unique)
POST /api/data/tst_books
{
  "title": "Test Book",
  "publisher": "publisher-001",
  "author": "690cdb7fae502df7d3330bbb",
  "isbn": "ISBN-2025-000001"  # Duplicate - should fail
}

# Invalid relation reference (non-existent publisher)
POST /api/data/tst_books
{
  "title": "Test Book",
  "publisher": "non-existent-publisher",  # Should fail or validate
  "author": "690cdb7fae502df7d3330bbb"
}

# Invalid data type (price should be number)
POST /api/data/tst_books
{
  "title": "Test Book",
  "publisher": "publisher-001",
  "author": "690cdb7fae502df7d3330bbb",
  "price": "invalid-number"  # Should fail
}

# Invalid object schema (coverImage.width should be number)
POST /api/data/tst_books
{
  "title": "Test Book",
  "publisher": "publisher-001",
  "author": "690cdb7fae502df7d3330bbb",
  "coverImage": {
    "url": "https://example.com/test.jpg",
    "width": "invalid"  # Should fail
  }
}
```

**Beklenen:**
- Her hata senaryosu için uygun validation error dönmeli
- Error message'lar açıklayıcı olmalı
- HTTP status code doğru olmalı (400 Bad Request)

---

### Test 15: Unique Constraint Violations

```powershell
# Try to create duplicate ISBN
POST /api/data/tst_books
{
  "title": "Book 1",
  "publisher": "publisher-001",
  "author": "690cdb7fae502df7d3330bbb"
}
# Returns: isbn = "ISBN-2025-000001"

POST /api/data/tst_books
{
  "title": "Book 2",
  "publisher": "publisher-001",
  "author": "690cdb7fae502df7d3330bbb",
  "isbn": "ISBN-2025-000001"  # Duplicate - should fail
}
```

**Beklenen:**
- Duplicate unique field için MongoDB unique index violation hatası
- Açıklayıcı error message

---

## 🔐 Dataset Yetkilendirme (Permissions)

### Permissions Yapısı

**Amaç:** Dataset'e erişim kontrolü (henüz implement edilmedi, schema tanımı mevcut)

**Yapı:**
```json
{
  "permissions": {
    "read": {
      "groups": ["managers"],
      "users": []
    },
    "write": {
      "groups": ["managers"],
      "users": []
    },
    "create": {
      "groups": ["managers"],
      "users": []
    },
    "update": {
      "groups": ["managers"],
      "users": []
    },
    "delete": {
      "groups": ["managers"],
      "users": []
    }
  }
}
```

**Permission Types:**
- `read`: Dataset'ten veri okuma (GET işlemleri)
- `write`: Genel yazma yetkisi (create, update, delete dahil - alternatif)
- `create`: Yeni kayıt oluşturma (POST)
- `update`: Mevcut kayıt güncelleme (PUT)
- `delete`: Kayıt silme (DELETE)

**Yetkilendirme Kapsamı:**
- **Groups:** MngKeeper'dan gelen grup isimleri (ör: "managers", "editors", "reviewers")
- **Users:** MngKeeper User ID'leri (ör: "690cdb7fae502df7d3330bbb")
- Her permission type için `groups` ve `users` array'leri ayrı ayrı tanımlanabilir
- Boş array (`[]`) = o permission type için kimse yetkili değil
- `null` veya tanımlanmamış = o permission type için yetkilendirme kontrolü yok (herkes erişebilir)

### Books Dataset Yetkilendirme Örneği

**Senaryo:** Sadece "managers" grubundaki kişiler Books dataset'e yazma ve okuma yapabilir

**Tanım:**
```json
{
  "permissions": {
    "read": {
      "groups": ["managers"],
      "users": []
    },
    "write": {
      "groups": ["managers"],
      "users": []
    }
  }
}
```

**Alternatif Senaryolar:**

**1. Managers okuma-yazma, Editors sadece okuma:**
```json
{
  "permissions": {
    "read": {
      "groups": ["managers", "editors"],
      "users": []
    },
    "write": {
      "groups": ["managers"],
      "users": []
    }
  }
}
```

**2. Managers ve belirli bir kullanıcı okuma-yazma:**
```json
{
  "permissions": {
    "read": {
      "groups": ["managers"],
      "users": ["690cdb7fae502df7d3330bbb"]  // serkan user ID
    },
    "write": {
      "groups": ["managers"],
      "users": ["690cdb7fae502df7d3330bbb"]
    }
  }
}
```

**3. Granular kontrol (create, update, delete ayrı):**
```json
{
  "permissions": {
    "read": {
      "groups": ["managers", "editors", "reviewers"],
      "users": []
    },
    "create": {
      "groups": ["managers"],
      "users": []
    },
    "update": {
      "groups": ["managers", "editors"],
      "users": []
    },
    "delete": {
      "groups": ["managers"],
      "users": []
    }
  }
}
```

**Beklenen Davranış (Implementasyon sonrası):**
- JWT token'dan kullanıcı bilgileri ve grup üyelikleri alınır
- Her CRUD işleminde ilgili permission kontrolü yapılır
- Yetkisiz erişim denemelerinde `403 Forbidden` hatası döner
- Permission tanımlı değilse veya `null` ise, yetkilendirme kontrolü yapılmaz (herkes erişebilir)

**Not:** Yetkilendirme implementasyonu henüz tamamlanmadı. Schema tanımı yapılabilir, ancak execution Phase 6'da implement edilecek.

---

## 📊 Index Tanımlama Stratejisi

### Index Tanımlama Prensibi

**Önemli:** Index tanımları sadece schema'da saklanır. MongoDB'de index oluşturma işlemi **şimdilik yapılmaz**. Index oluşturma, gelecekte geliştirilecek ayrı bir uygulamanın sorumluluğundadır.

### Index Türleri

1. **Unique Index** (`unique: true`)
   - Aynı değere sahip birden fazla kayıt olamaz
   - Örnek: `idx_isbn`, `idx_name`

2. **Non-Unique Index** (`unique: false`)
   - Aynı değere sahip birden fazla kayıt olabilir
   - Örnek: `idx_title`, `idx_publisher`

3. **Ascending Index** (`fields: { "fieldName": 1 }`)
   - Artan sıralama (A-Z, 0-9, küçükten büyüğe)
   - Çoğu durumda kullanılır

4. **Descending Index** (`fields: { "fieldName": -1 }`)
   - Azalan sıralama (Z-A, 9-0, büyükten küçüğe)
   - Örnek: `idx_publicationDate` (yeni kayıtlar önce)

5. **Composite Index** (Birden fazla field)
   - Örnek: `idx_title_bookCode` → `{ "title": 1, "bookCode": 1 }`
   - Field sırası önemlidir (MongoDB index prefix kuralı)
   - İlk field'a göre sorgulama daha hızlıdır

### Books Dataset Index Örnekleri

```json
{
  "indexList": [
    {
      "name": "idx_name",
      "fields": { "name": 1 },
      "unique": true
    },
    {
      "name": "idx_title",
      "fields": { "title": 1 },
      "unique": false
    },
    {
      "name": "idx_title_bookCode",
      "fields": { "title": 1, "bookCode": 1 },
      "unique": false
    },
    {
      "name": "idx_publicationDate",
      "fields": { "publicationDate": -1 },
      "unique": false
    }
  ]
}
```

**Açıklamalar:**
- `idx_name`: Unique ascending index - Her kitap adı benzersiz olmalı
- `idx_title`: Non-unique ascending index - Aynı başlığa sahip kitaplar olabilir
- `idx_title_bookCode`: Composite ascending index - Title ve bookCode birlikte indexlenir
- `idx_publicationDate`: Non-unique descending index - Yeni yayınlar önce gelir

---

## 📋 Önemli Notlar

### 1. Persons Field Type - MngKeeper Entegrasyonu

**API Endpoint:**
```
GET https://localhost:5001/api/user/{userId}
```

**Authentication:**
- MngDataGateway'in kendi JWT token'ı ile MngKeeper'a çağrı yapması gerekebilir
- Veya MngKeeper'da service-to-service authentication

**Cache Stratejisi:**
- In-memory cache (TTL: 5 dakika)
- Key: `{domainName}:user:{userId}`
- Cache miss durumunda MngKeeper API'ye çağrı

**Error Handling:**
- User not found → Validation error
- MngKeeper API unavailable → Cache'den getir (stale data)
- Network error → Retry mechanism

---

### 2. Relation Field Types (Mevcut Özellik)

**Tek Relation:**
- `publisher` → Single value
- MongoDB'de: `"publisher": "publisher-001"`

**Array Relation:**
- `genres` → Array of values
- MongoDB'de: `"genres": ["genre-001", "genre-002"]`

**Expansion:**
- `?expand=publisher` → Single object expansion
- `?expand=genres` → Array of objects expansion
- `?expand=publisher,genres` → Multiple expansions

---

### 3. Incremental Fields (Sequence Examples)

**Mevcut Seçenekler:**
```
{0}         → Counter value (required)
{0:D6}      → Zero-padded counter (6 digits)
{0:D4}      → Zero-padded counter (4 digits)
{0:D5}      → Zero-padded counter (5 digits)
{year}      → 2025 (4-digit year)
{yy}        → 25 (2-digit year)
{month}     → 12 (2-digit month)
{mm}        → 12 (2-digit month)
{day}       → 30 (2-digit day)
{dd}        → 30 (2-digit day)
{domain}    → seven (domain name from JWT)
{fieldName} → Dynamic field reference (e.g., publisherCode)
```

**Books Dataset'teki Sequence Örnekleri:**

1. **ISBN Sequence** (Yıl bazlı):
   - Format: `ISBN-{year}-{0:D6}`
   - Örnek: `ISBN-2025-000001`, `ISBN-2025-000002`
   - Yıl değişince: `ISBN-2026-000001` (yeni counter scope)

2. **Book Code Sequence** (Yıl-Ay bazlı):
   - Format: `BK-{yy}{month}-{0:D4}`
   - Örnek: `BK-2512-0001`, `BK-2512-0002`
   - Ay değişince: `BK-2601-0001` (yeni counter scope)

3. **Internal Book Number** (Dynamic prefix):
   - Format: `{publisherCode}-{year}-{0:D5}`
   - Örnek: `PRH-2025-00001`, `HC-2025-00001` (farklı publisher'lar için ayrı counter)
   - `publisherCode` field'ından dinamik olarak alınır

4. **Sequence Number** (Domain bazlı, custom startValue ve incrementStep):
   - Format: `{domain}-BOOK-{0:D6}`
   - Örnek: `seven-BOOK-001000`, `seven-BOOK-001010` (10'ar artış)
   - `startValue: 1000`, `incrementStep: 10`

**Counter Scope Kuralı:**
- Her unique resolved prefix ayrı bir counter'a sahiptir
- Örnek: `PRH-2025-*` ve `HC-2025-*` farklı counter'lar (publisherCode farklı)
- Örnek: `ISBN-2025-*` ve `ISBN-2026-*` farklı counter'lar (yıl farklı)

---

## 🎨 UI Considerations

**Not:** Bu dataset planı backend implementasyonu için hazırlanmıştır. UI tasarım ve implementasyon planı için:

📄 **UI Design Document:** `Mng.Ui/docs/DATASET_UI_DESIGN.md`

**UI Geliştirme Planı:**
- Dataset oluşturma/düzenleme formu
- Field type'a göre dinamik form alanları
- Relation, persons, personGroups field'lar için lookup component'leri
- Incremental field konfigürasyonu
- Predefined queries tanımlama arayüzü
- Permissions yönetimi arayüzü

**İlgili UI Dokümantasyonu:**
- `Mng.Ui/docs/RoadMap.md` - Phase 3.2: Dataset Management Sayfaları

---

## 🔗 İlgili Dosyalar

**Backend Dokümantasyon:**
- `docs/DATASET_SCHEMA_SUMMARY.md` - Field types açıklaması
- `docs/PERSONS_PERSONGROUPS_IMPLEMENTATION.md` - Persons field type planı
- `docs/STATUS.md` - Mevcut durum

**UI Dokümantasyon:**
- `Mng.Ui/docs/DATASET_UI_DESIGN.md` - Dataset UI tasarım planı
- `Mng.Ui/docs/RoadMap.md` - Phase 3.2: Dataset Management

**Test Scripts:**
- `tests/setup-test-datasets.ps1` - Mevcut test dataset setup örneği
- `tests/test-get-operations.ps1` - GET operations test örneği

---

## ✅ Checklist

### Phase 1: Dataset Category ve Schema
- [ ] "Book Categories" dataset category oluşturma
- [ ] Publishers dataset schema (category ile)
- [ ] Genres dataset schema (category ile)
- [ ] Books dataset schema (category + relations + persons + queries)
- [ ] Predefined query tanımı (books_by_publication_date_range)
- [ ] Category ve schema validation testleri

### Phase 2: Lookup Data
- [ ] Publishers data (3-5 örnek)
- [ ] Genres data (4-6 örnek)
- [ ] Data validation testleri

### Phase 3: Books Data
- [ ] Books data (relation fields ile)
- [ ] Expansion testleri
- [ ] Query testleri

### Phase 4: Persons Field Type
- [ ] MngKeeper API entegrasyonu
- [ ] Validation logic
- [ ] Expansion logic
- [ ] Cache mekanizması
- [ ] Books data ile test

---

**Son Güncelleme:** 30 Aralık 2025  
**Durum:** 📋 Planlama Tamamlandı - Implementation Bekleniyor

