# Dashboard & Widget İmplementasyon Sırası

**Son güncelleme:** Ocak 2026  
**Başlangıç:** `@dashboards` dataset oluşturuldu ✅

Bu doküman, dynamic dashboard ve widget kütüphanesi için **önerilen iş sırasını** tanımlar.

---

## 1. Dataset’ler (Backend / DG)

| # | İş | Durum | Açıklama |
|---|----|-------|----------|
| 1 | **@dashboards** | ✅ Tamamlandı | Dashboard tanımları, layout, permissions, sideMenuConfig |
| 2 | **@widget_categories** | 🔲 Sırada | Kategori dataset’i oluştur + seed (Card, Chart, Table, Banner) |
| 3 | **@widgets** | 🔲 Sonra | Widget tanımları, category relation, dataSource, config |

**Bağımlılık:** `@widgets` → `@widget_categories` (category relation). Önce 2, sonra 3.

---

## 2. Dashboard Tarafı (UI)

| # | İş | Durum | Açıklama |
|---|----|-------|----------|
| 4 | **Dashboard listesi** | ✅ Tamamlandı | `/apps/dashboards` — @dashboards listesi, yeni/düzenle/sil, filtre, pagination |
| 5 | **Dashboard görüntüleme** | ✅ Tamamlandı | `/dashboards/:slug` — Layout okuyup render; hücrelerde widget placeholder (widgetId), nested rows desteği |
| 6 | **Dashboard Builder** | ✅ Tamamlandı | `/apps/dashboards/:id/edit` — Form + layout editor, row/col ekleme, nested rows, widget atama (manuel + picker placeholder) |

**Mantık:** Önce liste + görüntüleme ile akış çalışsın, sonra Builder ile layout üretilebilsin.

---

## 3. Widget Tarafı (UI)

| # | İş | Durum | Açıklama |
|---|----|-------|----------|
| 7 | **Widget store / API** | 🔲 | @widgets CRUD, @widget_categories listesi; DG data API çağrıları |
| 8 | **Widget runtime** | 🔲 | Dashboard’daki hücrelerde gerçek widget bileşenleri (card/chart/table/banner), dataSource’a göre veri çekme |
| 9 | **Widget picker** | 🔲 | Builder içinde widget seçim modal’ı (Builder’a entegre) |

**Bağımlılık:** Widget runtime, `@widgets` ve (data tipinde) DG GET kullanımını gerektirir. 7 → 8.

---

## 4. Yetkilendirme & Menü

| # | İş | Durum | Açıklama |
|---|----|-------|----------|
| 10 | **Dashboard yetkilendirme** | 🔲 | `permissions.view` / `permissions.edit`; view sayfası + Builder’da kontrol |
| 11 | **Side Menu entegrasyonu** | 🔲 | Menüye dashboard ekleme (AF benzeri), dashboard seçici, `to: /dashboards/:slug` |

**Not:** 10, view ve Builder sayfaları hazır olduktan sonra; 11, menü yapısına dashboard’ların eklenmesi için.

---

## Özet Sıra (Tek Liste)

```
1. ✅ @dashboards dataset
2.    @widget_categories dataset + seed
3.    @widgets dataset
4.    Dashboard listesi (/apps/dashboards)
5.    Dashboard görüntüleme (/dashboards/:slug) + layout render + placeholder
6.    Dashboard Builder (form + layout editor + widget atama)
7.    Widget store + @widgets / @widget_categories API
8.    Widget runtime (card/chart/table/banner, dataSource)
9.    Widget picker (Builder’da)
10.   Dashboard yetkilendirme (view/edit)
11.   Side Menu’ye dashboard ekleme
```

---

## Hangi Adımla Devam?

- **Hemen sonra:** **2. @widget_categories**  
  Dataset’i oluşturup Card, Chart, Table, Banner kategorilerini ekleyerek **3. @widgets** için zemin hazırlanır.

- **Alternatif (dashboard-first):**  
  2–3’ü atlayıp **4–5–6** ile önce liste + view + Builder’ı **placeholder widget** ile getirip, ardından 2 → 3 → 7 → 8 → 9 ile widget tarafını eklemek de mümkün. Bu durumda layout’taki `widgetId` geçersiz veya boş olacağı için “Widget yok” göstermek gerekir.

**Öneri:** Önce **2 → 3** (kategoriler + widgets dataset), sonra **4 → 5 → 6** (dashboard UI). Böylece ilk dashboard kayıtlarında anlamlı widget atamaları yapılabilir.

---

**İlgili dokümanlar:**
- [DYNAMIC_DASHBOARD_SPEC.md](./DYNAMIC_DASHBOARD_SPEC.md) — Dashboard şeması, layout, permissions, Side Menu
- [WIDGET_LIBRARY_SPEC.md](./WIDGET_LIBRARY_SPEC.md) — Widget tipleri, dataSource, DG GET
- [DASHBOARD_BUILDER_MECHANISM.md](./DASHBOARD_BUILDER_MECHANISM.md) — Builder UI, layout editor, widget picker
