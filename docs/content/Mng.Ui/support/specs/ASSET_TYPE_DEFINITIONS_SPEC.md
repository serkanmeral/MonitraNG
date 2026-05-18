# Asset Type Tanımları — UI Tasarım Önerisi

**Amaç:** Asset Type Family (`mon_asset_type_family`) ve Asset Type (`mon_asset_types`) için tanımlama sayfalarının nasıl olacağını netleştirmek. Referans: [MONITORING_ASSET_DATASETS](../../../monitoring_plans/MONITORING_ASSET_DATASETS.md).

---

## 1. Önerilen Yapı: Tek Sayfa, İki Sekme

Tek bir giriş noktası, iki kavram (Aile + Tip) aynı sayfada **sekmeler** ile ayrılsın; kullanıcı “Asset Type tanımları”na girdiğinde hem aileleri hem tipleri görebilsin, arada geçiş kolay olsun.

| Öğe | Değer |
|-----|--------|
| **Route** | `/apps/asset-type-definitions` (veya `/apps/monitoring/asset-type-definitions`) |
| **Sayfa başlığı** | "Asset Type Tanımları" |
| **Layout** | `default` (BaseBreadcrumb + v-container) |
| **İçerik** | **v-tabs**: "Aileler" \| "Tipler" |

---

## 2. Sekme 1: Aileler (mon_asset_type_family)

### 2.1 Görünüm

- **Üst toolbar:** Arama (opsiyonel), **Yenile**, **Yeni Aile** (sadece `canEdit` → is_manager / is_admin).
- **Tablo (v-data-table):**
  - Kolonlar: **Ad** (name), **Kod** (code), **Açıklama** (description, kısaltılmış), **İşlemler** (Düzenle, Sil).
  - Silme: Onay modal’ı (Organizasyon sayfasındaki gibi).
- **Ekleme / düzenleme:** Ayrı sayfa veya modal.

### 2.2 Aile formu alanları

| Alan | Tip | Zorunlu | UI |
|------|-----|---------|-----|
| name | text | Evet | v-text-field "Aile adı" |
| code | text | Hayır | v-text-field "Kod (slug)" |
| description | text | Hayır | v-textarea "Açıklama" |

- **Kaydet**, **İptal**. Yetki: Kaydet/ Sil sadece `canEdit`.

---

## 3. Sekme 2: Tipler (mon_asset_types)

### 3.1 Görünüm

- **Üst toolbar:** Arama (opsiyonel), **Aile filtresi** (dropdown: tümü / belirli bir aile), **Yenile**, **Yeni Tip** (sadece `canEdit`).
- **Tablo:**
  - Kolonlar: **Ad** (name), **Aile** (family → aile adı, relation expand veya manuel join), **Toplama metodu** (collection_method), **Açıklama** (kısaltılmış), **İşlemler**.
  - Silme: Onay modal’ı.
- **Ekleme / düzenleme:** Ayrı sayfa veya geniş modal (collectibles için yer lazım).

### 3.2 Tip formu alanları

| Alan | Tip | Zorunlu | UI |
|------|-----|---------|-----|
| name | text | Evet | v-text-field "Tip adı" |
| family | relation | Evet | v-select "Aile" (mon_asset_type_family listesi) |
| collection_method | text | Evet | v-select "Toplama metodu" (SSH, WMI, SNMP, SNMP_V3, REST, OPC_UA) |
| description | text | Hayır | v-textarea "Açıklama" |
| collectibles | object[] | Evet | Aşağıda |

### 3.3 Collectibles editörü

Her öğe: `code`, `name`, `data_type`, (metric_key \| oid \| path), `overridable_params`.

- **Liste:** v-card içinde tekrarlayan satırlar (v-for).
- **Her satır:**
  - **Kod** (code) — v-text-field, zorunlu.
  - **Görünen ad** (name) — v-text-field.
  - **Veri tipi** (data_type) — v-select: number, string, object.
  - **Kaynak:** metric_key \| oid \| path — üç alandan biri (veya tek satırda 3 alan, boş bırakılabilir).
  - **Override parametreleri** (overridable_params) — virgülle ayrılmış metin (örn. "oid, interval") veya chip/input list.
  - **Sil** (satırı kaldır).
- **"Collectible ekle"** butonu ile yeni satır eklenir.
- En az bir collectible zorunlu (validasyon).

---

## 4. Alternatif: İki Ayrı Sayfa

Projede dataset-categories gibi **liste + create + edit** ayrı sayfalar tercih edilirse:

| Route | İçerik |
|-------|--------|
| `/apps/asset-type-families` | Aile listesi (tablo + Yeni Aile, Düzenle, Sil) |
| `/apps/asset-type-families/create` | Yeni aile formu |
| `/apps/asset-type-families/edit/[dataId]` | Aile düzenleme formu |
| `/apps/asset-types` | Tip listesi (tablo + Aile filtresi, Yeni Tip, Düzenle, Sil) |
| `/apps/asset-types/create` | Yeni tip formu (family, collection_method, collectibles) |
| `/apps/asset-types/edit/[dataId]` | Tip düzenleme formu |

- Menüde iki link: "Asset Type Aileleri", "Asset Tipler" (veya tek "Asset Type Tanımları" altında alt menü).
- Form bileşenleri: `AssetTypeFamilyForm.vue`, `AssetTypeForm.vue` (collectibles editör dahil).

---

## 5. Ortak Kurallar

- **Yetkilendirme:** Ekleme / düzenleme / silme butonları ve form aksiyonları sadece **is_manager** veya **is_admin** için (Organizasyon sayfasındaki gibi `canEdit = authStore.isManager`). Salt liste ve arama herkese açık.
- **API:** MngDataGateway data API — `mon_asset_type_family`, `mon_asset_types` (GET list, GET one, POST, PUT/PATCH, DELETE). Domain `mng_{domain_name}` üzerinden.
- **Breadcrumb:** Dashboard → Asset Type Tanımları (ve sekme/adım için ek kırıntı istenirse).
- **Silme onayı:** `confirm()` yerine modal (v-dialog) kullanılır.
- **Hata / boş durum:** Liste yüklenirken loading; boş liste için "Henüz aile/tip eklenmemiş" benzeri mesaj.
- **Aile silme kısıtı:** Bir asset tür ailesi altında en az bir asset türü tanımlıysa o aile silinemez. UI’da bu aile için Sil butonu devre dışı olmalı (veya tıklanınca uyarı gösterilmeli); backend’de de aynı kural uygulanabilir.

---

## 6. Önerilen Sıra (Tek sayfa + sekmeler)

1. **Route ve boş sayfa:** `pages/apps/asset-type-definitions/index.vue` — sadece başlık + v-tabs (Aileler, Tipler), içerik boş.
2. **Aileler sekmesi:** DG’den `mon_asset_type_family` listesi, tablo, Yeni Aile / Düzenle / Sil (store + form modal veya ayrı sayfa).
3. **Tipler sekmesi:** DG’den `mon_asset_types` listesi (family expand veya aile adı için ayrı çekim), tablo, Yeni Tip / Düzenle / Sil.
4. **Tip formu:** Family select, collection_method, description, collectibles editörü (tekrarlayan satırlar).
5. **Menü:** Apps (veya Monitoring) altında "Asset Type Tanımları" linki.

Bu sırayla ilerlenebilir; istersen önce sadece **Aileler** sekmesini bitirip sonra **Tipler**’e geçebiliriz.
