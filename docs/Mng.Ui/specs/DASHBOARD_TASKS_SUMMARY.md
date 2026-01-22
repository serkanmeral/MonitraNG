# Dashboard Tarafı — Yapılacak İşler Özeti

**Kapsam:** Implementasyon sırası 4, 5, 6 — Dashboard listesi, görüntüleme, Builder.

**Durum:** ✅ **Tamamlandı** (Ocak 2026)

---

## Genel Bakış

| # | Sayfa / Özellik | Route | Özet |
|---|-----------------|-------|------|
| 4 | **Dashboard listesi** | `/apps/dashboards` | @dashboards listesi, CRUD girişleri |
| 5 | **Dashboard görüntüleme** | `/dashboards/:slug` | Layout render, widget placeholder |
| 6 | **Dashboard Builder** | `/apps/dashboards/new`, `/apps/dashboards/:id/edit` | Form + layout editor |

---

## 4. Dashboard Listesi (`/apps/dashboards`)

### Yapılacaklar

- [ ] **Sayfa:** `pages/apps/dashboards/index.vue`
- [ ] **Store:** `stores/apps/dashboard.ts` — `fetchDashboards`, `fetchDashboardById`, `create`, `update`, `delete`
- [ ] **API:** DG `GET /api/v1/data/@dashboards` (liste), `GET .../{id}` (tek), `POST` (create), `PUT` (update), `DELETE` (delete)
- [ ] **UI:**
  - Tablo: name, title, slug, isDefault, isActive, order, oluşturulma
  - Filtre: arama, isActive
  - Aksiyonlar: Yeni Dashboard, Düzenle, Önizle, Sil
- [ ] **Yönlendirmeler:** Yeni → Builder (`/apps/dashboards/new`), Düzenle → Builder (`/apps/dashboards/:id/edit`), Önizle → `/dashboards/:slug`
- [ ] **Side menu:** Gerekirse “Dashboards” menü öğesi eklenmesi (apps altı)

### Notlar

- Pagination: `skip` / `limit` veya `X-Total-Count` kullanımı.
- Silmeden önce onay modal’ı.

---

## 5. Dashboard Görüntüleme (`/dashboards/:slug`)

### Yapılacaklar

- [ ] **Sayfa:** `pages/dashboards/[slug].vue` (veya `[slug].vue` uygun yapıda)
- [ ] **Store:** Listede kullanılan `dashboard` store’dan `fetchBySlug` (veya `fetchById`) — slug/name ile DG’den tek dashboard
- [ ] **API:** `GET /api/v1/data/@dashboards?filter=slug:eq:{slug}` veya slug yerine name kullanılıyorsa `name:eq:{slug}`; tek kayıt dönmeli. Alternatif: tümünü çekip client’ta slug’a göre filtrele (küçük liste için).
- [ ] **Layout render:**
  - Dashboard’un `layout` objesini oku (type: `rows`, `rows[]`).
  - Her `row` → `<v-row>`, her `col` → `<v-col :cols="span" :sm="spanSm" :md="spanMd" :lg="spanLg" :xl="spanXl">`.
  - Col içinde `widgetId` varsa → **widget placeholder** (örn. “Widget: {widgetId}” veya küçük kart). Henüz gerçek widget yok.
  - `widgetId` yoksa → boş hücre veya “Widget yok”.
- [ ] **Breadcrumb:** Örn. Home > Dashboards > {title}
- [ ] **Hata:** Slug ile dashboard bulunamazsa 404 / uygun mesaj.

### Notlar

- Nested row’lar (col içinde `rows`) varsa aynı mantıkla recursive render.
- Yetkilendirme (permissions.view) bu aşamada opsiyonel; eklenebilir.

---

## 6. Dashboard Builder

### 6.1 Sayfa ve Route

- [ ] **Sayfa:** `pages/apps/dashboards/new.vue` (yeni), `pages/apps/dashboards/[id]/edit.vue` (düzenleme)
- [ ] **Routing:** Liste “Yeni” → `/apps/dashboards/new`, “Düzenle” → `/apps/dashboards/:id/edit`
- [ ] **Store:** Create/update için `dashboard` store `create`, `update` kullanımı.

### 6.2 Sol Panel — Temel Bilgiler Formu

- [ ] **Form alanları:** name, title, description, slug, isDefault, isActive (spec’e uygun)
- [ ] **Validasyon:** name ve title zorunlu; name unique (create’te API hatası ile yakalanabilir)
- [ ] **Aksiyonlar:** Kaydet, İptal, (opsiyonel) Önizle
- [ ] **Kaydet:** Create → `POST /api/v1/data/@dashboards`; Edit → `PUT /api/v1/data/@dashboards/:id`; sonrası liste veya önizleme yönlendirmesi

### 6.3 Sağ Panel — Layout Editor

- [ ] **Row/column yapısı:**
  - `layout.type === 'rows'`, `layout.rows[]` — her row’da `cols[]`.
  - Row ekleme / silme / sıra değiştirme (↑↓ veya drag).
  - Her row içinde column ekleme / silme.
- [ ] **Column ayarları:** span, spanSm, spanMd, spanLg, spanXl (1–12); aynı row’da toplam 12 kuralı (uyarı/validasyon).
- [ ] **Widget atama:** Her col’da “Widget ekle” / “Widget değiştir” — **Widget Picker** (modal). Henüz @widgets yoksa:
  - Picker’ı atlayıp sadece **placeholder** (ör. “Widget seçilecek”) veya **manuel widgetId** girişi (test için) yapılabilir.
- [ ] **Layout state:** Vue `reactive` / Pinia ile `layout` objesi; form ile birlikte kaydedilir.

### 6.4 Bileşenler (önerilen)

- [ ] `DashboardForm.vue` — Sol panel form
- [ ] `LayoutEditor.vue` — Sağ panel; row/col ağacı
- [ ] `LayoutRowItem.vue` — Tek satır, col listesi, “+ Sütun”
- [ ] `LayoutColItem.vue` — Tek sütun, span ayarları, widget placeholder / picker tetikleyici
- [ ] (Sonra) `WidgetPickerModal.vue` — Widget seçimi; @widgets hazır olunca bağlanır

### 6.5 Kaydetme Akışı

- [ ] Form + layout validasyonu (örn. en az bir row, span toplamları).
- [ ] Create: `POST` body’de `name`, `title`, `description`, `slug`, `layout`, `isDefault`, `isActive`, `order`.
- [ ] Edit: `PUT` ile aynı alanların güncellenmesi.
- [ ] Başarı → liste veya `/dashboards/:slug` önizleme; hata → mesaj gösterimi.

---

## Özet Checklist (Dashboard Tarafı)

```
4. Dashboard listesi
   □ pages/apps/dashboards/index.vue
   □ stores/apps/dashboard.ts (CRUD)
   □ DG API: list, get, create, update, delete @dashboards
   □ Tablo, filtre, Yeni/Düzenle/Önizle/Sil

5. Dashboard görüntüleme
   □ pages/dashboards/[slug].vue
   □ fetch dashboard by slug (veya name)
   □ layout.rows → v-row / v-col render
   □ widgetId → placeholder (“Widget: id” veya “Widget yok”)

6. Dashboard Builder
   □ pages/apps/dashboards/new.vue + [id]/edit.vue
   □ DashboardForm (name, title, description, slug, isDefault, isActive)
   □ LayoutEditor (rows, cols, span ayarları)
   □ Row/col ekleme, silme, sıralama
   □ Widget atama (picker veya placeholder / manuel widgetId)
   □ Kaydet → POST/PUT @dashboards
```

---

## Bağımlılıklar

- **@dashboards:** Hazır ✅
- **@widgets / @widget_categories:** Builder’da **Widget Picker** için gerekli. Picker olmadan da ilerlenebilir: sadece layout + placeholder veya manuel `widgetId`.
- **Yetkilendirme (permissions):** 10. adımda; view/edit kontrolleri istenirse sonra eklenir.

---

## Uygulama Durumu

1. ✅ **Dashboard store** + DG `@dashboards` API bağlantısı (list, get by id/slug, create, update, delete).
2. ✅ **Dashboard listesi** sayfası (tablo, filtre, aksiyonlar, pagination).
3. ✅ **Dashboard görüntüleme** sayfası (layout render, widget placeholder, nested rows).
4. ✅ **Dashboard Builder** — form + layout editor (row/col yönetimi, nested rows, span ayarları); widget atama (manuel widgetId + Widget Picker placeholder).
5. 🔲 İleride: Widget Picker (@widgets hazır olunca bağlanacak), permissions kontrolleri, side menu entegrasyonu (rehber hazır).

---

**İlgili dokümanlar:**  
[DYNAMIC_DASHBOARD_SPEC](./DYNAMIC_DASHBOARD_SPEC.md) · [DASHBOARD_BUILDER_MECHANISM](./DASHBOARD_BUILDER_MECHANISM.md) · [DASHBOARD_WIDGET_IMPLEMENTATION_ORDER](./DASHBOARD_WIDGET_IMPLEMENTATION_ORDER.md)
