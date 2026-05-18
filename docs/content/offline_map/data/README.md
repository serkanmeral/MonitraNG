# OSM verisi (Railway Platform)

Bu klasör Türkiye OSM verisini ve filtreli çıktıları tutar.

## Kurulum (Faz 1.3)

1. **turkey-latest.osm.pbf** indirin (Geofabrik):
   - https://download.geofabrik.de/europe/turkey.html
   - İndirilen dosyayı bu klasöre koyun: `turkey-latest.osm.pbf`

2. **exports** alt klasörü script tarafından otomatik oluşturulur; PBF ve GeoJSON çıktıları buraya yazılır.

## Script çalıştırma

`ProjectRoot`, **içinde `data` klasörü olan dizin** olmalı (yani `docs\content\offline_map`). Örnek:

**Seçenek A — offline_map dizininden:**
```powershell
cd c:\Serkan\iSIM\MonitraNG\docs\content\offline_map
.\scripts\run-osm-filters.ps1 -ProjectRoot (Get-Location).Path
```

**Seçenek B — repo kökünden:**
```powershell
cd c:\Serkan\iSIM\MonitraNG
$offlineMap = Join-Path (Get-Location).Path "docs\content\offline_map"
.\docs\content\offline_map\scripts\run-osm-filters.ps1 -ProjectRoot $offlineMap
```

**Gereksinim:** `osmium` host'ta **veya** Docker. Osmium kurmak istemezseniz script'i **Docker** ile çalıştırın (host'ta osmium gerekmez):

```powershell
cd c:\Serkan\iSIM\MonitraNG\docs\content\offline_map
.\scripts\run-osm-filters.ps1 -UseDocker -ProjectRoot (Get-Location).Path
```

Yerel osmium kullanacaksanız: `osmium` path'te olmalı (örn. Windows: `choco install osmium-tool`).

## Klasör yapısı (script sonrası)

```
data/
  turkey-latest.osm.pbf   ← siz indirirsiniz
  exports/
    turkey_railways.osm.pbf   (rail, tram, subway, light_rail, narrow_gauge, preserved)
    turkey_stations.osm.pbf   (station, halt, subway_station)
    turkey_places.osm.pbf
    railways.geojson
    stations.geojson
    places.geojson
```

Filtreleri genişletmek veya veriyi zenginleştirmek için: `GEOSERVER_VERI_ZENGINLESTIRME.md`.

Referans: [railway-platform.md](../railway-platform.md) Bölüm 6.
