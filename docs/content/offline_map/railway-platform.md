# Railway Operations & GIS Editing Platform (TR) — Offline Dev Setup (Docker Desktop)

> Bu doküman Serkan'ın ihtiyaçlarına göre **tek bir geliştirme rehberi** olarak hazırlanmıştır.  
> Hedef: **Türkiye demiryolu odaklı** (OSM tabanlı) statik harita + **realtime tren izleme** + **alarm** + **UI'dan statik altyapı edit**.  
> Tüm bileşenler **ücretsiz / open‑source** ve **offline** çalışacak şekilde kurgulanmıştır.

---

## 0) Kapsam ve Hedefler

### 0.1 Operasyon (Viewer Mode)
- Demiryolu hatlarını (raylar) ve istasyonları haritada görmek
- Trenlerin **anlık konumlarını** görmek (harita üzerinde hareket eden marker'lar)
- Alarm durumlarında haritaya **işaret** koyabilmek
- Tren veya alarma tıklayınca **modal** içinde **widget** gösterebilmek (grafik, olay geçmişi, aksiyon butonları vb.)
- **Layer'a göre filtreleme** (tren tipi/hat/alarm seviyesi/durum/zaman vb.)

### 0.2 Edit (Editor Mode)
- Ray hattının geometrisini değiştirebilmek (vertex move)
- Yeni ray ekleyebilmek, ray silebilmek
- Yeni istasyon / sabit bileşen ekleyebilmek
- Edit işlemlerini **ayrı bir backend** üzerinden yönetmek (audit/validasyon/rol yönetimi için)

### 0.3 Veri Stratejisi (OSM)
- Tüm Türkiye kapsansın
- **Boundary (il/ilçe poligonları) yok**
- Yerleşim isimleri için **label** yeterli: `place=*`
- OSM verisi başlangıçta **seed** olarak kullanılacak; sonrasında edit'ler **kendi PostGIS şeman** üzerinde ilerleyecek
- İleride ihtiyaç oldukça veri zenginleştirme kolay olacak (yeni tag'ler/katmanlar eklenebilir)

---

## 1) Mimari (High‑Level)

### 1.1 Realtime Veri Akışı (Tren Konumları + Alarmlar)

Realtime veri **MonitraNG mevcut mimarisi** üzerinden sağlanır:

```text
MngSim (sentetik veri: tren konumu + alarm + SNMP + diğer)
    |
    v
MQTT / diğer kanal
    |
    v
MngEngine (veri okuma / ingestion)
    |
    v
MngReactor (anlamlandırma + MonitraNG formatına dönüşüm)
    |     |
    |     +-- PostGIS (alarms kaydı)
    v
RabbitMQ (publish)
    |
    v
MngHub (RabbitMQ'dan dinler)
    |
    v
WebSocket --> Mng.Ui (Railway Platform)
```

- **MngSim**: Genel simülatör; tren konumu ve alarm dahil sentetik veri üretir. Tren simülasyonu (rotalar, döngü, REST konum API, sensör verileri) ayrı spec: [MNGSIM_TRAIN_SIMULATION_SPEC.md](./MNGSIM_TRAIN_SIMULATION_SPEC.md).
- **MngEngine**: MQTT vb. üzerinden veri okur, MngReactor'a iletir.
- **MngReactor**: Gelen veriyi anlamlandırır; alarmları PostGIS'e kaydeder; tren konumu ve alarm event'lerini RabbitMQ'ya publish eder.
- **MngHub**: RabbitMQ'dan dinler, UI'a WebSocket ile push eder. Mng.Ui için realtime veri dağıtımını üstlenir.

### 1.2 Bileşen Diyagramı

```text
+---------------------------+
| Mng.Ui (Nuxt + Leaflet)   |
+------------+--------------+
  WMTS | WebSocket | REST
    v      v         v
GeoServer MngHub   MngReactor, Static Editor API
    ^        ^         ^
    |        | RabbitMQ | MngSim -> MngEngine
    |        +---------+
    v
+-------+     +----------+
|PostGIS|---->| GeoServer|
+-------+     +----------+
```

> GeoServer **realtime** taşımaz. Realtime tren konumları UI'da overlay olarak çizilir.  
> GeoServer statik katmanları WMTS/WMS ile servis eder (raylar/istasyonlar/places).

---

## 2) Bileşenler ve Roller

### 2.1 PostGIS (Veri Katmanı)
**Tek doğru kaynak (source of truth)**. Statik veriler + alarm pinleri burada tutulur.

Önerilen SRID: `4326` (WGS84)

Tablolar:
- `railways` (MultiLineString)
- `stations` (Point)
- `places` (Point) — label amaçlı
- `fixed_assets` (Point/Polygon) — sinyal, makas, kontrol noktası vb. (isteğe bağlı)
- `alarms` (Point + meta)
- `train_last_positions` (Point + meta) — **MVP dışı**; ileride reconnect UX veya raporlama için eklenebilir. Realtime tren konumları MngHub WebSocket ile sağlanır.

### 2.2 GeoServer (Harita Servisleme Katmanı)
- PostGIS'teki statik katmanları yayınlar
- UI, statik layer'ları **WMTS (öncelikli)** ile çeker
- GeoWebCache ile tile cache (performans)

Servisler:
- WMTS (recommended)
- WMS (opsiyonel)
- WFS read-only (opsiyonel; debug / QGIS bağlantısı için faydalı)

> Edit işlemleri GeoServer üzerinden yapılmayacak (WFS-T yok).

### 2.3 MngReactor (Tracking/Operations)
- **MngSim → MngEngine → MngReactor** zinciri üzerinden tren konumu ve alarm alır
- Alarmları PostGIS'e kaydeder; tren + alarm event'lerini RabbitMQ'ya publish eder
- REST ile alarm workflow (create/ack/close/notes/assign) ve modal detay verilerini sağlar

### 2.4 MngHub (Realtime Dağıtım)
- RabbitMQ'dan dinler; tren konumu ve alarm event'lerini alır
- Mng.Ui'a WebSocket ile push eder
- Mevcut MonitraNG altyapısı; Railway Platform için de kullanılır

### 2.5 Static GIS Editor API (.NET Core)
- Editor Mode'dan gelen çizim/edit değişikliklerini alır (GeoJSON)
- PostGIS'e yazar
- Validasyon + audit + rol yönetimi. MVP: doğrudan yazım; draft/publish ileride eklenebilir.

### 2.6 UI (Nuxt + Leaflet)
- Viewer Mode: WMTS layer + realtime overlay + filtreler + modals/widgets
- Editor Mode: Leaflet + Geoman ile çizim/edit + attribute form + save/cancel

---

## 3) OSM Veri Profili (Türkiye) — Core + Places (No Boundaries)

### 3.1 Dahil Edilecek Tag'ler

**Ray hatları (ways):**
- `railway=rail`
- `railway=tram`
- `railway=subway`
- `railway=light_rail`

**İstasyonlar (nodes):**
- `railway=station`
- `railway=halt`

**Yerleşim label (nodes):**
- `place=city`
- `place=town`
- `place=village`
- `place=hamlet`

> Boundary relations dahil edilmeyecek.

### 3.2 Zenginleştirme (sonradan kolay)
İhtiyaç olursa şu katmanlar eklenebilir:
- `railway=switch`, `railway=signal`, `railway=level_crossing`
- `public_transport=stop_position`, `railway=yard`, `railway=depot`
- belirli `highway=*` veya `waterway=*` (bağlam için)

---

## 4) Kurulum: Docker Compose (PostGIS + GeoServer)

> GIS altyapısı **ApplicationResources/mng_others** altında tanımlıdır.  
> Ön koşul: `mng_common` ayağa kalkmış olmalı (`mng_common_mng_network` için).

### 4.1 Konum: ApplicationResources/mng_others

PostGIS, GeoServer ve pgAdmin `mng_others/docker-compose.yml` içinde tanımlıdır. MngSim ile birlikte opsiyonel servisler olarak çalışır.

**Başlatma:**
```bash
# Önce mng_common (bir kez)
cd ApplicationResources/mng_common && docker compose up -d

# Sonra mng_others (GIS + MngSim)
cd ApplicationResources/mng_others && docker compose up -d
```

**Sadece GIS servisleri:**
```bash
docker compose up -d postgis geoserver pgadmin
```

**Portlar:**
- PostGIS: `localhost:5433` (host; container içi 5432)
- GeoServer: `http://localhost:8082/geoserver`
- pgAdmin (GIS): `http://localhost:5051` (container: pgadmin_gis)

> GeoServer host portu 8082 (Keycloak 8080 kullanabilir). Container içinden erişim: `postgis:5432`, `geoserver:8080`.

---

## 5) PostGIS Şema: Tablolar ve Index'ler

> Aşağıdaki SQL'leri `gis` DB'sinde çalıştır.  
> **Hazır script:** `ApplicationResources/mng_others/init-railway-schema.sql` — çalıştırma:  
> `Get-Content ApplicationResources/mng_others/init-railway-schema.sql | docker exec -i postgis psql -U gisuser -d gis`

```sql
CREATE EXTENSION IF NOT EXISTS postgis;

-- Ray hatları
CREATE TABLE IF NOT EXISTS railways (
  id            uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  name          text,
  railway_type  text,              -- rail/tram/subway/light_rail
  operator      text,
  status        text,
  source_osm_id bigint,
  tags          jsonb,
  geom          geometry(MultiLineString, 4326) NOT NULL,
  created_at    timestamptz DEFAULT now(),
  updated_at    timestamptz DEFAULT now()
);

-- İstasyonlar
CREATE TABLE IF NOT EXISTS stations (
  id            uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  name          text,
  station_type  text,              -- station/halt
  operator      text,
  source_osm_id bigint,
  tags          jsonb,
  geom          geometry(Point, 4326) NOT NULL,
  created_at    timestamptz DEFAULT now(),
  updated_at    timestamptz DEFAULT now()
);

-- Yerleşim label'ları
CREATE TABLE IF NOT EXISTS places (
  id            uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  name          text,
  place_type    text,              -- city/town/village/hamlet
  population    bigint,
  source_osm_id bigint,
  tags          jsonb,
  geom          geometry(Point, 4326) NOT NULL,
  created_at    timestamptz DEFAULT now(),
  updated_at    timestamptz DEFAULT now()
);

-- Sabit altyapı bileşenleri (opsiyonel)
CREATE TABLE IF NOT EXISTS fixed_assets (
  id          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  name        text,
  asset_type  text,                -- signal/switch/etc.
  tags        jsonb,
  geom        geometry(Geometry, 4326) NOT NULL,
  created_at  timestamptz DEFAULT now(),
  updated_at  timestamptz DEFAULT now()
);

-- Alarmlar
CREATE TABLE IF NOT EXISTS alarms (
  id          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  alarm_type  text,
  severity    text,                -- info/warn/critical
  status      text,                -- open/ack/closed
  description text,
  source      text,                -- manual/system
  tags        jsonb,
  geom        geometry(Point, 4326) NOT NULL,
  created_at  timestamptz DEFAULT now(),
  updated_at  timestamptz DEFAULT now()
);

-- Tren son konumları (MVP dışı; ileride reconnect/raporlama için)
-- Realtime tren konumları MngHub WebSocket ile sağlanır. Bu tablo şimdilik kullanılmaz; şema ileride hazır olsun diye tanımlı.
CREATE TABLE IF NOT EXISTS train_last_positions (
  train_id    text PRIMARY KEY,
  speed       numeric,
  heading     numeric,
  tags        jsonb,
  geom        geometry(Point, 4326) NOT NULL,
  updated_at  timestamptz DEFAULT now()
);

-- Index'ler
CREATE INDEX IF NOT EXISTS idx_railways_geom ON railways USING gist (geom);
CREATE INDEX IF NOT EXISTS idx_stations_geom ON stations USING gist (geom);
CREATE INDEX IF NOT EXISTS idx_places_geom ON places USING gist (geom);
CREATE INDEX IF NOT EXISTS idx_alarms_geom ON alarms USING gist (geom);
```

> Not: `gen_random_uuid()` için `pgcrypto` gerekebilir. Eğer hata alırsan:
```sql
CREATE EXTENSION IF NOT EXISTS pgcrypto;
```

---

## 6) OSM Verisini Alma (Türkiye) — Offline Hazırlık

### 6.1 Türkiye PBF indir
Geofabrik'ten "turkey-latest.osm.pbf" indirip `./data/` altına koy.

Önerilen klasör yapısı:
```text
project-root/
  docker-compose.yml
  postgis_data/
  geoserver_data/
  data/
    turkey-latest.osm.pbf
    exports/
```

> Offline çalışma için PBF'yi bir kez indirmen yeterli.

### 6.2 OSM'den filtreli çıktı üret (osmium)

Filtreler **yapılandırma dosyası** ile yönetilir; gelecekte kolayca değiştirilebilir.

**Config dosyası:** `osm-filters.json` (veya `osm-filters.yaml`)

```json
{
  "source_pbf": "data/turkey-latest.osm.pbf",
  "output_dir": "data/exports",
  "filters": [
    { "name": "railways", "type": "w", "tag": "railway", "values": ["rail","tram","subway","light_rail"], "output": "turkey_railways.osm.pbf" },
    { "name": "stations", "type": "n", "tag": "railway", "values": ["station","halt"], "output": "turkey_stations.osm.pbf" },
    { "name": "places", "type": "n", "tag": "place", "values": ["city","town","village","hamlet"], "output": "turkey_places.osm.pbf" }
  ]
}
```

- **type**: `n` (node), `w` (way), `r` (relation)
- **tag** / **values**: osmium formatı `{type}/{tag}={value1,value2}`
- Yeni filtre eklemek veya değer değiştirmek için config'i düzenleyin.

**Script ile çalıştırma:** (içinde `data` klasörü olan dizin = `docs/content/offline_map`; config: `osm-filters.json`)
```powershell
cd docs\content\offline_map
.\scripts\run-osm-filters.ps1 -ProjectRoot (Get-Location).Path
```
Script hem PBF hem GeoJSON üretir (Faz 1.3). Detay: `docs/content/offline_map/data/README.md`

**Manuel komutlar** (config kullanmadan):
```bash
osmium tags-filter data/turkey-latest.osm.pbf w/railway=rail,tram,subway,light_rail -o data/exports/turkey_railways.osm.pbf
osmium tags-filter data/turkey-latest.osm.pbf n/railway=station,halt -o data/exports/turkey_stations.osm.pbf
osmium tags-filter data/turkey-latest.osm.pbf n/place=city,town,village,hamlet -o data/exports/turkey_places.osm.pbf
```

> Gereksinim: `osmium` (host makinede).

### 6.3 GeoJSON'a dönüştür (osmium export)

```bash
osmium export data/exports/turkey_railways.osm.pbf -f geojson -o data/exports/railways.geojson
osmium export data/exports/turkey_stations.osm.pbf -f geojson -o data/exports/stations.geojson
osmium export data/exports/turkey_places.osm.pbf   -f geojson -o data/exports/places.geojson
```

> İstersen bu export adımını container içine de alabiliriz; şimdilik host'ta en hızlıdır.

---

## 7) PostGIS'e Yükleme (ogr2ogr)

> Gereksinim: `GDAL` (ogr2ogr) host makinede.
> Alternatif: "import container" ekleyebiliriz; şimdilik en net yöntem host.

Bağlantı dizesi (host): PostGIS host portu 5433 (yerel PostgreSQL 5432 kullanıyorsa).
```text
PG:"host=localhost port=5433 dbname=gis user=gisuser password=gispass"
```

### 7.1 railways (LineString -> MultiLineString normalize)

OSM export'ları bazen LineString verebilir. MultiLineString'e normalize etmek için:
- önce staging tabloya yükle
- sonra bizim `railways` tablosuna aktar

**Staging import:**
```bash
ogr2ogr -f "PostgreSQL" \
  PG:"host=localhost port=5433 dbname=gis user=gisuser password=gispass" \
  data/exports/railways.geojson \
  -nln osm_railways_raw \
  -lco GEOMETRY_NAME=geom \
  -lco FID=osm_fid \
  -nlt PROMOTE_TO_MULTI \
  -overwrite
```

**Aktarım (mapping):**
```sql
INSERT INTO railways (name, railway_type, source_osm_id, tags, geom)
SELECT
  (properties->>'name') as name,
  (properties->>'railway') as railway_type,
  NULL::bigint as source_osm_id,   -- MVP: NULL; tags içinde OSM verisi saklanır
  properties::jsonb as tags,
  ST_SetSRID(geom, 4326)::geometry(MultiLineString, 4326) as geom
FROM osm_railways_raw;
```

> **source_osm_id (MVP kararı):** OSM export formatı (osmium + ogr2ogr) tool zincirine göre değişebilir. MVP için `source_osm_id` **NULL** bırakılır; tüm OSM verisi `tags` (jsonb) içinde saklanır. İleride OSM senkronizasyonu gerekirse, tags veya ham GeoJSON incelenerek mapping güncellenir.

### 7.2 stations (Point)

```bash
ogr2ogr -f "PostgreSQL" \
  PG:"host=localhost port=5433 dbname=gis user=gisuser password=gispass" \
  data/exports/stations.geojson \
  -nln osm_stations_raw \
  -lco GEOMETRY_NAME=geom \
  -lco FID=osm_fid \
  -overwrite
```

```sql
INSERT INTO stations (name, station_type, source_osm_id, tags, geom)
SELECT
  (properties->>'name') as name,
  (properties->>'railway') as station_type,
  NULL::bigint as source_osm_id,   -- MVP: NULL; tags içinde OSM verisi saklanır
  properties::jsonb as tags,
  ST_SetSRID(geom, 4326)::geometry(Point, 4326) as geom
FROM osm_stations_raw;
```

### 7.3 places (Point)

```bash
ogr2ogr -f "PostgreSQL" \
  PG:"host=localhost port=5433 dbname=gis user=gisuser password=gispass" \
  data/exports/places.geojson \
  -nln osm_places_raw \
  -lco GEOMETRY_NAME=geom \
  -lco FID=osm_fid \
  -overwrite
```

```sql
INSERT INTO places (name, place_type, population, source_osm_id, tags, geom)
SELECT
  (properties->>'name') as name,
  (properties->>'place') as place_type,
  NULLIF(properties->>'population','')::bigint as population,
  NULL::bigint as source_osm_id,   -- MVP: NULL; tags içinde OSM verisi saklanır
  properties::jsonb as tags,
  ST_SetSRID(geom, 4326)::geometry(Point, 4326) as geom
FROM osm_places_raw;
```

### 7.4 Temizlik (opsiyonel)
```sql
DROP TABLE IF EXISTS osm_railways_raw;
DROP TABLE IF EXISTS osm_stations_raw;
DROP TABLE IF EXISTS osm_places_raw;
```

---

## 8) GeoServer Konfigürasyonu (Publish)

### 8.1 Workspace
- GeoServer UI → **Data > Workspaces > Add new** (veya REST/script ile)
  - Name: `tr_rail`
  - Namespace URI: `http://local/tr_rail`
- **Script ile (tek seferde):** `docs/content/offline_map/scripts/configure-geoserver-tr_rail.ps1` — workspace + store + layers (railways, stations, places). Varsayılan giriş: admin/geoserver (Docker imajı).

### 8.2 Store (PostGIS)
- **Data > Stores > Add new Store > PostGIS**
  - workspace: `tr_rail`
  - host: `postgis`
  - port: `5432`
  - database: `gis`
  - user: `gisuser`
  - password: `gispass`
  - validate → save

### 8.3 Layers Publish
- **Data > Layers > Add a new resource**
  - store: `tr_rail:postgis`
  - publish:
    - `railways`
    - `stations`
    - `places`
    - (opsiyonel) `alarms`, `fixed_assets`

### 8.4 WMTS / GeoWebCache
- GeoServer'da GWC entegre gelir.
- Katmanların tile cache ayarlarını aç:
  - **Tile Caching** menüsünden layer cache aktif et
- Seed (performans için önerilir):
  - Zoom seviyeleri (başlangıç için): `6–12`
  - Sonra ihtiyaca göre artırılır

> UI tarafında mümkünse WMTS kullanın. WMS sadece debug/opsiyonel.

---

## 9) API Sözleşmeleri (Öneri Şablonları)

> Bu bölüm CursorAI için "hedef contract"tır. Uygulama geliştirirken endpoint'ler birebir böyle kullanılabilir.

### 9.1 Tracking/Operations (MngReactor) + Realtime (MngHub)

#### 9.1.1 Realtime stream (MngHub WebSocket)
- UI, MngHub WebSocket'e bağlanır
- Mesaj tipleri: `train_position`, `alarm`

Mesaj örneği (tren):
```json
{
  "type": "train_position",
  "trainId": "T123",
  "lat": 39.92,
  "lon": 32.85,
  "speed": 82,
  "heading": 135,
  "timestamp": "2026-03-03T12:45:00Z",
  "meta": {
    "lineId": "ANK-IST",
    "operator": "TCDD"
  }
}
```

Mesaj örneği (alarm):
```json
{
  "type": "alarm",
  "id": "uuid",
  "alarmType": "manual_pin",
  "severity": "warn",
  "status": "open",
  "lat": 39.90,
  "lon": 32.80,
  "description": "Saha inceleme gerekli"
}
```

#### 9.1.2 Train detail (modal widget) — MngReactor REST
```http
GET /api/trains/{trainId}
GET /api/trains/{trainId}/history?from=...&to=...
GET /api/trains/{trainId}/events?from=...&to=...
```

#### 9.1.3 Alarms — MngReactor REST
```http
POST /api/alarms
PUT  /api/alarms/{id}/ack
PUT  /api/alarms/{id}/close
POST /api/alarms/{id}/notes
GET  /api/alarms?status=open&severity=critical
```

Alarm create örneği:
```json
{
  "alarmType": "manual_pin",
  "severity": "warn",
  "description": "Saha inceleme gerekli",
  "lat": 39.90,
  "lon": 32.80,
  "tags": {
    "lineId": "ANK-IST"
  }
}
```

### 9.2 Static GIS Editor API

#### 9.2.1 CRUD (GeoJSON)
```http
POST   /api/railways
PUT    /api/railways/{id}
DELETE /api/railways/{id}

POST   /api/stations
PUT    /api/stations/{id}
DELETE /api/stations/{id}

POST   /api/fixed-assets
PUT    /api/fixed-assets/{id}
DELETE /api/fixed-assets/{id}
```

GeoJSON Feature örneği (Railway):
```json
{
  "type": "Feature",
  "geometry": {
    "type": "LineString",
    "coordinates": [
      [32.80, 39.90],
      [32.85, 39.92]
    ]
  },
  "properties": {
    "name": "Ankara Line",
    "railway_type": "rail",
    "status": "active",
    "operator": "TCDD"
  }
}
```

#### 9.2.2 Audit (önerilir)
- `railways_audit` tablosu (id, action, user_id, before, after, created_at) — kim ne zaman ne değiştirdi
- MVP'de basit audit yeterli; event sourcing ileride düşünülebilir

#### 9.2.3 Draft/Publish — MVP dışı
- **MVP kararı**: Edit değişiklikleri **doğrudan** PostGIS'e yazılır. Draft/Publish workflow uygulanmaz.
- **Gerekçe**: MVP hızı; editor zaten yetkili kullanıcı; basitlik.
- **İleride**: Onay süreci veya rollback gerektiğinde `railways_draft` tablosu + publish endpoint eklenebilir.

---

## 10) UI (Nuxt + Leaflet) — Ekranlar ve Akışlar

### 10.1 Viewer Mode
- **Harita önizleme (Faz 2’den önce):** Tek sayfa Leaflet + GeoServer WMTS: `docs/content/offline_map/railway-map-preview.html`. GeoServer (http://localhost:8082) ayakta olmalı. Dosyayı doğrudan açabilir veya `npx serve docs/content/offline_map -p 3000` ile http://localhost:3000/railway-map-preview.html açın. Katman aç/kapa + isteğe bağlı OSM arka plan.
- Base layers:
  - GeoServer WMTS: `railways`, `stations`, `places`
- Overlays:
  - Realtime trains (MngHub WebSocket)
  - alarms (MngHub WebSocket + MngReactor REST)
- Sol panel:
  - layer toggle
  - layer bazlı filtreler
  - quick search (trainId / station name / place name)
- Click davranışı:
  - Train marker → modal → widget'lar (grafik, olaylar, aksiyon)
  - Alarm marker → modal → ack/close/note/assign

### 10.2 Editor Mode (Leaflet + Geoman)
- Layer seçimi: railways / stations / fixed_assets
- Araçlar:
  - draw polyline (rail)
  - edit polyline (vertex move)
  - draw point (station)
  - snap, undo/redo
- Sağ panel:
  - attribute form (name, operator, status...)
- Save:
  - Static GIS Editor API'ye GeoJSON gönder → doğrudan PostGIS'e yazılır (MVP: draft yok)

> Edit UI'da "save" edilmeden PostGIS'e yazılmamalı.

---

## 11) Güvenlik (MngKeeper + Keycloak)

Railway Platform, **MonitraNG mevcut auth altyapısını** kullanır. Ek auth servisi geliştirilmez.

### 11.1 Auth Akışı
- **MngKeeper** (Keycloak entegrasyonu): `POST /keeper/api/auth/token` ile JWT alınır
- **MngGateway**: MngReactor (`/reactor/api/*`) ve MngHub (`/hub/ws/*`) Bearer token gerektirir
- **Railway Platform UI**: Mng.Ui ile aynı login; aldığı JWT ile tüm API ve WebSocket çağrıları yapılır

### 11.2 Development
- GeoServer admin: basit şifre (harita tile'ları için)
- MngKeeper dev ortamı: Keycloak + domain ile token alınır
- GeoServer WMTS: Internal network'te public veya basit auth; JWT gerekmez

### 11.3 Production
- Reverse proxy (nginx/traefik) — mevcut MonitraNG altyapısı
- MngKeeper + Keycloak — zaten kullanılıyor
- Role based (Keycloak `user_groups` veya mevcut `admins`/`managers`):
  - **viewer**: Harita görüntüleme, tren/alarm izleme
  - **operator**: Alarm yönetimi (ack/close/note)
  - **editor**: Statik GIS edit (ray/istasyon)
  - **admin**: Tam yetki

> **Not:** Railway'e özel gruplar (`railway_viewer`, `railway_operator` vb.) Keycloak realm'de tanımlanabilir; MngReactor ve Static Editor API'de `user_groups` claim'ine göre authorization yapılır. Alternatif olarak mevcut `admins`/`managers` grupları kullanılabilir.

---

## 12) Zenginleştirme Planı (Kolay Yol)

Yeni bir katman eklemek için:
1) PBF'den yeni tag'lerle filtre üret
2) GeoJSON export
3) PostGIS'e import
4) Yeni tablo/layer publish
5) UI'ya layer toggle + filtre ekle

Örnek: `railway=switch` ekleme — **osm-filters.json**'a yeni filtre ekleyin:
```json
{ "name": "switches", "type": "n", "tag": "railway", "values": ["switch"], "output": "turkey_switches.osm.pbf" }
```
Sonra script'i çalıştırın veya manuel:
```bash
osmium tags-filter data/turkey-latest.osm.pbf n/railway=switch -o data/exports/turkey_switches.osm.pbf
osmium export data/exports/turkey_switches.osm.pbf -f geojson -o data/exports/switches.geojson
```
Sonra `fixed_assets` içine import edip publish edersin.

---

## 13) Hızlı Kontrol Listesi

- [ ] Docker: PostGIS + GeoServer çalışıyor
- [ ] PostGIS tabloları ve index'ler oluşturuldu
- [ ] Türkiye PBF indirildi (offline hazır)
- [ ] OSM filtre + export (railways/stations/places)
- [ ] PostGIS import tamam
- [ ] GeoServer workspace/store/layers publish
- [ ] WMTS cache aktif + seed (opsiyonel)
- [ ] MngSim + MngEngine + MngReactor + MngHub: tren ve alarm verisi akıyor
- [ ] UI Viewer Mode: layer'lar görünüyor
- [ ] UI Viewer Mode: trains stream overlay çalışıyor
- [ ] UI: alarm create + modal widget çalışıyor
- [ ] UI Editor Mode: rail/station edit → Static Editor API → PostGIS yazıyor

---

## 14) Notlar / Bilinen Riskler

- **Realtime tren verisi**: MngSim → MngEngine → MngReactor zinciri kullanılır. MngReactor RabbitMQ'ya publish eder; MngHub WebSocket ile UI'a iletir.
- **Alarm akışı**: MngSim alarm üretir → MngEngine yakalar → MngReactor PostGIS'e kaydeder + RabbitMQ'ya publish eder → MngHub WebSocket ile UI'a push eder.
- **train_last_positions**: MVP için kullanılmaz. Reconnect UX veya raporlama ihtiyacı doğarsa MngReactor bu tabloya yazacak şekilde genişletilebilir.
- **source_osm_id**: MVP için NULL. Tüm OSM verisi `tags` (jsonb) içinde saklanır. İleride OSM senkronizasyonu gerekirse mapping güncellenir.
- **Draft/Publish**: MVP için uygulanmaz; edit değişiklikleri doğrudan PostGIS'e yazılır. İleride onay süreci gerekirse eklenebilir.
- **OSM filtreleri**: `osm-filters.json` ile yönetilir; yeni katman veya tag eklemek için config düzenlenir.
- Türkiye genelinde railways/stations/places dataset'i Docker Desktop'ta yönetilebilir boyuttadır.
- İleride performans için:
  - WMTS seed zoom aralığı optimize edilmeli
  - UI'da clustering/virtualization uygulanmalı

---

## 15) Ek: Önerilen Repo Yapısı

```text
rail-ops-platform/
  docs/
    railway-platform.md
  infra/
    docker-compose.yml
    postgis_data/
    geoserver_data/
    osm-filters.json      # OSM filtre yapılandırması (kolay düzenlenebilir)
    scripts/
      run-osm-filters.ps1
    data/
      turkey-latest.osm.pbf
      exports/
  services/
    tracking-api/
    static-editor-api/
  ui/
    nuxt-app/
```

---

**Bitti.**  

**Implementasyon roadmap:** [RAILWAY_PLATFORM_ROADMAP.md](./RAILWAY_PLATFORM_ROADMAP.md) — her başarılı adım sonrası güncellenir.  
*Son güncelleme: Faz 1 tamamlandı (WMTS GetTile doğrulandı). Sıradaki adım: Faz 2 Realtime pipeline (tren konumu).*

İstersen bir sonraki adımda şunları da ekleyebilirim:
- MngReactor için RabbitMQ publish örnek .NET implementation iskeleti
- Static Editor API için minimal CRUD + PostGIS yazma örnekleri
- Nuxt + Leaflet + Geoman başlangıç kodu (Viewer + Editor ayrı sayfalar)
