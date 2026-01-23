<script setup lang="ts">
import { ref, computed, onMounted, watch, inject, onUnmounted } from 'vue';
import { useWidgetStore, type Widget } from '@/stores/apps/widget';
import { useAuthStore } from '@/stores/auth';
import { fetchWidgetData, type WidgetDataResponse } from '@/services/widgetDataService';
import StatCard from './card/StatCard.vue';
import TableWidget from './table/TableWidget.vue';
import BannerWidget from './banner/BannerWidget.vue';
import ChartWidget from './chart/ChartWidget.vue';

const props = defineProps<{
  widgetId?: string;
  widget?: Widget | null;
  t?: (key: string) => string;
}>();

const widgetStore = useWidgetStore();
const authStore = useAuthStore();
const widget = ref<Widget | null>(null);
const widgetData = ref<WidgetDataResponse | null>(null);
const loading = ref(false);
const error = ref<string | null>(null);

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
const refreshIntervalMs = computed(() => injectedRefreshInterval.value);
let refreshTimer: ReturnType<typeof setInterval> | null = null;

// Load widget data only (for refresh)
async function refreshWidgetData() {
  if (!widget.value) return;
  
  // Only refresh data if dataSource is configured
  if (widget.value.dataSource && widget.value.dataSource.type === 'data') {
    try {
      const data = await fetchWidgetData(widget.value);
      widgetData.value = data;
      error.value = null;
    } catch (dataError: any) {
      // Only log in development mode
      if (import.meta.env.DEV) {
        console.error('Widget data refresh error:', dataError);
      }
      // Don't set error on refresh failure, just log it
    }
  }
}

async function loadWidget() {
  // If widget is provided directly, use it
  if (props.widget) {
    loading.value = true;
    error.value = null;
    widget.value = props.widget;
    
    // Load widget data if dataSource is configured
    if (props.widget.dataSource && props.widget.dataSource.type === 'data') {
      try {
        const data = await fetchWidgetData(props.widget);
        widgetData.value = data;
      } catch (dataError: any) {
        // Only log in development mode
        if (import.meta.env.DEV) {
          console.error('Widget data fetch error:', dataError);
        }
        error.value = props.t?.('widgets.renderer.dataError') ?? `Veri çekilemedi: ${dataError.message}`;
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
    widget.value = loadedWidget;

    // Load widget data if dataSource is configured
    if (loadedWidget.dataSource && loadedWidget.dataSource.type === 'data') {
      try {
        const data = await fetchWidgetData(loadedWidget);
        widgetData.value = data;
      } catch (dataError: any) {
        // Only log in development mode
        if (import.meta.env.DEV) {
          console.error('Widget data fetch error:', dataError);
        }
        error.value = props.t?.('widgets.renderer.dataError') ?? `Veri çekilemedi: ${dataError.message}`;
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

// Setup refresh interval - watch both interval and widget
watch(
  [refreshIntervalMs, () => widget.value],
  ([intervalMs, currentWidget]) => {
    // Clear existing timer
    if (refreshTimer) {
      clearInterval(refreshTimer);
      refreshTimer = null;
    }
    
    // Setup new timer if interval is valid and widget is loaded
    if (intervalMs && intervalMs > 0 && currentWidget) {
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

// Component selection based on widget type
const widgetComponent = computed(() => {
  if (!widget.value) return null;

  switch (widget.value.type) {
    case 'card':
      return StatCard; // TODO: Add other card types
    case 'chart':
      return ChartWidget;
    case 'table':
      return TableWidget;
    case 'banner':
      return BannerWidget;
    default:
      return null;
  }
});
</script>

<template>
  <div class="widget-renderer">
    <!-- Loading state -->
    <div v-if="loading" class="d-flex justify-center align-center pa-4">
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
    <component
      v-else-if="widgetComponent && hasPermission"
      :is="widgetComponent"
      :widget="widget"
      :data="widgetData"
      :t="t"
    />

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
