# GeoServer Harita Verisini Zenginleştirme

Tren haritasında (MngSim train-map ve GeoServer WMTS) görünen öğeleri artırmak için aşağıdaki adımlar uygulanabilir.

---

## 1. Yerleşimler (places) katmanı

**Durum:** GeoServer’da `tr_rail:places` zaten yayında (şehir, ilçe, köy, mezra). Tren haritası ve tile proxy artık bu katmanı da kullanıyor.

- **MngSim:** “GeoServer arka plan” işaretlendiğinde **ray + istasyon + yerleşim** birlikte yüklenir.
- Veri yoksa veya boşsa: PostGIS’te `places` tablosunun dolu olduğundan ve GeoServer’da `tr_rail:places` layer’ının publish edildiğinden emin olun (bkz. `scripts/configure-geoserver-tr_rail.ps1`).

---

## 2. OSM filtrelerini genişletme (daha fazla ray ve istasyon)

Mevcut **osm-filters.json** aşağıdaki ek değerlerle güncellendi; veriyi yeniden üretmeniz gerekir.

### Railways (hatlar)

- **Eski:** `rail`, `tram`, `subway`, `light_rail`
- **Yeni (eklenen):** `narrow_gauge`, `preserved`
  - Dar hat ve turistik/tarihî hatlar da haritada görünür.

### Stations (istasyonlar)

- **Eski:** `station`, `halt`
- **Yeni (eklenen):** `subway_station`
  - Metro istasyonları da nokta olarak görünür.

### Veriyi yeniden üretme

1. **OSM filtre + export** (GeoJSON/PBF):
   ```powershell
   cd docs\content\offline_map
   .\scripts\run-osm-filters.ps1 -ProjectRoot (Get-Location).Path
   ```
   veya Docker ile:
   ```powershell
   .\scripts\run-osm-filters.ps1 -UseDocker -ProjectRoot (Get-Location).Path
   ```
   Çıktılar: `data/exports/railways.geojson`, `stations.geojson`, `places.geojson` (ve PBF’ler).

2. **PostGIS’e yükleme** (staging + aktarım):
   - `railway-platform.md` Bölüm 7’deki **ogr2ogr** komutları ile `osm_railways_raw`, `osm_stations_raw`, `osm_places_raw` tablolarını doldurun.
   - **Mevcut railways/stations/places verisini temizleyip** staging’den aktarın:
     ```sql
     TRUNCATE railways, stations, places;
     -- ardından import-staging-to-postgis.sql içeriğini çalıştırın
     ```
   - Veya `scripts/import-staging-to-postgis.sql` öncesi adımları uygulayıp INSERT’leri çalıştırın (staging tablolar zaten ogr2ogr ile doldurulmuş olmalı).

3. **GeoServer cache:** Veri güncellendiğinde GeoServer tile cache’i eski kalabilir. Gerekirse Tile Caching → Tile Layers → ilgili katman → **Seed/Truncate** ile cache’i temizleyin veya yeniden seed edin.

---

## 3. OSM kaynak verisini güncelleme

Daha güncel OSM verisi için Geofabrik’ten Türkiye PBF’i tekrar indirin:

- https://download.geofabrik.de/europe/turkey.html → **turkey-latest.osm.pbf**
- Dosyayı `docs/content/offline_map/data/turkey-latest.osm.pbf` konumuna koyun.
- Ardından yukarıdaki “Veriyi yeniden üretme” adımlarını (filtre script → ogr2ogr → PostGIS aktarım → isteğe bağlı cache temizleme) uygulayın.

---

## 4. Filtreye başka tipler eklemek

**osm-filters.json** içindeki `filters` dizisinde:

- **railways:** `railway` etiketinin diğer değerleri (örn. `construction`, `disused`) eklenebilir. OSM Wiki: [Key:railway](https://wiki.openstreetmap.org/wiki/Key:railway).
- **stations:** `railway` etiketinde `junction`, `crossing` eklenebilir (çok sayıda nokta oluşturabilir).
- **places:** `place` etiketinde `locality`, `suburb` vb. eklenebilir.

Değişiklikten sonra `run-osm-filters.ps1` tekrar çalıştırılmalı, ardından PostGIS import ve (isteğe bağlı) GeoServer cache işlemi yapılmalı.

---

## 6. OSM’e benzer görünüm: Şehir / ilçe / köy isimleri (etiketler)

Yerleşim noktalarının yanında **isimlerin** (Ankara, İstanbul, ilçe ve köy adları) görünmesi için GeoServer’da `places` katmanına **etiket (label) stili** uygulanır.

### Hazır SLD ve script

- **Stil dosyası:** `scripts/styles/places_labels.sld`  
  - `place_type` değerine göre farklı punto: **city** (şehir) büyük kalın, **town** (ilçe) orta, **village** (köy) ve **hamlet** (mezra) daha küçük.  
  - Etiket rengi koyu gri, beyaz halo ile okunaklı.

- **Uygulama:** Aynı dizindeki PowerShell script’i çalıştırın (GeoServer çalışıyor olmalı, PostGIS’te `places` verisi olmalı):
  ```powershell
  cd docs\content\offline_map\scripts
  .\apply-places-labels-style.ps1
  ```
  Varsayılan: `http://localhost:8082/geoserver`, kullanıcı `admin`, şifre `geoserver`. Farklı sunucu/şifre için parametreleri kullanın:
  ```powershell
  .\apply-places-labels-style.ps1 -BaseUrl "http://localhost:8082/geoserver" -Password "sizin_sifre"
  ```
  Script, `places_labels` stilini oluşturur/günceller ve `places` katmanının varsayılan stili yapar.

- **Tile cache:** WMTS tile önbelleği kullanıyorsanız, stili değiştirdikten sonra cache’i temizleyin: **Tile Caching** (Türkçe: **Önbelleğe Alma** / **Döşeme Önbelleği**) → **Tile Layers** → **tr_rail:places** → **Truncate** (Türkçe: **Kes** / **Önbelleği Temizle**). Böylece yeni etiketli tile’lar üretilir.

### Sonuç

- Tren haritasında “GeoServer arka plan” açıkken **ray + istasyon + yerleşim isimleri** (şehir, ilçe, köy) birlikte görünür; görünüm OSM’e daha yakın olur.

---

## 7. Özet

| Hedef | Yapılacak |
|------|-----------|
| Haritada yerleşimler (şehir/köy) görünsün | Zaten açık; `tr_rail:places` proxy ve train-map’te kullanılıyor. PostGIS/GeoServer’da layer yoksa ekleyin. |
| **Şehir / ilçe / köy isimleri (etiket) görünsün** | **`scripts/apply-places-labels-style.ps1` çalıştırın; gerekirse places tile cache’i truncate edin.** |
| Daha fazla ray (dar hat, tarihî hat) | osm-filters.json güncel (narrow_gauge, preserved). Veriyi yeniden üretip PostGIS’e import edin. |
| Metro istasyonları görünsün | osm-filters.json güncel (subway_station). Veriyi yeniden üretip PostGIS’e import edin. |
| OSM verisi güncel olsun | turkey-latest.osm.pbf’i indirip 1–2. adımları tekrarlayın. |
