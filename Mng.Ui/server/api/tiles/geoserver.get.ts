/**
 * GeoServer WMTS tile proxy.
 * Harita altlığı (çevrimdışı) için tile istekleri aynı origin üzerinden yapılır; CORS/SSL sorunu olmaz.
 * Query: layer, z, x, y. layer: railways|stations|places|roads|waterways|water_areas|landuse
 * MngSim ile uyumlu: TileMatrixSet (EPSG:900913/EPSG:3857) ve col/row ofset; 400 TileOutOfRange'de şeffaf tile.
 */
import { getQuery } from 'h3';

const LAYER_MAP: Record<string, string> = {
  railways: 'tr_rail:railways',
  stations: 'tr_rail:stations',
  places: 'tr_rail:places',
  roads: 'tr_rail:roads',
  waterways: 'tr_rail:waterways',
  water_areas: 'tr_rail:water_areas',
  landuse: 'tr_rail:landuse',
};

/** 1x1 şeffaf PNG (GeoServer 400 TileOutOfRange için boş tile) */
const TRANSPARENT_PNG = Buffer.from([
  0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a, 0x00, 0x00, 0x00, 0x0d, 0x49, 0x48, 0x44, 0x52,
  0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, 0x08, 0x06, 0x00, 0x00, 0x00, 0x1f, 0x15, 0xc4,
  0x89, 0x00, 0x00, 0x00, 0x0a, 0x49, 0x44, 0x41, 0x54, 0x78, 0x9c, 0x63, 0x00, 0x01, 0x00, 0x00,
  0x05, 0x00, 0x01, 0x0d, 0x0a, 0x2d, 0xb4, 0x00, 0x00, 0x00, 0x00, 0x49, 0x45, 0x4e, 0x44, 0xae,
  0x42, 0x60, 0x82,
]);

export default defineEventHandler(async (event) => {
  const config = useRuntimeConfig();
  const baseUrl = (config.public.geoServerBaseUrl as string)?.trim?.();
  if (!baseUrl) {
    throw createError({ statusCode: 404, statusMessage: 'GeoServer base URL not configured' });
  }

  const query = getQuery(event);
  const layer = (query.layer as string)?.trim?.();
  const zStr = query.z as string;
  const xStr = query.x as string;
  const yStr = query.y as string;

  if (!layer || !LAYER_MAP[layer]) {
    throw createError({
      statusCode: 400,
      statusMessage: 'Query "layer" required: railways, stations, places, roads, waterways, water_areas, landuse',
    });
  }

  const z = parseInt(zStr, 10);
  const x = parseInt(xStr, 10);
  const y = parseInt(yStr, 10);
  if (Number.isNaN(z) || Number.isNaN(x) || Number.isNaN(y)) {
    throw createError({ statusCode: 400, statusMessage: 'Query z, x, y must be integers' });
  }

  const tileMatrixSet = (config.public.geoServerTileMatrixSet as string)?.trim?.() || 'EPSG:900913';
  const colOffset = Number(config.public.geoServerTileColOffset) || 0;
  const rowOffset = Number(config.public.geoServerTileRowOffset) || 0;
  const tileCol = x + colOffset;
  const tileRow = y + rowOffset;

  const layerName = LAYER_MAP[layer];
  const wmtsUrl = `${baseUrl}/geoserver/gwc/service/wmts?request=GetTile&service=WMTS&version=1.0.0&format=image/png&tilematrixset=${encodeURIComponent(tileMatrixSet)}&style=&layer=${encodeURIComponent(layerName)}&tilematrix=${encodeURIComponent(tileMatrixSet)}:${z}&tilerow=${tileRow}&tilecol=${tileCol}`;

  try {
    const response = await $fetch.raw(wmtsUrl, { responseType: 'arrayBuffer' });
    const data = response._data as ArrayBuffer;
    const status = response.status;
    if (status !== 200) {
      throw createError({ statusCode: status, statusMessage: 'GeoServer tile error' });
    }
    setHeader(event, 'Content-Type', 'image/png');
    setHeader(event, 'Cache-Control', 'public, max-age=3600');
    return Buffer.from(data);
  } catch (e: any) {
    // GeoServer 400 TileOutOfRange (grid subset sınırı) → şeffaf tile; harita kırılmasın
    const status = e?.statusCode ?? e?.response?.status ?? e?.status;
    if (status === 400) {
      setHeader(event, 'Content-Type', 'image/png');
      setHeader(event, 'Cache-Control', 'public, max-age=60');
      return TRANSPARENT_PNG;
    }
    if (e?.statusCode) throw e;
    throw createError({ statusCode: 502, statusMessage: e?.message || 'GeoServer unreachable' });
  }
});
