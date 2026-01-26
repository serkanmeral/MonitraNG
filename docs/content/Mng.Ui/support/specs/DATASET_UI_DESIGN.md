# Dataset UI Design & Implementation Plan

**Tarih:** 30 Aralık 2025  
**Son Güncelleme:** 13 Ocak 2026  
**Durum:** 📋 Planlama Aşaması - Backend API'ler ile uyumlu hale getirildi  
**Hedef:** Dataset oluşturma, düzenleme ve yönetim için kapsamlı UI tasarımı

**İlgili Backend Dokümantasyon:**
- `MngDataGateway/docs/BOOKS_DATASET_PLAN.md` - Books dataset örneği ve test senaryoları
- `MngDataGateway/docs/STATUS.md` - Backend implementasyon durumu
- `docs/Mng.Ui/specs/DATASET_UI_DESIGN_EXPECTED_FEATURES.md` - Backend analizi ve eksiklikler

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
│ Default Value                                │
│ [Field type'a göre dinamik input]            │
├─────────────────────────────────────────────┤
│ Field Type Specific Options:                 │
│ [Dinamik olarak gösterilecek]                │
├─────────────────────────────────────────────┤
│ Validation Rules (Optional)                  │
│ [Field type'a göre dinamik validation alanları]│
├─────────────────────────────────────────────┤
│ [İptal] [Kaydet]                             │
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
   - (Object field için backend'de schema field'ı yok, validation rules kullanılabilir)

**Default Value:**
- Field type'a göre dinamik input gösterilir:
  - **text**: Text input
  - **number**: Number input
  - **bool**: Checkbox (true/false)
  - **datetime**: Date/time picker
  - **object**: JSON editor (opsiyonel)
  - **array**: Array editor (opsiyonel)

**Validation Rules (Field-Level):**
- Field type'a göre dinamik validation form alanları gösterilir:
  - **text**: 
    - Min Length (number)
    - Max Length (number)
    - Pattern (regex string)
  - **number**: 
    - Min (number)
    - Max (number)
  - **datetime**: 
    - Min Date (date picker)
    - Max Date (date picker)
  - **array**: 
    - Min Items (number)
    - Max Items (number)
  - **Custom Error Message**: Tüm field type'lar için (string)

**Not:** Validation rules opsiyoneldir. Belirtilmezse backend'de sadece temel validasyonlar (mandatory, unique, type) uygulanır.

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
│ Parameters *                                │
│ ┌─────────────────────────────────────┐   │
│ │ Name: [startDate]  Type: [datetime ▼]│   │
│ │ Description: [Start date]            │   │
│ │ ☑ Required                          │   │
│ │ [X]                                 │   │
│ └─────────────────────────────────────┘   │
│ ┌─────────────────────────────────────┐   │
│ │ Name: [endDate]  Type: [datetime ▼] │   │
│ │ Description: [End date]             │   │
│ │ ☑ Required                          │   │
│ │ [X]                                 │   │
│ └─────────────────────────────────────┘   │
│ [+ Parameter Ekle]                         │
│ Not: Parameter name pipeline'da :name formatında kullanılır│
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
│ Not: Parameter placeholder formatı :parameterName│
├─────────────────────────────────────────────┤
│ [İptal] [Kaydet]                            │
└─────────────────────────────────────────────┘
```

**Parameter Tanımları:**
- **Name**: Parameter adı (pipeline'da `:parameterName` formatında kullanılır)
- **Type**: Parameter tipi (text, number, bool, datetime)
- **Description**: Açıklama (opsiyonel)
- **Required**: Zorunlu mu (default: true)

**Not:** Backend'de eski format (comma-separated string) da destekleniyor, ancak yeni format (QueryParameterDefinition listesi) önerilir.

---

##### 2.4 Validation Definitions (Step 4) - Opsiyonel

**UI Tasarımı:**
```
┌────────────────────────────────────────────────────┐
│ Validation Definitions                              │
│ [+ Yeni Validation Ekle]                           │
├────────────────────────────────────────────────────┤
│ ┌──────────────────────────────────────────────┐  │
│ │ Validation: end_date_after_start_date         │  │
│ │ Type: Expression                              │  │
│ │ When: Both                                    │  │
│ │ [Düzenle] [Sil]                               │  │
│ └──────────────────────────────────────────────┘  │
│ ┌──────────────────────────────────────────────┐  │
│ │ Validation: external_api_validation           │  │
│ │ Type: HTTP                                    │  │
│ │ URL: https://api.example.com/validate         │  │
│ │ When: Create                                  │  │
│ │ [Düzenle] [Sil]                               │  │
│ └──────────────────────────────────────────────┘  │
│ ...                                               │
└────────────────────────────────────────────────────┘
```

**Validation Ekleme/Düzenleme Modal:**

**Expression-Based Validation:**
```
┌─────────────────────────────────────────────┐
│ Validation Definition                        │
├─────────────────────────────────────────────┤
│ Validation Name *                            │
│ [end_date_after_start_date]                  │
├─────────────────────────────────────────────┤
│ Description                                  │
│ [Ensure end date is after start date]        │
├─────────────────────────────────────────────┤
│ Validation Type *                            │
│ [Expression ▼]                               │
├─────────────────────────────────────────────┤
│ Expression *                                 │
│ [endDate > startDate]                        │
│ [Expression editor with field suggestions]   │
│ Not: Field names doğrudan kullanılabilir    │
│      Örnek: endDate > startDate, price / pageCount <= 10│
├─────────────────────────────────────────────┤
│ When *                                       │
│ [Both ▼]  (create, update, both)            │
├─────────────────────────────────────────────┤
│ Execution Order                              │
│ [0]  (Lower number = earlier execution)     │
├─────────────────────────────────────────────┤
│ [İptal] [Kaydet]                            │
└─────────────────────────────────────────────┘
```

**HTTP-Based Validation:**
```
┌─────────────────────────────────────────────┐
│ Validation Definition                        │
├─────────────────────────────────────────────┤
│ Validation Name *                            │
│ [external_api_validation]                    │
├─────────────────────────────────────────────┤
│ Description                                  │
│ [Validate data using external API]           │
├─────────────────────────────────────────────┤
│ Validation Type *                            │
│ [HTTP ▼]                                     │
├─────────────────────────────────────────────┤
│ URL *                                        │
│ [https://api.example.com/validate]           │
├─────────────────────────────────────────────┤
│ Method                                       │
│ [POST ▼]  (GET, POST - default: POST)      │
├─────────────────────────────────────────────┤
│ Fields (Optional)                            │
│ [Multi-select: field1, field2 ▼]            │
│ Not: Belirtilmezse tüm field'lar gönderilir│
├─────────────────────────────────────────────┤
│ When *                                       │
│ [Create ▼]  (create, update, both)          │
├─────────────────────────────────────────────┤
│ Execution Order                              │
│ [0]  (Lower number = earlier execution)     │
├─────────────────────────────────────────────┤
│ Response Format:                             │
│ {                                            │
│   "isValid": true/false,                     │
│   "errorMessage": "Hata mesajı (opsiyonel)" │
│ }                                            │
│ Not: Authorization header otomatik gönderilir│
├─────────────────────────────────────────────┤
│ [İptal] [Kaydet]                            │
└─────────────────────────────────────────────┘
```

**Validation Types:**
1. **Expression-Based**: JavaScript benzeri expression'lar
   - Field names doğrudan kullanılabilir (örn: `endDate`, `startDate`)
   - Operatörler: `>`, `<`, `>=`, `<=`, `==`, `!=`, `&&`, `||`, `+`, `-`, `*`, `/`
   - Örnekler: `endDate > startDate`, `price / pageCount <= 10`
2. **HTTP-Based**: External API endpoint'leri ile validation
   - URL: Validation endpoint URL'i
   - Method: GET veya POST (default: POST)
   - Fields: Hangi field'lar gönderilecek (opsiyonel)
   - Response: `{ "isValid": boolean, "errorMessage": string }`
   - Authorization: Otomatik olarak Authorization header gönderilir
   - Timeout: Default 30 saniye

**Not:** Validations sırayla çalıştırılır (order field'ına göre). HTTP validation timeout veya network error durumunda geçerli sayılır (safe default).

---

##### 2.5 Permissions (Step 5) - Opsiyonel - ⚠️ NOT YET IMPLEMENTED

**Durum:** Backend'de permissions field'ı henüz implement edilmemiştir. Bu step UI'da gösterilebilir ancak şu anda backend'e gönderilmeyecektir.

**Not:** Permissions yönetimi gelecekte implement edilecektir. Şu anda bu step'i atlanabilir veya UI'da gösterilip sadece görsel amaçlı kullanılabilir.

```
┌────────────────────────────────────────────────────┐
│ Dataset Permissions (Coming Soon)                  │
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

**Implementation Plan:**
- Phase 1'de permissions field'ı UI'da gösterilmeyecek veya disabled olacak
- Backend permissions implement edildikten sonra bu step aktif hale gelecek

---

##### 2.6 Index Definitions (Step 6) - Opsiyonel

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
   - Her field için: type, mandatory, unique, array, validation rules

3. **Validations Card**
   - Validation definitions listesi (expandable)
   - Her validation için: type, when, order

4. **Queries Card**
   - Predefined query listesi
   - Query execution butonu

5. **Permissions Card**
   - Permission özeti (⚠️ Backend'de henüz implement edilmemiş)

6. **Indexes Card**
   - Index listesi

---

## 🧩 Component'ler

### 1. DatasetForm.vue
Ana form component'i. Stepper yapısında:
- Step 1: Temel Bilgiler
- Step 2: Field Tanımları
- Step 3: Predefined Queries (opsiyonel)
- Step 4: Validation Definitions (opsiyonel)
- Step 5: Permissions (opsiyonel - ⚠️ Backend'de henüz implement edilmemiş)
- Step 6: Index Definitions (opsiyonel)

### 2. FieldDefinitionForm.vue
Field ekleme/düzenleme modal component'i.
- Field type'a göre dinamik form alanları
- Default value input (field type'a göre)
- Field-level validation rules (field type'a göre)
- Validation (VeeValidate)

### 3. QueryDefinitionForm.vue
Predefined query ekleme/düzenleme modal component'i.
- JSON editor (MongoDB aggregation pipeline)
- Parameter definitions (yeni format: QueryParameterDefinition listesi)
- Parameter validation

### 4. ValidationDefinitionForm.vue
Validation definition ekleme/düzenleme modal component'i.
- Validation type'a göre dinamik form alanları
  - Expression-based: Expression editor (syntax highlighting, field name suggestions)
  - HTTP-based: URL, method, fields, response format açıklaması

### 5. PermissionsEditor.vue
Permissions yönetimi component'i. (⚠️ Backend'de henüz implement edilmemiş)
- Group/User selection (MngKeeper API)
- Multi-select component
- **Not:** Şu anda UI'da gösterilmeyecek veya disabled olacak

### 6. IndexDefinitionForm.vue
Index ekleme/düzenleme modal component'i.

---

## 📝 Form Validation

### Dataset Name:
- `@` prefix opsiyonel (backend otomatik ekler)
- Unique kontrolü (backend'de)
- Regex: `^@?[a-zA-Z][a-zA-Z0-9_-]*$`
- Minimum 2 karakter, maksimum 100 karakter
- İlk karakter harf olmalı
- Underscore (`_`) ve dash (`-`) destekleniyor

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
// Get datasets list (paginated)
GET /api/v1/datasets?pageNumber=1&pageSize=20
Response: PagedResultDto<DatasetResponseDto>

// Get dataset by name
GET /api/v1/datasets/{name}
Response: DatasetResponseDto

// Create dataset
POST /api/v1/datasets
Body: CreateDatasetDto
Response: DatasetResponseDto (201 Created)

// Update dataset
PUT /api/v1/datasets/{name}
Body: UpdateDatasetDto
Response: DatasetResponseDto (200 OK)

// Delete dataset (hard delete + __deletedDatas backup)
DELETE /api/v1/datasets/{name}
Response: 204 No Content

// Restore deleted dataset
POST /api/v1/datasets/{name}/restore
Response: DatasetResponseDto (200 OK)
```

**Not:** 
- `GET /api/v1/datasets` endpoint'inde şu anda `search` parametresi desteklenmiyor (backend'de henüz implement edilmemiş)
- Dataset name validation: `^@?[a-zA-Z][a-zA-Z0-9_-]*$` (regex) - `@` prefix opsiyonel, underscore ve dash destekleniyor

### DTO Yapıları:

**CreateDatasetDto:**
```typescript
{
  name: string;              // Required, e.g., "@books" or "books"
  description?: string;      // Optional
  category?: string;         // Optional, category ID reference
  forceSchema?: boolean;     // Default: true
  logging?: string;          // "none" | "self" | "common", Default: "none"
  publishMode?: string;      // "none" | "basic" | "full", Default: "none"
  fields?: FieldDefinition[]; // Optional
  validations?: ValidationDefinition[]; // Optional
  queries?: QueryDefinitionDto[]; // Optional
  indexList?: IndexDefinition[]; // Optional
}
```

**UpdateDatasetDto:**
```typescript
{
  description?: string;      // Optional
  category?: string;         // Optional
  forceSchema?: boolean;     // Optional
  logging?: string;          // Optional
  publishMode?: string;      // Optional
  fields?: FieldDefinition[]; // Optional
  validations?: ValidationDefinition[]; // Optional
  queries?: QueryDefinitionDto[]; // Optional
  indexList?: IndexDefinition[]; // Optional
}
```

**FieldDefinition:**
```typescript
{
  fieldType: string;         // "text" | "number" | "bool" | "datetime" | "object" | "relation" | "persons" | "personGroups" | "incremental"
  name: string;              // Required, unique within dataset
  title?: string;            // Optional, display title
  mandatory: boolean;        // Default: false
  unique: boolean;           // Default: false
  isArray: boolean;          // Default: false
  defaultValue?: any;        // Optional, field type'a göre farklı tipler
  relationDataset?: string;  // For relation type: target dataset name
  incrementalOptions?: {     // For incremental type
    format?: string;         // Format template (e.g., "ISBN-{year}-{0:D6}")
    startValue: number;      // Default: 1
    incrementStep: number;   // Default: 1
  };
  validation?: FieldValidationRules; // Optional, field-level validation rules
}
```

**FieldValidationRules:**
```typescript
{
  // Number fields için
  min?: number;              // Minimum value
  max?: number;              // Maximum value
  
  // Text fields için
  minLength?: number;        // Minimum length
  maxLength?: number;        // Maximum length
  pattern?: string;          // Regex pattern
  
  // Array fields için
  minItems?: number;         // Minimum items
  maxItems?: number;         // Maximum items
  
  // DateTime fields için
  minDate?: Date;            // Minimum date
  maxDate?: Date;            // Maximum date
  
  // Custom error message
  message?: string;          // Custom error message
}
```

**ValidationDefinition:**
```typescript
{
  name: string;              // Required, unique
  description?: string;      // Optional
  type: "expression" | "http"; // Required, validation type
  expression?: string;       // For expression type: expression string (e.g., "endDate > startDate")
  url?: string;              // For http type: validation endpoint URL
  method?: "GET" | "POST";   // For http type: HTTP method (default: "POST")
  fields?: string[];         // For http type: which fields to send (optional)
  when?: "create" | "update" | "both"; // When to execute (default: "both")
  order?: number;            // Execution order (default: 0)
}
```

**QueryDefinitionDto:**
```typescript
{
  name: string;              // Required, unique
  description?: string;      // Optional
  parameters?: QueryParameterDefinitionDto[]; // Optional, new format (preferred)
  // Backward compatibility: parameters can also be string[] (legacy format)
  pipeline: object[];        // Required, MongoDB aggregation pipeline (JSON array)
}
```

**QueryParameterDefinitionDto:**
```typescript
{
  name: string;              // Required, parameter name (used as :parameterName in pipeline)
  type: string;              // "text" | "number" | "bool" | "datetime", Default: "text"
  description?: string;      // Optional
  required: boolean;         // Default: true
}
```

**DatasetResponseDto:**
```typescript
{
  dataId: string;
  name: string;
  description?: string;
  category?: string;
  forceSchema: boolean;
  logging: string;
  publishMode: string;
  fieldsCount: number;
  fields?: FieldDefinition[];
  validationsCount: number;
  validations?: ValidationDefinition[];
  queriesCount: number;
  queries?: QueryDefinitionResponseDto[];
  indexListCount: number;
  indexList?: IndexDefinition[];
  createInfo: CreateInfo;
  lastUpdateInfo?: UpdateInfo;
  historyCount: number;
}
```

### Lookup Data:
```typescript
// Get dataset categories (paginated with search)
GET /api/v1/dataset-categories?pageNumber=1&pageSize=20&search=term
Response: PagedResultDto<DatasetCategoryResponseDto>

// Get groups (MngKeeper)
GET /api/keeper/group
Response: Group[]

// Get users (MngKeeper)
GET /api/keeper/user
Response: User[]
```

**Not:** Permissions field'ı backend'de henüz implement edilmemiştir. Permissions yönetimi için Step 4 (Permissions) şu anda UI'da gösterilebilir ancak backend'e gönderilmeyecek.

---

## 📋 Implementasyon Checklist

### Phase 1: Temel Form Yapısı
- [ ] DatasetForm.vue component oluştur
- [ ] Stepper yapısı (6 step)
- [ ] Temel bilgiler formu (Step 1)
- [ ] Form validation (VeeValidate)

### Phase 2: Field Management
- [ ] FieldDefinitionForm.vue component
- [ ] Field list management
- [ ] Field type'a göre dinamik form
- [ ] Default value input (field type'a göre)
- [ ] Field-level validation rules (field type'a göre)
- [ ] Relation field lookup
- [ ] Incremental field configurator
- [ ] Object field (not: schema field'ı yok, validation rules kullanılabilir)

### Phase 3: Queries & Validations
- [ ] QueryDefinitionForm.vue component
- [ ] Query parameters (yeni format: QueryParameterDefinition listesi)
- [ ] ValidationDefinitionForm.vue component
- [ ] Expression-based validation editor (syntax highlighting, field name suggestions)
- [ ] HTTP-based validation form (URL, method, fields)
- [ ] JSON editor component (syntax highlighting)

### Phase 4: Index Definitions
- [ ] IndexDefinitionForm.vue component

### Phase 5: Dataset List & Detail
- [ ] Dataset listesi sayfası
- [ ] Dataset detay sayfası
- [ ] Dataset düzenleme sayfası

### Phase 6: Permissions (Future)
- [ ] PermissionsEditor.vue component (⚠️ Backend'de henüz implement edilmemiş)
- [ ] Group/User selection (MngKeeper API)
- [ ] Multi-select component

### Phase 7: Integration & Testing
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
   - `books_by_publication_date_range` (startDate: datetime, endDate: datetime)

4. Validation Definitions:
   - Expression-based: `endDate > startDate`
   - HTTP-based: `https://api.example.com/validate` (POST, create only)

5. Permissions (⚠️ Backend'de henüz implement edilmemiş):
   - Read: managers
   - Write: managers

---

## 📚 İlgili Dokümantasyon

- **Backend Plan:** `MngDataGateway/docs/BOOKS_DATASET_PLAN.md`
- **Backend Status:** `MngDataGateway/docs/STATUS.md`
- **UI Roadmap:** `Mng.Ui/docs/RoadMap.md` (Phase 3.2)

---

---

## ⚠️ Bilinen Eksiklikler ve Notlar

### Backend Durumu (10 Ocak 2026):
1. ✅ **Dataset CRUD:** Tamamen implement edildi
2. ✅ **Field Types:** Tüm field type'lar destekleniyor (text, number, bool, datetime, object, relation, persons, personGroups, incremental)
3. ✅ **Queries:** Predefined queries destekleniyor
4. ✅ **IndexList:** Index tanımları destekleniyor
5. ❌ **Search Parameter:** `GET /api/v1/datasets` endpoint'inde `search` parametresi henüz desteklenmiyor
6. ⚠️ **Permissions:** 
   - ✅ Domain Entity (`PermissionsDefinition`) mevcut
   - ✅ `PermissionService` implementasyonu tamamlandı
   - ❌ **DTO Layer Eksiklikleri:**
     - `CreateDatasetDto` - Permissions field'ı eksik
     - `UpdateDatasetDto` - Permissions field'ı eksik
     - `DatasetResponseDto` - Permissions field'ı eksik
   - ❌ **Service Layer Eksiklikleri:**
     - `DatasetService.CreateAsync` - Permissions mapping eksik
     - `DatasetService.UpdateAsync` - Permissions mapping eksik
     - `DatasetService.MapToDto` - Permissions mapping eksik
   - **Referans:** `MngDataGateway/ROADMAP.md` - Phase 3: Dataset Authorization

### UI Implementation Öncelikleri:
1. **Phase 1:** Temel form yapısı ve Field Management (Step 1-2)
2. **Phase 2:** Queries ve Index Definitions (Step 3, 5)
3. **Phase 3:** Permissions (Step 4) - Backend implement edildikten sonra

---

**Son Güncelleme:** 10 Ocak 2026  
**Durum:** 📋 Planlama Güncellendi - Backend API'ler ile uyumlu hale getirildi - Implementation Bekleniyor

