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

**Durum:** Kısmen Tamamlandı (User Management CRUD sayfaları oluşturuldu)

#### 7.1 User Management ✅ (Kısmen)
- ✅ **List Sayfası:** `/apps/users` - Kullanıcı listesi (v-data-table)
- ✅ **Create Sayfası:** `/apps/users/create` - Yeni kullanıcı oluşturma
- ✅ **Edit Sayfası:** `/apps/users/edit/[id]` - Kullanıcı düzenleme
- ✅ **Detail Sayfası:** `/apps/users/details/[id]` - Kullanıcı detayları
- ✅ **User Store:** `stores/apps/user.ts` - Pinia store (API entegrasyonu)
- ✅ **Temel Özellikler:**
  - CRUD işlemleri
  - Pagination, sorting, filtering
  - Group assignment
  - Status management (active/inactive)

#### 7.2 User Profile Enhancement 📋 (Planlanıyor) ⭐
- [ ] **Yeni Alanlar:**
  - Title (Unvan/İş Unvanı) - String, opsiyonel, max 100 karakter
  - Department (Departman) - String, opsiyonel, max 100 karakter
  - Gender (Cinsiyet) - Enum (NotSpecified, Male, Female)
  - PhoneNumber (Telefon Numarası) - String, opsiyonel, max 20 karakter
  - PhotoUrl (Profil Fotoğrafı) - String, MinIO URL, opsiyonel

- [ ] **Avatar Sistemi:**
  - PhotoUrl varsa → MinIO'dan fotoğraf göster
  - PhotoUrl yoksa → Gender'a göre renkli initials avatar:
    - Erkek (Male): `info` (açık mavi)
    - Kadın (Female): `pink` (pembe)
    - Belirtilmemiş (NotSpecified): `primary` (mavi)

- [ ] **Photo Upload:**
  - Photo upload component'i
  - MinIO entegrasyonu
  - Photo validation (max 5MB, jpg/jpeg/png/webp, max 2000x2000px)
  - Photo preview

- [ ] **UI Güncellemeleri:**
  - User form'larına yeni alanları ekleme (Title, Department, Gender, PhoneNumber, Photo)
  - User list/detail sayfalarında yeni alanları gösterme
  - Avatar component'lerini güncelleme (sidebar, header, user list)
  - Gender-based avatar renkleri
  - PhoneNumber formatı (mask veya validation - opsiyonel)

- [ ] **Backend Entegrasyonu:**
  - User interface'e yeni alanlar ekleme
  - API response mapping güncellemeleri
  - Photo upload endpoint entegrasyonu

#### 7.3 Server-Side Pagination & Performance Optimization ⚡ (Planlanıyor)

**Amaç:** Binlerce kullanıcı/grup için performans optimizasyonu

**Mevcut Durum:**
- ✅ Backend server-side pagination destekliyor
- ❌ Frontend client-side pagination kullanıyor (tüm veriler çekiliyor)

**Sorunlar:**
- Binlerce kullanıcı olduğunda ilk yüklemede 100 kullanıcı çekiliyor
- Tüm veriler frontend'de tutuluyor (memory sorunu)
- Search ve filter client-side yapılıyor
- Gereksiz network trafiği

**Çözüm:**
- [ ] `v-data-table`'ı server-side pagination moduna geçirme
- [ ] `server-items-length` prop'unu kullanma
- [ ] `@update:options` event'ini handle etme
- [ ] Search input'unu backend'e gönderme (debounce ile)
- [ ] Filter'ları backend'e gönderme (status, department, vb.)
- [ ] User Store'da `fetchUsers` metodunu optimize etme (default pageSize: 10)
- [ ] Group Store'da aynı optimizasyonları uygulama

**Performans İyileştirmeleri:**
- [ ] Debounced search (300ms)
- [ ] Loading states iyileştirme
- [ ] Cache stratejisi (opsiyonel)
- [ ] Virtual scrolling (çok büyük listeler için - opsiyonel)

**Öncelik:** Orta (küçük sistemlerde sorun yok, büyük sistemler için kritik)

#### 7.4 Group Management 📋 (Planlanıyor) ⭐

**Durum:** Planlama Aşaması  
**Yetki Gereksinimi:** Manager veya Admin (`isManager` veya `isAdmin`)

**Route:** `/apps/groups`  
**Component:** `pages/apps/groups/index.vue`

##### 7.4.1 Group List Sayfası

**Özellikler:**
- ✅ **Server-side Pagination** - Kullanıcı listesi gibi (önerilen)
- ✅ **Server-side Filtering** - Arama ve filtreleme backend'de
- ✅ **v-data-table** kullanımı (kullanıcı listesi ile aynı yapı)
- ✅ **Tabloda Gösterilecek Kolonlar:**
  - Grup Adı (name) - Sortable
  - Kişi Sayısı (memberCount) - Sortable
  - Oluşturulma Tarihi (createdAt) - Sortable
  - İşlemler (actions) - View, Edit, Delete butonları

**Table Headers:**
```typescript
const headers = [
  { title: 'Grup Adı', key: 'name', sortable: true },
  { title: 'Kişi Sayısı', key: 'memberCount', sortable: true },
  { title: 'Oluşturulma', key: 'createdAt', sortable: true },
  { title: 'İşlemler', key: 'actions', sortable: false, align: 'end' },
];
```

**Pagination & Filtering:**
- Server-side pagination (page, pageSize)
- Server-side search (searchTerm - grup adına göre)
- Server-side filtering (isActive - opsiyonel)
- Debounced search input (500ms)
- Items per page: 10, 25, 50, 100

##### 7.4.2 Group Store

**Store:** `stores/apps/group.ts`

**State:**
```typescript
interface GroupState {
  groups: Group[];
  currentGroup: Group | null;
  loading: boolean;
  error: string | null;
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}
```

**Group Interface:**
```typescript
export interface Group {
  id: string;
  groupId: string;
  name: string;
  description?: string;
  memberCount: number;
  isActive: boolean;
  createdAt: Date;
  updatedAt: Date | null;
  createdBy: string;
  updatedBy?: string | null;
}
```

**Actions:**
- `fetchGroups(params?: { page?, pageSize?, search?, isActive? })` - Server-side pagination ile grup listesi
- `fetchGroupById(groupId: string)` - Grup detayı
- `createGroup(group: CreateGroupRequest)` - Yeni grup oluştur
- `updateGroup(groupId: string, group: UpdateGroupRequest)` - Grup güncelle (sadece name)
- `deleteGroup(groupId: string)` - Grup sil (memberCount > 0 ise hata)
- `addUserToGroup(groupId: string, userId: string)` - Gruba kullanıcı ekle
- `removeUserFromGroup(groupId: string, userId: string)` - Gruptan kullanıcı çıkar

##### 7.4.3 Group CRUD Sayfaları

**Create Sayfası:**
- **Route:** `/apps/groups/create`
- **Component:** `pages/apps/groups/create/index.vue`
- **Form Alanları:**
  - Grup Adı (name) - Required, unique, max 100 karakter
  - Açıklama (description) - Opsiyonel, max 500 karakter
- **Validation:** VeeValidate + Yup

**Edit Sayfası:**
- **Route:** `/apps/groups/edit/[id].vue`
- **Component:** `pages/apps/groups/edit/[id].vue`
- **Düzenlenebilir Alanlar:**
  - ✅ **Sadece Grup Adı (name)** - Diğer alanlar read-only
- **Read-only Alanlar:**
  - Açıklama (description)
  - Kişi Sayısı (memberCount)
  - Oluşturulma Tarihi (createdAt)
  - Oluşturan (createdBy)
- **Not:** API'de sadece name güncellenebilir

**Detail Sayfası:**
- **Route:** `/apps/groups/details/[id].vue`
- **Component:** `pages/apps/groups/details/[id].vue`
- **Gösterilecek Bilgiler:**
  - Grup adı, açıklama
  - Kişi sayısı
  - Oluşturulma tarihi, güncellenme tarihi
  - Oluşturan, güncelleyen
  - Grup üyeleri listesi (opsiyonel - gelecekte eklenebilir)
- **Actions:**
  - Edit butonu (edit sayfasına yönlendirir)
  - Delete butonu (silme onayı ile)
  - "Kullanıcı Yönet" butonu (modal açar)

##### 7.4.4 Group Silme İşlemi

**Kısıtlamalar:**
- ❌ **İçinde kullanıcı olan gruplar silinemez** (`memberCount > 0`)
- ✅ Silme işlemi öncesi `memberCount` kontrolü yapılır
- ✅ Eğer `memberCount > 0` ise hata mesajı gösterilir:
  - "Bu grup içinde kullanıcılar bulunmaktadır. Önce tüm kullanıcıları gruptan çıkarmanız gerekmektedir."
- ✅ Sistem grupları (admins, managers, users, guests) silinemez (backend'de korumalı)

**Delete Confirmation Dialog:**
- Silme işlemi öncesi onay dialog'u
- Grup adı ve kişi sayısı gösterilir
- Eğer `memberCount > 0` ise dialog'da uyarı mesajı

##### 7.4.5 Group User Management Modal

**Component:** `components/apps/groups/GroupUserManagementModal.vue`

**Özellikler:**
- ✅ Modal component (v-dialog)
- ✅ Grup üyelerini listeleme
- ✅ Kullanıcı ekleme (multi-select veya search + add)
- ✅ Kullanıcı çıkarma (remove button)
- ✅ Kullanıcı arama (search input)
- ✅ Loading states
- ✅ Error handling

**Modal Props:**
```typescript
interface Props {
  groupId: string;
  groupName: string;
  isOpen: boolean;
}
```

**Modal Events:**
```typescript
interface Emits {
  (e: 'close'): void;
  (e: 'updated'): void; // Kullanıcı ekleme/çıkarma sonrası
}
```

**Kullanım:**
- Detail sayfasında "Kullanıcı Yönet" butonu modal'ı açar
- Edit sayfasında da kullanılabilir (opsiyonel)
- List sayfasında action butonu olarak da eklenebilir (opsiyonel)

**Modal İçeriği:**
- **Mevcut Üyeler Listesi:**
  - Kullanıcı adı, email, ad soyad
  - Remove butonu (her kullanıcı için)
  - Search input (mevcut üyeleri filtrele)
- **Kullanıcı Ekleme:**
  - Search input (tüm kullanıcıları ara)
  - Multi-select veya checkbox list
  - "Ekle" butonu
- **Loading States:**
  - Üye listesi yüklenirken
  - Kullanıcı ekleme/çıkarma sırasında

**API Endpoints:**
- `GET /api/user?page=1&pageSize=100` - Tüm kullanıcıları listele (ekleme için)
- `GET /api/group/{groupId}` - Grup detayı (üye listesi için - gelecekte)
- `POST /api/user/{userId}/groups/{groupId}` - Kullanıcıyı gruba ekle
- `DELETE /api/user/{userId}/groups/{groupId}` - Kullanıcıyı gruptan çıkar

**Not:** Şu an için grup detayında üye listesi yok. Kullanıcı listesinden grup filtresi ile üyeleri bulabiliriz veya backend'e üye listesi endpoint'i eklenebilir.

##### 7.4.6 Yetkilendirme

**Middleware:** `middleware/auth.global.js` (zaten mevcut)

**Sayfa Bazlı Kontrol:**
- Manager veya Admin yetkisi gerekli (`authStore.isManager`)
- Sayfa yüklemeden önce yetki kontrolü
- Yetkisiz erişimde uyarı mesajı veya yönlendirme

**Component İçinde Kontrol:**
```typescript
const authStore = useAuthStore();

if (!authStore.isManager) {
  // Uyarı mesajı göster veya yönlendir
}
```

##### 7.4.7 Sayfalama ve Filtreleme Stratejisi

**Önerilen Yaklaşım: Server-Side Pagination & Filtering** ⚡

**Neden Server-Side?**
- ✅ Kullanıcı listesi ile tutarlılık
- ✅ Büyük veri setleri için performans
- ✅ Backend zaten destekliyor (pagination, search, filter)
- ✅ Network trafiği optimizasyonu
- ✅ Memory kullanımı optimizasyonu

**Implementasyon:**
- `v-data-table` ile `server-items-length` prop'u
- `v-model:options` ile pagination state yönetimi
- `@update:options` event'i ile API çağrısı
- Debounced search input (500ms)
- Watch ile filter değişikliklerini dinleme

**Alternatif (Client-Side):**
- Küçük sistemler için (< 100 grup) client-side yeterli
- Ancak server-side daha ölçeklenebilir ve tutarlı

##### 7.4.8 API Endpoints (MngKeeper)

**Group Management:**
- ✅ `GET /api/group?page=1&pageSize=10&searchTerm=...&isActive=...` - Grup listesi
- ✅ `POST /api/group` - Grup oluşturma
- ✅ `GET /api/group/{groupId}` - Grup detayı
- ✅ `PUT /api/group/{groupId}` - Grup güncelleme (sadece name)
- ✅ `DELETE /api/group/{groupId}` - Grup silme

**User-Group Management:**
- ✅ `POST /api/user/{userId}/groups/{groupId}` - Kullanıcıyı gruba ekle
- ✅ `DELETE /api/user/{userId}/groups/{groupId}` - Kullanıcıyı gruptan çıkar

**Response Format:**
```typescript
// GetGroupsResponse
{
  groups: Group[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
  isSuccess: boolean;
  errorMessage?: string;
}
```

##### 7.4.9 Sidebar Menu Entegrasyonu

**Menu Item:**
- **Title:** "Grup Yönetimi"
- **Icon:** `UsersIcon` veya `GroupIcon` (vue-tabler-icons)
- **Route:** `/apps/groups`
- **Position:** "Apps" section altında, "Kullanıcı Yönetimi" altında veya yanında

**Menu Item Definition:**
```typescript
// components/lc/Full/vertical-sidebar/sidebarItem.ts
{
  title: "Grup Yönetimi",
  icon: UsersIcon, // veya GroupIcon
  to: "/apps/groups",
}
```

##### 7.4.10 Implementation Checklist

**Phase 1: Store & API Integration**
- [x] `stores/apps/group.ts` oluştur
- [x] Group interface tanımla
- [x] `fetchGroups` action (server-side pagination)
- [x] `fetchGroupById` action
- [x] `createGroup` action
- [x] `updateGroup` action (sadece name)
- [x] `deleteGroup` action (memberCount kontrolü) - ⚠️ NoContent response handling düzeltilecek
- [x] `addUserToGroup` action
- [x] `removeUserFromGroup` action

**Phase 2: List Page**
- [x] `pages/apps/groups/index.vue` oluştur
- [x] `v-data-table` entegrasyonu
- [x] Server-side pagination
- [x] Server-side search (debounced)
- [x] Table headers (name, memberCount, createdAt, actions)
- [x] View, Edit, Delete action butonları
- [x] Manager yetki kontrolü
- [x] Loading states
- [x] Error handling
- [x] Refresh butonu eklendi
- [x] Otomatik yenileme (route query ile)

**Phase 3: CRUD Pages**
- [x] `pages/apps/groups/create/index.vue` - Create form
- [x] `pages/apps/groups/edit/[id].vue` - Edit form (sadece name)
- [x] `pages/apps/groups/details/[id].vue` - Detail page
- [x] Form validation (VeeValidate + Yup)
- [x] Success/Error messages

**Phase 4: Delete Functionality**
- [x] Delete confirmation dialog
- [x] `memberCount > 0` kontrolü
- [x] Hata mesajı gösterimi
- [x] Sistem grupları koruması (backend'de)
- [ ] ⚠️ Delete işlemi çalışmıyor - NoContent (204) response handling düzeltilecek

**Phase 5: User Management Modal**
- [ ] `components/apps/groups/GroupUserManagementModal.vue` oluştur
- [ ] Mevcut üyeleri listeleme
- [ ] Kullanıcı arama (tüm kullanıcılar)
- [ ] Kullanıcı ekleme (multi-select veya checkbox)
- [ ] Kullanıcı çıkarma (remove button)
- [ ] Loading states
- [ ] Error handling
- [ ] Detail sayfasına entegrasyon

**Phase 6: Menu Integration**
- [x] Sidebar menu'ye "Grup Yönetimi" ekle
- [x] Icon seçimi (UserGroupIcon)
- [x] Route tanımı

**Phase 7: Testing & Polish**
- [x] Pagination test (çalışıyor)
- [x] Search test (çalışıyor)
- [x] CRUD işlemleri test (Create/Edit çalışıyor, Delete düzeltilecek)
- [x] Delete kısıtlamaları test (memberCount kontrolü çalışıyor)
- [ ] User management modal test
- [ ] ⚠️ Backend'de totalCount hesaplama sorunu kontrol edilecek (ilk sayfada yanlış değer)

**Bilinen Sorunlar:**
- ⚠️ **Group Delete:** NoContent (204) response için uygun handling yapılmalı
- ⚠️ **totalCount:** Backend'de ilk sayfa için yanlış değer dönüyor (11 yerine 12 olmalı)
- ✅ **refreshToken hatası:** Düzeltildi (refreshAccessToken olarak değiştirildi)
- [ ] Yetki kontrolü test
- [ ] Error handling test

**Öncelik:** Yüksek (User Management ile birlikte kritik)  
**Tahmini Süre:** 2-3 gün (User Management benzeri yapı olduğu için)

**Notlar:**
- Kullanıcı listesi ile aynı yapı ve pattern kullanılacak
- Server-side pagination önerilir (tutarlılık ve performans için)
- Manager yetkisi gerekli (Admin otomatik Manager)
- Grup silme işlemi için `memberCount` kontrolü kritik

#### 7.5 List Export Functionality 📊 (Planlanıyor) ⭐

**Durum:** Planlama Aşaması  
**Kapsam:** User List ve Group List için CSV/Excel/PDF export

##### 7.5.1 Mimari Yaklaşım

**Backend Export (Önerilen):**
- ✅ **Güvenlik:** Tüm veri backend'de kalır
- ✅ **Performans:** Büyük veri setleri için optimize edilmiş
- ✅ **Tutarlılık:** Aynı filtrelerle export
- ✅ **Bellek:** Frontend'de büyük veri tutulmaz

**Export Formatları:**
1. **CSV** (Öncelik 1 - En basit ve yaygın)
2. **Excel (XLSX)** (Öncelik 2 - Daha zengin formatlama)
3. **PDF** (Öncelik 3 - Yazdırma ve raporlama için - opsiyonel)

##### 7.5.2 Backend Implementation (MngKeeper)

**Yeni Endpoints:**
```
GET /api/group/export?format=csv&searchTerm=...&isActive=...
GET /api/group/export?format=xlsx&searchTerm=...&isActive=...
GET /api/user/export?format=csv&searchTerm=...&isActive=...
GET /api/user/export?format=xlsx&searchTerm=...&isActive=...
```

**Yeni Query Handlers:**
- `ExportGroupsQuery` / `ExportGroupsQueryHandler`
- `ExportUsersQuery` / `ExportUsersQueryHandler`

**Repository Methods:**
- `GetAllByDomainIdAsync(domainId, searchTerm, isActive)` - Pagination OLMADAN tüm veriyi çeker
- Mevcut `GetByDomainIdWithPaginationAsync`'den farklı olarak skip/limit olmaz

**Export Service:**
- CSV: `CsvHelper` NuGet paketi (MIT lisanslı, performanslı)
- Excel: `ClosedXML` NuGet paketi (MIT lisanslı, EPPlus alternatifi)
- PDF: `iTextSharp` veya `QuestPDF` (opsiyonel, daha sonra eklenebilir)

**Response:**
- `FileResult` (binary stream)
- Content-Type: `text/csv`, `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet`
- Content-Disposition: `attachment; filename="groups_2025-01-XX.csv"`

##### 7.5.3 Frontend Implementation (Nuxt 3)

**Export Button Component:**
- Dropdown menü (CSV, Excel seçenekleri)
- Loading state gösterimi
- Mevcut filtreleri export endpoint'ine aktarma
- Dosya indirme fonksiyonu

**Store Actions:**
- `exportGroups(format: 'csv' | 'xlsx', filters?: ExportFilters)`
- `exportUsers(format: 'csv' | 'xlsx', filters?: ExportFilters)`

**Utils:**
- `downloadFile(blob: Blob, filename: string)` - Blob API ile dosya indirme

**Export Filters:**
- Mevcut search/filter değerleri export endpoint'ine query parameter olarak gönderilir
- Export işlemi pagination olmadan tüm filtrelenmiş veriyi içerir

##### 7.5.4 Implementation Plan

**Faz 1: CSV Export (Öncelik 1)**
- [ ] Backend: `ExportGroupsQuery` + `CsvHelper` entegrasyonu
- [ ] Backend: `ExportUsersQuery` + `CsvHelper` entegrasyonu
- [ ] Backend: Repository'de `GetAllByDomainIdAsync` methodları
- [ ] Frontend: Export butonu (CSV)
- [ ] Frontend: Export fonksiyonu ve dosya indirme
- [ ] Test: Group list CSV export
- [ ] Test: User list CSV export

**Faz 2: Excel Export (Öncelik 2)**
- [ ] Backend: `ClosedXML` entegrasyonu
- [ ] Backend: Excel formatında export
- [ ] Frontend: Excel format seçeneği
- [ ] Test: Group list Excel export
- [ ] Test: User list Excel export

**Faz 3: PDF Export (Opsiyonel)**
- [ ] Backend: PDF library entegrasyonu
- [ ] Backend: PDF formatında export
- [ ] Frontend: PDF format seçeneği
- [ ] Test: PDF export

##### 7.5.5 Export Columns

**Group Export:**
- Grup Adı (name)
- Açıklama (description)
- Kişi Sayısı (memberCount)
- Durum (isActive - Aktif/Pasif)
- Oluşturulma Tarihi (createdAt)
- Güncellenme Tarihi (updatedAt)

**User Export:**
- Kullanıcı Adı (username)
- Email
- Ad (firstName)
- Soyad (lastName)
- Ünvan (title)
- Departman (department)
- Durum (isActive - Aktif/Pasif)
- Gruplar (groups - comma-separated)
- Oluşturulma Tarihi (createdAt)
- Güncellenme Tarihi (updatedAt)

##### 7.5.6 UI/UX

**Export Button Location:**
- Group List: Tablo üstünde, search bar yanında
- User List: Tablo üstünde, search bar yanında

**Export Button Design:**
- Icon: `DownloadIcon` (vue-tabler-icons)
- Dropdown menü: "CSV olarak indir", "Excel olarak indir"
- Loading state: Export sırasında spinner gösterimi
- Success feedback: Toast notification ("Export başarılı!")

**Error Handling:**
- Export başarısız olursa hata mesajı gösterimi
- Büyük veri setleri için timeout yönetimi
- Network hataları için retry mekanizması (opsiyonel)

##### 7.5.7 Gerekli Paketler

**Backend (MngKeeper - NuGet):**
- `CsvHelper` (CSV export için)
- `ClosedXML` (Excel export için)

**Frontend (Nuxt 3 - NPM):**
- CSV: Native Blob API (ek paket gerekmez)
- Excel: `xlsx` (SheetJS) - Opsiyonel, backend önerilir

##### 7.5.8 Öncelik

**Öncelik:** Orta-Yüksek
- CSV export: Yüksek öncelik (en basit ve en yaygın kullanım)
- Excel export: Orta öncelik (daha zengin formatlama gerekiyorsa)
- PDF export: Düşük öncelik (opsiyonel, yazdırma gereksinimleri varsa)

**Tahmini Süre:**
- CSV Export: 1-2 gün
- Excel Export: 1 gün (CSV'den sonra)
- PDF Export: 1-2 gün (opsiyonel)

**Notlar:**
- Export işlemleri server-side pagination kullanmaz
- Tüm filtrelenmiş veriyi export eder
- Büyük veri setleri için backend'de streaming export önerilir (gelecekte optimize edilebilir)
- Export dosya isimleri: `groups_YYYY-MM-DD_HHmmss.csv`, `users_YYYY-MM-DD_HHmmss.xlsx`

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

### Phase 11: Backend Infrastructure & Logging Improvements 🔧

**Durum:** Planlama Aşaması

#### 11.1 Seq Retention Policy Configuration 📊 (Planlanıyor)

**Amaç:** Seq'de log retention policy'lerini otomatik olarak yapılandırmak

**Gereksinimler:**
- Information loglar: 1 gün retention
- Warning loglar: 5 gün retention
- Error/Fatal loglar: 5 gün retention

**Mevcut Durum:**
- ✅ Console log formatı sadeleştirildi (detaylar Seq'de kalıyor)
- ✅ Seq retention policy configuration kodu eklendi (`SeqRetentionPolicy.cs`)
- ⚠️ Seq API endpoint'leri ile ilgili sorunlar var (404/401 hataları)
- ⚠️ Programatik yapılandırma şu an çalışmıyor

**Yapılacaklar:**
- [ ] Seq API endpoint'lerini doğrulama ve düzeltme
- [ ] Seq API authentication gereksinimlerini kontrol etme
- [ ] Retention policy'lerin programatik olarak ayarlanmasını sağlama
- [ ] Alternatif: Seq UI'dan manuel yapılandırma dokümantasyonu

**Notlar:**
- Seq API Key gerekli değil (development ortamında)
- Retention policy'ler Seq UI'dan manuel olarak da ayarlanabilir
- Bu özellik kritik değil, uygulama çalışmaya devam eder

**Öncelik:** Düşük (manuel yapılandırma mümkün)

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
3. **Phase 7.2** - User Profile Enhancement ⭐ (Title, Department, Gender, PhoneNumber, Photo) - Yüksek Öncelik
4. **Phase 7.3** - Server-Side Pagination & Performance Optimization ⚡ - Orta Öncelik (Büyük sistemler için kritik)
5. **Phase 7.5** - List Export Functionality 📊 (CSV/Excel/PDF) - Orta-Yüksek Öncelik
6. **Phase 3** - Dataset Yönetimi (Schema yönetimi)
7. **Phase 4** - Data Management (Veri CRUD işlemleri)
8. **Phase 7.4** - Group Management (Planlanıyor)
9. **Phase 7** - Component Library (Yeniden kullanılabilir component'ler)
10. **Phase 8** - State Management & API Integration
11. **Phase 9** - UI/UX İyileştirmeleri
12. **Phase 10** - Testing & Documentation
13. **Phase 11** - Backend Infrastructure & Logging Improvements 🔧 (Seq Retention Policy) - Düşük Öncelik

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

