<script setup lang="ts">
import { computed, ref } from 'vue';
import { useRoute } from 'vue-router';
import BaseBreadcrumb from '@/components/shared/BaseBreadcrumb.vue';
import OcDashboardView from '@/components/apps/operation-core/dashboards/OcDashboardView.vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { useOperationCoreBreadcrumbs } from '@/composables/useOperationCoreBreadcrumbs';
import { useOperationCoreStore } from '@/stores/apps/operationCore';
import type { OcDashboard } from '@/types/apps/operationCore';

definePageMeta({ layout: 'default' });

const { t } = useAppI18n();
const route = useRoute();
const store = useOperationCoreStore();

const dashboardId = computed(() => String(route.params.id ?? ''));
const dashboard = ref<OcDashboard | null>(null);

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

function onLoaded(d: OcDashboard) {
  dashboard.value = d;
  if (!store.workspaces.length) {
    store.loadWorkspaces().catch(() => {});
  }
}
</script>

<template>
  <div class="oc-flow oc-dashboard-page">
    <BaseBreadcrumb
      :title="dashboard?.name || t('operationCore.dashboards.title')"
      :breadcrumbs="breadcrumbs"
    />

    <OcDashboardView :dashboard-id="dashboardId" @loaded="onLoaded" />
  </div>
</template>
