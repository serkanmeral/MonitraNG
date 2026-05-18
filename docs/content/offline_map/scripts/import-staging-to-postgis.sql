-- Faz 1.4: Staging tablolardan railways, stations, places'a aktarım (ogr2ogr sütunları düz; tags = tüm öznitelikler jsonb)
INSERT INTO railways (name, railway_type, source_osm_id, tags, geom)
SELECT
  name,
  railway as railway_type,
  NULL::bigint as source_osm_id,
  (to_jsonb(r) - 'geom' - 'osm_fid')::jsonb as tags,
  ST_SetSRID(ST_CollectionExtract(ST_MakeValid(geom), 2), 4326)::geometry(MultiLineString, 4326) as geom
FROM osm_railways_raw r
WHERE geom IS NOT NULL;

INSERT INTO stations (name, station_type, source_osm_id, tags, geom)
SELECT
  name,
  railway as station_type,
  NULL::bigint as source_osm_id,
  (to_jsonb(s) - 'geom' - 'osm_fid')::jsonb as tags,
  ST_SetSRID(geom, 4326)::geometry(Point, 4326) as geom
FROM osm_stations_raw s
WHERE geom IS NOT NULL;

INSERT INTO places (name, place_type, population, source_osm_id, tags, geom)
SELECT
  name,
  place as place_type,
  NULLIF(trim(population::text), '')::bigint as population,
  NULL::bigint as source_osm_id,
  (to_jsonb(p) - 'geom' - 'osm_fid')::jsonb as tags,
  ST_SetSRID(geom, 4326)::geometry(Point, 4326) as geom
FROM osm_places_raw p
WHERE geom IS NOT NULL;

DROP TABLE IF EXISTS osm_railways_raw;
DROP TABLE IF EXISTS osm_stations_raw;
DROP TABLE IF EXISTS osm_places_raw;
