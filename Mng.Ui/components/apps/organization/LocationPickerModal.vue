<script setup lang="ts">
import { ref, watch, onUnmounted } from 'vue';

export interface LocationCoords {
  lat: number;
  lon: number;
}

const props = defineProps<{
  modelValue: LocationCoords | null;
  open: boolean;
}>();

const emit = defineEmits<{
  'update:modelValue': [value: LocationCoords | null];
  'update:open': [value: boolean];
}>();

const mapContainerRef = ref<HTMLElement | null>(null);
let map: import('leaflet').Map | null = null;
let marker: import('leaflet').Marker | null = null;

const DEFAULT_CENTER: [number, number] = [41.0082, 28.9784]; // İstanbul
const DEFAULT_ZOOM = 10;

async function initMap() {
  if (typeof window === 'undefined' || !mapContainerRef.value) return;

  const L = (await import('leaflet')).default;
  await import('leaflet/dist/leaflet.css');

  // Vite/build'da varsayılan marker ikon 404 verir; path'leri düzelt
  delete (L.Icon.Default.prototype as any)._getIconUrl;
  L.Icon.Default.mergeOptions({
    iconUrl: 'https://unpkg.com/leaflet@1.9.4/dist/images/marker-icon.png',
    iconRetinaUrl: 'https://unpkg.com/leaflet@1.9.4/dist/images/marker-icon-2x.png',
    shadowUrl: 'https://unpkg.com/leaflet@1.9.4/dist/images/marker-shadow.png',
  });

  const hasValidLocation =
    props.modelValue &&
    typeof props.modelValue.lat === 'number' &&
    typeof props.modelValue.lon === 'number' &&
    !Number.isNaN(props.modelValue.lat) &&
    !Number.isNaN(props.modelValue.lon);

  const center: [number, number] = hasValidLocation
    ? [props.modelValue!.lat, props.modelValue!.lon]
    : DEFAULT_CENTER;

  map = L.map(mapContainerRef.value).setView(center, hasValidLocation ? 15 : DEFAULT_ZOOM);

  L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
    attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>',
  }).addTo(map);

  if (hasValidLocation) {
    marker = L.marker([props.modelValue!.lat, props.modelValue!.lon]).addTo(map);
  }

  map.on('click', (e: import('leaflet').LeafletMouseEvent) => {
    const { lat, lng } = e.latlng;
    if (marker) {
      marker.setLatLng([lat, lng]);
    } else {
      marker = L.marker([lat, lng]).addTo(map!);
    }
    emit('update:modelValue', { lat, lon: lng });
  });

  // Modal tam acildiktan sonra Leaflet boyutlari guncellesin
  const invalidate = () => map?.invalidateSize();
  setTimeout(invalidate, 150);
  setTimeout(invalidate, 400);
  setTimeout(invalidate, 800);
}

function destroyMap() {
  if (map) {
    map.remove();
    map = null;
    marker = null;
  }
}

function confirm() {
  emit('update:open', false);
}

function clearLocation() {
  if (marker && map) {
    map.removeLayer(marker);
    marker = null;
  }
  emit('update:modelValue', null);
}

watch(
  () => props.open,
  (isOpen) => {
    if (isOpen) {
      setTimeout(() => initMap(), 250);
    } else {
      destroyMap();
    }
  }
);

onUnmounted(() => destroyMap());
</script>

<template>
  <v-dialog
    :model-value="open"
    max-width="960"
    width="90vw"
    persistent
    content-class="location-picker-dialog"
    @update:model-value="emit('update:open', $event)"
  >
    <v-card class="location-picker-card">
      <v-card-title class="d-flex align-center">
        <span class="text-subtitle-1">{{ $t('organization.locationPicker.title', 'Konum seç') }}</span>
        <v-spacer />
        <v-btn icon variant="text" size="small" @click="emit('update:open', false)">
          <v-icon>mdi-close</v-icon>
        </v-btn>
      </v-card-title>
      <v-divider />
      <v-card-text class="location-picker-card-text pa-0">
        <div class="location-picker-map-wrapper">
          <ClientOnly>
            <div ref="mapContainerRef" class="location-picker-map" />
            <template #fallback>
              <div class="d-flex align-center justify-center py-12 text-medium-emphasis">
                <v-progress-circular indeterminate color="primary" />
              </div>
            </template>
          </ClientOnly>
        </div>
      </v-card-text>
      <v-divider />
      <v-card-actions class="pa-3">
        <span v-if="modelValue && typeof modelValue.lat === 'number' && typeof modelValue.lon === 'number'" class="text-caption text-medium-emphasis mr-2">
          {{ modelValue.lat.toFixed(6) }}, {{ modelValue.lon.toFixed(6) }}
        </span>
        <v-spacer />
        <v-btn variant="text" size="small" @click="clearLocation">
          {{ $t('organization.locationPicker.clear', 'Temizle') }}
        </v-btn>
        <v-btn color="primary" variant="flat" size="small" @click="confirm">
          {{ $t('organization.locationPicker.confirm', 'Tamam') }}
        </v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>

<style scoped>
.location-picker-card {
  display: flex;
  flex-direction: column;
}
.location-picker-card-text {
  flex: 1;
  padding: 0 !important;
  overflow: hidden;
}
.location-picker-map-wrapper {
  width: 100%;
  height: 65vh;
  min-height: 400px;
  position: relative;
}
/* ClientOnly wrapper (ust oge) tam alani doldurur */
.location-picker-map-wrapper > * {
  position: absolute !important;
  inset: 0 !important;
  width: 100% !important;
  height: 100% !important;
}
.location-picker-map {
  position: absolute;
  inset: 0;
  width: 100%;
  height: 100%;
}
</style>

<style>
.location-picker-dialog .v-overlay__content {
  align-items: center;
  max-height: 90vh;
}
.location-picker-dialog .v-overlay__content > .v-card {
  max-height: 90vh;
}
</style>
