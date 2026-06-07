# Widget — DG dataset tasarımı

**Son güncelleme:** 7 Haziran 2026  
**Karar:** Template katalogu **DG dataset** (`@widget_templates`) — paket JSON değil. Bkz. [DEVAM.md](../DEVAM.md) D1.

---

## 1. Genel

| Dataset | Amaç | Durum |
|---------|------|-------|
| `@widget_categories` | Kategori gruplama (domain / tip) | ✅ Mevcut |
| `@widgets` | Widget Definition (müşteri kaydı) | ✅ Mevcut — manifest alanları Faz 0’da genişletilecek |
| `@widget_templates` | Template Catalog (seed şablon) | 📋 Taslak — Faz 0 kurulum |
| `@dashboards` | Layout + placement | ✅ Mevcut |

**Konum:** Tenant domain Mongo (MngDataGateway).  
**Okuyucular:** Mng.Ui (designer, WidgetHost, dashboard builder).  
**Yönetici:** Domain admin; `isSystem: true` kayıtlar UI’da silinemez (kural).

**publish_mode:** `none` — widget verisi canlı DG sorgularından gelir; template CRUD sonrası event gerekmez.

**Ayrı backend servisi yok** — tüm CRUD ve veri çekme DG + UI üzerinden. Bkz. [ARCHITECTURE.md §13](../ARCHITECTURE.md#13-backend-sınırı-açık-karar).

---

## 2. `@widget_templates`

### 2.1 Neden dataset?

| Seçenek | Red / Kabul |
|---------|-------------|
| Paket JSON (repo only) | ❌ Tenant admin klonlayamaz; runtime güncelleme zor |
| Ayrı microservice | ❌ Gereksiz — bkz. backend sınırı kararı |
| **DG dataset** | ✅ Mevcut CRUD, yetki, seed script pattern’i; klonlama `@widgets`’a tek akış |

### 2.2 Alanlar

| Alan | Tip | Zorunlu | Açıklama |
|------|-----|---------|----------|
| `templateId` | text | Evet | Stabil kimlik, örn. `alarm.open-count-stat` — benzersiz |
| `templateVersion` | text | Evet | Semver, örn. `1.0.0` |
| `domain` | text | Evet | `alarm` \| `siem` \| `operation-core` \| `document-intelligence` \| `generic` |
| `category` | relation | Evet | `@widget_categories` |
| `title` | text | Evet | Designer katalog listesi (birincil dil veya `tr`) |
| `description` | text | Hayır | Kısa açıklama |
| `tags` | text[] | Hayır | `kpi`, `realtime`, … |
| `manifest` | object | Evet | Tam [WidgetTemplateManifest](../MANIFEST_SCHEMA.md) JSON |
| `isSystem` | bool | Evet | `true` = MonitraNG seed; silme/patch kısıtlı |
| `isActive` | bool | Evet | Pasif şablon designer’da gizlenir |
| `order` | number | Hayır | Katalog sıralama |

Üst düzey alanlar (`templateId`, `domain`, …) **filtre/indeks** için; canonical tanım `manifest` object içinde.

### 2.3 İş kuralları (UI / seed)

- **Klonla:** `@widget_templates` → yeni `@widgets` kaydı (`templateId` + `templateVersion` kopyalanır)
- **Sistem şablonu:** `isSystem: true` — yalnızca seed script veya platform admin günceller
- **Tenant şablonu:** Admin `isSystem: false` özel template ekleyebilir (ileride, gelişmiş mod)
- **Sürüm:** `templateVersion` artınca mevcut `@widgets` kayıtları eski sürümle kalır (`definition.templateVersion`)

### 2.4 Kurulum dosyaları

| Dosya | Açıklama |
|-------|----------|
| [widget-templates-dataset-create.json](./widget-templates-dataset-create.json) | DG dataset create JSON |
| `widget_templates_seed.json` | Faz 1 — Alarm/SIEM şablon seed (henüz yok) |
| `setup-widget-templates-datasets.ps1` | Faz 0 — kurulum script iskeleti (henüz yok) |

---

## 3. `@widgets` genişletmesi (Faz 0)

Mevcut şemaya eklenecek alanlar (object veya ayrı text):

| Alan | Tip | Açıklama |
|------|-----|----------|
| `templateId` | text | Kaynak şablon; yoksa `legacy.custom` |
| `templateVersion` | text | Klon anındaki şablon sürümü |
| `manifestVersion` | text | `1.0` |
| `manifest` | object | Opsiyonel — tam definition manifest (geçiş dönemi `dataSource` + `config` yeterli) |

Runtime adapter: `manifest` yoksa mevcut `type` / `dataSource` / `config` okunur.

---

## 4. Veri sorguları (queryRef)

Widget **verisi** `@widget_templates` veya `@widgets`’tan değil; ilgili **domain dataset** predefined query’lerinden gelir.

Örnek:

```
manifest.dataBinding.queryRef = "@alarms/queries/openCount"
  → POST /api/v1/data/@alarms/queries/openCount
```

Domain dataset’lerinde predefined query tanımı **ayrı iş paketi** — bkz. [DEVAM.md](../DEVAM.md) seed backlog.

---

## 5. İlgili eski spec

| Dosya | Konum |
|-------|-------|
| `@widgets` create JSON | [widgets-dataset-create.json](../../../content/Mng.Ui/support/specs/datasets/widgets-dataset-create.json) |
| `@widget_categories` | [widget-categories-dataset-create.json](../../../content/Mng.Ui/support/specs/datasets/widget-categories-dataset-create.json) |
