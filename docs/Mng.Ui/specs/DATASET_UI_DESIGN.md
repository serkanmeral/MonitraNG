# Dataset UI Design & Implementation Plan

**Tarih:** 30 Aralık 2025  
**Durum:** 📋 Planlama Aşaması  
**Hedef:** Dataset oluşturma, düzenleme ve yönetim için kapsamlı UI tasarımı

**İlgili Backend Dokümantasyon:**
- `MngDataGateway/docs/BOOKS_DATASET_PLAN.md` - Books dataset örneği ve test senaryoları
- `MngDataGateway/docs/STATUS.md` - Backend implementasyon durumu

---

## 📋 Genel Bakış

Dataset UI, kullanıcıların dataset schema'larını görsel olarak oluşturmasına, düzenlemesine ve yönetmesine olanak sağlar. Books dataset örneği (`MngDataGateway/docs/BOOKS_DATASET_PLAN.md`) referans alınarak tasarlanmıştır.

---

## 🎯 Sayfa Yapısı

### 1. Dataset Listesi Sayfası

**Route:** `/apps/datasets`  
**Component:** `pages/apps/datasets/index.vue`

#### Özellikler:
- Dataset listesi tablosu (`v-data-table`)
- Dataset adı, kategori, açıklama
- Oluşturulma tarihi
- İşlemler: Görüntüle, Düzenle, Sil
- Yeni Dataset oluştur butonu
- Filtreleme ve arama

#### Tablo Kolonları:
| Kolon | Açıklama | Sıralanabilir |
|-------|----------|---------------|
| Name | Dataset adı (`@books`) | ✅ |
| Category | Kategori adı | ✅ |
| Description | Açıklama | ❌ |
| Fields | Field sayısı | ✅ |
| Created At | Oluşturulma tarihi | ✅ |
| Actions | İşlemler | ❌ |

---

### 2. Dataset Oluşturma/Düzenleme Sayfası

**Route:** 
- `/apps/datasets/create` - Yeni dataset
- `/apps/datasets/edit/[name]` - Düzenleme

**Component:** `pages/apps/datasets/form.vue`

#### Sayfa Bölümleri:

##### 2.1 Temel Bilgiler (Step 1)
```
┌─────────────────────────────────────┐
│ Dataset Adı *                       │
│ @books                              │
├─────────────────────────────────────┤
│ Açıklama                            │
│ Books dataset with relations...     │
├─────────────────────────────────────┤
│ Kategori                            │
│ [Dropdown: Book Categories ▼]       │
├─────────────────────────────────────┤
│ Schema Ayarları                     │
│ ☑ Force Schema Validation          │
│ ☐ Logging Mode: [None ▼]           │
│ ☐ Publish Mode: [None ▼]           │
└─────────────────────────────────────┘
```

**Form Alanları:**
- **Dataset Name** (`name`): Text input, `@` prefix ile başlamalı, unique
- **Description** (`description`): Textarea
- **Category** (`category`): Dropdown (Dataset Categories'dan)
- **Force Schema** (`forceSchema`): Checkbox
- **Logging** (`logging`): Radio/Select (none, self, common)
- **Publish Mode** (`publish_mode`): Radio/Select (none, basic, full)

---

##### 2.2 Field Tanımları (Step 2) - Ana Bölüm

**UI Tasarımı:**
```
┌────────────────────────────────────────────────────┐
│ Field Tanımları                                    │
│ [+ Yeni Field Ekle]                                │
├────────────────────────────────────────────────────┤
│ ┌──────────────────────────────────────────────┐  │
│ │ Field 1: ISBN (incremental)                  │  │
│ │ ☑ Mandatory  ☑ Unique                        │  │
│ │ [Düzenle] [Sil]                               │  │
│ └──────────────────────────────────────────────┘  │
│ ┌──────────────────────────────────────────────┐  │
│ │ Field 2: title (text)                        │  │
│ │ ☑ Mandatory  ☐ Unique                        │  │
│ │ [Düzenle] [Sil]                               │  │
│ └──────────────────────────────────────────────┘  │
│ ...                                               │
└────────────────────────────────────────────────────┘
```

**Field Ekleme/Düzenleme Modal:**

```
┌─────────────────────────────────────────────┐
│ Yeni Field Ekle                             │
├─────────────────────────────────────────────┤
│ Field Type *                                │
│ [Dropdown: text ▼]                          │
│   - text                                    │
│   - number                                  │
│   - bool                                    │
│   - datetime                                │
│   - object                                  │
│   - relation                                │
│   - persons                                 │
│   - personGroups                            │
│   - incremental                             │
├─────────────────────────────────────────────┤
│ Field Name *                                │
│ [isbn]                                      │
├─────────────────────────────────────────────┤
│ Field Title                                 │
│ [ISBN]                                      │
├─────────────────────────────────────────────┤
│ ☑ Mandatory  ☑ Unique  ☐ Array             │
├─────────────────────────────────────────────┤
│ Field Type Specific Options:                │
│ [Dinamik olarak gösterilecek]               │
├─────────────────────────────────────────────┤
│ [İptal] [Kaydet]                            │
└─────────────────────────────────────────────┘
```

**Field Type'a Göre Dinamik Form Alanları:**

1. **Relation Field:**
   - Relation Dataset: [Dropdown: @publishers ▼]
   - Relation Field: [__dataId] (default, editable)
   - Is Array: ☐

2. **Persons Field:**
   - Is Array: ☐
   - (MngKeeper user selection - no config needed in schema)

3. **PersonGroups Field:**
   - Is Array: ☐
   - (MngKeeper group selection - no config needed in schema)

4. **Incremental Field:**
   ```
   Format Template *
   [ISBN-{year}-{0:D6}]
   Placeholders: {year}, {yy}, {month}, {day}, {domain}, {fieldName}
   
   Start Value
   [1]
   
   Increment Step
   [1]
   ```

5. **Object Field:**
   ```
   Object Schema (JSON)
   {
     "url": "text",
     "alt": "text",
     "width": "number",
     "height": "number"
   }
   [JSON Editor Component]
   ```

---

##### 2.3 Predefined Queries (Step 3) - Opsiyonel

```
┌────────────────────────────────────────────────────┐
│ Predefined Queries                                 │
│ [+ Yeni Query Ekle]                                │
├────────────────────────────────────────────────────┤
│ ┌──────────────────────────────────────────────┐  │
│ │ Query: books_by_publication_date_range        │  │
│ │ Parameters: startDate, endDate                │  │
│ │ [Düzenle] [Sil]                               │  │
│ └──────────────────────────────────────────────┘  │
│ ...                                               │
└────────────────────────────────────────────────────┘
```

**Query Ekleme/Düzenleme Modal:**
```
┌─────────────────────────────────────────────┐
│ Predefined Query Ekle                       │
├─────────────────────────────────────────────┤
│ Query Name *                                │
│ [books_by_publication_date_range]           │
├─────────────────────────────────────────────┤
│ Description                                 │
│ [Get books published between two dates]     │
├─────────────────────────────────────────────┤
│ Parameters (comma-separated)                │
│ [startDate, endDate]                        │
├─────────────────────────────────────────────┤
│ MongoDB Aggregation Pipeline (JSON) *       │
│ [                                          │
│   {                                        │
│     "$match": {                            │
│       "publicationDate": {                 │
│         "$gte": ":startDate",              │
│         "$lte": ":endDate"                 │
│       }                                    │
│     }                                      │
│   }                                        │
│ ]                                          │
│ [JSON Editor with syntax highlighting]     │
├─────────────────────────────────────────────┤
│ [İptal] [Kaydet]                            │
└─────────────────────────────────────────────┘
```

---

##### 2.4 Permissions (Step 4) - Opsiyonel

```
┌────────────────────────────────────────────────────┐
│ Dataset Permissions                                │
├────────────────────────────────────────────────────┤
│ Read Permissions                                   │
│ Groups: [managers, editors] [+ Ekle]               │
│ Users: [690cdb7fae502df7d3330bbb] [+ Ekle]         │
├────────────────────────────────────────────────────┤
│ Write Permissions                                  │
│ Groups: [managers] [+ Ekle]                        │
│ Users: [] [+ Ekle]                                 │
├────────────────────────────────────────────────────┤
│ Create Permissions                                 │
│ Groups: [managers] [+ Ekle]                        │
│ Users: [] [+ Ekle]                                 │
├────────────────────────────────────────────────────┤
│ Update Permissions                                 │
│ Groups: [managers, editors] [+ Ekle]               │
│ Users: [] [+ Ekle]                                 │
├────────────────────────────────────────────────────┤
│ Delete Permissions                                 │
│ Groups: [managers] [+ Ekle]                        │
│ Users: [] [+ Ekle]                                 │
└────────────────────────────────────────────────────┘
```

**Group/User Seçim Modal:**
- MngKeeper API'den grup/kullanıcı listesi
- Multi-select component
- Chip'ler ile gösterim

---

##### 2.5 Index Definitions (Step 5) - Opsiyonel

**UI Tasarımı:**
```
┌────────────────────────────────────────────────────┐
│ Index Definitions                                  │
│ [+ Yeni Index Ekle]                                │
├────────────────────────────────────────────────────┤
│ ┌──────────────────────────────────────────────┐  │
│ │ Index: idx_name                              │  │
│ │ Fields: name (asc)                            │  │
│ │ ☑ Unique                                     │  │
│ │ [Düzenle] [Sil]                               │  │
│ └──────────────────────────────────────────────┘  │
│ ┌──────────────────────────────────────────────┐  │
│ │ Index: idx_title                              │  │
│ │ Fields: title (asc)                           │  │
│ │ ☐ Unique                                     │  │
│ │ [Düzenle] [Sil]                               │  │
│ └──────────────────────────────────────────────┘  │
│ ┌──────────────────────────────────────────────┐  │
│ │ Index: idx_title_bookCode                     │  │
│ │ Fields: title (asc), bookCode (asc)           │  │
│ │ ☐ Unique                                     │  │
│ │ [Düzenle] [Sil]                               │  │
│ └──────────────────────────────────────────────┘  │
│ ┌──────────────────────────────────────────────┐  │
│ │ Index: idx_isbn                              │  │
│ │ Fields: isbn (asc)                           │  │
│ │ ☑ Unique                                     │  │
│ │ [Düzenle] [Sil]                               │  │
│ └──────────────────────────────────────────────┘  │
│ ...                                               │
└────────────────────────────────────────────────────┘
```

**Index Ekleme/Düzenleme Modal:**
```
┌─────────────────────────────────────────────┐
│ Index Tanımı                               │
├─────────────────────────────────────────────┤
│ Index Name *                               │
│ [idx_name]                                 │
├─────────────────────────────────────────────┤
│ Fields *                                   │
│ ┌─────────────────────────────────────┐   │
│ │ Field: [name ▼]  Order: [Ascending ▼]│   │
│ │   Options: Ascending / Descending   │   │
│ │ [X]                                 │   │
│ └─────────────────────────────────────┘   │
│ [+ Field Ekle]                              │
│ Not: Birden fazla field ekleyerek          │
│      composite index oluşturabilirsiniz     │
├─────────────────────────────────────────────┤
│ ☑ Unique Index                              │
│ Not: Unique index'ler duplicate değerlere  │
│      izin vermez                            │
├─────────────────────────────────────────────┤
│ [İptal] [Kaydet]                            │
└─────────────────────────────────────────────┘
```

**Index Türleri:**
- **Unique Index**: Aynı değere sahip birden fazla kayıt olamaz (`unique: true`)
- **Non-Unique Index**: Aynı değere sahip birden fazla kayıt olabilir (`unique: false`)
- **Ascending Index**: Artan sıralama (`fields: { "fieldName": 1 }`)
- **Descending Index**: Azalan sıralama (`fields: { "fieldName": -1 }`)
- **Composite Index**: Birden fazla field içeren index (örn: `{ "title": 1, "bookCode": 1 }`)

**Notlar:**
- Index tanımları sadece schema'da saklanır, MongoDB'de otomatik oluşturulmaz
- Index oluşturma işlemi gelecekte geliştirilecek ayrı bir uygulamanın sorumluluğundadır
- Her index için benzersiz bir `name` tanımlanmalıdır
- Composite index'lerde field sırası önemlidir (MongoDB index prefix kuralı)

**Örnek Index Tanımları (Books Dataset):**
- **idx_name**: `name` field'ı için unique ascending index
  ```json
  { "name": "idx_name", "fields": { "name": 1 }, "unique": true }
  ```
- **idx_title**: `title` field'ı için non-unique ascending index
  ```json
  { "name": "idx_title", "fields": { "title": 1 }, "unique": false }
  ```
- **idx_title_bookCode**: `title` ve `bookCode` field'ları için composite ascending index
  ```json
  { "name": "idx_title_bookCode", "fields": { "title": 1, "bookCode": 1 }, "unique": false }
  ```
- **idx_publicationDate**: `publicationDate` field'ı için non-unique descending index (yeni kayıtlar önce)
  ```json
  { "name": "idx_publicationDate", "fields": { "publicationDate": -1 }, "unique": false }
  ```
- **idx_isbn**: `isbn` field'ı için unique ascending index (incremental field'dan otomatik)
- **idx_bookCode**: `bookCode` field'ı için unique ascending index (incremental field'dan otomatik)

---

### 3. Dataset Detay Sayfası

**Route:** `/apps/datasets/[name]`  
**Component:** `pages/apps/datasets/[name].vue`

#### Bölümler:
1. **Schema Bilgileri Card**
   - Dataset adı, kategori, açıklama
   - Schema ayarları (forceSchema, logging, publish_mode)

2. **Fields Card**
   - Field listesi (expandable)
   - Her field için: type, mandatory, unique, array

3. **Queries Card**
   - Predefined query listesi
   - Query execution butonu

4. **Permissions Card**
   - Permission özeti

5. **Indexes Card**
   - Index listesi

---

## 🧩 Component'ler

### 1. DatasetForm.vue
Ana form component'i. Stepper yapısında:
- Step 1: Temel Bilgiler
- Step 2: Field Tanımları
- Step 3: Predefined Queries (opsiyonel)
- Step 4: Permissions (opsiyonel)
- Step 5: Index Definitions (opsiyonel)

### 2. FieldDefinitionForm.vue
Field ekleme/düzenleme modal component'i.
- Field type'a göre dinamik form alanları
- Validation (VeeValidate)

### 3. QueryDefinitionForm.vue
Predefined query ekleme/düzenleme modal component'i.
- JSON editor (MongoDB aggregation pipeline)
- Parameter validation

### 4. PermissionsEditor.vue
Permissions yönetimi component'i.
- Group/User selection (MngKeeper API)
- Multi-select component

### 5. IndexDefinitionForm.vue
Index ekleme/düzenleme modal component'i.

---

## 📝 Form Validation

### Dataset Name:
- `@` ile başlamalı
- Unique kontrolü (backend'de)
- Regex: `^@[a-zA-Z0-9_]+$`

### Field Name:
- Unique (dataset içinde)
- Regex: `^[a-zA-Z][a-zA-Z0-9_]*$`

### Format Template (Incremental):
- `{0}` placeholder zorunlu
- Placeholder validation

### Aggregation Pipeline (Query):
- Valid JSON
- MongoDB aggregation pipeline syntax kontrolü (opsiyonel)

---

## 🎨 UI/UX Özellikleri

### 1. Stepper Navigation
- İleri/Geri butonları
- Her step'te validation kontrolü
- Step tamamlanma göstergesi

### 2. Field List Management
- Drag & drop sıralama (opsiyonel)
- Field'ları collapse/expand
- Hızlı düzenleme (inline editing opsiyonel)

### 3. JSON Editor
- Syntax highlighting
- Format validation
- Auto-format (beautify)

### 4. Lookup Components
- Relation Dataset: Dataset listesi dropdown
- Groups: MngKeeper API'den grup listesi
- Users: MngKeeper API'den kullanıcı listesi
- Arama özelliği

### 5. Validation Feedback
- Real-time validation
- Field-level error messages
- Form-level error summary

---

## 🔌 API Entegrasyonu

### Dataset CRUD:
```typescript
// Get datasets list
GET /api/datasets

// Get dataset by name
GET /api/datasets/{name}

// Create dataset
POST /api/datasets
Body: DatasetSchemaDto

// Update dataset
PUT /api/datasets/{name}
Body: DatasetSchemaDto

// Delete dataset
DELETE /api/datasets/{name}
```

### Lookup Data:
```typescript
// Get dataset categories
GET /api/dataset-categories

// Get groups (MngKeeper)
GET /api/keeper/group

// Get users (MngKeeper)
GET /api/keeper/user
```

---

## 📋 Implementasyon Checklist

### Phase 1: Temel Form Yapısı
- [ ] DatasetForm.vue component oluştur
- [ ] Stepper yapısı (5 step)
- [ ] Temel bilgiler formu (Step 1)
- [ ] Form validation (VeeValidate)

### Phase 2: Field Management
- [ ] FieldDefinitionForm.vue component
- [ ] Field list management
- [ ] Field type'a göre dinamik form
- [ ] Relation field lookup
- [ ] Incremental field configurator
- [ ] Object field JSON editor

### Phase 3: Advanced Features
- [ ] QueryDefinitionForm.vue component
- [ ] PermissionsEditor.vue component
- [ ] IndexDefinitionForm.vue component
- [ ] JSON editor component (syntax highlighting)

### Phase 4: Dataset List & Detail
- [ ] Dataset listesi sayfası
- [ ] Dataset detay sayfası
- [ ] Dataset düzenleme sayfası

### Phase 5: Integration & Testing
- [ ] API entegrasyonu
- [ ] Error handling
- [ ] Loading states
- [ ] Success/error notifications
- [ ] Test with Books dataset example

---

## 🎯 Örnek Kullanım Senaryosu

**Books Dataset Oluşturma:**
1. Temel Bilgiler:
   - Name: `@books`
   - Description: "Books dataset with relations and person fields"
   - Category: "Book Categories"
   - Force Schema: ✅

2. Field Tanımları:
   - `isbn` (incremental, mandatory, unique)
   - `title` (text, mandatory)
   - `publisher` (relation → @publishers)
   - `genres` (relation → @genres, array)
   - `author` (persons, mandatory)
   - `coAuthors` (persons, array)
   - `reviewerGroups` (personGroups, array)
   - `coverImage` (object)

3. Predefined Query:
   - `books_by_publication_date_range` (startDate, endDate)

4. Permissions:
   - Read: managers
   - Write: managers

---

## 📚 İlgili Dokümantasyon

- **Backend Plan:** `MngDataGateway/docs/BOOKS_DATASET_PLAN.md`
- **Backend Status:** `MngDataGateway/docs/STATUS.md`
- **UI Roadmap:** `Mng.Ui/docs/RoadMap.md` (Phase 3.2)

---

**Son Güncelleme:** 30 Aralık 2025  
**Durum:** 📋 Planlama Tamamlandı - Implementation Bekleniyor

