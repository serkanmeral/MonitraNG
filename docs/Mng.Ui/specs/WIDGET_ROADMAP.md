# Widget Sistemi Roadmap

**Tarih:** Ocak 2026  
**Durum:** 🚧 Geliştirme Aşamasında  
**Versiyon:** 1.0

---

## 📋 İçindekiler

1. [Mevcut Durum](#mevcut-durum)
2. [Tamamlanan Özellikler](#tamamlanan-özellikler)
3. [Yapılacaklar (Kısa Vadeli)](#yapılacaklar-kısa-vadeli)
4. [Yapılacaklar (Orta Vadeli)](#yapılacaklar-orta-vadeli)
5. [Yapılacaklar (Uzun Vadeli)](#yapılacaklar-uzun-vadeli)
6. [Teknik İyileştirmeler](#teknik-iyileştirmeler)
7. [Kullanıcı Deneyimi İyileştirmeleri](#kullanıcı-deneyimi-iyileştirmeleri)

---

## Mevcut Durum

### ✅ Tamamlanan Temel Altyapı

- **Widget Store & API:** Pinia store ile `@widgets` ve `@widget_categories` CRUD işlemleri
- **Widget List Sayfası:** `/apps/widgets` - Tam CRUD, filtreleme, arama, pagination
- **Widget Form:** Create/Edit formu, validasyon, dataset dropdown
- **Widget Renderer:** Widget tipine göre dinamik render sistemi
- **Dashboard Entegrasyonu:** Widget'ların dashboard'larda gösterilmesi

### ✅ Tamamlanan Widget Tipleri

- **Card Widget (StatCard):**
  - İkon, renk, format (number, currency, percentage)
  - Secondary value desteği
  - Trend göstergesi
  - İkon varyantları (icon, avatar, button)
  - Card varyantları (outlined, flat, elevated)
  - Action button desteği

- **Table Widget:**
  - Vuetify `v-data-table` entegrasyonu
  - Column yapılandırması (field, title, format, sortable)
  - Server-side pagination ve sorting
  - Client-side search/filtering
  - Özel field type desteği (person, personGroups, isArray)
  - Format desteği (text, number, currency, date, boolean)
  - Nested field desteği (publisher.name gibi)

- **Banner Widget:**
  - Banner tipi seçimi (info, warning, success, error, custom)
  - Variant desteği (tonal, filled, outlined, flat)
  - İkon ve resim desteği
  - Action button desteği
  - Dismissible banner
  - Template string desteği ({fieldName} placeholder'ları)
  - Static ve data-based banner desteği

- **Chart Widget:**
  - ApexCharts entegrasyonu
  - Chart tipleri: bar, line, area, pie, donut, radialBar, scatter, bubble
  - X-axis ve Y-axis yapılandırması
  - Multiple series desteği
  - Grouped/aggregated chart desteği
  - Responsive chart desteği
  - Chart options (legend, dataLabels, grid, toolbar, colors)

### ✅ Tamamlanan Yardımcı Araçlar

- **Config Builder:** Card widget için görsel yapılandırma aracı
- **Aggregate Pipeline Builder:** MongoDB aggregation pipeline oluşturma aracı
  - Hazır şablonlar (Toplam Sayı, Toplam Değer, Ortalama, vb.)
  - Stage yönetimi (ekle, sil, sırala)
  - JSON preview
- **Field Dropdown:** Pipeline/dataset'ten otomatik alan tespiti ve dropdown

---

## Tamamlanan Özellikler

### 1. Widget Store & API ✅

- [x] `@widgets` dataset CRUD işlemleri
- [x] `@widget_categories` listesi
- [x] Widget fetch, create, update, delete
- [x] Widget kategorilerine göre filtreleme
- [x] Error handling ve loading states

### 2. Widget List Sayfası ✅

- [x] `/apps/widgets` sayfası
- [x] Tablo görünümü (name, title, category, type, isActive, order, createdAt)
- [x] Arama (name, title)
- [x] Filtreleme (category, type, status)
- [x] Pagination
- [x] CRUD aksiyonları (Create, Edit, Delete, Preview)
- [x] Side menu entegrasyonu
- [x] i18n desteği (tr/en)

### 3. Widget Form ✅

- [x] Create/Edit formu
- [x] Dataset dropdown (otomatik dataset listesi)
- [x] Get Method seçimi (default, query, aggregate, predefined)
- [x] Query match JSON editor
- [x] Aggregate pipeline builder (görsel + JSON)
- [x] Config builder (card widget için)
- [x] Preview özelliği
- [x] Validasyon

### 4. Widget Renderer ✅

- [x] Widget tipine göre dinamik component render
- [x] Data source'dan veri çekme
- [x] Loading state
- [x] Error handling
- [x] Widget prop desteği (preview için)

### 5. Card Widget (StatCard) ✅

- [x] Temel stat card implementasyonu
- [x] İkon desteği (Material Design Icons)
- [x] Renk varyantları (primary, secondary, success, info, warning, error)
- [x] Format desteği (number, currency, percentage)
- [x] Decimal places ayarı
- [x] Secondary value gösterimi
- [x] Trend göstergesi (yüzde değişim)
- [x] İkon varyantları (icon, avatar, button)
- [x] Card varyantları (outlined, flat, elevated)
- [x] Background color desteği
- [x] Action button desteği
- [x] Subtitle desteği

### 6. Config Builder ✅

- [x] Card widget için görsel config builder
- [x] Expansion panels ile kategorize edilmiş ayarlar
- [x] JSON builder toggle
- [x] Tüm card config özelliklerini kapsayan form alanları

### 7. Aggregate Pipeline Builder ✅

- [x] Pipeline stage yönetimi
- [x] Hazır şablonlar (Toplam Sayı, Toplam Değer, Ortalama, Filtrele + Say, Grupla + Topla)
- [x] Stage ekleme/silme/sıralama
- [x] Her stage için JSON editor
- [x] JSON preview
- [x] Builder/JSON toggle

### 8. Field Dropdown ✅

- [x] Dataset schema'dan otomatik alan tespiti
- [x] Pipeline analizi ile output field tespiti
- [x] Autocomplete dropdown (manuel giriş + seçim)
- [x] valueField, secondaryValueField, trendField için dropdown

---

## Yapılacaklar (Kısa Vadeli)

### 1. Chart Widget ✅

**Öncelik:** Yüksek  
**Durum:** ✅ Tamamlandı

#### 1.1 Temel Chart Widget ✅

- [x] Chart widget component (`components/widgets/chart/ChartWidget.vue`)
- [x] Chart tipi seçimi (line, bar, pie, donut, area, radialBar, scatter, bubble)
- [x] ApexCharts entegrasyonu
- [x] Data mapping (x-axis, y-axis, series)
- [x] Chart config hint (WidgetForm'da)
- [x] Responsive chart desteği

#### 1.2 Chart Tipleri ✅

- [x] **Line Chart:** Zaman serisi verileri
- [x] **Bar Chart:** Kategorik veriler (vertical, horizontal, stacked)
- [x] **Pie Chart:** Oran gösterimi
- [x] **Donut Chart:** Oran gösterimi (gelişmiş)
- [x] **Area Chart:** Zaman serisi (dolu alan)
- [x] **Scatter Chart:** İki değişken ilişkisi
- [x] **Radial Bar Chart:** Yüzde göstergeleri

#### 1.3 Chart Config Builder

- [x] Chart tipi seçimi (config JSON)
- [x] X-axis/Y-axis ayarları (config JSON)
- [x] Series yapılandırması (config JSON)
- [x] Renk paleti seçimi (config JSON)
- [x] Legend ayarları (config JSON)
- [x] Tooltip ayarları (otomatik)
- [x] Grid/axis ayarları (config JSON)
- [ ] **Görsel Chart Config Builder** (gelecekte)

#### 1.4 Data Mapping ✅

- [x] Aggregate pipeline'dan chart verisi çıkarma
- [x] X-axis field seçimi
- [x] Y-axis field seçimi (multiple series)
- [x] Label field seçimi (pie/donut/radialBar için)
- [x] Value field seçimi

### 2. Table Widget ✅

**Öncelik:** Yüksek  
**Durum:** ✅ Temel Özellikler Tamamlandı

#### 2.1 Temel Table Widget ✅

- [x] Table widget component (`components/widgets/table/TableWidget.vue`)
- [x] Vuetify `v-data-table` entegrasyonu
- [x] Column yapılandırması (JSON config)
- [x] Server-side pagination desteği
- [x] Server-side sorting desteği
- [x] Client-side filtering/search desteği

#### 2.2 Table Config Builder

- [x] Column yapılandırması (JSON config)
- [x] Column başlıkları
- [x] Column formatları (text, number, currency, date, boolean)
- [x] Sortable column ayarları
- [x] Özel field type desteği (person, personGroups, isArray)
- [x] Nested field desteği (publisher.name)
- [ ] **Görsel Column Builder** (gelecekte)
- [ ] Column genişlikleri (config'de)
- [ ] Drag & drop column sıralama

#### 2.3 Table Özellikleri

- [ ] Row selection (single, multiple)
- [ ] Row actions (edit, delete, custom)
- [ ] Expandable rows
- [ ] Column resizing
- [ ] Column reordering
- [ ] Export (CSV, Excel)

### 3. Banner Widget ✅

**Öncelik:** Orta  
**Durum:** ✅ Tamamlandı

#### 3.1 Temel Banner Widget ✅

- [x] Banner widget component (`components/widgets/banner/BannerWidget.vue`)
- [x] Banner tipi seçimi (info, warning, success, error, custom)
- [x] Variant desteği (tonal, filled, outlined, flat)
- [x] İkon desteği (Material Design Icons)
- [x] Resim desteği
- [x] Action button desteği
- [x] Dismissible banner
- [x] Template string desteği ({fieldName} placeholder'ları)
- [x] Static ve data-based banner desteği

#### 3.2 Banner Config Builder

- [x] Banner tipi seçimi (config JSON)
- [x] Başlık ve içerik (config JSON + hint)
- [x] İkon/resim seçimi (config JSON)
- [x] Action button yapılandırması (config JSON)
- [x] Renk/stil ayarları (config JSON)
- [ ] **Görsel Banner Config Builder** (gelecekte)

### 4. Widget Geliştirmeleri 🔧

#### 4.1 Card Widget İyileştirmeleri

- [ ] Daha fazla card varyantı (gradient, glassmorphism)
- [ ] Animasyon desteği
- [ ] Click event handler
- [ ] Custom action handler
- [ ] Link/route desteği

#### 4.2 Widget Ortak Özellikleri

- [ ] Widget refresh butonu
- [ ] Widget loading skeleton
- [ ] Widget error retry
- [ ] Widget empty state
- [ ] Widget permissions kontrolü

### 5. Config Builder İyileştirmeleri 🔧

#### 5.1 Chart Config Builder

- [x] Chart config hint (WidgetForm'da örnek JSON)
- [ ] **Görsel chart config builder** (gelecekte)
- [ ] Live preview
- [ ] Chart tipi şablonları

#### 5.2 Table Config Builder

- [x] Table config hint (WidgetForm'da örnek JSON)
- [ ] **Görsel column builder** (gelecekte)
- [ ] Drag & drop column sıralama
- [ ] Column preview

#### 5.3 Banner Config Builder

- [x] Banner config hint (WidgetForm'da örnek JSON)
- [ ] **Görsel banner config builder** (gelecekte)

#### 5.3 Pipeline Builder İyileştirmeleri

- [ ] Daha fazla hazır şablon
- [ ] Stage-specific form alanları (görsel)
- [ ] Pipeline validation
- [ ] Pipeline test/execute (preview)
- [ ] Pipeline import/export

---

## Yapılacaklar (Orta Vadeli)

### 1. Widget Kütüphanesi Genişletme 📚

#### 1.1 Yeni Widget Tipleri

- [ ] **Gauge Widget:** Progress/percentage göstergesi
- [ ] **Progress Widget:** İlerleme çubuğu
- [ ] **List Widget:** Özel liste görünümü
- [ ] **Map Widget:** Harita görünümü (Google Maps, Leaflet)
- [ ] **Calendar Widget:** Takvim görünümü
- [ ] **Timeline Widget:** Zaman çizelgesi
- [ ] **KPI Widget:** KPI kartları
- [ ] **Heatmap Widget:** Isı haritası

#### 1.2 Widget Kombinasyonları

- [ ] Widget grouping
- [ ] Widget tabs
- [ ] Widget accordion
- [ ] Widget carousel

### 2. Widget Veri İşleme 🗄️

#### 2.1 Gelişmiş Data Source

- [ ] **Multiple Data Sources:** Bir widget'ta birden fazla data source
- [ ] **Data Joining:** İki dataset'i birleştirme
- [ ] **Data Transformation:** Client-side veri dönüşümü
- [ ] **Data Caching:** Widget verilerini cache'leme
- [ ] **Data Refresh:** Otomatik yenileme (polling)

#### 2.2 Real-time Data

- [ ] SignalR entegrasyonu
- [ ] WebSocket desteği
- [ ] Real-time widget güncellemeleri

### 3. Widget Etkileşimleri 🎯

#### 3.1 Widget Filtering

- [ ] Global filter (tüm widget'ları etkiler)
- [ ] Widget-to-widget filtering
- [ ] Date range picker
- [ ] Custom filter components

#### 3.2 Widget Actions

- [ ] Widget click event
- [ ] Widget drill-down
- [ ] Widget navigation
- [ ] Widget export

### 4. Widget Performans Optimizasyonu ⚡

#### 4.1 Lazy Loading

- [ ] Widget lazy loading (viewport'a girdiğinde yükle)
- [ ] Data lazy loading
- [ ] Image lazy loading

#### 4.2 Caching

- [ ] Widget data caching (localStorage, IndexedDB)
- [ ] Widget config caching
- [ ] Cache invalidation stratejisi

#### 4.3 Optimizasyon

- [ ] Widget memoization
- [ ] Data pagination optimizasyonu
- [ ] Chart rendering optimizasyonu
- [ ] Table virtualization (büyük tablolar için)

### 5. Widget Özelleştirme 🎨

#### 5.1 Widget Styling

- [ ] Custom CSS desteği
- [ ] Theme desteği (light/dark)
- [ ] Widget-specific themes
- [ ] CSS variables desteği

#### 5.2 Widget Layout

- [ ] Widget size ayarları
- [ ] Widget position ayarları
- [ ] Widget responsive breakpoints
- [ ] Widget grid system

---

## Yapılacaklar (Uzun Vadeli)

### 1. Widget Marketplace 🏪

- [ ] Widget template library
- [ ] Widget sharing
- [ ] Widget import/export
- [ ] Widget versioning
- [ ] Widget ratings/reviews

### 2. Widget Builder (Visual) 🛠️

- [ ] Drag & drop widget builder
- [ ] Visual widget editor
- [ ] Widget preview (live)
- [ ] Widget template system
- [ ] Widget code generation

### 3. Widget Analytics 📊

- [ ] Widget usage analytics
- [ ] Widget performance metrics
- [ ] Widget error tracking
- [ ] Widget user behavior tracking

### 4. Widget Permissions & Security 🔒

- [ ] Widget-level permissions
- [ ] Data source permissions
- [ ] Widget sharing permissions
- [ ] Widget encryption (sensitive data)

### 5. Widget Testing 🧪

- [ ] Widget unit tests
- [ ] Widget integration tests
- [ ] Widget E2E tests
- [ ] Widget performance tests
- [ ] Widget accessibility tests

### 6. Widget Documentation 📖

- [ ] Widget API documentation
- [ ] Widget usage guides
- [ ] Widget examples
- [ ] Widget best practices
- [ ] Widget troubleshooting guide

---

## Teknik İyileştirmeler

### 1. Code Quality

- [ ] TypeScript strict mode
- [ ] ESLint rules
- [ ] Code formatting (Prettier)
- [ ] Code review checklist
- [ ] Unit test coverage (>80%)

### 2. Architecture

- [ ] Widget plugin system
- [ ] Widget registry
- [ ] Widget factory pattern
- [ ] Widget lifecycle hooks
- [ ] Widget event system

### 3. Performance

- [ ] Bundle size optimization
- [ ] Tree shaking
- [ ] Code splitting
- [ ] Lazy loading routes
- [ ] Image optimization

### 4. Accessibility

- [ ] ARIA labels
- [ ] Keyboard navigation
- [ ] Screen reader support
- [ ] Color contrast
- [ ] Focus management

### 5. Internationalization

- [ ] Tüm widget'lar için i18n
- [ ] Date/time formatting
- [ ] Number formatting
- [ ] Currency formatting
- [ ] RTL (Right-to-Left) desteği

---

## Kullanıcı Deneyimi İyileştirmeleri

### 1. Widget Form UX

- [ ] Form wizard (step-by-step)
- [ ] Form validation (real-time)
- [ ] Form auto-save
- [ ] Form templates
- [ ] Form import/export

### 2. Widget List UX

- [ ] Grid view option
- [ ] Widget preview thumbnails
- [ ] Widget search (advanced)
- [ ] Widget bulk operations
- [ ] Widget favorites

### 3. Widget Preview

- [ ] Full-screen preview
- [ ] Device preview (mobile, tablet, desktop)
- [ ] Preview with sample data
- [ ] Preview sharing

### 4. Widget Help

- [ ] Contextual help
- [ ] Tooltips
- [ ] Inline documentation
- [ ] Video tutorials
- [ ] FAQ section

---

## Öncelik Matrisi

### 🔴 Yüksek Öncelik (Hemen Yapılmalı)

1. ✅ Chart Widget (Line, Bar, Pie, Donut, RadialBar, Scatter) - **TAMAMLANDI**
2. ✅ Table Widget - **TAMAMLANDI**
3. ✅ Banner Widget - **TAMAMLANDI**
4. Widget performans optimizasyonu
5. Widget error handling iyileştirmeleri
6. Görsel Config Builder'lar (Chart, Table, Banner)

### 🟡 Orta Öncelik (Yakın Zamanda)

1. Widget filtering
2. Widget caching
3. Table widget gelişmiş özellikler (row selection, actions, export)
4. Chart widget gelişmiş özellikler (zoom, brush, drill-down)

### 🟢 Düşük Öncelik (Gelecekte)

1. Widget marketplace
2. Visual widget builder
3. Widget analytics
4. Widget testing framework

---

## Bağımlılıklar

### Backend (MngDataGateway)

- [x] `@widgets` dataset ✅
- [x] `@widget_categories` dataset ✅
- [ ] Widget permissions API
- [ ] Widget analytics API
- [ ] Widget export API

### Frontend (Mng.Ui)

- [x] Dashboard system ✅
- [x] Widget store ✅
- [x] Dataset store ✅
- [x] Chart library (ApexCharts) ✅
- [ ] Export library (Excel, CSV)

---

## Notlar

- **Widget Tipi Genişletilebilirliği:** Yeni widget tipleri eklemek için `WidgetRenderer.vue` ve ilgili component'leri genişletmek yeterli.
- **Config Builder Genişletilebilirliği:** Her widget tipi için kendi config builder'ı eklenebilir.
- **Data Source Genişletilebilirliği:** Yeni data source tipleri (ör. external API) eklenebilir.
- **Performans:** Widget sayısı arttıkça lazy loading ve caching kritik hale gelecek.

---

## İlgili Dokümanlar

- [WIDGET_LIBRARY_SPEC.md](./WIDGET_LIBRARY_SPEC.md) - Widget kütüphanesi spesifikasyonu
- [DYNAMIC_DASHBOARD_SPEC.md](./DYNAMIC_DASHBOARD_SPEC.md) - Dashboard spesifikasyonu
- [DASHBOARD_TASKS_SUMMARY.md](./DASHBOARD_TASKS_SUMMARY.md) - Dashboard görev özeti
- [WIDGET_TEST_GUIDE.md](./WIDGET_TEST_GUIDE.md) - Widget test rehberi

---

**Son Güncelleme:** Ocak 2026  
**Güncelleyen:** AI Assistant  
**Versiyon:** 1.1

## Son Güncellemeler (Ocak 2026)

### ✅ Tamamlanan Özellikler

1. **Table Widget:** Server-side pagination, sorting, client-side search, özel field type desteği
2. **Banner Widget:** Template string desteği, multiple variants, action buttons
3. **Chart Widget:** 8 farklı chart tipi (bar, line, area, pie, donut, radialBar, scatter, bubble), multiple series, grouped charts
4. **Dokümantasyon:** tst_books dataset için kapsamlı örnekler (banner, chart, query examples)

### 📝 Eklenen Dokümanlar

- `docs/Mng.Ui/tst_book_data/banner_widget_examples.md` - Banner widget örnekleri
- `docs/Mng.Ui/tst_book_data/chart_widget_examples.md` - Chart widget örnekleri
- `docs/Mng.Ui/tst_book_data/widget_query_examples.md` - Query (Match JSON) örnekleri
