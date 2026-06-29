<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { usePanelErrorNotify } from '@/composables/useApiErrorNotify';
import {

  ocListDashboardsForWorkspace,
  ocSetBoardDefaultDashboard,
} from '@/services/operationCoreService';
import type { OpBoard } from '@/types/apps/operationCore';

const props = withDefaults(
  defineProps<{
    workspaceId: string;
    board: OpBoard | null;
    dashboardName?: string | null;
    density?: 'default' | 'compact';
    showClear?: boolean;
  }>(),
  { density: 'default', showClear: true }
);

const emit = defineEmits<{
  assigned: [dashboardId: string | null];
}>();

const { t } = useAppI18n();
const panelError = usePanelErrorNotify('errors.dg.generic');

const menuOpen = ref(false);
const loading = ref(false);
const saving = ref(false);
const errorLocal = ref<string | null>(null);
const dashboardItems = ref<{ value: string; title: string }[]>([]);
const selectedId = ref('');

const currentDashboardId = computed(() => props.board?.defaultDashboardId ?? null);
const hasDashboard = computed(() => !!currentDashboardId.value);

const displayLabel = computed(() => {
  if (!hasDashboard.value) return t('operationCore.board.dashboardLink.none');
  return props.dashboardName?.trim() || currentDashboardId.value || '—';
});

async function loadDashboards() {
  if (!props.workspaceId) return;
  loading.value = true;
  errorLocal.value = null;
  try {
    const rows = await ocListDashboardsForWorkspace(props.workspaceId);
    dashboardItems.value = rows.map((d) => ({ value: d.id, title: d.name }));
  } catch (e: unknown) {
    errorLocal.value = panelError(e, 'operationCore.board.dashboardLink.loadError');
  } finally {
    loading.value = false;
  }
}

watch(menuOpen, (open) => {
  if (open) {
    selectedId.value = currentDashboardId.value ?? '';
    void loadDashboards();
  }
});

async function saveSelection(clear = false) {
  if (!props.board?.__dataId) return;
  saving.value = true;
  errorLocal.value = null;
  try {
    const dashId = clear ? null : selectedId.value?.trim() || null;
    await ocSetBoardDefaultDashboard(props.board, dashId);
    menuOpen.value = false;
    emit('assigned', dashId);
  } catch (e: unknown) {
    errorLocal.value = panelError(e, 'operationCore.board.dashboardLink.saveError');
  } finally {
    saving.value = false;
  }
}
</script>

<template>
  <div class="d-inline-flex align-center flex-wrap ga-2">
    <v-chip
      v-if="hasDashboard"
      size="small"
      variant="tonal"
      color="primary"
      prepend-icon="mdi-view-dashboard-outline"
      class="text-none"
    >
      {{ displayLabel }}
    </v-chip>
    <span v-else class="text-caption text-medium-emphasis">
      {{ displayLabel }}
    </span>

    <v-menu v-model="menuOpen" :close-on-content-click="false" location="bottom start" min-width="320">
      <template #activator="{ props: menuProps }">
        <v-btn
          v-bind="menuProps"
          :size="density === 'compact' ? 'small' : 'small'"
          :variant="hasDashboard ? 'tonal' : 'flat'"
          :color="hasDashboard ? 'primary' : 'secondary'"
          class="text-none"
          :prepend-icon="hasDashboard ? 'mdi-swap-horizontal' : 'mdi-view-dashboard-plus-outline'"
        >
          {{
            hasDashboard
              ? t('operationCore.board.dashboardLink.change')
              : t('operationCore.board.dashboardLink.assign')
          }}
        </v-btn>
      </template>
      <v-card min-width="300" rounded="lg">
        <v-card-title class="text-subtitle-2 py-3">
          {{ t('operationCore.board.dashboardLink.menuTitle') }}
        </v-card-title>
        <v-divider />
        <v-card-text class="pt-4">
          <v-alert v-if="errorLocal" type="error" variant="tonal" density="compact" class="mb-3">
            {{ errorLocal }}
          </v-alert>
          <div v-if="loading" class="d-flex justify-center py-4">
            <v-progress-circular indeterminate size="28" color="primary" />
          </div>
          <template v-else>
            <v-select
              v-model="selectedId"
              :items="dashboardItems"
              item-title="title"
              item-value="value"
              :label="t('operationCore.workspaceDefinitions.boards.fieldDefaultDashboard')"
              variant="outlined"
              density="compact"
              hide-details
              clearable
              :no-data-text="t('operationCore.workspaceDefinitions.boards.dashboardNoData')"
            />
          </template>
        </v-card-text>
        <v-divider />
        <v-card-actions class="px-4 py-3">
          <v-btn
            v-if="showClear && hasDashboard"
            variant="text"
            size="small"
            class="text-none"
            :disabled="saving"
            @click="saveSelection(true)"
          >
            {{ t('operationCore.board.dashboardLink.clear') }}
          </v-btn>
          <v-spacer />
          <v-btn variant="text" size="small" class="text-none" @click="menuOpen = false">
            {{ t('operationCore.dashboards.editor.cancel') }}
          </v-btn>
          <v-btn
            color="primary"
            variant="flat"
            size="small"
            class="text-none"
            :loading="saving"
            :disabled="!selectedId"
            @click="saveSelection(false)"
          >
            {{ t('operationCore.dashboards.editor.save') }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-menu>
  </div>
</template>
