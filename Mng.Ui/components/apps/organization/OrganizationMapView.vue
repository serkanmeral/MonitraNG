<script setup lang="ts">
import { ref, watch, onUnmounted, onMounted, nextTick, computed } from 'vue';

export interface MapItem {
  __dataId: string;
  name: string;
  location: { lat: number; lon: number };
  description?: string | null;
  childCount?: number;
}

const props = withDefaults(
  defineProps<{
    items: MapItem[];
    highlightedItemId?: string | null;
    height?: string;
    popupHint?: string;
  }>(),
  { popupHint: 'Detaylar için tıklayın' }
);

const emit = defineEmits<{
  'marker-click': [item: MapItem];
}>();

const mapContainerRef = ref<HTMLElement | null>(null);
let map: import('leaflet').Map | null = null;
let L: typeof import('leaflet') | null = null;
const markers: import('leaflet').Marker[] = [];

const DEFAULT_CENTER: [number, number] = [41.0082, 28.9784]; // İstanbul
const DEFAULT_ZOOM = 10;

const mapHeight = computed(() => props.height ?? '65vh');

function hasValidLocation(item: MapItem): item is MapItem & { location: { lat: number; lon: number } } {
  const loc = item.location;
  return (
    loc != null &&
    typeof loc.lat === 'number' &&
    typeof loc.lon === 'number' &&
    !Number.isNaN(loc.lat) &&
    !Number.isNaN(loc.lon)
  );
}

async function initMap() {
  if (typeof window === 'undefined' || !mapContainerRef.value) return;

  const Leaflet = await import('leaflet');
  L = Leaflet.default;
  await import('leaflet/dist/leaflet.css');

  delete (L.Icon.Default.prototype as any)._getIconUrl;
  L.Icon.Default.mergeOptions({
    iconUrl: 'https://unpkg.com/leaflet@1.9.4/dist/images/marker-icon.png',
    iconRetinaUrl: 'https://unpkg.com/leaflet@1.9.4/dist/images/marker-icon-2x.png',
    shadowUrl: 'https://unpkg.com/leaflet@1.9.4/dist/images/marker-shadow.png',
  });

  const validItems = props.items.filter(hasValidLocation);
  const center: [number, number] =
    validItems.length > 0
      ? [validItems[0].location.lat, validItems[0].location.lon]
      : DEFAULT_CENTER;

  map = L.map(mapContainerRef.value).setView(center, DEFAULT_ZOOM);

  L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
    attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>',
  }).addTo(map);

  updateMarkers(validItems);

  if (validItems.length > 1) {
    const group = L.featureGroup(
      validItems.map((i) => L.marker([i.location.lat, i.location.lon]))
    );
    map.fitBounds(group.getBounds().pad(0.1));
  } else if (validItems.length === 1) {
    map.setView([validItems[0].location.lat, validItems[0].location.lon], 15);
  }

  const invalidate = () => map?.invalidateSize();
  setTimeout(invalidate, 100);
  setTimeout(invalidate, 400);
  setTimeout(invalidate, 800);
}

let resizeObserver: ResizeObserver | null = null;
let observedElement: HTMLElement | null = null;

function updateMarkers(items: MapItem[]) {
  if (!map || !L) return;

  markers.forEach((m) => {
    map!.removeLayer(m);
  });
  markers.length = 0;

  for (const item of items) {
    if (!hasValidLocation(item)) continue;
    const marker = L!.marker([item.location.lat, item.location.lon]).addTo(map!);
    const popupContent = buildPopupContent(item);
    marker.bindPopup(popupContent);
    marker.on('click', () => {
      emit('marker-click', item);
    });
    markers.push(marker);
  }
}

function buildPopupContent(item: MapItem): string {
  const lines: string[] = [`<strong>${escapeHtml(item.name)}</strong>`];
  if (item.description) {
    lines.push(`<div class="text-caption">${escapeHtml(item.description)}</div>`);
  }
  if (typeof item.childCount === 'number' && item.childCount > 0) {
    lines.push(`<div class="text-caption">${item.childCount} alt öğe</div>`);
  }
  if (props.popupHint) {
    lines.push(`<div class="text-caption text-medium-emphasis mt-1">${escapeHtml(props.popupHint)}</div>`);
  }
  return lines.join('<br/>');
}

function escapeHtml(s: string): string {
  const div = document.createElement('div');
  div.textContent = s;
  return div.innerHTML;
}

function destroyMap() {
  if (resizeObserver && observedElement) {
    try {
      resizeObserver.unobserve(observedElement);
    } catch (_) {}
    resizeObserver = null;
    observedElement = null;
  }
  markers.length = 0;
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
        resizeObserver = new ResizeObserver(() => map?.invalidateSize());
        resizeObserver.observe(observedElement);
      }
    }, 150);
  });
});

watch(
  () => [props.items, props.highlightedItemId],
  () => {
    if (!map) return;
    const validItems = props.items.filter(hasValidLocation);
    updateMarkers(validItems);
    if (props.highlightedItemId && validItems.length > 0) {
      const highlighted = validItems.find((i) => i.__dataId === props.highlightedItemId);
      if (highlighted) {
        map.setView([highlighted.location.lat, highlighted.location.lon], 15);
        const m = markers.find((_, idx) => validItems[idx]?.__dataId === props.highlightedItemId);
        m?.openPopup();
      }
    }
  },
  { deep: true }
);

onUnmounted(() => destroyMap());
</script>

<template>
  <div class="organization-map-view" :style="{ height: mapHeight }">
    <ClientOnly>
      <div ref="mapContainerRef" class="organization-map-container" />
      <template #fallback>
        <div class="map-fallback d-flex align-center justify-center">
          <v-progress-circular indeterminate color="primary" />
        </div>
      </template>
    </ClientOnly>
  </div>
</template>

<style scoped>
.organization-map-view {
  width: 100%;
  min-height: 0;
  height: 100%;
  position: relative;
  display: block;
  overflow: hidden;
  box-sizing: border-box;
}
.organization-map-container {
  position: absolute;
  inset: 0;
  width: 100%;
  height: 100%;
  box-sizing: border-box;
}
.organization-map-view > * {
  position: absolute !important;
  inset: 0 !important;
  width: 100% !important;
  height: 100% !important;
  box-sizing: border-box !important;
}
.map-fallback {
  min-height: 200px;
}
</style>
