# Mng.Ui — UI Tasarım Planlaması

**Amaç:** MonitraNG ana frontend uygulaması (Mng.Ui) için tek referans UI tasarım planlaması. Tasarım ilkeleri, bilgi mimarisi, sayfa envanteri ve modül planları bu dokümanda toplanır.

**İlişkili dokümanlar:**
- [Current Status](guides/current_status.md) — Güncel geliştirme durumu
- **Mng.Ui RoadMap** — Proje kökünde `Mng.Ui/RoadMap.md`; faz bazlı geliştirme planı
- [DOCUMENTATION_STANDARDS](../../DOCUMENTATION_STANDARDS.md) — Dokümantasyon standartları

---

## 1. Kapsam ve Hedef

| Öğe | Açıklama |
|-----|----------|
| **Ürün** | Mng.Ui — MonitraNG tek sayfa uygulaması (SPA) |
| **Teknoloji** | Nuxt 3, Vue 3, Vuetify 3, TypeScript, Pinia |
| **Hedef kitle** | Domain admin, manager, operatör kullanıcılar |
| **Tasarım hedefi** | Tutarlı, erişilebilir, ölçeklenebilir arayüz; modül bazlı genişleme |

Bu planlama:
- Yeni sayfa ve modüllerin nereye oturacağını tanımlar.
- Mevcut ve planlanan ekranları listeler.
- Tasarım kararları ve ilkeleri tek yerde toplar.
- Monitoring UI (Asset, Engine, Dashboard vb.) ile uyumu belirler.

---

## 2. Tasarım İlkeleri ve Standartlar

### 2.1 Tasarım Sistemi

- **Component kütüphanesi:** Vuetify 3 (Material Design tabanlı).
- **İkonlar:** Material Design Icons (MDI) ve Tabler Icons; Side Menu ve spec'lerde tanımlı.
- **Tipografi ve renk:** Vuetify theme (primary, secondary, error, surface vb.); layout SCSS ile tutarlı kullanım.
- **Dil:** i18n (vue-i18n); varsayılan Türkçe, fallback İngilizce; RTL desteği (Arapça vb.).

### 2.2 Sayfa ve Layout Standartları

| Kural | Açıklama |
|-------|----------|
| **Breadcrumb** | Tüm uygulama sayfalarında `BaseBreadcrumb` veya eşdeğer kullanım. |
| **Container** | Sayfa içeriği `v-card` veya `AppBaseCard` / `UiParentCard` ile sarmalanır. |
| **Formlar** | VeeValidate + Yup; zorunlu alanlar asterisk (*); hata mesajları alan altında. |
| **Tablolar** | `v-data-table`; server-side pagination tercih edilir (büyük listeler). |
| **Yetkilendirme** | `usePagePermissions` composable; buton ve menü öğeleri `canCreate`, `canUpdate`, `canDelete`, `canExport` ile koşullu. |

### 2.3 Erişilebilirlik ve UX

- Anlamlı loading state'ler (skeleton veya spinner).
- Form ve silme işlemlerinde onay diyalogları.
- Başarı/hata için tutarlı geri bildirim (toast/snackbar).
- Boş durumlar (empty state) için net mesaj ve aksiyon.
- Responsive davranış; mobilde menü ve tablolar kullanılabilir olmalı.

---

## 3. Bilgi Mimarisi (IA)

### 3.1 Menü Kaynağı

- Menü yapısı **MngDataGateway `@side_menu` dataset** üzerinden yönetilir.
- Side Menu Manager (`/apps/side-menu-manager`) ile CRUD; SignalR ile anlık güncelleme.
- Sayfa tipleri: `admin`, `manager`, `user`; yetki filtrelemesi `usePagePermissions` ve middleware ile.

### 3.2 Modül Grupları (Kavramsal)

| Grup | Açıklama | Örnek sayfalar |
|------|----------|-----------------|
| **Apps** | Uygulama yönetimi ve veri işlemleri | Domain, Users, Groups, Datasets, Dashboards, Widgets, Automated Forms, Side Menu Manager, Dataset Categories |
| **Monitoring** | İzleme (planlanan) | Organizasyon/Item, Asset, Engine, Agent, Dashboard, Workflow |
| **Auth** | Kimlik doğrulama | Login, Register, Forgot Password |
| **Dashboards** | Görsel panolar | Analytical, Classic, Ecommerce, vb. + kullanıcı tanımlı dashboard'lar |
| **Theme/Account** | Hesap ve tema | Profile, Account Settings, FAQ, Pricing |

Monitoring modülü, [Monitoring Implementasyon Planı](../../monitoring_plans/MONITORING_IMPLEMENTATION_PLAN.md) Faz 5 ile Mng.Ui’a entegre edilecektir.

---

## 4. UI Yol Haritası (Ana Başlıklar)

Öncelik sırasına göre **Mng.Ui** için planlama ve geliştirme ana başlıkları. Detaylar ilgili spec ve RoadMap'te.

| Öncelik | Ana başlık | Kapsam | Bağımlılık / Not |
|--------|-------------|--------|-------------------|
| **1** | **Organizasyon sayfası** | Item + Asset tree, view/edit panel (Side Menu Manager benzeri). Spec: [ORGANIZATION_PAGE_SPEC](specs/ORGANIZATION_PAGE_SPEC.md). | mon_items, mon_assets DG'de hazır (Faz 0). |
| **2** | **Monitoring menü yapısı** | Side Menu'da "İzleme" / "Monitoring" grubu; Organizasyon, Motorlar, Agent'lar, Dashboard route'ları. | Organizasyon sayfası ile birlikte veya hemen sonra. |
| **3** | **Engine / Agent CRUD sayfaları** | Engine listesi, Config String butonu, lastSeenAt; Agent listesi, asset_configs. | Reactor API hazır. |
| **4** | **Asset Type / Family yönetimi** | mon_asset_type_family, mon_asset_types listeleme ve CRUD (veya Automated Forms ile). | DG dataset'leri Faz 0. |
| **5** | **Monitoring dashboard** | Metrik görselleştirme; mon_metrics sorguları, grafik widget'ları. | Mevcut dashboard builder + monitoring'e özel widget/query. |
| **6** | **Workflow UI** | mon_workflows CRUD; koşul ve aksiyon tanımları. | MngWorkflow backend (Faz 3) ile. |
| **7** | **Domain / Data Management** | Domain CRUD (RoadMap Phase 2); genel Data Management `/apps/data/[datasetName]` (Phase 4). | Backend mevcut. |
| **8** | **Kullanıcı deneyimi iyileştirmeleri** | User profile (unvan, fotoğraf vb.), list export (CSV/Excel), server-side pagination tutarlılığı. | RoadMap Phase 7.x. |

**Sırada planlanması önerilenler (kısa vadeli):**

1. **Organizasyon sayfası** — Spec hazır; implementasyon (store, tree, form, sayfa).
2. **Monitoring menü** — Hangi route'lar, pageCode'lar; Side Menu'ya eklenmesi.
3. **Engine/Agent sayfaları** — Liste + form + Config String butonu için kısa spec veya Automated Forms ile dataset bazlı karar.

**Orta / uzun vadeli:** Monitoring dashboard stratejisi, Workflow UI, Domain/Data Management, UX iyileştirmeleri.

---

## 5. Sayfa ve Modül Envanteri

### 5.1 Mevcut Sayfalar (Özet)

| Route / Alan | Açıklama | Spec / Not |
|--------------|----------|------------|
| `/apps/domain` | Domain yönetimi | RoadMap Phase 2 |
| `/apps/users`, `/apps/users/create`, `/apps/users/edit/[id]`, `/apps/users/details/[id]` | Kullanıcı CRUD | RoadMap Phase 7 |
| `/apps/groups`, `/apps/groups/create`, `/apps/groups/edit/[id]`, `/apps/groups/details/[id]` | Grup CRUD | RoadMap Phase 7.4 |
| `/apps/datasets`, `/apps/datasets/create`, `/apps/datasets/edit/[name]`, `/apps/datasets/[name]` | Dataset CRUD, schema, permissions | [DATASET_UI_DESIGN](specs/DATASET_UI_DESIGN.md) |
| `/apps/dataset-categories/*` | Dataset kategorileri | RoadMap Phase 3.0 |
| `/apps/dashboards`, `/apps/dashboards/new`, `/apps/dashboards/[id]/edit` | Dashboard listesi ve builder | [DASHBOARD_BUILDER_MECHANISM](specs/DASHBOARD_BUILDER_MECHANISM.md), [DYNAMIC_DASHBOARD_SPEC](specs/DYNAMIC_DASHBOARD_SPEC.md) |
| `/apps/widgets`, `/apps/widgets/new`, `/apps/widgets/[id]/edit` | Widget kütüphanesi | [WIDGET_LIBRARY_SPEC](specs/WIDGET_LIBRARY_SPEC.md), [WIDGET_ROADMAP](specs/WIDGET_ROADMAP.md) |
| `/apps/automated-forms/*` | Otomatik formlar (liste, oluştur, düzenle, view) | [AUTOMATED_FORMS_PLANNING](specs/AUTOMATED_FORMS_PLANNING.md), current_status |
| `/apps/side-menu-manager` | Menü öğeleri CRUD, drag & drop | [SIDE_MENU_PLANNING](specs/SIDE_MENU_PLANNING.md) |
| `/apps/locale-editor` | Çeviri düzenleyici | i18n ROADMAP |
| `/apps/license-management` | Lisans yönetimi | — |
| `/dashboards/[slug]` | Dashboard runtime görüntüleme | Dashboard Builder |
| `/auth/login`, `/auth/Register`, vb. | Auth sayfaları | — |
| `/assetdata/assets-page` | Asset veri sayfası (erken/deney) | Monitoring ile uyumlaştırılacak |

### 5.2 Planlanan Sayfalar (RoadMap + Monitoring)

| Sayfa / Özellik | Kaynak | Kısa açıklama |
|-----------------|--------|----------------|
| Domain listesi / oluşturma / detay | RoadMap Phase 2 | Domain CRUD; admin kullanımı |
| Data Management (genel veri listesi/detay/form) | RoadMap Phase 4 | `/apps/data/[datasetName]`, schema tabanlı dinamik form |
| Sayfa yönetimi (`@pages`) | RoadMap Phase 5.x | Sayfa tanımları, menü sırası, yetkiler |
| **Monitoring — Organizasyon / Item** | [MONITORING_IMPLEMENTATION_PLAN](../../monitoring_plans/MONITORING_IMPLEMENTATION_PLAN.md) §8 | Item hiyerarşisi (mon_items); lokasyon, kind, tags |
| **Monitoring — Asset / Item CRUD** | Aynı | mon_assets, mon_items; connection_info, collectible_config; Reactor + DG |
| **Monitoring — Engine / Agent CRUD** | Aynı | mon_engines, mon_agents; config string üretimi (Reactor API) |
| **Monitoring — Dashboard** | Aynı | Metrik görselleştirme; widget, grafik; mon_metrics sorguları |
| **Monitoring — Workflow UI** | MONITORING_IMPLEMENTATION_PLAN Faz 3 | mon_workflows CRUD; koşul/aksiyon tanımları |
| User profile geliştirmeleri | RoadMap Phase 7.2 | Unvan, departman, cinsiyet, telefon, fotoğraf |
| List export (CSV/Excel) | RoadMap Phase 7.5 | Kullanıcı ve grup listeleri için |

---

## 6. Monitoring UI Entegrasyonu

Monitoring uygulaması için backend (Reactor, Engine, Simulator) planları [monitoring_plans](../../monitoring_plans/README.md) altında. Mng.Ui tarafında planlanan ekranlar:

| Ekran | İçerik | API / Backend |
|-------|--------|----------------|
| **Organizasyon / Item ağacı** | mon_items hiyerarşisi; lokasyon, kind, parent | MngDataGateway (data API) + Reactor |
| **Asset listesi / CRUD** | mon_assets; type, itemId, connection_info, collectible_config | Reactor → DG data API; connection_info şifreleme Reactor’da |
| **Asset Type / Family** | mon_asset_type_family, mon_asset_types (yönetim) | DG data API |
| **Engine listesi / CRUD** | mon_engines; config string butonu | Reactor API (MonEngines, config string endpoint) |
| **Agent listesi / CRUD** | mon_agents; asset_configs, period, schedule | Reactor API + DG |
| **Monitoring dashboard** | Metrik grafikleri; asset/engine/collectible filtreleri | Reactor / DG veya özel query; mon_metrics |
| **Workflow listesi / CRUD** | mon_workflows; koşul ve aksiyon tanımları | MngWorkflow + DG (planlı) |

Tasarım kararları:
- Monitoring sayfaları **Apps** altında “İzleme” veya “Monitoring” menü grubu olarak toplanabilir.
- Engine tanım sayfasında “Config String Oluştur” butonu [MONITORING_REACTOR_ARCHITECTURE](../../monitoring_plans/MONITORING_REACTOR_ARCHITECTURE.md) ile uyumlu şekilde Reactor API’yi çağırır.
- Asset/Item CRUD formları [MONITORING_ASSET_DATASETS](../../monitoring_plans/MONITORING_ASSET_DATASETS.md) ve [MONITORING_AGENT_ARCHITECTURE](../../monitoring_plans/MONITORING_AGENT_ARCHITECTURE.md) şemalarına göre tasarlanmalı.

---

## 7. Mevcut Spec’lere Referanslar

| Doküman | Konu |
|---------|------|
| [DATASET_UI_DESIGN](specs/DATASET_UI_DESIGN.md) | Dataset CRUD, schema formu, field/validation/index |
| [DATASET_UI_DESIGN_EXPECTED_FEATURES](specs/DATASET_UI_DESIGN_EXPECTED_FEATURES.md) | Backend–UI uyum analizi |
| [DASHBOARD_BUILDER_MECHANISM](specs/DASHBOARD_BUILDER_MECHANISM.md) | Dashboard builder akışı ve UI |
| [DYNAMIC_DASHBOARD_SPEC](specs/DYNAMIC_DASHBOARD_SPEC.md) | Dashboard dataset ve layout |
| [WIDGET_LIBRARY_SPEC](specs/WIDGET_LIBRARY_SPEC.md) | Widget tipleri ve kullanım |
| [WIDGET_ROADMAP](specs/WIDGET_ROADMAP.md) | Widget geliştirme sırası |
| [SIDE_MENU_PLANNING](specs/SIDE_MENU_PLANNING.md) | Menü yapısı, icon, permission |
| [AUTOMATED_FORMS_PLANNING](specs/AUTOMATED_FORMS_PLANNING.md) | Otomatik formlar tasarımı |
| [VUETIFY_TABLE_RECOMMENDATION](specs/VUETIFY_TABLE_RECOMMENDATION.md) | Tablo kullanım önerileri |
| [ORGANIZATION_PAGE_SPEC](specs/ORGANIZATION_PAGE_SPEC.md) | Organizasyon sayfası — tree (Item + Asset) + view/edit panel; Side Menu Manager benzeri yapı |
| [MONITORING_CONTROL_PAGE_SPEC](specs/MONITORING_CONTROL_PAGE_SPEC.md) | Monitoring kontrol sayfası — Engine/Agent durum, sync tetikleme, canlı metrikler, sistem sağlığı |

Yeni bir sayfa veya modül tasarlanırken ilgili spec varsa önce o spec’e uyum sağlanmalı; yoksa bu UI Tasarım Planlaması ve RoadMap’e göre yeni spec taslağı çıkarılabilir.

---

## 8. Açık Kararlar ve Sonraki Adımlar

### 8.1 Açık Tasarım Kararları

- **Monitoring menü yapısı:** “İzleme” tek header mı, yoksa “Organizasyon”, “Asset’ler”, “Motorlar” vb. alt başlıklar mı kullanılacak?
- **Monitoring dashboard:** Mevcut dashboard builder ve widget’lar mı kullanılacak, yoksa monitoring’e özel widget/query seti mi tanımlanacak?
- **Asset/Item ağacı:** [ORGANIZATION_PAGE_SPEC](specs/ORGANIZATION_PAGE_SPEC.md) ile Side Menu Manager benzeri tree + view/edit panel kararı verildi; implementasyon bu spec’e göre yapılacak.
- **Engine/Agent formları:** Generic dataset tabanlı (Automated Forms) mı, yoksa özel form sayfaları mı?

### 8.2 Önerilen Sonraki Adımlar

1. **Monitoring menü ve sayfa listesini netleştirmek** — Side Menu’de hangi pageCode ve route’lar kullanılacak.
2. **Asset/Item CRUD için bir UI spec taslağı** — mon_items ve mon_assets form alanları, validasyon, relation gösterimi.
3. **Engine/Agent sayfaları için kısa spec** — Config string butonu, lastSeenAt gösterimi, liste kolonları.
4. **Mevcut `/assetdata/assets-page`** — Monitoring planına göre revize veya yeni sayfalarla değiştirme kararı.

---

## 9. Güncelleme Notları

- **İlk sürüm:** UI Tasarım Planlaması oluşturuldu; kapsam, ilkeler, IA, sayfa envanteri, Monitoring entegrasyonu ve spec referansları eklendi.
- Bu doküman, yeni modül/sayfa eklendikçe veya tasarım kararları netleştikçe güncellenmelidir.
