# Sayfa Kataloğu — Mng.Ui iş modülleri

**Amaç:** Test kapsamını netleştirmek; hangi sayfanın ne işe yaradığını, hangi persona ve veri ile test edileceğini tanımlamak.

**Makine okunur sürüm:** [page-catalog.yml](./page-catalog.yml)  
**Son güncelleme:** 9 Haziran 2026

---

## Kapsam dışı (bilinçli)

Aşağıdaki route grupları **MaterialPro tema/demo** sayfalarıdır; iş modülü test paketine dahil edilmez:

| Prefix | Örnek | Neden |
|--------|-------|-------|
| `/ui-components/*` | Dialogs, Tabs | Tema bileşen demosu |
| `/datatables/*` | Basic, Filtering | Tema demosu |
| `/forms/*` | Form-Layouts | Tema demosu |
| `/tables/*` | Editable | Tema demosu |
| `/front-pages/*` | Pricing, Portfolio | Landing demo |
| `/widgets/charts/*` | — | Tema chart demosu |
| `/theme-pages/*` | faq, treeview | Tema sayfaları |
| `/auth/*` | Login (ayrı fixture) | Sadece auth fixture ile |
| `/apps/ecommerce/*`, `/apps/blog/*`, `/apps/chats/*`, `/apps/email/*`, `/apps/kanban/*`, `/apps/tickets/*`, `/apps/contacts/*`, `/apps/invoice/*` | — | Tema / örnek uygulamalar |

**Auth sayfaları** smoke dışında tutulur; Playwright auth fixture ayrı test eder.

---

## Öncelik tanımları

| Öncelik | Anlam | CI |
|---------|-------|-----|
| **P0** | Kritik iş modülü; smoke zorunlu | Her PR |
| **P1** | Önemli modül; flow testleri | Nightly |
| **P2** | Destekleyici / admin araçları | Modül sprint'inde |
| **P3** | Düşük trafik veya legacy | İsteğe bağlı |

---

## Persona gereksinimleri

| Persona | `pageType` erişimi | Test amacı |
|---------|-------------------|------------|
| **admin** | admin, manager, user | Tam smoke + flow |
| **manager** | manager, user | Admin route → `/unauthorized` |
| **user** | user (grup izinli) | Kısıtlı menü + view/edit |

Middleware: `auth.global.js`, `menu-permission.global.ts`

---

## P0 — Kritik modüller

### Operation Core

| Route | Amaç | Persona | Gerekli veri |
|-------|------|---------|--------------|
| `/apps/operation-core` | OC ana giriş / yönlendirme | admin, manager | workspace seed |
| `/apps/operation-core/workspace` | Operasyon alanı explorer | admin, manager | workspace, board |
| `/apps/operation-core/boards/[boardId]` | Kanban / liste pano | admin, manager | board id |
| `/apps/operation-core/work-items/new` | Yeni iş kaydı | admin, manager | workspace, flow |
| `/apps/operation-core/work-items/[id]/profile` | İş kaydı profil | admin, manager | work item id |
| `/apps/operation-core/notifications` | Bildirim kutusu | admin, manager | — |
| `/apps/operation-core/dashboards/[id]` | OC dashboard görünümü | admin | dashboard id |

**Backend diagnostic eşlemesi:** [../diagnostic/scripts/diagnostic-operation-pages.ps1](../diagnostic/scripts/diagnostic-operation-pages.ps1)

### Widgets & Dashboards

| Route | Amaç | Persona | Gerekli veri |
|-------|------|---------|--------------|
| `/apps/widgets` | Widget listesi, filtre, CRUD | admin | `@widgets`, `@widget_categories` |
| `/apps/widgets/new` | Yeni widget | admin | kategoriler |
| `/apps/dashboards` | Dashboard listesi | admin | dashboard dataset |
| `/apps/monitoring/widgets` | Monitoring widget yönetimi | admin | monitoring seed |

**Manuel checklist:** `docs/content/Mng.Ui/support/specs/WIDGET_LIST_PAGE_TEST.md`

### Datasets & yapılandırma

| Route | Amaç | Persona | Gerekli veri |
|-------|------|---------|--------------|
| `/apps/datasets` | Dataset listesi | admin | — |
| `/apps/dataset-categories` | Kategori yönetimi | admin | — |
| `/apps/side-menu-manager` | Menü ve sayfa izinleri | admin | `@pages` |

---

## P1 — Önemli modüller

### Monitoring

| Route | Amaç | Persona |
|-------|------|---------|
| `/apps/monitoring` | Monitoring ana | admin |
| `/apps/monitoring/map` | Harita görünümü | admin |
| `/apps/monitoring/control` | Kontrol paneli | admin |
| `/apps/monitoring/organization` | Organizasyon ağacı | admin |
| `/apps/monitoring/config` | Konfigürasyon | admin |

### Alarm Center

| Route | Amaç | Persona |
|-------|------|---------|
| `/apps/alarm-center` | Alarm merkezi ana | admin |
| `/apps/alarm-center/alarms` | Alarm listesi | admin |
| `/apps/alarm-center/rules` | Kural yönetimi | admin |
| `/apps/alarm-center/notification-policies` | Bildirim politikaları | admin |

### Document Intelligence

| Route | Amaç | Persona |
|-------|------|---------|
| `/apps/document-intelligence` | Doküman ağacı, markdown | admin, user |

**Backend diagnostic:** [../diagnostic/scripts/diagnostic-document-intelligence-pages.ps1](../diagnostic/scripts/diagnostic-document-intelligence-pages.ps1)

### SIEM Center

| Route | Amaç | Persona |
|-------|------|---------|
| `/apps/siem-center` | SIEM ana | admin |
| `/apps/siem-center/events` | Olay listesi | admin |
| `/apps/siem-center/reference` | Referans veriler | admin |

---

## P2 — Destekleyici modüller

### Operation Core — Admin

| Route | Amaç | Persona |
|-------|------|---------|
| `/apps/operation-core/admin/definitions` | Tanım yönetimi | admin |
| `/apps/operation-core/admin/workflows` | İş akışları | admin |
| `/apps/operation-core/admin/workspace-definitions` | Workspace tanımları | admin |
| `/apps/operation-core/admin/scheduled-jobs` | Zamanlanmış işler | admin |
| `/apps/operation-core/admin/alarms` | OC alarm admin | admin |
| `/apps/operation-core/admin/approvals` | Onay tanımları | admin |
| `/apps/operation-core/admin/mail-templates` | Mail şablonları | admin |
| `/apps/operation-core/admin/alarm-rules` | Alarm kuralları | admin |

### Kullanıcı & domain yönetimi

| Route | Amaç | Persona |
|-------|------|---------|
| `/apps/users` | Kullanıcı listesi | admin |
| `/apps/users/create` | Kullanıcı oluşturma | admin |
| `/apps/groups` | Grup listesi | admin |
| `/apps/domain` | Domain yönetimi | admin |
| `/apps/profile` | Kullanıcı profili | admin, user |
| `/apps/notes` | Notlar | admin, user |

### Automation & Task Manager

| Route | Amaç | Persona |
|-------|------|---------|
| `/apps/automation-center` | Otomasyon merkezi | admin |
| `/apps/automation-center/workflows` | Workflow listesi | admin |
| `/apps/task-manager` | Görev yöneticisi ana | admin, manager |
| `/apps/task-manager/projects` | Proje listesi | admin, manager |
| `/apps/task-manager/boards/[boardId]` | TM pano | admin, manager |

### Diğer

| Route | Amaç | Persona | Öncelik |
|-------|------|---------|---------|
| `/apps/agents` | Agent yönetimi | admin | P2 |
| `/apps/engines` | Engine listesi | admin | P2 |
| `/apps/schedules` | Zamanlama | admin | P2 |
| `/apps/collection-periods` | Toplama periyotları | admin | P2 |
| `/apps/automated-forms` | Otomatik formlar | admin | P2 |
| `/apps/asset-type-definitions` | Varlık tip tanımları | admin | P2 |
| `/apps/license-management` | Lisans yönetimi | admin | P2 |
| `/apps/organization` | Organizasyon | admin | P2 |
| `/apps/events` | Olaylar | admin | P2 |
| `/apps/locale-editor` | i18n düzenleyici | admin | P3 |
| `/` veya `/welcome` | Ana sayfa | tüm persona | P1 |

---

## Smoke assertion şablonu (her P0/P1 route)

Playwright smoke testinde minimum kontroller:

1. Login sonrası route'a git — redirect `/auth/login` olmamalı
2. HTTP 200 (SPA — sayfa render)
3. `console.error` yok (allowlist ile bilinen uyarılar hariç)
4. Kritik network isteklerinde 5xx yok
5. Modüle özgü ana UI elementi görünür (tablo, explorer, canvas vb.)
6. Sayfa başlığı veya breadcrumb beklenen i18n key / metin

Detay assertion listesi modül spec'lerinde genişletilir.

---

## Güncelleme kuralı

1. Yeni `/apps/*` sayfası eklendiğinde bu dosya + `page-catalog.yml` güncellenir
2. Öncelik değişikliği DEVAM.md karar tablosuna işlenir
3. Manuel checklist varsa ilgili satıra link eklenir
