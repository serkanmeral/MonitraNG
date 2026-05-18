# OSM Arka Planın GeoServer’dan Offline Alınması

Bu dokümanda, MngSim tren haritası (ve benzeri sayfalar) için “OSM arka plan”ın kendi GeoServer’ınızdan offline sunulması seçenekleri özetleniyor.

---

## Mevcut Durum

- **Tren haritası (train-map.html):** “OSM arka plan” açıldığında tile’lar doğrudan `https://{s}.tile.openstreetmap.org/` adresinden alınıyor → **çevrimiçi**, internet gerekir.
- **GeoServer (mng_others):** `tr_rail` workspace’inde WMTS ile yayında:
  - `tr_rail:railways` — demiryolları
  - `tr_rail:stations` — istasyonlar
  - `tr_rail:places` — yerleşimler  
  Bu veriler OSM’den filtrelenmiş; fakat **tam OSM arka planı değil** (yollar, binalar, etiketler yok), sadece demiryolu odaklı katmanlar.

---

## Seçenekler

### 1. Hızlı çözüm: Mevcut GeoServer katmanlarını “arka plan” gibi kullanmak

- **Ne yapılır:** Tren haritasında “OSM arka plan”a ek olarak (veya yerine) **“GeoServer arka plan”** seçeneği eklenir.
- **Kaynak:** GeoServer WMTS (`/geoserver/gwc/service/wmts`) ile `tr_rail:railways`, `tr_rail:stations`, isteğe bağlı `tr_rail:places` aynı anda açılır.
- **Sonuç:** Arka plan tam OSM görünümü olmaz; arka plan gri/tek renk kalır, üzerinde **ray + istasyon (ve yerleşim)** kendi sunucunuzdan gelir → **tamamen offline** (GeoServer’a erişim yeterli).
- **Artı:** Az iş, mevcut altyapı yeterli.  
- **Eksi:** Yol, bina, etiket yok; sadece demiryolu haritası hissi.

**Teknik:** Leaflet’te mevcut `railway-map-preview.html` örneğindeki gibi WMTS URL’i kullanılır; GeoServer base URL’i (örn. `http://localhost:8082`) config veya env’den okunabilir.

---

### 2. Orta vadeli: GeoServer’da OSM tarzı tam arka plan

- **Ne yapılır:** PostGIS’e Türkiye OSM verisinin **tamamı** (veya ana katmanlar: roads, buildings, water, landuse vb.) import edilir; GeoServer’da bir veya birkaç layer olarak yayınlanır ve **OSM’e benzer bir stil** (SLD/CSS) uygulanır. İstenirse GeoWebCache (GWC) ile tile’lanır.
- **Sonuç:** Arka plan gerçekten “OSM benzeri” olur ve **tamamen kendi sunucunuzdan** (offline) gelir.
- **Artı:** Tam kontrol, çevrimiçi OSM’e bağımlılık kalmaz.  
- **Eksi:** Veri hazırlığı, stil ve GWC cache işi; daha fazla disk ve bellek.

**Not:** Geofabrik `turkey-latest.osm.pbf` ve mevcut OSM filtre/import script’leri bu veriyi PostGIS’e taşımak için genişletilebilir; ardından GeoServer’da basemap layer(lar) + stil tanımlanır.

---

### 3. Alternatif: Önceden render edilmiş statik tile’lar

- **Ne yapılır:** OSM verisi ile önceden üretilmiş tile set (örn. PNG `{z}/{x}/{y}.png` veya MBTiles) bir dizinden veya basit bir tile sunucusundan (nginx, minimal API vb.) sunulur.
- **Sonuç:** Leaflet’te `L.tileLayer('/tiles/{z}/{x}/{y}.png')` veya MBTiles eklentisi ile kullanılır; **offline** çalışır.
- **Artı:** GeoServer’a ihtiyaç yok; sadece statik dosya sunumu.  
- **Eksi:** Tile’ların kim tarafından, hangi bölge ve zoom için üretileceği ayrıca planlanmalı (TCDD GIS dokümanında “Müşteri/İdare sorumluluğunda” deniyor).

---

## Öneri (Kısa vadede)

1. **Tren haritasında:**  
   - “OSM arka plan (çevrimiçi)” aynen kalsın.  
   - Ek olarak **“GeoServer arka plan (çevrimdışı)”** ekleyin: mevcut WMTS ile `tr_rail:railways` + `tr_rail:stations` (ve isteğe bağlı `places`) tek bir “arka plan” gibi açılsın.  
   - GeoServer base URL’i (örn. `http://localhost:8082`) MngSim tarafında **config veya ortam değişkeni** ile verilsin; böylece farklı ortamlarda (localhost, sunucu) adres değiştirilebilir.

2. **İleride:**  
   - Tam OSM görünümü istenirse Seçenek 2 (GeoServer’da OSM basemap + stil) veya Seçenek 3 (statik tile) ayrı bir iş paketi olarak planlanabilir.

---

## Uygulama Adımları (Sadece Seçenek 1 için)

1. **MngSim:**  
   - `train-map.html` (veya ilgili harita sayfası) içinde GeoServer WMTS için bir Leaflet tile katmanı tanımlanır (URL formatı `railway-map-preview.html` ile aynı).  
   - Arayüzde “GeoServer arka plan” checkbox’ı eklenir; işaretlendiğinde bu katman(lar) haritaya eklenir.

2. **Config:**  
   - GeoServer base URL’i için `appsettings.json` veya ortam değişkeni (örn. `TrainSim:GeoServerBaseUrl`) kullanılır.  
   - Bu URL, train-map’e bir şekilde iletilir (örn. sayfa yüklenirken bir config endpoint’inden veya HTML’e gömülü script ile).

3. **CORS:**  
   - GeoServer’dan tile alınacaksa, tarayıcı MngSim’in origin’inden istek atar; GeoServer’da bu origin için CORS açık olmalı (gerekirse reverse proxy ile aynı origin yapılabilir).

Bu adımlar uygulandığında, “GeoServer arka plan” seçildiğinde arka plan tamamen kendi GeoServer’ınızdan (offline) gelir; OSM arka plan ise isteğe bağlı çevrimiçi seçenek olarak kalır.
