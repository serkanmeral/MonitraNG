<script setup lang="ts">
import { computed, ref, onMounted, watch } from 'vue';
import { useRoute } from 'vue-router';
import BaseBreadcrumb from '@/components/shared/BaseBreadcrumb.vue';
import OcDashboardGrid from '@/components/apps/operation-core/dashboards/OcDashboardGrid.vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { useOperationCoreBreadcrumbs } from '@/composables/useOperationCoreBreadcrumbs';
import { useOperationCoreStore } from '@/stores/apps/operationCore';
import { ocGetDashboard } from '@/services/operationCoreService';
import type {
  OcDashboard,
  OcDashboardLayoutRow,
  OcDashboardWidget,
} from '@/types/apps/operationCore';

definePageMeta({ layout: 'default' });

const { t } = useAppI18n();
const route = useRoute();
const store = useOperationCoreStore();

const dashboardId = computed(() => String(route.params.id ?? ''));
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

const workspaceName = computed(() => {
  const wid = dashboard.value?.workspaceId;
  if (!wid) return null;
  return store.workspaces.find((w) => w.__dataId === wid)?.name ?? null;
});

const { breadcrumbs } = useOperationCoreBreadcrumbs({
  workspace: computed(() =>
    dashboard.value?.workspaceId
      ? { id: dashboard.value.workspaceId, name: workspaceName.value ?? '' }
      : null
  ),
  tail: computed(() =>
    dashboard.value ? { text: dashboard.value.name || t('operationCore.dashboards.title') } : null
  ),
});

async function load() {
  if (!dashboardId.value) return;
  loading.value = true;
  error.value = null;
  try {
    if (!store.workspaces.length) {
      await store.loadWorkspaces().catch(() => {});
    }
    dashboard.value = await ocGetDashboard(dashboardId.value);
  } catch (e) {
    error.value = e instanceof Error ? e.message : String(e);
    dashboard.value = null;
  } finally {
    loading.value = false;
  }
}

watch(dashboardId, () => load());
onMounted(load);
</script>

<template>
  <div class="oc-flow oc-dashboard-page">
    <BaseBreadcrumb
      :title="dashboard?.name || t('operationCore.dashboards.title')"
      :breadcrumbs="breadcrumbs"
    />

    <v-alert v-if="error" type="error" variant="tonal" class="mb-4" closable @click:close="error = null">
      {{ error }}
    </v-alert>

    <div v-if="loading" class="d-flex justify-center py-12">
      <v-progress-circular indeterminate color="primary" size="40" />
    </div>

    <template v-else-if="dashboard">
      <p
        v-if="dashboard.description"
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
