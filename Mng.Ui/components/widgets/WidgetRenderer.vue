<script setup lang="ts">
import { ref, computed, onMounted, watch, inject, onUnmounted, type Ref } from 'vue';
import { useWidgetStore, type Widget } from '@/stores/apps/widget';
import { useAuthStore } from '@/stores/auth';
import { fetchWidgetData, type WidgetDataResponse } from '@/services/widgetDataService';
import { resolveWidgetBatchDataId } from '@/services/widgetBatchDataService';
import { adaptWidgetForRuntime, shouldFetchWidgetData, type WidgetLike } from '@/utils/widgets/widgetManifestAdapter';
import { useWidgetSurfaceContext } from '@/composables/useWidgetSurfaceContext';
import {
  DASHBOARD_SURFACE_CONTEXT_KEY,
  DASHBOARD_WIDGET_BATCH_MODE_KEY,
  DASHBOARD_WIDGET_DATA_KEY,
} from '@/utils/widgets/dashboardSurfaceKeys';
import { DASHBOARD_SURFACE_MUTATIONS_KEY } from '@/utils/widgets/dashboardSurfaceMutations';
import {
  applyCrossFilterToVariables,
  getWidgetInteractions,
  isChartZoomEnabled,
  resolveDrillDownConfig,
  type WidgetActionConfig,
} from '@/utils/widgets/surfaceInteractions';
import { useWidgetDrillDown } from '@/composables/useWidgetDrillDown';
import { executeWidgetAction } from '@/utils/widgets/widgetActionExecutor';
import type { SurfaceContext } from '@/types/apps/widgetManifest';
import StatCard from './card/StatCard.vue';
import TableWidget from './table/TableWidget.vue';
import ListActivityWidget from './list/ListActivityWidget.vue';
import BannerWidget from './banner/BannerWidget.vue';
import ChartWidget from './chart/ChartWidget.vue';
import MapWidget from './map/MapWidget.vue';
import GaugeWidget from './gauge/GaugeWidget.vue';
import SiemScenarioCardsWidget from './siem/SiemScenarioCardsWidget.vue';
import WidgetActionBar from './WidgetActionBar.vue';

const props = defineProps<{
  widgetId?: string;
  widget?: Widget | null;
  /** Dashboard'daki widget örneği için override (timeRangeMinutes, limit, refreshIntervalSeconds) */
  configOverrides?: Record<string, any>;
  /** Manifest parametre çözümleme — timeRange, variables */
  surfaceContext?: import('@/types/apps/widgetManifest').SurfaceContext;
  t?: (key: string) => string;
}>();

const widgetStore = useWidgetStore();
const authStore = useAuthStore();

const injectedSurface = inject<Ref<SurfaceContext> | null>(DASHBOARD_SURFACE_CONTEXT_KEY, null);
const surfaceMutations = inject(DASHBOARD_SURFACE_MUTATIONS_KEY, null);

const actionLoadingId = ref<string | null>(null);
const selectedRow = ref<Record<string, unknown> | null>(null);
const batchDataMap = inject<Ref<Map<string, WidgetDataResponse>> | null>(DASHBOARD_WIDGET_DATA_KEY, null);
const batchMode = inject(DASHBOARD_WIDGET_BATCH_MODE_KEY, false);

const mergedSurfaceInput = computed(() => {
  const injected = injectedSurface?.value;
  const prop = props.surfaceContext;
  if (!injected && !prop) return undefined;
  return {
    locale: prop?.locale ?? injected?.locale,
    timeRange: { ...injected?.timeRange, ...prop?.timeRange },
    variables: { ...injected?.variables, ...prop?.variables },
  } satisfies SurfaceContext;
});

const resolvedSurfaceContext = useWidgetSurfaceContext(mergedSurfaceInput);
const { navigateDrillDown } = useWidgetDrillDown(() => resolvedSurfaceContext.value);
const widget = ref<WidgetLike | null>(null);
const widgetData = ref<WidgetDataResponse | null>(null);
const loading = ref(false);
const error = ref<string | null>(null);

// Config overrides ile birleştirilmiş widget (veri çekme ve refresh için)
const effectiveWidget = computed(() => {
  const w = widget.value;
  if (!w || !props.configOverrides || Object.keys(props.configOverrides).length === 0) return w;
  return {
    ...w,
    config: { ...(w.config || {}), ...props.configOverrides },
  };
});

// Check if user has permission to view this widget
const hasPermission = computed(() => {
  if (!widget.value) return false;
  
  // Admin kullanıcılar tüm widget'ları görebilir
  if (authStore.isAdmin) {
    return true;
  }
  
  // Eğer permissions tanımlı değilse, herkes görebilir
  if (!widget.value.permissions || !widget.value.permissions.groups || widget.value.permissions.groups.length === 0) {
    return true;
  }
  
  // Kullanıcının gruplarından biri widget'ın izin verdiği gruplar içinde mi?
  const userGroups = authStore.userGroups || [];
  const allowedGroups = widget.value.permissions.groups || [];
  
  return userGroups.some(group => allowedGroups.includes(group));
});

// Inject refresh interval from dashboard viewer
const injectedRefreshInterval = inject<computed<number | null>>('dashboardRefreshInterval', computed(() => null));
// Widget kendi refreshIntervalSeconds tanımladıysa onu kullan, yoksa dashboard'unkini
const refreshIntervalMs = computed(() => {
  const w = effectiveWidget.value;
  const widgetSec = w?.config?.refreshIntervalSeconds;
  if (widgetSec != null && widgetSec > 0) return widgetSec * 1000;
  return injectedRefreshInterval.value;
});
let refreshTimer: ReturnType<typeof setInterval> | null = null;

function resolveWidgetDataId(): string | undefined {
  const w = widget.value;
  if (!w) return props.widgetId;
  const id = resolveWidgetBatchDataId(w, props.widgetId);
  return id || undefined;
}

function applyBatchDataIfAvailable(): boolean {
  if (!batchMode || !batchDataMap?.value) return false;
  const id = resolveWidgetDataId();
  if (!id) return false;
  const data = batchDataMap.value.get(id);
  if (!data) return false;
  widgetData.value = data;
  error.value = null;
  return true;
}

async function fetchDataForWidget(w: WidgetLike) {
  if (batchMode) {
    if (applyBatchDataIfAvailable()) return;
    if (!shouldFetchWidgetData(w)) return;
    try {
      const data = await fetchWidgetData(w, resolvedSurfaceContext.value);
      widgetData.value = data;
      error.value = null;
    } catch (dataError: unknown) {
      const message = dataError instanceof Error ? dataError.message : String(dataError);
      if (import.meta.env.DEV) {
        console.error('Widget batch fallback fetch error:', message);
      }
      error.value = props.t?.('widgets.renderer.dataError') ?? `Veri çekilemedi: ${message}`;
    }
    return;
  }
  if (!shouldFetchWidgetData(w)) return;
  const data = await fetchWidgetData(w, resolvedSurfaceContext.value);
  widgetData.value = data;
  error.value = null;
}

// Load widget data only (for refresh)
async function refreshWidgetData() {
  const w = effectiveWidget.value ?? widget.value;
  if (!w) return;

  if (batchMode) {
    if (applyBatchDataIfAvailable()) return;
    if (shouldFetchWidgetData(w)) {
      try {
        await fetchDataForWidget(w);
      } catch (dataError: unknown) {
        if (import.meta.env.DEV) {
          console.error('Widget data refresh error:', dataError);
        }
      }
    }
    return;
  }

  if (shouldFetchWidgetData(w)) {
    try {
      await fetchDataForWidget(w);
    } catch (dataError: any) {
      if (import.meta.env.DEV) {
        console.error('Widget data refresh error:', dataError);
      }
    }
  }
}

async function loadWidget() {
  // If widget is provided directly, use it
  if (props.widget) {
    loading.value = true;
    error.value = null;
    widget.value = adaptWidgetForRuntime(props.widget as WidgetLike, resolvedSurfaceContext.value);

    const w = effectiveWidget.value ?? widget.value;
    if (w && shouldFetchWidgetData(w)) {
      try {
        await fetchDataForWidget(w);
      } catch (dataError: any) {
        if (!batchMode) {
          if (import.meta.env.DEV) {
            console.error('Widget data fetch error:', dataError);
          }
          error.value = props.t?.('widgets.renderer.dataError') ?? `Veri çekilemedi: ${dataError.message}`;
        }
      }
    }
    loading.value = false;
    return;
  }

  // Otherwise, load by widgetId
  if (!props.widgetId?.trim()) {
    error.value = props.t?.('widgets.renderer.noWidgetId') ?? 'Widget ID belirtilmemiş';
    return;
  }

  loading.value = true;
  error.value = null;

  try {
    // Load widget definition
    const loadedWidget = await widgetStore.fetchWidgetById(props.widgetId);
    widget.value = adaptWidgetForRuntime(loadedWidget as WidgetLike, resolvedSurfaceContext.value);

    const w = effectiveWidget.value ?? widget.value;
    if (w && shouldFetchWidgetData(w)) {
      try {
        await fetchDataForWidget(w);
      } catch (dataError: any) {
        if (!batchMode) {
          if (import.meta.env.DEV) {
            console.error('Widget data fetch error:', dataError);
          }
          error.value = props.t?.('widgets.renderer.dataError') ?? `Veri çekilemedi: ${dataError.message}`;
        }
      }
    }
  } catch (e: any) {
    // Only log in development mode
    if (import.meta.env.DEV) {
      console.error('Widget load error:', e);
    }
    
    // Provide more user-friendly error messages
    let errorMessage = props.t?.('widgets.renderer.loadError') ?? 'Widget yüklenemedi';
    if (e?.response?.status === 503 || e?.status === 503) {
      errorMessage = props.t?.('widgets.renderer.serviceUnavailable') ?? 'Widget servisi şu anda kullanılamıyor';
    } else if (e?.response?.status === 404 || e?.status === 404) {
      errorMessage = props.t?.('widgets.renderer.notFound') ?? 'Widget bulunamadı';
    } else if (e?.message) {
      errorMessage = `${errorMessage}: ${e.message}`;
    }
    
    error.value = errorMessage;
    widget.value = null;
  } finally {
    loading.value = false;
  }
}

onMounted(loadWidget);
watch(() => props.widgetId, loadWidget);
watch(() => props.widget, loadWidget, { deep: true });

watch(
  () => batchDataMap?.value,
  () => {
    applyBatchDataIfAvailable();
  },
  { deep: true },
);

watch(
  resolvedSurfaceContext,
  async () => {
    const source = props.widget;
    if (source) {
      widget.value = adaptWidgetForRuntime(source as WidgetLike, resolvedSurfaceContext.value);
      await refreshWidgetData();
      return;
    }
    if (widget.value) {
      widget.value = adaptWidgetForRuntime(widget.value as WidgetLike, resolvedSurfaceContext.value);
      await refreshWidgetData();
    }
  },
  { deep: true },
);

// Setup refresh interval - watch both interval and widget
watch(
  [refreshIntervalMs, () => widget.value],
  ([intervalMs, currentWidget]) => {
    if (refreshTimer) {
      clearInterval(refreshTimer);
      refreshTimer = null;
    }

    if (batchMode) return;
    // Setup new timer if interval is valid and widget is loaded
    if (intervalMs && intervalMs > 0 && currentWidget && shouldFetchWidgetData(currentWidget)) {
      refreshTimer = setInterval(() => {
        refreshWidgetData();
      }, intervalMs);
    }
  },
  { immediate: true }
);

// Cleanup on unmount
onUnmounted(() => {
  if (refreshTimer) {
    clearInterval(refreshTimer);
    refreshTimer = null;
  }
});

const showLoading = computed(() => {
  if (loading.value) return true;
  if (error.value) return false;
  if (!batchMode || !widget.value) return false;
  if (!shouldFetchWidgetData(widget.value)) return false;
  return !widgetData.value;
});

const widgetComponent = computed(() => {
  if (!widget.value) return null;

  const config = (widget.value.config ?? {}) as Record<string, unknown>;
  const templateId =
    (config.templateId as string | undefined) ??
    widget.value.templateId;
  if (templateId === 'siem.scenario-cards' || config.composite === true) {
    return SiemScenarioCardsWidget;
  }

  switch (widget.value.type) {
    case 'card':
      return StatCard;
    case 'chart':
      return ChartWidget;
    case 'table': {
      const manifest = config.manifest as { presentation?: { kind?: string } } | undefined;
      if (config.variant === 'activity' || manifest?.presentation?.kind === 'list') {
        return ListActivityWidget;
      }
      return TableWidget;
    }
    case 'banner':
      return BannerWidget;
    case 'map':
      return MapWidget;
    case 'gauge':
      return GaugeWidget;
    default:
      return null;
  }
});

const widgetInteractions = computed(() => (widget.value ? getWidgetInteractions(widget.value) : null));
const widgetActions = computed(() => widgetInteractions.value?.actions ?? []);
const drillDownConfig = computed(() => resolveDrillDownConfig(widgetInteractions.value));
const statDrillEnabled = computed(() => !!drillDownConfig.value && widget.value?.type === 'card');

function handleCrossFilterRow(row: Record<string, unknown>) {
  const interaction = widgetInteractions.value?.crossFilter;
  if (!interaction || !surfaceMutations || !injectedSurface) return;
  const next = applyCrossFilterToVariables(interaction, row, injectedSurface.value?.variables);
  const value = next?.[interaction.variable];
  if (value === undefined) return;
  surfaceMutations.setCrossFilterVariable(interaction.variable, value ?? null);
}

function handleRowActivate(row: Record<string, unknown>) {
  selectedRow.value = row;
  const drill = resolveDrillDownConfig(widgetInteractions.value);
  if (drill) {
    navigateDrillDown(drill, row);
    return;
  }
  handleCrossFilterRow(row);
}

function handleStatDrillDown() {
  const drill = resolveDrillDownConfig(widgetInteractions.value);
  if (!drill) return;
  const row =
    widgetData.value?.data?.[0] && typeof widgetData.value.data[0] === 'object'
      ? (widgetData.value.data[0] as Record<string, unknown>)
      : {};
  navigateDrillDown(drill, row);
}

function handleChartZoom(fromMs: number, toMs: number) {
  if (!surfaceMutations) return;
  surfaceMutations.setTimeRangeFromZoom(new Date(fromMs).toISOString(), new Date(toMs).toISOString());
}

function handleChartSegment(row: Record<string, unknown>) {
  const drill = resolveDrillDownConfig(widgetInteractions.value);
  if (drill) {
    navigateDrillDown(drill, row);
    return;
  }
  handleCrossFilterRow(row);
}

async function handleWidgetAction(action: WidgetActionConfig, row?: Record<string, unknown> | null) {
  actionLoadingId.value = action.id;
  try {
    await executeWidgetAction(
      action,
      row ?? selectedRow.value ?? {},
      resolvedSurfaceContext.value,
      navigateDrillDown,
    );
    await refreshWidgetData();
  } catch (e: unknown) {
    if (import.meta.env.DEV) console.error('Widget action error:', e);
  } finally {
    actionLoadingId.value = null;
  }
}
</script>

<template>
  <div class="widget-renderer">
    <!-- Loading state -->
    <div v-if="showLoading" class="d-flex justify-center align-center pa-4">
      <v-progress-circular indeterminate color="primary" size="32" />
    </div>

    <!-- Error state -->
    <v-alert
      v-else-if="error"
      type="error"
      variant="tonal"
      density="compact"
      class="mb-2"
    >
      {{ error }}
    </v-alert>

    <!-- Widget not found -->
    <v-card
      v-else-if="!widget"
      variant="outlined"
      class="pa-4"
    >
      <div class="text-body-2 text-medium-emphasis text-center">
        {{ t?.('widgets.renderer.notFound') ?? 'Widget bulunamadı' }}
      </div>
    </v-card>

    <!-- Permission Check -->
    <v-card
      v-else-if="!hasPermission"
      variant="outlined"
      class="pa-4"
    >
      <div class="text-body-2 text-medium-emphasis text-center">
        <v-icon color="error" size="48" class="mb-2">mdi-shield-alert</v-icon>
        <p class="text-h6 mb-2">{{ t?.('widgets.renderer.unauthorized') ?? 'Yetkiniz Yok' }}</p>
        <p class="text-body-2">{{ t?.('widgets.renderer.unauthorizedMessage') ?? 'Bu widget\'ı görüntüleme yetkiniz bulunmamaktadır.' }}</p>
      </div>
    </v-card>

    <!-- Widget component -->
    <div v-else-if="widgetComponent && hasPermission" class="widget-renderer-body">
      <WidgetActionBar
        v-if="widgetActions.length"
        :actions="widgetActions"
        :row="selectedRow"
        :is-admin="authStore.isAdmin"
        :user-groups="authStore.userGroups"
        :loading-id="actionLoadingId"
        :t="t"
        class="px-1"
        @action="(a) => handleWidgetAction(a, selectedRow)"
      />
      <component
        :is="widgetComponent"
        :widget="effectiveWidget ?? widget"
        :data="widgetData"
        :t="t"
        :interactions="widgetInteractions"
        :chart-zoom-enabled="widget ? isChartZoomEnabled(widget, widgetInteractions) : false"
        :drill-down-enabled="statDrillEnabled"
        @cross-filter="handleCrossFilterRow"
        @row-activate="handleRowActivate"
        @drill-down="handleStatDrillDown"
        @chart-zoom="handleChartZoom"
        @chart-segment-select="handleChartSegment"
        @action="(a: WidgetActionConfig, row: Record<string, unknown>) => handleWidgetAction(a, row)"
      />
    </div>

    <!-- Widget type not supported -->
    <v-card
      v-else
      variant="outlined"
      class="pa-4"
    >
      <div class="text-body-2 text-medium-emphasis text-center">
        {{ t?.('widgets.renderer.typeNotSupported', { type: widget.type }) ?? `Widget tipi desteklenmiyor: ${widget.type}` }}
      </div>
    </v-card>
  </div>
</template>

<style scoped>
.widget-renderer {
  width: 100%;
  height: 100%;
}
</style>
