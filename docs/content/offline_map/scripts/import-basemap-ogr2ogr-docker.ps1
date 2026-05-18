# Adim 6.3: GeoJSON'lari PostGIS'e yukler (ogr2ogr yerine Docker ile - GDAL kurulu olmasa da calisir).
# Kullanim: .\import-basemap-ogr2ogr-docker.ps1
# On kosul: data/exports/ altinda roads.geojson, waterways.geojson, water_areas.geojson, landuse.geojson olmali.
# PostGIS host'ta 5433 portunda erişilebilir olmali (Docker: host.docker.internal:5433).

$ErrorActionPreference = "Stop"
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$offlineMapRoot = (Resolve-Path (Join-Path $scriptDir "..")).Path
$exportsPath = Join-Path $offlineMapRoot "data\exports"

# Volume: exports klasorunu dogrudan /exports olarak bagla; dosya yolu kesin /exports/x.geojson olur
$pgHost = "host.docker.internal"
$pgPort = "5433"
$pgDb = "gis"
$pgUser = "gisuser"
$pgPass = "gispass"
$pgConn = "PG:host=$pgHost port=$pgPort dbname=$pgDb user=$pgUser password=$pgPass"

# GDAL imaji ghcr.io'da (Docker Hub osgeo/gdal:latest artik yok)
$image = "ghcr.io/osgeo/gdal:ubuntu-small-latest"
foreach ($name in @("roads","waterways","water_areas","landuse")) {
    $geojson = Join-Path $exportsPath "$name.geojson"
    if (-not (Test-Path $geojson)) {
        Write-Warning "Atlanıyor (dosya yok): $geojson"
        continue
    }
    Write-Host "Yukleniyor: $name..."
    $out = docker run --rm -v "${exportsPath}:/exports:ro" $image ogr2ogr -f PostgreSQL "$pgConn" "/exports/$name.geojson" -nln "osm_${name}_raw" -lco GEOMETRY_NAME=geom -lco FID=osm_fid -nlt PROMOTE_TO_MULTI -overwrite 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Host "ogr2ogr cikti: $out"
        throw "ogr2ogr hatasi: $name. Yukaridaki ciktiya bakin (baglanti, dosya yolu veya sürücü)."
    }
}
Write-Host "Tamamlandi. Sonraki adim: import-basemap-to-postgis.sql calistirin (Adim 6.4)."
