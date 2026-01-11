# Side Menu Planlama Dokümantasyonu

## Dokümantasyon Bilgileri
- **Oluşturulma Tarihi**: 2025-01-27
- **Durum**: Planlama Aşaması
- **Hedef Servis**: Mng.Ui (Nuxt.js Vue.js Uygulaması)

## Mevcut Durum Analizi

### Mevcut Side Menu Yapısı

#### Komponentler
- **Ana Sidebar Component**: `components/lc/Full/vertical-sidebar/index.vue`
- **Menu Item Component**: `components/lc/Full/vertical-sidebar/NavItem/index.vue`
- **Collapsible Menu Component**: `components/lc/Full/vertical-sidebar/NavCollapse/index.vue`
- **Menu Group Component**: `components/lc/Full/vertical-sidebar/NavGroup/index.vue`
- **Menu Data**: `components/lc/Full/vertical-sidebar/sidebarItem.ts`

#### Mevcut Özellikler
- ✅ Vertical Sidebar (sol tarafta)
- ✅ Mini Sidebar (daraltılmış mod)
- ✅ Expand on hover
- ✅ Nested menu desteği (children)
- ✅ Menu header/grupları
- ✅ Chip/Badge desteği
- ✅ Icon desteği (vue-tabler-icons)
- ✅ External link desteği
- ✅ Disabled menu item desteği
- ✅ User profile bilgisi (avatar, isim)
- ✅ Logout butonu

#### Mevcut Menu Interface
```typescript
interface menu {
  header?: string;
  title?: string;
  icon?: any;
  to?: string;
  chip?: string;
  chipBgColor?: string;
  chipColor?: string;
  chipVariant?: string;
  chipIcon?: string;
  children?: menu[];
  disabled?: boolean;
  type?: string;
  subCaption?: string;
}
```

#### State Yönetimi
- **Store**: `stores/customizer.ts`
- **State Properties**:
  - `Sidebar_drawer`: Sidebar açık/kapalı durumu
  - `mini_sidebar`: Mini sidebar modu
  - `setHorizontalLayout`: Yatay layout desteği (henüz kullanılmıyor)
  - `setRTLLayout`: RTL layout desteği

---

## Planlama İstekleri

### İstek 1: Side Menu Veritabanına Taşıma ✅ ANALİZ TAMAMLANDI

**Amaç**: Hard coded menu elemanlarını veritabanına taşımak

**Dataset Adı**: `@side_menu`

**Durum**: Analiz tamamlandı, menü yapısı detaylı analiz edildi

---

### İstek 2: Yetkilendirme Sistemi ✅ PLANLAMA TAMAMLANDI

**Amaç**: Menü elemanları ve sayfa erişimi için yetkilendirme sistemi kurmak

**Kapsam**:
- Her menü elemanı için kullanıcı gruplarına göre yetkilendirme tanımları
- Yetki tipleri: view, create, update, delete, export
- Menü görünürlük kontrolü (view yetkisi olmayanlar menüde görünmez)
- Sayfa erişim kontrolü (view yetkisi olmayanlar URL'den erişemez)
- Admin bypass (isAdmin: true ise tüm kontroller bypass edilir)

**Durum**: Planlama tamamlandı, yetkilendirme mantığı detaylandırıldı

---

### İstek 3: DOM Element Yetkilendirmesi ✅ PLANLAMA TAMAMLANDI

**Amaç**: Sayfa içindeki DOM elemanları (butonlar, action sütunları) için yetkilendirme kontrolü

**Kapsam**:
- Data table'larda action sütunları (Düzenle, Sil, Görüntüle butonları)
- Sayfa üstü butonları (Yeni Kayıt Ekle, Export butonları)
- Route-to-menu-item mapping (sayfa → menü item ilişkilendirme)
- Vue composable ve directive yaklaşımları
- Permission wrapper component'leri

**Durum**: Planlama tamamlandı, DOM element yetkilendirme yaklaşımları detaylandırıldı

---

### İstek 4: Sayfa Tipi Bazlı Yetkilendirme ✅ PLANLAMA TAMAMLANDI

**Amaç**: Sayfa tiplerine göre (User, Manager, Admin) menü görünürlüğü ve sayfa erişimi kontrolü

**Sayfa Tipleri:**
1. **User** (default): Normal kullanıcılar için, permission-based erişim
2. **Manager**: `is_manager: true` olanlar için, UI'da read-only
3. **Admin**: `is_admin: true` olanlar için, UI'da read-only

**Kapsam**:
- Menu item'larında `pageType` field'ı (user, manager, admin)
- Menü görünürlük kontrolü (sayfa tipine göre)
- Sayfa erişim kontrolü (sayfa tipine göre)
- UI read-only/editable durumu (sayfa tipine göre)
- Admin kullanıcıları tüm sayfaları görebilir

**Durum**: Planlama tamamlandı, sayfa tipi bazlı yetkilendirme mantığı detaylandırıldı

---

### İstek 5: Icon Seçim Sistemi ✅ PLANLAMA TAMAMLANDI

**Amaç**: Kullanıcıların menü item'ları için icon seçebilmesi

**Mevcut Icon Kütüphaneleri:**
1. **Material Design Icons** (`@mdi/font` v7.4.47):
   - CSS olarak import edilmiş: `@mdi/font/css/materialdesignicons.css`
   - Vuetify ile entegre
   - Kullanım: `mdi-{icon-name}` (örn: `mdi-magnify`, `mdi-home`)
   - Örnek kullanım: `<v-icon>mdi-magnify</v-icon>`

2. **Vue Tabler Icons** (`vue-tabler-icons` v2.21.0):
   - Plugin olarak kullanılıyor: `nuxtApp.vueApp.use(VueTablerIcons)`
   - Component olarak kullanılıyor: `import { ChartPieIcon } from 'vue-tabler-icons'`
   - Mevcut sidebar'da kullanılıyor
   - Örnek kullanım: `<ChartPieIcon size="20" />`

**Kapsam**:
- Icon tipi belirleme (mdi veya tabler)
- Icon seçici component (icon picker)
- Icon listesi ve kategorilendirme
- Icon arama/filtreleme
- Side Menu Manager'da icon seçimi

**Durum**: Planlama tamamlandı, icon seçim sistemi detaylandırıldı

---

### İstek 6: Sıralama ve Nested Header Desteği ✅ PLANLAMA TAMAMLANDI

**Amaç**: Header'lar ve item'lar için sıralama yönetimi ve nested header desteği

**Kapsam**:
- Header'lar için sıralama (order field'ı)
- Item'lar için sıralama (aynı parent altında)
- Drag & drop ile sıralama
- Bir header altına başka bir header eklenebilme (nested header)
- Item'ların header bilgisi değiştirilebilme (parent değiştirme)
- Item'ların sırası değiştirilebilme

**Durum**: Planlama tamamlandı, sıralama ve nested header desteği detaylandırıldı

---

## Detaylı Menü Yapısı Analizi

### Mevcut Menü Elemanları Listesi

Toplam **51 menü item'ı** ve **7 header** bulunmaktadır. Aşağıda detaylı liste:

#### 1. Header: "Home"
- **Analytical** → `/dashboards/analytical` (icon: ChartPieIcon)
- **Classic** → `/dashboards/classic` (icon: CoffeeIcon)
- **Demographical** → `/dashboards/demographical` (icon: CpuIcon)
- **Minimal** → `/dashboards/minimal` (icon: FlagIcon)
- **eCommerce** → `/dashboards/ecommerce` (icon: ShoppingCartIcon)
- **Modern** → `/dashboards/modern` (icon: ApertureIcon)

#### 2. Header: "Assets"
- **AssetList** → `/assetdata/assets-page` (icon: BoxIcon, chip: "2")

#### 3. Header: "Apps"
- **Contact** → `/apps/contacts` (icon: BoxIcon, chip: "2")
- **Kullanıcı Yönetimi** → `/apps/users` (icon: UserCircleIcon)
- **Grup Yönetimi** → `/apps/groups` (icon: UserCircleIcon)
- **Event Mesajları** → `/apps/events` (icon: BellIcon)
- **Blog** → `/blog` (icon: ChartDonut3Icon)
  - *Children:*
    - Posts → `/apps/blog/posts`
    - Detail → `/apps/blog/early-black-friday-amazon-deals-cheap-tvs-headphones`
- **E-Commerce** → `/ecommerce/` (icon: BasketIcon)
  - *Children:*
    - Shop → `/apps/ecommerce/products`
    - Detail → `/apps/ecommerce/product/detail/1`
    - List → `/apps/ecommerce/productlist`
    - Checkout → `/apps/ecommerce/checkout`
    - Add Product → `/apps/ecommerce/addproduct`
    - Edit Product → `/apps/ecommerce/editproduct`
- **Chats** → `/apps/chats` (icon: Message2Icon)
- **User Profile** → `/user` (icon: UserCircleIcon)
  - *Children:*
    - Profile → `/apps/user/profile`
    - Followers → `/apps/user/profile/followers`
    - Friends → `/apps/user/profile/friends`
    - Gallery → `/apps/user/profile/gallery`
- **Invoice** → `/` (icon: FileCheckIcon)
  - *Children:*
    - List → `/apps/invoice`
    - Details → `/apps/invoice/details/102`
    - Create → `/apps/invoice/create`
    - Edit → `/apps/invoice/edit/102`
- **Notes** → `/apps/notes` (icon: FilesIcon)
- **Calendar** → `/apps/calendar` (icon: CalendarIcon)
- **Email** → `/apps/email` (icon: MailIcon)
- **Tickets** → `/apps/tickets` (icon: TicketIcon)
- **Kanban** → `/apps/kanban` (icon: LayoutKanbanIcon)

#### 4. Header: "Pages"
- **Pricing** → `/theme-pages/pricing` (icon: CurrencyDollarIcon)
- **Account Setting** → `/theme-pages/account-settings` (icon: UserCircleIcon)
- **FAQ** → `/theme-pages/faq` (icon: HelpIcon)
- **Gallery Lightbox** → `/theme-pages/gallery-lightbox` (icon: PhotoAiIcon)
- **Search Results** → `/theme-pages/search-results` (icon: SearchIcon)
- **Social Contacts** → `/theme-pages/social-media-contacts` (icon: SocialIcon)
- **Treeview** → `/theme-pages/treeview` (icon: BrandTidalIcon)

#### 5. Header: "Components"
- **Ui Components** → `#` (icon: BoxIcon)
  - *Children:*
    - Alert → `/ui-components/alert`
    - Accordion → `/ui-components/accordion`
    - Avatar → `/ui-components/avatar`
    - Chip → `/ui-components/chip`
    - Dialog → `/ui-components/dialogs`
    - List → `/ui-components/list`
    - Menus → `/ui-components/menus`
    - Rating → `/ui-components/rating`
    - Tabs → `/ui-components/tabs`
    - Tooltip → `/ui-components/tooltip`
    - Typography → `/ui-components/typography`

#### 6. Header: "Charts"
- **Line** → `/charts/line-chart` (icon: ChartLineIcon)
- **Gredient** → `/charts/gredient-chart` (icon: ChartArcsIcon)
- **Area** → `/charts/area-chart` (icon: ChartAreaIcon)
- **Candlestick** → `/charts/candlestick-chart` (icon: ChartCandleIcon)
- **Column** → `/charts/column-chart` (icon: ChartDotsIcon)
- **Doughnut & Pie** → `/charts/doughnut-pie-chart` (icon: ChartDonut3Icon)
- **Radialbar & Radar** → `/charts/radialbar-chart` (icon: ChartRadarIcon)

#### 7. Header: "Forms"
- **Form Elements** → `/components/` (icon: AppsIcon)
  - *Children:*
    - Autocomplete → `/forms/form-elements/autocomplete`
    - Combobox → `/forms/form-elements/combobox`
    - Button → `/forms/form-elements/button`
    - Checkbox → `/forms/form-elements/checkbox`
    - Custom Inputs → `/forms/form-elements/custominputs`
    - File Inputs → `/forms/form-elements/fileinputs`
    - Radio → `/forms/form-elements/radio`
    - Date Time → `/forms/form-elements/date-time`
    - Select → `/forms/form-elements/select`
    - Slider → `/forms/form-elements/slider`
    - Switch → `/forms/form-elements/switch`
    - Time Picker → `/forms/form-elements/timepicker`
    - Stepper → `/forms/form-elements/stepper`
- **Form Layout** → `/forms/form-layouts` (icon: FileTextIcon)
- **Form Horizontal** → `/forms/form-horizontal` (icon: BoxAlignBottomIcon)
- **Form Vertical** → `/forms/form-vertical` (icon: BoxAlignLeftIcon)
- **Form Custom** → `/forms/form-custom` (icon: FileDotsIcon)
- **Form Validation** → `/forms/form-validation` (icon: FilesIcon)
- **Editor** → `/forms/editor` (icon: EditCircleIcon)

#### 8. Header: "Widget"
- **Cards** → `/widgets/cards` (icon: CardboardsIcon)
- **Banners** → `/widgets/banners` (icon: PhotoIcon)
- **Charts** → `/widgets/charts` (icon: ChartBarIcon)

#### 9. Header: "Tables"
- **Basic Table** → `/tables/basic` (icon: BorderAllIcon)
- **Dark Table** → `/tables/dark` (icon: BorderHorizontalIcon)
- **Density Table** → `/tables/density` (icon: BorderInnerIcon)
- **Fixed Header Table** → `/tables/fixed-header` (icon: BorderTopIcon)
- **Height Table** → `/tables/height` (icon: BorderVerticalIcon)
- **Editable Table** → `/tables/editable` (icon: BorderStyle2Icon)

#### 10. Header: "Data Tables"
- **Basic Table** → `/datatables/basic` (icon: ColumnsIcon)
- **Header Table** → `/datatables/headers` (icon: RowInsertBottomIcon)
- **Selection Table** → `/datatables/Selectable` (icon: EyeTableIcon)
- **Sorting Table** → `/datatables/sorting` (icon: SortAscendingIcon)
- **Pagination Table** → `/datatables/pagination` (icon: PageBreakIcon)
- **Filtering Table** → `/datatables/filtering` (icon: FilterIcon)
- **Grouping Table** → `/datatables/grouping` (icon: BoxModelIcon)
- **Table Slots** → `/datatables/slots` (icon: ServerIcon)
- **CRUD Table** → `/datatables/crudtable` (icon: JumpRopeIcon)

#### 11. Header: "Authentication"
- **Login** → `#` (icon: LoginIcon)
  - *Children:*
    - Side Login → `/auth/login`
    - Boxed Login → `/auth/login2`
- **Register** → `#` (icon: UserPlusIcon)
  - *Children:*
    - Side Register → `/auth/register`
    - Boxed Register → `/auth/register2`
- **Forgot Password** → `#` (icon: RotateIcon)
  - *Children:*
    - Side Forgot Password → `/auth/forgot-password`
    - Boxed Forgot Password → `/auth/forgot-password2`
- **Two Steps** → `#` (icon: ZoomCodeIcon)
  - *Children:*
    - Side Two Steps → `/auth/two-step`
    - Boxed Two Steps → `/auth/two-step2`
- **Error** → `/auth/404` (icon: AlertCircleIcon)
- **Maintenance** → `/auth/maintenance` (icon: SettingsIcon)

#### 12. Header: "Icons"
- **Material** → `/icons/material` (icon: BrandCodesandboxIcon)
- **Tabler** → `/icons/tabler` (icon: BrandTablerIcon)

#### 13. Özel Item: "Front Pages"
- **Front Pages** → `/` (icon: AppWindowIcon)
  - *Children:*
    - Homepage → `/front-pages/homepage`
    - About Us → `/front-pages/about-us`
    - Blog → `/front-pages/blog/posts`
    - Blog Details → `/front-pages/blog/early-black-friday-amazon-deals-cheap-tvs-headphones`
    - Contact Us → `/front-pages/contact-us`
    - Portfolio → `/front-pages/portfolio`
    - Pricing → `/front-pages/pricing`

### Menü İstatistikleri

- **Toplam Header Sayısı**: 12 (7 adet header, 5 adet header'sız grup)
- **Toplam Menu Item Sayısı**: 51 (tek seviye)
- **Toplam Alt Menu Item Sayısı**: 48 (nested children)
- **En Fazla Alt Menüye Sahip Item**: 
  - Form Elements (14 children)
  - Ui Components (11 children)
- **Chip/Badge Kullanan Item Sayısı**: 2 (AssetList, Contact)
- **External Link Kullanan Item**: 0 (hepsi internal route)
- **Disabled Item**: 0

### Icon Kullanım İstatistikleri

**En Çok Kullanılan İkonlar:**
- `CircleDotIcon`: 48 kez (alt menülerde)
- `UserCircleIcon`: 5 kez
- `BoxIcon`: 3 kez
- `ChartDonut3Icon`: 2 kez
- Diğer ikonlar: 1'er kez

**Toplam Unique Icon Sayısı**: ~40 farklı icon

### Dataset Field Yapısı Önerisi

`@side_menu` dataset'i için önerilen field yapısı:

```typescript
// Dataset: @side_menu
// Collection: @side_menu (MongoDB'de)

interface SideMenuItem {
  // Temel Bilgiler
  order: number;              // Sıralama (mandatory)
  itemType: 'header' | 'item'; // Item tipi (mandatory)
  
  // Header için (itemType === 'header')
  header?: string;            // Header metni (optional, header ise mandatory)
  
  // Item için (itemType === 'item')
  title?: string;             // Menü başlığı (optional, item ise mandatory)
  icon?: string;              // Icon adı (optional)
  iconType?: 'mdi' | 'tabler'; // Icon tipi (default: 'tabler')
  to?: string;                // Route path (optional)
  type?: 'internal' | 'external'; // Link tipi (default: 'internal')
  pageType?: 'user' | 'manager' | 'admin'; // Sayfa tipi (default: 'user')
  
  // Hiyerarşi
  parentId?: string;          // Parent item __dataId (optional, root ise null)
  level: number;              // Derinlik seviyesi (0: root, 1: child, 2: grandchild, vb.)
  
  // Chip/Badge
  chip?: string;              // Chip metni (optional)
  chipBgColor?: string;       // Chip arka plan rengi (optional)
  chipColor?: string;         // Chip metin rengi (optional)
  chipVariant?: string;       // Chip variant (optional)
  chipIcon?: string;          // Chip icon (optional)
  
  // Diğer Özellikler
  disabled?: boolean;         // Devre dışı mı (default: false)
  subCaption?: string;        // Alt başlık (optional)
  
  // Yetkilendirme (Permissions)
  permissions?: {
    // Her grup için ayrı yetkiler tanımlanabilir
    groups?: {
      [groupName: string]: {
        view?: boolean;      // Menüde görünürlük ve sayfa erişimi
        create?: boolean;    // Oluşturma yetkisi
        update?: boolean;    // Güncelleme yetkisi
        delete?: boolean;    // Silme yetkisi
        export?: boolean;    // Export yetkisi
      }
    };
  };
  
  // Metadata (BaseEntity'den gelir)
  // __dataId: string;
  // __createdAt: DateTime;
  // __updatedAt: DateTime;
  // __createdBy?: string;
  // __updatedBy?: string;
  // __deletedAt?: DateTime;
  // __version: number;
}
```

### Dataset Kategori: System Datasets

**Kategori Adı**: `System Datasets`  
**Kategori Açıklaması**: Sistem tarafından kullanılan dataset'ler (side menu, config, vb.)  
**Kategori Koleksiyonu**: `@dataset_categories`  
**Kategori Referansı**: Dataset oluştururken `category` field'ına `__dataId` değeri atanacak

**Not**: `@side_menu` dataset'i bu kategori içinde yer alacaktır.

---

### Dataset Schema Önerisi (MngDataGateway Format)

```json
{
  "name": "@side_menu",
  "description": "Side menu items dataset - stores all menu items and their hierarchy",
  "category": "{SystemDatasetsCategory__dataId}",
  "forceSchema": true,
  "logging": "none",
  "publish_mode": "none",
  "fields": [
    {
      "fieldType": "number",
      "name": "order",
      "title": "Sıralama",
      "mandatory": true,
      "validation": {
        "min": 0
      }
    },
    {
      "fieldType": "text",
      "name": "itemType",
      "title": "Item Tipi",
      "mandatory": true,
      "validation": {
        "pattern": "^(header|item)$"
      }
    },
    {
      "fieldType": "text",
      "name": "header",
      "title": "Header Metni",
      "mandatory": false
    },
    {
      "fieldType": "text",
      "name": "title",
      "title": "Menü Başlığı",
      "mandatory": false
    },
    {
      "fieldType": "text",
      "name": "icon",
      "title": "Icon Adı",
      "mandatory": false
    },
    {
      "fieldType": "text",
      "name": "iconType",
      "title": "Icon Tipi",
      "mandatory": false,
      "defaultValue": "tabler",
      "validation": {
        "pattern": "^(mdi|tabler)$"
      }
    },
    {
      "fieldType": "text",
      "name": "to",
      "title": "Route Path",
      "mandatory": false
    },
    {
      "fieldType": "text",
      "name": "type",
      "title": "Link Tipi",
      "mandatory": false,
      "defaultValue": "internal",
      "validation": {
        "pattern": "^(internal|external)$"
      }
    },
    {
      "fieldType": "text",
      "name": "pageType",
      "title": "Sayfa Tipi",
      "mandatory": false,
      "defaultValue": "user",
      "validation": {
        "pattern": "^(user|manager|admin)$"
      }
    },
    {
      "fieldType": "relation",
      "name": "parentId",
      "title": "Parent Item",
      "mandatory": false,
      "relationDataset": "@side_menu",
      "isArray": false
    },
    {
      "fieldType": "number",
      "name": "level",
      "title": "Seviye",
      "mandatory": true,
      "defaultValue": 0,
      "validation": {
        "min": 0,
        "max": 10
      }
    },
    {
      "fieldType": "text",
      "name": "chip",
      "title": "Chip Metni",
      "mandatory": false
    },
    {
      "fieldType": "text",
      "name": "chipBgColor",
      "title": "Chip Arka Plan Rengi",
      "mandatory": false
    },
    {
      "fieldType": "text",
      "name": "chipColor",
      "title": "Chip Metin Rengi",
      "mandatory": false
    },
    {
      "fieldType": "text",
      "name": "chipVariant",
      "title": "Chip Variant",
      "mandatory": false
    },
    {
      "fieldType": "text",
      "name": "chipIcon",
      "title": "Chip Icon",
      "mandatory": false
    },
    {
      "fieldType": "bool",
      "name": "disabled",
      "title": "Devre Dışı",
      "mandatory": false,
      "defaultValue": false
    },
    {
      "fieldType": "text",
      "name": "subCaption",
      "title": "Alt Başlık",
      "mandatory": false
    },
    {
      "fieldType": "object",
      "name": "permissions",
      "title": "Yetkilendirme",
      "mandatory": false,
      "isArray": false
    }
  ],
  "indexList": [
    {
      "name": "idx_order",
      "fields": {
        "order": 1
      },
      "unique": false
    },
    {
      "name": "idx_parentId",
      "fields": {
        "parentId": 1,
        "order": 1
      },
      "unique": false
    },
    {
      "name": "idx_level",
      "fields": {
        "level": 1,
        "order": 1
      },
      "unique": false
    },
    {
      "name": "idx_itemType_level",
      "fields": {
        "itemType": 1,
        "level": 1,
        "order": 1
      },
      "unique": false
    }
  ]
}
```

### Migration Stratejisi

> **Not**: Script yazmaya henüz erken, daha konuşulacak konular var. Bu bölüm planlama aşamasındadır.

#### Adım 1: Dataset Kategori Oluşturma
1. **System Datasets Kategorisi Oluşturma**
   - API: `POST /api/v1/dataset-categories`
   - Request Body:
     ```json
     {
       "categoryName": "System Datasets",
       "categoryDescription": "Sistem tarafından kullanılan dataset'ler (side menu, config, vb.)"
     }
     ```
   - Response'dan `__dataId` değerini al (category referansı için)

#### Adım 2: Dataset Oluşturma (Planlanan)
- [ ] MngDataGateway API üzerinden `@side_menu` dataset'ini oluştur
- [ ] System Datasets kategorisine bağla (category field'ına __dataId ekle)
- [ ] Field tanımlarını yap
- [ ] Index'leri oluştur

#### Adım 3: Veri Migration (Planlanan)
- [ ] Mevcut menü verilerini export etme stratejisi belirle
- [ ] Parent-child ilişkilerini `parentId` ile kurma mantığı
- [ ] `order` ve `level` hesaplama algoritması
- [ ] Veri yükleme ve doğrulama

#### Adım 4: Frontend Entegrasyonu (Planlanan)
- [ ] API endpoint tasarımı: `GET /api/data/@side_menu?filter={...}`
- [ ] Frontend'de menu items'ı API'den çekme stratejisi
- [ ] Cache mekanizması tasarımı (performans için)
- [ ] Mevcut `sidebarItem.ts` dosyası ile backward compatibility
- [ ] Fallback mekanizması (API hata durumunda)

### Teknik Notlar

- **Icon Mapping**: Icon adları string olarak saklanacak (örn: "ChartPieIcon"), frontend'de dinamik import yapılacak
- **Hiyerarşi**: Parent-child ilişkisi `parentId` ve `level` field'ları ile yönetilecek
- **Sıralama**: `order` field'ı ile menü sıralaması kontrol edilecek
- **Performance**: Menu items cache'lenecek (TTL: 5 dakika önerilir)
- **Fallback**: API'den veri çekilemezse fallback olarak hard-coded menu kullanılabilir

---

## Yetkilendirme Sistemi

### Genel Bakış

Yetkilendirme sistemi hem **menü görünürlüğü** hem de **sayfa erişimi** için çalışacak şekilde tasarlanmıştır. Her menü elemanı için kullanıcı gruplarına göre yetkilendirme tanımları yapılabilir.

### Yetki Tipleri

1. **view** - Menüde görünürlük ve sayfa erişimi
   - View yetkisi olmayanlar menüde görünmez
   - View yetkisi olmayanlar URL'den erişmeye çalıştığında yetkisiz erişim sayfasına yönlendirilir
2. **create** - Oluşturma yetkisi
3. **update** - Güncelleme yetkisi
4. **delete** - Silme yetkisi
5. **export** - Export yetkisi

### Yetkilendirme Yapısı

Her menü item'ı için `permissions` field'ı içinde grup bazlı yetkiler tanımlanır:

```json
{
  "permissions": {
    "groups": {
      "admins": {
        "view": true,
        "create": true,
        "update": true,
        "delete": true,
        "export": true
      },
      "managers": {
        "view": true,
        "create": true,
        "update": true,
        "delete": false,
        "export": true
      },
      "users": {
        "view": true,
        "create": false,
        "update": false,
        "delete": false,
        "export": false
      }
    }
  }
}
```

### Token Yapısı

**UserInfo Interface** (Token içinde):
```typescript
interface UserInfo {
  sub: string;
  username: string;
  email?: string;
  given_name?: string;
  family_name?: string;
  name?: string;
  preferred_username?: string;
  user_groups?: string[];      // Kullanıcının dahil olduğu gruplar
  isAdmin?: boolean;            // Admin yetkisi (tüm kontrolleri bypass eder)
  is_manager?: boolean;
  domain_id?: string;
  domain_name?: string;
  [key: string]: any;
}
```

**Auth Store Getters**:
- `authStore.isAdmin`: `userInfo?.isAdmin === true` kontrolü
- `authStore.userGroups`: `userInfo?.user_groups || []` array'i

### Yetkilendirme Mantığı

#### 1. Admin Kontrolü (Öncelikli)

```typescript
// Admin kullanıcılar tüm yetkilendirme kontrollerini bypass eder
if (authStore.isAdmin) {
  // Tüm menü item'ları görünür ve erişilebilir (sayfa tipi fark etmeksizin)
  // Tüm yetkiler (view, create, update, delete, export) otomatik olarak true
  // UI'da editable modda (read-only değil)
  return true;
}
```

#### 2. Sayfa Tipi Kontrolü

```typescript
function canAccessPageType(pageType: 'user' | 'manager' | 'admin'): boolean {
  const authStore = useAuthStore();
  
  // Admin tüm sayfa tiplerine erişebilir
  if (authStore.isAdmin) return true;
  
  // Sayfa tipi kontrolü
  switch (pageType) {
    case 'admin':
      // Sadece admin kullanıcılar erişebilir
      return authStore.isAdmin === true;
    
    case 'manager':
      // Manager veya admin kullanıcılar erişebilir
      return authStore.isManager === true || authStore.isAdmin === true;
    
    case 'user':
    default:
      // Tüm kullanıcılar erişebilir (permission kontrolü sonra yapılır)
      return true;
  }
}
```

#### 3. Menü Görünürlük Kontrolü

```typescript
function canViewMenuItem(menuItem: SideMenuItem, userGroups: string[]): boolean {
  const authStore = useAuthStore();
  
  // Admin kontrolü - Admin tüm item'ları görebilir
  if (authStore.isAdmin) return true;
  
  // Sayfa tipi kontrolü
  const pageType = menuItem.pageType || 'user';
  if (!canAccessPageType(pageType)) {
    return false; // Sayfa tipine erişim yoksa menüde gösterilmez
  }
  
  // Permissions yoksa herkes görebilir (backward compatibility)
  if (!menuItem.permissions || !menuItem.permissions.groups) {
    return true;
  }
  
  // Kullanıcının dahil olduğu gruplardan herhangi birinde view yetkisi var mı?
  return userGroups.some(groupName => {
    const groupPerms = menuItem.permissions.groups[groupName];
    return groupPerms?.view === true;
  });
}
```

#### 3. Sayfa Erişim Kontrolü

**Middleware veya Route Guard** içinde:

```typescript
// pages/[route].vue veya middleware/auth.ts
export default defineNuxtRouteMiddleware((to, from) => {
  const authStore = useAuthStore();
  
  // Admin kontrolü - Admin tüm sayfalara erişebilir
  if (authStore.isAdmin) {
    return; // Tüm sayfalara erişebilir (sayfa tipi fark etmeksizin)
  }
  
  // Sayfa için menü item'ını bul
  const menuItem = findMenuItemByRoute(to.path);
  
  if (!menuItem) {
    // Menü item bulunamazsa erişime izin ver (geçici çözüm)
    // Not: Bu durumda sayfa tipi kontrolü yapılamaz, dikkatli olunmalı
    return;
  }
  
  // Sayfa tipi kontrolü
  const pageType = menuItem.pageType || 'user';
  if (!canAccessPageType(pageType)) {
    // Sayfa tipine erişim yoksa yetkisiz erişim sayfasına yönlendir
    return navigateTo('/unauthorized');
  }
  
  // View yetkisi kontrolü (permission-based)
  if (!canViewMenuItem(menuItem, authStore.userGroups)) {
    // Yetkisiz erişim sayfasına yönlendir
    return navigateTo('/unauthorized');
  }
});
```

#### 4. Sayfa İçi Yetki Kontrolleri ve Read-Only Durumu

```typescript
// Composables/usePermissions.ts
export function usePermissions() {
  const authStore = useAuthStore();
  const route = useRoute();
  const menuStore = useSideMenuStore();
  
  // Mevcut sayfanın menü item'ını bul
  const currentMenuItem = computed(() => {
    const currentPath = route.path;
    return menuStore.allMenuItems.find(item => item.to === currentPath) || null;
  });
  
  // Sayfa tipine göre read-only durumu
  const isPageReadOnly = computed(() => {
    if (!currentMenuItem.value) return false;
    
    // Admin kullanıcılar sayfaları editable modda görür (read-only değil)
    if (authStore.isAdmin) return false;
    
    const pageType = currentMenuItem.value.pageType || 'user';
    
    // Manager ve Admin sayfa tipleri read-only
    if (pageType === 'manager' || pageType === 'admin') {
      return true;
    }
    
    // User sayfa tipleri permission-based (editable olabilir)
    return false;
  });
  
  function hasPermission(menuItem: SideMenuItem, permission: 'view' | 'create' | 'update' | 'delete' | 'export'): boolean {
    // Admin kontrolü - Admin tüm yetkilere sahip
    if (authStore.isAdmin) return true;
    
    // Sayfa read-only ise, create/update/delete yetkileri false
    if (isPageReadOnly.value && (permission === 'create' || permission === 'update' || permission === 'delete')) {
      return false;
    }
    
    // Permissions yoksa false döndür (güvenlik için)
    if (!menuItem.permissions || !menuItem.permissions.groups) {
      return false;
    }
    
    // Kullanıcının gruplarından herhangi birinde yetki var mı?
    return authStore.userGroups.some(groupName => {
      const groupPerms = menuItem.permissions.groups[groupName];
      return groupPerms?.[permission] === true;
    });
  }
  
  return {
    currentMenuItem,
    isPageReadOnly,
    hasPermission,
    canCreate: (item) => hasPermission(item, 'create'),
    canUpdate: (item) => hasPermission(item, 'update'),
    canDelete: (item) => hasPermission(item, 'delete'),
    canExport: (item) => hasPermission(item, 'export'),
  };
}
```

### Kullanım Örnekleri

#### Örnek 1: Menü Filtreleme

```vue
<script setup>
import { computed } from 'vue';
import { useAuthStore } from '@/stores/auth';
import { useSideMenuStore } from '@/stores/sideMenu';

const authStore = useAuthStore();
const menuStore = useSideMenuStore();

const visibleMenuItems = computed(() => {
  if (authStore.isAdmin) {
    return menuStore.allMenuItems; // Admin tüm menüleri görür
  }
  
  return menuStore.allMenuItems.filter(item => {
    if (!item.permissions?.groups) return true; // Permission yoksa göster
    
    return authStore.userGroups.some(group => {
      return item.permissions.groups[group]?.view === true;
    });
  });
});
</script>

<template>
  <template v-for="item in visibleMenuItems" :key="item.__dataId">
    <!-- Menu item render -->
  </template>
</template>
```

#### Örnek 2: Sayfa İçi Buton Kontrolü

```vue
<script setup>
import { usePermissions } from '@/composables/usePermissions';
import { useRoute } from 'vue-router';

const { hasPermission, canCreate, canUpdate, canDelete } = usePermissions();
const route = useRoute();
const menuItem = findMenuItemByRoute(route.path);
</script>

<template>
  <v-btn 
    v-if="canCreate(menuItem)" 
    color="primary" 
    @click="handleCreate"
  >
    Yeni Ekle
  </v-btn>
  
  <v-btn 
    v-if="canUpdate(menuItem)" 
    color="warning" 
    @click="handleUpdate"
  >
    Düzenle
  </v-btn>
  
  <v-btn 
    v-if="canDelete(menuItem)" 
    color="error" 
    @click="handleDelete"
  >
    Sil
  </v-btn>
</template>
```

### Permissions Field Yapısı (MongoDB'de)

Permissions field'ı `object` tipinde MongoDB'de saklanacak. MongoDB'de BSON object olarak saklanır ve JSON formatında serialize edilir.

**Yapı:**
```json
{
  "permissions": {
    "groups": {
      "admins": {
        "view": true,
        "create": true,
        "update": true,
        "delete": true,
        "export": true
      },
      "managers": {
        "view": true,
        "create": true,
        "update": true,
        "delete": false,
        "export": true
      },
      "users": {
        "view": true,
        "create": false,
        "update": false,
        "delete": false,
        "export": false
      }
    }
  }
}
```

**Örnek Menü Item - User Sayfası (MongoDB Document):**

```json
{
  "_id": ObjectId("..."),
  "__dataId": "menu-item-123",
  "__createInfo": {
    "createdAt": ISODate("2025-01-27T10:00:00Z"),
    "createdBy": "admin"
  },
  "__version": 1,
  "order": 10,
  "itemType": "item",
  "title": "Kullanıcı Yönetimi",
  "icon": "UserCircleIcon",
  "to": "/apps/users",
  "type": "internal",
  "pageType": "user",
  "parentId": null,
  "level": 0,
  "disabled": false,
  "permissions": {
    "groups": {
      "admins": {
        "view": true,
        "create": true,
        "update": true,
        "delete": true,
        "export": true
      },
      "managers": {
        "view": true,
        "create": true,
        "update": true,
        "delete": false,
        "export": true
      },
      "users": {
        "view": false,
        "create": false,
        "update": false,
        "delete": false,
        "export": false
      }
    }
  }
}
```

**Örnek Menü Item - Manager Sayfası (Read-Only):**

```json
{
  "__dataId": "menu-item-124",
  "order": 15,
  "itemType": "item",
  "title": "Kullanıcı Grupları",
  "icon": "UsersIcon",
  "to": "/apps/groups",
  "type": "internal",
  "pageType": "manager",
  "parentId": null,
  "level": 0,
  "disabled": false,
  "permissions": {
    "groups": {
      "admins": {
        "view": true,
        "create": true,
        "update": true,
        "delete": true,
        "export": true
      },
      "managers": {
        "view": true,
        "create": false,
        "update": false,
        "delete": false,
        "export": true
      }
    }
  }
}
```

**Örnek Menü Item - Admin Sayfası (Read-Only):**

```json
{
  "__dataId": "menu-item-125",
  "order": 20,
  "itemType": "item",
  "title": "Dataset Yönetimi",
  "icon": "DatabaseIcon",
  "to": "/apps/datasets",
  "type": "internal",
  "pageType": "admin",
  "parentId": null,
  "level": 0,
  "disabled": false,
  "permissions": {
    "groups": {
      "admins": {
        "view": true,
        "create": true,
        "update": true,
        "delete": true,
        "export": true
      }
    }
  }
}
```

**Örnek Header Item (Permissions Yok):**

```json
{
  "__dataId": "header-1",
  "order": 0,
  "itemType": "header",
  "header": "Apps",
  "level": 0,
  "parentId": null
}
```

**Örnek Nested Menu Item (Alt Menü):**

```json
{
  "__dataId": "menu-item-124",
  "order": 1,
  "itemType": "item",
  "title": "Posts",
  "icon": "CircleDotIcon",
  "to": "/apps/blog/posts",
  "type": "internal",
  "parentId": "menu-item-125",
  "level": 1,
  "disabled": false,
  "permissions": {
    "groups": {
      "admins": {
        "view": true,
        "create": true,
        "update": true,
        "delete": true,
        "export": true
      },
      "editors": {
        "view": true,
        "create": true,
        "update": true,
        "delete": false,
        "export": false
      }
    }
  }
}
```

**Not**: Permissions field'ı optional'dır. Eğer bir menü item'ında permissions yoksa:
- **Menü görünürlüğü**: Tüm kullanıcılar görebilir (backward compatibility)
- **Sayfa erişimi**: Tüm kullanıcılar erişebilir (backward compatibility)
- **Sayfa içi yetkiler**: Güvenlik için `false` döndürülür (güvenli varsayılan)
- **Header item'lar**: Permissions field'ı genellikle kullanılmaz (görünürlük kontrolü yapılmaz)

### Yetkisiz Erişim Sayfası

URL üzerinden yetkisiz erişim durumunda kullanıcı `/unauthorized` sayfasına yönlendirilir.

**Sayfa**: `pages/unauthorized.vue`  
**Route**: `/unauthorized`  
**İçerik**: 
- "Yetkisiz Erişim" mesajı
- Geri dönüş butonu (ana sayfaya veya önceki sayfaya)

### Güvenlik Notları

1. **Backend Kontrolü**: Frontend kontrolü yanında backend'de de yetki kontrolü yapılmalıdır (API endpoint'lerinde)
2. **Token Validation**: Her istekte token geçerliliği kontrol edilmelidir
3. **Group Name Validation**: Group name'ler case-sensitive olmalı (backend'de de aynı şekilde kontrol edilmeli)
4. **Default Permissions**: Permission tanımı olmayan item'lar için güvenli varsayılanlar belirlenmelidir
5. **Cache Invalidation**: Permission değişikliklerinde cache invalidation yapılmalıdır

---

## Backend API Kullanımı (MngDataGateway)

### Mevcut Endpoint'ler Yeterli mi? ✅ EVET

MngDataGateway'in mevcut dataset ve data endpoint'leri side menu yönetimi için **tamamen yeterlidir**. Backend'de herhangi bir değişiklik yapılmasına gerek yoktur.

### Dataset Schema Management Endpoints

**Kullanım Senaryoları:**

1. **Dataset Oluşturma** (`@side_menu`):
   ```
   POST /api/v1/datasets
   ```
   - `@side_menu` dataset'ini oluşturmak için
   - Field definitions, index definitions, category ile birlikte

2. **Dataset Schema Okuma**:
   ```
   GET /api/v1/datasets/@side_menu
   ```
   - Dataset schema'yı okumak için (field definitions, indexes, vb.)

3. **Dataset Schema Güncelleme**:
   ```
   PUT /api/v1/datasets/@side_menu
   ```
   - Schema'yı güncellemek için (gerekirse)

4. **Dataset Listesi**:
   ```
   GET /api/v1/datasets?category={categoryId}
   ```
   - System Datasets kategorisindeki tüm dataset'leri listelemek için

### Dataset Categories Endpoints

**Kullanım Senaryoları:**

1. **Kategori Oluşturma** (System Datasets):
   ```
   POST /api/v1/dataset-categories
   ```
   - System Datasets kategorisini oluşturmak için

2. **Kategori Listesi**:
   ```
   GET /api/v1/dataset-categories
   ```
   - Tüm kategorileri listelemek için

### Data CRUD Operations Endpoints

**Kullanım Senaryoları:**

1. **Menu Items Listesi Çekme**:
   ```
   GET /api/v1/data/@side_menu
   ```
   - Query Parameters:
     - `page`, `pageSize`: Pagination
     - `sort`: Sorting (örn: `order:asc,level:asc`)
     - `filter`: MongoDB filter (örn: `{"itemType":"item","level":0}`)
     - `fields`: Field selection (performans için)
   
   **Örnek Request:**
   ```
   GET /api/v1/data/@side_menu?page=1&pageSize=100&sort=order:asc,level:asc&filter={"level":0}
   ```
   
   **Örnek Response:**
   ```json
   {
     "data": [
       {
         "__dataId": "menu-item-1",
         "order": 0,
         "itemType": "header",
         "header": "Home",
         "level": 0,
         "permissions": { ... }
       },
       {
         "__dataId": "menu-item-2",
         "order": 1,
         "itemType": "item",
         "title": "Analytical",
         "icon": "ChartPieIcon",
         "to": "/dashboards/analytical",
         "level": 0,
         "parentId": null,
         "permissions": { ... }
       }
     ],
     "totalCount": 99,
     "page": 1,
     "pageSize": 100,
     "totalPages": 1
   }
   ```

2. **Tek Menu Item Okuma**:
   ```
   GET /api/v1/data/@side_menu/{__dataId}
   ```
   - Belirli bir menu item'ı okumak için

3. **Menu Item Oluşturma**:
   ```
   POST /api/v1/data/@side_menu
   ```
   - Yeni menu item eklemek için
   - Request body'de tüm field'lar (order, itemType, title, icon, to, permissions, vb.)

4. **Menu Item Güncelleme**:
   ```
   PUT /api/v1/data/@side_menu/{__dataId}
   ```
   - Mevcut menu item'ı güncellemek için

5. **Menu Item Silme**:
   ```
   DELETE /api/v1/data/@side_menu/{__dataId}
   ```
   - Menu item'ı silmek için (soft delete)

6. **Toplu Menu Item Ekleme** (Migration için):
   ```
   POST /api/v1/data/@side_menu/bulk
   ```
   - Hard-coded menü verilerini toplu olarak eklemek için
   - Request body'de array of menu items

### Frontend Kullanım Senaryoları

#### Senaryo 1: Uygulama Başlangıcında Menu Items Çekme

```typescript
// stores/sideMenu.ts
async function loadMenuItems() {
  try {
    // Tüm menu items'ı çek (root level önce, sonra nested)
    const response = await $fetch('/api/v1/data/@side_menu', {
      method: 'GET',
      params: {
        page: 1,
        pageSize: 1000, // Tüm items (pagination olmadan)
        sort: 'order:asc,level:asc', // Önce order, sonra level
        fields: 'order,itemType,header,title,icon,to,type,parentId,level,chip,chipBgColor,chipColor,chipVariant,chipIcon,disabled,subCaption,permissions,__dataId', // Gerekli field'lar
      },
      headers: {
        Authorization: `Bearer ${authStore.accessToken}`
      }
    });
    
    // Menu items'ı cache'le
    this.menuItems = response.data;
    this.menuItemsTree = buildMenuTree(response.data); // Parent-child ilişkisini kur
    
    return response.data;
  } catch (error) {
    console.error('Menu items yüklenemedi:', error);
    // Fallback: Hard-coded menu kullan
    return fallbackMenuItems;
  }
}
```

#### Senaryo 2: Permission Bazlı Filtreleme (Frontend'de)

```typescript
// Frontend'de permission kontrolü yapılır
function filterMenuItemsByPermission(items: SideMenuItem[], userGroups: string[], isAdmin: boolean): SideMenuItem[] {
  if (isAdmin) {
    return items; // Admin tüm items'ı görür
  }
  
  return items.filter(item => {
    // Header item'lar her zaman gösterilir
    if (item.itemType === 'header') {
      return true;
    }
    
    // Permission kontrolü
    if (!item.permissions?.groups) {
      return true; // Permission yoksa göster (backward compatibility)
    }
    
    // Kullanıcının gruplarından herhangi birinde view yetkisi var mı?
    return userGroups.some(groupName => {
      return item.permissions.groups[groupName]?.view === true;
    });
  });
}
```

#### Senaryo 3: Tree Yapısı Oluşturma (Frontend'de)

```typescript
function buildMenuTree(items: SideMenuItem[]): SideMenuItem[] {
  // Parent-child ilişkisini kur
  const itemMap = new Map<string, SideMenuItem>();
  const rootItems: SideMenuItem[] = [];
  
  // Önce tüm items'ı map'e ekle
  items.forEach(item => {
    itemMap.set(item.__dataId, { ...item, children: [] });
  });
  
  // Parent-child ilişkisini kur
  items.forEach(item => {
    const itemWithChildren = itemMap.get(item.__dataId)!;
    
    if (item.parentId) {
      const parent = itemMap.get(item.parentId);
      if (parent) {
        if (!parent.children) {
          parent.children = [];
        }
        parent.children.push(itemWithChildren);
      }
    } else {
      rootItems.push(itemWithChildren);
    }
  });
  
  // Children'ları sırala (order'a göre)
  function sortChildren(items: SideMenuItem[]) {
    items.forEach(item => {
      if (item.children && item.children.length > 0) {
        item.children.sort((a, b) => a.order - b.order);
        sortChildren(item.children);
      }
    });
  }
  
  sortChildren(rootItems);
  rootItems.sort((a, b) => a.order - b.order);
  
  return rootItems;
}
```

### Backend'de Ek Geliştirme Gereksinimi: ❌ YOK

**Neden Backend Değişikliği Gerekmeyecek:**

1. ✅ **Dataset Schema**: Mevcut field type'ları yeterli (text, number, bool, object, vb.)
2. ✅ **Data CRUD**: Tüm CRUD operasyonları mevcut
3. ✅ **Filtering & Sorting**: MongoDB filter ve sort desteği var
4. ✅ **Pagination**: Server-side pagination desteği var
5. ✅ **Field Selection**: Performans için field selection desteği var
6. ✅ **Bulk Insert**: Migration için bulk insert desteği var
7. ✅ **Soft Delete**: Soft delete desteği var

**Permission Kontrolü:**

- ✅ **Frontend'de Yapılacak**: Token'dan gelen `user_groups` ve `isAdmin` bilgisi ile frontend'de filtreleme yapılacak
- ✅ **Backend Sadece Veri Sağlar**: Backend sadece ham veriyi döndürür, permission kontrolü frontend'de yapılır
- ⚠️ **Not**: Güvenlik için backend'de de permission kontrolü yapılabilir (Phase 3 - Dataset Authorization), ancak bu şu an için gerekli değil çünkü:
  - Menu items'lar public data değil (authentication gerektirir)
  - Frontend permission kontrolü yeterli (UI görünürlüğü için)
  - Sayfa erişim kontrolü middleware'de yapılıyor
  - Backend API'ler zaten authentication gerektiriyor

### Önerilen API Kullanım Stratejisi

1. **Initial Load**: Uygulama başlangıcında tüm menu items çekilir ve cache'lenir
2. **Permission Filtering**: Frontend'de kullanıcı gruplarına göre filtreleme yapılır
3. **Tree Building**: Frontend'de parent-child ilişkisi kurulur
4. **Cache Management**: Menu items cache'lenir, permission değişikliklerinde invalidation yapılır
5. **Fallback**: API hata durumunda hard-coded menu kullanılır

### Sonuç

✅ **MngDataGateway'in mevcut endpoint'leri side menu yönetimi için tamamen yeterlidir.**  
✅ **Backend'de herhangi bir değişiklik yapılmasına gerek yoktur.**  
✅ **Tüm işlemler (CRUD, filtering, sorting, pagination) mevcut endpoint'lerle yapılabilir.**  
✅ **Permission kontrolü frontend'de yapılacak, backend sadece veri sağlayacak.**

---

## DOM Element Yetkilendirmesi

### Genel Bakış

Sayfa içindeki DOM elemanları (butonlar, action sütunları, vb.) için yetkilendirme kontrolü yapılabilir. Bu sistem, sayfaya ait menü item'ındaki permissions'a göre elemanların görünür/gizli olmasını sağlar.

### Desteklenen Element Tipleri

1. **Action Butonları** (Data Table'larda)
   - Düzenle butonu (update permission)
   - Sil butonu (delete permission)
   - Görüntüle butonu (view permission)

2. **Sayfa Üstü Butonları**
   - Yeni Kayıt Ekle butonu (create permission)
   - Export butonu (export permission)

3. **Action Sütunları** (Data Table'larda)
   - Tüm action sütunu (içindeki butonlar yetkilere göre)

### Route-to-Menu-Item Mapping

Her sayfanın hangi menü item'ına ait olduğunu bulmak için route path'i kullanılır:

```typescript
// Composables/useMenuPermissions.ts
export function useMenuPermissions() {
  const route = useRoute();
  const menuStore = useSideMenuStore();
  
  /**
   * Mevcut route'a göre menü item'ını bulur
   */
  function getCurrentMenuItem(): SideMenuItem | null {
    const currentPath = route.path;
    
    // Exact match önce kontrol edilir
    let menuItem = menuStore.allMenuItems.find(item => item.to === currentPath);
    
    // Exact match bulunamazsa, route'un menü item'ının to path'i ile başlayıp başlamadığını kontrol et
    if (!menuItem) {
      menuItem = menuStore.allMenuItems.find(item => {
        if (!item.to || item.to === '#' || item.to === '/') return false;
        return currentPath.startsWith(item.to);
      });
    }
    
    return menuItem || null;
  }
  
  return {
    getCurrentMenuItem,
  };
}
```

### Vue Directive Yaklaşımı

**v-permission Directive** oluşturulabilir:

```typescript
// plugins/v-permission.ts veya directives/permission.ts
import { useAuthStore } from '@/stores/auth';
import { useMenuPermissions } from '@/composables/useMenuPermissions';

export default defineNuxtPlugin((nuxtApp) => {
  nuxtApp.vueApp.directive('permission', {
    mounted(el: HTMLElement, binding) {
      const authStore = useAuthStore();
      const { getCurrentMenuItem } = useMenuPermissions();
      
      // Admin kontrolü
      if (authStore.isAdmin) {
        return; // Admin ise tüm elemanları göster
      }
      
      // Menü item'ını bul
      const menuItem = getCurrentMenuItem();
      if (!menuItem || !menuItem.permissions) {
        // Permission tanımı yoksa elemanı gizle (güvenli varsayılan)
        el.style.display = 'none';
        return;
      }
      
      // Permission tipini al (create, update, delete, export)
      const permission = binding.value; // 'create', 'update', 'delete', 'export'
      
      // Yetki kontrolü
      const hasPermission = checkPermission(menuItem, permission, authStore.userGroups);
      
      if (!hasPermission) {
        // Yetki yoksa elemanı gizle
        el.style.display = 'none';
      }
    },
    updated(el: HTMLElement, binding) {
      // Permission değiştiğinde tekrar kontrol et
      // (mounted ile aynı mantık)
      // ...
    }
  });
});
```

**Kullanım:**

```vue
<template>
  <!-- Yeni Kayıt Ekle Butonu -->
  <v-btn 
    v-permission="'create'" 
    color="primary" 
    to="/apps/users/create"
  >
    <UserPlusIcon class="mr-2" size="20" />
    Yeni Kullanıcı Ekle
  </v-btn>
  
  <!-- Export Butonu -->
  <v-btn 
    v-permission="'export'" 
    color="success" 
    @click="exportUsers"
  >
    <DownloadIcon class="mr-2" size="20" />
    Export
  </v-btn>
  
  <!-- Data Table Actions Column -->
  <template v-slot:item.actions="{ item }">
    <div class="d-flex ga-2 justify-end">
      <!-- Görüntüle Butonu (view permission) -->
      <v-btn
        v-permission="'view'"
        icon
        size="small"
        variant="text"
        color="primary"
        @click="viewUser(item)"
      >
        <EyeIcon size="18" />
        <v-tooltip activator="parent" location="top">Görüntüle</v-tooltip>
      </v-btn>
      
      <!-- Düzenle Butonu (update permission) -->
      <v-btn
        v-permission="'update'"
        icon
        size="small"
        variant="text"
        color="info"
        @click="editUser(item)"
      >
        <EditIcon size="18" />
        <v-tooltip activator="parent" location="top">Düzenle</v-tooltip>
      </v-btn>
      
      <!-- Sil Butonu (delete permission) -->
      <v-btn
        v-permission="'delete'"
        icon
        size="small"
        variant="text"
        color="error"
        @click="deleteUser(item)"
      >
        <TrashIcon size="18" />
        <v-tooltip activator="parent" location="top">Sil</v-tooltip>
      </v-btn>
    </div>
  </template>
</template>
```

### Composables Yaklaşımı (Alternatif/Önerilen)

**usePagePermissions Composable** oluşturulabilir:

```typescript
// composables/usePagePermissions.ts
export function usePagePermissions() {
  const authStore = useAuthStore();
  const route = useRoute();
  const menuStore = useSideMenuStore();
  
  // Mevcut sayfanın menü item'ını bul
  const currentMenuItem = computed(() => {
    const currentPath = route.path;
    
    // Exact match
    let item = menuStore.allMenuItems.find(item => item.to === currentPath);
    
    // Prefix match (nested routes için)
    if (!item) {
      item = menuStore.allMenuItems.find(item => {
        if (!item.to || item.to === '#' || item.to === '/') return false;
        return currentPath.startsWith(item.to);
      });
    }
    
    return item || null;
  });
  
  // Permission kontrol fonksiyonu
  function hasPermission(permission: 'view' | 'create' | 'update' | 'delete' | 'export'): boolean {
    // Admin kontrolü
    if (authStore.isAdmin) return true;
    
    // Menü item bulunamazsa false döndür (güvenli varsayılan)
    if (!currentMenuItem.value || !currentMenuItem.value.permissions) {
      return false;
    }
    
    // Permission kontrolü
    return authStore.userGroups.some(groupName => {
      const groupPerms = currentMenuItem.value!.permissions!.groups[groupName];
      return groupPerms?.[permission] === true;
    });
  }
  
  // Computed properties for common permissions
  const canView = computed(() => hasPermission('view'));
  const canCreate = computed(() => hasPermission('create'));
  const canUpdate = computed(() => hasPermission('update'));
  const canDelete = computed(() => hasPermission('delete'));
  const canExport = computed(() => hasPermission('export'));
  
  return {
    currentMenuItem,
    hasPermission,
    canView,
    canCreate,
    canUpdate,
    canDelete,
    canExport,
  };
}
```

**Kullanım:**

```vue
<script setup>
import { usePagePermissions } from '@/composables/usePagePermissions';

const { canCreate, canUpdate, canDelete, canExport } = usePagePermissions();
</script>

<template>
  <!-- Yeni Kayıt Ekle Butonu -->
  <v-btn 
    v-if="canCreate" 
    color="primary" 
    to="/apps/users/create"
  >
    <UserPlusIcon class="mr-2" size="20" />
    Yeni Kullanıcı Ekle
  </v-btn>
  
  <!-- Export Butonu -->
  <v-btn 
    v-if="canExport" 
    color="success" 
    @click="exportUsers"
  >
    <DownloadIcon class="mr-2" size="20" />
    Export
  </v-btn>
  
  <!-- Data Table Actions Column -->
  <template v-slot:item.actions="{ item }">
    <div class="d-flex ga-2 justify-end">
      <!-- Görüntüle Butonu -->
      <v-btn
        v-if="canView"
        icon
        size="small"
        variant="text"
        color="primary"
        @click="viewUser(item)"
      >
        <EyeIcon size="18" />
        <v-tooltip activator="parent" location="top">Görüntüle</v-tooltip>
      </v-btn>
      
      <!-- Düzenle Butonu -->
      <v-btn
        v-if="canUpdate"
        icon
        size="small"
        variant="text"
        color="info"
        @click="editUser(item)"
      >
        <EditIcon size="18" />
        <v-tooltip activator="parent" location="top">Düzenle</v-tooltip>
      </v-btn>
      
      <!-- Sil Butonu -->
      <v-btn
        v-if="canDelete"
        icon
        size="small"
        variant="text"
        color="error"
        @click="deleteUser(item)"
      >
        <TrashIcon size="18" />
        <v-tooltip activator="parent" location="top">Sil</v-tooltip>
      </v-btn>
    </div>
  </template>
</template>
```

### Action Column Görünürlük Kontrolü

Eğer hiçbir action butonuna yetki yoksa, action sütunu da gizlenebilir:

```vue
<script setup>
import { usePagePermissions } from '@/composables/usePagePermissions';

const { canView, canUpdate, canDelete } = usePagePermissions();

// Action sütununu göster/gizle
const showActionsColumn = computed(() => {
  return canView.value || canUpdate.value || canDelete.value;
});

// Headers array'inden action sütununu dinamik olarak ekle/çıkar
const headers = computed(() => {
  const baseHeaders = [
    { title: 'Kullanıcı Adı', key: 'username' },
    { title: 'E-posta', key: 'email' },
    { title: 'Durum', key: 'isActive' },
    { title: 'Gruplar', key: 'groups' },
    { title: 'Oluşturma Tarihi', key: 'createdAt' },
  ];
  
  // Yetki varsa action sütununu ekle
  if (showActionsColumn.value) {
    baseHeaders.push({ title: 'İşlemler', key: 'actions', sortable: false, align: 'end' });
  }
  
  return baseHeaders;
});
</script>

<template>
  <v-data-table
    :headers="headers"
    :items="items"
    ...
  >
    <!-- Actions Column sadece showActionsColumn true ise render edilir -->
    <template v-if="showActionsColumn" v-slot:item.actions="{ item }">
      <!-- Action buttons -->
    </template>
  </v-data-table>
</template>
```

### Component Wrapper Yaklaşımı (İleri Seviye)

Daha modüler bir yaklaşım için wrapper component oluşturulabilir:

```vue
<!-- components/shared/PermissionWrapper.vue -->
<script setup>
import { usePagePermissions } from '@/composables/usePagePermissions';

const props = defineProps<{
  permission: 'view' | 'create' | 'update' | 'delete' | 'export';
  fallback?: 'hidden' | 'disabled'; // 'hidden': gizle, 'disabled': devre dışı bırak
}>();

const { hasPermission } = usePagePermissions();
const canAccess = computed(() => hasPermission(props.permission));
</script>

<template>
  <template v-if="canAccess">
    <slot />
  </template>
  <template v-else-if="fallback === 'disabled'">
    <span :class="{ 'opacity-50': true }">
      <slot />
    </span>
  </template>
</template>
```

**Kullanım:**

```vue
<template>
  <PermissionWrapper permission="create">
    <v-btn color="primary" to="/apps/users/create">
      Yeni Kullanıcı Ekle
    </v-btn>
  </PermissionWrapper>
  
  <PermissionWrapper permission="export">
    <v-btn color="success" @click="exportUsers">
      Export
    </v-btn>
  </PermissionWrapper>
</template>
```

### Best Practices

1. **Önerilen Yaklaşım**: `usePagePermissions` composable kullanımı (daha temiz ve test edilebilir)
2. **Performance**: Permission kontrolleri computed property'lerde cache'lenir
3. **Fallback**: Permission tanımı yoksa eleman gizlenir (güvenli varsayılan)
4. **Admin Bypass**: Admin kullanıcılar tüm elemanları görür
5. **Route Matching**: Exact match önce, sonra prefix match kontrol edilir
6. **Cache**: Menü item'ları cache'lendiğinde permission kontrolleri de güncellenir

### Sayfa Tipi Bazlı Read-Only Modu

**Kurallar:**

1. **User Sayfaları** (`pageType: 'user'`):
   - Permission-based erişim
   - UI editable/read-only durumu permission'lara göre belirlenir
   - Örnek: Normal uygulama sayfaları

2. **Manager Sayfaları** (`pageType: 'manager'`):
   - Sadece `is_manager: true` veya `is_admin: true` olanlar erişebilir
   - UI **her zaman read-only** (editable değil)
   - Create, Update, Delete butonları gösterilmez
   - Örnek: Kullanıcı Yönetimi, Grup Yönetimi sayfaları

3. **Admin Sayfaları** (`pageType: 'admin'`):
   - Sadece `is_admin: true` olanlar erişebilir
   - UI **her zaman read-only** (editable değil)
   - Create, Update, Delete butonları gösterilmez
   - Örnek: Dataset yönetimi, sistem konfigürasyon sayfaları

**Admin Kullanıcı Özel Durumu:**
- Admin kullanıcılar (`is_admin: true`) **tüm sayfa tiplerine** erişebilir
- Admin kullanıcılar sayfaları **editable modda** görür (read-only değil)
- Admin kullanıcılar tüm işlemleri (create, update, delete) yapabilir

### Örnek: Tam Kullanım Senaryosu

```vue
<!-- pages/apps/users/index.vue -->
<script setup>
import { usePagePermissions } from '@/composables/usePagePermissions';
import { useUserStore } from '@/stores/apps/user';

const { canCreate, canUpdate, canDelete, canExport, canView, isPageReadOnly } = usePagePermissions();
const userStore = useUserStore();

// Headers dinamik olarak oluştur
const headers = computed(() => {
  const baseHeaders = [
    { title: 'Kullanıcı Adı', key: 'username' },
    { title: 'E-posta', key: 'email' },
    { title: 'Durum', key: 'isActive' },
    { title: 'Gruplar', key: 'groups' },
    { title: 'Oluşturma Tarihi', key: 'createdAt' },
  ];
  
  // Action sütunu ekle (en az bir yetki varsa)
  if (canView.value || canUpdate.value || canDelete.value) {
    baseHeaders.push({ 
      title: 'İşlemler', 
      key: 'actions', 
      sortable: false, 
      align: 'end' 
    });
  }
  
  return baseHeaders;
});

// Export fonksiyonu
const exportUsers = async () => {
  if (!canExport.value) return;
  await userStore.exportUsers('csv');
};
</script>

<template>
  <v-card>
    <v-card-item>
      <!-- Toolbar -->
      <div class="d-flex justify-space-between align-center mb-4">
        <div>
          <h2 class="text-h5">Kullanıcı Yönetimi</h2>
        </div>
        <div class="d-flex ga-2">
          <!-- Yeni Kullanıcı Ekle Butonu -->
          <!-- Read-only sayfalarda gösterilmez -->
          <v-btn 
            v-if="canCreate && !isPageReadOnly"
            color="primary" 
            to="/apps/users/create"
          >
            <UserPlusIcon class="mr-2" size="20" />
            Yeni Kullanıcı Ekle
          </v-btn>
          
          <!-- Export Butonu -->
          <v-btn 
            v-if="canExport"
            color="success" 
            @click="exportUsers"
          >
            <DownloadIcon class="mr-2" size="20" />
            Export
          </v-btn>
          
          <!-- Read-Only Indicator (Manager/Admin sayfaları için) -->
          <v-chip 
            v-if="isPageReadOnly" 
            color="info" 
            variant="flat"
            size="small"
          >
            <LockIcon size="16" class="mr-1" />
            Salt Okunur
          </v-chip>
        </div>
      </div>
      
      <!-- Data Table -->
      <v-data-table
        :headers="headers"
        :items="userStore.users"
        ...
      >
        <!-- Action Column -->
        <!-- Read-only sayfalarda sadece görüntüle butonu gösterilir -->
        <template v-if="canView || (canUpdate && !isPageReadOnly) || (canDelete && !isPageReadOnly)" v-slot:item.actions="{ item }">
          <div class="d-flex ga-2 justify-end">
            <!-- Görüntüle -->
            <v-btn
              v-if="canView"
              icon
              size="small"
              variant="text"
              color="primary"
              @click="viewUser(item)"
            >
              <EyeIcon size="18" />
              <v-tooltip activator="parent" location="top">Görüntüle</v-tooltip>
            </v-btn>
            
            <!-- Düzenle (Read-only sayfalarda gösterilmez) -->
            <v-btn
              v-if="canUpdate && !isPageReadOnly"
              icon
              size="small"
              variant="text"
              color="info"
              @click="editUser(item)"
            >
              <EditIcon size="18" />
              <v-tooltip activator="parent" location="top">Düzenle</v-tooltip>
            </v-btn>
            
            <!-- Sil (Read-only sayfalarda gösterilmez) -->
            <v-btn
              v-if="canDelete && !isPageReadOnly"
              icon
              size="small"
              variant="text"
              color="error"
              @click="deleteUser(item)"
            >
              <TrashIcon size="18" />
              <v-tooltip activator="parent" location="top">Sil</v-tooltip>
            </v-btn>
          </div>
        </template      >
        <!-- Action Column -->
        <!-- Read-only sayfalarda sadece görüntüle butonu gösterilir -->
        <template v-if="canView || (canUpdate && !isPageReadOnly) || (canDelete && !isPageReadOnly)" v-slot:item.actions="{ item }">
          <div class="d-flex ga-2 justify-end">
            <!-- Görüntüle -->
            <v-btn
              v-if="canView"
              icon
              size="small"
              variant="text"
              color="primary"
              @click="viewUser(item)"
            >
              <EyeIcon size="18" />
              <v-tooltip activator="parent" location="top">Görüntüle</v-tooltip>
            </v-btn>
            
            <!-- Düzenle (Read-only sayfalarda gösterilmez) -->
            <v-btn
              v-if="canUpdate && !isPageReadOnly"
              icon
              size="small"
              variant="text"
              color="info"
              @click="editUser(item)"
            >
              <EditIcon size="18" />
              <v-tooltip activator="parent" location="top">Düzenle</v-tooltip>
            </v-btn>
            
            <!-- Sil (Read-only sayfalarda gösterilmez) -->
            <v-btn
              v-if="canDelete && !isPageReadOnly"
              icon
              size="small"
              variant="text"
              color="error"
              @click="deleteUser(item)"
            >
              <TrashIcon size="18" />
              <v-tooltip activator="parent" location="top">Sil</v-tooltip>
            </v-btn>
          </div>
        </template>
      </v-data-table>
    </v-card-item>
  </v-card>
</template>
```

---

## Teknik Gereksinimler

### Öncelikler
- [ ] [Henüz belirlenmedi]

### Bağımlılıklar
- Vue.js 3 (Composition API)
- Nuxt.js 3
- Vuetify 3
- Pinia (State Management)
- vue-tabler-icons

### Performans Notları
- Menu items `shallowRef` ile tanımlı (performans optimizasyonu)
- Perfect Scrollbar kullanılıyor
- Lazy loading düşünülmeli (çok sayıda menu item varsa)

---

## Implementasyon Özeti ve Hazırlık Durumu

### Planlama Tamamlanma Durumu ✅

**Tamamlanan Planlamalar:**
- ✅ Dataset yapısı ve schema tasarımı
- ✅ Yetkilendirme sistemi mantığı (group-based permissions, admin bypass, page types)
- ✅ DOM element yetkilendirme yaklaşımı
- ✅ Side Menu Manager sayfa tasarımı ve komponent yapısı
- ✅ Icon seçim sistemi (MDI ve Tabler desteği)
- ✅ Cache ve performans stratejisi (localStorage + memory hybrid)
- ✅ Error handling ve fallback mekanizması
- ✅ Real-time updates planı (SignalR entegrasyonu)
- ✅ Menu search/filter planı
- ✅ Sıralama ve nested header desteği
- ✅ Tüm faz planları (9 faz)
- ✅ Önceliklendirme ve implementasyon sırası
- ✅ Ek özellikler planlaması (templates, bulk operations, export/import, vb.)

### Implementasyona Başlamadan Önce Yapılması Gerekenler ⚠️

1. **Kontrol Edilmesi Gereken Endpoint'ler ve Servisler:**
   - [ ] MngKeeper Group API: Grup listesi endpoint'i test edilmeli
   - [ ] MngDataGateway Bulk Update: Bulk update endpoint'i var mı kontrol edilmeli
   - [ ] MngHub SignalR: SignalR entegrasyonu mevcut mu kontrol edilmeli

2. **Karar Verilmesi Gereken Konular:**
   - [ ] **Icon Listesi Boyutu**: MDI için 100-200 icon mu, yoksa tüm icon'lar mı? (Öneri: 100-200 popüler icon)
   - [ ] **Migration Stratejisi**: Aşamalı geçiş mi, tek seferde mi? (Öneri: Aşamalı geçiş, downtime olmadan)

3. **Hazırlanması Gereken Dosyalar:**
   - [ ] Fallback menu backup: `compat/sidebarItem.fallback.ts` oluşturulmalı
   - [ ] Hard-coded menu export script: `scripts/migration/export-menu-items.ps1` oluşturulmalı

### İlk Implementasyon İçin Minimum Gereksinimler ✅

**Faz 1-3 (MVP) için yeterli:**
1. ✅ MngDataGateway API endpoint'leri (zaten biliniyor)
2. ✅ Dataset schema yapısı (detaylı planlandı)
3. ✅ Authentication token formatı (mevcut auth store'dan biliniyor)
4. ✅ Menu item yapısı (detaylı planlandı)
5. ✅ Fallback menu stratejisi (planlandı)

**Eksik ve kontrol edilmesi gerekenler:**
1. ⚠️ MngKeeper Group API endpoint (Permission Editor için)
2. ⚠️ MngDataGateway Bulk Update endpoint (drag & drop için)
3. ⚠️ SignalR entegrasyonu durumu (Real-time updates için)
4. ⚠️ Icon listesi kaynakları (Icon Picker için)

### İlk Adımlar (Faz 1 Başlangıç)

**1. Sistem Hazırlığı:**
```powershell
# 1. MngDataGateway API erişilebilir mi test et
# 2. Token al ve test et
# 3. MongoDB bağlantısı aktif mi kontrol et
```

**2. Dataset Oluşturma:**
```powershell
# 1. System Datasets kategorisi oluştur
POST /api/v1/dataset-categories
{
  "categoryName": "System Datasets",
  "categoryDescription": "System-level datasets for application configuration"
}

# 2. @side_menu dataset oluştur
POST /api/v1/datasets
{
  "name": "@side_menu",
  "category": "<category__dataId>",
  "fields": [...],
  "indexes": [...],
  "permissions": {...}
}
```

**3. Hard-Coded Menu Export:**
```typescript
// scripts/migration/export-menu-items.ts
// sidebarItem.ts'den menu items'ı export et
// Parent-child ilişkilerini kur
// Order ve level hesapla
// JSON formatına çevir
```

**4. Menu Verilerini Yükleme:**
```powershell
# Export edilen JSON'u bulk insert ile yükle
POST /api/v1/data/@side_menu/bulk
{
  "data": [...]
}
```

**5. Frontend Store Hazırlığı:**
```typescript
// stores/apps/sideMenu.ts
// Store'u oluştur
// API entegrasyonunu yap
// Cache mekanizmasını ekle
```

---

## Uygulama Planı

### Faz 1: Dataset ve Temel Altyapı ✅ PLANLAMA TAMAMLANDI

**Amaç**: Veritabanı altyapısını oluşturma ve temel dataset yapısını kurma

**Adımlar:**

1. **System Datasets Kategorisi Oluşturma**
   - [ ] MngDataGateway API ile kategori oluştur: `POST /api/v1/dataset-categories`
   - [ ] Kategori: "System Datasets"
   - [ ] `__dataId` değerini kaydet (dataset için category referansı olarak)

2. **@side_menu Dataset Oluşturma**
   - [ ] Dataset schema oluştur (field definitions, index definitions)
   - [ ] Category field'ına System Datasets `__dataId` ekle
   - [ ] Dataset'i MngDataGateway'e gönder: `POST /api/v1/datasets`
   - [ ] Dataset'in başarıyla oluşturulduğunu doğrula

3. **Hard-Coded Menu Verilerini Export Etme**
   - [ ] `sidebarItem.ts` dosyasını parse et
   - [ ] Parent-child ilişkilerini kur (parentId hesapla)
   - [ ] Order ve level değerlerini hesapla
   - [ ] Icon'ları map et (vue-tabler-icons component adları → string)
   - [ ] JSON formatına çevir

4. **Menu Verilerini Veritabanına Yükleme**
   - [ ] Export edilen JSON'u bulk insert ile yükle: `POST /api/v1/data/@side_menu/bulk`
   - [ ] Verilerin doğru yüklendiğini doğrula
   - [ ] Parent-child ilişkilerini kontrol et

**Tahmini Süre**: 2-3 saat

**Ön Koşullar**:
- MngDataGateway API erişilebilir olmalı
- MongoDB bağlantısı aktif olmalı
- Token ile authentication yapılabilmeli

---

### Faz 2: Frontend Store ve Composable'lar ✅ PLANLAMA TAMAMLANDI

**Amaç**: Frontend'de menu item yönetimi için store ve composable'ları oluşturma

**Adımlar:**

1. **Side Menu Store Oluşturma**
   - [ ] `stores/apps/sideMenu.ts` oluştur
   - [ ] State: `menuItems`, `menuItemsTree`, `loading`, `error`, `lastUpdated`
   - [ ] Actions:
     - [ ] `loadMenuItems()` - API'den menu items çek
     - [ ] `buildMenuTree()` - Flat array'i tree yapısına çevir
     - [ ] `filterMenuItemsByPermission()` - Permission bazlı filtreleme
     - [ ] `getMenuItemByRoute()` - Route path'ten menu item bul
   - [ ] Getters:
     - [ ] `visibleMenuItems` - Kullanıcının görebileceği menu items
     - [ ] `menuItemsByLevel` - Level bazlı gruplandırma

2. **Page Permissions Composable**
   - [ ] `composables/usePagePermissions.ts` oluştur
   - [ ] Route-to-menu-item mapping
   - [ ] Permission kontrol fonksiyonları
   - [ ] Read-only durumu kontrolü
   - [ ] Computed properties: `canView`, `canCreate`, `canUpdate`, `canDelete`, `canExport`, `isPageReadOnly`

3. **Menu Permissions Helper Fonksiyonları**
   - [ ] `utils/menu-permissions.ts` oluştur
   - [ ] `canViewMenuItem()` fonksiyonu
   - [ ] `canAccessPageType()` fonksiyonu
   - [ ] `hasPermission()` fonksiyonu
   - [ ] `checkPermission()` helper

4. **Icon Utils ve Mapping**
   - [ ] `utils/icons/icon-utils.ts` oluştur
   - [ ] Icon type detection (mdi vs tabler)
   - [ ] Icon render helper fonksiyonları
   - [ ] MDI icon listesi (statik)
   - [ ] Tabler icon listesi (dinamik veya statik)

**Tahmini Süre**: 4-5 saat

**Bağımlılıklar**: Faz 1 tamamlanmış olmalı

---

### Faz 3: Sidebar Entegrasyonu ✅ PLANLAMA TAMAMLANDI

**Amaç**: Mevcut sidebar'ı API'den gelen menu items ile çalışacak şekilde güncelleme

**Adımlar:**

1. **Sidebar Component Güncelleme**
   - [ ] `components/lc/Full/vertical-sidebar/index.vue` güncelle
   - [ ] Hard-coded `sidebarItems` yerine store'dan `visibleMenuItems` kullan
   - [ ] Loading state ekle
   - [ ] Error state ekle (fallback menu göster)

2. **Icon Component Güncelleme**
   - [ ] `components/lc/Full/vertical-sidebar/Icon.vue` güncelle
   - [ ] MDI icon desteği ekle
   - [ ] Tabler icon desteği ekle (mevcut)
   - [ ] `iconType` prop'u ekle
   - [ ] Backward compatibility (iconType yoksa default 'tabler')

3. **Route Guard/Middleware**
   - [ ] `middleware/menu-permission.ts` oluştur
   - [ ] Route-to-menu-item mapping
   - [ ] Sayfa tipi kontrolü
   - [ ] View permission kontrolü
   - [ ] Unauthorized redirect

4. **Unauthorized Page**
   - [ ] `pages/unauthorized.vue` oluştur
   - [ ] "Yetkisiz Erişim" mesajı
   - [ ] Geri dönüş butonu
   - [ ] Ana sayfaya yönlendirme

5. **Error Handling ve Fallback**
   - [ ] API hata durumunda fallback menu
   - [ ] Cache'den yükleme (varsa)
   - [ ] Error notification
   - [ ] Retry mekanizması (opsiyonel)

**Tahmini Süre**: 5-6 saat

**Bağımlılıklar**: Faz 2 tamamlanmış olmalı

---

### Faz 4: Cache ve Performans Optimizasyonu ✅ PLANLAMA TAMAMLANDI

**Amaç**: Menu items için cache mekanizması ve performans optimizasyonu

**Adımlar:**

1. **Cache Mekanizması**
   - [ ] `utils/cache/menu-cache.ts` oluştur
   - [ ] localStorage veya IndexedDB kullan
   - [ ] Cache key strategy: `menuItems_{userId}_{domainId}`
   - [ ] TTL yönetimi (5-10 dakika)
   - [ ] Cache set/get/clear fonksiyonları

2. **Cache Invalidation**
   - [ ] Manual refresh (toolbar butonu)
   - [ ] TTL sonrası otomatik refresh
   - [ ] Permission değişikliklerinde invalidation
   - [ ] Real-time invalidation (SignalR - Faz 5'te)

3. **Performans Optimizasyonları**
   - [ ] Virtual scrolling (çok sayıda menu item varsa)
   - [ ] Lazy loading (tree view için)
   - [ ] Debounce search/filter
   - [ ] Memoization (computed properties)
   - [ ] ShallowRef kullanımı (zaten var)

**Tahmini Süre**: 3-4 saat

**Bağımlılıklar**: Faz 3 tamamlanmış olmalı

---

### Faz 5: DOM Element Yetkilendirme ✅ PLANLAMA TAMAMLANDI

**Amaç**: Sayfa içindeki DOM elemanları için yetkilendirme kontrolü

**Adımlar:**

1. **usePagePermissions Composable Implementasyonu**
   - [ ] `composables/usePagePermissions.ts` implement et
   - [ ] Route-to-menu-item mapping
   - [ ] Permission kontrolleri
   - [ ] Read-only durumu
   - [ ] Computed properties

2. **Permission Wrapper Component (Opsiyonel)**
   - [ ] `components/shared/PermissionWrapper.vue` oluştur
   - [ ] Slot-based rendering
   - [ ] Fallback seçenekleri (hidden/disabled)

3. **v-permission Directive (Opsiyonel)**
   - [ ] `plugins/v-permission.ts` oluştur
   - [ ] Directive implementasyonu
   - [ ] Permission kontrolü

4. **Test ve Entegrasyon**
   - [ ] Mevcut sayfalarda test (users, groups, vb.)
   - [ ] Action butonları için permission kontrolü
   - [ ] Read-only durumu testleri

**Tahmini Süre**: 4-5 saat

**Bağımlılıklar**: Faz 3 tamamlanmış olmalı

---

### Faz 6: Side Menu Manager Sayfası ✅ PLANLAMA TAMAMLANDI

**Amaç**: Menu item'ları yönetmek için admin sayfası

**Adımlar:**

1. **Ana Sayfa**
   - [ ] `pages/apps/side-menu-manager/index.vue` oluştur
   - [ ] 3 bölümlü layout (tree + detail/form)
   - [ ] Responsive tasarım

2. **Store**
   - [ ] `stores/apps/sideMenuManager.ts` oluştur
   - [ ] State management
   - [ ] CRUD operations
   - [ ] Tree building
   - [ ] Order management
   - [ ] Circular reference kontrolü

3. **Tree View Component**
   - [ ] `components/apps/side-menu-manager/MenuTreeView.vue` oluştur
   - [ ] Hierarchical tree görünümü
   - [ ] Drag & drop desteği (vue-draggable-next)
   - [ ] Expand/collapse
   - [ ] Search/filter

4. **Detail/Form Components**
   - [ ] `components/apps/side-menu-manager/MenuItemDetail.vue` oluştur
   - [ ] `components/apps/side-menu-manager/MenuItemForm.vue` oluştur
   - [ ] Tüm form alanları
   - [ ] Validation
   - [ ] Parent selector
   - [ ] Icon picker entegrasyonu

5. **Icon Picker Component**
   - [ ] `components/apps/side-menu-manager/IconPicker.vue` oluştur
   - [ ] MDI icon listesi ve preview
   - [ ] Tabler icon listesi ve preview
   - [ ] Search/filter
   - [ ] Icon type toggle

6. **Permission Editor Component**
   - [ ] `components/apps/side-menu-manager/PermissionEditor.vue` oluştur
   - [ ] Grid tablo (groups × permissions)
   - [ ] Checkbox controls
   - [ ] Bulk select butonları
   - [ ] MngKeeper API entegrasyonu (grup listesi)

7. **Toolbar Component**
   - [ ] `components/apps/side-menu-manager/MenuItemToolbar.vue` oluştur
   - [ ] Yeni ekle butonları
   - [ ] Search input
   - [ ] Refresh button
   - [ ] Export/Import butonları

8. **Menu Item'e Side Menu Manager Link**
   - [ ] Sidebar'a "Side Menu Manager" item'ı ekle
   - [ ] Admin/Manager yetkisi gerektirsin
   - [ ] Route: `/apps/side-menu-manager`

**Tahmini Süre**: 12-15 saat

**Bağımlılıklar**: Faz 1-2 tamamlanmış olmalı

---

### Faz 7: Real-Time Updates (SignalR) ✅ PLANLAMA TAMAMLANDI

**Amaç**: Menu item değişikliklerini anlık olarak tüm kullanıcılara yayma

**Adımlar:**

1. **Hub Store Entegrasyonu (Varsa)**
   - [ ] Mevcut hub store'u kontrol et
   - [ ] Yoksa `stores/hub.ts` oluştur
   - [ ] SignalR connection setup
   - [ ] Connection management

2. **Event Listener Registration**
   - [ ] `@side_menu` dataset event'lerini dinle
   - [ ] `DataCreatedEvent` listener
   - [ ] `DataUpdatedEvent` listener
   - [ ] `DataDeletedEvent` listener

3. **Cache Invalidation on Events**
   - [ ] Event geldiğinde cache'i temizle
   - [ ] Menu items'ı yeniden yükle
   - [ ] Permission kontrolü yap (değişen item kullanıcının görebileceği mi?)

4. **UI Updates**
   - [ ] Toast notification (opsiyonel - "Menu güncellendi")
   - [ ] Smooth update (animation)
   - [ ] Loading state (yeniden yüklenirken)

**Tahmini Süre**: 4-5 saat

**Bağımlılıklar**: 
- Faz 4 tamamlanmış olmalı
- MngHub SignalR entegrasyonu aktif olmalı

**Not**: Eğer SignalR entegrasyonu yoksa bu faz atlanabilir, sonra eklenebilir

---

### Faz 8: Menu Item Search/Filter (Frontend) ✅ PLANLAMA TAMAMLANDI

**Amaç**: Sidebar'da menü içinde arama yapabilme

**Adımlar:**

1. **Search Input**
   - [ ] Sidebar üstüne search input ekle
   - [ ] Real-time filtreleme
   - [ ] Debounce (300ms)

2. **Filter Logic**
   - [ ] Title'a göre arama
   - [ ] Route path'e göre arama
   - [ ] Sub caption'a göre arama
   - [ ] Case-insensitive arama

3. **Tree Filtering**
   - [ ] Filtrelenmiş sonuçlarda parent-child ilişkisini koru
   - [ ] Match olan item'ların parent'larını göster
   - [ ] Expand/collapse filtrelenmiş tree

4. **Keyboard Shortcut**
   - [ ] Ctrl+K / Cmd+K ile search input'a focus
   - [ ] Escape ile search'i temizle

**Tahmini Süre**: 2-3 saat

**Bağımlılıklar**: Faz 3 tamamlanmış olmalı

---

### Faz 9: Dinamik Chip/Badge ve Diğer İyileştirmeler ✅ PLANLAMA TAMAMLANDI

**Amaç**: Menu item chip'lerini dinamik hale getirme ve ek özellikler

**Adımlar:**

1. **Dinamik Chip/Badge Sistemi**
   - [ ] Chip source field'ı dataset schema'ya ekle (opsiyonel - gelecek için)
   - [ ] API/Query tabanlı chip değerleri
   - [ ] Polling mekanizması (opsiyonel)
   - [ ] Real-time güncelleme (SignalR - opsiyonel)

2. **Menu Item Duplication**
   - [ ] Side Menu Manager'da "Kopyala" özelliği
   - [ ] Duplicate fonksiyonu
   - [ ] Form'da kopyalanan item düzenlenir

3. **Menu Item Keyboard Navigation**
   - [ ] Arrow keys ile navigation
   - [ ] Enter ile açma
   - [ ] Escape ile kapatma

4. **Bulk Operations (Opsiyonel)**
   - [ ] Multi-select checkbox'ları
   - [ ] Bulk delete
   - [ ] Bulk parent change
   - [ ] Bulk enable/disable

**Tahmini Süre**: 6-8 saat

**Bağımlılıklar**: Faz 6 tamamlanmış olmalı

**Öncelik**: Düşük (temel özellikler çalıştıktan sonra)

---

## Implementasyon İçin Gerekli Bilgiler ve Eksikler

### Tamamlanmış Planlamalar ✅

1. ✅ Dataset yapısı (field definitions, index definitions)
2. ✅ Yetkilendirme sistemi mantığı
3. ✅ DOM element yetkilendirme yaklaşımı
4. ✅ Sayfa tipi bazlı yetkilendirme
5. ✅ Icon seçim sistemi
6. ✅ Side Menu Manager sayfa tasarımı
7. ✅ Sıralama ve nested header desteği
8. ✅ Cache ve performans stratejisi
9. ✅ Error handling ve fallback stratejisi
10. ✅ Real-time updates planı
11. ✅ Menu search/filter planı

### Karar Verilmesi Gereken Konular ⚠️

Aşağıdaki konular implementasyona başlamadan önce karara bağlanmalı:

1. **Cache Storage Yöntemi:** ✅ KARAR VERİLDİ
   - ✅ **localStorage + Memory Cache (Hybrid)** kullanılacak
   - ✅ TTL: 10 dakika (600000 ms) menu items için, 2 dakika permissions için
   - ✅ Cache key: `menuItems_{userId}_{domainId}` formatı

2. **Fallback Menu:** ✅ KARAR VERİLDİ
   - ✅ API hata durumunda hard-coded fallback menu kullanılacak
   - ✅ Fallback menu: `compat/sidebarItem.fallback.ts` dosyasında tutulacak
   - ✅ Mevcut `sidebarItem.ts` içeriği backup olarak kopyalanacak

3. **Icon Listesi:** ⚠️ KARAR VERİLMELİ
   - **Öneri MDI**: Statik liste (100-200 popüler icon) - İlk aşamada yeterli
   - **Öneri Tabler**: Dinamik (package'dan çıkar) veya statik (mevcut sidebar'dan)
   - **Karar Gerekli**: MDI icon listesi boyutu (100-200 vs tüm icon'lar)

4. **Real-Time Updates:** ✅ KARAR VERİLDİ
   - ⚠️ SignalR entegrasyonu henüz mevcut değil (package var ama kullanım yok)
   - ✅ Faz 7'de implement edilecek (eğer SignalR entegrasyonu eklendiyse)
   - ✅ Yoksa bu faz atlanacak, sonra eklenebilir
   - **Karar Gerekli**: SignalR entegrasyonu var mı? Yoksa ne zaman eklenecek?

5. **Menu Item Search:** ✅ KARAR VERİLDİ
   - ✅ Sidebar'da search özelliği Faz 8'de eklenecek (orta öncelik)
   - ✅ MVP'den sonra

6. **Migration Stratejisi:** ⚠️ KARAR VERİLMELİ
   - **Öneri**: Aşamalı geçiş (downtime olmadan)
     - Önce dataset oluştur ve veri yükle
     - Frontend'i güncelle (API'den çeksin)
     - Fallback menu ile test et
     - Hard-coded menu'yu backup olarak tut (compat klasöründe)
   - **Alternatif**: Tek seferde geçiş (kısa downtime ile)
   - **Karar Gerekli**: Hangisi tercih edilir?

7. **Bulk Update Stratejisi:** ⚠️ KARAR VERİLMELİ
   - **MngDataGateway**: Bulk update endpoint'i var mı?
   - **Yoksa**: Her item için ayrı update (drag & drop sonrası)
   - **Varsa**: Batch update kullan
   - **Karar Gerekli**: Bulk update endpoint'i mevcut mu?

8. **Icon Type Default:** ✅ KARAR VERİLDİ
   - ✅ Default: `'tabler'` (mevcut sidebar ile uyumluluk için)
   - ✅ Mevcut icon'lar otomatik olarak tabler kabul edilecek

9. **Permission Field Yapısı:** ✅ KARAR VERİLDİ
   - ✅ Object field type kullanılacak
   - ✅ MongoDB'de nested object olarak saklanacak
   - ✅ Groups bazlı permissions (user bazlı yok)

10. **Page Type Default:** ✅ KARAR VERİLDİ
    - ✅ Default: `'user'` (tüm mevcut sayfalar user tipinde)
    - ✅ Manager ve Admin sayfaları manuel olarak işaretlenecek

### Eksik Olan Teknik Detaylar 📋

Implementasyona başlamadan önce aşağıdaki bilgilere ihtiyaç var:

1. **MngHub SignalR Entegrasyonu:** ⚠️ KONTROL EDİLMELİ
   - [ ] Mevcut SignalR kullanımı var mı? (package var ama implementation görünmüyor)
   - [ ] Varsa: Event listening nasıl yapılıyor? (örnek kod gerekiyor)
   - [ ] Varsa: Connection management nasıl? (connect, disconnect, reconnect)
   - [ ] Yoksa: Faz 7 atlanacak, sonra eklenecek

2. **MngKeeper API Entegrasyonu:** ⚠️ KONTROL EDİLMELİ
   - [ ] Grup listesini çekmek için endpoint nedir? (Permission Editor için)
   - [ ] Muhtemelen: `GET /api/v1/groups` veya benzeri
   - [ ] Authentication: Mevcut auth store'dan token kullanılacak
   - **Çözüm**: Mevcut group store'u kontrol edilebilir (zaten varsa kullanılır)

3. **Error Response Formatları:** ⚠️ KONTROL EDİLMELİ
   - [ ] MngDataGateway API hata formatı nedir?
   - [ ] Error code'lar neler? (401, 403, 404, 500, vb.)
   - [ ] Error message formatı nedir?
   - **Not**: Test scriptlerinden kontrol edilebilir

4. **Bulk Update Endpoint:** ⚠️ KONTROL EDİLMELİ
   - [ ] MngDataGateway'de bulk update endpoint'i var mı?
   - [ ] Mevcut: `POST /api/v1/data/{dataset}/bulk` (create için var)
   - [ ] Bulk update endpoint'i var mı? (PUT için)
   - **Yoksa**: Her item için ayrı update yapılacak (performans için optimize edilebilir)

5. **Hard-Coded Menu Export:** ✅ PLANLANDI
   - [ ] `sidebarItem.ts` dosyasından export script'i oluşturulacak
   - [ ] Parent-child ilişkileri nasıl kurulacak? (order ve parentId mapping)
   - **Çözüm**: Migration script'te implement edilecek

6. **Icon Listesi Kaynakları:** ⚠️ KONTROL EDİLMELİ
   - [ ] MDI icon listesi nereden alınacak? (Material Design Icons sitesinden manuel mi?)
   - [ ] Tabler icon listesi package'dan nasıl çıkarılacak? (reflection/dynamic import)
   - **Öneri**: İlk aşamada statik listeler, sonra dinamik hale getirilebilir

### Kontrol Edilmesi Gereken Dosyalar ve Endpoint'ler

1. **MngKeeper API:**
   - [ ] Group list endpoint: `GET /api/v1/groups` (test et)
   - [ ] Mevcut group store varsa: `stores/apps/group.ts` kontrol et

2. **MngDataGateway API:**
   - [ ] Bulk update endpoint var mı? (test et veya dokümantasyona bak)
   - [ ] Error response formatı (test scriptlerinden kontrol et)

3. **MngHub SignalR:**
   - [ ] Hub connection örneği var mı?
   - [ ] Event listening örneği var mı?
   - [ ] `stores/hub.ts` veya benzeri dosya var mı?

4. **Mevcut Code:**
   - [ ] `stores/apps/group.ts` - Grup store kontrolü (Permission Editor için)
   - [ ] `services/apiService.ts` - API service kontrolü (authentication, error handling)
   - [ ] `utils/` - Utility fonksiyonları kontrolü (cache, error handling, vb.)

### İlk Implementasyon İçin Minimum Gereksinimler ✅

**Faz 1-3 (MVP) için yeterli olan bilgiler:**
1. ✅ MngDataGateway API endpoint'leri (zaten biliniyor)
2. ✅ Dataset schema yapısı (detaylı planlandı)
3. ✅ Authentication token formatı (mevcut auth store'dan biliniyor)
4. ✅ Menu item yapısı (detaylı planlandı)
5. ✅ Cache stratejisi (localStorage + memory hybrid - karar verildi)
6. ✅ Fallback menu stratejisi (compat klasöründe backup - planlandı)
7. ✅ Error handling yaklaşımı (planlandı)

**Karar verilmesi gerekenler:**
1. ⚠️ **Icon Listesi Boyutu**: MDI için 100-200 popüler icon mu, yoksa tüm icon'lar mı? (Öneri: 100-200)
2. ⚠️ **Migration Stratejisi**: Aşamalı geçiş mi (downtime olmadan), tek seferde mi? (Öneri: Aşamalı)
3. ⚠️ **Bulk Update**: MngDataGateway'de bulk update endpoint'i var mı? (Kontrol edilmeli)

**Kontrol edilmesi gerekenler:**
1. ⚠️ **MngKeeper Group API**: Grup listesi endpoint'i (Permission Editor için)
2. ⚠️ **SignalR Entegrasyonu**: Mevcut mu, yoksa sonra mı eklenecek? (Faz 7 için)
3. ⚠️ **Error Response Format**: MngDataGateway API hata formatı (test scriptlerinden kontrol edilebilir)

### Önerilen İlk Implementasyon Sırası 🚀

**Minimum Viable Product (MVP) - Faz 1-3:**
1. ✅ Faz 1: Dataset oluşturma ve veri yükleme
2. ✅ Faz 2: Store ve composable'lar
3. ✅ Faz 3: Sidebar entegrasyonu

**Sonraki Fazlar:**
4. Faz 4: Cache ve performans (hemen ardından)
5. Faz 5: DOM element yetkilendirme
6. Faz 6: Side Menu Manager sayfası
7. Faz 7: Real-time updates (SignalR entegrasyonu varsa)
8. Faz 8: Menu search
9. Faz 9: Diğer iyileştirmeler

### Karar Verilmesi Gereken Anlar 🔔

**Implementasyon başlamadan önce:**
- [ ] Cache storage yöntemi seçilmeli
- [ ] Fallback menu stratejisi belirlenmeli
- [ ] Icon listesi boyutu belirlenmeli

**Faz 1 sırasında:**
- [ ] Migration script stratejisi (tek seferde vs aşamalı)

**Faz 7 öncesi:**
- [ ] SignalR entegrasyonu var mı kontrol edilmeli
- [ ] Yoksa bu faz atlanabilir

---

## Test Senaryoları

### Unit Testler
- [ ] Menu item render testi
- [ ] Nested menu açılma/kapanma testi
- [ ] Navigation testi
- [ ] State yönetimi testi

### Integration Testler
- [ ] Sidebar toggle testi
- [ ] Mini sidebar modu testi
- [ ] User profile bilgisi gösterimi testi
- [ ] Logout işlevi testi

### E2E Testler
- [ ] Menüden sayfa navigasyonu
- [ ] Responsive davranış (mobil/tablet/desktop)

---

## Notlar ve Kararlar

### Tasarım Kararları
- ✅ **Dataset Kategori**: "System Datasets" kategorisi oluşturulacak
- ✅ **Dataset İsmi**: `@side_menu` (MongoDB collection name olarak kullanılacak)
- ✅ **Kategori Bağlantısı**: Dataset `category` field'ı ile System Datasets kategorisine bağlanacak
- [ ] Icon mapping stratejisi (string → vue-tabler-icons component)
- [ ] Cache stratejisi (TTL, invalidation)
- [ ] Error handling ve fallback mekanizması

### Teknik Kararlar
- ✅ **Dataset Koleksiyonu**: `@dataset_categories` içinde "System Datasets" kategorisi
- ✅ **Kategori Entity**: `DatasetCategory` (categoryName, categoryDescription)
- ✅ **Kategori API**: `/api/v1/dataset-categories`
- ✅ **Dataset Schema**: Category field'ı `__dataId` referansı içerecek
- [ ] Menu item sıralama algoritması (order field'ı ile)
- [ ] Parent-child ilişki yönetimi (parentId, level)
- [ ] Performance optimization (caching, lazy loading)

### Konuşulacak Konular (Henüz Karar Verilmedi)

> **Önemli**: Aşağıdaki konular daha detaylı konuşulacak, script yazımına geçmeden önce karara varılmalı.

1. **Veri Yapısı ve Migration**
   - [ ] Mevcut hard-coded menü verilerini nasıl export edeceğiz?
   - [ ] Parent-child ilişkilerini nasıl kurmalıyız? (parentId, level)
   - [ ] Icon'ları nasıl map edeceğiz? (string → component)
   - [ ] Order ve level değerlerini nasıl hesaplayacağız?

2. **API Tasarımı**
   - [ ] Menu items'ı nasıl query edeceğiz? (filter, sort, expand)
   - [ ] Tree yapısını nasıl döndüreceğiz? (flat array mi, nested object mi?)
   - [ ] Performance için nasıl optimize edeceğiz? (caching, pagination)

3. **Frontend Entegrasyonu**
   - [ ] API'den gelen veriyi nasıl transform edeceğiz? (mevcut interface'e uygun)
   - [ ] Cache mekanizması nasıl olacak? (TTL, invalidation stratejisi)
   - [ ] Error handling nasıl olacak? (fallback, retry, offline mode)
   - [ ] Backward compatibility nasıl sağlanacak? (geçiş dönemi)

4. **Yetkilendirme ve Güvenlik** ✅ TAMAMLANDI (Planlandı)
   - ✅ Menu item'ları kullanıcıya göre filtreleme (view permission)
   - ✅ Permission bazlı menü gösterimi (group-based permissions)
   - ✅ Sayfa erişim kontrolü (route guard/middleware)
   - ✅ Admin bypass mekanizması (isAdmin: true)
   - ✅ Token'dan grup bilgileri alma (user_groups array)
   - ✅ DOM element yetkilendirme (butonlar, action sütunları)
   - ✅ Route-to-menu-item mapping stratejisi
   - ✅ Vue composable ve directive yaklaşımları
   - ✅ Backend API kullanımı (MngDataGateway endpoint'leri yeterli, değişiklik gerekmez)
   - [ ] Permission cache invalidation stratejisi
   - [ ] Yetkisiz erişim sayfası (`/unauthorized`) oluşturulması
   - [ ] `usePagePermissions` composable implementasyonu
   - [ ] `v-permission` directive implementasyonu (opsiyonel)
   - [ ] `PermissionWrapper` component implementasyonu (opsiyonel)

5. **Maintenance ve Yönetim**
   - [ ] Menu item'ları nasıl yöneteceğiz? (UI'dan mı, API'den mi?)
   - [ ] Değişiklikler nasıl propagate olacak? (cache invalidation)
   - [ ] Versioning gerekli mi? (menu versiyonları)

### Gelecek İyileştirmeler
- [ ] Multi-language support (menu item'lar için i18n)
- [ ] Menu item permissions (kullanıcı/grup bazlı görünürlük) ✅ Planlandı
- [ ] Menu analytics (hangi item'lar daha çok kullanılıyor)
- [ ] Dynamic menu generation (role-based) ✅ Planlandı (permission-based)
- [ ] Menu item badges (real-time notification counts)

---

## Ek Öneriler ve Önemli Konular

Aşağıdaki konular planlamaya dahil edilebilir. İhtiyaç durumuna göre önceliklendirilebilir:

### 1. Cache ve Performans Yönetimi ⚠️ ÖNEMLİ

**Cache Stratejisi:**

**Storage Yöntemi Seçimi:**
- **Önerilen: localStorage + Memory Cache (Hybrid Yaklaşım)**
  - **localStorage**: Persistent cache (sayfa yenilendiğinde korunur)
    - Key: `menuItems_{userId}_{domainId}`
    - Value: JSON stringified menu items array
    - TTL metadata: `menuItems_{userId}_{domainId}_ttl`
  - **Memory Cache (Pinia Store)**: Runtime cache (hızlı erişim)
    - Store içinde `menuItems` state
    - Computed properties ile optimize edilmiş filtreleme

**TTL (Time To Live) Stratejisi:**
- **Menu Items Cache**: 10 dakika (600000 ms)
- **Permission Cache**: 2 dakika (120000 ms) - daha sık güncellenebilir
- **TTL Storage**: localStorage'da timestamp olarak saklanır

**Cache Invalidation Mekanizması:**
1. **TTL Expiry**: 
   - Cache okunurken TTL kontrol edilir
   - Expire olmuşsa yeniden API'den çekilir
   
2. **Manual Refresh**:
   - Toolbar'dan "Yenile" butonu
   - Store action: `refreshMenuItems()`

3. **Real-Time Invalidation** (SignalR):
   - Menu item değişiklik event'i geldiğinde
   - Cache temizlenir ve yeniden yüklenir

4. **Permission Change**:
   - Token'daki grup bilgileri değiştiğinde
   - Cache temizlenir ve yeniden yüklenir

**Cache Key Strategy:**
```typescript
// utils/cache/menu-cache.ts
export function getMenuCacheKey(userId: string, domainId: string): string {
  return `menuItems_${userId}_${domainId}`;
}

export function getMenuCacheTTLKey(userId: string, domainId: string): string {
  return `menuItems_${userId}_${domainId}_ttl`;
}
```

**Cache Implementation:**

```typescript
// utils/cache/menu-cache.ts
interface CacheData {
  data: SideMenuItem[];
  timestamp: number;
  ttl: number; // milliseconds
}

export class MenuCache {
  /**
   * Cache'e menu items kaydet
   */
  static set(userId: string, domainId: string, items: SideMenuItem[], ttl: number = 600000): void {
    const cacheKey = getMenuCacheKey(userId, domainId);
    const ttlKey = getMenuCacheTTLKey(userId, domainId);
    
    const cacheData: CacheData = {
      data: items,
      timestamp: Date.now(),
      ttl: ttl
    };
    
    try {
      localStorage.setItem(cacheKey, JSON.stringify(cacheData.data));
      localStorage.setItem(ttlKey, JSON.stringify({ timestamp: cacheData.timestamp, ttl: cacheData.ttl }));
    } catch (error) {
      console.warn('Menu cache save failed:', error);
      // Storage quota exceeded - clear old cache
      this.clearOldCaches();
    }
  }
  
  /**
   * Cache'den menu items oku
   */
  static get(userId: string, domainId: string): SideMenuItem[] | null {
    const cacheKey = getMenuCacheKey(userId, domainId);
    const ttlKey = getMenuCacheTTLKey(userId, domainId);
    
    const cachedData = localStorage.getItem(cacheKey);
    const ttlData = localStorage.getItem(ttlKey);
    
    if (!cachedData || !ttlData) {
      return null; // Cache yok
    }
    
    try {
      const { timestamp, ttl } = JSON.parse(ttlData);
      const now = Date.now();
      
      // TTL kontrolü
      if (now - timestamp > ttl) {
        // Cache expire olmuş, temizle
        this.clear(userId, domainId);
        return null;
      }
      
      // Cache geçerli
      return JSON.parse(cachedData) as SideMenuItem[];
    } catch (error) {
      console.warn('Menu cache read failed:', error);
      this.clear(userId, domainId);
      return null;
    }
  }
  
  /**
   * Cache'i temizle
   */
  static clear(userId: string, domainId: string): void {
    const cacheKey = getMenuCacheKey(userId, domainId);
    const ttlKey = getMenuCacheTTLKey(userId, domainId);
    
    localStorage.removeItem(cacheKey);
    localStorage.removeItem(ttlKey);
  }
  
  /**
   * Tüm menu cache'lerini temizle (storage quota için)
   */
  static clearAll(): void {
    Object.keys(localStorage).forEach(key => {
      if (key.startsWith('menuItems_')) {
        localStorage.removeItem(key);
      }
    });
  }
  
  /**
   * Eski cache'leri temizle (storage quota exceeded durumunda)
   */
  static clearOldCaches(): void {
    const now = Date.now();
    const keysToRemove: string[] = [];
    
    Object.keys(localStorage).forEach(key => {
      if (key.endsWith('_ttl')) {
        try {
          const ttlData = JSON.parse(localStorage.getItem(key) || '{}');
          // 24 saatten eski cache'leri sil
          if (now - ttlData.timestamp > 24 * 60 * 60 * 1000) {
            const cacheKey = key.replace('_ttl', '');
            keysToRemove.push(key, cacheKey);
          }
        } catch {
          // Invalid TTL data, sil
          keysToRemove.push(key);
        }
      }
    });
    
    keysToRemove.forEach(key => localStorage.removeItem(key));
  }
}
```

**Store'da Cache Kullanımı:**

```typescript
// stores/apps/sideMenu.ts
import { MenuCache } from '@/utils/cache/menu-cache';

export const useSideMenuStore = defineStore('sideMenu', {
  state: () => ({
    menuItems: [] as SideMenuItem[],
    menuItemsTree: [] as SideMenuItem[],
    loading: false,
    error: null as string | null,
    lastUpdated: null as number | null,
  }),
  
  actions: {
    async loadMenuItems(forceRefresh: boolean = false) {
      const authStore = useAuthStore();
      
      if (!authStore.userInfo || !authStore.domainName) {
        this.error = 'User info veya domain bilgisi yok';
        return;
      }
      
      // Cache'den oku (forceRefresh değilse)
      if (!forceRefresh) {
        const cached = MenuCache.get(
          authStore.userInfo.sub,
          authStore.domainName || ''
        );
        
        if (cached) {
          this.menuItems = cached;
          this.menuItemsTree = this.buildMenuTree(cached);
          this.lastUpdated = Date.now();
          return;
        }
      }
      
      // API'den çek
      this.loading = true;
      this.error = null;
      
      try {
        const response = await $fetch('/api/v1/data/@side_menu', {
          method: 'GET',
          params: {
            page: 1,
            pageSize: 1000,
            sort: 'order:asc,level:asc',
          },
          headers: {
            Authorization: `Bearer ${authStore.accessToken}`
          }
        });
        
        this.menuItems = response.data;
        this.menuItemsTree = this.buildMenuTree(response.data);
        this.lastUpdated = Date.now();
        
        // Cache'e kaydet
        MenuCache.set(
          authStore.userInfo.sub,
          authStore.domainName || '',
          response.data,
          600000 // 10 dakika
        );
      } catch (error) {
        console.error('Menu items yüklenemedi:', error);
        this.error = 'Menu items yüklenirken hata oluştu';
        
        // Fallback: Hard-coded menu
        this.loadFallbackMenu();
      } finally {
        this.loading = false;
      }
    },
    
    refreshMenuItems() {
      // Cache'i temizle ve yeniden yükle
      const authStore = useAuthStore();
      if (authStore.userInfo && authStore.domainName) {
        MenuCache.clear(authStore.userInfo.sub, authStore.domainName || '');
      }
      return this.loadMenuItems(true);
    }
  }
});
```

**Performance Optimizasyonu:**
- ✅ **Virtual Scrolling**: 100+ menu item varsa düşünülebilir (şimdilik gerekli değil)
- ✅ **Lazy Loading**: Tree view için sadece görünen item'ları render et
- ✅ **Debounce Search**: 300ms debounce (menu search için)
- ✅ **Memoization**: Computed properties kullan (Pinia getters)
- ✅ **ShallowRef**: Menu items için `shallowRef` kullan (zaten mevcut)
- ✅ **Tree Caching**: Build edilmiş tree yapısını cache'le (performans için)

### 2. Error Handling ve Fallback Mekanizması ⚠️ ÖNEMLİ

**Fallback Stratejisi:**

1. **API Hatası Durumunda**:
   ```typescript
   async loadMenuItems() {
     try {
       // 1. Önce API'den çek
       const response = await fetchMenuItemsFromAPI();
       return response;
     } catch (error) {
       // 2. Cache'den yükle (varsa)
       const cached = MenuCache.get(userId, domainId);
       if (cached) {
         console.warn('API hatası, cache\'den yüklendi');
         return cached;
       }
       
       // 3. Hard-coded fallback menu kullan
       console.warn('API ve cache hatası, fallback menu kullanılıyor');
       return this.loadFallbackMenu();
     }
   }
   ```

2. **Retry Mekanizması** (Opsiyonel - İleri Seviye):
   ```typescript
   async loadMenuItemsWithRetry(maxRetries: number = 3) {
     let attempt = 0;
     let lastError: Error | null = null;
     
     while (attempt < maxRetries) {
       try {
         return await fetchMenuItemsFromAPI();
       } catch (error) {
         lastError = error as Error;
         attempt++;
         
         if (attempt >= maxRetries) break;
         
         // Exponential backoff
         const delay = Math.pow(2, attempt) * 1000; // 1s, 2s, 4s
         await new Promise(resolve => setTimeout(resolve, delay));
       }
     }
     
     // Tüm retry'lar başarısız, fallback'e geç
     throw lastError;
   }
   ```

3. **Offline Mode** (Opsiyonel - İleri Seviye):
   - Service Worker ile offline cache
   - Offline indicator (UI'da göster)
   - Cache'den menu gösterme
   - Online olduğunda otomatik refresh

**Error Scenarios ve Çözümleri:**

| Error Type | HTTP Status | Çözüm |
|------------|-------------|-------|
| Network Error | - | Cache'den yükle → Fallback menu |
| 401 Unauthorized | 401 | Token refresh dene → Fail ise login'e yönlendir |
| 403 Forbidden | 403 | Cache'den yükle → Fallback menu |
| 404 Not Found | 404 | Fallback menu (dataset henüz oluşturulmamış olabilir) |
| 500 Server Error | 500 | Cache'den yükle → Fallback menu → Retry (opsiyonel) |
| Timeout | - | Cache'den yükle → Fallback menu |

**Error Notification:**

```typescript
// stores/apps/sideMenu.ts
async loadMenuItems() {
  try {
    // API call
  } catch (error: any) {
    // Error notification
    const notificationStore = useNotificationStore();
    
    if (error.status === 401) {
      notificationStore.showError('Oturum süresi dolmuş, lütfen tekrar giriş yapın');
      await authStore.logout();
      navigateTo('/auth/login');
      return;
    }
    
    if (error.status === 403) {
      notificationStore.showWarning('Menü bilgilerine erişim yetkiniz yok');
      this.loadFallbackMenu();
      return;
    }
    
    // Diğer hatalar
    notificationStore.showWarning('Menü bilgileri yüklenirken hata oluştu, önbellekten yükleniyor');
    this.loadFallbackMenu();
  }
}
```

**Fallback Menu Stratejisi:**

**Yaklaşım 1: Compat Dosyası (Önerilen)**
- `compat/sidebarItem.fallback.ts` dosyası oluştur
- Mevcut `sidebarItem.ts` içeriğini buraya kopyala
- Import path: `@/compat/sidebarItem.fallback`

**Yaklaşım 2: Store İçinde Hard-Coded**
- Store'da fallback menu array'i tanımla
- Minimal menu (sadece kritik item'lar)

**Önerilen: Yaklaşım 1** - Mevcut menu'yu backup olarak tut, geçiş döneminde kullan

```typescript
// compat/sidebarItem.fallback.ts
// Mevcut sidebarItem.ts içeriğinin kopyası
// Bu dosya geçiş döneminde ve hata durumlarında kullanılacak

import sidebarItems from '@/components/lc/Full/vertical-sidebar/sidebarItem';

export default sidebarItems;
```

```typescript
// stores/apps/sideMenu.ts
import fallbackMenuItems from '@/compat/sidebarItem.fallback';

loadFallbackMenu() {
  console.warn('Fallback menu kullanılıyor');
  this.menuItems = fallbackMenuItems;
  this.menuItemsTree = this.buildMenuTree(fallbackMenuItems);
  this.lastUpdated = Date.now();
  this.error = 'API\'den menu yüklenemedi, fallback menu kullanılıyor';
}
```

### 3. Real-Time Updates (SignalR/WebSocket) ✅ PLANLAMA TAMAMLANDI

**Amaç**: Menu item değişikliklerini anlık olarak tüm kullanıcılara yaymak

**Kullanım Senaryosu:**
- Admin bir menu item'ı değiştirdiğinde
- Tüm kullanıcıların menüsü otomatik güncellenir (cache invalidation)
- Permission değişiklikleri anında yansır
- UI'da smooth update (animation)

**MngHub Entegrasyonu:**
- **Mevcut Durum**: `@microsoft/signalr` package mevcut ama kullanım görünmüyor
- **MngHub**: Dataset event'leri yayınlıyor (`DataCreatedEvent`, `DataUpdatedEvent`, vb.)
- **Routing Key Format**: `{domainId}.datacreatedevent` (örnek: `meral.datacreatedevent`)

**Implementation Plan:**

**Adım 1: Hub Store/Service Kontrolü**
- [ ] Mevcut hub store var mı kontrol et
- [ ] Yoksa oluştur: `stores/hub.ts` veya `services/hubService.ts`
- [ ] SignalR connection setup
- [ ] Connection management (connect, disconnect, reconnect)

**Adım 2: Event Listener Registration**
```typescript
// stores/apps/sideMenu.ts veya stores/hub.ts
import { HubConnection, HubConnectionBuilder } from '@microsoft/signalr';

const connection = new HubConnectionBuilder()
  .withUrl(`${hubUrl}/ws`)
  .withAutomaticReconnect()
  .build();

// Dataset event'lerini dinle
connection.on('DataCreatedEvent', (event) => {
  if (event.dataset === '@side_menu') {
    // Menu item eklendi
    sideMenuStore.refreshMenuItems();
  }
});

connection.on('DataUpdatedEvent', (event) => {
  if (event.dataset === '@side_menu') {
    // Menu item güncellendi
    sideMenuStore.refreshMenuItems();
  }
});

connection.on('DataDeletedEvent', (event) => {
  if (event.dataset === '@side_menu') {
    // Menu item silindi
    sideMenuStore.refreshMenuItems();
  }
});
```

**Adım 3: Selective Update (Optimizasyon - İleri Seviye)**
- [ ] Event'ten gelen dataId ile sadece ilgili item'ı güncelle
- [ ] Tree'yi yeniden build et
- [ ] Permission kontrolü yap (değişen item kullanıcının görebileceği mi?)

**Not**: Eğer SignalR entegrasyonu henüz yoksa, bu özellik sonra eklenebilir. İlk fazlarda manuel refresh yeterli olacaktır.

### 4. Menu Item Chip/Badge Dinamik Güncelleme ✅ PLANLAMA TAMAMLANDI

**Amaç**: Menu item'lardaki chip/badge'lerin dinamik olarak güncellenmesi

**Kullanım Senaryoları:**
- Bildirim sayısı (örn: "Event Mesajları" için 5 yeni bildirim)
- Pending işlem sayısı
- Real-time data (örneğin anlık veri sayısı)
- Dataset count (örn: "Kullanıcılar" için toplam kullanıcı sayısı)

**Yaklaşım - İlk Aşama: Statik Chip**
- İlk implementasyonda `chip` field'ı statik olarak kullanılacak
- Admin/Side Menu Manager'dan manuel olarak chip değeri girilecek
- Bu yeterli olacaktır (MVP için)

**Yaklaşım - İleri Aşama: Dinamik Chip (Gelecek)**

Dataset schema'ya `chipSource` field'ı eklenebilir (opsiyonel):

```typescript
interface ChipSource {
  type: 'static' | 'api' | 'dataset' | 'query';
  value?: string; // Statik değer (type: 'static')
  endpoint?: string; // API endpoint (type: 'api')
  dataset?: string; // Dataset adı (type: 'dataset' veya 'query')
  query?: string; // Query name (type: 'query')
  field?: string; // Sonuçtan hangi field alınacak (count, vb.)
  refreshInterval?: number; // Polling interval (ms) - opsiyonel
  format?: string; // Format string (örn: "{count} yeni")
}
```

**Implementation (İleri Seviye - Şimdilik Planlama):**

```typescript
// composables/useDynamicChip.ts
export function useDynamicChip(menuItem: SideMenuItem) {
  const chipValue = ref<string | null>(menuItem.chip || null);
  
  if (!menuItem.chipSource) {
    return { chipValue };
  }
  
  const { type, endpoint, dataset, query, refreshInterval } = menuItem.chipSource;
  
  async function updateChip() {
    try {
      let value: any;
      
      switch (type) {
        case 'api':
          value = await $fetch(endpoint!);
          break;
        case 'dataset':
          value = await $fetch(`/api/v1/data/${dataset}`);
          value = value.totalCount || value.data?.length || 0;
          break;
        case 'query':
          value = await $fetch(`/api/v1/data/${dataset}/query/${query}`);
          value = value.count || value.length || 0;
          break;
        default:
          value = menuItem.chipSource?.value || null;
      }
      
      chipValue.value = formatChipValue(value, menuItem.chipSource?.format);
    } catch (error) {
      console.warn('Chip update failed:', error);
      chipValue.value = menuItem.chip || null; // Fallback to static
    }
  }
  
  // İlk yükleme
  onMounted(() => {
    if (type !== 'static') {
      updateChip();
      
      // Polling (eğer refreshInterval varsa)
      if (refreshInterval && refreshInterval > 0) {
        const interval = setInterval(updateChip, refreshInterval);
        onUnmounted(() => clearInterval(interval));
      }
    }
  });
  
  return { chipValue };
}
```

**Not**: Bu özellik şimdilik planlama aşamasında kalacak, MVP'den sonra implement edilebilir.

### 5. Menu Item Search/Filter (Frontend) ✅ PLANLAMA TAMAMLANDI

**Amaç**: Sidebar'da menü içinde arama yapabilme

**Özellikler:**
- Search input (sidebar üstünde, profile bölümünün altında)
- Real-time filtreleme (debounced)
- Title, route path, subCaption'a göre arama
- Case-insensitive arama
- Filtrelenmiş sonuçlarda expand/collapse
- Parent-child ilişkisini koru (match olan item'ın parent'larını göster)
- Keyboard shortcut (Ctrl+K / Cmd+K) - Opsiyonel
- Clear button (X butonu)

**Implementation:**

```vue
<!-- components/lc/Full/vertical-sidebar/index.vue -->
<script setup>
import { ref, computed } from 'vue';
import { useSideMenuStore } from '@/stores/apps/sideMenu';
import { debounce } from 'lodash-es';

const menuStore = useSideMenuStore();
const searchQuery = ref('');

// Debounced search (300ms)
const debouncedSearch = debounce((query: string) => {
  searchQuery.value = query;
}, 300);

// Filtrelenmiş menu items
const filteredMenuItems = computed(() => {
  if (!searchQuery.value || searchQuery.value.trim() === '') {
    return menuStore.visibleMenuItems;
  }
  
  const query = searchQuery.value.toLowerCase().trim();
  
  function matches(item: SideMenuItem): boolean {
    // Title'a göre
    if (item.title && item.title.toLowerCase().includes(query)) {
      return true;
    }
    
    // Route path'e göre
    if (item.to && item.to.toLowerCase().includes(query)) {
      return true;
    }
    
    // Sub caption'a göre
    if (item.subCaption && item.subCaption.toLowerCase().includes(query)) {
      return true;
    }
    
    // Header'a göre
    if (item.header && item.header.toLowerCase().includes(query)) {
      return true;
    }
    
    return false;
  }
  
  // Tree'de filtreleme (parent-child ilişkisini koru)
  function filterTree(items: SideMenuItem[]): SideMenuItem[] {
    const result: SideMenuItem[] = [];
    
    items.forEach(item => {
      const itemMatches = matches(item);
      const filteredChildren = item.children ? filterTree(item.children) : [];
      
      // Item match ediyorsa veya children'larından biri match ediyorsa ekle
      if (itemMatches || filteredChildren.length > 0) {
        result.push({
          ...item,
          children: filteredChildren.length > 0 ? filteredChildren : item.children
        });
      }
    });
    
    return result;
  }
  
  return filterTree(menuStore.visibleMenuItems);
});

// Keyboard shortcut
onMounted(() => {
  const handleKeyDown = (e: KeyboardEvent) => {
    // Ctrl+K veya Cmd+K
    if ((e.ctrlKey || e.metaKey) && e.key === 'k') {
      e.preventDefault();
      // Search input'a focus (input ref'i gerekli)
      searchInputRef.value?.focus();
    }
    
    // Escape
    if (e.key === 'Escape' && searchQuery.value) {
      searchQuery.value = '';
    }
  };
  
  window.addEventListener('keydown', handleKeyDown);
  onUnmounted(() => {
    window.removeEventListener('keydown', handleKeyDown);
  });
});

const searchInputRef = ref<HTMLInputElement | null>(null);
</script>

<template>
  <v-navigation-drawer>
    <!-- Profile Section (mevcut) -->
    <div class="profile">
      <!-- ... mevcut profile kodu ... -->
    </div>
    
    <!-- Search Input -->
    <v-text-field
      ref="searchInputRef"
      v-model="searchQuery"
      prepend-inner-icon="mdi-magnify"
      placeholder="Menüde ara..."
      variant="outlined"
      density="compact"
      class="ma-3"
      clearable
      hide-details
    />
    
    <!-- Filtered Menu Items -->
    <perfect-scrollbar class="scrollnavbar">
      <v-list class="py-5 px-4 bg-muted" density="compact">
        <template v-for="(item, i) in filteredMenuItems" :key="item.__dataId || i">
          <LcFullVerticalSidebarNavGroup :item="item" v-if="item.header" />
          <LcFullVerticalSidebarNavCollapse 
            class="leftPadding" 
            :item="item" 
            :level="0" 
            v-else-if="item.children" 
          />
          <LcFullVerticalSidebarNavItem 
            :item="item" 
            v-else 
            class="leftPadding" 
          />
        </template>
        
        <!-- No Results -->
        <v-list-item v-if="filteredMenuItems.length === 0 && searchQuery">
          <v-list-item-title class="text-center text-caption text-medium-emphasis">
            Sonuç bulunamadı
          </v-list-item-title>
        </v-list-item>
      </v-list>
    </perfect-scrollbar>
  </v-navigation-drawer>
</template>
```

### 6. Menu Item Duplication (Kopyalama) ✅ PLANLAMA TAMAMLANDI

**Amaç**: Mevcut bir menu item'ı kopyalayarak yeni item oluşturma

**Kullanım Senaryosu:**
- Benzer menu item'lar için hızlı oluşturma
- Template olarak kullanma
- Toplu menu item oluşturma (örneğin: CRUD item'ları)

**Side Menu Manager'da:**

**Yöntem 1: Sağ Tık Menü**
- Tree'de item'a sağ tık → "Kopyala"
- Context menu açılır

**Yöntem 2: Detail View'dan**
- Detail view'da "Kopyala" butonu
- Kopyalanan item form'da açılır (create modunda)

**Yöntem 3: Toolbar'dan**
- Item seçili iken toolbar'da "Kopyala" butonu

**Implementation:**

```typescript
// stores/apps/sideMenuManager.ts
async function duplicateMenuItem(itemId: string) {
  const sourceItem = this.allMenuItems.find(item => item.__dataId === itemId);
  if (!sourceItem) return;
  
  // Yeni item oluştur (parent, level, order yeni olacak)
  const duplicatedItem: Partial<SideMenuItem> = {
    ...sourceItem,
    __dataId: undefined, // Yeni item
    title: `${sourceItem.title} (Kopya)`, // Başlığa "(Kopya)" ekle
    order: calculateNewOrderForParent(sourceItem.parentId),
    level: sourceItem.level, // Aynı seviyede
    parentId: sourceItem.parentId, // Aynı parent'ta
    // Children'lar kopyalanmaz (opsiyonel - eğer kopyalanacaksa recursive olarak)
    children: undefined,
  };
  
  // Form'da aç (create modunda)
  this.selectedItem = null;
  this.formMode = 'create';
  this.formData = duplicatedItem;
  
  // Veya direkt kaydet (opsiyonel)
  // return await this.createMenuItem(duplicatedItem);
}
```

**Nested Item Kopyalama:**
- Eğer item'ın children'ları varsa, onlar da kopyalanabilir
- Recursive duplication (opsiyonel)
- Kullanıcıya sor: "Alt menü öğelerini de kopyalamak istiyor musunuz?"

### 7. Menu Item Visibility Toggle ✅ PLANLAMA TAMAMLANDI

**Amaç**: Menu item'ı geçici olarak gizleme (silmeden)

**Mevcut Field:**
- `disabled` field'ı zaten var - Bu item'ı devre dışı bırakır (görünür ama tıklanamaz, gri renkte)

**Kullanım Senaryoları:**
1. **Geçici olarak devre dışı bırakma**: `disabled: true`
   - Item menüde görünür ama tıklanamaz (gri, disabled görünümü)
   - Kullanım: Bakım modu, geçici erişim kapatma
   
2. **Tamamen gizleme (İleri Seviye)**: `hidden: true` field'ı eklenebilir
   - Item menüde görünmez (tamamen gizli)
   - Kullanım: A/B testing, feature flag, geçici gizleme
   - **Not**: Şimdilik gerekli değil, `disabled` field'ı yeterli olacaktır

**Sidebar'da Disabled Item Gösterimi:**

```vue
<!-- components/lc/Full/vertical-sidebar/NavItem/index.vue -->
<v-list-item
  :to="item.type === 'external' ? '' : item.to"
  :disabled="item.disabled"
  :class="{ 'menu-item-disabled': item.disabled }"
  ...
>
  <!-- Item content -->
</v-list-item>
```

**Side Menu Manager'da:**
- Form'da "Devre Dışı" checkbox'ı mevcut
- Checkbox işaretlenirse `disabled: true` olarak kaydedilir

**Not**: Şimdilik `disabled` field'ı yeterli. `hidden` field'ı gelecekte eklenebilir.

### 8. Menu Item Keyboard Navigation ✅ PLANLAMA TAMAMLANDI

**Amaç**: Klavye ile menüde gezinebilme

**Keyboard Shortcuts:**

**Sidebar Navigation:**
- `Arrow Up/Down`: Menu item'lar arasında gezinme
- `Arrow Left/Right`: Expand/collapse (nested items için)
- `Enter` / `Space`: Menu item'ı aç (navigate)
- `Esc`: Search'i temizle / Menu'den çık

**Global Shortcuts:**
- `Ctrl+K` / `Cmd+K`: Search input'a focus (menu search için)
- `Ctrl+B` / `Cmd+B`: Sidebar toggle (mevcut özellik)

**Implementation:**

```vue
<!-- components/lc/Full/vertical-sidebar/index.vue -->
<script setup>
const menuStore = useSideMenuStore();
const selectedItemIndex = ref(-1);
const focusedItemId = ref<string | null>(null);

function handleKeyDown(e: KeyboardEvent) {
  if (!customizer.Sidebar_drawer) return; // Sidebar kapalıysa ignore
  
  const visibleItems = menuStore.visibleMenuItems;
  
  switch (e.key) {
    case 'ArrowDown':
      e.preventDefault();
      selectedItemIndex.value = Math.min(
        selectedItemIndex.value + 1,
        visibleItems.length - 1
      );
      focusedItemId.value = visibleItems[selectedItemIndex.value]?.__dataId || null;
      scrollToItem(focusedItemId.value);
      break;
      
    case 'ArrowUp':
      e.preventDefault();
      selectedItemIndex.value = Math.max(selectedItemIndex.value - 1, 0);
      focusedItemId.value = visibleItems[selectedItemIndex.value]?.__dataId || null;
      scrollToItem(focusedItemId.value);
      break;
      
    case 'ArrowRight':
      e.preventDefault();
      // Expand focused item (if has children)
      const focusedItem = visibleItems.find(item => item.__dataId === focusedItemId.value);
      if (focusedItem?.children) {
        expandItem(focusedItem.__dataId);
      }
      break;
      
    case 'ArrowLeft':
      e.preventDefault();
      // Collapse focused item (if has children)
      const focusedItem = visibleItems.find(item => item.__dataId === focusedItemId.value);
      if (focusedItem?.children) {
        collapseItem(focusedItem.__dataId);
      }
      break;
      
    case 'Enter':
    case ' ':
      e.preventDefault();
      // Navigate to focused item
      const item = visibleItems.find(i => i.__dataId === focusedItemId.value);
      if (item && item.to) {
        navigateTo(item.to);
      }
      break;
  }
}

onMounted(() => {
  window.addEventListener('keydown', handleKeyDown);
});

onUnmounted(() => {
  window.removeEventListener('keydown', handleKeyDown);
});
</script>
```

**Not**: Bu özellik accessibility için önemli ama MVP için zorunlu değil. İkinci fazda eklenebilir.

### 9. Menu Item Favorites/Bookmarks ✅ PLANLAMA TAMAMLANDI

**Amaç**: Kullanıcı bazlı favori menü item'ları

**Yaklaşım:**

**İlk Aşama: localStorage (MVP)**
- Kullanıcı bazlı favori listesi (localStorage'da)
- Key: `menuFavorites_{userId}`
- Value: `string[]` (favorite item __dataId'leri)

**İleri Aşama: Backend (Gelecek)**
- Favorileri veritabanında sakla (kullanıcı bazlı dataset)
- Sync across devices
- Cloud backup

**UI Yerleşimi:**
- **Seçenek 1**: Sidebar'ın en üstünde "Favorites" section (profile'ın hemen altında)
- **Seçenek 2**: Normal menüde, favoriler yıldız işareti ile gösterilir
- **Seçenek 3**: Her iki yaklaşım da kullanılabilir

**Implementation (MVP - localStorage):**

```typescript
// stores/apps/sideMenu.ts
const favoriteMenuIds = ref<string[]>([]);

function loadFavorites() {
  const authStore = useAuthStore();
  if (!authStore.userInfo) return;
  
  const key = `menuFavorites_${authStore.userInfo.sub}`;
  const stored = localStorage.getItem(key);
  
  if (stored) {
    try {
      favoriteMenuIds.value = JSON.parse(stored);
    } catch {
      favoriteMenuIds.value = [];
    }
  }
}

function toggleFavorite(itemId: string) {
  const index = favoriteMenuIds.value.indexOf(itemId);
  
  if (index > -1) {
    favoriteMenuIds.value.splice(index, 1);
  } else {
    favoriteMenuIds.value.push(itemId);
  }
  
  // Save to localStorage
  const authStore = useAuthStore();
  if (authStore.userInfo) {
    const key = `menuFavorites_${authStore.userInfo.sub}`;
    localStorage.setItem(key, JSON.stringify(favoriteMenuIds.value));
  }
}

const favoriteMenuItems = computed(() => {
  return menuItems.value.filter(item => 
    favoriteMenuIds.value.includes(item.__dataId)
  );
});
```

**Sidebar'da Favorites Section:**

```vue
<!-- components/lc/Full/vertical-sidebar/index.vue -->
<template>
  <v-navigation-drawer>
    <!-- Profile Section -->
    <div class="profile">...</div>
    
    <!-- Search Input -->
    <v-text-field ... />
    
    <!-- Favorites Section (sadece favori varsa göster) -->
    <template v-if="favoriteMenuItems.length > 0">
      <v-divider class="my-2" />
      <v-list-subheader class="text-uppercase">Favoriler</v-list-subheader>
      <v-list density="compact">
        <template v-for="item in favoriteMenuItems" :key="item.__dataId">
          <LcFullVerticalSidebarNavItem :item="item" />
        </template>
      </v-list>
      <v-divider class="my-2" />
    </template>
    
    <!-- Normal Menu Items -->
    <perfect-scrollbar>
      <!-- ... normal menu ... -->
    </perfect-scrollbar>
  </v-navigation-drawer>
</template>
```

**Not**: Bu özellik MVP için zorunlu değil, kullanıcı talebi olursa eklenebilir.

### 10. Menu Item Templates ✅ PLANLAMA TAMAMLANDI

**Amaç**: Yaygın menu yapılarını template olarak kaydetme

**Kullanım Senaryosu:**
- Side Menu Manager'da template oluşturma
- Template'den menu item oluşturma
- Örnek: "CRUD Menu Template" (List, Create, Edit, Detail item'ları içeren)
- Örnek: "Dashboard Template" (Analytical, Classic, Modern item'ları içeren)

**Yaklaşım:**

**İlk Aşama: Hard-Coded Templates (MVP)**
- Template'ler kodda tanımlı (constants olarak)
- Side Menu Manager'da "Template'den Oluştur" dropdown
- Template seçildiğinde form doldurulur

**İleri Aşama: Template Dataset (Gelecek)**
- Template'ler veritabanında saklanır (`@menu_templates` dataset)
- Admin template oluşturabilir
- Template'ler paylaşılabilir

**Implementation (MVP - Hard-Coded):**

```typescript
// utils/menu-templates.ts
export interface MenuTemplate {
  name: string;
  description: string;
  items: Partial<SideMenuItem>[];
}

export const menuTemplates: MenuTemplate[] = [
  {
    name: 'CRUD Template',
    description: 'List, Create, Edit, Detail item\'ları içeren standart CRUD menüsü',
    items: [
      {
        itemType: 'item',
        title: 'List',
        icon: 'ListIcon',
        iconType: 'tabler',
        to: '/apps/{module}/list',
        order: 0,
        level: 0,
        pageType: 'user',
      },
      {
        itemType: 'item',
        title: 'Create',
        icon: 'PlusIcon',
        iconType: 'tabler',
        to: '/apps/{module}/create',
        order: 1,
        level: 0,
        pageType: 'user',
      },
      {
        itemType: 'item',
        title: 'Edit',
        icon: 'EditIcon',
        iconType: 'tabler',
        to: '/apps/{module}/edit/:id',
        order: 2,
        level: 0,
        pageType: 'user',
      },
      {
        itemType: 'item',
        title: 'Detail',
        icon: 'EyeIcon',
        iconType: 'tabler',
        to: '/apps/{module}/detail/:id',
        order: 3,
        level: 0,
        pageType: 'user',
      },
    ]
  },
  {
    name: 'Dashboard Template',
    description: 'Dashboard item\'ları içeren menü',
    items: [
      {
        itemType: 'header',
        header: 'Dashboards',
        order: 0,
        level: 0,
      },
      {
        itemType: 'item',
        title: 'Analytical',
        icon: 'ChartPieIcon',
        iconType: 'tabler',
        to: '/dashboards/analytical',
        order: 1,
        level: 0,
      },
      // ... diğer dashboard item'ları
    ]
  }
];

/**
 * Template'den menu items oluştur
 * Placeholder'ları değiştir ({module}, {id}, vb.)
 */
export function createItemsFromTemplate(
  template: MenuTemplate,
  replacements: Record<string, string> = {}
): Partial<SideMenuItem>[] {
  return template.items.map(item => {
    let itemJson = JSON.stringify(item);
    
    // Placeholder'ları değiştir
    Object.keys(replacements).forEach(key => {
      const regex = new RegExp(`\\{${key}\\}`, 'g');
      itemJson = itemJson.replace(regex, replacements[key]);
    });
    
    return JSON.parse(itemJson) as Partial<SideMenuItem>;
  });
}
```

**Side Menu Manager'da Kullanım:**

```vue
<!-- Side Menu Manager Toolbar -->
<v-menu>
  <template v-slot:activator="{ props }">
    <v-btn v-bind="props" color="primary">
      Template'den Oluştur
      <v-icon end>mdi-chevron-down</v-icon>
    </v-btn>
  </template>
  <v-list>
    <v-list-item
      v-for="template in menuTemplates"
      :key="template.name"
      @click="createFromTemplate(template)"
    >
      <v-list-item-title>{{ template.name }}</v-list-item-title>
      <v-list-item-subtitle>{{ template.description }}</v-list-item-subtitle>
    </v-list-item>
  </v-list>
</v-menu>
```

**Not**: Bu özellik MVP için zorunlu değil, ileri fazda eklenebilir.

### 11. Menu Item Bulk Operations ✅ PLANLAMA TAMAMLANDI

**Amaç**: Birden fazla menu item üzerinde toplu işlem

**İşlemler:**
- Toplu silme
- Toplu parent değiştirme
- Toplu enable/disable
- Toplu permission güncelleme
- Toplu order değiştirme (shift yapma)

**Side Menu Manager'da:**

**Selection Mekanizması:**
- Tree view'da checkbox'lar (her item'ın yanında)
- "Tümünü Seç" / "Tümünü Temizle" butonları
- Seçili item sayısı gösterimi
- Context menu (sağ tık)

**Bulk Action Butonları:**
- Toolbar'da seçili item varsa bulk action butonları görünür
- "Seçili Öğeleri Sil" (confirmation dialog ile)
- "Seçili Öğeleri Taşı" (parent seçimi)
- "Seçili Öğeleri Etkinleştir/Devre Dışı Bırak"
- "Seçili Öğeleri Sırala" (order değiştirme)

**Implementation:**

```vue
<!-- components/apps/side-menu-manager/MenuTreeView.vue -->
<script setup>
const selectedItemIds = ref<string[]>([]);
const isSelectMode = ref(false);

function toggleSelectMode() {
  isSelectMode.value = !isSelectMode.value;
  if (!isSelectMode.value) {
    selectedItemIds.value = [];
  }
}

function toggleItemSelection(itemId: string) {
  const index = selectedItemIds.value.indexOf(itemId);
  if (index > -1) {
    selectedItemIds.value.splice(index, 1);
  } else {
    selectedItemIds.value.push(itemId);
  }
}

function selectAll() {
  selectedItemIds.value = menuStore.allMenuItems.map(item => item.__dataId);
}

function clearSelection() {
  selectedItemIds.value = [];
}
</script>

<template>
  <v-treeview>
    <template v-slot:prepend="{ item }">
      <!-- Selection Checkbox -->
      <v-checkbox
        v-if="isSelectMode"
        :model-value="selectedItemIds.includes(item.__dataId)"
        @update:model-value="toggleItemSelection(item.__dataId)"
        density="compact"
        hide-details
        class="mr-2"
      />
      <!-- Drag Handle -->
      <v-icon v-else class="mr-2 drag-handle">mdi-drag</v-icon>
    </template>
  </v-treeview>
</template>
```

**Bulk Delete:**

```typescript
// stores/apps/sideMenuManager.ts
async function bulkDeleteItems(itemIds: string[]) {
  // Confirmation dialog
  const confirmed = await showConfirmDialog({
    title: 'Toplu Silme',
    message: `${itemIds.length} adet menü öğesini silmek istediğinizden emin misiniz?`,
    warning: 'Bu işlem geri alınamaz!'
  });
  
  if (!confirmed) return;
  
  // Her item'ı sil (children'ları da recursive olarak)
  for (const itemId of itemIds) {
    await this.deleteMenuItem(itemId);
  }
  
  // Success notification
  showSuccess(`${itemIds.length} adet menü öğesi silindi`);
  
  // Tree'yi yeniden yükle
  await this.loadAllMenuItems();
}
```

**Bulk Move (Parent Değiştirme):**

```typescript
async function bulkMoveItems(itemIds: string[], newParentId: string | null) {
  // Yeni parent için level hesapla
  const newLevel = newParentId 
    ? calculateLevel(newParentId)
    : 0;
  
  // Yeni parent'ın son order'ını al
  const baseOrder = calculateNewOrderForParent(newParentId);
  
  // Her item'ı güncelle
  const updates = itemIds.map((itemId, index) => ({
    __dataId: itemId,
    parentId: newParentId,
    level: newLevel,
    order: baseOrder + index
  }));
  
  // Bulk update (her item için ayrı API call - MngDataGateway bulk update yoksa)
  for (const update of updates) {
    await this.updateMenuItem(update.__dataId, update);
  }
  
  // Tree'yi yeniden yükle
  await this.loadAllMenuItems();
}
```

**Not**: Bu özellik MVP için zorunlu değil, kullanıcı talebi olursa eklenebilir.

### 12. Menu Item Export/Import (JSON) ✅ PLANLAMA TAMAMLANDI

**Amaç**: Menu item'ları JSON formatında export/import etme

**Export Özellikleri:**

1. **Full Export**:
   - Tüm menu items'ı export et
   - Format: JSON array
   - Metadata içerir (__dataId, timestamps, vb.)

2. **Selective Export**:
   - Sadece seçili item'ları export et
   - Parent-child ilişkilerini koru

3. **Template Export**:
   - Sadece schema (field definitions)
   - Data olmadan (template olarak)

**Import Özellikleri:**

1. **Import Validation**:
   - JSON format kontrolü
   - Schema validation (gerekli field'lar var mı?)
   - Duplicate kontrolü (__dataId kontrolü)

2. **Import Preview**:
   - Ne eklenecek/güncellenecek göster
   - Confirmation dialog
   - Merge/Replace seçeneği

3. **Import Stratejileri**:
   - **Replace**: Mevcut menu'yu tamamen değiştir
   - **Merge**: Mevcut menu'ya ekle/üzerine yaz
   - **Append**: Sadece ekle (update yapma)

**Implementation:**

```typescript
// stores/apps/sideMenuManager.ts
async function exportMenuItems(selectedIds: string[] | null = null) {
  let itemsToExport: SideMenuItem[];
  
  if (selectedIds && selectedIds.length > 0) {
    // Selective export
    itemsToExport = this.allMenuItems.filter(item => 
      selectedIds.includes(item.__dataId)
    );
  } else {
    // Full export
    itemsToExport = this.allMenuItems;
  }
  
  // Metadata temizle (opsiyonel - import için)
  const cleanedItems = itemsToExport.map(item => {
    const { __dataId, __createInfo, __lastUpdateInfo, __version, ...rest } = item;
    return rest;
  });
  
  // JSON stringify
  const jsonString = JSON.stringify(cleanedItems, null, 2);
  
  // Download as file
  const blob = new Blob([jsonString], { type: 'application/json' });
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = `menu-items_${new Date().toISOString().split('T')[0]}.json`;
  link.click();
  URL.revokeObjectURL(url);
}

async function importMenuItems(file: File, strategy: 'replace' | 'merge' | 'append' = 'merge') {
  // File read
  const text = await file.text();
  let importedItems: Partial<SideMenuItem>[];
  
  try {
    importedItems = JSON.parse(text);
  } catch (error) {
    throw new Error('Geçersiz JSON formatı');
  }
  
  // Validation
  validateMenuItems(importedItems);
  
  // Preview (opsiyonel)
  const preview = generateImportPreview(importedItems, strategy);
  const confirmed = await showImportPreviewDialog(preview);
  
  if (!confirmed) return;
  
  // Import
  if (strategy === 'replace') {
    // Tüm mevcut item'ları sil (dikkatli!)
    // Sonra yeni item'ları ekle
    await this.deleteAllMenuItems();
    await this.bulkCreateMenuItems(importedItems);
  } else if (strategy === 'merge') {
    // Mevcut item'ları koru, yeni olanları ekle, var olanları güncelle
    for (const item of importedItems) {
      if (item.__dataId && this.allMenuItems.find(i => i.__dataId === item.__dataId)) {
        // Update existing
        await this.updateMenuItem(item.__dataId, item);
      } else {
        // Create new
        await this.createMenuItem(item);
      }
    }
  } else {
    // Append - sadece yeni item'ları ekle
    await this.bulkCreateMenuItems(importedItems);
  }
  
  // Refresh
  await this.loadAllMenuItems();
}
```

**Side Menu Manager Toolbar'da:**

```vue
<!-- Export Button -->
<v-btn color="success" @click="exportMenuItems()">
  <DownloadIcon class="mr-2" size="20" />
  Export
</v-btn>

<!-- Import Button -->
<v-file-input
  v-model="importFile"
  accept=".json"
  prepend-icon="mdi-upload"
  label="Import"
  hide-details
  density="compact"
  @change="handleImport"
  style="display: none;"
/>
```

**Not**: Export/Import özelliği MVP için önemli ama basit versiyonu yeterli olacaktır.

### 13. Menu Item Versioning ✅ PLANLAMA TAMAMLANDI

**Amaç**: Menu item değişiklik geçmişi ve versiyon kontrolü

**Yaklaşım:**

**MngDataGateway Dataset Logging:**
- Dataset'in `logging` mode'u kullanılabilir
- `logging: "self"` → Her item'da `__history` array
- `logging: "common"` → `@data_logs` collection
- `logging: "none"` → Log yok

**Önerilen: `logging: "self"`**
- Her menu item değişikliği item'ın kendisinde saklanır
- `__history` array'inde version history
- Rollback için version ID kullanılabilir

**Implementation (Gelecek):**

```typescript
// Side Menu Manager'da version history gösterimi
async function showVersionHistory(itemId: string) {
  const item = await getMenuItem(itemId);
  const history = item.__history || [];
  
  // Version history dialog göster
  showVersionHistoryDialog(history);
}

async function rollbackToVersion(itemId: string, versionId: string) {
  // Version'dan data restore et
  await restoreMenuItemFromVersion(itemId, versionId);
  
  // Refresh
  await this.loadAllMenuItems();
}
```

**Not**: Bu özellik ileri seviye bir özellik, MVP'den sonra eklenebilir. Şimdilik gerekli değil.

---

### 14. Menu Item Analytics ✅ PLANLAMA TAMAMLANDI

**Amaç**: Hangi menu item'ların daha çok kullanıldığını analiz etme

**Yaklaşım:**

**İlk Aşama: Click Tracking (localStorage)**
- Menu item'a tıklandığında localStorage'da kayıt
- Click count tracking
- Basit analytics

**İleri Aşama: Backend Analytics**
- Analytics dataset (`@menu_analytics`)
- Detaylı tracking (timestamp, user, session, vb.)
- Dashboard görünümü

**Implementation (MVP - localStorage):**

```typescript
// stores/apps/sideMenu.ts
const menuItemClickCounts = ref<Record<string, number>>({});

function trackMenuItemClick(itemId: string) {
  menuItemClickCounts.value[itemId] = (menuItemClickCounts.value[itemId] || 0) + 1;
  
  // Save to localStorage
  const key = `menuClickCounts_${authStore.userInfo?.sub}`;
  localStorage.setItem(key, JSON.stringify(menuItemClickCounts.value));
}

function getMenuItemClickCount(itemId: string): number {
  return menuItemClickCounts.value[itemId] || 0;
}

// Sidebar'da menu item'a tıklandığında
function handleMenuItemClick(item: SideMenuItem) {
  trackMenuItemClick(item.__dataId);
  navigateTo(item.to);
}
```

**Side Menu Manager'da Analytics Görünümü (Gelecek):**
- Her menu item'ın yanında click count gösterimi
- En çok kullanılan item'ları highlight etme
- Analytics dashboard (chart, graph, vb.)

**Not**: Bu özellik ileri seviye bir özellik, MVP'den sonra eklenebilir.

### Önceliklendirme ve Implementasyon Sırası

**Faz 1-3: Minimum Viable Product (MVP) - Yüksek Öncelik**

1. ✅ **Faz 1**: Dataset ve Temel Altyapı (2-3 saat)
   - System Datasets kategorisi
   - @side_menu dataset oluşturma
   - Hard-coded menu verilerini export ve yükleme

2. ✅ **Faz 2**: Frontend Store ve Composable'lar (4-5 saat)
   - Side Menu Store
   - Page Permissions Composable
   - Icon utils ve mapping

3. ✅ **Faz 3**: Sidebar Entegrasyonu (5-6 saat)
   - Sidebar component güncelleme
   - Icon component güncelleme
   - Route guard/middleware
   - Unauthorized page
   - Error handling ve fallback

**Faz 4-5: Temel Özellikler - Yüksek Öncelik**

4. ✅ **Faz 4**: Cache ve Performans (3-4 saat)
   - Cache mekanizması (localStorage + memory)
   - Cache invalidation
   - Performance optimizasyonları

5. ✅ **Faz 5**: DOM Element Yetkilendirme (4-5 saat)
   - usePagePermissions composable implementasyonu
   - Mevcut sayfalarda test ve entegrasyon

**Faz 6: Yönetim Arayüzü - Yüksek Öncelik**

6. ✅ **Faz 6**: Side Menu Manager Sayfası (12-15 saat)
   - Ana sayfa ve layout
   - Tree view component
   - Detail/Form components
   - Icon picker
   - Permission editor
   - Drag & drop
   - CRUD operations

**Faz 7-8: İyileştirmeler - Orta Öncelik**

7. ✅ **Faz 7**: Real-Time Updates (4-5 saat)
   - SignalR entegrasyonu (eğer mevcutsa)
   - Event listener registration
   - Cache invalidation on events
   - **Not**: SignalR entegrasyonu yoksa bu faz atlanabilir

8. ✅ **Faz 8**: Menu Search/Filter (2-3 saat)
   - Search input sidebar'a ekleme
   - Real-time filtreleme
   - Keyboard shortcut

**Faz 9: İleri Seviye Özellikler - Düşük Öncelik**

9. ✅ **Faz 9**: Diğer İyileştirmeler (6-8 saat)
   - Dinamik chip/badge (opsiyonel - gelecek için planlama)
   - Menu item duplication
   - Keyboard navigation
   - Favorites/Bookmarks
   - Templates
   - Bulk operations
   - Export/Import geliştirmeleri

**Toplam Tahmini Süre:**
- MVP (Faz 1-3): ~12-14 saat
- Temel Özellikler (Faz 4-5): ~7-9 saat
- Yönetim Arayüzü (Faz 6): ~12-15 saat
- İyileştirmeler (Faz 7-9): ~14-18 saat
- **Toplam: ~45-56 saat** (5-7 iş günü)

---

## Referanslar

### İlgili Dosyalar
- `Mng.Ui/layouts/default.vue` - Ana layout, sidebar burada kullanılıyor
- `Mng.Ui/components/lc/Full/vertical-sidebar/` - Sidebar komponentleri
- `Mng.Ui/stores/customizer.ts` - Sidebar state yönetimi
- `Mng.Ui/stores/auth.ts` - User bilgileri (profile için)

### İlgili Dokümantasyon
- [Henüz eklenmedi]

---

---

## Side Menu Manager Sayfası

### Genel Bakış

Side Menu Manager sayfası, menü elemanlarını yönetmek için kullanılacak bir yönetim panelidir. Bu sayfa sayesinde menü item'ları eklenebilir, güncellenebilir, silinebilir ve parent-child ilişkileri değiştirilebilir.

### Sayfa Yapısı

**Route**: `/apps/side-menu-manager` veya `/admin/side-menu-manager`

**Layout**: 3 Bölümlü Layout
- **Sol Panel** (25-30%): Tree view (menü item'ları hiyerarşik görünüm)
- **Orta Panel** (70-75%): Detail/Edit form (seçili item'ın detayları ve düzenleme formu)
- **Üst Toolbar**: Yeni item ekleme, arama, refresh butonları

### Sol Panel: Tree View

**Özellikler:**
- Menü item'ları hiyerarşik tree yapısında görüntüleme
- Header ve item'lar farklı icon/renk ile gösterilir
- Expand/collapse desteği
- **Sıralama (Order Management)**:
  - Header'lar için sıralama (order field'ı)
  - Item'lar için sıralama (aynı parent altında order field'ı)
  - Drag & drop ile sıralama (order değiştirme) ✅ Önerilen
  - Manuel order input ile sıralama (alternatif)
- **Parent Değiştirme**:
  - Item'ların header bilgisi değiştirilebilir (parentId field'ı)
  - Header'ların da parent'ı değiştirilebilir (nested header desteği)
  - Drag & drop ile parent değiştirme ✅ Önerilen
  - Form dropdown ile parent seçimi (alternatif)
- Seçili item highlight
- Search/filter (tree içinde arama)
- **Nested Header Desteği**: Bir header altına başka bir header eklenebilir

**Tree Item Gösterimi:**
- **Header Item**: 
  - Folder icon + header text (bold)
  - Level indicator (görsel - nested header için)
  - Order badge (opsiyonel - sıralama gösterimi için)
- **Menu Item**: 
  - Icon (varsa) + title
  - Chip/badge (varsa)
  - Route path (küçük, gri text)
  - Permission indicator (icon - varsa yetki tanımlı ise)
  - Order badge (opsiyonel)

**Vuetify Component Önerisi:**
- `v-treeview` component (native Vuetify tree component) - Basit kullanım için
- Custom tree component (daha fazla kontrol için) ✅ Önerilen - Drag & drop desteği için

**Drag & Drop Özellikleri:**
- Drag handle: Her item'ın yanında drag handle icon'u
- Sıralama: Aynı seviyede (aynı parent altında) sürükleyerek sıralama
- Parent değiştirme: Farklı parent'a sürükleyerek taşıma
- Visual feedback: Drop zone highlight, drag preview

### Orta Panel: Detail/Edit Form

**İki Mod:**

1. **Detail View Mod** (Sadece görüntüleme)
   - Seçili item'ın tüm bilgilerini göster
   - Düzenle butonu ile edit moduna geç

2. **Edit/Create Mod** (Düzenleme/Oluşturma)
   - Form alanları:
     - **Temel Bilgiler:**
       - Item Type (header/item) - Radio button veya Select
       - Parent (dropdown - parent item/header seçimi)
         - Seçenekler: "Root (En Üst Seviye)", tüm header'lar ve item'lar listesi
         - Tree view ile parent seçimi (opsiyonel - daha kullanıcı dostu)
       - Order (number input, spinner)
         - Aynı parent altındaki item'ların sırasını belirler
         - Parent değiştiğinde otomatik hesaplanabilir (yeni parent'ın son order + 1)
       - Level (auto-calculate, read-only gösterilebilir)
         - Parent'ın level'ı + 1 olarak otomatik hesaplanır
     
     - **Header için:**
       - Header Text (text input)
     
     - **Item için:**
       - Title (text input)
       - Icon Type (mdi/tabler) - Radio button veya Select (default: "tabler")
       - Icon (icon picker component - seçilen icon type'a göre)
       - Route Path (text input)
       - Link Type (internal/external) - Radio button
       - Page Type (user/manager/admin) - Select (default: "user")
       - Sub Caption (text input, optional)
       - Disabled (checkbox)
     
     - **Chip/Badge (Item için):**
       - Chip Text (text input, optional)
       - Chip Background Color (color picker, optional)
       - Chip Text Color (color picker, optional)
       - Chip Variant (select, optional)
       - Chip Icon (select, optional)
     
     - **Permissions (Item için):**
       - Permissions editor (complex component)
       - Her grup için checkbox grid:
         - Grup adı (MngKeeper'dan çekilecek)
         - view, create, update, delete, export checkbox'ları
       - Admin grup için otomatik tüm yetkiler true
   
   - **Action Butonları:**
     - Kaydet (Create/Update)
     - İptal
     - Sil (sadece mevcut item için, confirmation dialog ile)

### Üst Toolbar

**Özellikler:**
- **Yeni Header Ekle** butonu
  - Root level header ekleme
  - Seçili item/header'ın altına header ekleme (nested header)
- **Yeni Item Ekle** butonu
  - Root level item ekleme
  - Seçili header/item'ın altına item ekleme
- **Ara** (search input - tree'de filtreleme)
- **Yenile** (refresh button - API'den tekrar çek)
- **Expand All / Collapse All** (tree için)
- **Sıralama Modu** toggle (drag & drop aktif/pasif)
- **Export** (menu items'ı JSON olarak export et)
- **Import** (JSON'dan menu items import et) - Opsiyonel

### CRUD İşlemleri

#### 1. Yeni Menü Elemanı Ekleme

**Senaryo A: Yeni Header Ekleme (Root Level)**
1. Üst toolbar'dan "Yeni Header Ekle" butonuna tıkla
2. Orta panel'de create form açılır (itemType: "header")
3. Header text girilir
4. Parent: "Root (En Üst Seviye)" seçilir (null)
5. Order: Root level'daki header sayısı + 1 (otomatik hesaplanabilir)
6. Level: 0 (otomatik hesaplanır)
7. Kaydet → `POST /api/v1/data/@side_menu`

**Senaryo B: Yeni Header Ekleme (Nested Header - Alt Header)**
1. Tree'de bir header/item seçilir
2. Üst toolbar'dan "Yeni Header Ekle" butonuna tıkla (veya sağ tık → "Alt Header Ekle")
3. Orta panel'de create form açılır (itemType: "header")
4. Header text girilir
5. Parent: Seçili item/header otomatik seçilir (değiştirilebilir)
6. Order: Seçili parent'ın altındaki item sayısı + 1 (otomatik)
7. Level: Parent'ın level + 1 (otomatik hesaplanır)
8. Kaydet → `POST /api/v1/data/@side_menu`

**Senaryo C: Yeni Item Ekleme (Root Level)**
1. Üst toolbar'dan "Yeni Item Ekle" butonuna tıkla
2. Parent seçimi: "Root (En Üst Seviye)" (null)
3. Form doldurulur (title, icon, to, vb.)
4. Order: Root level'daki item sayısı + 1 (otomatik)
5. Level: 0 (otomatik hesaplanır)
6. Kaydet → `POST /api/v1/data/@side_menu`

**Senaryo D: Yeni Item Ekleme (Header Altına)**
1. Tree'de bir header seçilir
2. Üst toolbar'dan "Yeni Item Ekle" butonuna tıkla (veya sağ tık → "Alt Item Ekle")
3. Parent: Seçili header otomatik seçilir (değiştirilebilir)
4. Form doldurulur
5. Order: Seçili header'ın altındaki item sayısı + 1 (otomatik)
6. Level: Parent'ın level + 1 (otomatik hesaplanır)
7. Kaydet → `POST /api/v1/data/@side_menu`

**Senaryo E: Yeni Item Ekleme (Item Altına - Nested Item)**
1. Tree'de bir item seçilir
2. Üst toolbar'dan "Yeni Item Ekle" butonuna tıkla (veya sağ tık → "Alt Item Ekle")
3. Parent: Seçili item otomatik seçilir (değiştirilebilir)
4. Form doldurulur
5. Order: Seçili item'ın altındaki item sayısı + 1 (otomatik)
6. Level: Parent'ın level + 1 (otomatik hesaplanır)
7. Kaydet → `POST /api/v1/data/@side_menu`

#### 2. Menü Elemanı Güncelleme

1. Tree'de bir item'a tıkla (seç)
2. Orta panel'de detail view açılır
3. "Düzenle" butonuna tıkla → Edit mod
4. Form alanlarını düzenle
5. Kaydet → `PUT /api/v1/data/@side_menu/{__dataId}`

**Önemli Güncelleme Senaryoları:**

- **Parent Değiştirme (Header Bilgisi Değiştirme):**
  - **Yöntem 1: Form Üzerinden**
    - Parent dropdown'dan yeni parent seçilir (header veya item olabilir)
    - Level otomatik hesaplanır (yeni parent'ın level + 1)
    - Order otomatik ayarlanır (yeni parent'ın son order + 1) veya kullanıcı belirler
    - Update işlemi yapılır
  
  - **Yöntem 2: Drag & Drop (Önerilen)**
    - Tree'de item/header sürüklenir
    - Yeni parent üzerine bırakılır
    - Confirmation dialog (opsiyonel)
    - Level ve order otomatik hesaplanır
    - Update işlemi yapılır
  
  - **Özel Durumlar:**
    - Header'ın parent'ı değiştirilebilir (nested header)
    - Item'ın parent'ı header veya başka bir item olabilir
    - Circular reference kontrolü (bir item kendi child'ının parent'ı olamaz)

- **Sıralama Değiştirme (Order):**
  - **Yöntem 1: Drag & Drop (Önerilen)**
    - Aynı parent altındaki item'lar sürüklenerek sıralanır
    - Order değerleri otomatik güncellenir
    - Bulk update işlemi (birden fazla item'ın order'ı güncellenir)
  
  - **Yöntem 2: Manuel Order Input**
    - Order input değeri değiştirilir
    - Diğer item'ların order'ı otomatik ayarlanır (shift yapılır)
    - Update işlemi yapılır
  
  - **Özel Durumlar:**
    - Header'lar için sıralama (root level'da)
    - Header altındaki item'lar için sıralama
    - Nested item'lar için sıralama (aynı parent altında)

- **Item Type Değiştirme:**
  - Header → Item: Header text → Title'a dönüşür
  - Item → Header: Tüm item field'ları temizlenir (icon, to, permissions, vb.)
  - Dikkat: Children varsa uyarı gösterilmeli
  - Type değiştiğinde parent ve order korunur

#### 3. Menü Elemanı Silme

1. Tree'de bir item'a sağ tık → "Sil" veya
2. Detail view'da "Sil" butonuna tıkla
3. Confirmation dialog açılır:
   - "Bu menü elemanını silmek istediğinizden emin misiniz?"
   - Uyarı: Eğer children varsa → "Bu item'ın altında X adet alt item bulunmaktadır. Bunlar da silinecektir."
4. Onaylanırsa → `DELETE /api/v1/data/@side_menu/{__dataId}`
5. Tree'den kaldırılır (ve children'ları da)

**Not**: Soft delete kullanılacaksa, deleted item'lar ayrı bir görünümde gösterilebilir (filter ile).

#### 4. Sıralama Yönetimi (Order Management)

**Header Sıralama:**
- Root level'daki header'lar `order` field'ına göre sıralanır
- Header'lar drag & drop ile sıralanabilir
- Order değiştiğinde diğer header'ların order'ı güncellenir

**Item Sıralama:**
- Aynı parent altındaki item'lar `order` field'ına göre sıralanır
- Item'lar drag & drop ile sıralanabilir
- Order değiştiğinde aynı parent altındaki diğer item'ların order'ı güncellenir

**Bulk Order Update:**
- Drag & drop sonrası birden fazla item'ın order'ı değişebilir
- Optimizasyon: Tüm değişen order'ları tek API çağrısında güncelle (batch update)
- Veya her item için ayrı update (basit ama daha yavaş)

#### 5. Parent Değiştirme (Header/Group Değiştirme)

**Yöntem 1: Form Üzerinden**
1. Item veya Header seçilir
2. Edit moduna geçilir
3. Parent dropdown'dan yeni parent seçilir
   - Dropdown'da tüm header'lar ve item'lar listelenir
   - Seçili item ve children'ları listeden çıkarılır (circular reference önleme)
   - Tree view ile seçim (opsiyonel - daha kullanıcı dostu)
4. Level otomatik hesaplanır (yeni parent'ın level + 1)
5. Order otomatik ayarlanır (yeni parent'ın son order + 1) veya kullanıcı belirler
6. Kaydet → Update işlemi

**Yöntem 2: Drag & Drop** ✅ Önerilen
1. Tree'de item veya header sürüklenir
2. Yeni parent (header veya item) üzerine bırakılır
3. Drop zone highlight gösterilir
4. Confirmation dialog (opsiyonel - özellikle parent değiştiğinde)
5. Level ve order otomatik hesaplanır
6. Update işlemi yapılır
7. Tree yeniden render edilir

**Nested Header Desteği:**
- Bir header'ın parent'ı başka bir header olabilir
- Nested header'lar görsel olarak farklı gösterilir (indentation, icon)
- Örnek yapı:
  ```
  Header 1 (level 0)
    ├─ Item 1.1 (level 1)
    ├─ Header 1.1 (level 1) ← Nested Header
    │  ├─ Item 1.1.1 (level 2)
    │  └─ Item 1.1.2 (level 2)
    └─ Item 1.2 (level 1)
  ```

### Permission Editor Component

**Yapı:**
- Table/Grid görünümü
- Satırlar: Gruplar (MngKeeper'dan çekilen)
- Sütunlar: view, create, update, delete, export
- Her hücre: Checkbox
- Admin grup: Disabled, otomatik tüm yetkiler true

**Özellikler:**
- Grup listesi MngKeeper API'den çekilir
- Grup ekleme/çıkarma (MngKeeper'dan yapılmalı, burada sadece gösterilir)
- Bulk select: "Tümünü Seç" / "Tümünü Temizle" butonları
- Column-wise select: "Tüm view yetkileri", "Tüm create yetkileri", vb.

### API Entegrasyonu

#### Menu Items Yükleme

```typescript
// stores/sideMenuManager.ts
async function loadAllMenuItems() {
  const response = await $fetch('/api/v1/data/@side_menu', {
    method: 'GET',
    params: {
      page: 1,
      pageSize: 1000, // Tüm items
      sort: 'order:asc,level:asc',
    },
    headers: {
      Authorization: `Bearer ${authStore.accessToken}`
    }
  });
  
  this.allMenuItems = response.data;
  this.menuItemsTree = buildMenuTree(response.data);
  return response.data;
}
```

#### Yeni Item Ekleme

```typescript
async function createMenuItem(itemData: Partial<SideMenuItem>) {
  const response = await $fetch('/api/v1/data/@side_menu', {
    method: 'POST',
    body: {
      ...itemData,
      level: calculateLevel(itemData.parentId), // Parent'ın level + 1
    },
    headers: {
      Authorization: `Bearer ${authStore.accessToken}`
    }
  });
  
  // Tree'yi yeniden yükle
  await this.loadAllMenuItems();
  return response.data;
}
```

#### Item Güncelleme

```typescript
async function updateMenuItem(itemId: string, itemData: Partial<SideMenuItem>) {
  const response = await $fetch(`/api/v1/data/@side_menu/${itemId}`, {
    method: 'PUT',
    body: itemData,
    headers: {
      Authorization: `Bearer ${authStore.accessToken}`
    }
  });
  
  // Tree'yi yeniden yükle
  await this.loadAllMenuItems();
  return response.data;
}
```

#### Item Silme

```typescript
async function deleteMenuItem(itemId: string) {
  // Önce children'ları kontrol et
  const children = this.allMenuItems.filter(item => item.parentId === itemId);
  
  if (children.length > 0) {
    // Recursive silme veya uyarı göster
    // Önce children'ları sil, sonra parent'ı sil
    for (const child of children) {
      await this.deleteMenuItem(child.__dataId);
    }
  }
  
  const response = await $fetch(`/api/v1/data/@side_menu/${itemId}`, {
    method: 'DELETE',
    headers: {
      Authorization: `Bearer ${authStore.accessToken}`
    }
  });
  
  // Tree'yi yeniden yükle
  await this.loadAllMenuItems();
  return response;
}
```

### Validation Rules

#### Item Type: Header
- ✅ `header` text zorunlu
- ✅ `itemType` = "header"
- ✅ `order` zorunlu, >= 0
- ❌ `title`, `icon`, `to` gibi item field'ları kullanılamaz

#### Item Type: Item
- ✅ `title` zorunlu
- ✅ `itemType` = "item"
- ✅ `order` zorunlu, >= 0
- ✅ `level` zorunlu, >= 0
- ⚠️ `to` route path geçerli olmalı (internal route veya external URL)
- ⚠️ `icon` geçerli icon adı olmalı (vue-tabler-icons listesinden)

#### Parent-Child İlişkisi
- ⚠️ Bir item/header kendi kendinin parent'ı olamaz (parentId !== __dataId)
- ⚠️ Bir item/header'ın parent'ı, kendi child'ı olamaz (circular reference)
  - Örnek: Item A'nın parent'ı Item B ise, Item B'nin parent'ı Item A olamaz
  - Kontrol: Parent değiştirilirken recursive check yapılmalı
- ⚠️ Parent değiştirildiğinde level otomatik hesaplanmalı (yeni parent'ın level + 1)
- ⚠️ Parent değiştirildiğinde order otomatik ayarlanmalı (yeni parent'ın son order + 1)
- ⚠️ Header'lar da parent olabilir (nested header desteği)
- ⚠️ Item'lar da parent olabilir (nested item desteği)

#### Permissions
- ⚠️ Permission tanımlı değilse, tüm kullanıcılar erişebilir (backward compatibility)
- ⚠️ Admin grup için tüm yetkiler otomatik true (güvenlik için)

### UI/UX Önerileri

1. **Tree View:**
   - Smooth expand/collapse animasyonları
   - Selected item highlight (primary color)
   - Hover effects
   - Drag handle (opsiyonel - sıralama için)
   - Loading state (skeleton veya spinner)

2. **Form:**
   - Field validation (real-time)
   - Error messages (field altında)
   - Success notification (kaydetme sonrası)
   - Unsaved changes warning (sayfa kapanırken)
   - Auto-save draft (localStorage'da) - Opsiyonel

3. **Responsive:**
   - Mobil: Tree ve form stack (üst-alt)
   - Tablet: Tree ve form yan yana
   - Desktop: 3 bölümlü layout

4. **Keyboard Shortcuts:**
   - `Ctrl+N` / `Cmd+N`: Yeni item ekle
   - `Delete`: Seçili item'ı sil
   - `Ctrl+S` / `Cmd+S`: Kaydet
   - `Escape`: İptal / Form kapat

### Drag & Drop Özellikleri

**Kütüphane Seçimi:**
- **Vue Draggable Next** (`vue-draggable-next`) - Mevcut dependency'de var ✅
- Veya **Vue Draggable** (`vuedraggable`) - Mevcut dependency'de var ✅

**Kullanım Senaryoları:**

1. **Sıralama (Reorder)**:
   - Aynı parent altındaki item'lar arasında sürükleme
   - Order değerleri otomatik güncellenir
   - Visual feedback: Drag preview, drop zone highlight

2. **Parent Değiştirme (Move)**:
   - Farklı parent'a sürükleme
   - Level ve order otomatik hesaplanır
   - Circular reference kontrolü

**Implementation:**

```vue
<!-- components/apps/side-menu-manager/MenuTreeView.vue -->
<script setup>
import { VueDraggableNext } from 'vue-draggable-next';

const props = defineProps<{
  items: SideMenuItem[];
  selectedItemId: string | null;
}>();

function handleDragEnd(event: any) {
  const { newIndex, oldIndex } = event;
  
  if (newIndex === oldIndex) return; // Sıralama değişmedi
  
  // Order güncelleme
  const movedItem = props.items[oldIndex];
  const newOrder = calculateNewOrder(movedItem.parentId, newIndex);
  
  // Bulk update: Tüm etkilenen item'ların order'ını güncelle
  updateOrders(movedItem.parentId, oldIndex, newIndex);
}

function handleDropOnParent(droppedItem: SideMenuItem, targetParent: SideMenuItem) {
  // Circular reference kontrolü
  if (isCircularReference(droppedItem.__dataId, targetParent.__dataId)) {
    showError('Bu işlem döngüsel referans oluşturur!');
    return;
  }
  
  // Parent, level ve order güncelleme
  updateMenuItem({
    __dataId: droppedItem.__dataId,
    parentId: targetParent.__dataId,
    level: targetParent.level + 1,
    order: calculateNewOrderForParent(targetParent.__dataId)
  });
}
</script>
```

### Parent Seçici Component

**Parent Dropdown Geliştirmesi:**

```vue
<!-- Parent Selector Component -->
<v-select
  v-model="formData.parentId"
  label="Parent (Header/Group)"
  :items="parentOptions"
  item-title="label"
  item-value="value"
  variant="outlined"
  clearable
>
  <template v-slot:item="{ props, item }">
    <v-list-item v-bind="props">
      <template v-slot:prepend>
        <!-- Level indicator (indentation) -->
        <div :style="{ marginLeft: `${item.raw.level * 20}px` }"></div>
        <!-- Item type icon -->
        <v-icon v-if="item.raw.type === 'header'">mdi-folder</v-icon>
        <v-icon v-else>mdi-menu</v-icon>
      </template>
      <v-list-item-title>
        {{ item.raw.type === 'header' ? item.raw.header : item.raw.title }}
      </v-list-item-title>
    </v-list-item>
  </template>
</v-select>
```

**Parent Options Oluşturma:**

```typescript
// stores/sideMenuManager.ts
const parentOptions = computed(() => {
  const options = [
    { 
      label: 'Root (En Üst Seviye)', 
      value: null, 
      level: -1, 
      type: 'root' 
    }
  ];
  
  // Tüm header'ları ve item'ları ekle (current item ve children'ları hariç)
  function addItems(items: SideMenuItem[], excludeIds: string[] = []) {
    items.forEach(item => {
      if (excludeIds.includes(item.__dataId)) return;
      
      options.push({
        label: item.itemType === 'header' ? item.header : item.title,
        value: item.__dataId,
        level: item.level,
        type: item.itemType
      });
      
      // Children'ları da ekle (recursive)
      if (item.children && item.children.length > 0) {
        addItems(item.children, excludeIds);
      }
    });
  }
  
  // Current item ve children'larını exclude listesine ekle (circular reference önleme)
  const excludeIds = currentItem.value 
    ? [currentItem.value.__dataId, ...getAllChildrenIds(currentItem.value)]
    : [];
  
  addItems(menuItemsTree.value, excludeIds);
  
  return options;
});

function getAllChildrenIds(item: SideMenuItem): string[] {
  const ids: string[] = [];
  if (item.children) {
    item.children.forEach(child => {
      ids.push(child.__dataId);
      ids.push(...getAllChildrenIds(child));
    });
  }
  return ids;
}
```

### Sıralama (Order) Hesaplama Fonksiyonları

```typescript
// stores/sideMenuManager.ts veya utils/menu-utils.ts

/**
 * Yeni parent için order hesapla (en son order + 1)
 */
function calculateNewOrderForParent(parentId: string | null): number {
  const siblings = allMenuItems.value.filter(item => item.parentId === parentId);
  if (siblings.length === 0) return 0;
  
  const maxOrder = Math.max(...siblings.map(item => item.order));
  return maxOrder + 1;
}

/**
 * Drag & drop sonrası order'ları güncelle
 */
function updateOrdersAfterDrag(
  parentId: string | null,
  oldIndex: number,
  newIndex: number
) {
  const siblings = allMenuItems.value
    .filter(item => item.parentId === parentId)
    .sort((a, b) => a.order - b.order);
  
  if (oldIndex === newIndex) return;
  
  const movedItem = siblings[oldIndex];
  siblings.splice(oldIndex, 1);
  siblings.splice(newIndex, 0, movedItem);
  
  // Yeni order'ları ata
  const updates = siblings.map((item, index) => ({
    __dataId: item.__dataId,
    order: index
  }));
  
  // Bulk update yap
  bulkUpdateOrders(updates);
}

/**
 * Circular reference kontrolü
 */
function isCircularReference(itemId: string, newParentId: string | null): boolean {
  if (!newParentId) return false; // Root'a taşıma sorun değil
  
  // Yeni parent'ın tüm parent'larını kontrol et
  let currentParentId: string | null = newParentId;
  const checkedIds = new Set<string>();
  
  while (currentParentId) {
    if (checkedIds.has(currentParentId)) break; // Circular reference bulundu
    
    if (currentParentId === itemId) {
      return true; // Item kendi child'ının parent'ı olmaya çalışıyor
    }
    
    checkedIds.add(currentParentId);
    const parent = allMenuItems.value.find(item => item.__dataId === currentParentId);
    currentParentId = parent?.parentId || null;
  }
  
  return false;
}

/**
 * Level hesapla (parent'ın level + 1)
 */
function calculateLevel(parentId: string | null): number {
  if (!parentId) return 0; // Root level
  
  const parent = allMenuItems.value.find(item => item.__dataId === parentId);
  if (!parent) return 0;
  
  return parent.level + 1;
}
```

### Sayfa Komponentleri

**Ana Sayfa:**
- `pages/apps/side-menu-manager/index.vue`

**Alt Komponentler:**
- `components/apps/side-menu-manager/MenuTreeView.vue` - Sol panel tree (drag & drop desteği ile)
- `components/apps/side-menu-manager/MenuItemDetail.vue` - Orta panel detail view
- `components/apps/side-menu-manager/MenuItemForm.vue` - Orta panel edit/create form
- `components/apps/side-menu-manager/PermissionEditor.vue` - Permissions editor component
- `components/apps/side-menu-manager/MenuItemToolbar.vue` - Üst toolbar
- `components/apps/side-menu-manager/ParentSelector.vue` - Parent seçici component (opsiyonel)

**Store:**
- `stores/apps/sideMenuManager.ts` - State management (menu items, selected item, form state, order management)

### Yetkilendirme

**Sayfa Erişimi:**
- Admin veya Manager yetkisi gerektirebilir
- Route: `/apps/side-menu-manager`
- Menu item: Side Menu Manager (Admin/System kategorisinde)

**İşlem Yetkileri:**
- View: Menü item'larını görüntüleme
- Create: Yeni item ekleme
- Update: Item güncelleme
- Delete: Item silme

**Not**: Bu sayfa için özel permission kontrolü yapılabilir (sadece admin/manager erişebilir).

---

## Icon Seçim Sistemi

### Mevcut Icon Kütüphaneleri

Uygulamada **iki farklı icon kütüphanesi** kullanılıyor:

#### 1. Material Design Icons (MDI)

**Package**: `@mdi/font` v7.4.47  
**Import**: `@mdi/font/css/materialdesignicons.css` (plugins/vuetify.ts)  
**Kullanım**: String olarak icon adı (örn: `"mdi-home"`, `"mdi-magnify"`)  
**Vuetify Entegrasyonu**: Vuetify'ın `v-icon` component'i ile kullanılır

**Örnek Kullanım:**
```vue
<v-icon>mdi-home</v-icon>
<v-btn prepend-icon="mdi-magnify">Ara</v-btn>
```

**Icon Adı Formatı**: `mdi-{icon-name}` (örn: `mdi-home`, `mdi-account`, `mdi-cog`)

**Mevcut Kullanım Örnekleri:**
- `mdi-magnify` - Arama icon'u
- `mdi-chevron-down` - Aşağı ok
- `mdi-file-delimited` - CSV dosya icon'u
- `mdi-file-excel` - Excel dosya icon'u
- `mdi-close` - Kapatma icon'u

**Icon Listesi**: [Material Design Icons](https://materialdesignicons.com/) - 7000+ icon mevcut

#### 2. Vue Tabler Icons

**Package**: `vue-tabler-icons` v2.21.0  
**Plugin**: `nuxtApp.vueApp.use(VueTablerIcons)` (plugins/vuetify.ts)  
**Kullanım**: Component import (örn: `import { ChartPieIcon } from 'vue-tabler-icons'`)  
**Mevcut Sidebar**: Şu anda sidebar'da kullanılıyor

**Örnek Kullanım:**
```vue
<script setup>
import { ChartPieIcon, HomeIcon } from 'vue-tabler-icons';
</script>

<template>
  <ChartPieIcon size="20" />
  <component :is="iconComponent" size="14" />
</template>
```

**Icon Adı Formatı**: PascalCase component adı (örn: `ChartPieIcon`, `HomeIcon`, `UserIcon`)

**Mevcut Kullanım Örnekleri:**
- `ChartPieIcon` - Dashboard icon'u
- `UserCircleIcon` - Kullanıcı icon'u
- `BoxIcon` - Kutu/container icon'u
- `BellIcon` - Bildirim icon'u
- `CircleDotIcon` - Nokta icon'u (alt menüler için)

**Icon Listesi**: [Tabler Icons](https://tabler.io/icons) - 4000+ icon mevcut

### Icon Tipi Field'ı

Menu item'larda hangi icon kütüphanesinin kullanıldığını belirtmek için `iconType` field'ı eklenecek:

- **`iconType: 'mdi'`**: Material Design Icons kullanılıyor, `icon` field'ı `"mdi-{icon-name}"` formatında
- **`iconType: 'tabler'`**: Vue Tabler Icons kullanılıyor, `icon` field'ı `"{IconName}"` formatında (component adı)

**Default**: `"tabler"` (mevcut sidebar ile uyumluluk için)

### Icon Seçici Component (Icon Picker)

**Component**: `components/apps/side-menu-manager/IconPicker.vue`

**Özellikler:**

1. **Icon Type Seçimi**:
   - Radio button veya Select ile icon type seçimi (MDI veya Tabler)
   - Icon type değiştiğinde icon listesi yenilenir

2. **Icon Listesi Görünümü**:
   - Grid layout (responsive)
   - Her icon için preview gösterimi
   - Icon adı tooltip olarak gösterilir
   - Search/filter input ile icon arama

3. **Arama/Filtreleme**:
   - Icon adına göre arama
   - Kategorilere göre filtreleme (opsiyonel)
   - Popüler icon'lar (frequently used) - Opsiyonel

4. **Seçim**:
   - Icon'a tıklayınca seçilir
   - Seçili icon highlight (border/background)
   - Seçilen icon'un adı form field'ına yazılır

**Kullanım:**

```vue
<IconPicker 
  v-model="selectedIcon" 
  v-model:iconType="selectedIconType"
  :iconType="iconType"
/>
```

### Icon Listesi Yönetimi

#### MDI Icon Listesi

**Yaklaşım 1: Statik Liste (Önerilen - Başlangıç için)**
- Popüler/yararlı icon'ların manuel listesi
- 100-200 arası yaygın kullanılan icon
- Dosya: `utils/icons/mdi-icons.json` veya TypeScript array

**Yaklaşım 2: Dinamik Liste (İleri Seviye)**
- MDI package'ından tüm icon'ları parse etme
- Runtime'da icon listesi oluşturma
- Daha fazla icon seçeneği ama daha yavaş

**Önerilen Yaklaşım**: İlk aşamada **statik liste** kullan, sonra gerektiğinde dinamik listeye geçiş yap.

**MDI Icon Örnekleri (Yaygın Kullanılanlar):**

```typescript
const mdiIconList = [
  // Navigation
  'mdi-home', 'mdi-menu', 'mdi-menu-open', 'mdi-arrow-left', 'mdi-arrow-right',
  // User/Auth
  'mdi-account', 'mdi-account-circle', 'mdi-account-group', 'mdi-login', 'mdi-logout',
  // Files/Documents
  'mdi-file', 'mdi-file-document', 'mdi-folder', 'mdi-folder-open',
  // Actions
  'mdi-plus', 'mdi-minus', 'mdi-pencil', 'mdi-delete', 'mdi-content-save',
  'mdi-magnify', 'mdi-filter', 'mdi-download', 'mdi-upload', 'mdi-refresh',
  // Status
  'mdi-check', 'mdi-close', 'mdi-alert', 'mdi-information', 'mdi-warning',
  // Charts/Data
  'mdi-chart-line', 'mdi-chart-bar', 'mdi-chart-pie', 'mdi-table',
  // Settings
  'mdi-cog', 'mdi-cog-outline', 'mdi-tune', 'mdi-settings',
  // Communication
  'mdi-email', 'mdi-email-outline', 'mdi-bell', 'mdi-bell-outline',
  // ... daha fazlası
];
```

#### Tabler Icon Listesi

**Yaklaşım: Vue Tabler Icons'dan Component Adlarını Çıkarma**

Vue Tabler Icons package'ından tüm icon component'lerinin adlarını almak için:

```typescript
// utils/icons/tabler-icons.ts
import * as TablerIcons from 'vue-tabler-icons';

// Tüm icon component adlarını çıkar
const tablerIconNames = Object.keys(TablerIcons).filter(key => 
  key.endsWith('Icon') && typeof TablerIcons[key] === 'object'
);

// Component adından display name oluştur (örn: "ChartPieIcon" → "Chart Pie")
function formatIconName(componentName: string): string {
  return componentName
    .replace(/Icon$/, '') // Icon suffix'ini kaldır
    .replace(/([A-Z])/g, ' $1') // Büyük harflerden önce boşluk ekle
    .trim();
}
```

**Tabler Icon Örnekleri (Mevcut Sidebar'dan):**

```typescript
const tablerIconList = [
  'ChartPieIcon', 'CoffeeIcon', 'CpuIcon', 'FlagIcon', 'BasketIcon',
  'UserCircleIcon', 'BoxIcon', 'BellIcon', 'MailIcon', 'TicketIcon',
  'ShoppingCartIcon', 'Message2Icon', 'FilesIcon', 'CalendarIcon',
  // ... mevcut sidebar'daki tüm icon'lar
];
```

### Icon Picker Component Tasarımı

**Yapı:**

```vue
<!-- components/apps/side-menu-manager/IconPicker.vue -->
<script setup>
import { ref, computed, watch } from 'vue';
import { mdiIconList } from '@/utils/icons/mdi-icons';
import { tablerIconList } from '@/utils/icons/tabler-icons';

const props = defineProps<{
  modelValue: string; // Seçili icon adı
  iconType: 'mdi' | 'tabler'; // Icon tipi
}>();

const emit = defineEmits<{
  'update:modelValue': [value: string];
  'update:iconType': [value: 'mdi' | 'tabler'];
}>();

const selectedIconType = ref(props.iconType);
const searchQuery = ref('');
const selectedIcon = ref(props.modelValue);

// Icon listesi (type'a göre)
const iconList = computed(() => {
  return selectedIconType.value === 'mdi' ? mdiIconList : tablerIconList;
});

// Filtrelenmiş icon listesi
const filteredIcons = computed(() => {
  if (!searchQuery.value) return iconList.value;
  
  const query = searchQuery.value.toLowerCase();
  return iconList.value.filter(icon => {
    const displayName = formatIconDisplayName(icon, selectedIconType.value);
    return displayName.toLowerCase().includes(query) || icon.toLowerCase().includes(query);
  });
});

function formatIconDisplayName(iconName: string, type: 'mdi' | 'tabler'): string {
  if (type === 'mdi') {
    return iconName.replace('mdi-', '').replace(/-/g, ' ');
  } else {
    return iconName.replace(/Icon$/, '').replace(/([A-Z])/g, ' $1').trim();
  }
}

function selectIcon(iconName: string) {
  selectedIcon.value = iconName;
  emit('update:modelValue', iconName);
}

function changeIconType(type: 'mdi' | 'tabler') {
  selectedIconType.value = type;
  selectedIcon.value = ''; // Icon tipi değiştiğinde seçimi temizle
  emit('update:iconType', type);
}

// Icon render fonksiyonu
function renderIcon(iconName: string, type: 'mdi' | 'tabler') {
  // Preview için icon render etme
  // MDI için: <v-icon>mdi-xxx</v-icon>
  // Tabler için: Dynamic component import
}
</script>

<template>
  <v-card>
    <v-card-title>
      <div class="d-flex justify-space-between align-center">
        <span>Icon Seç</span>
        <!-- Icon Type Seçimi -->
        <v-btn-toggle v-model="selectedIconType" @update:modelValue="changeIconType" mandatory>
          <v-btn value="tabler" size="small">Tabler</v-btn>
          <v-btn value="mdi" size="small">MDI</v-btn>
        </v-btn-toggle>
      </div>
    </v-card-title>
    
    <v-card-text>
      <!-- Arama -->
      <v-text-field
        v-model="searchQuery"
        prepend-inner-icon="mdi-magnify"
        placeholder="Icon ara..."
        variant="outlined"
        density="compact"
        class="mb-4"
      />
      
      <!-- Icon Grid -->
      <div class="icon-grid">
        <div
          v-for="icon in filteredIcons"
          :key="icon"
          :class="['icon-item', { 'selected': selectedIcon === icon }]"
          @click="selectIcon(icon)"
        >
          <!-- Icon Preview -->
          <div class="icon-preview">
            <v-icon v-if="selectedIconType === 'mdi'" size="32">
              {{ icon }}
            </v-icon>
            <component
              v-else
              :is="icon"
              size="32"
            />
          </div>
          
          <!-- Icon Adı -->
          <div class="icon-name text-caption mt-1">
            {{ formatIconDisplayName(icon, selectedIconType) }}
          </div>
        </div>
      </div>
      
      <!-- Icon Bulunamadı -->
      <div v-if="filteredIcons.length === 0" class="text-center py-8">
        <p class="text-body-2 text-medium-emphasis">Icon bulunamadı</p>
      </div>
    </v-card-text>
  </v-card>
</template>

<style scoped>
.icon-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(100px, 1fr));
  gap: 12px;
  max-height: 400px;
  overflow-y: auto;
}

.icon-item {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  padding: 16px;
  border: 2px solid transparent;
  border-radius: 8px;
  cursor: pointer;
  transition: all 0.2s;
}

.icon-item:hover {
  background-color: rgba(var(--v-theme-primary), 0.1);
  border-color: rgba(var(--v-theme-primary), 0.3);
}

.icon-item.selected {
  background-color: rgba(var(--v-theme-primary), 0.2);
  border-color: rgb(var(--v-theme-primary));
}

.icon-preview {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 48px;
  height: 48px;
}

.icon-name {
  text-align: center;
  word-break: break-word;
  max-width: 100px;
}
</style>
```

### Sidebar'da Icon Render Etme

**Mevcut Durum:**

```vue
<!-- components/lc/Full/vertical-sidebar/Icon.vue -->
<script setup>
const props = defineProps({ item: Object, level: Number });
</script>

<template>
  <component :is="item" size="20" stroke-width="1.5" class="iconClass"></component>
</template>
```

**Güncellenmiş Versiyon:**

```vue
<!-- components/lc/Full/vertical-sidebar/Icon.vue -->
<script setup>
const props = defineProps({ 
  item: Object, // Icon adı veya component
  iconType: String, // 'mdi' veya 'tabler'
  level: Number 
});

// Icon component'ini resolve et
const iconComponent = computed(() => {
  if (!props.item) return null;
  
  // MDI icon ise v-icon kullan
  if (props.iconType === 'mdi') {
    return 'v-icon';
  }
  
  // Tabler icon ise component olarak kullan
  // Icon adını component'e çevir (dynamic import)
  if (typeof props.item === 'string') {
    // Tabler icon component adından import et
    try {
      const iconModule = require(`vue-tabler-icons/${props.item}`);
      return iconModule.default || iconModule;
    } catch {
      return null;
    }
  }
  
  return props.item;
});

const iconSize = computed(() => props.level > 0 ? 14 : 20);
</script>

<template>
  <!-- MDI Icon -->
  <v-icon 
    v-if="iconType === 'mdi' && typeof item === 'string'"
    :size="iconSize"
    class="iconClass"
  >
    {{ item }}
  </v-icon>
  
  <!-- Tabler Icon -->
  <component
    v-else
    :is="iconComponent"
    :size="iconSize"
    stroke-width="1.5"
    class="iconClass"
  />
</template>
```

**Sidebar Item Kullanımı:**

```vue
<!-- components/lc/Full/vertical-sidebar/index.vue -->
<LcFullVerticalSidebarNavItem 
  :item="item" 
  :iconType="item.iconType || 'tabler'"
/>
```

### Icon Tipi Migration Stratejisi

**Mevcut Menu Items için:**

1. Tüm mevcut menu items'lar `iconType` field'ı olmadan (undefined)
2. Backward compatibility: `iconType` yoksa default `"tabler"` kabul et
3. Migration script: Mevcut icon'ları kontrol et, eğer `mdi-` prefix'i varsa `iconType: 'mdi'` ekle

**Örnek Migration:**

```typescript
// Migration script
function migrateMenuItems(items: SideMenuItem[]) {
  return items.map(item => {
    if (item.icon && item.icon.startsWith('mdi-')) {
      return {
        ...item,
        iconType: 'mdi'
      };
    }
    return {
      ...item,
      iconType: item.iconType || 'tabler' // Default tabler
    };
  });
}
```

### Icon Listesi Dosyaları

**Dosya Yapısı:**

```
Mng.Ui/
  utils/
    icons/
      mdi-icons.ts          # MDI icon listesi (statik)
      tabler-icons.ts       # Tabler icon listesi (dinamik veya statik)
      icon-utils.ts         # Icon helper fonksiyonları
```

**Örnek İçerik:**

```typescript
// utils/icons/mdi-icons.ts
export const mdiIconList = [
  'mdi-home',
  'mdi-account',
  'mdi-cog',
  // ... yaygın kullanılan icon'lar
];

export const mdiIconCategories = {
  navigation: ['mdi-home', 'mdi-menu', 'mdi-arrow-left', ...],
  user: ['mdi-account', 'mdi-account-circle', 'mdi-account-group', ...],
  files: ['mdi-file', 'mdi-folder', 'mdi-file-document', ...],
  // ... kategoriler
};
```

```typescript
// utils/icons/tabler-icons.ts
import * as TablerIcons from 'vue-tabler-icons';

// Tüm icon component adlarını çıkar
export const tablerIconList = Object.keys(TablerIcons)
  .filter(key => key.endsWith('Icon') && typeof TablerIcons[key] === 'object')
  .sort();

// Icon component'ini al
export function getTablerIconComponent(iconName: string) {
  return TablerIcons[iconName];
}
```

### Icon Picker Kullanım Senaryosu

**Side Menu Manager Form'da:**

```vue
<!-- components/apps/side-menu-manager/MenuItemForm.vue -->
<v-select
  v-model="formData.iconType"
  label="Icon Tipi"
  :items="['tabler', 'mdi']"
  variant="outlined"
/>

<!-- Icon Picker -->
<IconPicker
  v-model="formData.icon"
  v-model:iconType="formData.iconType"
  :iconType="formData.iconType || 'tabler'"
/>

<!-- Seçili Icon Preview -->
<div v-if="formData.icon" class="mt-2">
  <span class="text-caption">Önizleme:</span>
  <v-icon v-if="formData.iconType === 'mdi'" size="24" class="ml-2">
    {{ formData.icon }}
  </v-icon>
  <component
    v-else
    :is="formData.icon"
    size="24"
    class="ml-2"
  />
</div>
```

### Özet

✅ **Mevcut Icon Kütüphaneleri:**
- Material Design Icons (`@mdi/font`) - 7000+ icon
- Vue Tabler Icons (`vue-tabler-icons`) - 4000+ icon

✅ **Eklenen Field:**
- `iconType`: `'mdi' | 'tabler'` (default: `'tabler'`)

✅ **Icon Picker Component:**
- Icon type seçimi
- Icon listesi ve arama
- Grid görünümü
- Preview ve seçim

✅ **Sidebar Entegrasyonu:**
- Her iki icon tipini destekleyen Icon component
- Backward compatibility (mevcut icon'lar çalışmaya devam eder)

---

## Güncellemeler

### 2025-01-27 (İlk Güncelleme)
- Dokümantasyon oluşturuldu
- Mevcut durum analizi yapıldı
- Planlama yapısı hazırlandı
- Yetkilendirme sistemi planlandı
- DOM element yetkilendirmesi planlandı
- Backend API kullanımı analiz edildi
- Side Menu Manager sayfası planlandı

### 2025-01-27 (Planlama Tamamlama)
- Tüm fazlar detaylandırıldı (9 faz)
- Cache ve performans stratejisi planlandı (localStorage + memory hybrid)
- Error handling ve fallback mekanizması detaylandırıldı
- Real-time updates planı eklendi (SignalR entegrasyonu)
- Menu search/filter planı detaylandırıldı
- Tüm ek özellikler planlandı (templates, bulk operations, export/import, analytics, vb.)
- Icon seçim sistemi detaylandırıldı (MDI ve Tabler desteği)
- Sıralama ve nested header desteği planlandı
- Implementasyon planı ve önceliklendirme oluşturuldu
- Eksikler ve karar verilmesi gereken noktalar listelendi

---

## Implementasyona Başlamak İçin Özet

### Tamamlanan Planlamalar ✅

Tüm planlama aşaması tamamlandı. Aşağıdaki konular detaylı olarak planlandı:

1. ✅ **Dataset Yapısı**: `@side_menu` dataset schema'sı tam olarak tanımlandı
2. ✅ **Yetkilendirme Sistemi**: Group-based permissions, admin bypass, page types
3. ✅ **DOM Element Yetkilendirme**: usePagePermissions composable yaklaşımı
4. ✅ **Side Menu Manager**: 3-panel layout, tree view, form, permission editor
5. ✅ **Icon Sistemi**: MDI ve Tabler icon desteği, Icon Picker component
6. ✅ **Cache Stratejisi**: localStorage + memory hybrid yaklaşımı
7. ✅ **Error Handling**: Fallback menu, retry mekanizması, error scenarios
8. ✅ **Real-Time Updates**: SignalR entegrasyonu planı (eğer mevcutsa)
9. ✅ **Menu Search**: Real-time filtreleme, keyboard shortcuts
10. ✅ **Tüm Faz Planları**: 9 faz, detaylı adımlar, tahmini süreler

### Karar Verilmesi Gereken Konular ⚠️

Implementasyona başlamadan önce aşağıdaki konularda karar verilmelidir:

1. **Icon Listesi Boyutu** (Düşük Öncelik):
   - MDI için 100-200 popüler icon mu, yoksa tüm icon'lar mı?
   - **Öneri**: 100-200 popüler icon ile başla, sonra genişletilebilir

2. **Migration Stratejisi** (Orta Öncelik):
   - Aşamalı geçiş mi (downtime olmadan), tek seferde mi?
   - **Öneri**: Aşamalı geçiş, fallback menu ile test

3. **Bulk Update Endpoint** (Orta Öncelik):
   - MngDataGateway'de bulk update var mı?
   - **Çözüm**: Kontrol edilmeli, yoksa her item için ayrı update

### Kontrol Edilmesi Gerekenler 📋

1. **MngKeeper Group API** (Faz 6 - Permission Editor için):
   - [ ] Grup listesi endpoint'i: `GET /api/v1/groups` veya benzeri
   - [ ] Test script'i ile kontrol edilmeli

2. **MngDataGateway Bulk Update** (Faz 6 - Drag & Drop için):
   - [ ] Bulk update endpoint'i var mı?
   - [ ] Yoksa her item için ayrı update stratejisi uygulanacak

3. **MngHub SignalR** (Faz 7 - Real-Time Updates için):
   - [ ] SignalR entegrasyonu mevcut mu?
   - [ ] Event listening örneği var mı?
   - [ ] Yoksa bu faz atlanacak, sonra eklenebilir

4. **Error Response Format** (Faz 1-3 için):
   - [ ] MngDataGateway API hata formatı nedir?
   - [ ] Test scriptlerinden kontrol edilebilir

### İlk Implementasyon Adımları 🚀

**Hemen Başlanabilir (Kontroller Tamamlandıktan Sonra):**

1. **Faz 1: Dataset ve Temel Altyapı** (2-3 saat)
   - System Datasets kategorisi oluştur
   - @side_menu dataset oluştur
   - Hard-coded menu'yu export et (script oluştur)
   - Menu verilerini yükle

2. **Faz 2: Frontend Store ve Composable'lar** (4-5 saat)
   - Side Menu Store oluştur
   - Page Permissions Composable oluştur
   - Icon utils ve mapping oluştur

3. **Faz 3: Sidebar Entegrasyonu** (5-6 saat)
   - Sidebar component güncelle
   - Icon component güncelle
   - Route guard/middleware oluştur
   - Unauthorized page oluştur

**Toplam MVP Süresi: ~12-14 saat** (1.5-2 iş günü)

### Öncelik Sırasına Göre Fazlar

**Yüksek Öncelik (MVP):**
1. ✅ Faz 1: Dataset ve Temel Altyapı
2. ✅ Faz 2: Frontend Store ve Composable'lar
3. ✅ Faz 3: Sidebar Entegrasyonu

**Yüksek Öncelik (Temel Özellikler):**
4. ✅ Faz 4: Cache ve Performans
5. ✅ Faz 5: DOM Element Yetkilendirme

**Yüksek Öncelik (Yönetim Arayüzü):**
6. ✅ Faz 6: Side Menu Manager Sayfası

**Orta Öncelik (İyileştirmeler):**
7. ✅ Faz 7: Real-Time Updates (SignalR - kontrol edilmeli)
8. ✅ Faz 8: Menu Search/Filter

**Düşük Öncelik (İleri Seviye):**
9. ✅ Faz 9: Diğer İyileştirmeler (templates, bulk operations, vb.)

### Sonuç

**Planlama Durumu**: ✅ %100 TAMAMLANDI

**Hazırlık Durumu**: ✅ İMPLEMENTASYONA HAZIR (Kontroller tamamlandıktan sonra)

**Tahmini Toplam Süre**: ~45-56 saat (5-7 iş günü)

**MVP Süresi**: ~12-14 saat (1.5-2 iş günü)

**İlk Adım**: Kontrolleri tamamla (MngKeeper Group API, MngDataGateway Bulk Update, SignalR) → Faz 1'e başla