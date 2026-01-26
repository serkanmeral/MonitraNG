# Dynamic Dashboard Builder Mekanizması

**Tarih:** Ocak 2026  
**Durum:** 📋 Planlama  
**Hedef:** Kullanıcı arayüzünde dashboard oluşturma, düzenleme ve layout yönetimi akışı.

**İlişkili dokümanlar:**
- [DYNAMIC_DASHBOARD_SPEC.md](./DYNAMIC_DASHBOARD_SPEC.md) — Dashboard dataset yapısı ve layout şeması
- [WIDGET_LIBRARY_SPEC.md](./WIDGET_LIBRARY_SPEC.md) — Widget tanımları

---

## 1. Genel Akış

### 1.1 Dashboard Yönetimi Sayfaları

| Sayfa | Route | Açıklama |
|-------|-------|----------|
| **Dashboard Listesi** | `/apps/dashboards` | Tüm dashboard'ları listeler, yeni oluşturma, düzenleme, silme |
| **Dashboard Builder** | `/apps/dashboards/:id/edit` | Dashboard oluşturma/düzenleme formu + layout editor |
| **Dashboard Görüntüleme** | `/dashboards/:slug` | Kullanıcıya sunulan dashboard (runtime render) |

### 1.2 Oluşturma Akışı

```
1. Dashboard Listesi → "Yeni Dashboard" butonu
2. Dashboard Builder açılır:
   a. Temel Bilgiler (name, title, description, slug)
   b. Layout Editor (satır/sütun ekleme, widget yerleştirme)
   c. Önizleme (isteğe bağlı)
3. Kaydet → @dashboards dataset'ine kaydedilir
4. Dashboard görüntüleme sayfasında render edilir
```

---

## 2. Dashboard Builder UI Tasarımı

### 2.1 Sayfa Yapısı (Split View)

```
┌─────────────────────────────────────────────────────────┐
│  Dashboard Builder - "Analitik Dashboard"               │
├──────────────────┬──────────────────────────────────────┤
│  Sol Panel       │  Sağ Panel (Layout Editor)          │
│  (Form)          │  (Visual Builder)                   │
│                  │                                      │
│  [Temel Bilgiler]│  ┌──────────────────────────────┐  │
│  - Name          │  │  Row 1                       │  │
│  - Title         │  │  ┌─────────┬──────────────┐  │  │
│  - Description   │  │  │ Widget  │  Widget      │  │  │
│  - Slug          │  │  │ (8 cols)│  (4 cols)    │  │  │
│  - isDefault     │  │  └─────────┴──────────────┘  │  │
│  - isActive      │  │                                │  │
│                  │  │  Row 2                       │  │
│  [Actions]       │  │  ┌─────────┬──────────────┐  │  │
│  [Kaydet]        │  │  │ Widget  │  Widget      │  │  │
│  [İptal]         │  │  │ (4 cols)│  (8 cols)    │  │  │
│  [Önizleme]      │  │  └─────────┴──────────────┘  │  │
│                  │  │                                │  │
│                  │  │  [+ Row Ekle]                  │  │
│                  │  └──────────────────────────────┘  │  │
│                  │                                      │
│                  │  [Widget Paleti] (Alt kısım)         │
│                  │  ┌─────┬─────┬─────┬─────┐          │
│                  │  │Card │Chart│Table│Banner│         │
│                  │  └─────┴─────┴─────┴─────┘          │
└──────────────────┴──────────────────────────────────────┘
```

### 2.2 Sol Panel: Temel Bilgiler Formu

**Form alanları:**
- **Name** (text, required, unique) — `analytical`
- **Title** (text, required) — `Analitik Dashboard`
- **Description** (textarea, optional)
- **Slug** (text, optional) — Route için. Boşsa `name` kullanılır
- **isDefault** (checkbox) — Varsayılan dashboard mu?
- **isActive** (checkbox, default: true)

**Actions:**
- **Kaydet** — `@dashboards` dataset'ine POST/PUT
- **İptal** — Geri dön
- **Önizleme** — Modal veya yeni tab'da preview (runtime render)

---

## 3. Layout Editor (Sağ Panel)

### 3.1 Görsel Builder Yaklaşımı

**Temel prensip:** Kullanıcı **satırlar** ekler, her satıra **sütunlar** ekler, her sütuna **widget** yerleştirir.

### 3.2 UI Bileşenleri

#### 3.2.1 Row Container (Satır)

```
┌─────────────────────────────────────────────────────┐
│  Row 1                                    [⋮] [×]   │
│  ┌─────────────────┬─────────────────────────────┐  │
│  │  Col 1 (8 cols) │  Col 2 (4 cols)            │  │
│  │  [Widget Card]  │  [Widget Chart]            │  │
│  │  [⋮] [×]        │  [⋮] [×]                   │  │
│  └─────────────────┴─────────────────────────────┘  │
│  [+ Sütun Ekle]                                      │
└─────────────────────────────────────────────────────┘
```

**Row actions:**
- **⋮ (Menu):** Row ayarları (align, justify, no-gutters, dense)
- **× (Sil):** Satırı sil
- **↑↓ (Sürükle):** Satır sırasını değiştir (drag handle)

#### 3.2.2 Column Container (Sütun)

```
┌─────────────────────────────────┐
│  Col 1 (8 cols)        [⋮] [×] │
│  ┌───────────────────────────┐  │
│  │  [Widget: Sales Overview] │  │
│  │  (Card widget)            │  │
│  └───────────────────────────┘  │
│  Responsive:                    │
│  xs: 12 | sm: 12 | md: 8 | lg: 8│
└─────────────────────────────────┘
```

**Column actions:**
- **⋮ (Menu):** Sütun ayarları (span değerleri, align-self, order)
- **× (Sil):** Sütunu sil
- **Widget seç/değiştir:** Widget picker açılır

**Responsive span editor:**
- Input'lar: `xs` (cols), `sm`, `md`, `lg`, `xl`
- Her biri 1-12 arası, toplam kontrolü (aynı satırda 12'yi geçmemeli)

#### 3.2.3 Widget Placeholder

**Widget yoksa:**
```
┌─────────────────────────┐
│  [+ Widget Ekle]        │
│  (Boş sütun)            │
└─────────────────────────┘
```

**Widget varsa:**
```
┌─────────────────────────┐
│  [Widget: Sales Overview]│
│  Type: Card             │
│  [Düzenle] [Kaldır]     │
└─────────────────────────┘
```

### 3.3 Widget Picker (Modal/Drawer)

**Widget seçimi için:**

```
┌─────────────────────────────────────────────┐
│  Widget Seç                                [×]│
├─────────────────────────────────────────────┤
│  [Ara...]                                   │
│  Kategori: [Tümü ▼]                        │
│  ┌───────────────────────────────────────┐  │
│  │  📊 Sales Overview Card               │  │
│  │  Type: Card | Category: Statistics    │  │
│  │  [Seç]                                 │  │
│  ├───────────────────────────────────────┤  │
│  │  📈 Total Sales Chart                  │  │
│  │  Type: Chart | Category: Analytics    │  │
│  │  [Seç]                                 │  │
│  ├───────────────────────────────────────┤  │
│  │  📋 Recent Tasks Table                 │  │
│  │  Type: Table | Category: Data         │  │
│  │  [Seç]                                 │  │
│  └───────────────────────────────────────┘  │
└─────────────────────────────────────────────┘
```

**Filtreleme:**
- **Arama:** Widget adı/başlığına göre
- **Kategori:** `@widget_categories` dropdown
- **Tip:** Card, Chart, Table, Banner

**Seçim:** Widget `__dataId`'si sütuna atanır.

### 3.4 Row/Column Ekleme

**Row ekleme:**
- **"+ Row Ekle"** butonu (en altta veya satırlar arası)
- Yeni boş satır eklenir, içinde varsayılan 1 sütun (12 cols)

**Column ekleme:**
- Her satırda **"+ Sütun Ekle"** butonu
- Yeni boş sütun eklenir (varsayılan span: 12)
- **Kısıtlama:** Aynı satırda toplam span 12'yi geçmemeli (validation)

**Nested row (ileride):**
- Sütun içinde "İç Satır Ekle" seçeneği
- Sütun `rows` array'i alır, `widgetId` yerine nested layout

---

## 4. Layout State Yönetimi

### 4.1 Vue Component Yapısı

```typescript
// DashboardBuilder.vue
interface DashboardBuilderState {
  // Form state
  form: {
    name: string;
    title: string;
    description?: string;
    slug?: string;
    isDefault: boolean;
    isActive: boolean;
  };
  
  // Layout state (row-based)
  layout: {
    type: 'rows';
    rows: LayoutRow[];
  };
  
  // UI state
  selectedRowIndex?: number;
  selectedColIndex?: { row: number; col: number };
  widgetPickerOpen: boolean;
  widgetPickerTarget?: { row: number; col: number };
}
```

### 4.2 Layout Manipülasyon Fonksiyonları

```typescript
// Row operations
addRow(): void;
removeRow(index: number): void;
moveRow(from: number, to: number): void;
updateRowProps(index: number, props: Partial<LayoutRow>): void;

// Column operations
addColumn(rowIndex: number): void;
removeColumn(rowIndex: number, colIndex: number): void;
updateColumnSpan(
  rowIndex: number, 
  colIndex: number, 
  breakpoint: 'span' | 'spanSm' | 'spanMd' | 'spanLg' | 'spanXl',
  value: number
): void;
assignWidget(rowIndex: number, colIndex: number, widgetId: string): void;
removeWidget(rowIndex: number, colIndex: number): void;
```

### 4.3 Validation

**Layout validation:**
- Her satırda sütun span'leri toplamı ≤ 12 (her breakpoint için)
- `widgetId` geçerli `@widgets` kaydına referans etmeli
- En az 1 satır olmalı

**Form validation:**
- `name` unique olmalı (backend'de kontrol)
- `slug` unique olmalı (opsiyonel, backend'de kontrol)

---

## 5. Kaydetme İşlemi

### 5.1 Save Flow

```
1. Form validation (client-side)
2. Layout validation (span toplamları, widget referansları)
3. Layout'u JSON'a serialize et
4. POST /api/v1/data/@dashboards (yeni) veya PUT /api/v1/data/@dashboards/{id} (düzenleme)
5. Başarılı → Dashboard listesine yönlendir veya önizleme göster
6. Hata → Hata mesajı göster
```

### 5.2 API Request

**Yeni dashboard:**
```json
POST /api/v1/data/@dashboards
{
  "name": "analytical",
  "title": "Analitik Dashboard",
  "description": "Satış ve ziyaretçi istatistikleri",
  "slug": "analytical",
  "layout": {
    "type": "rows",
    "rows": [
      {
        "cols": [
          { "widgetId": "widget-001", "span": 12, "spanLg": 8 },
          { "widgetId": "widget-002", "span": 12, "spanLg": 4 }
        ]
      }
    ]
  },
  "isDefault": true,
  "isActive": true,
  "order": 1
}
```

---

## 6. Önizleme (Preview)

### 6.1 Preview Modal/Page

**Modal veya yeni tab'da:**
- Dashboard runtime render'ı (`/dashboards/:slug` benzeri)
- Layout ve widget'lar gerçek verilerle gösterilir
- "Kapat" ile builder'a dönülür

**Kullanım:**
- Layout düzenlerken anlık önizleme (opsiyonel, performans için debounce)
- "Önizleme" butonu ile tam önizleme

---

## 7. Dashboard Listesi Sayfası

### 7.1 Liste Görünümü

```
┌─────────────────────────────────────────────────────┐
│  Dashboards                    [+ Yeni Dashboard] │
├─────────────────────────────────────────────────────┤
│  [Ara...]  Aktif: [Tümü ▼]                         │
│                                                      │
│  ┌──────────────────────────────────────────────┐  │
│  │  📊 Analitik Dashboard                       │  │
│  │  analytical | Varsayılan: ✓ | Aktif: ✓      │  │
│  │  [Düzenle] [Önizle] [Sil]                    │  │
│  ├──────────────────────────────────────────────┤  │
│  │  📈 Modern Dashboard                          │  │
│  │  modern | Aktif: ✓                           │  │
│  │  [Düzenle] [Önizle] [Sil]                    │  │
│  └──────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────┘
```

**Actions:**
- **Düzenle:** Builder'a yönlendir (`/apps/dashboards/:id/edit`)
- **Önizle:** `/dashboards/:slug` sayfasını yeni tab'da açar
- **Sil:** Onay modal → DELETE request

---

## 8. Alternatif Yaklaşımlar (İleride)

### 8.1 Drag-and-Drop Builder

**GridStack veya benzeri:**
- Widget'ları sürükle-bırak ile yerleştirme
- Yeniden boyutlandırma (resize handles)
- Daha interaktif, ancak daha karmaşık implementasyon

**Layout type:** `grid` (şu an `rows` kullanıyoruz)

### 8.2 Template-Based

**Hazır şablonlar:**
- "2 Kolon", "3 Kolon", "Sidebar + Main", "Masonry"
- Kullanıcı şablon seçer, widget'ları slot'lara atar
- Daha hızlı oluşturma, daha az esneklik

---

## 9. Özet ve Öncelikler

### 9.1 MVP (Minimum Viable Product)

1. ✅ **Dashboard Listesi** — CRUD listesi
2. ✅ **Dashboard Builder** — Form + basit layout editor (row/column ekleme, widget atama)
3. ✅ **Layout Editor** — Görsel row/column yönetimi, span ayarları
4. ✅ **Widget Picker** — Widget seçimi modal
5. ✅ **Kaydetme** — `@dashboards` dataset'ine kayıt
6. ✅ **Runtime Render** — Dashboard görüntüleme sayfası

### 9.2 İleride Eklenebilecekler

- 🔮 Drag-and-drop layout (GridStack)
- 🔮 Önizleme (anlık veya modal)
- 🔮 Layout template'leri
- 🔮 Nested rows (iç içe satırlar)
- 🔮 Layout import/export (JSON)
- 🔮 Dashboard kopyalama

---

## 10. Teknik Detaylar

### 10.1 Component Hiyerarşisi

```
DashboardBuilder.vue
├── DashboardForm.vue (sol panel)
│   └── Form fields (name, title, description, slug, isDefault, isActive)
│
└── LayoutEditor.vue (sağ panel)
    ├── RowList.vue
    │   └── RowItem.vue (her satır)
    │       ├── ColumnList.vue
    │       │   └── ColumnItem.vue (her sütun)
    │       │       ├── WidgetPlaceholder.vue (boşsa)
    │       │       └── WidgetCard.vue (widget varsa)
    │       └── AddColumnButton.vue
    └── AddRowButton.vue
    └── WidgetPickerModal.vue
```

### 10.2 Store/State

**Pinia store:** `stores/apps/dashboard.ts`

```typescript
interface DashboardStore {
  dashboards: Dashboard[];
  currentDashboard: Dashboard | null;
  loading: boolean;
  
  fetchDashboards(): Promise<void>;
  fetchDashboardById(id: string): Promise<void>;
  createDashboard(data: CreateDashboardDto): Promise<void>;
  updateDashboard(id: string, data: UpdateDashboardDto): Promise<void>;
  deleteDashboard(id: string): Promise<void>;
}
```

---

## 11. Kullanıcı Deneyimi Senaryoları

### Senaryo 1: Yeni Dashboard Oluşturma

1. `/apps/dashboards` → "Yeni Dashboard"
2. Builder açılır
3. Sol panel: Name: `my-dashboard`, Title: `My Dashboard`
4. Sağ panel: "+ Row Ekle" → boş satır
5. Satırda "+ Sütun Ekle" → 2 sütun
6. İlk sütuna tıkla → Widget Picker açılır → "Sales Overview" seç
7. İkinci sütuna tıkla → "Total Sales" seç
8. Span ayarları: İlk sütun `lg: 8`, ikinci `lg: 4`
9. "Kaydet" → Dashboard oluşturulur
10. Liste sayfasına dönülür

### Senaryo 2: Dashboard Düzenleme

1. Liste sayfasında "Düzenle"
2. Builder açılır, mevcut layout yüklenir
3. Yeni satır ekle, widget ekle
4. Mevcut widget'ı değiştir
5. Span değerlerini güncelle
6. "Kaydet" → Güncellenir

---

**Sonuç:** Bu mekanizma ile kullanıcılar görsel olarak dashboard oluşturup düzenleyebilir, widget'ları satır/sütun yapısında yerleştirebilir. Başlangıçta basit row-based yaklaşım, ileride drag-and-drop ile genişletilebilir.
