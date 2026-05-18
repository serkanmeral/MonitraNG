# Collectible şablonları — Tasarım notu

**Amaç:** Asset türü tanımlarken collectible’ları tek tek eklemek yerine, önceden tanımlanmış **şablonlar** ile (özellikle SNMP ve HTTP için) hızlıca doldurmayı sağlamak. Şablon = toplama metodu + isim + collectibles listesi.

---

## 1. Kavram

- **Collectible şablonu:** Belirli bir **collection_method** (SNMP, HTTP, SSH, WMI, …) için hazırlanmış, isimlendirilmiş bir **collectibles** listesi.
- **Kullanım:** Asset türü oluştururken/düzenlerken kullanıcı toplama metodunu seçer, ardından “Şablon uygula” ile bu metoda ait bir şablon seçer; şablonun collectibles’ı forma **kopyalanır**. İstenirse satır ekleyip çıkararak düzenleyebilir.
- **Bağ:** Asset türü kaydında şablona referans tutmak zorunlu değil; şablon sadece **formu doldurmak** için kullanılır. Yani `mon_asset_types` şeması değişmez; collectibles alanı yine tip bazında saklanır.

---

## 2. Metoda göre farklar (SNMP vs HTTP)

| Metot | Collectible’da öne çıkan alanlar | Örnek |
|-------|-----------------------------------|--------|
| **SNMP** | `oid`, `data_type`, `overridable_params` (örn. oid, interval) | sysDescr, sysUpTime, interface stats |
| **HTTP** | `path`, `data_type`, isteğe bağlı `method` / `headers` (şema genişletilebilir), `overridable_params` | /api/disk, /api/memory, HTML selector veya JSON path |
| **SSH/WMI** | `metric_key`, `data_type`, `overridable_params` (interval vb.) | cpu, memory |

Şu aşamada **SNMP** ve **HTTP** odaklı ilerlenebilir. Mevcut collectible yapısı (code, name, data_type, metric_key, oid, path, overridable_params) her iki metot için de kullanılabilir: SNMP’te ağırlık **oid**’de, HTTP’de **path**’te.

---

## 3. Veri modeli (öneri)

Yeni dataset: **`mon_collectible_templates`**

| Alan | Tip | Zorunlu | Açıklama |
|------|-----|---------|----------|
| name | text | Evet | Şablon adı (örn. "SNMP - Sistem bilgisi", "HTTP - Disk metrikleri") |
| collection_method | text | Evet | SNMP, HTTP, SSH, WMI, … (şablon bu metoda özel) |
| description | text | Hayır | Açıklama |
| collectibles | object[] | Evet | Aynı yapı: code, name, data_type, metric_key?, oid?, path?, overridable_params? |

- **Index:** (collection_method, name) unique — aynı metot altında aynı isimde tek şablon.
- **Konum:** `mng_{domain_name}` (diğer monitoring dataset’leri gibi).

Asset türü tarafında **değişiklik yok:** `mon_asset_types.collectibles` aynen kalır; şablon sadece UI’da “kopyala” kaynağıdır.

### Dataset oluşturma

`mon_collectible_templates` dataset’i **Monitoring Faz 0** script’i ile oluşturulur:

- **Script:** `scripts/tests/MngDataGateway/dataset/setup-monitoring-datasets.ps1` (adım 0.3).
- Tüm monitoring dataset’lerini (9 adet) oluşturmak için script’i çalıştırın; token için `scripts/tests/MngDataGateway/auth/load-token.ps1` (ve `get-token.ps1`) kullanılır. Domain claim’li bir Keeper token gerekir.
- Sadece bu dataset henüz yoksa ve diğerleri zaten varsa, script 409/“zaten mevcut” durumunda diğer adımları atlayıp devam eder; 0.3’te `mon_collectible_templates` oluşturulur.

---

## 4. UI önerisi

### 4.1 Collectible şablonları sayfası

- **Yer:** Asset Type Tanımları sayfasında **üçüncü sekme** (“Şablonlar”) veya ayrı menü öğesi: “Collectible şablonları”.
- **İçerik:** Liste (tablo): Ad, Toplama metodu, Açıklama, İşlemler (Düzenle, Sil). Toolbar: Arama, Metot filtresi, Yenile, **Yeni şablon** (canEdit).
- **Form (modal veya ayrı sayfa):** Şablon adı, Toplama metodu (v-select: SNMP, HTTP, …), Açıklama, **Collectibles** — mevcut Asset Type formundaki gibi tekrarlayan satırlar (code, name, data_type, oid/path/metric_key, overridable_params). Metoda göre etiketleri vurgulayabilirsin (SNMP’te “OID”, HTTP’de “Path”).
- **Silme:** Şablon silinebilir; sadece form doldurma için kullanıldığı için asset türü tarafında kısıt gerekmez.

### 4.2 Asset türü formunda şablon kullanımı

- **Sıra:** Kullanıcı önce **Aile** ve **Toplama metodu**nı seçer.
- **“Şablon uygula”:** Toplama metodu seçildikten sonra görünen bir dropdown (veya buton + dialog): “Collectible şablonu seç”. Sadece seçili `collection_method`’a ait şablonlar listelenir.
- **Uygula:** Şablon seçilince forma `collectibles` **kopyalanır** (mevcut satırların üzerine yazılabilir veya “Mevcuda ekle” seçeneği sunulabilir; basit versiyonda doğrudan üzerine yazmak yeterli).
- Kullanıcı isterse satır ekleyip çıkararak düzenlemeye devam eder; kayıt yine `mon_asset_types.collectibles` olarak gider.

---

## 5. Uygulama sırası (özet)

1. DG’de **mon_collectible_templates** dataset’i (şema + gerekirse seed).
2. **Collectible şablonları** UI: liste + CRUD + collectibles editörü (SNMP/HTTP için aynı yapı; metot etiketleri isteğe bağlı).
3. **Asset türü formu:** Toplama metodu seçildikten sonra “Şablon uygula” alanı; seçilen şablonun collectibles’ının forma kopyalanması.
4. İleride: HTTP için path dışında method/headers/response_parser gibi alanlar şema ve UI’da genişletilebilir.

Bu doküman, collectible şablonu fikrini ve SNMP/HTTP odaklı ilk adımları tanımlar; uygulama detayları sayfa bileşenlerine göre güncellenebilir.
