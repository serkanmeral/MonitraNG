# Dashboard Tarafı — Yapılacak İşler Özeti

**Kapsam:** Implementasyon sırası 4, 5, 6 — Dashboard listesi, görüntüleme, Builder.

**Durum:** ✅ **Tamamlandı** (Ocak 2026)

---

## Genel Bakış

| # | Sayfa / Özellik | Route | Özet |
|---|-----------------|-------|------|
| 4 | **Dashboard listesi** | `/apps/dashboards` | @dashboards listesi, CRUD girişleri |
| 5 | **Dashboard görüntüleme** | `/dashboards/:slug` | Layout render, widget renderer, refresh mekanizması |
| 6 | **Dashboard Builder** | `/apps/dashboards/new`, `/apps/dashboards/:id/edit` | Form + layout editor, yetkilendirme |
| 7 | **Dashboard Container** | `/dashboards/container` | Çoklu dashboard rotasyonu, widget refresh |
| 8 | **Dashboard Yetkilendirme** | - | View/edit izinleri, grup bazlı erişim kontrolü |
| 9 | **Widget Yetkilendirme** | - | Widget bazlı grup izinleri |
| 10 | **Widget Refresh** | - | Dashboard ve container içinde otomatik widget yenileme |
| 11 | **Side Menu Entegrasyonu** | - | Decoupled yapı, "Add to Side Menu" butonu |

---

## Özet Checklist (Dashboard Tarafı)

```
4. Dashboard listesi ✅
   ✅ pages/apps/dashboards/index.vue
   ✅ stores/apps/dashboard.ts (CRUD)
   ✅ DG API: list, get, create, update, delete @dashboards
   ✅ Tablo, filtre, Yeni/Düzenle/Önizle/Sil
   ✅ "Add to Side Menu" butonu

5. Dashboard görüntüleme ✅
   ✅ pages/dashboards/[slug].vue
   ✅ fetch dashboard by slug (veya name)
   ✅ layout.rows → v-row / v-col render
   ✅ Widget renderer entegrasyonu
   ✅ Widget refresh mekanizması
   ✅ Yetkilendirme kontrolü

6. Dashboard Builder ✅
   ✅ pages/apps/dashboards/new.vue + [id]/edit.vue
   ✅ DashboardForm (name, title, description, slug, isDefault, isActive, permissions)
   ✅ LayoutEditor (rows, cols, span ayarları)
   ✅ Row/col ekleme, silme, sıralama
   ✅ Widget atama (Widget Picker)
   ✅ Kaydet → POST/PUT @dashboards

7. Dashboard Container ✅
   ✅ pages/dashboards/container.vue
   ✅ Çoklu dashboard seçimi (yetkili dashboard'lar)
   ✅ Rotasyon mekanizması (setInterval)
   ✅ Widget refresh interval ayarı
   ✅ Rotasyon kontrolleri (Başlat/Duraklat, Önceki/Sonraki)

8. Dashboard Yetkilendirme ✅
   ✅ Dashboard.permissions.view.groups[]
   ✅ Dashboard.permissions.edit.groups[]
   ✅ useDashboardPermissions composable
   ✅ Admin bypass (isAdmin)
   ✅ UI entegrasyonu (DashboardForm)

9. Widget Yetkilendirme ✅
   ✅ Widget.permissions.groups[]
   ✅ WidgetRenderer permission kontrolü
   ✅ Admin bypass (isAdmin)
   ✅ UI entegrasyonu (WidgetForm)

10. Widget Refresh ✅
   ✅ Dashboard viewer refresh interval
   ✅ Container widget refresh interval
   ✅ Provide/Inject pattern
   ✅ WidgetRenderer otomatik yenileme

11. Side Menu Entegrasyonu ✅
   ✅ Dashboard ve Side Menu decoupled
   ✅ "Add to Side Menu" butonu
   ✅ Side Menu Manager pre-fill
```

---

## Bağımlılıklar

- **@dashboards:** Hazır ✅ (permissions field eklendi)
- **@widgets / @widget_categories:** Hazır ✅ (permissions field eklendi)
- **Yetkilendirme (permissions):** ✅ Tamamlandı - View/edit kontrolleri eklendi

---

## Uygulama Durumu

1. ✅ **Dashboard store** + DG `@dashboards` API bağlantısı (list, get by id/slug, create, update, delete).
2. ✅ **Dashboard listesi** sayfası (tablo, filtre, aksiyonlar, pagination).
3. ✅ **Dashboard görüntüleme** sayfası (layout render, widget renderer, nested rows).
4. ✅ **Dashboard Builder** — form + layout editor (row/col yönetimi, nested rows, span ayarları); widget atama (Widget Picker ile).
5. ✅ **Widget Picker** — Widget seçimi ve atama sistemi.
6. ✅ **Dashboard Container** — Birden fazla dashboard'u rotasyon ile görüntüleme.
7. ✅ **Dashboard Yetkilendirme** — View ve edit izinleri, grup bazlı erişim kontrolü.
8. ✅ **Widget Yetkilendirme** — Widget bazlı grup izinleri, yetkisiz erişim mesajları.
9. ✅ **Widget Refresh Mekanizması** — Dashboard ve container içinde widget verilerinin otomatik yenilenmesi.
10. ✅ **Side Menu Entegrasyonu** — Dashboard ve Side Menu decoupled, "Add to Side Menu" butonu ile entegrasyon.

---

## Eklenen Özellikler (Ocak 2026)

### 7. Dashboard Container (`/dashboards/container`)

#### 7.1 Özellikler

- ✅ **Çoklu Dashboard Seçimi:** Dropdown'dan birden fazla dashboard seçilebilir (sadece yetkili dashboard'lar görünür).
- ✅ **Rotasyon Mekanizması:** Seçilen dashboard'lar belirli aralıklarla otomatik olarak değişir.
- ✅ **Rotasyon Kontrolleri:** Başlat/Duraklat, Önceki/Sonraki butonları.
- ✅ **Widget Refresh:** Container içindeki widget'lar için ayrı refresh interval ayarı.
- ✅ **Yetkilendirme Filtreleme:** Dropdown'da sadece görüntüleme yetkisi olan dashboard'lar gösterilir.
- ✅ **Bilgilendirme Mesajları:** Tek dashboard seçildiğinde rotasyon uyarısı.

#### 7.2 Teknik Detaylar

- **Sayfa:** `pages/dashboards/container.vue`
- **Store:** `useDashboardStore` - `fetchDashboards`, `fetchDashboardById`
- **Composable:** `useDashboardPermissions` - `canViewDashboard`
- **Provide/Inject:** Widget refresh interval için `dashboardRefreshInterval`
- **Timer:** `setInterval` ile dashboard rotasyonu

### 8. Dashboard Yetkilendirme Sistemi

#### 8.1 Özellikler

- ✅ **View Permissions:** Dashboard görüntüleme izinleri (grup bazlı).
- ✅ **Edit Permissions:** Dashboard düzenleme izinleri (grup bazlı).
- ✅ **Admin Bypass:** `isAdmin` kullanıcıları tüm kontrolleri bypass eder.
- ✅ **UI Entegrasyonu:** Dashboard Builder formunda grup seçimi.
- ✅ **Erişim Kontrolü:** Yetkisiz erişimde "Unauthorized" mesajı.

#### 8.2 Teknik Detaylar

- **Data Model:** `Dashboard.permissions.view.groups[]`, `Dashboard.permissions.edit.groups[]`
- **Composable:** `useDashboardPermissions` - `canViewDashboard`, `canEditDashboard`
- **UI:** `DashboardForm.vue` - Grup multi-select'leri
- **Backend:** `@dashboards` dataset'inde `permissions` field'ı

### 9. Widget Yetkilendirme Sistemi

#### 9.1 Özellikler

- ✅ **View Permissions:** Widget görüntüleme izinleri (grup bazlı).
- ✅ **Admin Bypass:** `isAdmin` kullanıcıları tüm widget'ları görebilir.
- ✅ **UI Entegrasyonu:** Widget Form'da grup seçimi.
- ✅ **Erişim Kontrolü:** Yetkisiz widget'larda "Unauthorized" mesajı.

#### 9.2 Teknik Detaylar

- **Data Model:** `Widget.permissions.groups[]`
- **Component:** `WidgetRenderer.vue` - Permission kontrolü
- **UI:** `WidgetForm.vue` - Grup multi-select
- **Backend:** `@widgets` dataset'inde `permissions` field'ı

### 10. Widget Refresh Mekanizması

#### 10.1 Özellikler

- ✅ **Dashboard Viewer Refresh:** Dashboard sayfasında widget'lar için refresh interval ayarı.
- ✅ **Container Widget Refresh:** Container içinde widget'lar için ayrı refresh interval.
- ✅ **Provide/Inject Pattern:** Refresh interval'ın component tree'de paylaşılması.
- ✅ **Otomatik Yenileme:** `setInterval` ile widget verilerinin periyodik güncellenmesi.

#### 10.2 Teknik Detaylar

- **Dashboard Viewer:** `pages/dashboards/[slug].vue` - Refresh interval input'u
- **Container:** `pages/dashboards/container.vue` - Widget refresh interval ayarı
- **Widget Renderer:** `components/widgets/WidgetRenderer.vue` - `inject('dashboardRefreshInterval')`
- **Service:** `services/widgetDataService.ts` - `fetchWidgetData` fonksiyonu

### 11. Side Menu Entegrasyonu (Decoupled)

#### 11.1 Değişiklikler

- ✅ **Decoupling:** Dashboard ve Side Menu birbirinden bağımsız hale getirildi.
- ✅ **Dashboard Model:** `sideMenuConfig` field'ı kaldırıldı.
- ✅ **"Add to Side Menu" Butonu:** Dashboard listesi ve viewer sayfalarında buton eklendi.
- ✅ **Otomatik Doldurma:** Side Menu Manager sayfası dashboard verileri ile otomatik açılır.

#### 11.2 Teknik Detaylar

- **Dashboard List:** `pages/apps/dashboards/index.vue` - "Add to Side Menu" butonu
- **Side Menu Manager:** `pages/apps/side-menu-manager/index.vue` - Query param ile pre-fill
- **MenuItemForm:** `components/apps/side-menu-manager/MenuItemForm.vue` - Pre-filled item desteği

---

**İlgili dokümanlar:**  
[DYNAMIC_DASHBOARD_SPEC](./DYNAMIC_DASHBOARD_SPEC.md) · [DASHBOARD_BUILDER_MECHANISM](./DASHBOARD_BUILDER_MECHANISM.md) · [DASHBOARD_WIDGET_IMPLEMENTATION_ORDER](./DASHBOARD_WIDGET_IMPLEMENTATION_ORDER.md)

**Son Güncelleme:** Ocak 2026  
**Güncelleyen:** AI Assistant
