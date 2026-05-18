# Rota geometrileri (MngSim tren simülasyonu)

Bu klasör, tren simülasyonu için kullanılan **rota polyline** dosyalarını içerir. Her dosya bir rota için A→B yönünde koordinat dizisi ve toplam uzunluk (metre) sağlar.

## Dosya formatı

- `ANK-IST.json`, `ANK-KON.json`: Her biri `{ "coordinates": [[lon, lat], ...], "length_m": number }` (WGS84).

## Üretim

Geometriler PostGIS `railways` tablosundan export script ile üretilir. **PostGIS konteynerinin çalışıyor olması gerekir** (örn. `mng_common` docker-compose ile).

### Docker ile çalıştırma (önerilen)

```powershell
cd docs/content/offline_map
docker run --rm -v "${PWD}:/data" -e PGHOST=host.docker.internal -e PGPORT=5433 -e PGDATABASE=gis -e PGUSER=gisuser -e PGPASSWORD=gispass -w /data/scripts python:3.11-slim bash -c "pip install -q psycopg2-binary && python export_route_geometries.py"
```

Çıktılar `route-geometries/ANK-IST.json` ve `ANK-KON.json` olarak yazılır.

### Yerel Python ile

```bash
cd docs/content/offline_map/scripts
pip install -r requirements-export.txt
# Windows: set PGPASSWORD=gispass
python export_route_geometries.py
```

Ortam değişkenleri: `PGHOST` (varsayılan localhost), `PGPORT` (5433), `PGDATABASE` (gis), `PGUSER` (gisuser), `PGPASSWORD`.

Bkz. [MNGSIM_TRAIN_SIMULATION_SPEC.md](../MNGSIM_TRAIN_SIMULATION_SPEC.md) §4.2.
