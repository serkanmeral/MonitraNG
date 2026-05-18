-- Railway Platform PostGIS şeması (railway-platform.md Bölüm 5)
-- Çalıştırma: docker exec -i postgis psql -U gisuser -d gis -f - < init-railway-schema.sql
-- veya: Get-Content init-railway-schema.sql | docker exec -i postgis psql -U gisuser -d gis

CREATE EXTENSION IF NOT EXISTS postgis;
CREATE EXTENSION IF NOT EXISTS pgcrypto;

-- Ray hatları
CREATE TABLE IF NOT EXISTS railways (
  id            uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  name          text,
  railway_type  text,
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
  station_type  text,
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
  place_type    text,
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
  asset_type  text,
  tags        jsonb,
  geom        geometry(Geometry, 4326) NOT NULL,
  created_at  timestamptz DEFAULT now(),
  updated_at  timestamptz DEFAULT now()
);

-- Alarmlar
CREATE TABLE IF NOT EXISTS alarms (
  id          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  alarm_type  text,
  severity    text,
  status      text,
  description text,
  source      text,
  tags        jsonb,
  geom        geometry(Point, 4326) NOT NULL,
  created_at  timestamptz DEFAULT now(),
  updated_at  timestamptz DEFAULT now()
);

-- Tren son konumları (MVP dışı; ileride reconnect/raporlama için)
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
CREATE INDEX IF NOT EXISTS idx_fixed_assets_geom ON fixed_assets USING gist (geom);
