<script setup lang="ts">
import { computed, watch, onMounted, onUnmounted } from 'vue';
import { useMapPositions } from '@/composables/useMapPositions';
import MonitoringMapView from '@/components/apps/monitoring/MonitoringMapView.vue';
import type { Widget } from '@/stores/apps/widget';

const props = defineProps<{
  widget: Widget;
  t?: (key: string) => string;
}>();

const config = useRuntimeConfig();
const geoServerAvailable = computed(() => !!((config.public as any).geoServerBaseUrl as string)?.trim?.());

const { positions, loading, error, refresh } = useMapPositions();

/** Widget config'deki assetIds (boşsa tüm konumlar gösterilir) */
const allowedAssetIds = computed(() => {
  const cfg = props.widget?.config as any;
  const ids = cfg?.assetIds;
  if (Array.isArray(ids) && ids.length > 0) return new Set(ids);
  return null;
});

const filteredPositions = computed(() => {
  const list = positions.value;
  const allowed = allowedAssetIds.value;
  if (!allowed) return list;
  return list.filter((p) => allowed.has(p.assetId));
});

const refreshIntervalSeconds = computed(() => {
  const cfg = props.widget?.config as any;
  return cfg?.refreshIntervalSeconds ?? 0;
});

const mapWidgetConfig = computed(() => {
  const cfg = props.widget?.config as any;
  return {
    initialZoom: cfg?.defaultZoom != null ? Number(cfg.defaultZoom) : undefined,
    defaultBaseLayer: (cfg?.defaultBaseLayer === 'geoserver' || cfg?.defaultBaseLayer === 'osm') ? cfg.defaultBaseLayer : undefined,
    defaultLayerVisibility: cfg?.defaultLayerVisibility && typeof cfg.defaultLayerVisibility === 'object' ? cfg.defaultLayerVisibility : undefined,
  };
});

let refreshTimer: ReturnType<typeof setInterval> | null = null;

function setupRefreshTimer() {
  if (refreshTimer) {
    clearInterval(refreshTimer);
    refreshTimer = null;
  }
  const sec = refreshIntervalSeconds.value;
  if (sec > 0) {
    refreshTimer = setInterval(() => refresh(), sec * 1000);
  }
}

onMounted(() => {
  refresh();
  setupRefreshTimer();
});

onUnmounted(() => {
  if (refreshTimer) {
    clearInterval(refreshTimer);
    refreshTimer = null;
  }
});

watch(refreshIntervalSeconds, () => setupRefreshTimer());
</script>

<template>
  <div class="map-widget">
    <div v-if="loading && filteredPositions.length === 0" class="d-flex justify-center align-center pa-4" style="min-height: 280px;">
      <v-progress-circular indeterminate color="primary" size="32" />
    </div>
    <v-alert
      v-else-if="error"
      type="error"
      variant="tonal"
      density="compact"
      class="mb-2"
    >
      {{ error }}
    </v-alert>
    <ClientOnly v-else>
      <MonitoringMapView
        :positions="filteredPositions"
        :geo-server-available="geoServerAvailable"
        height="320px"
        popup-hint=""
        :initial-zoom="mapWidgetConfig.initialZoom"
        :default-base-layer="mapWidgetConfig.defaultBaseLayer"
        :default-layer-visibility="mapWidgetConfig.defaultLayerVisibility"
        :default-controls-open="false"
      />
    </ClientOnly>
  </div>
</template>

<style scoped>
.map-widget {
  width: 100%;
  min-height: 320px;
}
</style>
