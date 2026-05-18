-- Adım 6: Staging tablolardan roads, waterways, water_areas, landuse aktarımı
-- Önce ogr2ogr ile osm_*_raw tabloları doldurulmuş olmalı (bkz. ADIM_6_TAM_REHBER.md)

INSERT INTO roads (name, highway_type, source_osm_id, tags, geom)
SELECT
  r.name,
  r.highway AS highway_type,
  NULL::bigint,
  (to_jsonb(r) - 'geom' - 'osm_fid')::jsonb,
  ST_SetSRID(ST_CollectionExtract(ST_MakeValid(r.geom), 2), 4326)::geometry(MultiLineString, 4326)
FROM osm_roads_raw r
WHERE r.geom IS NOT NULL;

INSERT INTO waterways (name, waterway_type, source_osm_id, tags, geom)
SELECT
  w.name,
  w.waterway AS waterway_type,
  NULL::bigint,
  (to_jsonb(w) - 'geom' - 'osm_fid')::jsonb,
  ST_SetSRID(ST_CollectionExtract(ST_MakeValid(w.geom), 2), 4326)::geometry(MultiLineString, 4326)
FROM osm_waterways_raw w
WHERE w.geom IS NOT NULL;

INSERT INTO water_areas (name, natural_type, source_osm_id, tags, geom)
SELECT
  a.name,
  a.natural AS natural_type,
  NULL::bigint,
  (to_jsonb(a) - 'geom' - 'osm_fid')::jsonb,
  ST_SetSRID(ST_CollectionExtract(ST_MakeValid(a.geom), 3), 4326)::geometry(MultiPolygon, 4326)
FROM osm_water_areas_raw a
WHERE a.geom IS NOT NULL;

INSERT INTO landuse (name, landuse_type, source_osm_id, tags, geom)
SELECT
  l.name,
  l.landuse AS landuse_type,
  NULL::bigint,
  (to_jsonb(l) - 'geom' - 'osm_fid')::jsonb,
  ST_SetSRID(ST_CollectionExtract(ST_MakeValid(l.geom), 3), 4326)::geometry(MultiPolygon, 4326)
FROM osm_landuse_raw l
WHERE l.geom IS NOT NULL;

DROP TABLE IF EXISTS osm_roads_raw;
DROP TABLE IF EXISTS osm_waterways_raw;
DROP TABLE IF EXISTS osm_water_areas_raw;
DROP TABLE IF EXISTS osm_landuse_raw;
