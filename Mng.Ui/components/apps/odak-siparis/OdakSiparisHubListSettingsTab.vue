<script setup lang="ts">
import { computed, ref } from 'vue';
import { storeToRefs } from 'pinia';
import { VueDraggableNext } from 'vue-draggable-next';
import OdakSiparisListColumnFormatDialog from '@/components/apps/odak-siparis/OdakSiparisListColumnFormatDialog.vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { usePanelErrorNotify } from '@/composables/useApiErrorNotify';
import { useOdakSiparisHubSettingsStore } from '@/stores/apps/odakSiparisHubSettings';
import { isActiveListColumnFormat, type AfListColumnFormat } from '@/utils/afListColumnFormat';
import type { OdakHubListColumnConfig, OdakHubListConfig } from '@/utils/odakSiparisHubListConfig';
import type { OdakHubListSettingsScope } from '@/utils/odakSiparisHubSettingsService';
import { odakPackageSettingsFormatTypeLabelTr } from '@/utils/odakSiparisSettingsLabels';

const props = defineProps({
  scope: { type: String, required: true },
  hintKey: { type: String, required: true },
  fieldLabel: { type: Function, required: true },
});

const { t } = useAppI18n();
const panelError = usePanelErrorNotify('errors.dg.generic');
const hubStore = useOdakSiparisHubSettingsStore();
const { bootstrapStatus } = storeToRefs(hubStore);

const hubScope = computed(() => props.scope as OdakHubListSettingsScope);
const errorMessage = ref('');
const successMessage = ref('');

const listConfig = computed(
  () => hubStore.scopes[hubScope.value].config as OdakHubListConfig
);

const loading = computed(
  () => bootstrapStatus.value === 'loading' || !hubStore.scopeReady(hubScope.value)
);
const saving = computed(() => hubStore.scopeSaving(hubScope.value));
const canSave = computed(() => hubStore.canSaveScope(hubScope.value));
const canEdit = computed(() => hubStore.canEditScope(hubScope.value));

const formatDialogOpen = ref(false);
const formatColumn = ref<OdakHubListColumnConfig | null>(null);

const conditionFields = computed(() =>
  listConfig.value.columns.map((c) => ({
    value: c.fieldName,
    title: columnLabel(c.fieldName),
  }))
);

function columnLabel(fieldName: string): string {
  return (props.fieldLabel as (name: string) => string)(fieldName);
}

function formatSummary(element: OdakHubListColumnConfig): string {
  if (!isActiveListColumnFormat(element.format)) {
    return t('odakSiparis.packages.settings.listColumns.noFormat');
  }
  const typeKey = element.format?.type ?? 'none';
  const labelKey =
    typeKey === 'text-transform'
      ? 'textTransform'
      : typeKey === 'conditional-color'
        ? 'conditionalColor'
        : typeKey;
  return odakPackageSettingsFormatTypeLabelTr(labelKey);
}

function openFormatDialog(element: OdakHubListColumnConfig) {
  formatColumn.value = element;
  formatDialogOpen.value = true;
}

function onFormatSave(format: AfListColumnFormat | undefined) {
  if (!formatColumn.value) return;
  formatColumn.value.format = format;
  formatColumn.value = null;
}

function reorderColumns() {
  listConfig.value.columns.forEach((c, idx) => {
    c.order = idx + 1;
  });
}

async function save() {
  if (!canSave.value) return;
  reorderColumns();
  errorMessage.value = '';
  successMessage.value = '';
  try {
    await hubStore.saveScope(hubScope.value);
    successMessage.value = t('odakSiparis.packages.settings.saved');
  } catch (e: unknown) {
    errorMessage.value = panelError(e, 'errors.dg.generic');
  }
}

function resetDefaults() {
  hubStore.resetListScopeToDefaults(hubScope.value);
}
</script>

<template>
  <div>
    <v-alert type="info" variant="tonal" density="compact" class="mb-4">
      {{ t(hintKey) }}
    </v-alert>
    <v-alert v-if="errorMessage" type="error" variant="tonal" density="compact" class="mb-4">{{ errorMessage }}</v-alert>
    <v-alert v-if="successMessage" type="success" variant="tonal" density="compact" class="mb-4">{{ successMessage }}</v-alert>
    <v-progress-linear v-if="loading" indeterminate color="primary" class="mb-4" />

    <VueDraggableNext
      v-if="!loading && listConfig.columns.length"
      :list="listConfig.columns"
      item-key="fieldName"
      handle=".drag-handle"
      class="d-flex flex-column"
      @end="reorderColumns"
    >
      <v-card
        v-for="element in listConfig.columns"
        :key="element.fieldName"
        variant="outlined"
        class="mb-2 pa-3"
      >
        <div class="d-flex flex-wrap align-center ga-3">
          <v-icon class="drag-handle cursor-grab" icon="mdi-drag" />
          <div class="d-flex flex-column">
            <span class="font-weight-medium">{{ columnLabel(element.fieldName) }}</span>
            <span class="text-caption text-medium-emphasis">{{ formatSummary(element) }}</span>
          </div>
          <v-spacer />
          <v-btn
            variant="tonal"
            size="small"
            :color="isActiveListColumnFormat(element.format) ? 'primary' : undefined"
            :disabled="!canEdit"
            @click="openFormatDialog(element)"
          >
            <v-icon start size="18">mdi-format-paint</v-icon>
            {{ t('odakSiparis.packages.settings.listColumns.editFormat') }}
          </v-btn>
          <v-switch v-model="element.visible" :disabled="!canEdit" :label="t('odakSiparis.packages.settings.listColumns.visible')" hide-details density="compact" color="primary" />
          <v-switch v-model="element.sortable" :disabled="!canEdit" :label="t('odakSiparis.packages.settings.listColumns.sortable')" hide-details density="compact" />
          <v-switch
            v-if="scope === 'packages_list'"
            v-model="element.filterable"
            :disabled="!canEdit"
            :label="t('odakSiparis.packages.settings.listColumns.filterable')"
            hide-details
            density="compact"
          />
        </div>
      </v-card>
    </VueDraggableNext>

    <v-alert
      v-else-if="!loading && !listConfig.columns.length"
      type="warning"
      variant="tonal"
      density="compact"
      class="mb-4"
    >
      {{ t('odakSiparis.packages.settings.listColumns.empty') }}
    </v-alert>

    <OdakSiparisListColumnFormatDialog
      v-model="formatDialogOpen"
      :column="formatColumn"
      :column-label="formatColumn ? columnLabel(formatColumn.fieldName) : ''"
      :condition-fields="conditionFields"
      @save="onFormatSave"
    />

    <div class="d-flex ga-2 mt-4">
      <v-btn variant="tonal" :disabled="!canEdit" @click="resetDefaults">{{ t('odakSiparis.packages.settings.listColumns.reset') }}</v-btn>
      <v-spacer />
      <v-btn color="primary" variant="flat" :loading="saving" :disabled="!canSave" @click="save">{{ t('odakSiparis.packages.settings.save') }}</v-btn>
    </div>
  </div>
</template>

<style scoped>
.cursor-grab {
  cursor: grab;
}
</style>
