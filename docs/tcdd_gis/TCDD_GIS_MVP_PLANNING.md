# TCDD GIS MVP — Planlama Dökümanı

**Referans:** `teknik_sartname.pdf` (TCDD Kaynak Planlama ve Büyük Veri Analitiği Platformu Teknik Şartnamesi)  
**Tarih:** 2 Mart 2026  
**Platform:** MonitraNG (MngDataGateway, Mng.Ui, MngSim)

**Dataset prefix kuralı:** Tüm TCDD GIS dataset'leri `tcdd_gis_` prefix'i ile oluşturulacaktır.

---

## 1. Amaç ve Kapsam

Bu doküman, TCDD şartnamesindeki harita işlemlerini MonitraNG platformu üzerinde MVP olarak hayata geçirmek için yapılacak çalışmaları planlar. Şu aşamada odaklanılan alanlar:

1. **Harita işlemleri** — Online ve offline destekli harita bileşenleri
2. **Veri tanımlama** — Haritaya veri sağlayacak kayıt giriş ekranları
3. **Dashboard ve raporlama** — Harita widget’ları ve raporlar
4. **Simülasyon** — Sentetik veri üretimi (gerçek veri yokken test için)

---

## 2. Veri Kaynakları Matrisi

### 2.1 Hangi Veriler Nereden Gelir?

| Veri Türü | Kaynak | Açıklama |
|-----------|--------|----------|
| **Manuel giriş (kayıt ekranları)** | MngDataGateway dataset CRUD | Kullanıcı tarafından formlarla girilir |
| **Sentetik (simülatör)** | MngSim veya yeni sim | Gerçek sistem olmadan test verisi üretir |
| **Gerçek zamanlı (ileride)** | TİS, ATS, KKY vb. | Şartname kapsamında; MVP dışı |

### 2.2 Veri Türü Bazlı Matris

| Veri | Manuel Giriş Ekranı | Simülatör | Haritada Kullanım |
|------|---------------------|-----------|--------------------|
| **Lokasyon/Organizasyon** (istasyon, bölge, hat) | ✅ Evet | ❌ | ✅ Marker, hiyerarşi |
| **Güzergâh** (hat, rota, polyline) | ✅ Evet | Opsiyonel | ✅ Polyline çizim |
| **Varlık/Asset** (lokomotif, vagon, araç) | ✅ Evet | Opsiyonel | ❌ (konum üzerinden) |
| **Sefer/Tren konumu** (anlık koordinat) | ❌ | ✅ Evet | ✅ Hareketli marker |
| **Hız, yakıt, ATS durumu** | ❌ | ✅ Evet | ✅ Popup, renk kodu |
| **Alarm/Olay** (hız ihlali, güzergâh ihlali) | Opsiyonel (manuel alarm) | ✅ Evet | ✅ Harita üzerinde alarm |
| **Yük yoğunluğu, ton-km** | ❌ | ✅ Evet (türetilmiş) | ✅ Heatmap / katman |

---

## 3. Manuel Kayıt Giriş Ekranları (Dataset + Automated Forms)

MngDataGateway dataset’leri üzerinden CRUD; Mng.Ui Automated Forms ile dinamik formlar.

### 3.1 Oluşturulacak Dataset’ler ve Automated Form Tanımları

| Dataset | Açıklama | Kritik Alanlar | Form Kodu | Görüntülenme |
|--------|----------|----------------|-----------|--------------|
| `tcdd_gis_locations` | İstasyon, bölge, lokasyon | name, parentId, **location** {lat, lon}, kind, tags | `tcdd_gis_locations` | `/apps/automated-forms/view/tcdd_gis_locations` |
| `tcdd_gis_routes` | Güzergâh / hat tanımı | name, description, **geometry** (GeoJSON LineString), stations[] | `tcdd_gis_routes` | `/apps/automated-forms/view/tcdd_gis_routes` |
| `tcdd_gis_assets` | Varlık (lokomotif, vagon) | name, typeId, itemId, status, connection_info | `tcdd_gis_assets` | `/apps/automated-forms/view/tcdd_gis_assets` |
| `tcdd_gis_asset_types` | Varlık tipi (lokomotif, vagon) | name, collection_method, collectibles | `tcdd_gis_asset_types` | `/apps/automated-forms/view/tcdd_gis_asset_types` |
| `tcdd_gis_alerts_config` | Alarm eşikleri (hız, güzergâh) | type, threshold, routeId, action | `tcdd_gis_alerts_config` | `/apps/automated-forms/view/tcdd_gis_alerts_config` |

**Not:** `location` ve `geometry` (object tipi) için Automated Forms `object` field kullanılır; harita üzerinde seçim yapan bileşen eklenebilir (opsiyonel).

### 3.2 Lokasyon Girişi — location Object

- `location: { lat: number, lon: number }` — Mevcut `mon_items` ile uyumlu
- Opsiyonel: `location: { type: "Point", coordinates: [lon, lat] }` — GeoJSON standardı

### 3.3 Güzergâh Girişi — geometry

- `geometry: { type: "LineString", coordinates: [[lon, lat], ...] }` — GeoJSON
- Harita üzerinde çizim (CBS editör) veya koordinat listesi import

---

## 4. Simülatör Tarafı (Sentetik Veri)

### 4.1 Mevcut MngSim

- HTTP, SNMP, MQTT cihaz metrikleri
- Cihaz başına `location` (opsiyonel) mevcut
- **Eksik:** Tren/sefer, anlık konum akışı, hız, yakıt vb.

### 4.2 TCDD Tarzı Veri İçin Simülatör Seçenekleri

| Seçenek | Açıklama | Öneri |
|---------|----------|-------|
| **A) MngSim genişletme** | Yeni template: "Tren/Sefer" — konum, hız, yakıt üretir | ✅ Hızlı MVP |
| **B) Ayrı GIS Sim servisi** | Sadece harita için: konum akışı, alarm tetikleme | Gelecek faz |
| **C) Script / toplu veri** | Seed script ile `tcdd_gis_*` dataset’lere örnek veri | ✅ MVP’de yeterli |

**MVP Önerisi:** C + A kombinasyonu  
- C: Lokasyon, güzergâh, varlık tanımları → seed script  
- A: MngSim’e "Konum Simulator" modu → periyodik konum güncellemesi, MQTT/HTTP ile DataGateway’e veya doğrudan UI’a

### 4.3 Simülatörün Üreteceği Veriler

| Veri | Frekans | Format | Hedef |
|------|---------|--------|-------|
| Tren konumu (lat, lon) | 5–30 sn | JSON | `tcdd_gis_trip_positions` veya MQTT → real-time |
| Hız, yakıt tüketimi | Konumla birlikte | JSON | Trip detayı |
| Hız ihlali alarmı | Koşul sağlandığında | Event | Harita + bildirim |
| Güzergâh ihlali alarmı | Koşul sağlandığında | Event | Harita + bildirim |

---

## 4.4 Kural–Aksiyon ve MngWorkflow Entegrasyonu

**`tcdd_gis_alerts_config`** dataset'i kural tanımlarını tutar (hız eşiği, güzergâh ilişkisi, aksiyon türü). Bu yapı, **MngWorkflow** ile uyumludur.

| Kavram | TCDD GIS karşılığı | MngWorkflow karşılığı |
|--------|--------------------|------------------------|
| **Koşul (IF)** | `tcdd_gis_alerts_config`: type (hız_ihlali, güzergâh_ihlali), threshold, routeId | `mon_workflows`: collectibleCode, condition |
| **Aksiyon (THEN)** | `tcdd_gis_alerts_config`: action (bildirim, ui_alert vb.) | notification, http, email, ui_alert |
| **Tetikleyici** | Trip konum/hız verisi (`tcdd_gis_trip_positions` veya MQTT) | RabbitMQ — metrik/trip event |

**Entegrasyon senaryosu:**

1. Konum/hız verisi geldiğinde (simülatör veya gerçek TİS/ATS) RabbitMQ'ya event publish edilir.
2. MngWorkflow (veya TCDD'ye özel consumer) queue'dan alır; `tcdd_gis_alerts_config` ile koşul kontrolü yapar.
3. Eşleşme varsa aksiyon çalıştırılır: MngNotifier (bildirim), MngHub (UI uyarısı), e-posta vb.

**Gereksinimler:**

- MngWorkflow'un `tcdd_gis_*` event tipini ve hız/geo koşullarını desteklemesi (genişletme)
- Veya ayrı bir TCDD GIS rule consumer — `tcdd_gis_alerts_config` okuyup RabbitMQ'dan trip event'leri dinler

**MVP'de:** Simülatör kendi içinde alarm tetikleyebilir (basit mantık). MngWorkflow entegrasyonu **sonraki faz** olarak değerlendirilebilir.

---

## 5. Harita Fonksiyonları (Map Functions)

### 5.1 Temel Harita Altyapısı

| Fonksiyon | Açıklama | Online | Offline | Durum |
|-----------|----------|--------|---------|-------|
| Harita container | OpenLayers veya Leaflet | ✅ | ✅ | Mimari hazır |
| Altlık katmanı — Online | OSM, vb. tile servisi | ✅ | ❌ | Mevcut (OrganizationMapView) |
| Altlık katmanı — Offline | Statik tile (MBTiles, local tiles) | ❌ | ✅ | Henüz yok, mimari destekleyecek |
| Katman seçici | Hangi katmanın açık/kapalı olduğu | ✅ | ✅ | Eklenecek |
| Koordinat sistemi | EPSG:4326 (WGS84) varsayılan | ✅ | ✅ | Standart |

### 5.1.1 Harita Altlıkları — Karayolu ve Demiryolu Verisi

**Kullanılan servis:** OpenStreetMap (OSM) — `https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png`

**OSM standart katmanında bulunan veriler:**

| Veri tipi | Açıklama |
|-----------|----------|
| **Karayolları** | Yollar, caddeler, otoyollar |
| **Demiryolları** | Ana hatlar, şube hatları, metro, tramvay — Türkiye ve TCDD hatları dahil |
| **İstasyonlar** | Tren istasyonları, köprüler, tüneller |

Standart OSM tile’larında hem karayolları hem demiryolları gösterilir; demiryolları genelde ayrı bir çizgi stiliyle (kesikli/çift çizgi) ayırt edilir.

**Opsiyonel — Demiryoluna özel katman (OpenRailwayMap):**

Daha detaylı demiryolu gösterimi için OpenRailwayMap overlay katmanları kullanılabilir:

| Katman | URL | İçerik |
|--------|-----|--------|
| OpenRailwayMap Standard | `https://tiles.openrailwaymap.org/standard/{z}/{x}/{y}.png` | Ana hat, şube, metro, istasyon, köprü, tünel |
| Maxspeed | `https://tiles.openrailwaymap.org/maxspeed/{z}/{x}/{y}.png` | Hız sınırları |
| Electrification | `https://tiles.openrailwaymap.org/electrification/{z}/{x}/{y}.png` | Elektriklenmiş/elektriksiz hatlar |

Katman seçici ile bu altlıklar ek overlay olarak açılıp kapatılabilir.

### 5.2 Harita Üzerinde Gösterim Fonksiyonları

| Fonksiyon | Veri Kaynağı | Görsel |
|-----------|--------------|--------|
| Lokasyon marker’ları | `tcdd_gis_locations`, `mon_items` | Point, popup |
| Güzergâh çizgisi | `tcdd_gis_routes` | Polyline |
| Hareketli konum (anlık) | Simülatör veya gerçek API | Marker, trail |
| Alarm/Olay işaretleri | `tcdd_gis_alerts`, simülatör | Renkli marker, ikon |
| Yük yoğunluğu (ileride) | Türetilmiş veri | Heatmap katmanı |

### 5.3 Harita Araçları

| Araç | Açıklama |
|------|----------|
| Zoom in / out | Ölçek değiştirme |
| Pan (kaydırma) | Haritada gezinme |
| Koordinat gösterimi | İmlecin bulunduğu nokta (lat, lon) |
| Katman aç/kapa | POI, alarm, güzergâh katmanları |
| Basit CBS editör | Point, polyline, polygon çizimi (lokasyon/güzergâh girişi için) |
| Antetli çıktı (ileride) | A0–A4 PDF — sonraki faz |

### 5.4 Online / Offline Destek Mimarisi

- **Abstract tile provider:** Online = OSM (varsayılan) veya OpenRailwayMap overlay; Offline = `/tiles/{z}/{x}/{y}.png` (local) veya MBTiles
- **Config:** `mapBasemap: "online" | "offline"` — runtime’da değiştirilebilir
- **Offline altlık:** Şu an hazır değil; geliştirme sırasında interface/plugin yapısı offline’ı destekleyecek şekilde tasarlanacak

---

## 6. Dashboard ve Raporlama

### 6.1 Dashboard

| Bileşen | Açıklama |
|---------|----------|
| Harita widget | Lokasyon, güzergâh, anlık konum gösterimi |
| Harita–widget etkileşimi | Haritada seçim → diğer widget’lar filtre/güncelle |
| KPI widget’ları | Toplam varlık, aktif sefer, alarm sayısı |
| Alarm listesi widget | Son alarmlar, haritada tıklanınca odaklanma |

### 6.2 Raporlama

| Rapor | Veri | Format |
|-------|------|--------|
| Lokasyon listesi | `tcdd_gis_locations` | Tablo, filtreleme |
| Güzergâh raporu | `tcdd_gis_routes` | Harita + tablo |
| Konum geçmişi | `tcdd_gis_trip_positions` (sim/gerçek) | Zaman serisi, harita |
| Alarm özeti | `tcdd_gis_alerts` | Tablo, Excel/PDF |

---

## 7. Backend Değerlendirmesi

### 7.1 Mevcut Backend Yeterliliği

| Servis | Kullanım | Yeterli mi? |
|--------|----------|-------------|
| **MngDataGateway** | Dataset CRUD, geo veri (object/embed) | ✅ Evet |
| **MngKeeper** | Auth, domain | ✅ Evet |
| **MngHub** | Real-time (SignalR) — canlı konum push, UI uyarısı | ✅ Evet |
| **MngGateway** | API gateway | ✅ Evet |
| **MngNotifier** | Alarm bildirimi (e-posta, SMS) | ✅ Evet |
| **MngWorkflow** | Kural–aksiyon (IF koşul THEN aksiyon); `tcdd_gis_alerts_config` entegrasyonu | ⏳ Sonraki faz |

### 7.2 Coğrafi Veri ve Sorgular

- **MongoDB:** `object` alanında `{ type: "Point", coordinates: [lon, lat] }` saklanabilir
- **2dsphere index:** Coğrafi sorgular (`$near`, `$geoWithin`) için gerekli — MngDataGateway’e `geo` field type veya index tanımı eklenebilir
- **PostGIS:** Şartnamede geçiyor; MVP için zorunlu değil. İleride yoğun coğrafi analiz gerekirse değerlendirilir.

### 7.3 Yeni Backend Gerekir mi?

| İhtiyaç | Çözüm |
|---------|-------|
| Geo veri depolama | MngDataGateway + object/GeoJSON — **yeterli** |
| WMS/WFS (OGC) | GeoServer — **MVP dışı**; ileride ayrı servis |
| Tile sunucusu (offline) | Statik dosya sunumu (Mng.Ui static veya minimal servis) — **minimal** |
| Konum simülasyonu | MngSim genişletme veya seed script — **mevcut altyapı** |

**Sonuç:** Yeni bir backend servisi gerekmez. MngDataGateway, MngHub, MngSim mevcut yapılarıyla MVP için yeterlidir.

### 7.4 Olası MngDataGateway Eklentileri

- Dataset schema’ya `geo` veya `geojson` field type (opsiyonel; `object` da kullanılabilir)
- Geo index oluşturma (`2dsphere`) — index tanımına `geo` flag
- Coğrafi query endpoint: `GET /data/{dataset}?near=lon,lat&maxDistance=5000`

---

## 8. Uygulama Sırası (Fazlar)

| Faz | İçerik | Bağımlılık |
|-----|--------|------------|
| **Faz 1** | Harita altyapısı (online+offline mimari), lokasyon marker, güzergâh polyline | - |
| **Faz 2** | Dataset’ler + kayıt giriş ekranları (locations, routes, alerts-config) | Faz 1 |
| **Faz 3** | Dashboard + harita widget, etkileşim | Faz 1, 2 |
| **Faz 4** | Simülatör (konum + alarm) veya seed script | Faz 2 |
| **Faz 5** | Raporlama, dışa aktarma | Faz 3 |

---

## 9. Eksikler ve Netleşmemiş Noktalar

Bu bölüm, geliştirme öncesi netleştirilmesi veya karar verilmesi gereken alanları listeler.

### 9.1 Veri Giriş (Automated Forms)

| Konu | Mevcut Durum | Netleşmesi Gereken |
|------|--------------|---------------------|
| **Lokasyon alanı (location)** | DynamicFormField `object` tipi = JSON textarea. `LocationPickerModal` sadece `OrganizationItemForm` içinde kullanılıyor; Automated Forms'a entegre değil. | MVP'de manuel JSON (`{"lat":39.9,"lon":32.8}`) ile mi devam edecek; yoksa `location` veya `geopoint` field type'ı DynamicFormField'a eklenip LocationPickerModal entegre edilecek mi? |
| **Geometry alanı (güzergâh)** | Aynı şekilde `object` = JSON textarea. Harita üzerinde polyline çizim (CBS editör) yok. | MVP'de koordinat listesi (JSON LineString) manuel girilecek mi; yoksa basit bir polyline çizim bileşeni (Faz 2 veya sonrası) planlanacak mı? |
| **CBS editör** | Harita üzerinde nokta/çizgi çizimi Automated Forms'ta tanımlı değil. | "Basit CBS editör" Faz 1'de mi, Faz 2'de mi; yoksa sonraki fazda mı ele alınacak? |

### 9.2 Dataset ve Şema

| Konu | Mevcut Durum | Netleşmesi Gereken |
|------|--------------|---------------------|
| **tcdd_gis_trip_positions** | Simülatörün hedef dataset'i olarak geçiyor; şema tanımı yok. | Alan listesi: `tripId`, `assetId`, `lat`, `lon`, `speed`, `timestamp`, vb. Tam JSON schema MngDataGateway için hazırlanacak mı? |
| **tcdd_gis_alerts** | Raporlama ve haritada alarm gösterimi için referans var; şema yok. | Hangi alanlar? Örn: `assetId`, `type`, `severity`, `location`, `routeId`, `message`, `timestamp`. Dataset oluşturulacak mı? |
| **mon_assets vs tcdd_gis_assets** | SOW'da "Mevcut mon_assets genişletmesi veya tcdd_gis_assets" — belirsiz. | Hangisi kullanılacak? `mon_assets` varsa TCDD GIS'e özel alanlar nasıl eklenecek? Ayrı `tcdd_gis_assets` tercih edilirse `mon_items` / lokasyon ile ilişki nasıl kurulacak? |
| **tcdd_gis_routes.stations[]** | `stations[]` relation mi, ID dizisi mi? | `tcdd_gis_locations` __dataId referansları mı; sıra nasıl tutulacak? |
| **tcdd_gis_locations.parentId** | Hiyerarşi için parent referansı. | Aynı dataset içinde self-relation; MngDataGateway relation kurallarına uygun mu? |
| **Domain/Tenant** | `mon_*` dataset'leri `mng_{domain}` içinde. | `tcdd_gis_*` dataset'leri de domain-scoped mı, yoksa global mi? |

### 9.3 Harita ve Widget

| Konu | Mevcut Durum | Netleşmesi Gereken |
|------|--------------|---------------------|
| **Harita widget tipi** | WIDGET_LIBRARY_SPEC'te `card`, `chart`, `table`, `banner` var; `map` yok. | Dashboard harita widget'ı için yeni `map` tipi mi eklenecek; yoksa mevcut bir widget (örn. `card` içinde embedded harita) ile mi çözülecek? |
| **WidgetRenderer** | Harita bileşeni için özel render path var mı bilinmiyor. | `map` tipinde `OrganizationMapView` veya yeni `GisMapWidget` kullanılacak mı? |
| **Offline tile** | Mimari "destekleyecek" deniyor; tile kaynağı belirsiz. | Tile dosyası (MBTiles veya z/x/y) kim tarafından, hangi bölge için, hangi formatta sağlanacak? SOW'da "Müşteri/İdare sorumluluğunda" yazıyor — bu onaylanmış mı? |

### 9.4 Simülatör ve Veri Akışı

| Konu | Mevcut Durum | Netleşmesi Gereken |
|------|--------------|---------------------|
| **Konum simülasyonu mimarisi** | "MngSim genişletme" veya "ayrı GIS Sim" seçenekleri var; karar net değil. | MVP'de MngSim'e mi eklenecek; yoksa basit bir seed script + toplu insert yeterli mi? Canlı hareket için MQTT/HTTP → MngHub SignalR akışı tasarlandı mı? |
| **trip_positions yazma** | Simülatör "tcdd_gis_trip_positions veya MQTT → real-time" diyor. | Konum verisi DataGateway `POST /data/tcdd_gis_trip_positions` ile mi yazılacak; yoksa MQTT üzerinden MngHub'a mı gidecek? Her iki yol da kullanılacaksa hangisi öncelikli? |
| **Alarm MVP akışı** | "Simülatör kendi içinde alarm tetikleyebilir" — detay yok. | Simülatör alarm tetiklediğinde `tcdd_gis_alerts` dataset'ine mi yazacak; MngHub'a real-time push mı; yoksa sadece UI'da mı gösterilecek? |
| **Seed script** | "C: Seed script ile statik veri" öneriliyor. | Kim hazırlayacak? Hangi dataset'lere hangi örnek veriler? Script formatı (PowerShell, Node, curl)? |

### 9.5 Raporlama ve Menü

| Konu | Mevcut Durum | Netleşmesi Gereken |
|------|--------------|---------------------|
| **Rapor sayfaları** | Lokasyon listesi, güzergâh, konum geçmişi, alarm özeti listeleniyor. | Bu raporlar ayrı sayfalar mı (örn. `/apps/gis/reports/locations`); yoksa Dashboard widget'ları veya Automated Forms list view ile mi karşılanacak? |
| **Konum geçmişi raporu** | `tcdd_gis_trip_positions` zaman serisi + harita. | Özel bir sayfa mı; predefined query mi; yoksa genel tablo + harita widget kombinasyonu mu? |
| **Menü / navigasyon** | GIS sayfalarının menüde nerede görüneceği belirtilmemiş. | Yeni "GIS" veya "Harita" menü grubu mu; yoksa mevcut "Monitoring" veya "Apps" altına mı eklenecek? Side Menu / @side_menu yapılandırması var mı? |

### 9.6 MngWorkflow (Sonraki Faz)

| Konu | Mevcut Durum | Netleşmesi Gereken |
|------|--------------|---------------------|
| **Trip event formatı** | RabbitMQ'ya publish edilecek event yapısı tanımlı değil. | Örn: `{ "tripId", "assetId", "lat", "lon", "speed", "timestamp", "routeId" }` — standart format ne olacak? |
| **Routing key / queue** | Hangi queue, hangi exchange? | `tcdd_gis_trip_events` benzeri bir kuyruk mu kullanılacak? |
| **Hız/geo koşul türleri** | `tcdd_gis_alerts_config` yapısı MngWorkflow condition ile eşleşiyor mu? | Condition tipi enum'u (`speed_gt`, `outside_route` vb.) net mi? |

### 9.7 Özet Aksiyonlar

| Öncelik | Aksiyon |
|---------|---------|
| Yüksek | `location` ve `geometry` için MVP yaklaşımı kararı (manuel JSON vs. LocationPicker/CBS editör) |
| Yüksek | `mon_assets` vs `tcdd_gis_assets` kararı |
| Yüksek | `tcdd_gis_trip_positions` ve `tcdd_gis_alerts` dataset şemalarının çıkarılması |
| Orta | Harita widget tipi ve WidgetRenderer entegrasyonu |
| Orta | Simülatör konum akışı mimarisi (DG vs MQTT) |
| Orta | GIS menü/navigasyon yapısı |
| Düşük | Offline tile temini süreci |
| Düşük | MngWorkflow trip event formatı (sonraki faz) |

---

## 10. Şartname Uyumu

Planlarımızın TCDD teknik şartnamesini ne ölçüde karşıladığı ve karşılayamadığımız maddeler, ayrı bir dokümanda özetlenmiştir:

- **[TCDD GIS Şartname Uyum Matrisi](./TCDD_GIS_SARTNAME_UYUM.md)** — Karşılanan / kısmen karşılanan / karşılanamayan maddeler

---

## 11. İlgili Belgeler

- [TCDD GIS SOW](./TCDD_GIS_SOW.md) — Scope of Work (müşteri teslimat ve kabul kriterleri)
- [TCDD GIS SOP](./TCDD_GIS_MAP_SOP.md) — Standart işlem prosedürleri
- [Automated Forms Kullanım Kılavuzu](../content/Mng.Ui/support/guides/AUTOMATED_FORMS_USAGE.md)
- [TCDD Teknik Şartname](./teknik_sartname.pdf)
- [OrganizationMapView](../../Mng.Ui/components/apps/organization/OrganizationMapView.vue) — Mevcut harita bileşeni
- [LocationPickerModal](../../Mng.Ui/components/apps/organization/LocationPickerModal.vue) — Harita üzerinde nokta seçimi (OrganizationItemForm'da kullanılıyor)
- [MngSim README](../../MngSim/README.md)
- [MngWorkflow Planı](../content/monitoring_plans/MONITORING_WORKFLOW.md)
- [Raporlama Servisi Vizyon Notları](./REPORTING_SERVICE_VISION.md) — ayrı raporlama servisi fikri; detaylı planlama sonraya
- [TCDD GIS Şartname Uyum Matrisi](./TCDD_GIS_SARTNAME_UYUM.md) — hangi şartname maddelerinin karşılandığı ve karşılanamadığı
