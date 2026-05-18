/**
 * Harita için tren/asset konum verisi.
 * DataGateway mon_metrics (lat/lon collectible) ve mon_assets (isim) kullanır.
 * İleride MngHub'dan tek endpoint ile de beslenebilir.
 */
import { fetchFromDataGateway } from '@/services/apiService';
import type { MapPosition } from '@/components/apps/monitoring/MonitoringMapView.vue';

const METRICS_DATASET = 'mon_metrics';
const ASSETS_DATASET = 'mon_assets';
const LIMIT = 150;

interface MetricRow {
  meta?: { assetId?: string; collectibleCode?: string };
  value?: number | string;
  timestamp?: string;
}

export function useMapPositions() {
  const positions = ref<MapPosition[]>([]);
  const loading = ref(false);
  const error = ref<string | null>(null);
  const lastUpdated = ref<string | null>(null);

  async function refresh() {
    loading.value = true;
    error.value = null;
    try {
      const [latRes, lonRes] = await Promise.all([
        fetchFromDataGateway(
          `/api/v1/data/${METRICS_DATASET}?filter=${encodeURIComponent('meta.collectibleCode:eq:lat')}&sort=-timestamp&limit=${LIMIT}`
        ),
        fetchFromDataGateway(
          `/api/v1/data/${METRICS_DATASET}?filter=${encodeURIComponent('meta.collectibleCode:eq:lon')}&sort=-timestamp&limit=${LIMIT}`
        ),
      ]);

      const latRows: MetricRow[] = Array.isArray(latRes) ? latRes : latRes?.data ?? latRes?.items ?? [];
      const lonRows: MetricRow[] = Array.isArray(lonRes) ? lonRes : lonRes?.data ?? lonRes?.items ?? [];

      const byAsset = new Map<string, { lat?: number; lon?: number; updatedAt?: string }>();

      for (const row of latRows) {
        const assetId = row.meta?.assetId;
        if (!assetId) continue;
        const num = typeof row.value === 'number' ? row.value : parseFloat(String(row.value));
        if (Number.isNaN(num)) continue;
        const existing = byAsset.get(assetId) ?? {};
        byAsset.set(assetId, { ...existing, lat: num, updatedAt: row.timestamp ?? existing.updatedAt });
      }

      for (const row of lonRows) {
        const assetId = row.meta?.assetId;
        if (!assetId) continue;
        const num = typeof row.value === 'number' ? row.value : parseFloat(String(row.value));
        if (Number.isNaN(num)) continue;
        const existing = byAsset.get(assetId) ?? {};
        byAsset.set(assetId, { ...existing, lon: num, updatedAt: row.timestamp ?? existing.updatedAt });
      }

      const assetIds = [...byAsset.keys()].filter((id) => {
        const p = byAsset.get(id)!;
        return p.lat != null && p.lon != null && !Number.isNaN(p.lat) && !Number.isNaN(p.lon);
      });

      let nameByAsset: Record<string, string> = {};
      try {
        const assetsRes = await fetchFromDataGateway(
          `/api/v1/data/${ASSETS_DATASET}?limit=500`
        );
        const assets = Array.isArray(assetsRes) ? assetsRes : assetsRes?.data ?? assetsRes?.items ?? [];
        for (const a of assets) {
          const id = a?.__dataId ?? a?.dataId;
          if (id) nameByAsset[id] = a?.name ?? a?.itemName ?? id;
        }
      } catch (_) {
        // İsimler opsiyonel
      }

      const list: MapPosition[] = assetIds.map((assetId) => {
        const p = byAsset.get(assetId)!;
        return {
          assetId,
          name: nameByAsset[assetId],
          lat: p.lat!,
          lon: p.lon!,
          updatedAt: p.updatedAt ?? undefined,
        };
      });

      positions.value = list;
      lastUpdated.value = new Date().toISOString();
    } catch (e: any) {
      error.value = e?.data?.errorDescription ?? e?.message ?? String(e);
      positions.value = [];
    } finally {
      loading.value = false;
    }
  }

  return { positions, loading, error, lastUpdated, refresh };
}
