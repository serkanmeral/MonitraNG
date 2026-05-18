# Railway Platform — Implementasyon Roadmap

> Bu doküman her başarılı adım sonrası güncellenir.  
> Referans: [railway-platform.md](./railway-platform.md)

---

## Genel Bakış

| Faz | Açıklama | Durum |
|-----|----------|-------|
| 0 | Hazırlık | ⬜ Bekliyor |
| 1 | Harita altyapısı (PostGIS + GeoServer + OSM) | ✅ Tamamlandı |
| 2 | Realtime pipeline (tren konumu) | ⬜ Bekliyor |
| 3 | Alarm pipeline | ⬜ Bekliyor |
| 4 | Static GIS Editor API | ⬜ Bekliyor |
| 5 | UI Viewer Mode | ⬜ Bekliyor |
| 6 | UI Editor Mode | ⬜ Bekliyor |

**Durum göstergeleri:** ⬜ Bekliyor | 🔄 Devam ediyor | ✅ Tamamlandı

---

## Faz 0: Hazırlık

| # | Adım | Başarı kriteri | Durum |
|---|------|----------------|-------|
| 0.1 | Gereksinimler kurulumu | osmium, GDAL (ogr2ogr) host'ta çalışıyor | ⬜ |
| 0.2 | Türkiye PBF indir | Geofabrik'ten turkey-latest.osm.pbf, `data/` altında | ⬜ |
| 0.3 | Repo/klasör yapısı | `rail-ops-platform` veya MonitraNG altında gerekli klasörler oluşturuldu | ⬜ |

---

## Faz 1: Harita Altyapısı

**Referans:** railway-platform.md Bölüm 4, 5, 6, 7, 8

| # | Adım | Başarı kriteri | Durum |
|---|------|----------------|-------|
| 1.1 | Docker Compose ayağa kaldır | `mng_others` (PostGIS + GeoServer + pgAdmin) çalışıyor | ✅ |
| 1.2 | PostGIS şema oluştur | Tablolar (railways, stations, places, fixed_assets, alarms) ve index'ler | ✅ |
| 1.3 | OSM filtre + export | osm-filters.json ile railways, stations, places PBF + GeoJSON üretildi | ✅ |
| 1.4 | PostGIS import | ogr2ogr ile railways, stations, places yüklendi | ✅ |
| 1.5 | GeoServer konfigürasyonu | Workspace (tr_rail), Store, Layers publish | ✅ |
| 1.6 | WMTS doğrulama | GeoServer WMTS endpoint'inden tile alınabiliyor | ✅ |

**Faz 1.3 nasıl yapılır:** Geofabrik'ten `turkey-latest.osm.pbf` indir → `docs/content/offline_map/data/` içine koy → script çalıştır. **Osmium kurmadan:** `.\scripts\run-osm-filters.ps1 -UseDocker -ProjectRoot (Get-Location).Path` (Docker ile; detay: `data/README.md`).

---

## Faz 2: Realtime Pipeline (Tren Konumu)

**Referans:** railway-platform.md Bölüm 1.1, 2.3, 2.4, 9.1

| # | Adım | Başarı kriteri | Durum |
|---|------|----------------|-------|
| 2.1 | MngSim: tren konumu üretimi | MngSim sentetik tren konumu verisi üretiyor (MQTT/HTTP vb.) | ⬜ |
| 2.2 | MngEngine: tren verisi ingestion | MngSim'den gelen veriyi okuyup MngReactor'a iletiyor | ⬜ |
| 2.3 | MngReactor: tren adaptörü | Gelen veriyi MonitraNG formatına dönüştürüp RabbitMQ'ya publish ediyor | ⬜ |
| 2.4 | MngHub: tren event'i | RabbitMQ'dan train_position alıp WebSocket ile UI'a iletiyor | ⬜ |
| 2.5 | End-to-end test | MngSim → MngEngine → MngReactor → MngHub → WebSocket bağlantısı çalışıyor | ⬜ |

---

## Faz 3: Alarm Pipeline

**Referans:** railway-platform.md Bölüm 1.1, 2.3, 9.1.3

| # | Adım | Başarı kriteri | Durum |
|---|------|----------------|-------|
| 3.1 | MngSim: alarm üretimi | MngSim alarm event'i üretiyor | ⬜ |
| 3.2 | MngEngine: alarm ingestion | Alarm verisini MngReactor'a iletiyor | ⬜ |
| 3.3 | MngReactor: alarm işleme | PostGIS alarms tablosuna yazıyor + RabbitMQ'ya publish ediyor | ⬜ |
| 3.4 | MngHub: alarm event'i | RabbitMQ'dan alarm alıp WebSocket ile UI'a iletiyor | ⬜ |
| 3.5 | MngReactor: alarm REST API | POST/GET/PUT alarm endpoint'leri çalışıyor | ⬜ |

---

## Faz 4: Static GIS Editor API

**Referans:** railway-platform.md Bölüm 2.5, 9.2

| # | Adım | Başarı kriteri | Durum |
|---|------|----------------|-------|
| 4.1 | API projesi oluştur | .NET Core API; PostGIS bağlantısı | ⬜ |
| 4.2 | Railways CRUD | POST/PUT/DELETE /api/railways; GeoJSON → PostGIS | ⬜ |
| 4.3 | Stations CRUD | POST/PUT/DELETE /api/stations | ⬜ |
| 4.4 | Fixed-assets CRUD (opsiyonel) | POST/PUT/DELETE /api/fixed-assets | ⬜ |
| 4.5 | Audit log (opsiyonel) | railways_audit tablosu + yazım | ⬜ |
| 4.6 | MngGateway route | Static Editor API Gateway'e eklenmiş | ⬜ |

---

## Faz 5: UI Viewer Mode

**Referans:** railway-platform.md Bölüm 10.1

| # | Adım | Başarı kriteri | Durum |
|---|------|----------------|-------|
| 5.1 | Railway Platform sayfası | Mng.Ui içinde veya ayrı sayfa; Nuxt + Leaflet | ⬜ |
| 5.2 | WMTS base layer | GeoServer'dan railways, stations, places tile'ları görünüyor | ⬜ |
| 5.3 | MngHub WebSocket bağlantısı | JWT ile WebSocket bağlanıyor | ⬜ |
| 5.4 | Tren overlay | train_position event'leri haritada marker olarak görünüyor | ⬜ |
| 5.5 | Alarm overlay | alarm event'leri haritada pin olarak görünüyor | ⬜ |
| 5.6 | Layer toggle + filtreler | Sol panel; layer aç/kapa; basit filtreler | ⬜ |
| 5.7 | Train/Alarm modal | Tıklayınca modal; detay widget (MngReactor REST) | ⬜ |

---

## Faz 6: UI Editor Mode

**Referans:** railway-platform.md Bölüm 10.2

| # | Adım | Başarı kriteri | Durum |
|---|------|----------------|-------|
| 6.1 | Geoman entegrasyonu | Leaflet + Geoman; draw polyline, edit, draw point | ⬜ |
| 6.2 | Railway çizim | Yeni ray çizimi; attribute form; Static Editor API'ye save | ⬜ |
| 6.3 | Railway düzenleme | Vertex move; save → PostGIS | ⬜ |
| 6.4 | Station ekleme | Point çizimi; attribute form; save | ⬜ |
| 6.5 | Rol kontrolü | Editor yetkisi olan kullanıcılar edit yapabiliyor | ⬜ |

---

## Güncelleme Geçmişi

| Tarih | Faz/Adım | Not |
|-------|----------|-----|
| 2025-03-04 | 1.6 | WMTS GetTile doğrulandı (tr_rail:railways tile 200 OK). Faz 1 tamamlandı. |
| 2025-03-04 | 1.5 | GeoServer: workspace tr_rail, PostGIS store, layers railways/stations/places publish; WMS/WMTS GetCapabilities 200. Script: configure-geoserver-tr_rail.ps1. |
| 2025-03-04 | 1.4 | PostGIS import: Docker GDAL (ogr2ogr) ile staging; import-staging-to-postgis.sql ile railways (36963), stations (1324), places (47128) yüklendi. |
| 2025-03-03 | 1.3 hazırlık | OSM filtre script'i güncellendi: PBF + GeoJSON tek script (run-osm-filters.ps1); data/ ve data/exports yapısı + README; ogr2ogr bağlantı dizesi port 5433. |
| 2025-03-03 | 1.2 | PostGIS şema uygulandı: init-railway-schema.sql (railways, stations, places, fixed_assets, alarms, train_last_positions + GIST index'ler). |
| 2025-03-03 | 1.1 | mng_others (PostGIS + GeoServer) doğrulandı; container'lar ayakta, PostGIS 3.4, GeoServer erişilebilir. |
| — | — | Roadmap oluşturuldu |

---

## Hızlı Referans: Kontrol Listesi

Tamamlandıkça işaretle:

- [x] mng_others (PostGIS + GeoServer) çalışıyor
- [x] OSM verisi import edildi
- [x] GeoServer WMTS layer'ları yayında
- [ ] MngSim tren konumu üretiyor
- [ ] MngReactor RabbitMQ'ya publish ediyor
- [ ] MngHub WebSocket tren + alarm iletiyor
- [ ] UI Viewer: harita + tren + alarm görünüyor
- [ ] Static Editor API CRUD çalışıyor
- [ ] UI Editor: ray/istasyon edit → PostGIS
