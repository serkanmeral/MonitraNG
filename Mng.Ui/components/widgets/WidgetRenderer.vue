<script setup lang="ts">
import { ref, computed, onMounted, watch } from 'vue';
import { useWidgetStore, type Widget } from '@/stores/apps/widget';
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
const widget = ref<Widget | null>(null);
const widgetData = ref<WidgetDataResponse | null>(null);
const loading = ref(false);
const error = ref<string | null>(null);

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
        console.error('Widget data fetch error:', dataError);
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
        console.error('Widget data fetch error:', dataError);
        error.value = props.t?.('widgets.renderer.dataError') ?? `Veri çekilemedi: ${dataError.message}`;
      }
    }
  } catch (e: any) {
    error.value = props.t?.('widgets.renderer.loadError') ?? `Widget yüklenemedi: ${e.message}`;
    widget.value = null;
  } finally {
    loading.value = false;
  }
}

onMounted(loadWidget);
watch(() => props.widgetId, loadWidget);
watch(() => props.widget, loadWidget, { deep: true });

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

    <!-- Widget component -->
    <component
      v-else-if="widgetComponent"
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
