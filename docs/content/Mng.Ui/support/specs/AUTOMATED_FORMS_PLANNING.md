# Automated Forms (AF) Planlama Dokümantasyonu

**Tarih:** 13 Ocak 2026  
**Son Güncelleme:** 12 Ocak 2026  
**Durum:** ✅ Temel Özellikler Tamamlandı - İyileştirmeler Devam Ediyor  
**Hedef:** Dataset'lere göre runtime'da dinamik form oluşturma ve CRUD işlemleri yapma

---

## 📋 Genel Bakış

Automated Forms (AF) sistemi, kullanıcıların bir dataset seçerek o dataset için otomatik olarak dinamik form oluşturmasını ve bu form ile liste görüntüleme, oluşturma, düzenleme ve silme (CRUD) işlemleri yapmasını sağlar.

### Temel Özellikler

1. **Dataset Bazlı Form Oluşturma**: Kullanıcı bir dataset seçer, sistem otomatik olarak form elemanlarını oluşturur
2. **Runtime Form Generation**: Dataset field'larına göre runtime'da dinamik DOM elemanları oluşturulur
3. **CRUD İşlemleri**: Liste görüntüleme, yeni kayıt ekleme, düzenleme, silme
4. **Side Menu Entegrasyonu**: Formlar side menu'ye eklenebilir (path veya dropdown ile form seçimi)
5. **Kullanıcı Özelleştirme**: Liste sütun seçimi, sıralama, filtreleme
6. **Validasyon Desteği**: Dataset validation rule'ları form validasyonunda kullanılır
7. **Yetkilendirme**: Side Menu Manager'daki yetkilendirme sistemi kullanılır

---

## 🗄️ Dataset Yapısı: @automated_forms

Form metadata'ları `@automated_forms` dataset'inde saklanacak.

### Dataset Schema

```typescript
{
  // Temel Bilgiler
  formName: string;              // Form adı (unique, örn: "Books Management")
  formCode: string;              // Form kodu (unique identifier, örn: "books-form")
  description?: string;           // Form açıklaması
  
  // Dataset Referansı
  datasetName: string;           // Hedef dataset adı (örn: "@books")
  
  // Side Menu Entegrasyonu
  sideMenuConfig: {
    enabled: boolean;            // Side menu'ye eklensin mi?
    menuItemId?: string;         // Side menu item ID (eğer eklendiyse)
    routeType: 'path' | 'form';  // Route tipi: 'path' (gerçek path) veya 'form' (dropdown ile form seçimi)
    routePath?: string;          // Eğer routeType='path' ise, gerçek route path (örn: "/apps/books-form")
  };
  
  // Liste Konfigürasyonu (Default - kullanıcı override edebilir)
  listConfig: {
    defaultColumns: string[];    // Varsayılan görünen sütunlar (field name'ler)
    defaultSortBy?: string;      // Varsayılan sıralama field'ı
    defaultSortOrder?: 'asc' | 'desc'; // Varsayılan sıralama yönü
    pageSize: number;            // Varsayılan sayfa boyutu
  };
  
  // Form Field Konfigürasyonu
  formConfig: {
    visibleFields: string[];     // Form'da gösterilecek field'lar (boş ise tümü)
    readonlyFields: string[];    // Read-only field'lar (örn: incremental fields)
    fieldOrder: string[];        // Field sıralaması (boş ise dataset field sırası)
    fieldLabels?: {              // Field label override'ları
      [fieldName: string]: string;
    };
    relationFieldConfig?: {      // Relation field'lar için ID ve display field seçimi
      [fieldName: string]: {
        idField: string;         // Değer olarak kullanılacak field (default: '__dataId')
        displayField: string;    // Dropdown'da gösterilecek field (required)
      };
    };
    fieldLayout?: {              // Field layout ayarları (column span, group)
      [fieldName: string]: {
        columnSpan?: number;     // 1-12 (default: 6 for normal fields, 12 for object fields)
        group?: string;          // Field group name (for grouping fields)
      };
    };
  };
  
  // Metadata
  isActive: boolean;             // Form aktif mi?
  createdBy?: string;            // Oluşturan kullanıcı
  createdAt?: Date;              // Oluşturulma tarihi
  updatedAt?: Date;              // Güncellenme tarihi
}
```

### Field Definitions (Önerilen)

```typescript
[
  {
    fieldType: 'text',
    name: 'formName',
    title: 'Form Adı',
    mandatory: true,
    unique: false,
    validation: {
      minLength: 3,
      maxLength: 100
    }
  },
  {
    fieldType: 'text',
    name: 'formCode',
    title: 'Form Kodu',
    mandatory: true,
    unique: true,
    validation: {
      pattern: '^[a-zA-Z0-9_-]+$', // Alphanumeric, underscore, dash
      minLength: 3,
      maxLength: 50
    }
  },
  {
    fieldType: 'text',
    name: 'description',
    title: 'Açıklama',
    mandatory: false
  },
  {
    fieldType: 'text',
    name: 'datasetName',
    title: 'Dataset Adı',
    mandatory: true,
    validation: {
      pattern: '^@?[a-zA-Z][a-zA-Z0-9_-]*$' // Dataset name pattern
    }
  },
  {
    fieldType: 'object',
    name: 'sideMenuConfig',
    title: 'Side Menu Ayarları',
    mandatory: false
  },
  {
    fieldType: 'object',
    name: 'listConfig',
    title: 'Liste Ayarları',
    mandatory: false
  },
  {
    fieldType: 'object',
    name: 'formConfig',
    title: 'Form Ayarları',
    mandatory: false
  },
  {
    fieldType: 'bool',
    name: 'isActive',
    title: 'Aktif',
    mandatory: true,
    defaultValue: true
  }
]
```

---

## 📄 Sayfa Yapısı

### 1. Automated Forms Listesi Sayfası

**Route:** `/apps/automated-forms`  
**Component:** `pages/apps/automated-forms/index.vue`

#### Özellikler:
- Form listesi tablosu (`v-data-table`)
- Form adı, dataset referansı, aktif durumu
- Side menu durumu (eklenmiş/eklenmemiş)
- Oluşturulma tarihi
- İşlemler: Görüntüle, Düzenle, Sil, Formu Aç
- Yeni Form oluştur butonu
- Filtreleme ve arama

#### Tablo Kolonları:
| Kolon | Açıklama | Sıralanabilir |
|-------|----------|---------------|
| Form Adı | `formName` | ✅ |
| Dataset | `datasetName` | ✅ |
| Aktif | `isActive` | ✅ |
| Side Menu | Side menu'ye eklenmiş mi? | ❌ |
| Oluşturulma | `createdAt` | ✅ |
| İşlemler | Actions | ❌ |

---

### 2. Automated Form Oluşturma/Düzenleme Sayfası

**Route:**
- `/apps/automated-forms/create` - Yeni form
- `/apps/automated-forms/edit/[formCode]` - Düzenleme

**Component:** `pages/apps/automated-forms/form.vue`

#### Form Bölümleri:

##### 2.1 Temel Bilgiler
```
┌─────────────────────────────────────┐
│ Form Adı *                          │
│ Books Management                    │
├─────────────────────────────────────┤
│ Form Kodu *                         │
│ books-form                          │
├─────────────────────────────────────┤
│ Açıklama                            │
│ Books dataset için otomatik form... │
├─────────────────────────────────────┤
│ Dataset Seçimi *                    │
│ [Dropdown: @books ▼]                │
├─────────────────────────────────────┤
│ ☑ Aktif                            │
└─────────────────────────────────────┘
```

##### 2.2 Side Menu Ayarları
```
┌─────────────────────────────────────┐
│ ☑ Side Menu'ye Ekle                │
├─────────────────────────────────────┤
│ Route Tipi *                        │
│ ○ Gerçek Path                      │
│ ● Form Dropdown                    │
├─────────────────────────────────────┤
│ Route Path (eğer "Gerçek Path")    │
│ /apps/books-form                    │
└─────────────────────────────────────┘
```

**Not:** Eğer "Form Dropdown" seçilirse, side menu'de dropdown ile form seçimi yapılacak. Bu durumda özel bir sayfa gerekecek (`/apps/automated-forms/select` gibi).

##### 2.3 Liste Ayarları (Opsiyonel)
```
┌─────────────────────────────────────┐
│ Varsayılan Sütunlar                │
│ [Multi-select: title, author, ...]  │
├─────────────────────────────────────┤
│ Varsayılan Sıralama                │
│ Field: [title ▼] Direction: [ASC]   │
├─────────────────────────────────────┤
│ Sayfa Boyutu                       │
│ [20 ▼]                              │
└─────────────────────────────────────┘
```

##### 2.4 Form Ayarları (Opsiyonel)
```
┌─────────────────────────────────────┐
│ Gösterilecek Field'lar             │
│ [Multi-select: title, author, ...]  │
│ (Boş ise tüm field'lar gösterilir) │
├─────────────────────────────────────┤
│ Read-only Field'lar                │
│ [Multi-select: isbn, ...]           │
│ (Incremental field'lar otomatik)   │
├─────────────────────────────────────┤
│ Field Sıralaması                   │
│ [Drag & Drop sıralama]              │
└─────────────────────────────────────┘
```

---

### 3. Automated Form Runtime Sayfası (CRUD)

**Route:** `/apps/automated-forms/view/[formCode]`  
**Component:** `pages/apps/automated-forms/view/[formCode].vue`

Bu sayfa dinamik olarak oluşturulur. Dataset field'larına göre form elemanları render edilir.

#### 3.1 Liste Görünümü

**Özellikler:**
- Dataset data'larını listeleyen tablo (`v-data-table`)
- Kullanıcı bazlı sütun seçimi (localStorage'da saklanır)
- Sütun sıralaması (drag & drop veya toggle)
- Sıralama (sorting)
- Filtreleme (her sütun için)
- Sayfalama (pagination)
- Satır seçimi (multi-select)
- Toplu işlemler (seçilen kayıtları sil, export, vb.)
- Yeni Kayıt butonu
- Düzenle butonu (her satırda)
- Sil butonu (her satırda)
- Export butonları (CSV, JSON)

**Sütun Seçimi UI:**
- Sağ üstte "Sütunlar" butonu
- Dialog içinde sütun checkbox'ları
- Sütun sıralaması (drag & drop)
- "Varsayılanları Sıfırla" butonu

**Kullanıcı Bazlı Sütun Ayarları:**
- Key: `af_columns_${formCode}_${userId}`
- Value: `{ columns: string[], order: string[] }`
- localStorage'da saklanır

#### 3.2 Oluşturma/Düzenleme Formu

**Özellikler:**
- Dataset field'larına göre dinamik form elemanları
- Field type'a göre uygun input component'leri:
  - `text` → `v-text-field`
  - `number` → `v-text-field` (type="number")
  - `bool` → `v-checkbox` veya `v-switch`
  - `datetime` → `v-text-field` (type="datetime-local") veya date picker
  - `object` → JSON editor (v-textarea + JSON.parse)
  - `relation` → Autocomplete (`v-autocomplete`) - dataset'ten veri çeker
  - `persons` → Autocomplete - kullanıcı listesi
  - `personGroups` → Autocomplete - grup listesi
  - `incremental` → Read-only text field
  - `isArray: true` → Repeatable section (v-for ile)
- Validation rule'ları uygulanır (min, max, minLength, maxLength, pattern, vb.)
- Mandatory field'lar işaretlenir (*)
- Unique field'lar için backend validation (submit'te kontrol)
- Read-only field'lar disabled

**Form Layout:**
```
┌─────────────────────────────────────┐
│ Form Başlığı (Form Adı)            │
├─────────────────────────────────────┤
│                                     │
│ [Dinamik Form Field'ları]          │
│                                     │
├─────────────────────────────────────┤
│ [İptal] [Kaydet]                    │
└─────────────────────────────────────┘
```

---

### 4. Form Seçim Sayfası (Side Menu Dropdown İçin)

**Route:** `/apps/automated-forms/select`  
**Component:** `pages/apps/automated-forms/select.vue`

**Kullanım Senaryosu:** Side menu'de "Form Dropdown" route tipi seçildiğinde, kullanıcı bu sayfada form seçer ve seçilen form açılır.

**Özellikler:**
- Aktif formların listesi
- Form kartları veya liste görünümü
- Form adı, dataset referansı, açıklama
- Form seçme butonu
- Arama/filtreleme

---

## 🔧 Teknik Detaylar

### Store Yapısı

#### automatedForms Store
**Dosya:** `stores/apps/automatedForms.ts`

```typescript
interface AutomatedFormState {
  forms: AutomatedForm[];
  currentForm: AutomatedForm | null;
  loading: boolean;
  error: string | null;
}

interface AutomatedForm {
  __dataId?: string;
  formName: string;
  formCode: string;
  description?: string;
  datasetName: string;
  sideMenuConfig: {
    enabled: boolean;
    menuItemId?: string;
    routeType: 'path' | 'form';
    routePath?: string;
  };
  listConfig: {
    defaultColumns: string[];
    defaultSortBy?: string;
    defaultSortOrder?: 'asc' | 'desc';
    pageSize: number;
  };
  formConfig: {
    visibleFields: string[];
    readonlyFields: string[];
    fieldOrder: string[];
    fieldLabels?: { [fieldName: string]: string };
  };
  isActive: boolean;
  createdAt?: Date;
  updatedAt?: Date;
}
```

**Actions:**
- `fetchForms()` - Tüm formları getir
- `fetchFormByCode(formCode: string)` - Form koduna göre getir
- `createForm(formData: CreateAutomatedFormDto)` - Yeni form oluştur
- `updateForm(formCode: string, formData: UpdateAutomatedFormDto)` - Form güncelle
- `deleteForm(formCode: string)` - Form sil

---

### Composable'lar

#### useAutomatedForm
**Dosya:** `composables/useAutomatedForm.ts`

Form runtime işlemleri için composable.

```typescript
export function useAutomatedForm(formCode: string) {
  // Form metadata'yı yükle
  const form = ref<AutomatedForm | null>(null);
  const dataset = ref<DatasetResponseDto | null>(null);
  
  // Dataset data'larını yükle (liste için)
  const dataItems = ref<any[]>([]);
  const totalCount = ref(0);
  const loading = ref(false);
  
  // Sütun ayarları (kullanıcı bazlı)
  const userColumns = ref<string[]>([]);
  const columnOrder = ref<string[]>([]);
  
  // CRUD operations
  const fetchData = async (options: DataTableOptions) => Promise<void>;
  const createData = async (data: any) => Promise<any>;
  const updateData = async (dataId: string, data: any) => Promise<any>;
  const deleteData = async (dataId: string) => Promise<void>;
  
  // Form field'larını generate et
  const generateFormFields = () => FormField[];
  
  // Validation rule'larını uygula
  const validateField = (fieldName: string, value: any) => ValidationResult;
  
  return {
    form,
    dataset,
    dataItems,
    totalCount,
    loading,
    userColumns,
    columnOrder,
    fetchData,
    createData,
    updateData,
    deleteData,
    generateFormFields,
    validateField
  };
}
```

#### useFormColumnSettings
**Dosya:** `composables/useFormColumnSettings.ts`

Kullanıcı bazlı sütun ayarları için composable.

```typescript
export function useFormColumnSettings(formCode: string, userId: string) {
  const storageKey = computed(() => `af_columns_${formCode}_${userId}`);
  
  const loadSettings = (): ColumnSettings | null => {
    // localStorage'dan yükle
  };
  
  const saveSettings = (settings: ColumnSettings): void => {
    // localStorage'a kaydet
  };
  
  const resetSettings = (): void => {
    // localStorage'dan sil
  };
  
  return {
    loadSettings,
    saveSettings,
    resetSettings
  };
}
```

---

### Component'ler

#### DynamicFormField
**Dosya:** `components/apps/automated-forms/DynamicFormField.vue`

Dataset field type'ına göre uygun input component'ini render eder.

```vue
<template>
  <!-- text -->
  <v-text-field
    v-if="field.fieldType === 'text'"
    :model-value="modelValue"
    @update:model-value="$emit('update:modelValue', $event)"
    :label="fieldLabel"
    :required="field.mandatory"
    :disabled="isReadonly"
    :rules="validationRules"
  />
  
  <!-- number -->
  <v-text-field
    v-else-if="field.fieldType === 'number'"
    type="number"
    :model-value="modelValue"
    @update:model-value="$emit('update:modelValue', $event)"
    :label="fieldLabel"
    :required="field.mandatory"
    :disabled="isReadonly"
    :rules="validationRules"
  />
  
  <!-- bool -->
  <v-switch
    v-else-if="field.fieldType === 'bool'"
    :model-value="modelValue"
    @update:model-value="$emit('update:modelValue', $event)"
    :label="fieldLabel"
    :disabled="isReadonly"
  />
  
  <!-- datetime -->
  <v-text-field
    v-else-if="field.fieldType === 'datetime'"
    type="datetime-local"
    :model-value="modelValue"
    @update:model-value="$emit('update:modelValue', $event)"
    :label="fieldLabel"
    :required="field.mandatory"
    :disabled="isReadonly"
    :rules="validationRules"
  />
  
  <!-- relation - Autocomplete -->
  <v-autocomplete
    v-else-if="field.fieldType === 'relation'"
    :model-value="modelValue"
    @update:model-value="$emit('update:modelValue', $event)"
    :items="relationItems"
    :label="fieldLabel"
    :required="field.mandatory"
    :disabled="isReadonly"
    :loading="loadingRelation"
    :rules="validationRules"
  />
  
  <!-- persons - User Autocomplete -->
  <v-autocomplete
    v-else-if="field.fieldType === 'persons'"
    :model-value="modelValue"
    @update:model-value="$emit('update:modelValue', $event)"
    :items="userItems"
    :label="fieldLabel"
    :required="field.mandatory"
    :disabled="isReadonly"
    :multiple="field.isArray"
    :rules="validationRules"
  />
  
  <!-- personGroups - Group Autocomplete -->
  <v-autocomplete
    v-else-if="field.fieldType === 'personGroups'"
    :model-value="modelValue"
    @update:model-value="$emit('update:modelValue', $event)"
    :items="groupItems"
    :label="fieldLabel"
    :required="field.mandatory"
    :disabled="isReadonly"
    :multiple="field.isArray"
    :rules="validationRules"
  />
  
  <!-- incremental - Read-only -->
  <v-text-field
    v-else-if="field.fieldType === 'incremental'"
    :model-value="modelValue"
    :label="fieldLabel"
    readonly
    variant="outlined"
  />
  
  <!-- object - JSON Editor -->
  <v-textarea
    v-else-if="field.fieldType === 'object'"
    :model-value="jsonString"
    @update:model-value="handleJsonUpdate"
    :label="fieldLabel"
    :required="field.mandatory"
    :disabled="isReadonly"
    :rules="validationRules"
    rows="4"
  />
</template>
```

#### ColumnSelector
**Dosya:** `components/apps/automated-forms/ColumnSelector.vue`

Liste sütun seçimi dialog'u.

```vue
<template>
  <v-dialog v-model="dialog" max-width="600">
    <v-card>
      <v-card-title>Sütun Seçimi</v-card-title>
      <v-card-text>
        <v-list>
          <draggable v-model="columns" item-key="name">
            <template #item="{ element }">
              <v-list-item>
                <template #prepend>
                  <v-checkbox
                    v-model="element.visible"
                    @click.stop
                  />
                </template>
                <v-list-item-title>{{ element.title }}</v-list-item-title>
                <template #append>
                  <v-icon>mdi-drag</v-icon>
                </template>
              </v-list-item>
            </template>
          </draggable>
        </v-list>
      </v-card-text>
      <v-card-actions>
        <v-spacer />
        <v-btn @click="resetDefaults">Varsayılanları Sıfırla</v-btn>
        <v-btn @click="dialog = false">İptal</v-btn>
        <v-btn color="primary" @click="save">Kaydet</v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>
```

---

## 🔌 API Entegrasyonu

### @automated_forms Dataset CRUD

```typescript
// Get forms list
GET /api/data/@automated_forms?pageNumber=1&pageSize=20

// Get form by code (query ile)
GET /api/data/@automated_forms?formCode=books-form

// Create form
POST /api/data/@automated_forms
Body: CreateAutomatedFormDto

// Update form
PUT /api/data/@automated_forms/{dataId}
Body: UpdateAutomatedFormDto

// Delete form
DELETE /api/data/@automated_forms/{dataId}
```

### Dataset Schema ve Data İşlemleri

```typescript
// Get dataset schema
GET /api/v1/datasets/{datasetName}

// Get dataset data (liste için)
GET /api/data/{datasetName}?pageNumber=1&pageSize=20&sortBy=title&sortOrder=asc

// Create data
POST /api/data/{datasetName}
Body: any (dataset field'larına göre)

// Update data
PUT /api/data/{datasetName}/{dataId}
Body: any

// Delete data
DELETE /api/data/{datasetName}/{dataId}
```

### Relation Field İçin Autocomplete

```typescript
// Relation dataset'ten veri çek (autocomplete için)
GET /api/data/{relationDatasetName}?pageNumber=1&pageSize=50&search={searchTerm}
```

### Users ve Groups (persons/personGroups için)

```typescript
// Users list (persons field için)
GET /api/users?pageNumber=1&pageSize=50&search={searchTerm}

// Groups list (personGroups field için)
GET /api/groups?pageNumber=1&pageSize=50&search={searchTerm}
```

---

## 🎯 Side Menu Entegrasyonu

### Route Tipi: "Gerçek Path"

Side menu'de normal route olarak eklenir.

**MenuItemForm.vue'da ekleme:**
- Route Type: "Gerçek Path" seçilirse
- `to` field'ına route path girilir (örn: `/apps/automated-forms/view/books-form`)
- Normal menu item olarak çalışır

### Route Tipi: "Form Dropdown"

Özel bir sayfa gereklidir (`/apps/automated-forms/select`).

**MenuItemForm.vue'da ekleme:**
- Route Type: "Form Dropdown" seçilirse
- `to` field'ına `/apps/automated-forms/select` girilir
- Kullanıcı bu sayfada form seçer ve seçilen form açılır

**Alternatif Yaklaşım (Daha İyi):**
- Side menu'de özel bir component render edilebilir
- Dropdown ile form seçimi yapılır
- Seçilen form route'una yönlendirilir

---

## 🔐 Yetkilendirme

### Permission Kontrolü

Side Menu Manager'daki yetkilendirme sistemi kullanılır.

**usePagePermissions Composable:**
- Sayfa erişimi kontrolü (view permission)
- DOM element yetkilendirme (create, update, delete permissions)

**CRUD Sayfasında:**
```typescript
const { canView, canCreate, canUpdate, canDelete } = usePagePermissions();

// Yeni Kayıt butonu
<v-btn v-if="canCreate" @click="openCreateDialog">Yeni Kayıt</v-btn>

// Düzenle butonu (tablo satırında)
<v-btn v-if="canUpdate" @click="openEditDialog(item)">Düzenle</v-btn>

// Sil butonu (tablo satırında)
<v-btn v-if="canDelete" @click="openDeleteDialog(item)">Sil</v-btn>
```

---

## 📦 Field Type Desteği Detayları

### text
- Component: `v-text-field`
- Validation: minLength, maxLength, pattern

### number
- Component: `v-text-field` (type="number")
- Validation: min, max

### bool
- Component: `v-switch` veya `v-checkbox`
- Validation: Yok

### datetime
- Component: `v-text-field` (type="datetime-local") veya date picker
- Validation: minDate, maxDate
- Format: ISO 8601 UTC

### object
- Component: `v-textarea` + JSON editor
- Validation: JSON format validation
- Display: JSON string olarak göster, parse ederek kaydet

### relation
- Component: `v-autocomplete`
- Data Source: Relation dataset'ten veri çek
- Display Field: Dataset'teki bir field (genellikle title, name, vb.)
- Value Field: `__dataId` veya belirli bir field
- Search: Backend'de search endpoint'i ile

### persons
- Component: `v-autocomplete` (multiple: isArray'e göre)
- Data Source: Users API
- Display Field: userName veya email
- Value Field: userId veya email

### personGroups
- Component: `v-autocomplete` (multiple: isArray'e göre)
- Data Source: Groups API
- Display Field: groupName
- Value Field: groupId

### incremental
- Component: `v-text-field` (readonly)
- Değer: Backend tarafından otomatik generate edilir
- Form'da readonly gösterilir

### isArray: true
- Her field type için repeatable section
- `v-for` ile field'lar tekrarlanır
- "Ekle", "Sil" butonları
- Validation: minItems, maxItems

---

## 🎨 UI/UX Tasarım Notları

### Liste Görünümü
- Vuetify `v-data-table` kullanılır
- Server-side pagination, sorting, filtering
- Sütun seçimi dialog'u
- Responsive design

### Form Görünümü
- Vuetify form component'leri kullanılır
- Field'lar `v-row` ve `v-col` ile grid layout
- Validation error mesajları field altında
- Loading state'leri
- Success/error toast mesajları

### Sütun Seçimi
- Dialog içinde checkbox listesi
- Drag & drop ile sıralama (vue-draggable-next kullanılabilir)
- "Varsayılanları Sıfırla" butonu
- localStorage'da kullanıcı bazlı saklama

---

## 📝 Implementation Checklist

### Phase 1: Dataset ve Backend Hazırlığı ✅ TAMAMLANDI
- [x] `@automated_forms` dataset'i oluştur
- [x] Dataset field definitions tanımla
- [x] Test verisi ekle

### Phase 2: Store ve Composable'lar ✅ TAMAMLANDI
- [x] `automatedForms` store oluştur
- [ ] `useAutomatedForm` composable oluştur (Gerekli değil, runtime sayfasında direkt kullanılıyor)
- [ ] `useFormColumnSettings` composable oluştur (Gelecek geliştirme)

### Phase 3: Form Yönetimi Sayfaları ✅ TAMAMLANDI
- [x] Form listesi sayfası (`/apps/automated-forms`)
- [x] Form oluşturma/düzenleme sayfası (`/apps/automated-forms/create` ve `/apps/automated-forms/edit/[formCode]`)
- [ ] Form görüntüleme sayfası (opsiyonel - gerekirse eklenecek)

### Phase 4: Runtime Component'ler ✅ TAMAMLANDI
- [x] `DynamicFormField` component
- [ ] `ColumnSelector` component (Gelecek geliştirme)
- [x] Form runtime sayfası (`/apps/automated-forms/view/[formCode]`)

### Phase 5: CRUD İşlemleri ✅ TAMAMLANDI
- [x] Liste görünümü (data table)
- [x] Oluşturma formu
- [x] Düzenleme formu
- [x] Silme işlemi
- [x] Validasyon entegrasyonu (DynamicFormField component'inde)
- [x] Array field desteği (multiple seçim, otomatik array dönüşümü)
- [x] Relation field display field konfigürasyonu
- [x] Field layout ayarları (column span, group)
- [x] Gelişmiş hata mesajı gösterimi (validation error details)

### Phase 6: Özelleştirme 🔄 KISMEN TAMAMLANDI
- [x] Liste sütun konfigürasyonu (formConfig.columns - visible, order, sortable, filterable)
- [x] Sıralama (sorting)
- [x] Filtreleme (field-based filtering)
- [x] Sayfalama (pagination)
- [x] Global arama (search) - enableSearch özelliği
- [ ] Kullanıcı bazlı sütun ayarları (localStorage - gelecek geliştirme)
- [ ] Sütun sıralaması drag & drop (gelecek geliştirme)
- [x] Export işlemleri (CSV, JSON - client-side) ✅ TAMAMLANDI
- [ ] Server-side export işlemleri (büyük veri setleri için streaming export - gelecek geliştirme)

### Phase 7: Side Menu Entegrasyonu ✅ TAMAMLANDI
- [x] Side Menu Manager'da "Kayıtlı Formlar" dropdown'u eklendi

### Phase 8: Relation Field ve Layout Konfigürasyonu ✅ TAMAMLANDI
- [x] Relation field'lar için ID ve display field seçimi
- [x] Form tanımlama ekranında relation field konfigürasyonu UI'ı
- [x] DynamicFormField'da relation config kullanımı
- [x] Field layout ayarları (column span, group)
- [x] Array field'lar için multiple seçim desteği
- [x] Array field değer dönüşümü (tek değer → array)
- [x] Hata mesajı parsing iyileştirmesi
- [x] Route parametresi değişikliği izleme bug fix
- [x] "Gerçek Path" route tipi entegrasyonu (dropdown'dan form seçildiğinde otomatik path oluşturuluyor)
- [ ] "Form Dropdown" route tipi entegrasyonu (gelecek geliştirme - şu an gerekli değil)

### Phase 8: Yetkilendirme ⏳ BEKLİYOR
- [ ] Permission kontrolü entegrasyonu (usePagePermissions composable ile)
- [ ] DOM element yetkilendirme (create, update, delete butonları için)
- [x] Read-only state'leri (formConfig.readonlyFields ile)

### Phase 9: Test ve İyileştirmeler ⏳ DEVAM EDİYOR
- [x] Temel CRUD işlemleri test edildi (create, read, update, delete)
- [x] Liste, sıralama, filtreleme, arama test edildi
- [ ] Tüm field type'lar için kapsamlı test (gelecek)
- [ ] Validation testleri (gelecek)
- [ ] Permission testleri (gelecek)
- [ ] UI/UX iyileştirmeleri (gelecek)

---

## 🔄 Gelecek Geliştirmeler (Future Enhancements)

1. **Form Template'leri**: Önceden tanımlanmış form şablonları
2. **Conditional Fields**: Koşullu field gösterimi (örn: bir field değerine göre başka field göster)
3. **Form Actions**: Özel form action'ları (workflow, notification, vb.)
4. **Form Versions**: Form versiyonlama ve geri dönme
5. **Bulk Operations**: Toplu işlemler (toplu güncelleme, toplu silme)
6. **Server-Side Export**: Büyük veri setleri için server-side streaming export (CSV, JSON)
   - Backend'de formatlama ve streaming desteği
   - Client bellek kullanımını azaltma
   - Limit kısıtlaması olmadan export
7. **Export Templates**: Özel export template'leri
8. **Form Analytics**: Form kullanım istatistikleri
9. **Form Sharing**: Form paylaşımı ve embed desteği

---

## 📚 İlgili Dokümantasyon

- `docs/Mng.Ui/specs/DATASET_UI_DESIGN.md` - Dataset UI tasarımı
- `docs/Mng.Ui/specs/SIDE_MENU_PLANNING.md` - Side Menu planlama
- `MngDataGateway/docs/STATUS.md` - Backend API durumu
- `docs/MngDataGateway/api/DATASET_SCHEMA_SUMMARY.md` - Dataset schema özeti

---

**Son Güncelleme:** 13 Ocak 2026  
**Durum:** Planlama Aşaması
