<script setup lang="ts">
import { ref, computed, onMounted, watch, provide } from 'vue';
import BaseBreadcrumb from '@/components/shared/BaseBreadcrumb.vue';
import OcWorkspaceDefinitionsGeneralTab from '@/components/apps/operation-core/workspace-definitions/OcWorkspaceDefinitionsGeneralTab.vue';
import OcWorkspaceDefinitionsValuesTab from '@/components/apps/operation-core/workspace-definitions/OcWorkspaceDefinitionsValuesTab.vue';
import OcWorkspaceDefinitionsTagsTab from '@/components/apps/operation-core/workspace-definitions/OcWorkspaceDefinitionsTagsTab.vue';
import OcWorkspaceDefinitionsFlowsTab from '@/components/apps/operation-core/workspace-definitions/OcWorkspaceDefinitionsFlowsTab.vue';
import OcWorkspaceDefinitionsFormsTab from '@/components/apps/operation-core/workspace-definitions/OcWorkspaceDefinitionsFormsTab.vue';
import OcWorkspaceDefinitionsBoardsTab from '@/components/apps/operation-core/workspace-definitions/OcWorkspaceDefinitionsBoardsTab.vue';
import OcWorkspaceDefinitionsDashboardsTab from '@/components/apps/operation-core/workspace-definitions/OcWorkspaceDefinitionsDashboardsTab.vue';
import OcWorkspaceDefinitionsPoliciesTab from '@/components/apps/operation-core/workspace-definitions/OcWorkspaceDefinitionsPoliciesTab.vue';
import OcWorkspaceDefinitionsRulesTab from '@/components/apps/operation-core/workspace-definitions/OcWorkspaceDefinitionsRulesTab.vue';
import OcWorkspaceDefinitionsScheduledWorkItemsTab from '@/components/apps/operation-core/workspace-definitions/OcWorkspaceDefinitionsScheduledWorkItemsTab.vue';
import OcWorkspaceDefinitionsAutomationsTab from '@/components/apps/operation-core/workspace-definitions/OcWorkspaceDefinitionsAutomationsTab.vue';
import OcWorkspaceDefinitionsSlaTab from '@/components/apps/operation-core/workspace-definitions/OcWorkspaceDefinitionsSlaTab.vue';
import OcWorkspaceDefinitionsMailTab from '@/components/apps/operation-core/workspace-definitions/OcWorkspaceDefinitionsMailTab.vue';
import OcWorkspaceCreateDialog from '@/components/apps/operation-core/workspace-definitions/OcWorkspaceCreateDialog.vue';
import { useOperationCoreBreadcrumbs } from '@/composables/useOperationCoreBreadcrumbs';
import {
  OC_WORKSPACE_DEFINITION_TAB_KEYS,
  type OcWorkspaceDefinitionTabKey,
  useOcWorkspaceDefinitionTabs,
} from '@/composables/useOcWorkspaceDefinitionTabs';
import { useDisplay } from 'vuetify';
import { useAppI18n } from '@/composables/useAppI18n';
import { usePanelErrorNotify } from '@/composables/useApiErrorNotify';
import { useAuthStore } from '@/stores/auth';
import { ocListWorkspaces, ocReloadWorkspaceMetadataCache } from '@/services/operationCoreService';
import {
  OC_WORKSPACE_CATALOG_KEY,
  useOcWorkspaceCatalog,
} from '@/composables/useOcWorkspaceCatalog';
import type { OpWorkspace } from '@/types/apps/operationCore';

definePageMeta({ layout: 'default' });

const { t } = useAppI18n();
const panelError = usePanelErrorNotify('errors.dg.generic');
const { mdAndUp } = useDisplay();
const route = useRoute();
const router = useRouter();
const auth = useAuthStore();

const activeTab = ref<OcWorkspaceDefinitionTabKey>('general');
const { tabModel } = useOcWorkspaceDefinitionTabs(route, router, activeTab);

const workspaces = ref<OpWorkspace[]>([]);
const loadingWorkspaces = ref(true);
const selectedWorkspaceId = ref('');
const createDialogOpen = ref(false);
const reloadingCache = ref(false);
const cacheReloadMessage = ref<string | null>(null);
const cacheReloadError = ref<string | null>(null);

const workspaceCatalog = useOcWorkspaceCatalog(selectedWorkspaceId);
provide(OC_WORKSPACE_CATALOG_KEY, workspaceCatalog);

const { breadcrumbs } = useOperationCoreBreadcrumbs({
  tail: computed(() => ({
    text: t('operationCore.definitions.workspaceMenuTitle'),
    disabled: true,
  })),
});

const workspaceItems = computed(() =>
  workspaces.value.map((w) => ({
    title: w.name,
    value: w.__dataId,
    subtitle: w.workItemKeyPrefix ? `${w.workItemKeyPrefix} · ${w.workspaceType ?? ''}` : w.workspaceType,
  }))
);

const TAB_ICONS: Record<OcWorkspaceDefinitionTabKey, string> = {
  general: 'mdi-cog-outline',
  values: 'mdi-tune-variant',
  tags: 'mdi-tag-multiple-outline',
  flows: 'mdi-transit-connection-variant',
  forms: 'mdi-form-select',
  boards: 'mdi-view-column-outline',
  dashboards: 'mdi-view-dashboard-outline',
  policies: 'mdi-shield-account-outline',
  rules: 'mdi-format-list-checks',
  scheduled: 'mdi-calendar-clock',
  automations: 'mdi-lightning-bolt',
  sla: 'mdi-clock-check-outline',
  mail: 'mdi-bell-outline',
};

const tabItems = computed(() =>
  OC_WORKSPACE_DEFINITION_TAB_KEYS.map((key) => ({
    key,
    label: t(`operationCore.workspaceDefinitions.tabs.${key}`),
    icon: TAB_ICONS[key],
  }))
);

function syncWorkspaceFromRoute() {
  const q = route.query.workspaceId;
  if (typeof q === 'string' && q) {
    selectedWorkspaceId.value = q;
  }
}

function setWorkspaceId(id: string) {
  selectedWorkspaceId.value = id;
  router.replace({
    path: route.path,
    query: { ...route.query, workspaceId: id || undefined },
  });
}

async function loadWorkspaces() {
  loadingWorkspaces.value = true;
  try {
    workspaces.value = await ocListWorkspaces();
    syncWorkspaceFromRoute();
    if (!selectedWorkspaceId.value && workspaces.value.length > 0) {
      setWorkspaceId(workspaces.value[0]!.__dataId);
    }
  } finally {
    loadingWorkspaces.value = false;
  }
}

async function onWorkspaceCreated(id: string) {
  await loadWorkspaces();
  setWorkspaceId(id);
  activeTab.value = 'general';
}

async function reloadMetadataCache() {
  const wsId = selectedWorkspaceId.value?.trim();
  if (!wsId) return;

  reloadingCache.value = true;
  cacheReloadMessage.value = null;
  cacheReloadError.value = null;
  try {
    const result = await ocReloadWorkspaceMetadataCache(wsId);
    cacheReloadMessage.value = t('operationCore.workspaceDefinitions.metadataCacheReloadSuccess', {
      count: result.keysRemoved,
    });
  } catch (e: unknown) {
    cacheReloadError.value = panelError(e, 'operationCore.workspaceDefinitions.metadataCacheReloadError');
  } finally {
    reloadingCache.value = false;
  }
}

watch(
  () => route.query.workspaceId,
  () => syncWorkspaceFromRoute()
);

onMounted(() => {
  if (!auth.isManager) {
    void navigateTo('/unauthorized');
    return;
  }
  void loadWorkspaces();
});
</script>

<template>
  <div class="oc-flow oc-workspace-definitions-page">
    <BaseBreadcrumb
      :title="t('operationCore.definitions.workspaceMenuTitle')"
      :breadcrumbs="breadcrumbs"
    />

    <div class="oc-hero mb-4 pa-4 pa-md-5 rounded-lg">
      <h1 class="text-h5 font-weight-bold mb-2">
        {{ t('operationCore.definitions.workspaceMenuTitle') }}
      </h1>
      <p class="text-body-2 text-medium-emphasis mb-0">
        {{ t('operationCore.workspaceDefinitions.pageSubtitle') }}
      </p>
    </div>

    <v-card variant="outlined" rounded="lg" class="mb-4 pa-4 pa-md-5">
      <div class="d-flex align-start ga-3 flex-wrap">
        <v-select
          v-model="selectedWorkspaceId"
          :items="workspaceItems"
          item-title="title"
          item-value="value"
          :label="t('operationCore.workspaceDefinitions.selectWorkspace')"
          :hint="t('operationCore.workspaceDefinitions.selectWorkspaceHint')"
          persistent-hint
          :loading="loadingWorkspaces"
          density="comfortable"
          class="flex-grow-1"
          style="min-width: 240px"
          @update:model-value="setWorkspaceId"
        >
          <template #item="{ props: itemProps, item }">
            <v-list-item v-bind="itemProps" :subtitle="item.raw.subtitle" />
          </template>
        </v-select>
        <v-btn
          color="primary"
          variant="tonal"
          rounded="lg"
          class="text-none mt-1"
          prepend-icon="mdi-folder-plus-outline"
          @click="createDialogOpen = true"
        >
          {{ t('operationCore.workspaceDefinitions.newWorkspace') }}
        </v-btn>
        <v-btn
          v-if="selectedWorkspaceId"
          variant="outlined"
          rounded="lg"
          class="text-none mt-1"
          prepend-icon="mdi-cached"
          :loading="reloadingCache"
          :disabled="!selectedWorkspaceId || reloadingCache"
          @click="reloadMetadataCache"
        >
          {{ t('operationCore.workspaceDefinitions.reloadMetadataCache') }}
        </v-btn>
      </div>
      <p v-if="selectedWorkspaceId" class="text-caption text-medium-emphasis mb-0 mt-3">
        {{ t('operationCore.workspaceDefinitions.metadataCacheReloadHint') }}
      </p>
    </v-card>

    <v-alert
      v-if="cacheReloadMessage"
      type="success"
      variant="tonal"
      class="mb-4 rounded-lg"
      closable
      @click:close="cacheReloadMessage = null"
    >
      {{ cacheReloadMessage }}
    </v-alert>

    <v-alert
      v-if="cacheReloadError"
      type="error"
      variant="tonal"
      class="mb-4 rounded-lg"
      closable
      @click:close="cacheReloadError = null"
    >
      {{ cacheReloadError }}
    </v-alert>

    <v-alert
      v-if="!loadingWorkspaces && workspaces.length === 0"
      type="warning"
      variant="tonal"
      class="mb-4 d-flex align-center"
    >
      <div class="d-flex align-center justify-space-between ga-3 flex-wrap w-100">
        <span>{{ t('operationCore.workspaceDefinitions.noWorkspaces') }}</span>
        <v-btn
          color="primary"
          variant="flat"
          rounded="lg"
          class="text-none"
          prepend-icon="mdi-folder-plus-outline"
          @click="createDialogOpen = true"
        >
          {{ t('operationCore.workspaceDefinitions.newWorkspace') }}
        </v-btn>
      </div>
    </v-alert>

    <v-card v-else-if="selectedWorkspaceId" variant="outlined" class="rounded-lg">
      <div class="d-flex flex-column flex-md-row">
        <v-tabs
          v-model="tabModel"
          :direction="mdAndUp ? 'vertical' : 'horizontal'"
          color="primary"
          class="oc-def-side-tabs flex-shrink-0 py-2"
          show-arrows
        >
          <v-tab
            v-for="tab in tabItems"
            :key="tab.key"
            :value="tab.key"
            class="text-none justify-start"
          >
            <v-icon :icon="tab.icon" start size="20" />
            {{ tab.label }}
          </v-tab>
        </v-tabs>
        <v-divider :vertical="mdAndUp" />
        <v-tabs-window v-model="tabModel" class="flex-grow-1" style="min-width: 0">
          <v-tabs-window-item
            v-for="tab in tabItems"
            :key="tab.key"
            :value="tab.key"
          >
          <OcWorkspaceDefinitionsGeneralTab
            v-if="tab.key === 'general'"
            :workspace-id="selectedWorkspaceId"
          />
          <OcWorkspaceDefinitionsValuesTab
            v-else-if="tab.key === 'values'"
            :workspace-id="selectedWorkspaceId"
          />
          <OcWorkspaceDefinitionsTagsTab
            v-else-if="tab.key === 'tags'"
            :workspace-id="selectedWorkspaceId"
          />
          <OcWorkspaceDefinitionsFlowsTab
            v-else-if="tab.key === 'flows'"
            :workspace-id="selectedWorkspaceId"
          />
          <OcWorkspaceDefinitionsFormsTab
            v-else-if="tab.key === 'forms'"
            :workspace-id="selectedWorkspaceId"
          />
          <OcWorkspaceDefinitionsBoardsTab
            v-else-if="tab.key === 'boards'"
            :workspace-id="selectedWorkspaceId"
          />
          <OcWorkspaceDefinitionsDashboardsTab
            v-else-if="tab.key === 'dashboards'"
            :workspace-id="selectedWorkspaceId"
          />
          <OcWorkspaceDefinitionsPoliciesTab
            v-else-if="tab.key === 'policies'"
            :workspace-id="selectedWorkspaceId"
          />
          <OcWorkspaceDefinitionsRulesTab
            v-else-if="tab.key === 'rules'"
            :workspace-id="selectedWorkspaceId"
          />
          <OcWorkspaceDefinitionsScheduledWorkItemsTab
            v-else-if="tab.key === 'scheduled'"
            :workspace-id="selectedWorkspaceId"
          />
          <OcWorkspaceDefinitionsAutomationsTab
            v-else-if="tab.key === 'automations'"
            :workspace-id="selectedWorkspaceId"
          />
          <OcWorkspaceDefinitionsSlaTab
            v-else-if="tab.key === 'sla'"
            :workspace-id="selectedWorkspaceId"
          />
          <OcWorkspaceDefinitionsMailTab
            v-else-if="tab.key === 'mail'"
            :workspace-id="selectedWorkspaceId"
          />
          </v-tabs-window-item>
        </v-tabs-window>
      </div>
    </v-card>

    <v-alert v-else-if="!loadingWorkspaces" type="info" variant="tonal">
      {{ t('operationCore.workspaceDefinitions.pickWorkspace') }}
    </v-alert>

    <OcWorkspaceCreateDialog v-model="createDialogOpen" @created="onWorkspaceCreated" />
  </div>
</template>

<style scoped>
/* Dikey (sol) sekme yerleşimi — md ve üstü. Dar ekranda v-tabs yataya döner. */
.oc-def-side-tabs {
  min-width: 232px;
  max-width: 260px;
}

@media (max-width: 959px) {
  .oc-def-side-tabs {
    min-width: 0;
    max-width: none;
  }
}

.oc-def-side-tabs :deep(.v-tab) {
  justify-content: flex-start;
  text-align: left;
}
</style>
