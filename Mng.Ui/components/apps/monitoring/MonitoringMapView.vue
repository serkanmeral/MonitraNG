<script setup lang="ts">
import { ref, reactive, watch, onUnmounted, onMounted, nextTick, computed } from 'vue';

/** GeoServer katman anahtarları (haritada eklenme sırası: alttan üste) */
const GEOSERVER_LAYER_KEYS = ['landuse', 'roads', 'waterways', 'water_areas', 'railways', 'stations', 'places'] as const;
const GEOSERVER_LAYER_LABELS: Record<string, string> = {
  landuse: 'Arazi kullanımı',
  roads: 'Yollar',
  waterways: 'Su yolları',
  water_areas: 'Su alanları',
  railways: 'Demiryolları',
  stations: 'İstasyonlar',
  places: 'Yerleşimler',
};

/** Haritada gösterilecek tren/asset konum verisi (MngHub/DataGateway kaynaklı). */
export interface MapPosition {
  assetId: string;
  name?: string;
  lat: number;
  lon: number;
  updatedAt?: string;
  speed?: number;
  routeId?: string;
  trainId?: string;
  /** Ek alanlar (sensors vb.) modal için kullanılabilir */
  [key: string]: unknown;
}

const props = withDefaults(
  defineProps<{
    positions: MapPosition[];
    /** GeoServer altlığı kullanılabilsin mi (runtimeConfig.geoServerBaseUrl dolu ise) */
    geoServerAvailable?: boolean;
    height?: string;
    popupHint?: string;
    /** Başlangıç zoom seviyesi (örn. 4–14) */
    initialZoom?: number;
    /** Varsayılan harita altlığı */
    defaultBaseLayer?: 'osm' | 'geoserver';
    /** GeoServer katmanlarının varsayılan açık/kapalı (key: landuse, roads, ...) */
    defaultLayerVisibility?: Record<string, boolean>;
    /** Sağ panel (Harita & Katmanlar) varsayılan açık mı (dashboard widget'ta false verilebilir) */
    defaultControlsOpen?: boolean;
  }>(),
  { geoServerAvailable: false, popupHint: 'Detaylar için tıklayın', initialZoom: 6, defaultBaseLayer: 'osm', defaultControlsOpen: true }
);

const emit = defineEmits<{
  'marker-click': [position: MapPosition];
}>();

const mapContainerRef = ref<HTMLElement | null>(null);
let map: import('leaflet').Map | null = null;
let L: typeof import('leaflet') | null = null;
const markers: import('leaflet').Marker[] = [];
let osmLayer: import('leaflet').TileLayer | null = null;
let geoserverLayers: import('leaflet').TileLayer[] = [];

const DEFAULT_CENTER: [number, number] = [39.2, 32.5];
const DEFAULT_ZOOM = 6;

const mapHeight = computed(() => props.height ?? '70vh');

/** Tek seçim: 'osm' = çevrimiçi, 'geoserver' = çevrimdışı */
const baseLayer = ref<'osm' | 'geoserver'>(props.defaultBaseLayer ?? 'osm');

const defaultVisibility: Record<string, boolean> = {
  landuse: true,
  roads: true,
  waterways: true,
  water_areas: true,
  railways: true,
  stations: true,
  places: true,
};

/** Çevrimdışı seçiliyken hangi GeoServer katmanları görünsün (props.defaultLayerVisibility ile birleştirilir) */
const layerVisibility = reactive<Record<string, boolean>>({
  ...defaultVisibility,
  ...(props.defaultLayerVisibility || {}),
});

/** Sağ panel açık mı (açılır/kapanır) */
const controlsOpen = ref(props.defaultControlsOpen !== false);

function getLayerLabel(key: string): string {
  return GEOSERVER_LAYER_LABELS[key] ?? key;
}

function validPosition(p: MapPosition): boolean {
  return (
    typeof p.lat === 'number' &&
    typeof p.lon === 'number' &&
    !Number.isNaN(p.lat) &&
    !Number.isNaN(p.lon)
  );
}

function buildGeoserverLayers(): import('leaflet').TileLayer[] {
  if (!L) return [];
  const base = '/api/tiles/geoserver';
  return [
    L.tileLayer(`${base}?layer=landuse&z={z}&x={x}&y={y}`, { attribution: '' }),
    L.tileLayer(`${base}?layer=roads&z={z}&x={x}&y={y}`, { attribution: '' }),
    L.tileLayer(`${base}?layer=waterways&z={z}&x={x}&y={y}`, { attribution: '' }),
    L.tileLayer(`${base}?layer=water_areas&z={z}&x={x}&y={y}`, { attribution: '' }),
    L.tileLayer(`${base}?layer=railways&z={z}&x={x}&y={y}`, { attribution: 'GeoServer tr_rail' }),
    L.tileLayer(`${base}?layer=stations&z={z}&x={x}&y={y}`, { attribution: '' }),
    L.tileLayer(`${base}?layer=places&z={z}&x={x}&y={y}`, { attribution: '' }),
  ];
}

function getTrainIcon(displayLabel: string, hasAlert?: boolean): import('leaflet').DivIcon | undefined {
  if (!L) return undefined;
  const bg = hasAlert ? '#e85d04' : '#c00';
  const label = displayLabel.replace(/^T/, '') || '?';
  return L.divIcon({
    className: 'monitoring-train-marker',
    html: `<span style="background:${bg};color:#fff;border-radius:50%;width:20px;height:20px;display:inline-flex;align-items:center;justify-content:center;font-size:10px;font-weight:bold;border:2px solid #fff;box-shadow:0 1px 3px rgba(0,0,0,0.3)">${escapeHtml(String(label))}</span>`,
    iconSize: [24, 24],
    iconAnchor: [12, 12],
  });
}

function escapeHtml(s: string): string {
  const div = document.createElement('div');
  div.textContent = s;
  return div.innerHTML;
}

function buildPopupContent(p: MapPosition): string {
  const name = p.name ?? p.trainId ?? p.assetId ?? '—';
  const lines: string[] = [`<strong>${escapeHtml(name)}</strong>`];
  if (p.routeId) lines.push(`Rota: ${escapeHtml(p.routeId)}`);
  lines.push(`${Number(p.lat).toFixed(5)}, ${Number(p.lon).toFixed(5)}`);
  if (p.speed != null) lines.push(`Hız: ${Number(p.speed).toFixed(1)} km/h`);
  if (p.updatedAt) lines.push(`<span class="text-caption">${escapeHtml(new Date(p.updatedAt).toLocaleString('tr-TR'))}</span>`);
  if (props.popupHint) lines.push(`<div class="text-caption text-medium-emphasis mt-1">${escapeHtml(props.popupHint)}</div>`);
  return lines.join('<br/>');
}

function updateBaseLayers() {
  if (!map || !L) return;
  const useOsm = baseLayer.value === 'osm';
  if (osmLayer) {
    if (useOsm) map.addLayer(osmLayer);
    else map.removeLayer(osmLayer);
  }
  geoserverLayers.forEach((ly) => map.removeLayer(ly));
  if (baseLayer.value === 'geoserver') {
    GEOSERVER_LAYER_KEYS.forEach((key, i) => {
      if (layerVisibility[key] && geoserverLayers[i]) map.addLayer(geoserverLayers[i]);
    });
  }
}

function updateMarkers(positions: MapPosition[]) {
  if (!map || !L) return;

  markers.forEach((m) => map!.removeLayer(m));
  markers.length = 0;

  const valid = positions.filter(validPosition);
  for (const p of valid) {
    const label = p.trainId ?? p.name ?? p.assetId?.slice(0, 8) ?? '?';
    const marker = L!.marker([p.lat, p.lon], { icon: getTrainIcon(label) }).addTo(map!);
    marker.bindPopup(buildPopupContent(p), { maxWidth: 320 });
    marker.on('click', () => emit('marker-click', p));
    markers.push(marker);
  }
}

async function initMap() {
  if (typeof window === 'undefined' || !mapContainerRef.value) return;

  const Leaflet = await import('leaflet');
  L = Leaflet.default;
  await import('leaflet/dist/leaflet.css');

  const valid = props.positions.filter(validPosition);
  const center: [number, number] =
    valid.length > 0 ? [valid[0].lat, valid[0].lon] : DEFAULT_CENTER;

  const zoom = props.initialZoom ?? DEFAULT_ZOOM;
  map = L.map(mapContainerRef.value).setView(center, zoom);

  osmLayer = L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
    attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>',
  });
  if (baseLayer.value === 'osm' && osmLayer) osmLayer.addTo(map);

  if (props.geoServerAvailable) {
    geoserverLayers = buildGeoserverLayers();
    if (baseLayer.value === 'geoserver') {
      GEOSERVER_LAYER_KEYS.forEach((key, i) => {
        if (layerVisibility[key] && geoserverLayers[i]) geoserverLayers[i].addTo(map!);
      });
    }
  }

  updateMarkers(props.positions);

  if (valid.length > 1) {
    const group = L.featureGroup(markers);
    map.fitBounds(group.getBounds().pad(0.1));
  } else if (valid.length === 1) {
    map.setView([valid[0].lat, valid[0].lon], props.initialZoom ?? 12);
  }

  const invalidate = () => map?.invalidateSize();
  setTimeout(invalidate, 100);
  setTimeout(invalidate, 400);
  setTimeout(invalidate, 800);
}

let resizeObserver: ResizeObserver | null = null;
let observedElement: HTMLElement | null = null;

function destroyMap() {
  if (resizeObserver && observedElement) {
    try {
      resizeObserver.unobserve(observedElement);
    } catch (_) {}
    resizeObserver = null;
    observedElement = null;
  }
  markers.length = 0;
  geoserverLayers = [];
  osmLayer = null;
  if (map) {
    map.remove();
    map = null;
  }
}

onMounted(() => {
  nextTick(() => {
    setTimeout(async () => {
      await initMap();
      if (mapContainerRef.value && typeof ResizeObserver !== 'undefined') {
        observedElement = mapContainerRef.value;
        resizeObserver = new ResizeObserver(() => {
          map?.invalidateSize();
        });
        resizeObserver.observe(observedElement);
      }
    }, 150);
  });
});

watch(
  () => [props.positions, props.geoServerAvailable],
  () => {
    if (!map) return;
    updateMarkers(props.positions);
    if (props.geoServerAvailable && geoserverLayers.length === 0) {
      geoserverLayers = buildGeoserverLayers();
      if (baseLayer.value === 'geoserver') {
        GEOSERVER_LAYER_KEYS.forEach((key, i) => {
          if (layerVisibility[key] && geoserverLayers[i]) map!.addLayer(geoserverLayers[i]);
        });
      }
    }
  },
  { deep: true }
);

watch(baseLayer, () => {
  if (!map) return;
  updateBaseLayers();
});

watch(
  layerVisibility,
  () => {
    if (!map || baseLayer.value !== 'geoserver') return;
    updateBaseLayers();
  },
  { deep: true }
);

onUnmounted(() => destroyMap());
</script>

<template>
  <div class="monitoring-map-view" :style="{ height: mapHeight }">
    <div class="map-controls map-controls--right">
      <div class="map-controls-header">
        <v-btn
          size="small"
          variant="text"
          color="primary"
          class="map-controls-toggle"
          @click="controlsOpen = !controlsOpen"
        >
          <span class="map-controls-toggle-icon">{{ controlsOpen ? '▼' : '▶' }}</span>
          <span class="map-controls-toggle-text">Harita & katmanlar</span>
        </v-btn>
      </div>
      <v-expand-transition>
        <div v-show="controlsOpen" class="map-controls-body">
          <span class="map-control-label">Harita altlığı</span>
          <v-btn-toggle
            v-model="baseLayer"
            mandatory
            density="compact"
            color="primary"
            variant="outlined"
            class="map-base-layer-toggle"
            divided
          >
            <v-btn value="osm" size="small" class="map-base-layer-btn">
              <span class="map-base-layer-icon">🌐</span>
              <span class="map-base-layer-text">Çevrimiçi</span>
              <span class="map-base-layer-hint">OSM</span>
            </v-btn>
            <v-btn
              v-if="geoServerAvailable"
              value="geoserver"
              size="small"
              class="map-base-layer-btn"
            >
              <span class="map-base-layer-icon">🗺️</span>
              <span class="map-base-layer-text">Çevrimdışı</span>
              <span class="map-base-layer-hint">GeoServer</span>
            </v-btn>
          </v-btn-toggle>

          <template v-if="baseLayer === 'geoserver' && geoServerAvailable">
            <v-divider class="my-2" />
            <span class="map-control-label">Katmanlar</span>
            <div class="map-layer-list">
              <label
                v-for="key in GEOSERVER_LAYER_KEYS"
                :key="key"
                class="map-layer-item"
              >
                <input
                  v-model="layerVisibility[key]"
                  type="checkbox"
                  class="map-layer-checkbox"
                />
                <span class="map-layer-name">{{ getLayerLabel(key) }}</span>
              </label>
            </div>
          </template>
        </div>
      </v-expand-transition>
    </div>
    <div class="map-wrapper">
      <!-- ClientOnly kaldırıldı: fallback div'e bağlanan Leaflet küçük kalıyordu; tek container kullanılıyor -->
      <div ref="mapContainerRef" class="monitoring-map-container" />
    </div>
  </div>
</template>

<style scoped>
.monitoring-map-view {
  width: 100%;
  min-height: 0;
  height: 100%;
  position: relative;
  display: block;
  overflow: hidden;
  box-sizing: border-box;
}
.map-controls {
  position: absolute;
  top: 10px;
  z-index: 1000;
  background: #fff;
  border-radius: 10px;
  box-shadow: 0 2px 12px rgba(0, 0, 0, 0.15);
  font-size: 13px;
  min-width: 160px;
}
.map-controls--right {
  right: 10px;
  left: auto;
}
.map-controls-header {
  padding: 6px 10px;
  border-bottom: 1px solid rgba(0, 0, 0, 0.08);
}
.map-controls-toggle {
  text-transform: none;
  letter-spacing: 0;
  justify-content: flex-start;
  min-height: 36px;
}
.map-controls-toggle-icon {
  font-size: 10px;
  margin-right: 6px;
  opacity: 0.8;
}
.map-controls-toggle-text {
  font-size: 13px;
  font-weight: 500;
}
.map-controls-body {
  padding: 10px 12px;
  display: flex;
  flex-direction: column;
  gap: 8px;
}
.map-control-label {
  font-weight: 600;
  color: #333;
  font-size: 12px;
  text-transform: uppercase;
  letter-spacing: 0.02em;
}
.map-base-layer-toggle {
  border-radius: 8px;
  overflow: hidden;
}
.map-base-layer-btn {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 2px;
  min-height: auto;
  padding: 8px 14px !important;
}
.map-base-layer-icon {
  font-size: 1.1rem;
  line-height: 1;
}
.map-base-layer-text {
  font-weight: 500;
  font-size: 12px;
}
.map-base-layer-hint {
  font-size: 10px;
  opacity: 0.85;
}
.map-layer-list {
  display: flex;
  flex-direction: column;
  gap: 4px;
  max-height: 200px;
  overflow-y: auto;
}
.map-layer-item {
  display: flex;
  align-items: center;
  gap: 8px;
  cursor: pointer;
  font-size: 12px;
  padding: 2px 0;
  user-select: none;
  min-width: 140px;
}
.map-layer-item:hover {
  color: var(--v-theme-primary);
}
.map-layer-checkbox {
  width: 14px;
  height: 14px;
  accent-color: var(--v-theme-primary);
  cursor: pointer;
}
.map-layer-name {
  flex: 1;
  font-size: 13px;
  color: #333;
  line-height: 1.3;
  min-width: 0;
}
.map-wrapper {
  position: absolute;
  inset: 0;
  width: 100%;
  height: 100%;
  box-sizing: border-box;
}
.monitoring-map-container {
  position: absolute;
  inset: 0;
  width: 100%;
  height: 100%;
  box-sizing: border-box;
}
:deep(.monitoring-train-marker) {
  background: transparent !important;
  border: none !important;
}
</style>
