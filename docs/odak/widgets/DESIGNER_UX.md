# Widget & Dashboard Designer UX

**Son güncelleme:** 7 Haziran 2026  
**Durum:** 📋 Planlama v1 — implementasyon yok

---

## 1. İki ayrı designer

| Designer | Soru | Route (hedef) | Çıktı |
|----------|------|---------------|-------|
| **Widget Designer** | Ne gösterilecek? | `/apps/widgets/new`, `/apps/widgets/:id/edit` | `@widgets` kaydı |
| **Dashboard Designer** | Nerede duracak? | `/apps/dashboards/new`, `/apps/dashboards/:id/edit` | `@dashboards` layout |

Ortak: V1 template katalogu ([KATALOG_V1.md](./KATALOG_V1.md)).

---

## 2. Widget Designer — 4 adım wizard

```
[1 Katalog] → [2 Parametreler] → [3 Görünüm] → [4 Davranış]
     ↓              ↓                 ↓              ↓
 domain filtre   form (schema)    preset galeri   refresh, drill-down
 template arama  teknik yok       canlı önizleme  yetki grupları
```

### Adım 1 — Katalog

- Domain chip: Alarm | SIEM | MO | Dokümanlar
- Arama + tag (`kpi`, `realtime`)
- Kart: başlık, açıklama, kind ikonu
- **Klonla** → Definition draft

### Adım 2 — Parametreler

`parametersSchema` driven form — bkz. [MANIFEST_SCHEMA.md](./MANIFEST_SCHEMA.md)

- Dropdown, workspace seçici, süre preset
- Context binding gösterimi: “Dashboard filtresinden gelir” (disabled alan)
- **Gelişmiş mod** (admin accordion): ham JSON, aggregate — kapalı varsayılan

### Adım 3 — Görünüm (preset galeri)

- Thumbnail grid — [PRESENTATION_PRESETS.md](./PRESENTATION_PRESETS.md)
- Canlı önizleme (sample data veya gerçek fetch)
- Başlık / açıklama override

### Adım 4 — Davranış

- Yenileme: kapalı | 30s | 1m | 5m
- Tıklanınca: route picker + param map (kod yok)
- Yetki: grup multi-select

**Kaydet** → `@widgets` + `templateId` / `templateVersion` metadata

---

## 3. Dashboard Designer

Mevcut builder korunur ([DASHBOARD_BUILDER_MECHANISM.md](../../content/Mng.Ui/support/specs/DASHBOARD_BUILDER_MECHANISM.md)):

| Bölüm | Değişiklik (Faz 1) |
|-------|---------------------|
| Sol panel | Dashboard meta (name, title, slug, permissions) |
| Sağ panel | LayoutEditor — row/col |
| Widget picker | V1 katalog modal (domain filtre) |
| Hücre override | Çark menüsü — parametre / refresh (SurfaceContext override) |

**Surface toolbar** (Faz 2): time range + global variables — Grafana benzeri.

---

## 4. Kullanıcı persona

| Persona | Widget | Dashboard |
|---------|--------|-----------|
| NOC operatörü | Klon + parametre | Layout düzenlemez |
| Workspace lideri | MO queue widget | Workspace default pano |
| Domain admin | Yeni widget + gelişmiş | Dashboard CRUD |
| Tenant admin | Template seed görmez (isSystem) | Tüm dashboard’lar |

---

## 5. Mevcut formlardan geçiş

| Mevcut | Geçiş |
|--------|-------|
| `WidgetForm.vue` (teknik) | Wizard + gelişmiş mod accordion |
| `MonitoringWidgetForm` | Monitoring hazır olunca katalog filtresi; V1 dışı |
| `OcDashboardWidgetForm` | Faz 4 → unified wizard (MO filtresi) |

---

## 6. i18n

Anahtar prefix: `widgets.designer.*`, `dashboards.builder.*`  
Template başlıkları: manifest `title.tr` / `title.en`
