<script setup lang="ts">
import { computed, ref, watch, onMounted } from 'vue';
import OcDashboardGrid from '@/components/apps/operation-core/dashboards/OcDashboardGrid.vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { ocGetDashboard } from '@/services/operationCoreService';
import type {
  OcDashboard,
  OcDashboardLayoutRow,
  OcDashboardWidget,
} from '@/types/apps/operationCore';

defineOptions({ name: 'OcDashboardView' });

const props = withDefaults(
  defineProps<{
    dashboardId: string;
    /** Üstte pano açıklamasını göster (sayfa görünümü için). Inline kullanımda kapatılabilir. */
    showDescription?: boolean;
  }>(),
  { showDescription: true }
);

const emit = defineEmits<{
  loaded: [dashboard: OcDashboard];
  error: [message: string];
}>();

const { t } = useAppI18n();

const dashboard = ref<OcDashboard | null>(null);
const loading = ref(false);
const error = ref<string | null>(null);

const widgetMap = computed<Record<string, OcDashboardWidget>>(() => {
  const map: Record<string, OcDashboardWidget> = {};
  for (const w of dashboard.value?.widgets ?? []) map[w.key] = w;
  return map;
});

// Layout varsa onu kullan; yoksa tip bazlı varsayılan grid (özet kartları dar, list/chart geniş).
const rows = computed<OcDashboardLayoutRow[]>(() => {
  const layoutRows = dashboard.value?.layout?.rows;
  if (layoutRows && layoutRows.length) return layoutRows;

  const widgets = dashboard.value?.widgets ?? [];
  if (!widgets.length) return [];

  return [
    {
      cols: widgets.map((w) => {
        const isSummary = (w.widgetType || '').toLowerCase() === 'summarycard';
        return {
          widgetId: w.key,
          span: 12,
          spanMd: isSummary ? 4 : 12,
          spanLg: isSummary ? 3 : 6,
        };
      }),
    },
  ];
});

async function load() {
  if (!props.dashboardId) {
    dashboard.value = null;
    return;
  }
  loading.value = true;
  error.value = null;
  try {
    dashboard.value = await ocGetDashboard(props.dashboardId);
    if (dashboard.value) emit('loaded', dashboard.value);
  } catch (e) {
    const msg = e instanceof Error ? e.message : String(e);
    error.value = msg;
    dashboard.value = null;
    emit('error', msg);
  } finally {
    loading.value = false;
  }
}

watch(() => props.dashboardId, () => load());
onMounted(load);

defineExpose({ reload: load, dashboard });
</script>

<template>
  <div class="oc-dashboard-view">
    <v-alert v-if="error" type="error" variant="tonal" class="mb-4" closable @click:close="error = null">
      {{ error }}
    </v-alert>

    <div v-if="loading" class="d-flex justify-center py-12">
      <v-progress-circular indeterminate color="primary" size="40" />
    </div>

    <template v-else-if="dashboard">
      <p
        v-if="showDescription && dashboard.description"
        class="text-body-2 text-medium-emphasis mb-4"
      >
        {{ dashboard.description }}
      </p>

      <div v-if="!dashboard.widgets.length" class="text-center pa-12 text-medium-emphasis">
        <v-icon icon="mdi-view-dashboard-outline" size="56" class="mb-3 opacity-60" />
        <p class="text-body-1 mb-0">{{ t('operationCore.dashboards.noWidgets') }}</p>
      </div>

      <OcDashboardGrid
        v-else
        :rows="rows"
        :widget-map="widgetMap"
        :catalogs="dashboard.catalogs"
        :people="dashboard.people"
        :groups="dashboard.groups"
      />
    </template>
  </div>
</template>
