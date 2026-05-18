-- Adım 6: OSM benzeri arka plan için tablolar (yol, su, arazi)
-- Çalıştırma: docker exec -i postgis psql -U gisuser -d gis -f - < init-basemap-tables.sql
-- veya (PowerShell): Get-Content docs\content\offline_map\scripts\init-basemap-tables.sql | docker exec -i postgis psql -U gisuser -d gis

CREATE TABLE IF NOT EXISTS roads (
  id            uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  name          text,
  highway_type  text,
  source_osm_id bigint,
  tags          jsonb,
  geom          geometry(MultiLineString, 4326) NOT NULL,
  created_at    timestamptz DEFAULT now()
);

CREATE TABLE IF NOT EXISTS waterways (
  id            uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  name          text,
  waterway_type text,
  source_osm_id bigint,
  tags          jsonb,
  geom          geometry(MultiLineString, 4326) NOT NULL,
  created_at    timestamptz DEFAULT now()
);

CREATE TABLE IF NOT EXISTS water_areas (
  id            uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  name          text,
  natural_type  text,
  source_osm_id bigint,
  tags          jsonb,
  geom          geometry(MultiPolygon, 4326) NOT NULL,
  created_at    timestamptz DEFAULT now()
);

CREATE TABLE IF NOT EXISTS landuse (
  id            uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  name          text,
  landuse_type  text,
  source_osm_id bigint,
  tags          jsonb,
  geom          geometry(MultiPolygon, 4326) NOT NULL,
  created_at    timestamptz DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_roads_geom ON roads USING gist (geom);
CREATE INDEX IF NOT EXISTS idx_waterways_geom ON waterways USING gist (geom);
CREATE INDEX IF NOT EXISTS idx_water_areas_geom ON water_areas USING gist (geom);
CREATE INDEX IF NOT EXISTS idx_landuse_geom ON landuse USING gist (geom);
