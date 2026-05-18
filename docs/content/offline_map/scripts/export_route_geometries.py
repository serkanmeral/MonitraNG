#!/usr/bin/env python3
"""
PostGIS railways tablosundan rota polyline'larını export eder.
routes-reference.json'daki ANK-IST ve ANK-KON için A->B koridorunda
rail segmentlerini alıp açgözlü sıralama ile tek polyline üretir.
Çıktı: route-geometries/ROUTE_ID.json  { "coordinates": [[lon,lat],...], "length_m": ... }
"""
from __future__ import annotations

import json
import math
import os
import sys
from pathlib import Path

try:
    import psycopg2
    from psycopg2.extras import RealDictCursor
except ImportError:
    print("psycopg2 gerekli: pip install -r requirements-export.txt", file=sys.stderr)
    sys.exit(1)

# Script docs/content/offline_map/scripts/ içinde; route-geometries ve routes-reference bir üst + route-geometries / aynı seviye
SCRIPT_DIR = Path(__file__).resolve().parent
OFFLINE_MAP = SCRIPT_DIR.parent
ROUTE_GEOMETRIES_DIR = OFFLINE_MAP / "route-geometries"
ROUTES_REFERENCE_PATH = OFFLINE_MAP / "routes-reference.json"

# Export edilecek rotalar (sadece A->B; IST-ANK ve KON-ANK aynı geometri ters yön)
ROUTES_TO_EXPORT = [
    ("ANK-IST", "ankara-yht", "halkali"),
    ("ANK-KON", "ankara-yht", "konya"),
]

# Koridor buffer (metre) — iki istasyon arası çizginin etrafında bu kadar
CORRIDOR_BUFFER_M = 25_000
# İki segmentin "birleşik" sayılması için uç nokta mesafesi eşiği (metre)
CONNECT_THRESHOLD_M = 800


def haversine_m(lon1: float, lat1: float, lon2: float, lat2: float) -> float:
    R = 6_371_000  # metre
    phi1, phi2 = math.radians(lat1), math.radians(lat2)
    dphi = math.radians(lat2 - lat1)
    dlam = math.radians(lon2 - lon1)
    a = math.sin(dphi / 2) ** 2 + math.cos(phi1) * math.cos(phi2) * math.sin(dlam / 2) ** 2
    c = 2 * math.atan2(math.sqrt(a), math.sqrt(1 - a))
    return R * c


def coords_to_length_m(coords: list[list[float]]) -> float:
    total = 0.0
    for i in range(1, len(coords)):
        total += haversine_m(
            coords[i - 1][0], coords[i - 1][1],
            coords[i][0], coords[i][1],
        )
    return total


def load_stations() -> dict:
    with open(ROUTES_REFERENCE_PATH, encoding="utf-8") as f:
        data = json.load(f)
    return {s["id"]: (s["lon"], s["lat"]) for s in data["stations"]}


def get_connection_params():
    return {
        "host": os.environ.get("PGHOST", "localhost"),
        "port": int(os.environ.get("PGPORT", "5433")),
        "dbname": os.environ.get("PGDATABASE", "gis"),
        "user": os.environ.get("PGUSER", "gisuser"),
        "password": os.environ.get("PGPASSWORD", "gispass"),
    }


def fetch_segments_geojson(conn, lon_a: float, lat_a: float, lon_b: float, lat_b: float) -> list[list[list[float]]]:
    """Koridor (A-B arası buffer) içindeki rail segmentlerini LineString parçaları olarak döner."""
    sql = """
    WITH corridor AS (
      SELECT ST_Buffer(
        ST_MakeLine(
          ST_SetSRID(ST_Point(%s, %s), 4326),
          ST_SetSRID(ST_Point(%s, %s), 4326)
        )::geography,
        %s
      )::geometry AS g
    ),
    dumped AS (
      SELECT (ST_Dump(r.geom)).geom AS geom
      FROM railways r, corridor c
      WHERE r.railway_type = 'rail'
        AND ST_Intersects(r.geom, c.g)
    )
    SELECT ST_AsGeoJSON(geom) AS geojson
    FROM dumped
    """
    with conn.cursor(cursor_factory=RealDictCursor) as cur:
        cur.execute(sql, (lon_a, lat_a, lon_b, lat_b, CORRIDOR_BUFFER_M))
        rows = cur.fetchall()
    segments = []
    for row in rows:
        gj = json.loads(row["geojson"])
        if gj.get("type") == "LineString" and gj.get("coordinates"):
            segments.append(gj["coordinates"])
        elif gj.get("type") == "MultiLineString":
            for part in gj.get("coordinates", []):
                if part:
                    segments.append(part)
    return segments


def build_path_greedy(
    segments: list[list[list[float]]],
    lon_a: float, lat_a: float,
    lon_b: float, lat_b: float,
) -> list[list[float]]:
    """A'dan başlayıp B'ye ulaşana kadar segmentleri uç uca ekler. Başarısızsa mümkün olan en uzun path."""
    threshold_deg = CONNECT_THRESHOLD_M / 111_000  # kabaca 1 deg ~ 111km
    path = [[lon_a, lat_a]]
    used = [False] * len(segments)

    def dist_deg(pa: list[float], pb: list[float]) -> float:
        return math.hypot(pa[0] - pb[0], pa[1] - pb[1])

    def dist_m(pa: list[float], pb: list[float]) -> float:
        return haversine_m(pa[0], pa[1], pb[0], pb[1])

    head = path[-1]
    while dist_m(head, [lon_b, lat_b]) > CONNECT_THRESHOLD_M:
        best_idx = -1
        best_seg = None
        best_reverse = False
        for i, seg in enumerate(segments):
            if used[i] or len(seg) < 2:
                continue
            first = seg[0]
            last = seg[-1]
            if dist_deg(head, first) <= threshold_deg:
                best_idx = i
                best_seg = seg
                best_reverse = True
                break
            if dist_deg(head, last) <= threshold_deg:
                best_idx = i
                best_seg = seg
                best_reverse = False
                break
        if best_idx < 0:
            break
        used[best_idx] = True
        if best_reverse:
            path.extend(reversed(best_seg))
        else:
            path.extend(best_seg[1:])
        head = path[-1]

    path.append([lon_b, lat_b])
    return path


def main():
    ROUTE_GEOMETRIES_DIR.mkdir(parents=True, exist_ok=True)
    stations = load_stations()

    conn = psycopg2.connect(**get_connection_params())

    for route_id, from_id, to_id in ROUTES_TO_EXPORT:
        lon_a, lat_a = stations[from_id]
        lon_b, lat_b = stations[to_id]
        segments = fetch_segments_geojson(conn, lon_a, lat_a, lon_b, lat_b)
        if not segments:
            print(f"{route_id}: koridorda segment bulunamadı, atlanıyor.", file=sys.stderr)
            continue
        path = build_path_greedy(segments, lon_a, lat_a, lon_b, lat_b)
        length_m = coords_to_length_m(path)
        out = {
            "coordinates": path,
            "length_m": round(length_m, 2),
        }
        out_path = ROUTE_GEOMETRIES_DIR / f"{route_id}.json"
        with open(out_path, "w", encoding="utf-8") as f:
            json.dump(out, f, ensure_ascii=False, separators=(",", ":"))
        print(f"{route_id}: {len(path)} nokta, {length_m:.0f} m -> {out_path}")

    conn.close()
    print("Bitti.")


if __name__ == "__main__":
    main()
