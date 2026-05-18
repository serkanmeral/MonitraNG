# Adım 6 — Tam OSM Benzeri Arka Plan: Adım Adım Rehber

Bu rehber, **yol**, **su** (akarsu + göl/deniz alanları) ve **arazi kullanımı** (orman, yeşil alan, yerleşim vb.) verilerini OSM’den alıp PostGIS’e yükleyip GeoServer’da yayınlamanızı ve tren haritasında göstermenizi adım adım anlatır. Böylece “GeoServer arka plan” tek başına OSM’e çok daha benzeyen bir görünüme kavuşur (tamamen çevrimdışı).

---

## Ön koşullar

- **turkey-latest.osm.pbf** dosyası `docs/content/offline_map/data/` içinde olmalı (Geofabrik’ten indirin).
- **PostGIS** ve **GeoServer** çalışıyor olmalı (örn. `mng_others` docker-compose ile).
- **GDAL (ogr2ogr)** bilgisayarınızda yüklü olmalı (veya Docker ile GDAL kullanacağınızı aşağıda not ettik).
- Mevcut **railways, stations, places** katmanları zaten çalışıyorsa Adım 6’yı ek olarak uygulayabilirsiniz.

---

## Adım 6.1 — OSM’den yol, su ve arazi verisini çıkarma

1. **Proje köküne göre** `docs/content/offline_map` klasörüne gidin:
   ```powershell
   cd c:\Serkan\iSIM\MonitraNG\docs\content\offline_map
   ```

2. **Sadece Adım 6 filtrelerini** kullanarak PBF ve GeoJSON üretin (mevcut railways/stations/places’a dokunmaz):
   ```powershell
   .\scripts\run-osm-filters.ps1 -ConfigPath (Join-Path (Get-Location).Path "osm-filters-basemap.json") -ProjectRoot (Get-Location).Path
   ```
   Docker ile (osmium kurulu değilse):
   ```powershell
   .\scripts\run-osm-filters.ps1 -ConfigPath (Join-Path (Get-Location).Path "osm-filters-basemap.json") -ProjectRoot (Get-Location).Path -UseDocker
   ```

3. Kontrol: `data/exports/` altında şu dosyalar oluşmalı:
   - `roads.geojson`
   - `waterways.geojson`
   - `water_areas.geojson`
   - `landuse.geojson`

---

## Adım 6.2 — PostGIS’te tabloları oluşturma

1. **Basemap tablolarını** (roads, waterways, water_areas, landuse) oluşturun. PowerShell’de (PostGIS konteyner adı `postgis` ise):
   ```powershell
   Get-Content c:\Serkan\iSIM\MonitraNG\docs\content\offline_map\scripts\init-basemap-tables.sql | docker exec -i postgis psql -U gisuser -d gis
   ```
   Konteyner adı farklıysa (örn. `mng_others_postgis_1`) `postgis` yerine onu yazın.

2. Hata almadan “CREATE TABLE” ve “CREATE INDEX” çıktıları gelmeli.

---

## Adım 6.3 — GeoJSON’ları PostGIS’e yükleme (ogr2ogr)

**ogr2ogr (GDAL) bilgisayarınızda yüklü değilse** aşağıdaki **Docker ile** bölümünü kullanın; kurulum gerekmez.

---

### Seçenek A — Docker ile (önerilen; ogr2ogr kurulumu yok)

1. **Tek script** ile dört GeoJSON’ı da yükleyin (PostGIS’e Docker’dan erişim için `host.docker.internal:5433` kullanılır):
   ```powershell
   cd c:\Serkan\iSIM\MonitraNG\docs\content\offline_map\scripts
   .\import-basemap-ogr2ogr-docker.ps1
   ```
2. PostGIS şifre veya port farklıysa script içindeki `$pgPort` ve `$pgPass` değişkenlerini düzenleyin.
3. İlk çalıştırmada `ghcr.io/osgeo/gdal:ubuntu-small-latest` imajı indirilir; birkaç dakika sürebilir.

---

### Seçenek B — Yerel ogr2ogr (GDAL kurulu ise)

Aşağıdaki komutlar **Windows PowerShell** içindir. PostGIS host’ta **port 5433** (veya kendi portunuz).

Her komutu **sırayla** çalıştırın:

**Yollar (roads):**
```powershell
cd c:\Serkan\iSIM\MonitraNG\docs\content\offline_map
$env:PGPASSWORD = "gispass"
ogr2ogr -f "PostgreSQL" "PG:host=localhost port=5433 dbname=gis user=gisuser password=gispass" "data/exports/roads.geojson" -nln osm_roads_raw -lco GEOMETRY_NAME=geom -lco FID=osm_fid -nlt PROMOTE_TO_MULTI -overwrite
```

**Akarsular (waterways):**
```powershell
ogr2ogr -f "PostgreSQL" "PG:host=localhost port=5433 dbname=gis user=gisuser password=gispass" "data/exports/waterways.geojson" -nln osm_waterways_raw -lco GEOMETRY_NAME=geom -lco FID=osm_fid -nlt PROMOTE_TO_MULTI -overwrite
```

**Su alanları (water_areas):**
```powershell
ogr2ogr -f "PostgreSQL" "PG:host=localhost port=5433 dbname=gis user=gisuser password=gispass" "data/exports/water_areas.geojson" -nln osm_water_areas_raw -lco GEOMETRY_NAME=geom -lco FID=osm_fid -nlt PROMOTE_TO_MULTI -overwrite
```

**Arazi (landuse):**
```powershell
ogr2ogr -f "PostgreSQL" "PG:host=localhost port=5433 dbname=gis user=gisuser password=gispass" "data/exports/landuse.geojson" -nln osm_landuse_raw -lco GEOMETRY_NAME=geom -lco FID=osm_fid -nlt PROMOTE_TO_MULTI -overwrite
```

Not: Port veya şifre farklıysa `PG:host=... port=... password=...` kısmını kendi ayarınıza göre değiştirin.

---

## Adım 6.4 — Staging’den asıl tablolara aktarma (SQL)

1. **Import script’ini** çalıştırın (staging tablolardan roads, waterways, water_areas, landuse doldurulur; staging tablolar silinir):
   ```powershell
   Get-Content c:\Serkan\iSIM\MonitraNG\docs\content\offline_map\scripts\import-basemap-to-postgis.sql | docker exec -i postgis psql -U gisuser -d gis
   ```

2. **Hata alırsanız** (ör. “column does not exist”): ogr2ogr bazen sütun adını farklı yazar (örn. `highway` yerine başka bir isim). O zaman:
   - `docker exec -it postgis psql -U gisuser -d gis -c "\d osm_roads_raw"` ile staging tablonun sütunlarını görün.
   - `scripts/import-basemap-to-postgis.sql` içindeki ilgili INSERT’te sütun adını (ör. `r.highway`) buna göre değiştirip tekrar çalıştırın.

---

## Adım 6.5 — GeoServer’da katmanları yayınlama

1. **tr_rail** workspace ve **postgis** store zaten varsa (railways/stations/places çalışıyorsa), sadece yeni katmanları ekleyin:
   ```powershell
   cd c:\Serkan\iSIM\MonitraNG\docs\content\offline_map\scripts
   .\add-basemap-layers-geoserver.ps1
   ```
   Şifre farklıysa: `.\add-basemap-layers-geoserver.ps1 -Password "sizin_sifre"`

2. İlk kez kuruyorsanız önce:
   ```powershell
   .\configure-geoserver-tr_rail.ps1
   ```
   sonra yine `.\add-basemap-layers-geoserver.ps1` çalıştırın.

3. **Tile cache’i temizleyin** (yeni veriyle tile’lar üretilsin). GeoServer arayüzü **İngilizce** ise:
   - Sol menüden **Tile Caching** → **Tile Layers**.
   - Tabloda her katmanın **Actions** sütununda **Seed/Truncate** linki vardır. **tr_rail:roads**, **tr_rail:waterways**, **tr_rail:water_areas**, **tr_rail:landuse** (ve isteğe bağlı **tr_rail:railways**, **tr_rail:stations**, **tr_rail:places**) için bu **Seed/Truncate** linkine tıklayın.
   - Açılan sayfada **Truncate** bölümünü bulun (“Truncate tile layer” / “Delete cached tiles”) ve **Submit** (veya **Truncate**) butonuna basın. Böylece o katmanın önbelleği temizlenir.
   - Listeye dönüp diğer katmanlar için aynı adımı tekrarlayın.
   - (Türkçe arayüzde: **Önbelleğe Alma** → **Döşeme Katmanları** → ilgili satırdaki **Seed/Truncate** → **Kes** / **Önbelleği Temizle**.)

---

## Adım 6.6 — Tren haritasında görüntüleme

1. **MngSim** ve **train-map** tarafı projede güncellendi: proxy artık `roads`, `waterways`, `water_areas`, `landuse` katmanlarını kabul ediyor; harita sayfasında “GeoServer arka plan” açıldığında bu katmanlar **altta**, ray/istasyon/yerleşim **üstte** çiziliyor.

2. MngSim’i yeniden başlatıp tren haritasını açın, **“GeoServer arka plan”** kutusunu işaretleyin. Sıra: en altta arazi (landuse), sonra su alanları, akarsular, yollar, raylar, istasyonlar, yerleşim isimleri.

3. **Görünmüyorsa:** GeoServer’da her katmanın **varsayılan stili** olabilir (tek renk çizgi/dolgu). İsterseniz **Data → Styles** ile yol/su/arazi için ayrı SLD ekleyip daha OSM benzeri renkler verebilirsiniz (yollar gri, su mavi, arazi açık yeşil vb.). Bu rehberde sadece veri ve katman yayını anlatılıyor; stil opsiyoneldir.

---

## Kısa kontrol listesi

| # | Ne yaptınız? | Komut / yer |
|---|----------------|-------------|
| 6.1 | OSM filtre (yol, su, arazi) | `run-osm-filters.ps1 -ConfigPath osm-filters-basemap.json` |
| 6.2 | PostGIS tabloları | `init-basemap-tables.sql` → docker exec psql |
| 6.3 | GeoJSON → PostGIS (staging) | ogr2ogr x4 (roads, waterways, water_areas, landuse) |
| 6.4 | Staging → nihai tablolar | `import-basemap-to-postgis.sql` → docker exec psql |
| 6.5 | GeoServer katmanları | `add-basemap-layers-geoserver.ps1` + tile cache Truncate |
| 6.6 | Haritada görme | MngSim aç, “GeoServer arka plan” işaretle |

---

## Sık karşılaşılan sorunlar

- **“PBF bulunamadı”:** `data/turkey-latest.osm.pbf` yok; Geofabrik’ten indirip `docs/content/offline_map/data/` içine koyun.
- **“Connection refused” (port 5433):** PostGIS konteyneri çalışmıyor veya port 5433 host’a yayınlanmamış; `docker ps` ile kontrol edin.
- **“ogr2ogr: command not found”:** GDAL kurulu değil. **Adım 6.3 Seçenek A** kullanın: `scripts/import-basemap-ogr2ogr-docker.ps1` (Docker ile ogr2ogr çalıştırır, kurulum gerekmez).
- **GeoServer’da katman boş:** PostGIS’te `SELECT count(*) FROM roads;` ile veri var mı bakın; yoksa 6.3–6.4’ü tekrarlayın.
- **Haritada sadece gri:** Tile cache eski kalmış olabilir; GeoServer’da ilgili katmanlar için Truncate yapın.

Bu rehberi adım sırasıyla uyguladığınızda, Adım 6 tam OSM benzeri arka planı kendi sunucunuzda kurmuş olursunuz. Bir adımda takılırsanız, hangi adım ve tam hata mesajı ile yazarsanız, o adıma özel netleştirme yapabiliriz.

---

## İleride: Cache’i hızlandırma

Tile’lar ilk istekte üretildiği için bazen yavaş hissedilebilir. Seed (önceden cache doldurma) veya performansı artırmak için:

- **Seed sayfasında "Number of tasks to use":** Varsayılan 1 yerine **4–8** yaparak paralel tile üretimi artırılabilir (Sunucu yüküne dikkat edin).
- **Sadece kullanılan zoom aralığını seed edin:** Örn. Zoom 5–12 (Türkiye genel görünüm için yeterli); Zoom start/stop’u buna göre seçin. Böylece süre kısalır.
- **Sadece ihtiyaç duyulan katmanları seed edin:** roads, landuse, railways, stations, places; su katmanları kapalıysa onları atlayın.
- **GeoServer bellek:** Container’a daha fazla RAM verirseniz (örn. `docker run -m 2g` veya compose’ta `mem_limit`) tile üretimi daha akıcı olabilir.
- **BlobStore / disk:** Cache’in tutulduğu dizin SSD üzerindeyse okuma/yazma daha hızlı olur (GeoServer/GeoWebCache ayarlarından BlobStore path değiştirilebilir).

Bu ayarlar opsiyoneldir; ihtiyaç hissettiğinizde Seed/Truncate sayfasından “Number of tasks” ve zoom aralığını güncelleyerek deneyebilirsiniz.
