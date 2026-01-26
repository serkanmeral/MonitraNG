# Dynamic Dashboard Spesifikasyonu

**Tarih:** Ocak 2026  
**Durum:** 📋 Planlama  
**Hedef:** Dinamik dashboard tanımları (`@dashboards`), layout yapılandırması ve widget yerleşimi. Widget'lar bu dashboard'lar içinde konumlandırılacak.

**İlişkili doküman:** [WIDGET_LIBRARY_SPEC.md](./WIDGET_LIBRARY_SPEC.md) — Widget tanımları (`@widgets`), kategoriler (`@widget_categories`) ve data source.

---

## 1. Genel Bakış

- **Dashboard:** Kullanıcıya sunulan bir sayfa; widget'ların bir araya geldiği alan.
- **Layout:** Dashboard üzerinde widget'ların **konumunu** ve **responsive davranışını** tanımlar.
- **Widget:** `@widgets` dataset'inde tanımlı bileşen; layout içinde `widgetId` ile referans edilir.

**Akış:** `@dashboards` → dashboard kaydı → `layout` → satırlar/sütunlar → her sütunda `widgetId` → `@widgets` kaydı.

**Uygulama sırası önerisi:**
1. `@dashboards` dataset'i + layout şeması (bu doküman)
2. `@widget_categories` ve `@widgets` (Widget Library Spec)
3. Runtime: dashboard sayfası layout'u okuyup widget'ları render eder

---

## 2. @dashboards Dataset Schema

**Amaç:** Dashboard meta bilgisi ve layout tanımını saklamak.

### 2.1 Field'lar

| Field        | Tip     | Zorunlu | Açıklama |
|-------------|---------|---------|----------|
| `name`      | text    | evet    | Dashboard adı (unique, örn. `analytical`) |
| `title`     | text    | evet    | Görünen başlık (örn. "Analitik Dashboard") |
| `description` | text  | hayır   | Açıklama |
| `slug`      | text    | hayır   | Route için path (örn. `analytical` → `/dashboards/analytical`). Boşsa `name` kullanılabilir. |
| `layout`    | object  | evet    | Layout tanımı (aşağıda) |
| `permissions` | object | hayır   | Kullanıcı grubu yetkilendirmesi (aşağıda). Boş/null = herkes erişebilir. |
| `sideMenuConfig` | object | hayır | Side Menu entegrasyonu (Automated Forms gibi). Dashboard menüye eklenebilir. |
| `isDefault` | bool    | hayır   | Varsayılan dashboard mu? (default: false) |
| `isActive`  | bool    | evet    | Aktif mi? (default: true) |
| `order`     | number  | hayır   | Sıralama (default: 0) |

### 2.2 Örnek Schema (DG dataset create)

```json
{
  "name": "@dashboards",
  "description": "Dinamik dashboard tanımları",
  "forceSchema": true,
  "logging": "none",
  "publish_mode": "none",
  "fields": [
    { "fieldType": "text", "name": "name", "title": "Dashboard Adı", "mandatory": true, "unique": true },
    { "fieldType": "text", "name": "title", "title": "Başlık", "mandatory": true },
    { "fieldType": "text", "name": "description", "title": "Açıklama", "mandatory": false },
    { "fieldType": "text", "name": "slug", "title": "Route Slug", "mandatory": false },
    { "fieldType": "object", "name": "layout", "title": "Layout", "mandatory": true },
    { "fieldType": "object", "name": "permissions", "title": "Yetkilendirme", "mandatory": false },
    { "fieldType": "object", "name": "sideMenuConfig", "title": "Side Menu Ayarları", "mandatory": false },
    { "fieldType": "bool", "name": "isDefault", "title": "Varsayılan", "mandatory": false, "defaultValue": false },
    { "fieldType": "bool", "name": "isActive", "title": "Aktif", "mandatory": true, "defaultValue": true },
    { "fieldType": "number", "name": "order", "title": "Sıra", "mandatory": false, "defaultValue": 0 }
  ],
  "indexList": [
    { "name": "idx_name", "fields": { "name": 1 }, "unique": true },
    { "name": "idx_slug", "fields": { "slug": 1 } },
    { "name": "idx_order", "fields": { "order": 1 } }
  ]
}
```

---

## 2.3 Kullanıcı Grubu Yetkilendirmesi (`permissions`)

Dashboard’a **grup bazlı erişim** tanımlanır. Yapı, Side Menu ve DG dataset yetkilendirmesine benzer.

### 2.3.1 Yapı

```ts
interface DashboardPermissions {
  view?: { groups: string[]; users?: string[] };  // Dashboard görüntüleme (runtime sayfa)
  edit?: { groups: string[]; users?: string[] };  // Dashboard düzenleme (builder)
}
```

- **`view`:** Dashboard görüntüleme sayfası (`/dashboards/:slug`). Bu gruplar/kullanıcılar dashboard’u görebilir.
- **`edit`:** Dashboard Builder’da düzenleme. Bu gruplar/kullanıcılar layout’u değiştirebilir.
- **`groups`:** MngKeeper grup adları (örn. `managers`, `editors`).
- **`users`:** MngKeeper kullanıcı ID’leri (opsiyonel).
- **Boş / `null` / tanımsız:** İlgili yetki kontrolü yapılmaz; herkes erişebilir.

### 2.3.2 Kontrol Akışı

1. **Görüntüleme:** `/dashboards/:slug` açılırken kullanıcının `user_groups` (ve varsa `users`) ile `permissions.view` karşılaştırılır. Yetkisi yoksa 403 / yetkisiz sayfa.
2. **Düzenleme:** Dashboard Builder açılırken `permissions.edit` kontrol edilir. Yetkisi yoksa builder’a erişim engellenir.
3. **Admin bypass:** `isAdmin === true` ise tüm kontroller bypass edilir (mevcut Side Menu mantığıyla uyumlu).

### 2.3.3 Örnek

```json
{
  "permissions": {
    "view": { "groups": ["managers", "editors", "viewers"], "users": [] },
    "edit": { "groups": ["managers", "editors"], "users": [] }
  }
}
```

---

## 2.4 Side Menu Entegrasyonu (`sideMenuConfig`)

Automated Forms’ta olduğu gibi, dashboard’lar **Side Menu’ye eklenebilir**. Menu item `to` değeri `/dashboards/{slug}` olur.

### 2.4.1 Yapı

```ts
interface DashboardSideMenuConfig {
  enabled: boolean;           // Side menu’ye eklensin mi?
  menuItemId?: string;        // @side_menu kaydı __dataId (eklendiyse)
  routeType: 'path' | 'dashboard';  // 'path' = tek dashboard path, 'dashboard' = dropdown ile seçim
  routePath?: string;         // routeType='path' ise: /dashboards/{slug}
}
```

- **`enabled`:** Dashboard menüde gösterilecek mi?
- **`menuItemId`:** Side Menu’deki ilgili item’ın `__dataId`’si (entegrasyon takibi için).
- **`routeType`:**  
  - **`path`:** Tek bir dashboard’a sabit link. `routePath` = `/dashboards/analytical` gibi.  
  - **`dashboard`:** Özel bir “Dashboard seç” sayfası veya dropdown; kullanıcı dashboard seçer, o dashboard’a gider.
- **`routePath`:** `routeType === 'path'` iken kullanılır.

### 2.4.2 Dashboard’ı Menüye Ekleme (Side Menu Manager)

- **Yeni menu item** eklenir.
- **Route / link tipi:** “Dashboard” (veya “Path” + dashboard path).
- **Dashboard seçici:** Açılır listeden bir dashboard seçilir → `to` = `/dashboards/{slug}` atanır.
- **Yetkilendirme:** Menu item’da `permissions.groups` (view, create, update, delete, export) tanımlanır; mevcut Side Menu yetki sistemi kullanılır.

**Önemli:**  
- **Menü görünürlüğü** → Menu item `permissions` (Side Menu).  
- **Dashboard sayfa erişimi** → Dashboard `permissions.view` (@dashboards).  
İkisi birlikte kullanılır. Menüde görünüp dashboard’a tıklayınca `permissions.view` ile tekrar kontrol yapılır.

### 2.4.3 Örnek

```json
{
  "sideMenuConfig": {
    "enabled": true,
    "menuItemId": "menu-item-001",
    "routeType": "path",
    "routePath": "/dashboards/analytical"
  }
}
```

---

## 3. Dynamic Layout — Yaklaşımlar ve Öneri

### 3.1 Kısa Karşılaştırma

| Yaklaşım        | Esneklik | Vuetify uyumu | Drag‑and‑drop | Karmaşıklık | Öneri |
|-----------------|----------|---------------|----------------|-------------|-------|
| **Row-based**   | Orta     | Doğrudan      | Zor            | Düşük       | ✅ Öncelikli |
| **Grid (GridStack)** | Yüksek | Uyarlanabilir | Kolay          | Orta        | 🔮 Sonraki aşama |
| **Template**    | Düşük    | İyi           | Hayır          | Çok düşük   | Opsiyonel |

### 3.2 Önerilen Strateji

1. **Öncelik: Row-based layout**  
   - Vuetify `v-row` / `v-col` ile bire bir eşlenir.  
   - Mevcut analytical, modern, minimal dashboard’lar da satır/sütun yapısında.  
   - JSON’a kolay serialize, dataset’te `layout` object olarak saklanabilir.

2. **İleride: Grid layout**  
   - Drag-and-drop + resize ihtiyacı olursa `layoutType: 'grid'` ve GridStack (veya benzeri) eklenebilir.  
   - Bu aşamada sadece kavramsal olarak bırakıyoruz; şema ileride genişletilir.

3. **Template**  
   - “2 kolon”, “3 kolon”, “sidebar + ana alan” gibi hazır şablonlar istenirse, row-based üzerinden sabit `rows` şablonları tanımlanabilir.

---

## 4. Row-Based Layout (Öncelikli)

### 4.1 Mantık

- Layout = **satırlar** (`rows`) listesi.  
- Her satır = **sütunlar** (`cols`) listesi.  
- Her sütun = bir **widget** (`widgetId` → `@widgets.__dataId`) + **span** bilgisi.  
- Vuetify 12 kolonluk grid; `cols`, `sm`, `md`, `lg`, `xl` breakpoint’lerine göre span verilebilir.

### 4.2 TypeScript Tarifi

```ts
type LayoutType = 'rows';

interface DashboardLayout {
  type: LayoutType;
  rows: LayoutRow[];
}

interface LayoutRow {
  cols: LayoutCol[];
  /** Opsiyonel: v-row props */
  align?: 'start' | 'center' | 'end' | 'baseline' | 'stretch';
  justify?: 'start' | 'center' | 'end' | 'space-between' | 'space-around' | 'space-evenly';
  noGutters?: boolean;
  dense?: boolean;
}

interface LayoutCol {
  /** @widgets __dataId */
  widgetId: string;
  /** xs (default): 1–12 */
  span?: number;
  spanSm?: number;
  spanMd?: number;
  spanLg?: number;
  spanXl?: number;
  /** Opsiyonel: v-col class / align */
  alignSelf?: 'auto' | 'start' | 'center' | 'end' | 'baseline' | 'stretch';
  /** Opsiyonel: flex order */
  order?: number;
}
```

### 4.3 Vuetify Eşlemesi

- `LayoutRow` → `<v-row align="..." justify="..." no-gutters dense>`
- `LayoutCol` → `<v-col :cols="span" :sm="spanSm" :md="spanMd" :lg="spanLg" :xl="spanXl" align-self="...">`
- İçeride `widgetId` ile ilgili widget component’i render edilir.

### 4.4 Örnek Layout (Analytical benzeri)

```json
{
  "type": "rows",
  "rows": [
    {
      "cols": [
        { "widgetId": "widget-sales-overview", "span": 12, "spanLg": 8 },
        { "widgetId": "widget-total-sales", "span": 12, "spanLg": 4 }
      ]
    },
    {
      "cols": [
        { "widgetId": "widget-blog-card", "span": 12, "spanLg": 4 },
        { "widgetId": "widget-newsletter", "span": 12, "spanLg": 8 }
      ]
    },
    {
      "cols": [
        { "widgetId": "widget-bandwidth", "span": 12, "spanLg": 4 },
        { "widgetId": "widget-downloads", "span": 12, "spanLg": 4 },
        { "widgetId": "widget-weather", "span": 12, "spanLg": 4 }
      ]
    },
    {
      "cols": [
        { "widgetId": "widget-profile-contacts", "span": 12, "spanLg": 4 },
        { "widgetId": "widget-activity-timeline", "span": 12, "spanLg": 8 }
      ]
    }
  ]
}
```

### 4.5 İç içe Satırlar (Nested Rows)

Mevcut **modern** dashboard’da bir `v-col` içinde tekrar `v-row` > `v-col` var. Bunu desteklemek için sütun hem widget hem **iç satırlar** barındırabilir:

```ts
interface LayoutCol {
  widgetId?: string;      // Widget varsa
  rows?: LayoutRow[];     // İç içe satırlar varsa (widgetId ile birlikte kullanılmaz)
  span?: number;
  spanSm?: number;
  spanMd?: number;
  spanLg?: number;
  spanXl?: number;
  alignSelf?: string;
  order?: number;
}
```

- `widgetId` varsa → sütunda yalnızca widget.  
- `rows` varsa → sütunda yeni `v-row`(lar), onların `cols`’unda widget veya daha iç içe `rows` olabilir.  
- Aynı sütunda hem `widgetId` hem `rows` tanımlı olmamalı (validation).

**Örnek (modern tarzı üst blok):**

```json
{
  "type": "rows",
  "rows": [
    {
      "cols": [
        {
          "span": 12,
          "spanLg": 5,
          "widgetId": "widget-congrats"
        },
        {
          "span": 12,
          "spanLg": 7,
          "rows": [
            {
              "cols": [
                { "widgetId": "widget-purchases", "span": 12, "spanLg": 5 },
                { "widgetId": "widget-total-earnings", "span": 12, "spanLg": 7 }
              ]
            }
          ]
        }
      ]
    }
  ]
}
```

### 4.6 Varsayılanlar ve Kısıtlamalar

- `span` verilmezse `12` (tam genişlik) kabul edilebilir.  
- `span*` 1–12 aralığında olmalı.  
- Aynı satırda `cols` span’leri toplamı 12’yi geçmemeli (Vuetify grid kuralı). Farklı breakpoint’lerde farklı dağılım mümkün.

---

## 5. Grid Layout (İleride)

İleride **sürükle-bırak + yeniden boyutlandırma** istenirse:

- `layout.type === 'grid'`
- `layout.gridCols`: 12 (sabit)
- `layout.items`: `{ widgetId, x, y, w, h }[]` (grid birimi cinsinden)
- UI’da GridStack veya benzeri kütüphane kullanılır; kaydetme sırasında `items` güncellenir.

Bu aşamada sadece **kavramsal**; şema ve implementasyon sonra eklenebilir.

---

## 6. Dashboard Kaydı Örneği

```json
{
  "name": "analytical",
  "title": "Analitik Dashboard",
  "description": "Satış, ziyaretçi ve aktivite widget'ları",
  "slug": "analytical",
  "layout": {
    "type": "rows",
    "rows": [
      {
        "cols": [
          { "widgetId": "widget-sales-overview", "span": 12, "spanLg": 8 },
          { "widgetId": "widget-total-sales", "span": 12, "spanLg": 4 }
        ]
      },
      {
        "cols": [
          { "widgetId": "widget-blog-card", "span": 12, "spanLg": 4 },
          { "widgetId": "widget-newsletter", "span": 12, "spanLg": 8 }
        ]
      }
    ]
  },
  "permissions": {
    "view": { "groups": ["managers", "editors", "viewers"], "users": [] },
    "edit": { "groups": ["managers", "editors"], "users": [] }
  },
  "sideMenuConfig": {
    "enabled": true,
    "routeType": "path",
    "routePath": "/dashboards/analytical"
  },
  "isDefault": true,
  "isActive": true,
  "order": 1
}
```

---

## 7. Route ve Sayfa İlişkisi

- Liste: `/dashboards` — `@dashboards` kayıtları (aktif, sıralı).  
- Detay: `/dashboards/:slug` (veya `:name`) — ilgili dashboard’un `layout`’u okunur, widget’lar render edilir.  
- `slug` yoksa `name` kullanılır.

---

## 8. Özet ve Sıradaki Adımlar

| Adım | Konu | Doküman |
|------|------|---------|
| 1 | `@dashboards` dataset + row-based layout | Bu spec |
| 2 | `@widget_categories` + `@widgets` | [WIDGET_LIBRARY_SPEC](./WIDGET_LIBRARY_SPEC.md) |
| 3 | Runtime: dashboard sayfası + layout render + widget yükleme | İmplementasyon |

**Layout özeti:**
- Öncelik: **row-based** layout; Vuetify grid ile uyumlu, nested row destekli.  
- İleride: **grid** layout (drag-and-drop) eklenebilir.  
- Widget’lar `layout.rows[].cols[].widgetId` ile `@widgets`’a bağlanır.
