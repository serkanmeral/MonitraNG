# Mng.UI - Development Roadmap

## 📋 Genel Bakış

**Mng.UI**, MonitraNG ekosisteminin frontend uygulamasıdır. MaterialPro admin template tabanlı, modern ve kullanıcı dostu bir arayüz sağlar.

### Teknoloji Stack

- ✅ **Nuxt 3** - Vue 3 framework
- ✅ **Vuetify 3.7.1** - Material Design component library
- ✅ **TypeScript** - Type-safe development
- ✅ **Pinia** - State management
- ✅ **ApexCharts** - Data visualization
- ✅ **Axios** - HTTP client
- ✅ **VeeValidate** - Form validation

---

## 🏗️ Mimari Yapı

### Template Yapısı

```
Mng.Ui/
├── components/          # Vue component'leri
│   ├── apps/          # Uygulama-specific component'ler
│   ├── dashboards/    # Dashboard widget'ları
│   ├── forms/         # Form component'leri
│   ├── lc/            # Layout component'leri (sidebar, header)
│   ├── shared/        # Paylaşılan component'ler
│   └── ui-components/ # UI bileşenleri
├── pages/             # Nuxt file-based routing
├── stores/            # Pinia store'ları
├── services/          # API servis katmanı
├── types/             # TypeScript type definitions
└── utils/             # Yardımcı fonksiyonlar
```

### Component Standartları

1. **Sayfa Yapısı:**
   - `BaseBreadcrumb` - Sayfa başlığı ve breadcrumb navigation
   - `v-card` - Vuetify card container
   - `UiParentCard` / `AppBaseCard` - Özelleştirilmiş card component'leri

2. **Layout Sistemi:**
   - Vertical/Horizontal layout desteği
   - RTL (Right-to-Left) desteği
   - Responsive design
   - Customizer (tema, sidebar ayarları)

3. **API Entegrasyonu:**
   - `apiService.ts` - Merkezi API servis katmanı
   - JWT token authentication
   - Error handling

---

## 🎯 Geliştirme Planı

### Phase 1: Temel Altyapı ve Authentication ✅

**Durum:** Tamamlandı

- ✅ MaterialPro template entegrasyonu
- ✅ Nuxt 3 + Vuetify 3 kurulumu
- ✅ TypeScript yapılandırması
- ✅ Pinia store yapısı
- ✅ API servis katmanı (`apiService.ts`)
- ✅ Layout component'leri (sidebar, header)
- ✅ Authentication sayfaları (login, register)

---

### Phase 2: Domain Yönetimi Sayfaları 🚧

**Durum:** Planlama Aşaması

#### 2.1 Domain Listesi Sayfası
- **Route:** `/apps/domains`
- **Component:** `pages/apps/domains/index.vue`
- **Özellikler:**
  - Domain listesi tablosu
  - Pagination
  - Filtreleme ve arama
  - Domain detay görüntüleme
  - Domain durumu göstergesi

#### 2.2 Domain Oluşturma Sayfası
- **Route:** `/apps/domains/create`
- **Component:** `pages/apps/domains/create.vue`
- **Özellikler:**
  - Domain oluşturma formu
  - Form validation (VeeValidate)
  - Admin kullanıcı bilgileri
  - Başarı/hata mesajları

#### 2.3 Domain Detay Sayfası
- **Route:** `/apps/domains/[id].vue`
- **Component:** `pages/apps/domains/[id].vue`
- **Özellikler:**
  - Domain bilgileri görüntüleme
  - Domain istatistikleri
  - İlişkili kullanıcılar ve gruplar
  - Domain ayarları

**API Endpoints:**
- `GET /api/domain` - Domain listesi
- `POST /api/domain` - Domain oluşturma
- `GET /api/domain/{domainId}` - Domain detayı

---

### Phase 3: Dataset Yönetimi Sayfaları 📋

**Durum:** Planlama Aşaması

#### 3.1 Dataset Listesi Sayfası
- **Route:** `/apps/datasets`
- **Component:** `pages/apps/datasets/index.vue`
- **Özellikler:**
  - Dataset listesi (pagination)
  - Kategori filtreleme
  - Dataset arama
  - Dataset durumu

#### 3.2 Dataset Oluşturma/Düzenleme Sayfası
- **Route:** `/apps/datasets/create` | `/apps/datasets/edit/[name].vue`
- **Component:** `pages/apps/datasets/form.vue`
- **Özellikler:**
  - Dataset schema formu
  - Field tanımlama (9 field type)
  - Validation rules
  - Query definitions
  - Index management
  - Incremental field configuration

#### 3.3 Dataset Detay Sayfası
- **Route:** `/apps/datasets/[name].vue`
- **Component:** `pages/apps/datasets/[name].vue`
- **Özellikler:**
  - Schema görüntüleme
  - Field listesi ve detayları
  - Validation rules
  - Query definitions
  - Dataset istatistikleri

**API Endpoints:**
- `GET /api/datasets` - Dataset listesi
- `POST /api/datasets` - Dataset oluşturma
- `GET /api/datasets/{name}` - Dataset detayı
- `PUT /api/datasets/{name}` - Dataset güncelleme
- `DELETE /api/datasets/{name}` - Dataset silme

---

### Phase 4: Data Management Sayfaları 📊

**Durum:** Planlama Aşaması

#### 4.1 Data Listesi Sayfası
- **Route:** `/apps/data/[datasetName]`
- **Component:** `pages/apps/data/[datasetName].vue`
- **Özellikler:**
  - Dataset verilerini listeleme
  - Pagination (skip/limit)
  - Filtreleme (filter query)
  - Sıralama (sort)
  - Field selection (fields)
  - Relation expansion (expand)
  - Export functionality

#### 4.2 Data Detay Sayfası
- **Route:** `/apps/data/[datasetName]/[dataId].vue`
- **Component:** `pages/apps/data/[datasetName]/[dataId].vue`
- **Özellikler:**
  - Tek kayıt detay görüntüleme
  - Relation expansion
  - History görüntüleme (showHistory)
  - Edit/Delete işlemleri

#### 4.3 Data Oluşturma/Düzenleme Sayfası
- **Route:** `/apps/data/[datasetName]/create` | `/apps/data/[datasetName]/edit/[dataId].vue`
- **Component:** `pages/apps/data/[datasetName]/form.vue`
- **Özellikler:**
  - Dinamik form oluşturma (schema-based)
  - Field type'a göre input component'leri
  - Relation field'lar için lookup
  - Validation
  - Auto-increment field support

**API Endpoints:**
- `GET /api/data/{datasetName}` - Data listesi
- `GET /api/data/{datasetName}/{dataId}` - Data detayı
- `POST /api/data/{datasetName}` - Data oluşturma
- `PUT /api/data/{datasetName}/{dataId}` - Data güncelleme
- `DELETE /api/data/{datasetName}/{dataId}` - Data silme

---

### Phase 5: Yetkilendirme ve Sayfa Yönetimi Sistemi 🔐

**Durum:** Planlama Aşaması

#### 5.1 Sayfa ve Menü Yönetimi Dataset'i

**MongoDB Dataset:** `@pages` (MngDataGateway'de)

**Dataset Schema:**
```typescript
{
  _id: ObjectId,
  name: string,              // Sayfa unique identifier (örn: "domains-list")
  title: string,             // Sayfa başlığı (örn: "Domain Listesi")
  route: string,             // Nuxt route path (örn: "/apps/domains")
  icon: string,              // Icon name/class (örn: "mdi-domain")
  menuItem: boolean,         // Menüde gösterilsin mi?
  menuOrder: number,         // Menü sıralaması
  menuParent?: string,       // Parent menu item name (hierarchical menu)
  category?: string,         // Sayfa kategorisi (örn: "Management", "Reports")
  isActive: boolean,         // Sayfa aktif mi?
  permissions: {
    viewGroups: string[],   // View yetkisi olan gruplar
    editGroups: string[]     // Edit yetkisi olan gruplar
  },
  metadata: {
    description?: string,
    requiresAuth: boolean,   // Authentication gerekli mi?
    layout?: string          // Layout override (default: "default")
  },
  createdAt: DateTime,
  updatedAt: DateTime
}
```

**Özellikler:**
- Sayfa tanımları MongoDB'de merkezi olarak yönetilir
- Menü yapısı dinamik olarak dataset'ten oluşturulur
- Sayfa yetkileri grup bazlı tanımlanır (view/edit)
- Admin kullanıcılar tüm sayfalara tüm yetkilerle erişir

#### 5.2 Yetkilendirme Store (Permission Store)

**Store:** `stores/permission.ts`

**State:**
```typescript
{
  pages: PageDefinition[],    // Tüm sayfa tanımları
  userPermissions: Map<string, PermissionLevel>, // Kullanıcının sayfa bazlı yetkileri
  menuItems: MenuItem[],      // Filtrelenmiş menü öğeleri
  isLoading: boolean
}
```

**Actions:**
- `loadPages()` - MongoDB'den sayfa tanımlarını yükle
- `checkPermission(pageName: string, level: 'view' | 'edit')` - Yetki kontrolü
- `getUserMenuItems()` - Kullanıcının erişebileceği menü öğelerini getir
- `canAccessPage(route: string)` - Sayfa erişim kontrolü
- `canEditPage(route: string)` - Sayfa düzenleme yetkisi kontrolü

**Getters:**
- `hasViewPermission(pageName: string)` - View yetkisi var mı?
- `hasEditPermission(pageName: string)` - Edit yetkisi var mı?
- `filteredMenuItems` - Kullanıcının erişebileceği menü öğeleri

#### 5.3 Yetkilendirme Middleware

**Middleware:** `middleware/permission.ts`

**Özellikler:**
- Sayfa yüklemeden önce yetki kontrolü
- Admin kullanıcılar için bypass
- Yetkisiz erişimde 403 sayfasına yönlendirme
- Route bazlı yetki kontrolü

**Kullanım:**
```typescript
// pages/apps/domains/index.vue
definePageMeta({
  middleware: 'permission',
  permission: {
    page: 'domains-list',
    level: 'view' // veya 'edit'
  }
});
```

#### 5.4 Dinamik Menü Component'i

**Component:** `components/lc/Full/vertical-sidebar/DynamicMenu.vue`

**Özellikler:**
- MongoDB'den sayfa tanımlarını yükler
- Kullanıcı yetkilerine göre menüyü filtreler
- Hierarchical menu desteği (parent-child)
- Icon rendering
- Active route highlighting
- Menu caching (performance)

**Mevcut Component Güncellemesi:**
- `components/lc/Full/vertical-sidebar/index.vue` güncellenecek
- `components/lc/Full/vertical-sidebar/sidebarItem.ts` yerine dinamik menü kullanılacak

#### 5.5 Sayfa Yönetimi UI

**Route:** `/apps/pages` (Admin only)

**Component:** `pages/apps/pages/index.vue`

**Özellikler:**
- Sayfa listesi (CRUD)
- Sayfa oluşturma/düzenleme formu
- Grup bazlı yetki atama (view/edit)
- Menü sıralaması yönetimi
- Sayfa aktif/pasif yapma
- Icon seçici

**Form Alanları:**
- Name (unique identifier)
- Title
- Route
- Icon
- Menu Item (checkbox)
- Menu Order
- Menu Parent (dropdown)
- Category
- View Groups (multi-select)
- Edit Groups (multi-select)
- Description
- Requires Auth
- Layout Override

#### 5.6 Yetki Kontrolü Helper Functions

**Utils:** `utils/permissions.ts`

**Functions:**
```typescript
// Sayfa yetkisi kontrolü
export function canViewPage(pageName: string): boolean
export function canEditPage(pageName: string): boolean

// Route yetkisi kontrolü
export function canAccessRoute(route: string): boolean

// Component'lerde kullanım için composable
export function usePermissions() {
  return {
    canView: (pageName: string) => boolean,
    canEdit: (pageName: string) => boolean,
    hasPermission: (pageName: string, level: 'view' | 'edit') => boolean
  }
}
```

#### 5.7 Admin Bypass Mekanizması

**Mantık:**
- JWT token'da `isAdmin: true` ise tüm yetkiler verilir
- Admin kullanıcılar için permission kontrolü bypass edilir
- `authStore.isAdmin` getter'ı kullanılır

**Implementation:**
```typescript
// stores/permission.ts
function checkPermission(pageName: string, level: 'view' | 'edit'): boolean {
  // Admin bypass
  if (authStore.isAdmin) {
    return true;
  }
  
  // Normal kullanıcı kontrolü
  const page = pages.find(p => p.name === pageName);
  if (!page) return false;
  
  const userGroups = authStore.userGroups;
  
  if (level === 'view') {
    return page.permissions.viewGroups.some(g => userGroups.includes(g));
  } else if (level === 'edit') {
    return page.permissions.editGroups.some(g => userGroups.includes(g));
  }
  
  return false;
}
```

**API Endpoints (MngDataGateway):**
- `GET /api/data/@pages` - Sayfa listesi
- `GET /api/data/@pages/{pageId}` - Sayfa detayı
- `POST /api/data/@pages` - Sayfa oluşturma
- `PUT /api/data/@pages/{pageId}` - Sayfa güncelleme
- `DELETE /api/data/@pages/{pageId}` - Sayfa silme

**Dataset Tanımı:**
- Dataset adı: `@pages`
- Domain bazlı (her domain kendi sayfalarını yönetir)
- Default sayfalar domain oluşturulurken eklenebilir

**Default Sayfalar (Domain oluşturulurken):**
- Dashboard (`/dashboards/analytical`)
- Domain Management (`/apps/domains`) - Admin only
- Dataset Management (`/apps/datasets`) - Admin only
- User Management (`/apps/users`) - Admin only
- Group Management (`/apps/groups`) - Admin only
- Page Management (`/apps/pages`) - Admin only

---

### Phase 6: Dil Desteği (i18n) Sistemi 🌐

**Durum:** Planlama Aşaması

#### 6.1 Mevcut Durum

**Mevcut Yapı:**
- ✅ `vue-i18n` 9.9.1 kurulu
- ✅ `plugins/vuetify.ts` içinde i18n yapılandırması mevcut
- ✅ `utils/locales/` klasöründe dil dosyaları var (en, fr, ar, zh)
- ✅ `messages.ts` ile dil dosyaları import ediliyor
- ✅ Header'da `LanguageDD` component'i mevcut (4 dil: en, fr, ro, zh)

**Eksikler:**
- ❌ Türkçe dil desteği yok
- ❌ Dinamik dil değiştirme ve localStorage kaydı yok
- ❌ Component'lerde i18n kullanımı yok (hardcoded metinler)
- ❌ Sayfa bazlı çeviri yönetimi yok

#### 6.2 Dil Desteği Gereksinimleri

**Desteklenecek Diller:**
- 🇹🇷 **Türkçe (tr)** - Varsayılan dil
- 🇬🇧 **İngilizce (en)** - Fallback dil

**Gelecekte Eklenebilecek:**
- Diğer diller (ihtiyaca göre)

#### 6.3 Dil Dosyaları Yapısı

**Klasör Yapısı:**
```
utils/locales/
├── tr.json          # Türkçe çeviriler
├── en.json          # İngilizce çeviriler
└── messages.ts      # Dil dosyalarını import eden dosya
```

**Çeviri Key Yapısı:**
```typescript
{
  // Genel
  "common": {
    "save": "Kaydet",
    "cancel": "İptal",
    "delete": "Sil",
    "edit": "Düzenle",
    "create": "Oluştur",
    "search": "Ara",
    "filter": "Filtrele",
    "export": "Dışa Aktar"
  },
  
  // Sayfalar
  "pages": {
    "dashboard": {
      "title": "Kontrol Paneli",
      "welcome": "Hoş Geldiniz"
    },
    "domains": {
      "title": "Domain Yönetimi",
      "list": "Domain Listesi",
      "create": "Domain Oluştur"
    }
  },
  
  // Menü
  "menu": {
    "dashboard": "Kontrol Paneli",
    "domains": "Domainler",
    "datasets": "Dataset'ler",
    "users": "Kullanıcılar",
    "groups": "Gruplar"
  },
  
  // Formlar
  "forms": {
    "validation": {
      "required": "{field} gereklidir",
      "minLength": "{field} en az {min} karakter olmalıdır"
    }
  },
  
  // Hata Mesajları
  "errors": {
    "loginFailed": "Giriş başarısız",
    "unauthorized": "Yetkiniz yok",
    "notFound": "Sayfa bulunamadı"
  }
}
```

#### 6.4 Locale Store

**Store:** `stores/locale.ts`

**State:**
```typescript
{
  locale: string,           // Mevcut dil (tr, en)
  availableLocales: string[], // Desteklenen diller
  isLoading: boolean
}
```

**Actions:**
- `setLocale(locale: string)` - Dil değiştir ve localStorage'a kaydet
- `initializeLocale()` - localStorage'dan veya tarayıcı dilinden dil yükle
- `loadTranslations()` - Çeviri dosyalarını yükle

**Getters:**
- `currentLocale` - Mevcut dil
- `isTurkish` - Türkçe mi?
- `isEnglish` - İngilizce mi?

#### 6.5 Locale Plugin

**Plugin:** `plugins/locale.client.ts`

**Özellikler:**
- Uygulama başlangıcında locale store'u initialize eder
- localStorage'dan kaydedilmiş dili yükler
- Tarayıcı diline göre otomatik seçim (Türkçe varsa Türkçe, değilse İngilizce)
- i18n instance'ını locale store ile senkronize eder

#### 6.6 Dil Değiştirme Component'i

**Component:** `components/lc/Full/vertical-header/LanguageDD.vue` (Güncellenecek)

**Özellikler:**
- Sadece Türkçe ve İngilizce gösterir
- Dil değiştirme butonu
- Seçili dilin bayrağı gösterilir
- Dil değiştiğinde localStorage'a kaydedilir
- Sayfa yenilenmeden dil değişir

#### 6.7 Component'lerde i18n Kullanımı

**Composable:**
```typescript
// Component içinde
const { t } = useI18n();

// Kullanım
<h1>{{ t('pages.dashboard.title') }}</h1>
<v-btn>{{ t('common.save') }}</v-btn>
```

**Helper Functions:**
```typescript
// utils/i18n.ts
export function useTranslation() {
  const { t, locale } = useI18n();
  return { t, locale };
}

// Parametreli çeviri
t('forms.validation.required', { field: 'Kullanıcı adı' })
```

#### 6.8 Sayfa Bazlı Çeviri Yönetimi

**Yaklaşım:**
- Her sayfa için çeviri key'leri `pages.{pageName}` altında tutulur
- Component'ler için `components.{componentName}` altında tutulur
- Ortak çeviriler `common` altında tutulur

**Örnek:**
```json
{
  "pages": {
    "login": {
      "title": "Giriş Yap",
      "username": "Kullanıcı Adı",
      "password": "Şifre",
      "submit": "Giriş Yap"
    },
    "domains": {
      "title": "Domain Yönetimi",
      "list": {
        "title": "Domain Listesi",
        "create": "Yeni Domain"
      }
    }
  }
}
```

#### 6.9 Vuetify Locale Entegrasyonu

**Yapılandırma:**
- Vuetify component'lerinin kendi çevirileri için Vuetify locale desteği
- Vuetify'nin varsayılan mesajları (validation, date picker vb.) için locale ayarı

#### 6.10 Çeviri Yönetimi Best Practices

**Key Naming Convention:**
- Hierarchical yapı: `category.subcategory.key`
- Örnek: `pages.domains.list.title`
- Kısa ve açıklayıcı key'ler

**Çeviri Dosyaları Yönetimi:**
- Her dil için ayrı JSON dosyası
- TypeScript type safety için type definitions
- Çeviri key'lerinin otomatik tamamlanması (IDE desteği)

**Eksik Çeviri Yönetimi:**
- Fallback mekanizması: Çeviri bulunamazsa key gösterilir
- Development modunda eksik çeviri uyarıları
- Production'da fallback locale (İngilizce) kullanılır

#### 6.11 Migration Stratejisi

**Adım 1: Altyapı**
- Locale store oluştur
- Locale plugin oluştur
- Dil dosyalarını hazırla (tr.json, en.json)

**Adım 2: Temel Sayfalar**
- Login sayfası
- Dashboard sayfası
- Header ve Sidebar component'leri

**Adım 3: Yeni Sayfalar**
- Yeni eklenen her sayfa i18n ile başlar
- Hardcoded metin kullanılmaz

**Adım 4: Mevcut Sayfalar**
- Mevcut sayfaları aşamalı olarak i18n'e çevir
- Öncelik: En çok kullanılan sayfalar

#### 6.12 TypeScript Type Definitions

**Types:** `types/i18n.ts`

```typescript
// Çeviri key'lerinin type safety için
export interface TranslationKeys {
  common: {
    save: string;
    cancel: string;
    // ...
  };
  pages: {
    dashboard: {
      title: string;
      // ...
    };
    // ...
  };
  // ...
}
```

**Kullanım:**
```typescript
// Type-safe çeviri
const t = useTypedI18n<TranslationKeys>();
t('common.save'); // TypeScript otomatik tamamlama
```

**API Endpoints:**
- Çeviri dosyaları statik JSON dosyaları olarak tutulur
- Runtime'da API'den çeviri yükleme (gelecekte eklenebilir)

**Öncelik:**
- Phase 5'ten (Yetkilendirme) sonra
- Phase 7'den (Component Library) önce
- Yeni sayfalar i18n ile başlayacak

**Notlar:**
- Türkçe varsayılan dil olacak
- İngilizce fallback dil olacak
- Dil tercihi localStorage'da saklanacak
- Tarayıcı diline göre otomatik seçim yapılacak

---

### Phase 7: User & Group Management Sayfaları 👥

**Durum:** Planlama Aşaması

#### 6.1 User Management
- **Route:** `/apps/users`
- **Component:** `pages/apps/users/index.vue`
- **Özellikler:**
  - User listesi
  - User oluşturma/düzenleme
  - User detay sayfası
  - User-group assignment
  - Yetki kontrolü: Admin veya `users` sayfası için edit yetkisi

#### 6.2 Group Management
- **Route:** `/apps/groups`
- **Component:** `pages/apps/groups/index.vue`
- **Özellikler:**
  - Group listesi
  - Group oluşturma/düzenleme
  - Group detay sayfası
  - Group-user assignment
  - Group-permission assignment (sayfa bazlı)
  - Yetki kontrolü: Admin veya `groups` sayfası için edit yetkisi

**API Endpoints:**
- `GET /api/users` - User listesi
- `POST /api/users` - User oluşturma
- `GET /api/users/{userId}` - User detayı
- `PUT /api/users/{userId}` - User güncelleme
- `GET /api/groups` - Group listesi
- `POST /api/groups` - Group oluşturma
- `GET /api/groups/{groupId}` - Group detayı
- `PUT /api/groups/{groupId}` - Group güncelleme

---

**Durum:** Planlama Aşaması

#### 6.1 Ana Dashboard
- **Route:** `/dashboards/main`
- **Component:** `pages/dashboards/main.vue`
- **Özellikler:**
  - Domain istatistikleri
  - Dataset istatistikleri
  - Son aktiviteler
  - Hızlı erişim widget'ları

#### 6.2 Domain Dashboard
- **Route:** `/dashboards/domain/[domainId]`
- **Component:** `pages/dashboards/domain/[domainId].vue`
- **Özellikler:**
  - Domain-specific istatistikler
  - Dataset kullanım grafikleri
  - User activity charts
  - Storage usage

---

### Phase 7: Component Library Geliştirme 🧩

**Durum:** Planlama Aşaması

#### 7.1 Shared Components
- `DataTable.vue` - Generic data table component
- `DynamicForm.vue` - Schema-based form component
- `FieldRenderer.vue` - Field type'a göre input renderer
- `RelationLookup.vue` - Relation field lookup component
- `PaginationControls.vue` - Pagination component
- `FilterPanel.vue` - Advanced filtering component

#### 7.2 Form Components
- `TextField.vue` - Text input (text field type)
- `NumberField.vue` - Number input (number field type)
- `BooleanField.vue` - Checkbox/Switch (bool field type)
- `DateTimeField.vue` - Date/Time picker (datetime field type)
- `ObjectField.vue` - JSON editor (object field type)
- `RelationField.vue` - Relation selector (relation field type)
- `PersonsField.vue` - User selector (persons field type)
- `PersonGroupsField.vue` - Group selector (personGroups field type)

---

### Phase 8: State Management & API Integration 🔌

**Durum:** Planlama Aşaması

#### 8.1 Pinia Stores
- `authStore.ts` - Authentication state
- `domainStore.ts` - Domain management
- `datasetStore.ts` - Dataset management
- `dataStore.ts` - Data management
- `userStore.ts` - User management
- `groupStore.ts` - Group management

#### 8.2 API Service Enhancement
- JWT token management
- Request/Response interceptors
- Error handling
- Loading states
- Retry logic
- Request caching

---

### Phase 9: UI/UX İyileştirmeleri 🎨

**Durum:** Planlama Aşaması

- Loading states ve skeleton screens
- Error handling ve user feedback
- Toast notifications
- Confirmation dialogs
- Empty states
- Responsive design improvements
- Accessibility (a11y) improvements
- Dark mode optimizations

---

### Phase 10: Testing & Documentation 📝

**Durum:** Planlama Aşaması

- Component unit tests
- E2E tests
- API integration tests
- Storybook documentation
- Component usage examples
- API integration guide

---

## 🔗 Backend Entegrasyonları

### MngKeeper API
- **Base URL:** `https://localhost:5001`
- **Endpoints:**
  - Domain management
  - User management
  - Group management
  - Authentication

### MngDataGateway API
- **Base URL:** `https://localhost:5011` (runtime config'den)
- **Endpoints:**
  - Dataset management
  - Data CRUD operations
  - Query operations

---

## 📦 Bağımlılıklar

### Mevcut Bağımlılıklar
- Nuxt 3.13.2
- Vue 3.5.7
- Vuetify 3.7.1
- Pinia 2.2.2
- Axios 1.7.6
- ApexCharts 3.45.2
- VeeValidate 4.6.7
- TypeScript

### Eklenmesi Gerekenler
- (Gerekirse) Additional form libraries
- (Gerekirse) Chart libraries
- (Gerekirse) Date picker libraries

---

---

## 📝 Notlar

### Template Standartları
- Tüm sayfalar `BaseBreadcrumb` kullanmalı
- `v-card` ile sayfa içeriği sarmalanmalı
- Vuetify component'leri tercih edilmeli
- TypeScript tip güvenliği sağlanmalı
- Pinia store'ları state management için kullanılmalı

### API Entegrasyonu
- JWT token authentication
- Error handling
- Loading states
- Request/Response interceptors

### Component Yapısı
- Composition API kullanımı
- TypeScript type definitions
- Props validation
- Emit events
- Slot usage

### Yetkilendirme Sistemi Notları

**Admin Kullanıcılar:**
- JWT token'da `isAdmin: true` olan kullanıcılar
- Tüm sayfalara tüm yetkilerle (view + edit) erişir
- Permission kontrolü bypass edilir

**Normal Kullanıcılar:**
- JWT token'da `user_groups` array'inden grup bilgileri alınır
- Her sayfa için `viewGroups` ve `editGroups` kontrol edilir
- Kullanıcının en az bir grubu sayfa yetkilerinde varsa erişim verilir

**Sayfa Yetkilendirme Mantığı:**
1. Sayfa yüklemeden önce `permission` middleware kontrol eder
2. Admin ise → Erişim verilir
3. Normal kullanıcı ise → Grup bazlı kontrol yapılır
4. Yetki yoksa → 403 Forbidden sayfasına yönlendirilir

**Menü Filtreleme:**
- Menü component'i sadece kullanıcının erişebileceği sayfaları gösterir
- View yetkisi olmayan sayfalar menüde görünmez
- Admin kullanıcılar tüm menü öğelerini görür

**Dataset Yapısı:**
- `@pages` dataset'i MngDataGateway'de domain bazlı tutulur
- Her domain kendi sayfa tanımlarını yönetir
- Default sayfalar domain oluşturulurken eklenebilir

---

## 🎯 Öncelik Sırası

1. **Phase 2** - Domain Yönetimi (Temel CRUD işlemleri)
2. **Phase 5** - Yetkilendirme ve Sayfa Yönetimi Sistemi ⭐ (Öncelikli - Güvenlik için kritik)
3. **Phase 3** - Dataset Yönetimi (Schema yönetimi)
4. **Phase 4** - Data Management (Veri CRUD işlemleri)
5. **Phase 6** - User & Group Management
6. **Phase 7** - Component Library (Yeniden kullanılabilir component'ler)
7. **Phase 8** - State Management & API Integration
8. **Phase 9** - UI/UX İyileştirmeleri
9. **Phase 10** - Testing & Documentation

**Not:** Phase 5 (Yetkilendirme) Phase 2'den hemen sonra yapılmalı çünkü diğer sayfaların güvenliği için kritik.

---

## 🔄 Güncelleme Geçmişi

- **2025-01-XX** - RoadMap oluşturuldu
- Template analizi tamamlandı
- Phase planlaması yapıldı
- **2025-01-XX** - Phase 5: Yetkilendirme ve Sayfa Yönetimi Sistemi eklendi

---

**Son Güncelleme:** 2025-01-XX  
**Version:** 1.0.0  
**Status:** 📋 Planning Phase

