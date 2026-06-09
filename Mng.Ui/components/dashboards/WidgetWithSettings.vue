<script setup lang="ts">
import { ref, computed, onMounted, watch } from 'vue';
import WidgetRenderer from '@/components/widgets/WidgetRenderer.vue';
import { useWidgetStore } from '@/stores/apps/widget';
import type { WidgetConfigOverrides } from '@/stores/apps/dashboard';
import type { SurfaceContext } from '@/types/apps/widgetManifest';

const props = defineProps<{
  widgetId: string;
  widgetOverrides?: WidgetConfigOverrides;
  surfaceContext?: SurfaceContext;
  rowIdx: number;
  colIdx: number;
  canEdit?: boolean;
  t?: (key: string) => string;
}>();

const emit = defineEmits<{
  'update:overrides': [payload: { rowIdx: number; colIdx: number; overrides: WidgetConfigOverrides }];
}>();

const widgetStore = useWidgetStore();
const settingsOpen = ref(false);
const loadedWidget = ref<any>(null);

const timeRangeMinutes = ref<number | null>(60);
const limit = ref(500);
const refreshIntervalSeconds = ref(0);

const TIME_RANGE_OPTIONS = [
  { value: 20, labelKey: 'monitoring.control.last20min' },
  { value: 60, labelKey: 'monitoring.control.last1h' },
  { value: 360, labelKey: 'monitoring.control.last6h' },
  { value: 1440, labelKey: 'monitoring.control.last1d' },
  { value: 10080, labelKey: 'monitoring.control.last7d' },
  { value: null, labelKey: 'monitoring.control.allTime' },
];

const LIMIT_OPTIONS = [10, 50, 100, 250, 500, 1000, 2000];
const REFRESH_OPTIONS = [
  { value: 0, labelKey: 'monitoring.widgets.refreshOff' },
  { value: 30, labelKey: 'monitoring.widgets.refresh30s' },
  { value: 60, labelKey: 'monitoring.widgets.refresh60s' },
  { value: 120, labelKey: 'monitoring.widgets.refresh120s' },
  { value: 300, labelKey: 'monitoring.widgets.refresh300s' },
];

const isMonitoring = computed(() => !!loadedWidget.value?.config?.monitoring);
const isMapWidget = computed(() => loadedWidget.value?.type === 'map');
const showSettingsBtn = computed(() => props.canEdit && isMonitoring.value);

const effectiveOverrides = computed(() => {
  const base = loadedWidget.value?.config || {};
  const over = props.widgetOverrides || {};
  const merged: Record<string, any> = {
    refreshIntervalSeconds: over.refreshIntervalSeconds ?? base.refreshIntervalSeconds ?? 0,
  };
  if (!isMapWidget.value) {
    merged.timeRangeMinutes = over.timeRangeMinutes ?? base.timeRangeMinutes ?? 60;
    merged.limit = over.limit ?? base.limit ?? 500;
  }
  return merged;
});

function mt(key: string, fallback: string): string {
  return props.t?.(key) ?? fallback;
}

async function loadWidget() {
  if (!props.widgetId?.trim()) return;
  try {
    const w = await widgetStore.fetchWidgetById(props.widgetId);
    loadedWidget.value = w;
    syncFromOverrides();
  } catch {
    loadedWidget.value = null;
  }
}

function syncFromOverrides() {
  const eff = effectiveOverrides.value;
  if (eff.timeRangeMinutes != null) timeRangeMinutes.value = eff.timeRangeMinutes;
  if (eff.limit != null) limit.value = eff.limit;
  refreshIntervalSeconds.value = eff.refreshIntervalSeconds || 0;
}

function applySettings() {
  const overrides: Record<string, any> = { refreshIntervalSeconds: refreshIntervalSeconds.value || 0 };
  if (!isMapWidget.value) {
    overrides.timeRangeMinutes = timeRangeMinutes.value;
    overrides.limit = limit.value;
  }
  emit('update:overrides', {
    rowIdx: props.rowIdx,
    colIdx: props.colIdx,
    overrides,
  });
  settingsOpen.value = false;
}

onMounted(loadWidget);
watch(() => props.widgetId, loadWidget);
watch([effectiveOverrides, () => props.widgetOverrides], syncFromOverrides, { deep: true });
</script>

<template>
  <div class="widget-with-settings position-relative">
    <widgets-widget-renderer
      :widget-id="widgetId"
      :config-overrides="effectiveOverrides"
      :surface-context="surfaceContext"
      :t="t"
    />
    <v-menu
      v-if="showSettingsBtn"
      v-model="settingsOpen"
      :close-on-content-click="false"
      location="bottom end"
      origin="top end"
      transition="scale-transition"
    >
      <template #activator="{ props: menuProps }">
        <v-btn
          v-bind="menuProps"
          icon
          size="small"
          variant="flat"
          color="primary"
          class="widget-settings-btn"
        >
          <v-icon size="18">mdi-cog</v-icon>
          <v-tooltip activator="parent" location="top">
            {{ mt('monitoring.widgets.widgetSettings', 'Widget ayarları') }}
          </v-tooltip>
        </v-btn>
      </template>
      <v-card min-width="320" class="pa-4">
        <v-card-title class="text-subtitle-1 py-2">
          {{ mt('monitoring.widgets.widgetSettings', 'Widget ayarları') }}
        </v-card-title>
        <v-card-text>
          <template v-if="!isMapWidget">
            <v-select
              v-model="timeRangeMinutes"
              :items="TIME_RANGE_OPTIONS.map((o) => ({ value: o.value, title: mt(o.labelKey, String(o.value ?? 'Tümü')) }))"
              item-title="title"
              item-value="value"
              :label="mt('monitoring.control.timeRange', 'Zaman aralığı')"
              variant="outlined"
              density="compact"
              hide-details
              class="mb-3"
            />
            <v-select
              v-model="limit"
              :items="LIMIT_OPTIONS"
              :label="mt('monitoring.widgets.maxRecords', 'Maks. kayıt')"
              variant="outlined"
              density="compact"
              hide-details
              class="mb-3"
            />
          </template>
          <v-select
            v-model="refreshIntervalSeconds"
            :items="REFRESH_OPTIONS.map((o) => ({ value: o.value, title: mt(o.labelKey, o.value === 0 ? 'Kapalı' : `${o.value}s`) }))"
            item-title="title"
            item-value="value"
            :label="mt('monitoring.widgets.refreshRate', 'Yenileme')"
            variant="outlined"
            density="compact"
            hide-details
          />
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" size="small" @click="settingsOpen = false">
            {{ mt('monitoring.common.cancel', 'İptal') }}
          </v-btn>
          <v-btn color="primary" variant="flat" size="small" @click="applySettings">
            {{ mt('monitoring.common.save', 'Uygula') }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-menu>
  </div>
</template>

<style scoped>
.widget-with-settings {
  min-height: 80px;
}
.widget-settings-btn {
  position: absolute;
  top: 8px;
  right: 8px;
  z-index: 2;
}
</style>
